import { z } from 'zod';
import { angle, cadTool, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/** The 3D tools: only machines on the 3D or Full plan load them (the plugin labels them ToolGroup.ThreeD). */
export const SOLID_TOOL_NAMES = [
  'draw_box', 'draw_cylinder', 'draw_sphere', 'draw_wedge', 'draw_torus', 'draw_pyramid', 'extrude', 'revolve',
  'solid_union', 'solid_subtract', 'solid_intersect', 'set_view_3d', 'set_visual_style', 'get_solid_info',
] as const;

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

const RET = ' Returns {"handles": [...], "volume" (mm³), "volume_m3", "min", "max"} of the new solid: compare them with what you meant to build.';

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
    cadTool(b, 'draw_box',
      'Build a 3D solid BOX length (X) × width (Y) × height (Z), mm. Place it by corner (the min x/y/z corner, default [0,0,0]) or center; ' +
        'rotation turns it about Z around that point. Negative height with corner builds downwards. Example wall 4 m × 220 × 3.3 m: ' +
        '{"corner": [0, 0, 0], "length": 4000, "width": 220, "height": 3300, "layer": "A-WALL-3D"}.' + RET,
      {
        length: z.number().positive().describe('Size along X (mm).'),
        width: z.number().positive().describe('Size along Y (mm).'),
        height: z.number().describe('Size along Z (mm).'),
        corner: point.optional(),
        center: point.optional().describe('Centre of the box (alternative to corner).'),
        rotation: angle('Rotation about Z').optional(),
        ...entityProps,
      }),

    cadTool(b, 'draw_cylinder',
      'Build a 3D solid CYLINDER, CONE (top_radius 0) or frustum. base = centre of the bottom face. Axis: height along +Z (negative = downwards) ' +
        'or top_center for any direction. Example column D400 × 3.3 m: {"base": [0, 0, 0], "radius": 200, "height": 3300}.' + RET,
      {
        base: point.describe('Centre of the base circle [x, y, z].'),
        radius: z.number().positive().optional().describe('Base radius (mm).'),
        diameter: z.number().positive().optional().describe('Base diameter (alternative to radius).'),
        top_radius: z.number().min(0).optional().describe('Top radius (default = radius; 0 = cone).'),
        height: z.number().optional().describe('Height along Z (mm, negative = downwards).'),
        top_center: point.optional().describe('Centre of the top face, for an axis not along Z (instead of height).'),
        ...entityProps,
      }),

    cadTool(b, 'draw_sphere', 'Build a 3D solid SPHERE from center and radius (or diameter).' + RET, {
      center: point,
      radius: z.number().positive().optional(),
      diameter: z.number().positive().optional(),
      ...entityProps,
    }),

    cadTool(b, 'draw_wedge',
      'Build a 3D solid WEDGE on a length (X) × width (Y) base from corner (min corner): the vertical face of the full height stands at the ' +
        'corner side (x = corner x) and the slope goes down to 0 at x + length. rotation turns it about Z around the corner (e.g. 180 to slope towards -X). ' +
        'Typical use: ramps, sloped roofs, stair stringers.' + RET,
      {
        length: z.number().positive(),
        width: z.number().positive(),
        height: z.number().positive(),
        corner: point.optional().describe('Min corner of the base (default [0, 0, 0]).'),
        rotation: angle('Rotation about Z').optional(),
        ...entityProps,
      }),

    cadTool(b, 'draw_torus',
      'Build a 3D solid TORUS (ring) around center in the XY plane: radius = centre to the tube centre line, tube_radius = tube radius (< radius).' + RET,
      {
        center: point,
        radius: z.number().positive(),
        tube_radius: z.number().positive(),
        ...entityProps,
      }),

    cadTool(b, 'draw_pyramid',
      'Build a 3D solid PYRAMID (or frustum with top_radius > 0) with sides 3-32 (default 4). radius = radius of the circle INSCRIBED in the base polygon ' +
        '(a square base of side 1000 has radius 500). base = centre of the base; axis: height along Z or top_center. rotation turns the polygon about its axis.' + RET,
      {
        base: point,
        sides: z.number().int().min(3).max(32).optional(),
        radius: z.number().positive(),
        top_radius: z.number().min(0).optional().describe('Inscribed radius of the top (default 0 = pointed).'),
        height: z.number().optional(),
        top_center: point.optional(),
        rotation: angle('Rotation of the polygon about the axis').optional(),
        ...entityProps,
      }),

    cadTool(b, 'extrude',
      'EXTRUDE closed 2D profiles into 3D solids along their normal (profiles in the XY plane go up +Z; negative height goes down). ' +
        'taper_angle in degrees (positive narrows the solid towards the top, like EXTRUDE). One solid per closed area. ' +
        'Typical use: walls, slabs, footings from plan outlines: draw the outline on XY (or pass profile_points) and extrude by the height.' + RET,
      {
        ...profile,
        height: z.number().describe('Extrusion height (mm, along the profile normal; negative = opposite).'),
        taper_angle: z.number().min(-89.9).max(89.9).optional().describe('Taper angle in degrees (default 0).'),
        ...entityProps,
      }),

    cadTool(b, 'revolve',
      'REVOLVE closed 2D profiles about the axis axis_start → axis_end into 3D solids (angle degrees, default 360, counter-clockwise looking from axis_end towards axis_start). ' +
        'The axis must lie in the profile plane and must not cross the profile. Typical use: columns with capitals, vases, round tanks, pipes.' + RET,
      {
        ...profile,
        axis_start: point,
        axis_end: point,
        angle: z.number().gt(0).max(360).optional(),
        ...entityProps,
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
        'Use it after building to check the model by numbers (e.g. a 4000 × 220 × 3300 wall must be 2.904 m³; a wall with a 900 × 2100 door cut out loses 0.416 m³).',
      { handles: handles.max(200) },
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
  ];
}
