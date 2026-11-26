using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt H1 (docs/ban-giao/nang-cap-h1-test.md): the bugs found in the review, each with its test (real temporary
    /// folders; the chat and the PDF process are checked in their source, they need AutoCAD / Windows).
    /// </summary>
    public class NangCapH1TestSuaTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ccad-h1t-" + Guid.NewGuid().ToString("N"));
        private string Staged => Path.Combine(_root, SkillSync.StagedFolder);
        private string Skills => Path.Combine(_root, "skills");

        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static void Put(string dir, string rel, string text)
        {
            var p = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text, new UTF8Encoding(false));
        }

        private string At(string rel) => Path.Combine(Skills, rel);

        // ------------------------------------------------------------ 1. the notice is said after the chat is loaded

        [Fact]
        public void The_skill_notice_is_added_after_the_drawing_chat_is_loaded()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            int show = c.IndexOf("public void ShowPalette()", StringComparison.Ordinal);
            int sync = c.IndexOf("var skillNote = SyncShippedSkills();", show, StringComparison.Ordinal);
            int load = c.IndexOf("LoadTranscriptFor(AcApp.DocumentManager.MdiActiveDocument);", show, StringComparison.Ordinal);
            int note = c.IndexOf("if (skillNote != null) Ui.AddNote(skillNote);", show, StringComparison.Ordinal);
            Assert.True(show > 0 && sync > show && load > sync && note > load, "sync, then load the chat (it clears the chat), then the note");
            // the sync itself never writes to the chat (the note would be cleared at once)
            int fn = c.IndexOf("private string SyncShippedSkills()", StringComparison.Ordinal);
            int end = c.IndexOf("private void InitSkills()", fn, StringComparison.Ordinal);
            Assert.True(fn > 0 && end > fn);
            Assert.DoesNotContain("Ui.AddNote", c.Substring(fn, end - fn));
            // loading the chat really clears what was there before
            var ui = Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatControl.cs");
            int r = ui.IndexOf("private void RenderTranscript(", StringComparison.Ordinal);
            Assert.True(r > 0 && ui.IndexOf("ClearHistory();", r, StringComparison.Ordinal) - r < 200);
        }

        // ------------------------------------------------------------ 2. a side file the user edited is never written over

        [Fact]
        public void A_side_file_the_user_edited_is_kept_and_the_next_version_takes_the_next_name()
        {
            Put(Staged, "ve-mep/SKILL.md", "mep v1");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            File.WriteAllText(At("ve-mep/SKILL.md"), "mep của khách");
            Put(Staged, "ve-mep/SKILL.md", "mep v2");
            var r2 = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal(new[] { "ve-mep" }, r2.Notice);
            var side = At("ve-mep/SKILL.ban-moi.md");
            Assert.Equal("mep v2", File.ReadAllText(side));
            // the user merges in the side file, then version 3 arrives
            File.WriteAllText(side, "mep v2 + gộp của khách");
            Put(Staged, "ve-mep/SKILL.md", "mep v3");
            var r3 = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("mep của khách", File.ReadAllText(At("ve-mep/SKILL.md")));
            Assert.Equal("mep v2 + gộp của khách", File.ReadAllText(side));
            var side2 = At("ve-mep/SKILL.ban-moi-2.md");
            Assert.Equal("mep v3", File.ReadAllText(side2));
            Assert.Equal(new[] { "ve-mep" }, r3.Notice);
            // the same version again: nothing new, nothing said
            Assert.Empty(SkillSync.Sync(Staged, Skills, new HashSet<string>()).Notice);
            Assert.Equal(3, Directory.GetFiles(At("ve-mep")).Length);
            // version 4: the plugin's own side file (v3, not edited) is reused
            Put(Staged, "ve-mep/SKILL.md", "mep v4");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("mep v4", File.ReadAllText(side2));
            Assert.Equal("mep v2 + gộp của khách", File.ReadAllText(side));
            Assert.Equal(3, Directory.GetFiles(At("ve-mep")).Length);
        }

        [Fact]
        public void A_side_file_left_as_it_was_is_updated_in_place()
        {
            Put(Staged, "ve-mep/SKILL.md", "mep v1");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            File.WriteAllText(At("ve-mep/SKILL.md"), "của khách");
            Put(Staged, "ve-mep/SKILL.md", "mep v2");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Put(Staged, "ve-mep/SKILL.md", "mep v3");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("mep v3", File.ReadAllText(At("ve-mep/SKILL.ban-moi.md")));
            Assert.False(File.Exists(At("ve-mep/SKILL.ban-moi-2.md")));
        }

        [Fact]
        public void Side_names_are_never_loaded_as_a_skill()
        {
            Assert.EndsWith("SKILL.ban-moi.md", SkillSync.SidePath(Path.Combine("a", "SKILL.md")));
            Assert.EndsWith("SKILL.ban-moi-2.md", SkillSync.SidePath(Path.Combine("a", "SKILL.md"), 2));
            Assert.Equal(SkillSync.SidePath(Path.Combine("a", "SKILL.md")), SkillSync.SidePath(Path.Combine("a", "SKILL.md"), 1));
            // the skill list reads only "SKILL.md" in each folder
            Assert.Contains("Path.Combine(dir, \"SKILL.md\")", Src("plugin", "ClaudeCAD.Plugin", "Core", "SkillStore.cs"));
        }

        // ------------------------------------------------------------ 3. PDF renders: no half render reused, the temp folder cleaned

        [Fact]
        public void Old_renders_and_half_renders_are_cleaned_and_nothing_else()
        {
            var dir = Path.Combine(_root, "pdf-trang");
            Directory.CreateDirectory(dir);
            var now = DateTime.UtcNow;
            string F(string name, double ageHours)
            {
                var p = Path.Combine(dir, name);
                File.WriteAllBytes(p, new byte[] { 1 });
                File.SetLastWriteTimeUtc(p, now.AddHours(-ageHours));
                return p;
            }
            var cache = PdfRenderLogic.CacheName(@"C:\a\b.pdf", 10, now, 1, 1024);
            var fresh = F(cache, 24 * (PdfRenderLogic.CacheKeepDays - 1));
            var old = F(PdfRenderLogic.CacheName(@"C:\a\c.pdf", 10, now, 1, 1024), 24 * (PdfRenderLogic.CacheKeepDays + 1));
            var halfOld = F(Path.GetFileName(PdfRenderLogic.PartialName(Path.Combine(dir, cache), "dead0001")), PdfRenderLogic.PartialKeepHours + 1);
            var halfNew = F(Path.GetFileName(PdfRenderLogic.PartialName(Path.Combine(dir, cache), "beef0002")), 0.1);
            var script = F("pdf-trang.ps1", 24 * 30);
            var other = F("anh-cua-khach.png", 24 * 30);
            Assert.Equal(2, PdfRenderLogic.CleanCache(dir, now));
            Assert.True(File.Exists(fresh));
            Assert.False(File.Exists(old));
            Assert.False(File.Exists(halfOld));
            Assert.True(File.Exists(halfNew), "a render still being written is left alone");
            Assert.True(File.Exists(script) && File.Exists(other));
            Assert.Equal(0, PdfRenderLogic.CleanCache(Path.Combine(_root, "khong-co"), now));
            Assert.Equal(0, PdfRenderLogic.CleanCache(null, now));
        }

        [Fact]
        public void A_half_render_never_has_the_cache_name()
        {
            var cache = Path.Combine(_root, PdfRenderLogic.CacheName(@"\\nas\ban-ve\scan.pdf", 1, DateTime.UtcNow, 2, 2048));
            var part = PdfRenderLogic.PartialName(cache, "abcd1234");
            Assert.NotEqual(cache, part);
            Assert.StartsWith(cache, part);
            Assert.EndsWith(".png", part); // the Windows reader is given a .png name
            // the process writes the temporary name, which is moved to the cache name only after a good exit
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PdfPageImage.cs");
            Assert.Contains("PdfRenderLogic.Arguments(script, info.FullName, page, width, part)", s);
            int kill = s.IndexOf("proc.Kill()", StringComparison.Ordinal);
            int del = s.IndexOf("TryDelete(part);", kill, StringComparison.Ordinal);
            int move = s.IndexOf("File.Move(part, outPng);", StringComparison.Ordinal);
            Assert.True(kill > 0 && del > kill && move > del, "a render stopped at the time limit is deleted, a whole one renamed");
            Assert.Contains("PdfRenderLogic.CleanCache(Folder, DateTime.UtcNow);", s);
        }
    }
}
