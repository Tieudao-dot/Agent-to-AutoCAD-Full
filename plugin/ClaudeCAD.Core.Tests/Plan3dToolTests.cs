using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// 3D phase 3 tools (docs/ban-giao/ve-3d-dot-3.md): plan labels, registration, macro replay of the new handle
    /// parameters, and source checks of the rules that need AutoCAD to test (plan objects only read, no AutoCAD
    /// command, temporary objects disposed, the self-test steps).
    /// </summary>
    public class Plan3dToolTests
    {
        public static readonly string[] Phase3 = { "build_3d_from_plan", "section_view", "projection_view" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string PlanTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PlanTo3dTools.cs");
        private static string SectionTools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "SectionTools.cs");

        [Fact]
        public void Phase3_tools_are_ThreeD_only()
        {
            foreach (var t in Phase3)
            {
                Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(t));
                Assert.False(PlanLogic.Allowed("2d", t), t);
                Assert.False(PlanLogic.Allowed(null, t), t);
                Assert.True(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d3d", t), t);
            }
            // the 2D tools are still closed to a 3D machine: the section hatch comes from section_view itself
            Assert.False(PlanLogic.Allowed("3d", "draw_hatch"));
            Assert.Contains("3D group", PlanLogic.BlockedMessage("section_view", "2d", null));
        }

        [Fact]
        public void Phase3_tools_are_registered_in_their_files()
        {
            var rx = new Regex("(?:Register\\(\\s*|new\\s+ToolDef\\(\\s*)\"([a-z0-9_]+)\"");
            var names = rx.Matches(PlanTools + SectionTools).Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            Assert.Equal(Phase3.OrderBy(x => x).ToArray(), names);
            var svc = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs");
            Assert.Contains("RegisterPlanTo3d(d);", svc);
            Assert.Contains("RegisterSections(d);", svc);
        }

        [Fact]
        public void New_handle_parameters_are_remapped_on_macro_replay()
        {
            foreach (var k in new[] { "wall_handles", "centerline_handles", "door_handles", "window_handles", "slab_handles", "handles" })
                Assert.True(MacroLogic.IsHandleKey(k), k);
            // plain values are not handles
            foreach (var k in new[] { "thickness", "openings", "line", "position", "wall_layers" })
                Assert.False(MacroLogic.IsHandleKey(k), k);
            Assert.True(MacroLogic.ShouldRecord("build_3d_from_plan", true));
            Assert.True(MacroLogic.ShouldRecord("section_view", true));
        }

        [Fact]
        public void Plan_objects_are_only_read()
        {
            var s = PlanTools;
            // never opens a plan object for write, erases or modifies it (only the layer table, to add a result layer)
            Assert.Equal(new[] { "lt.UpgradeOpen()" }, Regex.Matches(s, @"\w+\.UpgradeOpen\(\)").Cast<Match>().Select(m => m.Value).Distinct().ToArray());
            Assert.DoesNotContain(".Erase(", s);
            Assert.DoesNotContain("OpenMode.ForWrite", s);
            Assert.DoesNotContain("GetEntity(ctx, h, true)", s);
            // every GetObject reads
            foreach (Match m in Regex.Matches(s, @"GetObject\([^;]*;"))
                Assert.Contains("OpenMode.ForRead", m.Value);
        }

        [Fact]
        public void No_AutoCAD_command_is_sent_and_the_temporary_section_is_erased()
        {
            foreach (var s in new[] { PlanTools, SectionTools })
            {
                Assert.DoesNotContain("SendStringToExecute", s);
                Assert.DoesNotContain("Editor.Command", s);
                Assert.DoesNotContain(".Command(", s);
                Assert.DoesNotContain("FLATSHOT", s.Replace("(FLATSHOT / VIEWBASE)", ""));
            }
            var sec = SectionTools;
            Assert.Contains("GetSection(", sec);
            Assert.Contains("GenerateSectionGeometry(", sec);
            // the Section helper entity is erased in the same transaction (or disposed if never added)
            Assert.Matches(@"if \(sec != null\)\s*\{\s*if \(!sec\.ObjectId\.IsNull\) \{ if \(!sec\.IsErased\) sec\.Erase\(\); \}\s*else sec\.Dispose\(\);", sec);
            // AutoCAD failures of the behind lines only lose those lines, with a note
            Assert.Contains("the result has the cut only", sec);
        }

        [Fact]
        public void Temporary_solids_and_regions_are_disposed()
        {
            var s = PlanTools;
            Assert.Contains("s.Dispose(); // emptied by the union", s);
            Assert.Contains("catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }", s);
            Assert.Contains("finally { foreach (var x in regions) x.r.Dispose(); }", s);
            Assert.Contains("using (var cut = (Region)regions[live[b]].r.Clone())", s);
            var sec = SectionTools;
            Assert.Contains("DisposeAll(cut);", sec);
            Assert.Contains("DisposeAll(behindCurves);", sec);
        }

        [Fact]
        public void Refusals_say_nothing_was_built()
        {
            var s = PlanTools;
            foreach (var needle in new[] { "this plan was already built", "no wall recognised. Nothing was built", "more than", "Pass area_min / area_max" })
                Assert.Contains(needle, s);
            Assert.Contains("Nothing was drawn", SectionTools);
        }

        [Fact]
        public void Self_test_builds_a_room_and_cuts_it()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            Assert.Contains("DrawingService.Build3dFromPlan(ctx, p)", s);
            Assert.Contains("DrawingService.SectionView(ctx, p)", s);
            Assert.Contains("3D đợt 3: dựng 3D phòng", s);
            Assert.Contains("3D đợt 3: cắt ngang tường", s);
        }

        [Fact]
        public void The_3d_skill_ships_as_a_builtin_sample()
        {
            Assert.Contains("ve-3d-kien-truc", SkillStore.BuiltinNames);
            var md = Src("skills", "ve-3d-kien-truc", "SKILL.md");
            var (name, description, error) = SkillStore.ParseFrontmatter(md);
            Assert.Null(error);
            Assert.Equal("ve-3d-kien-truc", name);
            Assert.InRange(description.Length, 20, 1024);
            Assert.Contains("plan: 3d", md);
        }

        [Fact]
        public void KT15_dry_run_is_dispatched_as_a_read()
        {
            Assert.Contains("readsWhen: p => p.Bool(\"dry_run\", false)", PlanTools);
            var d = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            // the per-call decision drives the version copy, the undo group, "Chỉ xem" and the macro record
            Assert.Contains("bool writes = def.WritesFor(p) ||", d);
            Assert.Contains("MacroLogic.ShouldRecord(tool, def.WritesFor(p))", d);
            Assert.Contains("if (def.WritesFor(p) && !ctx.Writable)", d);
            Assert.DoesNotContain("def.Writes ||", d);
            // nothing is created before the dry run returns
            int dry = PlanTools.IndexOf("if (dryRun)", StringComparison.Ordinal), layer = PlanTools.IndexOf("EnsureLayer(ctx, wall3d", StringComparison.Ordinal);
            Assert.True(dry > 0 && layer > dry);
        }

        [Fact]
        public void KT16_base_z_is_mm_scaled_like_the_heights()
        {
            Assert.Contains("double baseZmm = p.Num(\"base_z\", 0), baseZ = baseZmm / f;", PlanTools);
            Assert.Contains("Report(res, input, f, unitName, hmm, baseZmm)", PlanTools);
        }

        [Fact]
        public void KT20_door_blocks_are_read_by_name_before_the_wall_layer()
        {
            var s = PlanTools;
            int byName = s.IndexOf("string byName = e is BlockReference", StringComparison.Ordinal);
            int wall = s.IndexOf("FloorPlanLogic.LayerMatches(layer, wallLayers)) kind = \"wall\"", StringComparison.Ordinal);
            Assert.True(byName > 0 && wall > byName);
        }

        [Fact]
        public void KT14_rerun_message_never_suggests_erasing_another_storey()
        {
            var s = PlanTools;
            Assert.Contains("FloorPlanLogic.CheckRerun(", s);
            Assert.Contains("Never erase other storeys", s);
            Assert.Contains("Never erase the storey below", s);
            var skill = Src("skills", "ve-3d-kien-truc", "SKILL.md");
            Assert.Contains("Không bao giờ** gợi ý xóa tầng dưới", skill);
            Assert.Contains("3300 − 120 = 3180", skill);
        }
    }
}
