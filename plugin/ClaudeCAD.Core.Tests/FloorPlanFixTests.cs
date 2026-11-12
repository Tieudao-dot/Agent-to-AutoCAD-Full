using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test &amp; fix of 3D phase 3 (docs/ban-giao/ve-3d-test-3.md): messy floor plans that the first recognition built
    /// wrongly without a word, and the plugin rules around build_3d_from_plan / section_view that need AutoCAD (source checks).
    /// </summary>
    public class FloorPlanFixTests
    {
        private static readonly PlanOptions O = new PlanOptions();
        private static readonly FloorPlanLogic.Heights H = new FloorPlanLogic.Heights { Wall = 3000, Door = 2100, Sill = 900, Window = 1500 };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string PlanTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
        private static string SectionTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "SectionTools.cs");

        /// <summary>Plan box (xy) of a wall piece: rotation 0 or 90 only in these tests.</summary>
        private static (double x0, double y0, double x1, double y1) Plan(PlanBox b)
        {
            bool along = Math.Abs(Math.Cos(b.RotationDeg * Math.PI / 180)) > 0.5;
            double hx = (along ? b.Length : b.Width) / 2, hy = (along ? b.Width : b.Length) / 2;
            return (b.Center[0] - hx, b.Center[1] - hy, b.Center[0] + hx, b.Center[1] + hy);
        }

        [Fact]
        public void Two_walls_side_by_side_leave_the_space_between_them_unbuilt()
        {
            // wall A: faces y 0 / 200, wall B: faces y 600 / 800 (a 400 shaft between them), x 0 → 5000, ends closed
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 5000, 0, "A1"), new PlanSeg(0, 200, 5000, 200, "A2"),
                new PlanSeg(0, 600, 5000, 600, "B1"), new PlanSeg(0, 800, 5000, 800, "B2"),
                new PlanSeg(0, 0, 0, 200, "c1"), new PlanSeg(5000, 0, 5000, 200, "c2"),
                new PlanSeg(0, 600, 0, 800, "c3"), new PlanSeg(5000, 600, 5000, 800, "c4"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Equal(2, r.Walls.Count);
            Assert.All(r.Walls, w => Assert.Equal(200, w.Thickness, 6));
            var skip = Assert.Single(r.Skipped);
            Assert.Equal("LINE PAIR", skip.Type);
            Assert.Contains("space between two walls", skip.Reason);
            Assert.Contains("A2", skip.Handle);
            Assert.Contains("B1", skip.Handle);
            // nothing is built in the shaft
            Assert.DoesNotContain(FloorPlanLogic.Boxes(r, H), b => Plan(b).y0 < 590 && Plan(b).y1 > 210);
        }

        [Fact]
        public void A_wall_that_steps_from_200_to_100_has_no_bump_beyond_its_faces()
        {
            // outer face y = 0 the whole way; inner face y = 200 for x 0 → 1000, then y = 100 for x 1000 → 2000
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 2000, 0, "O"),
                new PlanSeg(0, 200, 1000, 200, "I1"), new PlanSeg(1000, 100, 2000, 100, "I2"),
                new PlanSeg(1000, 100, 1000, 200, "STEP"),
                new PlanSeg(0, 0, 0, 200, "E1"), new PlanSeg(2000, 0, 2000, 100, "E2"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Equal(2, r.Walls.Count);
            Assert.Empty(r.Skipped);
            foreach (var b in FloorPlanLogic.Boxes(r, H))
            {
                var (x0, y0, x1, y1) = Plan(b);
                Assert.True(x0 >= -1e-6 && x1 <= 2000 + 1e-6 && y0 >= -1e-6, $"box {x0},{y0} – {x1},{y1} outside the wall");
                // past the step only the 100 wall: nothing above y = 100
                if (x1 > 1000 + 1e-6) Assert.True(y1 <= 100 + 1e-6, $"bump: box {x0},{y0} – {x1},{y1} sticks out of the 100 wall");
            }
            // volume = 1000×200 + 1000×100 (the thin wall's overhang into the thick one is inside it)
            double area = FloorPlanLogic.Boxes(r, H).Sum(b => b.Length * b.Width);
            Assert.InRange(area, 1000 * 200 + 1000 * 100, 1000 * 200 + 1000 * 100 + 100 * 100 + 1e-6);
        }

        [Fact]
        public void An_L_of_a_100_and_a_300_centreline_wall_closes_without_sticking_out()
        {
            var c = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 3000, 0, "W100") { Thickness = 100 },
                new PlanSeg(0, 0, 0, 3000, "W300") { Thickness = 300 },
            };
            var r = FloorPlanLogic.Recognize(null, c, null, O);
            Assert.Equal(2, r.Walls.Count);
            var boxes = FloorPlanLogic.Boxes(r, H).Select(Plan).ToList();
            // outer faces: x = −150 (the 300 wall), y = −50 (the 100 wall); the corner is closed
            Assert.Equal(-150, boxes.Min(b => b.x0), 6);
            Assert.Equal(-50, boxes.Min(b => b.y0), 6);
            var fp = FloorPlanLogic.Footprint(r).Value;
            Assert.Equal(-50, fp.min[1], 6);
            // the 100 wall reaches the outer face of the 300 wall (corner square covered)
            Assert.Contains(boxes, b => b.x0 <= -150 + 1e-6 && b.y0 <= -50 + 1e-6);
        }

        [Fact]
        public void Window_lines_drawn_on_the_wall_layer_are_reported_not_built_as_walls()
        {
            // wall 220 (faces y 0 / 220, continuous), a window drawn as two more lines inside it at y 73 / 147
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 5000, 0, "F1"), new PlanSeg(0, 220, 5000, 220, "F2"),
                new PlanSeg(0, 0, 0, 220, "E1"), new PlanSeg(5000, 0, 5000, 220, "E2"),
                new PlanSeg(2000, 73, 3200, 73, "W1"), new PlanSeg(2000, 147, 3200, 147, "W2"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            var wall = Assert.Single(r.Walls);
            Assert.Equal(220, wall.Thickness, 6);
            var handles = r.Skipped.Select(x => x.Handle).ToList();
            Assert.Contains(handles, h => h.Contains("W1"));
            Assert.Contains(handles, h => h.Contains("W2"));
            Assert.DoesNotContain(handles, h => h.Contains("F1") || h.Contains("F2"));
            // each line reported once by the "inside a wall" rule
            Assert.Equal(r.Skipped.Count(x => x.Reason.Contains("inside wall")), r.Skipped.Where(x => x.Reason.Contains("inside wall")).Select(x => x.Handle).Distinct().Count());
        }

        private static List<PlanSeg> Rect(string h, double x0, double y0, double x1, double y1, double over = 0) => new List<PlanSeg>
        {
            new PlanSeg(x0 - over, y0, x1 + over, y0, h + "b"), new PlanSeg(x1, y0 - over, x1, y1 + over, h + "r"),
            new PlanSeg(x1 + over, y1, x0 - over, y1, h + "t"), new PlanSeg(x0, y1 + over, x0, y0 - over, h + "l"),
        };

        [Fact]
        public void Corner_lines_that_overshoot_by_30_give_the_same_walls()
        {
            var clean = new List<PlanSeg>(); clean.AddRange(Rect("o", -100, -100, 4100, 3100)); clean.AddRange(Rect("i", 100, 100, 3900, 2900));
            var messy = new List<PlanSeg>(); messy.AddRange(Rect("o", -100, -100, 4100, 3100, 30)); messy.AddRange(Rect("i", 100, 100, 3900, 2900, 30));
            var a = FloorPlanLogic.Recognize(clean, null, null, O);
            var b = FloorPlanLogic.Recognize(messy, null, null, O);
            Assert.Equal(4, a.Walls.Count);
            Assert.Equal(4, b.Walls.Count);
            Assert.All(b.Walls, w => Assert.Equal(200, w.Thickness, 6));
            Assert.Empty(b.Skipped);
            // the overshoot only adds the 30 stubs (at most 30 × 200 per stub, 8 stubs); never a whole extra wall
            double va = FloorPlanLogic.Boxes(a, H).Sum(x => x.Volume), vb = FloorPlanLogic.Boxes(b, H).Sum(x => x.Volume);
            Assert.InRange(vb - va, -1e-6, 8 * 30 * 200 * 3000.0 + 1e-6);
        }

        [Fact]
        public void A_plan_drawn_a_little_off_square_is_still_recognised()
        {
            // the right wall leans 0.2° (the inner and outer lines both), a common sloppy plan
            double k = Math.Tan(0.2 * Math.PI / 180) * 3000;
            var s = new List<PlanSeg>
            {
                new PlanSeg(-100, -100, 4100, -100, "ob"), new PlanSeg(4100, -100, 4100 + k, 3100, "or"), new PlanSeg(4100 + k, 3100, -100, 3100, "ot"), new PlanSeg(-100, 3100, -100, -100, "ol"),
                new PlanSeg(100, 100, 3900, 100, "ib"), new PlanSeg(3900, 100, 3900 + k, 2900, "ir"), new PlanSeg(3900 + k, 2900, 100, 2900, "it"), new PlanSeg(100, 2900, 100, 100, "il"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Equal(4, r.Walls.Count);
            Assert.Empty(r.Skipped);
        }

        [Fact]
        public void Two_rooms_sharing_a_partition_with_a_door_in_it()
        {
            // 8000 × 4000 by the faces, wall 200, partition 100 at x = 4000 (faces 3950 / 4050) with a 800 door gap
            var s = new List<PlanSeg>();
            s.AddRange(Rect("o", -100, -100, 8100, 4100));
            s.Add(new PlanSeg(100, 100, 3950, 100, "ib1")); s.Add(new PlanSeg(4050, 100, 7900, 100, "ib2"));
            s.Add(new PlanSeg(7900, 100, 7900, 3900, "ir"));
            s.Add(new PlanSeg(7900, 3900, 4050, 3900, "it2")); s.Add(new PlanSeg(3950, 3900, 100, 3900, "it1"));
            s.Add(new PlanSeg(100, 3900, 100, 100, "il"));
            s.Add(new PlanSeg(3950, 100, 3950, 1500, "p1a")); s.Add(new PlanSeg(3950, 2300, 3950, 3900, "p1b"));
            s.Add(new PlanSeg(4050, 100, 4050, 1500, "p2a")); s.Add(new PlanSeg(4050, 2300, 4050, 3900, "p2b"));
            s.Add(new PlanSeg(3950, 1500, 4050, 1500, "j1")); s.Add(new PlanSeg(3950, 2300, 4050, 2300, "j2"));
            var door = new List<PlanSymbol> { new PlanSymbol { Kind = "door", Min = new double[] { 4050, 1500 }, Max = new double[] { 4850, 2300 }, Sources = { "D" } } };
            var r = FloorPlanLogic.Recognize(s, null, door, O);
            Assert.Equal(5, r.Walls.Count);
            Assert.Empty(r.Skipped);
            Assert.Empty(r.OpenGaps); // the two T joints are filled, not left as openings
            var op = Assert.Single(r.Openings);
            Assert.Equal(800, op.Width, 6);
            Assert.Equal(100, r.Walls[op.Wall].Thickness, 6);
        }

        private static PlanResult Room()
        {
            // 6000 × 4000 by the faces, wall 200, a 900 gap in the bottom wall at x 2000 → 2900
            var s = new List<PlanSeg>
            {
                new PlanSeg(-100, -100, 2000, -100, "o1"), new PlanSeg(2900, -100, 6100, -100, "o2"),
                new PlanSeg(6100, -100, 6100, 4100, "o3"), new PlanSeg(6100, 4100, -100, 4100, "o4"), new PlanSeg(-100, 4100, -100, -100, "o5"),
                new PlanSeg(100, 100, 2000, 100, "i1"), new PlanSeg(2900, 100, 5900, 100, "i2"),
                new PlanSeg(5900, 100, 5900, 3900, "i3"), new PlanSeg(5900, 3900, 100, 3900, "i4"), new PlanSeg(100, 3900, 100, 100, "i5"),
                new PlanSeg(2000, -100, 2000, 100, "j1"), new PlanSeg(2900, -100, 2900, 100, "j2"),
            };
            return FloorPlanLogic.Recognize(s, null, null, O);
        }

        [Fact]
        public void Given_opening_at_a_corner_or_past_the_wall_end_is_refused_not_guessed()
        {
            var r = Room();
            Assert.Equal(4, r.Walls.Count);
            int before = r.Openings.Count;
            var corner = FloorPlanLogic.AddGivenOpening(r, "window", new double[] { 6000, 4000 }, 1000, null, null, O);
            Assert.Contains("meet", corner);
            var past = FloorPlanLogic.AddGivenOpening(r, "window", new double[] { 5600, 0 }, 1500, null, null, O);
            Assert.Contains("past the end", past);
            Assert.Equal(before, r.Openings.Count);
            // a window that fits is still fine, and no box of the result leaves the walls
            Assert.Null(FloorPlanLogic.AddGivenOpening(r, "window", new double[] { 4500, 0 }, 1500, 900.0, 2400.0, O));
            foreach (var b in FloorPlanLogic.Boxes(r, H))
            {
                var (x0, y0, x1, y1) = Plan(b);
                Assert.True(x0 >= -100 - 1e-6 && x1 <= 6100 + 1e-6 && y0 >= -100 - 1e-6 && y1 <= 4100 + 1e-6);
            }
        }

        [Fact]
        public void Given_opening_replaces_every_recognised_opening_it_overlaps()
        {
            var r = Room();
            // two recognised openings close together in the right wall (as if from two symbols)
            int right = r.Walls.FindIndex(w => Math.Abs(w.Dir[0]) < 1e-9 && w.Origin[0] > 3000);
            var w0 = r.Walls[right];
            double s = (2000 - w0.Origin[1]) / w0.Dir[1];
            r.Openings.Add(new PlanOpening { Wall = right, S0 = s - 600, S1 = s - 100, Kind = "window", From = "symbol" });
            r.Openings.Add(new PlanOpening { Wall = right, S0 = s + 100, S1 = s + 600, Kind = "window", From = "symbol" });
            Assert.Null(FloorPlanLogic.AddGivenOpening(r, "door", new double[] { 6000, 2000 }, 1400, null, null, O));
            var inRight = r.Openings.Where(x => x.Wall == right).ToList();
            var only = Assert.Single(inRight);
            Assert.Equal("given", only.From);
            Assert.Equal(1400, only.Width, 6);
        }

        [Fact]
        public void A_polyline_width_below_min_thickness_is_a_line_weight_not_a_wall()
        {
            Assert.Equal("line", FloorPlanLogic.WidePolylineRole(20, O));
            Assert.Equal("line", FloorPlanLogic.WidePolylineRole(0, O));
            Assert.Equal("wall", FloorPlanLogic.WidePolylineRole(200, O));
            Assert.Equal("wall", FloorPlanLogic.WidePolylineRole(50, O));
            Assert.Equal("skip", FloorPlanLogic.WidePolylineRole(800, O));
            // a plan in metres: 0.2 is a wall, 0.02 a line weight
            var m = PlanOptions.ForUnits(1000, 50, 600);
            Assert.Equal("wall", FloorPlanLogic.WidePolylineRole(0.2, m));
            Assert.Equal("line", FloorPlanLogic.WidePolylineRole(0.02, m));
            // the plugin uses it for wide polylines on the wall layers and says so in the notes
            Assert.Contains("FloorPlanLogic.WidePolylineRole(cw, input.Options)", PlanTools);
            Assert.Contains("ThinWidePolylines", PlanTools);
        }

        [Fact]
        public void Messy_room_with_print_width_face_lines_gives_the_same_walls_as_clean_lines()
        {
            // the same room as plain lines: what the plugin passes after WidePolylineRole says "line"
            var clean = Room();
            Assert.Equal(4, clean.Walls.Count);
            Assert.Single(clean.OpenGaps);
            Assert.Empty(clean.Skipped);
            Assert.Equal(2, clean.Caps);
        }

        [Fact]
        public void Dry_run_report_lists_skipped_and_earlier_solids()
        {
            var s = PlanTools;
            // skipped is part of Report(), which the dry run returns
            int report = s.IndexOf("private static Dictionary<string, object> Report(", StringComparison.Ordinal);
            Assert.True(report > 0);
            Assert.Contains("[\"skipped\"] = SkipList(res.Skipped)", s.Substring(report));
            // the rerun check runs before the dry run returns, and the dry run reports what it found
            int dup = s.IndexOf("var rerun = ExistingOver(", StringComparison.Ordinal), dry = s.IndexOf("if (dryRun)", StringComparison.Ordinal);
            Assert.True(dup > 0 && dup < dry);
            Assert.Contains("report[\"existing_solids\"]", s);
        }

        [Fact]
        public void Rerun_check_covers_slab_outlines_and_nothing_built_is_an_error()
        {
            var s = PlanTools;
            Assert.Contains("foreach (var (id, _) in input.Slabs)", s);
            Assert.Contains("nothing to build", s);
            Assert.Contains("if (created.Count == 0)", s);
            // the same slab outline twice gives one slab
            Assert.Contains("SameRegion(regions[i].r, regions[j].r)", s);
            // AutoCAD refusing a wall piece: INVALID_PARAMS, nothing built (the turn's transaction rolls back)
            Assert.Contains("AutoCAD could not build a wall piece", s);
            // a result layer name is checked before it is created
            Assert.Contains("ValidateSymbolName(name, \"layer\");", s);
        }

        [Fact]
        public void Section_refusals_are_reported_not_swallowed()
        {
            var s = SectionTools;
            Assert.Contains("refused.Add(", s);
            Assert.Contains("AutoCAD could not cut", s);
            Assert.Contains("AutoCAD could not make the section", s);
            Assert.Contains("AutoCAD could not make the view", s);
            // a locked layer keeps its own clear error
            Assert.Contains("ErrorStatus.OnLockedLayer", s);
        }
    }
}
