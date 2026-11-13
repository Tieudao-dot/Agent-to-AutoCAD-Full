using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa sau kiểm tra độc lập nâng cấp (docs/ban-giao/nang-cap-e.md): KT2-05 a notch of the stair well missed by the
    /// fit check, KT2-08 the overlapping roofs of an L house split into rectangles (tell the user first), KT2-09 the
    /// hip error suggesting gable, and the landing limits shared with the assistant.
    /// </summary>
    public class NangCapEKiemTraTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        // 5000 × 1200 well with a 200 × 200 notch (a column) at x 600–800 on the y = 0 side
        private static readonly List<double[]> Notched = new List<double[]>
        {
            new[] { 0.0, 0 }, new[] { 600.0, 0 }, new[] { 600.0, 200 }, new[] { 800.0, 200 }, new[] { 800.0, 0 },
            new[] { 5000.0, 0 }, new[] { 5000.0, 1200 }, new[] { 0.0, 1200 },
        };

        [Fact]
        public void KT2_05_a_notch_between_the_probe_points_does_not_fit()
        {
            var o = new StairLogic.Options { Type = "straight", Height = 1500 };
            var lay = StairLogic.Build(o);
            // flight 1 at y 100–1100 from x 300: the notch (y < 200) cuts into it between the probes (x 300, 860, …)
            var f = StairLogic.FrameFromStart(new[] { 300.0, 600 }, 0, o);
            Assert.Contains("flight 1", StairLogic.Outside(lay, f, Notched));
            // moved up clear of the notch (y 200–1200): fits, a well edge lying on the stair edge is fine
            var f2 = StairLogic.FrameFromStart(new[] { 300.0, 700 }, 0, o);
            Assert.Empty(StairLogic.Outside(lay, f2, Notched));
            // fitted into the well without a start: never through the notch
            var fit = StairLogic.FitInWell(lay, Notched, o, false);
            if (fit != null) Assert.Empty(StairLogic.Outside(lay, fit, Notched));
        }

        [Fact]
        public void KT2_05_a_well_corner_inside_a_part_or_an_edge_across_it()
        {
            var cs = new[] { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 500 }, new[] { 0.0, 500 } };
            // a well corner inside the rectangle
            Assert.True(StairLogic.WellCutsInto(cs, new List<double[]> { new[] { -10.0, -10 }, new[] { 2000.0, -10 }, new[] { 500.0, 250 }, new[] { -10.0, 600 } }, 1));
            // the same rectangle as the well (edges on edges, corners on corners): fits
            Assert.False(StairLogic.WellCutsInto(cs, cs.ToList(), 1));
            // a well 0.5 mm smaller (inside the tolerance): fits
            Assert.False(StairLogic.WellCutsInto(cs, new List<double[]> { new[] { 0.5, 0.5 }, new[] { 999.5, 0.5 }, new[] { 999.5, 499.5 }, new[] { 0.5, 499.5 } }, 1));
        }

        [Fact]
        public void KT2_08_split_roofs_overlap_and_the_user_is_told_first()
        {
            var ell = new List<double[]> { new[] { 0.0, 0 }, new[] { 8000.0, 0 }, new[] { 8000.0, 4000 }, new[] { 4000.0, 4000 }, new[] { 4000.0, 8000 }, new[] { 0.0, 8000 } };
            foreach (var type in new[] { "hip", "gable" })
            {
                var m = Assert.Throws<ToolException>(() => RoofLogic.Build(ell, 3000, new RoofLogic.Options { Type = type })).Message;
                Assert.Contains("overlap at the joint", m);
                Assert.Contains("twice", m);
                Assert.Contains("type flat", m);
            }
            var skill = Src("skills", "ve-3d-kien-truc", "SKILL.md");
            Assert.Contains("**báo trước** khách là hai mái sẽ chồng nhau ở chỗ nối", skill);
            Assert.Contains("overlap at the joint", Src("agent", "src", "tools", "solids3.ts"));
        }

        [Fact]
        public void KT2_09_hip_error_no_longer_suggests_gable_and_landing_limits_match()
        {
            var ell = new List<double[]> { new[] { 0.0, 0 }, new[] { 8000.0, 0 }, new[] { 8000.0, 4000 }, new[] { 4000.0, 4000 }, new[] { 4000.0, 8000 }, new[] { 0.0, 8000 } };
            var m = Assert.Throws<ToolException>(() => RoofLogic.Build(ell, 3000, new RoofLogic.Options { Type = "hip" })).Message;
            Assert.DoesNotContain("use type gable", m);
            Assert.Contains("gable needs a rectangle too", m);
            // the landing: 600–10000 in the plugin and in the schema of the assistant
            Assert.Equal(600, StairLogic.MinLanding);
            Assert.Contains("landing", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Type = "u", Height = 3300, Landing = 550 })).Message);
            Assert.Equal(1, StairLogic.Build(new StairLogic.Options { Type = "u", Height = 3300, Landing = 600 }).Pieces.Count(p => p.Part == "landing"));
            Assert.Contains("landing: z.number().min(E.MinLanding).max(E.MaxLanding)", Src("agent", "src", "tools", "solids3.ts"));
        }
    }
}
