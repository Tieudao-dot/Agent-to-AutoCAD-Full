import { z } from 'zod';
import { cadTool, excelParams, handles, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): MEP pipes and ducts in 3D (3D / Full plans), deferred.
 * The sizes, slopes, heights and layers of the standard live in the plugin (H7Numbers, Core/Mep.cs, approved by the owner
 * 26/11/2026); the descriptions repeat them (agent/test/nangcaph7.test.ts keeps both the same).
 */
/** The H7 tools (ToolGroup.ThreeD in the plugin). */
export const MEP_TOOL_NAMES = ['draw_mep', 'mep_report'] as const;

const SYSTEMS = ['supply', 'hot', 'drain', 'vent', 'fire', 'duct_supply', 'duct_return'] as const;

const routePoint = z
  .array(z.number())
  .min(2)
  .max(3)
  .describe('Point [x, y] (in plan; heights from elevation / under_beams, a drain falls by its slope) or [x, y, z], current UCS.');

export function mepTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'draw_mep',
      'Draw a pipe or duct route in 3D ("đi ống cấp nước…", "đi ống gió…"): straight pieces, an elbow at each corner, on the layer of its system. ' +
        'Only the size the user gives (or the default of the use: supply / hot branch D25, main D32; drain floor D60, wc / stack D110; vent D60) is drawn: the plugin never sizes pipes or works out flows; fire pipes and ducts need the size from the user. ' +
        'A drain falls 2 % by default, or the least slope of its size when more (D60 2.5 %); the result warns below that least slope; elbows 1.5 × D (ducts 1 × width); connect_to = a pipe of another route the first point is on (draws a tee). ' +
        'Returns the handles and the length; check with capture_view, then mep_report.',
      {
        system: z.enum(SYSTEMS).describe('supply = cấp nước, hot = nước nóng, drain = thoát nước, vent = thông hơi, fire = PCCC, duct_supply / duct_return = ống gió cấp / hồi.'),
        points: z.array(routePoint).min(2).max(200).describe('The route, in order (a drain: from the highest end).'),
        diameter: z.number().positive().max(2000).optional().describe('Round pipe outside diameter (mm).'),
        width: z.number().positive().max(5000).optional().describe('Duct width (mm, horizontal).'),
        height: z.number().positive().max(5000).optional().describe('Duct height (mm).'),
        use: z.enum(['branch', 'main', 'floor', 'wc', 'stack', 'vent']).optional().describe('Without diameter: the plugin default size of this use (supply / hot: branch, main; drain: floor, wc, stack).'),
        elevation: z.number().optional().describe('Height of the centre line (mm) when the points are in plan.'),
        under_beams: z.boolean().optional().describe('Top of the pipe 100 mm under the lowest beam over the route (300 mm under the slab without beams), instead of elevation.'),
        slope: z.number().min(0).max(100).optional().describe('Fall in % along the route (drains; others are level unless given).'),
        bend_radius: z.number().positive().optional().describe('Elbow centre-line radius (mm); default from the size.'),
        connect_to: z.string().regex(/^[0-9A-Fa-f]{1,16}$/).optional().describe('Handle of a straight pipe drawn by draw_mep that the first point is on: a tee there.'),
        layer: z.string().optional().describe('Layer (default: the 3D layer of the system).'),
      }),
    cadTool(b, 'mep_report',
      'Pipes and ducts drawn by draw_mep: action schedule = length by system and size, pieces, elbows, tees (excel: the table; waste_percent adds a length + waste column); ' +
        'action clash = each pipe against beams / columns (or other_handles) and other routes: "clash" (overlap) or "close" (under the clearance). ' +
        'Read only (the Excel file is the only file written).',
      {
        action: z.enum(['schedule', 'clash']).optional(),
        systems: z.array(z.enum(SYSTEMS)).optional().describe('Only these systems.'),
        waste_percent: z.number().min(0).max(100).optional().describe('schedule: add a column length × (1 + waste %).'),
        clearance: z.number().min(0).max(1000).optional().describe('clash: closer than this (mm) is "close" (default 50).'),
        other_handles: handles.max(5000).optional().describe('clash: check against these solids instead of the beam / column layers.'),
        pipes: z.boolean().optional().describe('clash: also against the pipes of other routes (default true).'),
        ...excelParams,
      }),
  ];
}
