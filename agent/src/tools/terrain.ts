import { z } from 'zod';
import { cadTool, handle, wcsPoint, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H6 (docs/ban-giao/nang-cap-h6.md): hạ tầng — survey points from a sheet, contours, chainage ticks, long
 * and cross sections. 2D / Full, deferred. Only the drawing changes (no file is written): no card. The default numbers
 * live in the plugin (H6Numbers); approved by the owner 26/11 (docs/dot-h-so-tieu-chuan.md), repeated in the description.
 */
export const TERRAIN_ACTIONS = ['import_points', 'contours', 'stations', 'profile', 'cross_sections'] as const;

export function terrainTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'terrain',
      'Infrastructure / survey drawings (hạ tầng), a drawing aid: the engineer checks it; no cut / fill volumes. ' +
        'import_points {file}: survey sheet .csv / .txt / .xlsx (columns from the header X / Y / Z / Tên / Mã, or by position name, X, Y, Z); VN-2000: X = North → drawing y, Y = East → drawing x, metres in the drawing units (× 1000 in a mm drawing), ' +
        'points at their level on C-SURVEY-PT with level and name texts. Real coordinates: ask the user, then origin "real" / "auto" / [X, Y]. ' +
        'contours: from those points (TIN), major lines labelled. stations {alignment_handle}: chainage ticks on the plan. ' +
        'profile {alignment_handle}: long section with its table (design: [[chainage, level], …] optional). cross_sections {alignment_handle}: at the stakes. ' +
        'Defaults: profile 1:1000 / 1:100, cross sections 1:200 and 15 m each side, stakes every 20 m (H every 100 m, Km every 1 000 m), plan ticks every 50 m, ' +
        'datum (MSS) whole metres at least 2 m below the lowest level, contour interval by plan scale (1:500 → 0.5 m, 1:1000 / 1:2000 → 1 m), a major line every 5. ' +
        'The contour interval and the 20 m stakes are defaults: the engineer checks them against the TCVN in force. The result gives the numbers used: tell the user.',
      {
        action: z.enum(TERRAIN_ACTIONS),
        file: z.string().min(1).max(260).optional().describe('import_points: full path of the survey sheet.'),
        origin: z.union([z.enum(['auto', 'real']), z.array(z.number()).length(2)]).optional()
          .describe('import_points: local origin [X, Y] in survey metres, "auto" (lowest X / Y rounded down to 100 m) or "real".'),
        swap_axes: z.boolean().optional().describe('import_points: false when the sheet is not VN-2000 (X already East).'),
        columns: z.object({ name: z.number().int().min(1).optional(), x: z.number().int().min(1), y: z.number().int().min(1), z: z.number().int().min(1).optional(), code: z.number().int().min(1).optional() })
          .optional().describe('import_points: column numbers (from 1) when the sheet has no clear header.'),
        labels: z.boolean().optional().describe('Write the level texts (import_points) / label major contours (default true).'),
        names: z.boolean().optional().describe('import_points: write point names (default true).'),
        points_layer: z.string().min(1).max(255).optional().describe('Layer of the survey points (default C-SURVEY-PT).'),
        scale: z.number().int().min(1).optional().describe('Print scale of the plan (1:scale): text heights, default contour interval.'),
        interval: z.number().positive().optional().describe('contours: interval in metres.'),
        major_every: z.number().int().min(0).max(100).optional().describe('contours: every n-th line is a major one (0 = none).'),
        max_edge: z.number().min(0).optional().describe('contours / sections: triangles with a longer edge (m) are left out; 0 keeps all.'),
        alignment_handle: handle.optional().describe('stations / profile / cross_sections: the centre line (polyline / line / arc).'),
        start_chainage: z.number().optional().describe('Chainage of the start of the alignment in metres (default 0).'),
        step: z.number().positive().optional().describe('Stake spacing in metres.'),
        position: wcsPoint.optional().describe('profile / cross_sections: lower-left corner (WCS, drawing units); default below the drawing.'),
        scale_h: z.number().int().min(1).optional().describe('profile: horizontal scale 1:n.'),
        scale_v: z.number().int().min(1).optional().describe('profile: vertical scale 1:n.'),
        datum: z.number().optional().describe('profile: datum level (mức so sánh) in metres.'),
        design: z.array(z.array(z.number()).length(2)).min(2).max(500).optional().describe('profile: design line [[chainage m, level m], …].'),
        title: z.string().min(1).max(120).optional().describe('profile: title.'),
        scale_cross: z.number().int().min(1).optional().describe('cross_sections: scale 1:n.'),
        half_width: z.number().positive().optional().describe('cross_sections: width each side of the centre line in metres.'),
        stakes: z.array(z.string().min(1).max(20)).min(1).max(200).optional().describe('cross_sections: only these stakes (names as in the profile, e.g. "Km0", "H1", "C3").'),
      }),
  ];
}
