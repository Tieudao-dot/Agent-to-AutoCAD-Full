using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b.md): the AutoCAD-free geometry of the new 2D editing tools
    /// (stretch_entities, break_entity, lengthen_entity, divide_entity, match_properties) and of the upgraded ones
    /// (fillet / chamfer of polylines and arcs, join with a gap tolerance). Points are [x, y(, z)] in drawing units,
    /// angles in radians. Drawing/Edit2DTools.cs applies the results to AutoCAD objects.
    /// </summary>
    public static class Edit2DLogic
    {
        private const double Eps = 1e-9;
        private const double TwoPi = 2 * Math.PI;

        private static double[] P(double x, double y, double z = 0) => new[] { x, y, z };
        private static double Z(double[] p) => p.Length > 2 ? p[2] : 0;
        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
        private static double Norm(double a) { a %= TwoPi; return a < 0 ? a + TwoPi : a; }
        private static double Ang(double[] c, double[] p) => Norm(Math.Atan2(p[1] - c[1], p[0] - c[0]));
        private static double[] OnCircle(double[] c, double r, double a) => P(c[0] + r * Math.Cos(a), c[1] + r * Math.Sin(a), Z(c));

        // ================================================================== STRETCH

        /// <summary>Without handles, stretch_entities changes at most this many objects (more: pass handles after asking the user).</summary>
        public const int MaxStretchUnasked = 20;

        /// <summary>
        /// Test & sửa đợt B (NB-1): the parameter the assistant adds to a window stretch (no handles): how many objects the
        /// turn may still change without asking (20 minus those already changed). Claude cannot set it (not in the schema,
        /// replaced by the assistant) and macros do not record it.
        /// </summary>
        public const string WindowLimitParam = "max_unasked";

        /// <summary>The window limit: the assistant's value clamped to 0–20; none (macro replay, older assistant) = 20.</summary>
        public static int WindowLimit(double? fromAssistant)
        {
            if (fromAssistant == null || double.IsNaN(fromAssistant.Value)) return MaxStretchUnasked;
            return (int)Math.Max(0, Math.Min(MaxStretchUnasked, Math.Floor(fromAssistant.Value)));
        }

        /// <summary>Why a window stretch was refused (the handles to call again with follow).</summary>
        public static string WindowLimitMessage(int count, int limit) =>
            limit >= MaxStretchUnasked
                ? $"stretch_entities: the window would change {count} objects (more than {MaxStretchUnasked}). Tell the user how many and ask; after they agree, call again with these handles: "
                : $"stretch_entities: the window would change {count} objects, and this turn has already changed others: together more than {MaxStretchUnasked} " +
                  $"(only {limit} more allowed without asking). Tell the user how many and ask; after they agree, call again with these handles: ";

        /// <summary>The crossing window: a rectangle from two corners, or a polygon of at least 3 points.</summary>
        public static List<double[]> StretchWindow(double[] corner1, double[] corner2, IList<double[]> polygon)
        {
            if (polygon != null && polygon.Count > 0)
            {
                if (polygon.Count < 3) throw ToolException.Invalid("stretch_entities: 'polygon' needs at least 3 points.");
                if (Math.Abs(Geometry.SignedArea(polygon)) < Eps) throw ToolException.Invalid("stretch_entities: the polygon has no area.");
                return polygon.ToList();
            }
            if (corner1 == null || corner2 == null)
                throw ToolException.Invalid("stretch_entities: give the crossing window as 'corner1' + 'corner2' (or 'polygon').");
            if (Math.Abs(corner1[0] - corner2[0]) < Eps || Math.Abs(corner1[1] - corner2[1]) < Eps)
                throw ToolException.Invalid("stretch_entities: the window corners must differ in X and in Y.");
            return Geometry.Rectangle(corner1, corner2);
        }

        /// <summary>Inside or on the edge of the polygon (2D, ray casting; the edge counts as inside, like a crossing window).</summary>
        public static bool InPolygon(double[] pt, IList<double[]> poly)
        {
            double x = pt[0], y = pt[1];
            double tol = 1e-7 * Math.Max(1, poly.Max(q => Math.Max(Math.Abs(q[0]), Math.Abs(q[1]))));
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                // on the edge?
                double dx = xj - xi, dy = yj - yi, l2 = dx * dx + dy * dy;
                if (l2 > 0)
                {
                    double t = Math.Max(0, Math.Min(1, ((x - xi) * dx + (y - yi) * dy) / l2));
                    double ex = xi + t * dx - x, ey = yi + t * dy - y;
                    if (ex * ex + ey * ey <= tol * tol) return true;
                }
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        public enum StretchKind { None, Some, All }

        /// <summary>
        /// STRETCH of one object from its stretch points (vertices, ends, insertion point...): none inside the window =
        /// unchanged; all inside = moved as a whole; some = only those points move. 'inside' lists the indices inside.
        /// </summary>
        public static StretchKind Classify(IList<double[]> stretchPoints, IList<double[]> window, out List<int> inside)
        {
            inside = new List<int>();
            for (int i = 0; i < stretchPoints.Count; i++) if (InPolygon(stretchPoints[i], window)) inside.Add(i);
            if (inside.Count == 0) return StretchKind.None;
            return inside.Count == stretchPoints.Count ? StretchKind.All : StretchKind.Some;
        }

        // ================================================================== BREAK

        /// <summary>Where break_entity splits a curve and which piece it removes.</summary>
        public sealed class BreakPlan
        {
            /// <summary>Parameters to split at (sorted, inside the curve).</summary>
            public List<double> SplitAt = new List<double>();
            /// <summary>A parameter inside the piece to remove, or null (one point: the curve is only split in two).</summary>
            public double? RemoveMid;
        }

        /// <summary>
        /// BREAK: one point (b null) splits an open curve in two there; two points remove the part between them (on a closed
        /// curve, the part from a to b in the curve's direction: counter-clockwise for a circle, like AutoCAD).
        /// </summary>
        public static BreakPlan Break(bool closed, double start, double end, double a, double? b)
        {
            double span = end - start, tol = 1e-9 * Math.Max(1, Math.Abs(span));
            bool AtEnd(double t) => Math.Abs(t - start) <= tol || Math.Abs(t - end) <= tol;
            var plan = new BreakPlan();
            if (b == null || Math.Abs(b.Value - a) <= tol)
            {
                if (closed) throw ToolException.Invalid("break_entity: a closed curve (circle, closed polyline...) cannot be broken at one point; give 'point2' too (the part from point to point2 is removed).");
                if (AtEnd(a)) throw ToolException.Invalid("break_entity: the point is at an end of the curve; there is nothing to break there.");
                plan.SplitAt.Add(a);
                return plan;
            }
            double bb = b.Value;
            if (!closed)
            {
                double lo = Math.Min(a, bb), hi = Math.Max(a, bb);
                if (Math.Abs(lo - start) <= tol && Math.Abs(hi - end) <= tol)
                    throw ToolException.Invalid("break_entity: the two points are the two ends; that would remove the whole curve (use erase_entities).");
                foreach (var t in new[] { lo, hi }) if (!AtEnd(t)) plan.SplitAt.Add(t);
                plan.RemoveMid = (lo + hi) / 2;
                return plan;
            }
            // closed: remove a → b in the curve's direction (wrapping past the start)
            double len = bb >= a ? bb - a : bb + span - a;
            double mid = a + len / 2;
            if (mid >= end) mid -= span;
            plan.SplitAt.Add(Math.Min(a, bb));
            plan.SplitAt.Add(Math.Max(a, bb));
            plan.RemoveMid = mid;
            return plan;
        }

        // ================================================================== LENGTHEN

        /// <summary>lengthen_entity modes: "delta" (add / remove that length), "total" (new length), "percent" (of the current length).</summary>
        public static double NewLength(string mode, double value, double current, string tool = "lengthen_entity")
        {
            double l;
            switch ((mode ?? "delta").Trim().ToLowerInvariant())
            {
                case "delta": l = current + value; break;
                case "total":
                    if (value <= 0) throw ToolException.Invalid($"{tool}: 'total' must be > 0.");
                    l = value; break;
                case "percent":
                    if (value <= 0) throw ToolException.Invalid($"{tool}: 'percent' must be > 0 (100 = unchanged, 50 = half as long).");
                    l = current * value / 100; break;
                default: throw ToolException.Invalid($"{tool}: 'mode' must be delta, total or percent.");
            }
            if (l <= Eps * Math.Max(1, current))
                throw ToolException.Invalid($"{tool}: the curve is {current:0.###} long; that change would leave nothing (new length {l:0.###}).");
            return l;
        }

        /// <summary>A straight segment s → e with the end (atEnd) or the start moved by d along it (d &lt; 0 shortens).</summary>
        public static (double[] s, double[] e) LengthenSegment(double[] s, double[] e, double d, bool atEnd, string tool = "lengthen_entity")
        {
            double l = Dist(s, e);
            if (l < Eps) throw ToolException.Invalid($"{tool}: the end segment has no length.");
            if (l + d <= Eps * Math.Max(1, l))
                throw ToolException.Invalid($"{tool}: shortening by {-d:0.###} is more than the end segment ({l:0.###}); shorten less, or edit the polyline vertices.");
            double ux = (e[0] - s[0]) / l, uy = (e[1] - s[1]) / l;
            if (atEnd) return (s, P(e[0] + ux * d, e[1] + uy * d, Z(e)));
            return (P(s[0] - ux * d, s[1] - uy * d, Z(s)), e);
        }

        /// <summary>An arc (CCW start → end) lengthened by d along the circle at its end or start; the sweep stays in (0, 2π).</summary>
        public static (double start, double end) LengthenArc(double start, double end, double radius, double d, bool atEnd, string tool = "lengthen_entity")
        {
            double sweep = Norm(end - start);
            if (sweep < Eps) sweep = TwoPi;
            double ns = sweep + d / radius;
            if (ns <= 1e-9) throw ToolException.Invalid($"{tool}: shortening by {-d:0.###} is more than the arc's length ({sweep * radius:0.###}).");
            if (ns >= TwoPi - 1e-9) throw ToolException.Invalid($"{tool}: the arc would close into a full circle (longest possible: {TwoPi * radius:0.###}, not included); draw a circle instead.");
            return atEnd ? (start, Norm(start + ns)) : (Norm(end - ns), end);
        }

        /// <summary>
        /// The arc segment s → e (bulge b) of a polyline lengthened by d at its end (atEnd) or start, around the same centre.
        /// Returns the new end points and bulge.
        /// </summary>
        public static (double[] s, double[] e, double bulge) LengthenBulge(double[] s, double[] e, double bulge, double d, bool atEnd, string tool = "lengthen_entity")
        {
            double theta = 4 * Math.Atan(bulge), chord = Dist(s, e);
            if (chord < Eps || Math.Abs(theta) < 1e-12) throw ToolException.Invalid($"{tool}: the end arc segment has no length.");
            var (c, r) = BulgeCircle(s, e, bulge);
            double sign = Math.Sign(theta);
            double nt = Math.Abs(theta) + d / r;
            if (nt <= 1e-9) throw ToolException.Invalid($"{tool}: shortening by {-d:0.###} is more than the end arc segment ({Math.Abs(theta) * r:0.###}).");
            if (nt >= TwoPi - 1e-9) throw ToolException.Invalid($"{tool}: the end arc segment would close into a full circle.");
            double nb = sign * Math.Tan(nt / 4);
            if (atEnd) return (s, OnCircle(c, r, Ang(c, s) + sign * nt), nb);
            return (OnCircle(c, r, Ang(c, e) - sign * nt), e, nb);
        }

        /// <summary>Centre and radius of the arc segment s → e with bulge b (b &gt; 0 = counter-clockwise).</summary>
        public static (double[] center, double radius) BulgeCircle(double[] s, double[] e, double bulge)
        {
            double theta = 4 * Math.Atan(bulge), chord = Dist(s, e);
            double r = chord / (2 * Math.Abs(Math.Sin(theta / 2)));
            double mx = (s[0] + e[0]) / 2, my = (s[1] + e[1]) / 2;
            double ux = (e[0] - s[0]) / chord, uy = (e[1] - s[1]) / chord;
            // the centre is to the left of s → e for a CCW arc shorter than half a circle
            double h = r * Math.Cos(theta / 2) * Math.Sign(theta);
            return (P(mx - uy * h, my + ux * h, Z(s)), r);
        }

        // ================================================================== DIVIDE / MEASURE

        public const int MaxMarks = 10000;

        /// <summary>
        /// Distances from the start of the curve where divide_entity puts a mark. DIVIDE (count N, 2–32767): open curve
        /// N − 1 marks at L·i/N; closed curve N marks from the start. MEASURE (segment s): a mark every s from the start
        /// (fromEnd: from the end), the last one before the far end.
        /// </summary>
        public static List<double> Stations(double length, bool closed, int? count, double? segment, bool fromEnd = false)
        {
            if (length <= Eps) throw ToolException.Invalid("divide_entity: the curve has no length.");
            if ((count == null) == (segment == null)) throw ToolException.Invalid("divide_entity: give either 'count' (DIVIDE into N equal parts) or 'segment_length' (MEASURE).");
            var list = new List<double>();
            if (count != null)
            {
                int n = count.Value;
                if (n < 2 || n > 32767) throw ToolException.Invalid("divide_entity: 'count' must be 2–32767 parts.");
                if (n > MaxMarks) throw new ToolException(ErrorCodes.TooManyEntities, $"divide_entity: {n} parts would create more than {MaxMarks} marks.");
                for (int i = closed ? 0 : 1; i < n; i++) list.Add(length * i / n);
                return list;
            }
            double s = segment.Value;
            if (s <= 0) throw ToolException.Invalid("divide_entity: 'segment_length' must be > 0.");
            double marks = Math.Floor(length / s - 1e-9);
            if (marks < 1) throw ToolException.Invalid($"divide_entity: the curve ({length:0.###}) is not longer than one segment ({s:0.###}); no mark to place.");
            if (marks > MaxMarks) throw new ToolException(ErrorCodes.TooManyEntities, $"divide_entity: segment {s:0.###} on a curve of {length:0.###} would create {marks:0} marks; the limit is {MaxMarks}.");
            for (int k = 1; k <= (int)marks; k++) list.Add(fromEnd ? length - k * s : k * s);
            return list;
        }

        // ================================================================== FILLET / CHAMFER of a polyline

        public sealed class PolyCornerResult
        {
            public List<double[]> Vertices = new List<double[]>();
            public List<double> Bulges = new List<double>();
            /// <summary>Original vertex indices that were filleted / chamfered.</summary>
            public List<int> Done = new List<int>();
            /// <summary>{vertex, reason} of the corners left as they were.</summary>
            public List<Dictionary<string, object>> Skipped = new List<Dictionary<string, object>>();
        }

        /// <summary>
        /// FILLET / CHAMFER of a polyline (LWPOLYLINE): every corner between two straight segments (vertex null), or one
        /// corner (vertex = its 0-based index). Fillet: an arc of 'radius' replaces the corner (bulge vertices); chamfer:
        /// a straight cut d1 back along the incoming segment and d2 along the outgoing one. Corners next to an arc segment,
        /// straight-through vertices and corners whose segments are too short are skipped with a reason (like AutoCAD's
        /// "N were too short"); a named vertex that cannot be done throws.
        /// </summary>
        public static PolyCornerResult PolylineCorners(IList<double[]> v, IList<double> bulges, bool closed, int? vertex,
            bool chamfer, double radius, double d1, double d2, string tool)
        {
            int n = v.Count;
            if (n < 3) throw ToolException.Invalid($"{tool}: the polyline has no corner (it needs at least 3 vertices).");
            double B(int i) => bulges != null && i >= 0 && i < bulges.Count ? bulges[i] : 0;
            int segs = closed ? n : n - 1;
            double SegLen(int i) => Dist(v[i], v[(i + 1) % n]);
            var used = new double[n]; // length taken from each segment (index = its start vertex)
            if (chamfer ? (d1 < Eps && d2 < Eps) : radius < Eps)
                throw ToolException.Invalid($"{tool}: {(chamfer ? "distances 0" : "radius 0")} leaves a polyline's corners unchanged; give a {(chamfer ? "distance" : "radius")} > 0.");
            if (vertex != null && (vertex < 0 || vertex >= n))
                throw ToolException.Invalid($"{tool}: 'vertex' must be 0–{n - 1} (the polyline has {n} vertices).");

            var tin = new double[n];
            var tout = new double[n];
            var turn = new double[n];
            var ok = new bool[n];
            var res = new PolyCornerResult();
            for (int i = 0; i < n; i++)
            {
                if (vertex != null && i != vertex) continue;
                string why = null;
                if (!closed && (i == 0 || i == n - 1)) why = "end of an open polyline (no corner)";
                int pi = (i - 1 + n) % n, ni = (i + 1) % n;
                double[] din = null, dout = null;
                if (why == null)
                {
                    if (Math.Abs(B(pi)) > 1e-12 || Math.Abs(B(i)) > 1e-12) why = "next to an arc segment";
                    else
                    {
                        double lin = SegLen(pi), lout = SegLen(i);
                        if (lin < Eps || lout < Eps) why = "zero-length segment";
                        else
                        {
                            din = new[] { (v[i][0] - v[pi][0]) / lin, (v[i][1] - v[pi][1]) / lin };
                            dout = new[] { (v[ni][0] - v[i][0]) / lout, (v[ni][1] - v[i][1]) / lout };
                            double phi = Math.Atan2(din[0] * dout[1] - din[1] * dout[0], din[0] * dout[0] + din[1] * dout[1]);
                            if (Math.Abs(phi) < 1e-6) why = "straight (no corner)";
                            else if (Math.PI - Math.Abs(phi) < 1e-6) why = "the segments double back";
                            else
                            {
                                double a = chamfer ? d1 : radius * Math.Tan(Math.Abs(phi) / 2);
                                double b = chamfer ? d2 : a;
                                if (a > lin - used[pi] + 1e-9 || b > lout - used[i] + 1e-9) why = chamfer ? "segments too short for these distances" : "segments too short for this radius";
                                else { tin[i] = a; tout[i] = b; turn[i] = phi; used[pi] += a; used[i] += b; ok[i] = true; }
                            }
                        }
                    }
                }
                if (why != null)
                {
                    if (vertex != null) throw ToolException.Invalid($"{tool}: vertex {i} cannot be done: {why}.");
                    if (closed || (i != 0 && i != n - 1)) res.Skipped.Add(new Dictionary<string, object> { ["vertex"] = i, ["reason"] = why });
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (!ok[i]) { res.Vertices.Add(Copy(v[i])); res.Bulges.Add(B(i)); continue; }
                int pi = (i - 1 + n) % n, ni = (i + 1) % n;
                double lin = SegLen(pi), lout = SegLen(i);
                var a = P(v[i][0] - (v[i][0] - v[pi][0]) / lin * tin[i], v[i][1] - (v[i][1] - v[pi][1]) / lin * tin[i], Z(v[i]));
                var b = P(v[i][0] + (v[ni][0] - v[i][0]) / lout * tout[i], v[i][1] + (v[ni][1] - v[i][1]) / lout * tout[i], Z(v[i]));
                res.Vertices.Add(a); res.Bulges.Add(chamfer ? 0 : Math.Tan(turn[i] / 4));
                res.Vertices.Add(b); res.Bulges.Add(B(i));
                res.Done.Add(i);
            }
            if (res.Done.Count == 0 && vertex == null)
                throw ToolException.Invalid($"{tool}: no corner of the polyline could be done (" +
                    string.Join("; ", res.Skipped.Select(s => $"vertex {s["vertex"]}: {s["reason"]}").Take(5)) + ").");
            MergeDuplicates(res, closed);
            return res;
        }

        private static double[] Copy(double[] p) => P(p[0], p[1], Z(p));

        /// <summary>Two corners that used a whole segment leave two equal vertices: keep one (with the later bulge).</summary>
        private static void MergeDuplicates(PolyCornerResult r, bool closed)
        {
            for (int i = r.Vertices.Count - 1; i > 0; i--)
                if (Dist(r.Vertices[i], r.Vertices[i - 1]) < 1e-9)
                {
                    r.Bulges[i - 1] = r.Bulges[i];
                    r.Vertices.RemoveAt(i);
                    r.Bulges.RemoveAt(i);
                }
            int last = r.Vertices.Count - 1;
            if (closed && last > 0 && Dist(r.Vertices[last], r.Vertices[0]) < 1e-9)
            {
                r.Vertices.RemoveAt(last);
                r.Bulges.RemoveAt(last);
            }
        }

        // ================================================================== FILLET / CHAMFER with arcs

        /// <summary>A line (A → B) or an arc (centre C, radius R, CCW from S to E radians) in the XY plane.</summary>
        public sealed class Prim
        {
            public bool IsArc;
            public double[] A, B;
            public double[] C;
            public double R, S, E;

            public static Prim Line(double[] a, double[] b) => new Prim { A = a, B = b };
            public static Prim Arc(double[] c, double r, double s, double e) => new Prim { IsArc = true, C = c, R = r, S = Norm(s), E = Norm(e) };
            public double[] Start => IsArc ? OnCircle(C, R, S) : A;
            public double[] End => IsArc ? OnCircle(C, R, E) : B;
        }

        /// <summary>
        /// An arc's angles as a CCW arc in the WCS (Prim.Arc). An arc whose normal is −Z runs clockwise in the WCS: its OCS X
        /// axis is −X, so the OCS angle a is the WCS angle π − a, and start / end swap. The same map turns them back.
        /// </summary>
        public static (double s, double e) WcsArcAngles(double startAngle, double endAngle, bool normalDown) =>
            normalDown ? (Norm(Math.PI - endAngle), Norm(Math.PI - startAngle)) : (Norm(startAngle), Norm(endAngle));

        public sealed class ArcCornerResult
        {
            public Prim New1, New2;
            /// <summary>The fillet arc (CCW S → E), or null.</summary>
            public Prim Fillet;
            /// <summary>The chamfer line, or null.</summary>
            public double[] ChamferStart, ChamferEnd;
        }

        /// <summary>Intersections of the two primitives extended (infinite line, full circle).</summary>
        public static List<double[]> Intersections(Prim p, Prim q)
        {
            if (!p.IsArc && !q.IsArc)
            {
                var x = Geometry.IntersectLines(p.A, p.B, q.A, q.B);
                return x == null ? new List<double[]>() : new List<double[]> { x };
            }
            if (p.IsArc && !q.IsArc) return LineCircle(q.A, q.B, p.C, p.R);
            if (!p.IsArc) return LineCircle(p.A, p.B, q.C, q.R);
            return CircleCircle(p.C, p.R, q.C, q.R);
        }

        private static List<double[]> LineCircle(double[] a, double[] b, double[] c, double r)
        {
            var list = new List<double[]>();
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l < Eps) return list;
            dx /= l; dy /= l;
            double t0 = (c[0] - a[0]) * dx + (c[1] - a[1]) * dy;
            double fx = a[0] + dx * t0 - c[0], fy = a[1] + dy * t0 - c[1];
            double h2 = r * r - (fx * fx + fy * fy);
            double tol = 1e-9 * Math.Max(1, r * r);
            if (h2 < -tol) return list;
            double h = Math.Sqrt(Math.Max(0, h2));
            list.Add(P(a[0] + dx * (t0 - h), a[1] + dy * (t0 - h), Z(a)));
            if (h > 1e-9) list.Add(P(a[0] + dx * (t0 + h), a[1] + dy * (t0 + h), Z(a)));
            return list;
        }

        private static List<double[]> CircleCircle(double[] c1, double r1, double[] c2, double r2)
        {
            var list = new List<double[]>();
            double d = Dist(c1, c2);
            if (d < Eps || d > r1 + r2 + 1e-9 || d < Math.Abs(r1 - r2) - 1e-9) return list;
            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h = Math.Sqrt(Math.Max(0, r1 * r1 - a * a));
            double ux = (c2[0] - c1[0]) / d, uy = (c2[1] - c1[1]) / d;
            double mx = c1[0] + ux * a, my = c1[1] + uy * a;
            list.Add(P(mx - uy * h, my + ux * h, Z(c1)));
            if (h > 1e-9) list.Add(P(mx + uy * h, my - ux * h, Z(c1)));
            return list;
        }

        /// <summary>The corner the two primitives make: the intersection nearest to their ends, and which end each keeps.</summary>
        private static (double[] x, bool keepStart1, bool keepStart2) CornerOf(Prim p, Prim q, string tool)
        {
            var xs = Intersections(p, q);
            if (xs.Count == 0) throw ToolException.Invalid($"{tool}: the two objects do not meet, even extended; there is no corner.");
            double Near(Prim k, double[] x) => Math.Min(Dist(k.Start, x), Dist(k.End, x));
            var best = xs.OrderBy(x => Near(p, x) + Near(q, x)).First();
            // each keeps its end farther from the corner
            return (best, Dist(p.Start, best) >= Dist(p.End, best), Dist(q.Start, best) >= Dist(q.End, best));
        }

        /// <summary>The primitive cut back (or extended) so that it runs from its kept end to the point t (on it, or on its extension).</summary>
        private static Prim Trimmed(Prim k, bool keepStart, double[] t)
        {
            if (!k.IsArc) return keepStart ? Prim.Line(k.A, t) : Prim.Line(t, k.B);
            double a = Ang(k.C, t);
            return keepStart ? Prim.Arc(k.C, k.R, k.S, a) : Prim.Arc(k.C, k.R, a, k.E);
        }

        /// <summary>
        /// How far (along the primitive, from its kept end) the point t is, in the direction of the corner; negative or NaN
        /// when t is behind the kept end. Used to reject fillet solutions on the wrong side.
        /// </summary>
        private static double Along(Prim k, bool keepStart, double[] t)
        {
            if (!k.IsArc)
            {
                var f = keepStart ? k.A : k.B; var o = keepStart ? k.B : k.A;
                double l = Dist(f, o);
                return ((t[0] - f[0]) * (o[0] - f[0]) + (t[1] - f[1]) * (o[1] - f[1])) / l;
            }
            double fa = keepStart ? k.S : k.E;
            return keepStart ? Norm(Ang(k.C, t) - fa) * k.R : Norm(fa - Ang(k.C, t)) * k.R;
        }

        private static Prim[] Offsets(Prim k, double r)
        {
            if (!k.IsArc)
            {
                double dx = k.B[0] - k.A[0], dy = k.B[1] - k.A[1], l = Math.Sqrt(dx * dx + dy * dy);
                double nx = -dy / l * r, ny = dx / l * r;
                return new[]
                {
                    Prim.Line(P(k.A[0] + nx, k.A[1] + ny), P(k.B[0] + nx, k.B[1] + ny)),
                    Prim.Line(P(k.A[0] - nx, k.A[1] - ny), P(k.B[0] - nx, k.B[1] - ny)),
                };
            }
            var list = new List<Prim> { Prim.Arc(k.C, k.R + r, 0, Math.PI) };
            if (k.R - r > Eps) list.Add(Prim.Arc(k.C, k.R - r, 0, Math.PI));
            return list.ToArray();
        }

        private static double[] Tangent(Prim k, double[] q)
        {
            if (!k.IsArc)
            {
                double dx = k.B[0] - k.A[0], dy = k.B[1] - k.A[1], l2 = dx * dx + dy * dy;
                double t = ((q[0] - k.A[0]) * dx + (q[1] - k.A[1]) * dy) / l2;
                return P(k.A[0] + dx * t, k.A[1] + dy * t, Z(k.A));
            }
            return OnCircle(k.C, k.R, Ang(k.C, q));
        }

        private static double Length(Prim k) => k.IsArc ? (Norm(k.E - k.S) < Eps ? TwoPi : Norm(k.E - k.S)) * k.R : Dist(k.A, k.B);

        /// <summary>
        /// FILLET of a line and an arc or of two arcs (two lines: Geometry.FilletLines). Each keeps its end farther from the
        /// corner (where they meet, extended); radius 0 trims / extends both to that corner. Of the circles of the radius
        /// tangent to both, the one nearest to the corner is used.
        /// </summary>
        public static ArcCornerResult FilletArcs(Prim p, Prim q, double radius, string tool = "fillet_entities")
        {
            if (radius < 0) throw ToolException.Invalid($"{tool}: 'radius' must be >= 0.");
            var (x, k1, k2) = CornerOf(p, q, tool);
            var r = new ArcCornerResult();
            if (radius < Eps)
            {
                r.New1 = Trimmed(p, k1, x);
                r.New2 = Trimmed(q, k2, x);
                CheckLength(r.New1, tool); CheckLength(r.New2, tool);
                return r;
            }
            double best = double.MaxValue;
            double[] bc = null, bt1 = null, bt2 = null;
            double ax1 = Along(p, k1, x), ax2 = Along(q, k2, x);
            foreach (var o1 in Offsets(p, radius))
                foreach (var o2 in Offsets(q, radius))
                    foreach (var c in Intersections(o1, o2))
                    {
                        var t1 = Tangent(p, c);
                        var t2 = Tangent(q, c);
                        // a fillet's tangent points lie between each kept end and the corner (a line short of the corner is extended)
                        double a1 = Along(p, k1, t1), a2 = Along(q, k2, t2);
                        if (!(a1 > 1e-9) || !(a2 > 1e-9) || a1 > ax1 + 1e-9 || a2 > ax2 + 1e-9) continue;
                        double score = Dist(t1, x) + Dist(t2, x);
                        if (score < best) { best = score; bc = c; bt1 = t1; bt2 = t2; }
                    }
            if (bc == null) throw ToolException.Invalid($"{tool}: no fillet of radius {radius} fits between these two objects (radius too large?).");
            r.New1 = Trimmed(p, k1, bt1);
            r.New2 = Trimmed(q, k2, bt2);
            CheckLength(r.New1, tool); CheckLength(r.New2, tool);
            // of the two arcs between the tangent points, the one facing the corner
            double s = Ang(bc, bt1), e = Ang(bc, bt2);
            double mid = s + Norm(e - s) / 2;
            double midOther = e + Norm(s - e) / 2;
            if (Dist(OnCircle(bc, radius, mid), x) <= Dist(OnCircle(bc, radius, midOther), x)) r.Fillet = Prim.Arc(bc, radius, s, e);
            else r.Fillet = Prim.Arc(bc, radius, e, s);
            return r;
        }

        /// <summary>
        /// CHAMFER of a line and an arc or of two arcs: a straight cut from the point d1 back along the first object (along
        /// the arc for an arc) to the point d2 back along the second, measured from the corner where they meet (extended).
        /// </summary>
        public static ArcCornerResult ChamferArcs(Prim p, Prim q, double d1, double d2, string tool = "chamfer_entities")
        {
            if (d1 < 0 || d2 < 0) throw ToolException.Invalid($"{tool}: distances must be >= 0.");
            var (x, k1, k2) = CornerOf(p, q, tool);
            double[] Back(Prim k, bool keepStart, double d)
            {
                // from the corner towards the kept end
                if (!k.IsArc)
                {
                    var f = keepStart ? k.A : k.B;
                    double l = Dist(f, x);
                    if (d > l + 1e-9) throw ToolException.Invalid($"{tool}: a chamfer distance ({d:0.###}) is longer than the line from the corner ({l:0.###}).");
                    return P(x[0] + (f[0] - x[0]) / l * d, x[1] + (f[1] - x[1]) / l * d, Z(x));
                }
                double room = Along(k, keepStart, x);
                if (d > room + 1e-9) throw ToolException.Invalid($"{tool}: a chamfer distance ({d:0.###}) is longer than the arc from the corner ({room:0.###}).");
                double ax = Ang(k.C, x);
                return OnCircle(k.C, k.R, keepStart ? ax - d / k.R : ax + d / k.R);
            }
            var c1 = Back(p, k1, d1);
            var c2 = Back(q, k2, d2);
            var r = new ArcCornerResult { New1 = Trimmed(p, k1, c1), New2 = Trimmed(q, k2, c2) };
            CheckLength(r.New1, tool); CheckLength(r.New2, tool);
            if (d1 > Eps || d2 > Eps) { r.ChamferStart = c1; r.ChamferEnd = c2; }
            return r;
        }

        private static void CheckLength(Prim k, string tool)
        {
            if (Length(k) < 1e-9 || (k.IsArc && Norm(k.E - k.S) < 1e-9))
                throw ToolException.Invalid($"{tool}: an object would be cut back to nothing; use a smaller radius / distance.");
        }

        // ================================================================== JOIN with a gap tolerance

        /// <summary>
        /// join_entities with 'tolerance': the closest pair of ends between the polyline (start / end) and the next object,
        /// when they are at most tol apart (and not already touching), or null. A straight segment bridges the gap.
        /// </summary>
        public static (bool plStart, bool objStart, double gap)? Bridge(double[] plS, double[] plE, double[] oS, double[] oE, double tol)
        {
            var c = new[]
            {
                (false, true, Dist(plE, oS)), (false, false, Dist(plE, oE)),
                (true, true, Dist(plS, oS)), (true, false, Dist(plS, oE)),
            }.OrderBy(t => t.Item3).First();
            if (c.Item3 <= 1e-9 || c.Item3 > tol) return null;
            return c;
        }

        // ================================================================== OFFSET both sides

        /// <summary>offset_entity: both_sides needs no side_point; one side needs it.</summary>
        public static void CheckOffsetSides(bool bothSides, bool hasSidePoint)
        {
            if (!bothSides && !hasSidePoint)
                throw ToolException.Invalid("offset_entity: give 'side_point' (a point on the side for the copy), or both_sides: true for a copy on each side.");
        }

        // ================================================================== MATCHPROP

        /// <summary>Properties match_properties copies on every plan.</summary>
        public static readonly string[] GeneralProps = { "layer", "color", "linetype", "lineweight", "linetype_scale", "transparency", "thickness" };
        /// <summary>Text / dimension properties (text style, height, width factor; dimension style): 2D / Full only.</summary>
        public static readonly string[] TextProps = { "text", "dimension" };

        /// <summary>
        /// The properties match_properties copies. None named: every general one, plus text / dimension when the plan has
        /// 2D (on the 3D plan they are listed in skipped). Unknown names throw; text / dimension named on a plan without 2D
        /// are refused earlier by the plan gate (PlanLogic.BlockedParam).
        /// </summary>
        public static List<string> MatchProps(string plan, IList<string> requested, out List<string> skipped)
        {
            skipped = new List<string>();
            var all = GeneralProps.Concat(TextProps).ToList();
            if (requested == null || requested.Count == 0)
            {
                var list = GeneralProps.ToList();
                if (PlanLogic.Has2D(plan)) list.AddRange(TextProps);
                else skipped.AddRange(TextProps);
                return list;
            }
            var res = new List<string>();
            foreach (var raw in requested)
            {
                var s = (raw ?? "").Trim().ToLowerInvariant();
                if (!all.Contains(s)) throw ToolException.Invalid($"match_properties: unknown property '{raw}'; use {string.Join(", ", all)}.");
                if (TextProps.Contains(s) && !PlanLogic.Has2D(plan)) { if (!skipped.Contains(s)) skipped.Add(s); continue; }
                if (!res.Contains(s)) res.Add(s);
            }
            if (res.Count == 0) throw ToolException.Invalid("match_properties: no property left to copy.");
            return res;
        }
    }
}
