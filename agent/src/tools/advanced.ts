import { z } from 'zod';
import { angle, cadTool, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/** Phase 2 — xrefs, exact trim/extend, dynamic block extras. */
export function advancedTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'attach_xref',
      'Attach an external DWG as an xref (overlay by default) and place it at position/scale/rotation. Only for files the user names. ' +
        'Fails if the file does not exist.',
      {
        path: z.string().min(1).describe('Full path to the .dwg file.'),
        name: z.string().optional().describe('Xref name (default: file name).'),
        position: point.optional(),
        scale: z.number().positive().optional(),
        rotation: angle('Rotation').optional(),
        overlay: z.boolean().optional().describe('true (default) = overlay, false = attachment (nests in other drawings).'),
        ...entityProps,
      }),

    cadTool(b, 'list_xrefs', 'List xrefs: name, saved path, overlay/attach, load status (Resolved, Unresolved, FileNotFound...), number of references.', {}, { readOnly: true }),

    cadTool(b, 'reload_xref', 'Reload xrefs by name (after the referenced file changed).', { names: z.array(z.string()).min(1) }),

    cadTool(b, 'detach_xref', 'Detach xrefs by name (removes all their references). Destructive: confirm with the user first.', {
      names: z.array(z.string()).min(1),
    }, { destructive: true }),

    cadTool(b, 'bind_xref', 'Bind xrefs into the drawing as normal blocks (insert=true merges names like INSERT; false keeps xref$0$ prefixes).', {
      names: z.array(z.string()).min(1),
      insert: z.boolean().optional(),
    }),

    cadTool(b, 'trim_entities',
      'TRIM with exact geometry: each curve in handles is cut at its intersections with cutting_handles and the piece containing / nearest to its ' +
        'pick point is removed. pick_points: one point per handle (or one for all), ON the part to delete. Works on lines, arcs, circles (need 2 ' +
        'intersections), polylines, ellipses, splines. Trimmed curves are replaced by new pieces (new handles returned).',
      {
        handles: handles.max(500),
        cutting_handles: handles,
        pick_points: points(1, 'pick points on the parts to remove'),
      }),

    cadTool(b, 'extend_entities',
      'EXTEND with exact geometry: lengthens open lines / arcs / polylines to the nearest boundary in boundary_handles. end: "auto" (default; uses ' +
        'pick_points near the end to extend, else the end with the shorter extension), "start", "end" or "both".',
      {
        handles: handles.max(500),
        boundary_handles: handles,
        end: z.enum(['auto', 'start', 'end', 'both']).optional(),
        pick_points: points(1).optional(),
      }),

    cadTool(b, 'get_dynamic_props',
      'Dynamic block properties of a block reference: name, current value, allowed values (e.g. visibility states, lookup values), units, read-only.',
      { handle },
      { readOnly: true }),

    cadTool(b, 'reset_dynamic_block', 'Reset dynamic block references to their default property values.', { handles }),
  ];
}
