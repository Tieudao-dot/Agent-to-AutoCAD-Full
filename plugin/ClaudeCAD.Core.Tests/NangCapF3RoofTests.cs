using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F3: hip / gable roofs over right-angled outlines (L / T / U) as one solid; new default thicknesses.</summary>
    public class NangCapF3RoofTests
    {
        private static List<double[]> P(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new[] { xy[2 * i], xy[2 * i + 1] }).ToList();

        // L: 8 × 4 m bar + 4 × 4 m wing (the KT2-08 house)
        private static readonly List<double[]> Ell = P(0, 0, 8000, 0, 8000, 4000, 4000, 4000, 4000, 8000, 0, 8000);
        // T: bar 12 × 4 m on top, stem 4 m wide, 6 m down
        private static readonly List<double[]> Tee = P(4000, 0, 8000, 0, 8000, 6000, 12000, 6000, 12000, 10000, 0, 10000, 0, 6000, 4000, 6000);
        // U: 12 × 10 m with a 4 × 6 m court open to the top
        private static readonly List<double[]> You = P(0, 0, 12000, 0, 12000, 10000, 8000, 10000, 8000, 4000, 4000, 4000, 4000, 10000, 0, 10000);

        private static List<double[]> Turn(List<double[]> pts, double deg)
        {
            double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180);
            return pts.Select(q => new[] { 1000 + q[0] * c - q[1] * s, 2000 + q[0] * s + q[1] * c }).ToList();
        }

        /// <summary>L∞ distance from p (inside) to the outside of a right-angled polygon whose edges are along x / y.</summary>
        private static double Linf(IList<double[]> poly, double x, double y)
        {
            double d = double.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                double dx = Math.Max(0, Math.Max(Math.Min(a[0], b[0]) - x, x - Math.Max(a[0], b[0])));
                double dy = Math.Max(0, Math.Max(Math.Min(a[1], b[1]) - y, y - Math.Max(a[1], b[1])));
                d = Math.Min(d, Math.Max(dx, dy));
            }
            return d;
        }

        /// <summary>Length (mm) of the roof's vertical column at (x, y), sampled every step mm.</summary>
        private static double Column(RoofLogic.Layout lay, double x, double y, double step = 2)
        {
            int n = 0;
            for (double z = lay.EaveZ - 200; z <= lay.TopZ + 200; z += step) if (lay.Contains(new[] { x, y, z })) n++;
            return n * step;
        }

        private static IEnumerable<double[]> Inside(RoofLogic.Layout lay, int count, double margin, int seed)
        {
            var rnd = new Random(seed);
            double minX = lay.Eave.Min(q => q[0]), maxX = lay.Eave.Max(q => q[0]), minY = lay.Eave.Min(q => q[1]), maxY = lay.Eave.Max(q => q[1]);
            int got = 0;
            for (int i = 0; i < count * 20 && got < count; i++)
            {
                double x = minX + rnd.NextDouble() * (maxX - minX), y = minY + rnd.NextDouble() * (maxY - minY);
                if (!Prism3.PointInPolygon(lay.Eave, x, y)) continue;
                bool nearEdge = false;
                for (int k = 0; k < lay.Eave.Count && !nearEdge; k++)
                {
                    var a = lay.Eave[k]; var b = lay.Eave[(k + 1) % lay.Eave.Count];
                    double dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy;
                    double t = Math.Max(0, Math.Min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / l2));
                    double ex = a[0] + t * dx - x, ey = a[1] + t * dy - y;
                    nearEdge = ex * ex + ey * ey < margin * margin;
                }
                if (nearEdge) continue;
                got++;
                yield return new[] { x, y };
            }
        }

        private static void HipMatchesTheSkeleton(List<double[]> outline, int pieces)
        {
            var lay = RoofLogic.Build(outline, 3000, new RoofLogic.Options { Type = "hip" });
            Assert.Equal(pieces, lay.Pieces.Count);
            double tan = Math.Tan(Math.PI / 6), up = 120 / Math.Cos(Math.PI / 6);
            foreach (var q in Inside(lay, 250, 20, 3))
            {
                double bottom = lay.EaveZ + Linf(lay.Eave, q[0], q[1]) * tan;
                Assert.True(lay.Contains(new[] { q[0], q[1], bottom + 3 }), $"in the slab at {q[0]:0},{q[1]:0}");
                Assert.True(lay.Contains(new[] { q[0], q[1], bottom + up - 3 }), $"in the slab (top) at {q[0]:0},{q[1]:0}");
                Assert.False(lay.Contains(new[] { q[0], q[1], bottom - 3 }), $"under the slab at {q[0]:0},{q[1]:0}");
                Assert.False(lay.Contains(new[] { q[0], q[1], bottom + up + 3 }), $"above the slab at {q[0]:0},{q[1]:0}");
            }
        }

        [Fact]
        public void Hip_roof_over_an_l_house_follows_the_valley() => HipMatchesTheSkeleton(Ell, 2);

        [Fact]
        public void Hip_roof_over_a_t_house_follows_the_valleys() => HipMatchesTheSkeleton(Tee, 2);

        [Fact]
        public void Hip_roof_over_a_u_house_follows_the_valleys() => HipMatchesTheSkeleton(You, 3);

        [Theory]
        [InlineData("hip")]
        [InlineData("gable")]
        public void One_slab_everywhere_no_overlap_so_the_volume_is_area_times_thickness(string type)
        {
            foreach (var outline in new[] { Ell, Tee, You, Turn(Ell, 37), Turn(Tee, -120) })
            {
                var lay = RoofLogic.Build(outline, 3000, new RoofLogic.Options { Type = type });
                Assert.True(lay.Pieces.Count >= 2);
                double up = 120 / Math.Cos(Math.PI / 6);
                foreach (var q in Inside(lay, 60, 40, 11))
                {
                    // the vertical thickness is the slab's everywhere: where two pieces meet the lower one is cut away
                    double c = Column(lay, q[0], q[1]);
                    Assert.True(Math.Abs(c - up) <= 4, $"{type}: column {c:0.0} mm at {q[0]:0},{q[1]:0} (slab {up:0.0})");
                }
            }
        }

        [Theory]
        [InlineData("hip")]
        [InlineData("gable")]
        public void The_underside_meets_the_wall_top_at_the_sloped_wall_lines(string type)
        {
            var lay = RoofLogic.Build(Ell, 3300, new RoofLogic.Options { Type = type });
            // hip: every wall; gable: the long sides of the main roof (y = 0, y = 4000 beyond the wing) and the wing's sides
            var walls = type == "hip"
                ? new[] { new[] { 6000.0, 1 }, new[] { 7999.0, 2000 }, new[] { 6000.0, 3999 }, new[] { 4001.0, 6000 }, new[] { 2000.0, 7999 }, new[] { 1.0, 5000 } }
                : new[] { new[] { 6000.0, 1 }, new[] { 6000.0, 3999 }, new[] { 4001.0, 6000 }, new[] { 1.0, 6000 } };
            foreach (var w in walls)
            {
                Assert.True(lay.Contains(new[] { w[0], w[1], 3300 + 4 }), $"{type}: slab on the wall at {w[0]},{w[1]}");
                Assert.False(lay.Contains(new[] { w[0], w[1], 3300 - 4 }), $"{type}: nothing in the wall at {w[0]},{w[1]}");
            }
        }

        [Fact]
        public void Gable_over_an_l_house_has_one_main_ridge_and_the_wing_ends_in_it()
        {
            var lay = RoofLogic.Build(Ell, 3000, new RoofLogic.Options { Type = "gable" });
            Assert.Equal(2, lay.Pieces.Count);
            double tan = Math.Tan(Math.PI / 6), up = 120 / Math.Cos(Math.PI / 6);
            // main roof = the 8 m bar (+ 600 eaves), ridge along x at y = 2000; the wing slopes to x = 0 and x = 4000
            Assert.Equal(lay.EaveZ + (2000 + 600) * tan + up, lay.TopZ, 6);
            // on the main ridge line inside the bar: the top is the ridge
            Assert.True(lay.Contains(new[] { 6000.0, 2000, lay.TopZ - 2 }));
            // in the wing, half way along it: the top follows the wing's own slopes (across x)
            double wingTop = lay.EaveZ + (600 + 1000) * tan + up; // 1000 mm in from the wall x = 0
            Assert.True(lay.Contains(new[] { 1000.0, 6000, wingTop - 3 }));
            Assert.False(lay.Contains(new[] { 1000.0, 6000, wingTop + 3 }));
            Assert.Contains(lay.Notes, n => n.Contains("gable ends are open"));
        }

        [Fact]
        public void Rectangles_keep_the_roof_of_before()
        {
            var rect = P(0, 0, 10000, 0, 10000, 6000, 0, 6000);
            foreach (var type in new[] { "hip", "gable", "flat" })
            {
                var lay = RoofLogic.Build(rect, 3300, new RoofLogic.Options { Type = type });
                Assert.Empty(lay.Pieces);
                Assert.NotNull(lay.Base);
            }
            // a convex polygon (hexagon) keeps the old hip as well
            var hex = Enumerable.Range(0, 6).Select(i => new[] { 5000 * Math.Cos(i * Math.PI / 3), 5000 * Math.Sin(i * Math.PI / 3) }).ToList();
            Assert.Empty(RoofLogic.Build(hex, 3000, new RoofLogic.Options { Type = "hip" }).Pieces);
            // flat over an L: one slab as before
            Assert.Empty(RoofLogic.Build(Ell, 3000, new RoofLogic.Options { Type = "flat" }).Pieces);
        }

        [Fact]
        public void Maximal_rectangles_of_l_t_u_and_a_plus()
        {
            Assert.Equal(2, RoofLogic.MaximalRectangles(Ell).Count);
            Assert.Equal(2, RoofLogic.MaximalRectangles(Tee).Count);
            Assert.Equal(3, RoofLogic.MaximalRectangles(You).Count);
            var plus = P(4000, 0, 8000, 0, 8000, 4000, 12000, 4000, 12000, 8000, 8000, 8000, 8000, 12000, 4000, 12000, 4000, 8000, 0, 8000, 0, 4000, 4000, 4000);
            var r = RoofLogic.MaximalRectangles(plus);
            Assert.Equal(2, r.Count);
            Assert.Contains(r, q => q.W == 12000 && q.H == 4000);
            Assert.Contains(r, q => q.W == 4000 && q.H == 12000);
            // gable over the plus: the main bar + the two arms of the other bar
            Assert.Equal(3, RoofLogic.Build(plus, 3000, new RoofLogic.Options { Type = "gable", Overhang = 0 }).Pieces.Count);
        }

        [Fact]
        public void Outlines_that_are_too_complex_or_not_right_angled_are_refused_with_options()
        {
            // a staircase of 15 steps (32 corners): 15 roof pieces, more than allowed
            int n = 15;
            var stairs = new List<double[]> { new[] { 0.0, 0 }, new[] { n * 1000.0, 0 } };
            for (int k = 1; k <= n; k++) { stairs.Add(new[] { (n - k + 1) * 1000.0, k * 1000.0 }); stairs.Add(new[] { (n - k) * 1000.0, k * 1000.0 }); }
            stairs.RemoveAt(stairs.Count - 1); // the last corner is (0, n): close back to (0, 0)
            stairs.Add(new[] { 0.0, n * 1000.0 });
            var m = Assert.Throws<ToolException>(() => RoofLogic.Build(stairs, 3000, new RoofLogic.Options { Type = "hip", Overhang = 0 })).Message;
            Assert.Contains("roof pieces", m);
            Assert.Contains("type flat", m);
            // a comb with 12 teeth: 52 corners, more than allowed
            var comb = new List<double[]> { new[] { 0.0, 0 } };
            double x = 0;
            for (int i = 0; i < 12; i++)
            {
                comb.Add(new[] { x + 1000, 0.0 }); comb.Add(new[] { x + 1000, -3000.0 }); comb.Add(new[] { x + 2000, -3000.0 }); comb.Add(new[] { x + 2000, 0.0 });
                x += 2000;
            }
            comb.Add(new[] { x + 1000, 0.0 }); comb.Add(new[] { x + 1000, 3000.0 }); comb.Add(new[] { 0.0, 3000 });
            var m2 = Assert.Throws<ToolException>(() => RoofLogic.Build(comb, 3000, new RoofLogic.Options { Type = "hip", Overhang = 0 })).Message;
            Assert.Contains("corners", m2);
            Assert.Contains("type flat", m2);
            // a concave outline with a slanted wall
            var slanted = P(0, 0, 8000, 0, 8000, 4000, 4000, 5000, 4000, 8000, 0, 8000);
            foreach (var type in new[] { "hip", "gable" })
                Assert.Contains("right-angled", Assert.Throws<ToolException>(() => RoofLogic.Build(slanted, 3000, new RoofLogic.Options { Type = type })).Message);
            Assert.True(RoofLogic.IsRightAngled(Ell));
            Assert.True(RoofLogic.IsRightAngled(Turn(You, 33)));
            Assert.False(RoofLogic.IsRightAngled(slanted));
        }

        // ------------------------------------------------------------ defaults (chủ dự án chốt 25/11/2026)

        [Fact]
        public void Sloped_roofs_default_to_120_flat_stays_150_and_given_numbers_are_kept()
        {
            var rect = P(0, 0, 10000, 0, 10000, 6000, 0, 6000);
            var flat = new RoofLogic.Options { Type = "flat" };
            RoofLogic.Check(flat);
            Assert.Equal(150, flat.Thickness);
            foreach (var t in new[] { "gable", "hip" })
            {
                var o = new RoofLogic.Options { Type = t };
                RoofLogic.Check(o);
                Assert.Equal(120, o.Thickness);
            }
            // a macro recorded with thickness 150 builds the very same roof as before
            var old = RoofLogic.Build(rect, 3300, new RoofLogic.Options { Type = "hip", Thickness = 150 });
            Assert.Equal(3300 - 600 * Math.Tan(Math.PI / 6) + 3600 * Math.Tan(Math.PI / 6) + 150 / Math.Cos(Math.PI / 6), old.TopZ, 3);
            Assert.Contains("default 120 for a sloped roof", Assert.Throws<ToolException>(() => RoofLogic.Check(new RoofLogic.Options { Type = "hip", Thickness = 5 })).Message);
            Assert.Contains("default 150 for a flat roof", Assert.Throws<ToolException>(() => RoofLogic.Check(new RoofLogic.Options { Type = "flat", Thickness = 5 })).Message);
        }

        [Fact]
        public void Stair_waist_defaults_to_120_the_gap_stays_100_and_given_numbers_are_kept()
        {
            Assert.Equal(120, StairLogic.DefaultWaist);
            Assert.Equal(100, StairLogic.DefaultGap);
            Assert.Equal(160, StairLogic.DefaultRiser);
            Assert.Equal(280, StairLogic.DefaultTread);
            Assert.Equal(1000, StairLogic.DefaultWidth);
            Assert.Equal(30, RoofLogic.DefaultSlopeDeg);
            Assert.Equal(600, RoofLogic.DefaultOverhang);
            var a = StairLogic.Build(new StairLogic.Options { Type = "straight", Height = 3200 });
            var b = StairLogic.Build(new StairLogic.Options { Type = "straight", Height = 3200, Waist = 150 });
            Assert.True(b.Volume > a.Volume, "a thicker waist (macro with waist 150) is the old, bigger stair");
        }
    }
}
