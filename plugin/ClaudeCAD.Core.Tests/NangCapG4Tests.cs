using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt G4: a real render (named_view action render) with its way back, and the library materials.</summary>
    public class NangCapG4Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string G4() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderG4.cs");
        private static string Tools() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");

        // ------------------------------------------------------------ qualities, sizes, estimate

        [Fact]
        public void Three_qualities_on_AutoCAD_presets_medium_by_default()
        {
            Assert.Equal(new[] { "low", "medium", "high" }, RenderLogic.Qualities.Select(q => q.Key));
            Assert.Equal(new[] { "Low", "Medium", "High" }, RenderLogic.Qualities.Select(q => q.Preset));
            Assert.Equal("medium", RenderLogic.QualityOf(null).Key);
            Assert.Equal("high", RenderLogic.QualityOf(" HIGH ").Key);
            Assert.Throws<ToolException>(() => RenderLogic.QualityOf("ultra"));
            foreach (var q in RenderLogic.Qualities) Assert.True(q.MaxWaitSeconds > q.EstimateSeconds, q.Key);
            // slower quality, longer estimate and wait
            Assert.True(RenderLogic.Qualities.Zip(RenderLogic.Qualities.Skip(1), (a, b) => a.EstimateSeconds < b.EstimateSeconds && a.MaxWaitSeconds < b.MaxWaitSeconds).All(x => x));
        }

        [Fact]
        public void Size_defaults_full_hd_one_side_keeps_16_9_and_limits()
        {
            Assert.Equal((1920, 1080), RenderLogic.Size(null, null));
            Assert.Equal((1280, 720), RenderLogic.Size(1280, null));
            Assert.Equal((1920, 1080), RenderLogic.Size(null, 1080));
            Assert.Equal((800, 800), RenderLogic.Size(800, 800));
            Assert.Throws<ToolException>(() => RenderLogic.Size(5000, null));
            Assert.Throws<ToolException>(() => RenderLogic.Size(10, 10));
            Assert.Throws<ToolException>(() => RenderLogic.Size(100.5, 100));
            // a tall side given alone never goes past the limit on the other
            var (w, h) = RenderLogic.Size(null, 4000);
            Assert.True(w <= G4Numbers.MaxSide && h == 4000);
        }

        [Fact]
        public void Estimate_scales_with_pixels_within_5_s_and_the_max_wait()
        {
            var m = RenderLogic.QualityOf("medium");
            Assert.Equal(m.EstimateSeconds, RenderLogic.Estimate(m, 1920, 1080));
            Assert.Equal(m.EstimateSeconds * 4, RenderLogic.Estimate(m, 3840, 2160));
            Assert.Equal(5, RenderLogic.Estimate(RenderLogic.QualityOf("low"), 64, 64));
            var h = RenderLogic.QualityOf("high");
            Assert.Equal(h.MaxWaitSeconds, RenderLogic.Estimate(h, 4096, 4096));
        }

        // ------------------------------------------------------------ the -RENDER answers

        [Fact]
        public void The_render_answers_end_with_enter_and_use_english_keywords()
        {
            var s = RenderLogic.CommandScript(RenderLogic.QualityOf("high"), 1920, 1080, @"C:\Users\Nguyen Van A\Temp\claudecad-render-x.png");
            Assert.Equal("_-RENDER\n_High\n_R\n1920\n1080\n_Y\nC:\\Users\\Nguyen Van A\\Temp\\claudecad-render-x.png\n", s);
            Assert.DoesNotContain(" \n_", s.Replace("Nguyen Van A", "")); // no space used as Enter
            Assert.DoesNotContain("\"", RenderLogic.CommandScript(RenderLogic.QualityOf("low"), 64, 64, "a\"b.png"));
            Assert.EndsWith(".png", RenderLogic.TempFile(Path.GetTempPath(), "abc"));
            Assert.Contains("claudecad-render-abc", RenderLogic.TempFile("/tmp", "abc"));
        }

        // ------------------------------------------------------------ the state of a render

        [Theory]
        [InlineData(10, 300, 5000, 5000, 2.0, false, false, RenderLogic.State.Done)]
        [InlineData(10, 300, 5000, 5000, 0.5, false, false, RenderLogic.State.Running)] // still being written
        [InlineData(10, 300, 0, 0, 0, false, false, RenderLogic.State.Running)]
        [InlineData(301, 300, 0, 0, 0, false, false, RenderLogic.State.TooLong)]
        [InlineData(20, 300, 0, 0, 0, true, false, RenderLogic.State.Cancelled)]
        [InlineData(20, 300, 0, 0, 0, false, true, RenderLogic.State.Failed)]
        [InlineData(400, 300, 5000, 5000, 3.0, true, false, RenderLogic.State.Done)] // a finished file wins over the rest
        public void State_of_a_render(double elapsed, int max, long size, long before, double same, bool cancelled, bool failed, RenderLogic.State want) =>
            Assert.Equal(want, RenderLogic.StateOf(elapsed, max, size, before, same, cancelled, failed));

        [Fact]
        public void Progress_and_reasons_in_words()
        {
            Assert.Equal("đang render 20 s / khoảng 60 s", RenderLogic.Progress(20.2, 60, 300));
            Assert.Contains("lâu hơn dự kiến", RenderLogic.Progress(90, 60, 300));
            Assert.Contains("300 s", RenderLogic.Progress(90, 60, 300));
            Assert.Contains("longer than the maximum wait", RenderLogic.FallbackReason(RenderLogic.State.TooLong));
            Assert.Contains("no RENDER", RenderLogic.FallbackReason(RenderLogic.State.Failed, "this AutoCAD has no RENDER command"));
        }

        // ------------------------------------------------------------ lighting

        [Fact]
        public void Lighting_auto_keeps_the_drawing_lights_else_default_light()
        {
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("auto", 2, false, false, false));
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("auto", 0, true, true, false));
            Assert.Equal(((bool?)true, (bool?)null), RenderLogic.LightPlan("auto", 0, false, false, false));
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("auto", 0, false, false, true));
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("drawing", 0, false, false, false));
            // sun: on when the drawing has one (default light off), else default light
            Assert.Equal(((bool?)false, (bool?)true), RenderLogic.LightPlan("sun", 0, false, true, true));
            Assert.Equal(((bool?)true, (bool?)null), RenderLogic.LightPlan("sun", 0, false, false, false));
            Assert.Equal(((bool?)null, (bool?)false), RenderLogic.LightPlan("default", 1, true, true, true));
            Assert.Contains("no sun yet", RenderLogic.LightWords("sun", 0, false, false));
            Assert.Contains("2 light", RenderLogic.LightWords("auto", 2, false, false));
            Assert.Contains("unchanged", RenderLogic.LightWords("drawing", 0, false, false));
            Assert.Throws<ToolException>(() => RenderLogic.OneOf("night", RenderLogic.Lightings, "auto", "lighting"));
            Assert.Equal("white", RenderLogic.OneOf(null, RenderLogic.Backgrounds, G4Numbers.DefaultBackground, "background"));
        }

        [Fact]
        public void File_name_of_a_render()
        {
            Assert.Equal("hien-tai-render", RenderLogic.Tail(null));
            Assert.Equal("Mặt tiền-render", RenderLogic.Tail(" Mặt tiền "));
            var f = MaterialLogic.ImagePath(null, Path.GetTempPath(), "nha", RenderLogic.Tail("Phối cảnh"));
            Assert.EndsWith("nha-Phối cảnh-render.png", f);
        }

        // ------------------------------------------------------------ library materials

        [Fact]
        public void A_preset_takes_the_library_material_already_in_the_drawing()
        {
            var inDrawing = new[] { "Global", "Gạch", "Masonry - Brick - Red", "Brick Veneer", "Glass - Clear", "Concrete Block", "Woodland Paint", "Metal - Steel" };
            Assert.Equal("Brick Veneer", MaterialLibraryLogic.Match("brick", inDrawing)); // first by name
            Assert.Equal("Glass - Clear", MaterialLibraryLogic.Match("glass", inDrawing));
            Assert.Equal("Concrete Block", MaterialLibraryLogic.Match("concrete", inDrawing));
            Assert.Equal("Metal - Steel", MaterialLibraryLogic.Match("steel", inDrawing));
            Assert.Equal("Woodland Paint", MaterialLibraryLogic.Match("plaster", inDrawing)); // "paint" as a word
            Assert.Null(MaterialLibraryLogic.Match("wood", inDrawing)); // "Woodland" is not the word "wood"
            Assert.Null(MaterialLibraryLogic.Match("stone", inDrawing));
            Assert.Null(MaterialLibraryLogic.Match("brick", new[] { "Global", "Gạch", "ByLayer" })); // never AutoCAD's own nor the simple one
            Assert.Null(MaterialLibraryLogic.Match("unknown", inDrawing));
            Assert.Null(MaterialLibraryLogic.Match("brick", null));
            foreach (var k in MaterialLogic.PresetKeys) Assert.True(MaterialLibraryLogic.Words.ContainsKey(k), k);
        }

        [Fact]
        public void Friendly_names_and_notes()
        {
            Assert.Equal("plaster", MaterialLogic.Find("sơn").Key);
            Assert.Equal("steel", MaterialLogic.Find("kim loại").Key);
            Assert.Equal("brick", MaterialLogic.Find("gạch").Key);
            Assert.Equal("roof_tile", MaterialLogic.Find("ngói").Key);
            Assert.Contains("MATBROWSEROPEN", MaterialLibraryLogic.Note("simple", true, "Gạch"));
            Assert.Contains("not installed", MaterialLibraryLogic.Note("simple", false, "Gạch"));
            Assert.Contains("library material", MaterialLibraryLogic.Note("library", true, "Gạch"));
            var f = MaterialLibraryLogic.Folders(@"C:\Program Files\Common Files", @"C:\Program Files (x86)\Common Files").ToList();
            Assert.Equal(2, f.Count);
            Assert.All(f, x => Assert.EndsWith(Path.Combine("Autodesk Shared", "Materials"), x));
        }

        // ------------------------------------------------------------ the AutoCAD side (code checks)

        [Fact]
        public void Render_actions_are_reads_and_never_recorded()
        {
            foreach (var a in new[] { "render", "render_status", "render_stop" })
            {
                Assert.Contains(a, MaterialLogic.ViewActions);
                Assert.True(RenderLogic.IsRenderAction(a));
            }
            Assert.False(RenderLogic.IsRenderAction("image"));
            Assert.Contains("|| RenderLogic.IsRenderAction(a); }));", Tools());
            // a read is never recorded in a macro (MacroLogic.ShouldRecord needs a write)
            Assert.False(MacroLogic.ShouldRecord("named_view", false));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf("named_view"));
            Assert.False(PlanLogic.Allowed("2d", "named_view"));
            Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "named_view"));
        }

        [Fact]
        public void The_scene_comes_back_and_undo_records_nothing()
        {
            var g = G4();
            Assert.Contains("db.DisableUndoRecording(true);", g);
            Assert.Equal(g.Split(new[] { "db.DisableUndoRecording(true);" }, StringSplitOptions.None).Length, g.Split(new[] { "db.DisableUndoRecording(false);" }, StringSplitOptions.None).Length);
            // every end puts the scene back: done, cancelled, too long / failed, stop, stale
            Assert.True(g.Split(new[] { "FinishRender(job);" }, StringSplitOptions.None).Length - 1 >= 5);
            // the overwrite card before anything is changed; a capture is said as such
            int card = g.IndexOf("RequireOverwriteCard(ctx, p, tool, file", StringComparison.Ordinal);
            Assert.True(card > 0 && card < g.IndexOf("PrepareScene(doc, job);", StringComparison.Ordinal));
            Assert.True(card < g.IndexOf("SendStringToExecute(RenderLogic.CommandScript", StringComparison.Ordinal));
            Assert.Contains("[\"method\"] = \"capture\"", g);
            Assert.Contains("NOT a render", g);
            // Chỉ xem: no setting changed; a running user command is never typed into
            Assert.Contains("ctx.Dispatcher?.ReadOnlyTurn == true", g);
            Assert.Contains("!string.IsNullOrEmpty(doc.CommandInProgress) || !doc.Editor.IsQuiescent", g);
            // Esc only when the render command itself runs
            Assert.Contains("if (IsRender(job.Doc?.CommandInProgress))", g);
            // the Stop button of the chat stops it too
            Assert.Contains("Drawing.DrawingService.StopRender()", Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs"));
        }

        [Fact]
        public void Set_material_library_only_for_a_preset_without_own_colour()
        {
            var t = Tools();
            Assert.Contains("if (preset != null && rgb == null && !opacity.HasValue)", t);
            Assert.Contains("p.Bool(\"library\", true) ? MaterialLibraryLogic.Match(preset.Key, names0) : null", t);
            Assert.Contains("r[\"library_note\"] = libraryNote;", t);
        }
    }
}
