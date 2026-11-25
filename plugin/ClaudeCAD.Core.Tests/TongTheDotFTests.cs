using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test tổng thể đợt F (25/11/2026, docs/ban-giao/test-tong-the-dot-f.md): where F1 → F4 meet each other and the older
    /// parts — labels, UCS classes, self-test steps, file-writing rules, the numbers in the documents.
    /// </summary>
    public class TongTheDotFTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        /// <summary>The tools the F batches added (F1 and F4 added none: F4's Excel is a parameter of four schedules).</summary>
        private static readonly string[] FTools = { "setup_standards", "create_sheet", "insert_library_block", "layer_state", "set_material", "named_view" };

        [Fact]
        public void Every_labelled_tool_has_a_vietnamese_label()
        {
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            var missing = PlanLogic.Groups.Keys.Where(t => !labels.Contains($"[\"{t}\"]")).ToList();
            Assert.True(missing.Count == 0, "no label: " + string.Join(", ", missing));
        }

        [Fact]
        public void Every_f_tool_has_its_intended_ucs_class_and_plan()
        {
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("insert_library_block", null));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("create_sheet", null));
            foreach (var t in new[] { "setup_standards", "layer_state", "set_material", "named_view" }) Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf(t, null));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("setup_standards"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("create_sheet"));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("insert_library_block"));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("layer_state"));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("set_material"));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("named_view"));
            // each work mode of a Full machine gets exactly its plan's F tools
            foreach (var t in FTools)
            {
                var g = PlanLogic.GroupOf(t);
                Assert.Equal(g != ToolGroup.ThreeD, PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), t));
                Assert.Equal(g != ToolGroup.TwoD, PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), t));
                Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "both"), t));
            }
        }

        [Fact]
        public void The_f4_self_test_step_needs_no_other_step()
        {
            var st = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            int a = st.IndexOf("Check(\"Nâng cấp đợt F4", StringComparison.Ordinal);
            int b = st.IndexOf("string versionFile", a, StringComparison.Ordinal);
            Assert.True(a > 0 && b > a);
            var step = st.Substring(a, b - a);
            Assert.Contains("SELFTEST-F4", step);
            Assert.DoesNotContain("CUA-DI", step);
            // the steps run F3, F2, F4 and each makes what it checks
            Assert.True(st.IndexOf("Nâng cấp đợt F3", StringComparison.Ordinal) < st.IndexOf("Nâng cấp đợt F2", StringComparison.Ordinal));
        }

        [Fact]
        public void Every_file_export_asks_before_replacing_a_file_and_takes_overwrite()
        {
            // export_stl, the Excel export and the PNG of named_view: an existing file is kept unless the call says so
            // (nâng cấp đợt F5: the card's Đồng ý; a macro replay still takes overwrite / replace as before)
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, exists,", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs"));
            Assert.Contains("\"overwrite\");", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs"));
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, File.Exists(full),", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExcelExport.cs"));
            var render = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");
            Assert.Contains("\"replace\", \"overwrite\");", render);
            Assert.Contains("if (!oldFlags.Any(f => p.Bool(f, false))) throw ToolException.Invalid(oldRefusal);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ConfirmF5.cs"));
        }

        [Fact]
        public void The_new_default_slabs_are_120_in_every_document()
        {
            Assert.Equal(120, StairLogic.DefaultWaist);
            Assert.Equal(120, RoofLogic.DefaultSlopedThickness);
            Assert.Equal(150, RoofLogic.DefaultFlatThickness);
            var check = Src("docs", "checklist-tong-the.md");
            Assert.DoesNotContain("chiếu nghỉ dày | **150 mm**", check);
            Assert.DoesNotContain("mặt mái) | **150 mm**", check);
            Assert.Contains("# Đợt F (F1 → F4)", check);
            foreach (var f in new[] { "checklist-dot-f2.md", "checklist-dot-f3.md", "checklist-dot-f4.md" })
                Assert.Contains("checklist-tong-the.md", Src("docs", f));
            var skill = Src("skills", "ve-3d-kien-truc", "SKILL.md");
            Assert.Contains("bản thang 120", skill);
            Assert.Contains("bản mái 120", skill);
            Assert.DoesNotContain("bản thang 150", skill);
        }

        [Fact]
        public void The_review_file_names_the_numbers_the_code_uses()
        {
            var d = Src("docs", "dot-f-cho-duyet.md");
            // F2 layer colours, F3 material colours and picture size: in the code, so in the file the owner reviews
            foreach (var kv in F2Numbers.LibraryLayerColors) Assert.Matches(new Regex(Regex.Escape(kv.Key) + "` " + kv.Value + "\\b"), d);
            Assert.Contains($"`KHUNG` màu {F2Numbers.FrameLayerColor}", d);
            Assert.Contains($"`KHUNG-VP` {F2Numbers.ViewportLayerColor}", d);
            foreach (var p in MaterialLogic.Presets) Assert.Contains($"{p.R},{p.G},{p.B}", d);
            Assert.Contains(MaterialLogic.DefaultImageSize.ToString(), d);
            Assert.Contains("384–1568", d);
        }
    }
}
