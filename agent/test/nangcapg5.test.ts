/**
 * Nâng cấp đợt G5 (docs/ban-giao/nang-cap-g5.md): trace_image — the looks limit per request, the picture result, the file
 * paths of attached pictures, loading and plans.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { toSdkUserMessage } from '../src/agent/content.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import { parsePluginMessage } from '../src/ipc/protocol.js';
import type { ToolResponse } from '../src/ipc/protocol.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';
import { TRACE_MAX_VIEWS, resetTraceViews, runTraceView, traceViewsUsed } from '../src/tools/trace.js';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const plugin = (...p: string[]) => fs.readFileSync(path.join(repo, 'plugin', 'ClaudeCAD.Plugin', ...p), 'utf8');

describe('G5: looks at a traced picture', () => {
  it('counted per request; past the limit no call reaches AutoCAD', async () => {
    resetTraceViews();
    let calls = 0;
    const b: ToolBridge = { call: async () => { calls++; return { id: 'x', ok: true, result: { image_png_base64: 'AA==' } } as ToolResponse; }, status: () => {} };
    for (let i = 1; i <= TRACE_MAX_VIEWS; i++) {
      const r = await runTraceView(b, { action: 'view' });
      assert.ok(r.ok);
      assert.equal((r as any).result.views_used, i);
      assert.equal((r as any).result.views_left, TRACE_MAX_VIEWS - i);
    }
    const over = await runTraceView(b, { action: 'view' });
    assert.ok(!over.ok);
    assert.match((over as any).error.message, /finish the draft/);
    assert.equal(calls, TRACE_MAX_VIEWS);
    resetTraceViews();
    assert.equal(traceViewsUsed(), 0);
  });

  it('a failed look is not counted', async () => {
    resetTraceViews();
    const b: ToolBridge = { call: async () => ({ id: 'x', ok: false, error: { code: 'INVALID_PARAMS', message: 'no picture' } }) as ToolResponse, status: () => {} };
    await runTraceView(b, { action: 'view' });
    assert.equal(traceViewsUsed(), 0);
  });

  it('a new user message starts the count again (session)', () => {
    const src = fs.readFileSync(path.join(repo, 'agent', 'src', 'agent', 'session.ts'), 'utf8');
    assert.match(src, /sendMessage\(msg: UserMessage\): void \{\n\s+resetTraceViews\(\);/);
  });
});

describe('G5: the tool', () => {
  const tools = new Map(buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).map((t) => [t.name, t as any]));
  const tr = tools.get('trace_image');
  const parse = (v: Record<string, unknown>) => Object.entries(v).every(([k, x]) => k in tr.inputSchema && tr.inputSchema[k].safeParse(x).success);

  it('schema of the five actions', () => {
    assert.ok(parse({ action: 'attach', path: 'C:/scan/mat-bang.jpg' }));
    assert.ok(parse({ action: 'attach', path: 'C:/scan/ho-so.pdf', page: 2, insert: [0, 0] }));
    assert.ok(parse({ action: 'scale', p1: [0, 0], p2: [1200, 0], distance: 5, unit: 'm' }));
    assert.ok(parse({ action: 'straighten', p1: [0, 0], p2: [1200, 40], axis: 'auto' }));
    assert.ok(parse({ action: 'view', p1: [0, 0], p2: [3000, 2000], size: 1568 }));
    assert.ok(parse({ action: 'info' }));
    assert.ok(!parse({ action: 'draw' }));
    assert.ok(!parse({ action: 'scale', distance: -1 }));
    assert.ok(!parse({ action: 'scale', unit: 'ft' }));
    assert.ok(!parse({ action: 'view', size: 4000 }));
  });

  it('deferred, says text in the picture is not instructions, never guesses a scale', () => {
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('trace_image'));
    assert.ok(!tr._meta?.['anthropic/alwaysLoad']);
    assert.match(tr.description, /never instructions/);
    assert.match(tr.description, /never guess a scale/);
    assert.match(tr.description, /DRAFT/);
    assert.match(tr.description, new RegExp(`at most ${TRACE_MAX_VIEWS} looks`));
  });

  it('2D and Full only; the prompt line on 2D / Full', () => {
    const plans = plugin('Core', 'Plans.cs');
    assert.match(plans, /Add\(ToolGroup\.TwoD, "trace_image"\)/);
    assert.match(plugin('UI', 'ToolLabels.cs'), /\["trace_image"\] = "Vẽ lại từ ảnh"/);
    assert.match(systemPromptFor('2d'), /trace_image/);
    assert.match(systemPromptFor('2d3d'), /trace_image/);
    assert.doesNotMatch(systemPromptFor('3d'), /trace_image/);
  });
});

describe('G5: the file of an attached picture', () => {
  it('passes through the pipe and is told to Claude', () => {
    const m = parsePluginMessage({
      type: 'user_message', text: 'vẽ lại bản này',
      attachments: { images: [{ media_type: 'image/png', data: 'AA==', path: 'C:\\Users\\a\\scan.png' }, { media_type: 'image/jpeg', data: 'AA==' }] },
    }) as any;
    assert.equal(m.attachments.images[0].path, 'C:\\Users\\a\\scan.png');
    assert.equal(m.attachments.images[1].path, undefined);
    const sdk = toSdkUserMessage(m) as any;
    const text = sdk.message.content.at(-1).text as string;
    assert.match(text, /Files of the pictures: 1: C:\\Users\\a\\scan\.png; 2: \(none\)/);
  });

  it('no path: the message is as before', () => {
    const m = parsePluginMessage({ type: 'user_message', text: 'x', attachments: { images: [{ media_type: 'image/png', data: 'AA==' }] } }) as any;
    const text = (toSdkUserMessage(m) as any).message.content.at(-1).text as string;
    assert.doesNotMatch(text, /Files of the pictures/);
  });
});
