using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F3: one piece of a sloped roof over a right-angled outline. The roof is the union of the pieces' tops
    /// (Top ∩ TopPlanes) minus the union of their undersides (Foot ∩ UnderPlanes): where two pieces meet, the higher slab
    /// wins and the lower one is cut away, so there is no overlap, the valleys come out of the union and the vertical
    /// thickness is the same everywhere (volume = eave area × thickness / cos slope).
    /// </summary>
    public sealed class RoofPiece
    {
        /// <summary>Plan rectangle (mm, CCW) of the piece.</summary>
        public List<double[]> Rect = new List<double[]>();
        /// <summary>Indexes (0–3) of the sloped edges of Rect (4 for hip, the 2 along the ridge for gable).</summary>
        public List<int> Sloped = new List<int>();
        public Prism3 Top, Foot;
        public List<Prism3> TopPlanes = new List<Prism3>(), UnderPlanes = new List<Prism3>();
        public double RidgeZ;

        public bool InTop(double[] p) => Top.Contains(p) && TopPlanes.All(q => q.Contains(p));
        public bool InUnder(double[] p) => Foot.Contains(p) && UnderPlanes.All(q => q.Contains(p));
    }

    public static partial class RoofLogic
    {
        /// <summary>Most corners of a right-angled outline a sloped roof is built over (larger: refused, flat still works).</summary>
        public const int MaxRightAngledCorners = 40;
        /// <summary>Most roof pieces (rectangles) of a right-angled outline (each costs AutoCAD booleans).</summary>
        public const int MaxRoofPieces = 12;

        /// <summary>Every edge is parallel or perpendicular to the first one (within StairLogic.SquareTol).</summary>
        public static bool IsRightAngled(IList<double[]> pts)
        {
            if (pts.Count < 4 || pts.Count % 2 != 0) return false;
            var u = Dir(pts[0], pts[1]);
            if (u == null) return false;
            for (int i = 0; i < pts.Count; i++)
            {
                var e = Dir(pts[i], pts[(i + 1) % pts.Count]);
                if (e == null) return false;
                double dot = Math.Abs(e[0] * u[0] + e[1] * u[1]);
                if (dot > StairLogic.SquareTol && Math.Abs(dot - 1) > StairLogic.SquareTol) return false;
            }
            return true;
        }

        /// <summary>
        /// KTF-01 (kiểm tra độc lập đợt F): a right-angled outline drawn by hand or turned and rounded is off by a little
        /// (IsRightAngled takes edges within StairLogic.SquareTol, ~20 mm over 10 m), and the grid of MaximalRectangles
        /// then found near-twin rectangles: thin slivers, a "slightly wider" rectangle taken as a wing, a wrong gable.
        /// The outline is squared first: the frame is the length-weighted mean direction of all edges (folded to 90°),
        /// every edge becomes exactly horizontal / vertical in it at the mean of its two ends, and each corner takes the
        /// x of its vertical edge and the y of its horizontal one. Moves a corner by about the drawing error only.
        /// Null when the edges do not alternate across / along (then the outline is not a right-angled one).
        /// </summary>
        public static List<double[]> SquareUp(IList<double[]> pts)
        {
            int n = pts.Count;
            if (n < 4 || n % 2 != 0) return null;
            double sx = 0, sy = 0;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
                if (l < 1e-9) return null;
                double t4 = 4 * Math.Atan2(dy, dx); // edge directions folded to 90°: the same for along / across
                sx += l * Math.Cos(t4); sy += l * Math.Sin(t4);
            }
            double th = Math.Atan2(sy, sx) / 4, c = Math.Cos(th), s = Math.Sin(th);
            var loc = pts.Select(q => new[] { q[0] * c + q[1] * s, -q[0] * s + q[1] * c }).ToList();
            // edge i (from corner i to i + 1) is along x or along y in the frame
            var alongX = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var a = loc[i]; var b = loc[(i + 1) % n];
                alongX[i] = Math.Abs(b[0] - a[0]) >= Math.Abs(b[1] - a[1]);
                if (i > 0 && alongX[i] == alongX[i - 1]) return null;
            }
            if (alongX[n - 1] == alongX[0]) return null;
            var lvl = new double[n]; // y of an edge along x, x of an edge along y
            for (int i = 0; i < n; i++)
            {
                var a = loc[i]; var b = loc[(i + 1) % n];
                lvl[i] = alongX[i] ? (a[1] + b[1]) / 2 : (a[0] + b[0]) / 2;
            }
            // KTF-01 (lần 2): levels closer than SnapTol are one level (a corner off by a few mm on both sides of an inward
            // corner, a 2 mm jog in a wall line): their edges become one line, the zero-length edges between them go
            double size = Math.Max(loc.Max(q => q[0]) - loc.Min(q => q[0]), loc.Max(q => q[1]) - loc.Min(q => q[1]));
            double tol = Math.Max(SnapMinMm, StairLogic.SquareTol * size);
            Snap(lvl, alongX, true, tol);
            Snap(lvl, alongX, false, tol);
            var res = new List<double[]>(n);
            for (int k = 0; k < n; k++)
            {
                int prev = (k - 1 + n) % n; // corner k sits between edge k - 1 and edge k
                double x = alongX[k] ? lvl[prev] : lvl[k], y = alongX[k] ? lvl[k] : lvl[prev];
                res.Add(new[] { x * c - y * s, x * s + y * c });
            }
            var clean = Clean(res, 1e-6);
            if (clean.Count < 4 || clean.Count % 2 != 0 || !IsRightAngled(clean)) return null;
            return clean;
        }

        /// <summary>Two roof parts whose widths differ by less than this (mm) are equally wide (which one is the main roof).</summary>
        public const double WidthTieMm = 50;

        /// <summary>Smallest distance (mm) under which two parallel wall lines of a right-angled outline are taken as one.</summary>
        public const double SnapMinMm = 5;

        /// <summary>Levels of the edges along x (horizontal) or along y, closer than tol, replaced by their mean.</summary>
        private static void Snap(double[] lvl, bool[] alongX, bool horizontal, double tol)
        {
            var idx = Enumerable.Range(0, lvl.Length).Where(i => alongX[i] == horizontal).OrderBy(i => lvl[i]).ToList();
            int start = 0;
            for (int k = 1; k <= idx.Count; k++)
            {
                if (k < idx.Count && lvl[idx[k]] - lvl[idx[k - 1]] <= tol) continue;
                double mean = 0;
                for (int j = start; j < k; j++) mean += lvl[idx[j]];
                mean /= k - start;
                for (int j = start; j < k; j++) lvl[idx[j]] = mean;
                start = k;
            }
        }

        private static double[] Dir(double[] a, double[] b)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            return l < 1e-9 ? null : new[] { dx / l, dy / l };
        }

        /// <summary>Local rectangle (x0, y0, x1, y1) in the frame of the outline's first edge.</summary>
        public struct LRect
        {
            public double X0, Y0, X1, Y1;
            public double W => X1 - X0;
            public double H => Y1 - Y0;
            public bool Inside(LRect o, double tol = 1e-6) => X0 >= o.X0 - tol && Y0 >= o.Y0 - tol && X1 <= o.X1 + tol && Y1 <= o.Y1 + tol;
            public override string ToString() => $"[{X0:0.###},{Y0:0.###} – {X1:0.###},{Y1:0.###}]";
        }

        /// <summary>
        /// The maximal rectangles of a right-angled outline given in a frame where its edges are along x / y: every
        /// rectangle of the grid cells that lies inside and cannot grow in any direction. The largest square centred at
        /// any inside point fits in one of them, which is what makes the hip roof right at the inward corners.
        /// </summary>
        public static List<LRect> MaximalRectangles(IList<double[]> local, double tol = 0.5)
        {
            var xs = Unique(local.Select(q => q[0]), tol);
            var ys = Unique(local.Select(q => q[1]), tol);
            int nx = xs.Count - 1, ny = ys.Count - 1;
            var inside = new int[nx + 1, ny + 1]; // prefix sums of inside cells
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    bool c = Prism3.PointInPolygon(local, (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2);
                    inside[i + 1, j + 1] = (c ? 1 : 0) + inside[i, j + 1] + inside[i + 1, j] - inside[i, j];
                }
            bool Full(int i0, int j0, int i1, int j1) // cells i0..i1-1 × j0..j1-1
            {
                if (i0 < 0 || j0 < 0 || i1 > nx || j1 > ny) return false;
                int n = inside[i1, j1] - inside[i0, j1] - inside[i1, j0] + inside[i0, j0];
                return n == (i1 - i0) * (j1 - j0);
            }
            var res = new List<LRect>();
            for (int i0 = 0; i0 < nx; i0++)
                for (int i1 = i0 + 1; i1 <= nx; i1++)
                    for (int j0 = 0; j0 < ny; j0++)
                        for (int j1 = j0 + 1; j1 <= ny; j1++)
                        {
                            if (!Full(i0, j0, i1, j1)) break; // taller ones are not full either
                            if (Full(i0 - 1, j0, i1, j1) || Full(i0, j0, i1 + 1, j1) || Full(i0, j0 - 1, i1, j1) || Full(i0, j0, i1, j1 + 1)) continue;
                            res.Add(new LRect { X0 = xs[i0], Y0 = ys[j0], X1 = xs[i1], Y1 = ys[j1] });
                        }
            return res;
        }

        private static List<double> Unique(IEnumerable<double> v, double tol)
        {
            var res = new List<double>();
            foreach (var x in v.OrderBy(q => q))
                if (res.Count == 0 || x - res[res.Count - 1] > tol) res.Add(x);
            return res;
        }

        /// <summary>
        /// The pieces of a sloped roof over a right-angled eave outline (local frame). Hip: every maximal rectangle, all four
        /// edges sloped (the union is the hip roof of the outline, with valleys at the inward corners). Gable: the widest
        /// maximal rectangle is the main roof (ridge along its long side, or short with ridge "short"); every other one is
        /// cut along the main ridge line, the parts inside the main rectangle are dropped and the wings keep a ridge
        /// running away from the main ridge, so they end in the main roof (an L / T house with one main ridge).
        /// </summary>
        public static List<(LRect r, bool ridgeAlongX)> RightAngledPieces(List<LRect> rects, string type, string ridge)
        {
            var res = new List<(LRect, bool)>();
            if (type == "hip")
            {
                foreach (var r in rects) res.Add((r, r.W >= r.H));
                return res;
            }
            // KTF-01 (lần 2): widths within WidthTieMm (drawing error) are equal, then the larger area is the main roof, so a
            // corner off by a few mm does not swap the main roof and the wing
            double widest = rects.Max(r => Math.Min(r.W, r.H));
            var main = rects.Where(r => Math.Min(r.W, r.H) >= widest - WidthTieMm).OrderByDescending(r => r.W * r.H).First();
            bool mainAlongX = ridge == "short" ? main.W < main.H : main.W >= main.H;
            res.Add((main, mainAlongX));
            double line = mainAlongX ? (main.Y0 + main.Y1) / 2 : (main.X0 + main.X1) / 2;
            foreach (var r in rects)
            {
                if (r.Equals(main)) continue;
                double lo = mainAlongX ? r.Y0 : r.X0, hi = mainAlongX ? r.Y1 : r.X1;
                var parts = new List<LRect>();
                if (lo < line - 1e-6 && hi > line + 1e-6)
                {
                    if (mainAlongX)
                    {
                        parts.Add(new LRect { X0 = r.X0, Y0 = r.Y0, X1 = r.X1, Y1 = line });
                        parts.Add(new LRect { X0 = r.X0, Y0 = line, X1 = r.X1, Y1 = r.Y1 });
                    }
                    else
                    {
                        parts.Add(new LRect { X0 = r.X0, Y0 = r.Y0, X1 = line, Y1 = r.Y1 });
                        parts.Add(new LRect { X0 = line, Y0 = r.Y0, X1 = r.X1, Y1 = r.Y1 });
                    }
                    // a wing cut at the main ridge runs away from it: its ridge is across the main ridge
                    // KTF-01 (lần 2): a cut part takes its ridge along its own long side (as an uncut wing), never across
                    // its long side: a bar half taken as a "wing across the main ridge" got a ridge as high as the bar is long
                    foreach (var q in parts)
                        if (!q.Inside(main)) res.Add((q, ridge == "short" ? q.W < q.H : q.W >= q.H));
                }
                else if (!r.Inside(main))
                {
                    bool along = ridge == "short" ? r.W < r.H : r.W >= r.H;
                    res.Add((r, along));
                }
            }
            return EndInHigherRidges(res);
        }

        /// <summary>Half the width across the ridge of a gable piece: its ridge height / tan.</summary>
        private static double Inset((LRect r, bool alongX) q) => (q.alongX ? q.r.H : q.r.W) / 2;

        private static bool Overlaps(LRect a, LRect b, double tol = 1e-6) =>
            Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > tol && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) > tol;

        /// <summary>
        /// Test & sửa đợt F3: a gable wing that runs into ANY higher roof across its ridge (not only the main one: the bar
        /// of a U house runs into both arms) is cut at that roof's ridge line and the part inside it is dropped, so its
        /// gable end is hidden under the higher roof instead of sticking out of that roof's far slope. Higher = a wider
        /// piece (higher ridge); equal widths: the earlier piece (the main roof first).
        /// </summary>
        internal static List<(LRect r, bool ridgeAlongX)> EndInHigherRidges(List<(LRect r, bool ridgeAlongX)> pieces)
        {
            var res = pieces.ToList();
            for (int guard = 0; guard < 200; guard++)
            {
                bool changed = false;
                for (int i = 0; i < res.Count && !changed; i++)
                {
                    var q = res[i];
                    for (int j = 0; j < res.Count && !changed; j++)
                    {
                        if (i == j) continue;
                        var p = res[j];
                        if (p.ridgeAlongX == q.ridgeAlongX || !Overlaps(p.r, q.r)) continue;
                        double ip = Inset(p), iq = Inset(q);
                        bool higher = ip > iq + WidthTieMm / 2 || (Math.Abs(ip - iq) <= WidthTieMm / 2 && j < i);
                        if (!higher) continue;
                        // p's ridge line runs along p (x = const when p's ridge is along y); q runs across it
                        double line = p.ridgeAlongX ? (p.r.Y0 + p.r.Y1) / 2 : (p.r.X0 + p.r.X1) / 2;
                        double lo = p.ridgeAlongX ? q.r.Y0 : q.r.X0, hi = p.ridgeAlongX ? q.r.Y1 : q.r.X1;
                        var parts = new List<LRect>();
                        if (lo < line - 1e-6 && hi > line + 1e-6)
                        {
                            if (p.ridgeAlongX)
                            {
                                parts.Add(new LRect { X0 = q.r.X0, Y0 = q.r.Y0, X1 = q.r.X1, Y1 = line });
                                parts.Add(new LRect { X0 = q.r.X0, Y0 = line, X1 = q.r.X1, Y1 = q.r.Y1 });
                            }
                            else
                            {
                                parts.Add(new LRect { X0 = q.r.X0, Y0 = q.r.Y0, X1 = line, Y1 = q.r.Y1 });
                                parts.Add(new LRect { X0 = line, Y0 = q.r.Y0, X1 = q.r.X1, Y1 = q.r.Y1 });
                            }
                        }
                        else parts.Add(q.r);
                        var kept = parts.Where(x => !x.Inside(p.r)).ToList();
                        if (kept.Count == 1 && kept[0].Equals(q.r)) continue; // nothing to cut
                        res.RemoveAt(i);
                        res.InsertRange(i, kept.Select(x => (x, q.ridgeAlongX)));
                        changed = true;
                    }
                }
                if (!changed) break;
            }
            return res;
        }

        /// <summary>Fills lay.Pieces for a sloped roof over the right-angled eave outline (CCW, mm).</summary>
        internal static void BuildRightAngled(Layout lay, List<double[]> eave, Options o, double ze, double up, double tan, double diag, string tool)
        {
            var org = eave[0];
            var ux = Dir(eave[0], eave[1]);
            var uy = new[] { -ux[1], ux[0] };
            var local = eave.Select(q => new[] { (q[0] - org[0]) * ux[0] + (q[1] - org[1]) * ux[1], (q[0] - org[0]) * uy[0] + (q[1] - org[1]) * uy[1] }).ToList();
            double[] W(double x, double y) => new[] { org[0] + x * ux[0] + y * uy[0], org[1] + x * ux[1] + y * uy[1] };
            var rects = MaximalRectangles(local);
            if (rects.Count == 0) throw ToolException.Invalid($"{tool}: the outline has no area. Nothing was built.");
            var pieces = RightAngledPieces(rects, o.Type, o.Ridge);
            if (pieces.Count > MaxRoofPieces)
                throw ToolException.Invalid($"{tool}: this right-angled outline needs {pieces.Count} roof pieces (at most {MaxRoofPieces}). Nothing was built. Tell the user; options: type flat, or build the roof in parts.");
            double zLow = ze - up - 1000;
            double top = ze;
            foreach (var (r, alongX) in pieces)
            {
                var rect = new List<double[]> { W(r.X0, r.Y0), W(r.X1, r.Y0), W(r.X1, r.Y1), W(r.X0, r.Y1) }; // CCW
                var k = new RoofPiece { Rect = rect };
                // edges 0 and 2 run along x, 1 and 3 along y
                if (o.Type == "hip") k.Sloped.AddRange(new[] { 0, 1, 2, 3 });
                else k.Sloped.AddRange(alongX ? new[] { 0, 2 } : new[] { 1, 3 });
                double inset = o.Type == "hip" ? Math.Min(r.W, r.H) / 2 : (alongX ? r.H : r.W) / 2;
                k.RidgeZ = ze + inset * tan + up;
                top = Math.Max(top, k.RidgeZ);
                k.Top = Prism3.Vertical(rect, ze, inset * tan + up + 10, "roof piece");
                k.Foot = Prism3.Vertical(rect, zLow - 2000, inset * tan + up + 4000 + (ze - zLow), "roof piece underside");
                foreach (var i in k.Sloped)
                {
                    var a = rect[i]; var b = rect[(i + 1) % 4];
                    k.TopPlanes.Add(Wedge(a, b, ze + up, tan, diag + 10, diag + 10, zLow, $"top plane {i}"));
                    k.UnderPlanes.Add(Wedge(a, b, ze, tan, diag + 10, diag + 10, zLow - 1000, $"underside {i}"));
                }
                lay.Pieces.Add(k);
            }
            lay.EaveZ = ze;
            lay.RidgeZ = lay.TopZ = top;
            lay.RidgeLength = 0; // several ridges: not one length
            lay.Notes.Add($"right-angled outline: one roof solid from {pieces.Count} pieces (valleys where the wings meet)");
            if (o.Type == "gable") lay.Notes.Add("the triangular gable ends are open: build the gable walls with extrude / presspull if needed");
        }
    }
}
