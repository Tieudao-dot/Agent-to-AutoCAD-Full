using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>One edge of a solid as get_solid_info lists it (Brep order, WCS mm).</summary>
    public sealed class EdgeDesc
    {
        public int Index;
        public double[] Start;
        public double[] End;
        /// <summary>A point in the middle of the edge (on the curve).</summary>
        public double[] Mid;
        public bool Straight;
        /// <summary>Indices of the faces the edge bounds (usually 2).</summary>
        public List<int> Faces = new List<int>();
    }

    /// <summary>One face of a solid as get_solid_info lists it (Brep order, WCS mm).</summary>
    public sealed class FaceDesc
    {
        public int Index;
        public bool Planar;
        /// <summary>Outward unit normal of a planar face; null for curved faces.</summary>
        public double[] Normal;
        /// <summary>Centre of the face's bounding box.</summary>
        public double[] Center;
        public double[] Min;
        public double[] Max;
        public double Area;
    }

    /// <summary>A plane as point + unit normal (slice_solid, mirror_3d).</summary>
    public sealed class PlaneDef
    {
        public double[] Point;
        public double[] Normal;
    }

    /// <summary>
    /// 3D phase 2, AutoCAD-free (unit-tested on CI): picking edges / faces by index or filter, the topology id that
    /// keeps indices honest between get_solid_info and the editing tools, planes, the align matrix, UCS presets and
    /// the parameter limits shared with the assistant (agent/src/tools/solids.ts).
    /// </summary>
    public static class Solid2Logic
    {
        // ------------------------------------------------------------------ limits shared with the assistant

        /// <summary>Edges / faces get_solid_info lists per solid by default, and at most.</summary>
        public const int DefaultListItems = 60;
        public const int MaxListItems = 300;
        /// <summary>Edges one fillet_edges / chamfer_edges call may take; faces one shell_solid may open.</summary>
        public const int MaxEdgeSelection = 500;
        public const int MaxFaceSelection = 100;
        /// <summary>Sections of a loft.</summary>
        public const int MinLoftSections = 2;
        public const int MaxLoftSections = 50;
        /// <summary>Solids check_interference compares (all pairs, or handles × other_handles).</summary>
        public const int MaxInterferenceSolids = 100;
        public const int MaxInterferencePairs = 200;
        /// <summary>Sweep twist (degrees) and scale.</summary>
        public const double MaxTwist = 3600;
        public const double MinSweepScale = 0.01;
        public const double MaxSweepScale = 100;

        public static readonly string[] EdgeFilters =
        {
            "all", "straight", "curved", "parallel_x", "parallel_y", "parallel_z", "top", "bottom", "front", "back", "left", "right",
        };

        public static readonly string[] FaceSides = { "top", "bottom", "front", "back", "left", "right" };

        public static readonly string[] UcsPresets = { "world", "top", "bottom", "front", "back", "left", "right" };

        // ------------------------------------------------------------------ topology id

        /// <summary>
        /// A short id of the solid's edges and faces in Brep order, relative to the min corner of its bounding box
        /// (so a solid moved as a whole keeps it), rounded to 0.01 mm. Same id = edge / face i is the same one.
        /// </summary>
        public static string TopologyId(IList<EdgeDesc> edges, IList<FaceDesc> faces, double[] min)
        {
            var o = min ?? new double[] { 0, 0, 0 };
            var sb = new StringBuilder();
            sb.Append(edges.Count).Append('/').Append(faces.Count).Append(';');
            void P(double[] p)
            {
                if (p == null) { sb.Append("n;"); return; }
                for (int i = 0; i < 3; i++)
                    sb.Append(Math.Round((i < p.Length ? p[i] : 0) - (i < o.Length ? o[i] : 0), 2).ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(';');
            }
            foreach (var e in edges) { P(e.Start); P(e.End); P(e.Mid); sb.Append(e.Straight ? 's' : 'c'); }
            foreach (var f in faces) { P(f.Center); sb.Append(f.Planar ? 'p' : 'c'); }
            // FNV-1a 64
            ulong h = 14695981039346656037UL;
            foreach (var ch in sb.ToString()) { h ^= ch; h *= 1099511628211UL; }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static string StaleMessage(string tool, string handle) =>
            $"{tool}: solid {handle} has changed since get_solid_info listed its edges / faces (its topology_id differs), so the indices may point at other edges. " +
            "Nothing was changed. Call get_solid_info again with edges / faces and use the new indices and topology_id (or use edge_filter / face_filter).";

        // ------------------------------------------------------------------ geometry helpers

        private static double Z(double[] p) => p.Length > 2 ? p[2] : 0;
        private static double[] V3(double[] p) => new[] { p[0], p[1], Z(p) };

        /// <summary>Tolerance for "on the top plane" / "parallel": proportional to the solid's size.</summary>
        public static double Tolerance(double[] min, double[] max)
        {
            var d = SolidLogic.Len(SolidLogic.Sub(max, min));
            return Math.Max(1e-6 * d, 1e-4);
        }

        private static int Axis(string side)
        {
            switch (side)
            {
                case "left": case "right": return 0;
                case "front": case "back": return 1;
                default: return 2;
            }
        }

        /// <summary>front = min Y (the front view looks towards +Y), back = max Y, left = min X, right = max X, bottom / top = min / max Z.</summary>
        private static bool AtMax(string side) => side == "right" || side == "back" || side == "top";

        private static double[] SideNormal(string side)
        {
            var n = new double[3];
            n[Axis(side)] = AtMax(side) ? 1 : -1;
            return n;
        }

        private static bool OnSide(double[] p, string side, double[] min, double[] max, double tol)
        {
            int a = Axis(side);
            double target = AtMax(side) ? V3(max)[a] : V3(min)[a];
            return Math.Abs(V3(p)[a] - target) <= tol;
        }

        private static bool Parallel(EdgeDesc e, int axis, double tol)
        {
            if (!e.Straight) return false;
            var d = SolidLogic.Sub(e.End, e.Start);
            double len = SolidLogic.Len(d);
            if (len < 1e-9) return false;
            for (int i = 0; i < 3; i++) if (i != axis && Math.Abs(d[i]) > tol) return false;
            return true;
        }

        private static string CheckFilter(string f, string tool)
        {
            var s = (f ?? "").Trim().ToLowerInvariant();
            if (!EdgeFilters.Contains(s))
                throw ToolException.Invalid($"{tool}: unknown edge_filter '{f}'. Use: {string.Join(", ", EdgeFilters)}.");
            return s;
        }

        // ------------------------------------------------------------------ edge / face selection

        /// <summary>
        /// The edges a filter list picks (every filter must hold: ["top", "parallel_x"] = the top edges along X).
        /// "top" … "right" = the edge lies on that side plane of the solid's bounding box.
        /// </summary>
        public static List<int> FilterEdges(IList<EdgeDesc> edges, IEnumerable<string> filters, double[] min, double[] max, string tool)
        {
            var fs = (filters ?? Enumerable.Empty<string>()).Select(f => CheckFilter(f, tool)).ToList();
            if (fs.Count == 0) throw ToolException.Invalid($"{tool}: edge_filter is empty. Use e.g. [\"parallel_z\"] or [\"all\"].");
            double tol = Tolerance(min, max);
            bool Holds(EdgeDesc e, string f)
            {
                switch (f)
                {
                    case "all": return true;
                    case "straight": return e.Straight;
                    case "curved": return !e.Straight;
                    case "parallel_x": return Parallel(e, 0, tol);
                    case "parallel_y": return Parallel(e, 1, tol);
                    case "parallel_z": return Parallel(e, 2, tol);
                    default: return OnSide(e.Start, f, min, max, tol) && OnSide(e.End, f, min, max, tol) && (e.Mid == null || OnSide(e.Mid, f, min, max, tol));
                }
            }
            return edges.Where(e => fs.All(f => Holds(e, f))).Select(e => e.Index).ToList();
        }

        /// <summary>The planar faces lying on these sides of the bounding box with the outward normal of that side (any side may match).</summary>
        public static List<int> FilterFaces(IList<FaceDesc> faces, IEnumerable<string> sides, double[] min, double[] max, string tool)
        {
            var ss = (sides ?? Enumerable.Empty<string>()).Select(x => (x ?? "").Trim().ToLowerInvariant()).ToList();
            if (ss.Count == 0) throw ToolException.Invalid($"{tool}: face_filter is empty. Use e.g. [\"top\"].");
            foreach (var s in ss)
                if (!FaceSides.Contains(s)) throw ToolException.Invalid($"{tool}: unknown face_filter '{s}'. Use: {string.Join(", ", FaceSides)}.");
            double tol = Tolerance(min, max);
            var picked = new List<int>();
            foreach (var f in faces)
            {
                if (!f.Planar || f.Normal == null) continue;
                foreach (var s in ss)
                {
                    var n = SideNormal(s);
                    // planar, normal along the side's axis (either sign: Brep orientation is not trusted), flat on that side plane
                    if (Math.Abs(Math.Abs(SolidLogic.Dot(f.Normal, n)) - 1) > 1e-6) continue;
                    if (f.Min != null && f.Max != null ? OnSide(f.Min, s, min, max, tol) && OnSide(f.Max, s, min, max, tol) : OnSide(f.Center, s, min, max, tol))
                    {
                        picked.Add(f.Index);
                        break;
                    }
                }
            }
            return picked;
        }

        /// <summary>Indices given by Claude: in range, no duplicates.</summary>
        public static List<int> CheckIndices(IEnumerable<int> indices, int count, string what, string tool, int max)
        {
            var list = indices.ToList();
            if (list.Count == 0) throw ToolException.Invalid($"{tool}: '{what}' is empty.");
            if (list.Count > max) throw ToolException.Invalid($"{tool}: at most {max} {what} per call.");
            foreach (var i in list)
                if (i < 0 || i >= count) throw ToolException.Invalid($"{tool}: {what} index {i} is out of range (the solid has {count}; indices start at 0).");
            if (list.Distinct().Count() != list.Count) throw ToolException.Invalid($"{tool}: the same {what} index is listed twice.");
            return list;
        }

        /// <summary>
        /// chamfer_edges: groups the edges by a base face each of them bounds (ChamferEdges needs one base face per
        /// call), fewest groups first. baseFace fixes the face for every edge (then every edge must bound it).
        /// </summary>
        public static List<(int face, List<int> edges)> ChamferGroups(IList<EdgeDesc> edges, IList<int> picked, int? baseFace, string tool)
        {
            var byIndex = edges.ToDictionary(e => e.Index);
            if (baseFace.HasValue)
            {
                foreach (var i in picked)
                    if (!byIndex[i].Faces.Contains(baseFace.Value))
                        throw ToolException.Invalid($"{tool}: edge {i} is not on base_face {baseFace.Value}. With base_face every edge must bound that face (get_solid_info lists the faces of each edge).");
                return new List<(int, List<int>)> { (baseFace.Value, picked.ToList()) };
            }
            var left = new HashSet<int>(picked);
            var groups = new List<(int, List<int>)>();
            while (left.Count > 0)
            {
                // greedy set cover: the face shared by the most remaining edges (lowest index on ties)
                var best = left.SelectMany(i => byIndex[i].Faces.Distinct().Select(f => (f, i)))
                    .GroupBy(x => x.f).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
                if (best == null) throw ToolException.Invalid($"{tool}: edge {left.First()} bounds no face; it cannot be chamfered.");
                var es = best.Select(x => x.i).Distinct().OrderBy(i => i).ToList();
                groups.Add((best.Key, es));
                foreach (var i in es) left.Remove(i);
            }
            return groups;
        }

        /// <summary>
        /// After one chamfer group changed the solid, finds an earlier edge again: a straight edge on the same line
        /// (its middle on the new edge's line, same direction), or a curved edge with the same middle point.
        /// </summary>
        public static int? MatchEdge(EdgeDesc old, IList<EdgeDesc> now, double tol)
        {
            EdgeDesc best = null;
            double bestD = double.MaxValue;
            foreach (var e in now)
            {
                if (e.Straight != old.Straight) continue;
                double d;
                if (old.Straight)
                {
                    var u = SolidLogic.Sub(old.End, old.Start);
                    var v = SolidLogic.Sub(e.End, e.Start);
                    double lu = SolidLogic.Len(u), lv = SolidLogic.Len(v);
                    if (lu < 1e-9 || lv < 1e-9) continue;
                    if (SolidLogic.Len(SolidLogic.Cross(u, v)) / (lu * lv) > 1e-6) continue;
                    // distance of the old middle to the new edge's line, and it must lie within the new edge
                    var w = SolidLogic.Sub(old.Mid ?? old.Start, e.Start);
                    double t = SolidLogic.Dot(w, v) / (lv * lv);
                    if (t < -1e-6 || t > 1 + 1e-6) continue;
                    d = SolidLogic.Len(SolidLogic.Cross(w, v)) / lv;
                }
                else
                {
                    if (old.Mid == null || e.Mid == null) continue;
                    d = SolidLogic.Len(SolidLogic.Sub(old.Mid, e.Mid));
                }
                if (d <= tol && d < bestD) { bestD = d; best = e; }
            }
            return best?.Index;
        }

        // ------------------------------------------------------------------ planes

        /// <summary>
        /// A plane from 'plane_points' (3 points), or 'point' + 'normal', or 'point' + 'plane' "xy" / "yz" / "zx"
        /// (normal Z / X / Y). Errors are for Claude.
        /// </summary>
        public static PlaneDef Plane(IList<double[]> points, double[] point, double[] normal, string named, string tool)
        {
            int given = (points != null ? 1 : 0) + (normal != null ? 1 : 0) + (named != null ? 1 : 0);
            if (given == 0) throw ToolException.Invalid($"{tool}: give the plane as plane_points (3 points), point + normal, or point + plane (\"xy\", \"yz\", \"zx\").");
            if (given > 1) throw ToolException.Invalid($"{tool}: give the plane one way only: plane_points, normal or plane.");
            if (points != null)
            {
                if (points.Count != 3) throw ToolException.Invalid($"{tool}: plane_points needs exactly 3 points.");
                var a = V3(points[0]); var b = V3(points[1]); var c = V3(points[2]);
                var n = SolidLogic.Cross(SolidLogic.Sub(b, a), SolidLogic.Sub(c, a));
                double scale = Math.Max(SolidLogic.Len(SolidLogic.Sub(b, a)) * SolidLogic.Len(SolidLogic.Sub(c, a)), 1e-12);
                if (SolidLogic.Len(n) / scale < 1e-9) throw ToolException.Invalid($"{tool}: the 3 plane_points lie on one line; they do not define a plane.");
                return new PlaneDef { Point = a, Normal = SolidLogic.Unit(n, tool) };
            }
            if (point == null) throw ToolException.Invalid($"{tool}: 'point' (a point on the plane) is needed with {(normal != null ? "normal" : "plane")}.");
            if (normal != null) return new PlaneDef { Point = V3(point), Normal = SolidLogic.Unit(V3(normal), $"{tool}: 'normal' must not be [0, 0, 0]") };
            switch ((named ?? "").Trim().ToLowerInvariant())
            {
                case "xy": return new PlaneDef { Point = V3(point), Normal = new double[] { 0, 0, 1 } };
                case "yz": return new PlaneDef { Point = V3(point), Normal = new double[] { 1, 0, 0 } };
                case "zx": case "xz": return new PlaneDef { Point = V3(point), Normal = new double[] { 0, 1, 0 } };
                default: throw ToolException.Invalid($"{tool}: unknown plane '{named}'. Use \"xy\", \"yz\" or \"zx\" (or plane_points / normal).");
            }
        }

        /// <summary>Signed distance of p from the plane (positive on the normal's side).</summary>
        public static double SignedDistance(PlaneDef pl, double[] p) => SolidLogic.Dot(SolidLogic.Sub(V3(p), pl.Point), pl.Normal);

        /// <summary>slice_solid 'keep': "both" (default), "positive" (the normal's side), "negative".</summary>
        public static string SliceKeep(string keep, string tool = "slice_solid")
        {
            var k = (keep ?? "both").Trim().ToLowerInvariant();
            if (k != "both" && k != "positive" && k != "negative")
                throw ToolException.Invalid($"{tool}: keep must be \"both\", \"positive\" (the side the normal points to) or \"negative\".");
            return k;
        }

        /// <summary>True when the plane passes through the box min–max (some corner strictly on each side).</summary>
        public static bool PlaneCutsBox(PlaneDef pl, double[] min, double[] max)
        {
            double tol = Tolerance(min, max);
            bool pos = false, neg = false;
            for (int i = 0; i < 8; i++)
            {
                var c = new[] { (i & 1) == 0 ? min[0] : max[0], (i & 2) == 0 ? min[1] : max[1], (i & 4) == 0 ? Z(min) : Z(max) };
                double d = SignedDistance(pl, c);
                if (d > tol) pos = true;
                if (d < -tol) neg = true;
            }
            return pos && neg;
        }

        // ------------------------------------------------------------------ transforms

        /// <summary>Rotation axis of rotate_3d: unit direction from start to end; the angle in radians.</summary>
        public static (double[] dir, double angleRad) RotationAxis(double[] start, double[] end, double angleDeg, string tool = "rotate_3d")
        {
            var d = SolidLogic.Sub(end, start);
            var u = SolidLogic.Unit(d, $"{tool}: axis_start and axis_end must differ");
            if (double.IsNaN(angleDeg) || double.IsInfinity(angleDeg)) throw ToolException.Invalid($"{tool}: 'angle' must be a number.");
            if (Math.Abs(angleDeg) > 3600) throw ToolException.Invalid($"{tool}: 'angle' must be within ±3600 degrees.");
            return (u, angleDeg * Math.PI / 180);
        }

        /// <summary>
        /// The ALIGN transform as a row-major 4×4 matrix (Matrix3d(double[])): 1 pair = move; 2 pairs = move and turn
        /// the first direction onto the second (smallest rotation); 3 pairs = also turn the plane of the 3 points.
        /// scale (2 or 3 pairs) scales by |d2 − d1| / |s2 − s1| about the first point.
        /// </summary>
        public static double[] AlignMatrix(IList<double[]> source, IList<double[]> dest, bool scale, string tool = "align")
        {
            if (source == null || dest == null || source.Count != dest.Count || source.Count < 1 || source.Count > 3)
                throw ToolException.Invalid($"{tool}: give 1 to 3 source_points and the same number of dest_points.");
            var s = source.Select(V3).ToList();
            var d = dest.Select(V3).ToList();
            if (s.Count == 1) return Translation(SolidLogic.Sub(d[0], s[0]));
            var su = SolidLogic.Sub(s[1], s[0]);
            var du = SolidLogic.Sub(d[1], d[0]);
            double ls = SolidLogic.Len(su), ld = SolidLogic.Len(du);
            if (ls < 1e-9) throw ToolException.Invalid($"{tool}: the first two source_points must differ");
            if (ld < 1e-9) throw ToolException.Invalid($"{tool}: the first two dest_points must differ");
            double k = scale ? ld / ls : 1;
            double[] r;
            if (s.Count == 2)
            {
                r = RotationBetween(SolidLogic.Scale(su, 1 / ls), SolidLogic.Scale(du, 1 / ld));
            }
            else
            {
                var sf = Frame(s, tool, "source_points");
                var df = Frame(d, tool, "dest_points");
                // R = D · Sᵀ (columns are the frame axes)
                r = new double[9];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        double v = 0;
                        for (int a = 0; a < 3; a++) v += df[a][i] * sf[a][j];
                        r[i * 3 + j] = v;
                    }
            }
            // x' = d0 + k R (x − s0)
            var m = new double[16];
            for (int i = 0; i < 3; i++)
            {
                double t = d[0][i];
                for (int j = 0; j < 3; j++) { m[i * 4 + j] = k * r[i * 3 + j]; t -= k * r[i * 3 + j] * s[0][j]; }
                m[i * 4 + 3] = t;
            }
            m[15] = 1;
            return m;
        }

        private static double[] Translation(double[] v) => new double[] { 1, 0, 0, v[0], 0, 1, 0, v[1], 0, 0, 1, v[2], 0, 0, 0, 1 };

        /// <summary>Orthonormal frame (x, y, z) of 3 points: x = p1 − p0, y in the plane towards p2.</summary>
        private static double[][] Frame(IList<double[]> p, string tool, string what)
        {
            var x = SolidLogic.Sub(p[1], p[0]);
            var w = SolidLogic.Sub(p[2], p[0]);
            var z = SolidLogic.Cross(x, w);
            if (SolidLogic.Len(z) < 1e-9 * Math.Max(SolidLogic.Len(x) * SolidLogic.Len(w), 1e-12))
                throw ToolException.Invalid($"{tool}: the 3 {what} lie on one line; use 2 pairs, or pick a third point off the line.");
            x = SolidLogic.Unit(x, tool);
            z = SolidLogic.Unit(z, tool);
            var y = SolidLogic.Cross(z, x);
            return new[] { x, y, z };
        }

        /// <summary>Row-major 3×3 rotation turning unit a onto unit b by the smallest angle (Rodrigues).</summary>
        public static double[] RotationBetween(double[] a, double[] b)
        {
            var v = SolidLogic.Cross(a, b);
            double c = SolidLogic.Dot(a, b);
            double s = SolidLogic.Len(v);
            if (s < 1e-12)
            {
                if (c > 0) return new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
                // opposite: half turn about any axis perpendicular to a (prefer one in the XY plane / about Z)
                var axis = Math.Abs(a[2]) < 0.9 ? SolidLogic.Unit(SolidLogic.Cross(a, new double[] { 0, 0, 1 }), "") : SolidLogic.Unit(SolidLogic.Cross(a, new double[] { 1, 0, 0 }), "");
                // for a horizontal a, turn about Z: keeps "up" up
                if (Math.Abs(a[2]) < 1e-9) axis = new double[] { 0, 0, 1 };
                return AxisAngle(axis, Math.PI);
            }
            return AxisAngle(SolidLogic.Scale(v, 1 / s), Math.Atan2(s, c));
        }

        public static double[] AxisAngle(double[] u, double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle), t = 1 - c;
            double x = u[0], y = u[1], z = u[2];
            return new[]
            {
                t * x * x + c, t * x * y - s * z, t * x * z + s * y,
                t * x * y + s * z, t * y * y + c, t * y * z - s * x,
                t * x * z - s * y, t * y * z + s * x, t * z * z + c,
            };
        }

        /// <summary>Applies a row-major 4×4 matrix to a point (tests, result checks).</summary>
        public static double[] Apply(double[] m, double[] p)
        {
            var q = V3(p);
            return new[]
            {
                m[0] * q[0] + m[1] * q[1] + m[2] * q[2] + m[3],
                m[4] * q[0] + m[5] * q[1] + m[6] * q[2] + m[7],
                m[8] * q[0] + m[9] * q[1] + m[10] * q[2] + m[11],
            };
        }

        // ------------------------------------------------------------------ UCS

        /// <summary>
        /// The axes (origin, x, y) of a UCS preset, like the UCS command's named orthographic UCSs relative to world:
        /// top = world XY, front = XZ plane seen from the front (x = +X, y = +Z), right = YZ (x = +Y, y = +Z) …
        /// </summary>
        public static (double[] x, double[] y) UcsPreset(string name, string tool = "set_ucs")
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "world":
                case "top": return (new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 });
                case "bottom": return (new double[] { 1, 0, 0 }, new double[] { 0, -1, 0 });
                case "front": return (new double[] { 1, 0, 0 }, new double[] { 0, 0, 1 });
                case "back": return (new double[] { -1, 0, 0 }, new double[] { 0, 0, 1 });
                case "left": return (new double[] { 0, -1, 0 }, new double[] { 0, 0, 1 });
                case "right": return (new double[] { 0, 1, 0 }, new double[] { 0, 0, 1 });
                default: throw ToolException.Invalid($"{tool}: unknown preset '{name}'. Use: {string.Join(", ", UcsPresets)}.");
            }
        }

        /// <summary>UCS axes from x_axis and a y direction (made perpendicular to x); z = x × y.</summary>
        public static (double[] x, double[] y, double[] z) UcsAxes(double[] xAxis, double[] yDir, string tool = "set_ucs")
        {
            var x = SolidLogic.Unit(V3(xAxis), $"{tool}: x_axis must not be [0, 0, 0]");
            var yd = V3(yDir);
            var y = SolidLogic.Sub(yd, SolidLogic.Scale(x, SolidLogic.Dot(yd, x)));
            if (SolidLogic.Len(y) < 1e-9 * Math.Max(SolidLogic.Len(yd), 1e-12)) throw ToolException.Invalid($"{tool}: y_axis must not be parallel to x_axis.");
            y = SolidLogic.Unit(y, tool);
            return (x, y, SolidLogic.Cross(x, y));
        }

        /// <summary>Names AutoCAD accepts for a saved UCS (no &lt;&gt;/\":;?*|,=` and at most 255 characters).</summary>
        public static string UcsName(string name, string tool = "set_ucs")
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0 || n.Length > 255 || n.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
                throw ToolException.Invalid($"{tool}: '{name}' is not a valid UCS name (1-255 characters, none of < > / \\ \" : ; ? * | , = `).");
            return n;
        }

        // ------------------------------------------------------------------ checks of the other tools

        public static void CheckRadius(double r, string what, string tool)
        {
            if (!(r > 0) || double.IsInfinity(r)) throw ToolException.Invalid($"{tool}: '{what}' must be a positive number of mm.");
        }

        /// <summary>The volume of a box with 'edges' vertical edges filleted with radius r: each loses (1 − π/4) r² h.</summary>
        public static double FilletedBoxVolume(double l, double w, double h, double r, int verticalEdges) =>
            l * w * h - verticalEdges * (1 - Math.PI / 4) * r * r * h;

        /// <summary>Sweep options within the limits of the assistant's schema.</summary>
        public static void CheckSweep(double twistDeg, double scale, string tool = "sweep")
        {
            if (Math.Abs(twistDeg) > MaxTwist) throw ToolException.Invalid($"{tool}: twist must be within ±{MaxTwist} degrees.");
            if (scale < MinSweepScale || scale > MaxSweepScale) throw ToolException.Invalid($"{tool}: scale must be between {MinSweepScale} and {MaxSweepScale}.");
        }
    }
}
