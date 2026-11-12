using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>3D phase 3: wall / opening recognition of build_3d_from_plan and the section frame (docs/ban-giao/ve-3d-dot-3.md).</summary>
    public class FloorPlanTests
    {
        private static readonly PlanOptions O = new PlanOptions();
        private static readonly FloorPlanLogic.Heights H = new FloorPlanLogic.Heights { Wall = 3300, Door = 2200, Sill = 900, Window = 1500 };

        /// <summary>A polyline as segments (open unless the last point repeats the first).</summary>
        private static IEnumerable<PlanSeg> Poly(string handle, params double[] xy)
        {
            for (int i = 0; i + 3 < xy.Length; i += 2) yield return new PlanSeg(xy[i], xy[i + 1], xy[i + 2], xy[i + 3], handle);
        }

        /// <summary>The room of the ve-kien-truc skill example: 4 × 6 m on the axes, wall 220, door 900 in the right wall.</summary>
        private static List<PlanSeg> SkillRoom()
        {
            var s = new List<PlanSeg>();
            s.AddRange(Poly("A1", 4110, 3450, 4110, 6110, -110, 6110, -110, -110, 4110, -110, 4110, 2550));
            s.AddRange(Poly("A2", 3890, 3450, 3890, 5890, 110, 5890, 110, 110, 3890, 110, 3890, 2550));
            s.Add(new PlanSeg(3890, 2550, 4110, 2550, "A3"));
            s.Add(new PlanSeg(3890, 3450, 4110, 3450, "A4"));
            return s;
        }

        private static List<PlanSymbol> SkillDoor() => new List<PlanSymbol>
        {
            new PlanSymbol { Kind = "door", Min = new double[] { 2990, 2550 }, Max = new double[] { 3890, 2550 }, Sources = { "D1" } }, // leaf
            new PlanSymbol { Kind = "door", Min = new double[] { 2990, 2550 }, Max = new double[] { 3890, 3450 }, Sources = { "D2" } }, // arc
        };

        private static double WallVolume(PlanResult r) => FloorPlanLogic.Boxes(r, H).Sum(b => b.Volume);

        [Fact]
        public void Skill_room_gives_four_walls_one_door_and_no_skipped_line()
        {
            var r = FloorPlanLogic.Recognize(SkillRoom(), null, SkillDoor(), O);
            Assert.Equal(4, r.Walls.Count);
            Assert.All(r.Walls, w => Assert.Equal(220, w.Thickness, 6));
            Assert.Empty(r.Skipped);
            Assert.Equal(2, r.Caps); // the two jamb lines
            var door = Assert.Single(r.Openings);
            Assert.Equal("door", door.Kind);
            Assert.Equal("gap", door.From);
            Assert.Equal(900, door.Width, 6);
            Assert.Equal(new[] { "D1", "D2" }, door.Sources.OrderBy(x => x).ToArray());
            Assert.Empty(r.OpenGaps);
            // the right wall runs the full outer length (corners filled), with the gap of the door
            var right = r.Walls.Single(w => Math.Abs(w.Dir[0]) < 1e-9 && w.Origin[0] > 2000);
            Assert.Equal(2, right.Solid.Count);
            Assert.Equal(6220 - 900, right.Solid.Sum(iv => iv[1] - iv[0]), 6);
        }

        [Fact]
        public void Skill_room_volume_matches_the_hand_calculation()
        {
            var r = FloorPlanLogic.Recognize(SkillRoom(), null, SkillDoor(), O);
            // footprint 4220×6220 − 3780×5780 − door 220×900, full height, + lintel 220×900×1100; each corner square is in two boxes
            double net = (4220.0 * 6220 - 3780.0 * 5780 - 220 * 900) * 3300 + 220.0 * 900 * 1100;
            double corners = 4 * 220.0 * 220 * 3300;
            Assert.Equal(net + corners, WallVolume(r), 0);
            var lintel = FloorPlanLogic.Boxes(r, H).Single(b => Math.Abs(b.Height - 1100) < 1e-6);
            Assert.Equal(2200 + 550, lintel.Center[2], 6);
            Assert.Equal(900, lintel.Length, 6);
        }

        [Fact]
        public void Duplicated_and_broken_lines_give_the_same_walls()
        {
            var clean = FloorPlanLogic.Recognize(SkillRoom(), null, SkillDoor(), O);
            var messy = SkillRoom();
            messy.AddRange(SkillRoom().Take(5).Select(s => new PlanSeg(s.X1, s.Y1, s.X2, s.Y2, "DUP"))); // drawn twice
            // the top outer line broken in two with a 2 mm gap
            var top = messy.First(s => s.Sources[0] == "A1" && Math.Abs(s.Y1 - 6110) < 1e-9);
            messy.Remove(top);
            messy.Add(new PlanSeg(4110, 6110, 2000, 6110, "B1"));
            messy.Add(new PlanSeg(1998, 6110, -110, 6110, "B2"));
            var r = FloorPlanLogic.Recognize(messy, null, SkillDoor(), O);
            Assert.Equal(clean.Walls.Count, r.Walls.Count);
            Assert.Empty(r.Skipped);
            Assert.Equal(WallVolume(clean), WallVolume(r), 0);
        }

        [Fact]
        public void T_junction_with_a_thinner_partition_is_closed()
        {
            // bottom wall 220 from x 0 to 6000 (faces y = -110 / 110), partition 110 going up at x = 3000 (faces 2945 / 3055)
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, -110, 6000, -110, "O"),
                new PlanSeg(0, 110, 2945, 110, "I1"),
                new PlanSeg(3055, 110, 6000, 110, "I2"),
                new PlanSeg(2945, 110, 2945, 3000, "P1"),
                new PlanSeg(3055, 110, 3055, 3000, "P2"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Equal(2, r.Walls.Count);
            var main = r.Walls.Single(w => Math.Abs(w.Thickness - 220) < 1e-6);
            Assert.Single(main.Solid); // the 110 gap of the inner face is filled from both sides
            Assert.Equal(0, main.Start, 6);
            Assert.Equal(6000, main.End, 6);
            Assert.Empty(r.OpenGaps);
            Assert.Empty(r.Skipped);
        }

        [Fact]
        public void Cross_junction_drawn_with_every_line_broken_is_filled()
        {
            // two 200 walls crossing at the origin, all four lines broken at the crossing square
            var s = new List<PlanSeg>
            {
                new PlanSeg(-3000, -100, -100, -100, "a"), new PlanSeg(100, -100, 3000, -100, "b"),
                new PlanSeg(-3000, 100, -100, 100, "c"), new PlanSeg(100, 100, 3000, 100, "d"),
                new PlanSeg(-100, -3000, -100, -100, "e"), new PlanSeg(-100, 100, -100, 3000, "f"),
                new PlanSeg(100, -3000, 100, -100, "g"), new PlanSeg(100, 100, 100, 3000, "h"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Equal(2, r.Walls.Count);
            Assert.Equal(2, r.JunctionsFilled);
            Assert.Empty(r.OpenGaps);
            Assert.All(r.Walls, w => Assert.Single(w.Solid));
        }

        [Fact]
        public void The_two_jambs_of_an_opening_never_become_a_wall()
        {
            // a 220 wall with a 400 window gap and its two jamb lines: jambs are 400 apart, inside the thickness range
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 2000, 0, "o1"), new PlanSeg(2400, 0, 5000, 0, "o2"),
                new PlanSeg(0, 220, 2000, 220, "i1"), new PlanSeg(2400, 220, 5000, 220, "i2"),
                new PlanSeg(2000, 0, 2000, 220, "j1"), new PlanSeg(2400, 0, 2400, 220, "j2"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Single(r.Walls);
            Assert.Equal(2, r.Caps);
            var gap = Assert.Single(r.OpenGaps); // no window symbol: reported, left open
            Assert.Equal(400, gap.s1 - gap.s0, 6);
        }

        [Fact]
        public void A_pier_shorter_than_the_wall_is_kept_when_jambs_close_it()
        {
            // two openings with a 150 pier between them in a 220 wall
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 1000, 0, "a"), new PlanSeg(1900, 0, 2050, 0, "b"), new PlanSeg(2950, 0, 5000, 0, "c"),
                new PlanSeg(0, 220, 1000, 220, "d"), new PlanSeg(1900, 220, 2050, 220, "e"), new PlanSeg(2950, 220, 5000, 220, "f"),
                new PlanSeg(1000, 0, 1000, 220, "j1"), new PlanSeg(1900, 0, 1900, 220, "j2"),
                new PlanSeg(2050, 0, 2050, 220, "j3"), new PlanSeg(2950, 0, 2950, 220, "j4"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            var w = Assert.Single(r.Walls);
            Assert.Equal(3, w.Solid.Count);
            Assert.Equal(150, w.Solid[1][1] - w.Solid[1][0], 6);
            Assert.Empty(r.Skipped);
        }

        [Fact]
        public void A_single_line_or_a_too_thick_pair_is_skipped_with_its_handle()
        {
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 4000, 0, "L1"),
                new PlanSeg(0, 5000, 4000, 5000, "L2"), new PlanSeg(0, 5800, 4000, 5800, "L3"), // 800 apart > 600
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Empty(r.Walls);
            Assert.Equal(new[] { "L1", "L2", "L3" }, r.Skipped.Select(x => x.Handle).OrderBy(x => x).ToArray());
            Assert.All(r.Skipped, x => Assert.Contains("not a wall", x.Reason));
        }

        [Fact]
        public void Window_symbol_in_a_gap_and_door_block_over_a_continuous_wall()
        {
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 2000, 0, "o1"), new PlanSeg(3200, 0, 8000, 0, "o2"),
                new PlanSeg(0, 220, 2000, 220, "i1"), new PlanSeg(3200, 220, 8000, 220, "i2"),
            };
            var sym = new List<PlanSymbol>
            {
                new PlanSymbol { Kind = "window", Min = new double[] { 2000, 0 }, Max = new double[] { 3200, 220 }, Sources = { "W1" } },
                // a door block drawn over the continuous part (its frame inside the wall, swing into the room)
                new PlanSymbol { Kind = "door", Min = new double[] { 5000, 0 }, Max = new double[] { 5900, 1120 }, Sources = { "B1" } },
            };
            var r = FloorPlanLogic.Recognize(s, null, sym, O);
            Assert.Equal(2, r.Openings.Count);
            var win = r.Openings.Single(x => x.Kind == "window");
            Assert.Equal("gap", win.From);
            Assert.Equal(1200, win.Width, 6);
            var door = r.Openings.Single(x => x.Kind == "door");
            Assert.Equal("symbol", door.From);
            Assert.Equal(900, door.Width, 6);
            var boxes = FloorPlanLogic.Boxes(r, H);
            // window: sill 0–900 and 2400–3300 above; door: 2200–3300
            Assert.Contains(boxes, b => Math.Abs(b.Length - 1200) < 1e-6 && Math.Abs(b.Height - 900) < 1e-6 && Math.Abs(b.Center[2] - 450) < 1e-6);
            Assert.Contains(boxes, b => Math.Abs(b.Length - 1200) < 1e-6 && Math.Abs(b.Height - 900) < 1e-6 && Math.Abs(b.Center[2] - 2850) < 1e-6);
            Assert.Contains(boxes, b => Math.Abs(b.Length - 900) < 1e-6 && Math.Abs(b.Height - 1100) < 1e-6);
            double expected = 220.0 * (8000 - 1200 - 900) * 3300 + 220.0 * 1200 * 1800 + 220.0 * 900 * 1100;
            Assert.Equal(expected, boxes.Sum(b => b.Volume), 0);
        }

        [Fact]
        public void Symbol_far_from_walls_or_touching_two_walls_is_skipped_not_guessed()
        {
            var s = SkillRoom();
            var sym = new List<PlanSymbol>
            {
                new PlanSymbol { Kind = "door", Min = new double[] { 20000, 0 }, Max = new double[] { 20900, 900 }, Sources = { "FAR" } },
                // a swing square in the bottom-left inner corner touching both inner faces
                new PlanSymbol { Kind = "door", Min = new double[] { 110, 110 }, Max = new double[] { 1010, 1010 }, Sources = { "CORNER" } },
            };
            var r = FloorPlanLogic.Recognize(s, null, sym, O);
            Assert.Contains(r.Skipped, x => x.Handle == "FAR" && x.Reason.Contains("not on or next to"));
            Assert.Contains(r.Skipped, x => x.Handle == "CORNER" && x.Reason.Contains("two walls"));
            Assert.Empty(r.Openings);
        }

        [Fact]
        public void Centerlines_with_a_thickness_close_their_corners()
        {
            // a 4000 × 3000 room by its axis, wall 200
            var c = Poly("C1", 0, 0, 4000, 0, 4000, 3000, 0, 3000, 0, 0).Select(x => { x.Thickness = 200; return x; }).ToList();
            var r = FloorPlanLogic.Recognize(null, c, null, O);
            Assert.Equal(4, r.Walls.Count);
            // footprint × 3.3 m; each wall end at a corner is extended by half the thickness, so the 200 × 200 corner square is in two boxes
            double net = (4200.0 * 3200 - 3800.0 * 2800) * 3300;
            Assert.Equal(net + 4 * 200.0 * 200 * 3300, WallVolume(r), 0);
        }

        [Fact]
        public void Given_opening_on_a_continuous_wall_and_in_a_gap()
        {
            var s = new List<PlanSeg>
            {
                new PlanSeg(0, 0, 2000, 0, "o1"), new PlanSeg(2900, 0, 6000, 0, "o2"),
                new PlanSeg(0, 220, 2000, 220, "i1"), new PlanSeg(2900, 220, 6000, 220, "i2"),
            };
            var r = FloorPlanLogic.Recognize(s, null, null, O);
            Assert.Single(r.OpenGaps);
            Assert.Null(FloorPlanLogic.AddGivenOpening(r, "door", new double[] { 2450, 110 }, 0, null, null, O));
            Assert.Empty(r.OpenGaps);
            Assert.Null(FloorPlanLogic.AddGivenOpening(r, "window", new double[] { 4500, 110 }, 1000, 1000, 2000, O));
            Assert.NotNull(FloorPlanLogic.AddGivenOpening(r, "window", new double[] { 1000, 110 }, 0, null, null, O)); // continuous wall, no width
            Assert.NotNull(FloorPlanLogic.AddGivenOpening(r, "door", new double[] { 1000, 5000 }, 900, null, null, O)); // no wall there
            FloorPlanLogic.ResolveHeights(r, H);
            var win = r.Openings.Single(x => x.Kind == "window");
            Assert.Equal(1000, win.Bottom, 6);
            Assert.Equal(2000, win.Top, 6);
            var boxes = FloorPlanLogic.Boxes(r, H);
            Assert.Contains(boxes, b => Math.Abs(b.Length - 1000) < 1e-6 && Math.Abs(b.Height - 1000) < 1e-6 && Math.Abs(b.Center[2] - 500) < 1e-6);
            Assert.Contains(boxes, b => Math.Abs(b.Length - 1000) < 1e-6 && Math.Abs(b.Height - 1300) < 1e-6);
            Assert.Contains(boxes, b => Math.Abs(b.Length - 900) < 1e-6 && Math.Abs(b.Height - 1100) < 1e-6); // door lintel in the gap
        }

        [Fact]
        public void Units_layers_and_heights()
        {
            Assert.Equal(1000, FloorPlanLogic.MmPerUnit(6, out var m));
            Assert.Equal("meters", m);
            Assert.Equal(1, FloorPlanLogic.MmPerUnit(0, out var u));
            Assert.Contains("taken as millimeters", u);
            var om = PlanOptions.ForUnits(1000, 50, 600); // a plan in metres: 0.05–0.6 m
            Assert.Equal(0.05, om.MinThickness, 9);
            Assert.Equal(0.005, om.JoinTol, 9);
            Assert.True(FloorPlanLogic.LayerMatches("a-wall", FloorPlanLogic.DefaultWallLayers));
            Assert.False(FloorPlanLogic.LayerMatches("A-WALL-PAT", FloorPlanLogic.DefaultWallLayers));
            Assert.True(FloorPlanLogic.LayerMatches("KT_TUONG_220", new[] { "KT_TUONG*" }));
            Assert.Equal("door", FloorPlanLogic.KindOfBlockName("CUA-DI-900"));
            Assert.Equal("window", FloorPlanLogic.KindOfBlockName("cua-so-1200"));
            Assert.Null(FloorPlanLogic.KindOfBlockName("BAN-GHE"));
            FloorPlanLogic.CheckHeights(new FloorPlanLogic.Heights());
            Assert.Throws<ToolException>(() => FloorPlanLogic.CheckHeights(new FloorPlanLogic.Heights { Wall = 3000, Door = 3100 }));
            Assert.Throws<ToolException>(() => FloorPlanLogic.CheckHeights(new FloorPlanLogic.Heights { Wall = 3000, Sill = 2000, Window = 1500 }));
            Assert.Throws<ToolException>(() => FloorPlanLogic.CheckHeights(new FloorPlanLogic.Heights { Wall = 50 }));
        }

        [Fact]
        public void A_plan_in_metres_is_recognised_with_scaled_tolerances()
        {
            var mm = SkillRoom();
            var m = mm.Select(x => new PlanSeg(x.X1 / 1000, x.Y1 / 1000, x.X2 / 1000, x.Y2 / 1000, x.Sources[0])).ToList();
            var sym = SkillDoor().Select(x => new PlanSymbol { Kind = x.Kind, Min = x.Min.Select(v => v / 1000).ToArray(), Max = x.Max.Select(v => v / 1000).ToArray(), Sources = x.Sources }).ToList();
            var r = FloorPlanLogic.Recognize(m, null, sym, PlanOptions.ForUnits(1000, 50, 600));
            Assert.Equal(4, r.Walls.Count);
            Assert.Single(r.Openings);
            var t = FloorPlanLogic.ThicknessSummary(r, 1000);
            Assert.Equal(220, Assert.Single(t).thickness);
        }

        [Fact]
        public void Thickness_summary_and_footprint()
        {
            var r = FloorPlanLogic.Recognize(SkillRoom(), null, SkillDoor(), O);
            var t = Assert.Single(FloorPlanLogic.ThicknessSummary(r));
            Assert.Equal(220, t.thickness);
            var fp = FloorPlanLogic.Footprint(r).Value;
            Assert.Equal(new double[] { -110, -110 }, fp.min.Select(x => Math.Round(x, 6)).ToArray());
            Assert.Equal(new double[] { 4110, 6110 }, fp.max.Select(x => Math.Round(x, 6)).ToArray());
        }

        // ------------------------------------------------------------------ sections

        private static void Near(double[] want, double[] got) { for (int i = 0; i < want.Length; i++) Assert.Equal(want[i], got[i], 9); }

        [Fact]
        public void Section_frame_right_and_up()
        {
            // front: viewer at -Y looking +Y sees X to the right, Z up
            var (u, v) = SectionLogic.Frame(new double[] { 0, -1, 0 });
            Near(new double[] { 1, 0, 0 }, u); Near(new double[] { 0, 0, 1 }, v);
            // right side: viewer at +X sees +Y to the right
            (u, v) = SectionLogic.Frame(new double[] { 1, 0, 0 });
            Near(new double[] { 0, 1, 0 }, u); Near(new double[] { 0, 0, 1 }, v);
            // plan from above
            (u, v) = SectionLogic.Frame(new double[] { 0, 0, 1 });
            Near(new double[] { 1, 0, 0 }, u); Near(new double[] { 0, 1, 0 }, v);
            Assert.Throws<ToolException>(() => SectionLogic.Frame(new double[] { 0, 0, 0 }));
        }

        [Fact]
        public void Section_line_plane_and_flip()
        {
            // line along +X: the viewer stands on the left (+Y side) and looks towards -Y
            var pl = SectionLogic.PlaneFromLine(new double[] { 0, 1000 }, new double[] { 5000, 1000 }, false, "section_view");
            Near(new double[] { 0, 1, 0 }, pl.Normal);
            var fl = SectionLogic.PlaneFromLine(new double[] { 0, 1000 }, new double[] { 5000, 1000 }, true, "section_view");
            Near(new double[] { 0, -1, 0 }, fl.Normal);
            Assert.Throws<ToolException>(() => SectionLogic.PlaneFromLine(new double[] { 1, 1 }, new double[] { 1, 1, 50 }, false, "section_view"));
            var (u, v) = SectionLogic.Frame(fl.Normal);
            // seen from -Y: the model point (3000, 1000, 1500) lands at (3000, 1500) of the frame
            Near(new double[] { 3000, 1500 }, SectionLogic.ToFrame(new double[] { 3000, 1000, 1500 }, fl.Point, u, v));
        }

        [Fact]
        public void Projection_plane_is_outside_the_model_on_the_viewer_side()
        {
            var min = new double[] { 0, 0, 0 }; var max = new double[] { 4000, 6000, 3300 };
            var pl = SectionLogic.ProjectionPlane(min, max, new double[] { 0, -1, 0 });
            Assert.True(pl.Point[1] < 0);
            Assert.False(Solid2Logic.PlaneCutsBox(pl, min, max));
        }

        [Fact]
        public void Auto_position_never_covers_the_model()
        {
            var p = SectionLogic.AutoPosition(new double[] { 0, 0 }, new double[] { 4000, 6000 }, 1000);
            Assert.Equal(new double[] { 5000, 0 }, p);
            Assert.False(SectionLogic.CoversModel(p, new[] { p[0] + 4000, p[1] + 3300 }, new double[] { 0, 0 }, new double[] { 4000, 6000 }));
            var big = SectionLogic.AutoPosition(new double[] { 0, 0 }, new double[] { 50000, 1000 }, 1000);
            Assert.Equal(55000, big[0], 9);
        }

        [Fact]
        public void Pieces_chain_into_loops_either_way_round()
        {
            // a square with one side reversed, plus a circle (closes by itself) and an open piece
            var pieces = new List<SectionLogic.Piece>
            {
                new SectionLogic.Piece { A = new double[] { 0, 0 }, B = new double[] { 10, 0 }, Index = 0 },
                new SectionLogic.Piece { A = new double[] { 10, 10 }, B = new double[] { 10, 0 }, Index = 1 },
                new SectionLogic.Piece { A = new double[] { 10, 10 }, B = new double[] { 0, 10 }, Index = 2 },
                new SectionLogic.Piece { A = new double[] { 0, 10 }, B = new double[] { 0, 0 }, Index = 3 },
                new SectionLogic.Piece { A = new double[] { 50, 0 }, B = new double[] { 50, 0 }, Index = 4 },
                new SectionLogic.Piece { A = new double[] { 70, 0 }, B = new double[] { 80, 0 }, Index = 5 },
            };
            var (loops, open) = SectionLogic.ChainLoops(pieces, 1e-6);
            Assert.Equal(2, loops.Count);
            Assert.Equal(new[] { (0, false), (1, true), (2, false), (3, false) }, loops[0].ToArray());
            Assert.Equal(new[] { 5 }, open.ToArray());
            Assert.Equal(Math.Tan(Math.PI / 8), SectionLogic.Bulge(0, Math.PI / 2, false), 12);
            Assert.Equal(-Math.Tan(Math.PI / 8), SectionLogic.Bulge(0, Math.PI / 2, true), 12);
        }
    }
}
