using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt A (13/11/2026), AutoCAD-free part of build_railing: the boxes of a railing (lan can) along a path of
    /// straight segments: a top rail, posts at every corner and at most post_spacing apart, and the infill between the
    /// posts (vertical bars with a clear gap of at most 100 mm, a panel, or nothing). All sizes in mm here; the tool
    /// scales them by INSUNITS. The defaults are written in docs/ban-giao/nang-cap-a.md and the 3D kiến trúc skill.
    /// </summary>
    public static class RailingLogic
    {
        public const double DefaultHeight = 1100;      // mm, top of the rail above the floor
        public const double MinHeight = 300, MaxHeight = 3000;
        public const double RailSize = 50;             // top / bottom rail section 50 × 50
        public const double PostSize = 50;             // posts 50 × 50
        public const double DefaultPostSpacing = 1500; // posts at most 1.5 m apart
        public const double MinPostSpacing = 200, MaxPostSpacing = 6000;
        public const double BarSize = 20;              // bars 20 × 20
        public const double MaxBarGap = 100;           // clear gap between bars (child safety)
        public const double BottomGap = 100;           // bottom rail / panel starts 100 mm above the floor
        public const double PanelThickness = 20;
        public const int MaxPoints = 2000;
        public const int MaxBoxes = 5000;
        public const string Layer3d = "A-RAIL-3D";
        public static readonly string[] Infills = { "bars", "panel", "none" };

        public sealed class Options
        {
            public double Height = DefaultHeight, PostSpacing = DefaultPostSpacing;
            public string Infill = "bars";
        }

        public sealed class Layout
        {
            public List<PlanBox> Boxes = new List<PlanBox>();
            public int Posts, Bars, Panels;
            public double Length; // mm along the path
        }

        public static string NormalizeInfill(string infill, string tool = "build_railing")
        {
            var s = (infill ?? "bars").Trim().ToLowerInvariant();
            if (s == "song" || s == "bar") s = "bars";
            if (s == "kinh" || s == "kính" || s == "glass") s = "panel";
            if (Array.IndexOf(Infills, s) < 0) throw ToolException.Invalid($"{tool}: infill must be one of {string.Join(", ", Infills)} (got '{infill}').");
            return s;
        }

        public static void Check(Options o, string tool = "build_railing")
        {
            if (!(o.Height >= MinHeight && o.Height <= MaxHeight))
                throw ToolException.Invalid($"{tool}: height must be {MinHeight}–{MaxHeight} mm (got {o.Height}; default {DefaultHeight}).");
            if (!(o.PostSpacing >= MinPostSpacing && o.PostSpacing <= MaxPostSpacing))
                throw ToolException.Invalid($"{tool}: post_spacing must be {MinPostSpacing}–{MaxPostSpacing} mm (got {o.PostSpacing}).");
            o.Infill = NormalizeInfill(o.Infill, tool);
        }

        private static PlanBox Box(double[] a, double[] u, double s, double len, double width, double z0, double z1, double rotDeg) => new PlanBox
        {
            Center = new[] { a[0] + u[0] * s, a[1] + u[1] * s, (z0 + z1) / 2 },
            Length = len, Width = width, Height = z1 - z0, RotationDeg = rotDeg,
        };

        /// <summary>
        /// The boxes (mm, z from the floor = 0) along the points (xy, mm); closed adds the segment back to the start.
        /// Repeated points are dropped; a path with no segment longer than a post is refused.
        /// </summary>
        public static Layout Build(IList<double[]> points, bool closed, Options o, string tool = "build_railing")
        {
            Check(o, tool);
            var pts = new List<double[]>();
            foreach (var p in points)
                if (pts.Count == 0 || Math.Abs(p[0] - pts[pts.Count - 1][0]) + Math.Abs(p[1] - pts[pts.Count - 1][1]) > 1e-6) pts.Add(new[] { p[0], p[1] });
            if (closed && pts.Count > 2 && Math.Abs(pts[0][0] - pts[pts.Count - 1][0]) + Math.Abs(pts[0][1] - pts[pts.Count - 1][1]) <= 1e-6) pts.RemoveAt(pts.Count - 1);
            if (pts.Count > MaxPoints) throw ToolException.Invalid($"{tool}: at most {MaxPoints} path points.");
            int segs = closed && pts.Count > 2 ? pts.Count : pts.Count - 1;
            if (segs < 1) throw ToolException.Invalid($"{tool}: the path needs at least 2 different points.");
            var res = new Layout();
            double railBottom = o.Height - RailSize;
            var postKeys = new HashSet<string>();
            void Post(double[] at, double rot)
            {
                var key = Math.Round(at[0], 3) + "," + Math.Round(at[1], 3);
                if (!postKeys.Add(key)) return;
                res.Boxes.Add(Box(at, new[] { 1.0, 0 }, 0, PostSize, PostSize, 0, railBottom, rot));
                res.Posts++;
            }
            for (int i = 0; i < segs; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1], len = Math.Sqrt(dx * dx + dy * dy);
                if (len < PostSize) continue; // a tiny jog: the posts and rails around it cover it
                var u = new[] { dx / len, dy / len };
                double rot = Math.Atan2(dy, dx) * 180 / Math.PI;
                res.Length += len;
                // top rail, a rail-size longer so the corners close
                res.Boxes.Add(Box(a, u, len / 2, len + RailSize, RailSize, railBottom, o.Height, rot));
                int n = Math.Max(1, (int)Math.Ceiling(len / o.PostSpacing - 1e-9));
                double step = len / n;
                for (int k = 0; k <= n; k++) Post(new[] { a[0] + u[0] * step * k, a[1] + u[1] * step * k }, rot);
                if (o.Infill == "none") continue;
                for (int k = 0; k < n; k++)
                {
                    double s0 = step * k + PostSize / 2, s1 = step * (k + 1) - PostSize / 2, clear = s1 - s0;
                    if (clear <= 1e-6) continue;
                    if (o.Infill == "panel")
                    {
                        res.Boxes.Add(Box(a, u, (s0 + s1) / 2, clear, PanelThickness, BottomGap, railBottom, rot));
                        res.Panels++;
                        continue;
                    }
                    // bottom rail between the posts, then bars with clear gaps ≤ MaxBarGap
                    res.Boxes.Add(Box(a, u, (s0 + s1) / 2, clear, RailSize, BottomGap, BottomGap + RailSize, rot));
                    int m = BarCount(clear);
                    double gap = (clear - m * BarSize) / (m + 1);
                    for (int j = 0; j < m; j++)
                    {
                        double c = s0 + gap * (j + 1) + BarSize * (j + 0.5);
                        res.Boxes.Add(Box(a, u, c, BarSize, BarSize, BottomGap + RailSize, railBottom, rot));
                        res.Bars++;
                    }
                }
            }
            if (res.Length <= 0) throw ToolException.Invalid($"{tool}: every path segment is shorter than a post ({PostSize} mm); nothing was built.");
            if (res.Boxes.Count > MaxBoxes)
                throw new ToolException(ErrorCodes.TooManyEntities, $"{tool}: {res.Boxes.Count} railing pieces (more than {MaxBoxes}). Nothing was built. Build a shorter path, or use infill \"panel\".");
            return res;
        }

        /// <summary>The fewest bars that keep every clear gap of a span at most MaxBarGap.</summary>
        public static int BarCount(double clear) =>
            clear <= MaxBarGap ? 0 : Math.Max(0, (int)Math.Ceiling((clear - MaxBarGap) / (BarSize + MaxBarGap) - 1e-9));
    }
}
