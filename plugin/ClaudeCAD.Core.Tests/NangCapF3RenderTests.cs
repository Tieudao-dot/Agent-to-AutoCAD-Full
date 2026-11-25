using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F3: set_material presets / checks and named_view (names, actions, PNG path), labels and plans.</summary>
    public class NangCapF3RenderTests
    {
        /// <summary>The 3D tools of đợt F3 (counted in SolidTests).</summary>
        public static readonly string[] NewTools = { "set_material", "named_view" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Theory]
        [InlineData("concrete", "Bê tông")]
        [InlineData("Bê tông", "Bê tông")]
        [InlineData("be tong", "Bê tông")]
        [InlineData("GLASS", "Kính")]
        [InlineData("kính", "Kính")]
        [InlineData("roof-tile", "Ngói")]
        [InlineData("roof_tile", "Ngói")]
        [InlineData("ngói", "Ngói")]
        [InlineData("gỗ", "Gỗ")]
        [InlineData("brick", "Gạch")]
        public void Presets_are_found_by_key_or_vietnamese_name(string asked, string name) => Assert.Equal(name, MaterialLogic.Find(asked).Name);

        [Fact]
        public void Preset_list_is_complete_and_sane()
        {
            Assert.Equal(new[] { "concrete", "brick", "plaster", "glass", "wood", "roof_tile", "steel", "stone" }, MaterialLogic.PresetKeys);
            Assert.Null(MaterialLogic.Find("unobtainium"));
            Assert.Null(MaterialLogic.Find(""));
            foreach (var p in MaterialLogic.Presets)
            {
                Assert.InRange(p.R, 0, 255); Assert.InRange(p.G, 0, 255); Assert.InRange(p.B, 0, 255);
                Assert.InRange(p.Opacity, 0, 100);
                Assert.Null(MaterialLogic.BadName(p.Name));
            }
            // only glass lets light through
            Assert.Equal(new[] { "glass" }, MaterialLogic.Presets.Where(p => p.Opacity < 100).Select(p => p.Key));
        }

        [Fact]
        public void Custom_colour_and_opacity_are_checked()
        {
            MaterialLogic.CheckLook(new double[] { 0, 128, 255 }, 50);
            MaterialLogic.CheckLook(null, null);
            Assert.Throws<ToolException>(() => MaterialLogic.CheckLook(new double[] { 0, 256, 0 }, null));
            Assert.Throws<ToolException>(() => MaterialLogic.CheckLook(new double[] { 0, 1.5, 0 }, null));
            Assert.Throws<ToolException>(() => MaterialLogic.CheckLook(new double[] { 0, 1 }, null));
            Assert.Throws<ToolException>(() => MaterialLogic.CheckLook(null, 101));
            Assert.Throws<ToolException>(() => MaterialLogic.CheckLook(null, -1));
            Assert.NotNull(MaterialLogic.BadName("a/b"));
            Assert.NotNull(MaterialLogic.BadName(" "));
            Assert.Null(MaterialLogic.BadName("Gỗ lim"));
        }

        [Fact]
        public void View_names_and_actions()
        {
            foreach (var a in new[] { "save", "restore", "list", "delete", "image", " SAVE " }) Assert.Contains(MaterialLogic.NormalizeAction(a), MaterialLogic.ViewActions);
            Assert.Throws<ToolException>(() => MaterialLogic.NormalizeAction("zoom"));
            Assert.Null(MaterialLogic.BadViewName("Mặt tiền 01"));
            Assert.NotNull(MaterialLogic.BadViewName("a:b"));
            Assert.NotNull(MaterialLogic.BadViewName(""));
        }

        [Fact]
        public void Png_goes_next_to_the_drawing_unless_a_full_path_is_given()
        {
            var dir = Path.GetTempPath();
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "phoi-canh.png")), MaterialLogic.ImagePath("phoi-canh.png", dir, "Nha", null));
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "Nha-Mặt tiền.png")), MaterialLogic.ImagePath(null, dir, "Nha", "Mặt tiền"));
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "Nha-sw_iso.png")), MaterialLogic.ImagePath(null, dir, "Nha", "sw_iso"));
            // a view name with characters a file cannot hold is made safe
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "Nha-A_B.png")), MaterialLogic.ImagePath(null, dir, "Nha", "A?B"));
            Assert.Contains(".png", Assert.Throws<ToolException>(() => MaterialLogic.ImagePath("a.jpg", dir, "Nha", null)).Message);
            Assert.Equal(1568, MaterialLogic.DefaultImageSize);
        }

        [Fact]
        public void Tools_are_labelled_3d_wcs_and_registered()
        {
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("set_material"));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("named_view"));
            Assert.True(PlanLogic.Allowed("3d", "set_material"));
            Assert.True(PlanLogic.Allowed("2d3d", "named_view"));
            Assert.False(PlanLogic.Allowed("2d", "set_material"));
            Assert.False(PlanLogic.Allowed("2d", "named_view"));
            // F1 work modes: 3D mode loads them, 2D mode does not
            Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "named_view"));
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), "set_material"));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("set_material", null));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("named_view", null));
            var reg = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");
            Assert.Contains("d.Register(new ToolDef(\"set_material\", true, SetMaterial, readsWhen: p => p.Bool(\"list\", false)));", reg);
            Assert.Contains("RegisterRender(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            // locked layers are skipped, not unlocked; an existing view / file is only replaced with replace: true
            Assert.Contains("if (ltr.IsLocked) { skipped.Add(", reg);
            Assert.Contains("already exists. Pass replace: true", reg);
            Assert.Contains("CCAD-SELFTEST-", Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs"));
        }
    }
}
