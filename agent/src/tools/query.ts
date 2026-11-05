import { z } from 'zod';
import { cadTool, color, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/** Filter shared by list_entities and count_entities. */
export const entityFilter = {
  types: z
    .array(z.string())
    .optional()
    .describe('Entity types: LINE, LWPOLYLINE (alias POLYLINE), CIRCLE, ARC, ELLIPSE, SPLINE, POINT, XLINE, RAY, HATCH, TEXT, MTEXT, ' +
      'ANYTEXT (= TEXT+MTEXT), INSERT (alias BLOCK), DIMENSION, MULTILEADER, TABLE, WIPEOUT.'),
  layers: z.array(z.string()).optional().describe('Layer names; wildcards * and ? allowed, e.g. ["A-*"].'),
  color: color.optional().describe('Effective ACI color (ByLayer is resolved through the layer), e.g. 1 for red.'),
  window: z
    .object({
      p1: point,
      p2: point,
      crossing: z.boolean().optional().describe('false (default): entity fully inside; true: touching/crossing the window.'),
    })
    .optional()
    .describe('Rectangular selection window in WCS.'),
};

/** Spec §7.6 — reading and querying. */
export function queryTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'get_drawing_info',
      'Read the state of the open drawing: file name, units (should be millimeters), entity count by type and by layer, extents (min/max), ' +
        'current layer / text style / dim style, layer and block counts, LTSCALE, DIMSCALE. Call this FIRST at the start of every task.',
      {},
      { readOnly: true }),

    cadTool(b, 'list_entities',
      'List model-space entities matching all given filters (types AND layers AND color AND window). Returns a compact summary per entity: ' +
        'handle, type, layer, non-ByLayer color and key numbers (line start/end/length, polyline vertices/closed/length/area, circle center/radius, ' +
        'text content/height/position, block name/position...). Default limit 200 (max 2000); use offset to page. ' +
        'Use get_entity for full geometry. Always use this (or find_text) to get real handles before modifying anything.',
      {
        ...entityFilter,
        limit: z.number().int().min(1).max(2000).optional(),
        offset: z.number().int().min(0).optional(),
      },
      { readOnly: true }),

    cadTool(b, 'count_entities',
      'Count entities matching the same filters as list_entities, without details: {"total", "by_type", "by_layer"}. ' +
        'Use it to report how many objects a destructive request would affect before asking the user to confirm.',
      entityFilter,
      { readOnly: true }),

    cadTool(b, 'get_entity',
      'Full properties of one entity (handle) or up to 100 (handles): all vertices and bulges, text and style, dimension points, block attributes, ' +
        'color/linetype/lineweight, bounding box.',
      { handle: handle.optional(), handles: handles.max(100).optional() },
      { readOnly: true }),

    cadTool(b, 'find_text',
      'Find TEXT, MTEXT, block ATTRIBUTE values, dimension text overrides, multileader text and table cells containing a string. ' +
        'Case-insensitive by default. Returns handle, type, full text, layer, position (and block_handle + tag for attributes, row/col for table cells). ' +
        'Use with edit_text (find + replace_with) to rename labels, e.g. "WC" → "VỆ SINH".',
      {
        query: z.string().min(1),
        match_case: z.boolean().optional(),
        whole_word: z.boolean().optional().describe('Only match the query as a whole word (e.g. "WC" but not "WCC").'),
        layers: z.array(z.string()).optional(),
        limit: z.number().int().min(1).max(5000).optional(),
      },
      { readOnly: true }),

    cadTool(b, 'measure_distance', 'Distance between two points in mm (and m), with dx, dy and angle in degrees.', { p1: point, p2: point }, { readOnly: true }),

    cadTool(b, 'measure_area',
      'Area and perimeter of a closed entity (closed polyline, circle, ellipse, hatch, region) by handle, or of a polygon given by points. ' +
        'Returns area_mm2, area_m2, perimeter_mm, perimeter_m. Report areas to the user in m².',
      { handle: handle.optional(), points: points(3).optional() },
      { readOnly: true }),

    cadTool(b, 'get_bounding_box',
      'Combined bounding box (min, max, width, height, center) of entities, plus each entity box when <= 50 handles.',
      { handles },
      { readOnly: true }),
  ];
}
