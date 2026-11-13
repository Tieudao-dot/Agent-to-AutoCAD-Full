using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test &amp; sửa nâng cấp đợt A (13/11/2026, docs/ban-giao/nang-cap-a-test.md): the bugs found in the review
    /// (NA-1 … NA-4) and the edges the first tests did not cover.
    /// </summary>
    public class NangCapATestSuaTests
    {
        private static string Src(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "plugin", "ClaudeCAD.Plugin"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray()));
        }

        private static List<double[]> Line(double len) => new List<double[]> { new double[] { 0, 0 }, new double[] { len, 0 } };

        // ------------------------------------------------------------ NA-2: a railing path far too long stops at once

        [Theory]
        [InlineData(1e7, 1500)]   // 10 km
        [InlineData(1e9, 200)]    // 1000 km, the closest posts
        [InlineData(1e15, 1500)]  // more spans than an int holds
        [InlineData(1e300, 6000)]
        public void A_far_too_long_railing_is_refused_before_the_pieces_are_laid_out(double lengthMm, double spacing)
        {
            var sw = Stopwatch.StartNew();
            foreach (var infill in RailingLogic.Infills)
            {
                var ex = Assert.Throws<ToolException>(() => RailingLogic.Build(Line(lengthMm), false, new RailingLogic.Options { PostSpacing = spacing, Infill = infill }));
                Assert.Equal(ErrorCodes.TooManyEntities, ex.Code);
                Assert.Contains("Nothing was built", ex.Message);
                Assert.Contains("units", ex.Message);
            }
            Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void Many_short_segments_stop_at_the_limit_too()
        {
            // 2000 points 3 m apart with bars: about 25 pieces a metre
            var pts = Enumerable.Range(0, 2000).Select(i => new double[] { i * 3000.0, 0 }).ToList();
            var ex = Assert.Throws<ToolException>(() => RailingLogic.Build(pts, false, new RailingLogic.Options()));
            Assert.Equal(ErrorCodes.TooManyEntities, ex.Code);
        }

        [Fact]
        public void A_long_but_possible_railing_is_still_built()
        {
            // 150 m with bars: under the limit; 1 km with no infill: under the limit
            var bars = RailingLogic.Build(Line(150000), false, new RailingLogic.Options());
            Assert.InRange(bars.Boxes.Count, 1000, RailingLogic.MaxBoxes);
            Assert.Equal(101, bars.Posts);
            var none = RailingLogic.Build(Line(1e6), false, new RailingLogic.Options { Infill = "none" });
            Assert.Equal(669, none.Boxes.Count); // 1 rail + 668 posts (667 spans)
        }

        // ------------------------------------------------------------ railing edges

        [Fact]
        public void A_tiny_jog_in_the_path_adds_no_piece_of_its_own()
        {
            var jog = new List<double[]> { new double[] { 0, 0 }, new double[] { 1000, 0 }, new double[] { 1000, 30 }, new double[] { 2000, 30 } };
            var lay = RailingLogic.Build(jog, false, new RailingLogic.Options { Infill = "none" });
            Assert.Equal(2000, lay.Length, 6); // the 30 mm jog is not a segment
            Assert.Equal(4, lay.Posts);
            Assert.Equal(6, lay.Boxes.Count);
        }

        [Fact]
        public void Repeated_points_and_a_two_point_loop_are_one_segment()
        {
            var twice = new List<double[]> { new double[] { 0, 0 }, new double[] { 0, 0 }, new double[] { 1200, 0 }, new double[] { 1200, 0 } };
            var a = RailingLogic.Build(twice, false, new RailingLogic.Options());
            var b = RailingLogic.Build(Line(1200), true, new RailingLogic.Options());
            Assert.Equal(2, a.Posts);
            Assert.Equal(a.Boxes.Count, b.Boxes.Count);
            Assert.Throws<ToolException>(() => RailingLogic.Build(new List<double[]> { new double[] { 5, 5 }, new double[] { 5, 5 } }, false, new RailingLogic.Options()));
            Assert.Throws<ToolException>(() => RailingLogic.Build(Line(40), false, new RailingLogic.Options())); // shorter than a post
        }

        [Theory]
        [InlineData(300)]
        [InlineData(1100)]
        [InlineData(3000)]
        public void Every_infill_stays_between_the_floor_and_the_top_rail(double height)
        {
            foreach (var infill in RailingLogic.Infills)
            {
                var lay = RailingLogic.Build(Line(2900), false, new RailingLogic.Options { Height = height, Infill = infill, PostSpacing = 700 });
                Assert.All(lay.Boxes, b =>
                {
                    Assert.True(b.Height > 0 && b.Length > 0 && b.Width > 0, $"{infill} {height}: a piece with no size");
                    Assert.True(b.Center[2] - b.Height / 2 >= -1e-9 && b.Center[2] + b.Height / 2 <= height + 1e-9);
                });
            }
            Assert.Throws<ToolException>(() => RailingLogic.Build(Line(2900), false, new RailingLogic.Options { Height = 299 }));
            Assert.Throws<ToolException>(() => RailingLogic.Build(Line(2900), false, new RailingLogic.Options { PostSpacing = 199 }));
            Assert.Throws<ToolException>(() => RailingLogic.Build(Line(2900), false, new RailingLogic.Options { Infill = "wood" }));
        }

        [Fact]
        public void Bar_gaps_are_never_wider_than_100_mm_for_any_span()
        {
            for (double clear = 20; clear <= 6000; clear += 7)
            {
                int m = RailingLogic.BarCount(clear);
                Assert.True((clear - m * RailingLogic.BarSize) / (m + 1) <= RailingLogic.MaxBarGap + 1e-9, $"span {clear}");
                Assert.True(m * RailingLogic.BarSize < clear || m == 0, $"span {clear}: bars do not fit");
            }
        }

        // ------------------------------------------------------------ NA-3: several storeys but no slab outline

        [Fact]
        public void Several_storeys_without_a_slab_outline_ask_before_building()
        {
            var msg = FloorPlanLogic.StoreysWithoutSlab(3, "auto", 0, 150);
            Assert.NotNull(msg);
            Assert.Contains("bbox", msg);
            Assert.Contains("\"none\"", msg);
            Assert.Contains("150", msg);
            Assert.NotNull(FloorPlanLogic.StoreysWithoutSlab(2, "auto", 0, 150)); // one storey + roof slab
            // nothing to ask: one level, a slab outline, bbox, none, or no slab thickness
            Assert.Null(FloorPlanLogic.StoreysWithoutSlab(1, "auto", 0, 150));
            Assert.Null(FloorPlanLogic.StoreysWithoutSlab(3, "auto", 1, 150));
            Assert.Null(FloorPlanLogic.StoreysWithoutSlab(3, "bbox", 0, 150));
            Assert.Null(FloorPlanLogic.StoreysWithoutSlab(3, "none", 0, 150));
            Assert.Null(FloorPlanLogic.StoreysWithoutSlab(3, "auto", 0, 0));
        }

        [Fact]
        public void The_tool_asks_about_missing_slabs_before_anything_is_built_and_a_dry_run_only_notes_it()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
            int ask = s.IndexOf("FloorPlanLogic.StoreysWithoutSlab(levels.Count, slabMode, input.Slabs.Count, hmm.Slab, tool)", StringComparison.Ordinal);
            Assert.True(ask > 0);
            Assert.True(ask < s.IndexOf("BuildWalls(ctx, tool, boxes", StringComparison.Ordinal));
            Assert.Contains("if (!dryRun) throw ToolException.Invalid(noSlab + \" Nothing was built.\");", s);
            // with slab "none" the walls fill the storey (no gap), so "none" is a real answer to the question
            Assert.Equal(3300, FloorPlanLogic.Storeys(3, 3300, null, 0, 0, false)[0].WallHeight);
        }

        // ------------------------------------------------------------ storeys

        [Fact]
        public void A_shorter_wall_height_is_kept_and_the_levels_follow_the_storey_height()
        {
            var l = FloorPlanLogic.Storeys(3, 3300, 2800, 150, 500, true);
            Assert.Equal(4, l.Count);
            Assert.All(l, x => Assert.Equal(2800, x.WallHeight));
            Assert.Equal(new[] { 500.0, 3800, 7100, 10400 }, l.Select(x => x.BaseZ));
            Assert.Equal(new[] { false, false, false, true }, l.Select(x => x.RoofOnly));
            Assert.Equal(new[] { 1, 2, 3, 4 }, l.Select(x => x.Number));
        }

        [Fact]
        public void The_rerun_check_of_columns_ignores_the_storey_below_and_above()
        {
            var outlines = new List<(double[] min, double[] max)> { (new double[] { 0, 0 }, new double[] { 300, 300 }) };
            Existing3d Col(string h, double z0, double z1) => new Existing3d { Handle = h, IsColumn = true, Min = new[] { 0.0, 0, z0 }, Max = new[] { 300.0, 300, z1 } };
            var existing = new[] { Col("S1", 0, 3150), Col("S2", 3300, 6450), Col("S3", 6600, 9750) };
            Assert.Equal(new[] { "S2" }, FloorPlanLogic.ColumnRerun(existing, outlines, 3300, 3150, 5));
            Assert.Empty(FloorPlanLogic.ColumnRerun(existing, outlines, 9900, 3150, 5));
            // one tall column through all storeys (the user's own) is a rerun of every storey it fills
            Assert.Equal(new[] { "T" }, FloorPlanLogic.ColumnRerun(new[] { Col("T", 0, 9900) }, outlines, 3300, 3150, 5));
            Assert.Empty(FloorPlanLogic.ColumnRerun(existing, new List<(double[] min, double[] max)>(), 0, 3150, 5));
        }

        // ------------------------------------------------------------ layer guess: names with marks, results, odd names

        [Theory]
        [InlineData("Tường 220", "wall")]
        [InlineData("TƯỜNG_XÂY", "wall")]
        [InlineData("Cửa sổ", "window")]
        [InlineData("cửa đi", "door")]
        [InlineData("CỘT", "column")]
        [InlineData("Sàn", "slab")]
        [InlineData("Lan can", "railing")]
        [InlineData("KT-CUA", "door")]
        [InlineData("TUONG$0$NEW", "wall")]
        public void Names_with_vietnamese_marks_and_separators_are_guessed(string layer, string kind) =>
            Assert.Equal(kind, FloorPlanLogic.GuessLayerKind(layer));

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("0")]
        [InlineData("Defpoints")]
        [InlineData("A-WALL-3D")]
        [InlineData("TUONG3D")]
        [InlineData("A-RAIL-3D")]
        [InlineData("TIM TƯỜNG")]
        [InlineData("DIM-CUA")]
        [InlineData("TUONG-HATCH")]
        [InlineData("CUASAT")] // not "CUA" + "SO" / "DI"
        [InlineData("SANH")]
        [InlineData("COLOR")]
        public void Result_layers_annotation_and_lookalikes_are_not_guessed(string layer) =>
            Assert.Null(FloorPlanLogic.GuessLayerKind(layer));

        // ------------------------------------------------------------ draw_solid

        [Fact]
        public void Every_shape_accepts_its_own_parameters_and_the_common_ones()
        {
            foreach (var shape in SolidLogic.Shapes)
            {
                var keys = SolidLogic.ShapeParams[shape].Concat(new[] { "shape", "layer" });
                SolidLogic.CheckShapeParams(shape, keys); // no throw
                Assert.Equal("draw_" + shape, SolidLogic.AliasOf(shape));
                Assert.True(PlanLogic.GroupOf(SolidLogic.AliasOf(shape)) == ToolGroup.ThreeD, shape);
            }
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("draw_solid"));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("build_railing"));
            var ex = Assert.Throws<ToolException>(() => SolidLogic.CheckShapeParams("sphere", new[] { "center", "radius", "height", "sides" }));
            Assert.Contains("height, sides are not parameters of shape 'sphere'", ex.Message);
            Assert.StartsWith("draw_solid:", ex.Message);
            Assert.Throws<ToolException>(() => SolidLogic.NormalizeShape(null));
            Assert.Throws<ToolException>(() => SolidLogic.NormalizeShape("cone"));
            Assert.Equal("box", SolidLogic.NormalizeShape(" BOX "));
        }

        [Fact]
        public void The_old_names_and_draw_solid_are_closed_on_a_2d_machine_and_open_on_3d_and_full()
        {
            foreach (var t in new[] { "draw_solid", "build_railing", "draw_box", "draw_cylinder", "draw_sphere", "draw_wedge", "draw_torus", "draw_pyramid" })
            {
                Assert.False(PlanLogic.Allowed("2d", t), t);
                Assert.True(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed(PlanLogic.PlanFull, t), t);
            }
        }

        [Fact]
        public void The_railing_pieces_report_errors_under_the_railing_name()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
            Assert.Contains("z + b.Center[2] / f }, b.RotationDeg, tool);", s);
        }

        // ------------------------------------------------------------ array_rect levels

        [Theory]
        [InlineData("3", true)]
        [InlineData("abc", true)]
        [InlineData("1", false)]
        [InlineData("0", false)]
        public void Levels_given_as_text_are_judged_like_numbers(string levels, bool blocked)
        {
            var p = new Dictionary<string, object> { ["levels"] = levels };
            Assert.Equal(blocked ? "levels" : null, PlanLogic.Blocked3dParam("2d", "array_rect", p));
            Assert.Null(PlanLogic.Blocked3dParam("3d", "array_rect", p));
            Assert.Null(PlanLogic.Blocked3dParam(PlanLogic.PlanFull, "array_rect", p));
            Assert.Null(PlanLogic.Blocked3dParam("2d", "copy_entities", p)); // only array_rect has levels
        }

        [Fact]
        public void A_2d_machine_keeps_array_rect_without_levels_and_level_spacing_alone_is_harmless()
        {
            Assert.True(PlanLogic.Allowed("2d", "array_rect"));
            Assert.Null(PlanLogic.BlockedParam("2d", "array_rect", new Dictionary<string, object> { ["rows"] = 2.0, ["cols"] = 3.0, ["level_spacing"] = 3300.0 }));
            Assert.Null(PlanLogic.BlockedParam("2d", "array_rect", new Dictionary<string, object> { ["levels"] = null }));
        }
    }
}
