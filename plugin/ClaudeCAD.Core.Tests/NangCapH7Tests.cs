using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): MEP pipes / ducts in 3D (Core/Mep.cs, Drawing/MepTools.cs).</summary>
    public class NangCapH7Tests
    {
        public static readonly string[] NewTools = { "draw_mep", "mep_report" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static void Near(double[] want, double[] got, double tol = 1e-6)
        {
            Assert.Equal(want.Length, got.Length);
            for (int i = 0; i < want.Length; i++) Assert.True(Math.Abs(want[i] - got[i]) <= tol, $"[{i}] {want[i]} vs {got[i]}");
        }

        /// <summary>p turned by angle about the axis through c (right hand), like Solid3d.Revolve.</summary>
        private static double[] Turn(double[] p, double[] c, double[] axis, double angle)
        {
            var k = MepLogic.Unit(axis);
            var v = MepLogic.Sub(p, c);
            var r = MepLogic.Add(MepLogic.Add(MepLogic.Mul(v, Math.Cos(angle)), MepLogic.Mul(MepLogic.Cross(k, v), Math.Sin(angle))), MepLogic.Mul(k, MepLogic.Dot(k, v) * (1 - Math.Cos(angle))));
            return MepLogic.Add(c, r);
        }

        // ------------------------------------------------------------ plans, registration

        [Fact]
        public void The_tools_are_3d_and_full_only_with_labels_a_ucs_height_and_a_card()
        {
            foreach (var t in NewTools)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.False(PlanLogic.Allowed("2d", t));
                Assert.True(PlanLogic.Allowed("3d", t));
                Assert.True(PlanLogic.Allowed("2d3d", t));
                Assert.False(string.IsNullOrEmpty(ToolLabelsSource(t)), t);
            }
            Assert.Equal(ParamKind.Height, UcsLogic.KindOf("draw_mep", "elevation"));
            Assert.Equal(ParamKind.Point, UcsLogic.KindOf("draw_mep", "points"));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("draw_mep", new Dictionary<string, object>())); // reads beams / the pipe of a tee
            var t2 = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("d.Register(new ToolDef(\"draw_mep\", true, DrawMep));", t2);
            Assert.Contains("d.Register(new ToolDef(\"mep_report\", false, MepReport));", t2);
            Assert.Contains("ExportExcel(ctx, p, tool, () => MepLogic.ScheduleTable(rows, waste), res);", t2); // the F5 overwrite card
            Assert.Contains("RegisterMep(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }

        private static string ToolLabelsSource(string tool)
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            int i = s.IndexOf("[\"" + tool + "\"] = \"", StringComparison.Ordinal);
            return i < 0 ? null : s.Substring(i, 40);
        }

        // ------------------------------------------------------------ systems and sizes

        [Fact]
        public void Each_system_has_its_layer_colour_and_shape()
        {
            Assert.Equal("P-SUPL-3D", MepLogic.SystemOf("supply").Layer3d);
            Assert.Equal("P-DRAIN-3D", MepLogic.SystemOf(" DRAIN ").Layer3d);
            Assert.True(MepLogic.SystemOf("drain").Slopes);
            Assert.False(MepLogic.SystemOf("supply").Slopes);
            Assert.True(MepLogic.SystemOf("duct_supply").Rect);
            Assert.Equal("M-DUCT-RET-3D", MepLogic.SystemOf("duct_return").Layer3d);
            Assert.Equal("F-SPRK-3D", MepLogic.SystemOf("fire").Layer3d);
            Assert.Equal(7, MepLogic.Systems.Select(s => s.Key).Distinct().Count());
            Assert.Throws<ToolException>(() => MepLogic.SystemOf("gas"));
        }

        [Fact]
        public void Sizes_come_from_the_user_or_the_default_of_the_use_never_for_fire_or_ducts()
        {
            var supply = MepLogic.SystemOf("supply");
            Assert.Equal((H7Numbers.SupplyBranchMm, H7Numbers.SupplyBranchMm), MepLogic.SizeOf(supply, null, null, null, null));
            Assert.Equal((H7Numbers.SupplyMainMm, H7Numbers.SupplyMainMm), MepLogic.SizeOf(supply, null, null, null, "main"));
            Assert.Equal((40.0, 40.0), MepLogic.SizeOf(supply, 40, null, null, "main")); // the given size wins
            var drain = MepLogic.SystemOf("drain");
            Assert.Contains("use = floor / wc / stack", Assert.Throws<ToolException>(() => MepLogic.SizeOf(drain, null, null, null, null)).Message);
            Assert.Equal(H7Numbers.DrainWcMm, MepLogic.SizeOf(drain, null, null, null, "wc").a);
            Assert.Contains("no default size", Assert.Throws<ToolException>(() => MepLogic.SizeOf(MepLogic.SystemOf("fire"), null, null, null, null)).Message);
            Assert.Equal((100.0, 100.0), MepLogic.SizeOf(MepLogic.SystemOf("fire"), 100, null, null, null));
            var duct = MepLogic.SystemOf("duct_supply");
            Assert.Contains("There is no default", Assert.Throws<ToolException>(() => MepLogic.SizeOf(duct, 300, null, null, null)).Message);
            Assert.Equal((400.0, 300.0), MepLogic.SizeOf(duct, null, 400, 300, null));
            Assert.Throws<ToolException>(() => MepLogic.SizeOf(supply, -5, null, null, null));
        }

        [Fact]
        public void A_drain_falls_by_its_slope_and_a_too_flat_one_is_known()
        {
            var z = MepLogic.Sloped(new List<double[]> { new[] { 0.0, 0 }, new[] { 3000.0, 0 }, new[] { 3000.0, 4000 } }, 2500, 2);
            Assert.Equal(2500, z[0][2], 9);
            Assert.Equal(2440, z[1][2], 9); // 3 m × 2 %
            Assert.Equal(2360, z[2][2], 9); // + 4 m
            Assert.Equal(1.2, MepLogic.MinSlope(110));
            Assert.Equal(2.5, MepLogic.MinSlope(60));
            Assert.Equal(0.5, MepLogic.MinSlope(250));
            Assert.Null(MepLogic.MinSlope(40));
            // the plugin warns below the least slope (source)
            Assert.Contains("is less than {min}% for D", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        // ------------------------------------------------------------ route geometry

        [Fact]
        public void An_l_route_gets_two_pipes_and_one_elbow_that_meets_them()
        {
            double R = 50;
            var pcs = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 3000 }, new[] { 2000.0, 0, 3000 }, new[] { 2000.0, 1000, 3000 } }, R);
            Assert.Equal(new[] { "pipe", "elbow", "pipe" }, pcs.Select(x => x.Kind).ToArray());
            var e = pcs[1];
            Near(new[] { 0.0, 0, 3000 }, pcs[0].A);
            Near(new[] { 1950.0, 0, 3000 }, pcs[0].B); // shortened by R tan 45°
            Near(new[] { 2000.0, 50, 3000 }, pcs[2].A);
            Near(pcs[0].B, e.Start);
            Assert.Equal(R, MepLogic.Dist(e.Start, e.Center), 9);
            Assert.Equal(Math.PI / 2, e.Angle, 9);
            // turned like Revolve, the start meets the next pipe
            Near(pcs[2].A, Turn(e.Start, e.Center, e.Axis, e.Angle), 1e-6);
            Assert.Equal(1950 + R * Math.PI / 2 + 950, pcs.Sum(x => x.Length), 6);
        }

        [Fact]
        public void A_turn_in_height_and_any_angle_still_meets_both_pipes()
        {
            var pcs = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 1000.0, 0, 0 }, new[] { 1000.0, 0, 1500 }, new[] { 2000.0, 700, 2500 } }, 80);
            var elbows = pcs.Where(x => x.Kind == "elbow").ToList();
            Assert.Equal(2, elbows.Count);
            for (int i = 0; i < pcs.Count; i++)
            {
                if (pcs[i].Kind != "elbow") continue;
                Near(pcs[i - 1].B, pcs[i].Start, 1e-6);
                Near(pcs[i + 1].A, Turn(pcs[i].Start, pcs[i].Center, pcs[i].Axis, pcs[i].Angle), 1e-6);
            }
        }

        [Fact]
        public void Straight_points_make_no_elbow_and_bad_routes_are_refused()
        {
            var pcs = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 0 }, new[] { 3000.0, 0 } }, 50);
            Assert.Equal(new[] { "pipe" }, pcs.Select(x => x.Kind).ToArray());
            Assert.Equal(3000, pcs[0].Length, 9);
            Assert.Contains("turns straight back", Assert.Throws<ToolException>(() => MepLogic.Layout(new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 0.0, 0 } }, 50)).Message);
            Assert.Contains("too short for its bends", Assert.Throws<ToolException>(() =>
                MepLogic.Layout(new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 60 }, new[] { 2000.0, 60 } }, 50)).Message);
            Assert.Contains("at least two", Assert.Throws<ToolException>(() => MepLogic.Layout(new List<double[]> { new[] { 5.0, 5 }, new[] { 5.0, 5 } }, 50)).Message);
        }

        [Fact]
        public void The_bend_radius_follows_the_size_and_the_drawing_units()
        {
            Assert.Equal(H7Numbers.PipeBendFactor * 110, MepLogic.BendRadius(MepLogic.SystemOf("drain"), 110, 1, null), 9);
            Assert.Equal(H7Numbers.DuctBendFactor * 400 / 1000, MepLogic.BendRadius(MepLogic.SystemOf("duct_supply"), 400, 1000, null), 9); // a drawing in metres
            Assert.Equal(0.2, MepLogic.BendRadius(MepLogic.SystemOf("supply"), 25, 1000, 200), 9);
        }

        [Fact]
        public void A_duct_keeps_its_width_level()
        {
            var (w, h) = MepLogic.Section(new[] { 1.0, 1, 0 });
            Assert.Equal(0, w[2], 9); // horizontal
            Assert.Equal(1, h[2], 9); // up
            var (wv, hv) = MepLogic.Section(new[] { 0.0, 0, 5 }); // a riser
            Near(new[] { 1.0, 0, 0 }, wv);
            Assert.Equal(0, MepLogic.Dot(wv, hv), 9);
            var (at, s) = MepLogic.Nearest(new[] { 0.0, 0, 0 }, new[] { 1000.0, 0, 0 }, new[] { 400.0, 30, 0 });
            Near(new[] { 400.0, 0, 0 }, at);
            Assert.Equal(0.4, s, 9);
        }

        // ------------------------------------------------------------ schedule

        [Fact]
        public void The_schedule_adds_lengths_by_system_and_size_and_counts_fittings()
        {
            var marks = new List<MepMark>
            {
                new MepMark { System = "drain", Kind = "pipe", Shape = "round", Size1 = 110, Size2 = 110, LengthMm = 2500 },
                new MepMark { System = "drain", Kind = "elbow", Shape = "round", Size1 = 110, Size2 = 110, LengthMm = 259.18 },
                new MepMark { System = "drain", Kind = "pipe", Shape = "round", Size1 = 60, Size2 = 60, LengthMm = 1000 },
                new MepMark { System = "supply", Kind = "pipe", Shape = "round", Size1 = 25, Size2 = 25, LengthMm = 4000 },
                new MepMark { System = "supply", Kind = "tee", Shape = "round", Size1 = 25, Size2 = 25 },
                new MepMark { System = "duct_supply", Kind = "pipe", Shape = "rect", Size1 = 400, Size2 = 300, LengthMm = 6000 },
            };
            var rows = MepLogic.Schedule(marks);
            Assert.Equal(new[] { "supply|D25", "drain|D60", "drain|D110", "duct_supply|400 × 300" }, rows.Select(r => r.System + "|" + r.Size).ToArray());
            var d110 = rows.Single(r => r.Size == "D110");
            Assert.Equal(2.75918, d110.LengthM, 6);
            Assert.Equal(1, d110.Elbows);
            Assert.Equal(1, rows.Single(r => r.System == "supply").Tees);
            var t = MepLogic.ScheduleTable(rows, null);
            Assert.Equal(6, t[0].Count);
            Assert.Equal("Cấp nước", t[1][0]);
            Assert.Equal("Tổng", t[t.Count - 1][0]);
            Assert.Equal(13.76, (double)t[t.Count - 1][2], 6);
            Assert.Equal(rows.Count, XlsxLogic.DataRows(t)); // the total row is not a data row
            var w = MepLogic.ScheduleTable(rows, 0.05);
            Assert.Equal(7, w[0].Count);
            Assert.Contains("hao hụt 5%", (string)w[0][3]);
            Assert.Equal(Math.Round(2.75918 * 1.05, 2), (double)w.Single(r => Equals(r[1], "D110"))[3], 6);
        }

        [Fact]
        public void The_waste_column_is_off_until_asked()
        {
            Assert.False(H7Numbers.WasteColumnByDefault);
            Assert.Null(MepLogic.WasteShare(null));
            Assert.Equal(0.07, MepLogic.WasteShare(7).Value, 9);
            Assert.Throws<ToolException>(() => MepLogic.WasteShare(-1));
        }

        // ------------------------------------------------------------ clash

        [Fact]
        public void A_clash_overlaps_and_a_close_pipe_is_under_the_clearance()
        {
            Assert.Equal("clash", MepLogic.ClashKind(true, 0, 50));
            Assert.Equal("close", MepLogic.ClashKind(false, 30, 50));
            Assert.Null(MepLogic.ClashKind(false, 50, 50));
            Assert.Equal(0, MepLogic.BoxGap(new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 }, new[] { 5.0, 5, 5 }, new[] { 20.0, 20, 20 }));
            Assert.Equal(30, MepLogic.BoxGap(new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 }, new[] { 40.0, 0, 0 }, new[] { 50.0, 10, 10 }), 9);
            Assert.Equal(5, MepLogic.BoxGap(new[] { 0.0, 0, 0 }, new[] { 10.0, 10, 10 }, new[] { 13.0, 14, 0 }, new[] { 20.0, 20, 10 }), 9);
            Assert.True(MepLogic.IsStructureLayer("A-BEAM-3D"));
            Assert.True(MepLogic.IsStructureLayer("A-COLS-3D"));
            Assert.False(MepLogic.IsStructureLayer("P-DRAIN-3D"));
            Assert.False(MepLogic.IsStructureLayer("A-WALL-3D"));
            // one route, or a branch on its main, is never a clash with itself (source)
            Assert.Contains("b.r.Route == a.r.Route || b.r.Link == a.r.Route || a.r.Link == b.r.Route", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        // ------------------------------------------------------------ numbers waiting for approval stay in one place

        [Fact]
        public void The_standard_numbers_are_only_in_h7_numbers_and_the_skill_says_the_same()
        {
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            foreach (var n in new[] { "1.5", "0.025", "= 50", "= 100", "= 300" }) Assert.DoesNotContain(" " + n + ";", tools);
            // approved 26/11/2026: the skill gives the same defaults (and says the least slopes are to be checked against TCVN)
            var skill = Src("skills", "ve-mep", "SKILL.md");
            foreach (var d in new[] { H7Numbers.SupplyBranchMm, H7Numbers.SupplyMainMm, H7Numbers.DrainFloorMm, H7Numbers.DrainWcMm, H7Numbers.VentMm })
                Assert.Contains("D" + MepLogic.Num(d), skill);
            Assert.Contains($"dốc {MepLogic.Num(H7Numbers.DrainSlopePercent)} %", skill);
            Assert.Contains($"cách đáy dầm {MepLogic.Num(H7Numbers.TopBelowBeamMm)} mm", skill);
            Assert.Contains($"cách đáy sàn {MepLogic.Num(H7Numbers.TopBelowSlabMm)} mm", skill);
            Assert.Contains($"dưới {MepLogic.Num(H7Numbers.ClearanceMm)} mm", skill);
            Assert.Contains("kỹ sư đối chiếu TCVN hiện hành", skill);
            foreach (var s in MepLogic.Systems) Assert.Contains($"`{s.Layer3d}` ({s.Color})", skill);
            foreach (var (size, percent) in H7Numbers.MinDrainSlope) Assert.Contains($"D{MepLogic.Num(size)} {percent.ToString(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} %", skill);
        }
    }
}
