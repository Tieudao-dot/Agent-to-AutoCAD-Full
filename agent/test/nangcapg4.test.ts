/**
 * Nâng cấp đợt G4 (docs/ban-giao/nang-cap-g4.md): named_view action render — the wait with progress, the way back (a
 * capture said as such), Dừng, the schema, loading and plans; set_material with the library materials.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';
import { RENDER_QUALITIES, runRender } from '../src/tools/render.js';
import type { ToolResponse } from '../src/ipc/protocol.js';

type Call = { tool: string; params: Record<string, unknown> };

function fakeBridge(statuses: Array<Record<string, unknown> | ToolResponse>, start: Record<string, unknown> = { render_started: true, file: 'C:/a.png', estimate_seconds: 60, max_wait_seconds: 300 }) {
  const calls: Call[] = [];
  const shown: string[] = [];
  let i = 0;
  const b: ToolBridge = {
    call: async (tool, params) => {
      calls.push({ tool, params });
      if (params.action === 'render') return { id: 'x', ok: true, result: start } as ToolResponse;
      if (params.action === 'render_stop') return { id: 'x', ok: true, result: { running: false, cancelled: true } } as ToolResponse;
      const s = statuses[Math.min(i++, statuses.length - 1)];
      return ('ok' in s ? s : { id: 'x', ok: true, result: s }) as ToolResponse;
    },
    status: (_t, _s, extra) => { if (extra?.summary) shown.push(extra.summary); },
  };
  return { b, calls, shown };
}

const fast = { pollMs: 0, sleep: async () => {} };

describe('G4: waiting for a render', () => {
  it('asks until the file is written and shows the progress', async () => {
    const { b, calls, shown } = fakeBridge([
      { running: true, progress: 'đang render 2 s / khoảng 60 s' },
      { running: true, progress: 'đang render 4 s / khoảng 60 s' },
      { running: false, method: 'render', file: 'C:/a.png' },
    ]);
    const r = await runRender(b, { action: 'render' }, fast);
    assert.ok(r.ok);
    const res = r.result as Record<string, unknown>;
    assert.equal(res.method, 'render');
    assert.equal(res.estimate_seconds, 60, 'the start answer joins the final one');
    assert.equal(JSON.parse(JSON.stringify(res)).render_started, undefined);
    assert.deepEqual(calls.map((c) => c.params.action), ['render', 'render_status', 'render_status', 'render_status']);
    assert.ok(shown[0].includes('khoảng 60 s'));
    assert.ok(shown.includes('đang render 4 s / khoảng 60 s'));
  });

  it('the way back: a capture comes back as such', async () => {
    const { b } = fakeBridge([{ running: false, method: 'capture', reason: 'the render took longer than the maximum wait and was stopped' }]);
    const r = await runRender(b, { action: 'render' }, fast);
    assert.equal(((r as any).result as Record<string, unknown>).method, 'capture');
  });

  it('a refused start (card, not 3D, a running command) is returned as it is, nothing waited', async () => {
    const calls: Call[] = [];
    const b: ToolBridge = { call: async (tool, params) => { calls.push({ tool, params }); return { id: 'x', ok: false, error: { code: 'INVALID_PARAMS', message: 'Confirmation required' } } as ToolResponse; }, status: () => {} };
    const r = await runRender(b, { action: 'render' }, fast);
    assert.ok(!r.ok);
    assert.equal(calls.length, 1);
  });

  it('Dừng: the aborted call stops the render in AutoCAD', async () => {
    const ac = new AbortController();
    const { b, calls } = fakeBridge([{ running: true, progress: 'x' }]);
    let n = 0;
    const r = await runRender(b, { action: 'render' }, { pollMs: 0, sleep: async () => { if (++n === 2) ac.abort(); }, signal: ac.signal });
    assert.equal(calls.at(-1)!.params.action, 'render_stop');
    assert.equal(((r as any).result as Record<string, unknown>).cancelled, true);
  });

  it('a status call failed by Stop also sends render_stop', async () => {
    const { b, calls } = fakeBridge([{ id: 'x', ok: false, error: { code: 'ACAD_ERROR', message: 'Cancelled by the user (Stop). Do not continue this task.' } } as ToolResponse]);
    const r = await runRender(b, { action: 'render' }, fast);
    assert.ok(!r.ok);
    assert.equal(calls.at(-1)!.params.action, 'render_stop');
  });

  it('never waits forever: a minute past the max wait it stops', async () => {
    let t = 0;
    const { b, calls } = fakeBridge([{ running: true, progress: 'x' }], { render_started: true, max_wait_seconds: 10, estimate_seconds: 5 });
    const r = await runRender(b, { action: 'render' }, { pollMs: 0, sleep: async () => { t += 30_000; }, now: () => t });
    assert.equal(calls.at(-1)!.params.action, 'render_stop');
    assert.match(String(((r as any).result as Record<string, unknown>).reason), /did not finish/);
  });
});

describe('G4: the tool', () => {
  const tools = new Map(buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).map((t) => [t.name, t as any]));
  const nv = tools.get('named_view');
  const parse = (v: Record<string, unknown>) => Object.entries(v).every(([k, x]) => k in nv.inputSchema && nv.inputSchema[k].safeParse(x).success);

  it('render is one more action of named_view (no new tool), deferred', () => {
    assert.ok(parse({ action: 'render', name: 'Mặt tiền', quality: 'high', width: 2560, height: 1440, background: 'sky', lighting: 'sun' }));
    assert.ok(!parse({ action: 'render_status' }), 'the inner actions are not offered to Claude');
    assert.ok(!parse({ action: 'render', quality: 'ultra' }));
    assert.ok(!parse({ action: 'render', width: 5000 }));
    assert.deepEqual([...RENDER_QUALITIES], ['low', 'medium', 'high']);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('named_view'));
    assert.ok(!nv._meta?.['anthropic/alwaysLoad']);
    assert.match(nv.description, /method capture/);
  });

  it('set_material takes library (default true) and says the source', () => {
    const sm = tools.get('set_material');
    assert.ok(sm.inputSchema.library.safeParse(false).success);
    assert.match(sm.description, /library/);
  });

  it('the render hint is in the 3D / Full prompts only', () => {
    assert.match(systemPromptFor('3d'), /action render/);
    assert.match(systemPromptFor('2d3d'), /action render/);
    assert.doesNotMatch(systemPromptFor('2d'), /action render/);
  });
});
