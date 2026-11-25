using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt G2: the bugs found in check_drawing / fix_drawing and the edges of the card tokens.</summary>
    public class NangCapG2TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingCheckTools.cs");

        private static (double[] min, double[] max) Box(double x, double y, double s = 100) => (new[] { x, y }, new[] { x + s, y + s });

        // ------------------------------------------------------------ G2T-1: far away with few objects

        [Theory]
        [InlineData(10)]
        [InlineData(12)]
        [InlineData(19)]
        [InlineData(40)]
        public void One_far_object_among_few_is_reported(int n)
        {
            var boxes = Enumerable.Range(0, n - 1).Select(i => Box(i * 500, (i % 3) * 400)).ToList();
            boxes.Add(Box(5e7, 5e7)); // 50 km away
            var far = DrawingCheckLogic.FarAway(boxes, 1);
            Assert.Equal(new[] { n - 1 }, far);
        }

        [Fact]
        public void A_normal_small_drawing_reports_nothing_far()
        {
            // a plan with its title 30 m below: inside 20 × the 10 m minimum, never "far"
            var boxes = Enumerable.Range(0, 9).Select(i => Box(i * 1000, 0)).ToList();
            boxes.Add(Box(0, -30000));
            Assert.Empty(DrawingCheckLogic.FarAway(boxes, 1));
            // fewer than FarMinObjects: skipped
            Assert.Empty(DrawingCheckLogic.FarAway(new[] { Box(0, 0), Box(5e7, 5e7) }.ToList(), 1));
            // an empty drawing
            Assert.Empty(DrawingCheckLogic.FarAway(new List<(double[] min, double[] max)>(), 1));
        }

        [Fact]
        public void Inward_ranks_never_lean_towards_an_outlier()
        {
            var v = new List<double> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 1e9 };
            Assert.Equal(8, DrawingCheckLogic.RankInward(v, 0.95, false));
            Assert.Equal(1, DrawingCheckLogic.RankInward(v, 0.05, true));
            Assert.Equal(0, DrawingCheckLogic.RankInward(new List<double>(), 0.5, true));
            Assert.Equal(7, DrawingCheckLogic.RankInward(new List<double> { 7 }, 0.95, false));
        }

        // ------------------------------------------------------------ G2T-2: attribute definitions are not empty texts

        [Fact]
        public void Attribute_definitions_are_not_texts()
        {
            Assert.Contains("e is AttributeDefinition ? null : e is DBText t ? t.TextString", Tools());
        }

        // ------------------------------------------------------------ G2T-3: unloaded on purpose is not missing

        [Theory]
        [InlineData("FileNotFound", true)]
        [InlineData("Unresolved", true)]
        [InlineData("Unloaded", false)]
        [InlineData("Unreferenced", false)]
        [InlineData("Resolved", false)]
        public void Only_xrefs_whose_file_was_not_found_are_missing(string status, bool missing)
        {
            Assert.Equal(missing, DrawingCheckLogic.XrefMissing(status));
            var t = Tools();
            Assert.Contains("DrawingCheckLogic.XrefMissing(btr.XrefStatus.ToString())", t);
            Assert.Contains("missing = def == null || (!def.IsLoaded && !ImageFileFound(def));", t);
        }

        // ------------------------------------------------------------ G2T-4: a fix on a drawing larger than the scan says so

        [Fact]
        public void A_fix_on_a_partly_scanned_drawing_says_so()
        {
            var t = Tools();
            Assert.Contains("if (s.Scanned < s.Total)", t);
            Assert.Contains("res[\"scanned_note\"]", t);
            Assert.Contains("[\"complete\"] = s.Scanned >= s.Total,", t);
        }

        // ------------------------------------------------------------ card tokens: one fix, one set, one layer

        [Fact]
        public void A_token_covers_exactly_one_fix_one_set_and_one_layer()
        {
            var hs = new[] { "2A", "2B", "3C" };
            var t = DrawingCheckLogic.FixToken("layer_zero", "A-WALL", hs);
            Assert.Matches("^[0-9A-F]{16}$", t);
            // order / case of the handles and of the layer name do not matter
            Assert.Equal(t, DrawingCheckLogic.FixToken("LAYER_ZERO", "a-wall", new[] { "3c", "2a", "2B", "2A" }));
            // another fix, another layer, one object more or less: another token (the card asked again)
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("defpoints", "A-WALL", hs));
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("layer_zero", "A-DOOR", hs));
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("layer_zero", "A-WALL", hs.Take(2)));
            Assert.NotEqual(t, DrawingCheckLogic.FixToken("layer_zero", "A-WALL", hs.Concat(new[] { "4D" })));
            // the token is checked before anything is changed (Hủy = nothing changed)
            var tools = Tools();
            int card = tools.IndexOf("RequireCard(ctx, p, \"fix_drawing\"", StringComparison.Ordinal);
            int write = tools.IndexOf("e.UpgradeOpen();", card, StringComparison.Ordinal);
            Assert.True(card > 0 && write > card);
            Assert.True(tools.IndexOf("e.UpgradeOpen();", StringComparison.Ordinal) > card, "no object is opened for write before the card");
        }

        [Fact]
        public void Read_only_check_write_fix_no_macro_2d_plan()
        {
            var t = Tools();
            Assert.Contains("d.Register(new ToolDef(\"check_drawing\", false, CheckDrawing));", t);
            Assert.Contains("d.Register(new ToolDef(\"fix_drawing\", true, FixDrawing));", t);
            Assert.Contains("fix_drawing", MacroLogic.NotRecorded);
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("check_drawing"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("fix_drawing"));
            Assert.False(PlanLogic.Allowed("3d", "check_drawing"));
            Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), "fix_drawing"));
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "check_drawing"));
            // no coordinates in or out: the UCS changes nothing
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("check_drawing", null));
        }
    }
}
