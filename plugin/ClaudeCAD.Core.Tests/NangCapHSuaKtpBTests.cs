using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa lỗi KTP phần B (H7 + KTP-15) sau kiểm tra độc lập đợt H phần 2: docs/ban-giao/nang-cap-h-sua-ktp-b.md.</summary>
    public class NangCapHSuaKtpBTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void Ktp13_a_duct_under_a_beam_is_measured_by_half_its_height_not_half_its_diagonal()
        {
            // a duct 600 × 300 along x, its top 100 below the beam's bottom (as under_beams puts it)
            var a = new[] { 0.0, 0, 0 }; var b = new[] { 6000.0, 0, 0 };
            var min = new[] { 2000.0, -2000, 250 }; var max = new[] { 2300.0, 2000, 850 };
            Assert.Equal(100, MepLogic.SectionBoxGap(a, b, true, 600, 300, min, max), 6);
            Assert.Equal(0, MepLogic.PipeBoxGap(a, b, Math.Sqrt(600 * 600 + 300 * 300) / 2, min, max), 6); // the old reach: "sát 0"
            // beside a column: half its width
            Assert.Equal(200, MepLogic.SectionBoxGap(a, b, true, 600, 300, new[] { 1000.0, 500, -500 }, new[] { 1300.0, 800, 500 }), 6);
            // a round pipe: its radius, as before
            Assert.Equal(250 - 55, MepLogic.SectionBoxGap(a, b, false, 110, 0, new[] { 0.0, -500, 250 }, new[] { 100.0, 500, 300 }), 6);
            // two ducts one over the other: each reaches by half its height
            Assert.Equal(1000 - 150 - 100, MepLogic.SectionSectionGap(a, b, true, 600, 300, new[] { 0.0, 0, 1000 }, new[] { 6000.0, 0, 1000 }, true, 400, 200), 6);
            // the solid check is still gated by the safe (diagonal) reach: a real clash is never skipped
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("MepLogic.PipeBoxGap(a.r.A, a.r.B, Reach(a.r, f)", tools);
            Assert.Contains("if (!hit && IsStraight(a.r))", tools);
        }

        [Fact]
        public void Ktp14_two_routes_meeting_end_to_end_are_a_joint()
        {
            Assert.True(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.0, 3000, 0 }, 1));
            Assert.True(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.5, 0, 0 }, new[] { 8000.0, 0, 0 }, 1));
            Assert.False(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4100.0, 0, 0 }, new[] { 8000.0, 0, 0 }, 1));
            Assert.Contains("MepLogic.EndToEnd(a.r.A, a.r.B, b.r.A, b.r.B, H7Numbers.JoinToleranceMm / f)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void Ktp16_a_branch_joins_its_own_system_and_the_checklist_gives_the_main_pipe_height()
        {
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("if (main.Mark.System != sys.Key)", tools);
            Assert.Contains("there the centre line is at x", tools);
            var list = Src("docs", "checklist-dot-h7.md");
            Assert.Contains("D60 từ (1000,0) tới (1000,-2000), cao độ 2780", list);
            Assert.Contains("**khác hệ**", list);
            // 2780 really is the main drain's centre at x = 1000 (2800, falling 2 %)
            Assert.Equal(2780, MepLogic.Sloped(new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 } }, 2800, 2)[1][2], 6);
        }

        [Fact]
        public void Ktp17_the_bend_radius_is_more_than_half_the_section_across_the_bend()
        {
            // a duct 200 × 500 turning up: the bend is across its height (250); in plan: across its width (100)
            var up = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 2000.0, 0, 0 }, new[] { 2000.0, 0, 2000 } }, 600).Single(x => x.Kind == "elbow");
            Assert.Equal(250, MepLogic.HalfInBendPlane(up, true, 200, 500), 6);
            var plan = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 2000.0, 0, 0 }, new[] { 2000.0, 2000, 0 } }, 600).Single(x => x.Kind == "elbow");
            Assert.Equal(100, MepLogic.HalfInBendPlane(plan, true, 200, 500), 6);
            Assert.Equal(55, MepLogic.HalfInBendPlane(plan, false, 110, 0), 6); // D110: bend_radius 20 is refused
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("is not more than half the section in the plane of a bend", tools);
            Assert.Contains("bend = H7Numbers.DuctBendFactor * 2 * need;", tools); // the default takes the side across the bend
        }

        [Fact]
        public void Ktp15_a_replayed_branch_joins_the_main_pipe_the_macro_drew_again()
        {
            Assert.True(MacroLogic.IsHandleKey("connect_to"));
            Assert.Contains("connect_to: handle.optional()", Src("agent", "src", "tools", "mep.ts"));
        }

        [Fact]
        public void Ktp27_28_29_excel_names_system_names_and_mixed_points()
        {
            Assert.Equal("ban-ve-ong-mep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", "mep_report"));
            Assert.Equal("ban-ve-va-cham-ong-mep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", F4Numbers.MepClashKey));
            Assert.Equal("ban-ve-thong-ke-thep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", "rebar_schedule"));
            Assert.Equal("Thống kê ống MEP", F4Numbers.SheetName["mep_report"]);
            foreach (var v in F4Numbers.SheetName.Values) Assert.True(v.Length <= F4Numbers.SheetNameMax, v);
            Assert.Equal("Cấp nước", MepLogic.SystemName("supply"));
            Assert.Equal("Ống gió cấp", MepLogic.SystemName("duct_supply"));
            Assert.Equal("xyz", MepLogic.SystemName("xyz"));
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("MepLogic.SystemName((string)r[\"system\"])", tools);
            Assert.Contains("}, res, F4Numbers.MepClashKey);", tools);
            Assert.Contains("some points have a height (x, y, z) and some not (x, y)", tools);
            Assert.Contains("leave out elevation and under_beams", tools);
        }

        [Fact]
        public void A_structure_layer_is_named_so_s_dash_only_as_its_prefix()
        {
            Assert.True(MepLogic.IsStructureLayer("S-BEAM"));
            Assert.True(MepLogic.IsStructureLayer("S-FOOT-3D"));
            Assert.True(MepLogic.IsStructureLayer("A-COLUMN"));
            Assert.False(MepLogic.IsStructureLayer("A-WALLS-NEW"));
            Assert.False(MepLogic.IsStructureLayer("P-DRAIN-3D"));
        }
    }
}
