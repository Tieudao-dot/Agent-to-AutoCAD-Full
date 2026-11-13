using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>A closed outline in plan: vertices [x, y] and one bulge per segment (like a closed lightweight polyline).</summary>
    public sealed class Outline2d
    {
        public List<double[]> Points = new List<double[]>();
        public List<double> Bulges = new List<double>();

        /// <summary>Signed area (counter-clockwise positive), arcs included.</summary>
        public double Area
        {
            get
            {
                double a = 0;
                int n = Points.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = Points[i]; var q = Points[(i + 1) % n];
                    a += (p[0] * q[1] - q[0] * p[1]) / 2;
                    double b = i < Bulges.Count ? Bulges[i] : 0;
                    if (Math.Abs(b) > 1e-12)
                    {
                        // the circular segment between the chord and the arc (positive bulge = arc to the right of the chord when going CCW... signed)
                        double c = Math.Sqrt((q[0] - p[0]) * (q[0] - p[0]) + (q[1] - p[1]) * (q[1] - p[1]));
                        double th = 4 * Math.Atan(b);
                        double r = c / (2 * Math.Sin(Math.Abs(th) / 2));
                        a += Math.Sign(th) * r * r * (Math.Abs(th) - Math.Sin(Math.Abs(th))) / 2;
                    }
                }
                return a;
            }
        }
    }

    /// <summary>An arc of a plan (drawing units): centre, radius, start angle and a counter-clockwise sweep (radians, 0 &lt; sweep ≤ 2π).</summary>
    public sealed class PlanArc
    {
        public double Cx, Cy, R, A0, Sweep;
        /// <summary>Wall thickness for centreline arcs (wide polylines / centerline_handles); 0 = a face line.</summary>
        public double Thickness;
        public List<string> Sources = new List<string>();
        public double Length => R * Sweep;
    }

    /// <summary>An opening of a curved wall: [A0, A0 + Sweep] (angles, CCW), the wall kept below Bottom and above Top.</summary>
    public sealed class PlanArcOpening
    {
        public double A0, Sweep;
        public string Kind, From; // door / window; gap / symbol / given
        public double Bottom = double.NaN, Top = double.NaN;
        public List<string> Sources = new List<string>();
    }

    /// <summary>
    /// One curved wall (nâng cấp đợt D): the centreline circle (centre, radius), the thickness, and the parts that are wall
    /// over the full height as angle intervals [start, start + sweep] (CCW, start in [0, 2π)).
    /// </summary>
    public sealed class PlanArcWall
    {
        public double Cx, Cy, R, Thickness;
        public List<double[]> Solid = new List<double[]>(); // [start, sweep]
        public List<PlanArcOpening> Openings = new List<PlanArcOpening>();
        /// <summary>Gaps with no door / window symbol: left open over the full height.</summary>
        public List<double[]> OpenGaps = new List<double[]>();
        public List<string> Sources = new List<string>();
        public string Mode; // pair / centerline / width
        public double Inner => R - Thickness / 2;
        public double Outer => R + Thickness / 2;
        public double SolidSweep => Solid.Sum(s => s[1]);
    }

    /// <summary>A piece of a curved wall: an annular sector from Z0 to Z1 (relative to the floor).</summary>
    public sealed class ArcPiece
    {
        public double Cx, Cy, Ri, Ro, A0, Sweep, Z0, Z1;
        public int Wall;
        public double Volume => 0.5 * Sweep * (Ro * Ro - Ri * Ri) * (Z1 - Z0);
    }

    /// <summary>
    /// Nâng cấp đợt D (13/11/2026), the AutoCAD-free part of the 3D additions: beams along a path, curved walls of
    /// build_3d_from_plan, PRESSPULL modes, the STL file writer, the volume schedule by layer (overlaps of columns
    /// inside walls deducted) and the clash table. Every tool is labelled 3D (Core/Plans.cs); the tables in the drawing
    /// need 2D too (Full), on 3D the numbers only go back to Claude.
    /// </summary>
    public static class Solid3Logic
    {
        public const double DefaultBeamWidth = 220, DefaultBeamHeight = 400; // mm
        public const double MinBeamSize = 50, MaxBeamWidth = 3000, MaxBeamHeight = 5000;
        public const int MaxBeamPaths = 500, MaxBeamSegments = 2000;
        /// <summary>KT2-19: segments of every path of one build_beam call together (each is an extrusion + a union on AutoCAD's main thread).</summary>
        public const int MaxBeamPieces = 5000;
        /// <summary>KT2-06: STL stores 32-bit floats; a model farther than this (mm) from 0 is moved to 0 or its vertices snap to a coarse grid.</summary>
        public const double StlFarCoordinate = 1e6;
        public const string BeamLayer = "A-BEAM-3D";
        public const int MaxScheduleSolids = 2000;
        public const int MaxScheduleOverlapPairs = 2000;
        public const int MaxStlSolids = 2000;
        /// <summary>Joints turning less than this (degrees) need no filler piece.</summary>
        public const double JointTolDeg = 0.5;
        /// <summary>Joints turning more than this (degrees) get a bevel instead of a mitre (no long spike).</summary>
        public const double MaxMitreDeg = 120;
        /// <summary>
        /// Test &amp; sửa đợt D (ND-1): a path that doubles back (turn of about 180°) needs no filler: the square ends of the two
        /// segments lie on each other, and the filler would be a flat triangle (no area) that AutoCAD cannot make a region of.
        /// </summary>
        public const double MaxFillerDeg = 179.5;

        // ================================================================== arcs

        /// <summary>The arc of a polyline segment p → q with a bulge (bulge &gt; 0 = counter-clockwise).</summary>
        public static PlanArc ArcFromBulge(double[] p, double[] q, double bulge)
        {
            double dx = q[0] - p[0], dy = q[1] - p[1];
            double c = Math.Sqrt(dx * dx + dy * dy);
            if (c <= 1e-12 || Math.Abs(bulge) <= 1e-12) return null;
            double th = 4 * Math.Atan(bulge);              // signed sweep
            double r = c / (2 * Math.Sin(Math.Abs(th) / 2));
            // the centre lies left of the chord for a CCW arc of less than half a turn: h = r cos(θ/2) = c / 2 / tan(θ/2)
            double h = c / 2 / Math.Tan(th / 2);
            double mx = (p[0] + q[0]) / 2, my = (p[1] + q[1]) / 2;
            double nx = -dy / c, ny = dx / c;
            double cx = mx + nx * h, cy = my + ny * h;
            double a0 = Math.Atan2(p[1] - cy, p[0] - cx), a1 = Math.Atan2(q[1] - cy, q[0] - cx);
            if (th < 0) { var t = a0; a0 = a1; a1 = t; } // store counter-clockwise
            return new PlanArc { Cx = cx, Cy = cy, R = r, A0 = Norm(a0), Sweep = Math.Abs(th) };
        }

        /// <summary>The arc through start, a middle point and end (an ARC entity of any normal, read in WCS).</summary>
        public static PlanArc ArcFrom3Points(double[] a, double[] m, double[] b)
        {
            double ax = a[0], ay = a[1], bx = m[0], by = m[1], cx = b[0], cy = b[1];
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12) return null;
            double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
            double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
            double r = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
            double t0 = Math.Atan2(ay - uy, ax - ux), tm = Math.Atan2(by - uy, bx - ux), t1 = Math.Atan2(cy - uy, cx - ux);
            double ccw = Norm(t1 - t0), mid = Norm(tm - t0);
            // the middle point lies on the counter-clockwise way from start to end, or the arc runs clockwise
            if (mid <= ccw) return new PlanArc { Cx = ux, Cy = uy, R = r, A0 = Norm(t0), Sweep = ccw <= 1e-12 ? 2 * Math.PI : ccw };
            return new PlanArc { Cx = ux, Cy = uy, R = r, A0 = Norm(t1), Sweep = 2 * Math.PI - ccw };
        }

        public static double Norm(double a)
        {
            a %= 2 * Math.PI;
            if (a < 0) a += 2 * Math.PI;
            if (a >= 2 * Math.PI - 1e-12) a = 0;
            return a;
        }

        /// <summary>Annular sector outline(s) from radius ri to ro over [a0, a0 + sweep]; sweeps over half a turn are split.</summary>
        public static List<Outline2d> SectorOutlines(double cx, double cy, double ri, double ro, double a0, double sweep)
        {
            var list = new List<Outline2d>();
            int n = Math.Max(1, (int)Math.Ceiling(sweep / Math.PI - 1e-9));
            double step = sweep / n;
            for (int k = 0; k < n; k++)
            {
                double s = a0 + k * step, e = s + step;
                double b = Math.Tan(step / 4);
                var o = new Outline2d();
                o.Points.Add(new[] { cx + ri * Math.Cos(s), cy + ri * Math.Sin(s) }); o.Bulges.Add(0);
                o.Points.Add(new[] { cx + ro * Math.Cos(s), cy + ro * Math.Sin(s) }); o.Bulges.Add(b);
                o.Points.Add(new[] { cx + ro * Math.Cos(e), cy + ro * Math.Sin(e) }); o.Bulges.Add(0);
                o.Points.Add(new[] { cx + ri * Math.Cos(e), cy + ri * Math.Sin(e) }); o.Bulges.Add(-b);
                list.Add(o);
            }
            return list;
        }

        // ================================================================== beams

        public sealed class BeamLayout
        {
            /// <summary>One outline per segment (rectangle or annular sector) plus the corner fillers.</summary>
            public List<Outline2d> Pieces = new List<Outline2d>();
            public double Length;
            public int Segments, Joints;
        }

        public static void CheckBeam(double width, double height, string tool = "build_beam")
        {
            if (!(width >= MinBeamSize && width <= MaxBeamWidth)) throw ToolException.Invalid($"{tool}: width must be {MinBeamSize}–{MaxBeamWidth} mm (got {Fmt(width)}).");
            if (!(height >= MinBeamSize && height <= MaxBeamHeight)) throw ToolException.Invalid($"{tool}: height must be {MinBeamSize}–{MaxBeamHeight} mm (got {Fmt(height)}).");
        }

        private static double[] Rot(double[] v, double ang) => new[] { v[0] * Math.Cos(ang) - v[1] * Math.Sin(ang), v[0] * Math.Sin(ang) + v[1] * Math.Cos(ang) };

        /// <summary>
        /// The plan outlines of a beam of width w centred on the path (drawing units): a rectangle per straight segment, an
        /// annular sector per arc segment (bulge), square ends, and at every corner a filler (mitre, or a bevel past
        /// MaxMitreDeg) closing the outside notch. The pieces overlap on the inside of corners; AutoCAD unites them.
        /// </summary>
        public static BeamLayout Beam(IList<double[]> pts, IList<double> bulges, bool closed, double w, string from = "the path", string tool = "build_beam")
        {
            var clean = new List<double[]>();
            var cb = new List<double>();
            for (int i = 0; i < pts.Count; i++)
            {
                double b = bulges != null && i < bulges.Count ? bulges[i] : 0;
                if (clean.Count > 0)
                {
                    var last = clean[clean.Count - 1];
                    if (Math.Abs(pts[i][0] - last[0]) < 1e-9 && Math.Abs(pts[i][1] - last[1]) < 1e-9) { cb[cb.Count - 1] = b; continue; }
                }
                clean.Add(pts[i]); cb.Add(b);
            }
            // KT2-12: an open path whose last point is its first one (PLINE drawn back to the start without Close) is a loop
            if (!closed && clean.Count > 2)
            {
                double scale = clean.Max(q => Math.Max(Math.Abs(q[0]), Math.Abs(q[1])));
                double tol = 1e-6 * Math.Max(1, scale);
                if (Math.Abs(clean[0][0] - clean[clean.Count - 1][0]) <= tol && Math.Abs(clean[0][1] - clean[clean.Count - 1][1]) <= tol)
                {
                    clean[clean.Count - 1] = (double[])clean[0].Clone();
                    closed = true;
                }
            }
            if (closed && clean.Count > 2 && Math.Abs(clean[0][0] - clean[clean.Count - 1][0]) < 1e-9 && Math.Abs(clean[0][1] - clean[clean.Count - 1][1]) < 1e-9)
            {
                clean.RemoveAt(clean.Count - 1); cb.RemoveAt(cb.Count - 1);
            }
            if (clean.Count < 2) throw ToolException.Invalid($"{tool}: {from} needs at least 2 different points.");
            if (closed && clean.Count < 3 && cb.All(x => Math.Abs(x) < 1e-12)) closed = false;
            int n = clean.Count, segs = closed ? n : n - 1;
            if (segs > MaxBeamSegments) throw ToolException.Invalid($"{tool}: {from} has {segs} segments; at most {MaxBeamSegments} per beam.");
            var lay = new BeamLayout { Segments = segs };
            var startT = new double[segs][];
            var endT = new double[segs][];
            double h = w / 2;
            for (int i = 0; i < segs; i++)
            {
                var a = clean[i]; var b = clean[(i + 1) % n];
                double bulge = cb[i];
                double dx = b[0] - a[0], dy = b[1] - a[1], L = Math.Sqrt(dx * dx + dy * dy);
                var chord = new[] { dx / L, dy / L };
                if (Math.Abs(bulge) <= 1e-9)
                {
                    var nl = new[] { -chord[1], chord[0] };
                    var o = new Outline2d();
                    o.Points.Add(new[] { a[0] - nl[0] * h, a[1] - nl[1] * h });
                    o.Points.Add(new[] { b[0] - nl[0] * h, b[1] - nl[1] * h });
                    o.Points.Add(new[] { b[0] + nl[0] * h, b[1] + nl[1] * h });
                    o.Points.Add(new[] { a[0] + nl[0] * h, a[1] + nl[1] * h });
                    o.Bulges.AddRange(new double[] { 0, 0, 0, 0 });
                    lay.Pieces.Add(o);
                    lay.Length += L;
                    startT[i] = chord; endT[i] = chord;
                }
                else
                {
                    var arc = ArcFromBulge(a, b, bulge);
                    if (arc.R - h <= 1e-6)
                        throw ToolException.Invalid($"{tool}: {from}: segment {i + 1} bends with radius {Fmt(arc.R)}, less than half the beam width {Fmt(w)}. Nothing was built. Use a larger radius or a narrower beam.");
                    double th = 4 * Math.Atan(bulge);
                    double sa = Math.Atan2(a[1] - arc.Cy, a[0] - arc.Cx), sb = Math.Atan2(b[1] - arc.Cy, b[0] - arc.Cx);
                    double ri = arc.R - h, ro = arc.R + h;
                    double bb = Math.Tan(th / 4);
                    var o = new Outline2d();
                    o.Points.Add(new[] { arc.Cx + ri * Math.Cos(sa), arc.Cy + ri * Math.Sin(sa) }); o.Bulges.Add(0);
                    o.Points.Add(new[] { arc.Cx + ro * Math.Cos(sa), arc.Cy + ro * Math.Sin(sa) }); o.Bulges.Add(bb);
                    o.Points.Add(new[] { arc.Cx + ro * Math.Cos(sb), arc.Cy + ro * Math.Sin(sb) }); o.Bulges.Add(0);
                    o.Points.Add(new[] { arc.Cx + ri * Math.Cos(sb), arc.Cy + ri * Math.Sin(sb) }); o.Bulges.Add(-bb);
                    lay.Pieces.Add(o);
                    lay.Length += arc.Length;
                    startT[i] = Rot(chord, -th / 2); endT[i] = Rot(chord, th / 2);
                }
            }
            // corners: between the end of segment i and the start of segment i + 1
            int joints = closed ? segs : segs - 1;
            for (int i = 0; i < joints; i++)
            {
                var v = clean[(i + 1) % n];
                var ta = endT[i]; var tb = startT[(i + 1) % segs];
                double cross = ta[0] * tb[1] - ta[1] * tb[0], dot = ta[0] * tb[0] + ta[1] * tb[1];
                double turn = Math.Atan2(cross, dot);
                if (Math.Abs(turn) * 180 / Math.PI < JointTolDeg || Math.Abs(turn) * 180 / Math.PI >= MaxFillerDeg) continue;
                // the outside of the corner is on the right of a left turn
                double side = turn > 0 ? -1 : 1;
                var na = new[] { -ta[1] * side * h, ta[0] * side * h };
                var nb = new[] { -tb[1] * side * h, tb[0] * side * h };
                var pa = new[] { v[0] + na[0], v[1] + na[1] };
                var pb = new[] { v[0] + nb[0], v[1] + nb[1] };
                var o = new Outline2d();
                o.Points.Add((double[])v.Clone());
                o.Points.Add(pa);
                if (Math.Abs(turn) * 180 / Math.PI <= MaxMitreDeg)
                {
                    double mx = na[0] + nb[0], my = na[1] + nb[1], ml = Math.Sqrt(mx * mx + my * my);
                    double k = h / Math.Cos(Math.Abs(turn) / 2) / ml;
                    o.Points.Add(new[] { v[0] + mx * k, v[1] + my * k });
                }
                o.Points.Add(pb);
                o.Bulges.AddRange(Enumerable.Repeat(0.0, o.Points.Count));
                lay.Pieces.Add(o);
                lay.Joints++;
            }
            return lay;
        }

        /// <summary>The top of a beam (drawing units): top_z (default the path z) minus the slab above it when slab_thickness is given.</summary>
        public static double BeamTop(double? topZ, double pathZ, double? slabThickness) => (topZ ?? pathZ) - (slabThickness ?? 0);

        // ================================================================== curved walls (build_3d_from_plan)

        private static double CircDist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>The common parts of two CCW angle intervals [a0, a0 + sa] and [b0, b0 + sb], as [start, sweep].</summary>
        public static List<double[]> Overlap(double a0, double sa, double b0, double sb)
        {
            var res = new List<double[]>();
            double rb = Norm(b0 - a0);
            foreach (var shift in new[] { 0.0, -2 * Math.PI })
            {
                double s = Math.Max(0, rb + shift), e = Math.Min(sa, rb + shift + sb);
                if (e - s > 1e-9) res.Add(new[] { Norm(a0 + s), e - s });
            }
            if (sb >= 2 * Math.PI - 1e-9 && sa >= 2 * Math.PI - 1e-9) return new List<double[]> { new[] { Norm(a0), 2 * Math.PI } };
            return res;
        }

        /// <summary>Is angle t inside [a0, a0 + sweep] (with a tolerance in radians)?</summary>
        public static bool InArc(double t, double a0, double sweep, double tol = 0) =>
            sweep >= 2 * Math.PI - 1e-9 || Norm(t - a0 + tol) <= sweep + 2 * tol;

        public sealed class CurvedResult
        {
            public List<PlanArcWall> Walls = new List<PlanArcWall>();
            public List<PlanSkip> Skipped = new List<PlanSkip>();
            /// <summary>Symbols used by curved walls: not passed to the straight-wall recognition.</summary>
            public HashSet<PlanSymbol> UsedSymbols = new HashSet<PlanSymbol>();
            public int Caps;
        }

        /// <summary>
        /// Curved walls of a floor plan: pairs of concentric face arcs a wall thickness apart (no other concentric arc
        /// between them), and centreline arcs with a thickness (wide polylines with bulges, centerline_handles). Pieces on the
        /// same circle are merged; gaps between them are door / window openings when a symbol covers them (doors: lintel above,
        /// windows: sill and lintel) and stay open otherwise. A symbol over a continuous curved wall is an opening of its angular
        /// extent. Face arcs without a partner are skipped with a reason.
        /// </summary>
        public static CurvedResult CurvedWalls(IList<PlanArc> faceArcs, IList<PlanArc> centerArcs, IList<PlanSymbol> symbols, PlanOptions o)
        {
            var res = new CurvedResult();
            var raw = new List<PlanArcWall>();
            faceArcs = faceArcs ?? new List<PlanArc>();
            var used = new bool[faceArcs.Count];
            for (int i = 0; i < faceArcs.Count; i++)
                for (int j = i + 1; j < faceArcs.Count; j++)
                {
                    var a = faceArcs[i]; var b = faceArcs[j];
                    if (CircDist(a.Cx, a.Cy, b.Cx, b.Cy) > o.JoinTol) continue;
                    double dr = Math.Abs(a.R - b.R);
                    if (dr < o.MinThickness - o.LineTol || dr > o.MaxThickness + o.LineTol) continue;
                    double rin = Math.Min(a.R, b.R), rout = Math.Max(a.R, b.R);
                    foreach (var ov in Overlap(a.A0, a.Sweep, b.A0, b.Sweep))
                    {
                        if (ov[1] * (rin + rout) / 2 < o.JoinTol) continue;
                        // another concentric arc between them over this part: they are not the two faces of one wall
                        bool between = false;
                        for (int k = 0; k < faceArcs.Count && !between; k++)
                        {
                            if (k == i || k == j) continue;
                            var c = faceArcs[k];
                            if (CircDist(a.Cx, a.Cy, c.Cx, c.Cy) > o.JoinTol || c.R <= rin + o.LineTol || c.R >= rout - o.LineTol) continue;
                            between = Overlap(ov[0], ov[1], c.A0, c.Sweep).Any(x => x[1] * c.R >= o.JoinTol);
                        }
                        if (between) continue;
                        used[i] = used[j] = true;
                        var w = new PlanArcWall { Cx = (a.Cx + b.Cx) / 2, Cy = (a.Cy + b.Cy) / 2, R = (rin + rout) / 2, Thickness = dr, Mode = "pair" };
                        w.Solid.Add(ov);
                        w.Sources.AddRange(a.Sources); w.Sources.AddRange(b.Sources);
                        raw.Add(w);
                    }
                }
            for (int i = 0; i < faceArcs.Count; i++)
            {
                if (used[i]) continue;
                foreach (var h in faceArcs[i].Sources.Distinct())
                    res.Skipped.Add(new PlanSkip(h, "ARC", $"curved line with no concentric arc {Fmt(o.MinThickness)}–{Fmt(o.MaxThickness)} away (a single arc, or a thickness outside min_thickness–max_thickness): not a curved wall"));
            }
            foreach (var c in centerArcs ?? new List<PlanArc>())
            {
                if (c.R - c.Thickness / 2 <= 1e-9)
                {
                    foreach (var h in c.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, "ARC", $"curved wall of radius {Fmt(c.R)} thinner than its thickness {Fmt(c.Thickness)}: not built"));
                    continue;
                }
                var w = new PlanArcWall { Cx = c.Cx, Cy = c.Cy, R = c.R, Thickness = c.Thickness, Mode = "centerline" };
                w.Solid.Add(new[] { Norm(c.A0), c.Sweep });
                w.Sources.AddRange(c.Sources);
                raw.Add(w);
            }

            // pieces on the same circle (same centre, radius and thickness) are one wall
            foreach (var w in raw)
            {
                var g = res.Walls.FirstOrDefault(x => CircDist(x.Cx, x.Cy, w.Cx, w.Cy) <= o.JoinTol && Math.Abs(x.R - w.R) <= o.LineTol && Math.Abs(x.Thickness - w.Thickness) <= o.LineTol);
                if (g == null) { res.Walls.Add(w); continue; }
                g.Solid.AddRange(w.Solid);
                foreach (var s in w.Sources) if (!g.Sources.Contains(s)) g.Sources.Add(s);
                if (g.Mode != w.Mode) g.Mode = "mixed";
            }
            foreach (var w in res.Walls) MergeIntervals(w);

            // gaps between the parts of a curved wall, and the symbols over them / over the wall
            var syms = (symbols ?? new List<PlanSymbol>()).ToList();
            foreach (var w in res.Walls)
            {
                var gaps = new List<double[]>();
                if (w.Solid.Count > 0 && !(w.Solid.Count == 1 && w.Solid[0][1] >= 2 * Math.PI - 1e-9))
                {
                    var sorted = w.Solid.OrderBy(s => Norm(s[0] - w.Solid[0][0])).ToList();
                    for (int k = 0; k < sorted.Count; k++)
                    {
                        // between this part and the next (the last one back to the first): a door / window gap only when
                        // it is short (an opening width, under a quarter turn), not the rest of the circle beyond a wall's end
                        var cur = sorted[k]; var next = sorted[(k + 1) % sorted.Count];
                        double s0 = Norm(cur[0] + cur[1]);
                        double gap = Norm(next[0] - s0);
                        if (gap <= Math.PI / 2 && gap * w.R >= o.MinOpening - o.JoinTol && gap * w.R <= o.MaxOpening + o.JoinTol) gaps.Add(new[] { s0, gap });
                    }
                }
                foreach (var gp in gaps)
                {
                    double mid = gp[0] + gp[1] / 2;
                    double mx = w.Cx + w.R * Math.Cos(mid), my = w.Cy + w.R * Math.Sin(mid);
                    var sym = syms.FirstOrDefault(s => !res.UsedSymbols.Contains(s) &&
                        mx >= s.Min[0] - w.Thickness / 2 - o.JoinTol && mx <= s.Max[0] + w.Thickness / 2 + o.JoinTol &&
                        my >= s.Min[1] - w.Thickness / 2 - o.JoinTol && my <= s.Max[1] + w.Thickness / 2 + o.JoinTol);
                    if (sym == null) { w.OpenGaps.Add(gp); continue; }
                    res.UsedSymbols.Add(sym);
                    var op = new PlanArcOpening { A0 = gp[0], Sweep = gp[1], Kind = sym.Kind, From = "gap" };
                    op.Sources.AddRange(sym.Sources);
                    w.Openings.Add(op);
                }
                // a symbol whose centre lies in the wall band over a solid part: an opening of the symbol's angular extent
                foreach (var s in syms)
                {
                    if (res.UsedSymbols.Contains(s)) continue;
                    double cx = (s.Min[0] + s.Max[0]) / 2, cy = (s.Min[1] + s.Max[1]) / 2;
                    double d = CircDist(cx, cy, w.Cx, w.Cy);
                    if (Math.Abs(d - w.R) > w.Thickness / 2 + o.JoinTol) continue;
                    double tc = Math.Atan2(cy - w.Cy, cx - w.Cx);
                    var part = w.Solid.FirstOrDefault(iv => InArc(tc, iv[0], iv[1]));
                    if (part == null) continue;
                    var angles = new[] { new[] { s.Min[0], s.Min[1] }, new[] { s.Max[0], s.Min[1] }, new[] { s.Max[0], s.Max[1] }, new[] { s.Min[0], s.Max[1] } }
                        .Select(pt => Norm(Math.Atan2(pt[1] - w.Cy, pt[0] - w.Cx) - tc)).Select(x => x > Math.PI ? x - 2 * Math.PI : x).ToList();
                    double lo = angles.Min(), hi = angles.Max();
                    double a0 = Norm(tc + lo), sw = hi - lo;
                    var clip = Overlap(part[0], part[1], a0, sw).OrderByDescending(x => x[1]).FirstOrDefault();
                    if (clip == null || clip[1] * w.R < o.MinOpening - o.JoinTol) continue;
                    res.UsedSymbols.Add(s);
                    var op = new PlanArcOpening { A0 = clip[0], Sweep = clip[1], Kind = s.Kind, From = "symbol" };
                    op.Sources.AddRange(s.Sources);
                    w.Openings.Add(op);
                }
            }
            return res;
        }

        private static void MergeIntervals(PlanArcWall w)
        {
            if (w.Solid.Count == 0) return;
            double baseA = w.Solid[0][0];
            var rel = w.Solid.Select(s => new[] { Norm(s[0] - baseA), s[1] }).OrderBy(s => s[0]).ToList();
            var merged = new List<double[]>();
            foreach (var s in rel)
            {
                if (merged.Count > 0 && s[0] <= merged[merged.Count - 1][0] + merged[merged.Count - 1][1] + 1e-6)
                {
                    var m = merged[merged.Count - 1];
                    m[1] = Math.Max(m[1], s[0] + s[1] - m[0]);
                }
                else merged.Add(new[] { s[0], s[1] });
            }
            // the last part running past a full turn into the first one
            if (merged.Count > 1)
            {
                var last = merged[merged.Count - 1];
                if (last[0] + last[1] >= 2 * Math.PI + merged[0][0] - 1e-6)
                {
                    last[1] = Math.Max(last[1], merged[0][0] + merged[0][1] + 2 * Math.PI - last[0]);
                    merged.RemoveAt(0);
                }
            }
            if (merged.Count == 1 && merged[0][1] >= 2 * Math.PI - 1e-6) { w.Solid = new List<double[]> { new[] { 0.0, 2 * Math.PI } }; return; }
            w.Solid = merged.Select(m => new[] { Norm(m[0] + baseA), Math.Min(m[1], 2 * Math.PI) }).ToList();
        }

        /// <summary>Short face lines joining the two faces of a curved wall (wall ends, door jambs): used, not walls.</summary>
        public static int DropCurvedCaps(List<PlanSeg> faces, IList<PlanArcWall> walls, PlanOptions o)
        {
            var ends = new List<double[]>();
            foreach (var w in walls)
                foreach (var s in w.Solid)
                {
                    if (s[1] >= 2 * Math.PI - 1e-9) continue;
                    foreach (var t in new[] { s[0], s[0] + s[1] })
                        foreach (var r in new[] { w.Inner, w.Outer })
                            ends.Add(new[] { w.Cx + r * Math.Cos(t), w.Cy + r * Math.Sin(t) });
                }
            if (ends.Count == 0) return 0;
            bool Near(double x, double y) => ends.Any(e => CircDist(e[0], e[1], x, y) <= 2 * o.JoinTol);
            return faces.RemoveAll(s => s.Length <= o.MaxThickness + o.LineTol && Near(s.X1, s.Y1) && Near(s.X2, s.Y2));
        }

        /// <summary>
        /// An opening the caller gives at a point of a curved wall (openings parameter): true when the point lies on one (then
        /// the opening is added, or err says why not), false when no curved wall is there (the straight walls are tried).
        /// </summary>
        public static bool AddGivenArcOpening(IList<PlanArcWall> walls, string kind, double[] at, double width, double? bottom, double? top, PlanOptions o, out string err)
        {
            err = null;
            foreach (var w in walls)
            {
                double d = CircDist(at[0], at[1], w.Cx, w.Cy);
                if (Math.Abs(d - w.R) > w.Thickness / 2 + o.JoinTol) continue;
                double t = Math.Atan2(at[1] - w.Cy, at[0] - w.Cx);
                bool onWall = w.Solid.Any(iv => InArc(t, iv[0], iv[1], o.JoinTol / w.R));
                var gap = w.OpenGaps.FirstOrDefault(g => InArc(t, g[0], g[1], o.JoinTol / w.R));
                var known = w.Openings.FirstOrDefault(x => InArc(t, x.A0, x.Sweep, o.JoinTol / w.R));
                if (!onWall && gap == null && known == null) continue;
                double a0, sw;
                if (width > 0) { sw = width / w.R; a0 = Norm(t - sw / 2); }
                else if (gap != null) { a0 = gap[0]; sw = gap[1]; }
                else if (known != null) { a0 = known.A0; sw = known.Sweep; }
                else { err = $"the point ({Fmt(at[0])}, {Fmt(at[1])}) is on a continuous curved wall: give the opening width"; return true; }
                if (sw >= 2 * Math.PI || sw * w.R > o.MaxOpening + o.JoinTol) { err = $"opening width {Fmt(sw * w.R)} on the curved wall is too large"; return true; }
                // a given opening replaces a recognised one / a gap there
                w.Openings.RemoveAll(x => Overlap(x.A0, x.Sweep, a0, sw).Count > 0);
                w.OpenGaps.RemoveAll(g => Overlap(g[0], g[1], a0, sw).Count > 0);
                w.Openings.Add(new PlanArcOpening { A0 = a0, Sweep = sw, Kind = kind, From = "given", Bottom = bottom ?? double.NaN, Top = top ?? double.NaN });
                return true;
            }
            return false;
        }

        /// <summary>
        /// The pieces of every curved wall: full height on the solid parts, the wall below / above each opening (like the
        /// straight walls, FloorPlanLogic.WallAround). Heights in drawing units, z relative to the floor.
        /// </summary>
        public static List<ArcPiece> ArcPieces(IList<PlanArcWall> walls, FloorPlanLogic.Heights h)
        {
            var list = new List<ArcPiece>();
            for (int wi = 0; wi < walls.Count; wi++)
            {
                var w = walls[wi];
                foreach (var op in w.Openings)
                {
                    if (op.From != "given" || double.IsNaN(op.Bottom)) op.Bottom = op.Kind == "door" ? 0 : h.Sill;
                    if (op.From != "given" || double.IsNaN(op.Top)) op.Top = op.Kind == "door" ? h.Door : h.Sill + h.Window;
                }
                // the full-height parts: the solid parts minus every opening
                foreach (var iv in w.Solid)
                {
                    var parts = new List<double[]> { new[] { 0.0, iv[1] } }; // relative to iv[0]
                    foreach (var op in w.Openings)
                    {
                        double rs = Norm(op.A0 - iv[0]);
                        // the opening may run past a full turn: cut both its windings
                        foreach (var shift in new[] { 0.0, -2 * Math.PI })
                        {
                            double os = rs + shift, oe = os + op.Sweep;
                            var next = new List<double[]>();
                            foreach (var pt in parts)
                            {
                                if (oe <= pt[0] || os >= pt[1]) { next.Add(pt); continue; }
                                if (os > pt[0]) next.Add(new[] { pt[0], os });
                                if (oe < pt[1]) next.Add(new[] { oe, pt[1] });
                            }
                            parts = next;
                        }
                    }
                    foreach (var pt in parts)
                        if (pt[1] - pt[0] > 1e-9) Add(list, w, wi, iv[0] + pt[0], pt[1] - pt[0], 0, h.Wall);
                }
                foreach (var op in w.Openings)
                {
                    var fake = new PlanOpening { Kind = op.Kind, From = op.From, Bottom = op.Bottom, Top = op.Top };
                    foreach (var z in FloorPlanLogic.WallAround(fake, h)) Add(list, w, wi, op.A0, op.Sweep, z[0], z[1]);
                }
            }
            return list;
        }

        private static void Add(List<ArcPiece> list, PlanArcWall w, int wi, double a0, double sweep, double z0, double z1)
        {
            if (sweep <= 1e-9 || z1 - z0 <= 1e-9) return;
            list.Add(new ArcPiece { Cx = w.Cx, Cy = w.Cy, Ri = w.Inner, Ro = w.Outer, A0 = Norm(a0), Sweep = sweep, Z0 = z0, Z1 = z1, Wall = wi });
        }

        /// <summary>The xy bounding box of an annular sector (for the footprint and the rerun check).</summary>
        public static (double[] min, double[] max) SectorBox(double cx, double cy, double ri, double ro, double a0, double sweep)
        {
            var pts = new List<double[]>();
            foreach (var r in new[] { ri, ro })
            {
                pts.Add(new[] { cx + r * Math.Cos(a0), cy + r * Math.Sin(a0) });
                pts.Add(new[] { cx + r * Math.Cos(a0 + sweep), cy + r * Math.Sin(a0 + sweep) });
                for (int k = 0; k < 4; k++)
                {
                    double t = k * Math.PI / 2;
                    if (InArc(t, a0, sweep)) pts.Add(new[] { cx + r * Math.Cos(t), cy + r * Math.Sin(t) });
                }
            }
            return (new[] { pts.Min(p => p[0]), pts.Min(p => p[1]) }, new[] { pts.Max(p => p[0]), pts.Max(p => p[1]) });
        }

        /// <summary>A piece as an axis-aligned PlanBox (the rerun check compares boxes).</summary>
        public static PlanBox PieceBox(ArcPiece p)
        {
            var (mn, mx) = SectorBox(p.Cx, p.Cy, p.Ri, p.Ro, p.A0, p.Sweep);
            return new PlanBox
            {
                Center = new[] { (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (p.Z0 + p.Z1) / 2 },
                Length = mx[0] - mn[0], Width = mx[1] - mn[1], Height = p.Z1 - p.Z0, RotationDeg = 0, Wall = -1 - p.Wall,
            };
        }

        // ================================================================== PRESSPULL

        public static readonly string[] PresspullModes = { "auto", "add", "subtract", "new" };

        public static string NormalizePresspullMode(string mode, string tool = "presspull")
        {
            var m = (mode ?? "auto").Trim().ToLowerInvariant();
            if (m == "union" || m == "them" || m == "thêm") m = "add";
            if (m == "cut" || m == "bot" || m == "bớt") m = "subtract";
            if (!PresspullModes.Contains(m)) throw ToolException.Invalid($"{tool}: mode must be \"auto\", \"add\", \"subtract\" or \"new\" (got \"{mode}\").");
            return m;
        }

        /// <summary>
        /// What a profile PRESSPULL does: no target = a new solid ("add" / "subtract" need one); with a target, "auto" pushes
        /// into the solid (the pulled piece overlaps it) = subtract, pulls out of it = add. overlap null = AutoCAD could not
        /// measure: refuse auto (the caller says add or subtract).
        /// </summary>
        public static string ResolvePresspull(string mode, bool hasTarget, double? overlap, double pieceVolume, string tool = "presspull")
        {
            if (!hasTarget)
            {
                if (mode == "add" || mode == "subtract") throw ToolException.Invalid($"{tool}: mode \"{mode}\" needs 'target' (the solid to add to / cut from). Without a target the profile becomes a new solid.");
                return "new";
            }
            if (mode == "new") return "new";
            if (mode != "auto") return mode;
            if (overlap == null) throw ToolException.Invalid($"{tool}: AutoCAD could not tell whether the pulled piece goes into the target or out of it. Nothing was changed. Call again with mode \"add\" or \"subtract\".");
            return overlap.Value > Math.Max(1e-9, pieceVolume * 1e-6) ? "subtract" : "add";
        }

        // ================================================================== STL

        /// <summary>The triangles of an STL file (binary or ASCII): 12 floats each, normal then 3 vertices.</summary>
        public static List<float[]> ReadStl(byte[] data)
        {
            var tris = new List<float[]>();
            if (data == null || data.Length == 0) return tris;
            if (data.Length >= 84)
            {
                uint n = BitConverter.ToUInt32(data, 80);
                if (84L + 50L * n == data.Length)
                {
                    for (int i = 0; i < n; i++)
                    {
                        var t = new float[12];
                        for (int k = 0; k < 12; k++) t[k] = BitConverter.ToSingle(data, 84 + i * 50 + k * 4);
                        tris.Add(t);
                    }
                    return tris;
                }
            }
            var text = Encoding.ASCII.GetString(data);
            var tok = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            float[] cur = null; int v = 0;
            for (int i = 0; i < tok.Length; i++)
            {
                var w = tok[i].ToLowerInvariant();
                if (w == "facet" && i + 4 < tok.Length && tok[i + 1].ToLowerInvariant() == "normal")
                {
                    cur = new float[12]; v = 0;
                    for (int k = 0; k < 3; k++) cur[k] = ParseF(tok[i + 2 + k]);
                    i += 4;
                }
                else if (w == "vertex" && cur != null && i + 3 < tok.Length && v < 3)
                {
                    for (int k = 0; k < 3; k++) cur[3 + v * 3 + k] = ParseF(tok[i + 1 + k]);
                    v++; i += 3;
                }
                else if (w == "endfacet" && cur != null) { if (v == 3) tris.Add(cur); cur = null; }
            }
            return tris;
        }

        private static float ParseF(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static byte[] WriteBinaryStl(IList<float[]> tris, string header)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var h = new byte[80];
                var hb = Encoding.ASCII.GetBytes(Ascii(header ?? ""));
                Array.Copy(hb, h, Math.Min(80, hb.Length));
                bw.Write(h);
                bw.Write((uint)tris.Count);
                foreach (var t in tris)
                {
                    for (int k = 0; k < 12; k++) bw.Write(t[k]);
                    bw.Write((ushort)0);
                }
                bw.Flush();
                return ms.ToArray();
            }
        }

        public static string WriteAsciiStl(IList<float[]> tris, string name)
        {
            var n = Ascii(string.IsNullOrWhiteSpace(name) ? "model" : name).Replace(' ', '_');
            var sb = new StringBuilder();
            sb.Append("solid ").Append(n).Append('\n');
            string F(float x) => x.ToString("G9", CultureInfo.InvariantCulture);
            foreach (var t in tris)
            {
                sb.Append("  facet normal ").Append(F(t[0])).Append(' ').Append(F(t[1])).Append(' ').Append(F(t[2])).Append('\n');
                sb.Append("    outer loop\n");
                for (int v = 0; v < 3; v++) sb.Append("      vertex ").Append(F(t[3 + v * 3])).Append(' ').Append(F(t[4 + v * 3])).Append(' ').Append(F(t[5 + v * 3])).Append('\n');
                sb.Append("    endloop\n  endfacet\n");
            }
            sb.Append("endsolid ").Append(n).Append('\n');
            return sb.ToString();
        }

        private static string Ascii(string s)
        {
            var f = FloorPlanLogic.Fold(s ?? "");
            var sb = new StringBuilder();
            foreach (var ch in f) sb.Append(ch >= 32 && ch < 127 ? ch : '_');
            return sb.ToString();
        }

        /// <summary>Default STL file name next to the drawing: &lt;drawing&gt;-3d.stl (safe characters only).</summary>
        public static string DefaultStlName(string drawingName) =>
            Util2DLogic.SafeFileName(string.IsNullOrEmpty(drawingName) ? "Drawing" : drawingName, "Drawing") + "-3d.stl";

        /// <summary>The shift that moves a model into the positive octant (STL readers / slicers expect it), or zero when it is there.</summary>
        public static double[] PositiveShift(double[] min) =>
            new[] { min[0] < 0 ? -min[0] : 0, min[1] < 0 ? -min[1] : 0, min[2] < 0 ? -min[2] : 0 };

        /// <summary>
        /// The shift of an STL export (mm): a model with a negative coordinate, or one far from 0 (survey / VN-2000
        /// coordinates, KT2-06: 32-bit floats would snap its vertices to a 64–256 mm grid), is moved so its bounding box
        /// starts at 0; a small model in the positive octant keeps its coordinates.
        /// </summary>
        public static double[] StlShift(double[] min, double[] max)
        {
            bool far = min.Concat(max).Any(v => Math.Abs(v) > StlFarCoordinate);
            return far ? new[] { -min[0], -min[1], -min[2] } : PositiveShift(min);
        }

        /// <summary>KT2-19: refuse a build_beam call with too many segments in all before anything is built.</summary>
        public static void CheckBeamTotal(int segments, string tool = "build_beam")
        {
            if (segments > MaxBeamPieces)
                throw ToolException.Invalid($"{tool}: {segments} beam segments in one call; at most {MaxBeamPieces}. Nothing was built. Build the beams in several calls (fewer paths each).");
        }

        // ================================================================== volume schedule

        public sealed class VolSolid
        {
            public string Handle, Layer;
            public double Volume, Area; // drawing units³ / ²
            public int Order;
        }

        public sealed class VolOverlap
        {
            public string A, B;
            public double? Volume;
        }

        public sealed class VolRow
        {
            public string Layer;
            public int Count;
            public double Gross, Deducted, Area;
            public double Net => Gross - Deducted;
        }

        public sealed class VolDeduction
        {
            public string From, Kept;
            public double Volume;
        }

        public sealed class VolResult
        {
            public List<VolRow> Rows = new List<VolRow>();
            public List<VolDeduction> Deductions = new List<VolDeduction>();
            public List<string> Warnings = new List<string>();
            public double OverlapTotal;
        }

        /// <summary>
        /// Rank of a layer for the overlap rule (smaller keeps its full volume): columns, beams, walls, slabs, the rest. The usual
        /// way of measuring concrete: a column is counted to its full height, the beam / wall / slab around it loses the common part.
        /// </summary>
        public static int LayerRank(string layer)
        {
            var f = FloorPlanLogic.Fold(layer ?? "");
            var words = f.Split(new[] { '-', '_', ' ', '.', '$', '|' }, StringSplitOptions.RemoveEmptyEntries);
            // Test & sửa đợt D (ND-5): a short key (COL, COT, DAM, SAN) is the whole word or the word plus digits / an S (COT1,
            // COLS); only the long keys match as a prefix (COLUMNS, BEAMS, TUONGXAY). Before, "SANH" (lobby), "COLOR",
            // "DAMPER" were taken for slab / column / beam.
            bool Has(params string[] keys) => words.Any(w => keys.Any(k =>
                w == k || (k.Length >= 4 ? w.StartsWith(k, StringComparison.Ordinal)
                                         : w.StartsWith(k, StringComparison.Ordinal) && (w.Substring(k.Length) == "S" || w.Substring(k.Length).All(char.IsDigit)))));
            if (Has("COL", "COLUMN", "COT")) return 0;
            if (Has("BEAM", "DAM")) return 1;
            if (Has("WALL", "TUONG")) return 2;
            if (Has("SLAB", "SAN", "FLOR", "FLOOR")) return 3;
            return 4;
        }

        /// <summary>
        /// The volume schedule by layer. mode "deduct": every overlap is taken off the solid that ranks lower (priority layers
        /// first in the order given, then LayerRank, then the order of the solids), so a column inside a wall is counted once,
        /// in the column. "none": the overlaps are only reported (warning). Unmeasured overlaps are warned about.
        /// </summary>
        public static VolResult Schedule(IList<VolSolid> solids, IList<VolOverlap> overlaps, IList<string> priority, string mode)
        {
            var res = new VolResult();
            var byHandle = solids.ToDictionary(s => s.Handle, StringComparer.OrdinalIgnoreCase);
            int Pri(VolSolid s)
            {
                if (priority != null)
                    for (int i = 0; i < priority.Count; i++)
                        if (FloorPlanLogic.LayerMatches(s.Layer, new[] { priority[i] })) return i;
                return (priority?.Count ?? 0) + LayerRank(s.Layer);
            }
            var deducted = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            int unmeasured = 0;
            foreach (var ov in overlaps ?? new List<VolOverlap>())
            {
                if (!byHandle.TryGetValue(ov.A, out var a) || !byHandle.TryGetValue(ov.B, out var b)) continue;
                if (ov.Volume == null) { unmeasured++; continue; }
                if (ov.Volume.Value <= 0) continue;
                res.OverlapTotal += ov.Volume.Value;
                // the lower rank keeps its volume; on a tie the later solid loses it
                var (keep, lose) = Pri(a) < Pri(b) || (Pri(a) == Pri(b) && a.Order <= b.Order) ? (a, b) : (b, a);
                if (mode == "deduct")
                {
                    deducted[lose.Handle] = (deducted.TryGetValue(lose.Handle, out var d) ? d : 0) + ov.Volume.Value;
                    res.Deductions.Add(new VolDeduction { From = lose.Handle, Kept = keep.Handle, Volume = ov.Volume.Value });
                }
            }
            foreach (var g in solids.GroupBy(s => s.Layer ?? "", StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Min(s => Pri(s))).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                res.Rows.Add(new VolRow
                {
                    Layer = g.First().Layer ?? "",
                    Count = g.Count(),
                    Gross = g.Sum(s => s.Volume),
                    // a solid never loses more than its own volume (overlaps with three solids at one spot)
                    Deducted = g.Sum(s => Math.Min(s.Volume, deducted.TryGetValue(s.Handle, out var d) ? d : 0)),
                    Area = g.Sum(s => s.Area),
                });
            }
            if (res.OverlapTotal > 0 && mode != "deduct")
                res.Warnings.Add("Some solids overlap (e.g. columns drawn inside walls): the volumes are counted twice where they overlap. Use overlap \"deduct\" to take the common part off once.");
            if (unmeasured > 0)
                res.Warnings.Add($"{unmeasured} overlapping pair(s) could not be measured by AutoCAD (often faces that coincide): their common part is NOT deducted; check them with check_interference.");
            return res;
        }

        private static string Num(double v, int decimals) =>
            Math.Round(v, decimals).ToString("F" + decimals, CultureInfo.InvariantCulture).Replace('.', ',');

        public static double M3(double v, double mmPerUnit) => v * mmPerUnit * mmPerUnit * mmPerUnit / 1e9;
        public static double M2(double a, double mmPerUnit) => a * mmPerUnit * mmPerUnit / 1e6;

        /// <summary>Table data (header row, a row per layer, total) with comma decimals, like the 2D schedules.</summary>
        public static List<List<object>> VolumeTable(VolResult r, int decimals, double mmPerUnit, bool withArea)
        {
            var data = new List<List<object>>();
            var head = new List<object> { "STT", "Layer", "Số khối", "Thể tích (m³)", "Trừ phần giao (m³)", "Thể tích thực (m³)" };
            if (withArea) head.Add("Diện tích mặt (m²)");
            data.Add(head);
            int i = 0;
            foreach (var row in r.Rows)
            {
                var line = new List<object> { (++i).ToString(CultureInfo.InvariantCulture), row.Layer, row.Count.ToString(CultureInfo.InvariantCulture),
                    Num(M3(row.Gross, mmPerUnit), decimals), Num(M3(row.Deducted, mmPerUnit), decimals), Num(M3(row.Net, mmPerUnit), decimals) };
                if (withArea) line.Add(Num(M2(row.Area, mmPerUnit), decimals));
                data.Add(line);
            }
            var total = new List<object> { "", "Tổng", r.Rows.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture),
                Num(M3(r.Rows.Sum(x => x.Gross), mmPerUnit), decimals), Num(M3(r.Rows.Sum(x => x.Deducted), mmPerUnit), decimals), Num(M3(r.Rows.Sum(x => x.Net), mmPerUnit), decimals) };
            if (withArea) total.Add(Num(M2(r.Rows.Sum(x => x.Area), mmPerUnit), decimals));
            data.Add(total);
            return data;
        }

        public sealed class ClashRow
        {
            public string A, LayerA, B, LayerB;
            public double? Volume; // drawing units³ (null = not measured); ClashTable converts with mmPerUnit
        }

        /// <summary>The clash table (check_interference with table_position): a row per pair and the total.</summary>
        public static List<List<object>> ClashTable(IList<ClashRow> rows, int decimals, double mmPerUnit = 1)
        {
            var data = new List<List<object>> { new List<object> { "STT", "Khối 1", "Layer 1", "Khối 2", "Layer 2", "Thể tích giao (m³)" } };
            int i = 0;
            foreach (var r in rows)
                data.Add(new List<object> { (++i).ToString(CultureInfo.InvariantCulture), r.A, r.LayerA ?? "", r.B, r.LayerB ?? "",
                    r.Volume == null ? "chưa đo được" : Num(M3(r.Volume.Value, mmPerUnit), decimals) });
            data.Add(new List<object> { "", "Tổng", "", rows.Count.ToString(CultureInfo.InvariantCulture) + " cặp", "", Num(M3(rows.Where(r => r.Volume != null).Sum(r => r.Volume.Value), mmPerUnit), decimals) });
            return data;
        }

        private static string Fmt(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
    }
}
