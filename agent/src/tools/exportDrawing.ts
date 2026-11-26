import { z } from 'zod';
import { cadTool, confirmDesc, handles, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt H2 (docs/ban-giao/nang-cap-h2.md): export the drawing to another format: DXF, a DWG of an older version,
 * FBX of the 3D solids (3D / Full: the plugin refuses fbx on the 2D plan, PlanLogic.ThreeDValues). Deferred (loaded when
 * needed): one tool for the three formats.
 */
export const EXPORT_FORMATS = ['dxf', 'dwg', 'fbx'] as const;
export const EXPORT_VERSIONS = ['2018', '2013', '2010', '2007', '2004', '2000'] as const;

export function exportDrawingTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'export_drawing',
      'EXPORT the drawing to another format as a NEW file (the open drawing keeps its file, name and version): ' +
        'dxf; dwg of an older version (to send to someone on an older AutoCAD: version 2018 / 2013 / 2010 / 2007 / 2004 / 2000, default 2013); ' +
        'fbx = the 3D solids as meshes for 3ds Max / Blender (3D / Full plans; lines and texts are not in an FBX). ' +
        'Whole drawing by default (dxf / dwg keep the layouts); handles or layers = only those objects of model space. ' +
        'output: path ending in .dxf / .dwg / .fbx (default <drawing>-acad2013.dwg / .dxf, <drawing>.fbx next to the drawing). ' +
        'An existing file needs the card (the error lists the handles; then confirm_handles). Network folders other than the drawing\'s are refused. ' +
        'SketchUp: offer dwg / dxf 2013 or older (lines and faces), or export_stl for solids; do not promise more. Tell the user the file path.',
      {
        format: z.enum(EXPORT_FORMATS),
        version: z.enum(EXPORT_VERSIONS).optional().describe('dxf / dwg: AutoCAD version (default 2013).'),
        handles: handles.max(20000).optional(),
        layers: z.array(z.string().min(1).max(255)).min(1).optional(),
        output: z.string().min(1).max(260).optional(),
        overwrite: z.boolean().optional(),
        confirm_handles: handles.max(50).optional().describe(confirmDesc),
      },
      { readOnly: true }),
  ];
}
