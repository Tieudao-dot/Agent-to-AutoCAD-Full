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
        /// <summary>KFG-26: the unit price column when a price has decimals (12,5 USD shows 12.50, not 13).</summary>
        public const string ExcelPriceDecimalsFormat = "#,##0.00";
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

    /// <summary>
    /// One cell of the .xlsx with a formula (and the value it gives, for readers that do not recalculate) or a style. A string
    /// Value without a formula is always written as text (never turned into a number, unlike a plain string cell).
    /// </summary>
    public sealed class XlsxCell
    {
        public object Value;
        public string Formula;
        /// <summary>0 normal, 1 bold, 2 money, 3 money bold, 4 money with 2 decimals (KFG-26).</summary>
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

        /// <summary>KFG-02: keys of a source schedule's result that warn about its numbers; the estimate passes them on.</summary>
        public static readonly string[] SourceWarningKeys = { "warning", "warnings", "contains_others", "left_out_layers", "skipped", "note", "notes" };

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
            /// <summary>KFG-03: a price name that fits several different items once accents / separators are dropped (price → items).</summary>
            public List<(string price, List<string> items)> Ambiguous = new List<(string, List<string>)>();
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

        /// <summary>
        /// Test & sửa G3: a unit as the "check" note compares it: "m3", "m³", "M^3" and "m 3" are the same unit (the
        /// superscripts of the schedules and the plain digits the user types).
        /// </summary>
        public static string UnitKey(string unit) =>
            Regex.Replace(Key((unit ?? "").Normalize(NormalizationForm.FormKD)).Replace("^", ""), @"\s+", "");

        /// <summary>KFG-03: level 1 — the name as written, accents kept: lower case, spaces collapsed.</summary>
        public static string KeyExact(string s) => Regex.Replace((s ?? "").Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant(), @"\s+", " ");

        /// <summary>KFG-03: level 2 — accents kept, '-' '_' '.' as spaces ("A-WALL" = "A WALL"), used only when it fits one item.</summary>
        public static string KeySep(string s) =>
            Regex.Replace(KeyExact(s).Replace('-', ' ').Replace('_', ' ').Replace('.', ' '), @"\s+", " ").Trim();

        /// <summary>KFG-03: a name written with accents (then it must match them: "Sân" is not "Sàn").</summary>
        public static bool HasAccents(string s) => Key(s) != KeySep(s);

        private static bool Wild(string key, string pattern)
        {
            var rx = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
            return Regex.IsMatch(key, rx, RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// The price of a row (KFG-03: money, so rather empty than a wrong price): (1) a price with the same name, accents kept
        /// (case and spaces do not matter); else (2) the same name with '-' '_' '.' as spaces; else (3), only for a price the
        /// user typed without accents, the same name without accents. Levels 2 and 3 count only when that price name fits one
        /// item of the list (else: ambiguous, left empty and reported). Else the one pattern with '*' that fits. Two prices
        /// that differ for the same row: none (ambiguous), never a guess.
        /// </summary>
        public static Price Match(string item, IList<Price> prices, out string note) => Match(item, prices, out note, out _, new[] { item });

        public static Price Match(string item, IList<Price> prices, out string note, out List<Price> fitting) => Match(item, prices, out note, out fitting, new[] { item });

        /// <summary>The same, knowing every item of the list (for the "fits one item only" rule of levels 2 and 3).</summary>
        public static Price Match(string item, IList<Price> prices, out string note, out List<Price> fitting, IEnumerable<string> allItems)
        {
            note = null;
            fitting = new List<Price>();
            var items = allItems.Select(KeyExact).Distinct().ToList();
            bool Unique(Price p, Func<string, string> key) => items.Count(i => key(i) == key(p.Item)) <= 1;
            var plain = prices.Where(p => !p.Item.Contains("*")).ToList();
            var level = new List<Func<string, string>> { KeyExact, KeySep, Key };
            for (int l = 0; l < level.Count; l++)
            {
                var key = level[l];
                var hit = plain.Where(p => (l < 2 || !HasAccents(p.Item)) && key(p.Item) == key(item)).ToList();
                if (hit.Count == 0) continue;
                fitting = hit;
                var sure = l == 0 ? hit : hit.Where(p => Unique(p, key)).ToList();
                if (sure.Count == 0) { note = "the price name fits several items (" + string.Join(", ", hit.Select(p => p.Item)) + "): ask which"; return null; }
                if (sure.Select(p => p.UnitPrice).Distinct().Count() > 1) { note = "several unit prices given for this item"; return null; }
                return sure[0];
            }
            var wild = prices.Where(p => p.Item.Contains("*") && (HasAccents(p.Item) ? Wild(KeySep(item), KeySep(p.Item)) : Wild(Key(item), Key(p.Item)))).ToList();
            fitting = wild;
            if (wild.Count == 0) return null;
            // KFG-30: a pattern typed without accents ("San*") that fits names whose fixed letters differ only in accents
            // ("Sân phơi", "Sàn gỗ") is ambiguous, as a plain name is: no price, ask which
            var amb = wild.Where(p => !HasAccents(p.Item) && WildAccentClash(p.Item, allItems)).ToList();
            if (amb.Count > 0) { note = "the price pattern fits names that differ only in accents (" + string.Join(", ", amb.Select(p => p.Item)) + "): ask which"; return null; }
            if (wild.Select(p => p.UnitPrice).Distinct().Count() > 1) { note = "several price patterns fit (" + string.Join(", ", wild.Select(p => p.Item)) + ")"; return null; }
            return wild[0];
        }

        /// <summary>
        /// KFG-30: the accented letters of a name where an unaccented pattern's fixed parts fall ("San*" on "Sân phơi" → "sân").
        /// null = the pattern does not fit.
        /// </summary>
        public static string WildFixedPart(string pattern, string item)
        {
            var s = KeySep(item);
            var m = new StringBuilder(s.Length);
            foreach (var ch in s) { var k = Key(ch.ToString()); m.Append(k.Length == 1 ? k[0] : char.ToLowerInvariant(ch)); }
            var parts = Key(pattern).Split('*');
            var rx = "^" + string.Join("(?:.*)", parts.Select(x => "(" + Regex.Escape(x) + ")")) + "$";
            var hit = Regex.Match(m.ToString(), rx, RegexOptions.CultureInvariant);
            if (!hit.Success) return null;
            return string.Join("*", Enumerable.Range(1, parts.Length).Select(g => s.Substring(hit.Groups[g].Index, hit.Groups[g].Length)));
        }

        /// <summary>KFG-30: an unaccented pattern fits items whose fixed letters differ in accents.</summary>
        public static bool WildAccentClash(string pattern, IEnumerable<string> items) =>
            (items ?? Enumerable.Empty<string>()).Select(i => WildFixedPart(pattern, i)).Where(f => f != null).Distinct().Count() > 1;

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
                var pr = Match(row.Item, prices, out var note, out var fitting, rows.Select(x => x.Item));
                foreach (var fp in fitting) used.Add(fp); // Test & sửa G3: an ambiguous price fitted a row, it is not "unused"
                // the quantity as shown (and as the Excel formula multiplies it)
                double q = Math.Round(row.Quantity, G3Numbers.QuantityDecimals, MidpointRounding.AwayFromZero);
                var line = new Line { Item = row.Item, Unit = row.Unit, Quantity = q, Note = note };
                if (pr != null)
                {
                    line.UnitPrice = pr.UnitPrice;
                    line.Amount = RoundMoney(q * pr.UnitPrice);
                    line.PricedBy = pr.Item;
                    if (!string.IsNullOrWhiteSpace(pr.Unit) && UnitKey(pr.Unit) != UnitKey(row.Unit))
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
            // KFG-03: the price names that fit several items, and those items (Claude asks which one is meant)
            // KFG-30: and the patterns typed without accents that fit names differing only in accents
            foreach (var p in prices.Where(p => p.Item.Contains("*") && !HasAccents(p.Item)))
            {
                var its = rows.Select(x => x.Item).Where(i => WildFixedPart(p.Item, i) != null).Distinct().ToList();
                if (WildAccentClash(p.Item, its)) r.Ambiguous.Add((p.Item, its));
            }
            foreach (var p in prices.Where(p => !p.Item.Contains("*")))
            {
                foreach (var key in new Func<string, string>[] { KeySep, Key })
                {
                    if (key == Key && HasAccents(p.Item)) continue;
                    var its = rows.Select(x => x.Item).Where(i => key(i) == key(p.Item)).Distinct().ToList();
                    if (its.Select(KeyExact).Distinct().Count() > 1 && !rows.Any(x => KeyExact(x.Item) == KeyExact(p.Item))) { r.Ambiguous.Add((p.Item, its)); break; }
                }
            }
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
            bool priceDecimals = r.Lines.Any(l => l.UnitPrice.HasValue && Math.Abs(l.UnitPrice.Value - Math.Round(l.UnitPrice.Value)) > 1e-9);
            int first = 2;
            foreach (var l in r.Lines)
            {
                int n = rows.Count + 1;
                rows.Add(new List<object>
                {
                    // Test & sửa G3: names stay text in Excel (a room "101" or a layer "2024" is not a number)
                    (double)(n - 1), new XlsxCell { Value = l.Item }, new XlsxCell { Value = l.Unit }, Math.Round(l.Quantity, G3Numbers.QuantityDecimals),
                    // KFG-26: a unit price with decimals (12,5 USD) keeps them in Excel
                    new XlsxCell { Value = l.UnitPrice.HasValue ? (object)l.UnitPrice.Value : null, Style = priceDecimals ? 4 : 2 },
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
                rows.Add(new List<object> { "", new XlsxCell { Value = p.Name }, "%", p.Rate, "", new XlsxCell { Formula = $"ROUND(F{sub}*D{n}/100,{d})", Value = p.Amount, Style = 2 } });
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
