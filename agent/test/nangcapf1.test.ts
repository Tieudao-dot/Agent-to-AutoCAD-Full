/**
 * Nâng cấp đợt F1 (25/11/2026, docs/ban-giao/nang-cap-f1.md): (1) coordinates in the current UCS, (2) the work mode
 * "Cả hai / 2D / 3D" of a Full machine — only that group's tools and rules, and "switch the mode", never "buy a plan".
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { contextPreamble } from '../src/agent/content.js';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import { SYSTEM_PROMPT, THREE_D_RULES, TWO_D_HINTS, systemPromptFor } from '../src/agent/systemPrompt.js';
import { parsePluginMessage } from '../src/ipc/protocol.js';
import type { ToolBridge } from '../src/tools/common.js';
import { point } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };

describe('F1: coordinates follow the UCS', () => {
  it('the prompt says points are in the current UCS, World = WCS, and set_ucs changes the system', () => {
    const p = systemPromptFor('2d3d');
    assert.match(p, /current UCS, like AutoCAD's own commands \(World UCS = WCS\)/);
    assert.match(p, /results then carry "coordinates"/);
    assert.match(THREE_D_RULES, /set_ucs \/ reset_ucs change the system every later tool works in/);
    assert.match(THREE_D_RULES, /never convert between systems yourself/);
    assert.doesNotMatch(SYSTEM_PROMPT, /never convert points to the UCS/);
    assert.match(point.description ?? '', /current UCS/);
  });

  it('set_ucs / reset_ucs say their own axes are WCS and later tools use the UCS', () => {
    const tools = buildTools(bridge);
    const set = tools.find((t) => t.name === 'set_ucs')!;
    const reset = tools.find((t) => t.name === 'reset_ucs')!;
    assert.match(set.description, /origin \/ x_axis \/ y_axis are WCS/);
    assert.match(set.description, /Every later tool takes and returns points in the new UCS/);
    assert.match(reset.description, /later tools take and return WCS points again/);
  });
});

describe('F1: work mode of a Full machine', () => {
  it('the plugin sends work_mode only as the narrowing of the plan it sends', () => {
    const ok = parsePluginMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d', plan_tools: ['draw_line'], work_mode: '2d' } }) as any;
    assert.equal(ok.context.work_mode, '2d');
    // a mode that is not the plan, an unknown mode, a mode without a plan: ignored
    for (const context of [
      { service_plan: '2d', work_mode: '3d' },
      { service_plan: '2d3d', work_mode: '2d' },
      { service_plan: '3d', work_mode: 'both' },
      { work_mode: '3d' },
    ]) {
      const m = parsePluginMessage({ type: 'user_message', text: 'x', context }) as any;
      assert.equal(m.context?.work_mode, undefined, JSON.stringify(context));
    }
  });

  it('2D mode: the 2D rules only, a mode section and no plan / upgrade talk', () => {
    const p = systemPromptFor('2d', '2d');
    assert.ok(p.includes(TWO_D_HINTS));
    assert.ok(!p.includes('# 3D modelling'));
    assert.match(p, /# Work mode: 2D \(this machine's plan is Full\)/);
    assert.match(p, /Đang ở chế độ 2D, đổi sang 3D hoặc Cả hai/);
    assert.match(p, /never speak of plans, buying, upgrading or the plugin owner/);
    assert.doesNotMatch(p, /Máy này dùng gói 2D/);
    assert.doesNotMatch(p, /# This machine's service plan/);
  });

  it('3D mode: the 3D rules only and the switch back to 2D', () => {
    const p = systemPromptFor('3d', '3d');
    assert.ok(p.includes(THREE_D_RULES));
    assert.ok(!p.includes(TWO_D_HINTS));
    assert.match(p, /Đang ở chế độ 3D, đổi sang 2D hoặc Cả hai/);
    assert.doesNotMatch(p, /Máy này dùng gói 3D/);
  });

  it('a real 2D / 3D plan is unchanged (no mode)', () => {
    assert.equal(systemPromptFor('2d', undefined), systemPromptFor('2d'));
    assert.match(systemPromptFor('3d'), /Máy này dùng gói 3D/);
    assert.doesNotMatch(systemPromptFor('3d'), /Work mode/);
  });

  it('the line in every message names the mode, not the plan', () => {
    const withMode = contextPreamble({ type: 'user_message', text: 'vẽ hộp', context: { service_plan: '2d', work_mode: '2d' } } as never);
    const text = withMode.join('\n');
    assert.match(text, /Chế độ người dùng chọn: 2D \(máy gói Full\)/);
    assert.match(text, /never to buy a plan/);
    assert.doesNotMatch(text, /Gói của máy này/);
    const plan = contextPreamble({ type: 'user_message', text: 'vẽ', context: { service_plan: '2d' } } as never).join('\n');
    assert.match(plan, /Gói của máy này: 2D/);
  });

  it('switching the mode restarts Claude with the same conversation and the new tools; same mode: no restart', async () => {
    const starts: any[] = [];
    const init = { type: 'system', subtype: 'init', session_id: 's1', model: 'claude-opus-5-5', apiKeySource: 'oauth', tools: [], skills: [] } as unknown as SDKMessage;
    const result = { type: 'result', subtype: 'success', is_error: false, session_id: 's1' } as unknown as SDKMessage;
    const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
      starts.push(params.options);
      const gen = (async function* () {
        for await (const _m of params.prompt) {
          yield init;
          yield result;
        }
      })();
      return Object.assign(gen, { interrupt: async () => {}, close: () => {} });
    }) as unknown as QueryFn;
    const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-f1-'));
    const s = new AgentSession({ emit: () => {}, bridge, version: 'test', workDir, log: () => {}, onCancel: () => {}, queryFn: fn });
    const tick = () => new Promise((r) => setTimeout(r, 20));
    const sent = (context: Record<string, unknown>) => s.sendMessage({ type: 'user_message', text: 'vẽ', context } as never);

    sent({ service_plan: '2d3d' });
    await tick();
    assert.equal(starts.length, 1);
    sent({ service_plan: '2d', plan_tools: ['draw_line', 'ping'], work_mode: '2d' });
    await tick();
    assert.equal(starts.length, 2);
    assert.equal(starts[1].resume, 's1');
    assert.match(starts[1].systemPrompt, /# Work mode: 2D/);
    sent({ service_plan: '2d', plan_tools: ['ping', 'draw_line'], work_mode: '2d' });
    await tick();
    assert.equal(starts.length, 2);
    // a real 2D plan with the same tools is not the 2D mode: the prompt differs, so Claude restarts
    sent({ service_plan: '2d', plan_tools: ['ping', 'draw_line'] });
    await tick();
    assert.equal(starts.length, 3);
    assert.doesNotMatch(starts[2].systemPrompt, /Work mode/);
  });

  it('the modes cut about a third of the tool descriptions a chat opens with', () => {
    const size = (tools: ReturnType<typeof buildTools>) => tools.reduce((n, t) => n + t.name.length + t.description.length, 0);
    const full = size(buildTools(bridge, undefined, '2d3d'));
    const names = buildTools(bridge).map((t) => t.name);
    const twoD = size(buildTools(bridge, names.filter((n) => !/solid|3d|extrude|revolve|sweep|loft|ucs|interference|section_view|projection_view|build_|presspull|stl|volume_schedule|visual_style/.test(n)), '2d'));
    assert.ok(twoD < full * 0.8, `2D ${twoD} vs Full ${full}`);
  });
});
