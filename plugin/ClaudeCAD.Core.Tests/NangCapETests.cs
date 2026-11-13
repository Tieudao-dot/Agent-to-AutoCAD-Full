using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt E (13/11/2026, docs/ban-giao/nang-cap-e.md): stairs (straight / L / U) and roofs (flat / gable / hip).
    /// AutoCAD-free geometry (Core/StairRoof.cs) checked point by point; the AutoCAD part is checked in the sources.
    /// </summary>
    public class NangCapETests
    {
        public static readonly string[] NewTools = { "build_stair", "build_roof" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StairRoofTools.cs");

        private static StairLogic.Options Stair(string type, double h = 3300) => new StairLogic.Options { Type = type, Height = h };

        private static bool InStair(StairLogic.Layout l, double x, double y, double z) => l.Pieces.Any(p => p.Contains(new[] { x, y, z }));

        // ------------------------------------------------------------ labels

        [Fact]
        public void New_tools_are_3d_and_registered()
        {
            foreach (var t in NewTools)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.Contains($"\"{t}\"", Tools);
            }
            Assert.Contains("RegisterStairRoof(d)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }

        [Fact]
        public void Plugin_builds_from_the_layout_and_checks_the_well()
        {
            var t = Tools;
            // prisms aligned with a right-handed frame (no mirror transform on a solid)
            Assert.Contains("pm.Normalized()", t);
            Assert.Contains("Matrix3d.AlignCoordinateSystem", t);
            // the well: a start that sticks out, or nothing fits → refused, nothing built
            Assert.Contains("StairLogic.Outside(lay, frame", t);
            Assert.Contains("StairLogic.FitInWell(lay, well, o", t);
            Assert.Contains("does not fit the well", t);
            // the railing solids are listed in "handles" too (created in this turn)
            Assert.Contains("((List<object>)r[\"handles\"]).AddRange(rids", t);
            // the roof: base ∩ top planes − ∩ underside planes; on the highest wall solid under the outline
            Assert.Contains("BooleanOperationType.BoolIntersect", t);
            Assert.Contains("BooleanOperationType.BoolSubtract", t);
            Assert.Contains("walls.Max(e => e.MaxPoint.Z) * f", t);
            // the self-test has an đợt E step
            var st = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            Assert.Contains("Nâng cấp đợt E", st);
            Assert.Contains("DrawingService.BuildStair", st);
            Assert.Contains("DrawingService.BuildRoof", st);
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            foreach (var n in NewTools) Assert.Contains($"[\"{n}\"]", labels);
        }

        // ------------------------------------------------------------ stairs: risers

        [Theory]
        [InlineData(3300, 160, 21)]   // 157.1 mm
        [InlineData(3000, 160, 19)]   // 18.75 → 19, 157.9 mm
        [InlineData(3600, 160, 23)]   // 22.5 → 23 (half rounds up), 156.5 mm
        [InlineData(3300, 175, 19)]   // 18.86 → 19, 173.7 mm
        [InlineData(250, 160, 2)]     // at least 2
        public void Riser_count_is_the_whole_number_nearest_the_wanted_riser(double h, double r, int n)
        {
            Assert.Equal(n, StairLogic.RiserCount(h, r));
            var l = StairLogic.Build(new StairLogic.Options { Type = "straight", Height = h < 300 ? 300 : h, Riser = r });
            Assert.Equal(l.Risers * l.RiserHeight, h < 300 ? 300 : h, 6);
        }

        [Fact]
        public void Defaults_are_the_agreed_numbers()
        {
            Assert.Equal(160, StairLogic.DefaultRiser);
            Assert.Equal(280, StairLogic.DefaultTread);
            Assert.Equal(1000, StairLogic.DefaultWidth);
            Assert.Equal(150, RoofLogic.DefaultThickness);
            Assert.Equal(30, RoofLogic.DefaultSlopeDeg);
            Assert.Equal(600, RoofLogic.DefaultOverhang);
            Assert.Equal("A-STAIR-3D", StairLogic.Layer3d);
            Assert.Equal("A-ROOF-3D", RoofLogic.Layer3d);
        }

        [Fact]
        public void Bad_numbers_are_refused_with_the_range()
        {
            Assert.Contains("height", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 0 })).Message);
            Assert.Contains("riser", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 3300, Riser = 400 })).Message);
            Assert.Contains("width", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 3300, Width = 100 })).Message);
            Assert.Contains("type", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 3300, Type = "spiral" })).Message);
            Assert.Contains("at least 4", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 450, Type = "l" })).Message);
            Assert.Contains("first_flight_risers", Assert.Throws<ToolException>(() => StairLogic.Build(new StairLogic.Options { Height = 3300, Type = "u", FirstRisers = 20 })).Message);
        }

        [Theory]
        [InlineData("1 vế", "straight")]
        [InlineData("chữ L", "l")]
        [InlineData("U", "u")]
        [InlineData("dog-leg", "u")]
        public void Stair_type_aliases(string given, string want) => Assert.Equal(want, StairLogic.NormalizeType(given));

        // ------------------------------------------------------------ stairs: geometry

        [Fact]
        public void Straight_flight_has_its_steps_and_waist()
        {
            var l = StairLogic.Build(Stair("straight"));
            Assert.Equal(21, l.Risers);
            double r = l.RiserHeight, t = 280;
            Assert.Single(l.Pieces);
            Assert.Equal(20 * t, l.Length, 6); // in the well; the top ledge rests on the upper floor (Test & sửa đợt E, NE-3)
            Assert.Equal(1000, l.Breadth, 6);
            for (int k = 1; k <= 20; k++)
            {
                double u = (k - 0.5) * t;
                Assert.True(InStair(l, u, 500, k * r - 1), $"tread {k} below its top");
                Assert.False(InStair(l, u, 500, k * r + 1), $"nothing above tread {k}");
                Assert.False(InStair(l, u, 1001, k * r - 1), "nothing beside the flight");
            }
            // the last riser reaches the upper floor
            Assert.True(InStair(l, 20 * t + 50, 500, 3300 - 1));
            // the waist under the middle of the flight: 150 mm thick (perpendicular), nothing below it
            double tan = r / t, cos = t / Math.Sqrt(r * r + t * t), uMid = 10 * t;
            double under = uMid * tan - 150 / cos;
            Assert.True(InStair(l, uMid, 500, under + 2));
            Assert.False(InStair(l, uMid, 500, under - 2));
            Assert.Contains(l.Warnings, w => w.Contains("21 risers"));
            Assert.True(l.Volume > 0);
        }

        [Fact]
        public void L_stair_has_a_square_landing_and_turns()
        {
            var l = StairLogic.Build(Stair("l"));
            int n1 = 11, n2 = 10;
            Assert.Equal(new[] { n1, n2 }, l.Flights.Select(f => f.Risers).ToArray());
            double r = l.RiserHeight, t = 280, R1 = (n1 - 1) * t, L1 = n1 * r;
            Assert.Equal(L1, l.Flights[1].BaseZ, 6);
            Assert.Equal(R1 + 1000, l.Length, 6);
            Assert.Equal(1000 + (n2 - 1) * t, l.Breadth, 6);
            // landing top at L1, 150 thick
            Assert.True(InStair(l, R1 + 500, 500, L1 - 1));
            Assert.False(InStair(l, R1 + 500, 500, L1 + 1));
            Assert.False(InStair(l, R1 + 500, 500, L1 - 151));
            // second flight walks +y: its first tread is one riser above the landing
            Assert.True(InStair(l, R1 + 500, 1000 + 140, L1 + r - 1));
            Assert.False(InStair(l, R1 + 500, 1000 + 140, L1 + r + 1));
            Assert.False(InStair(l, R1 - 500, 1500, 1500), "the corner beside the turn is empty");
            // the flights reach into the landing (the pieces overlap so they unite)
            Assert.True(l.Pieces[0].Contains(new[] { R1 + 50, 500, L1 - 10 }));
            Assert.True(l.Pieces[2].Contains(new[] { R1 + 500, 1000 - 50, L1 - 10 }));
        }

        [Fact]
        public void U_stair_has_two_flights_side_by_side()
        {
            var l = StairLogic.Build(Stair("u"));
            double r = l.RiserHeight, t = 280, R1 = 10 * t, L1 = 11 * r;
            Assert.Equal(2 * 1000 + StairLogic.DefaultGap, l.Breadth, 6);
            // flight 2 walks back (-x) beside flight 1
            Assert.True(InStair(l, R1 - 140, 1100 + 500, L1 + r - 1));
            Assert.False(InStair(l, R1 - 140, 1050, L1 + r - 1), "the gap between the flights is empty");
            Assert.True(InStair(l, R1 + 500, 1050, L1 - 1), "the landing spans both flights and the gap");
            // flight 2 has 9 treads (+ the top ledge on the upper floor): it ends 280 (180) mm short of the start of flight 1
            Assert.Equal(0, l.MinX, 6);
            Assert.Equal(R1 - 9 * t, l.Footprint.Single(q => q.Part == "flight 2").X0, 6);
            Assert.Equal(R1 - (9 * t + StairLogic.TopLedge), l.Footprint.Single(q => q.Part == "top ledge").X0, 6);
        }

        [Fact]
        public void Placed_stair_follows_start_direction_and_turn()
        {
            foreach (var turn in new[] { "left", "right" })
            {
                var o = Stair("l"); o.Turn = turn;
                var l = StairLogic.Build(o);
                var f = StairLogic.FrameFromStart(new[] { 5000.0, 2000 }, 90, o); // walking +y
                var placed = StairLogic.Place(l.Pieces, f, 1000);
                bool In(double x, double y, double z) => placed.Any(p => p.Contains(new[] { x, y, z }));
                double r = l.RiserHeight;
                // first tread straight ahead of the start point, base_z added
                Assert.True(In(5000, 2000 + 140, 1000 + r - 1));
                Assert.True(In(4600, 2000 + 140, 1000 + r - 1));
                Assert.False(In(5600, 2000 + 140, 1000 + r - 1));
                // the second flight goes to the left (-x) or right (+x) of the walking direction
                double R1 = 10 * 280, sx = turn == "left" ? -1 : 1, yMid = 2000 + R1 + 500, z2 = 1000 + 11 * r + r - 1;
                Assert.True(In(5000 + sx * (500 + 140), yMid, z2), turn);
                Assert.False(In(5000 - sx * (500 + 140), yMid, z2), turn);
                Assert.Equal(turn, StairLogic.TurnOf(f));
            }
        }

        [Fact]
        public void Placed_prisms_keep_their_volume_when_mirrored()
        {
            var o = Stair("u"); o.Turn = "right";
            var l = StairLogic.Build(o);
            var f = StairLogic.FrameFromStart(new[] { 0.0, 0 }, 30, o);
            var placed = StairLogic.Place(l.Pieces, f, 0);
            Assert.Equal(l.Volume, placed.Sum(p => p.Volume), 3);
            foreach (var p in placed)
            {
                var (_, w) = p.Normalized();
                var c = Prism3.Cross(p.U, p.V);
                Assert.Equal(1, Math.Abs(Prism3.Dot(w, c)), 9);
            }
        }

        [Fact]
        public void Stair_fits_a_well_or_says_it_does_not()
        {
            var o = Stair("u");
            var l = StairLogic.Build(o);
            // a well just big enough: (R1 + landing) × (2 w + gap) with the stair's min x
            double W = l.Length, H = l.Breadth;
            var well = new List<double[]> { new[] { 0.0, 0 }, new[] { W + 5, 0 }, new[] { W + 5, H + 5 }, new[] { 0.0, H + 5 } };
            var f = StairLogic.FitInWell(l, well, o, false);
            Assert.NotNull(f);
            Assert.Empty(StairLogic.Outside(l, f, well));
            var small = new List<double[]> { new[] { 0.0, 0 }, new[] { W - 300, 0 }, new[] { W - 300, H }, new[] { 0.0, H } };
            Assert.Null(StairLogic.FitInWell(l, small, o, false));
            // straight stair in a well turned 90°: runs along the long side
            var s = Stair("straight", 3000); var ls = StairLogic.Build(s);
            var tall = new List<double[]> { new[] { 0.0, 0 }, new[] { 1200.0, 0 }, new[] { 1200.0, 6000 }, new[] { 0.0, 6000 } };
            var fs = StairLogic.FitInWell(ls, tall, s, false);
            Assert.NotNull(fs);
            Assert.True(Math.Abs(fs.X[1]) > 0.99, "runs along y");
            // a start point that sticks out names the part
            var f2 = StairLogic.FrameFromStart(new[] { 600.0, 5000 }, 90, s);
            Assert.Contains("flight 1", StairLogic.Outside(ls, f2, tall));
        }

        [Fact]
        public void Railing_follows_the_flights_on_the_asked_side()
        {
            var o = Stair("straight", 3000); o.Railing = "left";
            var l = StairLogic.Build(o);
            Assert.NotEmpty(l.Rail);
            Assert.Contains(l.Rail, p => p.Part.StartsWith("handrail"));
            var f = StairLogic.FrameFromStart(new[] { 0.0, 0 }, 0, o); // walking +x: left = +y
            var placed = StairLogic.Place(l.Rail, f, 0);
            double r = l.RiserHeight, u = 5 * 280, nose = r + u * r / 280;
            Assert.Contains(placed, p => p.Contains(new[] { u, 475, nose + 900 - 10 }));
            Assert.DoesNotContain(placed, p => p.Contains(new[] { u, -475, nose + 900 - 10 }));
            o.Railing = "both";
            Assert.Equal(2, StairLogic.Build(o).Rail.Count(p => p.Part.StartsWith("handrail")));
            o.Railing = "none";
            Assert.Empty(StairLogic.Build(o).Rail);
        }

        // ------------------------------------------------------------ roofs

        private static readonly List<double[]> Rect = new List<double[]> { new[] { 0.0, 0 }, new[] { 10000.0, 0 }, new[] { 10000.0, 6000 }, new[] { 0.0, 6000 } };

        [Fact]
        public void Flat_roof_is_a_slab_on_the_walls()
        {
            var lay = RoofLogic.Build(Rect, 6600, new RoofLogic.Options { Type = "flat" });
            Assert.Equal(0, lay.Overhang);
            Assert.Equal(10000.0 * 6000 * 150, lay.Base.Volume, 3);
            Assert.True(lay.Contains(new[] { 5000.0, 3000, 6700 }));
            Assert.False(lay.Contains(new[] { 5000.0, 3000, 6599 }));
            Assert.Equal(6750, lay.TopZ, 6);
            var withEave = RoofLogic.Build(Rect, 6600, new RoofLogic.Options { Type = "flat", Overhang = 500 });
            Assert.Equal(11000.0 * 7000, withEave.PlanArea, 3);
        }

        private static double MinDist(List<double[]> eave, IEnumerable<int> edges, double x, double y) =>
            edges.Min(i => { var a = eave[i]; var b = eave[(i + 1) % eave.Count]; var nn = RoofLogic.OutNormal(a, b); return -((x - a[0]) * nn[0] + (y - a[1]) * nn[1]); });

        private static void CheckSloped(RoofLogic.Layout lay, IEnumerable<int> edges, double thick, double slope)
        {
            double tan = Math.Tan(slope * Math.PI / 180), up = thick / Math.Cos(slope * Math.PI / 180);
            var rnd = new Random(7);
            double minX = lay.Eave.Min(q => q[0]), maxX = lay.Eave.Max(q => q[0]), minY = lay.Eave.Min(q => q[1]), maxY = lay.Eave.Max(q => q[1]);
            for (int i = 0; i < 400; i++)
            {
                double x = minX + 20 + rnd.NextDouble() * (maxX - minX - 40), y = minY + 20 + rnd.NextDouble() * (maxY - minY - 40);
                double d = MinDist(lay.Eave, edges, x, y);
                if (d < 20) continue;
                double bottom = lay.EaveZ + d * tan;
                Assert.True(lay.Contains(new[] { x, y, bottom + 5 }), $"in the slab at {x:0},{y:0}");
                Assert.True(lay.Contains(new[] { x, y, bottom + up - 5 }), $"in the slab at {x:0},{y:0} (top)");
                Assert.False(lay.Contains(new[] { x, y, bottom - 5 }), $"under the slab at {x:0},{y:0}");
                Assert.False(lay.Contains(new[] { x, y, bottom + up + 5 }), $"above the slab at {x:0},{y:0}");
            }
        }

        [Fact]
        public void Hip_roof_on_a_rectangle()
        {
            var lay = RoofLogic.Build(Rect, 3300, new RoofLogic.Options { Type = "hip" });
            Assert.Equal(600, lay.Overhang);
            Assert.Equal(4, lay.Outer.Count);
            double tan = Math.Tan(30 * Math.PI / 180), up = 150 / Math.Cos(30 * Math.PI / 180);
            Assert.Equal(3300 - 600 * tan, lay.EaveZ, 6);
            Assert.Equal(lay.EaveZ + 3600 * tan + up, lay.RidgeZ, 3);
            Assert.Equal(11200 - 7200, lay.RidgeLength, 6);
            Assert.Equal(11200.0 * 7200 / Math.Cos(30 * Math.PI / 180), lay.RoofArea, 3);
            // the underside meets the wall top at the wall line
            Assert.True(lay.Contains(new[] { 5000.0, 0 + 1, 3300 + 5 }));
            Assert.False(lay.Contains(new[] { 5000.0, 0 + 1, 3300 - 5 }));
            CheckSloped(lay, new[] { 0, 1, 2, 3 }, 150, 30);
        }

        [Fact]
        public void Gable_roof_slopes_to_the_long_sides()
        {
            var lay = RoofLogic.Build(Rect, 3300, new RoofLogic.Options { Type = "gable", SlopeDeg = 25, Overhang = 400 });
            Assert.Equal(2, lay.Outer.Count);
            Assert.Equal(10800, lay.RidgeLength, 6);
            var e = lay.Eave;
            int first = RoofLogic.Dist(e[0], e[1]) > RoofLogic.Dist(e[1], e[2]) ? 0 : 1;
            CheckSloped(lay, new[] { first, first + 2 }, 150, 25);
            var shortRidge = RoofLogic.Build(Rect, 3300, new RoofLogic.Options { Type = "gable", Ridge = "short" });
            Assert.Equal(7200, shortRidge.RidgeLength, 6);
            Assert.True(shortRidge.RidgeZ > lay.RidgeZ);
        }

        [Fact]
        public void Hip_roof_on_a_convex_polygon_and_refusals()
        {
            var hex = Enumerable.Range(0, 6).Select(i => new[] { 5000 * Math.Cos(i * Math.PI / 3), 5000 * Math.Sin(i * Math.PI / 3) }).ToList();
            var lay = RoofLogic.Build(hex, 3000, new RoofLogic.Options { Type = "hip", Overhang = 0 });
            CheckSloped(lay, Enumerable.Range(0, 6), 150, 30);
            Assert.Equal(3000 + 5000 * Math.Cos(Math.PI / 6) * Math.Tan(Math.PI / 6) + 150 / Math.Cos(Math.PI / 6), lay.RidgeZ, 0);
            var ell = new List<double[]> { new[] { 0.0, 0 }, new[] { 8000.0, 0 }, new[] { 8000.0, 4000 }, new[] { 4000.0, 4000 }, new[] { 4000.0, 8000 }, new[] { 0.0, 8000 } };
            Assert.Contains("convex", Assert.Throws<ToolException>(() => RoofLogic.Build(ell, 3000, new RoofLogic.Options { Type = "hip" })).Message);
            Assert.Contains("rectangle", Assert.Throws<ToolException>(() => RoofLogic.Build(ell, 3000, new RoofLogic.Options { Type = "gable" })).Message);
            // a flat roof takes any outline, the overhang mitred at the inward corner
            var flat = RoofLogic.Build(ell, 3000, new RoofLogic.Options { Type = "flat", Overhang = 500 });
            Assert.Equal(6, flat.Eave.Count);
            Assert.Equal(8000.0 * 8000 - 4000 * 4000 + 500 * (8000 * 4) + 500 * 500 * 4, flat.PlanArea, 3);
            Assert.Contains("slope", Assert.Throws<ToolException>(() => RoofLogic.Build(Rect, 3000, new RoofLogic.Options { Type = "hip", SlopeDeg = 80 })).Message);
            Assert.Contains("crosses", Assert.Throws<ToolException>(() => RoofLogic.Build(new List<double[]> { new[] { 0.0, 0 }, new[] { 100.0, 0 }, new[] { 100.0, 100 }, new[] { 60.0, -50 } }, 0, new RoofLogic.Options { Type = "flat" })).Message);
        }

        [Theory]
        [InlineData("mái bằng", "flat")]
        [InlineData("2 dốc", "gable")]
        [InlineData("4 mái", "hip")]
        public void Roof_type_aliases(string given, string want) => Assert.Equal(want, RoofLogic.NormalizeType(given));
    }
}
