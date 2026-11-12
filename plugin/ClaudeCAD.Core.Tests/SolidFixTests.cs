using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>3D phase 1, test round 1: the fixes found when reviewing the 3D tools (docs/ban-giao/ve-3d-test-1.md).</summary>
    public class SolidFixTests
    {
        private static string Source(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin" }.Concat(parts).ToArray())));

        // ------------------------------------------------------------ macros: solid_subtract's target / subtract

        [Fact]
        public void Macro_subtract_uses_the_replayed_solids()
        {
            Assert.True(MacroLogic.IsHandleKey("target"));
            Assert.True(MacroLogic.IsHandleKey("subtract"));
            var map = new Dictionary<string, string> { ["A1"] = "B1", ["A2"] = "B2" };
            var p = new Dictionary<string, object> { ["target"] = "A1", ["subtract"] = new List<object> { "A2" }, ["keep_tools"] = false };
            var r = (Dictionary<string, object>)MacroLogic.Remap(p, map);
            Assert.Equal("B1", r["target"]);
            Assert.Equal(new List<object> { "B2" }, r["subtract"]);
        }

        [Fact]
        public void Macro_subtract_runs_after_the_batch_that_built_its_solids()
        {
            var calls = new List<MacroCall>
            {
                new MacroCall { Tool = "draw_box", ResultHandles = { "A1" } },
                new MacroCall { Tool = "draw_box", ResultHandles = { "A2" } },
                new MacroCall { Tool = "solid_subtract", Params = { ["target"] = "A1", ["subtract"] = new List<object> { "A2" } }, ResultHandles = { "A1" } },
            };
            var groups = MacroLogic.PlanBatches(calls, _ => true);
            Assert.Equal(2, groups.Count);
            Assert.Equal(new[] { 0, 1 }, groups[0]);
            Assert.Equal(new[] { 2 }, groups[1]);
            // the wall was not rebuilt on replay: the subtract is skipped, never sent with the user's old handle
            var missing = MacroLogic.Unresolved(calls[2].Params, new HashSet<string> { "A1", "A2" }, new Dictionary<string, string> { ["A2"] = "B2" });
            Assert.Equal(new[] { "A1" }, missing);
            var m = new Macro { Calls = calls };
            Assert.Equal(new[] { "A1", "A2" }, MacroLogic.CreatedHandles(m));
            Assert.Empty(MacroLogic.ExternalHandles(m));
        }

        // ------------------------------------------------------------ extrude

        [Fact]
        public void Extrude_goes_up_for_a_flat_profile_whatever_its_normal()
        {
            Assert.Equal(3300, SolidLogic.ExtrudeHeightAlongNormal(3300, 1));
            Assert.Equal(-3300, SolidLogic.ExtrudeHeightAlongNormal(3300, -1));   // normal -Z: Extrude(-h) goes +Z
            Assert.Equal(200, SolidLogic.ExtrudeHeightAlongNormal(-200, -1));
            Assert.Equal(500, SolidLogic.ExtrudeHeightAlongNormal(500, 0));       // vertical profile: its own normal
        }

        [Fact]
        public void Taper_bound_is_the_assistant_schema_bound()
        {
            SolidLogic.CheckExtrude(100, 89.9);
            SolidLogic.CheckExtrude(100, -89.9);
            Assert.Throws<ToolException>(() => SolidLogic.CheckExtrude(100, 89.95));
            var ts = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agent", "src", "tools", "solids.ts")));
            Assert.Contains($".min(-{SolidLogic.MaxTaper}).max({SolidLogic.MaxTaper})", ts.Replace(" ", ""));
        }

        // ------------------------------------------------------------ wedge orientation

        [Fact]
        public void Wedge_centroid_check()
        {
            double l = 3000, w = 1200, h = 600;
            Assert.True(SolidLogic.WedgeCentroidOk(new[] { -l / 6, 0, -h / 6 }, l, w, h));
            Assert.False(SolidLogic.WedgeCentroidOk(new[] { l / 6, 0, -h / 6 }, l, w, h));   // tall face at +X
            Assert.False(SolidLogic.WedgeCentroidOk(new[] { -l / 6, 0, h / 6 }, l, w, h));   // upside down
            Assert.False(SolidLogic.WedgeCentroidOk(new[] { 0, -w / 6, -h / 6 }, l, w, h));  // slope along Y
        }

        // ------------------------------------------------------------ visual styles of localized AutoCAD

        [Fact]
        public void Every_style_has_a_type_fallback()
        {
            foreach (var key in SolidLogic.VisualStyleKeys)
                Assert.NotNull(SolidLogic.VisualStyleTypeOf(SolidLogic.VisualStyleName(key)));
        }

        [Fact]
        public void Style_is_picked_by_name_then_by_type()
        {
            var english = new List<(string, string, bool)> { ("2dWireframe", "Wireframe2D", false), ("Conceptual", "Conceptual", false), ("Realistic", "Realistic", false) };
            Assert.Equal("Conceptual", SolidLogic.PickVisualStyle(english, "Conceptual"));
            // a German drawing: the names differ, the types do not
            var german = new List<(string, string, bool)>
            {
                ("2D-Drahtkörper", "Wireframe2D", false), ("Konzeptuell", "Conceptual", false), ("Realistisch", "Realistic", false),
                ("Röntgen", "XRay", false), ("Schattiert", "Shaded", false), ("Schattiert mit Kanten", "ShadedWithEdges", false),
                ("Basic", "Basic", true),
            };
            Assert.Equal("Konzeptuell", SolidLogic.PickVisualStyle(german, "Conceptual"));
            Assert.Equal("2D-Drahtkörper", SolidLogic.PickVisualStyle(german, "2dWireframe"));
            Assert.Equal("Röntgen", SolidLogic.PickVisualStyle(german, "X-Ray"));
            Assert.Equal("Schattiert mit Kanten", SolidLogic.PickVisualStyle(german, "Shaded with edges"));
            Assert.Null(SolidLogic.PickVisualStyle(german, "Hidden"));
            // an old drawing names 3D wireframe "3dWireframe"; an internal style is never picked
            Assert.Equal("3dWireframe", SolidLogic.PickVisualStyle(new List<(string, string, bool)> { ("3dWireframe", null, false) }, "Wireframe"));
            Assert.Null(SolidLogic.PickVisualStyle(new List<(string, string, bool)> { ("Conceptual", "Conceptual", true) }, "Conceptual"));
        }

        [Fact]
        public void Style_type_names_are_members_of_VisualStyleType()
        {
            // the AutoCAD enum GraphicsInterface.VisualStyleType (checked in AcDbMgd 2022 / 2025 / 2026)
            var members = new HashSet<string> { "Wireframe2D", "Wireframe3D", "Hidden", "Conceptual", "Shaded", "ShadedWithEdges", "Realistic", "XRay" };
            foreach (var key in SolidLogic.VisualStyleKeys)
                Assert.Contains(SolidLogic.VisualStyleTypeOf(SolidLogic.VisualStyleName(key)), members);
        }

        // ------------------------------------------------------------ plugin source guards

        [Fact]
        public void New_solids_are_disposed_when_they_do_not_reach_the_drawing()
        {
            var src = Source("Drawing", "SolidTools.cs");
            // every solid is appended through AddSolid (disposes on a locked layer...), never Acad.Add directly
            Assert.Single(Regex.Matches(src, @"Acad\.Add\(").Cast<Match>());
            // primitives are built through Build (disposes when CreateXxx / TransformBy throws)
            foreach (var api in new[] { "CreateBox", "CreateFrustum", "CreateSphere", "CreateWedge", "CreateTorus", "CreatePyramid" })
                Assert.Matches(@"Build\(\w+ =>\s*\{\s*\w+\." + api, src);
        }

        [Fact]
        public void Boolean_failures_become_clear_errors()
        {
            var src = Source("Drawing", "SolidTools.cs");
            // the tools call BooleanChecked; BooleanInto (raw) is only for the self-test and BooleanChecked itself
            Assert.Equal(3, Regex.Matches(src, @"BooleanChecked\(target").Count);
            Assert.Equal(2, Regex.Matches(src, @"BooleanInto\(").Count); // definition + the call in BooleanChecked
        }

        [Fact]
        public void Wedge_and_pyramid_follow_the_api_documentation()
        {
            // AcDbMgd.xml: CreatePyramid(height, sides, radius = inscribed circle of the bottom polygon, topRadius);
            // CreateFrustum / CreatePyramid are centred at half the height (PlaceOnAxis lifts them by h / 2)
            var src = Source("Drawing", "SolidTools.cs");
            Assert.Contains("CreatePyramid(h, sides, r, top)", src);
            Assert.Contains("CreateFrustum(h, radius, radius, topRadius)", src);
            Assert.Contains("PlaceOnAxis(x, h / 2, b, axis, rot)", src);
            // a square base of side 1000 has inscribed radius 500
            Assert.Equal(1000.0 * 1000 * 600 / 3, SolidLogic.PyramidVolume(4, 500, 0, 600), 6);
        }
    }
}
