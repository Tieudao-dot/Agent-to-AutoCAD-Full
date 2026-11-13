using System;
using System.Collections.Generic;
using System.IO;
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
    }
}
