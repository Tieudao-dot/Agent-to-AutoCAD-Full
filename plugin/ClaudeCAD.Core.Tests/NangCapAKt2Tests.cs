using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa sau kiểm tra độc lập nâng cấp (13/11/2026, docs/ban-giao/kiem-tra-doc-lap-nang-cap.md): KT2-10, KT2-11, KT2-21,
    /// KT2-22 of nâng cấp đợt A. The AutoCAD-free logic is tested; the AutoCAD side is checked in the sources.
    /// </summary>
    public class NangCapAKt2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string PlanTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");

        // ------------------------------------------------------------ KT2-10: array_rect level_spacing is mm

        [Theory]
        [InlineData(3300, 6, 3.3)]    // metres: "3 tầng cột, cách 3300" is 3.3 units, not 3.3 km
        [InlineData(3300, 4, 3300)]   // millimetres
        [InlineData(3300, 0, 3300)]   // no units set: taken as mm
        [InlineData(3300, 5, 330)]    // centimetres
        [InlineData(-3300, 6, -3.3)]  // downwards
        [InlineData(0, 6, 0)]
        public void KT2_10_level_spacing_is_mm_scaled_by_the_drawing_units(double mm, int insunits, double units) =>
            Assert.Equal(units, FloorPlanLogic.ToDrawingUnits(mm, insunits), 9);

        [Fact]
        public void KT2_10_array_rect_converts_only_the_level_spacing()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            Assert.Contains("double ls = FloorPlanLogic.ToDrawingUnits(p.Num(\"level_spacing\", 0), Convert.ToInt32(ctx.Db.Insunits));", s);
            // rows and columns stay drawing units, like every 2D tool
            Assert.Contains("double rs = p.Num(\"row_spacing\", 0);", s);
            Assert.Contains("double cs = p.Num(\"col_spacing\", 0);", s);
            Assert.Contains("res[\"level_step\"] = Acad.R(ls);", s);
        }

        // ------------------------------------------------------------ KT2-11: the same column outline twice

        [Fact]
        public void KT2_11_the_same_outline_twice_is_one_column()
        {
            double[] a0 = { 0, 0 }, a1 = { 220, 220 };
            Assert.True(FloorPlanLogic.SameOutline(a0, a1, 48400, new[] { 0.0, 0 }, new[] { 220.0, 220 }, 48400, 1e-3));
            Assert.True(FloorPlanLogic.SameOutline(a0, a1, 48400, new[] { 1e-5, 0 }, new[] { 220.0, 220 }, 48400.00001, 1e-3));
            // another column beside it, a bigger one around it, a circle in the same box: not the same outline
            Assert.False(FloorPlanLogic.SameOutline(a0, a1, 48400, new[] { 4000.0, 0 }, new[] { 4220.0, 220 }, 48400, 1e-3));
            Assert.False(FloorPlanLogic.SameOutline(a0, a1, 48400, new[] { -20.0, -20 }, new[] { 240.0, 240 }, 67600, 1e-3));
            Assert.False(FloorPlanLogic.SameOutline(a0, a1, 48400, new[] { 0.0, 0 }, new[] { 220.0, 220 }, Math.PI * 110 * 110, 1e-3));
            // an area AutoCAD could not give never matches (both are built rather than one lost)
            Assert.False(FloorPlanLogic.SameOutline(a0, a1, 0, new[] { 0.0, 0 }, new[] { 220.0, 220 }, 0, 1e-3));
            Assert.False(FloorPlanLogic.SameOutline(null, null, 48400, a0, a1, 48400, 1e-3));
        }

        [Fact]
        public void KT2_11_the_duplicate_is_dropped_when_the_plan_is_read_and_reported()
        {
            var s = PlanTools;
            // at reading, so a dry run counts the columns like the build
            int read = s.IndexOf("void AddColumn(Entity e, string h)", StringComparison.Ordinal);
            int same = s.IndexOf("FloorPlanLogic.SameOutline(c.min, c.max, c.area, bmin, bmax, area, tol)", StringComparison.Ordinal);
            int build = s.IndexOf("private static void BuildColumns(", StringComparison.Ordinal);
            Assert.True(read > 0 && same > read && build > same);
            Assert.Contains("the same outline as {input.Columns[same].handle}: one column built", s);
            Assert.Contains("input.ColumnShapes.Add((bmin, bmax, area));", s);
        }

        // ------------------------------------------------------------ KT2-21: one storey from storey_height, no slab outline

        [Fact]
        public void KT2_21_one_storey_from_storey_height_without_a_slab_outline_asks_first()
        {
            var msg = FloorPlanLogic.StoreyWithoutSlab(true, "auto", 0, 150);
            Assert.NotNull(msg);
            Assert.Contains("150 mm below the next storey", msg);
            Assert.Contains("slab \"bbox\"", msg);
            Assert.Contains("slab \"none\"", msg);
            // the user's choice, a slab outline, no slab thickness, or a wall height given outright: nothing to ask
            Assert.Null(FloorPlanLogic.StoreyWithoutSlab(true, "bbox", 0, 150));
            Assert.Null(FloorPlanLogic.StoreyWithoutSlab(true, "none", 0, 150));
            Assert.Null(FloorPlanLogic.StoreyWithoutSlab(true, "auto", 1, 150));
            Assert.Null(FloorPlanLogic.StoreyWithoutSlab(true, "auto", 0, 0));
            Assert.Null(FloorPlanLogic.StoreyWithoutSlab(false, "auto", 0, 150));
            // with slab "none" the walls fill the storey, so storey 2 at base_z 3300 sits on them
            Assert.Equal(3300, FloorPlanLogic.Storeys(1, 3300, null, 0, 0, false)[0].WallHeight);
            Assert.Equal(3150, FloorPlanLogic.Storeys(1, 3300, null, 150, 0, false)[0].WallHeight);
        }

        [Fact]
        public void KT2_21_the_tool_asks_before_building_and_keeps_the_wall_height_path()
        {
            var s = PlanTools;
            int ask = s.IndexOf("?? FloorPlanLogic.StoreyWithoutSlab(p.Has(\"storey_height\") && !p.Has(\"wall_height\"), slabMode, input.Slabs.Count, hmm.Slab, tool);", StringComparison.Ordinal);
            int build = s.IndexOf("BuildWalls(ctx, tool, boxes,", StringComparison.Ordinal);
            Assert.True(ask > 0 && build > ask);
            // a dry run reports it as a note, a build is refused
            Assert.Contains("if (!dryRun) throw ToolException.Invalid(noSlab + \" Nothing was built.\");", s);
        }

        // ------------------------------------------------------------ KT2-22: a layer guessed as a railing

        [Fact]
        public void KT2_22_a_guessed_railing_layer_is_asked_about_before_build_railing()
        {
            Assert.Equal("railing", FloorPlanLogic.GuessLayerKind("LAN CAN"));
            var use = FloorPlanLogic.GuessUse("railing");
            Assert.Contains("ask the user first", use);
            Assert.Contains("request_confirmation", use);
            Assert.Contains("only after their yes", use);
            Assert.DoesNotContain("path_handle", use);
            // the plan layers keep their parameter name (the assistant's gate refuses the build until the user's yes)
            foreach (var k in new[] { "wall", "door", "window", "slab", "column" }) Assert.Equal(k + "_layers", FloorPlanLogic.GuessUse(k));
            var s = PlanTools;
            Assert.Contains("[\"use\"] = FloorPlanLogic.GuessUse(x.kind),", s);
            Assert.Contains("A layer guessed as a railing is the same: ask before build_railing", s);
            Assert.Contains("tường / cửa đi / cửa sổ / sàn / cột / lan can", s);
        }
    }
}
