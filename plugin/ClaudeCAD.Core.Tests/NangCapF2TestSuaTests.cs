using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2-test.md): the fixes of the review and the cases across
    /// UCS, plans, work modes, metre drawings and macros that the F2 tests did not cover.
    /// </summary>
    public class NangCapF2TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");

        // ------------------------------------------------------------ F2T-1: listing the library in a UCS

        [Fact]
        public void A_create_tool_called_only_to_read_runs_in_any_ucs()
        {
            // insert_library_block without a name only lists (readsWhen): it is still a Create tool...
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("insert_library_block", new Dictionary<string, object>()));
            // ...so the dispatcher must run a read-only call as is instead of refusing it (it has nothing to place)
            var d = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            int read = d.IndexOf("if (!ctx.Writable) return def.Handler(ctx, p);", StringComparison.Ordinal);
            int refuse = d.IndexOf("cannot place new objects in the UCS without a transaction", StringComparison.Ordinal);
            Assert.True(read > 0 && refuse > read, "read-only Create call must return before the refusal");
            Assert.Contains("readsWhen: p => !p.Has(\"name\")", Tools);
        }

        // ------------------------------------------------------------ F2T-2: TrueType style made by STYLE

        [Fact]
        public void A_truetype_style_is_compared_by_its_type_face()
        {
            Assert.Contains("[\"font\"] = !string.IsNullOrEmpty(r.Font.TypeFace) ? r.Font.TypeFace : r.FileName", Tools);
            // the comparison itself: "Arial" (type face) equals the wanted Arial, "arial.ttf" alone would not
            var wanted = StandardsLogic.TextStyleParams("KT");
            var styleDialog = new Dictionary<string, object> { ["font"] = "Arial", ["height"] = 0.0, ["width_factor"] = F2Numbers.WidthFactor };
            Assert.Empty(StandardsLogic.Differences(wanted, styleDialog));
            var fileOnly = new Dictionary<string, object> { ["font"] = "arial.ttf", ["height"] = 0.0, ["width_factor"] = F2Numbers.WidthFactor };
            Assert.Single(StandardsLogic.Differences(wanted, fileOnly));
        }

        // ------------------------------------------------------------ F2T-3: the sheet in a turned UCS

        [Fact]
        public void The_sheet_sizes_the_drawing_in_a_turned_ucs()
        {
            Assert.Contains("ucs0.BoxFromWcs(new[] { db.Extmin.X, db.Extmin.Y, db.Extmin.Z }", Tools);
            Assert.Contains("if (!p.Has(\"view_center\")) center = ucs0.ToWcs(", Tools);
            // a 40 × 4 m strip drawn along the UCS x axis of a UCS turned 90°: in WCS it is 4 × 40 m (tall), on the turned
            // viewport it is wide again, so A3 landscape fits it at a far more detailed scale than a 40 m tall box
            var f = new UcsFrame(new double[3], new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 });
            var (mn, mx) = f.BoxFromWcs(new[] { -4000.0, 0, 0 }, new[] { 0.0, 40000, 0 });
            double ew = mx[0] - mn[0], eh = mx[1] - mn[1];
            Assert.Equal(40000, ew, 6);
            Assert.Equal(4000, eh, 6);
            var a3 = SheetLogic.Build("A3", null);
            Assert.True(SheetLogic.FitScale(a3, 1, ew, eh) < SheetLogic.FitScale(a3, 1, 4000, 40000));
            int n = SheetLogic.FitScale(a3, 1, ew, eh);
            Assert.True(SheetLogic.Fits(a3, n, 1, ew, eh));
            Assert.False(SheetLogic.Fits(a3, n, 1, 4000, 40000)); // the WCS box would not fit at that scale
        }

        // ------------------------------------------------------------ F2T-4: unit names of the dimension style

        [Theory]
        [InlineData(1, "VN-100")]
        [InlineData(10, "VN-100-CM")]
        [InlineData(25.4, "VN-100-IN")]
        [InlineData(100, "VN-100-DM")]
        [InlineData(304.8, "VN-100-FT")]
        [InlineData(1000, "VN-100-M")]
        public void Dim_style_names_say_the_drawing_unit(double mmPerUnit, string name) =>
            Assert.Equal(name, StandardsLogic.DimStyleName("VN", 100, mmPerUnit));

        [Fact]
        public void Every_insunits_gets_a_matching_name()
        {
            foreach (var (code, suffix) in new[] { (4, ""), (0, ""), (5, "-CM"), (14, "-DM"), (6, "-M"), (1, "-IN"), (2, "-FT") })
            {
                double f = FloorPlanLogic.MmPerUnit(code, out _);
                Assert.Equal("KT-50" + suffix, StandardsLogic.DimStyleName("KT", 50, f));
            }
        }

        // ------------------------------------------------------------ cross: work modes, plans, macros, metres

        [Fact]
        public void A_full_machine_in_3d_mode_is_told_to_switch_the_mode_for_the_2d_tools()
        {
            foreach (var t in new[] { "setup_standards", "create_sheet" })
            {
                Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), t));
                var m = WorkModeLogic.BlockedMessage(t, "3d");
                Assert.Contains("Đang ở chế độ 3D, đổi sang 2D hoặc Cả hai", m);
                Assert.DoesNotContain("plugin owner", m);
                // a real 3D machine: the plan message
                Assert.Contains("plugin owner", PlanLogic.BlockedMessage(t, "3d", ""));
            }
            foreach (var t in new[] { "insert_library_block", "layer_state" })
                foreach (var mode in WorkModeLogic.Modes)
                    Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", mode), t), t + " " + mode);
        }

        [Fact]
        public void A_library_block_recorded_in_a_ucs_replays_in_that_ucs()
        {
            var f = new UcsFrame(new[] { 5000.0, 0, 0 }, new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 });
            var call = new Dictionary<string, object> { ["name"] = "cua_di", ["position"] = new List<object> { 0.0, 0.0 } };
            var rec = UcsLogic.ForMacro(call, f);
            var replay = UcsLogic.MarkReplay((Dictionary<string, object>)JsonCodec.Parse(JsonCodec.Serialize(rec)));
            var g = UcsLogic.TakeMacroFrame(replay);
            Assert.Equal(5000, g.ToWcs(new[] { 0.0, 0 })[0], 6);
            Assert.False(replay.ContainsKey(UcsLogic.MacroKey));
            // the settings tools keep no UCS of their own
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("layer_state", new Dictionary<string, object> { ["action"] = "save" }));
        }

        [Fact]
        public void A_metre_drawing_gets_metre_numbers_and_millimetre_symbols()
        {
            const double m = 1000;
            var th = StandardsLogic.TextHeights(100, m);
            Assert.Equal(0.35, (double)th["room_name"], 9);
            Assert.Equal(0.05, StandardsLogic.LtScale(100, m), 9);
            Assert.Equal(10, SheetLogic.CustomScale(100, m), 9); // 1 m of model = 10 mm on paper at 1:100
            var a3 = SheetLogic.Build("A3", null);
            Assert.True(SheetLogic.Fits(a3, 100, m, 30, 20));   // a 30 × 20 m house fits A3 at 1:100
            Assert.False(SheetLogic.Fits(a3, 100, m, 50, 30));
            // library sizes stay in mm whatever the units (the plugin divides by mm per unit when drawing)
            var door = LibraryBlocks.Require("cua_di");
            Assert.Equal(900, LibraryBlocks.Sizes(door, null, null).w, 9);
            Assert.Contains("q[0] / f, q[1] / f", Tools);
        }

        [Fact]
        public void Overwrite_refusals_name_the_card_and_change_nothing()
        {
            var msg = F2Confirm.CardMessage("layer_state", "delete the layer state 'IN'", new[] { "1F" });
            Assert.Contains("request_confirmation", msg);
            Assert.Contains("Nothing was changed", msg);
            Assert.Contains("1F", msg);
            Assert.Contains("A typed \"ok\" does not count", msg);
        }
    }
}
