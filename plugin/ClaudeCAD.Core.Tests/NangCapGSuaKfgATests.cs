using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa các lỗi kiểm tra độc lập đợt F + G, phần G2 (docs/ban-giao/nang-cap-g-sua-kfg-a.md): KFG-07, 08, 15, 16, 17, 18 and
    /// the suspicions on the sheet viewport, a dimension text " " and the lines of the drawing set seen as copies.
    /// </summary>
    public class NangCapGSuaKfgATests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingCheckTools.cs");

        private static DrawingCheckLogic.DupItem D(string g, params double[] n) => new DrawingCheckLogic.DupItem { Group = g, Nums = n };

        // ------------------------------------------------------------ KFG-15: within 0.1 mm, not on a grid

        [Fact]
        public void Two_lines_0_002_mm_apart_across_a_grid_line_are_copies()
        {
            double tol = G2Numbers.DuplicateTolMm;
            // before: a grid of 0.1 put 1000.049 and 1000.051 in two cells (two keys), so no copy was found
            var items = new List<DrawingCheckLogic.DupItem>
            {
                D("M|A|line|", 1000.049, 0, 0, 4000, 0, 0),
                D("M|A|line|", 1000.051, 0, 0, 4000, 0, 0),   // 0.002 away: a copy
                D("M|A|line|", 1000.2, 0, 0, 4000, 0, 0),     // 0.15 from the first: not a copy
                D("M|B|line|", 1000.049, 0, 0, 4000, 0, 0),   // another layer: not compared
                D("M|A|line|", 1000.049, 0, 0, 4000.5, 0, 0), // one end 0.5 away: not a copy
            };
            Assert.Equal(new List<int> { 1 }, DrawingCheckLogic.DuplicateIndices(items, tol));
            // exactly at the tolerance still counts (≤ 0.1 mm), negative numbers too
            Assert.Equal(new List<int> { 1 }, DrawingCheckLogic.DuplicateIndices(new[] { D("g", -5.0), D("g", -5.1) }, tol));
            Assert.Empty(DrawingCheckLogic.DuplicateIndices(new[] { D("g", -5.0), D("g", -5.11) }, tol));
            // angles / scales are exact parts, not compared with the length tolerance
            Assert.NotEqual(DrawingCheckLogic.Exact(0.0), DrawingCheckLogic.Exact(0.05));
            Assert.Contains("DrawingCheckLogic.DuplicateIndices(dupKeys, dupTol)", Tools);
        }

        // ------------------------------------------------------------ nghi vấn G1 × G2: the drawing set's lines

        [Fact]
        public void Lines_of_the_views_from_the_model_are_never_called_copies()
        {
            Assert.True(Util2DLogic.IsDerivedViewLayer(SectionLogic.ElevationLayer));
            Assert.True(Util2DLogic.IsDerivedViewLayer(SectionLogic.OutlineLayer));
            Assert.Contains("if (Util2DLogic.IsDerivedViewLayer(e.Layer) || IsViewLine(e)) return null;", Tools); // lượt 2: + the mark (KFG-32)
        }

        // ------------------------------------------------------------ KFG-07: hatches that never had a boundary

        [Fact]
        public void Hatches_of_the_views_and_hatches_drawn_without_boundary_are_not_unassociated()
        {
            Assert.Contains("!h.Associative && !Util2DLogic.IsDerivedViewLayer(layer) && !IsPlainHatch(ctx, h)", Tools);
            var draw = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawTools.cs");
            Assert.Equal(2, draw.Split(new[] { "MarkPlainHatch(ctx, hatch);" }, StringSplitOptions.None).Length - 1); // pick point + points
            Assert.Contains("if (h.Associative) return;", Tools); // an associative hatch is never marked
        }

        // ------------------------------------------------------------ KFG-08: a default name with the user's viewport

        [Fact]
        public void A_default_layout_with_a_viewport_and_another_object_is_a_sheet()
        {
            // Layout2 with the user's viewport and a line / text / title block: checked in full
            Assert.False(DrawingCheckLogic.IsUnusedTab("Layout2", 1, false));
            // a layout of another name is always a sheet
            Assert.False(DrawingCheckLogic.IsUnusedTab("BV-01", 0, false));
            var t = Tools;
            Assert.Contains("if (DrawingCheckLogic.IsUnusedTab(sp.layout, others, asked))", t);
            Assert.Contains("(others > 0 || ownViewports > 0) && !hasTitle", t);
            // the sheet's own viewport: Layout.GetViewports first, else the first of the block
            Assert.Contains("GetViewports()", t);
            Assert.Contains("bool isSheet = sheetVp.IsNull ? firstViewport : id == sheetVp;", t);
        }

        [Fact]
        public void A_default_layout_with_only_a_viewport_is_named_in_a_note_not_reported()
        {
            // the viewport AutoCAD makes when Layout1 is first opened: not reported (no title block / unlocked / scale)
            Assert.True(DrawingCheckLogic.IsUnusedTab("Layout1", 0, false));
            Assert.True(DrawingCheckLogic.IsUnusedTab("layout 2", 0, false));
            // the user says it is a sheet: checked in full
            Assert.False(DrawingCheckLogic.IsUnusedTab("Layout1", 0, true));
            var note = DrawingCheckLogic.UnusedTabsNote(new[] { "Layout1", "Layout2" });
            Assert.StartsWith("Layout1, Layout2: only a viewport", note);
            Assert.Contains("sheets", note);
            var t = Tools;
            Assert.Contains("if (ownViewports > 0) s.UnusedTabs.Add(sp.layout);", t);
            Assert.Contains("s.Notes.Add(DrawingCheckLogic.UnusedTabsNote(s.UnusedTabs));", t);
            Assert.Contains("p.StringsOpt(\"sheets\")", t);
            var ts = Src("agent", "src", "tools", "drawingCheck.ts");
            Assert.Equal(2, ts.Split(new[] { "sheets: z.array(" }, StringSplitOptions.None).Length - 1); // check_drawing and fix_drawing
            Assert.Contains("Layout2", Src("docs", "checklist-dot-g2.md"));
        }

        // ------------------------------------------------------------ KFG-16, KFG-17

        [Fact]
        public void Dimensions_on_layouts_count_and_the_fix_reads_the_same_spaces_as_the_check()
        {
            var t = Tools;
            Assert.Contains("dims++; // Sửa KFG-16", t);
            Assert.DoesNotContain("if (model) dims++;", t);
            Assert.Contains("ScanDrawing(ctx, new HashSet<string>(StringComparer.Ordinal) { fix }, p.Bool(\"layouts\", true), p.StringsOpt(\"sheets\"))", t);
            var ts = Src("agent", "src", "tools", "drawingCheck.ts");
            int fix = ts.IndexOf("'fix_drawing'", StringComparison.Ordinal);
            Assert.True(ts.IndexOf("layouts: z.boolean()", fix, StringComparison.Ordinal) > fix, "fix_drawing has layouts");
            // the card token is made from the objects of that scan only: model-only and with layouts give other tokens
            Assert.NotEqual(DrawingCheckLogic.FixToken("not_bylayer", null, new[] { "2F" }), DrawingCheckLogic.FixToken("not_bylayer", null, new[] { "2F", "3A" }));
        }

        // ------------------------------------------------------------ KFG-18: a target layer that does not print

        [Fact]
        public void Objects_are_never_moved_onto_a_layer_that_does_not_print()
        {
            Assert.Null(DrawingCheckLogic.TargetLayerStateError("layer_zero", "A-WALL", false, false, true));
            Assert.Contains("off", DrawingCheckLogic.TargetLayerStateError("layer_zero", "A-WALL", true, false, true));
            Assert.Contains("frozen", DrawingCheckLogic.TargetLayerStateError("defpoints", "A-DOOR", false, true, true));
            var img = DrawingCheckLogic.TargetLayerStateError("layer_zero", G5Numbers.Layer, false, false, false);
            Assert.Contains("not plotted", img);
            Assert.Contains("ask the user", img);
            Assert.Contains("DrawingCheckLogic.TargetLayerStateError(fix, tl.Name, tl.IsOff, tl.IsFrozen, tl.IsPlottable)", Tools);
        }

        // ------------------------------------------------------------ nghi vấn G2: a dimension text " "

        [Fact]
        public void A_dimension_text_of_one_space_hides_the_number_and_is_reported()
        {
            Assert.True(DrawingCheckLogic.HidesMeasurement(" "));
            Assert.True(DrawingCheckLogic.HidesMeasurement("9999"));
            Assert.False(DrawingCheckLogic.HidesMeasurement(""));
            Assert.False(DrawingCheckLogic.HidesMeasurement(null));
            Assert.False(DrawingCheckLogic.HidesMeasurement("<> TT"));
        }

        // ------------------------------------------------------------ lượt 2: KFG-38 (polyline backwards), KFG-34, KFG-32

        [Fact]
        public void An_open_polyline_drawn_backwards_is_a_copy_through_DupItem()
        {
            var v = new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 500 } };
            var rv = new List<double[]> { v[2], v[1], v[0] };
            DrawingCheckLogic.DupItem Pl(List<double[]> verts, List<double> bulges)
            {
                var n = DrawingCheckLogic.PolylineNumbers(verts, bulges, false);
                return new DrawingCheckLogic.DupItem
                {
                    Group = "M|A-WALL|pline|o" + DrawingCheckLogic.Exact(n.Where((_, i) => i % 3 == 2).ToArray()),
                    Nums = n.Where((_, i) => i % 3 != 2).ToArray(),
                };
            }
            var fwd = Pl(v, new List<double> { 0.5, 0, 0 });
            var back = Pl(rv, new List<double> { 0, -0.5, 0 });
            var other = Pl(v, new List<double> { 0, 0, 0 }); // straight instead of the arc: not a copy
            Assert.Equal(new List<int> { 1 }, DrawingCheckLogic.DuplicateIndices(new[] { fwd, back, other }, G2Numbers.DuplicateTolMm));
            Assert.Contains("var xy = nums.Where((_, i) => i % 3 != 2)", Tools); // the plugin splits the same way
            Assert.Equal(DrawingCheckLogic.Exact(0.0), DrawingCheckLogic.Exact(-0.0)); // the bulge negated backwards
        }

        [Fact]
        public void Twenty_thousand_lines_of_one_x_are_checked_fast_and_give_the_same_answer()
        {
            var items = new List<DrawingCheckLogic.DupItem>();
            for (int i = 0; i < 20000; i++) items.Add(D("M|A|line|", 0, i * 10.0, 0, 3000, i * 10.0, 0));
            items.Add(D("M|A|line|", 0.05, 50.05, 0, 3000, 50, 0));   // a copy of line 5 (≤ 0.1 mm)
            items.Add(D("M|A|line|", 0, 70.2, 0, 3000, 70.2, 0));     // 0.2 from line 7: not a copy
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var dup = DrawingCheckLogic.DuplicateIndices(items, G2Numbers.DuplicateTolMm);
            sw.Stop();
            Assert.Equal(new List<int> { 20000 }, dup);
            Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void Lines_and_hatches_of_a_view_on_a_layer_of_the_users_choice_are_marked()
        {
            var sec = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "SectionTools.cs");
            Assert.Contains("MarkViewLine(ctx, c);", sec);                 // every line section_view / projection_view add (AddCurve)
            Assert.Contains("MarkPlainHatch(ctx, hatch); // Sửa KFG-32", sec); // the section hatch
            var t = Tools;
            Assert.Contains("if (Util2DLogic.IsDerivedViewLayer(e.Layer) || IsViewLine(e)) return null;", t);
            Assert.Contains("internal const string ViewLineApp = \"CLAUDECAD_VIEW\";", t);
            // the old rounded keys are gone
            Assert.DoesNotContain("ShapeKey(", Src("plugin", "ClaudeCAD.Plugin", "Core", "DrawingCheck.cs"));
            Assert.DoesNotContain("DuplicateIndices(IList<string>", Src("plugin", "ClaudeCAD.Plugin", "Core", "DrawingCheck.cs"));
        }
    }
}
