using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F4: the numbers of the Excel export, one place each (docs/ban-giao/nang-cap-f4.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class F4Numbers
    {
        /// <summary>Column width (Excel characters) = longest text of the column + this, kept between Min and Max.</summary>
        public const double ColumnPadding = 2, ColumnMinWidth = 6, ColumnMaxWidth = 60;
        /// <summary>Excel's limit for a sheet name.</summary>
        public const int SheetNameMax = 31;
        /// <summary>Rows written at most (the schedules list far fewer; a runaway list stops here).</summary>
        public const int MaxRows = 100000;
        /// <summary>Default file names next to the drawing: &lt;drawing&gt;-&lt;suffix&gt;.xlsx.</summary>
        public static readonly IReadOnlyDictionary<string, string> DefaultSuffix = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["block_schedule"] = "thong-ke-block",
            ["area_schedule"] = "dien-tich",
            ["volume_schedule"] = "khoi-luong",
            ["check_interference"] = "va-cham",
            ["check_drawing"] = "soat-ban-ve", // nâng cấp đợt G2
            ["compare_drawings"] = "so-sanh", // nâng cấp đợt H3
            ["mep_report"] = "ong-mep", // KTP-27
            [MepClashKey] = "va-cham-ong-mep", // KTP-27: the clash file of mep_report, apart from its schedule
            ["rebar_schedule"] = "thong-ke-thep", // KTP-27 (nghi vấn): it fell back to "bang" too
        };
        /// <summary>KTP-27: the key of mep_report's clash table (its own file name and sheet).</summary>
        public const string MepClashKey = "mep_report_clash";
        /// <summary>Sheet names (the drawing table's title, shortened to Excel's 31 characters when needed).</summary>
        public static readonly IReadOnlyDictionary<string, string> SheetName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["block_schedule"] = "Thống kê block",
            ["area_schedule"] = "Diện tích",
            ["volume_schedule"] = "Khối lượng 3D",
            ["check_interference"] = "Va chạm khối 3D",
            ["check_drawing"] = "Soát bản vẽ", // nâng cấp đợt G2
            ["compare_drawings"] = "So sánh bản vẽ", // nâng cấp đợt H3
            ["mep_report"] = "Thống kê ống MEP", // KTP-27
            [MepClashKey] = "Va chạm ống MEP",
            ["rebar_schedule"] = "Thống kê thép",
        };
    }

    /// <summary>
    /// A small .xlsx writer (Office Open XML SpreadsheetML in a zip, no Excel or other library needed on the machine): one
    /// sheet, the first row bold, numbers as numbers (so they add up), text as inline UTF-8 strings (Vietnamese kept).
    /// </summary>
    public static class XlsxWriter
    {
        private static readonly Regex Number = new Regex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// A cell value as Excel should see it: double / int / long / decimal → number; a string that is a plain invariant
        /// number ("12", "-3.25") → number; anything else (codes with leading zeros like "007", "1,5", text) → text.
        /// </summary>
        public static object CellValue(object v)
        {
            switch (v)
            {
                case null: return "";
                case XlsxCell xc: return CellValue(xc.Value); // nâng cấp đợt G3: a formula cell, sized by its value
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? (object)"" : d;
                case float fl: return (double)fl;
                case int i: return (double)i;
                case long l: return (double)l;
                case decimal m: return (double)m;
                case bool b: return b ? "có" : "không";
                case string s:
                    var t = s.Trim();
                    if (t.Length > 0 && t.Length <= 20 && Number.IsMatch(t)
                        && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
                    return s;
                default: return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            }
        }

        /// <summary>Sheet name Excel accepts: no [ ] : * ? / \, not empty, at most 31 characters.</summary>
        public static string SafeSheetName(string name)
        {
            var s = new string((name ?? "").Where(c => "[]:*?/\\".IndexOf(c) < 0 && !char.IsControl(c)).ToArray()).Trim().Trim('\'');
            if (s.Length == 0) s = "Bang";
            return s.Length > F4Numbers.SheetNameMax ? s.Substring(0, F4Numbers.SheetNameMax) : s;
        }

        /// <summary>Column letters: 0 → A, 25 → Z, 26 → AA.</summary>
        public static string Column(int index)
        {
            var sb = new StringBuilder();
            int n = index + 1;
            while (n > 0) { int r = (n - 1) % 26; sb.Insert(0, (char)('A' + r)); n = (n - 1) / 26; }
            return sb.ToString();
        }

        /// <summary>Width of each column in Excel characters.</summary>
        public static double[] ColumnWidths(IList<IList<object>> rows)
        {
            int cols = rows.Count == 0 ? 0 : rows.Max(r => r?.Count ?? 0);
            var w = new double[cols];
            foreach (var r in rows)
                for (int c = 0; r != null && c < r.Count; c++)
                {
                    var v = CellValue(r[c]);
                    int len = v is double d ? d.ToString("R", CultureInfo.InvariantCulture).Length : ((string)v).Length;
                    w[c] = Math.Max(w[c], len);
                }
            for (int c = 0; c < cols; c++)
                w[c] = Math.Min(F4Numbers.ColumnMaxWidth, Math.Max(F4Numbers.ColumnMinWidth, w[c] + F4Numbers.ColumnPadding));
            return w;
        }

        internal static string Esc(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                // Test & sửa đợt F4 (NF4-3): XML 1.0 refuses U+FFFE / U+FFFF and a half surrogate pair (Excel would say the file is damaged)
                if (ch == '\uFFFE' || ch == '\uFFFF') continue;
                if (char.IsHighSurrogate(ch))
                {
                    if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(ch).Append(s[i + 1]); i++; }
                    continue;
                }
                if (char.IsLowSurrogate(ch)) continue;
                switch (ch)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default:
                        // XML 1.0 has no control characters other than tab / CR / LF
                        if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>The sheet XML (row 1 bold = style 1).</summary>
        public static string SheetXml(IList<IList<object>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            var widths = ColumnWidths(rows);
            if (widths.Length > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < widths.Length; c++)
                    sb.Append("<col min=\"").Append(c + 1).Append("\" max=\"").Append(c + 1).Append("\" width=\"")
                      .Append(widths[c].ToString("0.##", CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r] ?? new List<object>();
                sb.Append("<row r=\"").Append(r + 1).Append("\">");
                for (int c = 0; c < row.Count; c++)
                {
                    string refName = Column(c) + (r + 1).ToString(CultureInfo.InvariantCulture);
                    string style = r == 0 ? " s=\"1\"" : "";
                    // nâng cấp đợt G3: a formula (with the value it gives) and / or a style (money = thousands separated)
                    if (row[c] is XlsxCell xc)
                    {
                        string st = xc.Style > 0 ? " s=\"" + xc.Style.ToString(CultureInfo.InvariantCulture) + "\"" : style;
                        var cv = xc.Value is string xs ? xs : CellValue(xc.Value); // Test & sửa G3: a text stays text
                        if (string.IsNullOrEmpty(xc.Formula))
                        {
                            if (cv is double xd) sb.Append("<c r=\"").Append(refName).Append('"').Append(st).Append("><v>").Append(xd.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                            else if (cv is string ts && ts.Length > 0)
                                sb.Append("<c r=\"").Append(refName).Append('"').Append(st).Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Esc(ts)).Append("</t></is></c>");
                            else if (st.Length > 0) sb.Append("<c r=\"").Append(refName).Append('"').Append(st).Append("/>");
                            continue;
                        }
                        sb.Append("<c r=\"").Append(refName).Append('"').Append(st).Append(cv is double ? "" : " t=\"str\"").Append("><f>").Append(Esc(xc.Formula)).Append("</f>");
                        sb.Append("<v>").Append(cv is double fd ? fd.ToString("R", CultureInfo.InvariantCulture) : Esc((string)cv)).Append("</v></c>");
                        continue;
                    }
                    var v = CellValue(row[c]);
                    if (v is double d)
                        sb.Append("<c r=\"").Append(refName).Append('"').Append(style).Append("><v>")
                          .Append(d.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                    else
                    {
                        var s = (string)v;
                        if (s.Length == 0 && style.Length == 0) continue;
                        sb.Append("<c r=\"").Append(refName).Append('"').Append(style).Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                          .Append(Esc(s)).Append("</t></is></c>");
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private const string ContentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string RootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        // font 0 = Calibri 11, font 1 = the same, bold; cellXfs 0 = normal, 1 = bold (the header row)
        private const string Styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"" + G3Numbers.ExcelMoneyFormat + "\"/><numFmt numFmtId=\"165\" formatCode=\"" + G3Numbers.ExcelPriceDecimalsFormat + "\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"5\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            // nâng cấp đợt G3: 2 = money, 3 = money bold (cost_estimate)
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        private static string WorkbookXml(string sheet) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"" + Esc(SafeSheetName(sheet)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets><calcPr fullCalcOnLoad=\"1\"/></workbook>";

        /// <summary>The whole .xlsx file.</summary>
        public static byte[] Build(IList<IList<object>> rows, string sheetName)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (rows.Count > F4Numbers.MaxRows) throw ToolException.Invalid($"Excel export: {rows.Count} rows, the limit is {F4Numbers.MaxRows}.");
            var utf8 = new UTF8Encoding(false);
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    void Add(string name, string text)
                    {
                        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
                        using (var s = e.Open())
                        {
                            var b = utf8.GetBytes(text);
                            s.Write(b, 0, b.Length);
                        }
                    }
                    Add("[Content_Types].xml", ContentTypes);
                    Add("_rels/.rels", RootRels);
                    Add("xl/workbook.xml", WorkbookXml(sheetName));
                    Add("xl/_rels/workbook.xml.rels", WorkbookRels);
                    Add("xl/styles.xml", Styles);
                    Add("xl/worksheets/sheet1.xml", SheetXml(rows));
                }
                return ms.ToArray();
            }
        }

        /// <summary>Writes the file through a temporary file next to it (a crash never leaves half a file under the name).</summary>
        public static void Write(string path, IList<IList<object>> rows, string sheetName)
        {
            var bytes = Build(rows, sheetName);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            File.WriteAllBytes(temp, bytes);
            try
            {
                // KTF (nghi vấn): replace in one step, so a failure leaves the old file whole (Delete + Move could lose it)
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Where an Excel export goes: the same rules as export_stl / plot_pdf (next to the drawing, never over a file unasked).</summary>
    public static class XlsxLogic
    {
        /// <summary>The 'excel' parameter: true = the default name next to the drawing, a string = a file name / path ending in .xlsx.</summary>
        public static string Requested(object excel, string tool)
        {
            switch (excel)
            {
                case null: return null;
                case bool b: return b ? "" : null;
                case string s when s.Trim().Length > 0: return s.Trim();
                default: throw ToolException.Invalid($"{tool}: 'excel' must be true (default file next to the drawing) or a file name ending in .xlsx.");
            }
        }

        public static string DefaultName(string drawingName, string tool)
        {
            // the drawing's file name without folder and extension ('\\' and '/' both, whatever the OS of the tests)
            var file = (drawingName ?? "").Trim();
            int cut = Math.Max(file.LastIndexOf('\\'), file.LastIndexOf('/'));
            if (cut >= 0) file = file.Substring(cut + 1);
            var stem = file.Length == 0 ? "ban-ve" : Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(stem)) stem = "ban-ve";
            return stem + "-" + (F4Numbers.DefaultSuffix.TryGetValue(tool, out var s) ? s : "bang") + ".xlsx";
        }

        /// <summary>
        /// Full path of the file: a relative name / bare file name goes next to the drawing (drawingDir). Refuses a name
        /// that does not end in .xlsx, has no name, has characters Windows refuses, is a folder or sits in a missing folder.
        /// </summary>
        public static string Resolve(string requested, string drawingDir, string drawingName, string tool,
            Func<string, bool> dirExists, Func<string, bool> isDirectory, string key = null)
        {
            string output = string.IsNullOrEmpty(requested) ? DefaultName(drawingName, key ?? tool) : requested;
            if (!output.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw ToolException.Invalid($"{tool}: the Excel file must end with .xlsx ('{output}').");
            string full;
            try { full = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(drawingDir, output)); }
            catch (Exception) { throw ToolException.Invalid($"{tool}: '{output}' is not a valid file path."); }
            var bad = string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(full)) ? "the file has no name before .xlsx" : Util2DLogic.BadFileName(Path.GetFileName(full));
            if (bad != null) throw ToolException.Invalid($"{tool}: '{output}': {bad}. Give another file name ending in .xlsx.");
            if (isDirectory(full)) throw ToolException.Invalid($"{tool}: '{output}' is a folder; give a file name ending in .xlsx.");
            var dir = Path.GetDirectoryName(full);
            if (!dirExists(dir)) throw ToolException.Invalid($"{tool}: folder '{dir}' does not exist.");
            return full;
        }

        private static readonly Regex CommaNumber = new Regex(@"^-?(0|[1-9][0-9]*)(,[0-9]+)?$", RegexOptions.CultureInvariant);

        /// <summary>KTF-02: a column of a measured quantity: its header names a unit in brackets, "(m²)", "(m³)", "(m)".</summary>
        public static bool IsQuantityColumn(object header)
        {
            var h = Convert.ToString(header, CultureInfo.InvariantCulture) ?? "";
            return h.Contains("(m²)") || h.Contains("(m³)") || h.Contains("(m2)") || h.Contains("(m3)") || h.Contains("(m)");
        }

        /// <summary>
        /// KTF-02: the table as Excel gets it. The drawing tables write "12,50" (Vietnamese decimal comma); in the quantity
        /// columns (IsQuantityColumn) such a cell becomes the number 12.5 so Excel adds it up. Other cells and the drawing
        /// table itself are unchanged ("chưa đo được", "3 cặp" stay text).
        /// </summary>
        public static List<List<object>> ForExcel(List<List<object>> data)
        {
            if (data == null || data.Count == 0) return data;
            var head = data[0];
            var qty = new HashSet<int>(Enumerable.Range(0, head.Count).Where(c => IsQuantityColumn(head[c])));
            var out1 = new List<List<object>> { new List<object>(head) };
            for (int r = 1; r < data.Count; r++)
            {
                var row = new List<object>(data[r]);
                foreach (int c in qty)
                {
                    if (c >= row.Count || !(row[c] is string s)) continue;
                    var t = s.Trim();
                    if (CommaNumber.IsMatch(t) && double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) row[c] = n;
                }
                out1.Add(row);
            }
            return out1;
        }

        /// <summary>KTF-06: rows of data (not the header, not the closing "Tổng" row every schedule adds).</summary>
        public static int DataRows(List<List<object>> data)
        {
            if (data == null || data.Count <= 1) return 0;
            var last = data[data.Count - 1];
            bool total = last.Any(c => string.Equals(Convert.ToString(c, CultureInfo.InvariantCulture)?.Trim(), "Tổng", StringComparison.Ordinal));
            return data.Count - 1 - (total ? 1 : 0);
        }

        public static string ExistsMessage(string tool, string full) =>
            $"{tool}: '{full}' already exists. Nothing was written. Ask the user, then call again with overwrite: true, or give another file name in 'excel'.";
    }
}
