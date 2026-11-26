import { z } from 'zod';
import { cadTool, excelParams, handles, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H8 (docs/ban-giao/nang-cap-h8.md): structure. build_footing (3D / Full), draw_rebar and rebar_schedule
 * (2D / Full), all deferred. The numbers (approved 26/11/2026) live in the plugin (H8Numbers, Core/Rebar.cs); the
 * descriptions repeat the defaults the user sees. The plugin never designs: the main bars come from the user.
 */
export const FOOTING_TOOL_NAMES = ['build_footing'] as const;
export const REBAR_TOOL_NAMES = ['draw_rebar', 'rebar_schedule'] as const;

const xy = z.array(z.number()).min(2).max(3).describe('Point [x, y] in mm (current UCS).');
const bars = z.string().regex(/^\s*\d+\s*(Ø|ø|Φ|φ|D|d|phi|fi)\s*\d+([.,]\d+)?\s*$/).describe('Count and diameter, e.g. "2Ø18".');
const spaced = z.string().regex(/^\s*(Ø|ø|Φ|φ|D|d|phi|fi)\s*\d+([.,]\d+)?\s*(a|@|s)\s*\d+([.,]\d+)?\s*$/).describe('Diameter and spacing, e.g. "Ø8a200".');

export function structureTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'build_footing',
      'Pad (móng đơn) or strip (móng băng) footings in 3D with lean concrete under them (100 thick, 100 wider each side) on S-FOOT-3D / S-LEAN-3D. ' +
        'Sizes the user does not give are samples (pad 1200 × 1200, base 350; strip 1000 wide, 300 high, rib 300 × 600): say "cỡ mẫu, kỹ sư quyết". ' +
        'A drawing aid, no structural design; volume_schedule gives the concrete.',
      {
        kind: z.enum(['pad', 'strip']),
        positions: z.array(xy).min(1).max(500).optional().describe('pad: the centres.'),
        size: z.array(z.number().positive()).min(1).max(2).optional().describe('pad: [B, L] mm.'),
        height: z.number().positive().optional().describe('Base / strip height (mm).'),
        column: z.array(z.number().positive()).length(2).optional().describe('pad: the neck [b, h] (mm), with pedestal_height.'),
        pedestal_height: z.number().positive().optional(),
        path_points: z.array(xy).min(2).max(2001).optional().describe('strip: its centre line.'),
        closed: z.boolean().optional(),
        width: z.number().positive().optional().describe('strip width (mm).'),
        rib: z.union([z.array(z.number().positive()).length(2), z.literal(false)]).optional().describe('strip: the rib [b, h] (mm), false = none.'),
        bottom_z: z.number().optional().describe('Bottom of the footing (mm).'),
        lean: z.boolean().optional().describe('Lean concrete under it (default true).'),
      }),
    cadTool(b, 'draw_rebar',
      'Draw the reinforcement of a beam (elevation + section), a column section or a slab panel in 2D, with a mark bubble per bar kind for rebar_schedule. ' +
        'The main bars must come from the user ("dầm 2Ø18 dưới, 2Ø16 trên", "cột 8Ø20"): never choose them. Samples when not given: stirrups Ø6 a150 near the supports / a200 in the middle, ' +
        'column ties Ø6 a200, slab Ø8 a200. Cover: slab 20, beam 25, column 25 mm; anchorage 30d, lap 40d (column 35d); plain bars (≤ Ø10, CB240-T) get 180° hooks, ribbed CB400-V beam bars an L bend of 10d at each anchored end; ' +
        'scales 1:20 sections / 1:50 elevation and slab. These are defaults: the engineer checks them against the TCVN in force. A drawing aid, no structural design.',
      {
        element: z.enum(['beam', 'column', 'slab']),
        position: xy.describe('Lower left corner (mm, current UCS).'),
        width: z.number().positive().describe('Beam / column width b, or slab width along x (mm).'),
        height: z.number().positive().optional().describe('Beam / column section height h (mm).'),
        span: z.number().positive().optional().describe('beam: clear span (mm).'),
        length: z.number().positive().optional().describe('column: its height (mm); slab: length along y (mm).'),
        bottom: bars.optional().describe('beam: bottom bars from the user, e.g. "2Ø18".'),
        top: bars.optional().describe('beam: top bars from the user, e.g. "2Ø16".'),
        stirrup: spaced.optional().describe('beam: stirrups near the supports, e.g. "Ø6a150".'),
        stirrup_middle: spaced.optional().describe('beam: stirrups in the middle, e.g. "Ø6a200".'),
        bars: bars.optional().describe('column: its bars from the user, e.g. "8Ø20" (even, 4 or more).'),
        tie: spaced.optional().describe('column: ties, e.g. "Ø6a200".'),
        bars_x: spaced.optional().describe('slab: bars along x, e.g. "Ø8a200".'),
        bars_y: spaced.optional().describe('slab: bars along y (default as bars_x).'),
        anchor: z.number().positive().optional().describe('beam: anchorage into each support (mm, default 30d).'),
        lap: z.number().positive().optional().describe('column: lap above (mm, default 35d).'),
        scale: z.union([z.string(), z.number().positive()]).optional().describe('Print scale for the texts, e.g. "1:50".'),
        start_mark: z.number().int().min(1).max(9999).optional().describe('First bar mark (default: after the highest in the drawing).'),
      }),
    cadTool(b, 'rebar_schedule',
      'The rebar schedule from the bars drawn by draw_rebar: per mark (element, shape, Ø, grade, length of one bar rounded to 10 mm, count, total m, kg = 0.00617 × d² × m, splices of 11.7 m stock bars with their laps added to the length and weight), ' +
        'totals per diameter and grade, total weight. No waste unless the user gives waste_percent. table_position = the table in the drawing; excel = the .xlsx. Counted from the drawing, not a structural design. A mark used for two bar kinds keeps two rows and a warning: tell the user to renumber.',
      {
        handles: handles.max(20000).optional().describe('Only these bars (default: every bar of the drawing).'),
        waste_percent: z.number().min(0).max(100).optional().describe('Add a weight + waste column.'),
        table_position: xy.optional().describe('Insert the table at this point.'),
        title: z.string().max(80).optional(),
        ...excelParams,
      }),
  ];
}
