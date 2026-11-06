import { z } from 'zod';
import { cadTool, color, type AnyTool, type ToolBridge } from './common.js';

/** Spec §7.4 — text and dimension styles. */
export function styleTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'create_text_style',
      'Create (or update) a text style. font: TrueType face name ("Arial", "Times New Roman" — required for Vietnamese diacritics) or an SHX file ' +
        '("romans.shx"). height 0 (default) = choose per text. width_factor default 1.',
      {
        name: z.string().min(1),
        font: z.string().min(1),
        height: z.number().min(0).optional(),
        width_factor: z.number().positive().optional(),
        oblique_angle: z.number().optional(),
        bold: z.boolean().optional(),
        italic: z.boolean().optional(),
        bigfont: z.string().optional().describe('SHX big font file (only with SHX fonts).'),
      }),

    cadTool(b, 'create_dim_style',
      'Create (or update) a dimension style, starting from the current dimension variables with metric defaults (decimal mm, 0 decimals, text above). ' +
        'Sizes are PAPER sizes in mm, multiplied by scale (DIMSCALE): for a 1:100 drawing use scale 100, text_height 2.5, arrow_size 1.5 (arrow "archtick" ' +
        'for Vietnamese architectural drawings), extension_offset 1.5, extension_beyond 1.5. Then set_current_styles.',
      {
        name: z.string().min(1),
        text_height: z.number().positive().optional(),
        arrow_size: z.number().min(0).optional(),
        extension_offset: z.number().min(0).optional().describe('DIMEXO: gap between object and extension line.'),
        extension_beyond: z.number().min(0).optional().describe('DIMEXE: extension beyond the dimension line.'),
        scale: z.number().positive().optional().describe('DIMSCALE (print scale denominator, e.g. 100 for 1:100).'),
        text_style: z.string().optional(),
        arrow: z.enum(['closed_filled', 'archtick', 'oblique', 'dot', 'dotsmall', 'open', 'open30', 'open90', 'closed', 'closed_blank', 'integral', 'none', 'origin']).optional(),
        text_gap: z.number().min(0).optional().describe('DIMGAP.'),
        dim_line_extension: z.number().min(0).optional().describe('DIMDLE (with ticks).'),
        baseline_spacing: z.number().min(0).optional().describe('DIMDLI.'),
        decimals: z.number().int().min(0).max(8).optional(),
        linear_scale: z.number().positive().optional().describe('DIMLFAC (e.g. 0.001 to show metres).'),
        fixed_extension_length: z.number().positive().optional(),
        text_position: z.enum(['above', 'centered', 'outside']).optional(),
        color: color.optional(),
      }),

    cadTool(b, 'set_current_styles', 'Make a text style, dimension style and/or multileader style current.', {
      text_style: z.string().optional(),
      dim_style: z.string().optional(),
      mleader_style: z.string().optional(),
    }),

    cadTool(b, 'list_styles',
      'List text styles (font, height, width factor), dimension styles (text height, arrow, scale) and multileader styles, with the current ones.',
      {},
      { readOnly: true }),
  ];
}
