using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b-test.md): the fixes NB-1 (window stretch and the
    /// 20-object rule), NB-2 (join: bridge a gap only when nothing touching is left), the arc normal −Z map (moved to Core to be tested),
    /// plus edge cases of the đợt B geometry that the first tests did not cover.
    /// </summary>
    public class NangCapBTestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static double[] P(double x, double y) => new[] { x, y, 0.0 };
        private const double Tol = 1e-6;
        private static double Deg(double d) => d * Math.PI / 180;

        // ------------------------------------------------------------ NB-1: window stretch limit

        [Fact]
        public void Window_limit_defaults_to_20_and_is_clamped()
        {
            Assert.Equal(20, Edit2DLogic.WindowLimit(null));
            Assert.Equal(20, Edit2DLogic.WindowLimit(double.NaN));
            Assert.Equal(20, Edit2DLogic.WindowLimit(500)); // never more than the rule
            Assert.Equal(7, Edit2DLogic.WindowLimit(7.9));
            Assert.Equal(0, Edit2DLogic.WindowLimit(-3));
            Assert.Equal("max_unasked", Edit2DLogic.WindowLimitParam);
        }

        [Fact]
        public void Window_limit_message_says_why()
        {
            Assert.Contains("more than 20", Edit2DLogic.WindowLimitMessage(25, 20));
            var m = Edit2DLogic.WindowLimitMessage(8, 5);
            Assert.Contains("already changed", m);
            Assert.Contains("only 5 more", m);
            Assert.Contains("call again with these handles", m);
        }

        [Fact]
        public void Stretch_uses_the_assistant_limit_and_macros_drop_it()
        {
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Edit2DTools.cs");
            Assert.Contains("Edit2DLogic.WindowLimit(p.Has(Edit2DLogic.WindowLimitParam)", tools);
            Assert.Contains("existing > limit", tools); // KT2-15: existing objects of the window (not those created this turn) vs the limit
            Assert.DoesNotContain("plan.Count > Edit2DLogic.MaxStretchUnasked", tools);
            Assert.Contains("copy.Remove(Edit2DLogic.WindowLimitParam)", Src("plugin", "ClaudeCAD.Plugin", "Core", "Macros.cs"));
        }

        [Fact]
        public void Recorded_window_stretch_has_no_limit()
        {
            var rec = new MacroRecorder();
            rec.Record("stretch_entities", new Dictionary<string, object>
            {
                ["corner1"] = new List<object> { 0.0, 0.0 }, ["corner2"] = new List<object> { 10.0, 10.0 },
                ["displacement"] = new List<object> { 5.0, 0.0 }, ["max_unasked"] = 3.0,
            }, new Dictionary<string, object> { ["handles"] = new List<object> { "1A" } });
            var call = rec.Take().Single();
            Assert.False(call.Params.ContainsKey("max_unasked"));
            Assert.True(call.Params.ContainsKey("corner1"));
        }

        // ------------------------------------------------------------ NB-2: join bridges last

        [Fact]
        public void Join_bridges_only_when_no_touching_object_is_left()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs");
            int join = src.IndexOf("private static object JoinEntities", StringComparison.Ordinal);
            int end = src.IndexOf("private static bool TryBridge", StringComparison.Ordinal);
            var body = src.Substring(join, end - join);
            // the bridge is outside the catch of an exact join, after the pass, smallest gap first, one at a time
            Assert.Contains("if (!progress && tol > 0)", body);
            Assert.Contains("OrderBy(x => BridgeGap(pl, x, tol))", body);
            int catchAt = body.IndexOf("catch (Autodesk.AutoCAD.Runtime.Exception)", StringComparison.Ordinal);
            int bridgeAt = body.IndexOf("TryBridge(pl, e, tol", StringComparison.Ordinal);
            int stuckAt = body.IndexOf("if (!progress && tol > 0)", StringComparison.Ordinal);
            Assert.True(catchAt > 0 && stuckAt > catchAt && bridgeAt > stuckAt);
        }

        [Fact]
        public void Bridge_picks_the_nearest_pair_of_ends_within_tolerance()
        {
            var b = Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(100.4, 0.3), P(200, 0), 1);
            Assert.NotNull(b);
            Assert.False(b.Value.plStart);
            Assert.True(b.Value.objStart);
            Assert.Equal(0.5, b.Value.gap, 6);
            // start of the polyline to the end of the object
            var s = Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(-50, 0), P(-0.2, 0), 1);
            Assert.True(s.Value.plStart);
            Assert.False(s.Value.objStart);
            // touching (exact join's job) or too far: none
            Assert.Null(Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(100, 0), P(200, 0), 1));
            Assert.Null(Edit2DLogic.Bridge(P(0, 0), P(100, 0), P(101.5, 0), P(200, 0), 1));
        }

        // ------------------------------------------------------------ arcs with normal −Z (fillet / chamfer): no bug, now tested

        [Fact]
        public void Arc_normal_down_maps_to_a_ccw_wcs_arc_and_back()
        {
            // OCS of normal −Z: X = −WCS X. OCS arc 0 → 90° = WCS points (−r, 0) → (0, r): CCW from 90° to 180°
            var (s, e) = Edit2DLogic.WcsArcAngles(0, Deg(90), true);
            Assert.Equal(Deg(90), s, 9);
            Assert.Equal(Deg(180), e, 9);
            var (s0, e0) = Edit2DLogic.WcsArcAngles(s, e, true);
            Assert.Equal(0, s0, 9);
            Assert.Equal(Deg(90), e0, 9);
            var (u, v) = Edit2DLogic.WcsArcAngles(Deg(30), Deg(60), false);
            Assert.Equal(Deg(30), u, 9);
            Assert.Equal(Deg(60), v, 9);
            Assert.Contains("Edit2DLogic.WcsArcAngles(a.StartAngle, a.EndAngle, a.Normal.Z < 0)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs"));
            Assert.Contains("Edit2DLogic.WcsArcAngles(k.S, k.E, a.Normal.Z < 0)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ModifyTools.cs"));
        }

        [Fact]
        public void Fillet_with_an_arc_drawn_with_normal_down_matches_the_same_arc_drawn_up()
        {
            // the same quarter arc (centre 100,0, r 50, WCS 90° → 180°), once as normal +Z and once as normal −Z (OCS 0 → 90°)
            var line = Edit2DLogic.Prim.Line(P(-100, 0), P(60, 0));
            var up = Edit2DLogic.Prim.Arc(P(100, 0), 50, Deg(90), Deg(180));
            var (s, e) = Edit2DLogic.WcsArcAngles(0, Deg(90), true);
            var down = Edit2DLogic.Prim.Arc(P(100, 0), 50, s, e);
            var a = Edit2DLogic.FilletArcs(line, up, 10);
            var b = Edit2DLogic.FilletArcs(line, down, 10);
            Assert.Equal(a.Fillet.C[0], b.Fillet.C[0], 6);
            Assert.Equal(a.Fillet.C[1], b.Fillet.C[1], 6);
            Assert.Equal(a.New2.S, b.New2.S, 6);
            Assert.Equal(a.New2.E, b.New2.E, 6);
        }

        // ------------------------------------------------------------ fillet / chamfer edge cases

        [Fact]
        public void Fillet_extends_an_arc_that_stops_short_of_the_line()
        {
            var line = Edit2DLogic.Prim.Line(P(-100, 0), P(60, 0));
            var shortArc = Edit2DLogic.Prim.Arc(P(100, 0), 50, Deg(100), Deg(170)); // ends 10° before the line
            var r = Edit2DLogic.FilletArcs(line, shortArc, 10);
            Assert.NotNull(r.Fillet);
            // the fillet is tangent to both: its centre is 10 above the line and 50 + 10 from the arc's centre (outside)
            Assert.Equal(10, Math.Abs(r.Fillet.C[1]), 6);
            double dc = Math.Sqrt(Math.Pow(r.Fillet.C[0] - 100, 2) + Math.Pow(r.Fillet.C[1], 2));
            Assert.True(Math.Abs(dc - 60) < 1e-6 || Math.Abs(dc - 40) < 1e-6);
            // the arc keeps its start and now runs past its old end, towards the line
            Assert.Equal(Deg(100), r.New2.S, 6);
            Assert.True(r.New2.E > Deg(170) && r.New2.E < Deg(180));
        }

        [Fact]
        public void Fillet_line_crossing_an_arc_keeps_the_far_ends()
        {
            // a line through the arc: x from −100 to 80 crosses the circle (100,0) r 50 at x = 50
            var line = Edit2DLogic.Prim.Line(P(-100, 0), P(80, 0));
            var arc = Edit2DLogic.Prim.Arc(P(100, 0), 50, Deg(90), Deg(180));
            var r = Edit2DLogic.FilletArcs(line, arc, 0);
            Assert.Equal(-100, r.New1.A[0], 6);
            Assert.Equal(50, r.New1.B[0], 6);
            Assert.Equal(Deg(90), r.New2.S, 6);
            Assert.Equal(Deg(180), r.New2.E, 6);
        }

        [Fact]
        public void Chamfer_line_and_arc_cut_lies_on_both()
        {
            var line = Edit2DLogic.Prim.Line(P(-100, 0), P(60, 0));
            var arc = Edit2DLogic.Prim.Arc(P(100, 0), 50, Deg(90), Deg(180));
            var r = Edit2DLogic.ChamferArcs(line, arc, 10, 20);
            Assert.Equal(40, r.ChamferStart[0], 6);
            Assert.Equal(0, r.ChamferStart[1], 6);
            // 20 back along the arc from 180°: angle 180° − 20/50 rad
            double a = Math.PI - 20.0 / 50;
            Assert.Equal(100 + 50 * Math.Cos(a), r.ChamferEnd[0], 6);
            Assert.Equal(50 * Math.Sin(a), r.ChamferEnd[1], 6);
            Assert.Throws<ToolException>(() => Edit2DLogic.ChamferArcs(line, arc, 200, 10)); // longer than the line
            Assert.Throws<ToolException>(() => Edit2DLogic.ChamferArcs(line, arc, 10, 100)); // longer than the arc
        }

        [Fact]
        public void Polyline_closed_corner_at_vertex_0_and_last_segment_shared()
        {
            // square 100: fillet R50 at every corner uses each side fully, leaves a circle of 4 arcs, no duplicate vertex
            var v = new List<double[]> { P(0, 0), P(100, 0), P(100, 100), P(0, 100) };
            var r = Edit2DLogic.PolylineCorners(v, new List<double> { 0, 0, 0, 0 }, true, null, false, 50, 0, 0, "fillet_entities");
            Assert.Equal(4, r.Done.Count);
            Assert.Equal(4, r.Vertices.Count);
            Assert.All(r.Bulges, b => Assert.Equal(Math.Tan(Math.PI / 8), b, 9));
            for (int i = 0; i < r.Vertices.Count; i++)
            {
                var a = r.Vertices[i]; var b = r.Vertices[(i + 1) % r.Vertices.Count];
                Assert.True(Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) > 1);
            }
        }

        [Fact]
        public void Polyline_too_big_radius_on_a_short_side_is_skipped_not_crossed()
        {
            // 100 × 20: R15 needs 15 on each side of a corner; the 20 side holds only one corner
            var v = new List<double[]> { P(0, 0), P(100, 0), P(100, 20), P(0, 20) };
            var r = Edit2DLogic.PolylineCorners(v, new List<double> { 0, 0, 0, 0 }, true, null, false, 15, 0, 0, "fillet_entities");
            Assert.Equal(2, r.Done.Count);
            Assert.Equal(2, r.Skipped.Count);
            Assert.All(r.Skipped, s => Assert.Contains("too short", (string)s["reason"]));
        }

        [Fact]
        public void Polyline_named_end_vertex_of_open_polyline_throws()
        {
            var v = new List<double[]> { P(0, 0), P(100, 0), P(100, 100) };
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(v, null, false, 0, false, 5, 0, 0, "fillet_entities"));
            Assert.Throws<ToolException>(() => Edit2DLogic.PolylineCorners(v, null, false, 3, false, 5, 0, 0, "fillet_entities"));
            var ok = Edit2DLogic.PolylineCorners(v, null, false, 1, false, 5, 0, 0, "fillet_entities");
            Assert.Equal(new[] { 1 }, ok.Done);
            Assert.Empty(ok.Skipped);
        }

        // ------------------------------------------------------------ break / lengthen / divide edge cases

        [Fact]
        public void Break_closed_curve_point_at_the_seam()
        {
            double tp = 2 * Math.PI;
            // point at the end parameter (2π, same place as 0) to 1: removes 2π → 1 (wrapping), the 0 → 1 part
            var b = Edit2DLogic.Break(true, 0, tp, tp, 1);
            Assert.Equal(new[] { 1.0, tp }, b.SplitAt);
            Assert.Equal(0.5, b.RemoveMid.Value, 9);
            // closed polyline (parameters 0–4): from vertex 3 to vertex 1 goes through vertex 0
            var pl = Edit2DLogic.Break(true, 0, 4, 3, 1);
            Assert.Equal(new[] { 1.0, 3.0 }, pl.SplitAt);
            Assert.Equal(0.0, pl.RemoveMid.Value, 9);
        }

        [Fact]
        public void Lengthen_negative_bulge_at_start_keeps_the_centre()
        {
            // CW quarter arc from (0,100) to (100,0) around the origin (bulge −tan(22.5°)), lengthened at the start by 10
            double b = -Math.Tan(Math.PI / 8);
            var (s, e, nb) = Edit2DLogic.LengthenBulge(P(0, 100), P(100, 0), b, 10, false);
            Assert.Equal(100, Math.Sqrt(s[0] * s[0] + s[1] * s[1]), 6);
            Assert.Equal(100, e[0], 9);
            Assert.True(nb < 0);
            Assert.Equal(Math.PI / 2 + 0.1, 4 * Math.Atan(-nb), 9);
            Assert.True(s[0] < 0); // went back past (0,100), CCW side
        }

        [Fact]
        public void Lengthen_total_and_percent_on_a_segment()
        {
            Assert.Equal(150, Edit2DLogic.NewLength("total", 150, 100), 9);
            Assert.Equal(50, Edit2DLogic.NewLength("percent", 50, 100), 9);
            Assert.Equal(80, Edit2DLogic.NewLength("delta", -20, 100), 9);
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("delta", -100, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("total", 0, 100));
            Assert.Throws<ToolException>(() => Edit2DLogic.NewLength("scale", 2, 100));
            var (s, e) = Edit2DLogic.LengthenSegment(P(0, 0), P(100, 0), -30, false);
            Assert.Equal(30, s[0], 9);
            Assert.Equal(100, e[0], 9);
        }

        [Fact]
        public void Measure_exact_multiple_has_no_mark_on_the_end_and_closed_from_start()
        {
            Assert.Equal(new[] { 50.0 }, Edit2DLogic.Stations(100, false, null, 50));
            Assert.Equal(new[] { 25.0, 50.0, 75.0 }, Edit2DLogic.Stations(100, true, null, 25));
            Assert.Equal(new[] { 75.0, 50.0, 25.0 }, Edit2DLogic.Stations(100, false, null, 25, fromEnd: true));
            Assert.Equal(10000, Edit2DLogic.Stations(10000, false, null, 0.99999).Count);
        }

        // ------------------------------------------------------------ stretch window

        [Fact]
        public void Stretch_window_any_corner_order_and_polygon()
        {
            var w = Edit2DLogic.StretchWindow(P(100, 50), P(0, 0), null);
            Assert.True(Edit2DLogic.InPolygon(P(50, 25), w));
            Assert.True(Edit2DLogic.InPolygon(P(100, 25), w)); // on the edge
            Assert.False(Edit2DLogic.InPolygon(P(101, 25), w));
            var tri = new List<double[]> { P(0, 0), P(100, 0), P(0, 100) };
            var poly = Edit2DLogic.StretchWindow(null, null, tri);
            Assert.True(Edit2DLogic.InPolygon(P(10, 10), poly));
            Assert.False(Edit2DLogic.InPolygon(P(60, 60), poly));
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(null, null, new List<double[]> { P(0, 0), P(1, 1), P(2, 2) }));
            Assert.Throws<ToolException>(() => Edit2DLogic.StretchWindow(P(0, 0), P(0, 10), null));
        }

        // ------------------------------------------------------------ match_properties by plan

        [Fact]
        public void Match_props_text_on_full_and_2d_but_not_3d()
        {
            Assert.Contains("text", Edit2DLogic.MatchProps("2d", null, out _));
            Assert.Contains("dimension", Edit2DLogic.MatchProps("2d3d", null, out _));
            var three = Edit2DLogic.MatchProps("3d", null, out var sk);
            Assert.DoesNotContain("text", three);
            Assert.Equal(new[] { "text", "dimension" }, sk);
            // in a batch on the 3D plan: refused by the plan gate, with the right label
            var calls = new List<(string, IDictionary<string, object>)>
            {
                ("match_properties", new Dictionary<string, object> { ["source"] = "1A", ["handles"] = new List<object> { "2B" }, ["properties"] = new List<object> { "layer", "Text" } }),
                ("match_properties", new Dictionary<string, object> { ["source"] = "1A", ["handles"] = new List<object> { "2B" }, ["properties"] = new List<object> { "layer" } }),
            };
            var blocked = PlanLogic.BlockedCalls("3d", calls);
            Assert.Single(blocked);
            Assert.Contains("thuộc tính chữ", blocked[0]);
            Assert.Empty(PlanLogic.BlockedCalls("2d3d", calls));
        }
    }
}
