using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt H5 (docs/ban-giao/nang-cap-h5-test.md): the bugs found in the review of batch_files.</summary>
    [Collection("BatchDocSwitches")]
    public class NangCapH5TestSuaTests : IDisposable
    {
        public NangCapH5TestSuaTests() { BatchDocSwitches.ClearForTests(); BatchDocSwitches.Now = () => _now; }
        public void Dispose() { BatchDocSwitches.ClearForTests(); BatchDocSwitches.Now = () => DateTime.UtcNow; }

        private DateTime _now = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        // ------------------------------------------------------------ H5T-01: a PDF run is not stopped by its own drawing switches

        [Fact]
        public void The_drawings_the_run_brings_to_the_front_are_known_as_the_runs_for_a_short_time()
        {
            var doc = new object();
            var users = new object();
            Assert.False(BatchDocSwitches.IsRuns(doc));
            BatchDocSwitches.MarkPath(@"D:\Dự án\Nhà phố\KT-01.dwg");
            Assert.True(BatchDocSwitches.IsRuns(null, @"d:/dự án/nhà phố/KT-01.DWG"), "the file being opened, before its drawing is known");
            BatchDocSwitches.Mark(doc);
            Assert.True(BatchDocSwitches.IsRuns(doc));
            Assert.False(BatchDocSwitches.IsRuns(users, @"D:\Dự án\Nhà phố\KT-02.dwg"), "the user's own drawing is never the run's");
            _now = _now.AddSeconds(BatchDocSwitches.KeepSeconds + 1);
            Assert.False(BatchDocSwitches.IsRuns(doc), "after a while a switch to it is the user's again");
            Assert.False(BatchDocSwitches.IsRuns(null, @"D:\Dự án\Nhà phố\KT-01.dwg"));
            BatchDocSwitches.Mark(doc); // marked again when its file is done: the time restarts
            Assert.True(BatchDocSwitches.IsRuns(doc));
            Assert.False(BatchDocSwitches.IsRuns(null, null));
        }

        [Fact]
        public void The_chat_ignores_those_switches_only_during_a_turn_and_the_pdf_task_marks_them()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            int fn = c.IndexOf("private void OnDocumentActivatedCore(", StringComparison.Ordinal);
            int ignore = c.IndexOf("if (_busy && e.Document != null && BatchDocSwitches.IsRuns(e.Document, DocPath(e.Document))) return;", fn, StringComparison.Ordinal);
            int cancel = c.IndexOf("The user switched to another drawing during this turn", fn, StringComparison.Ordinal);
            Assert.True(fn > 0 && ignore > fn && cancel > ignore, "checked before the turn is stopped for a drawing switch");
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs");
            int mark = t.IndexOf("BatchDocSwitches.MarkPath(file);", StringComparison.Ordinal);
            int open = t.IndexOf("DocumentCollectionExtension.Open(dm, file, true)", StringComparison.Ordinal);
            int markDoc = t.IndexOf("BatchDocSwitches.Mark(doc);", StringComparison.Ordinal);
            int active = t.IndexOf("dm.MdiActiveDocument = doc;", StringComparison.Ordinal);
            Assert.True(mark > 0 && open > mark && markDoc > open && active > markDoc, "the file is marked before it is opened, the drawing before it is made active");
        }

        // ------------------------------------------------------------ H5T-02: a temporary copy is never taken for a drawing

        [Fact]
        public void A_temporary_copy_left_behind_is_not_collected_but_a_listed_file_is_kept()
        {
            var files = new Dictionary<string, string[]>
            {
                [@"D:\p"] = new[] { @"D:\p\A.dwg", @"D:\p\.claudecad-tam-0a1b2c3d4e.dwg", @"D:\p\.CLAUDECAD-TAM-ffff.DWG", @"D:\p\B.dwg" },
            };
            var got = BatchLogic.Collect("standards", @"D:\p", null, false, d => files.TryGetValue(d, out var f) ? f : new string[0], d => new string[0], out _);
            Assert.Equal(new[] { @"D:\p\A.dwg", @"D:\p\B.dwg" }, got);
            Assert.True(BatchLogic.IsOwnTemp(@"D:\p\.claudecad-tam-x.dwg"));
            Assert.False(BatchLogic.IsOwnTemp(@"D:\p\claudecad-tam.dwg"));
            Assert.False(BatchLogic.IsOwnTemp(null));
            // the swap writes exactly that name
            Assert.Contains("Path.Combine(Path.GetDirectoryName(file), BatchLogic.TempPrefix + Guid", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs"));
        }

        // ------------------------------------------------------------ safety of the user's files (review, kept by tests)

        [Fact]
        public void A_backup_never_replaces_an_earlier_one_and_is_written_before_the_file()
        {
            var a = BatchLogic.BackupPath(@"D:\Dự án", "20261126-100000", @"D:\Dự án\Tầng 1\Mặt bằng.dwg");
            var b = BatchLogic.BackupPath(@"D:\Dự án", "20261126-100001", @"D:\Dự án\Tầng 1\Mặt bằng.dwg");
            Assert.NotEqual(a, b); // two runs: two stamps, two backups
            Assert.Contains(H5Numbers.BackupFolder, a);
            Assert.Contains("20261126-100000", a);
            Assert.EndsWith("Mặt bằng.dwg", a);
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs");
            int copy = t.IndexOf("if (!File.Exists(backup)) File.Copy(file, backup, false);", StringComparison.Ordinal);
            int read = t.IndexOf("db.ReadDwgFile(file", StringComparison.Ordinal);
            int save = t.IndexOf("db.SaveAs(temp, version);", StringComparison.Ordinal);
            int swap = t.IndexOf("File.Replace(temp, file, null);", StringComparison.Ordinal);
            Assert.True(copy > 0 && read > copy && save > read && swap > save, "backup, read, write a temporary copy, then swap");
            // the backup folder is never read as the user's drawings
            var dirs = new Dictionary<string, string[]> { [@"D:\p"] = new[] { @"D:\p\" + H5Numbers.BackupFolder, @"D:\p\Tầng 1" } };
            var fs = new Dictionary<string, string[]>
            {
                [@"D:\p"] = new[] { @"D:\p\A.dwg" },
                [@"D:\p\" + H5Numbers.BackupFolder] = new[] { @"D:\p\" + H5Numbers.BackupFolder + @"\A.dwg" },
                [@"D:\p\Tầng 1"] = new[] { @"D:\p\Tầng 1\B.dwg" },
            };
            var got = BatchLogic.Collect("standards", @"D:\p", null, true, d => fs.TryGetValue(d, out var f) ? f : new string[0], d => dirs.TryGetValue(d, out var x) ? x : new string[0], out _);
            Assert.Equal(new[] { @"D:\p\A.dwg", @"D:\p\Tầng 1\B.dwg" }, got);
        }
    }
}
