import { z } from 'zod';
import { cadTool, type AnyTool, type ToolBridge } from './common.js';

/** Phase 1a: round-trip check with the plugin. */
export function systemTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(
      b,
      'ping',
      'Check the connection to the AutoCAD plugin. Returns plugin version, AutoCAD version and the active drawing name, ' +
        'and echoes back "message". Use only when the user asks whether the connection works or when debugging; ' +
        'it never changes the drawing.',
      { message: z.string().optional().describe('Any text to echo back.') },
      { readOnly: true },
    ),
  ];
}
