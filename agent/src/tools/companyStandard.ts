import { z } from 'zod';
import { cadTool, confirmDesc, handles, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H4 (docs/ban-giao/nang-cap-h4.md): "học chuẩn công ty từ bản vẽ mẫu". 2D / Full (like setup_standards),
 * deferred: loaded when needed.
 */
export const COMPANY_ACTIONS = ['learn', 'list', 'show', 'delete'] as const;

export function companyStandardTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'company_standard',
      'Company standards kept on this computer, learnt from a company template drawing (.dwg / .dwt / .dxf; the file is only read). ' +
        'learn {file, name}: layers (color, linetype, lineweight, description), text / dimension / multileader styles, title blocks (blocks with attributes on a layout), paper, units, text heights. ' +
        'list; show {name}; delete {name} (card). Learning again under the same name replaces it (card). ' +
        'Then use company: <name> in setup_standards (apply), check_drawing (check against it), create_sheet (its paper and title block; attributes = tag → text).',
      {
        action: z.enum(COMPANY_ACTIONS),
        file: z.string().min(1).max(260).optional().describe('learn: full path of the template.'),
        name: z.string().min(1).max(60).optional().describe('The standard (e.g. the company name).'),
        confirm_handles: handles.max(5).optional().describe(confirmDesc),
      }),
  ];
}
