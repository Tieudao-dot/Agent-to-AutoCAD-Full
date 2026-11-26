using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H1 (docs/ban-giao/nang-cap-h1.md): the skills on an upgraded machine (SkillSync).</summary>
    public class NangCapH1Tests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ccad-h1-" + Guid.NewGuid().ToString("N"));
        private string Staged => Path.Combine(_root, SkillSync.StagedFolder);
        private string Skills => Path.Combine(_root, "skills");

        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private void Put(string dir, string rel, string text)
        {
            var p = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text, new UTF8Encoding(false));
        }

        private string Read(string rel) => File.ReadAllText(Path.Combine(Skills, rel));

        [Fact]
        public void The_hash_ignores_line_ends_and_the_bom()
        {
            var lf = Encoding.UTF8.GetBytes("# Skill\nmột dòng\n");
            var crlf = Encoding.UTF8.GetBytes("# Skill\r\nmột dòng\r\n");
            var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(crlf).ToArray();
            Assert.Equal(SkillSync.Hash(lf), SkillSync.Hash(crlf));
            Assert.Equal(SkillSync.Hash(lf), SkillSync.Hash(bom));
            Assert.NotEqual(SkillSync.Hash(lf), SkillSync.Hash(Encoding.UTF8.GetBytes("# Skill\nmột dòng khác\n")));
            Assert.Equal(64, SkillSync.Hash(lf).Length);
        }

        [Fact]
        public void A_fresh_machine_gets_every_skill()
        {
            Put(Staged, "ve-kien-truc/SKILL.md", "v2");
            Put(Staged, "README.md", "r");
            var r = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal(2, r.Added.Count);
            Assert.Equal("v2", Read("ve-kien-truc/SKILL.md"));
            Assert.Empty(r.Notice);
            Assert.True(File.Exists(Path.Combine(Skills, SkillSync.ManifestFile)));
        }

        [Fact]
        public void A_skill_the_user_never_changed_is_replaced_and_a_changed_one_kept_with_the_new_one_next_to_it()
        {
            // version 1 installed by this plugin (recorded), then the user edits ve-mep; version 2 arrives
            Put(Staged, "ve-kien-truc/SKILL.md", "kt v1");
            Put(Staged, "ve-mep/SKILL.md", "mep v1");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Put(Skills, "ve-mep/SKILL.md", "mep v1 + ghi chú của khách");
            Put(Staged, "ve-kien-truc/SKILL.md", "kt v2");
            Put(Staged, "ve-mep/SKILL.md", "mep v2");
            var r = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("kt v2", Read("ve-kien-truc/SKILL.md"));
            Assert.Equal("mep v1 + ghi chú của khách", Read("ve-mep/SKILL.md")); // never touched
            Assert.Equal("mep v2", Read("ve-mep/SKILL" + SkillSync.SideSuffix + ".md"));
            Assert.Equal(new[] { "ve-kien-truc/SKILL.md" }, r.Replaced);
            Assert.Equal(new[] { "ve-mep" }, r.Notice);
            Assert.Contains("ve-mep", SkillSync.NoticeText(r.Notice));
            // said once: the next start (same version) says nothing and changes nothing
            var again = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Empty(again.Notice);
            Assert.Equal("mep v1 + ghi chú của khách", Read("ve-mep/SKILL.md"));
            // a later version: said again (new version next to it)
            Put(Staged, "ve-mep/SKILL.md", "mep v3");
            var v3 = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal(new[] { "ve-mep" }, v3.Notice);
            Assert.Equal("mep v3", Read("ve-mep/SKILL" + SkillSync.SideSuffix + ".md"));
        }

        [Fact]
        public void An_upgrade_from_a_version_without_the_record_uses_the_hashes_of_the_published_skills()
        {
            // the machine of v0.9.3: no record, the skill as it was published (with Windows line ends)
            Put(Skills, "ve-kien-truc/SKILL.md", "kt published\r\n");
            Put(Skills, "ve-ha-tang/SKILL.md", "ht changed by the user");
            Put(Staged, "ve-kien-truc/SKILL.md", "kt new");
            Put(Staged, "ve-ha-tang/SKILL.md", "ht new");
            var known = new HashSet<string> { SkillSync.Hash(Encoding.UTF8.GetBytes("kt published\n")), SkillSync.Hash(Encoding.UTF8.GetBytes("ht published")) };
            var r = SkillSync.Sync(Staged, Skills, known);
            Assert.Equal("kt new", Read("ve-kien-truc/SKILL.md"));
            Assert.Equal("ht changed by the user", Read("ve-ha-tang/SKILL.md"));
            Assert.Equal(new[] { "ve-ha-tang" }, r.Notice);
        }

        [Fact]
        public void Nothing_of_the_user_is_deleted_and_their_own_skills_stay()
        {
            Put(Skills, "cua-toi/SKILL.md", "skill của khách");
            Put(Skills, "ve-kien-truc/ghi-chu.md", "file thêm của khách");
            Put(Staged, "ve-kien-truc/SKILL.md", "kt");
            SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("skill của khách", Read("cua-toi/SKILL.md"));
            Assert.Equal("file thêm của khách", Read("ve-kien-truc/ghi-chu.md"));
            Assert.DoesNotContain("File.Delete", Src("plugin", "ClaudeCAD.Plugin", "Core", "SkillSync.cs"));
            // no staged folder (dev layout / old install): nothing at all
            Assert.Empty(SkillSync.Sync(Path.Combine(_root, "none"), Skills, null).Notice);
        }

        [Fact]
        public void A_broken_record_starts_again_without_losing_anything()
        {
            Directory.CreateDirectory(Skills);
            File.WriteAllText(Path.Combine(Skills, SkillSync.ManifestFile), "{ not json");
            Put(Skills, "ve-mep/SKILL.md", "user");
            Put(Staged, "ve-mep/SKILL.md", "new");
            var r = SkillSync.Sync(Staged, Skills, new HashSet<string>());
            Assert.Equal("user", Read("ve-mep/SKILL.md"));
            Assert.Equal(new[] { "ve-mep" }, r.Notice);
        }

        [Fact]
        public void The_published_skills_of_main_are_known()
        {
            Assert.True(SkillSync.KnownShipped.Count >= 10);
            Assert.All(SkillSync.KnownShipped, h => Assert.Matches("^[0-9a-f]{64}$", h));
            Assert.Equal("ve-kien-truc", SkillSync.SkillOf("ve-kien-truc/SKILL.md"));
            Assert.Equal("README", SkillSync.SkillOf("README.md"));
            Assert.EndsWith(Path.Combine("x", "SKILL.ban-moi.md"), SkillSync.SidePath(Path.Combine("x", "SKILL.md")));
        }

        [Fact]
        public void The_installer_and_the_in_app_update_stage_the_new_skills()
        {
            var iss = Src("installer", "ClaudeCAD.iss");
            Assert.Contains("DestDir: \"{app}\\Contents\\skills-ban-moi\"; Flags: recursesubdirs createallsubdirs ignoreversion", iss);
            Assert.Contains("onlyifdoesntexist uninsneveruninstall", iss); // the user's skills are still never overwritten / uninstalled
            Assert.Contains("Type: filesandordirs; Name: \"{app}\\Contents\\skills-ban-moi\"", iss);
            var script = UpdateLogic.ApplyScript(1234, @"C:\staged", @"C:\app\ClaudeCAD.bundle", @"C:\log.txt");
            var line = script.Split('\n').Single(l => l.Contains(SkillSync.StagedFolder));
            Assert.Contains("/MIR", line);
            Assert.StartsWith("robocopy", line.Trim());
            int purge = script.IndexOf("/PURGE", StringComparison.Ordinal);
            Assert.True(script.IndexOf(line, StringComparison.Ordinal) > purge, "after the program mirror (which drops the folder)");
            Assert.Contains("SyncShippedSkills();", Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs"));
        }

        // ------------------------------------------------------------ 2. trace_image reads the PDF page itself

        [Fact]
        public void The_pdf_page_is_rendered_by_windows_with_a_safe_command_line()
        {
            var args = PdfRenderLogic.Arguments(@"C:\Temp\pdf-trang.ps1", @"D:\Bản vẽ\nhà phố.pdf", 2, 2048, @"C:\Temp\out.png");
            Assert.Contains("-ExecutionPolicy Bypass -File \"C:\\Temp\\pdf-trang.ps1\"", args);
            Assert.Contains("-Pdf \"D:\\Bản vẽ\\nhà phố.pdf\"", args); // spaces and Vietnamese stay one argument
            Assert.Contains("-Page 2 -Width 2048", args);
            Assert.Equal("\"C:\\dir\\\\\"", PdfRenderLogic.Quote(@"C:\dir\")); // a trailing backslash does not eat the quote
            // the script only uses the PDF reader of Windows on the local file: no network, no other command
            var sc = PdfRenderLogic.Script;
            Assert.Contains("Windows.Data.Pdf.PdfDocument", sc);
            Assert.Contains("RenderToStreamAsync", sc);
            foreach (var bad in new[] { "Invoke-WebRequest", "Invoke-Expression", "http", "Net.WebClient", "Start-Process" })
                Assert.DoesNotContain(bad, sc, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Render_width_cache_name_and_png_size()
        {
            Assert.Equal(PdfRenderLogic.MinWidthPx, PdfRenderLogic.RenderWidth(1, 200));
            Assert.Equal(1280, PdfRenderLogic.RenderWidth(1, 1024));      // the whole page: about the look's width (steps of 256)
            Assert.Equal(PdfRenderLogic.MaxWidthPx, PdfRenderLogic.RenderWidth(0.05, 1568)); // a small part: capped
            var t = new DateTime(2026, 11, 26, 7, 0, 0, DateTimeKind.Utc);
            var a = PdfRenderLogic.CacheName(@"D:\a.pdf", 1000, t, 1, 2048);
            Assert.Equal(a, PdfRenderLogic.CacheName(@"d:\A.PDF", 1000, t, 1, 2048));
            Assert.NotEqual(a, PdfRenderLogic.CacheName(@"D:\a.pdf", 1001, t, 1, 2048));            // the file changed: render again
            Assert.NotEqual(a, PdfRenderLogic.CacheName(@"D:\a.pdf", 1000, t.AddSeconds(1), 1, 2048));
            Assert.NotEqual(a, PdfRenderLogic.CacheName(@"D:\a.pdf", 1000, t, 2, 2048));
            Assert.EndsWith("-p1-w2048.png", a);
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0x08, 0, 0, 0, 0x0B, 0x50 };
            Assert.Equal((2048, 2896), PdfRenderLogic.PngSize(png));
            Assert.Null(PdfRenderLogic.PngSize(new byte[24]));
        }

        [Fact]
        public void The_self_test_pdf_is_a_valid_one_page_pdf()
        {
            var b = PdfRenderLogic.SamplePdf();
            var s = Encoding.ASCII.GetString(b);
            Assert.Equal("pdf", TraceLogic.KindFromContent(b.Take(1024).ToArray()));
            Assert.Equal(1, TraceLogic.PdfPageCount(b));
            int startxref = int.Parse(s.Substring(s.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10).Split('\n')[0]);
            Assert.StartsWith("xref", s.Substring(startxref));
            // every object offset in the table points at its "n 0 obj"
            var lines = s.Substring(startxref).Split('\n').Skip(3).Take(4).ToList();
            for (int i = 0; i < 4; i++) Assert.StartsWith((i + 1) + " 0 obj", s.Substring(int.Parse(lines[i].Substring(0, 10))));
        }

        [Fact]
        public void The_look_of_a_pdf_uses_the_page_rendered_from_the_file_and_keeps_the_old_way()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs");
            Assert.Contains("PdfPageImage.Render(pdfFile, pdfPage, width, out pdfWhy)", t);
            Assert.Contains("look.From = \"pdf_page\"", t);
            Assert.Contains("res[\"picture_visible\"] = false;", t); // the fallback: AutoCAD's view, blank → stop and tell
            var r = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "PdfPageImage.cs");
            Assert.Contains("WaitForExit(PdfRenderLogic.TimeoutSeconds * 1000)", r);
            Assert.Contains("CreateNoWindow = true", r);
            Assert.Contains("Path.GetTempPath()", r); // renders go to the user's temp folder, not next to their files
        }

        // ------------------------------------------------------------ 3. KFG-43

        [Fact]
        public void Twenty_thousand_points_of_one_x_y_at_other_heights_are_checked_fast()
        {
            var items = new List<DrawingCheckLogic.DupItem>();
            for (int i = 0; i < 20000; i++) items.Add(new DrawingCheckLogic.DupItem { Group = "M|A|point|", Nums = new[] { 100.0, 200.0, i * 1.0 } });
            items.Add(new DrawingCheckLogic.DupItem { Group = "M|A|point|", Nums = new[] { 100.05, 200.0, 7.05 } }); // a copy of point 7
            items.Add(new DrawingCheckLogic.DupItem { Group = "M|A|point|", Nums = new[] { 100.0, 200.0, 7.5 } });   // 0.5 above: not a copy
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var dup = DrawingCheckLogic.DuplicateIndices(items, G2Numbers.DuplicateTolMm);
            sw.Stop();
            Assert.Equal(new List<int> { 20000 }, dup);
            Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
        }
    }
}
