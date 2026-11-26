/**
 * Nâng cấp đợt H5 (docs/ban-giao/nang-cap-h5.md): batch_files — plan without the card, one plugin call per file after it,
 * Dừng between files, one failing file does not stop the run, plan / mode errors stop it, the summary.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS } from '../src/agent/confirmation.js';
import { systemPromptFor } from '../src/agent/systemPrompt.js';
import type { ToolResponse } from '../src/ipc/protocol.js';
import { runBatch, stopsRun } from '../src/tools/batchFiles.js';
import type { ToolBridge } from '../src/tools/common.js';
import { ALWAYS_LOADED_TOOLS, buildTools } from '../src/tools/index.js';

function fakeBridge(count: number, fail: (i: number) => ToolResponse | null = () => null) {
  const calls: Array<Record<string, unknown>> = [];
  const b: ToolBridge = {
    status: () => {},
    call: async (_tool: string, params: Record<string, unknown>) => {
      calls.push(params);
      if (params.action === 'plan') return { id: 'p', ok: true, result: { count, stamp: '20261126-100000', confirm_token: 'ABCDEF0123456789' } } as ToolResponse;
      if (params.action === 'run') {
        const i = params.index as number;
        return fail(i) ?? ({ id: 'r', ok: true, result: { file: `f${i}.dwg`, status: 'ok', note: 'x' } } as ToolResponse);
      }
      return { id: 'f', ok: true, result: { ok: (params.rows as unknown[]).length, excel: params.excel ? 'C:/x.xlsx' : undefined } } as ToolResponse;
    },
  };
  return { b, calls };
}

describe('H5: batch_files', () => {
  it('is deferred, a card tool, one prompt line on 2D / Full, none on 3D', () => {
    const t = buildTools({ call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} }).find((x) => x.name === 'batch_files') as any;
    assert.ok(t);
    assert.ok(!ALWAYS_LOADED_TOOLS.includes('batch_files'));
    assert.ok(F5_CARD_TOOLS.has('batch_files'));
    assert.ok(t.inputSchema.task.safeParse('standards').success);
    assert.ok(!t.inputSchema.task.safeParse('erase').success);
    for (const p of ['2d', '2d3d'] as const) assert.match(systemPromptFor(p), /batch_files/);
    assert.ok(!systemPromptFor('3d').includes('batch_files'));
  });

  it('without the card: only the plan, no file is touched', async () => {
    const { b, calls } = fakeBridge(3);
    const r = await runBatch(b, { task: 'check', folder: 'C:/a' });
    assert.ok(r.ok);
    assert.deepEqual(calls.map((c) => c.action), ['plan']);
  });

  it('with the card: one call per file with the stamp of the plan, then the summary', async () => {
    const { b, calls } = fakeBridge(3);
    const r = await runBatch(b, { task: 'standards', folder: 'C:/a', user_confirmed: ['ABCDEF0123456789'], excel: true });
    assert.ok(r.ok);
    assert.deepEqual(calls.map((c) => c.action), ['plan', 'run', 'run', 'run', 'finish']);
    assert.deepEqual(calls.filter((c) => c.action === 'run').map((c) => c.index), [0, 1, 2]);
    assert.ok(calls.filter((c) => c.action === 'run').every((c) => c.stamp === '20261126-100000' && c.excel === undefined));
    assert.equal(calls[4].excel, true);
    assert.equal((r as any).result.done, 3);
  });

  it('a failing file is a row, the run goes on; a plan / Chỉ xem / card error stops it', async () => {
    const one = fakeBridge(3, (i) => (i === 1 ? ({ id: 'e', ok: false, error: { code: 'ACAD_ERROR', message: 'eBadDwgHeader' } } as ToolResponse) : null));
    const r = await runBatch(one.b, { task: 'check', folder: 'C:/a', user_confirmed: ['T'] });
    assert.equal((r as any).result.files.length, 3);
    assert.equal((r as any).result.files[1].status, 'error');
    const stop = fakeBridge(3, () => ({ id: 'e', ok: false, error: { code: 'READ_ONLY', message: 'Chỉ xem' } } as ToolResponse));
    const s = await runBatch(stop.b, { task: 'standards', folder: 'C:/a', user_confirmed: ['T'] });
    assert.ok(!s.ok);
    assert.equal(stop.calls.filter((c) => c.action === 'run').length, 1);
    assert.ok(stopsRun({ id: 'x', ok: false, error: { code: 'INVALID_PARAMS', message: 'Confirmation required: batch_files would …' } } as ToolResponse));
  });

  it('Dừng stops between two files and says how far it got', async () => {
    const ac = new AbortController();
    let n = 0;
    const { b, calls } = fakeBridge(5, () => { if (++n === 2) ac.abort(); return null; });
    const r = await runBatch(b, { task: 'pdf', folder: 'C:/a', user_confirmed: ['T'] }, { signal: ac.signal });
    assert.equal(calls.filter((c) => c.action === 'run').length, 2);
    assert.match((r as any).result.stopped, /after 2 of 5/);
    assert.equal(calls[calls.length - 1].action, 'finish');
  });

  it('Test & sửa H5: refused half way, the files already done are still reported (and nothing else runs)', async () => {
    const card = { id: 'e', ok: false, error: { code: 'INVALID_PARAMS', message: 'Confirmation required: batch_files would …' } } as ToolResponse;
    const { b, calls } = fakeBridge(4, (i) => (i === 2 ? card : null));
    const r = await runBatch(b, { task: 'standards', folder: 'C:/a', user_confirmed: ['T'] });
    assert.ok(r.ok);
    const res = (r as any).result;
    assert.equal(res.done, 2);
    assert.equal(res.ok, 2);
    assert.match(res.stopped, /after 2 of 4/);
    assert.match(res.stopped, /Confirmation required/);
    assert.deepEqual(res.files.map((f: any) => f.file), ['f0.dwg', 'f1.dwg']);
    assert.equal(calls.filter((c) => c.action === 'run').length, 3);
    assert.ok(!calls.some((c) => c.action === 'finish'), 'the summary needs the card too: not called');
  });

  it('Test & sửa H5: a failing file keeps its name from the plan', async () => {
    const { b } = fakeBridge(2, (i) => (i === 0 ? ({ id: 'e', ok: false, error: { code: 'ACAD_ERROR', message: 'x' } } as ToolResponse) : null));
    const planned: ToolBridge = { ...b, call: async (t, p) => {
      const r = await b.call(t, p);
      if (p.action === 'plan' && r.ok) (r.result as any).files = ['ban-ve-a.dwg', 'ban-ve-b.dwg'];
      return r;
    } };
    const r = await runBatch(planned, { task: 'check', folder: 'C:/a', user_confirmed: ['T'] });
    assert.equal((r as any).result.files[0].file, 'ban-ve-a.dwg');
  });
});
