using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt G4 (docs/ban-giao/nang-cap-g4-test.md): the way back comes early and clean when -RENDER does not go as
    /// the plugin expects, the temporary file path is safe to type, the scene / view / temp file always come back.
    /// </summary>
    public class NangCapG4TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void A_render_command_that_ended_without_a_picture_falls_back_after_the_grace_not_the_max_wait()
        {
            var q = RenderLogic.QualityOf("medium");
            // ended 5 s ago, nothing written yet: still waiting (the file may be on its way)
            Assert.Equal(RenderLogic.State.Running, RenderLogic.StateOf(30, q.MaxWaitSeconds, 0, 0, 0, false, false, 5));
            // ended past the grace with no file, or an empty file: the way back at once
            Assert.Equal(RenderLogic.State.NoFile, RenderLogic.StateOf(30, q.MaxWaitSeconds, 0, 0, 0, false, false, G4Numbers.EndGraceSeconds));
            Assert.Equal(RenderLogic.State.NoFile, RenderLogic.StateOf(30, q.MaxWaitSeconds, 0, 0, 0, false, false, 60));
            // a written picture still wins, and Esc / failure keep their meaning
            Assert.Equal(RenderLogic.State.Done, RenderLogic.StateOf(30, q.MaxWaitSeconds, 5000, 5000, 2, false, false, 60));
            Assert.Equal(RenderLogic.State.Cancelled, RenderLogic.StateOf(30, q.MaxWaitSeconds, 0, 0, 0, true, false, 60));
            // still running: as before (the old overload)
            Assert.Equal(RenderLogic.State.Running, RenderLogic.StateOf(30, q.MaxWaitSeconds, 0, 0, 0, false, false));
            Assert.True(G4Numbers.EndGraceSeconds < q.MaxWaitSeconds);
            var why = RenderLogic.FallbackReason(RenderLogic.State.NoFile);
            Assert.Contains("without writing the picture", why);
            Assert.DoesNotContain("render took", why);
        }

        [Fact]
        public void An_answer_that_reaches_the_command_line_means_another_question_order()
        {
            var q = RenderLogic.QualityOf("high");
            string tmp = @"C:\Temp\claudecad-render-abc.png";
            foreach (var u in new[] { "_High", "HIGH", "1920", "1080", "_Y", "Y", tmp, "_R" })
                Assert.True(RenderLogic.IsOurAnswer(u, q, 1920, 1080, tmp), u);
            foreach (var u in new[] { "", "LINE", "RENDER", "100", "N" })
                Assert.False(RenderLogic.IsOurAnswer(u, q, 1920, 1080, tmp), u);
            // the script itself: one answer per line, the temp file the last one
            var script = RenderLogic.CommandScript(q, 1920, 1080, tmp);
            Assert.StartsWith("_-RENDER\n", script);
            Assert.EndsWith(tmp + "\n", script);
            Assert.Equal(7, script.Split('\n').Length - 1);
        }

        [Fact]
        public void The_temporary_render_file_is_in_a_folder_safe_to_type_at_the_command_line()
        {
            Assert.Equal(@"C:\Users\NGUYEN~1\AppData\Local\Temp\", RenderLogic.SafeTempDir(new[] { @"C:\Users\NGUYEN~1\AppData\Local\Temp\" }));
            // a space ends an answer at the AutoCAD command line; accents may not survive typing
            Assert.Equal(@"C:\ProgramData\ClaudeCAD\render", RenderLogic.SafeTempDir(new[] { @"C:\Users\Nguyen Van A\AppData\Local\Temp\", @"C:\ProgramData\ClaudeCAD\render" }));
            Assert.Null(RenderLogic.SafeTempDir(new[] { @"C:\Users\Nguyễn\Temp\", @"D:\my temp\" }));
            Assert.Null(RenderLogic.SafeTempDir(null));
            var t = RenderLogic.TempFile(@"C:\Temp\", "abc");
            Assert.DoesNotContain(" ", t);
        }

        [Fact]
        public void A_library_match_says_it_is_a_name_match_and_the_material_is_not_changed()
        {
            var n = MaterialLibraryLogic.Note("library", true, "Gạch", "Masonry - Brick - Red");
            Assert.Contains("'Masonry - Brick - Red'", n);
            Assert.Contains("name matches", n);
            Assert.Contains("not changed", n);
            Assert.Contains("library material", n);
        }

        [Fact]
        public void The_plugin_puts_everything_back_and_reports_render_or_capture_truthfully()
        {
            var g = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderG4.cs");
            // the early ways back
            Assert.Contains("doc.CommandEnded += OnRenderEnded;", g);
            Assert.Contains("doc.CommandEnded -= OnRenderEnded;", g);
            Assert.Contains("RenderLogic.IsOurAnswer(e.GlobalCommandName", g);
            Assert.Contains("job.EndedAt == DateTime.MinValue ? -1", g);
            // the temp folder: a safe path, else the picture at once (said as a capture)
            Assert.Contains("RenderLogic.SafeTempDir(cands)", g);
            Assert.Contains("if (tempDir == null)", g);
            // the temp file goes in every way but the finished render (copied first)
            Assert.Contains("if (!keepTemp && !string.IsNullOrEmpty(job.Temp))", g);
            Assert.Contains("FinishRender(job, keepTemp: true);", g);
            // the view comes back even if the background / light could not
            int scene = g.IndexOf("putting back the scene failed", StringComparison.Ordinal), view = g.IndexOf("putting back the view failed", StringComparison.Ordinal);
            Assert.True(scene > 0 && view > scene);
            // a finished render answered while nobody asked is kept for the next status
            Assert.Contains("_lastRenderResult", g);
            // "render" is said only on a written file; every other ending is a capture or a stop
            Assert.Equal(1, g.Split(new[] { "[\"method\"] = \"render\"" }, StringSplitOptions.None).Length - 1);
            Assert.Contains("[\"method\"] = \"capture\"", g);
        }
    }
}
