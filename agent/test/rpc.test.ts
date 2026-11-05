import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ToolCallMessage } from '../src/ipc/protocol.js';
import { ToolRpc } from '../src/ipc/rpc.js';

describe('ToolRpc', () => {
  it('correlates responses by id and generates sequential ids', async () => {
    const sent: ToolCallMessage[] = [];
    const rpc = new ToolRpc((m) => (sent.push(m), true));
    const p1 = rpc.call('draw_line', { start: [0, 0], end: [1000, 0] });
    const p2 = rpc.call('draw_circle', { center: [0, 0], radius: 500 });
    assert.deepEqual(sent.map((m) => m.id), ['req-1', 'req-2']);
    assert.equal(sent[0]!.type, 'tool_call');
    assert.equal(sent[0]!.tool, 'draw_line');

    // answer out of order
    assert.equal(rpc.handleResponse({ id: 'req-2', ok: true, result: { handles: ['B2'] } }), true);
    assert.equal(rpc.handleResponse({ id: 'req-1', ok: true, result: { handles: ['A1'] } }), true);
    assert.deepEqual(await p1, { id: 'req-1', ok: true, result: { handles: ['A1'] } });
    assert.deepEqual(await p2, { id: 'req-2', ok: true, result: { handles: ['B2'] } });
    assert.equal(rpc.pendingCount, 0);
  });

  it('times out with a TIMEOUT error instead of hanging', async () => {
    const rpc = new ToolRpc(() => true, 50);
    const t0 = Date.now();
    const r = await rpc.call('batch_execute', { calls: [] });
    assert.equal(r.ok, false);
    assert.equal(!r.ok && r.error.code, 'TIMEOUT');
    assert.match(!r.ok ? r.error.message : '', /did not answer within/);
    assert.ok(Date.now() - t0 >= 45);
    assert.equal(rpc.pendingCount, 0);
  });

  it('ignores a late response that arrives after the timeout', async () => {
    const rpc = new ToolRpc(() => true, 20);
    const r = await rpc.call('regen', {});
    assert.equal(r.ok, false);
    assert.equal(rpc.handleResponse({ id: 'req-1', ok: true, result: {} }), false);
  });

  it('fails immediately when the pipe is not connected', async () => {
    const rpc = new ToolRpc(() => false, 10_000);
    const r = await rpc.call('draw_line', {});
    assert.equal(!r.ok && r.error.code, 'ACAD_ERROR');
    assert.equal(rpc.pendingCount, 0);
  });

  it('failAll resolves every pending call (disconnect / cancel)', async () => {
    const rpc = new ToolRpc(() => true, 10_000);
    const calls = [rpc.call('a', {}), rpc.call('b', {}), rpc.call('c', {})];
    rpc.failAll('ACAD_ERROR', 'Connection to AutoCAD lost.');
    const results = await Promise.all(calls);
    assert.deepEqual(results.map((r) => (!r.ok ? r.error.message : '')), [
      'Connection to AutoCAD lost.',
      'Connection to AutoCAD lost.',
      'Connection to AutoCAD lost.',
    ]);
    assert.equal(rpc.pendingCount, 0);
  });

  it('passes plugin error envelopes through unchanged', async () => {
    const rpc = new ToolRpc(() => true);
    const p = rpc.call('draw_polyline', { layer: 'A-WALL' });
    rpc.handleResponse({ id: 'req-1', ok: false, error: { code: 'LAYER_NOT_FOUND', message: 'create it first' } });
    assert.deepEqual(await p, { id: 'req-1', ok: false, error: { code: 'LAYER_NOT_FOUND', message: 'create it first' } });
  });
});
