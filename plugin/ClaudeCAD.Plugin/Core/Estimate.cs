using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt G3: the numbers and words of the cost estimate the user meets, one place each
    /// (docs/ban-giao/nang-cap-g3.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class G3Numbers
    {
        /// <summary>Currency written in the headers (the user may give another, e.g. "USD").</summary>
        public const string Currency = "đồng";
        /// <summary>Amounts are rounded to this many decimals (0 = whole đồng), in the result, the drawing table and the Excel formulas.</summary>
        public const int MoneyDecimals = 0;
        /// <summary>Excel number format of the money columns (thousands separated; Excel shows the separator of the user's language).</summary>
        public const string ExcelMoneyFormat = "#,##0";
        /// <summary>Drawing table / chat: thousands separator and decimal comma (Vietnamese), e.g. 1.234.567 and 12,5.</summary>
        public const string ThousandsSeparator = ".", DecimalSeparator = ",";
        /// <summary>Quantities keep this many decimals.</summary>
        public const int QuantityDecimals = 3;
        public const string Title = "BẢNG DỰ TOÁN";
        public const string SheetName = "Dự toán", FileSuffix = "du-toan";
        public const string SubtotalLabel = "Cộng", TotalLabel = "Tổng cộng";
        /// <summary>Units of the quantities of each source.</summary>
        public const string UnitVolume = "m³", UnitArea = "m²", UnitCount = "cái";
        /// <summary>Most extra lines in percent (VAT, management…) and most price entries of one call.</summary>
        public const int MaxPercentLines = 5, MaxPrices = 500;
    }

    /// <summary>One cell of the .xlsx with a formula (and the value it gives, for readers that do not recalculate) or a style.</summary>
    public sealed class XlsxCell
    {
        public object Value;
        public string Formula;
        /// <summary>0 normal, 1 bold, 2 money, 3 money bold.</summary>
        public int Style;
    }

    /// <summary>
    /// Nâng cấp đợt G3: "lập dự toán" — the rows of an existing schedule (volume by layer, blocks, room / layer areas) with
    /// the unit prices the user gives. Prices only come from the user: a row without one stays empty and is listed, never
    /// priced as 0. AutoCAD-free (Drawing/EstimateTools.cs runs the source schedule and writes the table / file).
    /// </summary>
    public static class EstimateLogic
    {
        public const string Tool = "cost_estimate";
        /// <summary>source → the schedule tool whose plan group the estimate needs.</summary>
        public static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["volume"] = "volume_schedule", ["blocks"] = "block_schedule", ["areas"] = "area_schedule",
        };

        public sealed class Row
        {
            public string Item = "", Unit = "";
            public double Quantity;
        }

        public sealed class Price
        {
            public string Item = "";
            public double UnitPrice;
            public string Unit;
        }

        public sealed class Line
        {
            public string Item = "", Unit = "";
            public double Quantity;
            /// <summary>null = no price from the user.</summary>
            public double? UnitPrice;
            public double? Amount;
            /// <summary>The price entry that matched (its text as given).</summary>
            public string PricedBy;
            public string Note;
        }

        public sealed class Percent
        {
            public string Name = "";
            public double Rate;
            public double Amount;
        }

        public sealed class Result
        {
            public List<Line> Lines = new List<Line>();
            public double Subtotal;
            public List<Percent> Percents = new List<Percent>();
            public double Total;
            public List<string> Missing = new List<string>();
            public List<string> Unused = new List<string>();
        }

        /// <summary>A name as the matching compares it: no accents (đ = d), lower case, '-' '_' '.' as spaces, spaces collapsed.</summary>
        public static string Key(string s)
        {
            var d = (s ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(ch == '-' || ch == '_' || ch == '.' ? ' ' : char.ToLowerInvariant(ch));
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static bool Wild(string key, string pattern)
        {
            var rx = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
            return Regex.IsMatch(key, rx, RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// The price of a row: the entry with the same name (Key: accents, case, '-' / spaces do not matter), else the one
        /// pattern with '*' that fits ("A-WALL*"). Two patterns with different prices: no price (ambiguous), never a guess.
        /// </summary>
        public static Price Match(string item, IList<Price> prices, out string note)
        {
            note = null;
            var k = Key(item);
            var exact = prices.Where(p => !p.Item.Contains("*") && Key(p.Item) == k).ToList();
            if (exact.Count > 0)
            {
                if (exact.Select(p => p.UnitPrice).Distinct().Count() > 1) { note = "several unit prices given for this item"; return null; }
                return exact[0];
            }
            var wild = prices.Where(p => p.Item.Contains("*") && Wild(k, Key(p.Item))).ToList();
            if (wild.Count == 0) return null;
            if (wild.Select(p => p.UnitPrice).Distinct().Count() > 1) { note = "several price patterns fit (" + string.Join(", ", wild.Select(p => p.Item)) + ")"; return null; }
            return wild[0];
        }

        public static double RoundMoney(double v) => Math.Round(v, G3Numbers.MoneyDecimals, MidpointRounding.AwayFromZero);

        public static Result Build(IList<Row> rows, IList<Price> prices, IList<(string name, double rate)> percents)
        {
            prices = prices ?? new List<Price>();
            if (prices.Count > G3Numbers.MaxPrices) throw ToolException.Invalid($"{Tool}: at most {G3Numbers.MaxPrices} prices in one call.");
            foreach (var p in prices)
            {
                if (string.IsNullOrWhiteSpace(p.Item)) throw ToolException.Invalid($"{Tool}: every price needs the item it is for.");
                if (double.IsNaN(p.UnitPrice) || double.IsInfinity(p.UnitPrice) || p.UnitPrice < 0) throw ToolException.Invalid($"{Tool}: the unit price of '{p.Item}' must be a number ≥ 0.");
            }
            var pc = percents ?? new List<(string, double)>();
            if (pc.Count > G3Numbers.MaxPercentLines) throw ToolException.Invalid($"{Tool}: at most {G3Numbers.MaxPercentLines} percent lines.");
            var r = new Result();
            var used = new HashSet<Price>();
            foreach (var row in rows)
            {
                var pr = Match(row.Item, prices, out var note);
                // the quantity as shown (and as the Excel formula multiplies it)
                double q = Math.Round(row.Quantity, G3Numbers.QuantityDecimals, MidpointRounding.AwayFromZero);
                var line = new Line { Item = row.Item, Unit = row.Unit, Quantity = q, Note = note };
                if (pr != null)
                {
                    used.Add(pr);
                    line.UnitPrice = pr.UnitPrice;
                    line.Amount = RoundMoney(q * pr.UnitPrice);
                    line.PricedBy = pr.Item;
                    if (!string.IsNullOrWhiteSpace(pr.Unit) && Key(pr.Unit) != Key(row.Unit))
                        line.Note = $"the price is per {pr.Unit.Trim()}, the quantity is in {row.Unit}: check";
                }
                else r.Missing.Add(row.Item);
                r.Lines.Add(line);
            }
            r.Subtotal = r.Lines.Where(l => l.Amount.HasValue).Sum(l => l.Amount.Value);
            foreach (var (name, rate) in pc)
            {
                if (string.IsNullOrWhiteSpace(name) || double.IsNaN(rate) || rate < 0 || rate > 100)
                    throw ToolException.Invalid($"{Tool}: a percent line needs a name and a rate between 0 and 100.");
                r.Percents.Add(new Percent { Name = name.Trim(), Rate = rate, Amount = RoundMoney(r.Subtotal * rate / 100) });
            }
            r.Total = r.Subtotal + r.Percents.Sum(x => x.Amount);
            r.Unused = prices.Where(p => !used.Contains(p)).Select(p => p.Item).ToList();
            return r;
        }

        /// <summary>1234567 → "1.234.567"; 12.5 → "12,5" (Vietnamese separators, as the drawing tables write numbers).</summary>
        public static string Vn(double v, int decimals)
        {
            var s = Math.Round(v, decimals, MidpointRounding.AwayFromZero).ToString("#,##0." + new string('#', Math.Max(0, decimals)), CultureInfo.InvariantCulture).TrimEnd('.');
            return s.Replace(",", "\u0001").Replace(".", G3Numbers.DecimalSeparator).Replace("\u0001", G3Numbers.ThousandsSeparator);
        }

        public static string[] Header(string currency)
        {
            var c = string.IsNullOrWhiteSpace(currency) ? G3Numbers.Currency : currency.Trim();
            return new[] { "STT", "Hạng mục", "Đơn vị", "Khối lượng", $"Đơn giá ({c})", $"Thành tiền ({c})" };
        }

        /// <summary>The table drawn in the drawing (texts): missing prices stay empty.</summary>
        public static List<List<object>> DrawingTable(Result r, string currency)
        {
            var t = new List<List<object>> { Header(currency).Cast<object>().ToList() };
            int i = 1;
            foreach (var l in r.Lines)
                t.Add(new List<object> { (i++).ToString(CultureInfo.InvariantCulture), l.Item, l.Unit, Vn(l.Quantity, G3Numbers.QuantityDecimals),
                    l.UnitPrice.HasValue ? Vn(l.UnitPrice.Value, 2) : "", l.Amount.HasValue ? Vn(l.Amount.Value, G3Numbers.MoneyDecimals) : "" });
            t.Add(new List<object> { "", G3Numbers.SubtotalLabel, "", "", "", Vn(r.Subtotal, G3Numbers.MoneyDecimals) });
            foreach (var p in r.Percents)
                t.Add(new List<object> { "", p.Name, "%", Vn(p.Rate, 2), "", Vn(p.Amount, G3Numbers.MoneyDecimals) });
            if (r.Percents.Count > 0) t.Add(new List<object> { "", G3Numbers.TotalLabel, "", "", "", Vn(r.Total, G3Numbers.MoneyDecimals) });
            return t;
        }

        /// <summary>
        /// The Excel sheet: numbers as numbers, Thành tiền = ROUND(Khối lượng × Đơn giá) as a formula (empty while the price is
        /// empty, so typing a price in Excel fills it), Cộng = SUM, percent lines = Cộng × rate, Tổng cộng = Cộng + them.
        /// </summary>
        public static List<List<object>> ExcelRows(Result r, string currency)
        {
            int d = G3Numbers.MoneyDecimals;
            var rows = new List<List<object>> { Header(currency).Cast<object>().ToList() };
            int first = 2;
            foreach (var l in r.Lines)
            {
                int n = rows.Count + 1;
                rows.Add(new List<object>
                {
                    (double)(n - 1), l.Item, l.Unit, Math.Round(l.Quantity, G3Numbers.QuantityDecimals),
                    new XlsxCell { Value = l.UnitPrice.HasValue ? (object)l.UnitPrice.Value : null, Style = 2 },
                    new XlsxCell { Formula = $"IF(E{n}=\"\",\"\",ROUND(D{n}*E{n},{d}))", Value = l.Amount.HasValue ? (object)l.Amount.Value : null, Style = 2 },
                });
            }
            int last = rows.Count;
            int sub = rows.Count + 1;
            rows.Add(new List<object> { "", G3Numbers.SubtotalLabel, "", "", "",
                new XlsxCell { Formula = last >= first ? $"SUM(F{first}:F{last})" : "0", Value = r.Subtotal, Style = 3 } });
            var pRows = new List<int>();
            foreach (var p in r.Percents)
            {
                int n = rows.Count + 1;
                pRows.Add(n);
                rows.Add(new List<object> { "", p.Name, "%", p.Rate, "", new XlsxCell { Formula = $"ROUND(F{sub}*D{n}/100,{d})", Value = p.Amount, Style = 2 } });
            }
            if (r.Percents.Count > 0)
                rows.Add(new List<object> { "", G3Numbers.TotalLabel, "", "", "",
                    new XlsxCell { Formula = "F" + sub + string.Concat(pRows.Select(x => "+F" + x)), Value = r.Total, Style = 3 } });
            return rows;
        }

        /// <summary>The default Excel file name next to the drawing: &lt;drawing&gt;-du-toan.xlsx.</summary>
        public static string DefaultFile(string drawingName)
        {
            var file = (drawingName ?? "").Trim();
            int cut = Math.Max(file.LastIndexOf('\\'), file.LastIndexOf('/'));
            if (cut >= 0) file = file.Substring(cut + 1);
            int dot = file.LastIndexOf('.');
            var stem = dot > 0 ? file.Substring(0, dot) : file;
            if (string.IsNullOrWhiteSpace(stem)) stem = "ban-ve";
            return stem + "-" + G3Numbers.FileSuffix + ".xlsx";
        }

        /// <summary>The rows of a source schedule's result (volume_schedule / block_schedule / area_schedule).</summary>
        public static List<Row> RowsOf(string source, IDictionary<string, object> result, string quantity)
        {
            var list = new List<Row>();
            if (result == null || !result.TryGetValue("rows", out var rv) || !(rv is IEnumerable<object> rs)) return list;
            double N(IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;
            string S(IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";
            foreach (var o in rs.OfType<IDictionary<string, object>>())
                switch (source)
                {
                    case "volume":
                        list.Add(new Row { Item = S(o, "layer"), Unit = G3Numbers.UnitVolume, Quantity = N(o, quantity == "gross" ? "volume_m3" : "net_m3") });
                        break;
                    case "blocks":
                        var v = S(o, "value");
                        list.Add(new Row { Item = v.Length > 0 ? S(o, "name") + " — " + v : S(o, "name"), Unit = G3Numbers.UnitCount, Quantity = N(o, "count") });
                        break;
                    default:
                        var name = S(o, "name");
                        list.Add(new Row { Item = name.Length > 0 ? name : "(chưa có tên)", Unit = G3Numbers.UnitArea, Quantity = N(o, "area_m2") });
                        break;
                }
            return list;
        }
    }
}
