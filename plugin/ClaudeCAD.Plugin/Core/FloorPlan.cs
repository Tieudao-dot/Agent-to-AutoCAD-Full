using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>A straight 2D piece of a wall line (drawing units) and the handles it came from.</summary>
    public sealed class PlanSeg
    {
        public double X1, Y1, X2, Y2;
        public List<string> Sources = new List<string>();
        /// <summary>Wall thickness for centreline pieces (wide polylines / centerline_handles); 0 = a face line.</summary>
        public double Thickness;

        public PlanSeg() { }
        public PlanSeg(double x1, double y1, double x2, double y2, params string[] sources)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
            Sources.AddRange(sources);
        }

        public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    /// <summary>A door / window symbol (block, or loose lines and arcs) by its bounding box.</summary>
    public sealed class PlanSymbol
    {
        public string Kind; // "door" / "window"
        public double[] Min, Max;
        public List<string> Sources = new List<string>();
    }

    /// <summary>One straight wall: centreline from Origin along Dir, thickness, and the solid / open parts along it.</summary>
    public sealed class PlanWall
    {
        public double[] Origin; // a point of the centreline (s = 0)
        public double[] Dir;    // unit direction
        public double Thickness;
        /// <summary>Parts that are wall over the full height, [s0, s1] along Dir, sorted, disjoint.</summary>
        public List<double[]> Solid = new List<double[]>();
        public List<string> Sources = new List<string>();
        public string Mode; // "pair" (two face lines), "centerline", "width" (wide polyline)

        public double Start => Solid.Count > 0 ? Solid[0][0] : 0;
        public double End => Solid.Count > 0 ? Solid[Solid.Count - 1][1] : 0;
        public double[] At(double s) => new[] { Origin[0] + Dir[0] * s, Origin[1] + Dir[1] * s };
        public double AngleDeg => Math.Atan2(Dir[1], Dir[0]) * 180 / Math.PI;
    }

    /// <summary>A door or window in a wall: [S0, S1] along the wall, the wall kept below Bottom and above Top.</summary>
    public sealed class PlanOpening
    {
        public int Wall;
        public double S0, S1;
        public string Kind;  // "door" / "window"
        public string From;  // "gap" (both wall lines stop), "symbol" (symbol over a continuous wall), "given" (openings parameter)
        public double Bottom, Top; // relative to the floor
        public List<string> Sources = new List<string>();
        public double Width => S1 - S0;
    }

    public sealed class PlanSkip
    {
        public string Handle, Type, Reason;
        public PlanSkip(string handle, string type, string reason) { Handle = handle; Type = type; Reason = reason; }
    }

    /// <summary>A box of the result: centre (z relative to the floor), length along the wall, thickness, height, angle.</summary>
    public sealed class PlanBox
    {
        public double[] Center;
        public double Length, Width, Height, RotationDeg;
        public int Wall;
        public double Volume => Length * Width * Height;
    }

    /// <summary>Recognition settings, all in drawing units.</summary>
    public sealed class PlanOptions
    {
        public double MinThickness = 50, MaxThickness = 600;
        /// <summary>Two points closer than this are the same point (gaps / overlaps of sloppy drawings).</summary>
        public double JoinTol = 5;
        /// <summary>Lines closer than this to each other's line are collinear.</summary>
        public double LineTol = 1;
        public double MinOpening = 300, MaxOpening = 6000;
        public double AngleTolDeg = 0.5;

        public static PlanOptions ForUnits(double mmPerUnit, double minThicknessMm, double maxThicknessMm)
        {
            double k = 1 / mmPerUnit;
            return new PlanOptions
            {
                MinThickness = minThicknessMm * k, MaxThickness = maxThicknessMm * k, JoinTol = 5 * k, LineTol = 1 * k,
                MinOpening = 300 * k, MaxOpening = 6000 * k,
            };
        }
    }

    public sealed class PlanResult
    {
        public List<PlanWall> Walls = new List<PlanWall>();
        public List<PlanOpening> Openings = new List<PlanOpening>();
        public List<PlanSkip> Skipped = new List<PlanSkip>();
        /// <summary>Gaps between two parts of one wall with no door / window symbol: left open, full height.</summary>
        public List<(int wall, double s0, double s1)> OpenGaps = new List<(int, double, double)>();
        /// <summary>Short end lines of the wall lines (door jambs, wall ends): used, not walls themselves.</summary>
        public int Caps;
        /// <summary>Gaps filled because another wall crosses them (a + junction drawn with broken lines).</summary>
        public int JunctionsFilled;
    }

    /// <summary>
    /// 3D phase 3, AutoCAD-free part of build_3d_from_plan: recognises walls from the 2D lines of a floor plan
    /// (pairs of parallel face lines t apart like the ve-kien-truc skill draws them, centrelines with a thickness,
    /// wide polylines), the door / window openings (gaps between two parts of a wall, symbols over a wall, or given
    /// points) and splits every wall into boxes. Nothing is guessed: what is not understood goes to Skipped with a reason.
    /// </summary>
    public static class FloorPlanLogic
    {
        // limits shared with the assistant (agent/src/tools/plan3d.ts, PLAN3D_LIMITS)
        public const int MaxSourceEntities = 5000;
        public const int MaxSegments = 3000;
        public const int MaxSymbols = 1000;
        public const int MaxOpeningsGiven = 500;
        public const int MaxBoxes = 5000;
        public const int MaxReportItems = 100;
        public const double MinWallHeight = 100, MaxWallHeight = 100000;   // mm
        public const double MaxThicknessLimit = 3000;                       // mm
        public const double MaxSlabThickness = 5000;                        // mm

        public const double DefaultWallHeight = 3300, DefaultDoorHeight = 2200, DefaultSill = 900, DefaultWindowHeight = 1500, DefaultSlabThickness = 150;
        public const double DefaultMinThickness = 50, DefaultMaxThickness = 600;

        public static readonly string[] DefaultWallLayers = { "A-WALL" };
        public static readonly string[] DefaultDoorLayers = { "A-DOOR" };
        public static readonly string[] DefaultWindowLayers = { "A-WIND" };
        public static readonly string[] DefaultSlabLayers = { "A-SLAB", "A-FLOR" };

        /// <summary>Layers of the result (the "3D kiến trúc" skill uses the same names).</summary>
        public const string WallLayer3d = "A-WALL-3D", SlabLayer3d = "A-SLAB-3D";

        // ------------------------------------------------------------------ units, layers

        /// <summary>mm per drawing unit from INSUNITS (UnitsValue as int): 0 undefined → mm.</summary>
        public static double MmPerUnit(int insunits, out string name)
        {
            switch (insunits)
            {
                case 1: name = "inches"; return 25.4;
                case 2: name = "feet"; return 304.8;
                case 4: name = "millimeters"; return 1;
                case 5: name = "centimeters"; return 10;
                case 6: name = "meters"; return 1000;
                case 14: name = "decimeters"; return 100;
                case 0: name = "unitless (taken as millimeters)"; return 1;
                default: name = "unit " + insunits.ToString(CultureInfo.InvariantCulture) + " (taken as millimeters)"; return 1;
            }
        }

        /// <summary>Layer name matches one of the patterns (exact, case-insensitive; '*' and '?' wildcards).</summary>
        public static bool LayerMatches(string layer, IEnumerable<string> patterns)
        {
            if (layer == null) return false;
            foreach (var p in patterns)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p.IndexOf('*') < 0 && p.IndexOf('?') < 0) { if (string.Equals(layer, p, StringComparison.OrdinalIgnoreCase)) return true; continue; }
                if (Glob(layer.ToUpperInvariant(), 0, p.ToUpperInvariant(), 0)) return true;
            }
            return false;
        }

        private static bool Glob(string s, int i, string p, int j)
        {
            while (j < p.Length)
            {
                if (p[j] == '*')
                {
                    for (int k = i; k <= s.Length; k++) if (Glob(s, k, p, j + 1)) return true;
                    return false;
                }
                if (i >= s.Length || (p[j] != '?' && p[j] != s[i])) return false;
                i++; j++;
            }
            return i == s.Length;
        }

        /// <summary>Door / window blocks of the ve-kien-truc skill by name (CUA-DI-900, CUA-SO-1200), on any layer.</summary>
        public static string KindOfBlockName(string name)
        {
            var n = (name ?? "").ToUpperInvariant();
            if (n.StartsWith("CUA-DI", StringComparison.Ordinal)) return "door";
            if (n.StartsWith("CUA-SO", StringComparison.Ordinal)) return "window";
            return null;
        }

        // ------------------------------------------------------------------ heights

        public sealed class Heights
        {
            public double Wall = DefaultWallHeight, Door = DefaultDoorHeight, Sill = DefaultSill, Window = DefaultWindowHeight, Slab = DefaultSlabThickness;
        }

        /// <summary>Checks the heights (mm) for Claude; a door as high as the wall leaves no lintel (allowed).</summary>
        public static void CheckHeights(Heights h, string tool = "build_3d_from_plan")
        {
            if (h.Wall < MinWallHeight || h.Wall > MaxWallHeight)
                throw ToolException.Invalid($"{tool}: wall_height must be {MinWallHeight}–{MaxWallHeight} mm (got {h.Wall}).");
            if (h.Door <= 0 || h.Door > h.Wall)
                throw ToolException.Invalid($"{tool}: door_height must be > 0 and at most wall_height ({h.Wall}); got {h.Door}.");
            if (h.Sill < 0 || h.Window <= 0 || h.Sill + h.Window > h.Wall)
                throw ToolException.Invalid($"{tool}: window_sill + window_height must fit in the wall (0 ≤ sill, sill + height ≤ {h.Wall}); got {h.Sill} + {h.Window}.");
            if (h.Slab < 0 || h.Slab > MaxSlabThickness)
                throw ToolException.Invalid($"{tool}: slab_thickness must be 0–{MaxSlabThickness} mm (0 = no slab); got {h.Slab}.");
        }

        // ------------------------------------------------------------------ geometry helpers

        private static double Angle(PlanSeg s)
        {
            double a = Math.Atan2(s.Y2 - s.Y1, s.X2 - s.X1);
            if (a < 0) a += Math.PI;
            if (a >= Math.PI) a -= Math.PI;
            return a;
        }

        private static bool Parallel(double a, double b, double tolRad)
        {
            double d = Math.Abs(a - b);
            return d < tolRad || Math.PI - d < tolRad;
        }

        private static double[] Unit(PlanSeg s)
        {
            double l = s.Length;
            return new[] { (s.X2 - s.X1) / l, (s.Y2 - s.Y1) / l };
        }

        private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>Point of the wall frame: s along the wall from its origin, p across (left of Dir positive).</summary>
        private static (double s, double p) ToFrame(PlanWall w, double x, double y)
        {
            double dx = x - w.Origin[0], dy = y - w.Origin[1];
            return (dx * w.Dir[0] + dy * w.Dir[1], -dx * w.Dir[1] + dy * w.Dir[0]);
        }

        // ------------------------------------------------------------------ clean / merge lines

        /// <summary>
        /// Drops tiny pieces and merges collinear pieces that overlap or nearly touch (duplicated lines, lines broken
        /// in several pieces, small gaps of a sloppy drawing).
        /// </summary>
        public static List<PlanSeg> MergeCollinear(IEnumerable<PlanSeg> input, PlanOptions o)
        {
            var segs = input.Where(s => s.Length > o.JoinTol).ToList();
            double tolRad = o.AngleTolDeg * Math.PI / 180;
            var used = new bool[segs.Count];
            var result = new List<PlanSeg>();
            // group by direction + offset of the line: sort by angle so groups stay small
            var order = Enumerable.Range(0, segs.Count).OrderBy(i => Angle(segs[i])).ToList();
            for (int oi = 0; oi < order.Count; oi++)
            {
                int i = order[oi];
                if (used[i]) continue;
                var baseSeg = segs[i];
                var u = Unit(baseSeg);
                double ai = Angle(baseSeg);
                var members = new List<int> { i };
                used[i] = true;
                for (int oj = 0; oj < order.Count; oj++)
                {
                    int j = order[oj];
                    if (used[j]) continue;
                    var sj = segs[j];
                    if (sj.Thickness != baseSeg.Thickness) continue;
                    if (!Parallel(ai, Angle(sj), tolRad)) continue;
                    double p1 = -(sj.X1 - baseSeg.X1) * u[1] + (sj.Y1 - baseSeg.Y1) * u[0];
                    double p2 = -(sj.X2 - baseSeg.X1) * u[1] + (sj.Y2 - baseSeg.Y1) * u[0];
                    if (Math.Abs(p1) > o.LineTol || Math.Abs(p2) > o.LineTol) continue;
                    members.Add(j);
                    used[j] = true;
                }
                // intervals along u
                var iv = members.Select(m =>
                {
                    var s = segs[m];
                    double t1 = (s.X1 - baseSeg.X1) * u[0] + (s.Y1 - baseSeg.Y1) * u[1];
                    double t2 = (s.X2 - baseSeg.X1) * u[0] + (s.Y2 - baseSeg.Y1) * u[1];
                    return (a: Math.Min(t1, t2), b: Math.Max(t1, t2), src: s.Sources);
                }).OrderBy(x => x.a).ToList();
                double ca = iv[0].a, cb = iv[0].b;
                var csrc = new List<string>(iv[0].src);
                void Flush()
                {
                    result.Add(new PlanSeg
                    {
                        X1 = baseSeg.X1 + u[0] * ca, Y1 = baseSeg.Y1 + u[1] * ca,
                        X2 = baseSeg.X1 + u[0] * cb, Y2 = baseSeg.Y1 + u[1] * cb,
                        Sources = csrc.Distinct().ToList(), Thickness = baseSeg.Thickness,
                    });
                }
                for (int k = 1; k < iv.Count; k++)
                {
                    if (iv[k].a <= cb + o.JoinTol) { cb = Math.Max(cb, iv[k].b); csrc.AddRange(iv[k].src); }
                    else { Flush(); ca = iv[k].a; cb = iv[k].b; csrc = new List<string>(iv[k].src); }
                }
                Flush();
            }
            return result;
        }

        // ------------------------------------------------------------------ walls from face-line pairs

        private sealed class Rect
        {
            public double[] C0, C1; // centreline ends
            public double T;
            public List<string> Src = new List<string>();
            public string Mode;
        }

        /// <summary>A segment k joins the ends (x1,y1) and (x2,y2) (an end cap / door jamb), either way round.</summary>
        private static bool Joins(PlanSeg k, double x1, double y1, double x2, double y2, double tol) =>
            (Dist(k.X1, k.Y1, x1, y1) <= tol && Dist(k.X2, k.Y2, x2, y2) <= tol) ||
            (Dist(k.X2, k.Y2, x1, y1) <= tol && Dist(k.X1, k.Y1, x2, y2) <= tol);

        /// <summary>
        /// Wall rectangles from pairs of parallel face lines MinThickness–MaxThickness apart. A pair counts when the
        /// lines overlap by at least the thickness, or by less when a cap (jamb line) closes an end — so the two jambs
        /// of an opening never become a wall. A line between the two lines blocks the pair (only nearest neighbours).
        /// The longer line's overhang (up to the thickness) is added at each end: that fills L corners and T joints.
        /// </summary>
        private static List<Rect> PairRects(List<PlanSeg> segs, PlanOptions o, bool[] usedOut)
        {
            double tolRad = o.AngleTolDeg * Math.PI / 180;
            var cands = new List<(int i, int j, Rect r, List<int> caps)>();
            var ang = segs.Select(Angle).ToArray();
            for (int i = 0; i < segs.Count; i++)
            {
                var si = segs[i];
                var u = Unit(si);
                double li = si.Length;
                for (int j = i + 1; j < segs.Count; j++)
                {
                    if (!Parallel(ang[i], ang[j], tolRad)) continue;
                    var sj = segs[j];
                    double p1 = -(sj.X1 - si.X1) * u[1] + (sj.Y1 - si.Y1) * u[0];
                    double p2 = -(sj.X2 - si.X1) * u[1] + (sj.Y2 - si.Y1) * u[0];
                    if (Math.Abs(p1 - p2) > 2 * o.LineTol + li * Math.Sin(tolRad)) continue;
                    double off = (p1 + p2) / 2, d = Math.Abs(off);
                    if (d < o.MinThickness - o.LineTol || d > o.MaxThickness + o.LineTol) continue;
                    double t1 = (sj.X1 - si.X1) * u[0] + (sj.Y1 - si.Y1) * u[1];
                    double t2 = (sj.X2 - si.X1) * u[0] + (sj.Y2 - si.Y1) * u[1];
                    double ja = Math.Min(t1, t2), jb = Math.Max(t1, t2);
                    double o0 = Math.Max(0, ja), o1 = Math.Min(li, jb);
                    double ov = o1 - o0;
                    if (ov <= o.JoinTol) continue;
                    var caps = new List<int>();
                    if (ov < d - o.LineTol)
                    {
                        // short piece: only with a cap at an end (a pier between two openings), never two jambs facing each other
                        foreach (var end in new[] { o0, o1 })
                        {
                            double ax = si.X1 + u[0] * end, ay = si.Y1 + u[1] * end;
                            double bx = ax - u[1] * off, by = ay + u[0] * off;
                            for (int k = 0; k < segs.Count; k++)
                                if (k != i && k != j && Joins(segs[k], ax, ay, bx, by, 2 * o.JoinTol)) caps.Add(k);
                        }
                        if (caps.Count == 0) continue;
                    }
                    // nearest neighbours only: another parallel line between them over most of the overlap blocks the pair
                    bool blocked = false;
                    for (int k = 0; k < segs.Count && !blocked; k++)
                    {
                        if (k == i || k == j || !Parallel(ang[i], ang[k], tolRad)) continue;
                        var sk = segs[k];
                        double q1 = -(sk.X1 - si.X1) * u[1] + (sk.Y1 - si.Y1) * u[0];
                        double q2 = -(sk.X2 - si.X1) * u[1] + (sk.Y2 - si.Y1) * u[0];
                        double q = (q1 + q2) / 2;
                        if (Math.Abs(q1 - q2) > 2 * o.LineTol + li * Math.Sin(tolRad)) continue;
                        if (!(Math.Abs(q) > o.LineTol && Math.Abs(q - off) > o.LineTol && Math.Sign(q) == Math.Sign(off) && Math.Abs(q) < d)) continue;
                        double r1 = (sk.X1 - si.X1) * u[0] + (sk.Y1 - si.Y1) * u[1];
                        double r2 = (sk.X2 - si.X1) * u[0] + (sk.Y2 - si.Y1) * u[1];
                        double kov = Math.Min(o1, Math.Max(r1, r2)) - Math.Max(o0, Math.Min(r1, r2));
                        if (kov > 0.5 * ov) blocked = true;
                    }
                    if (blocked) continue;
                    double e0 = Math.Min(o0 - Math.Min(0, ja), d), e1 = Math.Min(Math.Max(li, jb) - o1, d);
                    double c = off / 2;
                    var nx = -u[1]; var ny = u[0];
                    cands.Add((i, j, new Rect
                    {
                        C0 = new[] { si.X1 + u[0] * (o0 - e0) + nx * c, si.Y1 + u[1] * (o0 - e0) + ny * c },
                        C1 = new[] { si.X1 + u[0] * (o1 + e1) + nx * c, si.Y1 + u[1] * (o1 + e1) + ny * c },
                        T = d,
                        Src = si.Sources.Concat(sj.Sources).Distinct().ToList(),
                        Mode = "pair",
                    }, caps));
                }
            }
            // the jambs that close a short pier are its caps, not a wall across it
            var capOf = new HashSet<int>(cands.SelectMany(c => c.caps));
            var rects = new List<Rect>();
            foreach (var c in cands)
            {
                if (capOf.Contains(c.i) && capOf.Contains(c.j)) continue;
                rects.Add(c.r);
                usedOut[c.i] = true;
                usedOut[c.j] = true;
            }
            return rects;
        }

        /// <summary>
        /// Wall rectangles from centrelines with a thickness (centerline_handles, wide polylines): an end that meets
        /// another centreline's end is extended by half the thickness so L corners close.
        /// </summary>
        private static List<Rect> CenterRects(List<PlanSeg> segs, PlanOptions o)
        {
            var rects = new List<Rect>();
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                var u = Unit(s);
                double h = s.Thickness / 2;
                bool Meets(double x, double y) => segs.Where((k, j) => j != i).Any(k => Dist(k.X1, k.Y1, x, y) <= o.JoinTol || Dist(k.X2, k.Y2, x, y) <= o.JoinTol);
                double e0 = Meets(s.X1, s.Y1) ? h : 0, e1 = Meets(s.X2, s.Y2) ? h : 0;
                rects.Add(new Rect
                {
                    C0 = new[] { s.X1 - u[0] * e0, s.Y1 - u[1] * e0 },
                    C1 = new[] { s.X2 + u[0] * e1, s.Y2 + u[1] * e1 },
                    T = s.Thickness,
                    Src = new List<string>(s.Sources),
                    Mode = "centerline",
                });
            }
            return rects;
        }

        /// <summary>Rectangles on one line with the same thickness become one wall; the space between two parts is a gap.</summary>
        private static List<PlanWall> MergeRects(List<Rect> rects, PlanOptions o)
        {
            double tolRad = o.AngleTolDeg * Math.PI / 180;
            var used = new bool[rects.Count];
            var walls = new List<PlanWall>();
            for (int i = 0; i < rects.Count; i++)
            {
                if (used[i]) continue;
                var r = rects[i];
                var seg = new PlanSeg(r.C0[0], r.C0[1], r.C1[0], r.C1[1]);
                if (seg.Length <= o.JoinTol) { used[i] = true; continue; }
                var u = Unit(seg);
                double ai = Angle(seg);
                var w = new PlanWall { Origin = r.C0, Dir = u, Thickness = r.T, Mode = r.Mode };
                var iv = new List<double[]>();
                for (int j = i; j < rects.Count; j++)
                {
                    if (used[j]) continue;
                    var q = rects[j];
                    var qs = new PlanSeg(q.C0[0], q.C0[1], q.C1[0], q.C1[1]);
                    if (qs.Length <= o.JoinTol) continue;
                    if (Math.Abs(q.T - r.T) > 2 * o.LineTol || !Parallel(ai, Angle(qs), tolRad)) continue;
                    var (s0, p0) = ToFrame(w, q.C0[0], q.C0[1]);
                    var (s1, p1) = ToFrame(w, q.C1[0], q.C1[1]);
                    if (Math.Abs(p0) > 2 * o.LineTol || Math.Abs(p1) > 2 * o.LineTol) continue;
                    used[j] = true;
                    iv.Add(new[] { Math.Min(s0, s1), Math.Max(s0, s1) });
                    w.Sources.AddRange(q.Src);
                    if (q.Mode != w.Mode) w.Mode = "mixed";
                }
                iv.Sort((a, b) => a[0].CompareTo(b[0]));
                foreach (var x in iv)
                {
                    if (w.Solid.Count > 0 && x[0] <= w.Solid[w.Solid.Count - 1][1] + o.JoinTol)
                        w.Solid[w.Solid.Count - 1][1] = Math.Max(w.Solid[w.Solid.Count - 1][1], x[1]);
                    else w.Solid.Add(new[] { x[0], x[1] });
                }
                w.Sources = w.Sources.Distinct().ToList();
                walls.Add(w);
            }
            return walls;
        }

        // ------------------------------------------------------------------ recognition

        /// <summary>
        /// Walls from face lines (pairs) and centreline pieces (Thickness > 0), the openings from the gaps and the
        /// symbols, and what was skipped. Lines are in drawing units; o too.
        /// </summary>
        public static PlanResult Recognize(IList<PlanSeg> faceLines, IList<PlanSeg> centerLines, IList<PlanSymbol> symbols, PlanOptions o)
        {
            var res = new PlanResult();
            var faces = MergeCollinear(faceLines ?? new List<PlanSeg>(), o);
            var centers = MergeCollinear(centerLines ?? new List<PlanSeg>(), o);
            var usedFace = new bool[faces.Count];
            var rects = PairRects(faces, o, usedFace);
            rects.AddRange(CenterRects(centers, o));
            res.Walls = MergeRects(rects, o);

            // face lines without a partner: a short end line between paired lines is a cap (door jamb / wall end)
            var pairedEnds = new List<double[]>();
            for (int i = 0; i < faces.Count; i++)
                if (usedFace[i]) { pairedEnds.Add(new[] { faces[i].X1, faces[i].Y1 }); pairedEnds.Add(new[] { faces[i].X2, faces[i].Y2 }); }
            bool NearEnd(double x, double y) => pairedEnds.Any(e => Dist(e[0], e[1], x, y) <= 2 * o.JoinTol) || res.Walls.Any(w => InsideWall(w, x, y, o.JoinTol * 2));
            for (int i = 0; i < faces.Count; i++)
            {
                if (usedFace[i]) continue;
                var s = faces[i];
                if (s.Length <= o.MaxThickness + o.LineTol && NearEnd(s.X1, s.Y1) && NearEnd(s.X2, s.Y2)) { res.Caps++; continue; }
                foreach (var h in s.Sources.Distinct())
                    res.Skipped.Add(new PlanSkip(h, "LINE", $"no parallel wall line {Fmt(o.MinThickness)}–{Fmt(o.MaxThickness)} away (a single line, or a wall thickness outside min_thickness–max_thickness): not a wall"));
            }

            AssignSymbols(res, symbols ?? new List<PlanSymbol>(), o);
            FillJunctions(res, o);
            return res;
        }

        private static string Fmt(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);

        private static bool InsideWall(PlanWall w, double x, double y, double tol)
        {
            var (s, p) = ToFrame(w, x, y);
            if (Math.Abs(p) > w.Thickness / 2 + tol) return false;
            return w.Solid.Any(iv => s >= iv[0] - tol && s <= iv[1] + tol);
        }

        /// <summary>The s / p ranges of a world box in the wall's frame (its four corners).</summary>
        private static (double s0, double s1, double p0, double p1) BoxInFrame(PlanWall w, double[] min, double[] max)
        {
            double s0 = double.MaxValue, s1 = double.MinValue, p0 = double.MaxValue, p1 = double.MinValue;
            foreach (var (x, y) in new[] { (min[0], min[1]), (max[0], min[1]), (max[0], max[1]), (min[0], max[1]) })
            {
                var (s, p) = ToFrame(w, x, y);
                s0 = Math.Min(s0, s); s1 = Math.Max(s1, s); p0 = Math.Min(p0, p); p1 = Math.Max(p1, p);
            }
            return (s0, s1, p0, p1);
        }

        /// <summary>Symbols of one kind whose boxes touch are one symbol (a door leaf line + its swing arc).</summary>
        public static List<PlanSymbol> ClusterSymbols(IEnumerable<PlanSymbol> symbols, double tol)
        {
            var list = symbols.Select(s => new PlanSymbol { Kind = s.Kind, Min = (double[])s.Min.Clone(), Max = (double[])s.Max.Clone(), Sources = new List<string>(s.Sources) }).ToList();
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < list.Count && !merged; i++)
                    for (int j = i + 1; j < list.Count && !merged; j++)
                    {
                        var a = list[i]; var b = list[j];
                        if (a.Kind != b.Kind) continue;
                        if (a.Min[0] > b.Max[0] + tol || b.Min[0] > a.Max[0] + tol || a.Min[1] > b.Max[1] + tol || b.Min[1] > a.Max[1] + tol) continue;
                        a.Min = new[] { Math.Min(a.Min[0], b.Min[0]), Math.Min(a.Min[1], b.Min[1]) };
                        a.Max = new[] { Math.Max(a.Max[0], b.Max[0]), Math.Max(a.Max[1], b.Max[1]) };
                        a.Sources.AddRange(b.Sources);
                        list.RemoveAt(j);
                        merged = true;
                    }
            }
            return list;
        }

        private static void AssignSymbols(PlanResult res, IList<PlanSymbol> symbols, PlanOptions o)
        {
            var clusters = ClusterSymbols(symbols, o.JoinTol);
            // gaps of every wall
            var gaps = new List<(int wall, double s0, double s1)>();
            for (int wi = 0; wi < res.Walls.Count; wi++)
            {
                var w = res.Walls[wi];
                for (int k = 1; k < w.Solid.Count; k++) gaps.Add((wi, w.Solid[k - 1][1], w.Solid[k][0]));
            }
            var gapTaken = new Dictionary<int, PlanOpening>();
            foreach (var sym in clusters)
            {
                // 1. a gap the symbol overlaps (both wall lines stop: the skill's way to draw an opening)
                int best = -1; double bestScore = 0;
                for (int gi = 0; gi < gaps.Count; gi++)
                {
                    var g = gaps[gi]; var w = res.Walls[g.wall];
                    var (s0, s1, p0, p1) = BoxInFrame(w, sym.Min, sym.Max);
                    double half = w.Thickness / 2 + Math.Max(o.JoinTol, w.Thickness / 2);
                    if (p1 < -half || p0 > half) continue;
                    double ov = Math.Min(s1, g.s1) - Math.Max(s0, g.s0);
                    if (ov <= o.JoinTol) continue;
                    if (ov > bestScore) { bestScore = ov; best = gi; }
                }
                if (best >= 0)
                {
                    var g = gaps[best];
                    double width = g.s1 - g.s0;
                    if (width > o.MaxOpening || width < o.MinOpening)
                    {
                        foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, $"the gap it sits in is {Fmt(width)} wide (opening must be {Fmt(o.MinOpening)}–{Fmt(o.MaxOpening)}): left as drawn, no {sym.Kind}"));
                        continue;
                    }
                    if (gapTaken.TryGetValue(best, out var taken))
                    {
                        if (taken.Kind == sym.Kind) { taken.Sources.AddRange(sym.Sources); continue; }
                        foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, $"the opening already has a {taken.Kind} symbol ({string.Join(", ", taken.Sources.Take(3))}): not used"));
                        continue;
                    }
                    var op = new PlanOpening { Wall = g.wall, S0 = g.s0, S1 = g.s1, Kind = sym.Kind, From = "gap", Sources = new List<string>(sym.Sources) };
                    gapTaken[best] = op;
                    res.Openings.Add(op);
                    continue;
                }
                // 2. over a continuous wall: the wall part under the symbol
                // (a symbol drawn only beside the wall face, like a swing arc, touches it: pov ≈ 0 still counts)
                int bw = -1; double bestArea = 0, secondArea = 0; double bs0 = 0, bs1 = 0;
                for (int wi = 0; wi < res.Walls.Count; wi++)
                {
                    var w = res.Walls[wi];
                    var (s0, s1, p0, p1) = BoxInFrame(w, sym.Min, sym.Max);
                    double pov = Math.Min(p1, w.Thickness / 2) - Math.Max(p0, -w.Thickness / 2);
                    if (pov < -o.JoinTol) continue;
                    double wBest = 0, wa = 0, wb = 0;
                    foreach (var iv in w.Solid)
                    {
                        double a = Math.Max(s0, iv[0]), b = Math.Min(s1, iv[1]);
                        if (b - a <= o.JoinTol) continue;
                        double score = (b - a) * Math.Max(pov, o.JoinTol);
                        if (score > wBest) { wBest = score; wa = a; wb = b; }
                    }
                    if (wBest <= 0) continue;
                    if (wBest > bestArea) { secondArea = bestArea; bestArea = wBest; bw = wi; bs0 = wa; bs1 = wb; }
                    else if (wBest > secondArea) secondArea = wBest;
                }
                if (bw >= 0 && secondArea > 0.9 * bestArea)
                {
                    foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, "touches two walls equally (e.g. at a corner): not guessed; pass it in 'openings' with its point and width"));
                    continue;
                }
                if (bw >= 0)
                {
                    double width = bs1 - bs0;
                    if (width < o.MinOpening || width > o.MaxOpening)
                    {
                        foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, $"over a continuous wall, but its width along the wall is {Fmt(width)} (opening must be {Fmt(o.MinOpening)}–{Fmt(o.MaxOpening)}): no opening cut"));
                        continue;
                    }
                    if (res.Openings.Any(x => x.Wall == bw && x.S0 < bs1 && x.S1 > bs0))
                    {
                        foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, "overlaps another opening of the same wall: not used"));
                        continue;
                    }
                    res.Openings.Add(new PlanOpening { Wall = bw, S0 = bs0, S1 = bs1, Kind = sym.Kind, From = "symbol", Sources = new List<string>(sym.Sources) });
                    continue;
                }
                foreach (var h in sym.Sources.Distinct()) res.Skipped.Add(new PlanSkip(h, sym.Kind, "not on or next to any recognised wall: no opening"));
            }
            for (int gi = 0; gi < gaps.Count; gi++)
                if (!gapTaken.ContainsKey(gi)) res.OpenGaps.Add(gaps[gi]);
        }

        /// <summary>
        /// A gap without symbol that another wall runs into or through (a + junction drawn with every line broken)
        /// is a junction, not an opening: the gap becomes wall again.
        /// </summary>
        private static void FillJunctions(PlanResult res, PlanOptions o)
        {
            var keep = new List<(int wall, double s0, double s1)>();
            foreach (var g in res.OpenGaps)
            {
                var w = res.Walls[g.wall];
                double width = g.s1 - g.s0;
                bool junction = false;
                if (width <= o.MaxThickness + 2 * o.JoinTol)
                {
                    var mid = w.At((g.s0 + g.s1) / 2);
                    for (int k = 0; k < res.Walls.Count && !junction; k++)
                    {
                        if (k == g.wall) continue;
                        var x = res.Walls[k];
                        double cross = Math.Abs(x.Dir[0] * w.Dir[1] - x.Dir[1] * w.Dir[0]);
                        if (cross < 0.5) continue; // only walls across this one
                        // the other wall reaches into the gap: one of its ends (or its body) within the gap square
                        var (s, p) = ToFrame(x, mid[0], mid[1]);
                        if (Math.Abs(p) <= x.Thickness / 2 + o.JoinTol && s >= x.Start - w.Thickness / 2 - 2 * o.JoinTol && s <= x.End + w.Thickness / 2 + 2 * o.JoinTol) junction = true;
                    }
                }
                if (junction)
                {
                    res.JunctionsFilled++;
                    for (int k = 1; k < w.Solid.Count; k++)
                    {
                        if (Math.Abs(w.Solid[k - 1][1] - g.s0) < 1e-9 && Math.Abs(w.Solid[k][0] - g.s1) < 1e-9)
                        {
                            w.Solid[k - 1][1] = w.Solid[k][1];
                            w.Solid.RemoveAt(k);
                            // openings found later in this wall still refer to s, which did not change
                            break;
                        }
                    }
                }
                else keep.Add(g);
            }
            res.OpenGaps = keep;
        }

        /// <summary>
        /// An opening the caller gives by a point (openings parameter): the gap the point is in, or a width centred
        /// on the point along the wall that contains it. Returns the error text when no wall fits.
        /// </summary>
        public static string AddGivenOpening(PlanResult res, string kind, double[] at, double width, double? bottom, double? top, PlanOptions o)
        {
            for (int wi = 0; wi < res.Walls.Count; wi++)
            {
                var w = res.Walls[wi];
                var (s, p) = ToFrame(w, at[0], at[1]);
                if (Math.Abs(p) > w.Thickness / 2 + o.JoinTol) continue;
                if (s < w.Start - o.JoinTol || s > w.End + o.JoinTol) continue;
                double s0, s1;
                var gap = res.OpenGaps.FindIndex(g => g.wall == wi && s >= g.s0 - o.JoinTol && s <= g.s1 + o.JoinTol);
                if (gap >= 0 && width <= 0) { s0 = res.OpenGaps[gap].s0; s1 = res.OpenGaps[gap].s1; res.OpenGaps.RemoveAt(gap); }
                else
                {
                    if (width <= 0) return $"the point [{Fmt(at[0])}, {Fmt(at[1])}] is on a continuous wall: give the opening width";
                    s0 = s - width / 2; s1 = s + width / 2;
                    if (gap >= 0) res.OpenGaps.RemoveAt(gap);
                }
                var existing = res.Openings.FirstOrDefault(x => x.Wall == wi && x.S0 < s1 - o.JoinTol && x.S1 > s0 + o.JoinTol);
                if (existing != null) res.Openings.Remove(existing); // the given opening replaces a recognised one
                res.Openings.Add(new PlanOpening { Wall = wi, S0 = s0, S1 = s1, Kind = kind, From = "given", Bottom = bottom ?? double.NaN, Top = top ?? double.NaN });
                return null;
            }
            return $"no recognised wall at [{Fmt(at[0])}, {Fmt(at[1])}]";
        }

        // ------------------------------------------------------------------ boxes

        /// <summary>The z ranges (relative to the floor) where wall remains at an opening.</summary>
        public static List<double[]> WallAround(PlanOpening op, Heights h)
        {
            double bottom = !double.IsNaN(op.Bottom) && op.From == "given" ? op.Bottom : op.Kind == "door" ? 0 : h.Sill;
            double top = !double.IsNaN(op.Top) && op.From == "given" ? op.Top : op.Kind == "door" ? h.Door : h.Sill + h.Window;
            var z = new List<double[]>();
            if (bottom > 1e-9) z.Add(new[] { 0.0, Math.Min(bottom, h.Wall) });
            if (top < h.Wall - 1e-9) z.Add(new[] { Math.Max(top, 0), h.Wall });
            return z.Where(r => r[1] - r[0] > 1e-9).ToList();
        }

        /// <summary>Fills the resolved bottom / top of every opening (for the report).</summary>
        public static void ResolveHeights(PlanResult res, Heights h)
        {
            foreach (var op in res.Openings)
            {
                if (op.From != "given" || double.IsNaN(op.Bottom)) op.Bottom = op.Kind == "door" ? 0 : h.Sill;
                if (op.From != "given" || double.IsNaN(op.Top)) op.Top = op.Kind == "door" ? h.Door : h.Sill + h.Window;
            }
        }

        /// <summary>
        /// Every wall split into boxes: full height on its solid parts, the wall below / above each opening. Heights in
        /// drawing units (scale h before), z relative to the floor.
        /// </summary>
        public static List<PlanBox> Boxes(PlanResult res, Heights h)
        {
            var boxes = new List<PlanBox>();
            for (int wi = 0; wi < res.Walls.Count; wi++)
            {
                var w = res.Walls[wi];
                // pieces along the wall: (s0, s1, z ranges)
                var parts = w.Solid.Select(iv => (s0: iv[0], s1: iv[1], z: new List<double[]> { new[] { 0.0, h.Wall } })).ToList();
                foreach (var op in res.Openings.Where(x => x.Wall == wi).OrderBy(x => x.S0))
                {
                    var around = WallAround(op, h);
                    var next = new List<(double s0, double s1, List<double[]> z)>();
                    foreach (var pt in parts)
                    {
                        if (pt.s1 <= op.S0 || pt.s0 >= op.S1) { next.Add(pt); continue; }
                        if (pt.s0 < op.S0) next.Add((pt.s0, op.S0, pt.z));
                        next.Add((Math.Max(pt.s0, op.S0), Math.Min(pt.s1, op.S1), around));
                        if (pt.s1 > op.S1) next.Add((op.S1, pt.s1, pt.z));
                    }
                    // an opening in a gap: nothing there yet
                    if (op.From != "symbol" && !parts.Any(pt => pt.s0 < op.S1 && pt.s1 > op.S0)) next.Add((op.S0, op.S1, around));
                    else if (op.From != "symbol")
                    {
                        // a given opening partly over a gap: add the gap part
                        double covered = parts.Where(pt => pt.s0 < op.S1 && pt.s1 > op.S0).Sum(pt => Math.Min(pt.s1, op.S1) - Math.Max(pt.s0, op.S0));
                        if (covered < op.S1 - op.S0 - 1e-9)
                        {
                            var holes = new List<(double, double)>();
                            double cur = op.S0;
                            foreach (var pt in parts.Where(pt => pt.s0 < op.S1 && pt.s1 > op.S0).OrderBy(pt => pt.s0))
                            {
                                if (pt.s0 > cur) holes.Add((cur, pt.s0));
                                cur = Math.Max(cur, pt.s1);
                            }
                            if (cur < op.S1) holes.Add((cur, op.S1));
                            foreach (var (a, b) in holes) next.Add((a, b, around));
                        }
                    }
                    parts = next.OrderBy(x => x.s0).ToList();
                }
                foreach (var pt in parts)
                {
                    if (pt.s1 - pt.s0 <= 1e-9) continue;
                    foreach (var z in pt.z)
                    {
                        var c = w.At((pt.s0 + pt.s1) / 2);
                        boxes.Add(new PlanBox
                        {
                            Center = new[] { c[0], c[1], (z[0] + z[1]) / 2 },
                            Length = pt.s1 - pt.s0, Width = w.Thickness, Height = z[1] - z[0],
                            RotationDeg = w.AngleDeg, Wall = wi,
                        });
                    }
                }
            }
            return boxes;
        }

        /// <summary>Total centreline length (mm) per thickness (mm, rounded), longest first: "tường 220: 42 m".</summary>
        public static List<(double thickness, double length)> ThicknessSummary(PlanResult res, double mmPerUnit = 1)
        {
            return res.Walls
                .GroupBy(w => Math.Round(w.Thickness * mmPerUnit))
                .Select(g => (g.Key, g.Sum(w => w.Solid.Sum(iv => iv[1] - iv[0])) * mmPerUnit))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }

        /// <summary>Bounding box of the walls' footprint (xy).</summary>
        public static (double[] min, double[] max)? Footprint(PlanResult res)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var w in res.Walls)
            {
                var n = new[] { -w.Dir[1], w.Dir[0] };
                foreach (var s in new[] { w.Start, w.End })
                    foreach (var sg in new[] { -1, 1 })
                    {
                        var c = w.At(s);
                        double x = c[0] + n[0] * sg * w.Thickness / 2, y = c[1] + n[1] * sg * w.Thickness / 2;
                        x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                    }
            }
            return res.Walls.Count == 0 ? ((double[], double[])?)null : (new[] { x0, y0 }, new[] { x1, y1 });
        }

        /// <summary>Two xy boxes overlap (touching counts).</summary>
        public static bool BoxesOverlap(double[] amin, double[] amax, double[] bmin, double[] bmax, double tol = 0) =>
            !(amin[0] > bmax[0] + tol || bmin[0] > amax[0] + tol || amin[1] > bmax[1] + tol || bmin[1] > amax[1] + tol);
    }

    /// <summary>
    /// 3D phase 3, AutoCAD-free part of section_view / projection_view: the frame of the 2D result (a point P of the
    /// model lands at position + ((P − origin)·u, (P − origin)·v)), the plane of a plan section line, where to place the
    /// result so it does not cover the model, and the joining of exploded pieces into closed loops.
    /// </summary>
    public static class SectionLogic
    {
        public const int MaxSolids = 500;
        public const int MaxResultEntities = 20000;
        public const string OutlineLayer = "A-SECT", HatchLayer = "A-SECT-PAT", BehindLayer = "A-SECT-BACK", ElevationLayer = "A-ELEV";
        public const double DefaultHatchScale = 10;

        /// <summary>
        /// Right (u) and up (v) of a viewer who looks along −n (n points from the plane towards the viewer):
        /// up is +Z for a vertical plane, +Y for a plan (n = +Z) and its mirror from below.
        /// </summary>
        public static (double[] u, double[] v) Frame(double[] n)
        {
            var nn = SolidLogic.Unit(n, "the plane normal must not be [0, 0, 0]");
            var up = Math.Abs(nn[2]) > 0.99 ? new double[] { 0, nn[2] > 0 ? 1 : -1, 0 } : new double[] { 0, 0, 1 };
            if (Math.Abs(nn[2]) > 0.99)
            {
                // looking down (n = +Z): right = X, up = Y; looking up (n = −Z): right = −X... keep Y up, X mirrored
                var u0 = SolidLogic.Unit(SolidLogic.Cross(new double[] { 0, 1, 0 }, nn), "frame");
                var v0 = SolidLogic.Cross(nn, u0);
                return (Clean(u0), Clean(v0));
            }
            var u = SolidLogic.Unit(SolidLogic.Cross(up, nn), "frame");
            var v = SolidLogic.Cross(nn, u);
            return (Clean(u), Clean(v));
        }

        private static double[] Clean(double[] a) => a.Select(x => Math.Abs(x) < 1e-12 ? 0 : x).ToArray();

        /// <summary>
        /// The vertical cutting plane of a section line drawn in plan from a to b: the viewer stands on the left of
        /// a → b and looks to the right (flip: the other way). Normal points to the viewer.
        /// </summary>
        public static PlaneDef PlaneFromLine(double[] a, double[] b, bool flip, string tool)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1];
            double l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-9) throw ToolException.Invalid($"{tool}: the two points of 'line' must differ (in plan).");
            var n = new[] { -dy / l, dx / l, 0 };
            if (flip) n = new[] { -n[0], -n[1], 0.0 };
            return new PlaneDef { Point = new[] { a[0], a[1], SolidLogic.Z3(a) }, Normal = Clean(n) };
        }

        /// <summary>Maps a model point into the result's 2D frame (before placing).</summary>
        public static double[] ToFrame(double[] p, double[] origin, double[] u, double[] v)
        {
            var d = SolidLogic.Sub(p, origin);
            return new[] { SolidLogic.Dot(d, u), SolidLogic.Dot(d, v) };
        }

        /// <summary>
        /// The projection plane of an elevation: outside the model's box on the viewer's side, so nothing is cut
        /// and the whole model is "behind" it. viewDir points from the model to the viewer (like set_view_3d).
        /// </summary>
        public static PlaneDef ProjectionPlane(double[] min, double[] max, double[] viewDir)
        {
            var n = SolidLogic.Unit(viewDir, "the view direction must not be [0, 0, 0]");
            var c = new[] { (min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (SolidLogic.Z3(min) + SolidLogic.Z3(max)) / 2 };
            double r = SolidLogic.Len(SolidLogic.Sub(max, min)) / 2;
            double margin = Math.Max(r * 0.1, 10);
            var k = r + margin;
            return new PlaneDef { Point = new[] { c[0] + n[0] * k, c[1] + n[1] * k, c[2] + n[2] * k }, Normal = n };
        }

        /// <summary>
        /// Lower-left corner of a w × h result placed to the right of the model (its xy box), a gap of 10 % of the
        /// model width (at least minGap) away, bottom aligned with the model: never on top of it.
        /// </summary>
        public static double[] AutoPosition(double[] modelMin, double[] modelMax, double minGap)
        {
            double width = modelMax[0] - modelMin[0];
            double gap = Math.Max(minGap, width * 0.1);
            return new[] { modelMax[0] + gap, modelMin[1] };
        }

        /// <summary>Bulge of an arc piece from start to end angle (radians, counter-clockwise), reversed when walked backwards.</summary>
        public static double Bulge(double startAngle, double endAngle, bool reversed)
        {
            double sweep = endAngle - startAngle;
            while (sweep <= 0) sweep += 2 * Math.PI;
            while (sweep > 2 * Math.PI) sweep -= 2 * Math.PI;
            double b = Math.Tan(sweep / 4);
            return reversed ? -b : b;
        }

        /// <summary>A piece of an exploded outline: its ends in the 2D frame.</summary>
        public sealed class Piece
        {
            public double[] A, B;
            public int Index;
        }

        /// <summary>
        /// Joins pieces end to end into closed loops (index list, true = walked backwards). Pieces that close by
        /// themselves (a circle) are loops of one. Leftovers (open chains) are returned apart.
        /// </summary>
        public static (List<List<(int index, bool reversed)>> loops, List<int> open) ChainLoops(IList<Piece> pieces, double tol)
        {
            var loops = new List<List<(int, bool)>>();
            var open = new List<int>();
            var used = new bool[pieces.Count];
            bool Same(double[] a, double[] b) => Math.Abs(a[0] - b[0]) <= tol && Math.Abs(a[1] - b[1]) <= tol;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                var loop = new List<(int, bool)> { (i, false) };
                if (Same(pieces[i].A, pieces[i].B)) { loops.Add(loop); continue; }
                var start = pieces[i].A;
                var cur = pieces[i].B;
                bool closed = false;
                while (true)
                {
                    if (Same(cur, start)) { closed = true; break; }
                    int next = -1; bool rev = false;
                    for (int j = 0; j < pieces.Count; j++)
                    {
                        if (used[j]) continue;
                        if (Same(pieces[j].A, cur)) { next = j; rev = false; break; }
                        if (Same(pieces[j].B, cur)) { next = j; rev = true; break; }
                    }
                    if (next < 0) break;
                    used[next] = true;
                    loop.Add((next, rev));
                    cur = rev ? pieces[next].A : pieces[next].B;
                }
                if (closed) loops.Add(loop);
                else open.AddRange(loop.Select(x => x.Item1));
            }
            return (loops, open);
        }

        /// <summary>The visual check of a placed result: its box must not overlap the model's xy box.</summary>
        public static bool CoversModel(double[] resMin, double[] resMax, double[] modelMin, double[] modelMax) =>
            FloorPlanLogic.BoxesOverlap(resMin, resMax, modelMin, modelMax);
    }
}
