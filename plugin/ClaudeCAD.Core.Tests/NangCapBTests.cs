using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b.md): stretch_entities, break_entity, lengthen_entity,
    /// divide_entity, group_entities, match_properties; fillet / chamfer of polylines and arcs; offset on both sides;
    /// join with a gap tolerance. AutoCAD-free logic here; the parts that need AutoCAD are checked in the sources.
    /// </summary>
    public class NangCapBTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static double[] P(double x, double y) => new[] { x, y, 0.0 };
        private const double Tol = 1e-6;

        // ------------------------------------------------------------ plan labels (phương án A, 13/11/2026)

        [Theory]
        [InlineData("stretch_entities")]
        [InlineData("break_entity")]
        [InlineData("lengthen_entity")]
        [InlineData("divide_entity")]
        public void New_2D_edit_tools_are_TwoD(string t)
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(t));
            Assert.True(PlanLogic.Allowed("2d", t));
            Assert.True(PlanLogic.Allowed("2d3d", t));
            Assert.False(PlanLogic.Allowed("3d", t));
        }

        [Theory]
        [InlineData("group_entities")]
        [InlineData("match_properties")]
        public void Group_and_matchprop_are_on_every_plan(string t)
        {
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf(t));
            foreach (var plan in new[] { "2d", "3d", "2d3d", null, "lạ" }) Assert.True(PlanLogic.Allowed(plan, t));
        }

        [Theory]
        [InlineData("offset_entity")]
        [InlineData("fillet_entities")]
        [InlineData("chamfer_entities")]
        [InlineData("join_entities")]
        public void Upgraded_tools_keep_their_name_and_label(string t) => Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(t));

        [Fact]
        public void New_tools_are_registered_in_Edit2DTools()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Edit2DTools.cs");
            foreach (var t in new[] { "stretch_entities", "break_entity", "lengthen_entity", "divide_entity", "group_entities", "match_properties" })
                Assert.Contains($"d.Register(\"{t}\", true,", src);
            Assert.Contains("RegisterEdit2D(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }

        // ------------------------------------------------------------ match_properties on the 3D plan

        private static Dictionary<string, object> Props(params object[] v) => new Dictionary<string, object> { ["source"] = "A", ["handles"] = new List<object> { "B" }, ["properties"] = v.ToList() };

        [Fact]
        public void Matchprop_text_or_dimension_is_refused_without_2D()
        {
            Assert.Equal("properties", PlanLogic.BlockedParam("3d", "match_properties", Props("layer", "text")));
            Assert.Equal("properties", PlanLogic.BlockedParam("3d", "match_properties", Props(" Dimension ")));
            Assert.Equal("properties", PlanLogic.BlockedParam("3d", "match_properties", new Dictionary<string, object> { ["properties"] = "text" }));
            Assert.Null(PlanLogic.BlockedParam("3d", "match_properties", Props("layer", "color")));
            Assert.Null(PlanLogic.BlockedParam("3d", "match_properties", new Dictionary<string, object> { ["source"] = "A" }));
            Assert.Null(PlanLogic.BlockedParam("2d", "match_properties", Props("text", "dimension")));
            Assert.Null(PlanLogic.BlockedParam("2d3d", "match_properties", Props("text")));
            Assert.False(PlanLogic.CallAllowed("3d", "match_properties", Props("text")));
            Assert.True(PlanLogic.CallAllowed("3d", "match_properties", Props("layer")));
            var msg = PlanLogic.BlockedParamMessage("match_properties", "properties", "3d", "0900");
            Assert.Contains("text / dimension", msg);
            Assert.Contains("2D group", msg);
            Assert.DoesNotContain("block attributes", msg);
        }

        [Fact]
        public void A_macro_with_matchprop_text_is_refused_on_3d_with_a_clear_label()
        {
            var calls = new List<(string, IDictionary<string, object>)> { ("match_properties", Props("text")), ("group_entities", new Dictionary<string, object>()) };
            Assert.Equal(new[] { "match_properties (thuộc tính chữ / kích thước)" }, PlanLogic.BlockedCalls("3d", calls));
            Assert.Empty(PlanLogic.BlockedCalls("2d", calls));
        }

        [Fact]
        public void Matchprop_defaults_leave_text_out_on_3d()
        {
            var p3 = Edit2DLogic.MatchProps("3d", null, out var sk3);
            Assert.DoesNotContain("text", p3);
            Assert.DoesNotContain("dimension", p3);
            Assert.Equal(new[] { "text", "dimension" }, sk3);
            var p2 = Edit2DLogic.MatchProps("2d", null, out var sk2);
            Assert.Contains("text", p2);
            Assert.Contains("layer", p2);
            Assert.Empty(sk2);
            Assert.Equal(new[] { "layer", "color" }, Edit2DLogic.MatchProps("2d", new[] { "Layer", "color", "layer" }, out _));
            Assert.Throws<ToolException>(() => Edit2DLogic.MatchProps("2d", new[] { "font" }, out _));
            Assert.Throws<ToolException>(() => Edit2DLogic.MatchProps("3d", new[] { "text" }, out _)); // nothing left
        }

        // ------------------------------------------------------------ STRETCH

        [Fact]
        public void Stretch_moves_only_the_points_inside_the_crossing_window()
        {
            var w = Edit2DLogic.StretchWindow(P(50, -10), P(150, 60), null);
            // a 100 x 50 rectangle 0..100: its right edge (x = 100) is inside
            var rect = new List<double[]> { P(0, 0), P(100, 0), P(100, 50), P(0, 50) };
            Assert.Equal(Edit2DLogic.StretchKind.Some, Edit2DLogic.Classify(rect, w, out var inside));
            Assert.Equal(new[] { 1, 2 }, inside);
            Assert.Equal(Edit2DLogic.StretchKind.All, Edit2DLogic.Classify(new[] { P(60, 0), P(140, 50) }, w, out _));
            Assert.Equal(Edit2DLogic.StretchKind.None, Edit2DLogic.Classify(new[] { P(0, 0), P(40, 50) }, w, out _));
            Assert.True(Edit2DLogic.InPolygon(P(50, 20), w)); // on the edge counts
        }

        [Fact]
        public void Stretch_window_needs_two_different_corners_or_a_polygon()
        {
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(P(0, 0), P(0, 10), null));
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(null, null, null));
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(null, null, new[] { P(0, 0), P(1, 1) }));
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(null, null, new[] { P(0, 0), P(1, 1), P(2, 2) }));
            var tri = Edit2DLogic.StretchWindow(null, null, new[] { P(0, 0), P(10, 0), P(0, 10) });
            Assert.True(Edit2DLogic.InPolygon(P(2, 2), tri));
            Assert.False(Edit2DLogic.InPolygon(P(8, 8), tri));
        }

        [Fact]
        public void Stretch_without_handles_asks_above_20_objects_and_skips_partial_3D_objects()
        {
            Assert.Equal(20, Edit2DLogic.MaxStretchUnasked);
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Edit2DTools.cs");
            Assert.Contains("!named && existing > limit", src); // limit = Edit2DLogic.WindowLimit(...): 20 at most (NB-1); existing: KT2-15
            Assert.Equal(20, Edit2DLogic.WindowLimit(null));
            Assert.Contains("kind == Edit2DLogic.StretchKind.Some && IsExplodeConfirmEntity(ent)", src);
            Assert.Contains("ltr.IsLocked", src);
        }

        // ------------------------------------------------------------ BREAK

        [Fact]
        public void Break_open_curve_at_one_point_splits_it()
        {
            var b = Edit2DLogic.Break(false, 0, 10, 4, null);
            Assert.Equal(new[] { 4.0 }, b.SplitAt);
            Assert.Null(b.RemoveMid);
            Assert.Throws<ToolException>(() => Edit2DLogic.Break(false, 0, 10, 0, null));
            Assert.Throws<ToolException>(() => Edit2DLogic.Break(true, 0, 6.28, 1, null));
        }

        [Fact]
        public void Break_two_points_removes_the_part_between()
        {
            var b = Edit2DLogic.Break(false, 0, 10, 7, 3);
            Assert.Equal(new[] { 3.0, 7.0 }, b.SplitAt);
            Assert.Equal(5, b.RemoveMid.Value, 9);
            var end = Edit2DLogic.Break(false, 0, 10, 6, 10); // to the end: only one split
            Assert.Equal(new[] { 6.0 }, end.SplitAt);
            Assert.Equal(8, end.RemoveMid.Value, 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.Break(false, 0, 10, 0, 10));
        }

        [Fact]
        public void Break_closed_curve_removes_from_point_to_point2_in_its_direction()
        {
            double tp = 2 * Math.PI;
            var ccw = Edit2DLogic.Break(true, 0, tp, 1, 2);
            Assert.Equal(1.5, ccw.RemoveMid.Value, 9);
            var wrap = Edit2DLogic.Break(true, 0, tp, 5, 1); // 5 → 2π → 1
            double mid = wrap.RemoveMid.Value;
            Assert.True(mid > 5 || mid < 1, $"mid {mid} must be on the wrapped part");
            Assert.Equal(new[] { 1.0, 5.0 }, wrap.SplitAt);
        }

        // ------------------------------------------------------------ LENGTHEN

        [Theory]
        [InlineData("delta", 20, 100, 120)]
        [InlineData("delta", -30, 100, 70)]
        [InlineData("total", 250, 100, 250)]
        [InlineData("percent", 50, 100, 50)]
        [InlineData(" Percent ", 150, 80, 120)]
        public void Lengthen_new_length(string mode, double v, double cur, double expect) =>
            Assert.Equal(expect, Edit2DLogic.NewLength(mode, v, cur), 9);

        [Fact]
        public void Lengthen_refuses_nothing_left_and_bad_modes()
        {
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("delta", -100, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("total", 0, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("percent", -5, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("scale", 2, 100));
        }

        [Fact]
        public void Lengthen_line_moves_only_the_chosen_end()
        {
            var (s, e) = Edit2DLogic.LengthenSegment(P(0, 0), P(100, 0), 25, true);
            Assert.Equal(new[] { 0.0, 0.0 }, s.Take(2));
            Assert.Equal(125, e[0], 9);
            var (s2, e2) = Edit2DLogic.LengthenSegment(P(0, 0), P(0, 100), -40, false);
            Assert.Equal(40, s2[1], 9);
            Assert.Equal(100, e2[1], 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.LengthenSegment(P(0, 0), P(10, 0), -10, true));
        }

        [Fact]
        public void Lengthen_arc_keeps_the_circle()
        {
            // quarter arc R = 100 (length 157.08) + 100 mm at the end = 1 rad more
            var (s, e) = Edit2DLogic.LengthenArc(0, Math.PI / 2, 100, 100, true);
            Assert.Equal(0, s, 9);
            Assert.Equal(Math.PI / 2 + 1, e, 9);
            var (s2, e2) = Edit2DLogic.LengthenArc(0, Math.PI / 2, 100, 50, false);
            Assert.Equal(2 * Math.PI - 0.5, s2, 9);
            Assert.Equal(Math.PI / 2, e2, 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.LengthenArc(0, Math.PI / 2, 100, -200, true));
            Assert.Throws<ToolException>(() => Edit2DLogic.LengthenArc(0, Math.PI, 100, 400, true)); // would close
        }

        [Fact]
        public void Lengthen_polyline_arc_segment_keeps_centre_and_radius()
        {
            // CCW quarter circle centre (0,0) R 100 from (100,0) to (0,100): bulge tan(π/8)
            double b = Math.Tan(Math.PI / 8);
            var (c, r) = Edit2DLogic.BulgeCircle(P(100, 0), P(0, 100), b);
            Assert.Equal(0, c[0], 6); Assert.Equal(0, c[1], 6); Assert.Equal(100, r, 6);
            var (s, e, nb) = Edit2DLogic.LengthenBulge(P(100, 0), P(0, 100), b, 100 * Math.PI / 2, true); // to a half circle
            Assert.Equal(100, s[0], 6);
            Assert.Equal(-100, e[0], 6); Assert.Equal(0, e[1], 6);
            Assert.Equal(1, nb, 6); // bulge of a half circle
            // clockwise arc (negative bulge) shortened at the start
            var (s2, e2, nb2) = Edit2DLogic.LengthenBulge(P(0, 100), P(100, 0), -b, -100 * Math.PI / 4, false);
            Assert.Equal(Math.Cos(Math.PI / 4) * 100, s2[0], 6);
            Assert.Equal(Math.Sin(Math.PI / 4) * 100, s2[1], 6);
            Assert.Equal(100, e2[0], 6);
            Assert.Equal(-Math.Tan(Math.PI / 16), nb2, 6);
        }

        // ------------------------------------------------------------ DIVIDE / MEASURE

        [Fact]
        public void Divide_open_and_closed()
        {
            Assert.Equal(new[] { 25.0, 50.0, 75.0 }, Edit2DLogic.Stations(100, false, 4, null));
            Assert.Equal(new[] { 0.0, 25.0, 50.0, 75.0 }, Edit2DLogic.Stations(100, true, 4, null));
            Assert.Throws<ToolException>(() => Edit2DLogic.Stations(100, false, 1, null));
            Assert.Throws<ToolException>(() => Edit2DLogic.Stations(100, false, 4, 10.0));
            Assert.Throws<ToolException>(() => Edit2DLogic.Stations(100, false, null, null));
        }

        [Fact]
        public void Measure_from_start_or_end_stops_before_the_far_end()
        {
            Assert.Equal(new[] { 30.0, 60.0, 90.0 }, Edit2DLogic.Stations(100, false, null, 30));
            Assert.Equal(new[] { 70.0, 40.0, 10.0 }, Edit2DLogic.Stations(100, false, null, 30, fromEnd: true));
            Assert.Equal(new[] { 25.0, 50.0, 75.0 }, Edit2DLogic.Stations(100, false, null, 25)); // no mark on the end itself
            Assert.Throws<ToolException>(() => Edit2DLogic.Stations(100, false, null, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.Stations(100, false, null, 0.001)); // 100000 marks
        }

        // ------------------------------------------------------------ fillet / chamfer: polyline

        private static List<double[]> Rect() => new List<double[]> { P(0, 0), P(100, 0), P(100, 50), P(0, 50) };

        [Fact]
        public void Fillet_all_corners_of_a_closed_rectangle()
        {
            var r = Edit2DLogic.PolylineCorners(Rect(), new double[4], true, null, false, 10, 0, 0, "fillet_entities");
            Assert.Equal(new[] { 0, 1, 2, 3 }, r.Done);
            Assert.Equal(8, r.Vertices.Count);
            Assert.Empty(r.Skipped);
            // corner 1 (100,0), CCW rectangle: left turn → positive bulge tan(π/8)
            Assert.Equal(90, r.Vertices[2][0], 9); Assert.Equal(0, r.Vertices[2][1], 9);
            Assert.Equal(Math.Tan(Math.PI / 8), r.Bulges[2], 9);
            Assert.Equal(100, r.Vertices[3][0], 9); Assert.Equal(10, r.Vertices[3][1], 9);
            Assert.Equal(0, r.Bulges[3], 9);
            // vertex 0 of a closed polyline is a corner too
            Assert.Equal(0, r.Vertices[0][0], 9); Assert.Equal(10, r.Vertices[0][1], 9);
        }

        [Fact]
        public void Fillet_one_vertex_and_clockwise_turn()
        {
            var cw = new List<double[]> { P(0, 0), P(0, 50), P(100, 50) }; // right turn at (0,50)
            var r = Edit2DLogic.PolylineCorners(cw, null, false, 1, false, 20, 0, 0, "fillet_entities");
            Assert.Equal(new[] { 1 }, r.Done);
            Assert.Equal(4, r.Vertices.Count);
            Assert.Equal(-Math.Tan(Math.PI / 8), r.Bulges[1], 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(cw, null, false, 0, false, 20, 0, 0, "fillet_entities")); // an end
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(cw, null, false, 5, false, 20, 0, 0, "fillet_entities"));
        }

        [Fact]
        public void Too_short_and_arc_corners_are_skipped_with_a_reason()
        {
            // 100 x 50 with radius 30: tangent length 30, the 50 mm sides need 60 → only some corners fit
            var r = Edit2DLogic.PolylineCorners(Rect(), new double[4], true, null, false, 30, 0, 0, "fillet_entities");
            Assert.NotEmpty(r.Skipped);
            Assert.All(r.Skipped, s => Assert.Contains("too short", (string)s["reason"]));
            Assert.Equal(4, r.Done.Count + r.Skipped.Count);
            // every segment keeps a non-negative length
            for (int i = 0; i < r.Vertices.Count; i++)
                Assert.True(Dist(r.Vertices[i], r.Vertices[(i + 1) % r.Vertices.Count]) > 1e-9);
            var withArc = new double[] { 0, 0.5, 0, 0 };
            var r2 = Edit2DLogic.PolylineCorners(Rect(), withArc, true, null, false, 5, 0, 0, "fillet_entities");
            Assert.Equal(new[] { 0, 3 }, r2.Done);
            Assert.Contains(r2.Skipped, s => (int)s["vertex"] == 1 && ((string)s["reason"]).Contains("arc"));
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(Rect(), new double[4], true, null, false, 0, 0, 0, "fillet_entities"));
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(Rect(), new double[4], true, null, false, 500, 0, 0, "fillet_entities"));
        }

        [Fact]
        public void A_fillet_using_a_whole_side_leaves_no_duplicate_vertex()
        {
            // 50 x 50 square, radius 25: both corners of each side use it all → a circle of 4 arcs (4 vertices)
            var sq = new List<double[]> { P(0, 0), P(50, 0), P(50, 50), P(0, 50) };
            var r = Edit2DLogic.PolylineCorners(sq, null, true, null, false, 25, 0, 0, "fillet_entities");
            Assert.Equal(4, r.Done.Count);
            Assert.Equal(4, r.Vertices.Count);
            Assert.All(r.Bulges, b => Assert.Equal(Math.Tan(Math.PI / 8), b, 9));
        }

        [Fact]
        public void Chamfer_polyline_corners_with_two_distances()
        {
            var r = Edit2DLogic.PolylineCorners(Rect(), null, true, 1, true, 0, 10, 5, "chamfer_entities");
            Assert.Equal(new[] { 1 }, r.Done);
            Assert.Equal(5, r.Vertices.Count);
            Assert.Equal(90, r.Vertices[1][0], 9);   // 10 back along the incoming side
            Assert.Equal(5, r.Vertices[2][1], 9);    // 5 along the outgoing side
            Assert.All(r.Bulges, b => Assert.Equal(0, b, 9));
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(Rect(), null, true, null, true, 0, 0, 0, "chamfer_entities"));
        }

        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

        // ------------------------------------------------------------ fillet / chamfer: arcs

        // line from the left to (60,0); arc centre (100,0) R 50 from 90° to 180° (ends (100,50), (50,0)): corner at (50,0)
        private static Edit2DLogic.Prim Ln() => Edit2DLogic.Prim.Line(P(-100, 0), P(60, 0));
        private static Edit2DLogic.Prim Ar() => Edit2DLogic.Prim.Arc(P(100, 0), 50, Math.PI / 2, Math.PI);

        [Fact]
        public void Fillet_line_and_arc()
        {
            var r = Edit2DLogic.FilletArcs(Ln(), Ar(), 10);
            double cx = 100 - Math.Sqrt(60 * 60 - 10 * 10);
            Assert.Equal(cx, r.Fillet.C[0], 6);
            Assert.Equal(10, r.Fillet.C[1], 6);
            Assert.Equal(10, r.Fillet.R, 9);
            // line keeps (-100,0) and ends at the tangent point under the centre
            Assert.Equal(-100, r.New1.A[0], 9);
            Assert.Equal(cx, r.New1.B[0], 6);
            // the arc keeps (100,50) (its start) and now ends at the tangent point on the circle
            Assert.Equal(Math.PI / 2, r.New2.S, 9);
            var t2 = r.New2.End;
            Assert.Equal(50, Dist(t2, P(100, 0)), 6);
            Assert.Equal(10, Dist(t2, r.Fillet.C), 6);
            // the fillet arc runs from one tangent point to the other, the short way
            var ends = new[] { r.Fillet.Start, r.Fillet.End };
            Assert.Contains(ends, q => Dist(q, r.New1.B) < 1e-6);
            Assert.Contains(ends, q => Dist(q, t2) < 1e-6);
            double sweep = ((r.Fillet.E - r.Fillet.S) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            Assert.True(sweep < Math.PI);
        }

        [Fact]
        public void Fillet_radius_0_trims_both_to_the_corner()
        {
            var shortLine = Edit2DLogic.Prim.Line(P(-100, 0), P(30, 0)); // stops before the arc: extended
            var r = Edit2DLogic.FilletArcs(shortLine, Ar(), 0);
            Assert.Null(r.Fillet);
            Assert.Equal(50, r.New1.B[0], 6);
            Assert.Equal(Math.PI, r.New2.E, 6);
        }

        [Fact]
        public void Fillet_two_arcs()
        {
            // two R 50 arcs meeting at (0,0): centres (-50,0) [0°..90° → (0,0) start] and (50,0) [90°..180° → (0,0) end]
            var a1 = Edit2DLogic.Prim.Arc(P(-50, 0), 50, 0, Math.PI / 2);
            var a2 = Edit2DLogic.Prim.Arc(P(50, 0), 50, Math.PI / 2, Math.PI);
            var r = Edit2DLogic.FilletArcs(a1, a2, 10);
            Assert.NotNull(r.Fillet);
            Assert.Equal(10, r.Fillet.R, 9);
            // tangent to both circles: centre at R + r (outside both) or R − r
            double d1 = Dist(r.Fillet.C, P(-50, 0)), d2 = Dist(r.Fillet.C, P(50, 0));
            Assert.True(Math.Abs(d1 - 60) < 1e-6 || Math.Abs(d1 - 40) < 1e-6);
            Assert.True(Math.Abs(d2 - 60) < 1e-6 || Math.Abs(d2 - 40) < 1e-6);
            Assert.Equal(Math.PI / 2, r.New1.E, 9); // a1 keeps its end (0? no: its far end (−50,50))
            Assert.Equal(Math.PI / 2, r.New2.S, 9);
        }

        [Fact]
        public void Fillet_refuses_objects_that_never_meet_and_too_large_radii()
        {
            var far = Edit2DLogic.Prim.Line(P(-100, 200), P(100, 200));
            Assert.Throws<ToolException>(() => Edit2DLogic.FilletArcs(far, Ar(), 5));
            Assert.Throws<ToolException>(() => Edit2DLogic.FilletArcs(Ln(), Ar(), 1000));
            Assert.Throws<ToolException>(() => Edit2DLogic.FilletArcs(Ln(), Ar(), -1));
        }

        [Fact]
        public void Chamfer_line_and_arc_measures_along_the_arc()
        {
            var r = Edit2DLogic.ChamferArcs(Ln(), Ar(), 10, 50 * Math.PI / 6); // 30° back along the arc
            Assert.Equal(40, r.ChamferStart[0], 6);
            Assert.Equal(0, r.ChamferStart[1], 6);
            Assert.Equal(100 + 50 * Math.Cos(Math.PI * 5 / 6), r.ChamferEnd[0], 6);
            Assert.Equal(50 * Math.Sin(Math.PI * 5 / 6), r.ChamferEnd[1], 6);
            Assert.Equal(40, r.New1.B[0], 6);
            Assert.Equal(Math.PI * 5 / 6, r.New2.E, 6);
            Assert.Throws<ToolException>(() => Edit2DLogic.ChamferArcs(Ln(), Ar(), 10, 500));
        }

        [Fact]
        public void Fillet_and_chamfer_sources_handle_polylines_and_arcs_and_keep_the_old_two_line_path()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            Assert.Contains("Geometry.FilletLines(", src);   // two lines: unchanged behaviour
            Assert.Contains("Geometry.ChamferLines(", src);
            Assert.Contains("Edit2DLogic.FilletArcs(", src);
            Assert.Contains("Edit2DLogic.ChamferArcs(", src);
            Assert.Contains("Edit2DLogic.PolylineCorners(", src);
            Assert.Contains("if (!p.Has(\"handle2\"))", src);
        }

        // ------------------------------------------------------------ join tolerance / offset both sides

        [Fact]
        public void Join_bridges_the_nearest_ends_within_the_tolerance()
        {
            var b = Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(100.4, 0), P(200, 0), 1);
            Assert.NotNull(b);
            Assert.False(b.Value.plStart);
            Assert.True(b.Value.objStart);
            Assert.Equal(0.4, b.Value.gap, 9);
            var atStart = Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(-50, 0), P(-0.5, 0.0), 1);
            Assert.True(atStart.Value.plStart);
            Assert.False(atStart.Value.objStart);
            Assert.Null(Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(102, 0), P(200, 0), 1)); // too far
            Assert.Null(Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(100, 0), P(200, 0), 1)); // touching: normal join
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            Assert.Contains("TryBridge(pl, e, tol, out var gap)", src);
            Assert.Contains("pl.RemoveVertexAt(added);", src); // undone when the join still fails
        }

        [Fact]
        public void Offset_needs_a_side_point_unless_both_sides()
        {
            Edit2DLogic.CheckOffsetSides(true, false);
            Edit2DLogic.CheckOffsetSides(false, true);
            Assert.Throws<ToolException>(() => Edit2DLogic.CheckOffsetSides(false, false));
            Assert.Contains("foreach (var d in new[] { dist, -dist })", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs"));
        }

        // ------------------------------------------------------------ macros / undo

        [Fact]
        public void Macro_replay_maps_the_matchprop_source_handle()
        {
            Assert.True(MacroLogic.IsHandleKey("source"));
            Assert.True(MacroLogic.IsHandleKey("handle"));
            Assert.False(MacroLogic.IsHandleKey("properties"));
        }

        [Fact]
        public void New_tools_write_through_the_dispatcher_so_one_undo_removes_the_turn()
        {
            // writes = true: the dispatcher wraps them in the turn's undo group (UndoScope), like every other write tool
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Edit2DTools.cs");
            Assert.DoesNotContain("StartTransaction", src);
            Assert.DoesNotContain("SendStringToExecute", src);
        }
    }
}
