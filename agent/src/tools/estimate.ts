import { z } from 'zod';
import { cadTool, handles, point, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt G3 (25/11/2026): "lập dự toán". The quantities come from an existing schedule (volume_schedule on 3D,
 * block_schedule / area_schedule on 2D: the plugin checks that schedule's plan), the unit prices only from the user.
 * Not always loaded (deferred).
 */
export function estimateTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'cost_estimate',
      'COST ESTIMATE ("lập dự toán"): quantities of a schedule (quantities volume = m³ by layer of the 3D solids, blocks = block counts, ' +
        'areas = room / layer areas; quantities_params = that schedule\'s filters) × the unit prices THE USER GAVE → amounts, subtotal, total. ' +
        'Never invent, estimate or look up a price: a row without the user\'s price stays empty, is not in the total and is listed in missing_prices ' +
        '(ask the user for it). Prices match rows by name (accents / case do not matter; "A-WALL*" patterns). extra: percent lines (VAT…) only when the user says. ' +
        'excel writes an .xlsx with formulas (amount = quantity × price, SUM), so prices changed in Excel update; an existing file needs the card ' +
        '(confirm_handles after Đồng ý). table_position also draws the table.',
      {
        quantities: z.enum(['volume', 'blocks', 'areas']).describe('Which schedule gives the quantities.'),
        quantities_params: z.record(z.string(), z.unknown()).optional().describe('Filters of the source schedule, e.g. {"layers": ["A-WALL-3D"]} or {"group_by": "room"}.'),
        prices: z.array(z.object({ item: z.string().min(1), unit_price: z.number().min(0), unit: z.string().optional() })).max(500).optional()
          .describe('Unit prices exactly as the user gave them (per m³ / m² / cái of that row).'),
        extra: z.array(z.object({ name: z.string().min(1), percent: z.number().min(0).max(100) })).max(5).optional()
          .describe('Lines in percent of the subtotal (e.g. Thuế VAT 10), only when the user asks.'),
        quantity: z.enum(['net', 'gross']).optional().describe('Volumes: net (default, common parts counted once) or gross.'),
        currency: z.string().max(20).optional().describe('Default "đồng".'),
        excel: z.union([z.boolean(), z.string()]).optional().describe('true = <drawing>-du-toan.xlsx next to the drawing, or a file name.'),
        confirm_handles: handles.max(10).optional().describe('After Đồng ý on the card: the handles its error listed.'),
        table_position: point.optional().describe('Also draw the table here (2D).'),
        title: z.string().optional(),
        text_height: z.number().positive().optional(),
        layer: z.string().optional(),
      }),
  ];
}
