using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test tổng thể nâng cấp A → E (13/11/2026, docs/ban-giao/test-tong-the-nang-cap.md): where the phases meet.</summary>
    public class TongTheNangCapTests
    {
        private static string Src(params string[] parts)
        {
            var all = new List<string> { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin", "ClaudeCAD.Plugin" };
            all.AddRange(parts);
            return File.ReadAllText(Path.GetFullPath(Path.Combine(all.ToArray())));
        }

        // TT2-1: a house of 2 storeys (3300, slab 150) with roof_slab: top walls 3300 → 6450, roof slab 6450 → 6600,
        // the slab of storey 1 at 3150 → 3300. The roof goes on the roof slab (6600), never into it.
        [Fact]
        public void Roof_sits_on_a_roof_slab_lying_on_the_wall_top()
        {
            var slabs = new List<(double, double)> { (3150, 3300), (6450, 6600) };
            Assert.Equal(6600, RoofLogic.RoofBase(6450, slabs, out var onSlab));
            Assert.True(onSlab);
        }

        [Fact]
        public void Roof_stays_on_the_walls_without_a_roof_slab()
        {
            // no roof_slab: only the slab between the storeys (under the top walls)
            Assert.Equal(6450, RoofLogic.RoofBase(6450, new List<(double, double)> { (3150, 3300) }, out var onSlab));
            Assert.False(onSlab);
            Assert.Equal(6450, RoofLogic.RoofBase(6450, null, out onSlab));
            Assert.False(onSlab);
            // a slab 5 mm above the wall top is not lying on it (outside the 1 mm tolerance)
            Assert.Equal(6450, RoofLogic.RoofBase(6450, new List<(double, double)> { (6455, 6605) }, out onSlab));
            Assert.False(onSlab);
            // within 1 mm (drawing rounding) it is
            Assert.Equal(6600.5, RoofLogic.RoofBase(6450, new List<(double, double)> { (6450.5, 6600.5) }, out onSlab));
            Assert.True(onSlab);
        }

        [Fact]
        public void Build_roof_looks_for_the_roof_slab_only_when_wall_top_z_is_not_given()
        {
            var t = Src("Drawing", "StairRoofTools.cs");
            Assert.Contains("RoofLogic.RoofBase(wallTop, slabs, out roofOnSlab)", t);
            Assert.Contains("FloorPlanLogic.SlabLayer3d", t);
            // wall_top_z given: used as is (the branch above the slab lookup)
            Assert.True(t.IndexOf("if (p.Has(\"wall_top_z\")) { wallTopMm = p.Num(\"wall_top_z\");", StringComparison.Ordinal) <
                        t.IndexOf("RoofLogic.RoofBase(", StringComparison.Ordinal));
            // a flat roof on a roof slab is a second slab: said in the notes
            Assert.Contains("this flat roof is a second slab on top of it", t);
        }

        // TT2-3: CLAUDECHATSELFTEST had no step for đợt C
        [Fact]
        public void Self_test_has_a_step_for_every_upgrade_phase()
        {
            var t = Src("SelfTest.cs");
            foreach (var phase in new[] { "A", "B", "C", "D", "E" })
                Assert.Contains($"Check(\"Nâng cấp đợt {phase}:", t);
            Assert.Contains("DrawingService.FindReplaceText(ctx, new Params(\"find_replace_text\"", t);
            Assert.Contains("DrawingService.AreaSchedule(ctx, new Params(\"area_schedule\"", t);
            // the replace step goes through the approval like the card (user_confirmed), and is refused without it
            Assert.Contains("[SolidLogic.UserConfirmedParam] = new List<object> { text, mtext }", t);
            Assert.Contains("thay khi chưa duyệt thẻ: không bị từ chối", t);
        }

        // The self-test steps of the phases draw side by side in one test drawing: their x ranges must not meet (a step
        // reading "the walls under the outline" or "texts in the room" would pick up another step's objects).
        [Fact]
        public void Self_test_steps_use_separate_places()
        {
            var t = Src("SelfTest.cs");
            Assert.Contains("const double px = 20000;", t); // plan + đợt A, up to px + 9000 (railing)
            Assert.Contains("const double cx = 32000;", t); // đợt C room 32000–36000
            Assert.Contains("const double bx = 40000;", t); // đợt B, 40000–40300
            Assert.Contains("const double dx = 50000;", t); // đợt D, 50000–71000
            Assert.Contains("const double dx = 90000;", t); // đợt E, 89500–110600
        }

        // ---------------------------------------------------------------- kiểm tra độc lập nâng cấp (KT2)

        private static MacroCall Call(string tool, Dictionary<string, object> p, params string[] result) =>
            new MacroCall { Tool = tool, Params = p, ResultHandles = new List<string>(result) };

        // KT2-01: a window stretch lists the existing objects it caught; they are not "made by the macro", so a later
        // slice of the user's solid S is still refused (KT-10) and the stretch maps nothing on replay.
        [Fact]
        public void Macro_stretch_by_window_does_not_make_the_users_solid_its_own()
        {
            var m = new Macro
            {
                Name = "x",
                Calls = new List<MacroCall>
                {
                    Call("stretch_entities", new Dictionary<string, object> { ["corner1"] = new List<object> { 0.0, 0.0 }, ["corner2"] = new List<object> { 10.0, 10.0 } }, "S1"),
                    Call("slice_solid", new Dictionary<string, object> { ["handle"] = "S1" }, "S1", "N2"),
                },
            };
            var edits = MacroLogic.ExternalSolidEdits(m);
            Assert.Single(edits);
            Assert.Contains("slice_solid (S1)", edits[0]);
            Assert.DoesNotContain("S1", MacroLogic.CreatedHandles(m));
            Assert.Contains("N2", MacroLogic.CreatedHandles(m));
            Assert.Contains("S1", MacroLogic.ExternalHandles(m));
            var map = new Dictionary<string, string>();
            MacroLogic.Learn(map, m.Calls[0], new List<string> { "OTHER" });
            Assert.Empty(map);
        }

        [Fact]
        public void Macro_maps_only_created_handles_of_a_call_that_also_returns_its_inputs()
        {
            // fillet of two lines returns them plus the new arc: only the arc is the macro's
            var c = Call("fillet_entities", new Dictionary<string, object> { ["handle1"] = "L1", ["handle2"] = "L2" }, "L1", "L2", "A3");
            Assert.Equal(new[] { "A3" }, MacroLogic.NewHandles(c));
            var map = new Dictionary<string, string>();
            MacroLogic.Learn(map, c, new List<string> { "L1", "L2", "B3" });
            Assert.Equal("B3", map["A3"]);
            Assert.False(map.ContainsKey("L1"));
            // a drawing call keeps working as before
            var d = Call("draw_solid", new Dictionary<string, object> { ["shape"] = "box" }, "B9");
            MacroLogic.Learn(map, d, new List<string> { "C9" });
            Assert.Equal("C9", map["B9"]);
            // match_properties / lengthen / move: in place
            foreach (var t in new[] { "match_properties", "lengthen_entity", "move_entities", "stretch_entities" })
                Assert.Empty(MacroLogic.NewHandles(Call(t, new Dictionary<string, object>(), "Z1")));
        }

        [Fact]
        public void Macro_built_solid_then_stretched_and_sliced_still_runs()
        {
            var m = new Macro
            {
                Name = "y",
                Calls = new List<MacroCall>
                {
                    Call("draw_solid", new Dictionary<string, object> { ["shape"] = "box" }, "B1"),
                    Call("stretch_entities", new Dictionary<string, object> { ["handles"] = new List<object> { "B1" } }, "B1"),
                    Call("slice_solid", new Dictionary<string, object> { ["handle"] = "B1" }, "B1", "B2"),
                },
            };
            Assert.Empty(MacroLogic.ExternalSolidEdits(m));
        }

        // KT2-20: every labelled tool has a Vietnamese label in the status line
        [Fact]
        public void Every_plan_labelled_tool_has_a_vietnamese_status_label()
        {
            var labels = Src("UI", "ToolLabels.cs");
            var missing = PlanLogic.Groups.Keys.Where(t => !labels.Contains($"[\"{t}\"] = ")).ToList();
            Assert.Empty(missing);
        }

        // KT2-29: the draw_solid saving is the measured 1.1k, not the 2k guess
        [Fact]
        public void Draw_solid_saving_is_the_measured_one()
        {
            var t = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agent", "src", "tools", "solids.ts")));
            Assert.Contains("about 1.1k", t);
            Assert.DoesNotContain("about 2k", t);
        }
        // TT-01 (14/11/2026, thử thật bộ cài): AutoCAD or the plugin's helpers holding files gave Inno's English
        // three-button box. The installer now asks in Vietnamese, stops only its own helpers, never closes AutoCAD.
        [Fact]
        public void Installer_handles_files_in_use_in_vietnamese()
        {
            var iss = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "installer", "ClaudeCAD.iss")));
            Assert.Contains("CloseApplications=no", iss);
            Assert.DoesNotContain("CloseApplications=yes", iss);
            int prep = iss.IndexOf("function PrepareToInstall(var NeedsRestart: Boolean): String;", StringComparison.Ordinal);
            var body = iss.Substring(prep, iss.IndexOf("\nend;", prep, StringComparison.Ordinal) - prep);
            // KT-07 first, then AutoCAD, then the helpers, then the check of the files
            int kt07 = body.IndexOf("if ReplacesOriginal() then DropOriginalStagedUpdate();", StringComparison.Ordinal);
            int acad = body.IndexOf("WaitForAutoCADClosed()", StringComparison.Ordinal);
            int node = body.IndexOf("PluginProcesses('node.exe', ExpandConstant('{app}'), True);", StringComparison.Ordinal);
            int claude = body.IndexOf("PluginProcesses('claude.exe', ExpandConstant('{app}'), True);", StringComparison.Ordinal);
            int busy = body.IndexOf("FileInUse()", StringComparison.Ordinal);
            Assert.True(kt07 >= 0 && kt07 < acad && acad < node && node < claude && claude < busy, "order KT-07 → AutoCAD → helpers → files");
            Assert.Contains("Hãy khởi động lại máy rồi chạy lại bộ cài, trước khi mở AutoCAD.", body);
            // AutoCAD: asked (Thử lại / Hủy), never stopped; helpers: only inside {app}
            Assert.Contains("PluginProcesses('acad.exe', '', False)", iss);
            Assert.DoesNotContain("PluginProcesses('acad.exe', '', True)", iss);
            Assert.Contains("'Hãy lưu bản vẽ và đóng hết AutoCAD, rồi bấm Thử lại.'", iss);
            Assert.Contains("['Thử lại', 'Hủy']", iss);
            Assert.Contains("if (Dir <> '') and (Pos(Prefix, Lowercase(Path)) <> 1) then continue;", iss);
            // the leftover retry box (a file in use while copying) is Vietnamese too
            Assert.Contains("AbortRetryIgnoreRetry=&Thử lại", iss);
            Assert.Contains("AbortRetryIgnoreCancel=Hủy cài đặt", iss);
        }
    }
}
