using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt F4: the Excel writer at its edges, and macros never writing the Excel file again.</summary>
    public class NangCapF4TestSuaTests
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static Dictionary<string, string> Unzip(byte[] xlsx)
        {
            var d = new Dictionary<string, string>();
            using (var z = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
                foreach (var e in z.Entries)
                    using (var r = new StreamReader(e.Open(), Encoding.UTF8)) d[e.FullName] = r.ReadToEnd();
            return d;
        }

        private static List<XElement> Cells(IList<IList<object>> rows, string sheet = "Bảng")
        {
            var files = Unzip(XlsxWriter.Build(rows, sheet));
            XDocument.Parse(files["xl/workbook.xml"]);
            return XDocument.Parse(files["xl/worksheets/sheet1.xml"]).Descendants(S + "c").ToList();
        }

        private static IList<IList<object>> Rows(params object[] body) =>
            new List<IList<object>> { new List<object> { "Tên" }, new List<object>(body) };

        // ------------------------------------------------------------ NF4-3: characters XML refuses

        [Fact]
        public void Odd_characters_never_break_the_file()
        {
            string nasty = "A&B <C> \"D\" 'E' \u0001\u0007\u000B￾￿" + "\uD800" + "x" + "\uDC00" + "y 😀 Đường";
            var cells = Cells(Rows(nasty), "Sheet [1]: a/b\\c?*'");
            var c = cells.Single(x => (string)x.Attribute("r") == "A2");
            Assert.Equal("inlineStr", (string)c.Attribute("t"));
            Assert.Equal("A&B <C> \"D\" 'E' xy 😀 Đường", c.Value); // the emoji (a whole pair) and Vietnamese kept
            Assert.Equal("", XlsxWriter.Esc("𐀀".Substring(0, 1)));
            Assert.Equal("😀", XlsxWriter.Esc("😀"));
        }

        // ------------------------------------------------------------ formulas never

        [Theory]
        [InlineData("=SUM(A1:A9)")]
        [InlineData("+84 912")]
        [InlineData("-cửa")]
        [InlineData("@HYPERLINK(\"x\")")]
        [InlineData("=1+1")]
        public void Text_that_looks_like_a_formula_stays_text(string s)
        {
            var c = Cells(Rows(s)).Single(x => (string)x.Attribute("r") == "A2");
            Assert.Equal("inlineStr", (string)c.Attribute("t"));
            Assert.Empty(c.Descendants(S + "f"));
            Assert.Equal(s, c.Value);
        }

        // ------------------------------------------------------------ numbers on a Vietnamese machine

        [Fact]
        public void Numbers_are_written_with_a_dot_whatever_the_machine_culture()
        {
            var before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                var c = Cells(Rows(1234.5, "12.25", 0.000125)).ToDictionary(x => (string)x.Attribute("r"));
                Assert.Equal("1234.5", c["A2"].Element(S + "v").Value);
                Assert.Equal("12.25", c["B2"].Element(S + "v").Value);
                Assert.Equal(0.000125, double.Parse(c["C2"].Element(S + "v").Value, CultureInfo.InvariantCulture), 12);
                // "1,5" (a Vietnamese decimal typed as text) stays text: never read as 15
                Assert.IsType<string>(XlsxWriter.CellValue("1,5"));
                // the column widths are written invariant too
                var sheet = Unzip(XlsxWriter.Build(Rows(1234.5), "x"))["xl/worksheets/sheet1.xml"];
                Assert.DoesNotContain("width=\"8,", sheet);
            }
            finally { Thread.CurrentThread.CurrentCulture = before; }
        }

        // ------------------------------------------------------------ sheet names

        [Theory]
        [InlineData("Khối lượng 3D theo layer của tòa nhà A", 31)]
        [InlineData("[]:*?/\\", 4)]
        [InlineData("'quoted'", 6)]
        [InlineData("", 4)]
        public void Sheet_names_are_what_excel_accepts(string name, int len)
        {
            var s = XlsxWriter.SafeSheetName(name);
            Assert.Equal(len, s.Length);
            Assert.True(s.IndexOfAny("[]:*?/\\".ToCharArray()) < 0);
            Assert.False(s.StartsWith("'") || s.EndsWith("'"));
        }

        // ------------------------------------------------------------ NF4-2: macros

        [Fact]
        public void A_recorded_schedule_keeps_its_table_but_not_its_excel_file()
        {
            var rec = new MacroRecorder();
            foreach (var t in new[] { "block_schedule", "area_schedule", "volume_schedule", "check_interference" })
                rec.Record(t, new Dictionary<string, object> { ["table_position"] = new List<object> { 0.0, 0.0 }, ["excel"] = true, ["overwrite"] = true }, null);
            // another tool's own 'overwrite' (define_block) is kept
            rec.Record("define_block", new Dictionary<string, object> { ["name"] = "CUA", ["overwrite"] = true }, null);
            var calls = rec.Take();
            Assert.Equal(5, calls.Count);
            foreach (var c in calls.Take(4))
            {
                Assert.False(c.Params.ContainsKey("excel"), c.Tool);
                Assert.False(c.Params.ContainsKey("overwrite"), c.Tool);
                Assert.True(c.Params.ContainsKey("table_position"), c.Tool);
            }
            Assert.True(calls[4].Params.ContainsKey("overwrite"));
            Assert.Equal(new[] { "block_schedule", "area_schedule", "volume_schedule", "check_interference" }, MacroLogic.ExcelTools.OrderBy(x => x == "block_schedule" ? 0 : x == "area_schedule" ? 1 : x == "volume_schedule" ? 2 : 3));
        }

        // ------------------------------------------------------------ file rules

        [Fact]
        public void A_failed_write_leaves_no_temporary_file()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ccad-f4t-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(dir);
            try
            {
                // the target is a folder: the move fails, the temporary copy is cleaned up
                var target = Path.Combine(dir, "a.xlsx");
                Directory.CreateDirectory(target);
                Assert.ThrowsAny<Exception>(() => XlsxWriter.Write(target, Rows("x"), "x"));
                Assert.Empty(Directory.GetFiles(dir));
                Assert.Contains("is a folder", Assert.Throws<ToolException>(() => XlsxLogic.Resolve("a.xlsx", dir, "Nha.dwg", "block_schedule", Directory.Exists, Directory.Exists)).Message);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
