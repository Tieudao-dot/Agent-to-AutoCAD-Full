import { z } from 'zod';
import { cadTool, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt D (13/11/2026, docs/ban-giao/nang-cap-d.md): beams, PRESSPULL, STL export, volume schedule by layer.
 * Labelled 3D (3D / Full plans). The schedules' table_position is text (2D): the plugin refuses it without 2D and the
 * schemas hide it on the 3D plan (PLAN_HIDDEN_PARAMS), so on 3D the numbers only come back to Claude.
 */
export const SOLID3_TOOL_NAMES = ['build_beam', 'presspull', 'export_stl', 'volume_schedule'] as const;

/** Defaults and limits shared with the plugin (Core/Solids3.cs, Solid3Logic). */
export const SOLID3 = {
  DefaultBeamWidth: 220,
  DefaultBeamHeight: 400,
  MinBeamSize: 50,
  MaxBeamWidth: 3000,
  MaxBeamHeight: 5000,
  MaxBeamPaths: 500,
  MaxBeamSegments: 2000,
  BeamLayer: 'A-BEAM-3D',
  MaxScheduleSolids: 2000,
  MaxStlSolids: 2000,
} as const;

const S = SOLID3;

/** The table parameters of the 3D schedules (Full only; hidden on the 3D plan). */
export const solidTable = {
  table_position: point.optional().describe('Full plan only: also insert the result as a TABLE with its top-left corner here (omit = only return the numbers).'),
  title: z.string().optional(),
  text_height: z.number().positive().optional().describe('Table text height (default 2.5 × DIMSCALE).'),
};

export function solid3Tools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'build_beam',
      `BEAMS (dầm): a solid of width × height (default ${S.DefaultBeamWidth} × ${S.DefaultBeamHeight} mm) centred on each path, top at top_z ` +
        '(mm, default the path z) minus slab_thickness when given (a beam under a slab: top_z = slab top level). Paths: path_handles (lines, arcs, lightweight ' +
        'polylines with straight and arc segments; one beam each, only read) or path_points [[x, y], ...] with path_bulges (+ closed). Corners are closed (mitre). ' +
        `On ${S.BeamLayer}. Returns handles, beams [{handle, length_m, volume_m3, top_z_mm, min, max}], total_volume_m3.`,
      {
        path_handles: handles.max(S.MaxBeamPaths).optional(),
        path_points: points(2, 'path points').max(S.MaxBeamSegments + 1).optional(),
        path_bulges: z.array(z.number()).optional().describe('Bulge per segment of path_points (like draw_polyline).'),
        closed: z.boolean().optional(),
        width: z.number().min(S.MinBeamSize).max(S.MaxBeamWidth).optional(),
        height: z.number().min(S.MinBeamSize).max(S.MaxBeamHeight).optional(),
        top_z: z.number().optional().describe('Level of the beam top, or of the slab top with slab_thickness (mm).'),
        slab_thickness: z.number().min(0).max(5000).optional(),
        layer: z.string().min(1).max(255).optional(),
      }),

    cadTool(b, 'presspull',
      'PRESSPULL: (1) handle + faces / face_filter (+ topology_id from get_solid_info faces: true): move those faces of a solid by distance along their outward normal ' +
        '(positive = out, the solid grows; negative = in). (2) a closed area (profile_points [+ profile_bulges] or profile_handles) pulled by distance along its normal ' +
        '(XY profiles: positive = up): a new solid, or with target added to / cut from that solid (mode auto: a pull into the solid cuts, out of it adds; add; subtract; new). ' +
        'Changing an existing solid (handle, target): when it existed before this turn, first call request_confirmation with its handle and end the turn; run it after the user agrees. ' +
        'Returns handle(s), volume_before and the new volume / min / max, mode.',
      {
        handle: handle.optional().describe('The solid whose faces are pulled.'),
        faces: z.array(z.number().int().min(0)).max(100).optional(),
        face_filter: z.array(z.enum(['top', 'bottom', 'front', 'back', 'left', 'right'])).min(1).optional(),
        topology_id: z.string().optional(),
        profile_points: points(3, 'profile vertices').max(5000).optional(),
        profile_bulges: z.array(z.number()).optional(),
        profile_handles: handles.max(500).optional(),
        distance: z.number().describe('mm; the sign gives the direction.'),
        target: handle.optional().describe('Solid to add the pulled profile to / cut it from.'),
        mode: z.enum(['auto', 'add', 'subtract', 'new']).optional(),
        erase_profile: z.boolean().optional().describe('Erase profile_handles after use (only construction lines of this turn).'),
        layer: z.string().min(1).max(255).optional().describe('Layer of a new solid.'),
      },
      { destructive: true }),

    cadTool(b, 'export_stl',
      'EXPORT 3D solids to an STL file (3D printing / other software): handles, or every solid (on layers). One file, in mm, moved into the positive octant when needed ' +
        '(moved_by_mm). output: file path ending in .stl (default <drawing>-3d.stl next to the drawing; a relative name goes there too). An existing file needs overwrite: true ' +
        '(ask the user first). Binary by default (ascii: true for text). The drawing is not changed.',
      {
        handles: handles.max(S.MaxStlSolids).optional(),
        layers: z.array(z.string()).optional(),
        output: z.string().min(1).max(260).optional(),
        ascii: z.boolean().optional(),
        overwrite: z.boolean().optional(),
      }),

    cadTool(b, 'volume_schedule',
      'VOLUME SCHEDULE of 3D solids by layer (khối lượng): count, volume_m3, overlap deducted, net_m3 and surface area per layer, totals. handles, or every solid (on layers). ' +
        'Overlaps (e.g. columns drawn inside walls, beams into columns) are measured: overlap "deduct" (default) counts a common part once, in the column, then beam, wall, ' +
        'slab (or the priority layers in order); "none" only warns. Returns rows, totals, deductions, warnings (tell the user).',
      {
        handles: handles.max(S.MaxScheduleSolids).optional(),
        layers: z.array(z.string()).optional(),
        overlap: z.enum(['deduct', 'none']).optional(),
        priority: z.array(z.string()).max(50).optional().describe('Layers (wildcards) that keep their full volume first, in order.'),
        surface_area: z.boolean().optional().describe('Also the surface area (default true).'),
        decimals: z.number().int().min(0).max(6).optional(),
        ...solidTable,
      }),
  ];
}
