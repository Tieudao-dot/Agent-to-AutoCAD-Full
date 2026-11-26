using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test tổng thể đợt H phần 2 (H6 → H9): docs/ban-giao/test-tong-the-dot-h-phan-2.md.</summary>
    public class TongTheDotHPhan2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly string[] TwoD = { "terrain", "draw_rebar", "rebar_schedule" };
        private static readonly string[] ThreeD = { "draw_mep", "mep_report", "build_footing" };

        [Fact]
        public void The_six_new_tools_are_blocked_by_plan_the_same_way()
        {
            foreach (var t in TwoD)
            {
                Assert.True(PlanLogic.Allowed(PlanLogic.Plan2D, t), t);
                Assert.False(PlanLogic.Allowed(PlanLogic.Plan3D, t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.PlanFull, t), t);
            }
            foreach (var t in ThreeD)
            {
                Assert.False(PlanLogic.Allowed(PlanLogic.Plan2D, t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.Plan3D, t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.PlanFull, t), t);
            }
        }

        [Fact]
        public void Each_new_tool_is_registered_labelled_in_both_languages_and_classed_for_the_ucs()
        {
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            var en = Src("plugin", "ClaudeCAD.Plugin", "Core", "LangEn.cs");
            var ucs = Src("plugin", "ClaudeCAD.Plugin", "Core", "Ucs.cs");
            var drawing = string.Join("\n", new[] { "TerrainTools.cs", "MepTools.cs", "StructureTools.cs" }.Select(f => Src("plugin", "ClaudeCAD.Plugin", "Drawing", f)));
            foreach (var t in TwoD.Concat(ThreeD))
            {
                var m = System.Text.RegularExpressions.Regex.Match(labels, $"\\[\"{t}\"\\] = \"([^\"]+)\"");
                Assert.True(m.Success, t);
                Assert.Contains("\"" + m.Groups[1].Value + "\"", en); // the English label of the tool
                Assert.Contains($"new ToolDef(\"{t}\"", drawing);
                // the two reports read no coordinates (Ref by default: a table_position is a UCS point); the four that draw are classed
                if (t != "mep_report" && t != "rebar_schedule") Assert.Contains($"\"{t}\"", ucs);
            }
            // writes go through the dispatcher (Chỉ xem refuses them); the two reports read, except a table put in the drawing
            Assert.Contains("new ToolDef(\"mep_report\", false", drawing);
            Assert.Contains("new ToolDef(\"rebar_schedule\", true, RebarSchedule, readsWhen: p => !p.Has(\"table_position\"))", drawing);
            foreach (var t in new[] { "terrain", "draw_mep", "build_footing", "draw_rebar" }) Assert.Contains($"new ToolDef(\"{t}\", true", drawing);
        }

        [Fact]
        public void Drawing_texts_and_tables_of_h6_to_h8_stay_vietnamese()
        {
            foreach (var f in new[] { "Core/Terrain.cs", "Drawing/TerrainTools.cs", "Core/Mep.cs", "Drawing/MepTools.cs", "Core/Rebar.cs", "Drawing/StructureTools.cs" })
            {
                var s = Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(f.Split('/')).ToArray());
                Assert.DoesNotContain("Tr(", s);
                Assert.DoesNotContain("in Vietnamese", s);
            }
        }

        [Fact]
        public void The_six_unsure_numbers_carry_the_tcvn_note_where_the_machine_reads_them()
        {
            var mepDesc = Src("agent", "src", "tools", "mep.ts");
            var structDesc = Src("agent", "src", "tools", "structure.ts");
            var terrDesc = Src("agent", "src", "tools", "terrain.ts");
            // the 3D tools: ve-mep / ve-ket-cau are 2D skills (not loaded on a 3D machine), so the descriptions say it
            Assert.Contains("the least slopes are defaults: the engineer checks them against the TCVN in force", mepDesc);
            Assert.Contains("the engineer checks them against the TCVN in force", structDesc);
            Assert.Contains("the engineer checks them against the TCVN in force", terrDesc);
            var ketcau = Src("skills", "ve-ket-cau", "SKILL.md");
            var mep = Src("skills", "ve-mep", "SKILL.md");
            var hatang = Src("skills", "ve-ha-tang", "SKILL.md");
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", ketcau);
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", mep);
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", hatang);
        }

        [Fact]
        public void The_approved_defaults_of_part_2()
        {
            Assert.Equal(2.5, MepLogic.DefaultSlope(H7Numbers.DrainFloorMm));
            Assert.Equal(2.0, MepLogic.DefaultSlope(H7Numbers.DrainWcMm));
            Assert.Equal(50, H6Numbers.PlanTickM);
            Assert.Equal(20, H6Numbers.StakeDetailM);
            Assert.False(H7Numbers.WasteColumnByDefault);
            Assert.False(H8Numbers.WasteColumnByDefault);
            Assert.Null(RebarLogic.WasteShare(null));
            Assert.Null(MepLogic.WasteShare(null));
            Assert.Equal(1000, H6Numbers.UnitsPerMetre); // restored between calls (H6T-3)
        }
    }
}
