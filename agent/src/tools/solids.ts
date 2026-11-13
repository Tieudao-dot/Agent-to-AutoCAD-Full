import { z } from 'zod';
import { angle, cadTool, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';
import { PLAN3D_TOOL_NAMES, plan3dTools } from './plan3d.js';
import { SOLID3_TOOL_NAMES, solid3Tools, solidTable } from './solids3.js';

/** The 3D tools: only machines on the 3D or Full plan load them (the plugin labels them ToolGroup.ThreeD). */
export const SOLID_TOOL_NAMES = [
  'draw_solid', 'extrude', 'revolve',
  'solid_union', 'solid_subtract', 'solid_intersect', 'set_view_3d', 'set_visual_style', 'get_solid_info',
  // phase 2
  'sweep', 'loft', 'slice_solid', 'fillet_edges', 'chamfer_edges', 'shell_solid', 'rotate_3d', 'mirror_3d', 'align',
  'set_ucs', 'reset_ucs', 'check_interference',
  // phase 3 (plan3d.ts)
  ...PLAN3D_TOOL_NAMES,
  // nâng cấp đợt D (solids3.ts)
  ...SOLID3_TOOL_NAMES,
] as const;

/**
 * The six primitives of draw_solid (nâng cấp đợt A, 13/11/2026: one tool instead of draw_box … draw_pyramid, about 2k
 * tokens less per session). The plugin still runs the old names for recorded macros; Claude only sees draw_solid.
 */
export const SOLID_SHAPES = ['box', 'cylinder', 'sphere', 'wedge', 'torus', 'pyramid'] as const;
export const SOLID_ALIASES = ['draw_box', 'draw_cylinder', 'draw_sphere', 'draw_wedge', 'draw_torus', 'draw_pyramid'] as const;

/** Phase 2 tools that change an existing solid: like the booleans, a solid from before this turn always needs a yes. */
export const SOLID_EDIT_TOOLS = ['slice_solid', 'fillet_edges', 'chamfer_edges', 'shell_solid'] as const;

/** Limits shared with the plugin (Core/Solids2.cs, Solid2Logic). */
export const SOLID2_LIMITS = {
  DefaultListItems: 60,
  MaxListItems: 300,
  MaxEdgeSelection: 500,
  MaxFaceSelection: 100,
  MinLoftSections: 2,
  MaxLoftSections: 50,
  MaxInterferenceSolids: 100,
  MaxInterferenceCandidates: 2000,
  MaxTwist: 3600,
  MinSweepScale: 0.01,
  MaxSweepScale: 100,
} as const;

export const EDGE_FILTERS = ['all', 'straight', 'curved', 'parallel_x', 'parallel_y', 'parallel_z', 'top', 'bottom', 'front', 'back', 'left', 'right'] as const;
export const FACE_SIDES = ['top', 'bottom', 'front', 'back', 'left', 'right'] as const;
export const UCS_PRESETS = ['world', 'top', 'bottom', 'front', 'back', 'left', 'right'] as const;

/** The booleans change (and erase) existing solids: the confirmation gate treats them specially. */
export const SOLID_BOOLEAN_TOOLS = ['solid_union', 'solid_subtract', 'solid_intersect'] as const;

export const VIEW_PRESETS = ['top', 'bottom', 'front', 'back', 'left', 'right', 'sw_iso', 'se_iso', 'ne_iso', 'nw_iso'] as const;
export const VISUAL_STYLES = ['2d_wireframe', 'wireframe', 'hidden', 'conceptual', 'shaded', 'shaded_with_edges', 'realistic', 'xray'] as const;

export const viewPreset = z
  .enum(VIEW_PRESETS)
  .describe('Named view like the ViewCube: top (plan), bottom, front (looking towards +Y), back, left, right, sw_iso / se_iso / ne_iso / nw_iso (from that corner, above).');
export const viewDirection = point.describe('Custom view direction [x, y, z] from the target towards the viewer, e.g. [-1, -1, 1] = SW iso. Overrides view.');
export const visualStyle = z
  .enum(VISUAL_STYLES)
  .describe('Visual style: 2d_wireframe (AutoCAD default), wireframe, hidden, conceptual (best to check a model), shaded, shaded_with_edges, realistic, xray.');

const RET = ' Returns handles, volume (mm³), volume_m3, min and max of the new solid(s).';

/**
 * The solid-building tools take only the layer (decided 12/11/2026, to save tokens): color / linetype / lineweight
 * are set afterwards with set_entity_props, and solids are never built on a paper-space layout. The plugin still
 * accepts those parameters (older macros), Claude just does not see them.
 */
const solidLayer = { layer: entityProps.layer };

const profile = {
  profile_points: points(3, 'profile vertices').max(5000).optional()
    .describe('Closed profile polygon [[x, y], ...] (mm), drawn in the XY plane at the z of the first point; not added to the drawing.'),
  profile_bulges: z.array(z.number()).optional().describe('Optional bulge per segment of profile_points (like draw_polyline, one per vertex).'),
  profile_handles: handles.max(500).optional()
    .describe('Existing profile objects: closed polylines, circles, ellipses, closed splines, regions, or lines / arcs that form closed loops. One solid per closed area.'),
  erase_profile: z.boolean().optional()
    .describe('Erase the profile objects after use (default false). Only for profiles you drew in this turn as construction lines.'),
};

const ON_EXISTING =
  ' It changes existing solids: when any of them existed before this turn (not built by you in this turn), first call request_confirmation ' +
  'with the handles and end the turn; run it after the user agrees.';

/** 3D phase 1: solid primitives, extrude / revolve, booleans, 3D views and visual styles, solid query. Z up, mm. */
export function solidTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'draw_solid',
      'Build a 3D solid primitive (mm, Z up). shape and its parameters: ' +
        'box: length (X) × width (Y) × height (Z), corner (min corner, default [0,0,0]; negative height builds down) or center, rotation about Z; ' +
        'cylinder (cone / frustum with top_radius, 0 = cone): base (bottom centre), radius or diameter, height along Z (negative = down) or top_center; ' +
        'sphere: center, radius or diameter; ' +
        'wedge (ramps, sloped roofs): length × width × height on corner, the full-height face at the corner side, sloping to 0 at x + length, rotation about Z (180 = slope to -X); ' +
        'torus: center, radius (to the tube centre), tube_radius (< radius); ' +
        'pyramid (frustum with top_radius > 0): base, sides 3-32 (default 4), radius INSCRIBED in the base (square of side 1000 → 500), height or top_center, rotation. ' +
        'Example wall 4 m × 220 × 3.3 m: {"shape": "box", "corner": [0, 0, 0], "length": 4000, "width": 220, "height": 3300, "layer": "A-WALL-3D"}; ' +
        'column D400: {"shape": "cylinder", "base": [0, 0, 0], "radius": 200, "height": 3300}.' + RET,
      {
        shape: z.enum(SOLID_SHAPES),
        length: z.number().positive().optional(),
        width: z.number().positive().optional(),
        height: z.number().optional(),
        corner: point.optional(),
        center: point.optional(),
        base: point.optional(),
        radius: z.number().positive().optional(),
        diameter: z.number().positive().optional(),
        top_radius: z.number().min(0).optional(),
        top_center: point.optional(),
        tube_radius: z.number().positive().optional(),
        sides: z.number().int().min(3).max(32).optional(),
        rotation: angle('Rotation about Z').optional(),
        ...solidLayer,
      }),

    cadTool(b, 'extrude',
      'EXTRUDE closed 2D profiles into 3D solids along their normal (profiles in the XY plane go up +Z; negative height goes down). ' +
        'taper_angle in degrees (positive narrows the solid towards the top, like EXTRUDE). One solid per closed area.' + RET,
      {
        ...profile,
        height: z.number().describe('Extrusion height (mm): for profiles on the XY plane positive = up (+Z), negative = down; other profiles along their normal.'),
        taper_angle: z.number().min(-89.9).max(89.9).optional().describe('Taper angle in degrees (default 0).'),
        ...solidLayer,
      }),

    cadTool(b, 'revolve',
      'REVOLVE closed 2D profiles about the axis axis_start → axis_end into 3D solids (angle degrees, default 360, counter-clockwise looking from axis_end towards axis_start). ' +
        'The axis must lie in the profile plane and must not cross the profile.' + RET,
      {
        ...profile,
        axis_start: point,
        axis_end: point,
        angle: z.number().gt(0).max(360).optional(),
        ...solidLayer,
      }),

    cadTool(b, 'solid_union',
      'UNION 3D solids: every solid in handles is merged into the first one (it keeps its handle and layer); the others are erased. ' +
        'Returns the handle, the erased handles and the new volume.' + ON_EXISTING,
      { handles: handles.min(2).max(500) },
      { destructive: true }),

    cadTool(b, 'solid_subtract',
      'SUBTRACT 3D solids: cuts the subtract solids out of target (e.g. door / window openings out of a wall, a hole out of a plate). ' +
        'The subtract solids are erased unless keep_tools is true. Returns the target handle and its new volume (check it went down).' + ON_EXISTING,
      {
        target: handle.describe('The solid to cut.'),
        subtract: handles.max(500).describe('The solids to remove from target.'),
        keep_tools: z.boolean().optional().describe('Keep the subtracted solids in the drawing (default false).'),
      },
      { destructive: true }),

    cadTool(b, 'solid_intersect',
      'INTERSECT 3D solids: the first solid in handles becomes the common volume of all of them; the others are erased. ' +
        'Fails (nothing changed) when they do not overlap.' + ON_EXISTING,
      { handles: handles.min(2).max(500) },
      { destructive: true }),

    cadTool(b, 'get_solid_info',
      'Read 3D solids: volume (mm³ and m³), centroid, bounding box min / max / size [dx, dy, dz], and the number of faces, edges and vertices. ' +
        'Use it after building to check the model by numbers (e.g. a 4000 × 220 × 3300 wall must be 2.904 m³; a wall with a 900 × 2100 door cut out loses 0.416 m³). ' +
        'edges: true / faces: true also list them with indices (edge: i, line / curve, start, end, faces, parallel x / y / z; face: i, plane / curved, center, normal, area, side top / bottom / front / back / left / right) ' +
        'and a topology_id: pass both to fillet_edges / chamfer_edges / shell_solid. At most 10 solids when listing.',
      {
        handles: handles.max(200),
        edges: z.boolean().optional().describe('List the edges with indices (default false).'),
        faces: z.boolean().optional().describe('List the faces with indices (default false).'),
        max_items: z.number().int().min(1).max(SOLID2_LIMITS.MaxListItems).optional()
          .describe(`Edges / faces listed per solid (default ${SOLID2_LIMITS.DefaultListItems}).`),
      },
      { readOnly: true }),

    cadTool(b, 'set_view_3d',
      "Turn the user's model view to a 3D view (top, front, right, sw_iso...) or a custom direction, framing the whole model (or handles). " +
        'zoom: false keeps the current framing. Only changes the view (no undo step; allowed in read-only mode).',
      {
        view: viewPreset.optional().describe('Named view (default sw_iso).'),
        direction: viewDirection.optional(),
        handles: handles.max(5000).optional().describe('Frame these objects instead of the whole model.'),
        zoom: z.boolean().optional(),
      }),

    cadTool(b, 'set_visual_style',
      "Change the visual style of the user's current model viewport: 2d_wireframe, wireframe, hidden, conceptual, shaded, shaded_with_edges, realistic, xray. " +
        'Only changes the display (no undo step; allowed in read-only mode). Large models display slower in realistic / shaded.',
      { style: visualStyle }),

    ...solidTools2(b),
    ...plan3dTools(b),
    ...solid3Tools(b),
  ];
}

