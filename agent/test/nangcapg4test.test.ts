/**
 * Test & sửa đợt G4 (docs/ban-giao/nang-cap-g4-test.md): a status call that times out while AutoCAD renders in the
 * foreground is not the end of the render; the wait stays bounded.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ToolResponse } from '../src/ipc/protocol.js';
import type { ToolBridge } from '../src/tools/common.js';
import '../src/tools/index.js'; // load order as in the assistant (render.ts and solids.ts import each other)
import { runRender } from '../src/tools/render.js';

function bridge(statuses: ToolResponse[], start = { render_started: true, estimate_seconds: 60, max_wait_seconds: 300 }) {
  const calls: string[] = [];
  const shown: string[] = [];
  let i = 0;
  const b: ToolBridge = {
    call: async (_t, p) => {
      calls.push(String(p.action));
      if (p.action === 'render') return { id: 'x', ok: true, result: start } as ToolResponse;
      if (p.action === 'render_stop') return { id: 'x', ok: true, result: { running: false, cancelled: true } } as ToolResponse;
      return statuses[Math.min(i++, statuses.length - 1)]!;
    },
    status: (_t, _s, extra) => { if (extra?.summary) shown.push(extra.summary); },
  };
  return { b, calls, shown };
}
const timeout = { id: 'x', ok: false, error: { code: 'TIMEOUT', message: 'named_view did not answer within 30 s.' } } as ToolResponse;

describe('Test & sửa G4: AutoCAD busy rendering', () => {
  it('a timed-out status is asked again, and the render answer still arrives', async () => {
    const { b, calls, shown } = bridge([timeout, timeout, { id: 'x', ok: true, result: { running: false, method: 'render', file: 'C:/a.png' } } as ToolResponse]);
    const r = await runRender(b, { action: 'render' }, { pollMs: 0, sleep: async () => {} });
    assert.ok(r.ok);
    assert.equal((r.result as Record<string, unknown>).method, 'render');
    assert.deepEqual(calls, ['render', 'render_status', 'render_status', 'render_status']);
    assert.ok(shown.some((s) => s.includes('bận render')));
  });

  it('but not forever: timeouts past the max wait stop the render, never a "render" claim', async () => {
    let t = 0;
    const { b, calls } = bridge([timeout], { render_started: true, estimate_seconds: 5, max_wait_seconds: 10 });
    const r = await runRender(b, { action: 'render' }, { pollMs: 0, sleep: async () => { t += 30_000; }, now: () => t });
    assert.equal(calls.at(-1), 'render_stop');
    const res = (r as any).result as Record<string, unknown>;
    assert.notEqual(res.method, 'render');
    assert.match(String(res.reason), /did not answer/);
  });
});
