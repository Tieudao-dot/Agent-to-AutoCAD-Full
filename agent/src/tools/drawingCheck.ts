import { z } from 'zod';
import { cadTool, confirmDesc, excelParams, handles, type AnyTool, type ToolBridge } from './common.js';

/** Nâng cấp đợt G2 (docs/ban-giao/nang-cap-g2.md): the groups of check_drawing, as in plugin Core/DrawingCheck.cs. */
export const CHECK_GROUPS = [
  'layer_zero', 'defpoints', 'hidden_layers', 'nonstandard_layers', 'not_bylayer', 'nonstandard_styles', 'text_height',
  'empty_text', 'dim_override', 'no_dimensions', 'duplicates', 'zero_length', 'far_away', 'hatch_broken', 'hatch_unassociated',
  'unnamed_blocks', 'empty_attributes', 'missing_refs', 'no_title_block', 'viewport_unlocked', 'viewport_scale', 'title_empty',
] as const;

/** The groups fix_drawing repairs (the others are only reported). */
export const FIX_GROUPS = [
  'layer_zero', 'defpoints', 'not_bylayer', 'nonstandard_styles', 'empty_text', 'dim_override', 'duplicates', 'zero_length', 'viewport_unlocked',
] as const;

/** Nâng cấp đợt G2: check the drawing before printing / sending it (read only), repair a group after the card (2D / Full plans). */
export function drawingCheckTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'check_drawing',
      'Check the drawing before printing / sending it ("soát bản vẽ"); changes nothing. Reads model and layouts and returns the problems ' +
        'by group {id, name (Vietnamese, for the user), level (nên sửa / cân nhắc / gợi ý), count, examples (handles), fix (a fix_drawing id or null), hint}: ' +
        'layers (layer 0, Defpoints, hidden layers holding objects, names outside the standard), colour / linetype / lineweight not ByLayer, ' +
        'styles and text heights outside the standard, empty texts, overridden dimension values, walls without dimensions, duplicates, zero length, ' +
        'objects far away, broken / unassociated hatches, unnamed blocks, empty attributes, missing xrefs / images, sheets without title block, ' +
        'unlocked or non-standard viewports, empty title cells. Names / styles / heights are only checked when the drawing uses setup_standards ' +
        '(notes say what was skipped). Also scanned / total. excel: also write the list to .xlsx.',
      {
        groups: z.array(z.enum(CHECK_GROUPS)).optional().describe('Only these groups (default all).'),
        layouts: z.boolean().optional().describe('Also check the layouts (default true).'),
        examples: z.number().int().min(1).max(50).optional().describe('Example handles per group (default 5).'),
        ...excelParams,
      },
      { readOnly: true }),

    cadTool(b, 'fix_drawing',
      'Repair one group found by check_drawing: layer_zero / defpoints (move to layer, which the user chooses), not_bylayer (back to ByLayer), ' +
        'nonstandard_styles (the style of the standard), empty_text / duplicates (extra copies) / zero_length (erase), dim_override (measured value again), ' +
        'viewport_unlocked (lock). Objects on locked layers are left out. The first call changes nothing and refuses with one token for the card: ' +
        'call request_confirmation with it and one Vietnamese sentence (what, how many), end the turn; after Đồng ý call again with the same parameters ' +
        'and confirm_handles. One U undoes it.',
      {
        fix: z.enum(FIX_GROUPS),
        layer: z.string().optional().describe('layer_zero / defpoints: the layer the objects go to (must exist).'),
        handles: handles.max(2000).optional().describe('Only these objects of the group (default the whole group).'),
        confirm_handles: handles.max(50).optional().describe(confirmDesc),
      },
      { destructive: true }),
  ];
}