const edgeSelection = {
  edges: z.array(z.number().int().min(0)).min(1).max(SOLID2_LIMITS.MaxEdgeSelection).optional()
    .describe('Edge indices from get_solid_info (edges: true); needs topology_id.'),
  edge_filter: z.array(z.enum(EDGE_FILTERS)).min(1).optional()
    .describe('Pick edges by filter instead of indices; every filter must hold. parallel_x / _y / _z = straight edges along that axis; ' +
      'top / bottom / front (min Y) / back / left (min X) / right = edges lying on that side of the bounding box; e.g. ["parallel_z"] = the vertical edges, ["top"] = the top rim, ["all"].'),
  topology_id: z.string().optional().describe('The topology_id get_solid_info returned with the indices (the tool refuses if the solid changed since).'),
};

const planeArgs = {
  plane_points: points(3, 'points on the plane').max(3).optional().describe('3 points on the plane (not on one line).'),
  point: point.optional().describe('A point on the plane (with normal or plane).'),
  normal: point.optional().describe('Plane normal [x, y, z] (with point).'),
  plane: z.enum(['xy', 'yz', 'zx']).optional().describe('Axis plane through point: xy (horizontal), yz (normal X), zx (normal Y).'),
};

const ON_EXISTING_SOLID =
  ' It changes an existing solid: when it existed before this turn (also when you built it in an earlier turn), first call request_confirmation ' +
  'with the handle and one Vietnamese sentence, and end the turn; run it after the user agrees.';

