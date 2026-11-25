import { z } from 'zod';
import { cadTool, handles, point, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt G1 (25/11/2026): "ra bộ hồ sơ" in one call. The plugin plans the set (Core/DrawingSet.cs) and makes it
 * with the tools that exist (section_view / projection_view for the views, create_sheet for the sheets, plot_pdf for the
 * PDF); each part only when the machine's plan / work mode has that tool's group. Not always loaded (deferred).
 */
export function drawingSetTools(b: ToolBridge): AnyTool[] {
  const xy = point.describe('[x, y] in the current UCS.');
  return [
    cadTool(b, 'create_drawing_set',
      'DRAWING SET ("ra bộ hồ sơ"): from the 3D model (and the 2D plan) makes plans (a horizontal cut 1.2 m above each storey; no solids: the 2D drawing), ' +
        'elevations (front / back / left / right), sections (default: A-A along and B-B across the middle) and a perspective, one view per sheet with the ' +
        'title block, numbered KT-01… in that order; title, sheet number, scale and paper are filled in. pdf: true also prints the set into one PDF. ' +
        'Parts the plan / work mode lacks are left out and listed in missing (a 3D machine gets the views in model space, no sheets). ' +
        'Running it again replaces the set made before only after the card (the error lists the handles; after Đồng ý call again with confirm_handles); ' +
        'the user\'s own layouts are never touched. Returns sheets, missing, skipped.',
      {
        kinds: z.array(z.enum(['plan', 'elevation', 'section', 'perspective'])).min(1).max(4).optional().describe('Default all four.'),
        elevations: z.array(z.enum(['front', 'back', 'left', 'right'])).max(4).optional().describe('Default all four; front looks +Y of the UCS.'),
        sections: z.array(z.object({ line: z.array(xy).length(2), name: z.string().max(60).optional() })).max(8).optional()
          .describe('Section lines in plan (default two through the middle); name e.g. "MẶT CẮT 1-1".'),
        plan_source: z.enum(['auto', '3d', '2d']).optional().describe('auto (default): cuts of the solids when there are some, else the 2D drawing.'),
        plan_areas: z.array(z.object({ min: xy, max: xy, title: z.string().max(60).optional() })).max(10).optional()
          .describe('2D plans drawn side by side (one per storey): their rectangles; with plan_source 2d.'),
        paper: z.enum(['A0', 'A1', 'A2', 'A3', 'A4']).optional().describe('Default A3.'),
        orientation: z.enum(['landscape', 'portrait']).optional(),
        scale: z.union([z.string(), z.number().positive()]).optional().describe('One scale for plans, elevations and sections (default: 1:100, larger when a view does not fit).'),
        prefix: z.string().max(20).optional().describe('Sheet number prefix (default KT).'),
        fields: z.object({ company: z.string(), project: z.string(), item: z.string(), drawn_by: z.string(), checked_by: z.string(), date: z.string() })
          .partial().optional().describe('Title block texts for every sheet (ask the user; leave out what they did not give).'),
        pdf: z.boolean().optional().describe('Also print the whole set into one PDF (only when the user asks).'),
        pdf_output: z.string().optional().describe('PDF path (default <drawing>-bo-ho-so.pdf next to the drawing).'),
        confirm_handles: handles.max(10).optional().describe('After Đồng ý on the card: the handles its error listed.'),
      }),
  ];
}
