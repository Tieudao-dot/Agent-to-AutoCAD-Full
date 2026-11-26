using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H8 (docs/ban-giao/nang-cap-h8.md): footings, rebar details and the rebar schedule (Core/Rebar.cs).</summary>
    public class NangCapH8Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void The_tools_sit_in_their_plans_with_labels_ucs_and_the_card()
        {
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("build_footing"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("draw_rebar"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("rebar_schedule"));
            Assert.False(PlanLogic.Allowed("2d", "build_footing"));
            Assert.False(PlanLogic.Allowed("3d", "draw_rebar"));
            Assert.True(PlanLogic.Allowed("2d3d", "rebar_schedule"));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("build_footing", new Dictionary<string, object>()));
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("draw_rebar", new Dictionary<string, object>()));
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            foreach (var t in new[] { "build_footing", "draw_rebar", "rebar_schedule" }) Assert.Contains($"[\"{t}\"] = \"", labels);
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StructureTools.cs");
            Assert.Contains("d.Register(new ToolDef(\"rebar_schedule\", true, RebarSchedule, readsWhen: p => !p.Has(\"table_position\")));", tools);
            Assert.Contains("ExportExcel(ctx, p, tool, () => table, res);", tools);
            Assert.Contains("RegisterStructure(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            // the structural layers are not "outside the standard" for check_drawing (like H6T-4 / H7T-06)
            foreach (var l in new[] { H8Numbers.FootingLayer, H8Numbers.LeanLayer, H8Numbers.ConcreteLayer, H8Numbers.RebarLayer, H8Numbers.TextLayer })
                Assert.True(DrawingCheckLogic.IsStandardLayer(l), l);
        }

        [Fact]
        public void Bars_are_read_as_the_user_writes_them_and_never_chosen()
        {
            Assert.Equal((2, 18.0), RebarLogic.ParseBars("2Ø18", "bottom"));
            Assert.Equal((4, 16.0), RebarLogic.ParseBars(" 4 D 16 ", "bottom"));
            Assert.Equal((8, 20.0), RebarLogic.ParseBars("8phi20", "bars"));
            Assert.Contains("does not choose bars", Assert.Throws<ToolException>(() => RebarLogic.ParseBars(null, "bottom")).Message);
            Assert.Contains("not a standard bar", Assert.Throws<ToolException>(() => RebarLogic.ParseBars("2Ø17", "bottom")).Message);
            Assert.Equal((8.0, 200.0), RebarLogic.ParseSpaced("Ø8a200", "x"));
            Assert.Equal((6.0, 150.0), RebarLogic.ParseSpaced("D6@150", "stirrup"));
            Assert.Throws<ToolException>(() => RebarLogic.ParseSpaced("Ø8a20", "x"));
            Assert.Equal("CB240-T", RebarLogic.Grade(10));
            Assert.Equal("CB400-V", RebarLogic.Grade(12));
            Assert.Equal(new double[] { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32 }, H8Numbers.Diameters);
            // the tool descriptions say it too (source)
            Assert.Contains("never choose them", Src("agent", "src", "tools", "structure.ts"));
        }

        [Fact]
        public void Cover_anchorage_laps_and_hooks_are_the_approved_numbers()
        {
            Assert.Equal(20, RebarLogic.Cover("slab", 8));
            Assert.Equal(25, RebarLogic.Cover("beam", 18));
            Assert.Equal(32, RebarLogic.Cover("column", 32)); // never under the bar
            Assert.Equal(40, RebarLogic.Cover("footing", 16, lean: true));
            Assert.Equal(70, RebarLogic.Cover("footing", 16, lean: false));
            Assert.Equal(540, RebarLogic.Anchor(18));
            Assert.Equal(720, RebarLogic.Lap(18, "beam"));
            Assert.Equal(700, RebarLogic.Lap(20, "column"));
            Assert.Null(RebarLogic.CheckLength("anchor", 540, 18));
            Assert.Contains("under its least value 270 mm", RebarLogic.CheckLength("anchor", 200, 18)); // 15d
            Assert.Contains("least value 250 mm", RebarLogic.CheckLength("lap", 150, 8));               // 250 mm > 20d
            Assert.Equal(Math.PI * (2.5 * 8 + 8) / 2 + 3 * 8, RebarLogic.MainHook(8), 9);
            Assert.Equal(0, RebarLogic.MainHook(16));
            Assert.Equal(75, RebarLogic.StirrupHook(6));
            Assert.Equal(75, RebarLogic.StirrupHook(10)); // max(6d, 75): 60 < 75
            Assert.Equal(96, RebarLogic.StirrupHook(16)); // 6d
            Assert.Equal(0.617, RebarLogic.KgPerM(10), 9);
            Assert.Equal(1.99908, RebarLogic.KgPerM(18), 9);
        }

        [Fact]
        public void A_beam_has_its_bars_running_into_the_supports_and_stirrups_by_zones()
        {
            var warn = new List<string>();
            var k = RebarLogic.BeamBars(220, 400, 5000, (2, 18), (2, 16), 6, 150, 200, null, warn);
            Assert.Empty(warn);
            Assert.Equal(5000 + 2 * 540 + 2 * 180, k[0].LengthMm, 9); // ribbed: 30d into each support + an L bend of 10d
            Assert.Equal(5000 + 2 * 480 + 2 * 160, k[1].LengthMm, 9);
            Assert.Equal("bẻ L 10d hai đầu", k[0].Shape);
            Assert.Equal("2Ø18 L=6440", RebarLogic.Label(k[0].Count, k[0].Dia, k[0].LengthMm));
            // a plain main bar keeps its 180° hooks, no L bend
            var pl = RebarLogic.BeamBars(220, 400, 5000, (2, 10), (2, 10), 6, 150, 200, null, new List<string>());
            Assert.Equal(5000 + 2 * 300 + 2 * RebarLogic.MainHook(10), pl[0].LengthMm, 9);
            Assert.Equal(10 * 16, RebarLogic.AnchorEnd(16), 9);
            Assert.Equal(2, k[0].Count);
            Assert.Equal("stirrup", k[2].Role);
            double cover = 25, w = 220 - 2 * cover - 6, h = 400 - 2 * cover - 6;
            Assert.Equal(2 * (w + h) + 2 * 75, k[2].LengthMm, 9);
            var pos = RebarLogic.StirrupPositions(5000, 150, 200);
            Assert.Equal(k[2].Count, pos.Count);
            Assert.Equal(50, pos[0], 9);
            Assert.True(pos.Zip(pos.Skip(1), (a, b) => b - a).Take(5).All(d => Math.Abs(d - 150) < 1e-9), "end zone a150");
            Assert.Contains(pos.Zip(pos.Skip(1), (a, b) => b - a), d => Math.Abs(d - 200) < 1e-9);
            Assert.True(pos.Last() <= 4950 + 1e-9);
            // a short anchorage is warned about
            RebarLogic.BeamBars(220, 400, 5000, (2, 18), (2, 16), 6, 150, 200, 150, warn);
            Assert.NotEmpty(warn);
            // bars that do not fit are refused, not squeezed
            Assert.Contains("do not fit in one row", Assert.Throws<ToolException>(() => RebarLogic.BeamBars(200, 400, 5000, (6, 25), (2, 16), 6, 150, 200, null, new List<string>())).Message);
        }

        [Fact]
        public void A_column_and_a_slab_count_their_bars()
        {
            var warn = new List<string>();
            var c = RebarLogic.ColumnBarKinds(300, 300, 3300, (8, 20), 6, 200, null, warn);
            Assert.Equal(3300 + 700, c[0].LengthMm, 9);
            Assert.Equal(8, c[0].Count);
            Assert.Equal((int)Math.Floor((3300 - 100) / 200.0) + 1, c[1].Count);
            var pts = RebarLogic.ColumnBars(8, 300, 300, 25, 6, 20);
            Assert.Equal(8, pts.Count);
            Assert.Equal(8, pts.Select(q => $"{q[0]:0.#},{q[1]:0.#}").Distinct().Count());
            Assert.Throws<ToolException>(() => RebarLogic.ColumnBars(7, 300, 300, 25, 6, 20));
            var s = RebarLogic.SlabBars(4000, 3000, (8, 200), (8, 200));
            Assert.Equal((int)Math.Floor((3000 - 40) / 200.0) + 1, s[0].Count); // bars along x, spaced along y
            Assert.Equal(4000 - 40 + 2 * RebarLogic.MainHook(8), s[0].LengthMm, 9); // plain Ø8: hooks
            Assert.Equal("Ø8a200", s[0].Label);
        }

        [Fact]
        public void The_schedule_rounds_weighs_and_totals_per_diameter_without_waste()
        {
            var marks = new List<RebarMark>
            {
                new RebarMark { Mark = 1, Dia = 18, Shape = "thẳng", Element = "dầm", LengthMm = 6083, Count = 2 },
                new RebarMark { Mark = 2, Dia = 16, Shape = "thẳng", Element = "dầm", LengthMm = 5960, Count = 2 },
                new RebarMark { Mark = 3, Dia = 6, Shape = "đai", Element = "dầm", LengthMm = 1186, Count = 30 },
                new RebarMark { Mark = 1, Dia = 18, Shape = "thẳng", Element = "dầm", LengthMm = 6083, Count = 2 }, // the same mark again: counts add
                new RebarMark { Mark = 4, Dia = 20, Shape = "thẳng", Element = "cột", LengthMm = 12500, Count = 8 },
            };
            var rows = RebarLogic.Schedule(marks);
            Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.Mark).ToArray());
            var r1 = rows[0];
            Assert.Equal(6080, r1.LengthMm); // rounded to 10 mm
            Assert.Equal(4, r1.Count);
            Assert.Equal(Math.Round(6.08 * 4 * 0.00617 * 18 * 18, 2), r1.Kg, 9);
            Assert.Equal(8, rows[3].Splices); // 12.5 m from 11.7 m stock bars: one splice each
            Assert.Equal(12500 + 35 * 20, rows[3].LengthMm); // its lap (column 35d) is added to the length and the weight
            Assert.Equal(Math.Round(13.2 * 8 * 0.00617 * 20 * 20, 2), rows[3].Kg, 9);
            Assert.Contains("gồm 1 nối chồng 35d = 700", rows[3].Shape);
            var t = RebarLogic.ScheduleTable(rows, null);
            Assert.Equal(10, t[0].Count);
            Assert.Equal("Tổng", t[t.Count - 1][0]);
            Assert.Contains(t, l => Equals(l[1], "Cộng Ø18"));
            Assert.Equal(rows.Count + 4, XlsxLogic.DataRows(t)); // marks + one total per diameter (4) are data, the last total is not
            Assert.Equal(Math.Round(rows.Sum(r => r.Kg), 2), (double)t[t.Count - 1][8], 9);
            Assert.False(H8Numbers.WasteColumnByDefault);
            Assert.Null(RebarLogic.WasteShare(null));
            var w = RebarLogic.ScheduleTable(rows, 0.03);
            Assert.Equal(11, w[0].Count);
            Assert.Contains("hao hụt 3%", (string)w[0][9]);
            Assert.Equal(1, RebarLogic.NextMark(new int[0]));
            Assert.Equal(8, RebarLogic.NextMark(new[] { 3, 7, 1 }));
        }

        [Fact]
        public void A_mark_on_two_bar_kinds_is_warned_about_and_never_merged()
        {
            var warn = new List<string>();
            var rows = RebarLogic.Schedule(new List<RebarMark>
            {
                new RebarMark { Mark = 5, Dia = 18, Shape = "thẳng", Element = "dầm", LengthMm = 6440, Count = 2 },
                new RebarMark { Mark = 5, Dia = 25, Shape = "thẳng", Element = "dầm", LengthMm = 7000, Count = 3 },
                new RebarMark { Mark = 6, Dia = 16, Shape = "thẳng", Element = "dầm", LengthMm = 6280, Count = 2 },
                new RebarMark { Mark = 6, Dia = 16, Shape = "thẳng", Element = "dầm", LengthMm = 6280, Count = 2 }, // the same kind twice: adds
            }, warn);
            Assert.Equal(3, rows.Count);
            Assert.Equal(new[] { 18.0, 25.0 }, rows.Where(r => r.Mark == 5).Select(r => r.Dia).ToArray());
            Assert.Equal(Math.Round(7.0 * 3 * 0.00617 * 25 * 25, 2), rows.Single(r => r.Dia == 25).Kg, 9);
            Assert.Equal(4, rows.Single(r => r.Mark == 6).Count);
            Assert.Single(warn);
            Assert.Contains("Bar mark 5", warn[0]);
            Assert.Contains("renumber", warn[0]);
        }

        [Fact]
        public void A_bar_longer_than_a_stock_bar_gets_its_laps_added()
        {
            Assert.Equal((0, 720.0, 11000.0), RebarLogic.WithLaps(11000, 18, "dầm"));
            var b = RebarLogic.WithLaps(12000, 18, "dầm"); // 40d for a beam
            Assert.Equal(1, b.splices);
            Assert.Equal(12720, b.lengthMm, 9);
            Assert.Equal(700, RebarLogic.WithLaps(12000, 20, "cột").lapEach, 9); // 35d for a column
            // the laps make the bar longer: counted again (23 000 + 1 lap of 800 = 23 800 > 23 400: 2 splices)
            var l = RebarLogic.WithLaps(23000, 20, "dầm");
            Assert.Equal(2, l.splices);
            Assert.Equal(23000 + 2 * 800, l.lengthMm, 9);
        }

        [Fact]
        public void Sample_sizes_and_the_unsure_numbers_are_said()
        {
            Assert.Equal(1200, H8Numbers.PadSize);
            Assert.Equal(350, H8Numbers.PadHeight);
            Assert.Equal(100, H8Numbers.LeanThickness);
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StructureTools.cs");
            Assert.Contains("cỡ mẫu, kỹ sư quyết", t);
            Assert.Contains("the engineer checks them against the TCVN in force", t);
            var skill = Src("skills", "ve-ket-cau", "SKILL.md");
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", skill);
            Assert.Contains("không tự chọn", skill);
            foreach (var n in new[] { "20", "25", "40", "70", "30d", "40d", "35d", "0,00617", "11,7 m" }) Assert.Contains(n, skill);
        }
    }
}
