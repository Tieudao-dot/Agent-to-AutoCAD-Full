using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>3D phase 1: the AutoCAD-free part (views, styles, checks, volumes) and the 3D plan labels.</summary>
    public class SolidTests
    {
        private static readonly string[] Tools3d =
        {
            "draw_box", "draw_cylinder", "draw_sphere", "draw_wedge", "draw_torus", "draw_pyramid", "extrude", "revolve",
            "solid_union", "solid_subtract", "solid_intersect", "set_view_3d", "set_visual_style", "get_solid_info",
        };

        // ------------------------------------------------------------ plan labels

        [Fact]
        public void Every_3d_tool_is_labelled_ThreeD_and_open_only_to_3d_and_full()
        {
            foreach (var t in Tools3d)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.False(PlanLogic.Allowed("2d", t), t);
                Assert.True(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d3d", t), t);
                // a machine without a valid plan line gets the default plan (2D): no 3D
                Assert.False(PlanLogic.Allowed(null, t), t);
            }
            // phase 1 + phase 2 (Solid2Tests.Phase2) + phase 3 (Plan3dToolTests.Phase3)
            Assert.Equal(Tools3d.Length + Solid2Tests.Phase2.Length + Plan3dToolTests.Phase3.Length, PlanLogic.Groups.Count(kv => kv.Value == ToolGroup.ThreeD));
            // the self-check picture stays in every plan
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("capture_view"));
        }

        [Fact]
        public void The_3d_tools_are_registered_in_SolidTools()
        {
            var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing", "SolidTools.cs"));
            var rx = new Regex("(?:Register\\(\\s*|new\\s+ToolDef\\(\\s*)\"([a-z0-9_]+)\"");
            var names = rx.Matches(File.ReadAllText(file)).Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            Assert.Equal(Tools3d.OrderBy(x => x).ToArray(), names);
        }

        [Fact]
        public void Three_d_message_names_the_3d_group()
        {
            var msg = PlanLogic.BlockedMessage("draw_box", "2d", "");
            Assert.Contains("3D group", msg);
            Assert.Contains("\"2D\"", msg);
        }

        // ------------------------------------------------------------ views

        [Theory]
        [InlineData("top", 0, 0, 1)]
        [InlineData("front", 0, -1, 0)]
        [InlineData("right", 1, 0, 0)]
        [InlineData("left", -1, 0, 0)]
        [InlineData("back", 0, 1, 0)]
        [InlineData("sw_iso", -1, -1, 1)]
        [InlineData("se_iso", 1, -1, 1)]
        [InlineData("ne_iso", 1, 1, 1)]
        [InlineData("nw_iso", -1, 1, 1)]
        public void Presets_look_like_the_viewcube(string name, double x, double y, double z)
        {
            var p = SolidLogic.Preset(name);
            Assert.Equal(new[] { x, y, z }, p.Direction);
        }

        [Theory]
        [InlineData("iso", "sw_iso")]
        [InlineData("SW ISO", "sw_iso")]
        [InlineData("ne-iso", "ne_iso")]
        [InlineData("trước", "front")]
        [InlineData("Trên", "top")]
        [InlineData("side", "right")]
        [InlineData("plan", "top")]
        public void Preset_aliases(string alias, string name) => Assert.Equal(name, SolidLogic.Preset(alias).Name);

        [Fact]
        public void Unknown_preset_lists_the_names()
        {
            var ex = Assert.Throws<ToolException>(() => SolidLogic.Preset("isometric-ish"));
            Assert.Equal(ErrorCodes.InvalidParams, ex.Code);
            Assert.Contains("sw_iso", ex.Message);
        }

        [Fact]
        public void Screen_axes_top_and_front_and_iso()
        {
            var (r, u) = SolidLogic.ScreenAxes(SolidLogic.Preset("top"));
            AssertVec(new double[] { 1, 0, 0 }, r);
            AssertVec(new double[] { 0, 1, 0 }, u);
            (r, u) = SolidLogic.ScreenAxes(SolidLogic.Preset("front"));
            AssertVec(new double[] { 1, 0, 0 }, r);
            AssertVec(new double[] { 0, 0, 1 }, u);
            (r, u) = SolidLogic.ScreenAxes(SolidLogic.Preset("sw_iso"));
            var d = SolidLogic.Unit(SolidLogic.Preset("sw_iso").Direction, "x");
            Assert.Equal(0, SolidLogic.Dot(r, d), 9);
            Assert.Equal(0, SolidLogic.Dot(u, d), 9);
            Assert.Equal(0, SolidLogic.Dot(r, u), 9);
            Assert.True(u[2] > 0, "Z goes up on screen in an iso view");
            Assert.True(r[0] > 0 && r[1] < 0, "SW iso: +X goes right-down, -Y right");
        }

        [Fact]
        public void Custom_direction_is_normalised_and_vertical_uses_y_up()
        {
            var v = SolidLogic.Custom(new double[] { 0, 0, 5 });
            AssertVec(new double[] { 0, 0, 1 }, v.Direction);
            Assert.Equal(new double[] { 0, 1, 0 }, v.Up);
            Assert.Throws<ToolException>(() => SolidLogic.Custom(new double[] { 0, 0, 0 }));
        }

        [Fact]
        public void Projected_size_of_a_box()
        {
            double[] a = { 0, 0, 0 }, b = { 4000, 220, 3300 };
            var (w, h, c) = SolidLogic.ProjectedSize(a, b, SolidLogic.Preset("top"));
            Assert.Equal(4000, w, 6);
            Assert.Equal(220, h, 6);
            Assert.Equal(new double[] { 2000, 110, 1650 }, c);
            (w, h, _) = SolidLogic.ProjectedSize(a, b, SolidLogic.Preset("front"));
            Assert.Equal(4000, w, 6);
            Assert.Equal(3300, h, 6);
            (w, h, _) = SolidLogic.ProjectedSize(a, b, SolidLogic.Preset("right"));
            Assert.Equal(220, w, 6);
            Assert.Equal(3300, h, 6);
            // a cube seen along the diagonal: width = face diagonal (√2), height = 2·√(2/3)
            (w, h, _) = SolidLogic.ProjectedSize(new double[] { 0, 0, 0 }, new double[] { 1, 1, 1 }, SolidLogic.Preset("ne_iso"));
            Assert.Equal(Math.Sqrt(2), w, 9);
            Assert.Equal(2 * Math.Sqrt(2.0 / 3.0), h, 9);
        }

        // ------------------------------------------------------------ visual styles

        [Theory]
        [InlineData("conceptual", "Conceptual")]
        [InlineData("Conceptual", "Conceptual")]
        [InlineData("2d_wireframe", "2dWireframe")]
        [InlineData("2D Wireframe", "2dWireframe")]
        [InlineData("2dWireframe", "2dWireframe")]
        [InlineData("shaded", "Shaded")]
        [InlineData("realistic", "Realistic")]
        [InlineData("hidden", "Hidden")]
        [InlineData("shaded with edges", "Shaded with edges")]
        [InlineData("x-ray", "X-Ray")]
        public void Visual_style_names(string input, string acad) => Assert.Equal(acad, SolidLogic.VisualStyleName(input));

        [Fact]
        public void Unknown_visual_style_is_refused()
        {
            var ex = Assert.Throws<ToolException>(() => SolidLogic.VisualStyleName("toon"));
            Assert.Contains("conceptual", ex.Message);
        }

        // ------------------------------------------------------------ parameter checks

        [Fact]
        public void Primitive_checks()
        {
            SolidLogic.CheckBox(1, 2, 3);
            Assert.Throws<ToolException>(() => SolidLogic.CheckBox(0, 2, 3));
            Assert.Throws<ToolException>(() => SolidLogic.CheckBox(1, -2, 3));
            Assert.Throws<ToolException>(() => SolidLogic.CheckBox(1, 2, double.NaN));
            SolidLogic.CheckFrustum(100, 0, 500);   // cone
            SolidLogic.CheckFrustum(100, 100, -500); // downwards
            Assert.Throws<ToolException>(() => SolidLogic.CheckFrustum(0, 10, 10));
            Assert.Throws<ToolException>(() => SolidLogic.CheckFrustum(10, -1, 10));
            Assert.Throws<ToolException>(() => SolidLogic.CheckFrustum(10, 10, 0));
            SolidLogic.CheckTorus(500, 50);
            Assert.Throws<ToolException>(() => SolidLogic.CheckTorus(50, 50));
            Assert.Throws<ToolException>(() => SolidLogic.CheckTorus(50, 0));
            SolidLogic.CheckPyramid(4, 100, 0, 200);
            Assert.Throws<ToolException>(() => SolidLogic.CheckPyramid(2, 100, 0, 200));
            Assert.Throws<ToolException>(() => SolidLogic.CheckPyramid(33, 100, 0, 200));
            SolidLogic.CheckExtrude(-3300, 0);
            SolidLogic.CheckExtrude(100, 45);
            Assert.Throws<ToolException>(() => SolidLogic.CheckExtrude(0, 0));
            Assert.Throws<ToolException>(() => SolidLogic.CheckExtrude(100, 90));
        }

        [Fact]
        public void Revolve_axis_and_angle()
        {
            var (dir, a) = SolidLogic.RevolveAxis(new double[] { 0, 0, 0 }, new double[] { 0, 500, 0 }, 360);
            AssertVec(new double[] { 0, 1, 0 }, dir);
            Assert.Equal(2 * Math.PI, a, 12);
            (_, a) = SolidLogic.RevolveAxis(new double[] { 0, 0 }, new double[] { 0, 0, 1 }, 90);
            Assert.Equal(Math.PI / 2, a, 12);
            Assert.Throws<ToolException>(() => SolidLogic.RevolveAxis(new double[] { 1, 1, 1 }, new double[] { 1, 1, 1 }, 360));
            Assert.Throws<ToolException>(() => SolidLogic.RevolveAxis(new double[] { 0, 0 }, new double[] { 0, 1 }, 0));
            Assert.Throws<ToolException>(() => SolidLogic.RevolveAxis(new double[] { 0, 0 }, new double[] { 0, 1 }, 361));
        }

        [Fact]
        public void Profile_on_one_side_of_the_axis()
        {
            // a rectangle right of the Y axis, touching it: fine (a cylinder with a hole of radius 0)
            var rect = new[] { new double[] { 0, 0 }, new double[] { 300, 0 }, new double[] { 300, 1000 }, new double[] { 0, 1000 } };
            Assert.Null(SolidLogic.ProfileCrossesAxis(rect, new double[] { 0, 0 }, new double[] { 0, 1 }));
            // the axis through the middle of the profile
            Assert.NotNull(SolidLogic.ProfileCrossesAxis(rect, new double[] { 150, 0 }, new double[] { 150, 1 }));
            // an axis along Z cannot revolve an XY profile: a clear error now (KT-30), not AutoCAD's generic one
            Assert.Contains("plane", SolidLogic.ProfileCrossesAxis(rect, new double[] { 150, 0, 0 }, new double[] { 150, 0, 1 }));
        }

        [Fact]
        public void Box_center_from_corner_and_rotation()
        {
            Assert.Equal(new double[] { 2000, 110, 1650 }, SolidLogic.BoxCenter(new double[] { 0, 0, 0 }, 4000, 220, 3300, 0));
            var c = SolidLogic.BoxCenter(new double[] { 100, 100, 0 }, 4000, 220, 3300, 90);
            Assert.Equal(100 - 110, c[0], 9);
            Assert.Equal(100 + 2000, c[1], 9);
            Assert.Equal(1650, c[2], 9);
        }

        // ------------------------------------------------------------ volumes

        [Fact]
        public void Volumes()
        {
            // the wall of the plan's example: 4 m × 0.22 m × 3.3 m ≈ 2.9 m³
            Assert.Equal(2.904, SolidLogic.ToCubicMetres(SolidLogic.BoxVolume(4000, 220, 3300)), 9);
            Assert.Equal(Math.PI * 100 * 100 * 500, SolidLogic.FrustumVolume(100, 100, 500), 6);
            Assert.Equal(Math.PI * 100 * 100 * 500 / 3, SolidLogic.FrustumVolume(100, 0, 500), 6);
            Assert.Equal(4.0 / 3 * Math.PI * 1000, SolidLogic.SphereVolume(10), 9);
            Assert.Equal(500, SolidLogic.WedgeVolume(10, 10, 10), 9);
            Assert.Equal(2 * Math.PI * Math.PI * 500 * 50 * 50, SolidLogic.TorusVolume(500, 50), 6);
            // square pyramid, inscribed radius 50 → side 100: V = 100² · 300 / 3
            Assert.Equal(100 * 100 * 300 / 3.0, SolidLogic.PyramidVolume(4, 50, 0, 300), 6);
            Assert.Equal(100 * 100, SolidLogic.PolygonAreaByApothem(4, 50), 9);
            // a frustum of a pyramid with equal radii is a prism
            Assert.Equal(100 * 100 * 300, SolidLogic.PyramidVolume(4, 50, 50, 300), 6);
        }

        [Fact]
        public void Self_test_volume_verdict()
        {
            double expected = SolidLogic.BoxVolume(1000, 800, 600) - SolidLogic.FrustumVolume(200, 200, 600);
            Assert.True(SelfTestRules.CheckVolume("hộp trừ trụ", expected * 1.0004, expected).Ok);
            var bad = SelfTestRules.CheckVolume("hộp trừ trụ", SolidLogic.BoxVolume(1000, 800, 600), expected);
            Assert.False(bad.Ok);
            Assert.Contains("0,1 %", bad.Detail);
            Assert.False(SelfTestRules.CheckVolume("hộp", 0, expected).Ok);
        }

        private static void AssertVec(double[] expected, double[] actual)
        {
            for (int i = 0; i < 3; i++) Assert.Equal(expected[i], actual[i], 9);
        }
    }
}
