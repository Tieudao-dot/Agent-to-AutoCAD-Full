using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test tổng thể đợt H phần 1 (26/11/2026, docs/ban-giao/test-tong-the-dot-h-phan-1.md): the cross checks of H1 → H5 with
    /// each other and with the older parts.
    /// </summary>
    public class TestTongTheDotHTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly string[] NewTools = { "export_drawing", "compare_drawings", "company_standard", "batch_files" };

        // ------------------------------------------------------------ TTH-1: one header check for every reader of another drawing

        [Fact]
        public void Batch_setup_checks_the_header_like_the_shared_reader_before_its_backup()
        {
            var od = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "OtherDrawing.cs");
            Assert.Contains("internal static string RequireReadableHeader(string full, string ours, string tool)", od);
            Assert.Contains("if (kind == \"dwg\") code = RequireReadableHeader(full, ours, tool);", od);
            var b = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "BatchFilesTools.cs");
            int fn = b.IndexOf("private static void BatchStandards(", StringComparison.Ordinal);
            int head = b.IndexOf("OtherDrawing.RequireReadableHeader(", fn, StringComparison.Ordinal);
            int backup = b.IndexOf("BatchLogic.BackupPath(", fn, StringComparison.Ordinal);
            int read = b.IndexOf("db.ReadDwgFile(", fn, StringComparison.Ordinal);
            Assert.True(head > fn && head < backup && backup < read, "header check, then backup, then read");
            Assert.Contains("OtherDrawingLogic.ReadFailure(file, ex.ErrorStatus.ToString(), code, ours, tool)", b);
            // the clear reason of a newer drawing (not "AutoCAD: <status>")
            Assert.Contains("newer AutoCAD", OtherDrawingLogic.HeaderRefusal(@"C:\du an\a.dwg", "AC1036", "AC1032", "batch_files"));
        }

        // ------------------------------------------------------------ TTH-2: file tools keep the WCS

        [Fact]
        public void The_file_tools_of_h_never_turn_numbers_into_the_ucs()
        {
            foreach (var t in NewTools)
                Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf(t, new Dictionary<string, object>()));
            // trace_image stays Ref (its points are UCS points, G5 / F1)
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("trace_image", new Dictionary<string, object>()));
        }

        // ------------------------------------------------------------ across H1 → H5

        [Fact]
        public void Every_new_tool_is_labelled_planned_and_the_ones_writing_files_ask_the_card()
        {
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            var conf = Src("agent", "src", "agent", "confirmation.ts");
            foreach (var t in NewTools)
            {
                Assert.Contains($"[\"{t}\"]", labels);
                Assert.Contains($"'{t}'", conf);
                Assert.NotNull(PlanLogic.GroupOf(t));
            }
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("export_drawing"));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("compare_drawings"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("company_standard"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("batch_files"));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("trace_image"));
        }

        [Fact]
        public void Every_reader_of_another_drawing_goes_through_the_shared_reader()
        {
            foreach (var f in new[] { "CompareTools.cs", "CompanyStandardTools.cs", "BatchFilesTools.cs" })
                Assert.Contains("OtherDrawing.Open(ctx,", Src("plugin", "ClaudeCAD.Plugin", "Drawing", f));
            // only the shared reader and the batch writer (which writes the file back) call ReadDwgFile themselves
            var drawing = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin", "ClaudeCAD.Plugin", "Drawing"));
            var readers = Directory.GetFiles(drawing, "*.cs").Where(f => File.ReadAllText(f).Contains(".ReadDwgFile(")).Select(Path.GetFileName).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "BatchFilesTools.cs", "OtherDrawing.cs" }, readers);
        }

        [Fact]
        public void A_template_dwt_is_read_by_compare_and_company_but_batch_writes_only_dwg()
        {
            Assert.Equal("dwg", OtherDrawingLogic.KindOf(@"C:\mau\A.dwt", true, 100, "compare_drawings"));
            Assert.Contains("ext == \".dwg\" || (ext == \".dxf\"", Src("plugin", "ClaudeCAD.Plugin", "Core", "BatchFiles.cs"));
        }
    }
}
