import { z } from 'zod';
import { angle, cadTool, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

const scale = z.union([z.string(), z.number().positive()]).describe('Print scale "1:100" (or 100).');

/** Card handles: only after the user pressed Đồng ý on a card listing them (else the plugin refuses). */
const OVERWRITE = 'Handles to overwrite, only after the user approved them on a request_confirmation card.';

/**
 * Nâng cấp đợt F2 (25/11/2026): 2D used every day. setup_standards / create_sheet are 2D (text, dimensions, layouts);
 * insert_library_block / layer_state every plan, like the block and layer tools (Core/Plans.cs). Kept to four tools with
 * short descriptions: every tool costs tokens each time a chat starts.
 */
export function standards2dTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'setup_standards',
      'Set up the standard styles for a print scale: text style VN-ARIAL (KT-ARIAL with standard "ve-kien-truc"), dimension style VN-<scale> ' +
        '(arch ticks, 2.5 mm text, sized for the drawing units), multileader style VN-ML-<scale> (arrow notes for add_mleader), linetypes CENTER/DASHED/HIDDEN/PHANTOM/DASHDOT, LTSCALE; makes them current and returns ' +
        'text_heights for that scale. Safe to run again (creates only what is missing). An existing style that differs is kept: ask with request_confirmation ' +
        '(kept_handles), then call again with handles. scale default: the palette scale, else 1:100.',
      {
        scale: scale.optional(),
        standard: z.string().optional().describe('The palette "Tiêu chuẩn" skill, e.g. "ve-kien-truc".'),
        company: z.string().min(1).max(60).optional().describe('A company standard learnt with company_standard (name): its layers / styles instead of the Vietnamese ones (scale not used).'),
        make_current: z.boolean().optional(),
        handles: handles.max(50).optional().describe(OVERWRITE),
      }),

    cadTool(b, 'create_sheet',
      'Create a print layout: paper A0-A4 (DWG To PDF), frame (25 mm binding margin), title block KHUNG-TEN-VN with fields and a locked viewport at the scale ' +
        '(none: the smallest standard scale that fits the drawing). view_center: model point at the viewport middle (default: drawing centre). ' +
        'Never changes an existing layout. Print with plot_pdf.',
      {
        name: z.string().optional().describe('Layout name, default BV-01, BV-02…'),
        paper: z.enum(['A0', 'A1', 'A2', 'A3', 'A4']).optional().describe('Default A3.'),
        orientation: z.enum(['landscape', 'portrait']).optional().describe('Default landscape (A4 portrait).'),
        scale: scale.optional(),
        view_center: point.optional(),
        fields: z
          .object({
            company: z.string(), project: z.string(), item: z.string(), title: z.string(), drawn_by: z.string(),
            checked_by: z.string(), date: z.string(), scale: z.string(), sheet_no: z.string(), paper: z.string(),
          })
          .partial()
          .optional()
          .describe('Title block texts; date, scale, paper are filled automatically.'),
        locked: z.boolean().optional(),
        activate: z.boolean().optional(),
        company: z.string().min(1).max(60).optional().describe('A company standard learnt with company_standard (name): its paper and title block (copied from the template).'),
        attributes: z.record(z.string(), z.string()).optional().describe('Company title block: tag → text.'),
      }),

    cadTool(b, 'insert_library_block',
      'Insert a plan symbol from the built-in library (drawn by ClaudeCAD, mm sizes, block CUA-DI-900 etc.). Without name: list the library with sizes and base points. ' +
        'Names: cua_di, cua_di_2_canh, cua_truot, cua_so, bon_cau, lavabo, bon_tam, khay_tam, giuong, sofa, ban, ghe, tu, bep, cay. ' +
        'Doors: base at the hinge, opening along +x, open to +y (rotation / flip to place). Default layers A-DOOR, A-WIND, A-FIXT, A-FURN, L-PLNT.',
      {
        name: z.string().optional(),
        position: point.optional(),
        positions: points(1).optional(),
        width: z.number().positive().optional().describe('mm (door / window opening, tree diameter…).'),
        depth: z.number().positive().optional().describe('mm (wall thickness for windows / sliding doors, length of a bed…).'),
        rotation: angle('Rotation').optional(),
        flip: z.boolean().optional().describe('Mirror left-right (door hinge on the other side).'),
        layer: z.string().optional(),
      }),

    cadTool(b, 'layer_state',
      'Named layer states (AutoCAD LAYERSTATE): save the on/frozen/locked/plot/color/linetype/lineweight of every layer, restore, list, delete. ' +
        'Saving over an existing name or deleting needs a request_confirmation card with the handle the refusal names, then the same call with handles.',
      {
        action: z.enum(['save', 'restore', 'list', 'delete']),
        name: z.string().optional(),
        description: z.string().optional(),
        handles: handles.max(50).optional().describe(OVERWRITE),
      }),
  ];
}

