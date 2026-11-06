/**
 * v0.6 palette features on the sidecar side: capture_view image results, plan /
 * next-step / skill-draft tools, the new message context (read-only, project notes,
 * switches) and the SDK's predicted next prompt.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import type { SDKMessage } from '@anthropic-ai/claude-agent-sdk';
import { contextPreamble } from '../src/agent/content.js';
import { AgentSession } from '../src/agent/session.js';
import { SYSTEM_PROMPT } from '../src/agent/systemPrompt.js';
import { parsePluginMessage, type SidecarToPlugin } from '../src/ipc/protocol.js';
import { captureTools, toCaptureResult } from '../src/tools/capture.js';
import { LOCAL_TOOL_NAMES, interactionTools, skillMarkdown } from '../src/tools/interaction.js';

describe('capture_view', () => {
  it('turns the plugin result into an image block plus metadata', () => {
    const r = toCaptureResult({ id: '1', ok: true, result: { image_png_base64: 'QUJD', width: 800, height: 600, region: 'handles', mm_per_pixel: 12.5 } });
    assert.equal(r.content.length, 2);
    assert.deepEqual(r.content[0], { type: 'image', data: 'QUJD', mimeType: 'image/png' });
    const meta = JSON.parse((r.content[1] as { text: string }).text);
    assert.equal(meta.width, 800);
    assert.equal(meta.image_png_base64, undefined, 'the base64 must not be repeated as text');
  });

  it('reports plugin errors and empty images as errors', () => {
    const err = toCaptureResult({ id: '1', ok: false, error: { code: 'ACAD_ERROR', message: 'no graphics' } });
    assert.equal(err.isError, true);
    const empty = toCaptureResult({ id: '1', ok: true, result: { width: 1 } });
    assert.equal(empty.isError, true);
  });

  it('forwards only the parameters Claude set and logs a summary', async () => {
    const calls: Array<{ tool: string; params: Record<string, unknown> }> = [];
    const statuses: string[] = [];
    const [t] = captureTools({
      call: async (tool, params) => {
        calls.push({ tool, params });
        return { id: 'x', ok: true, result: { image_png_base64: 'QUJD', width: 1024, height: 512 } };
      },
      status: (tool, state, extra) => statuses.push(`${tool}:${state}:${extra?.summary ?? ''}`),
    });
    const r = await t!.handler({ handles: ['2F4A'], size: 1024 }, {});
    assert.deepEqual(calls, [{ tool: 'capture_view', params: { handles: ['2F4A'], size: 1024 } }]);
    assert.equal((r.content[0] as { type: string }).type, 'image');
    assert.deepEqual(statuses, ['capture_view:running:', 'capture_view:done:ảnh 1024×512']);
  });
});

describe('plan / next-step / skill-draft tools', () => {
  const emitted: SidecarToPlugin[] = [];
  const tools = Object.fromEntries(interactionTools((m) => emitted.push(m)).map((t) => [t.name, t]));

  it('are all local tools', () => {
    for (const n of Object.keys(tools)) assert.ok(LOCAL_TOOL_NAMES.includes(n), n);
  });

  it('propose_plan shows a plan card and asks Claude to end the turn', async () => {
    const r = await tools.propose_plan!.handler({ summary: 'Vẽ mặt bằng tầng 1', steps: ['Lưới trục 5x4', 'Tường 220'] }, {});
    const m = emitted.at(-1) as Extract<SidecarToPlugin, { type: 'plan_request' }>;
    assert.equal(m.type, 'plan_request');
    assert.deepEqual(m.steps, ['Lưới trục 5x4', 'Tường 220']);
    assert.match(m.id, /^plan-\d+$/);
    assert.match((r.content[0] as { text: string }).text, /End your turn/);
  });

  it('suggest_next_steps emits tool suggestions', async () => {
    await tools.suggest_next_steps!.handler({ items: ['Ghi kích thước các phòng', 'Thêm tên phòng'] }, {});
    assert.deepEqual(emitted.at(-1), { type: 'suggestions', items: ['Ghi kích thước các phòng', 'Thêm tên phòng'], source: 'tool' });
  });

  it('propose_skill sends a complete SKILL.md', async () => {
    await tools.propose_skill!.handler({
      name: 'chuan-cong-ty-x',
      description: 'Chuẩn bản vẽ kiến trúc của công ty X: layer, chữ, kích thước.',
      body: '# Chuẩn công ty X\n\n| Layer | Màu |\n|---|---|\n| A-WALL | 7 |',
    }, {});
    const m = emitted.at(-1) as Extract<SidecarToPlugin, { type: 'skill_draft' }>;
    assert.equal(m.type, 'skill_draft');
    assert.match(m.content, /^---\nname: chuan-cong-ty-x\ndescription: "Chuẩn bản vẽ kiến trúc/);
    assert.match(m.content, /\| A-WALL \| 7 \|/);
  });

  it('skillMarkdown drops a frontmatter repeated in the body', () => {
    const md = skillMarkdown('a-b', 'mô tả đủ dài cho skill này nhé', '---\nname: x\ndescription: y\n---\nNội dung');
    assert.equal(md, '---\nname: a-b\ndescription: "mô tả đủ dài cho skill này nhé"\n---\n\nNội dung\n');
    // a colon or quotes in the description stay inside one quoted YAML string
    assert.match(skillMarkdown('a-b', 'Chuẩn công ty X: layer "A-*", #ghi chú', 'x'), /^---\nname: a-b\ndescription: "Chuẩn công ty X: layer 'A-\*', #ghi chú"\n---/);
  });
});

describe('message context v0.6', () => {
  it('parses read-only, notes, switches and plan approval', () => {
    const m = parsePluginMessage({
      type: 'user_message',
      text: 'Làm theo kế hoạch.',
      plan: 'approved',
      context: { read_only: true, notes: '  tường bao 220  ', visual_check: false, plan_first: false, suggestions: false, junk: 1 },
    });
    assert.deepEqual(m, {
      type: 'user_message',
      text: 'Làm theo kế hoạch.',
      context: { read_only: true, notes: 'tường bao 220', visual_check: false, plan_first: false, suggestions: false },
      plan: 'approved',
    });
  });

  it('ignores defaults (true switches, empty notes)', () => {
    const m = parsePluginMessage({ type: 'user_message', text: 'x', context: { visual_check: true, notes: '   ', read_only: false } });
    assert.deepEqual(m, { type: 'user_message', text: 'x' });
  });

  it('puts the new context in the preamble', () => {
    const all = contextPreamble({
      type: 'user_message',
      text: 'x',
      plan: 'approved',
      context: { read_only: true, notes: 'cao độ ±0.000 = +3.200', visual_check: false, plan_first: false, suggestions: false },
    }).join('\n');
    assert.match(all, /CHỈ XEM/);
    assert.match(all, /Ghi chú dự án[^\n]*\ncao độ ±0\.000 = \+3\.200/);
    assert.match(all, /capture_view only when asked/);
    assert.match(all, /do not call propose_plan/);
    assert.match(all, /do not call suggest_next_steps/);
    assert.match(all, /bấm "Làm"/);
  });

  it('the system prompt explains the new tools', () => {
    for (const t of ['capture_view', 'propose_plan', 'suggest_next_steps', 'propose_skill', 'CHỈ XEM', 'Ghi chú dự án']) assert.ok(SYSTEM_PROMPT.includes(t), t);
  });
});

describe('predicted next prompt', () => {
  function session() {
    const events: SidecarToPlugin[] = [];
    const s = new AgentSession({
      emit: (m) => events.push(m),
      bridge: { call: async (id) => ({ id, ok: true, result: {} }), status: () => {} },
      version: 'test',
      workDir: fs.mkdtempSync(path.join(os.tmpdir(), 'claudecad-v06-')),
      log: () => {},
    });
    return { s, events };
  }

  it('is enabled in the query options', () => {
    const { s } = session();
    assert.equal(s.buildOptions().promptSuggestions, true);
  });

  it('becomes an sdk suggestion for the palette', () => {
    const { s, events } = session();
    s.onMessage({ type: 'prompt_suggestion', suggestion: '  Ghi kích thước  ', uuid: 'u', session_id: 's' } as unknown as SDKMessage);
    s.onMessage({ type: 'prompt_suggestion', suggestion: '   ', uuid: 'u', session_id: 's' } as unknown as SDKMessage);
    assert.deepEqual(events, [{ type: 'suggestions', items: ['Ghi kích thước'], source: 'sdk' }]);
  });
});

describe('manual edits (v0.7)', () => {
  it('parses manual_changes and drops bad handles', () => {
    const m = parsePluginMessage({
      type: 'user_message',
      text: 'dời cửa sang trái 500',
      context: { manual_changes: { text: 'sửa 2 (A-WALL 2); xóa 1 (A-DOOR 1)', since: 'previous', modified: ['2F4A', 'xx;drop', '2F4B'], erased: ['30'], more: 0 } },
    });
    assert.deepEqual((m as { context?: unknown }).context, {
      manual_changes: { text: 'sửa 2 (A-WALL 2); xóa 1 (A-DOOR 1)', since: 'previous', modified: ['2F4A', '2F4B'], erased: ['30'] },
    });
    const none = parsePluginMessage({ type: 'user_message', text: 'x', context: { manual_changes: { text: '  ' } } });
    assert.deepEqual(none, { type: 'user_message', text: 'x' });
  });

  it('tells Claude to read the edited objects again', () => {
    const all = contextPreamble({
      type: 'user_message',
      text: 'x',
      context: { manual_changes: { text: 'sửa 1 (A-WALL 1)', since: 'session', modified: ['2F4A'], added: ['31'], more: 5 } },
    }).join('\n');
    assert.match(all, /sửa bản vẽ bằng tay ngoài khung chat since your previous turn: sửa 1 \(A-WALL 1\)/);
    assert.match(all, /sửa: 2F4A; thêm: 31 \(\+5 nữa\)/);
    assert.match(all, /read them again/);
    assert.ok(SYSTEM_PROMPT.includes('Manual edits between turns'));
  });
});

describe('layer scope', () => {
  it('parses picked layers and the selection breakdown', () => {
    const m = parsePluginMessage({
      type: 'user_message',
      text: 'đổi màu sang đỏ',
      attachments: {
        selection: { handles: ['1A', '1B'], count: 2, layers: { 'A-WALL': 2, bad: 'x' } },
        layers: { names: ['A-WALL', '', 'A-DOOR', 3], counts: { 'A-WALL': 120, 'A-DOOR': 8 }, total: 128 },
      },
    }) as { attachments?: unknown };
    assert.deepEqual(m.attachments, {
      selection: { handles: ['1A', '1B'], count: 2, layers: { 'A-WALL': 2 } },
      layers: { names: ['A-WALL', 'A-DOOR'], counts: { 'A-WALL': 120, 'A-DOOR': 8 }, total: 128 },
    });
    const onlyLayers = parsePluginMessage({ type: 'user_message', text: 'khóa lại', attachments: { layers: { names: ['A-TEMP'] } } }) as { attachments?: unknown };
    assert.deepEqual(onlyLayers.attachments, { layers: { names: ['A-TEMP'] } });
  });

  it('explains layers vs objects to Claude', () => {
    const all = contextPreamble({
      type: 'user_message',
      text: 'đổi màu sang đỏ',
      attachments: {
        selection: { handles: ['1A'], count: 1, layers: { 'A-WALL': 1 } },
        layers: { names: ['A-WALL', 'A-DOOR'], counts: { 'A-WALL': 120, 'A-DOOR': 8 }, total: 128 },
      },
    }).join('\n');
    assert.match(all, /theo layer: A-WALL 1/);
    assert.match(all, /Layer người dùng chọn: A-WALL \(120 đối tượng\), A-DOOR \(8 đối tượng\) — tổng 128 đối tượng/);
    assert.match(all, /set_layer_props on each layer, no confirmation needed/);
    assert.match(all, /request_confirmation/);
    assert.ok(SYSTEM_PROMPT.includes('Changing the properties of layers the user chose'));
  });
});

describe('turn id', () => {
  it('is parsed and echoed in turn_end', async () => {
    const m = parsePluginMessage({ type: 'user_message', text: 'x', turn_id: 't-42' });
    assert.deepEqual(m, { type: 'user_message', text: 'x', turn_id: 't-42' });
  });
});
