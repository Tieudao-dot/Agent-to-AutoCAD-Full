using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F4 (25/11/2026, docs/ban-giao/nang-cap-f4.md): Excel export of the schedules, prewarm wiring.</summary>
    public class NangCapF4Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static Dictionary<string, string> Unzip(byte[] xlsx)
        {
            var d = new Dictionary<string, string>();
            using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
                foreach (var e in zip.Entries)
                    using (var r = new StreamReader(e.Open(), Encoding.UTF8)) d[e.FullName] = r.ReadToEnd();
            return d;
        }

        private static List<IList<object>> AreaRows() => new List<IList<object>>
        {
            new List<object> { "STT", "Tên phòng", "Layer", "Diện tích (m²)" },
            new List<object> { "1", "PHÒNG NGỦ 1", "A-ROOM", "12.50" },
            new List<object> { "2", "Bếp & ăn <chung>", "A-ROOM", "18.25" },
            new List<object> { "", "Tổng", "", "30.75" },
        };

        // ------------------------------------------------------------ cells

        [Theory]
        [InlineData("12", 12.0)]
        [InlineData("-3.25", -3.25)]
        [InlineData("0.5", 0.5)]
        [InlineData(" 7 ", 7.0)]
        public void Plain_numbers_become_numbers(string s, double want) => Assert.Equal(want, (double)XlsxWriter.CellValue(s), 9);

        [Theory]
        [InlineData("007")]
        [InlineData("1,5")]
        [InlineData("1e5")]
        [InlineData("CUA-DI-900")]
        [InlineData("12 m²")]
        [InlineData("")]
        public void Codes_and_text_stay_text(string s) => Assert.IsType<string>(XlsxWriter.CellValue(s));

        [Fact]
        public void Typed_values()
        {
            Assert.Equal(3.0, XlsxWriter.CellValue(3));
            Assert.Equal(2.5, XlsxWriter.CellValue(2.5));
            Assert.Equal("", XlsxWriter.CellValue(double.NaN));
            Assert.Equal("", XlsxWriter.CellValue(null));
            Assert.Equal("có", XlsxWriter.CellValue(true));
        }

        [Fact]
        public void Columns_and_sheet_names()
        {
            Assert.Equal("A", XlsxWriter.Column(0));
            Assert.Equal("Z", XlsxWriter.Column(25));
            Assert.Equal("AA", XlsxWriter.Column(26));
            Assert.Equal("BA", XlsxWriter.Column(52));
            Assert.Equal("Diện tích", XlsxWriter.SafeSheetName("Diện tích"));
            Assert.Equal("ab", XlsxWriter.SafeSheetName("a[b]:*?/\\"));
            Assert.Equal("Bang", XlsxWriter.SafeSheetName("  "));
            Assert.Equal(F4Numbers.SheetNameMax, XlsxWriter.SafeSheetName(new string('x', 40)).Length);
            foreach (var kv in F4Numbers.SheetName) Assert.True(kv.Value.Length <= F4Numbers.SheetNameMax, kv.Key);
        }

        [Fact]
        public void Column_widths_fit_the_text_within_limits()
        {
            var w = XlsxWriter.ColumnWidths(new List<IList<object>> { new List<object> { "a", new string('x', 200), "12345" } });
            Assert.Equal(F4Numbers.ColumnMinWidth, w[0]);
            Assert.Equal(F4Numbers.ColumnMaxWidth, w[1]);
            Assert.Equal(5 + F4Numbers.ColumnPadding, w[2]);
        }

        // ------------------------------------------------------------ the file

        [Fact]
        public void The_file_is_a_valid_xlsx_with_vietnamese_text_and_numbers_that_add_up()
        {
            var files = Unzip(XlsxWriter.Build(AreaRows(), "Diện tích"));
            foreach (var part in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml" })
            {
                Assert.True(files.ContainsKey(part), part);
                XDocument.Parse(files[part]); // well-formed XML
            }
            Assert.Contains("name=\"Diện tích\"", files["xl/workbook.xml"]);
            var sheet = XDocument.Parse(files["xl/worksheets/sheet1.xml"]);
            var cells = sheet.Descendants(S + "c").ToList();
            XElement Cell(string r) => cells.Single(c => (string)c.Attribute("r") == r);
            // text: inline strings, Vietnamese and & < > kept
            Assert.Equal("inlineStr", (string)Cell("B2").Attribute("t"));
            Assert.Equal("PHÒNG NGỦ 1", Cell("B2").Value);
            Assert.Equal("Bếp & ăn <chung>", Cell("B3").Value);
            // numbers: no t attribute, a <v> Excel can add up
            Assert.Null(Cell("D2").Attribute("t"));
            Assert.Equal(12.5, double.Parse(Cell("D2").Element(S + "v").Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1.0, double.Parse(Cell("A2").Element(S + "v").Value, System.Globalization.CultureInfo.InvariantCulture));
            // header bold (style 1), body normal
            Assert.Equal("1", (string)Cell("A1").Attribute("s"));
            Assert.Null(Cell("A2").Attribute("s"));
            // empty body cells are left out
            Assert.DoesNotContain(cells, c => (string)c.Attribute("r") == "A5");
            Assert.Equal(4, sheet.Descendants(S + "col").Count());
        }

        [Fact]
        public void Writing_goes_through_a_temporary_file_and_replaces_only_when_asked_by_the_caller()
        {
            var dir = Path.Combine(Path.GetTempPath(), "claudecad-f4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "a.xlsx");
                XlsxWriter.Write(path, AreaRows(), "x");
                Assert.True(File.Exists(path));
                long first = new FileInfo(path).Length;
                XlsxWriter.Write(path, AreaRows().Take(2).ToList(), "x");
                Assert.NotEqual(first, new FileInfo(path).Length);
                Assert.Single(Directory.GetFiles(dir)); // no temporary file left
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void A_runaway_list_is_refused()
        {
            var rows = Enumerable.Range(0, F4Numbers.MaxRows + 1).Select(i => (IList<object>)new List<object> { i }).ToList();
            Assert.Throws<ToolException>(() => XlsxWriter.Build(rows, "x"));
        }

        // ------------------------------------------------------------ where the file goes

        [Fact]
        public void Excel_parameter_true_or_a_name()
        {
            Assert.Null(XlsxLogic.Requested(null, "t"));
            Assert.Null(XlsxLogic.Requested(false, "t"));
            Assert.Equal("", XlsxLogic.Requested(true, "t"));
            Assert.Equal("bang.xlsx", XlsxLogic.Requested(" bang.xlsx ", "t"));
            Assert.Throws<ToolException>(() => XlsxLogic.Requested(5.0, "t"));
            Assert.Throws<ToolException>(() => XlsxLogic.Requested("  ", "t"));
        }

        [Theory]
        [InlineData("block_schedule", "Nha-pho-thong-ke-block.xlsx")]
        [InlineData("area_schedule", "Nha-pho-dien-tich.xlsx")]
        [InlineData("volume_schedule", "Nha-pho-khoi-luong.xlsx")]
        [InlineData("check_interference", "Nha-pho-va-cham.xlsx")]
        public void Default_names_next_to_the_drawing(string tool, string name)
        {
            Assert.Equal(name, XlsxLogic.DefaultName(@"C:\Du an\Nha-pho.dwg", tool));
            var dir = Path.GetTempPath();
            var full = XlsxLogic.Resolve("", dir, "Nha-pho.dwg", tool, _ => true, _ => false);
            Assert.Equal(Path.Combine(Path.GetFullPath(dir), name), full);
        }

        [Fact]
        public void Bad_names_are_refused_like_export_stl()
        {
            string dir = Path.GetTempPath();
            Assert.Throws<ToolException>(() => XlsxLogic.Resolve("bang.csv", dir, "a.dwg", "t", _ => true, _ => false));
            Assert.Throws<ToolException>(() => XlsxLogic.Resolve(".xlsx", dir, "a.dwg", "t", _ => true, _ => false));
            Assert.Throws<ToolException>(() => XlsxLogic.Resolve("a|b.xlsx", dir, "a.dwg", "t", _ => true, _ => false));
            Assert.Throws<ToolException>(() => XlsxLogic.Resolve("x.xlsx", dir, "a.dwg", "t", _ => false, _ => false)); // missing folder
            Assert.Throws<ToolException>(() => XlsxLogic.Resolve("x.xlsx", dir, "a.dwg", "t", _ => true, _ => true));  // a folder
            Assert.Equal("ban-ve-dien-tich.xlsx", XlsxLogic.DefaultName("", "area_schedule"));
            Assert.Contains("overwrite: true", XlsxLogic.ExistsMessage("t", "x.xlsx"));
            Assert.Contains("Nothing was written", XlsxLogic.ExistsMessage("t", "x.xlsx"));
        }

        // ------------------------------------------------------------ wiring (source)

        [Fact]
        public void The_four_schedules_export_their_own_table()
        {
            var u = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("ExportExcel(ctx, p, \"block_schedule\", () => Util2DLogic.BlockTable(rows, byAttr), res);", u);
            Assert.Contains("ExportExcel(ctx, p, \"area_schedule\", () => Util2DLogic.AreaTable(rows, groupBy, decimals, mmPerUnit), res);", u);
            Assert.Contains("ExportExcel(ctx, p, \"volume_schedule\", () => Solid3Logic.VolumeTable(res, decimals, f, withArea), r);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid3Tools.cs"));
            Assert.Contains("ExportExcel(ctx, p, \"check_interference\", () => Solid3Logic.ClashTable(", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Solid2Tools.cs"));
            var x = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ExcelExport.cs");
            // nâng cấp đợt F5: an existing file needs the card; a macro replay keeps the old overwrite parameter
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, File.Exists(full), XlsxLogic.ExistsMessage(tool, full), \"overwrite\");", x);
            Assert.Contains("XlsxLogic.Resolve(requested, drawingDir", x);
            // the schedules stay read tools: the Excel file is not a change of the drawing (like export_stl)
            Assert.Contains("readsWhen: p => !p.Has(\"table_position\")", u);
        }

        [Fact]
        public void The_plan_of_the_schedule_decides_and_the_3d_plan_can_export()
        {
            Assert.True(PlanLogic.Allowed("3d", "volume_schedule"));
            Assert.True(PlanLogic.Allowed("3d", "check_interference"));
            Assert.False(PlanLogic.Allowed("3d", "area_schedule"));
            var p = new Dictionary<string, object> { ["excel"] = true };
            Assert.Null(PlanLogic.BlockedParam("3d", "volume_schedule", p));
            Assert.Null(PlanLogic.BlockedParam("3d", "check_interference", p));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("volume_schedule", p));
        }

        [Fact]
        public void The_palette_prewarms_after_ready_mode_and_plan_changes()
        {
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("SchedulePrewarm(); // F4: the assistant (re)started", c);
            Assert.Contains("if (!_busy) SchedulePrewarm(); // F4: the next message finds Claude ready with the new mode", c);
            Assert.Contains("if (_busy || !Activation.IsAllowed) return;", c);
            Assert.Contains("[\"type\"] = \"prewarm\"", c);
            Assert.Contains("foreach (var kv in PlanContext(servicePlan, limitingMode)) ctx[kv.Key] = kv.Value;", c);
            Assert.Contains("internal const int PrewarmDelayMs = 3000;", c);
        }

        [Fact]
        public void Roof_layer_color_has_a_name()
        {
            Assert.Equal(30, RoofLogic.Layer3dColor);
            Assert.Contains("EnsureLayer(ctx, layer, RoofLogic.Layer3dColor)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StairRoofTools.cs"));
        }
    }
}
