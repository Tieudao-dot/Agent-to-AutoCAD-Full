using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt F3 (25/11/2026, docs/ban-giao/nang-cap-f3-test.md): edge and cross cases of the right-angled roofs,
    /// the materials and the named views, found by a fresh review of the F3 code.
    /// </summary>
    public class NangCapF3TestSuaTests
    {
        private static List<double[]> P(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new[] { xy[2 * i], xy[2 * i + 1] }).ToList();

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly double Tan = Math.Tan(Math.PI / 6), Up = 120 / Math.Cos(Math.PI / 6);

        /// <summary>Highest z (mm) of the roof's column at (x, y), sampled every step mm; NaN when the column is empty.</summary>
        private static double TopAt(RoofLogic.Layout lay, double x, double y, double step = 1)
        {
            double best = double.NaN;
            for (double z = lay.EaveZ - 200; z <= lay.TopZ + 200; z += step) if (lay.Contains(new[] { x, y, z })) best = z;
            return best;
        }

        private static List<double[]> Turn(List<double[]> pts, double deg, double ox = 0, double oy = 0)
        {
            double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180);
            return pts.Select(q => new[] { ox + q[0] * c - q[1] * s, oy + q[0] * s + q[1] * c }).ToList();
        }

        private static double[] TurnPt(double x, double y, double deg, double ox = 0, double oy = 0)
        {
            double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180);
            return new[] { ox + x * c - y * s, oy + x * s + y * c };
        }

        // U, 12 × 10 m: arms 4 m wide on both sides, bar 4 m deep at the bottom (the court is open to the top)
        private static readonly List<double[]> You = P(0, 0, 12000, 0, 12000, 10000, 8000, 10000, 8000, 4000, 4000, 4000, 4000, 10000, 0, 10000);
        // U with arms wider (5 m) than the bar is deep (3 m): the arms are the main roofs
        private static readonly List<double[]> WideArms = P(0, 0, 14000, 0, 14000, 9000, 9000, 9000, 9000, 3000, 5000, 3000, 5000, 9000, 0, 9000);

        // ------------------------------------------------------------ gable U: no gable end sticks out of a wing's slope

        [Theory]
        [InlineData(0)]
        [InlineData(90)]
        [InlineData(180)]
        [InlineData(270)]
        public void Gable_u_house_the_bar_ends_in_both_arms_and_no_gable_sticks_out(double deg)
        {
            var lay = RoofLogic.Build(Turn(WideArms, deg, 500, 700), 3000, new RoofLogic.Options { Type = "gable", Overhang = 0 });
            // along the outer wall of each arm the roof is at its eave: the arm slopes down to it (no gable of the bar there)
            foreach (var (x, y) in new[] { (1.0, 1500.0), (13999.0, 1500.0), (1.0, 6000.0), (13999.0, 6000.0) })
            {
                var q = TurnPt(x, y, deg, 500, 700);
                double top = TopAt(lay, q[0], q[1]);
                Assert.True(Math.Abs(top - (lay.EaveZ + Up)) <= 3, $"{deg}°: roof top {top:0.0} at the outer wall ({x},{y}); eave + slab = {lay.EaveZ + Up:0.0}");
            }
            // the bar's ridge (along x at y = 1500) runs between the arms and stops at their ridges (x = 2500 and 11500)
            var mid = TurnPt(7000, 1500, deg, 500, 700);
            Assert.True(Math.Abs(TopAt(lay, mid[0], mid[1]) - (lay.EaveZ + 1500 * Tan + Up)) <= 3, $"{deg}°: bar ridge");
            // over an arm, between its ridge and its outer wall, the arm's slope (not the bar's ridge) is the top
            var arm = TurnPt(13000, 1500, deg, 500, 700);
            Assert.True(Math.Abs(TopAt(lay, arm[0], arm[1]) - (lay.EaveZ + 1000 * Tan + Up)) <= 3, $"{deg}°: arm slope over the bar");
        }

        [Fact]
        public void Gable_u_house_with_the_bar_as_the_main_roof_and_t_house_keep_their_shape()
        {
            // You: arms 4 m = bar 4 m; whichever is main, the outer walls of the arms are eaves
            var lay = RoofLogic.Build(You, 3000, new RoofLogic.Options { Type = "gable", Overhang = 0 });
            foreach (var (x, y) in new[] { (1.0, 7000.0), (11999.0, 7000.0) })
                Assert.True(Math.Abs(TopAt(lay, x, y) - (lay.EaveZ + Up)) <= 3, $"outer wall at ({x},{y})");
            // thickness stays one slab everywhere
            foreach (var (x, y) in new[] { (2000.0, 2000.0), (6000.0, 2000.0), (10000.0, 2000.0), (2000.0, 8000.0), (10000.0, 8000.0), (3900.0, 3900.0) })
            {
                int n = 0;
                for (double z = lay.EaveZ - 200; z <= lay.TopZ + 200; z += 1) if (lay.Contains(new[] { x, y, z })) n++;
                Assert.True(Math.Abs(n - Up) <= 3, $"column {n} at ({x},{y})");
            }
        }

        // ------------------------------------------------------------ hand-drawn outlines (a corner off by < 1 mm)

        [Theory]
        [InlineData("hip")]
        [InlineData("gable")]
        public void A_hand_drawn_l_house_off_by_under_a_millimetre_is_the_same_roof(string type)
        {
            var exact = P(0, 0, 8000, 0, 8000, 4000, 4000, 4000, 4000, 8000, 0, 8000);
            var hand = P(0, 0, 8000, 0, 8000.6, 4000, 4000, 4000.7, 3999.4, 8000, 0, 8000.5);
            var a = RoofLogic.Build(exact, 3000, new RoofLogic.Options { Type = type });
            var b = RoofLogic.Build(hand, 3000, new RoofLogic.Options { Type = type });
            Assert.Equal(a.Pieces.Count, b.Pieces.Count);
            foreach (var (x, y) in new[] { (2000.0, 2000.0), (6000.0, 2000.0), (2000.0, 6000.0), (3800.0, 3800.0) })
                Assert.True(Math.Abs(TopAt(a, x, y) - TopAt(b, x, y)) <= 3, $"{type}: top at ({x},{y})");
        }

        // ------------------------------------------------------------ narrow wings, very long wings, equal wings

        [Theory]
        [InlineData(600)]
        [InlineData(1000)]
        public void A_very_narrow_wing_still_gives_one_slab(double wing)
        {
            var l = P(0, 0, 10000, 0, 10000, 5000, wing, 5000, wing, 12000, 0, 12000);
            foreach (var type in new[] { "hip", "gable" })
            {
                var lay = RoofLogic.Build(l, 3000, new RoofLogic.Options { Type = type });
                foreach (var (x, y) in new[] { (wing / 2, 9000.0), (5000.0, 2500.0), (wing / 2, 4900.0) })
                {
                    int n = 0;
                    for (double z = lay.EaveZ - 200; z <= lay.TopZ + 200; z += 1) if (lay.Contains(new[] { x, y, z })) n++;
                    Assert.True(Math.Abs(n - Up) <= 3, $"{type} wing {wing}: column {n} at ({x},{y})");
                }
            }
        }

        // ------------------------------------------------------------ materials and views

        [Fact]
        public void The_global_material_is_never_recoloured()
        {
            // Global is the default material of every layer: changing its colour would recolour the whole drawing
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");
            Assert.Contains("MaterialLogic.IsProtected(name)", t);
            Assert.True(MaterialLogic.IsProtected("Global"));
            Assert.True(MaterialLogic.IsProtected(" global "));
            Assert.False(MaterialLogic.IsProtected("Bê tông"));
        }

        [Fact]
        public void Image_paths_and_view_names()
        {
            Assert.EndsWith("ban-ve-sw_iso.png", MaterialLogic.ImagePath(null, Path.GetTempPath(), null, "sw_iso"));
            Assert.EndsWith("nha-Phoi canh 1.png", MaterialLogic.ImagePath(null, Path.GetTempPath(), "nha", "Phoi canh 1"));
            Assert.Throws<ToolException>(() => MaterialLogic.ImagePath("anh.jpg", Path.GetTempPath(), "nha", "v"));
            Assert.NotNull(MaterialLogic.BadViewName("a:b"));
            Assert.NotNull(MaterialLogic.BadViewName(" "));
            Assert.Null(MaterialLogic.BadViewName("Phối cảnh 1"));
            Assert.Equal("glass", MaterialLogic.Find("Kính").Key);
            Assert.Equal("roof_tile", MaterialLogic.Find("roof-tile").Key);
            Assert.Null(MaterialLogic.Find("gạch men"));
        }
    }
}
