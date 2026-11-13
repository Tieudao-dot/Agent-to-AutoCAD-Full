import { z } from 'zod';
import { cadTool, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/**
 * Nâng cấp đợt D (13/11/2026, docs/ban-giao/nang-cap-d.md): beams, PRESSPULL, STL export, volume schedule by layer.
 * Labelled 3D (3D / Full plans). The schedules' table_position is text (2D): the plugin refuses it without 2D and the
 * schemas hide it on the 3D plan (PLAN_HIDDEN_PARAMS), so on 3D the numbers only come back to Claude.
 */
export const SOLID3_TOOL_NAMES = ['build_beam', 'presspull', 'export_stl', 'volume_schedule'] as const;

/** Nâng cấp đợt E (13/11/2026, docs/ban-giao/nang-cap-e.md): stairs and roofs, 3D / Full. */
export const STAIR_ROOF_TOOL_NAMES = ['build_stair', 'build_roof'] as const;

/** Defaults agreed with the project owner (13/11/2026) and limits, shared with the plugin (Core/StairRoof.cs). */
export const STAIR_ROOF = {
  DefaultRiser: 160,
  DefaultTread: 280,
  DefaultWidth: 1000,
  DefaultWaist: 150,
  DefaultGap: 100,
  MinRiser: 100,
  MaxRiser: 250,
  MinTread: 150,
  MaxTread: 500,
  MinWidth: 500,
  MaxWidth: 5000,
  MinHeight: 200,
  MaxHeight: 20000,
  RailHeight: 900,
  StairLayer: 'A-STAIR-3D',
  DefaultRoofThickness: 150,
  DefaultSlopeDeg: 30,
  DefaultOverhang: 600,
  MinSlopeDeg: 5,
  MaxSlopeDeg: 60,
  MinRoofThickness: 30,
  MaxRoofThickness: 1000,
  MaxOverhang: 3000,
  RoofLayer: 'A-ROOF-3D',
} as const;

const E = STAIR_ROOF;

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
        '(XY profiles: positive = up): a new solid, or with target added to / cut from that solid (mode auto: a pull into the solid cuts, out of it adds; add; subtract; new; add / subtract need target). ' +
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

    cadTool(b, 'build_stair',
      `STAIR (cầu thang) as one solid on ${E.StairLayer}: type straight (1 vế), l (chữ L), u (chữ U). height = floor to floor (mm, e.g. the storey height 3300; required): ` +
        `it is split into whole equal risers nearest riser (default ${E.DefaultRiser}); tell the user the risers / riser_mm returned. Defaults (use the user's numbers when given): ` +
        `tread ${E.DefaultTread}, width ${E.DefaultWidth} (flight width), landing = width (L / U, at mid height), waist ${E.DefaultWaist}, gap ${E.DefaultGap} between the U flights. ` +
        'Place it by start (middle of the bottom edge of the first step) + direction (degrees, walking up the first flight) + turn (left / right, L / U), or by the stair well ' +
        '(well_handle: closed polyline / well_points): without start it is fitted into the well (keeping direction when given); it does not fit = an error with the size needed (tell the user, never build it outside). ' +
        `base_z = lower floor level (mm). railing left / right / both (walking up): handrail ${E.RailHeight} mm + posts along the flights, on A-RAIL-3D. ` +
        'Returns handle, risers, riser_mm, flights, landing_z_mm, footprint_mm, start, direction_deg, turn, warnings.',
      {
        type: z.enum(['straight', 'l', 'u']).optional().describe('Default straight.'),
        height: z.number().min(E.MinHeight).max(E.MaxHeight).describe('Floor-to-floor height (mm).'),
        start: point.optional(),
        direction: z.number().optional().describe('Walking direction of the first flight in plan (degrees, default 0 = +X).'),
        turn: z.enum(['left', 'right']).optional(),
        well_handle: handle.optional(),
        well_points: points(3, 'well outline points').max(200).optional(),
        riser: z.number().min(E.MinRiser).max(E.MaxRiser).optional(),
        tread: z.number().min(E.MinTread).max(E.MaxTread).optional(),
        width: z.number().min(E.MinWidth).max(E.MaxWidth).optional(),
        landing: z.number().min(500).max(10000).optional().describe('Landing depth (mm, default = width).'),
        first_flight_risers: z.number().int().min(2).optional(),
        waist: z.number().min(50).max(500).optional(),
        gap: z.number().min(0).max(5000).optional(),
        base_z: z.number().optional(),
        railing: z.enum(['none', 'left', 'right', 'both']).optional(),
        layer: z.string().min(1).max(255).optional(),
        rail_layer: z.string().min(1).max(255).optional(),
      }),

    cadTool(b, 'build_roof',
      `ROOF (mái) as one solid on ${E.RoofLayer}: type flat (mái bằng, slab thickness default ${E.DefaultRoofThickness}), gable (2 dốc, rectangle only; ridge along the long side, or ridge short) ` +
        `or hip (4 dốc, convex outline only). Sloped: slope default ${E.DefaultSlopeDeg}°, slab ${E.DefaultRoofThickness} mm, overhang (eave past the walls) default ${E.DefaultOverhang} mm (flat: 0); ` +
        "use the user's numbers when given. Outline = outer face of the walls: outline_handle (closed polyline), outline_points, or from_walls: true (box around the wall solids). " +
        'It sits on the wall top: wall_top_z (mm) or, by default, the top of the wall solids under it (wall_layers, default A-WALL-3D: the top storey). An L / T house cannot take a hip ' +
        'or gable roof: the error says so; tell the user (split it into rectangles, or flat). Gable ends are left open. Returns handle, eave_z_mm, top_z_mm, ridge_length_m, plan_area_m2, roof_area_m2, volume_m3, wall_top_from.',
      {
        type: z.enum(['flat', 'gable', 'hip']).describe('flat = mái bằng, gable = 2 dốc, hip = 4 dốc.'),
        outline_handle: handle.optional(),
        outline_points: points(3, 'outline points').max(200).optional(),
        from_walls: z.boolean().optional(),
        wall_top_z: z.number().optional(),
        wall_layers: z.array(z.string()).optional(),
        thickness: z.number().min(E.MinRoofThickness).max(E.MaxRoofThickness).optional(),
        slope: z.number().min(E.MinSlopeDeg).max(E.MaxSlopeDeg).optional().describe('Degrees.'),
        overhang: z.number().min(0).max(E.MaxOverhang).optional(),
        ridge: z.enum(['long', 'short']).optional(),
        layer: z.string().min(1).max(255).optional(),
      }),
  ];
}
