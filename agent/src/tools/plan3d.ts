import { z } from 'zod';
import { cadTool, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/**
 * 3D phase 3 (docs/ban-giao/ve-3d-dot-3.md): build 3D from a 2D floor plan, 2D sections and projections of solids.
 * Only machines on the 3D or Full plan load them (the plugin labels them ToolGroup.ThreeD); their 2D lines / hatch
 * are results of a 3D tool, so a 3D machine needs no 2D tool for them.
 */
export const PLAN3D_TOOL_NAMES = ['build_3d_from_plan', 'section_view', 'projection_view'] as const;

/** Limits and defaults shared with the plugin (Core/FloorPlan.cs, FloorPlanLogic / SectionLogic). */
export const PLAN3D_LIMITS = {
  MaxSourceEntities: 5000,
  MaxOpeningsGiven: 500,
  MinWallHeight: 100,
  MaxWallHeight: 100000,
  MaxThicknessLimit: 3000,
  MaxSlabThickness: 5000,
  DefaultWallHeight: 3300,
  DefaultDoorHeight: 2200,
  DefaultSill: 900,
  DefaultWindowHeight: 1500,
  DefaultSlabThickness: 150,
  DefaultMinThickness: 50,
  DefaultMaxThickness: 600,
  MaxSectionSolids: 500,
  DefaultHatchScale: 10,
} as const;

export const PLAN3D_LAYERS = {
  wall: ['A-WALL'],
  door: ['A-DOOR'],
  window: ['A-WIND'],
  slab: ['A-SLAB', 'A-FLOR'],
  wall3d: 'A-WALL-3D',
  slab3d: 'A-SLAB-3D',
  section: 'A-SECT',
  sectionHatch: 'A-SECT-PAT',
  sectionBehind: 'A-SECT-BACK',
  elevation: 'A-ELEV',
} as const;

const L = PLAN3D_LIMITS;
const layerList = (what: string, def: readonly string[]) =>
  z.array(z.string().min(1).max(255)).min(1).max(50).optional().describe(`Layers of the ${what} (default ${def.join(', ')}; '*' wildcards, e.g. "KT_TUONG*").`);

const opening = z.object({
  kind: z.enum(['door', 'window']),
  at: point.describe('A point in the wall (in plan) where the opening is, e.g. the middle of a gap or of a door symbol.'),
  width: z.number().positive().optional().describe('Opening width along the wall (mm). Leave out for a gap: the gap width is used. Must stay within the wall; a point where two walls meet is refused.'),
  height: z.number().positive().optional().describe('Door height, or window height (mm).'),
  sill: z.number().min(0).optional().describe('Window sill height above the floor (mm).'),
});

const planeArgs = {
  plane_points: points(3, 'points on the plane').max(3).optional().describe('3 points on the cutting plane.'),
  point: point.optional().describe('A point on the plane (with normal or plane).'),
  normal: point.optional().describe('Plane normal [x, y, z], pointing to the viewer (with point).'),
  plane: z.enum(['xy', 'yz', 'zx']).optional().describe('Axis plane through point: xy (horizontal cut, seen from above), yz (normal +X), zx (normal +Y).'),
};

const VIEW_DIRS = ['top', 'bottom', 'front', 'back', 'left', 'right', 'sw_iso', 'se_iso', 'ne_iso', 'nw_iso'] as const;

export function plan3dTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'build_3d_from_plan',
      'BUILD 3D FROM A 2D FLOOR PLAN ("dựng 3D mặt bằng này, tường cao 3,3 m"): reads walls, doors / windows and slabs of the plan (by layer like the ve-kien-truc skill: ' +
        'walls A-WALL drawn as two face lines t apart, doors A-DOOR, windows A-WIND or CUA-DI-* / CUA-SO-* blocks, slab outlines A-SLAB / A-FLOR) and builds wall solids ' +
        '(openings left: lintel above doors, sill and lintel at windows) on A-WALL-3D and slabs (top at base_z, slab_thickness down) on A-SLAB-3D. ' +
        'Or name the objects: wall_handles (face lines), centerline_handles + thickness (walls drawn by their axis), wide polylines (width = thickness; a width below min_thickness is only a print line weight: read as a face line) are read too, ' +
        'door_handles / window_handles, slab_handles. Only straight walls; curved walls, walls inside blocks / xrefs and unclear pieces are SKIPPED and listed ' +
        'with handle and reason (nothing is guessed). The plan objects are only read, never changed. ' +
        'Use dry_run: true first on a plan you do not know: it returns walls, openings, open_gaps (gaps with no symbol, left open), skipped and existing_solids (solids of an earlier run over this plan) without building. ' +
        'Read that report, tell the user what was skipped and ask about open gaps / missing doors, then build (add "openings" for doors / windows the plan does not show). ' +
        'Refuses (nothing built) when solids from an earlier run are already on the result layers over this plan, unless on_existing "add". ' +
        'Returns handles, wall_solid {volume_m3, pieces_volume_m3, min, max}, slabs, walls / openings / skipped lists, thickness_summary.',
      {
        wall_height: z.number().min(L.MinWallHeight).max(L.MaxWallHeight).optional().describe(`Wall height (mm, default ${L.DefaultWallHeight}).`),
        door_height: z.number().positive().optional().describe(`Door opening height (mm, default ${L.DefaultDoorHeight}).`),
        window_sill: z.number().min(0).optional().describe(`Window sill height (mm, default ${L.DefaultSill}).`),
        window_height: z.number().positive().optional().describe(`Window opening height (mm, default ${L.DefaultWindowHeight}).`),
        slab_thickness: z.number().min(0).max(L.MaxSlabThickness).optional().describe(`Slab thickness (mm, default ${L.DefaultSlabThickness}; 0 = no slab).`),
        base_z: z.number().optional().describe('Floor level: walls start here, slab top is here (default 0; e.g. 3300 for the next storey).'),
        slab: z.enum(['auto', 'none', 'bbox']).optional()
          .describe('auto (default): slabs from closed outlines on the slab layers / slab_handles (an outline inside another is a hole); bbox: one slab under the walls\' bounding box; none.'),
        wall_layers: layerList('wall face lines', PLAN3D_LAYERS.wall),
        door_layers: layerList('door symbols', PLAN3D_LAYERS.door),
        window_layers: layerList('window symbols', PLAN3D_LAYERS.window),
        slab_layers: layerList('slab outlines', PLAN3D_LAYERS.slab),
        wall_handles: handles.max(L.MaxSourceEntities).optional().describe('Wall face lines (lines / polylines; two lines per wall) instead of the wall layers.'),
        centerline_handles: handles.max(L.MaxSourceEntities).optional().describe('Wall axis lines / polylines (with thickness) instead of the wall layers.'),
        thickness: z.number().positive().max(L.MaxThicknessLimit).optional().describe('Wall thickness for centerline_handles (mm).'),
        door_handles: handles.max(L.MaxSourceEntities).optional().describe('Door symbols (blocks, or leaf lines + arcs) instead of the door layers.'),
        window_handles: handles.max(L.MaxSourceEntities).optional().describe('Window symbols instead of the window layers.'),
        slab_handles: handles.max(L.MaxSourceEntities).optional().describe('Closed slab outlines (polylines, circles, regions) instead of the slab layers.'),
        openings: z.array(opening).max(L.MaxOpeningsGiven).optional()
          .describe('Extra or corrected openings by a point in the wall (replace a recognised one there), e.g. for an open gap the user says is a door.'),
        min_thickness: z.number().positive().max(L.MaxThicknessLimit).optional().describe(`Thinnest wall to recognise (mm, default ${L.DefaultMinThickness}).`),
        max_thickness: z.number().positive().max(L.MaxThicknessLimit).optional().describe(`Thickest wall to recognise (mm, default ${L.DefaultMaxThickness}).`),
        area_min: point.optional().describe('With area_max: only read plan objects lying inside this rectangle (one building / one part of a big plan).'),
        area_max: point.optional(),
        on_existing: z.enum(['stop', 'add']).optional().describe('stop (default): refuse when this plan was already built; add: build again anyway.'),
        dry_run: z.boolean().optional().describe('Only recognise and report; build nothing.'),
        wall_layer_3d: z.string().min(1).max(255).optional().describe(`Layer of the wall solids (default ${PLAN3D_LAYERS.wall3d}, created when missing).`),
        slab_layer_3d: z.string().min(1).max(255).optional().describe(`Layer of the slab solids (default ${PLAN3D_LAYERS.slab3d}).`),
      }),

    cadTool(b, 'section_view',
      'SECTION (mặt cắt) of 3D solids as 2D: the cut outline (closed polylines on A-SECT) + hatch (A-SECT-PAT) and, with show_behind, the lines seen behind the cut (A-SECT-BACK). ' +
        'Plane: line [[x1, y1], [x2, y2]] = a vertical section line in plan (you stand on the left of line start → end and look to the right; flip turns it), ' +
        'or plane_points / point + normal / point + plane like slice_solid (normal points to the viewer; plane "xy" at a z = a plan cut seen from above). ' +
        'The result is drawn flat in XY beside the model (right of everything, never on it) or at position (its lower-left corner). The solids are not changed. ' +
        'Returns handles, cut_area_m2 (check: a wall t × h cut across has t × h), size, position and frame (how a model point maps onto the drawing).',
      {
        handles: handles.max(L.MaxSectionSolids).describe('The 3D solids to cut.'),
        line: points(2, 'section line points').max(2).optional(),
        ...planeArgs,
        flip: z.boolean().optional().describe('Look the other way (the normal reversed).'),
        show_behind: z.boolean().optional().describe('Also draw what is seen behind the cut (AutoCAD hidden-line view; default false).'),
        hatch: z.union([z.boolean(), z.string().min(1).max(64)]).optional().describe('Hatch pattern of the cut (default ANSI31; e.g. "AR-CONC", "SOLID"); false = no hatch.'),
        hatch_scale: z.number().positive().optional().describe(`Hatch pattern scale (default ${L.DefaultHatchScale}).`),
        position: point.optional().describe('Lower-left corner of the drawn section (default: beside the model).'),
        layer: z.string().min(1).max(255).optional().describe(`Outline layer (default ${PLAN3D_LAYERS.section}).`),
        hatch_layer: z.string().min(1).max(255).optional().describe(`Hatch layer (default ${PLAN3D_LAYERS.sectionHatch}).`),
        behind_layer: z.string().min(1).max(255).optional().describe(`Layer of the lines behind (default ${PLAN3D_LAYERS.sectionBehind}).`),
      }),

    cadTool(b, 'projection_view',
      'PROJECTION (hình chiếu, e.g. mặt đứng) of 3D solids as 2D lines (A-ELEV) seen from a direction: view front (looking towards +Y), back, left, right, top, bottom, ' +
        'an iso, or direction [x, y, z] from the model to the viewer. method auto (default): AutoCAD\'s hidden-line view (hidden_lines_removed true); when AutoCAD gives none, ' +
        'every edge is projected (hidden edges too; hidden_lines_removed false). Drawn flat beside the model or at position. The solids are not changed.',
      {
        handles: handles.max(L.MaxSectionSolids).describe('The 3D solids to project.'),
        view: z.enum(VIEW_DIRS).optional().describe('Named direction (default front).'),
        direction: point.optional().describe('Custom direction from the model to the viewer (overrides view).'),
        method: z.enum(['auto', 'edges']).optional(),
        position: point.optional().describe('Lower-left corner of the drawn view (default: beside the model).'),
        layer: z.string().min(1).max(255).optional().describe(`Layer (default ${PLAN3D_LAYERS.elevation}).`),
      }),
  ];
}
