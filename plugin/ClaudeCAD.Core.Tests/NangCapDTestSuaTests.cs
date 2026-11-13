using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test &amp; sửa Nâng cấp đợt D (13/11/2026, docs/ban-giao/nang-cap-d-test.md): the bugs ND-1…ND-6 found in the review of
    /// đợt D, and the self-test step of đợt D. AutoCAD-free logic; the parts that need AutoCAD are checked in the sources.
    /// </summary>
    public class NangCapDTestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools3 => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs");
        private static string Solid2Src => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");

        private static double[] P(double x, double y) => new[] { x, y };

        // ------------------------------------------------------------ ND-1: a beam path that doubles back

        [Fact]
        public void Beam_that_doubles_back_has_no_flat_corner_piece()
        {
            // 0 → 4000 → back to 1000: a turn of exactly 180°; the filler would be a flat triangle (no region)
            var lay = Solid3Logic.Beam(new List<double[]> { P(0, 0), P(4000, 0), P(1000, 0) }, null, false, 220);
            Assert.Equal(0, lay.Joints);
            Assert.Equal(2, lay.Pieces.Count);
            Assert.All(lay.Pieces, o => Assert.True(Math.Abs(o.Area) > 1, "every piece has an area"));
            Assert.Equal(7000, lay.Length, 6);
        }

        [Fact]
        public void Beam_turning_almost_back_still_closes_its_corner()
        {
            // a 170° turn: still a corner piece (bevel), with an area
            double t = 170 * Math.PI / 180;
            var lay = Solid3Logic.Beam(new List<double[]> { P(0, 0), P(4000, 0), P(4000 + 3000 * Math.Cos(t), 3000 * Math.Sin(t)) }, null, false, 220);
            Assert.Equal(1, lay.Joints);
            Assert.All(lay.Pieces, o => Assert.True(Math.Abs(o.Area) > 1));
            // and a 179.9° turn (just under the limit... above it): no flat piece either way
            double u = 179.9 * Math.PI / 180;
            var lay2 = Solid3Logic.Beam(new List<double[]> { P(0, 0), P(4000, 0), P(4000 + 3000 * Math.Cos(u), 3000 * Math.Sin(u)) }, null, false, 220);
            Assert.Equal(0, lay2.Joints);
            Assert.True(Solid3Logic.MaxFillerDeg < 180 && Solid3Logic.MaxFillerDeg > Solid3Logic.MaxMitreDeg);
        }

        [Fact]
        public void Closed_beam_ring_doubling_back_is_still_built()
        {
            // a closed path going out and back along the same line: two segments, no flat corners
            var lay = Solid3Logic.Beam(new List<double[]> { P(0, 0), P(3000, 0) }, null, true, 220);
            Assert.All(lay.Pieces, o => Assert.True(Math.Abs(o.Area) > 1));
        }

        // ------------------------------------------------------------ ND-2: presspull mode without target

        [Fact]
        public void Presspull_add_or_subtract_without_target_is_refused_before_building()
        {
            Assert.Throws<ToolException>(() => Solid3Logic.ResolvePresspull("subtract", false, null, 0));
            Assert.Throws<ToolException>(() => Solid3Logic.ResolvePresspull("add", false, null, 0));
            var s = Tools3;
            int check = s.IndexOf("if (target == null) Solid3Logic.ResolvePresspull(mode, false, null, 0, tool);", StringComparison.Ordinal);
            Assert.True(check > 0, "the plugin checks the mode when there is no target");
            Assert.True(check < s.IndexOf("var regions = ProfileRegions(ctx, p, out var sources, out _);", StringComparison.Ordinal), "before any region / solid is made");
            // mode "new" never opens the target for write (the assistant asks no card for it)
            Assert.Contains("Solid3d ts = target != null && mode != \"new\" ? SolidForWrite(ctx, target, tool) : null;", s);
            Assert.Contains("was not changed.", s);
        }

        // ------------------------------------------------------------ ND-3: macros

        private static MacroCall Call(string tool, Dictionary<string, object> p, params string[] result) =>
            new MacroCall { Tool = tool, Params = p, ResultHandles = result.ToList() };

        [Fact]
        public void Macro_with_presspull_on_an_older_solid_is_not_run()
        {
            Assert.Contains("presspull", MacroLogic.AlwaysConfirmTools);
            var faces = new Macro { Calls = { Call("presspull", new Dictionary<string, object> { ["handle"] = "2F4A", ["face_filter"] = new List<object> { "top" }, ["distance"] = 200.0 }, "2F4A") } };
            var ext = MacroLogic.ExternalSolidEdits(faces);
            Assert.Single(ext);
            Assert.StartsWith("presspull (2F4A)", ext[0]);
            var cut = new Macro { Calls = { Call("presspull", new Dictionary<string, object> { ["profile_handles"] = new List<object> { "C1" }, ["target"] = "2F4A", ["distance"] = -100.0 }, "2F4A") } };
            Assert.StartsWith("presspull (2F4A)", Assert.Single(MacroLogic.ExternalSolidEdits(cut)));
        }

        [Fact]
        public void Macro_presspull_of_its_own_solid_or_a_new_solid_runs()
        {
            // the profile curves (existing lines) are only read: a new solid from them needs no card
            var fresh = new Macro { Calls = { Call("presspull", new Dictionary<string, object> { ["profile_handles"] = new List<object> { "C1" }, ["distance"] = 500.0 }, "N1") } };
            Assert.Empty(MacroLogic.ExternalSolidEdits(fresh));
            // mode "new" does not change the target
            var sep = new Macro { Calls = { Call("presspull", new Dictionary<string, object> { ["profile_handles"] = new List<object> { "C1" }, ["target"] = "2F4A", ["mode"] = "new", ["distance"] = 500.0 }, "N2") } };
            Assert.Empty(MacroLogic.ExternalSolidEdits(sep));
            // a box the macro built itself, then pulled
            var own = new Macro
            {
                Calls =
                {
                    Call("draw_box", new Dictionary<string, object> { ["length"] = 1000.0 }, "B1"),
                    Call("presspull", new Dictionary<string, object> { ["handle"] = "B1", ["face_filter"] = new List<object> { "top" }, ["distance"] = 200.0 }, "B1"),
                },
            };
            Assert.Empty(MacroLogic.ExternalSolidEdits(own));
            // the other tools keep every handle param
            Assert.Equal(new[] { "T", "S" }, MacroLogic.SolidEditHandles("solid_subtract", new Dictionary<string, object> { ["target"] = "T", ["subtract"] = new List<object> { "S" } }).ToArray());
        }

        // ------------------------------------------------------------ ND-4: clash volumes in a metre drawing

        [Fact]
        public void Clash_table_converts_drawing_units()
        {
            var rows = new List<Solid3Logic.ClashRow> { new Solid3Logic.ClashRow { A = "A1", LayerA = "DAM", B = "B1", LayerB = "COT", Volume = 0.25 } };
            // 0.25 m³ in a drawing in metres
            var t = Solid3Logic.ClashTable(rows, 3, 1000);
            Assert.Equal("0,250", t[1][5]);
            Assert.Equal("0,250", t[2][5]);
            // mm drawing: unchanged (0.25e9 mm³ = 0.25 m³)
            rows[0].Volume = 0.25e9;
            Assert.Equal("0,250", Solid3Logic.ClashTable(rows, 3)[1][5]);
            var s = Solid2Src;
            int start = s.IndexOf("private static object CheckInterferenceTool", StringComparison.Ordinal);
            var body = s.Substring(start, s.IndexOf("// =====", start, StringComparison.Ordinal) > 0 ? s.IndexOf("// =====", start, StringComparison.Ordinal) - start : s.Length - start);
            Assert.DoesNotContain("ToCubicMetres", body);
            Assert.Contains("FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _)", body);
            Assert.Contains("Solid3Logic.ClashTable(clashRows, dec, mmPerUnit)", body);
        }

        // ------------------------------------------------------------ ND-5: layer ranks

        [Theory]
        [InlineData("A-SANH", 4)]        // sảnh (lobby), not a slab
        [InlineData("COLOR", 4)]
        [InlineData("M-DAMPER", 4)]
        [InlineData("COTE", 4)]
        [InlineData("COT1", 0)]
        [InlineData("KC-COT", 0)]
        [InlineData("COLS", 0)]
        [InlineData("S-COLUMNS", 0)]
        [InlineData("BEAMS", 1)]
        [InlineData("DAM2", 1)]
        [InlineData("TUONGXAY", 2)]
        [InlineData("A-WALLS-3D", 2)]
        [InlineData("SAN", 3)]
        [InlineData("SÀN 150", 3)]
        [InlineData("A-FLOOR", 3)]
        public void Layer_rank_short_keys_match_whole_words(string layer, int rank) => Assert.Equal(rank, Solid3Logic.LayerRank(layer));

        // ------------------------------------------------------------ ND-6: STL name

        [Fact]
        public void Stl_output_needs_a_name_before_the_extension()
        {
            var s = Tools3;
            Assert.Contains("string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(full)) ? \"the file has no name before .stl\"", s);
            Assert.Null(Util2DLogic.BadFileName("nha-3d.stl"));
            Assert.NotNull(Util2DLogic.BadFileName("con.stl"));
            Assert.NotNull(Util2DLogic.BadFileName("a?.stl"));
        }

        // ------------------------------------------------------------ self-test step

        [Fact]
        public void Self_test_has_a_dot_d_step()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            Assert.Contains("Nâng cấp đợt D: dầm chữ L, tường cong, kéo mặt trên (PRESSPULL), xuất STL", s);
            foreach (var call in new[] { "DrawingService.BuildBeam(", "DrawingService.Build3dFromPlan(", "DrawingService.Presspull(", "DrawingService.ExportStl(" })
                Assert.Contains(call, s.Substring(s.IndexOf("Nâng cấp đợt D:", StringComparison.Ordinal)));
            Assert.Contains("internal static object Presspull(ToolContext ctx, Params p)", Tools3);
            Assert.Contains("internal static object ExportStl(ToolContext ctx, Params p)", Tools3);
        }
    }
}
