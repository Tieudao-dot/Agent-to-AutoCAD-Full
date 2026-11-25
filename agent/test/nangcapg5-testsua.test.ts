/**
 * Test & sửa đợt G5 (docs/ban-giao/nang-cap-g5-test.md): a look that shows no picture stops the tracing; the looks are
 * counted only through trace_image (batch_execute cannot run it: the plugin forbids it in a batch, noTransaction).
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ToolBridge } from '../src/tools/common.js';
import { buildTools } from '../src/tools/index.js';
import { TRACE_MAX_VIEWS, resetTraceViews, runTraceView, traceViewsUsed } from '../src/tools/trace.js';

describe('Test & sửa đợt G5: trace_image', () => {
  it('tells Claude to stop when a look shows no picture', () => {
    const t = buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).find((x) => x.name === 'trace_image')!;
    assert.match(t.description, /picture_visible false shows no picture: stop and tell the user/);
  });

  it('counts only good looks and refuses past the limit without calling the plugin', async () => {
    let calls = 0;
    let fail = true;
    const b: ToolBridge = {
      call: async () => { calls++; return fail ? ({ id: 'x', ok: false, error: { code: 'INVALID_PARAMS', message: 'no picture' } } as never) : ({ id: 'x', ok: true, result: {} } as never); },
      status: () => {},
    };
    resetTraceViews();
    await runTraceView(b, { action: 'view' });
    assert.equal(traceViewsUsed(), 0); // a failed look is not counted
    fail = false;
    for (let i = 0; i < TRACE_MAX_VIEWS; i++) await runTraceView(b, { action: 'view' });
    const before = calls;
    const r = await runTraceView(b, { action: 'view' });
    assert.equal(r.ok, false);
    assert.equal(calls, before);
    resetTraceViews();
    assert.equal(traceViewsUsed(), 0);
  });
});
