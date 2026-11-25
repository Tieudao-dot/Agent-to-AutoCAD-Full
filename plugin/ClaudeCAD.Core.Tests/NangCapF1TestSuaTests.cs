using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt F1: the bugs found in the review (UCS + work mode) and edge / cross-plan cases.</summary>
    public class NangCapF1TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Plugin(params string[] parts) => Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray());

        private static void Near(double[] want, double[] got)
        {
            Assert.Equal(want.Length, got.Length);
            for (int i = 0; i < want.Length; i++) Assert.True(Math.Abs(want[i] - got[i]) < 1e-6, $"[{i}] want {want[i]} got {got[i]}");
        }

        private static readonly double S = Math.Sqrt(0.5);

        // UCS turned 45° in plan about (1000, 0, 0)
        private static UcsFrame Turned45() => new UcsFrame(new[] { 1000.0, 0, 0 }, new[] { S, S, 0 }, new[] { -S, S, 0 });

        private static UcsFrame Front() => new UcsFrame(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 0, 1 });

        // the WCS box of a UCS rectangle (what EntityInfo.Extents gives for an object drawn there)
        private static (double[] min, double[] max) WcsBoxOf(UcsFrame f, double x0, double y0, double x1, double y1)
        {
            var pts = new[] { f.ToWcs(new[] { x0, y0 }), f.ToWcs(new[] { x1, y0 }), f.ToWcs(new[] { x1, y1 }), f.ToWcs(new[] { x0, y1 }) };
            return (new[] { pts.Min(p => p[0]), pts.Min(p => p[1]), pts.Min(p => p[2]) }, new[] { pts.Max(p => p[0]), pts.Max(p => p[1]), pts.Max(p => p[2]) });
        }

        // ------------------------------------------------------------ TS-F1-01: build_3d_from_plan area_min / area_max in a UCS

        [Fact]
        public void Plan_area_in_a_turned_ucs_is_a_rectangle_along_the_ucs_axes()
        {
            var f = Turned45();
            double[] amin = { 0, 0 }, amax = { 10000, 8000 };
            // a wall drawn inside the area (UCS 1000..3000 × 1000..1200): inside
            var (wmin, wmax) = WcsBoxOf(f, 1000, 1000, 3000, 1200);
            Assert.True(f.PlanBoxInside(wmin, wmax, amin, amax));
            // corners given in any order
            Assert.True(f.PlanBoxInside(wmin, wmax, amax, amin));
            // a wall outside the area along the UCS y (y 8500..8700): outside
            var (omin, omax) = WcsBoxOf(f, 1000, 8500, 3000, 8700);
            Assert.False(f.PlanBoxInside(omin, omax, amin, amax));
            // what the old code did: both corners turned to WCS one by one, then a WCS box of them. That box misses the wall
            // that is inside the UCS area (the bug the fix removes)
            var c1 = f.ToWcs(amin);
            var c2 = f.ToWcs(amax);
            double bx0 = Math.Min(c1[0], c2[0]), bx1 = Math.Max(c1[0], c2[0]);
            Assert.False(wmin[0] >= bx0 && wmax[0] <= bx1, "the old two-corner WCS box should not hold this wall");
        }

        [Fact]
        public void Plan_area_in_world_and_a_moved_ucs_is_the_plain_box()
        {
            Assert.True(UcsFrame.World.PlanBoxInside(new[] { 10.0, 10, 0 }, new[] { 20.0, 20, 0 }, new[] { 0.0, 0 }, new[] { 100.0, 100 }));
            Assert.False(UcsFrame.World.PlanBoxInside(new[] { 10.0, 10, 0 }, new[] { 120.0, 20, 0 }, new[] { 0.0, 0 }, new[] { 100.0, 100 }));
            // moved only (origin 5000, 0, 3300): the area 0..100 is WCS 5000..5100; z of the plan does not matter
            var moved = new UcsFrame(new[] { 5000.0, 0, 3300 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 });
            Assert.True(moved.PlanBoxInside(new[] { 5010.0, 10, 0 }, new[] { 5090.0, 90, 0 }, new[] { 0.0, 0 }, new[] { 100.0, 100 }));
            Assert.False(moved.PlanBoxInside(new[] { 10.0, 10, 0 }, new[] { 90.0, 90, 0 }, new[] { 0.0, 0 }, new[] { 100.0, 100 }));
        }

        [Fact]
        public void Build_3d_from_plan_tests_its_area_in_the_ucs()
        {
            var src = Plugin("Drawing", "PlanTo3dTools.cs");
            Assert.Contains("var areaFrame = amin != null ? UcsScope.Input : null;", src);
            Assert.Contains("amin = areaFrame.FromWcs(amin); amax = areaFrame.FromWcs(amax);", src);
            Assert.Contains("areaFrame.PlanBoxInside(", src);
            Assert.Equal(2, src.Split(new[] { "InArea(EntityInfo.Extents(e), amin, amax, areaFrame)" }, StringSplitOptions.None).Length - 1);
            // World (UcsScope.Input null): the very same test as before F1
            Assert.Contains("return e.MinPoint.X >= amin[0] && e.MinPoint.Y >= amin[1] && e.MaxPoint.X <= amax[0] && e.MaxPoint.Y <= amax[1];", src);
        }

        // ------------------------------------------------------------ TS-F1-02: profiles from points in a tilted UCS

        [Fact]
        public void A_profile_on_a_front_ucs_keeps_its_shape()
        {
            // the old way: profile points (UCS front) turned to WCS, then a flat polyline from their WCS x, y: every point
            // of a 1000 × 500 rectangle lands on the WCS x axis (y = 0) — a profile with no area
            var f = Front();
            var ucsPts = new[] { new[] { 0.0, 0 }, new[] { 1000.0, 0 }, new[] { 1000.0, 500 }, new[] { 0.0, 500 } };
            var wcs = ucsPts.Select(p => f.ToWcs(p)).ToList();
            Assert.All(wcs, p => Assert.Equal(0, p[1], 9));
            Assert.False(f.ZUp);
            // the fix builds from the UCS numbers (FromWcs) and carries the polyline by the UCS matrix: same points back
            var local = wcs.Select(p => f.FromWcs(p)).ToList();
            for (int i = 0; i < 4; i++) Near(new[] { ucsPts[i][0], ucsPts[i][1], 0 }, local[i]);
            var m = f.ToWcsMatrix();
            foreach (var (l, w) in local.Zip(wcs, (a, b) => (a, b)))
            {
                var t = new[] { m[0] * l[0] + m[1] * l[1] + m[2] * l[2] + m[3], m[4] * l[0] + m[5] * l[1] + m[6] * l[2] + m[7], m[8] * l[0] + m[9] * l[1] + m[10] * l[2] + m[11] };
                Near(w, t);
            }
        }

        [Fact]
        public void Profiles_from_points_use_the_ucs_plane_and_extrude_along_the_ucs_z()
        {
            var acad = Plugin("Drawing", "Acad.cs");
            Assert.Contains("public static Polyline MakeProfilePolyline(", acad);
            Assert.Contains("if (f == null || f.ZUp) return MakePolyline(wcsVerts, closed, bulges);", acad);
            Assert.Contains("pl.TransformBy(ToWcsMatrix(f));", acad);
            Assert.Contains("Acad.MakeProfilePolyline(pts, true, bulges)", Plugin("Drawing", "SolidTools.cs"));
            // up = the UCS z (World: Vector3d.ZAxis, so Normal·Z = Normal.Z as before)
            Assert.Contains("ExtrudeHeightAlongNormal(h, reg.Normal.DotProduct(Acad.UcsZ))", Plugin("Drawing", "SolidTools.cs"));
            Assert.Contains("ExtrudeHeightAlongNormal(dist, reg.Normal.DotProduct(Acad.UcsZ))", Plugin("Drawing", "Solid3Tools.cs"));
            Assert.Contains("public static Vector3d UcsZ => UcsScope.Input == null ? Vector3d.ZAxis : V3(UcsScope.Input.Z);", acad);
        }

        [Fact]
        public void Extrude_height_sign_against_the_ucs_z()
        {
            // front UCS: z = WCS −y. A profile whose normal is the UCS z goes up (+), one drawn the other way round flips
            var z = Front().Z;
            Assert.Equal(1000, SolidLogic.ExtrudeHeightAlongNormal(1000, UcsFrame.Dot(new[] { 0.0, -1, 0 }, z)));
            Assert.Equal(-1000, SolidLogic.ExtrudeHeightAlongNormal(1000, UcsFrame.Dot(new[] { 0.0, 1, 0 }, z)));
            // World as before
            Assert.Equal(1000, SolidLogic.ExtrudeHeightAlongNormal(1000, 1));
            Assert.Equal(-1000, SolidLogic.ExtrudeHeightAlongNormal(1000, -1));
        }

        // ------------------------------------------------------------ TS-F1-03: no "plan" talk in a work mode

        [Fact]
        public void Match_properties_skip_reason_names_the_mode_not_the_plan()
        {
            // a 3D machine (no work mode): exactly as before
            Assert.Equal(" (this machine's plan has no 2D)", WorkModeLogic.NoTwoDReason(null));
            // a Full machine in the 3D mode: the mode, never a plan
            var r = WorkModeLogic.NoTwoDReason("3d");
            Assert.DoesNotContain("plan has", r);
            Assert.Contains("3D work mode", r);
            Assert.Contains("Cả hai", r);
            Assert.Contains("WorkModeLogic.NoTwoDReason(ctx.Dispatcher?.WorkMode)", Plugin("Drawing", "Edit2DTools.cs"));
            Assert.Contains("public string WorkMode => _host?.WorkMode;", Plugin("Drawing", "ToolDispatcher.cs"));
        }

        // ------------------------------------------------------------ cross-plan and edge cases of the work mode

        [Theory]
        [InlineData("2d", "3d")]
        [InlineData("2d", "2d")]
        [InlineData("3d", "2d")]
        [InlineData("3d", "both")]
        public void A_2d_or_3d_machine_ignores_a_stale_mode(string plan, string mode)
        {
            Assert.Equal(plan, WorkModeLogic.EffectivePlan(plan, mode));
            Assert.Null(WorkModeLogic.Limiting(plan, mode));
            Assert.False(WorkModeLogic.ShowsSwitch(plan));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("goi-la")]
        public void An_unknown_or_missing_plan_has_no_switch(string plan)
        {
            Assert.False(WorkModeLogic.ShowsSwitch(plan));
            Assert.Null(WorkModeLogic.Limiting(plan, "2d"));
            Assert.Equal(plan, WorkModeLogic.EffectivePlan(plan, "3d"));
        }

        [Fact]
        public void Full_in_each_mode_gets_exactly_one_plan_and_a_mode_message()
        {
            Assert.Equal(PlanLogic.PlanFull, WorkModeLogic.EffectivePlan(PlanLogic.PlanFull, "both"));
            Assert.Null(WorkModeLogic.Limiting(PlanLogic.PlanFull, "both"));
            foreach (var m in new[] { "2d", "3d" })
            {
                Assert.Equal(m, WorkModeLogic.EffectivePlan(PlanLogic.PlanFull, m));
                Assert.Equal(m, WorkModeLogic.Limiting(PlanLogic.PlanFull, m));
                foreach (var text in new[] { WorkModeLogic.BlockedMessage("draw_solid", m), WorkModeLogic.BlockedParamMessage("insert_block", "attributes", m) })
                {
                    Assert.Contains("chế độ", text);
                    Assert.DoesNotContain("mua", text, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("liên hệ", text, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("nâng cấp", text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void Changed_note_says_when_the_mode_applies()
        {
            Assert.Contains("lượt đang chạy giữ chế độ cũ", WorkModeLogic.ChangedNote("2d", true));
            Assert.Contains("áp dụng từ tin nhắn tới", WorkModeLogic.ChangedNote("3d", false));
            Assert.Contains("đủ lệnh 2D và 3D", WorkModeLogic.ChangedNote("both", false));
        }

        [Fact]
        public void Settings_keep_a_valid_mode_and_drop_a_bad_one()
        {
            var s = new PaletteSettings { WorkMode = "3D" };
            var back = PaletteSettings.FromJson(s.ToJson());
            Assert.Equal("3d", back.WorkMode);
            var bad = PaletteSettings.FromJson(s.ToJson().Replace("\"3d\"", "\"4d\""));
            Assert.Equal("both", bad.WorkMode);
        }

        // ------------------------------------------------------------ macros: edge cases

        [Fact]
        public void A_bad_macro_ucs_is_refused_not_guessed()
        {
            var p = new Dictionary<string, object> { ["_ucs"] = "front" };
            Assert.Throws<ToolException>(() => UcsLogic.TakeMacroFrame(p));
            var p2 = new Dictionary<string, object> { ["_ucs"] = new Dictionary<string, object> { ["origin"] = new List<object> { 0.0, 0.0 }, ["x_axis"] = new List<object> { 1.0, 0.0 }, ["y_axis"] = new List<object> { 2.0, 0.0 } } };
            Assert.Throws<ToolException>(() => UcsLogic.TakeMacroFrame(p2)); // parallel axes
        }

        [Fact]
        public void Replay_keeps_a_stored_ucs_and_marks_only_calls_without_one()
        {
            var stored = Turned45().ToJson();
            var withUcs = UcsLogic.MarkReplay(new Dictionary<string, object> { ["_ucs"] = stored, ["start"] = new List<object> { 0.0, 0.0 } });
            Assert.Same(stored, withUcs["_ucs"]);
            var older = UcsLogic.MarkReplay(new Dictionary<string, object> { ["start"] = new List<object> { 0.0, 0.0 } });
            Assert.Equal("world", older["_ucs"]);
            var f = UcsLogic.TakeMacroFrame(withUcs);
            Assert.False(withUcs.ContainsKey("_ucs"));
            Near(Turned45().O, f.O);
            Near(Turned45().X, f.X);
            Assert.True(UcsLogic.TakeMacroFrame(older).IsWorld);
        }

        [Fact]
        public void Recording_in_world_returns_the_same_dictionary()
        {
            var p = new Dictionary<string, object> { ["start"] = new List<object> { 0.0, 0.0 } };
            Assert.Same(p, UcsLogic.ForMacro(p, UcsFrame.World));
            Assert.Same(p, UcsLogic.ForMacro(p, null));
            var r = UcsLogic.ForMacro(p, Turned45());
            Assert.NotSame(p, r);
            Assert.False(p.ContainsKey("_ucs")); // the call's own params are not changed
        }

        [Fact]
        public void A_near_world_ucs_is_world()
        {
            // a UCS read back from AutoCAD with rounding noise is still World: nothing converted, nothing recorded
            var f = new UcsFrame(new[] { 1e-12, 0, 0 }, new[] { 1.0, 1e-13, 0 }, new[] { -1e-13, 1.0, 0 });
            Assert.True(f.IsWorld);
            Assert.Null(UcsLogic.FixResult(null, f));
        }
    }
}
