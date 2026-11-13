using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt E (13/11/2026): a flat profile swept straight: the profile (u, v) lies in the plane through Origin
    /// spanned by U and V and is extruded along Ext by Depth. All in mm. The plugin builds it as a region in XY extruded
    /// along +Z, then aligned (Normalized gives a right-handed frame, so no mirror transform is needed).
    /// </summary>
    public sealed class Prism3
    {
        public List<double[]> Profile = new List<double[]>();
        public double[] Origin = { 0, 0, 0 };
        public double[] U = { 1, 0, 0 }, V = { 0, 1, 0 }, Ext = { 0, 0, 1 };
        public double Depth;
        public string Part = "";

        public static double Area2d(IList<double[]> pts)
        {
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i]; var q = pts[(i + 1) % pts.Count];
                a += p[0] * q[1] - q[0] * p[1];
            }
            return a / 2;
        }

        public double Volume => Math.Abs(Area2d(Profile)) * Depth;

        internal static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        internal static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        /// <summary>
        /// The same prism as origin + profile in (U, V) extruded along W = U × V (right-handed): when Ext points the other
        /// way the origin moves to the far side.
        /// </summary>
        public (double[] origin, double[] w) Normalized()
        {
            var w = Cross(U, V);
            if (Dot(w, Ext) >= 0) return (Origin, w);
            return (new[] { Origin[0] + Ext[0] * Depth, Origin[1] + Ext[1] * Depth, Origin[2] + Ext[2] * Depth }, w);
        }

        /// <summary>True when the point (mm) lies inside (for tests; U, V, Ext orthonormal).</summary>
        public bool Contains(double[] p, double tol = 1e-6)
        {
            var d = new[] { p[0] - Origin[0], p[1] - Origin[1], p[2] - Origin[2] };
            double u = Dot(d, U), v = Dot(d, V), e = Dot(d, Ext);
            if (e < -tol || e > Depth + tol) return false;
            return PointInPolygon(Profile, u, v);
        }

        public static bool PointInPolygon(IList<double[]> poly, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
            }
            return inside;
        }

        /// <summary>A vertical prism: the plan outline (x, y, mm) from z0 up by height.</summary>
        public static Prism3 Vertical(IEnumerable<double[]> outline, double z0, double height, string part) => new Prism3
        {
            Profile = outline.Select(q => new[] { q[0], q[1] }).ToList(),
            Origin = new[] { 0.0, 0, z0 },
            U = new[] { 1.0, 0, 0 }, V = new[] { 0.0, 1, 0 }, Ext = new[] { 0.0, 0, 1 },
            Depth = height, Part = part,
        };

        /// <summary>A plan rectangle (x0..x1, y0..y1) from z0 to z1.</summary>
        public static Prism3 Box(double x0, double y0, double x1, double y1, double z0, double z1, string part) =>
            Vertical(new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } }, z0, z1 - z0, part);
    }

    /// <summary>
    /// Nâng cấp đợt E (13/11/2026), AutoCAD-free part of build_stair: straight (1 vế), L and U stairs (chữ L, chữ U) as
    /// prisms in mm. Each flight is one prism (the stepped profile with its sloping waist slab underneath) extruded across
    /// the flight width; landings are boxes; the optional railing (handrail + posts along the flights) is separate.
    /// The defaults were agreed by the project owner on 13/11/2026 (docs/ke-hoach-nang-cap-lenh.md, point 6).
    /// </summary>
    public static class StairLogic
    {
        public const double DefaultRiser = 160, DefaultTread = 280, DefaultWidth = 1000;
        /// <summary>Waist (bản thang) and landing slab thickness; the gap between the two flights of a U stair.</summary>
        public const double DefaultWaist = 150, DefaultGap = 100;
        public const double MinRiser = 100, MaxRiser = 250, MinTread = 150, MaxTread = 500;
        public const double MinWidth = 500, MaxWidth = 5000, MinHeight = 200, MaxHeight = 20000;
        public const double MinWaist = 50, MaxWaist = 500, MaxGap = 5000;
        /// <summary>The top step reaches the upper floor: the stair runs this far onto it (its last riser is built).</summary>
        public const double TopLedge = 100;
        /// <summary>Flights reach this far into a landing so the pieces overlap and unite.</summary>
        public const double LandingOverlap = 100;
        public const int MaxRisersPerFlightHint = 18;
        public const double RailHeight = 900, RailSize = 50, PostSize = 50, MaxPostSpacing = 1500;
        public const string Layer3d = "A-STAIR-3D";
        /// <summary>A corner counts as square when the cosine of its angle is below this (about 0.1°: hand-drawn outlines).</summary>
        public const double SquareTol = 2e-3;
        public static readonly string[] Types = { "straight", "l", "u" };
        public static readonly string[] Railings = { "none", "left", "right", "both" };

        public sealed class Options
        {
            public string Type = "straight";
            public double Height;                 // floor to floor, mm
            public double Riser = DefaultRiser, Tread = DefaultTread, Width = DefaultWidth, Waist = DefaultWaist, Gap = DefaultGap;
            public double? Landing;               // landing depth (default = width)
            public int? FirstRisers;              // risers of the first flight (L / U)
            public string Turn = "left";          // L / U: the side the stair turns to, walking up
            public string Railing = "none";
        }

        public sealed class Flight
        {
            public int Risers;
            public double Run;      // mm on plan, top ledge included
            public double BaseZ;    // level it starts from
        }

        /// <summary>A plan rectangle of the stair. OnUpperFloor: the top step's ledge resting on the upper floor slab
        /// (past the last riser, outside the stair well): not counted when the stair is fitted into a well.</summary>
        public sealed class Rect { public double X0, Y0, X1, Y1; public string Part; public bool OnUpperFloor; }

        /// <summary>
        /// The stair in its own frame (mm): x = walking direction of the first flight, y = the turn side (left for a
        /// straight stair), z up from the lower floor. The first flight occupies x ≥ 0, 0 ≤ y ≤ width.
        /// </summary>
        public sealed class Layout
        {
            public string Type;
            public int Risers;
            public double RiserHeight;
            public List<Flight> Flights = new List<Flight>();
            public List<Prism3> Pieces = new List<Prism3>();
            public List<Prism3> Rail = new List<Prism3>();
            public List<Rect> Footprint = new List<Rect>();
            public List<string> Warnings = new List<string>();
            /// <summary>The plan box of the stair in its well (the top ledge on the upper floor not included).</summary>
            public double MinX, MaxX, MinY, MaxY;
            public double Length => MaxX - MinX;
            public double Breadth => MaxY - MinY;
            public double Volume => Pieces.Sum(p => p.Volume);
        }

        public static string NormalizeType(string type, string tool = "build_stair")
        {
            var s = (type ?? "straight").Trim().ToLowerInvariant().Replace("-", " ").Replace("_", " ");
            switch (s)
            {
                case "straight": case "single": case "1 vế": case "1 ve": case "mot ve": case "một vế": case "thang": case "thẳng": case "thang thẳng": case "thang thang": case "i":
                    return "straight";
                case "l": case "l shape": case "chữ l": case "chu l": case "l shaped":
                    return "l";
                case "u": case "u shape": case "chữ u": case "chu u": case "u shaped": case "dog leg": case "dogleg":
                    return "u";
            }
            throw ToolException.Invalid($"{tool}: type must be one of straight (1 vế), l (chữ L), u (chữ U) (got '{type}').");
        }

        public static string NormalizeRailing(string r, string tool = "build_stair")
        {
            var s = (r ?? "none").Trim().ToLowerInvariant();
            if (s == "trai" || s == "trái") s = "left";
            if (s == "phai" || s == "phải") s = "right";
            if (s == "hai ben" || s == "hai bên" || s == "2") s = "both";
            if (s == "khong" || s == "không" || s == "false") s = "none";
            if (s == "true") s = "both";
            if (Array.IndexOf(Railings, s) < 0) throw ToolException.Invalid($"{tool}: railing must be one of {string.Join(", ", Railings)} (got '{r}').");
            return s;
        }

        public static string NormalizeTurn(string t, string tool = "build_stair")
        {
            var s = (t ?? "left").Trim().ToLowerInvariant();
            if (s == "trai" || s == "trái" || s == "l") return "left";
            if (s == "phai" || s == "phải" || s == "r") return "right";
            if (s == "left" || s == "right") return s;
            throw ToolException.Invalid($"{tool}: turn must be left or right (got '{t}').");
        }

        private static void Range(double v, double min, double max, string name, string tool, double def = double.NaN)
        {
            if (!(v >= min && v <= max))
                throw ToolException.Invalid($"{tool}: {name} must be {min}–{max} mm (got {v}{(double.IsNaN(def) ? "" : $"; default {def}")}).");
        }

        public static void Check(Options o, string tool = "build_stair")
        {
            o.Type = NormalizeType(o.Type, tool);
            o.Turn = NormalizeTurn(o.Turn, tool);
            o.Railing = NormalizeRailing(o.Railing, tool);
            if (!(o.Height > 0))
                throw ToolException.Invalid($"{tool}: pass 'height' = floor-to-floor height in mm (e.g. the storey height 3300). Nothing was built.");
            Range(o.Height, MinHeight, MaxHeight, "height", tool);
            Range(o.Riser, MinRiser, MaxRiser, "riser", tool, DefaultRiser);
            Range(o.Tread, MinTread, MaxTread, "tread", tool, DefaultTread);
            Range(o.Width, MinWidth, MaxWidth, "width", tool, DefaultWidth);
            Range(o.Waist, MinWaist, MaxWaist, "waist", tool, DefaultWaist);
            if (!(o.Gap >= 0 && o.Gap <= MaxGap)) throw ToolException.Invalid($"{tool}: gap must be 0–{MaxGap} mm (got {o.Gap}).");
            if (o.Landing.HasValue) Range(o.Landing.Value, Math.Min(o.Width, 600), 10000, "landing", tool);
        }

        /// <summary>The whole number of risers whose height is nearest the wanted riser (at least 2).</summary>
        public static int RiserCount(double height, double riser) => Math.Max(2, (int)Math.Round(height / riser, MidpointRounding.AwayFromZero));

        /// <summary>
        /// The side profile (u along the walking direction, v up from the level the flight starts at) of a flight with
        /// m treads (m + 1 risers, the last one reaching the next level). Under the steps a waist slab of thickness w
        /// (perpendicular to the slope), cut flat at the bottom level and at the landing / upper floor slab bottom.
        /// fromLanding: the flight starts at a landing edge (reaching back ovStart into it); ovEnd: how far the flight
        /// runs past its last riser (into the landing, or onto the upper floor).
        /// </summary>
        public static List<double[]> FlightProfile(int m, double r, double t, double w, double lt, bool fromLanding, double ovStart, double ovEnd)
        {
            double len = Math.Sqrt(r * r + t * t), tan = r / t, cos = t / len;
            double Zu(double u) => u * tan - w / cos;
            double baseB = fromLanding ? -lt : 0, uStart = fromLanding ? -ovStart : 0;
            double top = (m + 1) * r, topB = top - lt, uEnd = m * t + ovEnd;
            double ub = (baseB + w / cos) / tan, ut = (topB + w / cos) / tan;
            var pts = new List<double[]> { new[] { uStart, baseB } };
            if (ub >= uEnd) pts.Add(new[] { uEnd, baseB });
            else
            {
                if (ub > uStart) pts.Add(new[] { ub, baseB });
                if (ut < uEnd) { pts.Add(new[] { ut, topB }); pts.Add(new[] { uEnd, topB }); }
                else pts.Add(new[] { uEnd, Zu(uEnd) });
            }
            pts.Add(new[] { uEnd, top });
            if (ovEnd > 0) pts.Add(new[] { m * t, top });
            for (int k = m; k >= 1; k--)
            {
                pts.Add(new[] { k * t, k * r });
                pts.Add(new[] { (k - 1) * t, k * r });
            }
            // now at (0, r): the first riser down to the start level
            pts.Add(new[] { 0.0, 0 });
            if (fromLanding) pts.Add(new[] { uStart, 0.0 });
            // drop repeated points
            var clean = new List<double[]>();
            foreach (var p in pts)
                if (clean.Count == 0 || Math.Abs(p[0] - clean[clean.Count - 1][0]) + Math.Abs(p[1] - clean[clean.Count - 1][1]) > 1e-9) clean.Add(p);
            if (clean.Count > 1 && Math.Abs(clean[0][0] - clean[clean.Count - 1][0]) + Math.Abs(clean[0][1] - clean[clean.Count - 1][1]) <= 1e-9) clean.RemoveAt(clean.Count - 1);
            return clean;
        }

        /// <summary>A flight prism in the stair frame: walking along dir (unit, plan) from start (plan point on the side edge
        /// at u = 0), across along side (unit, plan) by width, levels from baseZ.</summary>
        private static Prism3 FlightPrism(List<double[]> profile, double[] start, double[] dir, double[] side, double width, double baseZ, string part) => new Prism3
        {
            Profile = profile,
            Origin = new[] { start[0], start[1], baseZ },
            U = new[] { dir[0], dir[1], 0 }, V = new[] { 0.0, 0, 1 }, Ext = new[] { side[0], side[1], 0 },
            Depth = width, Part = part,
        };

        public static Layout Build(Options o, string tool = "build_stair")
        {
            Check(o, tool);
            var lay = new Layout { Type = o.Type };
            int n = RiserCount(o.Height, o.Riser);
            double r = o.Height / n, t = o.Tread, w = o.Width, lt = o.Waist;
            lay.Risers = n;
            lay.RiserHeight = r;
            if (!(r >= MinRiser && r <= MaxRiser))
                throw ToolException.Invalid($"{tool}: the height {o.Height} mm gives {n} risers of {Math.Round(r)} mm, outside {MinRiser}–{MaxRiser} mm. Nothing was built.");
            double blondel = 2 * r + t;
            if (blondel < 550 || blondel > 700)
                lay.Warnings.Add($"2 × riser + tread = {Math.Round(blondel)} mm (comfortable stairs: 600–650 mm).");
            double ov = Math.Min(LandingOverlap, lt);
            var X = new[] { 1.0, 0 }; var Y = new[] { 0.0, 1 }; var nX = new[] { -1.0, 0 };
            if (o.Type == "straight")
            {
                if (o.FirstRisers.HasValue) throw ToolException.Invalid($"{tool}: first_flight_risers is for L / U stairs only.");
                int m = n - 1;
                var prof = FlightProfile(m, r, t, o.Waist, lt, false, 0, TopLedge);
                lay.Pieces.Add(FlightPrism(prof, new[] { 0.0, 0 }, X, Y, w, 0, "flight 1"));
                lay.Flights.Add(new Flight { Risers = n, Run = m * t + TopLedge, BaseZ = 0 });
                lay.Footprint.Add(new Rect { X0 = 0, Y0 = 0, X1 = m * t, Y1 = w, Part = "flight 1" });
                lay.Footprint.Add(new Rect { X0 = m * t, Y0 = 0, X1 = m * t + TopLedge, Y1 = w, Part = "top ledge", OnUpperFloor = true });
                if (n > MaxRisersPerFlightHint)
                    lay.Warnings.Add($"One flight of {n} risers (more than {MaxRisersPerFlightHint} in a row is tiring and many codes ask for a landing): consider an L or U stair.");
                AddRail(lay, o, 0, m, r, t, new[] { 0.0, 0 }, X, Y, w, 0);
            }
            else
            {
                if (n < 4) throw ToolException.Invalid($"{tool}: an {(o.Type == "l" ? "L" : "U")} stair needs at least 4 risers (the height gives {n}). Use type straight. Nothing was built.");
                int n1 = o.FirstRisers ?? (n + 1) / 2;
                if (n1 < 2 || n1 > n - 2) throw ToolException.Invalid($"{tool}: first_flight_risers must be 2–{n - 2} (the stair has {n} risers).");
                int n2 = n - n1, m1 = n1 - 1, m2 = n2 - 1;
                double L1 = n1 * r, R1 = m1 * t, R2 = m2 * t + TopLedge;
                double ld = o.Landing ?? w;
                var prof1 = FlightProfile(m1, r, t, o.Waist, lt, false, 0, ov);
                lay.Pieces.Add(FlightPrism(prof1, new[] { 0.0, 0 }, X, Y, w, 0, "flight 1"));
                lay.Flights.Add(new Flight { Risers = n1, Run = R1, BaseZ = 0 });
                lay.Footprint.Add(new Rect { X0 = 0, Y0 = 0, X1 = R1, Y1 = w, Part = "flight 1" });
                var prof2 = FlightProfile(m2, r, t, o.Waist, lt, true, ov, TopLedge);
                if (o.Type == "l")
                {
                    if (ld < w) throw ToolException.Invalid($"{tool}: the landing of an L stair must be at least the flight width ({w} mm).");
                    lay.Pieces.Add(Prism3.Box(R1, 0, R1 + ld, w, L1 - lt, L1, "landing"));
                    lay.Footprint.Add(new Rect { X0 = R1, Y0 = 0, X1 = R1 + ld, Y1 = w, Part = "landing" });
                    // second flight walks +y from the landing's far side, its side edge at x = R1 + ld - w
                    lay.Pieces.Add(FlightPrism(prof2, new[] { R1 + ld - w, w }, Y, X, w, L1, "flight 2"));
                    lay.Footprint.Add(new Rect { X0 = R1 + ld - w, Y0 = w, X1 = R1 + ld, Y1 = w + m2 * t, Part = "flight 2" });
                    lay.Footprint.Add(new Rect { X0 = R1 + ld - w, Y0 = w + m2 * t, X1 = R1 + ld, Y1 = w + R2, Part = "top ledge", OnUpperFloor = true });
                    AddRail(lay, o, 0, m1, r, t, new[] { 0.0, 0 }, X, Y, w, 0);
                    AddRail(lay, o, 1, m2, r, t, new[] { R1 + ld - w, w }, Y, X, w, L1);
                }
                else
                {
                    double span = 2 * w + o.Gap;
                    lay.Pieces.Add(Prism3.Box(R1, 0, R1 + ld, span, L1 - lt, L1, "landing"));
                    lay.Footprint.Add(new Rect { X0 = R1, Y0 = 0, X1 = R1 + ld, Y1 = span, Part = "landing" });
                    // second flight walks -x from x = R1, beside the first one past the gap
                    lay.Pieces.Add(FlightPrism(prof2, new[] { R1, w + o.Gap }, nX, Y, w, L1, "flight 2"));
                    lay.Footprint.Add(new Rect { X0 = R1 - m2 * t, Y0 = w + o.Gap, X1 = R1, Y1 = span, Part = "flight 2" });
                    lay.Footprint.Add(new Rect { X0 = R1 - R2, Y0 = w + o.Gap, X1 = R1 - m2 * t, Y1 = span, Part = "top ledge", OnUpperFloor = true });
                    AddRail(lay, o, 0, m1, r, t, new[] { 0.0, 0 }, X, Y, w, 0);
                    AddRail(lay, o, 1, m2, r, t, new[] { R1, w + o.Gap }, nX, Y, w, L1);
                }
                lay.Flights.Add(new Flight { Risers = n2, Run = R2, BaseZ = L1 });
                if (n1 > MaxRisersPerFlightHint || n2 > MaxRisersPerFlightHint)
                    lay.Warnings.Add($"A flight has more than {MaxRisersPerFlightHint} risers ({n1} + {n2}).");
            }
            // the plan size in the stair well: the top ledge rests on the upper floor, past the well edge
            var inWell = lay.Footprint.Where(q => !q.OnUpperFloor).ToList();
            lay.MinX = inWell.Min(q => q.X0); lay.MaxX = inWell.Max(q => q.X1);
            lay.MinY = inWell.Min(q => q.Y0); lay.MaxY = inWell.Max(q => q.Y1);
            return lay;
        }

        /// <summary>
        /// The railing of one flight (index fi): a handrail RailHeight above the nosing line and posts on the treads at
        /// most MaxPostSpacing apart, on the left / right side as you walk up. side = the across direction of the prism
        /// (from the start edge); the flight lies between start and start + side × width.
        /// </summary>
        private static void AddRail(Layout lay, Options o, int fi, int m, double r, double t, double[] start, double[] dir, double[] side, double width, double baseZ)
        {
            if (o.Railing == "none" || m < 1) return;
            // left of the walking direction, in the stair frame (a right turn is mirrored when placed: see Place)
            var left = new[] { -dir[1], dir[0] };
            bool sideIsLeft = left[0] * side[0] + left[1] * side[1] > 0;
            if (o.Type != "straight" && o.Turn == "right") sideIsLeft = !sideIsLeft;
            var edges = new List<double>(); // across offsets of the rail (start of a RailSize strip)
            bool wantLeft = o.Railing == "left" || o.Railing == "both", wantRight = o.Railing == "right" || o.Railing == "both";
            // the edge at offset 0 is on the left when !sideIsLeft (side points right)
            if (wantLeft) edges.Add(sideIsLeft ? width - RailSize : 0);
            if (wantRight) edges.Add(sideIsLeft ? 0 : width - RailSize);
            double tan = r / t, len = Math.Sqrt(r * r + t * t), cos = t / len, s = RailSize / cos;
            double Nose(double u) => r + u * tan; // nosing line: (0, r), (t, 2r), ...
            double uEnd = m * t;
            var rail = new List<double[]>
            {
                new[] { 0.0, Nose(0) + RailHeight - s }, new[] { uEnd, Nose(uEnd) + RailHeight - s },
                new[] { uEnd, Nose(uEnd) + RailHeight }, new[] { 0.0, Nose(0) + RailHeight },
            };
            // posts on tread centres: first, last and every MaxPostSpacing between
            var treads = new List<int>();
            int every = Math.Max(1, (int)Math.Floor(MaxPostSpacing / t));
            for (int k = 1; k <= m; k += every) treads.Add(k);
            if (treads[treads.Count - 1] != m) treads.Add(m);
            foreach (var e in edges)
            {
                var o0 = new[] { start[0] + side[0] * e, start[1] + side[1] * e };
                lay.Rail.Add(FlightPrism(rail, o0, dir, side, RailSize, baseZ, $"handrail {fi + 1}"));
                foreach (var k in treads)
                {
                    double u = (k - 0.5) * t, zTop = Nose(u) + RailHeight - s;
                    double cx = o0[0] + dir[0] * u + side[0] * RailSize / 2, cy = o0[1] + dir[1] * u + side[1] * RailSize / 2;
                    double hx = PostSize / 2;
                    lay.Rail.Add(Prism3.Box(cx - hx, cy - hx, cx + hx, cy + hx, baseZ + k * r, baseZ + zTop + s / 2, $"post {fi + 1}"));
                }
            }
        }

        /// <summary>The stair frame in plan: origin (mm), x axis (unit) and the turn-side axis.</summary>
        public sealed class Frame
        {
            public double[] Origin, X, Y;
            public double BaseZ;
            public double[] Map(double x, double y) => new[] { Origin[0] + X[0] * x + Y[0] * y, Origin[1] + X[1] * x + Y[1] * y };
        }

        /// <summary>
        /// The frame from the start point (centre of the bottom edge of the first step, mm), the walking direction
        /// (degrees in plan) and the turn side: y points to the left of the walking direction, or to the right for a
        /// right turn (L / U).
        /// </summary>
        public static Frame FrameFromStart(double[] start, double dirDeg, Options o)
        {
            double a = dirDeg * Math.PI / 180;
            var x = new[] { Math.Cos(a), Math.Sin(a) };
            var y = new[] { -x[1], x[0] };
            if (o.Type != "straight" && o.Turn == "right") y = new[] { -y[0], -y[1] };
            var org = new[] { start[0] - y[0] * o.Width / 2, start[1] - y[1] * o.Width / 2 };
            return new Frame { Origin = org, X = x, Y = y };
        }

        /// <summary>The prisms of the stair placed in plan (mm) with the frame; z moved up by baseZ.</summary>
        public static List<Prism3> Place(IEnumerable<Prism3> pieces, Frame f, double baseZ)
        {
            var res = new List<Prism3>();
            double[] Vec(double[] v) => new[] { f.X[0] * v[0] + f.Y[0] * v[1], f.X[1] * v[0] + f.Y[1] * v[1], v[2] };
            foreach (var p in pieces)
            {
                var o = f.Map(p.Origin[0], p.Origin[1]);
                var q = new Prism3
                {
                    Profile = p.Profile.Select(v => new[] { v[0], v[1] }).ToList(),
                    Origin = new[] { o[0], o[1], p.Origin[2] + baseZ },
                    Ext = Vec(p.Ext), Depth = p.Depth, Part = p.Part,
                };
                if (Math.Abs(p.Ext[2]) > 0.5)
                {
                    // a plan outline at origin (0, 0, z): map its points (the frame may be mirrored: the area only flips sign)
                    q.Profile = p.Profile.Select(v => f.Map(v[0], v[1])).ToList();
                    q.Origin = new[] { 0.0, 0, p.Origin[2] + baseZ };
                    q.U = new[] { 1.0, 0, 0 }; q.V = new[] { 0.0, 1, 0 }; q.Ext = new[] { 0.0, 0, 1 };
                }
                else
                {
                    q.U = Vec(p.U); q.V = Vec(p.V);
                }
                res.Add(q);
            }
            return res;
        }

        /// <summary>The plan corners (mm) of the footprint rectangles, placed with the frame.</summary>
        public static List<(string part, double[][] corners)> PlacedFootprint(Layout lay, Frame f, bool inWellOnly = false) =>
            lay.Footprint.Where(r => !inWellOnly || !r.OnUpperFloor).Select(r => (r.Part, new[] { f.Map(r.X0, r.Y0), f.Map(r.X1, r.Y0), f.Map(r.X1, r.Y1), f.Map(r.X0, r.Y1) })).ToList();

        /// <summary>The parts of the stair that stick out of the well outline (mm), empty when it fits. The top ledge resting
        /// on the upper floor may pass the well edge.</summary>
        public static List<string> Outside(Layout lay, Frame f, IList<double[]> well, double tol = 1)
        {
            var bad = new List<string>();
            foreach (var (part, cs) in PlacedFootprint(lay, f, true))
            {
                var probe = new List<double[]>();
                for (int i = 0; i < 4; i++)
                {
                    var a = cs[i]; var b = cs[(i + 1) % 4];
                    for (int k = 0; k < 4; k++) probe.Add(new[] { a[0] + (b[0] - a[0]) * k / 4, a[1] + (b[1] - a[1]) * k / 4 });
                }
                if (probe.Any(p => !InsideOrOn(well, p, tol))) bad.Add(part);
            }
            return bad;
        }

        private static bool InsideOrOn(IList<double[]> poly, double[] p, double tol)
        {
            if (Prism3.PointInPolygon(poly, p[0], p[1])) return true;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy;
                double k = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2));
                double ex = a[0] + dx * k - p[0], ey = a[1] + dy * k - p[1];
                if (ex * ex + ey * ey <= tol * tol) return true;
            }
            return false;
        }

        /// <summary>
        /// Fits the stair into a well outline (mm) with no start point given: tries each corner with the stair running
        /// along either side from it (longer sides first), keeping the turn the user asked for and, when given, the walking
        /// direction of the first flight (degrees in plan, within 1°). Null when nothing fits.
        /// </summary>
        public static Frame FitInWell(Layout lay, IList<double[]> well, Options o, bool turnGiven, double? dirDeg = null)
        {
            var pts = RoofLogic.Clean(well);
            if (Prism3.Area2d(pts) < 0) pts.Reverse();
            var tries = new List<(double len, Frame f)>();
            for (int i = 0; i < pts.Count; i++)
            {
                var c = pts[i]; var nx = pts[(i + 1) % pts.Count]; var pv = pts[(i - 1 + pts.Count) % pts.Count];
                foreach (var (a, b) in new[] { (nx, pv), (pv, nx) })
                {
                    double ax = a[0] - c[0], ay = a[1] - c[1], al = Math.Sqrt(ax * ax + ay * ay);
                    double bx = b[0] - c[0], by = b[1] - c[1], bl = Math.Sqrt(bx * bx + by * by);
                    if (al < 1e-9 || bl < 1e-9) continue;
                    var x = new[] { ax / al, ay / al }; var y = new[] { bx / bl, by / bl };
                    if (Math.Abs(x[0] * y[0] + x[1] * y[1]) > SquareTol) continue; // only square corners
                    if (dirDeg.HasValue)
                    {
                        double rad = dirDeg.Value * Math.PI / 180;
                        if (x[0] * Math.Cos(rad) + x[1] * Math.Sin(rad) < Math.Cos(Math.PI / 180)) continue;
                    }
                    bool yLeft = x[0] * y[1] - x[1] * y[0] > 0;
                    if (o.Type == "straight" ? !yLeft : turnGiven && (o.Turn == "left") != yLeft) continue;
                    var org = new[] { c[0] - x[0] * lay.MinX - y[0] * lay.MinY, c[1] - x[1] * lay.MinX - y[1] * lay.MinY };
                    tries.Add((al, new Frame { Origin = org, X = x, Y = y }));
                }
            }
            foreach (var t in tries.OrderByDescending(q => q.len))
                if (Outside(lay, t.f, pts).Count == 0) return t.f;
            return null;
        }

        /// <summary>The turn of a frame (left when y is to the left of x).</summary>
        public static string TurnOf(Frame f) => f.X[0] * f.Y[1] - f.X[1] * f.Y[0] > 0 ? "left" : "right";
    }

    /// <summary>
    /// Nâng cấp đợt E (13/11/2026), AutoCAD-free part of build_roof: flat (mái bằng), gable (2 dốc) and hip (4 dốc) roofs
    /// over a house outline (mm). A sloped roof is a slab of the given thickness: (eave prism ∩ the wedges under the top
    /// planes) − (∩ the wedges under the underside planes). The underside meets the wall top at the wall line; the
    /// eave overhangs the walls and ends with a plumb cut. Gable needs a rectangle, hip a convex outline.
    /// </summary>
    public static class RoofLogic
    {
        public const double DefaultThickness = 150, DefaultSlopeDeg = 30, DefaultOverhang = 600;
        public const double MinThickness = 30, MaxThickness = 1000, MinSlopeDeg = 5, MaxSlopeDeg = 60, MaxOverhang = 3000;
        public const int MaxPoints = 200;
        public const string Layer3d = "A-ROOF-3D";
        public static readonly string[] Types = { "flat", "gable", "hip" };

        /// <summary>
        /// Test tổng thể nâng cấp (TT2-1): a slab whose underside lies on the wall top (± this, mm) is a roof slab
        /// (build_3d_from_plan roof_slab), so the roof sits on the slab, not on the walls (it would cut into the slab).
        /// </summary>
        public const double SlabOnWallTolMm = 1;

        /// <summary>
        /// Where the roof sits when wall_top_z is not given (mm): on the wall top, or on top of the slab solids lying on
        /// the wall top (a roof slab). slabs = (bottom z, top z) in mm of the slab solids under the outline.
        /// </summary>
        public static double RoofBase(double wallTopMm, IEnumerable<(double Bottom, double Top)> slabs, out bool onSlab)
        {
            var onTop = (slabs ?? Enumerable.Empty<(double, double)>()).Where(s => Math.Abs(s.Bottom - wallTopMm) <= SlabOnWallTolMm && s.Top > wallTopMm).ToList();
            onSlab = onTop.Count > 0;
            return onSlab ? onTop.Max(s => s.Top) : wallTopMm;
        }

        public sealed class Options
        {
            public string Type = "hip";
            public double Thickness = DefaultThickness, SlopeDeg = DefaultSlopeDeg;
            public double? Overhang;           // default 0 for flat, DefaultOverhang for sloped
            public string Ridge = "long";       // gable: the ridge runs along the long or the short side
        }

        public sealed class Layout
        {
            public string Type;
            public List<double[]> Eave = new List<double[]>();  // outline of the roof edge in plan (mm, CCW)
            public Prism3 Base;                                  // eave outline prism
            public List<Prism3> Outer = new List<Prism3>();      // intersect with Base
            public List<Prism3> Inner = new List<Prism3>();      // their intersection is cut away
            public double EaveZ, RidgeZ, TopZ, Overhang, PlanArea, RoofArea, RidgeLength;
            public bool Rectangle;
            public List<string> Notes = new List<string>();

            /// <summary>True when the point (mm) is inside the roof (for tests).</summary>
            public bool Contains(double[] p) =>
                Base.Contains(p) && Outer.All(q => q.Contains(p)) && !(Inner.Count > 0 && Inner.All(q => q.Contains(p)));
        }

        public static string NormalizeType(string type, string tool = "build_roof")
        {
            var s = (type ?? "hip").Trim().ToLowerInvariant().Replace("-", " ").Replace("_", " ");
            switch (s)
            {
                case "flat": case "bằng": case "bang": case "mái bằng": case "mai bang": return "flat";
                case "gable": case "2 dốc": case "2 doc": case "2 mái": case "2 mai": case "hai mái": case "hai mai": case "hai dốc": case "hai doc": return "gable";
                case "hip": case "4 dốc": case "4 doc": case "4 mái": case "4 mai": case "bốn mái": case "bon mai": case "bốn dốc": case "bon doc": return "hip";
            }
            throw ToolException.Invalid($"{tool}: type must be one of flat (mái bằng), gable (2 dốc), hip (4 dốc) (got '{type}').");
        }

        /// <summary>A corner closer than this (mm) to the line through its neighbours is dropped: a drafting jog of a
        /// millimetre in a straight wall is not a corner (it would make a rectangle look like an L house).</summary>
        public const double JogTol = 1.0;

        /// <summary>Drops repeated points, a closing point equal to the first and points on (or within JogTol mm of) a straight line.</summary>
        public static List<double[]> Clean(IList<double[]> pts, double tol = 1e-6)
        {
            var res = new List<double[]>();
            foreach (var p in pts)
                if (res.Count == 0 || Math.Abs(p[0] - res[res.Count - 1][0]) + Math.Abs(p[1] - res[res.Count - 1][1]) > tol) res.Add(new[] { p[0], p[1] });
            if (res.Count > 1 && Math.Abs(res[0][0] - res[res.Count - 1][0]) + Math.Abs(res[0][1] - res[res.Count - 1][1]) <= tol) res.RemoveAt(res.Count - 1);
            bool changed = true;
            while (changed && res.Count > 3)
            {
                changed = false;
                for (int i = 0; i < res.Count; i++)
                {
                    var a = res[(i - 1 + res.Count) % res.Count]; var b = res[i]; var c = res[(i + 1) % res.Count];
                    double cr = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]);
                    double l = Math.Sqrt((c[0] - a[0]) * (c[0] - a[0]) + (c[1] - a[1]) * (c[1] - a[1]));
                    if (Math.Abs(cr) <= 1e-9 * Math.Max(1, l * l) || (l > 1e-9 && Math.Abs(cr) / l <= JogTol && Dot2(b, a, c) <= 0))
                    {
                        res.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
            return res;
        }

        /// <summary>(a − b)·(c − b): not positive when b lies between a and c (a jog), positive for a spike.</summary>
        private static double Dot2(double[] b, double[] a, double[] c) => (a[0] - b[0]) * (c[0] - b[0]) + (a[1] - b[1]) * (c[1] - b[1]);

        public static bool IsConvex(IList<double[]> pts)
        {
            int sign = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Count]; var c = pts[(i + 2) % pts.Count];
                double cr = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]);
                if (Math.Abs(cr) < 1e-9) continue;
                int s = Math.Sign(cr);
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        public static bool IsRectangle(IList<double[]> pts)
        {
            if (pts.Count != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % 4]; var c = pts[(i + 2) % 4];
                double ux = b[0] - a[0], uy = b[1] - a[1], vx = c[0] - b[0], vy = c[1] - b[1];
                double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
                if (lu < 1e-9 || lv < 1e-9 || Math.Abs(ux * vx + uy * vy) > StairLogic.SquareTol * lu * lv) return false;
            }
            return true;
        }

        /// <summary>The outline (CCW) moved outwards by d (mm; mitred corners).</summary>
        public static List<double[]> Offset(IList<double[]> pts, double d)
        {
            var p = pts.Select(q => new[] { q[0], q[1] }).ToList();
            if (Prism3.Area2d(p) < 0) p.Reverse();
            if (Math.Abs(d) < 1e-12) return p;
            int n = p.Count;
            var res = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                var a = p[(i - 1 + n) % n]; var b = p[i]; var c = p[(i + 1) % n];
                var n1 = OutNormal(a, b); var n2 = OutNormal(b, c);
                // b + d (n1 + n2) / (1 + n1·n2): the intersection of the two moved edges
                double k = 1 + n1[0] * n2[0] + n1[1] * n2[1];
                if (k < 1e-9) throw ToolException.Invalid("build_roof: the outline turns back on itself; it cannot be offset.");
                res.Add(new[] { b[0] + d * (n1[0] + n2[0]) / k, b[1] + d * (n1[1] + n2[1]) / k });
            }
            return res;
        }

        /// <summary>Outward unit normal of edge a → b of a CCW outline.</summary>
        public static double[] OutNormal(double[] a, double[] b)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            return new[] { dy / l, -dx / l };
        }

        /// <summary>
        /// A wedge under the plane z = z0 + s · tan(slope), s = distance inwards from the edge a → b (CCW): a prism with
        /// its profile across the edge (from s = -margin to s = reach, from zLow up to the plane) extruded along the edge
        /// (pad beyond both ends).
        /// </summary>
        public static Prism3 Wedge(double[] a, double[] b, double z0, double tan, double reach, double pad, double zLow, string part)
        {
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
            var e = new[] { dx / l, dy / l };
            var inward = new[] { -e[1], e[0] };
            const double margin = 10;
            return new Prism3
            {
                Profile = new List<double[]>
                {
                    new[] { -margin, zLow }, new[] { reach, zLow }, new[] { reach, z0 + reach * tan }, new[] { -margin, z0 - margin * tan },
                },
                Origin = new[] { a[0] - e[0] * pad, a[1] - e[1] * pad, 0 },
                U = new[] { inward[0], inward[1], 0 }, V = new[] { 0.0, 0, 1 }, Ext = new[] { e[0], e[1], 0 },
                Depth = l + 2 * pad, Part = part,
            };
        }

        /// <summary>The largest distance from the edges (the lowest of the distances to the sloped edges) inside a convex outline.</summary>
        public static double MaxInset(IList<double[]> eave, IList<int> edges)
        {
            double best = 0;
            double minX = eave.Min(q => q[0]), maxX = eave.Max(q => q[0]), minY = eave.Min(q => q[1]), maxY = eave.Max(q => q[1]);
            double D(double x, double y)
            {
                double d = double.MaxValue;
                foreach (var i in edges)
                {
                    var a = eave[i]; var b = eave[(i + 1) % eave.Count];
                    var nn = OutNormal(a, b);
                    d = Math.Min(d, -((x - a[0]) * nn[0] + (y - a[1]) * nn[1]));
                }
                return d;
            }
            // coarse grid, then finer grids around the best point
            double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, hx = (maxX - minX) / 2, hy = (maxY - minY) / 2;
            for (int round = 0; round < 6; round++)
            {
                double bx = cx, by = cy;
                for (int i = 0; i <= 40; i++)
                    for (int j = 0; j <= 40; j++)
                    {
                        double x = cx - hx + 2 * hx * i / 40, y = cy - hy + 2 * hy * j / 40;
                        if (!Prism3.PointInPolygon(eave, x, y)) continue;
                        double d = D(x, y);
                        if (d > best) { best = d; bx = x; by = y; }
                    }
                cx = bx; cy = by; hx /= 8; hy /= 8;
            }
            return best;
        }

        public static void Check(Options o, string tool = "build_roof")
        {
            o.Type = NormalizeType(o.Type, tool);
            if (!(o.Thickness >= MinThickness && o.Thickness <= MaxThickness))
                throw ToolException.Invalid($"{tool}: thickness must be {MinThickness}–{MaxThickness} mm (got {o.Thickness}; default {DefaultThickness}).");
            if (o.Type != "flat" && !(o.SlopeDeg >= MinSlopeDeg && o.SlopeDeg <= MaxSlopeDeg))
                throw ToolException.Invalid($"{tool}: slope must be {MinSlopeDeg}–{MaxSlopeDeg} degrees (got {o.SlopeDeg}; default {DefaultSlopeDeg}).");
            if (o.Overhang.HasValue && !(o.Overhang.Value >= 0 && o.Overhang.Value <= MaxOverhang))
                throw ToolException.Invalid($"{tool}: overhang must be 0–{MaxOverhang} mm (got {o.Overhang}).");
            var r = (o.Ridge ?? "long").Trim().ToLowerInvariant();
            if (r != "long" && r != "short") throw ToolException.Invalid($"{tool}: ridge must be long or short (got '{o.Ridge}').");
            o.Ridge = r;
        }

        /// <summary>The roof over the wall outline (plan, mm), with the wall top at wallTop (mm).</summary>
        public static Layout Build(IList<double[]> outline, double wallTop, Options o, string tool = "build_roof")
        {
            Check(o, tool);
            var pts = Clean(outline);
            if (pts.Count < 3 || Math.Abs(Prism3.Area2d(pts)) < 1e-6) throw ToolException.Invalid($"{tool}: the outline needs at least 3 points around an area. Nothing was built.");
            if (pts.Count > MaxPoints) throw ToolException.Invalid($"{tool}: at most {MaxPoints} outline points.");
            if (Prism3.Area2d(pts) < 0) pts.Reverse();
            if (SelfIntersects(pts)) throw ToolException.Invalid($"{tool}: the outline crosses itself. Nothing was built.");
            double oh = o.Overhang ?? (o.Type == "flat" ? 0 : DefaultOverhang);
            var lay = new Layout { Type = o.Type, Overhang = oh, Rectangle = IsRectangle(pts) };
            if (o.Type == "hip" && !IsConvex(pts))
                throw ToolException.Invalid($"{tool}: a hip roof (4 dốc) is only built over a convex outline (a rectangle or a polygon with no inward corner); this outline has inward corners (an L / T / U house). Nothing was built. Split the outline into rectangles and build a roof on each, or use type gable / flat.");
            if (o.Type == "gable" && !lay.Rectangle)
                throw ToolException.Invalid($"{tool}: a gable roof (2 dốc) is only built over a rectangle; this outline has {pts.Count} corners or is not square. Nothing was built. Split it into rectangles, or use type hip (convex outline) / flat.");
            var eave = Offset(pts, oh);
            if (SelfIntersects(eave)) throw ToolException.Invalid($"{tool}: with an overhang of {oh} mm the roof edge crosses itself (narrow inward corners). Nothing was built. Use a smaller overhang.");
            lay.Eave = eave;
            lay.PlanArea = Prism3.Area2d(eave);
            if (o.Type == "flat")
            {
                lay.Base = Prism3.Vertical(eave, wallTop, o.Thickness, "roof slab");
                lay.EaveZ = wallTop;
                lay.RidgeZ = lay.TopZ = wallTop + o.Thickness;
                lay.RoofArea = lay.PlanArea;
                return lay;
            }
            double th = o.SlopeDeg * Math.PI / 180, tan = Math.Tan(th), cos = Math.Cos(th);
            double up = o.Thickness / cos;           // vertical thickness of the slab
            double ze = wallTop - oh * tan;          // underside at the eave line
            var edges = new List<int>();
            if (o.Type == "hip") for (int i = 0; i < eave.Count; i++) edges.Add(i);
            else
            {
                double l0 = Dist(eave[0], eave[1]), l1 = Dist(eave[1], eave[2]);
                bool firstLong = l0 >= l1;
                bool ridgeAlong0 = o.Ridge == "long" ? firstLong : !firstLong;
                // the sloped edges are the two parallel to the ridge
                edges.AddRange(ridgeAlong0 ? new[] { 0, 2 } : new[] { 1, 3 });
                lay.RidgeLength = ridgeAlong0 ? l0 : l1;
                if (Math.Abs(l0 - l1) < 1e-6) lay.Notes.Add("square outline: the ridge runs along the first side");
            }
            double minX = eave.Min(q => q[0]), maxX = eave.Max(q => q[0]), minY = eave.Min(q => q[1]), maxY = eave.Max(q => q[1]);
            double diag = Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
            double inset = MaxInset(eave, edges);
            lay.EaveZ = ze;
            lay.RidgeZ = ze + inset * tan + up;   // top of the roof at the ridge
            lay.TopZ = lay.RidgeZ;
            double zLow = ze - up - 1000;
            lay.Base = Prism3.Vertical(eave, ze, inset * tan + up + 10, "roof");
            foreach (var i in edges)
            {
                var a = eave[i]; var b = eave[(i + 1) % eave.Count];
                lay.Outer.Add(Wedge(a, b, ze + up, tan, diag + 10, diag + 10, zLow, $"top plane {i}"));
                lay.Inner.Add(Wedge(a, b, ze, tan, diag + 10, diag + 10, zLow - 1000, $"underside {i}"));
            }
            lay.RoofArea = lay.PlanArea / cos;
            if (o.Type == "hip" && lay.Rectangle)
            {
                double l0 = Dist(eave[0], eave[1]), l1 = Dist(eave[1], eave[2]);
                lay.RidgeLength = Math.Abs(l0 - l1);
            }
            if (o.Type == "gable") lay.Notes.Add("the triangular gable ends are open: build the gable walls with extrude / presspull if needed");
            return lay;
        }

        public static double Dist(double[] a, double[] b) => Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));

        public static bool SelfIntersects(IList<double[]> p)
        {
            int n = p.Count;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (j == i + 1 || (i == 0 && j == n - 1)) continue;
                    if (SegCross(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n])) return true;
                }
            return false;
        }

        private static bool SegCross(double[] a, double[] b, double[] c, double[] d)
        {
            double O(double[] p, double[] q, double[] r) => (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]);
            double d1 = O(a, b, c), d2 = O(a, b, d), d3 = O(c, d, a), d4 = O(c, d, b);
            return ((d1 > 1e-9 && d2 < -1e-9) || (d1 < -1e-9 && d2 > 1e-9)) && ((d3 > 1e-9 && d4 < -1e-9) || (d3 < -1e-9 && d4 > 1e-9));
        }
    }
}
