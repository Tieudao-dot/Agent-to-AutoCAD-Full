import { z } from 'zod';
import { angle, cadTool, entityProps, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

const RET = ' Returns {"handles": [...]} of the created entity.';

/** Spec §7.1 — geometry creation. */
export function drawTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'draw_line',
      'Draw one straight LINE from start to end (mm, WCS). For more than a couple of lines use batch_execute.' + RET,
      { start: point.describe('Start point [x, y], e.g. [0, 0]'), end: point.describe('End point [x, y], e.g. [4000, 0]'), ...entityProps }),

    cadTool(b, 'draw_polyline',
      'Draw a lightweight POLYLINE (LWPOLYLINE) through vertices. Use closed=true for rooms/outlines. ' +
        'Arc segments: bulges[i] applies to the segment from vertex i to i+1 (0 = straight, 1 = semicircle bulging to the right of travel, ' +
        '-1 = to the left, tan(angle/4) in general). Example wall outline: {"vertices": [[0,0],[4000,0],[4000,6000],[0,6000]], "closed": true, "layer": "A-WALL"}.' + RET,
      {
        vertices: points(2, 'vertices'),
        closed: z.boolean().optional().describe('Close the polyline (last vertex back to first). Default false.'),
        bulges: z.array(z.number()).optional().describe('Optional bulge per segment (length = vertices-1 when open, = vertices when closed).'),
        width: z.number().min(0).optional().describe('Constant global width in mm (default 0).'),
        ...entityProps,
      }),

    cadTool(b, 'draw_circle', 'Draw a CIRCLE from center and radius (or diameter).' + RET, {
      center: point,
      radius: z.number().positive().optional().describe('Radius in mm.'),
      diameter: z.number().positive().optional().describe('Diameter in mm (alternative to radius).'),
      ...entityProps,
    }),

    cadTool(b, 'draw_arc',
      'Draw an ARC. Two ways: (a) center + radius + start_angle + end_angle (degrees, the arc runs counter-clockwise from start to end), ' +
        'or (b) three points p1 (start), p2 (any point on the arc), p3 (end).' + RET,
      {
        center: point.optional(),
        radius: z.number().positive().optional(),
        start_angle: angle('Start angle').optional(),
        end_angle: angle('End angle').optional(),
        p1: point.optional().describe('Start point (three-point form).'),
        p2: point.optional().describe('Point on the arc (three-point form).'),
        p3: point.optional().describe('End point (three-point form).'),
        ...entityProps,
      }),

    cadTool(b, 'draw_ellipse',
      'Draw an ELLIPSE (or elliptical arc). major_axis_end is the absolute point at the end of the major axis; ratio = minor/major (0 < ratio <= 1).' + RET,
      {
        center: point,
        major_axis_end: point.describe('Absolute end point of the major semi-axis, e.g. center [0,0] and [3000,0] → major radius 3000 along X.'),
        ratio: z.number().positive().max(1).describe('Minor/major axis ratio.'),
        start_angle: angle('Start parameter angle').optional().describe('For an elliptical arc; default 0.'),
        end_angle: angle('End parameter angle').optional().describe('For an elliptical arc; default 360.'),
        ...entityProps,
      }),

    cadTool(b, 'draw_spline', 'Draw a SPLINE through fit points (smooth curve, e.g. contour lines, organic shapes).' + RET, {
      fit_points: points(2, 'fit points'),
      closed: z.boolean().optional().describe('Repeat the first point at the end. Default false.'),
      ...entityProps,
    }),

    cadTool(b, 'draw_rectangle',
      'Draw a rectangle as a closed polyline. Either corner1 + corner2 (opposite corners), or corner + width + height (+ rotation in degrees about that corner).' + RET,
      {
        corner1: point.optional(),
        corner2: point.optional(),
        corner: point.optional().describe('Base corner for the width/height form.'),
        width: z.number().optional().describe('Size along X before rotation (mm, may be negative).'),
        height: z.number().optional().describe('Size along Y before rotation (mm, may be negative).'),
        rotation: angle('Rotation').optional(),
        ...entityProps,
      }),

    cadTool(b, 'draw_polygon',
      'Draw a regular polygon (closed polyline). inscribed=true puts the vertices on the circle of the given radius; false makes the edges tangent to it.' + RET,
      {
        center: point,
        sides: z.number().int().min(3).max(1024),
        radius: z.number().positive(),
        inscribed: z.boolean().optional().describe('Default true.'),
        rotation: angle('Rotation of the first vertex').optional(),
        ...entityProps,
      }),

    cadTool(b, 'draw_point', 'Draw POINT entities (node markers). Give position, or positions for many at once.' + RET, {
      position: point.optional(),
      positions: points(1).optional(),
      ...entityProps,
    }),

    cadTool(b, 'draw_xline',
      'Draw an XLINE (infinite construction line), typically for grid axes ("trục"). Give base and direction as a vector [dx, dy] ' +
        '(e.g. [0,1] vertical, [1,0] horizontal), or angle in degrees, or a second point "through".' + RET,
      {
        base: point,
        direction: point.optional().describe('Direction vector [dx, dy], e.g. [0, 1] for a vertical axis.'),
        angle: angle().optional(),
        through: point.optional().describe('Second point the line passes through.'),
        ...entityProps,
      }),

    cadTool(b, 'draw_ray', 'Draw a RAY (semi-infinite line) from base along direction / angle / through point.' + RET, {
      base: point,
      direction: point.optional().describe('Direction vector [dx, dy].'),
      angle: angle().optional(),
      through: point.optional(),
      ...entityProps,
    }),

    cadTool(b, 'draw_hatch',
      'Create a HATCH. Boundary either from existing closed curves (boundary_handles: closed polylines, circles, ellipses — preferred, associative) ' +
        'or from a polygon of points (boundary_points). pattern: "SOLID" for solid fill or a standard acadiso.pat name ' +
        '(ANSI31 diagonal lines, ANSI37 cross, AR-CONC concrete, AR-SAND sand, AR-BRSTD brick, GRAVEL, EARTH, NET, DOTS). ' +
        'scale multiplies the pattern spacing (for mm drawings ANSI31 usually needs 20-100).' + RET,
      {
        pattern: z.string().optional().describe('Pattern name, default "SOLID".'),
        scale: z.number().positive().optional().describe('Pattern scale, default 1.'),
        angle: angle('Pattern angle').optional(),
        boundary_handles: handles.optional(),
        boundary_points: points(3, 'boundary vertices').optional(),
        boundary_bulges: z.array(z.number()).optional().describe('Optional bulges for boundary_points segments.'),
        associative: z.boolean().optional().describe('Keep the hatch associative to boundary_handles. Default true.'),
        ...entityProps,
      }),

    cadTool(b, 'draw_wipeout', 'Create a WIPEOUT (masking polygon that hides objects behind it), e.g. behind text or symbols.' + RET, {
      points: points(3, 'polygon vertices'),
      ...entityProps,
    }),

    cadTool(b, 'draw_revcloud',
      'Draw a revision cloud ("mây sửa đổi") around a polygon: a closed polyline made of outward arcs of about arc_length chord.' + RET,
      {
        points: points(3, 'polygon vertices around the revised area'),
        arc_length: z.number().positive().optional().describe('Approximate arc chord length in mm (default 500; use ~ 1-2 % of the cloud size).'),
        ...entityProps,
      }),
  ];
}
