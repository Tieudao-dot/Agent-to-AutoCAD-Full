using System;
using System.Collections.Generic;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// AutoCAD-independent 2D geometry helpers (unit-tested on any OS).
    /// Points are double[] {x, y, z}; angles in radians unless the name says Deg.
    /// </summary>
    public static class Geometry
    {
        public const double Eps = 1e-9;

        public static double DegToRad(double deg) => deg * Math.PI / 180.0;
        public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

        public static double NormalizeRad(double a)
        {
            double t = a % (2 * Math.PI);
            if (t < 0) t += 2 * Math.PI;
            return t;
        }

        public static double Dist(double[] a, double[] b)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1], dz = (b.Length > 2 ? b[2] : 0) - (a.Length > 2 ? a[2] : 0);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static double[] P(double x, double y, double z = 0) => new[] { x, y, z };

        /// <summary>Corners of an axis-aligned (optionally rotated about corner1) rectangle, CCW.</summary>
        public static List<double[]> Rectangle(double[] c1, double[] c2)
        {
            double x1 = Math.Min(c1[0], c2[0]), x2 = Math.Max(c1[0], c2[0]);
            double y1 = Math.Min(c1[1], c2[1]), y2 = Math.Max(c1[1], c2[1]);
            double z = c1.Length > 2 ? c1[2] : 0;
            if (x2 - x1 < Eps || y2 - y1 < Eps) throw ToolException.Invalid("draw_rectangle: the two corners must differ in both X and Y.");
            return new List<double[]> { P(x1, y1, z), P(x2, y1, z), P(x2, y2, z), P(x1, y2, z) };
        }

        /// <summary>Rectangle from a corner + width/height (negative = other side), rotated by rotDeg about the corner.</summary>
        public static List<double[]> Rectangle(double[] corner, double width, double height, double rotDeg)
        {
            if (Math.Abs(width) < Eps || Math.Abs(height) < Eps) throw ToolException.Invalid("draw_rectangle: width and height must be non-zero.");
            double a = DegToRad(rotDeg), c = Math.Cos(a), s = Math.Sin(a);
            var local = new[] { new[] { 0.0, 0 }, new[] { width, 0 }, new[] { width, height }, new[] { 0.0, height } };
            var r = new List<double[]>();
            double z = corner.Length > 2 ? corner[2] : 0;
            foreach (var l in local) r.Add(P(corner[0] + l[0] * c - l[1] * s, corner[1] + l[0] * s + l[1] * c, z));
            if (SignedArea(r) < 0) r.Reverse();
            return r;
        }

        /// <summary>Regular polygon. inscribed: vertices on the circle; otherwise edges tangent to it.</summary>
        public static List<double[]> RegularPolygon(double[] center, int sides, double radius, bool inscribed, double rotDeg)
        {
            if (sides < 3 || sides > 1024) throw ToolException.Invalid("draw_polygon: 'sides' must be between 3 and 1024.");
            if (radius <= 0) throw ToolException.Invalid("draw_polygon: 'radius' must be > 0.");
            double vr = inscribed ? radius : radius / Math.Cos(Math.PI / sides);
            // circumscribed: rotate half a step so that an edge (not a vertex) is at rotDeg
            double start = DegToRad(rotDeg) + (inscribed ? 0 : Math.PI / sides);
            var r = new List<double[]>(sides);
            double z = center.Length > 2 ? center[2] : 0;
            for (int i = 0; i < sides; i++)
            {
                double a = start + 2 * Math.PI * i / sides;
                r.Add(P(center[0] + vr * Math.Cos(a), center[1] + vr * Math.Sin(a), z));
            }
            return r;
        }

        /// <summary>Shoelace area (positive = counter-clockwise).</summary>
        public static double SignedArea(IList<double[]> pts)
        {
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % pts.Count];
                a += p[0] * q[1] - q[0] * p[1];
            }
            return a / 2;
        }

        /// <summary>Perimeter of a closed polygon.</summary>
        public static double Perimeter(IList<double[]> pts, bool closed = true)
        {
            double l = 0;
            int n = closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < n; i++) l += Dist(pts[i], pts[(i + 1) % pts.Count]);
            return l;
        }

        /// <summary>
        /// Circle through three points. Returns (center, radius, startAngle, endAngle) of the
        /// counter-clockwise arc that starts at p1, passes through p2 and ends at p3.
        /// </summary>
        public static (double[] center, double radius, double start, double end) ArcFrom3Points(double[] p1, double[] p2, double[] p3)
        {
            double ax = p1[0], ay = p1[1], bx = p2[0], by = p2[1], cx = p3[0], cy = p3[1];
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < Eps) throw ToolException.Invalid("draw_arc: p1, p2, p3 are collinear; they do not define an arc.");
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            double ux = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            double uy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            var center = P(ux, uy, p1.Length > 2 ? p1[2] : 0);
            double r = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
            double t1 = Math.Atan2(ay - uy, ax - ux), t2 = Math.Atan2(by - uy, bx - ux), t3 = Math.Atan2(cy - uy, cx - ux);
            // is p2 on the CCW sweep from p1 to p3?
            bool ccw = NormalizeRad(t2 - t1) < NormalizeRad(t3 - t1);
            return ccw ? (center, r, NormalizeRad(t1), NormalizeRad(t3)) : (center, r, NormalizeRad(t3), NormalizeRad(t1));
        }

        /// <summary>
        /// Revision cloud as polyline vertices + bulges: each polygon edge is split into
        /// chords of about arcLength, every chord becomes an outward arc.
        /// </summary>
        public static (List<double[]> vertices, List<double> bulges) RevCloud(IList<double[]> polygon, double arcLength)
        {
            if (polygon.Count < 3) throw ToolException.Invalid("draw_revcloud: 'points' needs at least 3 points.");
            if (arcLength <= 0) throw ToolException.Invalid("draw_revcloud: 'arc_length' must be > 0.");
            var pts = new List<double[]>(polygon);
            if (Dist(pts[0], pts[pts.Count - 1]) < Eps) pts.RemoveAt(pts.Count - 1);
            if (SignedArea(pts) < 0) pts.Reverse(); // make CCW: right side of travel = outside
            const double bulge = 0.5773502691896257; // tan(120°/4): arcs with 120° included angle
            var verts = new List<double[]>();
            var bulges = new List<double>();
            int segTotal = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                double len = Dist(a, b);
                int n = Math.Max(1, (int)Math.Round(len / arcLength));
                for (int k = 0; k < n; k++)
                {
                    double t = (double)k / n;
                    verts.Add(P(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a.Length > 2 ? a[2] : 0));
                    bulges.Add(bulge); // positive bulge on a CCW loop bulges to the right = outward
                    if (++segTotal > 20000) throw ToolException.Invalid("draw_revcloud: too many arcs; increase 'arc_length'.");
                }
            }
            return (verts, bulges);
        }

        private static readonly int[] LineWeights = { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };

        /// <summary>
        /// Lineweight from mm (0.25) or hundredths of mm (25) to the nearest AutoCAD value
        /// in hundredths of mm. -1 = ByLayer, -2 = ByBlock, -3 = Default.
        /// </summary>
        public static int LineWeightFrom(object v, string paramName = "lineweight")
        {
            if (v is string s)
            {
                switch (s.Trim().ToLowerInvariant())
                {
                    case "bylayer": return -1;
                    case "byblock": return -2;
                    case "default": return -3;
                }
                if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) v = parsed;
            }
            if (!(v is double d) || d < 0 || d > 211)
                throw ToolException.Invalid($"'{paramName}' must be a lineweight in mm (e.g. 0.25, 0.5) or \"bylayer\"/\"default\".");
            double hundredths = d <= 2.11 + Eps ? d * 100 : d;
            int best = LineWeights[0];
            foreach (var w in LineWeights)
                if (Math.Abs(w - hundredths) < Math.Abs(best - hundredths)) best = w;
            return best;
        }

        /// <summary>Direction vector from [dx,dy] or an angle in degrees.</summary>
        public static double[] Direction(double[] vector, double? angleDeg)
        {
            if (vector != null)
            {
                double len = Math.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);
                if (len < Eps) throw ToolException.Invalid("'direction' must be a non-zero vector.");
                return new[] { vector[0] / len, vector[1] / len, vector[2] / len };
            }
            if (angleDeg.HasValue)
            {
                double a = DegToRad(angleDeg.Value);
                return new[] { Math.Cos(a), Math.Sin(a), 0 };
            }
            throw ToolException.Invalid("give 'direction' as a vector [dx, dy] or 'angle' in degrees.");
        }
    }
}
