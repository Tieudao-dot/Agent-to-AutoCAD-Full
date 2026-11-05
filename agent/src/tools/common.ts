import { tool, type SdkMcpToolDefinition } from '@anthropic-ai/claude-agent-sdk';
import { z } from 'zod';
import type { ToolResponse } from '../ipc/protocol.js';

/** What a tool body needs: forward the call to the plugin and report status to the palette. */
export interface ToolBridge {
  call(tool: string, params: Record<string, unknown>): Promise<ToolResponse>;
  status(tool: string, state: 'running' | 'done' | 'error'): void;
}

export type AnyTool = SdkMcpToolDefinition<any>;

/** MCP result for Claude. Successful results are compact JSON; errors carry the code. */
export function toCallToolResult(resp: ToolResponse) {
  if (resp.ok) {
    return { content: [{ type: 'text' as const, text: JSON.stringify(resp.result ?? {}) }] };
  }
  return {
    content: [{ type: 'text' as const, text: `ERROR ${resp.error.code}: ${resp.error.message}` }],
    isError: true,
  };
}

/** Removes undefined values so the plugin only sees parameters Claude actually set. */
export function clean(args: Record<string, unknown>): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(args)) if (v !== undefined) out[k] = v;
  return out;
}

/**
 * Declares a ClaudeCAD tool whose body only forwards the call over the pipe and
 * waits for the result (spec §5).
 */
export function cadTool<Shape extends z.ZodRawShape>(
  bridge: ToolBridge,
  name: string,
  description: string,
  shape: Shape,
  opts: { readOnly?: boolean; destructive?: boolean } = {},
): AnyTool {
  return tool<Shape>(
    name,
    description,
    shape,
    async (args) => {
      bridge.status(name, 'running');
      const resp = await bridge.call(name, clean(args as Record<string, unknown>));
      bridge.status(name, resp.ok ? 'done' : 'error');
      return toCallToolResult(resp);
    },
    {
      annotations: {
        readOnlyHint: opts.readOnly ?? false,
        destructiveHint: opts.destructive ?? false,
        openWorldHint: false,
      },
    },
  ) as unknown as AnyTool;
}

// ------------------------------------------------------------------ shared schemas

export const point = z
  .array(z.number())
  .min(2)
  .max(3)
  .describe('Point [x, y] or [x, y, z] in millimetres, WCS. Missing z = 0. Example: [4000, 3600]');

export const points = (min: number, what = 'points') =>
  z.array(point).min(min).describe(`Array of ${what} [[x, y], ...] in mm (at least ${min}).`);

export const handle = z
  .string()
  .regex(/^[0-9A-Fa-f]{1,16}$/)
  .describe('Entity handle: hex string exactly as returned by other tools, e.g. "2F4A".');

export const handles = z.array(handle).min(1).describe('Entity handles (hex strings) returned by previous tools.');

export const color = z
  .union([z.number().int().min(0).max(256), z.enum(['bylayer', 'byblock'])])
  .describe('AutoCAD Color Index 1-255 (1 red, 2 yellow, 3 green, 4 cyan, 5 blue, 6 magenta, 7 white/black, 8 dark grey, 9 light grey) or "bylayer" (default) / "byblock".');

export const angle = (what = 'Angle') =>
  z.number().describe(`${what} in degrees, counter-clockwise from the +X axis.`);

/** Optional properties every drawing tool accepts (spec §7). */
export const entityProps = {
  layer: z
    .string()
    .optional()
    .describe('Layer name. Must already exist (create_layer first). Default: current layer.'),
  color: color.optional(),
  linetype: z
    .string()
    .optional()
    .describe('Linetype name, e.g. "CONTINUOUS", "DASHED", "CENTER", "HIDDEN". Loaded from acadiso.lin if missing. Default: ByLayer.'),
};
