import { z } from 'zod';
import { angle, cadTool, color, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

const CONFIRM =
  ' DESTRUCTIVE when it affects objects selected by a filter or more than 20 objects: first tell the user how many and what, ask for confirmation, ' +
  'and only call this after they agree.';

/**
 * match_properties (nâng cấp đợt B): the properties every plan copies, and the text / dimension ones that need 2D. Without
 * 2D the schema offers only the general ones and the description drops MATCH_PROPS_TEXT_DESC (KT2-14, tools/index.ts).
 */
export const MATCH_PROPS_GENERAL = ['layer', 'color', 'linetype', 'lineweight', 'linetype_scale', 'transparency', 'thickness'] as const;
export const MATCH_PROPS_TEXT = ['text', 'dimension'] as const;
export const MATCH_PROPS_TEXT_DESC =
  ', text (style, height: text to text) and dimension (style: dimension to dimension). Default: all (text / dimension only on plans with 2D).';

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
      'Offset a line, polyline, arc, circle, ellipse or spline by distance towards side_point (any point on the side where the copy should go), ' +
        'or both_sides: true for one copy on each side (no side_point). ' +
        'Typical use: second wall line ("tường 220": offset the wall axis/edge by 220; wall centred on an axis: both_sides, distance 110). Returns the new handle(s).',
      { handle, distance: z.number().positive(), side_point: point.optional(), both_sides: z.boolean().optional() }),

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
        levels: z.number().int().min(1).max(1000).optional().describe('3D: levels stacked along Z (default 1), like ARRAYRECT levels, e.g. 3 storeys of columns.'),
        level_spacing: z.number().optional().describe('Distance between levels along Z in mm, converted to the drawing\'s units like the 3D heights (3300 = 3.3 in a metres drawing; negative = downwards); needed when levels > 1.'),
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
      'Fillet the corner between two LINEs / ARCs (line + arc, arc + arc) with radius (0 = trim/extend to a sharp corner). Each keeps its end farther from the corner. ' +
        'Returns the two objects and the new arc (arc_handle). An LWPOLYLINE: handle1 only (no handle2) fillets all its corners between straight segments, ' +
        'or just "vertex" (0-based index); changed in place, too-short corners are listed in skipped.',
      { handle1: handle, handle2: handle.optional(), radius: z.number().min(0), vertex: z.number().int().min(0).optional() }),

    cadTool(b, 'chamfer_entities',
      'Chamfer the corner between two LINEs / ARCs: dist1 along object 1, dist2 along object 2 (default = dist1) from the intersection (along the arc for an arc). ' +
        'An LWPOLYLINE: handle1 only chamfers all its corners (or "vertex"), dist1 back along the incoming segment, dist2 along the outgoing one.',
      { handle1: handle, handle2: handle.optional(), dist1: z.number().min(0), dist2: z.number().min(0).optional(), vertex: z.number().int().min(0).optional() }),

    cadTool(b, 'explode_entities',
      'Explode polylines, blocks, dimensions, hatches... into their components. The originals are erased; returns the new handles. ' +
        '3D solids, surfaces and meshes that existed before this turn are refused ("Confirmation required") until the user presses Đồng ý ' +
        'on a request_confirmation card listing their handles.' + CONFIRM,
      { handles }),

    cadTool(b, 'join_entities',
      'Join touching LINE / ARC / LWPOLYLINE entities into one polyline (the first handle, or a new polyline if the first is a line/arc). ' +
        'Order does not matter. tolerance (mm): gaps up to it are bridged by a straight segment (listed in bridged). ' +
        'Returns the polyline handle, joined count and not_joined handles.',
      { handles, tolerance: z.number().min(0).optional() }),

    cadTool(b, 'erase_entities',
      'Erase entities by handle (max 1000 per call; the plugin refuses more with TOO_MANY_ENTITIES). ' +
        'Erasing objects chosen by a filter/condition (e.g. "everything on layer A-TEMP") or more than 20 objects REQUIRES confirmation: ' +
        'call count_entities/list_entities, tell the user the number, ask, and erase only after they explicitly agree. ' +
        '3D solids, surfaces and meshes that existed before this turn are refused ("Confirmation required") until the user presses Đồng ý ' +
        'on a request_confirmation card listing their handles.',
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

    // nâng cấp đợt B (Drawing/Edit2DTools.cs)
    cadTool(b, 'stretch_entities',
      'STRETCH: crossing window corner1 + corner2 (or polygon); vertices / ends inside it move by from → to (or displacement), the rest stays ' +
        '(objects wholly inside just move). Without handles: everything in the window (max 20 objects, else the plugin lists them to ask about); ' +
        'with handles: only those. 3D objects partly inside are skipped.' + CONFIRM,
      {
        handles: handles.optional(),
        corner1: point.optional(), corner2: point.optional(),
        polygon: points(3, 'crossing polygon').max(1000).optional(), // KT2-25: the plugin takes at most 1000 points
        from: point.optional(), to: point.optional(), displacement: point.optional(),
      }),

    cadTool(b, 'break_entity',
      'BREAK a curve: at "point" only = split in two there (not for closed curves); point + point2 = remove the part between them ' +
        '(closed curve: from point to point2 counter-clockwise / in its direction). Points are snapped onto the curve. The pieces get new handles.' + CONFIRM,
      { handle, point, point2: point.optional() }),

    cadTool(b, 'lengthen_entity',
      'LENGTHEN a line, arc or open polyline at one end: mode delta (value mm added, negative shortens; default), total (new length) or percent ' +
        '(of the current length). end: start | end (default), or near_point picks the end nearest to it. Same handle.' + CONFIRM,
      {
        handle,
        mode: z.enum(['delta', 'total', 'percent']).optional(),
        value: z.number(),
        end: z.enum(['start', 'end']).optional(),
        near_point: point.optional(),
      }),

    cadTool(b, 'divide_entity',
      'DIVIDE (count = N equal parts) or MEASURE (segment_length, from start or end) a curve: places points, or block_name inserts that block ' +
        '(align_block default true = rotated along the curve). The curve is unchanged. Points show per the point style (PDMODE).',
      {
        handle,
        count: z.number().int().min(2).max(10000).optional(),
        segment_length: z.number().positive().optional(),
        from: z.enum(['start', 'end']).optional(),
        block_name: z.string().optional(),
        align_block: z.boolean().optional(),
      }),

    cadTool(b, 'group_entities',
      'GROUP: action create (default) groups handles under name (optional, auto CLAUDE_GROUP_n; optional description); action ungroup removes ' +
        'the group by name, or the groups of the handles. Objects are not changed.',
      {
        action: z.enum(['create', 'ungroup']).optional(),
        handles: handles.optional(),
        name: z.string().optional(),
        description: z.string().optional(),
      }),

    cadTool(b, 'match_properties',
      'MATCHPROP: copy properties from source to handles: ' + MATCH_PROPS_GENERAL.join(', ') + MATCH_PROPS_TEXT_DESC + CONFIRM,
      {
        source: handle,
        handles,
        properties: z.array(z.enum([...MATCH_PROPS_GENERAL, ...MATCH_PROPS_TEXT])).optional(),
      }),
  ];
}
