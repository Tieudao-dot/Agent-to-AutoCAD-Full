using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): every number of the MEP pipes in one place.
    /// The STANDARD numbers (sizes, slope, heights, bend radius, clearance, layers / colours) were approved by the owner on
    /// 26/11/2026 as proposed (docs/dot-h-so-tieu-chuan.md, H7). The least drain slopes (TCVN 4474) and the fire pipe sizes are
    /// defaults for the engineer to check against the TCVN in force. Change them here only (the tool descriptions and the
    /// skill ve-mep repeat them; tests keep them the same).
    /// </summary>
    public static class H7Numbers
    {
        // ---- standard numbers, approved 26/11/2026 (docs/dot-h-so-tieu-chuan.md, H7)
        /// <summary>Default sizes (mm, outside diameter) when the user gives none: supply branch / main, drain floor / WC + stack, vent.</summary>
        public const double SupplyBranchMm = 25, SupplyMainMm = 32, DrainFloorMm = 60, DrainWcMm = 110, VentMm = 60;
        /// <summary>
        /// Default slope of a drain (%), and the least slope by size (mm → %): below it the result warns. The default of a size is
        /// the larger of the two (approved 26/11/2026: "ống nhỏ 2,5 %, ống lớn 2 %": D60 falls 2.5 %, D110 and up 2 %), so no
        /// default drain is warned about.
        /// </summary>
        public const double DrainSlopePercent = 2;
        public static readonly (double size, double percent)[] MinDrainSlope = { (50, 2.5), (100, 1.2), (125, 1.0), (150, 0.7), (200, 0.5) };
        /// <summary>under_beams: the top of a pipe / duct this far under the lowest beam above it (mm); without a beam, under the slab.</summary>
        public const double TopBelowBeamMm = 100, TopBelowSlabMm = 300;
        /// <summary>Bend radius (centre line) of an elbow: round pipe × its diameter, rectangular duct × its width.</summary>
        public const double PipeBendFactor = 1.5, DuctBendFactor = 1.0;
        /// <summary>A pipe closer than this (mm) to a beam / column / other pipe is reported as "close" (sát), not only a clash.</summary>
        public const double ClearanceMm = 50;
        /// <summary>KTP-14: two pipe ends closer than this (mm) are one joint (a route going on from another), not a clash.</summary>
        public const double JoinToleranceMm = 1;
        /// <summary>Lengths in the schedule: metres with this many decimals.</summary>
        public const int LengthDecimals = 2;
        /// <summary>The 3D layers of a system: its 2D layer + this ("P-DRAIN" → "P-DRAIN-3D").</summary>
        public const string Layer3dSuffix = "-3D";

        // ---- the waste (hao hụt) column of the schedule: off by default (approved 26/11/2026)
        /// <summary>Show the waste column (length × (1 + share)) by default. Off (the owner chose no waste, 26/11/2026); a call can still ask for it.</summary>
        public const bool WasteColumnByDefault = false;

        // ---- ordinary numbers (table "Con số chờ chủ dự án duyệt" of the handover)
        /// <summary>Most route points in one call; most solids read by a schedule / clash check.</summary>
        public const int MaxPoints = 200, MaxSolids = 5000;
        /// <summary>A tee / an elbow is drawn this much fatter than its pipe so it shows (a fitting's hub).</summary>
        public const double FittingOversize = 1.15;
        /// <summary>The body of a tee along the main pipe: this many branch diameters long.</summary>
        public const double TeeLengthFactor = 2;
        /// <summary>Most pairs reported by the clash check.</summary>
        public const int MaxClashRows = 300;
    }

    /// <summary>One MEP system: its layer, colour, shape and whether it slopes.</summary>
    public sealed class MepSystem
    {
        public string Key, Name, Layer2d;
        public short Color;
        public bool Rect, Slopes;
        /// <summary>Uses (key → default size mm); empty: the user must give the size.</summary>
        public Dictionary<string, double> Uses = new Dictionary<string, double>(StringComparer.Ordinal);
        public string DefaultUse;
        public string Layer3d => Layer2d + H7Numbers.Layer3dSuffix;
    }

    /// <summary>A piece of a route: a straight pipe (A → B) or an elbow (a turn of Angle about Axis through Center, from Start, along StartDir).</summary>
    public sealed class MepPiece
    {
        public string Kind; // "pipe" or "elbow"
        public double[] A, B;
        public double[] Start, Center, Axis, StartDir;
        public double Angle, Radius;
        /// <summary>The width axis of the section at the start (a duct's width): carried along the route so a duct never twists.</summary>
        public double[] W;
        /// <summary>Test & sửa H7: an elbow where a duct's section must turn (a riser between runs in two directions).</summary>
        public bool Twist;
        /// <summary>Centre-line length (drawing units).</summary>
        public double Length;
    }

    /// <summary>A pipe / fitting as the schedule reads it (from the mark the drawing tool leaves on each solid).</summary>
    public sealed class MepMark
    {
        public string System, Kind, Shape;
        public double Size1, Size2, LengthMm;
    }

    public static class MepLogic
    {
        public const string Tool = "draw_mep", ReportTool = "mep_report";
        /// <summary>Key of the record on each solid (its extension dictionary).</summary>
        public const string MarkKey = "CLAUDECAD_MEP";

        public static readonly MepSystem[] Systems =
        {
            new MepSystem { Key = "supply", Name = "Cấp nước", Layer2d = "P-SUPL", Color = 5, DefaultUse = "branch",
                Uses = { ["branch"] = H7Numbers.SupplyBranchMm, ["main"] = H7Numbers.SupplyMainMm } },
            new MepSystem { Key = "hot", Name = "Nước nóng", Layer2d = "P-HOT", Color = 1, DefaultUse = "branch",
                Uses = { ["branch"] = H7Numbers.SupplyBranchMm, ["main"] = H7Numbers.SupplyMainMm } },
            new MepSystem { Key = "drain", Name = "Thoát nước", Layer2d = "P-DRAIN", Color = 3, Slopes = true, DefaultUse = null,
                Uses = { ["floor"] = H7Numbers.DrainFloorMm, ["wc"] = H7Numbers.DrainWcMm, ["stack"] = H7Numbers.DrainWcMm } },
            new MepSystem { Key = "vent", Name = "Thông hơi", Layer2d = "P-VENT", Color = 8, DefaultUse = "vent",
                Uses = { ["vent"] = H7Numbers.VentMm } },
            new MepSystem { Key = "fire", Name = "PCCC", Layer2d = "F-SPRK", Color = 1 },
            new MepSystem { Key = "duct_supply", Name = "Ống gió cấp", Layer2d = "M-DUCT", Color = 4, Rect = true },
            new MepSystem { Key = "duct_return", Name = "Ống gió hồi", Layer2d = "M-DUCT-RET", Color = 6, Rect = true },
        };

        /// <summary>KTP-28: the Vietnamese name of a system key (for the Excel file; the JSON for Claude keeps the key).</summary>
        public static string SystemName(string key) => Systems.FirstOrDefault(s => s.Key == key)?.Name ?? key;

        public static MepSystem SystemOf(string key, string tool = Tool)
        {
            var k = (key ?? "").Trim().ToLowerInvariant();
            var s = Systems.FirstOrDefault(x => x.Key == k);
            if (s == null) throw ToolException.Invalid($"{tool}: system must be one of {string.Join(", ", Systems.Select(x => x.Key))} (got '{key}').");
            return s;
        }

        /// <summary>
        /// The size (mm) of a route: diameter for a pipe (width × height for a duct, returned as two numbers). Given sizes win;
        /// else the default of the use; fire pipes and ducts have no default (the user must say the size).
        /// </summary>
        public static (double a, double b) SizeOf(MepSystem s, double? diameter, double? width, double? height, string use, string tool = Tool)
        {
            if (s.Rect)
            {
                if (!width.HasValue || !height.HasValue)
                    throw ToolException.Invalid($"{tool}: {s.Key} is a rectangular duct: give width and height (mm). There is no default: ask the user for the duct size.");
                if (width <= 0 || height <= 0) throw ToolException.Invalid($"{tool}: width and height must be more than 0 (mm).");
                return (width.Value, height.Value);
            }
            if (diameter.HasValue)
            {
                if (diameter <= 0) throw ToolException.Invalid($"{tool}: diameter must be more than 0 (mm).");
                return (diameter.Value, diameter.Value);
            }
            if (s.Uses.Count == 0)
                throw ToolException.Invalid($"{tool}: {s.Key} pipes have no default size: ask the user for the diameter (mm). The plugin only draws the size given; it does not size pipes.");
            var u = string.IsNullOrWhiteSpace(use) ? s.DefaultUse : use.Trim().ToLowerInvariant();
            if (u == null || !s.Uses.TryGetValue(u, out var d))
                throw ToolException.Invalid($"{tool}: give the diameter (mm) or use = {string.Join(" / ", s.Uses.Keys)} for {s.Key}.");
            return (d, d);
        }

        /// <summary>The least slope (%) for a drain of this size (the row at or below it), null when smaller than the table.</summary>
        public static double? MinSlope(double sizeMm)
        {
            double? r = null;
            foreach (var (size, percent) in H7Numbers.MinDrainSlope) if (sizeMm >= size - 1e-9) r = percent;
            return r;
        }

        /// <summary>The default slope (%) of a drain of this size: DrainSlopePercent, or its least slope when that is more.</summary>
        public static double DefaultSlope(double sizeMm) => Math.Max(H7Numbers.DrainSlopePercent, MinSlope(sizeMm) ?? 0);

        /// <summary>The bend radius (drawing units) of a route.</summary>
        public static double BendRadius(MepSystem s, double a, double mmPerUnit, double? givenMm) =>
            (givenMm ?? (s.Rect ? H7Numbers.DuctBendFactor * a : H7Numbers.PipeBendFactor * a)) / (mmPerUnit > 0 ? mmPerUnit : 1);

        /// <summary>
        /// z of each point of a sloping route given in plan (2D points): from z0 at the first point, falling slope % along the
        /// plan length (the flow goes from the first point to the last).
        /// </summary>
        public static List<double[]> Sloped(IList<double[]> plan, double z0, double slopePercent)
        {
            var r = new List<double[]>();
            double run = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                if (i > 0) run += Math.Sqrt(Sq(plan[i][0] - plan[i - 1][0]) + Sq(plan[i][1] - plan[i - 1][1]));
                r.Add(new[] { plan[i][0], plan[i][1], z0 - run * slopePercent / 100 });
            }
            return r;
        }

        /// <summary>
        /// The pieces of a route: straight pipes between the corners, an elbow of the bend radius at each corner (the pipes are
        /// shortened by the elbow). Points on one straight line make no elbow. A route turning back on itself, a segment too short
        /// for its elbows, or fewer than two distinct points are refused.
        /// </summary>
        public static List<MepPiece> Layout(IList<double[]> points, double bendRadius, string tool = Tool)
        {
            var pts = new List<double[]>();
            foreach (var p in points)
            {
                var q = new[] { p[0], p[1], p.Length > 2 ? p[2] : 0 };
                if (pts.Count == 0 || Dist(pts[pts.Count - 1], q) > 1e-9) pts.Add(q);
            }
            if (pts.Count < 2) throw ToolException.Invalid($"{tool}: the route needs at least two different points.");
            // drop the middle of points on one straight line (no elbow there)
            var clean = new List<double[]> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var u = Unit(Sub(pts[i], clean[clean.Count - 1])); var v = Unit(Sub(pts[i + 1], pts[i]));
                // Test & sửa H7: a turn under MinTurn is no corner (a revolve of a few millionths of a radian can fail)
                if (Dot(u, v) < Math.Cos(MinTurn)) clean.Add(pts[i]);
            }
            clean.Add(pts[pts.Count - 1]);
            int n = clean.Count;
            var trim = new double[n];
            var elbows = new MepPiece[n];
            for (int i = 1; i < n - 1; i++)
            {
                var u = Unit(Sub(clean[i], clean[i - 1])); var v = Unit(Sub(clean[i + 1], clean[i]));
                double theta = Math.Acos(Math.Max(-1, Math.Min(1, Dot(u, v))));
                if (theta > Math.PI - 1e-3) throw ToolException.Invalid($"{tool}: the route turns straight back at point {i + 1}: split it into two routes.");
                double t = bendRadius * Math.Tan(theta / 2);
                trim[i] = t;
                var start = Sub(clean[i], Mul(u, t));
                var nrm = Unit(Sub(v, Mul(u, Dot(v, u))));
                elbows[i] = new MepPiece
                {
                    Kind = "elbow", Start = start, Center = Add(start, Mul(nrm, bendRadius)), Axis = Unit(Cross(u, v)), StartDir = u,
                    Angle = theta, Radius = bendRadius, Length = bendRadius * theta,
                };
            }
            var pieces = new List<MepPiece>();
            // the width axis, carried through the route: level at the start, turned with each elbow (a duct never twists on a riser)
            var w = Section(Sub(clean[1], clean[0])).w;
            for (int i = 0; i < n - 1; i++)
            {
                double len = Dist(clean[i], clean[i + 1]);
                if (trim[i] + trim[i + 1] > len + 1e-9)
                    throw ToolException.Invalid($"{tool}: the segment from point {i + 1} to point {i + 2} is too short for its bends ({Math.Round(len, 1)} long, the bends need {Math.Round(trim[i] + trim[i + 1], 1)}): use a smaller bend_radius or move the points.");
                var d = Unit(Sub(clean[i + 1], clean[i]));
                var a = Add(clean[i], Mul(d, trim[i]));
                var b = Sub(clean[i + 1], Mul(d, trim[i + 1]));
                // Test & sửa H7: a run that is not vertical keeps its width level (the user's width is horizontal); a riser
                // carries the width of the elbow before it
                if (!IsVertical(d)) { var lv = Section(d).w; w = Dot(lv, w) < 0 ? Mul(lv, -1) : lv; }
                else
                {
                    w = Unit(Sub(w, Mul(d, Dot(w, d)))); // across this segment
                    if (Norm(w) < 1e-9) w = Section(d).w;
                }
                if (Dist(a, b) > 1e-9) pieces.Add(new MepPiece { Kind = "pipe", A = a, B = b, Length = Dist(a, b), W = w });
                if (i + 1 < n - 1)
                {
                    var e = elbows[i + 1];
                    e.W = w;
                    var next = Unit(Sub(clean[i + 2], clean[i + 1]));
                    if (IsVertical(d) && !IsVertical(next))
                    {
                        // a riser into a level run: the elbow starts with the section that ends level; the section turns at
                        // the top of the riser when the two runs go different ways (a real duct needs a transition there)
                        var target = Section(next).w;
                        var back = Rotate(target, e.Axis, -e.Angle);
                        back = Unit(Sub(back, Mul(d, Dot(back, d))));
                        if (Norm(back) > 1e-9)
                        {
                            if (Dot(back, w) < 0) back = Mul(back, -1);
                            e.Twist = Math.Abs(Dot(back, w)) < 1 - 1e-6;
                            e.W = back;
                        }
                    }
                    w = Rotate(e.W, e.Axis, e.Angle);
                    // any other turn: the next level run must meet the end of the elbow, else the section turns there
                    if (!IsVertical(next) && Math.Abs(Dot(w, Section(next).w)) < 1 - 1e-6) e.Twist = true;
                    pieces.Add(e);
                }
            }
            return pieces;
        }

        /// <summary>Test & sửa H7: smallest turn (radians) that makes an elbow; less is drawn straight (about 0.06°).</summary>
        public const double MinTurn = 1e-3;

        /// <summary>A vertical direction (a riser): no level width axis.</summary>
        public static bool IsVertical(double[] d) => Norm(Cross(new[] { 0.0, 0, 1 }, Unit(d))) < 1e-6;

        /// <summary>
        /// Test & sửa H7: the slopes (%) of a route given with its heights, from the first point to the last: (smallest fall, a
        /// segment rises). Vertical segments (a stack) are left out; null when every segment is vertical.
        /// </summary>
        public static (double? least, bool rises) SlopesOf(IList<double[]> pts)
        {
            double? least = null; bool rises = false;
            for (int i = 1; i < pts.Count; i++)
            {
                double run = Math.Sqrt(Sq(pts[i][0] - pts[i - 1][0]) + Sq(pts[i][1] - pts[i - 1][1]));
                if (run < 1e-9) continue;
                double fall = (pts[i - 1][2] - pts[i][2]) / run * 100;
                if (fall < -1e-9) rises = true;
                least = least.HasValue ? Math.Min(least.Value, fall) : fall;
            }
            return (least, rises);
        }

        /// <summary>The axes of a cross-section along a direction: w horizontal (the width of a duct), h the other one.</summary>
        public static (double[] w, double[] h) Section(double[] dir)
        {
            var z = Unit(dir);
            var w = Cross(new[] { 0.0, 0, 1 }, z);
            if (Norm(w) < 1e-9) w = new[] { 1.0, 0, 0 }; // a vertical pipe: width along x
            w = Unit(w);
            return (w, Unit(Cross(z, w)));
        }

        /// <summary>v turned by angle about the unit axis k (right hand, like Solid3d.Revolve).</summary>
        public static double[] Rotate(double[] v, double[] k, double angle) =>
            Add(Add(Mul(v, Math.Cos(angle)), Mul(Cross(k, v), Math.Sin(angle))), Mul(k, Dot(k, v) * (1 - Math.Cos(angle))));

        /// <summary>The section axes of a piece: its carried width axis when it has one, else level (Section).</summary>
        public static (double[] w, double[] h) SectionOf(double[] dir, double[] carried)
        {
            if (carried == null) return Section(dir);
            var z = Unit(dir);
            var w = Unit(Sub(carried, Mul(z, Dot(carried, z))));
            if (Norm(w) < 1e-9) return Section(dir);
            return (w, Unit(Cross(z, w)));
        }

        /// <summary>The point of a pipe axis A → B nearest to p, and its share along it (0..1).</summary>
        public static (double[] at, double share) Nearest(double[] a, double[] b, double[] p)
        {
            var ab = Sub(b, a);
            double l2 = Dot(ab, ab);
            double s = l2 > 0 ? Math.Max(0, Math.Min(1, Dot(Sub(p, a), ab) / l2)) : 0;
            return (Add(a, Mul(ab, s)), s);
        }

        // ------------------------------------------------------------ schedule

        public sealed class ScheduleRow
        {
            public string System, Size;
            public double LengthM;
            public int Pipes, Elbows, Tees;
        }

        public static string SizeText(MepMark m) =>
            m.Shape == "rect" ? $"{Num(m.Size1)} × {Num(m.Size2)}" : "D" + Num(m.Size1);

        /// <summary>The schedule: one row per system + size (by the order of Systems, then size), lengths in metres.</summary>
        public static List<ScheduleRow> Schedule(IEnumerable<MepMark> marks)
        {
            var rows = new Dictionary<(string, string), ScheduleRow>();
            foreach (var m in marks)
            {
                if (m == null || m.System == null) continue;
                var key = (m.System, SizeText(m));
                if (!rows.TryGetValue(key, out var r)) rows[key] = r = new ScheduleRow { System = m.System, Size = key.Item2 };
                if (m.Kind == "elbow") { r.Elbows++; r.LengthM += m.LengthMm / 1000; }
                else if (m.Kind == "tee") r.Tees++;
                else { r.Pipes++; r.LengthM += m.LengthMm / 1000; }
            }
            int Order(string sys) { int i = Array.FindIndex(Systems, x => x.Key == sys); return i < 0 ? 99 : i; }
            return rows.Values.OrderBy(r => Order(r.System)).ThenBy(r => SizeOrder(r.Size)).ThenBy(r => r.Size, StringComparer.Ordinal).ToList();
        }

        private static double SizeOrder(string size)
        {
            var digits = new string((size ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        /// <summary>The Excel / table rows of the schedule (waste share null: no waste column).</summary>
        public static List<List<object>> ScheduleTable(IList<ScheduleRow> rows, double? wasteShare)
        {
            var head = new List<object> { "Hệ", "Cỡ ống (mm)", "Chiều dài (m)", "Số đoạn ống", "Số co", "Số tê" };
            if (wasteShare.HasValue) head.Insert(3, $"Chiều dài + hao hụt {Num(wasteShare.Value * 100)}% (m)");
            var t = new List<List<object>> { head };
            foreach (var r in rows)
            {
                var name = Systems.FirstOrDefault(s => s.Key == r.System)?.Name ?? r.System;
                var line = new List<object> { name, r.Size, Math.Round(r.LengthM, H7Numbers.LengthDecimals), r.Pipes, r.Elbows, r.Tees };
                if (wasteShare.HasValue) line.Insert(3, Math.Round(r.LengthM * (1 + wasteShare.Value), H7Numbers.LengthDecimals));
                t.Add(line);
            }
            double len = rows.Sum(r => r.LengthM);
            var total = new List<object> { "Tổng", "", Math.Round(len, H7Numbers.LengthDecimals), rows.Sum(r => r.Pipes), rows.Sum(r => r.Elbows), rows.Sum(r => r.Tees) };
            if (wasteShare.HasValue) total.Insert(3, Math.Round(len * (1 + wasteShare.Value), H7Numbers.LengthDecimals));
            t.Add(total);
            return t;
        }

        /// <summary>The waste share to show: the call's waste_percent, else none unless WasteColumnByDefault (then 0 %).</summary>
        public static double? WasteShare(double? wastePercent, string tool = ReportTool)
        {
            if (wastePercent.HasValue)
            {
                if (wastePercent < 0 || wastePercent > 100) throw ToolException.Invalid($"{tool}: waste_percent must be 0 – 100.");
                return wastePercent.Value / 100;
            }
            return H7Numbers.WasteColumnByDefault ? 0 : (double?)null;
        }

        // ------------------------------------------------------------ clash

        /// <summary>"clash" (they overlap), "close" (gap under the clearance), or null (clear); gap from the boxes (drawing units).</summary>
        public static string ClashKind(bool interfere, double boxGap, double clearance) =>
            interfere ? "clash" : boxGap < clearance - 1e-9 ? "close" : null;

        /// <summary>The gap between two boxes (0 when they touch or overlap).</summary>
        public static double BoxGap(double[] aMin, double[] aMax, double[] bMin, double[] bMax)
        {
            double s = 0;
            for (int k = 0; k < 3; k++)
            {
                double d = Math.Max(0, Math.Max(aMin[k] - bMax[k], bMin[k] - aMax[k]));
                s += d * d;
            }
            return Math.Sqrt(s);
        }

        /// <summary>Distance from point p to a box (0 inside).</summary>
        public static double PointBox(double[] p, double[] min, double[] max)
        {
            double s = 0;
            for (int k = 0; k < 3; k++) { double d = Math.Max(0, Math.Max(min[k] - p[k], p[k] - max[k])); s += d * d; }
            return Math.Sqrt(s);
        }

        /// <summary>
        /// The gap between a straight pipe (axis a → b, radius r) and a box (a beam, a column): the distance from the axis to the
        /// box minus r (0 when they touch). The distance along the axis is convex: found by halving the search.
        /// </summary>
        public static double PipeBoxGap(double[] a, double[] b, double r, double[] min, double[] max)
        {
            double lo = 0, hi = 1;
            double F(double t) => PointBox(Add(a, Mul(Sub(b, a), t)), min, max);
            for (int i = 0; i < 80; i++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                if (F(m1) <= F(m2)) hi = m2; else lo = m1;
            }
            double d = Math.Min(F((lo + hi) / 2), Math.Min(F(0), F(1)));
            return Math.Max(0, d - r);
        }

        /// <summary>
        /// KTP-13: how far the section of a straight pipe reaches towards direction n (unit) from its axis: a round pipe its
        /// radius; a duct (width level, height across) |n·w|·W/2 + |n·h|·H/2, so a duct under a beam reaches up by half its
        /// height, not half its diagonal. A vertical duct (its width axis is not kept) reaches by half its diagonal.
        /// </summary>
        public static double SectionReach(double[] a, double[] b, bool rect, double size1, double size2, double[] n)
        {
            if (!rect) return size1 / 2;
            var d = Unit(Sub(b, a));
            if (IsVertical(d) || n == null || Norm(n) < 1e-12) return Math.Sqrt(size1 * size1 + size2 * size2) / 2;
            var (w, h) = Section(d);
            var u = Unit(n);
            return Math.Abs(Dot(u, w)) * size1 / 2 + Math.Abs(Dot(u, h)) * size2 / 2;
        }

        /// <summary>The point of the box nearest to q.</summary>
        public static double[] NearestOnBox(double[] q, double[] min, double[] max) =>
            new[] { Math.Max(min[0], Math.Min(max[0], q[0])), Math.Max(min[1], Math.Min(max[1], q[1])), Math.Max(min[2], Math.Min(max[2], q[2])) };

        /// <summary>
        /// KTP-13: the gap from a straight pipe / duct (axis a → b, section rect / size1 × size2, drawing units) to a box: the
        /// shortest axis-to-box distance less the section's reach towards the box.
        /// </summary>
        public static double SectionBoxGap(double[] a, double[] b, bool rect, double size1, double size2, double[] min, double[] max)
        {
            double lo = 0, hi = 1;
            double F(double t) => PointBox(Add(a, Mul(Sub(b, a), t)), min, max);
            for (int i = 0; i < 80; i++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                if (F(m1) <= F(m2)) hi = m2; else lo = m1;
            }
            double tb = (lo + hi) / 2;
            if (F(0) < F(tb)) tb = 0;
            if (F(1) < F(tb)) tb = 1;
            var q = Add(a, Mul(Sub(b, a), tb));
            var n = Sub(NearestOnBox(q, min, max), q);
            double d = Norm(n);
            return d < 1e-12 ? 0 : Math.Max(0, d - SectionReach(a, b, rect, size1, size2, n));
        }

        /// <summary>KTP-13: the gap between two straight pipes / ducts: the shortest axis distance less each section's reach towards the other.</summary>
        public static double SectionSectionGap(double[] a1, double[] b1, bool rect1, double s11, double s12, double[] a2, double[] b2, bool rect2, double s21, double s22)
        {
            var (p, q) = ClosestOfSegments(a1, b1, a2, b2);
            var n = Sub(q, p);
            double d = Norm(n);
            if (d < 1e-12) return 0;
            return Math.Max(0, d - SectionReach(a1, b1, rect1, s11, s12, n) - SectionReach(a2, b2, rect2, s21, s22, Mul(n, -1)));
        }

        /// <summary>KTP-14: two pipes meet end to end (one route going on from another, or a size change): an end of one is an end of the other.</summary>
        public static bool EndToEnd(double[] a1, double[] b1, double[] a2, double[] b2, double tol)
        {
            foreach (var x in new[] { a1, b1 })
                foreach (var y in new[] { a2, b2 })
                    if (Dist(x, y) <= tol) return true;
            return false;
        }

        /// <summary>
        /// KTP-17: half the section in the plane of an elbow's bend (drawing units): the bend radius must be more, or the
        /// inside of the bend folds over itself. A round pipe: its radius; a duct: the side across the bend (its height for a
        /// turn up / down, its width for a turn in plan).
        /// </summary>
        public static double HalfInBendPlane(MepPiece e, bool rect, double size1, double size2)
        {
            if (!rect) return size1 / 2;
            var across = Unit(Cross(e.StartDir, e.Axis));
            var w = e.W != null && Norm(e.W) > 1e-9 ? Unit(e.W) : Section(e.StartDir).w;
            var h = Unit(Cross(e.StartDir, w));
            return Math.Abs(Dot(across, w)) * size1 / 2 + Math.Abs(Dot(across, h)) * size2 / 2;
        }

        private static (double[] p, double[] q) ClosestOfSegments(double[] a1, double[] b1, double[] a2, double[] b2)
        {
            var d1 = Sub(b1, a1); var d2 = Sub(b2, a2); var rr = Sub(a1, a2);
            double a = Dot(d1, d1), e = Dot(d2, d2), f = Dot(d2, rr), s, t;
            if (a <= 1e-12 && e <= 1e-12) return (a1, a2);
            if (a <= 1e-12) { s = 0; t = Clamp(f / e); }
            else
            {
                double c = Dot(d1, rr);
                if (e <= 1e-12) { t = 0; s = Clamp(-c / a); }
                else
                {
                    double bb = Dot(d1, d2), den = a * e - bb * bb;
                    s = den > 1e-12 ? Clamp((bb * f - c * e) / den) : 0;
                    t = (bb * s + f) / e;
                    if (t < 0) { t = 0; s = Clamp(-c / a); }
                    else if (t > 1) { t = 1; s = Clamp((bb - c) / a); }
                }
            }
            return (Add(a1, Mul(d1, s)), Add(a2, Mul(d2, t)));
        }

        /// <summary>The gap between two straight pipes (axes and radii): the shortest distance between the axes minus both radii.</summary>
        public static double PipePipeGap(double[] a1, double[] b1, double r1, double[] a2, double[] b2, double r2)
        {
            // closest points of two segments (Ericson, Real-Time Collision Detection 5.1.9)
            var d1 = Sub(b1, a1); var d2 = Sub(b2, a2); var rr = Sub(a1, a2);
            double a = Dot(d1, d1), e = Dot(d2, d2), f = Dot(d2, rr), s, t;
            if (a <= 1e-12 && e <= 1e-12) return Math.Max(0, Dist(a1, a2) - r1 - r2);
            if (a <= 1e-12) { s = 0; t = Clamp(f / e); }
            else
            {
                double c = Dot(d1, rr);
                if (e <= 1e-12) { t = 0; s = Clamp(-c / a); }
                else
                {
                    double bb = Dot(d1, d2), den = a * e - bb * bb;
                    s = den > 1e-12 ? Clamp((bb * f - c * e) / den) : 0;
                    t = (bb * s + f) / e;
                    if (t < 0) { t = 0; s = Clamp(-c / a); }
                    else if (t > 1) { t = 1; s = Clamp((bb - c) / a); }
                }
            }
            return Math.Max(0, Dist(Add(a1, Mul(d1, s)), Add(a2, Mul(d2, t))) - r1 - r2);
        }

        private static double Clamp(double x) => Math.Max(0, Math.Min(1, x));

        /// <summary>A layer that holds beams or columns (the solids pipes are checked against by default).</summary>
        public static bool IsStructureLayer(string layer)
        {
            var l = (layer ?? "").ToUpperInvariant();
            // KTP (nghi vấn): "S-" only as the prefix of a structure layer (S-BEAM, S-COL…), not inside any name ("A-WALLS-NEW")
            return l.Contains("BEAM") || l.Contains("COL") || l.Contains("DAM") || l.Contains("COT") || l.StartsWith("S-", StringComparison.Ordinal);
        }

        // ------------------------------------------------------------ vectors

        public static double Sq(double x) => x * x;
        public static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        public static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        public static double[] Mul(double[] a, double k) => new[] { a[0] * k, a[1] * k, a[2] * k };
        public static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        public static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        public static double Norm(double[] a) => Math.Sqrt(Dot(a, a));
        public static double Dist(double[] a, double[] b) => Norm(Sub(a, b));
        public static double[] Unit(double[] a) { double n = Norm(a); return n > 0 ? Mul(a, 1 / n) : new[] { 0.0, 0, 0 }; }
        public static string Num(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
