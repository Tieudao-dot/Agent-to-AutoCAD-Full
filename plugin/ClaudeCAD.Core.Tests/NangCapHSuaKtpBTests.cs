using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa lỗi KTP phần B (H7 + KTP-15) sau kiểm tra độc lập đợt H phần 2: docs/ban-giao/nang-cap-h-sua-ktp-b.md.</summary>
    public class NangCapHSuaKtpBTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void Ktp13_a_duct_under_a_beam_is_measured_by_half_its_height_not_half_its_diagonal()
        {
            // a duct 600 × 300 along x, its top 100 below the beam's bottom (as under_beams puts it)
            var a = new[] { 0.0, 0, 0 }; var b = new[] { 6000.0, 0, 0 };
            var min = new[] { 2000.0, -2000, 250 }; var max = new[] { 2300.0, 2000, 850 };
            Assert.Equal(100, MepLogic.SectionBoxGap(a, b, true, 600, 300, min, max), 6);
            Assert.Equal(0, MepLogic.PipeBoxGap(a, b, Math.Sqrt(600 * 600 + 300 * 300) / 2, min, max), 6); // the old reach: "sát 0"
            // beside a column: half its width
            Assert.Equal(200, MepLogic.SectionBoxGap(a, b, true, 600, 300, new[] { 1000.0, 500, -500 }, new[] { 1300.0, 800, 500 }), 6);
            // a round pipe: its radius, as before
            Assert.Equal(250 - 55, MepLogic.SectionBoxGap(a, b, false, 110, 0, new[] { 0.0, -500, 250 }, new[] { 100.0, 500, 300 }), 6);
            // two ducts one over the other: each reaches by half its height
            Assert.Equal(1000 - 150 - 100, MepLogic.SectionSectionGap(a, b, true, 600, 300, new[] { 0.0, 0, 1000 }, new[] { 6000.0, 0, 1000 }, true, 400, 200), 6);
            // the solid check is still gated by the safe (diagonal) reach: a real clash is never skipped
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("MepLogic.PipeBoxGap(a.r.A, a.r.B, Reach(a.r, f)", tools);
            Assert.Contains("if (!hit && IsStraight(a.r))", tools);
        }

        [Fact]
        public void Ktp14_two_routes_meeting_end_to_end_are_a_joint()
        {
            Assert.True(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.0, 3000, 0 }, 1));
            Assert.True(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4000.5, 0, 0 }, new[] { 8000.0, 0, 0 }, 1));
            Assert.False(MepLogic.EndToEnd(new[] { 0.0, 0, 0 }, new[] { 4000.0, 0, 0 }, new[] { 4100.0, 0, 0 }, new[] { 8000.0, 0, 0 }, 1));
            Assert.Contains("MepLogic.EndToEnd(a.r.A, a.r.B, b.r.A, b.r.B, H7Numbers.JoinToleranceMm / f)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void Ktp16_a_branch_joins_its_own_system_and_the_checklist_gives_the_main_pipe_height()
        {
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("if (main.Mark.System != sys.Key)", tools);
            Assert.Contains("there the centre line is at x", tools);
            var list = Src("docs", "checklist-dot-h7.md");
            Assert.Contains("D60 từ (1000,0) tới (1000,-2000), cao độ 2780", list);
            Assert.Contains("**khác hệ**", list);
            // 2780 really is the main drain's centre at x = 1000 (2800, falling 2 %)
            Assert.Equal(2780, MepLogic.Sloped(new List<double[]> { new[] { 0.0, 0 }, new[] { 1000.0, 0 } }, 2800, 2)[1][2], 6);
        }

        [Fact]
        public void Ktp17_the_bend_radius_is_more_than_half_the_section_across_the_bend()
        {
            // a duct 200 × 500 turning up: the bend is across its height (250); in plan: across its width (100)
            var up = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 2000.0, 0, 0 }, new[] { 2000.0, 0, 2000 } }, 600).Single(x => x.Kind == "elbow");
            Assert.Equal(250, MepLogic.HalfInBendPlane(up, true, 200, 500), 6);
            var plan = MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 2000.0, 0, 0 }, new[] { 2000.0, 2000, 0 } }, 600).Single(x => x.Kind == "elbow");
            Assert.Equal(100, MepLogic.HalfInBendPlane(plan, true, 200, 500), 6);
            Assert.Equal(55, MepLogic.HalfInBendPlane(plan, false, 110, 0), 6); // D110: bend_radius 20 is refused
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("is not more than half the section in the plane of a bend", tools);
            Assert.Contains("bend = H7Numbers.DuctBendFactor * 2 * need;", tools); // the default takes the side across the bend
        }

        [Fact]
        public void Ktp15_a_replayed_branch_joins_the_main_pipe_the_macro_drew_again()
        {
            Assert.True(MacroLogic.IsHandleKey("connect_to"));
            Assert.Contains("connect_to: handle.optional()", Src("agent", "src", "tools", "mep.ts"));
        }

        [Fact]
        public void Ktp27_28_29_excel_names_system_names_and_mixed_points()
        {
            Assert.Equal("ban-ve-ong-mep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", "mep_report"));
            Assert.Equal("ban-ve-va-cham-ong-mep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", F4Numbers.MepClashKey));
            Assert.Equal("ban-ve-thong-ke-thep.xlsx", XlsxLogic.DefaultName("ban-ve.dwg", "rebar_schedule"));
            Assert.Equal("Thống kê ống MEP", F4Numbers.SheetName["mep_report"]);
            foreach (var v in F4Numbers.SheetName.Values) Assert.True(v.Length <= F4Numbers.SheetNameMax, v);
            Assert.Equal("Cấp nước", MepLogic.SystemName("supply"));
            Assert.Equal("Ống gió cấp", MepLogic.SystemName("duct_supply"));
            Assert.Equal("xyz", MepLogic.SystemName("xyz"));
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("MepLogic.SystemName((string)r[\"system\"])", tools);
            Assert.Contains("}, res, F4Numbers.MepClashKey);", tools);
            Assert.Contains("some points have a height (x, y, z) and some not (x, y)", tools);
            Assert.Contains("leave out elevation and under_beams", tools);
        }

        private static List<double[]> TurnedBeamCorners(double cx, double cy, double angleDeg, double len, double w, double z0, double z1)
        {
            double a = angleDeg * Math.PI / 180, ux = Math.Cos(a), uy = Math.Sin(a);
            var r = new List<double[]>();
            foreach (var z in new[] { z0, z1 })
                foreach (var (sl, sw) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    r.Add(new[] { cx + sl * len / 2 * ux - sw * w / 2 * uy, cy + sl * len / 2 * uy + sw * w / 2 * ux, z });
            return r;
        }

        [Fact]
        public void Ktp45_a_beam_at_an_angle_is_measured_by_its_own_turned_box()
        {
            var box = MepLogic.FitPlanBox(TurnedBeamCorners(0, 0, 45, 4000, 300, 2600, 3000));
            Assert.Equal(2000, box.HalfL, 6);
            Assert.Equal(150, box.HalfW, 6);
            Assert.Equal(2600, box.Zmin, 6);
            // a riser D110 beside the beam: 500√2 from its axis, so 707 − 150 − 55 = 502 mm, not "sát 0" from the world box
            var a = new[] { 500.0, -500, 0 }; var b = new[] { 500.0, -500, 3000 };
            Assert.Equal(500 * Math.Sqrt(2) - 150 - 55, MepLogic.SectionPlanBoxGap(a, b, false, 110, 0, box), 6);
            Assert.Equal(500 * Math.Sqrt(2) - 150 - 55, MepLogic.PipePlanBoxGap(a, b, 55, box), 6);
            double hw = 2000 / Math.Sqrt(2) + 150 / Math.Sqrt(2);
            Assert.Equal(0, MepLogic.PipeBoxGap(a, b, 55, new[] { -hw, -hw, 2600 }, new[] { hw, hw, 3000 }), 6); // the old way
            // a level beam along x: the turned box is its plain box
            var flat = MepLogic.FitPlanBox(TurnedBeamCorners(1000, 0, 0, 4000, 300, 2600, 3000));
            Assert.Equal(2000, flat.HalfL, 6);
            Assert.Equal(1000, flat.Cx, 6);
            // under_beams: a route that passes beside the beam is not under it, one across it is
            Assert.False(MepLogic.SegmentOverPlanBox(new[] { 500.0, -500 }, new[] { 2000.0, 1000 }, box)); // parallel, 707 off the axis
            Assert.True(MepLogic.SegmentOverPlanBox(new[] { -1000.0, 1000 }, new[] { 1000.0, -1000 }, box)); // across it
            Assert.Contains("MepLogic.SegmentOverPlanBox(plan[i - 1], plan[i], turned)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void Ktp38_a_pipe_drawn_twice_or_overlapping_still_clashes_and_two_routes_end_to_end_do_not()
        {
            double tol = H7Numbers.JoinToleranceMm;
            var a1 = new[] { 0.0, 0, 3000 }; var b1 = new[] { 4000.0, 0, 3000 };
            // the same pipe twice: shares its ends, the solids overlap → reported
            Assert.False(MepLogic.JointOnly(true, a1, b1, a1, b1, tol));
            // overlapping along its length from a shared end (0 → 2000 inside 0 → 4000) → reported
            Assert.False(MepLogic.JointOnly(true, a1, b1, a1, new[] { 2000.0, 0, 3000 }, tol));
            // a route going on from the end of another: touching, no overlap → a joint
            Assert.True(MepLogic.JointOnly(false, a1, b1, b1, new[] { 4000.0, 3000, 3000 }, tol));
            // no shared end: never a joint
            Assert.False(MepLogic.JointOnly(false, a1, b1, new[] { 5000.0, 0, 3000 }, new[] { 8000.0, 0, 3000 }, tol));
            var tools = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs");
            Assert.Contains("if (endToEnd && MepLogic.JointOnly(hit,", tools);
            Assert.True(tools.IndexOf("if (endToEnd && MepLogic.JointOnly(hit,", StringComparison.Ordinal) > tools.IndexOf("hit = a.s.CheckInterference(b.s)", StringComparison.Ordinal),
                "the joint is skipped only after the solid check");
        }

        [Fact]
        public void Ktp51_two_routes_turning_at_a_shared_end_are_a_joint_even_when_the_solid_check_finds_the_wedge()
        {
            double tol = H7Numbers.JoinToleranceMm;
            var a1 = new[] { 0.0, 0, 3000 }; var b1 = new[] { 4000.0, 0, 3000 };
            // (0,0)→(4000,0) then (4000,0)→(4000,3000): the flat ends overlap in a wedge (overlap = true), still a joint
            Assert.True(MepLogic.JointOnly(true, a1, b1, b1, new[] { 4000.0, 3000, 3000 }, tol));
            Assert.True(MepLogic.JointOnly(true, a1, b1, b1, new[] { 6000.0, 2000, 3000 }, tol)); // 45°
            Assert.True(MepLogic.JointOnly(true, a1, b1, b1, new[] { 8000.0, 0, 3000 }, tol));    // straight on
            // the same way from the shared end: drawn twice / over a part of it → reported
            Assert.False(MepLogic.JointOnly(true, a1, b1, a1, b1, tol));
            Assert.False(MepLogic.JointOnly(true, a1, b1, new[] { 2000.0, 0, 3000 }, b1, tol));
            Assert.False(MepLogic.JointOnly(true, a1, b1, b1, new[] { 1000.0, 500, 3000 }, tol)); // back over it at a few degrees
            Assert.True(MepLogic.JointOnly(false, a1, b1, a1, b1, tol)); // no overlap found: nothing to report anyway
        }

        [Fact]
        public void Ktp46_two_branches_into_one_point_of_a_main_pipe_are_one_joint()
        {
            Assert.True(MepLogic.SameMainJoint("tee", "main1", "tee", "main1"));
            Assert.True(MepLogic.SameMainJoint("tee", "main1", "pipe", "main1")); // a tee and the other branch's first pipe
            Assert.False(MepLogic.SameMainJoint("pipe", "main1", "pipe", "main1")); // two branch pipes: still checked
            Assert.False(MepLogic.SameMainJoint("tee", "main1", "tee", "main2"));
            Assert.False(MepLogic.SameMainJoint("tee", null, "tee", null));
            Assert.Contains("MepLogic.SameMainJoint(a.r.Mark.Kind, a.r.Link, b.r.Mark.Kind, b.r.Link)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void Ktp17_29_round_two_messages_and_a_false_under_beams()
        {
            var e = Assert.Throws<ToolException>(() => MepLogic.Layout(new List<double[]> { new[] { 0.0, 0, 0 }, new[] { 100.0, 0, 0 }, new[] { 100.0, 100, 0 }, new[] { 200.0, 100, 0 } }, 165));
            Assert.Contains("lengthen it (move the points apart) or leave that corner out", e.Message);
            Assert.DoesNotContain("use a smaller bend_radius", e.Message);
            Assert.Contains("p.Bool(\"under_beams\", false)))", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "MepTools.cs"));
        }

        [Fact]
        public void A_structure_layer_is_named_so_s_dash_only_as_its_prefix()
        {
            Assert.True(MepLogic.IsStructureLayer("S-BEAM"));
            Assert.True(MepLogic.IsStructureLayer("S-FOOT-3D"));
            Assert.True(MepLogic.IsStructureLayer("A-COLUMN"));
            Assert.False(MepLogic.IsStructureLayer("A-WALLS-NEW"));
            Assert.False(MepLogic.IsStructureLayer("P-DRAIN-3D"));
        }
    }
}
