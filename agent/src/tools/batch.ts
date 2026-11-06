import { z } from 'zod';
import { cadTool, type AnyTool, type ToolBridge } from './common.js';

export const MAX_BATCH_CALLS = 500;

/** Spec §7.8 — the most important tool for performance. */
export function batchTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'batch_execute',
      `Run up to ${MAX_BATCH_CALLS} ClaudeCAD tool calls sequentially in ONE AutoCAD transaction (one round-trip). ` +
        'ALWAYS use this when creating or changing more than ~3 objects (grids, walls, repeated symbols, dimensions, many text edits). ' +
        'Each call is {"tool": "<tool name WITHOUT the mcp__claudecad__ prefix>", "params": {...same params as that tool...}}. ' +
        'Calls run in order; a failing call is rolled back alone and later calls still run — check each result. ' +
        'Layers/styles needed by later calls can be created by earlier calls in the same batch. ' +
        'Not allowed inside a batch: batch_execute itself, zoom_*, regen, save_drawing, purge_unused, undo_last_turn. ' +
        'Example: {"calls": [{"tool": "create_layer", "params": {"name": "A-AXIS", "color": 1, "linetype": "CENTER"}}, ' +
        '{"tool": "draw_line", "params": {"start": [0,-1000], "end": [0,11800], "layer": "A-AXIS"}}]}. ' +
        'Returns {"ok_count", "error_count", "handles" (all created/modified), "results": [{"index", "tool", "ok", "result"|"error"}]}.',
      {
        calls: z
          .array(
            z.object({
              tool: z.string().min(1).describe('Tool name without prefix, e.g. "draw_line".'),
              params: z.record(z.string(), z.unknown()).optional().describe('Parameters of that tool.'),
            }),
          )
          .min(1)
          .max(MAX_BATCH_CALLS),
      }),
  ];
}
