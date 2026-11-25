import { tool } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import { summarizeResult } from '../agent/content.js';
import type { ToolResponse } from '../ipc/protocol.js';
import { cadTool, clean, confirmDesc, handles, toCallToolResult, type AnyTool, type ToolBridge } from './common.js';
import { viewPreset, visualStyle } from './solids.js';

/**
 * Nâng cấp đợt F3 (25/11/2026, docs/ban-giao/nang-cap-f3.md): materials and named views (+ a PNG with the materials).
 * Labelled 3D (3D / Full plans). Plain AutoCAD materials (colour + opacity), seen in the Realistic visual style.
 */
export const RENDER_TOOL_NAMES = ['set_material', 'named_view'] as const;

/** Preset materials, shared with the plugin (Core/Materials.cs, MaterialLogic.Presets). */
export const MATERIAL_PRESETS = ['concrete', 'brick', 'plaster', 'glass', 'wood', 'roof_tile', 'steel', 'stone'] as const;

/** Nâng cấp đợt G4: the render qualities (Core/Render.cs, RenderLogic.Qualities). */
export const RENDER_QUALITIES = ['low', 'medium', 'high'] as const;

/** How often the assistant asks AutoCAD whether the render is done (Core/Render.cs, G4Numbers.PollMs). */
export const RENDER_POLL_MS = 2000;

export interface RenderWaitOptions {
  pollMs?: number;
  sleep?: (ms: number) => Promise<void>;
  signal?: AbortSignal;
  now?: () => number;
}

const realSleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));

/**
 * Nâng cấp đợt G4: named_view action render. The plugin starts AutoCAD's -RENDER and returns at once; this asks render_status
 * every couple of seconds (AutoCAD stays free between the questions) and shows the progress in the palette, until the plugin
 * says the file is written, or a capture was saved instead (too long / failed), or the user stopped it. Dừng in the chat
 * (the call fails as cancelled, or the signal aborts) sends render_stop. A safety stop a minute past the max wait.
 */
export async function runRender(b: ToolBridge, params: Record<string, unknown>, o: RenderWaitOptions = {}): Promise<ToolResponse> {
  const sleep = o.sleep ?? realSleep;
  const now = o.now ?? Date.now;
  const poll = o.pollMs ?? RENDER_POLL_MS;
  const first = await b.call('named_view', params);
  if (!first.ok) return first;
  const start = first.result as Record<string, unknown>;
  if (start?.render_started !== true) return first;
  const maxWait = typeof start.max_wait_seconds === 'number' ? start.max_wait_seconds : 900;
  const estimate = typeof start.estimate_seconds === 'number' ? start.estimate_seconds : 60;
  const t0 = now();
  b.status('named_view', 'running', { summary: `bắt đầu render, khoảng ${estimate} s (Dừng / Esc để hủy)` });
  const stop = async (why: string): Promise<ToolResponse> => {
    const r = await b.call('named_view', { action: 'render_stop' }).catch(() => undefined);
    return r && r.ok ? { ...r, result: { ...(r.result as object), reason: why } } : { id: first.id, ok: false, error: { code: 'ACAD_ERROR', message: `named_view: ${why}` } } as ToolResponse;
  };
  for (;;) {
    if (o.signal?.aborted) return stop('stopped by the user (Dừng)');
    await sleep(poll);
    if (o.signal?.aborted) return stop('stopped by the user (Dừng)');
    const st = await b.call('named_view', { action: 'render_status' });
    if (!st.ok) {
      if (/cancel/i.test(st.error.message)) await stop('stopped by the user (Dừng)');
      // Test & sửa G4: AutoCAD busy rendering in the foreground does not answer in time; that is not the end of the
      // render: keep asking (its answer waits in the plugin), within the safety limit
      if (st.error.code === 'TIMEOUT' && (now() - t0) / 1000 <= maxWait + 60) {
        b.status('named_view', 'running', { summary: 'AutoCAD đang bận render, chờ thêm…' });
        continue;
      }
      if (st.error.code === 'TIMEOUT') return stop('AutoCAD did not answer before the maximum wait');
      return st;
    }
    const r = st.result as Record<string, unknown>;
    if (r?.running !== true) {
      // the start parameters Claude may need (file, quality) join the final answer
      return { ...st, result: { ...start, ...r, render_started: undefined } };
    }
    if (typeof r.progress === 'string') b.status('named_view', 'running', { summary: r.progress });
    if ((now() - t0) / 1000 > maxWait + 60) return stop('the plugin did not finish the render in time');
  }
}

