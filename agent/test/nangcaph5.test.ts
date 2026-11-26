/**
 * Nâng cấp đợt H5 (docs/ban-giao/nang-cap-h5.md): batch_files — plan without the card, one plugin call per file after it,
 * Dừng between files, one failing file does not stop the run, plan / mode errors stop it, the summary.
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { F5_CARD_TOOLS, applyUserConfirmation } from '../src/agent/confirmation.js';
import { BATCH_WRITE_MS, timeoutFor } from '../src/ipc/rpc.js';
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

describe('Sửa KTH-01 / 02: batch_files on the real path', () => {
  const TOKEN = 'ABCDEF0123456789';
  /** A bridge like index.ts: each plugin call goes through applyUserConfirmation (the card of the turn), then a fake plugin. */
  function realBridge(approved: string[]) {
    const seen: Array<Record<string, unknown>> = [];
    const g = { userText: 'Đồng ý', answersQuestion: true, confirmedByButton: approved.length > 0, cardConfirmed: new Set(approved) };
    const b: ToolBridge = {
      status: () => {},
      call: async (tool: string, raw: Record<string, unknown>) => {
        const params = applyUserConfirmation(tool, raw, g as any);
        seen.push(params);
        if (params.action === 'plan') return { id: 'p', ok: true, result: { count: 2, stamp: '20261126-110000', confirm_token: TOKEN } } as ToolResponse;
        const ok = Array.isArray(params.user_confirmed) && (params.user_confirmed as string[]).includes(TOKEN);
        if (!ok) return { id: 'r', ok: false, error: { code: 'INVALID_PARAMS', message: 'Confirmation required: batch_files would check 2 drawing(s)' } } as ToolResponse;
        if (params.action === 'run') return { id: 'r', ok: true, result: { file: `f${params.index}.dwg`, status: 'ok' } } as ToolResponse;
        return { id: 'f', ok: true, result: { ok: 2 } } as ToolResponse;
      },
    };
    return { b, seen };
  }

  it('Claude sends confirm_handles after Đồng ý: every file runs', async () => {
    const { b, seen } = realBridge([TOKEN]);
    const r = await runBatch(b, { task: 'check', folder: 'C:/a', confirm_handles: [TOKEN] });
    assert.ok(r.ok);
    assert.deepEqual(seen.map((s) => s.action), ['plan', 'run', 'run', 'finish']);
    assert.ok(seen.every((s) => s.confirm_handles === undefined)); // the bridge sends user_confirmed instead
    assert.equal((r as any).result.done, 2);
  });

  it('confirm_handles that were not on an approved card: refused at the first file, nothing done', async () => {
    const { b, seen } = realBridge([]);
    const r = await runBatch(b, { task: 'standards', folder: 'C:/a', confirm_handles: [TOKEN] });
    assert.ok(!r.ok);
    assert.match((r as any).error.message, /Confirmation required/);
    assert.equal(seen.filter((s) => s.action === 'run').length, 1);
  });

  it('without confirm_handles: the plan only', async () => {
    const { b, seen } = realBridge([TOKEN]);
    await runBatch(b, { task: 'pdf', folder: 'C:/a' });
    assert.deepEqual(seen.map((s) => s.action), ['plan']);
  });

  it('one file has the time of a slow tool, not 30 s', () => {
    assert.equal(timeoutFor('batch_files', { action: 'run', task: 'check' }, 30_000), 120_000);
    assert.equal(timeoutFor('batch_files', { action: 'run', task: 'blocks' }, 30_000), 120_000);
    assert.equal(timeoutFor('batch_files', { action: 'run', task: 'standards' }, 30_000), BATCH_WRITE_MS);
    assert.equal(timeoutFor('batch_files', { action: 'run', task: 'pdf' }, 30_000), BATCH_WRITE_MS);
    assert.equal(BATCH_WRITE_MS, 5 * 60_000);
    assert.equal(timeoutFor('batch_files', { action: 'plan', task: 'pdf' }, 30_000), 30_000);
  });

  it('nghi vấn KTH: export_drawing, compare_drawings and company_standard learn are slow tools too', () => {
    assert.equal(timeoutFor('export_drawing', { format: 'fbx' }, 30_000), 5 * 60_000);
    assert.equal(timeoutFor('compare_drawings', { file: 'C:/a.dwg' }, 30_000), 120_000);
    assert.equal(timeoutFor('company_standard', { action: 'learn', file: 'C:/a.dwt' }, 30_000), 120_000);
    assert.equal(timeoutFor('company_standard', { action: 'list' }, 30_000), 30_000);
  });
});
