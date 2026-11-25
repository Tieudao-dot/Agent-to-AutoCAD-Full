import { z } from 'zod';
import { cadTool, handles, type AnyTool, type ToolBridge } from './common.js';
import { viewPreset, visualStyle } from './solids.js';

/**
 * Nâng cấp đợt F3 (25/11/2026, docs/ban-giao/nang-cap-f3.md): materials and named views (+ a PNG with the materials).
 * Labelled 3D (3D / Full plans). Plain AutoCAD materials (colour + opacity), seen in the Realistic visual style.
 */
export const RENDER_TOOL_NAMES = ['set_material', 'named_view'] as const;

/** Preset materials, shared with the plugin (Core/Materials.cs, MaterialLogic.Presets). */
export const MATERIAL_PRESETS = ['concrete', 'brick', 'plaster', 'glass', 'wood', 'roof_tile', 'steel', 'stone'] as const;

export function renderTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'set_material',
      `Give a material to solids (handles) and / or layers: preset ${MATERIAL_PRESETS.join(' / ')} (bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá; made once in the drawing), ` +
        'an existing material name, ByLayer / ByBlock (reset), or a new name with color [r, g, b] (+ opacity %). list: true = the drawing\'s materials (read-only). ' +
        'Changing existing objects follows the usual rule (more than 20: ask first). Materials show in the realistic visual style (capture_view, named_view image).',
      {
        material: z.string().min(1).max(255).optional().describe('Preset key, existing material, ByLayer / ByBlock, or a new name with color.'),
        handles: handles.max(5000).optional(),
        layers: z.array(z.string().min(1).max(255)).min(1).optional(),
        color: z.array(z.number().int().min(0).max(255)).length(3).optional().describe('[r, g, b] of a new / changed material.'),
        opacity: z.number().min(0).max(100).optional().describe('% (100 = opaque).'),
        list: z.boolean().optional(),
      }),

    cadTool(b, 'named_view',
      'Named views of model space: save (the current view under name; replace: true to overwrite, ask first), restore, list, delete; ' +
        'image = save a PNG file of the model (off-screen, the user\'s view unchanged) from the saved view name or view (default sw_iso), ' +
        'visual_style default realistic (shows the materials), handles to frame, output .png (relative = next to the drawing). Tell the user the file path.',
      {
        action: z.enum(['save', 'restore', 'list', 'delete', 'image']),
        name: z.string().min(1).max(255).optional(),
        replace: z.boolean().optional(),
        view: viewPreset.optional(),
        visual_style: visualStyle.optional(),
        handles: handles.max(5000).optional(),
        output: z.string().min(1).max(1024).optional(),
        size: z.number().int().min(384).max(1568).optional().describe('Long side in pixels (default 1568).'),
      }),
  ];
}
