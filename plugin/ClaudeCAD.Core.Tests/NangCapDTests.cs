using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt D (13/11/2026, docs/ban-giao/nang-cap-d.md): beams, curved walls of build_3d_from_plan, PRESSPULL,
    /// STL export, volume schedule by layer (columns inside walls), clash table. AutoCAD-free logic (Core/Solids3.cs);
    /// the parts that need AutoCAD are checked in the sources.
    /// </summary>
    public class NangCapDTests
    {
        public static readonly string[] NewTools = { "build_beam", "presspull", "export_stl", "volume_schedule" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools3 => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs");
        private static string PlanTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
        private static string Solid2Src => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs");

        private static readonly double Deg = Math.PI / 180;
        private static PlanOptions Opt => new PlanOptions(); // mm drawing: thickness 50–600, openings 300–6000

        // ------------------------------------------------------------ plan labels (phương án A)

        [Fact]
        public void New_tools_are_3d_and_open_to_3d_and_full_only()
        {
            foreach (var t in NewTools)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.True(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d3d", t), t);
                Assert.False(PlanLogic.Allowed("2d", t), t);
            }
        }

        [Theory]
        [InlineData("volume_schedule")]
        [InlineData("check_interference")]
        public void A_table_in_the_drawing_needs_full_the_numbers_do_not(string tool)
        {
            var withTable = new Dictionary<string, object> { ["table_position"] = new List<object> { 0.0, 0.0 } };
            var numbers = new Dictionary<string, object> { ["handles"] = new List<object> { "A1", "A2" } };
            Assert.True(PlanLogic.CallAllowed("3d", tool, numbers));
            Assert.False(PlanLogic.CallAllowed("3d", tool, withTable));
            Assert.Equal("table_position", PlanLogic.BlockedParam("3d", tool, withTable));
            Assert.True(PlanLogic.CallAllowed("2d3d", tool, withTable));
            var msg = PlanLogic.BlockedParamMessage(tool, "table_position", "3d", "");
            Assert.Contains("without table_position", msg);
            Assert.Contains("Full", msg);
            // a macro with a table on the 3D plan is refused with a clear label
            Assert.Equal(new List<string> { tool + " (bảng trong bản vẽ)" }, PlanLogic.BlockedCalls("3d", new[] { (tool, (IDictionary<string, object>)withTable) }));
        }

        [Fact]
        public void Registration_reads_and_writes()
        {
            var s = Tools3;
            Assert.Contains("d.Register(\"build_beam\", true, BuildBeam);", s);
            Assert.Contains("d.Register(\"presspull\", true, Presspull);", s);
            Assert.Contains("new ToolDef(\"export_stl\", false, ExportStl, noTransaction: true)", s);
            Assert.Contains("new ToolDef(\"volume_schedule\", true, VolumeSchedule, readsWhen: p => !p.Has(\"table_position\"))", s);
            Assert.Contains("new ToolDef(\"check_interference\", true, CheckInterferenceTool, readsWhen: p => !p.Has(\"table_position\"))", Solid2Src);
            Assert.Contains("RegisterSolids3(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }

        // ------------------------------------------------------------ arcs

        [Fact]
        public void Arc_from_bulge_both_ways()
        {
            var a = Solid3Logic.ArcFromBulge(new[] { -1000.0, 0 }, new[] { 1000.0, 0 }, 1);
            Assert.Equal(0, a.Cx, 6); Assert.Equal(0, a.Cy, 6); Assert.Equal(1000, a.R, 6);
            Assert.Equal(Math.PI, a.A0, 9); Assert.Equal(Math.PI, a.Sweep, 9); // CCW below the chord
            var b = Solid3Logic.ArcFromBulge(new[] { -1000.0, 0 }, new[] { 1000.0, 0 }, -1);
            Assert.Equal(0, b.A0, 9); Assert.Equal(Math.PI, b.Sweep, 9);           // CW above = CCW from the end
            var q = Solid3Logic.ArcFromBulge(new[] { 1000.0, 0 }, new[] { 0, 1000.0 }, Math.Tan(Math.PI / 8));
            Assert.Equal(0, q.Cx, 6); Assert.Equal(0, q.Cy, 6); Assert.Equal(1000, q.R, 6); Assert.Equal(Math.PI / 2, q.Sweep, 9);
            Assert.Null(Solid3Logic.ArcFromBulge(new[] { 0.0, 0 }, new[] { 0.0, 0 }, 1));
        }

        [Fact]
        public void Arc_from_three_points_any_direction()
        {
            var a = Solid3Logic.ArcFrom3Points(new[] { 1000.0, 0 }, new[] { 0, 1000.0 }, new[] { -1000.0, 0 });
            Assert.Equal(0, a.A0, 9); Assert.Equal(Math.PI, a.Sweep, 9); Assert.Equal(1000, a.R, 6);
            var b = Solid3Logic.ArcFrom3Points(new[] { -1000.0, 0 }, new[] { 0, 1000.0 }, new[] { 1000.0, 0 });
            Assert.Equal(0, b.A0, 9); Assert.Equal(Math.PI, b.Sweep, 9);
            var c = Solid3Logic.ArcFrom3Points(new[] { 1000.0, 0 }, new[] { 0, -1000.0 }, new[] { 0, 1000.0 }); // 270° CW
            Assert.Equal(Math.PI / 2, c.A0, 9); Assert.Equal(1.5 * Math.PI, c.Sweep, 9);
        }

        [Fact]
        public void Sector_outlines_have_the_annulus_area_and_split_past_half_a_turn()
        {
            var q = Solid3Logic.SectorOutlines(0, 0, 3000, 3220, 0, Math.PI / 2);
            Assert.Single(q);
            Assert.Equal(0.5 * Math.PI / 2 * (3220.0 * 3220 - 3000.0 * 3000), q[0].Area, 3);
            var full = Solid3Logic.SectorOutlines(0, 0, 3000, 3220, 0, 2 * Math.PI);
            Assert.Equal(2, full.Count);
            Assert.Equal(Math.PI * (3220.0 * 3220 - 3000.0 * 3000), full.Sum(o => o.Area), 3);
            var (mn, mx) = Solid3Logic.SectorBox(0, 0, 3000, 3220, 0, Math.PI / 2);
            Assert.Equal(new[] { 0.0, 0 }, mn.Select(v => Math.Round(v, 6)));
            Assert.Equal(new[] { 3220.0, 3220 }, mx.Select(v => Math.Round(v, 6)));
        }

        // ------------------------------------------------------------ beams

        [Fact]
        public void Straight_beam_is_one_rectangle()
        {
            var l = Solid3Logic.Beam(new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 } }, null, false, 220);
            Assert.Single(l.Pieces);
            Assert.Equal(4000 * 220, l.Pieces[0].Area, 6);
            Assert.Equal(4000, l.Length, 9);
            Assert.Equal(0, l.Joints);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void L_beam_gets_a_mitre_filler_closing_the_outside_corner(bool leftTurn)
        {
            var pts = new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, leftTurn ? 3000 : -3000 } };
            var l = Solid3Logic.Beam(pts, null, false, 200);
            Assert.Equal(3, l.Pieces.Count);
            Assert.Equal(1, l.Joints);
            // the filler of a right angle is the 100 × 100 square at the outside corner
            var filler = l.Pieces[2];
            Assert.Equal(100 * 100, Math.Abs(filler.Area), 6);
            double sx = leftTurn ? 1 : 1, sy = leftTurn ? -1 : 1;
            Assert.Contains(filler.Points, p => Math.Abs(p[0] - (4000 + 100 * sx)) < 1e-6 && Math.Abs(p[1] - 100 * sy) < 1e-6);
            // union area = the two rectangles − the inner overlap square + the filler = w × centreline length
            Assert.Equal(7000, l.Length, 9);
        }

        [Fact]
        public void Sharp_turn_gets_a_bevel_not_a_spike()
        {
            var pts = new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 0.0, 500 } };
            var l = Solid3Logic.Beam(pts, null, false, 200);
            Assert.Equal(3, l.Pieces[2].Points.Count); // triangle
        }

        [Fact]
        public void Curved_beam_is_an_annular_sector_and_too_tight_bends_are_refused()
        {
            var pts = new List<double[]> { new[] { 3000.0, 0 }, new[] { 0, 3000.0 } };
            var l = Solid3Logic.Beam(pts, new List<double> { Math.Tan(Math.PI / 8) }, false, 200);
            Assert.Single(l.Pieces);
            Assert.Equal(0.5 * Math.PI / 2 * (3100.0 * 3100 - 2900.0 * 2900), Math.Abs(l.Pieces[0].Area), 3);
            Assert.Equal(3000 * Math.PI / 2, l.Length, 6);
            var e = Assert.Throws<ToolException>(() => Solid3Logic.Beam(new List<double[]> { new[] { 100.0, 0 }, new[] { 0, 100.0 } }, new List<double> { Math.Tan(Math.PI / 8) }, false, 220));
            Assert.Contains("radius", e.Message);
            // a line then a tangent arc: no corner filler
            var la = Solid3Logic.Beam(new List<double[]> { new[] { 0.0, -2000 }, new[] { 3000.0, 0 - 0.0 }, new[] { 0, 3000.0 } },
                new List<double> { 0, Math.Tan(Math.PI / 8) }, false, 200);
            Assert.Equal(1, la.Joints); // (0,-2000)→(3000,0) is not tangent to the arc: a filler
        }

        [Fact]
        public void Closed_beam_ring_and_checks()
        {
            var sq = new List<double[]> { new[] { 0.0, 0 }, new[] { 4000.0, 0 }, new[] { 4000.0, 4000 }, new[] { 0.0, 4000 } };
            var l = Solid3Logic.Beam(sq, null, true, 220);
            Assert.Equal(4, l.Joints);
            Assert.Equal(16000, l.Length, 9);
            Assert.Throws<ToolException>(() => Solid3Logic.CheckBeam(20, 400));
            Assert.Throws<ToolException>(() => Solid3Logic.CheckBeam(220, 6000));
            Solid3Logic.CheckBeam(220, 400);
            Assert.Throws<ToolException>(() => Solid3Logic.Beam(new List<double[]> { new[] { 1.0, 1 }, new[] { 1.0, 1 } }, null, false, 220));
            Assert.Equal(3300 - 150, Solid3Logic.BeamTop(3300, 0, 150), 9);
            Assert.Equal(2800, Solid3Logic.BeamTop(null, 2800, null), 9);
        }

        // ------------------------------------------------------------ curved walls

        private static PlanArc Arc(double r, double a0Deg, double sweepDeg, string h) =>
            new PlanArc { Cx = 0, Cy = 0, R = r, A0 = a0Deg * Deg, Sweep = sweepDeg * Deg, Sources = { h } };

        private static readonly FloorPlanLogic.Heights H = new FloorPlanLogic.Heights();

        [Fact]
        public void Two_concentric_arcs_are_one_curved_wall()
        {
            var res = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 90, "A"), Arc(3220, 0, 90, "B") }, null, null, Opt);
            var w = Assert.Single(res.Walls);
            Assert.Equal(220, w.Thickness, 9);
            Assert.Equal(3110, w.R, 9);
            Assert.Empty(res.Skipped);
            var pieces = Solid3Logic.ArcPieces(res.Walls, H);
            var p = Assert.Single(pieces);
            Assert.Equal(0.5 * Math.PI / 2 * (3220.0 * 3220 - 3000.0 * 3000) * 3300, p.Volume, 1);
        }

        [Fact]
        public void A_single_arc_is_skipped_with_a_reason_and_three_arcs_pair_neighbours_only()
        {
            var one = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 90, "A") }, null, null, Opt);
            Assert.Empty(one.Walls);
            Assert.Contains("no concentric arc", Assert.Single(one.Skipped).Reason);
            // 3000, 3220, 3440: two walls of 220, not one of 440
            var three = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 90, "A"), Arc(3220, 0, 90, "B"), Arc(3440, 0, 90, "C") }, null, null, Opt);
            Assert.Equal(2, three.Walls.Count);
            Assert.All(three.Walls, w => Assert.Equal(220, w.Thickness, 9));
        }

        [Fact]
        public void Door_gap_with_a_symbol_gets_a_lintel_and_an_empty_gap_stays_open()
        {
            // both faces broken between 40° and 50° (R 3110: ~543 mm)
            var arcs = new List<PlanArc> { Arc(3000, 0, 40, "A1"), Arc(3000, 50, 40, "A2"), Arc(3220, 0, 40, "B1"), Arc(3220, 50, 40, "B2") };
            double mid = 45 * Deg;
            var door = new PlanSymbol { Kind = "door", Min = new[] { 3110 * Math.Cos(mid) - 300, 3110 * Math.Sin(mid) - 300 }, Max = new[] { 3110 * Math.Cos(mid) + 300, 3110 * Math.Sin(mid) + 300 }, Sources = { "D" } };
            var res = Solid3Logic.CurvedWalls(arcs, null, new List<PlanSymbol> { door }, Opt);
            var w = Assert.Single(res.Walls);
            Assert.Equal(2, w.Solid.Count);
            var op = Assert.Single(w.Openings);
            Assert.Equal("door", op.Kind); Assert.Equal("gap", op.From);
            Assert.Equal(10 * Deg, op.Sweep, 6);
            Assert.Contains(door, res.UsedSymbols);
            var pieces = Solid3Logic.ArcPieces(res.Walls, H);
            Assert.Equal(3, pieces.Count); // two full-height parts + the lintel
            var lintel = pieces.Single(p => p.Z0 > 0);
            Assert.Equal(2200, lintel.Z0, 9); Assert.Equal(3300, lintel.Z1, 9);

            var open = Solid3Logic.CurvedWalls(arcs, null, null, Opt);
            Assert.Single(open.Walls[0].OpenGaps);
            Assert.Empty(open.Walls[0].Openings);
            Assert.Equal(2, Solid3Logic.ArcPieces(open.Walls, H).Count);
        }

        [Fact]
        public void Window_symbol_over_a_continuous_curved_wall_cuts_sill_and_lintel()
        {
            var arcs = new List<PlanArc> { Arc(3000, 0, 90, "A"), Arc(3220, 0, 90, "B") };
            double mid = 45 * Deg;
            double cx = 3110 * Math.Cos(mid), cy = 3110 * Math.Sin(mid);
            var win = new PlanSymbol { Kind = "window", Min = new[] { cx - 500, cy - 500 }, Max = new[] { cx + 500, cy + 500 }, Sources = { "W" } };
            var res = Solid3Logic.CurvedWalls(arcs, null, new List<PlanSymbol> { win }, Opt);
            var op = Assert.Single(res.Walls[0].Openings);
            Assert.Equal("symbol", op.From);
            var pieces = Solid3Logic.ArcPieces(res.Walls, H);
            Assert.Contains(pieces, p => p.Z0 == 0 && p.Z1 == 900 && Math.Abs(p.Sweep - op.Sweep) < 1e-9);       // sill
            Assert.Contains(pieces, p => p.Z0 == 2400 && p.Z1 == 3300 && Math.Abs(p.Sweep - op.Sweep) < 1e-9);    // lintel
            // the wall volume minus the window hole
            double full = 0.5 * Math.PI / 2 * (3220.0 * 3220 - 3000.0 * 3000) * 3300;
            double hole = 0.5 * op.Sweep * (3220.0 * 3220 - 3000.0 * 3000) * 1500;
            Assert.Equal(full - hole, pieces.Sum(p => p.Volume), 1);
            // a symbol far from the wall is left for the straight walls
            var far = new PlanSymbol { Kind = "door", Min = new[] { 9000.0, 9000 }, Max = new[] { 9900.0, 9900 }, Sources = { "F" } };
            Assert.DoesNotContain(far, Solid3Logic.CurvedWalls(arcs, null, new List<PlanSymbol> { far }, Opt).UsedSymbols);
        }

        [Fact]
        public void Centerline_arcs_round_walls_and_given_openings()
        {
            var c = new PlanArc { Cx = 0, Cy = 0, R = 3110, A0 = 0, Sweep = Math.PI / 2, Thickness = 220, Sources = { "P" } };
            var res = Solid3Logic.CurvedWalls(null, new List<PlanArc> { c }, null, Opt);
            Assert.Equal("centerline", Assert.Single(res.Walls).Mode);
            // a round room: two full circles
            var round = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 360, "C1"), Arc(3220, 0, 360, "C2") }, null, null, Opt);
            Assert.Equal(2 * Math.PI, round.Walls[0].Solid.Single()[1], 9);
            Assert.Equal(Math.PI * (3220.0 * 3220 - 3000.0 * 3000) * 3300, Solid3Logic.ArcPieces(round.Walls, H).Sum(p => p.Volume), 0);
            // a given door on the round wall at 90° (0, 3110), 900 wide
            Assert.True(Solid3Logic.AddGivenArcOpening(round.Walls, "door", new[] { 0.0, 3110 }, 900, 0, 2100, Opt, out var err));
            Assert.Null(err);
            var pieces = Solid3Logic.ArcPieces(round.Walls, H);
            Assert.Contains(pieces, p => Math.Abs(p.Z0 - 2100) < 1e-9);
            Assert.Equal(900, round.Walls[0].Openings.Single().Sweep * 3110, 6);
            // the wrap: an opening across 0° of a full circle is cut on both sides
            var round2 = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 360, "C1"), Arc(3220, 0, 360, "C2") }, null, null, Opt);
            Assert.True(Solid3Logic.AddGivenArcOpening(round2.Walls, "window", new[] { 3110.0, 0 }, 1000, 900, 2400, Opt, out _));
            var p2 = Solid3Logic.ArcPieces(round2.Walls, H);
            double ring = Math.PI * (3220.0 * 3220 - 3000.0 * 3000) * 3300;
            double holeA = 1000.0 / 3110;
            Assert.Equal(ring - 0.5 * holeA * (3220.0 * 3220 - 3000.0 * 3000) * 1500, p2.Sum(p => p.Volume), 0);
            // not on a curved wall: false (the straight walls are tried); on a continuous wall without width: an error
            Assert.False(Solid3Logic.AddGivenArcOpening(round.Walls, "door", new[] { 9000.0, 0 }, 900, 0, 2100, Opt, out _));
            var res2 = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 90, "A"), Arc(3220, 0, 90, "B") }, null, null, Opt);
            Assert.True(Solid3Logic.AddGivenArcOpening(res2.Walls, "door", new[] { 3110 * Math.Cos(45 * Deg), 3110 * Math.Sin(45 * Deg) }, 0, 0, 2100, Opt, out var err2));
            Assert.Contains("width", err2);
        }

        [Fact]
        public void Wrap_gap_of_a_quarter_wall_is_not_a_door()
        {
            // a quarter wall of R 1000: the remaining 270° (4.7 m) is no opening
            var res = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(1000, 0, 90, "A"), Arc(1220, 0, 90, "B") }, null, null, Opt);
            Assert.Empty(res.Walls[0].OpenGaps);
            Assert.Empty(res.Walls[0].Openings);
        }

        [Fact]
        public void Radial_end_lines_of_a_curved_wall_are_caps()
        {
            var res = Solid3Logic.CurvedWalls(new List<PlanArc> { Arc(3000, 0, 90, "A"), Arc(3220, 0, 90, "B") }, null, null, Opt);
            var faces = new List<PlanSeg> { new PlanSeg(3000, 0, 3220, 0, "E1"), new PlanSeg(0, 3000, 0, 3220, "E2"), new PlanSeg(0, 0, 5000, 0, "L") };
            Assert.Equal(2, Solid3Logic.DropCurvedCaps(faces, res.Walls, Opt));
            Assert.Equal("L", Assert.Single(faces).Sources[0]);
        }

        [Fact]
        public void Build_3d_from_plan_reads_curved_walls()
        {
            var s = PlanTools;
            Assert.Contains("Solid3Logic.CurvedWalls(input.FaceArcs, input.CenterArcs, symbols, opt)", s);
            Assert.Contains("Solid3Logic.AddGivenArcOpening(curved.Walls", s);
            Assert.Contains("report[\"curved_walls\"]", s);
            Assert.Contains("Solid3Logic.SectorOutlines(a.Cx, a.Cy, a.Ri, a.Ro, a.A0, a.Sweep)", s);
            // arcs are no longer skipped as "curved wall: not built"
            Assert.DoesNotContain("has arc segments (curved wall): those segments are not built", s);
            Assert.Contains("case Arc arcE:", s);
            Assert.Contains("case Circle ci:", s);
        }

        // ------------------------------------------------------------ PRESSPULL

        [Fact]
        public void Presspull_modes()
        {
            Assert.Equal("subtract", Solid3Logic.NormalizePresspullMode("cut"));
            Assert.Equal("add", Solid3Logic.NormalizePresspullMode("thêm"));
            Assert.Equal("auto", Solid3Logic.NormalizePresspullMode(null));
            Assert.Throws<ToolException>(() => Solid3Logic.NormalizePresspullMode("boom"));
            Assert.Equal("new", Solid3Logic.ResolvePresspull("auto", false, null, 1000));
            Assert.Throws<ToolException>(() => Solid3Logic.ResolvePresspull("add", false, null, 1000));
            Assert.Equal("subtract", Solid3Logic.ResolvePresspull("auto", true, 1000, 1000)); // pushed into the solid
            Assert.Equal("add", Solid3Logic.ResolvePresspull("auto", true, 0, 1000));          // pulled out of a face
            Assert.Equal("add", Solid3Logic.ResolvePresspull("add", true, 1000, 1000));
            Assert.Throws<ToolException>(() => Solid3Logic.ResolvePresspull("auto", true, null, 1000));
        }

        [Fact]
        public void Presspull_source_uses_face_extrusion_and_guards()
        {
            var s = Tools3;
            Assert.Contains("s.ExtrudeFaces(faces.Select(i => t.FaceIds[i]).ToArray(), dist, 0)", s);
            Assert.Contains("SolidForWrite(ctx, h, tool)", s);
            Assert.Contains("SolidForWrite(ctx, target, tool)", s);
            Assert.Contains("would leave nothing", s);
        }

        // ------------------------------------------------------------ STL

        private static List<float[]> Tris() => new List<float[]>
        {
            new float[] { 0, 0, 1, 0, 0, 0, 1000, 0, 0, 0, 1000, 0 },
            new float[] { 0, 0, -1, 0, 0, 0, 0, 1000, 0, 1000, 0, 0 },
        };

        [Fact]
        public void Stl_binary_and_ascii_round_trip()
        {
            var bin = Solid3Logic.WriteBinaryStl(Tris(), "solid header that starts like ascii");
            Assert.Equal(84 + 2 * 50, bin.Length);
            var back = Solid3Logic.ReadStl(bin);
            Assert.Equal(2, back.Count);
            Assert.Equal(Tris()[0], back[0]);
            var ascii = Solid3Logic.WriteAsciiStl(Tris(), "Mặt bằng tầng 1");
            Assert.StartsWith("solid MAT_BANG_TANG_1", ascii);
            var back2 = Solid3Logic.ReadStl(System.Text.Encoding.ASCII.GetBytes(ascii));
            Assert.Equal(2, back2.Count);
            Assert.Equal(Tris()[1], back2[1]);
            Assert.Empty(Solid3Logic.ReadStl(new byte[0]));
        }

        [Fact]
        public void Stl_name_shift_and_source_guards()
        {
            Assert.Equal("Nha_A-3d.stl", Solid3Logic.DefaultStlName("Nha:A"));
            Assert.Equal("Drawing-3d.stl", Solid3Logic.DefaultStlName(""));
            Assert.Equal(new[] { 500.0, 0, 200 }, Solid3Logic.PositiveShift(new[] { -500.0, 10, -200 }));
            var s = Tools3;
            Assert.Contains("Util2DLogic.BadFileName(Path.GetFileName(full))", s);
            Assert.Contains("must end with .stl", s);
            Assert.Contains("overwrite", s);
            Assert.Contains("tr.Abort();", s);          // moved copies never stay in the drawing
            Assert.Contains("c.StlOut(file, false);", s);
            Assert.Contains("is a folder", s);
        }

        // ------------------------------------------------------------ volume schedule

        private static Solid3Logic.VolSolid V(string h, string layer, double v, int order) => new Solid3Logic.VolSolid { Handle = h, Layer = layer, Volume = v, Area = 1e6, Order = order };

        [Fact]
        public void Column_inside_a_wall_is_counted_once_in_the_column()
        {
            double wall = 4000.0 * 220 * 3300, col = 220.0 * 220 * 3300;
            var solids = new List<Solid3Logic.VolSolid> { V("W", "A-WALL-3D", wall, 0), V("C", "A-COLS-3D", col, 1) };
            var ov = new List<Solid3Logic.VolOverlap> { new Solid3Logic.VolOverlap { A = "W", B = "C", Volume = col } };
            var r = Solid3Logic.Schedule(solids, ov, null, "deduct");
            Assert.Equal("A-COLS-3D", r.Rows[0].Layer); // columns first
            Assert.Equal(col, r.Rows[0].Net, 6);
            var w = r.Rows.Single(x => x.Layer == "A-WALL-3D");
            Assert.Equal(col, w.Deducted, 6);
            Assert.Equal(wall - col, w.Net, 6);
            Assert.Equal(wall, r.Rows.Sum(x => x.Net), 6); // the real concrete
            Assert.Equal("W", Assert.Single(r.Deductions).From);
            Assert.Empty(r.Warnings);
            // overlap "none": nothing deducted, a warning
            var n = Solid3Logic.Schedule(solids, ov, null, "none");
            Assert.Equal(0, n.Rows.Sum(x => x.Deducted), 9);
            Assert.Contains("counted twice", Assert.Single(n.Warnings));
            // priority: the wall keeps its volume, the column loses it
            var pr = Solid3Logic.Schedule(solids, ov, new[] { "A-WALL*" }, "deduct");
            Assert.Equal("C", pr.Deductions.Single().From);
            // an overlap AutoCAD could not measure: warned, not deducted
            var um = Solid3Logic.Schedule(solids, new List<Solid3Logic.VolOverlap> { new Solid3Logic.VolOverlap { A = "W", B = "C", Volume = null } }, null, "deduct");
            Assert.Equal(0, um.Rows.Sum(x => x.Deducted), 9);
            Assert.Contains("could not be measured", Assert.Single(um.Warnings));
        }

        [Fact]
        public void Never_deducts_more_than_the_solid()
        {
            var solids = new List<Solid3Logic.VolSolid> { V("C1", "A-COLS-3D", 100, 0), V("C2", "A-COLS-3D", 100, 1), V("B", "A-BEAM-3D", 50, 2) };
            var ov = new List<Solid3Logic.VolOverlap>
            {
                new Solid3Logic.VolOverlap { A = "C1", B = "B", Volume = 40 },
                new Solid3Logic.VolOverlap { A = "C2", B = "B", Volume = 40 },
            };
            var r = Solid3Logic.Schedule(solids, ov, null, "deduct");
            var beam = r.Rows.Single(x => x.Layer == "A-BEAM-3D");
            Assert.Equal(50, beam.Deducted, 9);
            Assert.Equal(0, beam.Net, 9);
        }

        [Theory]
        [InlineData("A-COLS-3D", 0)]
        [InlineData("KT_COT", 0)]
        [InlineData("A-BEAM-3D", 1)]
        [InlineData("DẦM", 1)]
        [InlineData("A-WALL-3D", 2)]
        [InlineData("TƯỜNG 220", 2)]
        [InlineData("A-SLAB-3D", 3)]
        [InlineData("A-RAIL-3D", 4)]
        [InlineData("0", 4)]
        public void Layer_rank(string layer, int rank) => Assert.Equal(rank, Solid3Logic.LayerRank(layer));

        [Fact]
        public void Volume_and_clash_tables()
        {
            var solids = new List<Solid3Logic.VolSolid> { V("W", "A-WALL-3D", 2.904e9, 0), V("C", "A-COLS-3D", 0.15972e9, 1) };
            var r = Solid3Logic.Schedule(solids, new List<Solid3Logic.VolOverlap> { new Solid3Logic.VolOverlap { A = "W", B = "C", Volume = 0.15972e9 } }, null, "deduct");
            var t = Solid3Logic.VolumeTable(r, 3, 1, true);
            Assert.Equal(new object[] { "STT", "Layer", "Số khối", "Thể tích (m³)", "Trừ phần giao (m³)", "Thể tích thực (m³)", "Diện tích mặt (m²)" }, t[0]);
            Assert.Equal("0,160", t[1][3]);
            Assert.Equal("2,744", t[2][5]);
            Assert.Equal("Tổng", t[3][1]);
            Assert.Equal("2,904", t[3][5]);
            Assert.Equal(6, Solid3Logic.VolumeTable(r, 3, 1, false)[0].Count);
            // a drawing in metres: m³ from units³
            Assert.Equal(2.904, Solid3Logic.M3(2.904, 1000), 9);

            var c = Solid3Logic.ClashTable(new List<Solid3Logic.ClashRow>
            {
                new Solid3Logic.ClashRow { A = "1A", LayerA = "A-BEAM-3D", B = "2B", LayerB = "M-DUCT", Volume = 0.05e9 },
                new Solid3Logic.ClashRow { A = "1A", LayerA = "A-BEAM-3D", B = "3C", LayerB = "M-DUCT", Volume = null },
            }, 3);
            Assert.Equal("0,050", c[1][5]);
            Assert.Equal("chưa đo được", c[2][5]);
            Assert.Equal("2 cặp", c[3][3]);
        }

        [Fact]
        public void Schedule_and_clash_table_sources()
        {
            var s = Tools3;
            Assert.Contains("InsertSchedule(ctx, p, Solid3Logic.VolumeTable(", s);
            Assert.Contains("OverlapVolume(solids[i], solids[j])", s);
            Assert.Contains("Solid3Logic.MaxScheduleOverlapPairs", s);
            var s2 = Solid2Src;
            Assert.Contains("Solid3Logic.ClashTable(clashRows, dec, mmPerUnit)", s2); // Test & sửa đợt D (ND-4): drawing units
            Assert.Contains("[\"layer_a\"] = sa.Layer", s2);
        }
    }
}
