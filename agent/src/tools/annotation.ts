import { z } from 'zod';
import { angle, cadTool, entityProps, handle, point, points, type AnyTool, type ToolBridge } from './common.js';

const RET = ' Returns {"handles": [...]}.';

const justify = z
  .string()
  .optional()
  .describe('Justification: left (default), center, right, middle_center, top_left, top_center, top_right, middle_left, middle_right, bottom_left, bottom_center, bottom_right.');

const dimCommon = {
  dim_style: z.string().optional().describe('Dimension style name (default: current). Create one with create_dim_style.'),
  text: z.string().optional().describe('Text override. Omit for the measured value; "<>" stands for the measured value, e.g. "<> (thông thủy)".'),
  ...entityProps,
};

/** Spec §7.2 — annotation and dimensions. */
export function annotationTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'add_text',
      'Add single-line TEXT (DBText). height is the model-space height in mm (for a 1:100 print with 2.5 mm letters use 250). ' +
        'Use a TrueType style (e.g. Arial) for Vietnamese diacritics. No line breaks — use add_mtext for multi-line.' + RET,
      {
        position: point.describe('Insertion / alignment point.'),
        text: z.string().min(1),
        height: z.number().positive().optional().describe('Text height in mm (default: style height or TEXTSIZE).'),
        rotation: angle('Text rotation').optional(),
        style: z.string().optional().describe('Text style name (default: current).'),
        justify,
        width_factor: z.number().positive().optional(),
        ...entityProps,
      }),

    cadTool(b, 'add_mtext',
      'Add multi-line MTEXT. Line breaks with "\\n". width = wrapping width in mm (0 = no wrap). justify/attachment default top_left.' + RET,
      {
        position: point,
        text: z.string().min(1),
        height: z.number().positive().optional(),
        width: z.number().min(0).optional(),
        style: z.string().optional(),
        justify,
        rotation: angle('Rotation').optional(),
        ...entityProps,
      }),

    cadTool(b, 'add_dim_linear',
      'Linear dimension between p1 and p2 with the dimension line through dim_line_point. orientation auto (default: horizontal when the dim line ' +
        'is above/below the points, vertical when left/right), horizontal, vertical; or an explicit rotation in degrees. ' +
        'Example (bottom of a 4000 wide room, 800 below): {"p1":[0,0],"p2":[4000,0],"dim_line_point":[2000,-800]}.' + RET,
      {
        p1: point, p2: point, dim_line_point: point,
        orientation: z.enum(['auto', 'horizontal', 'vertical']).optional(),
        rotation: angle('Dimension line rotation').optional(),
        ...dimCommon,
      }),

    cadTool(b, 'add_dim_aligned', 'Aligned dimension parallel to p1→p2, dimension line through dim_line_point.' + RET, {
      p1: point, p2: point, dim_line_point: point, ...dimCommon,
    }),

    cadTool(b, 'add_dim_angular',
      'Angular dimension: either 3 points (center vertex, p1, p2) or two LINE handles (line1_handle, line2_handle). arc_point (optional) places the arc.' + RET,
      {
        center: point.optional(), p1: point.optional(), p2: point.optional(),
        line1_handle: handle.optional(), line2_handle: handle.optional(),
        arc_point: point.optional(),
        ...dimCommon,
      }),

    cadTool(b, 'add_dim_radial', 'Radius dimension of a CIRCLE or ARC (entity_handle). angle (degrees) places the leader; leader_length in mm.' + RET, {
      entity_handle: handle, leader_length: z.number().min(0).optional(), angle: angle('Leader direction').optional(), ...dimCommon,
    }),

    cadTool(b, 'add_dim_diameter', 'Diameter dimension of a CIRCLE or ARC (entity_handle).' + RET, {
      entity_handle: handle, leader_length: z.number().min(0).optional(), angle: angle('Leader direction').optional(), ...dimCommon,
    }),

    cadTool(b, 'add_dim_ordinate',
      'Ordinate dimension (X or Y coordinate of feature_point, measured from origin, default [0,0]); use_x=true for X datum. Useful for levels/coordinates.' + RET,
      { feature_point: point, leader_end: point, use_x: z.boolean().optional(), origin: point.optional(), ...dimCommon }),

    cadTool(b, 'add_dim_continue',
      'Continued dimension chain (DIMCONTINUE) from an existing linear/aligned dimension: each point in points[] adds a dimension from the previous ' +
        'second extension point, on the same dimension line. Ideal for axis grids and wall openings.' + RET,
      { base_dim_handle: handle, points: points(1, 'next extension points'), ...entityProps }),

    cadTool(b, 'add_dim_baseline',
      'Baseline dimensions (DIMBASELINE) from the first extension point of an existing linear/aligned dimension to each point, stacked by spacing ' +
        '(default DIMDLI x DIMSCALE). Typical overall dimension above a chain.' + RET,
      { base_dim_handle: handle, points: points(1, 'extension points'), spacing: z.number().positive().optional(), ...entityProps }),

    cadTool(b, 'add_mleader',
      'Multileader with arrow at arrow_point and MTEXT at text_point, e.g. pipe diameter "D110" or cable size notes. Line breaks with "\\n".' + RET,
      {
        arrow_point: point, text_point: point, text: z.string().min(1),
        height: z.number().positive().optional(), style: z.string().optional().describe('Multileader style name.'),
        text_style: z.string().optional(), arrow_size: z.number().positive().optional(),
        ...entityProps,
      }),

    cadTool(b, 'add_table',
      'Insert a TABLE (schedules, coordinate tables). position = top-left corner. data[r][c] strings/numbers; rows/cols default to data size; ' +
        'col_widths in mm; optional title row (merged); text_height mm; alignment left/center/right.' + RET,
      {
        position: point,
        rows: z.number().int().min(1).max(1000).optional(),
        cols: z.number().int().min(1).max(100).optional(),
        col_widths: z.array(z.number().positive()).optional(),
        row_height: z.number().positive().optional(),
        data: z.array(z.array(z.union([z.string(), z.number()]))).optional(),
        title: z.string().optional(),
        text_height: z.number().positive().optional(),
        alignment: z.enum(['left', 'center', 'right', 'top_left', 'top_center']).optional(),
        ...entityProps,
      }),
  ];
}
