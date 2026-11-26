import { tool } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import { USER_CONFIRMED_PARAM } from '../agent/confirmation.js';
import type { ToolResponse } from '../ipc/protocol.js';
import { clean, confirmDesc, handles, toCallToolResult, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H5 (docs/ban-giao/nang-cap-h5.md): batch_files, "làm nhiều file một lúc". The plugin does one file per call
 * (action run, index); this runs them one after another with the progress in the palette, stops between two files when the
 * user presses Dừng (no file is left half written), keeps going when one file fails, and ends with the summary (action
 * finish, the Excel file on request). Without the card the call only plans: the files, what is skipped, the token.
 */
export const BATCH_TASKS = ['check', 'blocks', 'standards', 'pdf'] as const;

export interface BatchOptions {
  signal?: AbortSignal;
}

type Row = Record<string, unknown>;

/** Errors that stop a whole run (the same answer for every file): plan / mode / Chỉ xem / card. */
export function stopsRun(r: ToolResponse): boolean {
  return !r.ok && ['NOT_IN_PLAN', 'READ_ONLY'].includes(r.error.code) || (!r.ok && /Confirmation required/.test(r.error.message));
}

/**
 * Sửa KTH-01: the call carries the card. Claude sends confirm_handles; only the bridge (session.paramsToSend) turns the
 * approved ones into the plugin's confirmed flag (USER_CONFIRMED_PARAM), on each plugin call, after this point. So the run starts on confirm_handles; when they
 * were not approved on a card, the plugin refuses the first file and that refusal is the answer (nothing was done).
 */
export function cardGiven(params: Record<string, unknown>): boolean {
  const asked = params.confirm_handles;
  return (Array.isArray(asked) && asked.length > 0) || params[USER_CONFIRMED_PARAM] !== undefined;
}

export async function runBatch(b: ToolBridge, params: Record<string, unknown>, o: BatchOptions = {}): Promise<ToolResponse> {
  const { excel, ...base } = params;
  const plan = await b.call('batch_files', { ...base, action: 'plan' });
  if (!plan.ok || !cardGiven(base)) return plan; // the plan (files, skipped, token) for the card
  const p = plan.result as Row;
  const count = typeof p.count === 'number' ? p.count : 0;
  const stamp = p.stamp;
  const rows: Row[] = [];
  let stopped = false;
  for (let i = 0; i < count; i++) {
    if (o.signal?.aborted) { stopped = true; break; }
    b.status('batch_files', 'running', { summary: `file ${i + 1} / ${count} (Dừng để ngừng sau file đang làm)` });
    const r = await b.call('batch_files', { ...base, action: 'run', index: i, stamp });
    if (!r.ok && r.error.code === 'TIMEOUT') {
      // Soát KTH-02: no answer in time. AutoCAD may still be working on this file (the write / print can finish after the
      // wait), so the run stops here instead of starting the next file, and the user checks that file before running again
      return partialResult(plan.id, rows, count, `TIMEOUT: ${r.error.message}`.slice(0, 400), listed(p, i));
    }
    if (stopsRun(r)) {
      if (rows.length === 0) return r;
      // Test & sửa H5: refused half way (e.g. a file was added to the folder, so the card no longer matches): the files
      // already done are still reported, so the user knows which ones were changed
      return partialResult(plan.id, rows, count, `${r.ok ? '' : `${r.error.code}: ${r.error.message}`}`.slice(0, 400));
    }
    rows.push(r.ok ? (r.result as Row) : { index: i, file: listed(p, i), status: 'error', reason: `${r.error.code}: ${r.error.message}`.slice(0, 300) });
  }
  const fin = await b.call('batch_files', { ...base, action: 'finish', stamp, rows, excel: excel === true });
  const summary = fin.ok ? (fin.result as Row) : { finish_error: fin.error.message };
  const short = rows.map((r) => ({ file: r.file, status: r.status, ...(r.reason ? { reason: r.reason } : {}), ...(r.note ? { note: r.note } : {}) }));
  return {
    id: plan.id,
    ok: true,
    result: {
      ...summary,
      done: rows.length,
      of: count,
      ...(stopped ? { stopped: `stopped by the user (Dừng) after ${rows.length} of ${count} files; the files not reached were not touched` } : {}),
      files: short.slice(0, 200),
    },
  } as ToolResponse;
}

/** The name of file i as the plan listed it (the plan lists the first ones), else its number. */
function listed(plan: Row, i: number): string {
  const f = Array.isArray(plan.files) ? (plan.files as unknown[])[i] : undefined;
  return typeof f === 'string' ? f : `#${i + 1}`;
}

function totals(rows: Row[]): Record<string, number> {
  const t: Record<string, number> = { ok: 0, error: 0, skipped: 0 };
  for (const r of rows) if (typeof r.status === 'string' && r.status in t) t[r.status]! += 1;
  return t;
}

/** A run stopped by the plugin after some files: what was done, and why it stopped (no summary file is written). */
export function partialResult(id: string, rows: Row[], count: number, why: string, timedOutFile?: string): ToolResponse {
  const short = rows.map((r) => ({ file: r.file, status: r.status, ...(r.reason ? { reason: r.reason } : {}), ...(r.backup ? { backup: r.backup } : {}) }));
  return {
    id,
    ok: true,
    result: {
      ...totals(rows),
      done: rows.length,
      of: count,
      stopped: `the run stopped after ${rows.length} of ${count} files (${why}); the files listed here were done, the others were not touched. Tell the user, then plan again if they want the rest.`,
      ...(timedOutFile !== undefined ? {
        timed_out_file: timedOutFile,
        check_first: `${timedOutFile} got no answer in time: AutoCAD may still have finished it (written or printed; for standards its backup is already in _sao-luu-claudecad). ` +
          'Tell the user to open it and check before running again; a new run would do it again.',
      } : {}),
      files: short.slice(0, 200),
    },
  } as ToolResponse;
}

export function batchFilesTools(b: ToolBridge): AnyTool[] {
  return [
    tool('batch_files',
      'Work on many drawing files at once ("cả thư mục này"): a folder (recursive: sub-folders too) or files. task check = check_drawing of each (company: a company standard), ' +
        'blocks = block counts, standards = setup_standards in each file (scale / standard / company; each file is copied to _sao-luu-claudecad first, then written), ' +
        'pdf = every layout of each file into <file>.pdf. Without confirm_handles it only lists the files and the token: ask with request_confirmation, then call again ' +
        'with confirm_handles (same parameters). Files open elsewhere / read-only are skipped; one failing file does not stop the run; Dừng stops between files. excel: true = summary .xlsx.',
      {
        task: z.enum(BATCH_TASKS),
        folder: z.string().min(1).max(260).optional(),
        files: z.array(z.string().min(1).max(260)).min(1).max(200).optional(),
        recursive: z.boolean().optional(),
        company: z.string().min(1).max(60).optional(),
        scale: z.union([z.string(), z.number().positive()]).optional().describe('standards: print scale "1:100".'),
        standard: z.string().optional(),
        groups: z.array(z.string()).optional().describe('check: only these groups.'),
        layouts: z.union([z.boolean(), z.array(z.string())]).optional().describe('check: also layouts (default true); pdf: names (default all).'),
        excel: z.boolean().optional(),
        confirm_handles: handles.max(5).optional().describe(confirmDesc),
      },
      async (args, extra) => {
        const params = clean(args as Record<string, unknown>);
        // check takes layouts as a yes / no, pdf as names
        if (params.task === 'pdf' && typeof params.layouts === 'boolean') delete params.layouts;
        if (params.task !== 'pdf' && Array.isArray(params.layouts)) delete params.layouts;
        b.status('batch_files', 'running');
        const t0 = Date.now();
        const resp = await runBatch(b, params, { signal: (extra as { signal?: AbortSignal } | undefined)?.signal });
        const ms = Date.now() - t0;
        if (resp.ok) b.status('batch_files', 'done', { ms });
        else b.status('batch_files', 'error', { ms, error: `${resp.error.code}: ${resp.error.message}`.slice(0, 300) });
        return toCallToolResult(resp);
      },
      { annotations: { readOnlyHint: false, destructiveHint: false, openWorldHint: false } },
    ) as unknown as AnyTool,
  ];
}
