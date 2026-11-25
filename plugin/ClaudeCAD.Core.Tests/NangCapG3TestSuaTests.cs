using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt G3 (25/11/2026, docs/ban-giao/nang-cap-g3-test.md): units typed "m3", ambiguous prices reported as
    /// unused, names like numbers in Excel; and the checks of the review (matching, sums, percent lines, Excel formulas).
    /// </summary>
    public class NangCapG3TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static EstimateLogic.Row R(string item, double q, string unit = G3Numbers.UnitVolume) => new EstimateLogic.Row { Item = item, Quantity = q, Unit = unit };
        private static EstimateLogic.Price P(string item, double price, string unit = null) => new EstimateLogic.Price { Item = item, UnitPrice = price, Unit = unit };

        private static XDocument Sheet(IList<IList<object>> rows)
        {
            var bytes = XlsxWriter.Build(rows, G3Numbers.SheetName);
            using (var z = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            using (var s = z.GetEntry("xl/worksheets/sheet1.xml").Open())
                return XDocument.Load(s);
        }

        private static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static XElement Cell(XDocument d, string r) => d.Descendants(X + "c").First(c => (string)c.Attribute("r") == r);

        // ------------------------------------------------------------ TG3-1: "m3" is m³

        [Theory]
        [InlineData("m3")]
        [InlineData("M³")]
        [InlineData("m^3")]
        [InlineData(" m 3 ")]
        public void A_unit_typed_without_superscript_is_the_same_unit(string unit)
        {
            var r = EstimateLogic.Build(new[] { R("A-WALL-3D", 2) }, new[] { P("A-WALL-3D", 1000, unit) }, null);
            Assert.Null(r.Lines[0].Note);
            var other = EstimateLogic.Build(new[] { R("A-WALL-3D", 2) }, new[] { P("A-WALL-3D", 1000, "m2") }, null);
            Assert.Contains("check", other.Lines[0].Note);
            Assert.Equal("m2", EstimateLogic.UnitKey(G3Numbers.UnitArea));
        }

        // ------------------------------------------------------------ TG3-2: an ambiguous price is not "unused"

        [Fact]
        public void Prices_that_fit_a_row_but_disagree_leave_it_empty_and_are_not_called_unused()
        {
            var r = EstimateLogic.Build(new[] { R("CUA-DI-900", 3, G3Numbers.UnitCount) },
                new[] { P("CUA*", 1_000_000), P("CUA-DI*", 2_000_000), P("TU-BEP", 5) }, null);
            Assert.Null(r.Lines[0].UnitPrice);
            Assert.Contains("several price patterns fit", r.Lines[0].Note);
            Assert.Equal(new[] { "CUA-DI-900" }, r.Missing);
            Assert.Equal(new[] { "TU-BEP" }, r.Unused); // before: CUA* and CUA-DI* were listed as unused too
            // the same price twice is not ambiguous
            var same = EstimateLogic.Build(new[] { R("CUA-DI-900", 3) }, new[] { P("CUA*", 7), P("cua di*", 7) }, null);
            Assert.Equal(21, same.Lines[0].Amount);
            Assert.Empty(same.Unused);
        }

        // ------------------------------------------------------------ TG3-3: names like numbers stay text in Excel

        [Fact]
        public void A_room_called_101_stays_text_in_excel_and_the_numbers_stay_numbers()
        {
            var r = EstimateLogic.Build(new[] { R("101", 12.5, G3Numbers.UnitArea), R("007", 3, G3Numbers.UnitArea) }, new[] { P("101", 400000) }, new[] { ("2024", 10.0) });
            var d = Sheet(EstimateLogic.ExcelRows(r, null).Select(x => (IList<object>)x).ToList());
            Assert.Equal("inlineStr", (string)Cell(d, "B2").Attribute("t"));
            Assert.Equal("101", Cell(d, "B2").Value);
            Assert.Equal("007", Cell(d, "B3").Value);
            Assert.Equal("inlineStr", (string)Cell(d, "B5").Attribute("t")); // the percent line named "2024"
            Assert.Null(Cell(d, "D2").Attribute("t")); // quantity: a number
            Assert.Equal("12.5", Cell(d, "D2").Element(X + "v").Value);
            // the F4 tables keep their old rule (a plain string like "12" is a number there)
            var f4 = Sheet(new List<IList<object>> { new List<object> { "STT" }, new List<object> { "12" } });
            Assert.Null(Cell(f4, "A2").Attribute("t"));
        }

        // ------------------------------------------------------------ checked: formulas, sums, percent lines

        [Fact]
        public void Excel_formulas_point_at_the_right_cells_with_missing_prices_and_percent_lines()
        {
            var r = EstimateLogic.Build(new[] { R("A-WALL-3D", 2.9041), R("A-COLS-3D", 1), R("A-SLAB-3D", 0.5) },
                new[] { P("A-WALL*", 1_234_567.5), P("A-SLAB-3D", 0) }, new[] { ("Chi phí chung", 5.0), ("Thuế VAT", 10.0) });
            // the quantity as shown (3 decimals) × price, rounded per line, then summed
            Assert.Equal(EstimateLogic.RoundMoney(2.904 * 1_234_567.5), r.Lines[0].Amount);
            Assert.Null(r.Lines[1].Amount);
            Assert.Equal(0, r.Lines[2].Amount); // a price 0 the user gave is a price
            Assert.Equal(new[] { "A-COLS-3D" }, r.Missing);
            Assert.Equal(r.Lines[0].Amount + 0, r.Subtotal);
            // both percent lines are on the subtotal (not on each other)
            Assert.Equal(EstimateLogic.RoundMoney(r.Subtotal * 0.05), r.Percents[0].Amount);
            Assert.Equal(EstimateLogic.RoundMoney(r.Subtotal * 0.10), r.Percents[1].Amount);
            Assert.Equal(r.Subtotal + r.Percents.Sum(x => x.Amount), r.Total);

            var d = Sheet(EstimateLogic.ExcelRows(r, null).Select(x => (IList<object>)x).ToList());
            Assert.Equal("IF(E2=\"\",\"\",ROUND(D2*E2,0))", Cell(d, "F2").Element(X + "f").Value);
            Assert.Equal("str", (string)Cell(d, "F3").Attribute("t")); // no price: the formula gives ""
            Assert.Equal("SUM(F2:F4)", Cell(d, "F5").Element(X + "f").Value);
            Assert.Equal("ROUND(F5*D6/100,0)", Cell(d, "F6").Element(X + "f").Value);
            Assert.Equal("ROUND(F5*D7/100,0)", Cell(d, "F7").Element(X + "f").Value);
            Assert.Equal("F5+F6+F7", Cell(d, "F8").Element(X + "f").Value);
            Assert.Equal(r.Total, double.Parse(Cell(d, "F8").Element(X + "v").Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("2", (string)Cell(d, "E2").Attribute("s")); // money format
        }

        [Fact]
        public void One_row_huge_and_tiny_numbers()
        {
            var one = EstimateLogic.Build(new[] { R("A", 1) }, new[] { P("a", 0.4) }, null);
            Assert.Equal(0, one.Total);
            Assert.Equal(1, EstimateLogic.Build(new[] { R("A", 1) }, new[] { P("a", 0.5) }, null).Total); // half rounds up
            var d = Sheet(EstimateLogic.ExcelRows(one, null).Select(x => (IList<object>)x).ToList());
            Assert.Equal("SUM(F2:F2)", Cell(d, "F3").Element(X + "f").Value);
            Assert.Empty(d.Descendants(X + "c").Where(c => (string)c.Attribute("r") == "F4")); // no Tổng cộng without percent lines
            var big = EstimateLogic.Build(new[] { R("A", 123456.789) }, new[] { P("a", 98_765_432) }, null);
            Assert.Equal(EstimateLogic.RoundMoney(123456.789 * 98_765_432), big.Total);
            Assert.Equal("12.193.263.111.263", EstimateLogic.Vn(12_193_263_111_263, 0));
            var zero = EstimateLogic.Build(new[] { R("A", 0) }, new[] { P("a", 5) }, null);
            Assert.Equal(0, zero.Total);
        }

        [Fact]
        public void Names_with_odd_characters_and_the_limits()
        {
            Assert.NotNull(EstimateLogic.Match("Tường (gạch) 220+", new[] { P("tuong (gach) 220+", 1) }, out _));
            Assert.NotNull(EstimateLogic.Match("PHÒNG KHÁCH", new[] { P("phong-khach", 1) }, out _));
            Assert.Null(EstimateLogic.Match("PHÒNG KHÁCH 2", new[] { P("phong khach", 1) }, out _)); // no guessing "close enough"
            Assert.NotNull(EstimateLogic.Match("a.b[c]", new[] { P("A.B[*", 1) }, out _));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(new[] { R("A", 1) }, Enumerable.Range(0, G3Numbers.MaxPrices + 1).Select(i => P("x" + i, 1)).ToList(), null));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(new[] { R("A", 1) }, null, Enumerable.Range(0, G3Numbers.MaxPercentLines + 1).Select(i => ("p" + i, 1.0)).ToList()));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(new[] { R("A", 1) }, new[] { P("a", -1) }, null));
        }

        // ------------------------------------------------------------ checked: no sample price for Claude to take

        [Fact]
        public void No_sample_price_where_claude_reads()
        {
            var texts = new[]
            {
                Src("agent", "src", "tools", "estimate.ts"), Src("agent", "src", "agent", "systemPrompt.ts"),
                Src("skills", "ve-kien-truc", "SKILL.md"), Src("skills", "ve-3d-kien-truc", "SKILL.md"),
            };
            foreach (var t in texts)
            {
                Assert.DoesNotMatch(@"[0-9][0-9.,]*\s*(đồng|đ/|vnđ|VND)", t);
                Assert.DoesNotMatch(@"unit_price""?\s*:\s*[0-9]", t);
            }
            // the old parameter name is gone everywhere Claude or a macro reads it
            Assert.DoesNotContain("p.Str(\"source\")", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "EstimateTools.cs"));
            Assert.DoesNotContain("source:", Src("agent", "src", "tools", "estimate.ts"));
        }
    }
}
