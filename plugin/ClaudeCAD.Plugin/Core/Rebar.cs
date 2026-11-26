using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H8 (docs/ban-giao/nang-cap-h8.md): every number of the structural tools (footings, rebar details, the rebar
    /// schedule) in one place. The STANDARD numbers were approved by the owner on 26/11/2026 as proposed
    /// (docs/dot-h-so-tieu-chuan.md, H8). Four are defaults the engineer checks against the TCVN in force: the cover of a footing
    /// without lean concrete (70), the least anchorage / lap, the 75 mm of a stirrup hook, the scales of TCVN 5572.
    /// The plugin draws and schedules what the user gives: it never designs (no bar count / main bar size is chosen for the user).
    /// </summary>
    public static class H8Numbers
    {
        // ---- standard numbers, approved 26/11/2026
        /// <summary>Concrete cover (mm, to the outside of the bars) by element; never less than the bar diameter.</summary>
        public const double CoverSlab = 20, CoverBeam = 25, CoverColumn = 25, CoverFootingLean = 40, CoverFootingNoLean = 70;
        /// <summary>Anchorage and lap lengths (× d); the least ones (× d and mm) below which the result warns.</summary>
        public const double AnchorFactor = 30, LapBeamSlabFactor = 40, LapColumnFactor = 35;
        public const double MinAnchorFactor = 15, MinAnchorMm = 200, MinLapFactor = 20, MinLapMm = 250;
        /// <summary>Plain bars (CB240-T) up to this diameter, ribbed (CB400-V) above.</summary>
        public const double PlainMaxDiameter = 10;
        public const string PlainGrade = "CB240-T", RibbedGrade = "CB400-V";
        /// <summary>Plain bar: 180° hook, inside bend 2.5d, straight 3d. Stirrup: 135° hook, straight max(6d, 75 mm). Ribbed bar: L bend 10d.</summary>
        public const double PlainHookBendFactor = 2.5, PlainHookStraightFactor = 3, StirrupHookFactor = 6, StirrupHookMinMm = 75, RibbedBendFactor = 10;
        /// <summary>Weight of a bar: KgPerMFactor × d² (kg / m, d in mm).</summary>
        public const double KgPerMFactor = 0.00617;
        public static readonly double[] Diameters = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32 };
        /// <summary>Sample bars when the user gives none (never the main bars): slab Ø8 a200; beam stirrups Ø6 a150 in the quarter of the span by each support, a200 in the middle; column ties Ø6 a200.</summary>
        public const double SlabBarDia = 8, SlabBarSpacing = 200, StirrupDia = 6, StirrupEndSpacing = 150, StirrupMidSpacing = 200, TieDia = 6, TieSpacing = 200;
        /// <summary>The end zone of the stirrups: this share of the span by each support.</summary>
        public const double StirrupEndShare = 0.25;
        /// <summary>A stock bar (mm): longer bars need splices (counted in the schedule).</summary>
        public const double StockLengthMm = 11700;
        /// <summary>Sample footings ("cỡ mẫu, kỹ sư quyết"): pad B × L, base height; strip width, height, rib b × h; lean concrete thickness and margin.</summary>
        public const double PadSize = 1200, PadHeight = 350, StripWidth = 1000, StripHeight = 300, RibWidth = 300, RibHeight = 600, LeanThickness = 100, LeanMargin = 100;
        /// <summary>Scales (1 : n): beam / column sections, beam elevation and slab plan, footing plan.</summary>
        public const int SectionScale = 20, ElevationScale = 50, FootingPlanScale = 100;
        /// <summary>Lengths of the schedule rounded to this (mm); weights with this many decimals (kg).</summary>
        public const double LengthRoundMm = 10;
        public const int WeightDecimals = 2;
        /// <summary>The waste (hao hụt) column of the schedule: off (approved 26/11/2026); a call can still ask for it.</summary>
        public const bool WasteColumnByDefault = false;
        /// <summary>Text on the paper (mm): labels and bar marks; the bubble of a bar mark (mm on paper).</summary>
        public const double TextHeightPaper = 2.5, MarkBubblePaper = 8;

        // ---- ordinary numbers (table "Con số chờ chủ dự án duyệt" of the handover)
        /// <summary>Most bars of one kind in a section; most footings / stirrup lines drawn in one call.</summary>
        public const int MaxBarsInSection = 40, MaxFootings = 500, MaxStirrupLines = 400;
        /// <summary>The first stirrup this far (mm) from the face of the support; spacings allowed between these (mm).</summary>
        public const double FirstStirrupMm = 50, MinSpacingMm = 50, MaxSpacingMm = 1000;
        /// <summary>Layers: footings, lean concrete (3D); concrete outline, bars, texts (2D details).</summary>
        public const string FootingLayer = "S-FOOT-3D", LeanLayer = "S-LEAN-3D", ConcreteLayer = "S-CONC", RebarLayer = "S-REBAR", TextLayer = "S-REBAR-TEXT";
        public const short FootingColor = 8, LeanColor = 9, ConcreteColor = 7, RebarColor = 1, TextColor = 7;
    }

    /// <summary>One bar kind of a detail as the schedule reads it (the mark on each drawn bar).</summary>
    public sealed class RebarMark
    {
        public int Mark;
        public double Dia;
        public string Shape, Element;
        /// <summary>Length of one bar (mm, hooks / anchors included) and how many bars of it.</summary>
        public double LengthMm;
        public int Count;
    }

    public static class RebarLogic
    {
        public const string DrawTool = "draw_rebar", ScheduleTool = "rebar_schedule", FootingTool = "build_footing";
        public const string MarkKey = "CLAUDECAD_REBAR";

        private static readonly Regex Bars = new Regex(@"^\s*(\d+)\s*(?:Ø|ø|Φ|φ|D|d|phi|fi)\s*(\d+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex Spaced = new Regex(@"^\s*(?:Ø|ø|Φ|φ|D|d|phi|fi)\s*(\d+(?:[.,]\d+)?)\s*(?:a|@|s)\s*(\d+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant);

        private static double N(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

        /// <summary>"2Ø18" (also "2D18", "2 phi 18") → (2, 18). The diameter must be a standard one.</summary>
        public static (int n, double d) ParseBars(string text, string what, string tool = DrawTool)
        {
            var m = Bars.Match(text ?? "");
            if (!m.Success) throw ToolException.Invalid($"{tool}: {what} must be like \"2Ø18\" (count Ø diameter), got '{text}'. The plugin does not choose bars: ask the user.");
            int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            double d = CheckDia(N(m.Groups[2].Value), what, tool);
            if (n < 1 || n > H8Numbers.MaxBarsInSection) throw ToolException.Invalid($"{tool}: {what}: 1 – {H8Numbers.MaxBarsInSection} bars.");
            return (n, d);
        }

        /// <summary>"Ø8a200" (also "D8@200") → (8, 200).</summary>
        public static (double dia, double spacing) ParseSpaced(string text, string what, string tool = DrawTool)
        {
            var m = Spaced.Match(text ?? "");
            if (!m.Success) throw ToolException.Invalid($"{tool}: {what} must be like \"Ø8a200\" (Ø diameter a spacing in mm), got '{text}'.");
            double d = CheckDia(N(m.Groups[1].Value), what, tool), s = N(m.Groups[2].Value);
            if (s < H8Numbers.MinSpacingMm || s > H8Numbers.MaxSpacingMm)
                throw ToolException.Invalid($"{tool}: {what}: spacing {Num(s)} mm is outside {Num(H8Numbers.MinSpacingMm)} – {Num(H8Numbers.MaxSpacingMm)}.");
            return (d, s);
        }

        public static double CheckDia(double d, string what, string tool = DrawTool)
        {
            if (!H8Numbers.Diameters.Any(x => Math.Abs(x - d) < 1e-9))
                throw ToolException.Invalid($"{tool}: {what}: Ø{Num(d)} is not a standard bar ({string.Join(", ", H8Numbers.Diameters.Select(Num))}).");
            return d;
        }

        public static bool Plain(double d) => d <= H8Numbers.PlainMaxDiameter + 1e-9;
        public static string Grade(double d) => Plain(d) ? H8Numbers.PlainGrade : H8Numbers.RibbedGrade;
        public static double KgPerM(double d) => H8Numbers.KgPerMFactor * d * d;

        /// <summary>The cover (mm) of an element (slab / beam / column / footing), never under the bar diameter.</summary>
        public static double Cover(string element, double barDia, bool lean = true)
        {
            double c;
            switch (element)
            {
                case "slab": c = H8Numbers.CoverSlab; break;
                case "column": c = H8Numbers.CoverColumn; break;
                case "footing": c = lean ? H8Numbers.CoverFootingLean : H8Numbers.CoverFootingNoLean; break;
                default: c = H8Numbers.CoverBeam; break;
            }
            return Math.Max(c, barDia);
        }

        public static double Anchor(double d) => H8Numbers.AnchorFactor * d;
        public static double Lap(double d, string element) => (element == "column" ? H8Numbers.LapColumnFactor : H8Numbers.LapBeamSlabFactor) * d;

        /// <summary>A warning when a given anchorage / lap is under its least value, else null.</summary>
        public static string CheckLength(string kind, double givenMm, double d)
        {
            bool lap = kind == "lap";
            double least = Math.Max((lap ? H8Numbers.MinLapFactor : H8Numbers.MinAnchorFactor) * d, lap ? H8Numbers.MinLapMm : H8Numbers.MinAnchorMm);
            return givenMm < least - 1e-9
                ? $"The {(lap ? "lap" : "anchorage")} {Num(givenMm)} mm of Ø{Num(d)} is under its least value {Num(least)} mm: tell the user (default, the engineer checks the TCVN in force)."
                : null;
        }

        /// <summary>Extra length of one end hook (mm): plain bar 180° (half a bend of 2.5d inside + 3d straight); ribbed bar: none.</summary>
        public static double MainHook(double d) =>
            Plain(d) ? Math.PI * (H8Numbers.PlainHookBendFactor * d + d) / 2 + H8Numbers.PlainHookStraightFactor * d : 0;

        /// <summary>
        /// Extra length at an anchored end of a main bar (mm): plain bar, its 180° hook; ribbed bar, its L bend of 10d
        /// (skill ve-ket-cau: "thép gân bẻ L 10d khi neo").
        /// </summary>
        public static double AnchorEnd(double d) => Plain(d) ? MainHook(d) : H8Numbers.RibbedBendFactor * d;

        /// <summary>The straight end of a stirrup / tie hook (mm): max(6d, 75).</summary>
        public static double StirrupHook(double d) => Math.Max(H8Numbers.StirrupHookFactor * d, H8Numbers.StirrupHookMinMm);

        /// <summary>One stirrup / tie of a b × h section (mm): its centre-line rectangle inside the cover, plus its two hooks.</summary>
        public static double StirrupLength(double b, double h, double cover, double d)
        {
            double w = b - 2 * cover - d, t = h - 2 * cover - d;
            if (w <= 0 || t <= 0) throw ToolException.Invalid($"{DrawTool}: the section {Num(b)} × {Num(h)} is too small for its cover {Num(cover)} mm.");
            return 2 * (w + t) + 2 * StirrupHook(d);
        }

        /// <summary>
        /// The stirrups of a beam span (mm): end zones (StirrupEndShare of the span by each support) at the end spacing, the middle
        /// at the middle spacing; first stirrup half a spacing from the support. Returns the positions along the span.
        /// </summary>
        public static List<double> StirrupPositions(double span, double endSpacing, double midSpacing)
        {
            var r = new List<double>();
            double zone = span * H8Numbers.StirrupEndShare;
            double x = Math.Min(H8Numbers.FirstStirrupMm, span / 2);
            while (x <= span - H8Numbers.FirstStirrupMm + 1e-9)
            {
                r.Add(x);
                bool end = x < zone - 1e-9 || x > span - zone + 1e-9;
                x += end ? endSpacing : midSpacing;
            }
            return r;
        }

        /// <summary>Positions (mm, from the left face) of n bars across a width b, inside the cover and the stirrup.</summary>
        public static List<double> BarPositions(int n, double b, double cover, double stirrupDia, double barDia)
        {
            double first = cover + stirrupDia + barDia / 2, last = b - first;
            if (last < first - 1e-9) throw ToolException.Invalid($"{DrawTool}: Ø{Num(barDia)} bars do not fit in {Num(b)} mm with the cover.");
            if (n == 1) return new List<double> { b / 2 };
            double gap = (last - first) / (n - 1);
            if (gap < barDia - 1e-9) throw ToolException.Invalid($"{DrawTool}: {n}Ø{Num(barDia)} do not fit in one row of {Num(b)} mm (less than one bar between them): ask the user for fewer bars or a wider section.");
            return Enumerable.Range(0, n).Select(i => first + i * gap).ToList();
        }

        /// <summary>Positions of n column bars round a b × h section: the 4 corners first, then the rest along the sides (even counts).</summary>
        public static List<double[]> ColumnBars(int n, double b, double h, double cover, double tieDia, double barDia)
        {
            if (n < 4) throw ToolException.Invalid($"{DrawTool}: a column needs at least 4 bars (one in each corner).");
            if (n % 2 != 0) throw ToolException.Invalid($"{DrawTool}: give an even number of column bars (both sides alike).");
            double e = cover + tieDia + barDia / 2;
            var pts = new List<double[]> { new[] { e, e }, new[] { b - e, e }, new[] { b - e, h - e }, new[] { e, h - e } };
            int rest = n - 4;
            // share the rest between the two pairs of sides by their length
            int alongB = (int)Math.Round(rest / 2.0 * (b / (b + h))) , alongH = rest / 2 - alongB;
            // Test & sửa H8: bars along a side at least one bar apart (clear), like the beam rows
            foreach (var (count, side) in new[] { (alongB, b), (alongH, h) })
            {
                double gap = (side - 2 * e) / (count + 1);
                if (gap - barDia < barDia - 1e-9)
                    throw ToolException.Invalid($"{DrawTool}: {n}Ø{Num(barDia)} do not fit round a {Num(b)} × {Num(h)} column (less than one bar between them): ask the user for fewer bars or a bigger section.");
            }
            for (int i = 1; i <= alongB; i++) { double x = e + (b - 2 * e) * i / (alongB + 1); pts.Add(new[] { x, e }); pts.Add(new[] { x, h - e }); }
            for (int i = 1; i <= alongH; i++) { double y = e + (h - 2 * e) * i / (alongH + 1); pts.Add(new[] { e, y }); pts.Add(new[] { b - e, y }); }
            return pts;
        }

        /// <summary>Label of a bar kind: "2Ø18 L=5200" (length rounded to LengthRoundMm).</summary>
        public static string Label(int count, double dia, double lengthMm) => $"{count}Ø{Num(dia)} L={Num(RoundLength(lengthMm))}";
        public static string SpacedLabel(double dia, double spacing) => $"Ø{Num(dia)}a{Num(spacing)}";

        public static double RoundLength(double mm) => Math.Round(mm / H8Numbers.LengthRoundMm, MidpointRounding.AwayFromZero) * H8Numbers.LengthRoundMm;

        /// <summary>Splices of one bar of this length from stock bars.</summary>
        public static int Splices(double lengthMm) => lengthMm <= H8Numbers.StockLengthMm + 1e-9 ? 0 : (int)Math.Ceiling(lengthMm / H8Numbers.StockLengthMm) - 1;

        /// <summary>
        /// A bar longer than a stock bar: its splices and the length with their laps (40d beam / slab, 35d column) added; the laps
        /// lengthen the bar, so the splices are counted again until they stay the same. element: "cột" / "column" or else.
        /// </summary>
        public static (int splices, double lapEach, double lengthMm) WithLaps(double lengthMm, double d, string element)
        {
            double lap = Lap(d, element == "cột" || element == "column" ? "column" : "beam");
            int n = Splices(lengthMm);
            for (int i = 0; i < 50; i++)
            {
                int m = Splices(lengthMm + n * lap);
                if (m == n) break;
                n = m;
            }
            return (n, lap, lengthMm + n * lap);
        }

        // ------------------------------------------------------------ the bar kinds of an element (what is drawn and scheduled)

        public sealed class BarKind
        {
            public string Role, Shape, Element;
            public double Dia, LengthMm, Spacing;
            /// <summary>Test & sửa H8: the spacing in the middle of a beam span (stirrups by zones), 0 when the same.</summary>
            public double MidSpacing;
            public int Count;
            public string Label => Spacing > 0
                ? (MidSpacing > 0 && Math.Abs(MidSpacing - Spacing) > 1e-9 ? $"{SpacedLabel(Dia, Spacing)}/a{Num(MidSpacing)}" : SpacedLabel(Dia, Spacing))
                : $"{Count}Ø{Num(Dia)}";
        }

        private static string MainShape(double d) => Plain(d) ? "thẳng, 2 móc 180°" : "thẳng";
        private static string AnchoredShape(double d) => Plain(d) ? "thẳng, 2 móc 180°" : $"bẻ L {Num(H8Numbers.RibbedBendFactor)}d hai đầu";

        /// <summary>
        /// A beam span: bottom and top bars (span + an anchorage into each support, hooks on plain bars, an L bend of 10d on
        /// ribbed bars), stirrups by zones.
        /// anchorMm: the anchorage asked (default 30d); a warning when under the least one.
        /// </summary>
        public static List<BarKind> BeamBars(double b, double h, double span, (int n, double d) bottom, (int n, double d) top,
            double stirrupDia, double endSpacing, double midSpacing, double? anchorMm, List<string> warnings)
        {
            if (span <= 0) throw ToolException.Invalid($"{DrawTool}: span must be more than 0 (mm).");
            double cover = Cover("beam", Math.Max(bottom.d, top.d));
            StirrupLength(b, h, cover, stirrupDia); // the section fits
            BarPositions(bottom.n, b, cover, stirrupDia, bottom.d);
            BarPositions(top.n, b, cover, stirrupDia, top.d);
            double Len(double d)
            {
                double a = anchorMm ?? Anchor(d);
                var w = CheckLength("anchor", a, d);
                if (w != null && !warnings.Contains(w)) warnings.Add(w);
                return span + 2 * a + 2 * AnchorEnd(d);
            }
            var pos = StirrupPositions(span, endSpacing, midSpacing);
            return new List<BarKind>
            {
                new BarKind { Role = "bottom", Element = "dầm", Dia = bottom.d, Count = bottom.n, LengthMm = Len(bottom.d), Shape = AnchoredShape(bottom.d) },
                new BarKind { Role = "top", Element = "dầm", Dia = top.d, Count = top.n, LengthMm = Len(top.d), Shape = AnchoredShape(top.d) },
                new BarKind { Role = "stirrup", Element = "dầm", Dia = stirrupDia, Count = pos.Count, LengthMm = StirrupLength(b, h, cover, stirrupDia), Shape = "đai kín, móc 135°", Spacing = endSpacing, MidSpacing = midSpacing },
            };
        }

        /// <summary>A column of height length: its bars (length + a lap above, hooks on plain bars) and its ties.</summary>
        public static List<BarKind> ColumnBarKinds(double b, double h, double length, (int n, double d) bars, double tieDia, double tieSpacing, double? lapMm, List<string> warnings)
        {
            if (length <= 0) throw ToolException.Invalid($"{DrawTool}: length (the column height, mm) must be more than 0.");
            double cover = Cover("column", bars.d);
            ColumnBars(bars.n, b, h, cover, tieDia, bars.d);
            StirrupLength(b, h, cover, tieDia);
            double lap = lapMm ?? Lap(bars.d, "column");
            var w = CheckLength("lap", lap, bars.d);
            if (w != null) warnings.Add(w);
            int ties = (int)Math.Floor((length - 2 * H8Numbers.FirstStirrupMm) / tieSpacing + 1e-9) + 1;
            return new List<BarKind>
            {
                new BarKind { Role = "main", Element = "cột", Dia = bars.d, Count = bars.n, LengthMm = length + lap + 2 * MainHook(bars.d), Shape = MainShape(bars.d) },
                new BarKind { Role = "tie", Element = "cột", Dia = tieDia, Count = Math.Max(1, ties), LengthMm = StirrupLength(b, h, cover, tieDia), Shape = "đai kín, móc 135°", Spacing = tieSpacing },
            };
        }

        /// <summary>A slab panel w × l: the bars along x (spaced along y) and along y (spaced along x), inside the cover.</summary>
        public static List<BarKind> SlabBars(double w, double l, (double d, double s) x, (double d, double s) y)
        {
            if (w <= 0 || l <= 0) throw ToolException.Invalid($"{DrawTool}: width and length of the slab must be more than 0 (mm).");
            double cx = Cover("slab", x.d), cy = Cover("slab", y.d);
            int nx = (int)Math.Floor((l - 2 * cx) / x.s + 1e-9) + 1, ny = (int)Math.Floor((w - 2 * cy) / y.s + 1e-9) + 1;
            if (nx < 1 || ny < 1 || w - 2 * cx <= 0 || l - 2 * cy <= 0) throw ToolException.Invalid($"{DrawTool}: the slab is too small for its cover.");
            return new List<BarKind>
            {
                new BarKind { Role = "x", Element = "sàn", Dia = x.d, Count = nx, Spacing = x.s, LengthMm = w - 2 * cx + 2 * MainHook(x.d), Shape = MainShape(x.d) },
                new BarKind { Role = "y", Element = "sàn", Dia = y.d, Count = ny, Spacing = y.s, LengthMm = l - 2 * cy + 2 * MainHook(y.d), Shape = MainShape(y.d) },
            };
        }

        // ------------------------------------------------------------ schedule

        public sealed class ScheduleRow
        {
            public int Mark, Count, Splices;
            public double Dia, LengthMm, TotalM, Kg;
            public string Shape, Grade, Element;
        }

        /// <summary>
        /// One row per bar kind, by mark number: the same mark with the same Ø, length, shape and element adds its counts; the same
        /// mark on another bar kind keeps its own row and is warned about (renumber), never merged. A bar longer than a stock
        /// bar gets its laps added to its length (and weight), said in its shape. Lengths rounded, weights from the rounded length.
        /// </summary>
        public static List<ScheduleRow> Schedule(IEnumerable<RebarMark> marks, List<string> warnings = null)
        {
            var rows = new Dictionary<(int, double, double, string, string), ScheduleRow>();
            foreach (var m in marks)
            {
                if (m == null || m.Count <= 0) continue;
                var key = (m.Mark, m.Dia, RoundLength(m.LengthMm), m.Shape ?? "", m.Element ?? "");
                if (!rows.TryGetValue(key, out var r))
                {
                    var w = WithLaps(m.LengthMm, m.Dia, m.Element);
                    string shape = m.Shape;
                    if (w.splices > 0) shape = $"{m.Shape}; gồm {w.splices} nối chồng {Num(Math.Round(w.lapEach / m.Dia))}d = {Num(RoundLength(w.lapEach))}";
                    rows[key] = r = new ScheduleRow { Mark = m.Mark, Dia = m.Dia, LengthMm = RoundLength(w.lengthMm), Shape = shape, Grade = Grade(m.Dia), Element = m.Element, Splices = w.splices };
                }
                r.Count += m.Count;
            }
            foreach (var g in rows.Values.GroupBy(r => r.Mark).Where(g => g.Count() > 1))
                warnings?.Add($"Bar mark {g.Key} is used for {g.Count()} different bar kinds ({string.Join("; ", g.Select(r => $"{r.Element} Ø{Num(r.Dia)} L={Num(r.LengthMm)}"))}): "
                    + "each kept on its own row, not merged. Tell the user to renumber them (draw_rebar start_mark).");
            foreach (var r in rows.Values)
            {
                r.TotalM = r.LengthMm * r.Count / 1000;
                r.Kg = Math.Round(r.TotalM * KgPerM(r.Dia), H8Numbers.WeightDecimals);
                r.Splices *= r.Count;
            }
            return rows.Values.OrderBy(r => r.Mark).ThenBy(r => r.Dia).ThenBy(r => r.LengthMm).ToList();
        }

        /// <summary>The table: a row per mark, then a total per diameter + grade, then the total weight (waste share null: no waste column).</summary>
        public static List<List<object>> ScheduleTable(IList<ScheduleRow> rows, double? wasteShare)
        {
            var head = new List<object> { "Số hiệu", "Cấu kiện", "Hình dạng", "Đường kính (mm)", "Loại thép", "Dài 1 thanh (mm)", "Số thanh", "Tổng dài (m)", "Khối lượng (kg)", "Số mối nối" };
            if (wasteShare.HasValue) head.Insert(9, $"Khối lượng + hao hụt {Num(wasteShare.Value * 100)}% (kg)");
            var t = new List<List<object>> { head };
            foreach (var r in rows)
            {
                var l = new List<object> { r.Mark, r.Element ?? "", r.Shape ?? "", r.Dia, r.Grade, r.LengthMm, r.Count, Math.Round(r.TotalM, 2), r.Kg, r.Splices };
                if (wasteShare.HasValue) l.Insert(9, Math.Round(r.Kg * (1 + wasteShare.Value), H8Numbers.WeightDecimals));
                t.Add(l);
            }
            foreach (var g in rows.GroupBy(r => (r.Dia, r.Grade)).OrderBy(g => g.Key.Dia))
            {
                double kg = Math.Round(g.Sum(r => r.Kg), H8Numbers.WeightDecimals);
                var l = new List<object> { "", $"Cộng Ø{Num(g.Key.Dia)}", "", g.Key.Dia, g.Key.Grade, "", g.Sum(r => r.Count), Math.Round(g.Sum(r => r.TotalM), 2), kg, g.Sum(r => r.Splices) };
                if (wasteShare.HasValue) l.Insert(9, Math.Round(kg * (1 + wasteShare.Value), H8Numbers.WeightDecimals));
                t.Add(l);
            }
            double all = Math.Round(rows.Sum(r => r.Kg), H8Numbers.WeightDecimals);
            var total = new List<object> { "Tổng", "", "", "", "", "", rows.Sum(r => r.Count), Math.Round(rows.Sum(r => r.TotalM), 2), all, rows.Sum(r => r.Splices) };
            if (wasteShare.HasValue) total.Insert(9, Math.Round(all * (1 + wasteShare.Value), H8Numbers.WeightDecimals));
            t.Add(total);
            return t;
        }

        public static double? WasteShare(double? wastePercent, string tool = ScheduleTool)
        {
            if (wastePercent.HasValue)
            {
                if (wastePercent < 0 || wastePercent > 100) throw ToolException.Invalid($"{tool}: waste_percent must be 0 – 100.");
                return wastePercent.Value / 100;
            }
            return H8Numbers.WasteColumnByDefault ? 0 : (double?)null;
        }

        /// <summary>The next free bar mark: one more than the highest in the drawing (1 when there is none).</summary>
        public static int NextMark(IEnumerable<int> existing) => (existing?.DefaultIfEmpty(0).Max() ?? 0) + 1;

        public static string Num(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
