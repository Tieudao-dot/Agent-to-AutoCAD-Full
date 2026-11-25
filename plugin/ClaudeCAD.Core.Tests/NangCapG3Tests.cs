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
    /// <summary>
    /// Nâng cấp đợt G3 (docs/ban-giao/nang-cap-g3.md): cost_estimate — prices only from the user (a missing one is never 0),
    /// name matching, rounding, percent lines, the drawing table, the Excel formulas and money format, the plan of the source.
    /// </summary>
    public class NangCapG3Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static List<EstimateLogic.Row> Rows() => new List<EstimateLogic.Row>
        {
            new EstimateLogic.Row { Item = "A-WALL-3D", Unit = "m³", Quantity = 12.5 },
            new EstimateLogic.Row { Item = "A-SLAB-3D", Unit = "m³", Quantity = 8.333 },
            new EstimateLogic.Row { Item = "A-COLS-3D", Unit = "m³", Quantity = 1.2 },
        };

        private static EstimateLogic.Price P(string item, double price, string unit = null) => new EstimateLogic.Price { Item = item, UnitPrice = price, Unit = unit };

        [Fact]
        public void A_row_without_the_users_price_stays_empty_is_listed_and_is_not_in_the_total()
        {
            var r = EstimateLogic.Build(Rows(), new[] { P("A-WALL-3D", 1_200_000), P("A-SLAB-3D", 2_500_000) }, null);
            Assert.Equal(15_000_000, r.Lines[0].Amount);
            Assert.Equal(20_832_500, r.Lines[1].Amount);
            Assert.Null(r.Lines[2].UnitPrice);
            Assert.Null(r.Lines[2].Amount);
            Assert.Equal(new[] { "A-COLS-3D" }, r.Missing);
            Assert.Equal(35_832_500, r.Subtotal);
            Assert.Equal(r.Subtotal, r.Total);
            // no price at all: every row missing, total 0, nothing guessed
            var none = EstimateLogic.Build(Rows(), null, null);
            Assert.Equal(3, none.Missing.Count);
            Assert.All(none.Lines, l => Assert.Null(l.Amount));
            // a price of 0 the user gave is a price
            Assert.Empty(EstimateLogic.Build(Rows().Take(1).ToList(), new[] { P("A-WALL-3D", 0) }, null).Missing);
        }

        [Fact]
        public void Prices_match_by_name_without_accents_case_or_dashes_and_by_one_pattern_never_by_guess()
        {
            Assert.Equal(EstimateLogic.Key("Tường gạch 220"), EstimateLogic.Key("tuong  GACH-220"));
            Assert.Equal("dat dap", EstimateLogic.Key("Đất_đắp"));
            var rows = new List<EstimateLogic.Row>
            {
                new EstimateLogic.Row { Item = "Tường gạch 220", Unit = "m³", Quantity = 2 },
                new EstimateLogic.Row { Item = "A-WALL-EXT", Unit = "m³", Quantity = 1 },
                new EstimateLogic.Row { Item = "Cửa đi D1", Unit = "cái", Quantity = 3 },
                new EstimateLogic.Row { Item = "Phòng khách", Unit = "m²", Quantity = 20 },
            };
            var prices = new[] { P("tuong gach 220", 100), P("A-WALL*", 50), P("Cua*", 10), P("Cửa đi*", 20), P("phong ngu", 5) };
            var r = EstimateLogic.Build(rows, prices, null);
            Assert.Equal(200, r.Lines[0].Amount);           // exact (accents, case)
            Assert.Equal(50, r.Lines[1].Amount);            // one pattern
            Assert.Null(r.Lines[2].Amount);                 // two patterns, two prices: ambiguous, not guessed
            Assert.Contains("several", r.Lines[2].Note);
            Assert.Null(r.Lines[3].Amount);                 // "Phòng khách" ≠ "phong ngu": no fuzzy match
            Assert.Equal(new[] { "Cửa đi D1", "Phòng khách" }, r.Missing);
            Assert.Contains("phong ngu", r.Unused);
            // a price given per another unit is used but flagged
            var u = EstimateLogic.Build(rows.Take(1).ToList(), new[] { P("tuong gach 220", 100, "m²") }, null);
            Assert.Contains("per m²", u.Lines[0].Note);
        }

        [Fact]
        public void Amounts_round_to_whole_dong_and_percent_lines_come_only_from_the_call()
        {
            var r = EstimateLogic.Build(new List<EstimateLogic.Row> { new EstimateLogic.Row { Item = "x", Unit = "m³", Quantity = 0.5 } }, new[] { P("x", 666_667) }, new[] { ("Thuế VAT", 10.0) });
            Assert.Equal(333_334, r.Lines[0].Amount);       // 333 333.5 → 333 334 (half away from zero)
            Assert.Equal(1.235, EstimateLogic.Build(new List<EstimateLogic.Row> { new EstimateLogic.Row { Item = "y", Quantity = 1.2345 } }, null, null).Lines[0].Quantity); // as shown / in Excel
            Assert.Equal(33_333, r.Percents[0].Amount);     // 33 333.4
            Assert.Equal(366_667, r.Total);
            Assert.Empty(EstimateLogic.Build(Rows(), new[] { P("A-WALL-3D", 1) }, null).Percents); // none unless asked
            Assert.Throws<ToolException>(() => EstimateLogic.Build(Rows(), null, new[] { ("VAT", 150.0) }));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(Rows(), new[] { P("x", -1) }, null));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(Rows(), new[] { P(" ", 1) }, null));
            Assert.Throws<ToolException>(() => EstimateLogic.Build(Rows(), null, Enumerable.Repeat(("a", 1.0), 6).ToList()));
        }

        [Fact]
        public void The_drawing_table_writes_vietnamese_numbers_and_leaves_missing_prices_empty()
        {
            Assert.Equal("1.234.567", EstimateLogic.Vn(1234567, 0));
            Assert.Equal("12,5", EstimateLogic.Vn(12.5, 3));
            Assert.Equal("0", EstimateLogic.Vn(0, 0));
            var r = EstimateLogic.Build(Rows(), new[] { P("A-WALL-3D", 1_200_000) }, new[] { ("Thuế VAT", 10.0) });
            var t = EstimateLogic.DrawingTable(r, null);
            Assert.Equal("Thành tiền (đồng)", t[0][5]);
            Assert.Equal("15.000.000", t[1][5]);
            Assert.Equal("", t[2][4]);
            Assert.Equal("", t[2][5]);
            Assert.Equal("Cộng", t[4][1]);
            Assert.Equal("Thuế VAT", t[5][1]);
            Assert.Equal("Tổng cộng", t[6][1]);
            Assert.Equal("16.500.000", t[6][5]);
            Assert.Equal("Đơn giá (USD)", EstimateLogic.DrawingTable(r, "USD")[0][4]);
        }

        private static Dictionary<string, string> Unzip(byte[] b)
        {
            var d = new Dictionary<string, string>();
            using (var z = new ZipArchive(new MemoryStream(b)))
                foreach (var e in z.Entries)
                    using (var s = new StreamReader(e.Open(), Encoding.UTF8)) d[e.FullName] = s.ReadToEnd();
            return d;
        }

        [Fact]
        public void The_excel_file_has_formulas_and_a_thousands_format_so_a_price_typed_there_updates_the_total()
        {
            var r = EstimateLogic.Build(Rows(), new[] { P("A-WALL-3D", 1_200_000), P("A-SLAB-3D", 2_500_000) }, new[] { ("Thuế VAT", 10.0) });
            var rows = EstimateLogic.ExcelRows(r, null);
            Assert.Equal("IF(E2=\"\",\"\",ROUND(D2*E2,0))", ((XlsxCell)rows[1][5]).Formula);
            Assert.Null(((XlsxCell)rows[3][4]).Value);      // the missing price: an empty cell to type in
            Assert.Equal("SUM(F2:F4)", ((XlsxCell)rows[4][5]).Formula);
            Assert.Equal("ROUND(F5*D6/100,0)", ((XlsxCell)rows[5][5]).Formula);
            Assert.Equal("F5+F6", ((XlsxCell)rows[6][5]).Formula);
            var files = Unzip(XlsxWriter.Build(rows.Select(x => (IList<object>)x).ToList(), G3Numbers.SheetName));
            foreach (var f in files.Where(f => f.Key.EndsWith(".xml"))) XDocument.Parse(f.Value); // well-formed
            var sheet = files["xl/worksheets/sheet1.xml"];
            Assert.Contains("<f>IF(E2=&quot;&quot;,&quot;&quot;,ROUND(D2*E2,0))</f><v>15000000</v>", sheet);
            Assert.Contains("<f>SUM(F2:F4)</f>", sheet);
            Assert.Contains("s=\"2\"", sheet);
            Assert.Contains("s=\"3\"", sheet);
            Assert.Contains("formatCode=\"#,##0\"", files["xl/styles.xml"]);
            Assert.Contains("cellXfs count=\"4\"", files["xl/styles.xml"]);
            Assert.Contains("fullCalcOnLoad=\"1\"", files["xl/workbook.xml"]);
            Assert.Contains("name=\"Dự toán\"", files["xl/workbook.xml"]);
            // the old schedules' files are unchanged apart from the shared styles: plain numbers, no formulas
            var plain = Unzip(XlsxWriter.Build(new List<IList<object>> { new List<object> { "a" }, new List<object> { 1.5 } }, "x"))["xl/worksheets/sheet1.xml"];
            Assert.DoesNotContain("<f>", plain);
            Assert.Equal("cong-trinh-du-toan.xlsx", EstimateLogic.DefaultFile(@"D:\du an\cong-trinh.dwg"));
        }

        [Fact]
        public void The_rows_come_from_the_source_schedule_and_its_plan_decides()
        {
            var vol = new Dictionary<string, object> { ["rows"] = new List<object>
            {
                new Dictionary<string, object> { ["layer"] = "A-WALL-3D", ["net_m3"] = 10.0, ["volume_m3"] = 11.0 },
            } };
            Assert.Equal(10, EstimateLogic.RowsOf("volume", vol, "net")[0].Quantity);
            Assert.Equal(11, EstimateLogic.RowsOf("volume", vol, "gross")[0].Quantity);
            Assert.Equal("m³", EstimateLogic.RowsOf("volume", vol, "net")[0].Unit);
            var blk = new Dictionary<string, object> { ["rows"] = new List<object> { new Dictionary<string, object> { ["name"] = "CUA-DI-900", ["count"] = 4, ["value"] = "D1" } } };
            var b = EstimateLogic.RowsOf("blocks", blk, "net")[0];
            Assert.Equal("CUA-DI-900 — D1", b.Item); Assert.Equal(4, b.Quantity); Assert.Equal("cái", b.Unit);
            var area = new Dictionary<string, object> { ["rows"] = new List<object> { new Dictionary<string, object> { ["name"] = "", ["area_m2"] = 12.5 } } };
            Assert.Equal("(chưa có tên)", EstimateLogic.RowsOf("areas", area, "net")[0].Item);
            Assert.Empty(EstimateLogic.RowsOf("areas", new Dictionary<string, object>(), "net"));
            // the plan of each source: volumes are 3D, blocks / areas 2D; the estimate itself is in every plan
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(EstimateLogic.Sources["volume"]));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(EstimateLogic.Sources["blocks"]));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(EstimateLogic.Sources["areas"]));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("cost_estimate"));
            // the drawn table is text: refused on the 3D plan (numbers + Excel only), allowed on 2D / Full
            var withTable = new Dictionary<string, object> { ["table_position"] = new List<object> { 0.0, 0.0 } };
            Assert.Equal("table_position", PlanLogic.BlockedParam(PlanLogic.Plan3D, "cost_estimate", withTable));
            Assert.Null(PlanLogic.BlockedParam(PlanLogic.PlanFull, "cost_estimate", withTable));
            Assert.Null(PlanLogic.BlockedParam(PlanLogic.Plan3D, "cost_estimate", new Dictionary<string, object> { ["excel"] = true }));
        }

        [Fact]
        public void The_plugin_checks_the_source_plan_the_card_the_macro_and_read_only()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "EstimateTools.cs");
            Assert.Contains("PlanLogic.Allowed(plan, sourceTool)", t);
            Assert.Contains("WorkModeLogic.BlockedMessage(sourceTool", t);
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, File.Exists(full)", t);
            Assert.Contains("MacroRunning == true) res[\"excel_note\"]", t);
            // a read (also in Chỉ xem) unless it draws the table
            Assert.Contains("new ToolDef(\"cost_estimate\", true, CostEstimate, readsWhen: p => !p.Has(\"table_position\"))", t);
            Assert.Contains("RegisterEstimate(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            Assert.Contains("[\"cost_estimate\"] = \"Lập dự toán\"", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            // the source never draws a table or writes its own file
            Assert.Contains("\"table_position\", \"excel\", \"overwrite\"", t);
            Assert.False(MacroLogic.IsHandleKey("quantities")); // a macro replay keeps it as it is
        }
    }
}
