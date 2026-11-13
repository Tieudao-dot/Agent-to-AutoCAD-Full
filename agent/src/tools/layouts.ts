import { z } from 'zod';
import { cadTool, entityProps, handle, point, type AnyTool, type ToolBridge } from './common.js';

const scale = z
  .union([z.string(), z.number().positive()])
  .describe('Scale as "1:100" (paper:model), or 100 (= 1:100), or 0.01.');

/** Phase 2 — layouts / paper space, viewports, PDF plotting. */
export function layoutTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'list_layouts',
      'List layouts (tabs): name, tab order, current, plot device, paper (canonical media name, size in mm), number of viewports and entities. ' +
        'Paper-space coordinates are mm on the sheet (1 unit = 1 mm printed).',
      {},
      { readOnly: true }),

    cadTool(b, 'create_layout',
      'Create a new layout (tab), optionally copying an existing one (copy_from) and setting device/media (default DWG To PDF.pc3 + ISO A3 full bleed). ' +
        'To draw a title block on it, pass "layout": "<name>" to any draw/add/insert tool (coordinates in sheet mm).',
      {
        name: z.string().min(1),
        copy_from: z.string().optional(),
        device: z.string().optional(),
        media: z.string().optional().describe('Canonical media name, e.g. "ISO_full_bleed_A3_(420.00_x_297.00_MM)", "ISO_full_bleed_A1_(841.00_x_594.00_MM)". See list_plot_media.'),
        activate: z.boolean().optional().describe('Switch to the new layout. Default false.'),
      }),

    cadTool(b, 'set_current_layout', 'Switch the active tab: a layout name, or "Model" for model space.', { name: z.string().min(1) }),

    cadTool(b, 'delete_layout', 'Delete a layout (and everything on it). Destructive: ask the user to confirm first.', { name: z.string().min(1) }, { destructive: true }),

    cadTool(b, 'add_viewport',
      'Add a viewport on a paper-space layout showing model space at a given scale. Position on the sheet in mm: center + width + height, or corner1 + corner2. ' +
        'view_center = model point (mm) at the middle of the viewport. Example: A3 sheet, 1:100 plan of a 12x9 m house: ' +
        '{"layout": "MB-01", "center": [200, 150], "width": 300, "height": 230, "view_center": [6000, 4500], "scale": "1:100", "locked": true}.',
      {
        layout: z.string().min(1),
        center: point.optional(),
        width: z.number().positive().optional(),
        height: z.number().positive().optional(),
        corner1: point.optional(),
        corner2: point.optional(),
        view_center: point.optional(),
        scale: scale.optional(),
        locked: z.boolean().optional(),
        freeze_layers: z.array(z.string()).optional().describe('Layers frozen in this viewport only (VP freeze).'),
        layer: entityProps.layer,
      }),

    cadTool(b, 'set_viewport', 'Change a viewport: scale, view_center, locked, size, sheet position, per-viewport frozen/thawed layers.', {
      handle,
      scale: scale.optional(),
      view_center: point.optional(),
      locked: z.boolean().optional(),
      width: z.number().positive().optional(),
      height: z.number().positive().optional(),
      center: point.optional(),
      freeze_layers: z.array(z.string()).optional(),
      thaw_layers: z.array(z.string()).optional(),
    }),

    cadTool(b, 'plot_pdf',
      'Plot a layout (or "Model") to a PDF file with DWG To PDF.pc3 (foreground plot). Only when the user asks for a PDF / print. ' +
        'Defaults: current layout, the paper set on the layout (A3 full bleed if the device lacks it), monochrome.ctb, scale "fit", area "layout" (Model: "extents"), output next to the drawing as ' +
        '<drawing>-<layout>.pdf. area "window" needs window_p1/window_p2. layouts: several layouts (or ["all"] = every paper layout in tab order) ' +
        'into ONE multi-page PDF (default <drawing>-<n>-layouts.pdf). Returns the output path.',
      {
        layout: z.string().optional(),
        layouts: z.array(z.string().min(1)).min(1).max(255).optional(),
        output: z.string().optional().describe('Path ending in .pdf (a relative path or bare file name goes next to the drawing).'),
        media: z.string().optional(),
        device: z.string().optional(),
        plot_style: z.string().optional().describe('e.g. "monochrome.ctb", "acad.ctb".'),
        scale: z.union([z.literal('fit'), z.string(), z.number().positive()]).optional(),
        area: z.enum(['layout', 'extents', 'display', 'window']).optional(),
        window_p1: point.optional(),
        window_p2: point.optional(),
        center: z.boolean().optional(),
        landscape: z.boolean().optional(),
      }),

    cadTool(b, 'list_plot_media',
      'List paper sizes (canonical + readable names) of a plot device (default DWG To PDF.pc3), available plot style tables and plot devices.',
      { device: z.string().optional(), filter: z.string().optional().describe('e.g. "A3" or "A1".') },
      { readOnly: true }),
  ];
}
