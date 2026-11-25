/**
 * Nâng cấp đợt F5 (docs/ban-giao/nang-cap-f5.md): ⚙ "Nạp gọn lệnh" sent with every message / prewarm (the F4 switch-off
 * still wins), the overwrite cards (confirm_handles → user_confirmed only for handles on the approved card), the
 * multileader of the standard.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKMessage, SDKUserMessage } from '@anthropic-ai/claude-agent-sdk';
import { F5_CARD_TOOLS, USER_CONFIRMED_PARAM, applyUserConfirmation, refusedForConfirmation } from '../src/agent/confirmation.js';
import { AgentSession, type QueryFn } from '../src/agent/session.js';
import { TOOL_SEARCH_HINT, systemPromptFor } from '../src/agent/systemPrompt.js';
import { parsePluginMessage } from '../src/ipc/protocol.js';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';

const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
const tick = (ms = 20) => new Promise((r) => setTimeout(r, ms));

function fakeClaude() {
  const starts: any[] = [];
  const fn = ((params: { prompt: AsyncIterable<SDKUserMessage>; options: any }) => {
    starts.push(params.options);
    let stop!: () => void;
    const stopped = new Promise<void>((r) => (stop = r));
    const gen = (async function* () {
      const it = params.prompt[Symbol.asyncIterator]();
      while (true) {
        const next = await Promise.race([it.next(), stopped.then(() => ({ done: true, value: undefined }))]);
        if (next.done) return;
        yield { type: 'system', subtype: 'init', session_id: 's1', model: 'm', apiKeySource: 'oauth', tools: [], skills: [] } as unknown as SDKMessage;
        yield { type: 'result', subtype: 'success', is_error: false, session_id: 's1' } as unknown as SDKMessage;
      }
    })();
    return Object.assign(gen, { interrupt: async () => {}, close: () => stop() });
  }) as unknown as QueryFn;
  return { fn, starts };
}

function session(fn: QueryFn, deferTools?: boolean) {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-f5-'));
  return new AgentSession({ emit: () => {}, bridge, version: 'test', workDir, log: () => {}, onCancel: () => {}, queryFn: fn, ...(deferTools === undefined ? {} : { deferTools }) });
}

const msg = (text: string, context: Record<string, unknown>) => ({ type: 'user_message', text, context }) as never;

describe('F5: ⚙ Nạp gọn lệnh', () => {
  it('the palette sends defer_tools: false only when it is off', () => {
    const off = parsePluginMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d3d', defer_tools: false } }) as any;
    assert.equal(off.context.defer_tools, false);
    const on = parsePluginMessage({ type: 'user_message', text: 'x', context: { service_plan: '2d3d', defer_tools: true } }) as any;
    assert.equal(on.context.defer_tools, undefined);
    const pre = parsePluginMessage({ type: 'prewarm', context: { service_plan: '2d3d', defer_tools: false } }) as any;
    assert.equal(pre.context.defer_tools, false);
  });

  it('off from the palette: the next message restarts Claude (resumed) with every tool in full; on again: back', async () => {
    const c = fakeClaude();
    const s = session(c.fn);
    s.sendMessage(msg('a', { service_plan: '2d3d' }));
    await tick();
    assert.deepEqual(c.starts[0].tools, ['Read', 'Skill', 'ToolSearch']);
    s.sendMessage(msg('b', { service_plan: '2d3d', defer_tools: false }));
    await tick();
    assert.equal(c.starts.length, 2);
    assert.deepEqual(c.starts[1].tools, ['Read', 'Skill']);
    assert.equal(c.starts[1].env.ENABLE_TOOL_SEARCH, 'false');
    assert.ok(!String(c.starts[1].systemPrompt).includes(TOOL_SEARCH_HINT));
    assert.equal(c.starts[1].resume, 's1', 'the same chat goes on');
    s.sendMessage(msg('c', { service_plan: '2d3d', defer_tools: false }));
    await tick();
    assert.equal(c.starts.length, 2, 'no restart while it stays off');
    s.sendMessage(msg('d', { service_plan: '2d3d' }));
    await tick();
    assert.equal(c.starts.length, 3);
    assert.deepEqual(c.starts[2].tools, ['Read', 'Skill', 'ToolSearch']);
    s.close();
  });

  it('the environment / file switch-off wins over the palette, and the palette never restarts it', async () => {
    const c = fakeClaude();
    const s = session(c.fn, false);
    s.sendMessage(msg('a', { service_plan: '2d3d' }));
    await tick();
    s.sendMessage(msg('b', { service_plan: '2d3d', defer_tools: false }));
    await tick();
    assert.equal(c.starts.length, 1);
    assert.deepEqual(c.starts[0].tools, ['Read', 'Skill']);
    s.close();
  });

  it('a prewarm with the other setting restarts the waiting process with it', async () => {
    const c = fakeClaude();
    const s = session(c.fn);
    s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d' } });
    await tick();
    s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d', defer_tools: false } });
    await tick();
    assert.equal(c.starts.length, 2);
    assert.deepEqual(c.starts[1].tools, ['Read', 'Skill']);
    s.prewarm({ type: 'prewarm', context: { service_plan: '2d3d', defer_tools: false } });
    await tick();
    assert.equal(c.starts.length, 2, 'the same setting: nothing restarted');
    // the hint goes out of the mode prompts too
    assert.ok(!systemPromptFor('2d', '2d', false).includes(TOOL_SEARCH_HINT));
    s.close();
  });
});

describe('F5: overwrite cards', () => {
  const g = { userText: 'đồng ý', answersQuestion: true, cardConfirmed: new Set(['ABCDEF0123456789', '2A']) } as never;

  it('only handles on the approved card reach the plugin as user_confirmed; confirm_handles never does', () => {
    for (const tool of F5_CARD_TOOLS) {
      const out = applyUserConfirmation(tool, { output: 'x', confirm_handles: ['abcdef0123456789', 'FFFF'] }, g);
      assert.deepEqual(out[USER_CONFIRMED_PARAM], ['ABCDEF0123456789'], tool);
      assert.ok(!('confirm_handles' in out), tool);
      // Claude cannot set user_confirmed itself
      const forged = applyUserConfirmation(tool, { [USER_CONFIRMED_PARAM]: ['FFFF'] }, { userText: 'x', answersQuestion: false } as never);
      assert.ok(!(USER_CONFIRMED_PARAM in forged), tool);
    }
    const b = applyUserConfirmation('batch_execute', { calls: [{ tool: 'named_view', params: { action: 'delete', name: 'A', confirm_handles: ['2a'] } }] }, g) as any;
    assert.deepEqual(b.calls[0].params[USER_CONFIRMED_PARAM], ['2A']);
  });

  it('the plugin refusal makes the turn a question (the card), like the other overwrites', () => {
    assert.ok(refusedForConfirmation({ ok: false, error: { message: 'Confirmation required: export_stl would overwrite the existing file' } }));
  });

  it('every tool with a card has confirm_handles; the old overwrite / replace stay for skills and macros', () => {
    const tools = new Map(buildTools(bridge).map((t) => [t.name, t.inputSchema as Record<string, unknown>]));
    for (const t of F5_CARD_TOOLS) assert.ok('confirm_handles' in tools.get(t)!, t);
    assert.ok('overwrite' in tools.get('export_stl')!);
    assert.ok('overwrite' in tools.get('block_schedule')!);
    assert.ok('replace' in tools.get('named_view')!);
  });

  it('the descriptions and the prompt say the card, not "overwrite: true"', () => {
    const d = new Map(buildTools(bridge).map((t) => [t.name, t.description]));
    for (const t of ['plot_pdf', 'export_stl', 'named_view', 'set_material']) assert.match(d.get(t)!, /card/, t);
    assert.doesNotMatch(systemPromptFor('2d3d'), /then overwrite: true/);
  });
});

describe('F5: multileader of the standard', () => {
  it('setup_standards makes it, add_mleader uses it without a style', () => {
    const d = new Map(buildTools(bridge).map((t) => [t.name, t.description]));
    assert.match(d.get('setup_standards')!, /multileader style VN-ML-<scale>/);
    assert.match(d.get('add_mleader')!, /current multileader style \(after setup_standards/);
    const skill = fs.readFileSync(path.join(path.resolve(path.dirname(new URL(import.meta.url).pathname), '..', '..', '..'), 'skills', 've-kien-truc', 'SKILL.md'), 'utf8');
    assert.match(skill, /KT-ML-100/);
  });
});
