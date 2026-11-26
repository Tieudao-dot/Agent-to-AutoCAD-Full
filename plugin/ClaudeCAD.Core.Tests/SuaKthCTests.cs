using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa KTH (kiểm tra độc lập đợt H phần 1), phần H5: docs/ban-giao/nang-cap-h-sua-kth-c.md.</summary>
    public class SuaKthCTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly string T = Path.GetTempPath();
        private const string S = "20261126-110000";

        [Fact]
        public void KTH03_two_files_of_one_name_outside_the_folder_get_two_backups()
        {
            string root = Path.Combine(T, "goc"), a = Path.Combine(T, "A", "mat-bang.dwg"), b = Path.Combine(T, "B", "mat-bang.dwg");
            var ba = BatchLogic.BackupPath(root, S, a);
            var bb = BatchLogic.BackupPath(root, S, b);
            Assert.NotEqual(ba, bb);
            Assert.Equal(Path.Combine(T, "A", H5Numbers.BackupFolder, S, "mat-bang.dwg"), ba);
            // inside the folder: the tree under it
            Assert.Equal(Path.Combine(root, H5Numbers.BackupFolder, S, "tang1", "c.dwg"), BatchLogic.BackupPath(root, S, Path.Combine(root, "tang1", "c.dwg")));
            // a folder whose name starts like the root is not inside it
            Assert.Equal(Path.Combine(T, "goc-2", H5Numbers.BackupFolder, S, "d.dwg"), BatchLogic.BackupPath(root, S, Path.Combine(T, "goc-2", "d.dwg")));
        }

        [Fact]
        public void KTH06_the_backup_folders_of_a_run()
        {
            string root = Path.Combine(T, "goc");
            Assert.Single(BatchLogic.BackupFolders(root, new[] { Path.Combine(root, "a.dwg"), Path.Combine(root, "x", "b.dwg") }, S));
            Assert.Equal(2, BatchLogic.BackupFolders(null, new[] { Path.Combine(T, "A", "a.dwg"), Path.Combine(T, "B", "b.dwg") }, S).Count);
            Assert.Contains("AddBackupFolders(r, b, (string)r[\"stamp\"])", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs"));
        }

        [Fact]
        public void KTH04_05_07_and_macros_in_the_tool()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs");
            // 04: the block count reads every object of model space (no ModelEntities limit)
            var blocks = t.Substring(t.IndexOf("private static void BatchBlocks", StringComparison.Ordinal), 1500);
            Assert.DoesNotContain("G2Numbers.MaxScan", blocks);
            Assert.Contains("foreach (ObjectId id in ms)", blocks);
            // 05: groups and scale are checked in the plan, before the card
            var plan = t.Substring(t.IndexOf("private static BatchPlan PlanOf", StringComparison.Ordinal), 3500);
            Assert.Contains("DrawingCheckLogic.Find(g) == null", plan);
            Assert.Contains("StandardsLogic.ScaleDenominator(p.Get(\"scale\"), tool)", plan);
            Assert.Throws<ToolException>(() => StandardsLogic.ScaleDenominator("abc", "batch_files"));
            Assert.Throws<ToolException>(() => StandardsLogic.ScaleDenominator("2:1", "batch_files"));
            // 07: a folder that does not exist is refused, also with files
            Assert.Contains("if (folder != null && !Directory.Exists(folder))", plan);
            // macro: never run from a macro (its card cannot be answered)
            Assert.Contains("ctx.Dispatcher?.MacroRunning == true", plan);
        }

        [Fact]
        public void KTH08_the_checklists_say_what_the_code_does()
        {
            foreach (var f in new[] { "checklist-dot-h5.md", "checklist-tong-the.md" })
                Assert.Contains("ở tab đang xem", Src("docs", f));
        }
    }
}
