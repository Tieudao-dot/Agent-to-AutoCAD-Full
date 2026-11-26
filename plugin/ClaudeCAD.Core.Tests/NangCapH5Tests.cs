using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H5 (docs/ban-giao/nang-cap-h5.md): batch_files — tasks, the list of files, skips, token, backups, summary, wiring.</summary>
    public class NangCapH5Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly string R = Path.Combine(Path.GetTempPath(), "duan");

        /// <summary>A fake folder tree: R (a.dwg, b.dxf, ghi-chu.txt), R/tang1 (c.dwg), R/_sao-luu-claudecad (old.dwg), R/tang1/sau (d.dwg).</summary>
        private static List<string> Collect(string task, bool recursive, out int more, int extraFiles = 0)
        {
            string P(params string[] x) => Path.Combine(new[] { R }.Concat(x).ToArray());
            var files = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [R] = new[] { P("b.dxf"), P("a.dwg"), P("ghi-chu.txt") }.Concat(Enumerable.Range(0, extraFiles).Select(i => P($"x{i:000}.dwg"))).ToArray(),
                [P("tang1")] = new[] { P("tang1", "c.dwg") },
                [P("tang1", "sau")] = new[] { P("tang1", "sau", "d.dwg") },
                [P(H5Numbers.BackupFolder)] = new[] { P(H5Numbers.BackupFolder, "old.dwg") },
            };
            var dirs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [R] = new[] { P("tang1"), P(H5Numbers.BackupFolder) },
                [P("tang1")] = new[] { P("tang1", "sau") },
            };
            return BatchLogic.Collect(task, R, null, recursive, d => files.TryGetValue(d, out var f) ? f : new string[0], d => dirs.TryGetValue(d, out var x) ? x : new string[0], out more);
        }

        [Fact]
        public void Tasks_and_the_tool_each_needs()
        {
            Assert.Equal(new[] { "check", "blocks", "standards", "pdf" }, BatchLogic.Tasks);
            Assert.Equal("check_drawing", BatchLogic.SubTool("check"));
            Assert.Equal("block_schedule", BatchLogic.SubTool("blocks"));
            Assert.Equal("setup_standards", BatchLogic.SubTool("standards"));
            Assert.Equal("plot_pdf", BatchLogic.SubTool("pdf"));
            Assert.True(BatchLogic.Writes("standards"));
            Assert.False(BatchLogic.Writes("check") || BatchLogic.Writes("blocks") || BatchLogic.Writes("pdf"));
            Assert.Throws<ToolException>(() => BatchLogic.TaskOf("erase"));
            Assert.Equal("pdf", BatchLogic.TaskOf(" PDF "));
        }

        [Fact]
        public void The_folder_its_sub_folders_never_the_backups_dxf_only_to_read()
        {
            Assert.Equal(new[] { Path.Combine(R, "a.dwg"), Path.Combine(R, "b.dxf") }, Collect("check", false, out _));
            var deep = Collect("check", true, out _);
            Assert.Equal(4, deep.Count);
            Assert.DoesNotContain(deep, f => f.Contains(H5Numbers.BackupFolder));
            Assert.DoesNotContain(Collect("standards", true, out _), f => f.EndsWith(".dxf"));
            Assert.DoesNotContain(Collect("pdf", true, out _), f => f.EndsWith(".dxf"));
        }

        [Fact]
        public void At_most_MaxFiles_and_the_rest_counted()
        {
            var list = Collect("check", false, out int more, H5Numbers.MaxFiles + 10);
            Assert.Equal(H5Numbers.MaxFiles, list.Count);
            Assert.Equal(12, more); // a.dwg, b.dxf + MaxFiles + 10 x-files
            Assert.Throws<ToolException>(() => BatchLogic.Collect("check", null, null, false, d => new string[0], d => new string[0], out _));
            Assert.Throws<ToolException>(() => BatchLogic.Collect("standards", null, new[] { @"C:\a.dxf" }, false, d => new string[0], d => new string[0], out _));
            Assert.Single(BatchLogic.Collect("check", null, new[] { @"C:\a.dwg", @"c:\A.DWG" }, false, d => new string[0], d => new string[0], out _));
        }

        [Fact]
        public void Skips_before_anything_runs()
        {
            Assert.Null(BatchLogic.SkipReason("check", true, 100, true, true, true, false)); // reading: read-only / open elsewhere is fine
            Assert.Contains("read-only", BatchLogic.SkipReason("standards", true, 100, true, false, false, false));
            Assert.Contains(".dwl", BatchLogic.SkipReason("standards", true, 100, false, true, false, false));
            Assert.Contains("open in this AutoCAD", BatchLogic.SkipReason("standards", true, 100, false, false, true, false));
            Assert.Contains("unsaved", BatchLogic.SkipReason("pdf", true, 100, false, false, true, true));
            Assert.Null(BatchLogic.SkipReason("pdf", true, 100, false, false, true, false));
            Assert.Contains("does not exist", BatchLogic.SkipReason("check", false, 0, false, false, false, false));
            Assert.Contains("MB", BatchLogic.SkipReason("check", true, (long)(OtherDrawingNumbers.MaxFileMb + 1) * 1024 * 1024, false, false, false, false));
        }

        [Fact]
        public void The_card_token_is_the_run_task_settings_and_files()
        {
            var f = new[] { @"C:\a.dwg", @"C:\b.dwg" };
            var t = BatchLogic.Token("standards", "scale=100", f);
            Assert.Equal(t, BatchLogic.Token("standards", "scale=100", new[] { @"c:\B.DWG", @"C:\a.dwg" }));
            Assert.NotEqual(t, BatchLogic.Token("standards", "scale=50", f));
            Assert.NotEqual(t, BatchLogic.Token("check", "scale=100", f));
            Assert.NotEqual(t, BatchLogic.Token("standards", "scale=100", new[] { @"C:\a.dwg" }));
            Assert.Matches("^[0-9A-F]{16}$", t);
            Assert.Equal("company=ABC;scale=100", BatchLogic.Settings(new Dictionary<string, object> { ["scale"] = 100, ["company"] = "ABC", ["index"] = 3 }, "company", "scale"));
            Assert.Equal("groups=a,b", BatchLogic.Settings(new Dictionary<string, object> { ["groups"] = new List<object> { "a", "b" } }, "groups"));
        }

        [Fact]
        public void Backups_keep_the_folder_tree_under_the_stamp()
        {
            Assert.Equal("20261126-093000", BatchLogic.Stamp(new DateTime(2026, 11, 26, 9, 30, 0)));
            Assert.True(BatchLogic.ValidStamp("20261126-093000"));
            Assert.False(BatchLogic.ValidStamp("..\\..\\x") || BatchLogic.ValidStamp(null) || BatchLogic.ValidStamp("2026-11-26"));
            Assert.Equal(Path.Combine(R, H5Numbers.BackupFolder, "20261126-093000", "tang1", "c.dwg"),
                BatchLogic.BackupPath(R, "20261126-093000", Path.Combine(R, "tang1", "c.dwg")));
            var other = Path.Combine(Path.GetTempPath(), "khac", "e.dwg");
            Assert.Equal(Path.Combine(Path.GetTempPath(), "khac", H5Numbers.BackupFolder, "20261126-093000", "e.dwg"), BatchLogic.BackupPath(null, "20261126-093000", other));
            Assert.Equal(Path.Combine(R, "a.pdf"), BatchLogic.PdfOf(Path.Combine(R, "a.dwg")));
            Assert.Equal(Path.Combine(R, "tong-hop-check-20261126-093000.xlsx"), BatchLogic.SummaryPath(R, "check", "20261126-093000"));
            Assert.Equal(Path.Combine("tang1", "c.dwg"), BatchLogic.Show(R, Path.Combine(R, "tang1", "c.dwg")));
        }

        [Fact]
        public void The_summary_counts_each_file_once()
        {
            var rows = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["file"] = "a.dwg", ["status"] = "ok", ["note"] = "Không thấy lỗi", ["nen_sua"] = 0, ["can_nhac"] = 1, ["goi_y"] = 2 },
                new Dictionary<string, object> { ["file"] = "b.dwg", ["status"] = "error", ["reason"] = "eBadDwgHeader" },
                new Dictionary<string, object> { ["file"] = "c.dwg", ["status"] = "skipped", ["reason"] = "the file is read-only" },
            };
            var t = BatchLogic.Totals(rows);
            Assert.Equal(1, t["ok"]); Assert.Equal(1, t["error"]); Assert.Equal(1, t["skipped"]);
            var s = BatchLogic.SummaryRows("check", rows);
            Assert.Equal(new object[] { "File", "Kết quả", "Ghi chú", "Nên sửa", "Cân nhắc", "Gợi ý" }, s[0]);
            Assert.Equal("Lỗi", s[2][1]);
            Assert.Equal("eBadDwgHeader", s[2][2]);
            Assert.Equal("1 xong, 1 lỗi, 1 bỏ qua", s[4][1]);
            var b = BatchLogic.BlockRows(new List<Dictionary<string, object>> { new Dictionary<string, object> { ["file"] = "a.dwg", ["counts"] = new Dictionary<string, object> { ["CUA-DI"] = 3, ["BAN"] = 1 } } });
            Assert.Equal(3, b.Count);
            Assert.Equal(new object[] { "a.dwg", "BAN", 1.0 }, b[1]);
        }

        [Fact]
        public void Wiring_plan_mode_view_only_macro_and_the_atomic_write()
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("batch_files"));
            Assert.False(PlanLogic.Allowed(PlanLogic.Plan3D, "batch_files"));
            Assert.False(MacroLogic.ShouldRecord("batch_files", false));
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs");
            Assert.Contains("d.Register(new ToolDef(\"batch_files\", false, BatchFiles, noTransaction: true));", t);
            Assert.Contains("ctx.Dispatcher?.RequireToolInPlan(BatchLogic.SubTool(b.Task))", t);
            Assert.Contains("BatchLogic.Writes(b.Task) && ctx.Dispatcher?.ViewOnly == true", t);
            Assert.Contains("RequireCard(ctx, p, tool, what, new[] { b.Token });", t);
            Assert.Contains("OtherDrawing.Open(ctx, file, tool)", t); // reading through the shared reader of H3
            // writing: backup first, then a temporary file swapped in (never a half-written drawing), same DWG version
            int backup = t.IndexOf("File.Copy(file, backup, false)", StringComparison.Ordinal);
            int save = t.IndexOf("db.SaveAs(temp, version)", StringComparison.Ordinal);
            int swap = t.IndexOf("File.Replace(temp, file, null)", StringComparison.Ordinal);
            Assert.True(backup > 0 && save > backup && swap > save);
            Assert.Contains("RegisterBatchFiles(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            Assert.Contains("[\"batch_files\"] = \"Làm nhiều file\"", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            Assert.Contains("internal void RequireToolInPlan(string subTool)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs"));
        }
    }
}
