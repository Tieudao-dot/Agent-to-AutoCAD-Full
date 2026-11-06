import { z } from 'zod';
import { angle, cadTool, color, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

const CONFIRM =
  ' DESTRUCTIVE when it affects objects selected by a filter or more than 20 objects: first tell the user how many and what, ask for confirmation, ' +
  'and only call this after they agree.';

/** Spec §7.5 — modification. */
export function modifyTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'move_entities',
      'Move entities by the vector from → to (or by "displacement" [dx, dy]).' + CONFIRM,
      { handles, from: point.optional(), to: point.optional(), displacement: point.optional().describe('Vector [dx, dy] instead of from/to.') }),

    cadTool(b, 'copy_entities',
      'Copy entities by from → to, or to several places at once with displacements [[dx,dy], ...]. Block attributes are kept. ' +
        'Returns all new handles and "copies" (one handle list per displacement).',
      { handles, from: point.optional(), to: point.optional(), displacements: points(1, 'displacement vectors').optional() }),

    cadTool(b, 'rotate_entities', 'Rotate entities about base by angle (degrees, CCW positive).' + CONFIRM, {
      handles, base: point, angle: angle('Rotation angle'),
    }),

    cadTool(b, 'scale_entities', 'Scale entities uniformly about base by factor (> 0).' + CONFIRM, {
      handles, base: point, factor: z.number().positive(),
    }),

    cadTool(b, 'mirror_entities',
      'Mirror entities about the axis axis_p1 → axis_p2. keep_original=true (default) creates mirrored copies; false mirrors in place. ' +
        'Single-line text stays readable (like MIRRTEXT=0).',
      { handles, axis_p1: point, axis_p2: point, keep_original: z.boolean().optional() }),

    cadTool(b, 'offset_entity',
      'Offset a line, polyline, arc, circle, ellipse or spline by distance towards side_point (any point on the side where the copy should go). ' +
        'Typical use: second wall line ("tường 220": offset the wall axis/edge by 220). Returns the new handle(s).',
      { handle, distance: z.number().positive(), side_point: point }),

    cadTool(b, 'array_rect',
      'Rectangular array (non-associative copies): rows x cols with row_spacing (Y) and col_spacing (X) in mm, optional angle of the array axes. ' +
        'The originals are item [0,0]. Example: 3 rows x 8 cols of chairs at 900 x 600 → {"rows":3,"cols":8,"row_spacing":900,"col_spacing":600}.',
      {
        handles,
        rows: z.number().int().min(1).max(1000),
        cols: z.number().int().min(1).max(1000),
        row_spacing: z.number().describe('Distance between rows (mm, along Y; negative = downwards).'),
        col_spacing: z.number().describe('Distance between columns (mm, along X; negative = leftwards).'),
        angle: angle('Array axis rotation').optional(),
      }),

    cadTool(b, 'array_polar',
      'Polar array around center: count items (including the original) over fill_angle degrees (default 360). rotate_items (default true) rotates copies.',
      {
        handles, center: point,
        count: z.number().int().min(2).max(10000),
        fill_angle: z.number().optional(),
        rotate_items: z.boolean().optional(),
      }),

    cadTool(b, 'fillet_entities',
      'Fillet the corner between two LINEs with radius (0 = trim/extend to a sharp corner). Each line keeps its end farther from the corner. ' +
        'Returns the two lines and the new arc (arc_handle).',
      { handle1: handle, handle2: handle, radius: z.number().min(0) }),

    cadTool(b, 'chamfer_entities',
      'Chamfer the corner between two LINEs: dist1 along line 1, dist2 along line 2 (default = dist1) from the intersection.',
      { handle1: handle, handle2: handle, dist1: z.number().min(0), dist2: z.number().min(0).optional() }),

    cadTool(b, 'explode_entities',
      'Explode polylines, blocks, dimensions, hatches... into their components. The originals are erased; returns the new handles.' + CONFIRM,
      { handles }),

    cadTool(b, 'join_entities',
      'Join touching LINE / ARC / LWPOLYLINE entities into one polyline (the first handle, or a new polyline if the first is a line/arc). ' +
        'Order does not matter. Returns the polyline handle, joined count and not_joined handles.',
      { handles }),

    cadTool(b, 'erase_entities',
      'Erase entities by handle (max 1000 per call; the plugin refuses more with TOO_MANY_ENTITIES). ' +
        'Erasing objects chosen by a filter/condition (e.g. "everything on layer A-TEMP") or more than 20 objects REQUIRES confirmation: ' +
        'call count_entities/list_entities, tell the user the number, ask, and erase only after they explicitly agree.',
      { handles: handles.max(1000) },
      { destructive: true }),

    cadTool(b, 'set_entity_props',
      'Change layer, color, linetype, lineweight or linetype_scale of entities.' + CONFIRM,
      {
        handles,
        layer: entityProps.layer,
        color: color.optional(),
        linetype: entityProps.linetype,
        lineweight: entityProps.lineweight,
        linetype_scale: z.number().positive().optional(),
      }),

    cadTool(b, 'edit_text',
      'Edit text of a TEXT, MTEXT, block attribute (use its own handle from find_text), dimension (text override; "" restores the measured value, ' +
        '"<>" stands for it), multileader, or table cell (row, col). Either replace everything with "text", or replace a substring with ' +
        '"find" + "replace_with" (keeps MTEXT formatting). Optional new height (mm) and rotation (degrees).',
      {
        handle,
        text: z.string().optional(),
        find: z.string().optional(),
        replace_with: z.string().optional(),
        match_case: z.boolean().optional(),
        height: z.number().positive().optional(),
        rotation: angle('Text rotation').optional(),
        row: z.number().int().min(0).optional().describe('Table row (tables only).'),
        col: z.number().int().min(0).optional().describe('Table column (tables only).'),
      }),

    cadTool(b, 'edit_polyline',
      'Modify an LWPOLYLINE: replace all vertices (and optional bulges), change bulges only, open/close it, or set constant width.',
      {
        handle,
        vertices: points(2, 'new vertices').optional(),
        bulges: z.array(z.number()).optional(),
        closed: z.boolean().optional(),
        width: z.number().min(0).optional(),
      }),
  ];
}
