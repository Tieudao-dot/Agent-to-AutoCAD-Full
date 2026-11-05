import { z } from 'zod';
import { cadTool, point, type AnyTool, type ToolBridge } from './common.js';

/** Spec §7.7 — view and document. */
export function viewTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'zoom_extents', 'Zoom the current view to show the whole drawing. Good after drawing something new.', {}),

    cadTool(b, 'zoom_window', 'Zoom the current view to the rectangle p1–p2 (WCS, mm).', { p1: point, p2: point }),

    cadTool(b, 'regen', 'Regenerate the display (rarely needed; batch_execute already refreshes the screen).', {}, { readOnly: true }),

    cadTool(b, 'save_drawing',
      'Save the drawing (QSAVE). ONLY call when the user explicitly asks to save. For a never-saved drawing ask the user for a full path ending in .dwg.',
      { path: z.string().optional() }),

    cadTool(b, 'purge_unused',
      'Purge unused blocks, layers, linetypes, text and dimension styles. ONLY call when the user explicitly asks to purge/clean up. Returns what was removed.',
      {},
      { destructive: true }),

    cadTool(b, 'undo_last_turn',
      'Undo the previous chat turn (all of its changes, as one U). Call it at the start of a turn when the user asks to undo / "hoàn tác", ' +
        'before changing anything else, and do not draw in the same turn.',
      {}),
  ];
}
