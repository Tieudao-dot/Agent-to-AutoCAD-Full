using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt E (13/11/2026, docs/ban-giao/nang-cap-e-test.md): edge cases of build_stair / build_roof.</summary>
    public class NangCapETestSuaTests
    {
        private static StairLogic.Options Stair(string type, double h = 3300) => new StairLogic.Options { Type = type, Height = h };

        private static bool In(IEnumerable<Prism3> ps, double x, double y, double z) => ps.Any(p => p.Contains(new[] { x, y, z }));

        // ------------------------------------------------------------ stair profiles

        public static IEnumerable<object[]> ProfileCases()
        {
            foreach (var h in new[] { 2700.0, 3000, 3300, 3600, 4500 })
                foreach (var r in new[] { 100.0, 160, 250 })
                    foreach (var t in new[] { 150.0, 280, 500 })
                        foreach (var w in new[] { 50.0, 150, 500 })
                            yield return new object[] { h, r, t, w };
        }

        [Theory]
        [MemberData(nameof(ProfileCases))]
        public void Every_flight_profile_is_a_simple_outline_holding_its_steps(double h, double riser, double tread, double waist)
        {
            foreach (var type in StairLogic.Types)
            {
                var o = new StairLogic.Options { Type = type, Height = h, Riser = riser, Tread = tread, Waist = waist };
                StairLogic.Layout l;
                try { l = StairLogic.Build(o); }
                catch (ToolException) { continue; } // riser outside 100–250 after the split: refused, fine
                foreach (var p in l.Pieces)
                {
                    Assert.False(RoofLogic.SelfIntersects(p.Profile), $"{type} {h}/{riser}/{tread}/{waist}: {p.Part} crosses itself");
                    Assert.True(Math.Abs(Prism3.Area2d(p.Profile)) > 1, $"{type}: {p.Part} empty");
                }
                // the middle of every tread (just under its surface) is solid; just above it is air
                double r = l.RiserHeight;
                var f1 = l.Flights[0];
                for (int k = 1; k < f1.Risers; k++)
                {
                    double x = (k - 0.5) * tread, z = k * r;
                    Assert.True(In(l.Pieces, x, 500, z - 1), $"{type} {h}/{riser}/{tread}/{waist}: tread {k}");
                    Assert.False(In(l.Pieces, x, 500, z + 1), $"{type} {h}/{riser}/{tread}/{waist}: above tread {k}");
                }
                Assert.Equal(h, l.Risers * r, 6);
            }
        }

        // ------------------------------------------------------------ railings on a right turn

        [Theory]
        [InlineData("l")]
        [InlineData("u")]
        public void Right_turn_railing_is_on_the_asked_side_in_the_drawing(string type)
        {
            foreach (var side in new[] { "left", "right" })
            {
                var o = Stair(type); o.Turn = "right"; o.Railing = side;
                var l = StairLogic.Build(o);
                var f = StairLogic.FrameFromStart(new[] { 0.0, 0 }, 0, o); // walking +x; left = +y
                var rail = StairLogic.Place(l.Rail, f, 0);
                double r = l.RiserHeight, u = 3 * 280, z = r + u * r / 280 + 900 - 10;
                double want = side == "left" ? 475 : -475;
                Assert.True(In(rail, u, want, z), $"{type} right turn, railing {side}");
                Assert.False(In(rail, u, -want, z), $"{type} right turn, railing {side}: other side");
            }
        }

        // ------------------------------------------------------------ U stair extremes

        [Fact]
        public void U_stair_with_a_short_first_flight_and_no_gap()
        {
            var o = Stair("u"); o.FirstRisers = 2; o.Gap = 0;
            var l = StairLogic.Build(o);
            Assert.True(l.MinX < 0);
            Assert.Equal(l.MaxX - l.MinX, l.Length, 6);
            o = Stair("u"); o.FirstRisers = 19;
            l = StairLogic.Build(o);
            Assert.Equal(2, l.Flights[1].Risers);
            o = Stair("u"); o.FirstRisers = 20;
            Assert.Throws<ToolException>(() => StairLogic.Build(o));
        }

        // ------------------------------------------------------------ wells

        private static List<double[]> Rotated(IEnumerable<double[]> pts, double deg, double dx, double dy)
        {
            double a = deg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return pts.Select(p => new[] { dx + c * p[0] - s * p[1], dy + s * p[0] + c * p[1] }).ToList();
        }

        [Theory]
        [InlineData("straight", 30)]
        [InlineData("l", 45)]
        [InlineData("u", -20)]
        public void Stair_fits_a_turned_well_given_clockwise_with_extra_points(string type, double deg)
        {
            var o = Stair(type);
            var l = StairLogic.Build(o);
            double W = l.Length + 2, H = l.Breadth + 2;
            // clockwise, with a repeated point and a point in the middle of a side
            var well = Rotated(new[] { new[] { 0.0, 0 }, new[] { 0.0, H }, new[] { W / 2, H }, new[] { W, H }, new[] { W, H }, new[] { W, 0 } }, deg, 7000, -3000);
            var f = StairLogic.FitInWell(l, well, o, false);
            Assert.NotNull(f);
            Assert.Empty(StairLogic.Outside(l, f, well));
            // exact size fits too (on the edges)
            var exact = Rotated(new[] { new[] { 0.0, 0 }, new[] { l.Length, 0 }, new[] { l.Length, l.Breadth }, new[] { 0.0, l.Breadth } }, deg, 7000, -3000);
            Assert.NotNull(StairLogic.FitInWell(l, exact, o, false));
        }

        // NE-1: the walking direction is kept when the stair is fitted into a well
        [Theory]
        [InlineData(0)]
        [InlineData(90)]
        [InlineData(180)]
        [InlineData(270)]
        public void Well_fitting_keeps_the_given_direction(double dir)
        {
            var o = Stair("straight", 3000);
            var l = StairLogic.Build(o);
            var well = new List<double[]> { new[] { 0.0, 0 }, new[] { 6000.0, 0 }, new[] { 6000.0, 6000 }, new[] { 0.0, 6000 } };
            var f = StairLogic.FitInWell(l, well, o, false, dir);
            Assert.NotNull(f);
            double a = dir * Math.PI / 180;
            Assert.Equal(1, f.X[0] * Math.Cos(a) + f.X[1] * Math.Sin(a), 6);
            Assert.Empty(StairLogic.Outside(l, f, well));
            // a narrow well along x cannot take a stair walking along y
            var narrow = new List<double[]> { new[] { 0.0, 0 }, new[] { 6000.0, 0 }, new[] { 6000.0, 1200 }, new[] { 0.0, 1200 } };
            bool alongY = dir == 90 || dir == 270;
            Assert.Equal(alongY, StairLogic.FitInWell(l, narrow, o, false, dir) == null);
            Assert.NotNull(StairLogic.FitInWell(l, narrow, o, false));
        }

        [Fact]
        public void Plugin_passes_the_direction_to_the_well_fitting()
        {
            var t = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "plugin", "ClaudeCAD.Plugin", "Drawing", "StairRoofTools.cs")));
            Assert.Contains("StairLogic.FitInWell(lay, well, o, p.Has(\"turn\"), dir)", t);
            Assert.Contains("but not walking up towards", t);
        }

        // NE-3: a well as long as the treads takes the stair (the top ledge rests on the upper floor)
        [Theory]
        [InlineData("straight")]
        [InlineData("l")]
        [InlineData("u")]
        public void Well_as_long_as_the_treads_fits(string type)
        {
            var o = Stair(type, 3000); // 19 risers
            var l = StairLogic.Build(o);
            Assert.Contains(l.Footprint, q => q.OnUpperFloor && q.Part == "top ledge");
            var well = new List<double[]> { new[] { 0.0, 0 }, new[] { l.Length, 0 }, new[] { l.Length, l.Breadth }, new[] { 0.0, l.Breadth } };
            var f = StairLogic.FitInWell(l, well, o, false);
            Assert.NotNull(f);
            Assert.Empty(StairLogic.Outside(l, f, well));
            if (type == "straight") Assert.Equal(18 * 280, l.Length, 6);
            // the ledge itself is still built: the solid reaches TopLedge past the last riser at the upper floor level
            var placed = StairLogic.Place(l.Pieces, f, 0);
            var top = StairLogic.PlacedFootprint(l, f).Single(q => q.part == "top ledge").corners;
            double cx = top.Average(c => c[0]), cy = top.Average(c => c[1]);
            Assert.True(In(placed, cx, cy, 3000 - 1));
            // (straight / L: past the well edge; a U stair with an odd riser count ends inside the well box, beside flight 1)
            if (type != "u") Assert.False(Prism3.PointInPolygon(well, cx, cy));
            // 100 mm too short is still refused
            var shortWell = new List<double[]> { new[] { 0.0, 0 }, new[] { l.Length - 100, 0 }, new[] { l.Length - 100, l.Breadth }, new[] { 0.0, l.Breadth } };
            if (type == "straight") Assert.Null(StairLogic.FitInWell(l, shortWell, o, false));
        }

        // ------------------------------------------------------------ roofs

        // NE-2: a drafting jog of up to 1 mm is not a corner
        [Fact]
        public void A_millimetre_jog_does_not_make_an_L_house()
        {
            var pts = new List<double[]>
            {
                new[] { 0.0, 0 }, new[] { 5000.0, 0.8 }, new[] { 10000.0, 0 }, new[] { 10000.0, 3000 }, new[] { 10000.4, 3000.2 }, new[] { 10000.0, 6000 },
                new[] { 4000.0, 6000 }, new[] { 4000.0, 6001 }, new[] { 0.0, 6001 },
            };
            Assert.Equal(4, RoofLogic.Clean(pts).Count);
            var hip = RoofLogic.Build(pts, 3000, new RoofLogic.Options { Type = "hip" });
            Assert.True(hip.Rectangle);
            Assert.Equal(4, hip.Eave.Count);
            var gable = RoofLogic.Build(pts, 3000, new RoofLogic.Options { Type = "gable" });
            Assert.Equal(11200, gable.RidgeLength, 0);
            // a real 300 mm step is still an L house for a hip roof
            var step = new List<double[]> { new[] { 0.0, 0 }, new[] { 10000.0, 0 }, new[] { 10000.0, 6000 }, new[] { 4000.0, 6000 }, new[] { 4000.0, 6300 }, new[] { 0.0, 6300 } };
            Assert.Throws<ToolException>(() => RoofLogic.Build(step, 3000, new RoofLogic.Options { Type = "hip" }));
            // a spike (going out and straight back) is dropped as before; a short real corner stays
            Assert.Equal(4, RoofLogic.Clean(new List<double[]> { new[] { 0.0, 0 }, new[] { 10000.0, 0 }, new[] { 10000.0, 6000 }, new[] { 0.0, 6000 }, new[] { 0.0, 3000 }, new[] { -0.5, 3000 }, new[] { 0.0, 3000 } }).Count);
            Assert.Equal(5, RoofLogic.Clean(new List<double[]> { new[] { 0.0, 0 }, new[] { 10000.0, 0 }, new[] { 10000.0, 6000 }, new[] { 50.0, 6000 }, new[] { 0.0, 5950 } }).Count);
        }

        [Fact]
        public void A_well_drawn_with_a_jog_still_takes_the_stair()
        {
            var o = Stair("u");
            var l = StairLogic.Build(o);
            double W = l.Length + 50, H = l.Breadth + 50;
            var well = new List<double[]> { new[] { 0.0, 0 }, new[] { W / 2, 0.6 }, new[] { W, 0 }, new[] { W, H }, new[] { 0.0, H } };
            Assert.NotNull(StairLogic.FitInWell(l, well, o, false));
        }

        [Fact]
        public void Gable_roof_on_a_clockwise_rectangle_with_extra_points()
        {
            var pts = new List<double[]>
            {
                new[] { 0.0, 0 }, new[] { 0.0, 6000 }, new[] { 5000.0, 6000 }, new[] { 10000.0, 6000 }, new[] { 10000.0, 6000 }, new[] { 10000.0, 0 }, new[] { 0.0, 0 },
            };
            var l = RoofLogic.Build(pts, 3000, new RoofLogic.Options { Type = "gable" });
            Assert.Equal(11200, l.RidgeLength, 3);
            double tan = Math.Tan(Math.PI / 6);
            Assert.Equal(3000 - 600 * tan + 3600 * tan + 150 / Math.Cos(Math.PI / 6), l.TopZ, 1);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(60)]
        public void Hip_roof_on_a_triangle_matches_the_planes(double slope)
        {
            var tri = new List<double[]> { new[] { 0.0, 0 }, new[] { 8000.0, 0 }, new[] { 2000.0, 6000 } };
            var o = new RoofLogic.Options { Type = "hip", SlopeDeg = slope, Overhang = 0 };
            var l = RoofLogic.Build(tri, 3000, o);
            double tan = Math.Tan(slope * Math.PI / 180), up = 150 / Math.Cos(slope * Math.PI / 180);
            var rnd = new Random(5);
            for (int i = 0; i < 300; i++)
            {
                double x = rnd.NextDouble() * 8000, y = rnd.NextDouble() * 6000;
                if (!Prism3.PointInPolygon(l.Eave, x, y)) continue;
                double s = double.MaxValue;
                for (int k = 0; k < 3; k++)
                {
                    var a = l.Eave[k]; var b = l.Eave[(k + 1) % 3]; var n = RoofLogic.OutNormal(a, b);
                    s = Math.Min(s, -((x - a[0]) * n[0] + (y - a[1]) * n[1]));
                }
                double under = 3000 + s * tan;
                if (s < 2) continue;
                Assert.True(l.Contains(new[] { x, y, under + up / 2 }));
                Assert.False(l.Contains(new[] { x, y, under - 2 }));
                Assert.False(l.Contains(new[] { x, y, under + up + 2 }));
            }
            Assert.True(l.TopZ <= l.Base.Origin[2] + l.Base.Depth);
        }
    }
}
