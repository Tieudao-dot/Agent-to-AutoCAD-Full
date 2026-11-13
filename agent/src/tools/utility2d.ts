import { z } from 'zod';
import { cadTool, entityProps, handles, point, type AnyTool, type ToolBridge } from './common.js';

const table = {
  table_position: point.optional().describe('Insert the result as a TABLE with its top-left corner here (omit = only return the numbers).'),
  title: z.string().optional(),
  text_height: z.number().positive().optional().describe('Table text height in drawing units (default 2.5 mm on paper × DIMSCALE).'),
  layout: z.string().optional().describe('Space to read and to put the table in (default Model).'),
  layer: z.string().optional(),
};

/**
 * Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c.md): 2D utilities (2D / Full plans). draw_hatch pick_point is in
 * draw.ts, plot_pdf layouts in layouts.ts.
 */
export function utility2dTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'dim_floor_plan',
      'Dimension a whole floor plan: chains of linear dimensions outside each side (bottom, top, left, right): row 1 = wall corners and ' +
        'openings seen from that side, row 2 = axes (if axis_layers / axis_handles), row 3 = overall size. Current dimension style unless ' +
        'dim_style. Walls: wall_layers (default ["A-WALL"]) or wall_handles (lines / polylines). offset = distance of row 1 from the walls ' +
        '(drawing units; default 8 mm on paper × DIMSCALE, at least 5 % of the plan), spacing between rows (default = offset). Returns {"handles": [...], "by_side_row"}.',
      {
        wall_layers: z.array(z.string()).optional(),
        wall_handles: handles.optional(),
        axis_layers: z.array(z.string()).optional(),
        axis_handles: handles.optional(),
        sides: z.array(z.enum(['bottom', 'top', 'left', 'right'])).optional(),
        offset: z.number().positive().optional(),
        spacing: z.number().positive().optional(),
        dim_style: z.string().optional(),
        ...entityProps,
      }),

    cadTool(b, 'block_schedule',
      'Count block references by name (dynamic blocks by their own name; no xrefs), optionally by the value of one attribute ' +
        '(group_by_attribute, e.g. "MA" to count doors per code). names / layers: filters (wildcards). Returns {"total", "rows": [{name, value?, count}]}; ' +
        'with table_position also inserts the schedule as a table (STT, name, value, count, total row).',
      {
        names: z.array(z.string()).optional(),
        layers: z.array(z.string()).optional(),
        group_by_attribute: z.string().optional(),
        ...table,
      }),

    cadTool(b, 'area_schedule',
      'Areas of closed regions (closed polylines, circles, ellipses, hatches, regions; a hatch drawn on a room polyline counts once) by layer ' +
        '(group_by "layer", default) or by room (group_by "room": the text inside each region is its name, e.g. "P.NGỦ 1"). layers / handles: which ' +
        'regions; text_layers: which texts name rooms; min_area in m². Returns {"total_m2", "rows"}; with table_position also a table with the total.',
      {
        group_by: z.enum(['layer', 'room']).optional(),
        layers: z.array(z.string()).optional(),
        handles: handles.optional(),
        text_layers: z.array(z.string()).optional(),
        min_area: z.number().min(0).optional(),
        decimals: z.number().int().min(0).max(4).optional(),
        allow_nested: z.boolean().optional().describe('Insert the table even when an area contains others (counted twice); only after asking.'),
        ...table,
      }),

    cadTool(b, 'find_replace_text',
      'Find and replace text in TEXT, MTEXT, block attributes, dimension text overrides, multileaders and table cells. ALWAYS two steps: ' +
        '(1) call without handles = preview, nothing changes: returns the count, examples (before / after) and match_handles; (2) call ' +
        'request_confirmation with match_handles and one Vietnamese sentence (count, find → replace, an example) and end the turn; after the ' +
        'user presses Đồng ý call again with handles = match_handles. Without the card the plugin refuses. replace "" deletes the found text.',
      {
        find: z.string().min(1),
        replace: z.string(),
        match_case: z.boolean().optional(),
        whole_word: z.boolean().optional(),
        types: z.array(z.enum(['text', 'mtext', 'attribute', 'dimension', 'mleader', 'table'])).optional(),
        layers: z.array(z.string()).optional(),
        layout: z.string().optional(),
        handles: handles.max(5000).optional().describe('Only in step 2: the match_handles the user approved.'),
        limit: z.number().int().min(1).max(500).optional().describe('Examples in the preview (default 30).'),
      }),
  ];
}
