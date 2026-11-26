import { z } from 'zod';
import { cadTool, excelParams, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H3 (docs/ban-giao/nang-cap-h3.md): compare the open drawing with another DWG / DXF file (read only).
 * Deferred (loaded when needed).
 */
export function compareTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'compare_drawings',
      'Compare the open drawing with another .dwg / .dxf ("so với bản cũ / bản sửa"); the other file is only read, never changed. ' +
        'Model space objects are matched by handle when both come from one file, else by geometry (match says which; tell the user): ' +
        'added / removed / changed with what changed (geometry, layer, colour, text, block, attributes; 3D solids by box and volume on 3D / Full). ' +
        'mark: revision clouds on layer X-SO-SANH (green added, red removed, yellow changed; deleting the layer removes them); ' +
        'many clouds need the card (the error gives the token; then confirm_handles). excel: the whole list. Notes say what was left out.',
      {
        file: z.string().min(1).max(260).describe('Full path of the other drawing (or of a file attached in the chat).'),
        other_is: z.enum(['old', 'new']).optional().describe('The other file is the older version (default) or the newer one.'),
        match: z.enum(['auto', 'handle', 'geometry']).optional(),
        tolerance: z.number().positive().max(1000).optional().describe('mm (default 1).'),
        mark: z.boolean().optional().describe('Ring the differences with clouds (default false: only the list).'),
        ...excelParams,
      },
      // sửa KTH (nghi vấn): mark draws clouds (and excel writes a file), so it is not declared read-only (a read-only tool may
      // be run at the same time as another)
      { readOnly: false }),
  ];
}
