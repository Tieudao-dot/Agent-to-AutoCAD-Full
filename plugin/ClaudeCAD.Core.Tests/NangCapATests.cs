using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt A (13/11/2026, docs/ban-giao/nang-cap-a.md): draw_solid, the layer guess of build_3d_from_plan,
    /// columns, several storeys at once, build_railing, array_rect levels. The AutoCAD-free logic is tested here; the
    /// rules that need AutoCAD are checked in the sources.
    /// </summary>
    public class NangCapATests
    {
        /// <summary>New tools of PlanTo3dTools.cs (besides the phase 3 ones).</summary>
        public static readonly string[] NewPlanTools = { "build_railing" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string PlanTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");

        // ------------------------------------------------------------ plan labels

        [Theory]
        [InlineData("draw_solid")]
        [InlineData("build_railing")]
        [InlineData("draw_box")]       // macro alias
        [InlineData("draw_pyramid")]   // macro alias
        public void New_tools_and_aliases_are_ThreeD_only(string t)
        {
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
            Assert.False(PlanLogic.Allowed("2d", t));
            Assert.False(PlanLogic.Allowed(null, t));
            Assert.True(PlanLogic.Allowed("3d", t));
            Assert.True(PlanLogic.Allowed("2d3d", t));
        }

        [Fact]
        public void A_macro_with_the_old_primitive_names_still_runs_on_3d_and_is_refused_on_2d()
        {
            var calls = new List<(string, IDictionary<string, object>)> { ("draw_box", new Dictionary<string, object> { ["length"] = 1.0 }) };
            Assert.Empty(PlanLogic.BlockedCalls("3d", calls));
            Assert.Equal(new[] { "draw_box" }, PlanLogic.BlockedCalls("2d", calls));
        }

        // ------------------------------------------------------------ draw_solid

        [Theory]
        [InlineData("box", "box")]
        [InlineData(" Cylinder ", "cylinder")]
        [InlineData("cube", "box")]
        [InlineData("trụ", "cylinder")]
        [InlineData("nêm", "wedge")]
        [InlineData("pyramid", "pyramid")]
        public void Shape_names_are_normalised(string given, string expected) =>
            Assert.Equal(expected, SolidLogic.NormalizeShape(given));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("cone")]
        public void Unknown_shape_is_a_clear_error(string given)
        {
            var ex = Assert.Throws<ToolException>(() => SolidLogic.NormalizeShape(given));
            Assert.Contains("box, cylinder, sphere, wedge, torus, pyramid", ex.Message);
        }

        [Fact]
        public void A_parameter_of_another_shape_is_refused_and_the_own_ones_pass()
        {
            SolidLogic.CheckShapeParams("box", new[] { "shape", "length", "width", "height", "corner", "rotation", "layer", "color" });
            SolidLogic.CheckShapeParams("cylinder", new[] { "shape", "base", "radius", "top_center", "top_radius" });
            SolidLogic.CheckShapeParams("pyramid", new[] { "base", "sides", "radius", "rotation", "height" });
            var ex = Assert.Throws<ToolException>(() => SolidLogic.CheckShapeParams("box", new[] { "length", "width", "height", "tube_radius" }));
            Assert.Contains("tube_radius", ex.Message);
            Assert.Contains("A box takes", ex.Message);
            Assert.Throws<ToolException>(() => SolidLogic.CheckShapeParams("sphere", new[] { "center", "radius", "height" }));
            Assert.Throws<ToolException>(() => SolidLogic.CheckShapeParams("wedge", new[] { "length", "width", "height", "center" }));
        }

        [Fact]
        public void Every_shape_has_parameters_and_an_alias_registered()
        {
            var solids = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "SolidTools.cs");
            Assert.Contains("d.Register(\"draw_solid\", true, DrawSolid);", solids);
            foreach (var s in SolidLogic.Shapes)
            {
                Assert.True(SolidLogic.ShapeParams.ContainsKey(s), s);
                Assert.Contains($"d.Register(\"{SolidLogic.AliasOf(s)}\"", solids);
            }
        }

        // ------------------------------------------------------------ layer guess

        [Theory]
        [InlineData("TUONG", "wall")]
        [InlineData("KT_TƯỜNG", "wall")]
        [InlineData("A-WALL-NEW", "wall")]
        [InlineData("tuong xay 220", "wall")]
        [InlineData("CUA", "door")]
        [InlineData("CUA DI", "door")]
        [InlineData("A-DOOR-01", "door")]
        [InlineData("CUA SO", "window")]
        [InlineData("CUA_SO", "window")]
        [InlineData("CỬA SỔ", "window")]
        [InlineData("A-GLAZ", "window")]
        [InlineData("COT", "column")]
        [InlineData("CỘT BTCT", "column")]
        [InlineData("S-COLS", "column")]
        [InlineData("SAN", "slab")]
        [InlineData("A-FLOOR", "slab")]
        [InlineData("LAN CAN", "railing")]
        [InlineData("A-RAIL", "railing")]
        public void Layer_names_are_guessed(string layer, string kind) =>
            Assert.Equal(kind, FloorPlanLogic.GuessLayerKind(layer));

        [Theory]
        [InlineData("0")]
        [InlineData("DEFPOINTS")]
        [InlineData("A-WALL-3D")]   // a result layer
        [InlineData("A-COLS-3D")]
        [InlineData("TUONG-HATCH")] // a hatch
        [InlineData("KT_DIM")]
        [InlineData("SANH")]        // sảnh (lobby) is not sàn
        [InlineData("C-SIDEWALK")]  // not a wall
        [InlineData("A-OUTDOOR")]   // not a door
        [InlineData("C-TRAIL")]     // not a railing
        [InlineData("TRUC TUONG")]  // wall axis lines are not face lines
        [InlineData("TIM-TUONG")]
        [InlineData("A-WALL-CEN")]
        [InlineData("A-FURN")]
        [InlineData("CHU")]
        [InlineData(null)]
        public void Not_a_plan_part_is_not_guessed(string layer) =>
            Assert.Null(FloorPlanLogic.GuessLayerKind(layer));

        [Fact]
        public void The_guess_is_reported_and_never_built_from()
        {
            var s = PlanTools;
            Assert.Contains("report[\"layer_guess\"] = guesses;", s);
            Assert.Contains("Do not build from a guess", s);
            // only layers that were not read are guessed
            Assert.Contains("input.Unread[layer]", s);
        }

        // ------------------------------------------------------------ columns

        [Fact]
        public void Columns_are_closed_outlines_up_to_3_m()
        {
            Assert.Null(FloorPlanLogic.ColumnSkipReason(220, 220));
            Assert.Null(FloorPlanLogic.ColumnSkipReason(400, 600));
            Assert.Null(FloorPlanLogic.ColumnSkipReason(3000, 3000));
            Assert.NotNull(FloorPlanLogic.ColumnSkipReason(4000, 220));
            Assert.NotNull(FloorPlanLogic.ColumnSkipReason(0, 220));
            Assert.Equal(new[] { "A-COLS", "A-COL" }, FloorPlanLogic.DefaultColumnLayers);
            Assert.Equal("A-COLS-3D", FloorPlanLogic.ColumnLayer3d);
        }

        [Fact]
        public void Columns_are_built_per_storey_as_high_as_the_walls_and_counted_in_the_rerun_check()
        {
            var s = PlanTools;
            Assert.Contains("BuildColumns(ctx, tool, input, bz, hu.Wall, f, col3d", s);
            Assert.Contains("FloorPlanLogic.ColumnRerun(input.Existing, colOutlines, bz, hu.Wall", s);
            // existing column solids are read for the rerun check, never as plan
            Assert.Contains("string.Equals(layer, column3d, StringComparison.OrdinalIgnoreCase)", s);
        }

        private static Existing3d Old(string h, bool column, double x0, double y0, double x1, double y1, double z0, double z1) =>
            new Existing3d { Handle = h, IsColumn = column, Min = new[] { x0, y0, z0 }, Max = new[] { x1, y1, z1 } };

        [Fact]
        public void A_column_built_again_is_a_rerun_only_against_columns_at_the_same_level()
        {
            var outlines = new List<(double[] min, double[] max)> { (new[] { 0.0, 0.0 }, new[] { 300.0, 300.0 }) };
            var same = Old("C1", true, 0, 0, 300, 300, 0, 3150);
            var below = Old("C0", true, 0, 0, 300, 300, -3300, -150);
            var beside = Old("C2", true, 300, 0, 600, 300, 0, 3150); // only touching
            var wallThere = Old("W1", false, 0, 0, 300, 300, 0, 3150); // a wall solid is not a column
            Assert.Equal(new[] { "C1" }, FloorPlanLogic.ColumnRerun(new[] { same, below, beside, wallThere }, outlines, 0, 3150, 5));
            // storey 2 over the same outline: the column of storey 1 does not count
            Assert.Empty(FloorPlanLogic.ColumnRerun(new[] { same }, outlines, 3300, 3150, 5));
            Assert.Empty(FloorPlanLogic.ColumnRerun(new[] { same }, new List<(double[] min, double[] max)>(), 0, 3150, 5));
        }

        [Fact]
        public void A_column_the_user_built_inside_a_wall_is_not_a_rerun_of_the_wall()
        {
            // a 4000 × 220 wall along X and an existing column solid at its corner, same height
            var wall = new PlanBox { Center = new[] { 2000.0, 110, 1650 }, Length = 4000, Width = 220, Height = 3300, RotationDeg = 0 };
            var column = Old("C1", true, 0, 0, 220, 220, 0, 3300);
            var none = new List<(double[] min, double[] max)>();
            Assert.Empty(FloorPlanLogic.CheckRerun(new[] { column }, new[] { wall }, none, 0, 3300, 0, 5).Rerun);
            // the same solid on the wall layer is the wall built again (unchanged, KT-14)
            var oldWall = Old("W1", false, 0, 0, 4000, 220, 0, 3300);
            Assert.Equal(new[] { "W1" }, FloorPlanLogic.CheckRerun(new[] { oldWall, column }, new[] { wall }, none, 0, 3300, 0, 5).Rerun);
        }

        [Fact]
        public void Column_sizes_are_summarised_for_the_report()
        {
            var sizes = new List<(double dx, double dy)> { (220, 220), (220.2, 219.9), (400, 220), (220, 400), (300, 300) };
            var sum = FloorPlanLogic.ColumnSizeSummary(sizes);
            Assert.Equal(3, sum.Count);
            // 220 × 400 and 400 × 220 are the same size; the most frequent first, ties by size
            Assert.Equal((220.0, 220.0, 2), sum[0]);
            Assert.Equal((220.0, 400.0, 2), sum[1]);
            Assert.Equal((300.0, 300.0, 1), sum[2]);
            Assert.Equal(2, FloorPlanLogic.ColumnSizeSummary(sizes, 2).Count);
        }

        // ------------------------------------------------------------ storeys

        [Fact]
        public void Three_storeys_of_3_3_m_walls_are_storey_minus_slab()
        {
            var l = FloorPlanLogic.Storeys(3, 3300, null, 150, 0, false);
            Assert.Equal(3, l.Count);
            Assert.Equal(new[] { 0.0, 3300, 6600 }, l.Select(x => x.BaseZ));
            Assert.All(l, x => Assert.Equal(3150, x.WallHeight));
            // L24: the slab of storey k + 1 (base − slab … base) sits exactly on the walls of storey k
            for (int k = 0; k + 1 < l.Count; k++) Assert.Equal(l[k + 1].BaseZ - 150, l[k].BaseZ + l[k].WallHeight, 6);
        }

        [Fact]
        public void One_storey_is_the_old_behaviour()
        {
            var l = FloorPlanLogic.Storeys(1, null, null, 150, 0, false);
            Assert.Single(l);
            Assert.Equal(FloorPlanLogic.DefaultWallHeight, l[0].WallHeight);
            var w = FloorPlanLogic.Storeys(1, null, 3180, 120, 3300, false);
            Assert.Equal(3300, w[0].BaseZ);
            Assert.Equal(3180, w[0].WallHeight);
        }

        [Fact]
        public void Storeys_without_storey_height_stack_wall_plus_slab_and_start_at_base_z()
        {
            var l = FloorPlanLogic.Storeys(2, null, 3180, 120, 3300, true);
            Assert.Equal(new[] { 3300.0, 6600, 9900 }, l.Select(x => x.BaseZ));
            Assert.False(l[1].RoofOnly);
            Assert.True(l[2].RoofOnly); // the roof slab on the last storey's walls
            Assert.Equal(l[2].BaseZ - 120, l[1].BaseZ + l[1].WallHeight, 6);
        }

        [Fact]
        public void A_wall_taller_than_storey_minus_slab_is_refused()
        {
            var ex = Assert.Throws<ToolException>(() => FloorPlanLogic.Storeys(3, 3300, 3300, 150, 0, false));
            Assert.Contains("3150", ex.Message);
            Assert.Throws<ToolException>(() => FloorPlanLogic.Storeys(0, 3300, null, 150, 0, false));
            Assert.Throws<ToolException>(() => FloorPlanLogic.Storeys(51, 3300, null, 150, 0, false));
            Assert.Throws<ToolException>(() => FloorPlanLogic.Storeys(2, 200, null, 150, 0, false));
            // no slab: the walls fill the storey
            Assert.Equal(3300, FloorPlanLogic.Storeys(2, 3300, null, 0, 0, false)[0].WallHeight);
        }

        [Fact]
        public void Metres_drawing_storey_levels_are_mm_scaled_L27()
        {
            // the levels are mm (like base_z, KT-16) and divided by mm per unit when the solids are placed
            Assert.Contains("double bz = lv.BaseZ / f;", PlanTools);
            Assert.Equal(1000, FloorPlanLogic.MmPerUnit(6, out _));
        }

        [Fact]
        public void Every_storey_is_checked_before_anything_is_built()
        {
            var s = PlanTools;
            int check = s.IndexOf("var rerun = ExistingOver(ctx, input, lv.RoofOnly ? noBoxes : boxes", StringComparison.Ordinal);
            int build = s.IndexOf("BuildWalls(ctx, tool, boxes, bz, f, wall3d, parts", StringComparison.Ordinal);
            Assert.True(check > 0 && build > check);
            // the storeys above are copies of the first storey's solids moved up, not built and united again
            Assert.Contains("parts = CopyLevel(ctx, tool, first, (lv.BaseZ - levels[0].BaseZ) / f, lv.RoofOnly", s);
            Assert.Contains("c.TransformBy(up);", s);
            // the storey below is never proposed for erasing (L27 / KT-14 kept)
            Assert.Contains("Never erase other storeys", s);
            Assert.Contains("Never erase the storey below for this", s);
        }

        // ------------------------------------------------------------ railing

        [Fact]
        public void Railing_defaults_are_the_documented_ones()
        {
            Assert.Equal(1100, RailingLogic.DefaultHeight);
            Assert.Equal(1500, RailingLogic.DefaultPostSpacing);
            Assert.Equal(100, RailingLogic.MaxBarGap);
            Assert.Equal("A-RAIL-3D", RailingLogic.Layer3d);
            var doc = Src("docs", "ban-giao", "nang-cap-a.md");
            Assert.Contains("1100", doc);
            Assert.Contains("1500", doc);
        }

        [Fact]
        public void A_3_m_straight_railing_has_posts_rails_and_bars_with_small_gaps()
        {
            var lay = RailingLogic.Build(new List<double[]> { new double[] { 0, 0 }, new double[] { 3000, 0 } }, false, new RailingLogic.Options());
            Assert.Equal(3000, lay.Length, 6);
            Assert.Equal(3, lay.Posts); // 0, 1500, 3000
            Assert.True(lay.Bars > 0);
            // every box stays within the height
            Assert.All(lay.Boxes, b => Assert.True(b.Center[2] - b.Height / 2 >= -1e-9 && b.Center[2] + b.Height / 2 <= 1100 + 1e-9));
            // bars: the clear gaps of a 1450 span are at most 100
            int m = RailingLogic.BarCount(1450);
            Assert.True((1450 - m * RailingLogic.BarSize) / (m + 1) <= RailingLogic.MaxBarGap + 1e-9);
            Assert.True((1450 - (m - 1) * RailingLogic.BarSize) / m > RailingLogic.MaxBarGap); // the fewest bars
            // the top rail is at the top
            Assert.Contains(lay.Boxes, b => Math.Abs(b.Center[2] + b.Height / 2 - 1100) < 1e-9 && b.Length > 3000);
        }

        [Fact]
        public void A_closed_railing_shares_its_corner_posts()
        {
            var sq = new List<double[]> { new double[] { 0, 0 }, new double[] { 1000, 0 }, new double[] { 1000, 1000 }, new double[] { 0, 1000 } };
            var lay = RailingLogic.Build(sq, true, new RailingLogic.Options { Infill = "panel" });
            Assert.Equal(4000, lay.Length, 6);
            Assert.Equal(4, lay.Posts);
            Assert.Equal(4, lay.Panels);
            Assert.Equal(0, lay.Bars);
            // the same with the first point repeated at the end
            var lay2 = RailingLogic.Build(sq.Concat(new[] { new double[] { 0, 0 } }).ToList(), true, new RailingLogic.Options { Infill = "none" });
            Assert.Equal(4, lay2.Posts);
            Assert.Equal(8, lay2.Boxes.Count); // 4 rails + 4 posts
        }

        [Fact]
        public void Railing_input_is_checked()
        {
            Assert.Throws<ToolException>(() => RailingLogic.Build(new List<double[]> { new double[] { 0, 0 } }, false, new RailingLogic.Options()));
            Assert.Throws<ToolException>(() => RailingLogic.Build(new List<double[]> { new double[] { 0, 0 }, new double[] { 10, 0 } }, false, new RailingLogic.Options()));
            Assert.Throws<ToolException>(() => RailingLogic.Build(new List<double[]> { new double[] { 0, 0 }, new double[] { 1000, 0 } }, false, new RailingLogic.Options { Height = 100 }));
            Assert.Throws<ToolException>(() => RailingLogic.NormalizeInfill("wood"));
            // more pieces than AutoCAD unites in reasonable time: refused (a 300 m railing with bars)
            var far = Assert.Throws<ToolException>(() => RailingLogic.Build(new List<double[]> { new double[] { 0, 0 }, new double[] { 300000, 0 } }, false, new RailingLogic.Options()));
            Assert.Contains("more than 2000", far.Message);
            // posts 70 apart leave no room for a bar: only posts and the top rail
            var tight = RailingLogic.Build(new List<double[]> { new double[] { 0, 0 }, new double[] { 60, 0 } }, false, new RailingLogic.Options());
            Assert.Equal(3, tight.Boxes.Count);
            Assert.Equal("panel", RailingLogic.NormalizeInfill("kính"));
            Assert.Equal("bars", RailingLogic.NormalizeInfill(null));
        }

        [Fact]
        public void The_self_test_has_the_dot_A_steps()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            Assert.Contains("Nâng cấp đợt A: dựng 2 tầng + sàn mái + 2 cột", s);
            Assert.Contains("Nâng cấp đợt A: lan can thẳng 3 m", s);
            // what the storeys step expects: walls of storey height − slab, storey 2 one storey height up
            Assert.Equal(2850, FloorPlanLogic.Storeys(2, 3000, null, 150, 10000, true)[1].WallHeight);
            Assert.Equal(new[] { 10000.0, 13000, 16000 }, FloorPlanLogic.Storeys(2, 3000, null, 150, 10000, true).Select(x => x.BaseZ));
            // what the railing step expects: 3 posts, and pieces that only touch (volume of the union = their sum)
            var lay = RailingLogic.Build(new List<double[]> { new[] { 0.0, 0.0 }, new[] { 3000.0, 0.0 } }, false, new RailingLogic.Options());
            Assert.Equal(3, lay.Posts);
            Assert.Equal(31390000, lay.Boxes.Sum(b => b.Volume), 3);
            for (int i = 0; i < lay.Boxes.Count; i++)
                for (int j = i + 1; j < lay.Boxes.Count; j++)
                    Assert.False(Overlap(lay.Boxes[i], lay.Boxes[j]), $"pieces {i} and {j} overlap");
        }

        /// <summary>Two axis-aligned railing pieces (rotation 0) share volume.</summary>
        private static bool Overlap(PlanBox a, PlanBox b)
        {
            double[] ha = { a.Length / 2, a.Width / 2, a.Height / 2 }, hb = { b.Length / 2, b.Width / 2, b.Height / 2 };
            for (int k = 0; k < 3; k++)
                if (Math.Min(a.Center[k] + ha[k], b.Center[k] + hb[k]) - Math.Max(a.Center[k] - ha[k], b.Center[k] - hb[k]) <= 1e-9) return false;
            return true;
        }

        [Fact]
        public void Railing_reads_its_path_only_and_scales_by_insunits()
        {
            var s = PlanTools;
            Assert.Contains("Level(path.Select(q => q.Z), from);", s);
            Assert.Contains("var e = Acad.GetEntity(ctx, from, false);", s);
            Assert.Contains("q[0] * f, q[1] * f", s);
            Assert.Contains("b.Length / f", s);
        }

        // ------------------------------------------------------------ array_rect levels

        [Fact]
        public void Array_levels_are_3d()
        {
            var two = new Dictionary<string, object> { ["levels"] = 3.0 };
            Assert.Equal("levels", PlanLogic.BlockedParam("2d", "array_rect", two));
            Assert.Null(PlanLogic.BlockedParam("3d", "array_rect", two));
            Assert.Null(PlanLogic.BlockedParam("2d3d", "array_rect", two));
            // one level, or none, stays Common
            Assert.Null(PlanLogic.BlockedParam("2d", "array_rect", new Dictionary<string, object> { ["levels"] = 1.0 }));
            Assert.Null(PlanLogic.BlockedParam("2d", "array_rect", new Dictionary<string, object> { ["rows"] = 3.0 }));
            Assert.True(PlanLogic.CallAllowed("2d", "array_rect", new Dictionary<string, object> { ["rows"] = 3.0 }));
            Assert.False(PlanLogic.CallAllowed("2d", "array_rect", two));
            var msg = PlanLogic.BlockedParamMessage("array_rect", "levels", "2d", "");
            Assert.Contains("3D group", msg);
            Assert.DoesNotContain("block attributes", msg);
            // block attributes still say text
            Assert.Contains("block attributes", PlanLogic.BlockedParamMessage("insert_block", "attributes", "3d", ""));
            Assert.Equal(new[] { "array_rect (số tầng)" },
                PlanLogic.BlockedCalls("2d", new List<(string, IDictionary<string, object>)> { ("array_rect", two) }));
        }

        [Fact]
        public void Array_levels_stack_along_z()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            Assert.Contains("Vector3d.ZAxis * (l * ls)", s);
            Assert.Contains("(long)rows * cols * levels * hs.Count", s);
            Assert.Contains("levels > 1 needs 'level_spacing'", s);
        }
    }
}
