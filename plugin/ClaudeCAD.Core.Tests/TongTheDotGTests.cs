using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test tổng thể đợt G (docs/ban-giao/test-tong-the-dot-g.md): G1 → G5 against each other and the older parts — every
    /// table names the new tools, the parts of one batch do not trip the others, the output files never share a name.
    /// </summary>
    public class TongTheDotGTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        /// <summary>The tools the G batches added (G4 is an action of named_view).</summary>
        private static readonly string[] GTools = { "create_drawing_set", "check_drawing", "fix_drawing", "cost_estimate", "trace_image" };

        [Fact]
        public void Every_g_tool_is_in_every_table_once()
        {
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            var prompt = Src("agent", "src", "agent", "systemPrompt.ts");
            var index = Src("agent", "src", "tools", "index.ts");
            var always = Regex.Match(index, @"ALWAYS_LOADED_TOOLS: readonly string\[\] = \[(.*?)\];", RegexOptions.Singleline).Groups[1].Value;
            var tools = string.Concat(Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agent", "src", "tools")), "*.ts").Select(File.ReadAllText));
            foreach (var t in GTools)
            {
                Assert.NotNull(PlanLogic.GroupOf(t));
                Assert.Single(Regex.Matches(labels, Regex.Escape($"[\"{t}\"]")));
                Assert.Single(Regex.Matches(tools, $"cadTool\\(b, '{t}'|tool\\('{t}'"));
                Assert.DoesNotContain($"'{t}'", always); // deferred: the always-loaded part does not grow
                Assert.Contains(t, prompt);
            }
            // the plan of each: G1 / G3 every plan (their parts are checked inside), G2 / G5 2D
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("create_drawing_set"));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("cost_estimate"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("check_drawing"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("fix_drawing"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("trace_image"));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("named_view")); // G4 render
            // the file-overwrite card of F5 for every new tool that writes a file over an existing one
            var conf = Src("agent", "src", "agent", "confirmation.ts");
            var f5 = Regex.Match(conf, @"F5_CARD_TOOLS = new Set\(\[(.*?)\]\);", RegexOptions.Singleline).Groups[1].Value;
            foreach (var t in new[] { "create_drawing_set", "cost_estimate", "check_drawing", "fix_drawing", "named_view" }) Assert.Contains($"'{t}'", f5);
            // a repair and a trace are never replayed on another drawing
            Assert.Contains("fix_drawing", MacroLogic.NotRecorded);
            Assert.Contains("trace_image", MacroLogic.NotRecorded);
            // one self-test step per batch
            var st = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            foreach (var n in new[] { "G1", "G2", "G3", "G4", "G5" }) Assert.Single(Regex.Matches(st, "Check\\(\"Nâng cấp đợt " + n + ":"));
            // the install notes have a row each
            var readme = Src("bo-cai", "README.md");
            foreach (var n in new[] { "G1", "G2", "G3", "G4", "G5" }) Assert.Contains("Nâng cấp đợt " + n, readme);
        }

        [Fact]
        public void The_files_of_the_five_batches_never_share_a_name()
        {
            var names = new[]
            {
                DrawingSetLogic.PdfName(@"C:\a\nha.dwg"),
                EstimateLogic.DefaultFile(@"C:\a\nha.dwg"),
                XlsxLogic.DefaultName(@"C:\a\nha.dwg", "check_drawing"),
                XlsxLogic.DefaultName(@"C:\a\nha.dwg", "block_schedule"),
                XlsxLogic.DefaultName(@"C:\a\nha.dwg", "area_schedule"),
                XlsxLogic.DefaultName(@"C:\a\nha.dwg", "volume_schedule"),
                XlsxLogic.DefaultName(@"C:\a\nha.dwg", "check_interference"),
                "nha-" + RenderLogic.Tail(null) + ".png",
                TraceLogic.CopyName("nha", "x.png", "0123abcd"),
            };
            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void The_perspective_sheet_of_a_drawing_set_is_not_a_wrong_viewport_scale_for_the_check()
        {
            Assert.True(DrawingCheckLogic.IsPlanView(0, 0, 1));
            Assert.True(DrawingCheckLogic.IsPlanView(0, 0, 0));
            var d = DrawingSetLogic.PerspectiveDirection;
            Assert.False(DrawingCheckLogic.IsPlanView(d[0], d[1], d[2]));
            Assert.Contains("DrawingCheckLogic.IsPlanView(vp.ViewDirection.X", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingCheckTools.cs"));
            // the sheets of the set are what the check expects: frame + title block, standard scale, locked viewport
            Assert.Contains("[\"locked\"] = true", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingSetTools.cs"));
            Assert.All(new[] { 100, 125, 150, 200 }, n => Assert.Contains(n, F2Numbers.StandardScales));
            // the layers the set draws on are layers of the standard (no "nonstandard layer" for its views)
            foreach (var l in new[] { SectionLogic.OutlineLayer, SectionLogic.HatchLayer, SectionLogic.BehindLayer, SectionLogic.ElevationLayer, F2Numbers.FrameLayer, F2Numbers.ViewportLayer, G5Numbers.Layer })
                Assert.True(DrawingCheckLogic.IsStandardLayer(l), l);
        }

        [Fact]
        public void Room_and_layer_areas_leave_out_the_views_drawn_from_the_3d_model()
        {
            foreach (var l in new[] { "A-SECT", "a-sect-pat", "A-SECT-BACK", "A-ELEV" }) Assert.True(Util2DLogic.IsDerivedViewLayer(l), l);
            foreach (var l in new[] { "A-FLOR", "A-SLAB", "A-WALL", "X-ANH-GOC", "0", null }) Assert.False(Util2DLogic.IsDerivedViewLayer(l), l ?? "null");
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("if (handles == null && layers == null && Util2DLogic.IsDerivedViewLayer(e.Layer))", t);
            Assert.Contains("res[\"left_out_layers\"]", t);
        }
    }
}
