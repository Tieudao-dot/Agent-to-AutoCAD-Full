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

export async function runBatch(b: ToolBridge, params: Record<string, unknown>, o: BatchOptions = {}): Promise<ToolResponse> {
  const { excel, ...base } = params;
  const plan = await b.call('batch_files', { ...base, action: 'plan' });
  if (!plan.ok || base[USER_CONFIRMED_PARAM] === undefined) return plan; // the plan (files, skipped, token) for the card
  const p = plan.result as Row;
  const count = typeof p.count === 'number' ? p.count : 0;
  const stamp = p.stamp;
  const rows: Row[] = [];
  let stopped = false;
  for (let i = 0; i < count; i++) {
    if (o.signal?.aborted) { stopped = true; break; }
    b.status('batch_files', 'running', { summary: `file ${i + 1} / ${count} (Dừng để ngừng sau file đang làm)` });
    const r = await b.call('batch_files', { ...base, action: 'run', index: i, stamp });
    if (stopsRun(r)) return r;
    rows.push(r.ok ? (r.result as Row) : { index: i, status: 'error', reason: `${r.error.code}: ${r.error.message}`.slice(0, 300) });
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