const CHECK_AFTER = ' Returns the handle, volume_before and the new volume / min / max: check them, then look with capture_view (sw_iso, conceptual).';

/** 3D phase 2: sweep / loft, slice / fillet / chamfer / shell, 3D transforms, UCS, interference. */
function solidTools2(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'sweep',
      'SWEEP a closed 2D profile along a path into a 3D solid. Path: path_handle (line, arc, polyline, 3D polyline, spline, helix) ' +
        'or path_points (a polyline through the points, not added to the drawing). align (default true) stands the profile perpendicular to the path start with its centre on the path; ' +
        'false sweeps it where it is. Fails (nothing drawn) when the solid would cut itself (profile wider than a tight bend).' + RET,
      {
        ...profile,
        path_handle: handle.optional().describe('The path curve.'),
        path_points: points(2, 'path points').max(5000).optional().describe('Path through these points [[x, y, z], ...].'),
        align: z.boolean().optional(),
        twist: z.number().min(-SOLID2_LIMITS.MaxTwist).max(SOLID2_LIMITS.MaxTwist).optional().describe('Twist of the profile along the whole path, degrees (default 0).'),
        scale: z.number().min(SOLID2_LIMITS.MinSweepScale).max(SOLID2_LIMITS.MaxSweepScale).optional().describe('Scale of the profile at the end of the path (default 1).'),
        bank: z.boolean().optional().describe('Let the profile bank (roll) with a 3D path (default false).'),
        ...solidLayer,
      }),

    cadTool(b, 'loft',
      'LOFT a 3D solid through 2 or more closed sections in order (bottom to top). ' +
        'Sections: section_handles (closed curves: polylines, circles, ellipses, closed splines) or sections_points (closed polygons, each flat at the z of its points). ' +
        'ruled: straight between sections (default smooth). Optional guide_handles (curves touching every section) or path_handle. erase_profile erases the section curves.' + RET,
      {
        section_handles: handles.min(SOLID2_LIMITS.MinLoftSections).max(SOLID2_LIMITS.MaxLoftSections).optional(),
        sections_points: z.array(points(3, 'section vertices').max(5000)).min(SOLID2_LIMITS.MinLoftSections).max(SOLID2_LIMITS.MaxLoftSections).optional()
          .describe('Closed section polygons [[[x, y, z], ...], ...], bottom to top.'),
        ruled: z.boolean().optional(),
        closed: z.boolean().optional().describe('Loop back to the first section (3+ sections, e.g. a ring).'),
        guide_handles: handles.max(50).optional(),
        path_handle: handle.optional(),
        erase_profile: profile.erase_profile,
        ...solidLayer,
      }),

    cadTool(b, 'slice_solid',
      'SLICE a 3D solid with a plane (cut a building model in two, cut a block at an angle). keep: "both" (default: the handle keeps the positive side, ' +
        'the side the normal points to, and the negative half becomes a new solid), "positive" or "negative" (the other half is discarded). ' +
        'Plane: plane_points, point + normal, or point + plane ("xy" / "yz" / "zx"). Fails (nothing changed) when the plane misses the solid. ' +
        'Returns handles [kept, new half], pieces with volume / min / max of each.' + ON_EXISTING_SOLID,
      {
        handle: handle.describe('The solid to slice.'),
        ...planeArgs,
        keep: z.enum(['both', 'positive', 'negative']).optional(),
      },
      { destructive: true }),

    cadTool(b, 'fillet_edges',
      'FILLET (round) edges of a 3D solid with a radius. Pick the edges by edge_filter (e.g. ["parallel_z"] = vertical edges of a box) or by indices + topology_id from ' +
        'get_solid_info with edges: true. Fails clearly (nothing changed) when the radius is too large for the faces next to an edge.' + CHECK_AFTER + ON_EXISTING_SOLID,
      {
        handle: handle.describe('The solid.'),
        radius: z.number().positive().describe('Fillet radius (mm).'),
        ...edgeSelection,
      },
      { destructive: true }),

    cadTool(b, 'chamfer_edges',
      'CHAMFER (bevel) edges of a 3D solid by distance (and distance2 on the other face; then base_face says which face distance is measured on). ' +
        'Pick the edges like fillet_edges (edge_filter or indices + topology_id).' + CHECK_AFTER + ON_EXISTING_SOLID,
      {
        handle: handle.describe('The solid.'),
        distance: z.number().positive().describe('Chamfer distance on the base face (mm).'),
        distance2: z.number().positive().optional().describe('Distance on the other face (default = distance).'),
        base_face: z.number().int().min(0).optional().describe('Face index (from get_solid_info faces: true; needs topology_id) that every edge bounds.'),
        ...edgeSelection,
      },
      { destructive: true }),

    cadTool(b, 'shell_solid',
      'SHELL a 3D solid: hollow it out to walls of thickness (inward, the outer size stays), with the picked faces removed (open), e.g. an open-top tank or box. ' +
        'Faces: face_filter (["top"]) or face indices + topology_id from get_solid_info (faces: true); no faces (or faces: []) = a closed hollow shell, no topology_id needed. ' +
        'Fails clearly (nothing changed) when the wall is too thick.' + CHECK_AFTER + ON_EXISTING_SOLID,
      {
        handle: handle.describe('The solid.'),
        thickness: z.number().positive().describe('Wall thickness (mm).'),
        faces: z.array(z.number().int().min(0)).max(SOLID2_LIMITS.MaxFaceSelection).optional().describe('Indices of the faces to remove (open).'),
        face_filter: z.array(z.enum(FACE_SIDES)).min(1).optional().describe('Remove the flat faces on these sides of the bounding box (any of them).'),
        topology_id: edgeSelection.topology_id,
      },
      { destructive: true }),

    cadTool(b, 'rotate_3d',
      'ROTATE objects (solids or any entities) in 3D about the axis axis_start → axis_end by angle degrees (right-hand rule: counter-clockwise looking from axis_end back to axis_start). ' +
        'E.g. stand a profile up: axis along X, 90°. rotate_entities is the 2D rotation about Z.',
      {
        handles: handles.max(5000),
        axis_start: point,
        axis_end: point,
        angle: z.number().min(-3600).max(3600).describe('Angle in degrees.'),
      }),

    cadTool(b, 'mirror_3d',
      'MIRROR objects in 3D across a plane (plane_points, point + normal, or point + plane "xy" / "yz" / "zx"). keep_original true (default) makes mirrored copies; ' +
        'false moves the originals. mirror_entities is the 2D mirror about a line.',
      {
        handles: handles.max(5000),
        ...planeArgs,
        keep_original: z.boolean().optional(),
      }),

    cadTool(b, 'align',
      'ALIGN objects by 1 to 3 point pairs, like the ALIGN command: source_points[i] moves onto dest_points[i]. 1 pair = move; 2 pairs = move and turn the first direction onto the second; ' +
        '3 pairs = also turn the plane of the three points (full 3D orientation). scale (2-3 pairs) scales so the first two points match exactly.',
      {
        handles: handles.max(5000),
        source_points: points(1, 'source points').max(3),
        dest_points: points(1, 'destination points').max(3),
        scale: z.boolean().optional(),
      }),

    cadTool(b, 'set_ucs',
      "Set the user's UCS (working plane) for their own drawing and view: preset (world, top, bottom, front, back, left, right; at origin, default [0,0,0]), " +
        'origin + x_axis + y_axis, a saved UCS by name, or just origin to move the current one. save_as stores it as a named UCS; plan_view turns the view to look straight at it. ' +
        'It does NOT change your coordinates: every tool still takes and returns WCS. The user\'s U undoes it with the turn. Not allowed in read-only mode.',
      {
        preset: z.enum(UCS_PRESETS).optional(),
        origin: point.optional(),
        x_axis: point.optional().describe('X direction [x, y, z] (with y_axis).'),
        y_axis: point.optional().describe('A direction in the XY plane of the UCS, made perpendicular to x_axis.'),
        name: z.string().min(1).max(255).optional().describe('Restore this saved UCS.'),
        save_as: z.string().min(1).max(255).optional().describe('Save the new UCS under this name (must not exist).'),
        plan_view: z.boolean().optional(),
      }),

    cadTool(b, 'reset_ucs', "Set the user's UCS back to World (WCS). Your coordinates were WCS all along; this only resets the user's working plane.", {}),

    cadTool(b, 'check_interference',
      'CHECK INTERFERENCE (clashes) between 3D solids: every pair of handles, or each of handles against each of other_handles (e.g. beams against ducts). ' +
        `Returns interfere, the clashing pairs with their overlap volume (volume null = AutoCAD saw a clash but could not measure it: still a clash), touching_pairs (only touching), unconfirmed_count / unconfirmed_pairs / unconfirmed_note for flagged pairs past the measuring limit (they may only touch: not clashes and not safe; say so and check a smaller list), truncated + truncated_note when more than ${SOLID2_LIMITS.MaxInterferenceCandidates} pairs with touching bounding boxes had to be cut (tell the user; check smaller groups), at most ${SOLID2_LIMITS.MaxInterferenceSolids} solids per list; each pair is checked once even when the lists overlap; each clash has layer_a / layer_b. Read-only unless table_position (a clash table: pair, layers, overlap m³).`,
      {
        handles: handles.max(SOLID2_LIMITS.MaxInterferenceSolids),
        other_handles: handles.max(SOLID2_LIMITS.MaxInterferenceSolids).optional(),
        ...solidTable,
        decimals: z.number().int().min(0).max(6).optional(),
      }),
  ];
}
