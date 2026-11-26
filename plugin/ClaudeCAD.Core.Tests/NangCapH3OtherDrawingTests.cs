using System;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt H3 (26/11/2026, docs/ban-giao/nang-cap-h3.md): the shared part "open another drawing only to read it"
    /// (Core/OtherDrawing.cs, Drawing/OtherDrawing.cs), used by H3 (compare two drawings) and H4.
    /// </summary>
    public class NangCapH3OtherDrawingTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void Only_dwg_and_dxf_files_are_read_with_a_reason_for_the_others()
        {
            Assert.Equal("dwg", OtherDrawingLogic.KindOf(@"C:\a\Bản vẽ cũ.DWG", true, 1000, "compare_drawings"));
            Assert.Equal("dxf", OtherDrawingLogic.KindOf(@"C:\a\b.dxf", true, 1000, "compare_drawings"));
            Assert.Contains("lock file", Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.dwl", true, 10, "t")).Message);
            Assert.Contains("backup", Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.bak", true, 10, "t")).Message);
            Assert.Contains("not a drawing", Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.pdf", true, 10, "t")).Message);
            Assert.Contains("no file", Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.dwg", false, 0, "t")).Message);
            Assert.Contains("empty", Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.dwg", true, 0, "t")).Message);
            long max = (long)OtherDrawingNumbers.MaxFileMb * 1024 * 1024;
            Assert.Equal("dwg", OtherDrawingLogic.KindOf(@"C:\a\b.dwg", true, max, "t"));
            var big = Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf(@"C:\a\b.dwg", true, max + 1, "compare_drawings"));
            Assert.Contains("compare_drawings", big.Message);
            Assert.Throws<ToolException>(() => OtherDrawingLogic.KindOf("  ", true, 10, "t"));
        }

        [Fact]
        public void The_release_of_a_dwg_comes_from_its_first_bytes()
        {
            byte[] H(string s) => Encoding.ASCII.GetBytes(s);
            Assert.Equal("AC1032", OtherDrawingLogic.DwgCode(H("AC1032\0\0\0")));
            Assert.Equal("AC1015", OtherDrawingLogic.DwgCode(H("AC1015")));
            Assert.Null(OtherDrawingLogic.DwgCode(H("%PDF-1.4")));
            Assert.Null(OtherDrawingLogic.DwgCode(H("AC10")));
            Assert.Null(OtherDrawingLogic.DwgCode(null));
            Assert.Null(OtherDrawingLogic.DwgCode(H("ACAB12")));
            Assert.Equal("AutoCAD 2018", OtherDrawingLogic.ReleaseOf("AC1032"));
            Assert.Equal("AutoCAD 2010", OtherDrawingLogic.ReleaseOf("AC1024"));
            Assert.Equal("a newer AutoCAD", OtherDrawingLogic.ReleaseOf("AC1036"));
            Assert.Equal("an old AutoCAD", OtherDrawingLogic.ReleaseOf("AC1006"));
            // the list is in order, so "newer than the newest known" holds
            var codes = OtherDrawingLogic.Releases.Select(r => r.code).ToList();
            Assert.Equal(codes.OrderBy(c => c, StringComparer.Ordinal), codes);
        }

        [Fact]
        public void A_drawing_of_a_newer_autocad_is_refused_before_autocad_reads_it()
        {
            Assert.True(OtherDrawingLogic.IsNewer("AC1036", "AC1032"));
            Assert.False(OtherDrawingLogic.IsNewer("AC1032", "AC1032"));
            Assert.False(OtherDrawingLogic.IsNewer("AC1015", "AC1032"));
            Assert.False(OtherDrawingLogic.IsNewer("AC1036", null)); // unknown own format: let AutoCAD try
            Assert.Null(OtherDrawingLogic.HeaderRefusal(@"C:\a\b.dwg", "AC1032", "AC1032", "t"));
            var newer = OtherDrawingLogic.HeaderRefusal(@"C:\a\b.dwg", "AC1036", "AC1032", "compare_drawings");
            Assert.Contains("newer AutoCAD", newer);
            Assert.Contains("Save As", newer);
            Assert.Contains("compare_drawings", newer);
            Assert.Contains("does not start like a DWG", OtherDrawingLogic.HeaderRefusal(@"C:\a\b.dwg", null, "AC1032", "t"));
        }

        [Theory]
        [InlineData("FileSharingViolation", "locked")]
        [InlineData("eFileLockedByACAD", "locked")]
        [InlineData("FileAccessErr", "locked")]
        [InlineData("AccessDenied: no", "locked")]
        [InlineData("DwgNeedsAHigherVersion", "newer")]
        [InlineData("DwgNeedsRecovery", "damaged")]
        [InlineData("BadDwgHeader", "damaged")]
        [InlineData("", "damaged")]
        [InlineData(null, "damaged")]
        public void Read_errors_say_in_use_newer_or_damaged(string status, string want)
        {
            var m = OtherDrawingLogic.ReadFailure(@"C:\a\Bản vẽ.dwg", status, "AC1032", "AC1032", "compare_drawings");
            Assert.Contains(want, m);
            Assert.Contains("Bản vẽ.dwg", m);
            Assert.StartsWith("compare_drawings:", m);
        }

        [Fact]
        public void A_header_newer_than_this_autocad_reads_as_newer_whatever_the_error()
        {
            Assert.Contains("newer", OtherDrawingLogic.ReadFailure(@"C:\a\b.dwg", "InvalidInput", "AC1036", "AC1032", "t"));
        }

        [Fact]
        public void Same_file_ignores_case_and_slashes()
        {
            Assert.True(OtherDrawingLogic.SameFile(@"C:\Du an\Nha.dwg", "c:/du an/NHA.DWG"));
            Assert.False(OtherDrawingLogic.SameFile(@"C:\Du an\Nha.dwg", @"C:\Du an\Nha-cu.dwg"));
            Assert.False(OtherDrawingLogic.SameFile(null, @"C:\a.dwg"));
        }

        [Fact]
        public void The_newest_code_of_the_running_autocad_comes_from_the_enum_names()
        {
            // DwgVersion: AC1015 = 23, AC1018 = 25, AC1021 = 27, AC1024 = 29, AC1027 = 31, AC1032 = 33, Current = 33, Newest = 33, MC0To0 = 0
            var values = new System.Collections.Generic.Dictionary<string, int>
            {
                ["MC0To0"] = 0, ["AC1015"] = 23, ["AC1018"] = 25, ["AC1021"] = 27, ["AC1024"] = 29, ["AC1027"] = 31, ["AC1032"] = 33, ["Current"] = 33, ["Newest"] = 33,
            };
            Assert.Equal("AC1032", OtherDrawingLogic.NewestCode(values.Keys.ToArray(), n => values[n], 33));
            Assert.Equal("AC1027", OtherDrawingLogic.NewestCode(values.Keys.ToArray(), n => values[n], 31));
            Assert.Equal("AC1032", OtherDrawingLogic.NewestCode(values.Keys.ToArray(), n => values[n], 99)); // not found: the newest name
            Assert.Null(OtherDrawingLogic.NewestCode(new string[0], n => 0, 0));
        }

        [Fact]
        public void The_other_drawing_is_read_without_locking_or_saving_it_and_its_path_follows_the_trace_image_rule()
        {
            var s = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "OtherDrawing.cs");
            Assert.Contains("FileOpenMode.OpenForReadAndAllShare", s);
            Assert.Contains("db.CloseInput(true)", s);
            Assert.Contains("DrawingService.CheckPathAllowed(ctx, path, DrawingService.AttachmentFolder(), tool)", s);
            Assert.Contains("FileShare.ReadWrite | FileShare.Delete", s); // the header is read without locking either
            foreach (var bad in new[] { ".Save(", "SaveAs(", "OpenForReadAndWriteNoShare", "OpenForReadAndReadShare", "OpenMode.ForWrite", "UpgradeOpen" })
                Assert.DoesNotContain(bad, s);
            // the header check comes before AutoCAD reads the file
            Assert.True(s.IndexOf("code = RequireReadableHeader(full, ours, tool)", StringComparison.Ordinal) < s.IndexOf("db.ReadDwgFile(", StringComparison.Ordinal));
            Assert.Contains("OtherDrawingLogic.HeaderRefusal(full, code, ours, tool)", s); // test tổng thể đợt H: the check is in RequireReadableHeader
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs");
            Assert.Contains("internal static void CheckPathAllowed(ToolContext ctx, string path, string chatDir, string tool)", t);
            Assert.Contains("Nâng cấp đợt H3: đọc bản vẽ khác chỉ để đọc", Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs"));
        }
    }
}