export function renderTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'set_material',
      `Give a material to solids (handles) and / or layers: preset ${MATERIAL_PRESETS.join(' / ')} (bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá; made once in the drawing), ` +
        'an existing material name, ByLayer / ByBlock (reset), or a new name with color [r, g, b] (+ opacity %). list: true = the drawing\'s materials (read-only). ' +
        'A preset uses a matching Autodesk library material when the drawing has one (dragged in from the Materials Browser), else the simple colour one; the result says which (source, library_note: pass it on). ' +
        'Changing existing objects follows the usual rule (more than 20: ask first); whole layers, or a new color / opacity for a material already in the drawing, need the card (the error lists the handles). Materials show in the realistic visual style (capture_view, named_view image).',
      {
        material: z.string().min(1).max(255).optional().describe('Preset key, existing material, ByLayer / ByBlock, or a new name with color.'),
        handles: handles.max(5000).optional(),
        layers: z.array(z.string().min(1).max(255)).min(1).optional(),
        color: z.array(z.number().int().min(0).max(255)).length(3).optional().describe('[r, g, b] of a new / changed material.'),
        opacity: z.number().min(0).max(100).optional().describe('% (100 = opaque).'),
        list: z.boolean().optional(),
        library: z.boolean().optional().describe('Default true: a preset takes the matching Autodesk library material already in the drawing (result source library / simple); false = always the simple one.'),
        confirm_handles: handles.max(50).optional().describe(confirmDesc),
      }),

    tool('named_view',
      'Named views of model space: save (the current view under name), restore, list, delete; overwriting / deleting a saved view or an existing PNG needs the card (the error lists the handles, then confirm_handles); ' +
        'image = save a PNG file of the model (off-screen, the user\'s view unchanged) looking along the saved view name (its zoom is not kept: the whole model or handles is framed) or view (default sw_iso), ' +
        'visual_style default realistic (shows the materials), handles to frame, output .png (relative = next to the drawing). ' +
        'render = a real AutoCAD render (looks best, takes seconds to minutes; the call waits and the palette shows the progress; the user can stop it): ' +
        'of the saved view name, else the current view; quality low / medium (default) / high, width × height (default 1920 × 1080), ' +
        'background white (default) / sky / drawing, lighting auto (default: the drawing\'s lights or AutoCAD default light) / default / sun / drawing; ' +
        'these are set for the render only and put back. If the render cannot finish, a picture like image is saved instead and the result says method capture: ' +
        'tell the user plainly whether the file is a render or a capture. Tell the user the file path.',
      {
        action: z.enum(['save', 'restore', 'list', 'delete', 'image', 'render']),
        name: z.string().min(1).max(255).optional(),
        replace: z.boolean().optional(),
        confirm_handles: handles.max(50).optional().describe(confirmDesc),
        view: viewPreset.optional(),
        visual_style: visualStyle.optional(),
        handles: handles.max(5000).optional(),
        output: z.string().min(1).max(1024).optional(),
        size: z.number().int().min(384).max(1568).optional().describe('image: long side in pixels (default 1568).'),
        quality: z.enum(RENDER_QUALITIES).optional().describe('render: low (fast draft) / medium (default) / high (slow).'),
        width: z.number().int().min(64).max(4096).optional().describe('render: pixels (default 1920).'),
        height: z.number().int().min(64).max(4096).optional().describe('render: pixels (default 1080).'),
        background: z.enum(['white', 'sky', 'drawing']).optional(),
        lighting: z.enum(['auto', 'default', 'sun', 'drawing']).optional(),
      },
      async (args, extra) => {
        const params = clean(args as Record<string, unknown>);
        b.status('named_view', 'running');
        const t0 = Date.now();
        const resp = params.action === 'render'
          ? await runRender(b, params, { signal: (extra as { signal?: AbortSignal } | undefined)?.signal })
          : await b.call('named_view', params);
        const ms = Date.now() - t0;
        if (resp.ok) b.status('named_view', 'done', { ms, ...summarizeResult('named_view', resp.result) });
        else b.status('named_view', 'error', { ms, error: `${resp.error.code}: ${resp.error.message}`.slice(0, 300) });
        return toCallToolResult(resp);
      },
      { annotations: { readOnlyHint: false, destructiveHint: false, openWorldHint: false } },
    ) as unknown as AnyTool,
  ];
}
