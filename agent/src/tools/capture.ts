import { tool } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import type { ToolResponse } from '../ipc/protocol.js';
import { clean, handles, point, toCallToolResult, type AnyTool, type ToolBridge } from './common.js';

/**
 * MCP result of capture_view: the PNG as an image block Claude can look at, plus the
 * metadata (window, scale) as text. The plugin sends the image base64 in the JSON result.
 */
export interface CaptureCallResult {
  content: Array<{ type: 'text'; text: string } | { type: 'image'; data: string; mimeType: string }>;
  isError?: boolean;
}

export function toCaptureResult(resp: ToolResponse): CaptureCallResult {
  if (!resp.ok) return toCallToolResult(resp);
  const r = (typeof resp.result === 'object' && resp.result !== null ? resp.result : {}) as Record<string, unknown>;
  const { image_png_base64: data, ...meta } = r;
  if (typeof data !== 'string' || data.length === 0) {
    return { content: [{ type: 'text' as const, text: `ERROR ACAD_ERROR: capture_view returned no image ${JSON.stringify(meta)}` }], isError: true };
  }
  return {
    content: [
      { type: 'image' as const, data, mimeType: 'image/png' },
      { type: 'text' as const, text: JSON.stringify(meta) },
    ],
  };
}

/** "Claude tự nhìn bản vẽ": an off-screen picture of model space (the user's view is not changed). */
export function captureTools(b: ToolBridge): AnyTool[] {
  return [
    tool(
      'capture_view',
      'Look at the drawing: returns a PNG picture of model space (plan view, dark background) plus the WCS window it shows and mm_per_pixel. ' +
        "It renders off-screen: the user's view is not zoomed and nothing is changed. " +
        'Default: the current view. handles: frame those objects (e.g. what you just drew); p1/p2: a WCS window; extents: true for the whole drawing. ' +
        'Use it after a big drawing task to check your work visually (overlaps, texts on top of each other, doors in the wrong wall, gaps), ' +
        'or when the user asks what is in an area. Each picture costs about 1-1.6k input tokens: use one or two per task, not after every small change. ' +
        'Read exact coordinates with the query tools, not from the picture.',
      {
        handles: handles.max(5000).optional(),
        p1: point.optional(),
        p2: point.optional(),
        extents: z.boolean().optional(),
        size: z.number().int().min(384).max(1568).optional().describe('Long side of the picture in pixels (default 1024).'),
      },
      async (args) => {
        b.status('capture_view', 'running');
        const t0 = Date.now();
        const resp = await b.call('capture_view', clean(args as Record<string, unknown>));
        const ms = Date.now() - t0;
        if (resp.ok) {
          const r = resp.result as Record<string, unknown>;
          b.status('capture_view', 'done', { ms, summary: `ảnh ${r?.width ?? '?'}×${r?.height ?? '?'}` });
        } else {
          b.status('capture_view', 'error', { ms, error: `${resp.error.code}: ${resp.error.message}`.slice(0, 300) });
        }
        return toCaptureResult(resp) as never;
      },
      { annotations: { readOnlyHint: true, destructiveHint: false, openWorldHint: false } },
    ) as unknown as AnyTool,
  ];
}
