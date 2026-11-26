using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H6 (hạ tầng): the numbers of <c>terrain</c>, one place each (docs/ban-giao/nang-cap-h6.md).
    /// The first group follows the standards / practice (docs/dot-h-so-tieu-chuan.md, mục H6): approved by the owner 26/11
    /// ("dùng hết số đề xuất"); the skill ve-ha-tang and the tool description repeat them (change both with them).
    /// The second group are plain numbers ("Con số chờ chủ dự án duyệt" in the handoff).
    /// </summary>
    public static class H6Numbers
    {
        // ---------------------------------------------------------------- theo tiêu chuẩn / thông lệ: ĐÃ DUYỆT 26/11
        /// <summary>Long section (trắc dọc): horizontal / vertical print scales (1:1000 / 1:100).</summary>
        public static int ProfileScaleH = 1000;
        public static int ProfileScaleV = 100;
        /// <summary>Cross sections (trắc ngang): one print scale for both directions.</summary>
        public static int CrossScale = 200;
        /// <summary>Datum (mức so sánh, MSS): whole metres, at least this far below the lowest level.</summary>
        public static double DatumMarginM = 2;
        /// <summary>Stakes: detail (cọc chi tiết), hundred-metre (cọc H), kilometre (cọc Km). Plus start, end and vertices.</summary>
        public static double StakeDetailM = 20;
        public static double Stake100M = 100;
        public static double StakeKmM = 1000;
        /// <summary>Chainage ticks on the plan of the alignment (50 m; the sections use the 20 m stakes). Owner 26/11.</summary>
        public static double PlanTickM = 50;
        /// <summary>Cross-section width each side of the centre line when the user gives none.</summary>
        public static double CrossHalfWidthM = 15;
        /// <summary>Contour interval (khoảng cao đều) by the plan's print scale.</summary>
        public static readonly (int scale, double interval)[] ContourIntervals = { (500, 0.5), (1000, 1.0), (2000, 1.0) };
        /// <summary>Every n-th contour is a major one (đường cái: heavier, labelled).</summary>
        public static int MajorEvery = 5;
        /// <summary>Decimals: survey point level, levels on sections, distances, slopes (%).</summary>
        public static int DecPoint = 3;
        public static int DecSection = 2;
        public static int DecDistance = 2;
        public static int DecSlope = 2;
        /// <summary>Printed text heights (mm on paper): normal, title, big title.</summary>
        public static double TextMm = 2.5;
        public static double TitleMm = 3.5;
        public static double BigTitleMm = 5;
        /// <summary>VN-2000: only the axes are swapped (X = North → AutoCAD y; Y = East → AutoCAD x); no projection change.</summary>
        public static bool AxisSwap = true;
        /// <summary>Metres → drawing units (the drawing is in mm).</summary>
        public static double UnitsPerMetre = 1000;

        // ---------------------------------------------------------------- số thường (chờ duyệt chung ở bàn giao)
        public static int MaxPoints = 10000;
        public static int MaxFileMb = 20;
        public static int MaxStakes = 1000;
        public static int MaxSections = 200;
        public static int MaxLevels = 2000;
        /// <summary>Print scale of the plan when the user gives none (skill ve-ha-tang: 1:500).</summary>
        public static int PlanScaleDefault = 500;
        /// <summary>Cross sections: sampling step along the section, and the flatness under which a sample is dropped.</summary>
        public static double CrossSampleM = 1;
        public static double CrossFlatM = 0.01;
        /// <summary>Triangles with an edge longer than this × the median edge are dropped (no contours across empty ground).</summary>
        public static double MaxEdgeFactor = 4;
        /// <summary>Layout on paper (mm): table row of the long section / cross section, label column, tick on the plan, gaps.</summary>
        public static double ProfileRowMm = 10;
        public static double ProfileLabelMm = 45;
        public static double CrossRowMm = 8;
        public static double CrossLabelMm = 30;
        public static double PlanTickMm = 4;
        public static double GapMm = 15;
        public static int SectionsPerColumn = 5;
        /// <summary>Two survey points closer than this (m) are one point (the second is dropped).</summary>
        public static double SamePointM = 0.001;
        /// <summary>Coordinates bigger than this (m) are "real" VN-2000 ones: ask about the origin.</summary>
        public static double RealCoordinateM = 100000;

        // ---------------------------------------------------------------- layers (name, ACI colour)
        public static (string name, int color) LayerPoint = ("C-SURVEY-PT", 3);
        public static (string name, int color) LayerLevel = ("C-LEVEL", 2);
        public static (string name, int color) LayerPointName = ("C-COORD", 3);
        public static (string name, int color) LayerMinor = ("C-TOPO-MINR", 8);
        public static (string name, int color) LayerMajor = ("C-TOPO-MAJR", 33);
        public static (string name, int color) LayerStation = ("C-STATION", 2);
        public static (string name, int color) LayerGrid = ("C-PROF-GRID", 8);
        public static (string name, int color) LayerGround = ("C-PROF-GRND", 3);
        public static (string name, int color) LayerDesign = ("C-PROF-DSGN", 1);
        public static (string name, int color) LayerText = ("C-PROF-TEXT", 7);
        public static (string name, int color) LayerNote = ("C-TEXT", 7);

        public static double ContourIntervalFor(int scale)
        {
            if (scale <= 0) scale = PlanScaleDefault;
            var best = ContourIntervals[0];
            foreach (var c in ContourIntervals) if (Math.Abs(Math.Log((double)c.scale / scale)) < Math.Abs(Math.Log((double)best.scale / scale))) best = c;
            return best.interval;
        }
    }

    public sealed class SurveyPoint
    {
        public string Name;
        public double X, Y;
        public double? Z;
        public string Code;
        public int Row;
    }

    public sealed class SurveyFile
    {
        public List<SurveyPoint> Points = new List<SurveyPoint>();
        public List<string> Skipped = new List<string>();
        public List<string> Warnings = new List<string>();
        public string Columns = "";
        public bool HasZ;
    }

    /// <summary>Reads survey points from CSV / TXT (comma, semicolon, tab or spaces; decimal comma too) or the first sheet of an .xlsx.</summary>
    public static class SurveyReader
    {
        public const string Tool = "terrain";
        public static readonly string[] Extensions = { ".csv", ".txt", ".xlsx" };

        public static List<List<string>> ReadFile(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            if (!Extensions.Contains(ext))
                throw ToolException.Invalid($"{Tool}: '{Path.GetFileName(path)}' is not .csv / .txt / .xlsx. Save the survey sheet as CSV or .xlsx (an old .xls: Save As .xlsx).");
            if (!File.Exists(path)) throw ToolException.Invalid($"{Tool}: '{path}' does not exist.");
            long bytes = new FileInfo(path).Length;
            if (bytes > (long)H6Numbers.MaxFileMb * 1024 * 1024)
                throw ToolException.Invalid($"{Tool}: '{Path.GetFileName(path)}' is {bytes / 1048576} MB, more than {H6Numbers.MaxFileMb} MB for a survey sheet.");
            var data = File.ReadAllBytes(path);
            return ext == ".xlsx" ? ReadXlsx(data) : ParseText(DecodeText(data));
        }

        /// <summary>UTF-8 (with or without BOM), else the Windows code page of Vietnamese files (1258), else Latin.</summary>
        public static string DecodeText(byte[] data)
        {
            try { return new UTF8Encoding(false, true).GetString(data).TrimStart('﻿'); }
            catch (DecoderFallbackException) { }
            try { return Encoding.GetEncoding(1258).GetString(data); } catch (Exception) { }
            return Encoding.GetEncoding("iso-8859-1").GetString(data);
        }

        public static char? Delimiter(IList<string> lines)
        {
            char? best = null; int bestScore = 0;
            foreach (var d in new[] { '\t', ';', ',' })
            {
                var counts = lines.Take(20).Select(l => l.Count(c => c == d)).Where(c => c > 0).ToList();
                if (counts.Count == 0) continue;
                // the same number of separators on most lines
                int mode = counts.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
                int score = counts.Count(c => c == mode) * 10 + mode;
                if (score > bestScore) { best = d; bestScore = score; }
            }
            return best;
        }

        public static List<List<string>> ParseText(string text)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Trim().Length > 0).ToList();
            var d = Delimiter(lines);
            var rows = new List<List<string>>();
            foreach (var l in lines)
            {
                if (d == null) rows.Add(Regex.Split(l.Trim(), @"\s+").ToList());
                else rows.Add(SplitQuoted(l, d.Value));
            }
            // a decimal comma ("1234,567") only when the separator is not a comma
            if (d != ',')
                foreach (var r in rows)
                    for (int i = 0; i < r.Count; i++)
                        if (Regex.IsMatch(r[i].Trim(), @"^[+-]?\d+,\d+$")) r[i] = r[i].Trim().Replace(',', '.');
            return rows;
        }

        private static List<string> SplitQuoted(string line, char d)
        {
            var cells = new List<string>(); var sb = new StringBuilder(); bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
                else if (c == d && !q) { cells.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(c);
            }
            cells.Add(sb.ToString().Trim());
            return cells;
        }

        /// <summary>The first worksheet of an .xlsx: shared / inline strings and numbers (formulas: their cached value).</summary>
        public static List<List<string>> ReadXlsx(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    var shared = new List<string>();
                    var ss = zip.GetEntry("xl/sharedStrings.xml");
                    if (ss != null)
                    {
                        var doc = Load(ss);
                        foreach (XmlNode si in doc.GetElementsByTagName("si")) shared.Add(TextOf(si));
                    }
                    var sheet = zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(e => SheetNumber(e.FullName)).FirstOrDefault();
                    if (sheet == null) throw ToolException.Invalid($"{Tool}: the .xlsx has no worksheet.");
                    var sd = Load(sheet);
                    var rows = new List<List<string>>();
                    foreach (XmlNode row in sd.GetElementsByTagName("row"))
                    {
                        var cells = new List<string>();
                        foreach (XmlNode c in row.ChildNodes)
                        {
                            if (c.LocalName != "c") continue;
                            int col = ColumnIndex(c.Attributes?["r"]?.Value) ?? cells.Count;
                            while (cells.Count < col) cells.Add("");
                            string t = c.Attributes?["t"]?.Value ?? "";
                            string v = null;
                            foreach (XmlNode k in c.ChildNodes) if (k.LocalName == "v") v = k.InnerText;
                            string val;
                            if (t == "s") val = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int si) && si >= 0 && si < shared.Count ? shared[si] : "";
                            else if (t == "inlineStr") val = TextOf(c);
                            else val = v ?? "";
                            cells.Add(val.Trim());
                        }
                        if (cells.Any(x => x.Length > 0)) rows.Add(cells);
                    }
                    return rows;
                }
            }
            catch (ToolException) { throw; }
            catch (Exception e) when (e is InvalidDataException || e is XmlException || e is IOException)
            {
                throw ToolException.Invalid($"{Tool}: the .xlsx cannot be read ({e.Message}). Save it again from Excel, or as CSV.");
            }
        }

        private static int SheetNumber(string name)
        {
            var m = Regex.Match(name, @"sheet(\d+)\.xml$", RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : int.MaxValue;
        }

        private static XmlDocument Load(ZipArchiveEntry e)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var s = e.Open())
            using (var r = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc.Load(r);
            return doc;
        }

        private static string TextOf(XmlNode n)
        {
            var sb = new StringBuilder();
            foreach (XmlNode t in ((XmlElement)n).GetElementsByTagName("t")) sb.Append(t.InnerText);
            return sb.ToString();
        }

        /// <summary>"C12" → 2 (0-based column).</summary>
        public static int? ColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return null;
            int n = 0, i = 0;
            for (; i < cellRef.Length && char.IsLetter(cellRef[i]); i++) n = n * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
            return i == 0 ? (int?)null : n - 1;
        }

        public static bool TryNumber(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(" ", "");
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v);
        }

        /// <summary>Lower case, no Vietnamese marks, đ → d ("Cao độ" → "cao do").</summary>
        public static string Plain(string s)
        {
            var d = (s ?? "").Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return Regex.Replace(sb.ToString(), @"\s*\(.*?\)\s*", " ").Trim();
        }

        private static readonly string[] NameKeys = { "ten", "ten diem", "diem", "stt", "so hieu", "ma diem", "name", "point", "pt", "id", "so tt", "tt" };
        private static readonly string[] XKeys = { "x", "toa do x", "northing", "n" };
        private static readonly string[] YKeys = { "y", "toa do y", "easting", "e" };
        private static readonly string[] ZKeys = { "z", "h", "cao do", "do cao", "elevation", "elev", "cao do h", "cao do z" };
        private static readonly string[] CodeKeys = { "ma", "code", "ghi chu", "mo ta", "desc", "description", "loai" };

        public sealed class Columns { public int Name = -1, X = -1, Y = -1, Z = -1, Code = -1; }

        /// <summary>The header row (its index) and the columns it names; -1 when there is no header.</summary>
        public static int FindHeader(IList<List<string>> rows, out Columns cols)
        {
            cols = new Columns();
            for (int r = 0; r < Math.Min(rows.Count, 10); r++)
            {
                var c = new Columns();
                for (int i = 0; i < rows[r].Count; i++)
                {
                    var k = Plain(rows[r][i]);
                    if (k.Length == 0) continue;
                    if (c.X < 0 && XKeys.Contains(k)) c.X = i;
                    else if (c.Y < 0 && YKeys.Contains(k)) c.Y = i;
                    else if (c.Z < 0 && ZKeys.Contains(k)) c.Z = i;
                    else if (c.Name < 0 && NameKeys.Contains(k)) c.Name = i;
                    else if (c.Code < 0 && CodeKeys.Contains(k)) c.Code = i;
                }
                if (c.X >= 0 && c.Y >= 0) { cols = c; return r; }
            }
            return -1;
        }

        /// <summary>
        /// The points of the rows. Columns: given (0-based), else a header (X / Y / Z / Tên / Mã, Vietnamese or English),
        /// else by position: name, X, Y, Z[, code] (or X, Y, Z when the first column is a number and there are 3).
        /// </summary>
        public static SurveyFile Interpret(IList<List<string>> rows, Columns given = null)
        {
            var f = new SurveyFile();
            int start = 0;
            Columns c;
            if (given != null && given.X >= 0 && given.Y >= 0)
            {
                c = given;
                f.Columns = "as given";
                if (rows.Count > 0 && !TryNumber(Cell(rows[0], c.X), out _)) start = 1;
            }
            else
            {
                int h = FindHeader(rows, out c);
                if (h >= 0) { start = h + 1; f.Columns = "from the header row"; }
                else
                {
                    var first = rows.FirstOrDefault(r => r.Count >= 2) ?? new List<string>();
                    int n = first.Count;
                    bool firstNumber = TryNumber(Cell(first, 0), out _);
                    if (n == 2) c = new Columns { X = 0, Y = 1 };
                    else if (n == 3 && firstNumber) c = new Columns { X = 0, Y = 1, Z = 2 };
                    else if (n == 3) c = new Columns { Name = 0, X = 1, Y = 2 };
                    else c = new Columns { Name = 0, X = 1, Y = 2, Z = 3, Code = n >= 5 ? 4 : -1 };
                    f.Columns = "by position (no header row)";
                }
            }
            f.Columns += $": name {Show(c.Name)}, X {Show(c.X)}, Y {Show(c.Y)}, Z {Show(c.Z)}, code {Show(c.Code)}";
            f.HasZ = c.Z >= 0;
            int auto = 0;
            for (int r = start; r < rows.Count; r++)
            {
                var row = rows[r];
                if (!TryNumber(Cell(row, c.X), out double x) || !TryNumber(Cell(row, c.Y), out double y))
                {
                    f.Skipped.Add($"row {r + 1}: X / Y is not a number");
                    continue;
                }
                double? z = null;
                if (c.Z >= 0)
                {
                    if (TryNumber(Cell(row, c.Z), out double zz)) z = zz;
                    else { f.Skipped.Add($"row {r + 1}: Z is not a number"); continue; }
                }
                auto++;
                var name = c.Name >= 0 ? Cell(row, c.Name) : "";
                f.Points.Add(new SurveyPoint
                {
                    Name = name.Length > 0 ? name : auto.ToString(CultureInfo.InvariantCulture),
                    X = x, Y = y, Z = z, Code = c.Code >= 0 ? Cell(row, c.Code) : "", Row = r + 1,
                });
            }
            if (f.Points.Count > H6Numbers.MaxPoints)
                throw ToolException.Invalid($"{Tool}: {f.Points.Count} points, at most {H6Numbers.MaxPoints} in one call. Split the sheet.");
            var swapped = LooksSwapped(f.Points);
            if (swapped != null) f.Warnings.Add(swapped);
            return f;
        }

        private static string Show(int i) => i < 0 ? "-" : SurveyReaderColumn(i);
        private static string SurveyReaderColumn(int i) => XlsxWriter.Column(i);
        private static string Cell(IList<string> r, int i) => i >= 0 && i < r.Count ? (r[i] ?? "").Trim() : "";

        /// <summary>
        /// VN-2000 X is the Northing (about 900 000 – 2 600 000 m in Việt Nam), Y the Easting (about 100 000 – 900 000 m):
        /// columns the other way round look swapped. Only a warning: the points are not changed.
        /// </summary>
        public static string LooksSwapped(IList<SurveyPoint> pts)
        {
            if (pts.Count == 0) return null;
            double mx = Median(pts.Select(p => p.X)), my = Median(pts.Select(p => p.Y));
            if (mx >= 1e5 && mx < 9e5 && my >= 9e5 && my < 3e6)
                return $"X (median {mx:0}) looks like an Easting and Y (median {my:0}) like a Northing: in VN-2000 X is the Northing. The columns may be swapped; ask the user before going on.";
            return null;
        }

        public static double Median(IEnumerable<double> v)
        {
            var s = v.OrderBy(x => x).ToList();
            if (s.Count == 0) return 0;
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
        }

        /// <summary>Survey (m) → drawing (units): x = (Y − Y0) × k, y = (X − X0) × k with the VN-2000 swap; z = Z × k.</summary>
        public static (double x, double y, double z) ToDrawing(SurveyPoint p, double x0, double y0, bool swap)
        {
            double k = H6Numbers.UnitsPerMetre;
            double dx = p.X - x0, dy = p.Y - y0;
            return swap ? (dy * k, dx * k, (p.Z ?? 0) * k) : (dx * k, dy * k, (p.Z ?? 0) * k);
        }

        /// <summary>"auto" origin: the lowest X / Y rounded down to 100 m.</summary>
        public static (double x0, double y0) AutoOrigin(IList<SurveyPoint> pts) =>
            (Math.Floor(pts.Min(p => p.X) / 100) * 100, Math.Floor(pts.Min(p => p.Y) / 100) * 100);

        public static bool IsReal(IList<SurveyPoint> pts) => pts.Any(p => Math.Abs(p.X) > H6Numbers.RealCoordinateM || Math.Abs(p.Y) > H6Numbers.RealCoordinateM);

        /// <summary>Points closer than SamePointM to an earlier one are dropped (a TIN needs distinct points).</summary>
        public static List<int> Duplicates(IList<(double x, double y)> pts, double tol)
        {
            var seen = new Dictionary<(long, long), List<int>>();
            var dup = new List<int>();
            double cell = Math.Max(tol, 1e-9);
            for (int i = 0; i < pts.Count; i++)
            {
                long cx = (long)Math.Floor(pts[i].x / cell), cy = (long)Math.Floor(pts[i].y / cell);
                bool same = false;
                for (long a = cx - 1; a <= cx + 1 && !same; a++)
                    for (long b = cy - 1; b <= cy + 1 && !same; b++)
                        if (seen.TryGetValue((a, b), out var l))
                            foreach (var j in l)
                                if (Math.Abs(pts[j].x - pts[i].x) <= tol && Math.Abs(pts[j].y - pts[i].y) <= tol) { same = true; break; }
                if (same) { dup.Add(i); continue; }
                if (!seen.TryGetValue((cx, cy), out var list)) seen[(cx, cy)] = list = new List<int>();
                list.Add(i);
            }
            return dup;
        }
    }

    /// <summary>Delaunay triangulation of the survey points (Bowyer–Watson with an x sweep) and linear interpolation on it.</summary>
    public sealed class Tin
    {
        public double[] X, Y, Z;
        public List<int[]> Triangles = new List<int[]>();
        public int Dropped;
        private double _gx0, _gy0, _cell; private int _nx, _ny; private List<int>[] _grid;

        public double MinZ => Z.Length == 0 ? 0 : Z.Min();
        public double MaxZ => Z.Length == 0 ? 0 : Z.Max();

        /// <param name="maxEdge">triangles with a longer edge are dropped; 0 = none; negative = MaxEdgeFactor × the median edge</param>
        public static Tin Build(IList<(double x, double y, double z)> pts, double maxEdge = -1)
        {
            var t = new Tin { X = pts.Select(p => p.x).ToArray(), Y = pts.Select(p => p.y).ToArray(), Z = pts.Select(p => p.z).ToArray() };
            int n = pts.Count;
            if (n < 3) throw ToolException.Invalid("terrain: at least 3 points with a level are needed for a surface.");
            // shifted to the centre for precision (VN-2000 coordinates in mm are 10^9)
            double cx = t.X.Average(), cy = t.Y.Average();
            var xs = t.X.Select(v => v - cx).ToArray(); var ys = t.Y.Select(v => v - cy).ToArray();
            double span = Math.Max(xs.Max() - xs.Min(), ys.Max() - ys.Min());
            if (span <= 0) throw ToolException.Invalid("terrain: the points are all at one place.");
            var order = Enumerable.Range(0, n).OrderBy(i => xs[i]).ThenBy(i => ys[i]).ToArray();
            double big = span * 20;
            var vx = new double[n + 3]; var vy = new double[n + 3];
            Array.Copy(xs, vx, n); Array.Copy(ys, vy, n);
            vx[n] = -big; vy[n] = -big; vx[n + 1] = big; vy[n + 1] = -big; vx[n + 2] = 0; vy[n + 2] = big;
            var open = new List<Tri> { Tri.Make(n, n + 1, n + 2, vx, vy) };
            var done = new List<Tri>();
            var edges = new Dictionary<(int, int), int>();
            foreach (var p in order)
            {
                double px = vx[p], py = vy[p];
                edges.Clear();
                for (int k = open.Count - 1; k >= 0; k--)
                {
                    var tr = open[k];
                    double dx = px - tr.Cx;
                    if (dx > 0 && dx * dx > tr.R2) { done.Add(tr); open.RemoveAt(k); continue; }
                    double dy = py - tr.Cy;
                    if (dx * dx + dy * dy <= tr.R2 * (1 + 1e-12))
                    {
                        AddEdge(edges, tr.A, tr.B); AddEdge(edges, tr.B, tr.C); AddEdge(edges, tr.C, tr.A);
                        open.RemoveAt(k);
                    }
                }
                foreach (var e in edges) if (e.Value == 1) open.Add(Tri.Make(e.Key.Item1, e.Key.Item2, p, vx, vy));
            }
            done.AddRange(open);
            var all = done.Where(tr => tr.A < n && tr.B < n && tr.C < n && tr.Area2(vx, vy) > span * span * 1e-14).Select(tr => new[] { tr.A, tr.B, tr.C }).ToList();
            if (all.Count == 0) throw ToolException.Invalid("terrain: the points are on one line: no surface can be made.");
            if (maxEdge < 0)
            {
                var lens = new List<double>();
                foreach (var tr in all) for (int k = 0; k < 3; k++) lens.Add(Dist(t, tr[k], tr[(k + 1) % 3]));
                maxEdge = H6Numbers.MaxEdgeFactor * SurveyReader.Median(lens);
            }
            if (maxEdge > 0)
            {
                int before = all.Count;
                all = all.Where(tr => Dist(t, tr[0], tr[1]) <= maxEdge && Dist(t, tr[1], tr[2]) <= maxEdge && Dist(t, tr[2], tr[0]) <= maxEdge).ToList();
                t.Dropped = before - all.Count;
                if (all.Count == 0) throw ToolException.Invalid("terrain: every triangle is longer than max_edge: give a bigger max_edge (or 0).");
            }
            t.Triangles = all;
            t.Index();
            return t;
        }

        private static double Dist(Tin t, int a, int b) => Math.Sqrt((t.X[a] - t.X[b]) * (t.X[a] - t.X[b]) + (t.Y[a] - t.Y[b]) * (t.Y[a] - t.Y[b]));

        private static void AddEdge(Dictionary<(int, int), int> edges, int a, int b)
        {
            var k = a < b ? (a, b) : (b, a);
            edges[k] = edges.TryGetValue(k, out int c) ? c + 1 : 1;
        }

        private struct Tri
        {
            public int A, B, C; public double Cx, Cy, R2;
            public static Tri Make(int a, int b, int c, double[] x, double[] y)
            {
                double ax = x[a], ay = y[a], bx = x[b], by = y[b], cx = x[c], cy = y[c];
                double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                var t = new Tri { A = a, B = b, C = c };
                if (Math.Abs(d) < 1e-300) { t.Cx = (ax + bx + cx) / 3; t.Cy = (ay + by + cy) / 3; t.R2 = double.MaxValue; return t; }
                double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
                t.Cx = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
                t.Cy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
                t.R2 = (ax - t.Cx) * (ax - t.Cx) + (ay - t.Cy) * (ay - t.Cy);
                return t;
            }
            public double Area2(double[] x, double[] y) => Math.Abs((x[B] - x[A]) * (y[C] - y[A]) - (x[C] - x[A]) * (y[B] - y[A]));
        }

        private void Index()
        {
            double minx = X.Min(), maxx = X.Max(), miny = Y.Min(), maxy = Y.Max();
            double area = Math.Max((maxx - minx) * (maxy - miny), 1e-9);
            _cell = Math.Max(Math.Sqrt(area / Math.Max(Triangles.Count, 1)) * 2, 1e-6);
            _gx0 = minx; _gy0 = miny;
            _nx = Math.Max(1, (int)Math.Ceiling((maxx - minx) / _cell) + 1);
            _ny = Math.Max(1, (int)Math.Ceiling((maxy - miny) / _cell) + 1);
            _grid = new List<int>[_nx * _ny];
            for (int k = 0; k < Triangles.Count; k++)
            {
                var tr = Triangles[k];
                int i0 = CellX(Math.Min(X[tr[0]], Math.Min(X[tr[1]], X[tr[2]]))), i1 = CellX(Math.Max(X[tr[0]], Math.Max(X[tr[1]], X[tr[2]])));
                int j0 = CellY(Math.Min(Y[tr[0]], Math.Min(Y[tr[1]], Y[tr[2]]))), j1 = CellY(Math.Max(Y[tr[0]], Math.Max(Y[tr[1]], Y[tr[2]])));
                for (int i = i0; i <= i1; i++)
                    for (int j = j0; j <= j1; j++)
                        (_grid[j * _nx + i] ?? (_grid[j * _nx + i] = new List<int>())).Add(k);
            }
        }

        private int CellX(double x) => Math.Min(_nx - 1, Math.Max(0, (int)Math.Floor((x - _gx0) / _cell)));
        private int CellY(double y) => Math.Min(_ny - 1, Math.Max(0, (int)Math.Floor((y - _gy0) / _cell)));

        /// <summary>The level at (x, y) on the surface, or null outside it.</summary>
        public double? ZAt(double x, double y)
        {
            if (x < _gx0 - _cell || y < _gy0 - _cell || x > _gx0 + _nx * _cell || y > _gy0 + _ny * _cell) return null;
            var l = _grid[CellY(y) * _nx + CellX(x)];
            if (l == null) return null;
            foreach (var k in l)
            {
                var tr = Triangles[k];
                double x1 = X[tr[0]], y1 = Y[tr[0]], x2 = X[tr[1]], y2 = Y[tr[1]], x3 = X[tr[2]], y3 = Y[tr[2]];
                double d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3);
                if (Math.Abs(d) < 1e-300) continue;
                double a = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / d;
                double b = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / d;
                double c = 1 - a - b;
                const double eps = -1e-9;
                if (a >= eps && b >= eps && c >= eps) return a * Z[tr[0]] + b * Z[tr[1]] + c * Z[tr[2]];
            }
            return null;
        }
    }

    public sealed class ContourLine
    {
        public double Level;
        public bool Major;
        public bool Closed;
        public List<(double x, double y)> Points = new List<(double x, double y)>();
    }

    public static class Contouring
    {
        /// <summary>The levels k × interval between the lowest and highest point (k a whole number), majors every majorEvery.</summary>
        public static List<(double level, bool major)> Levels(double minZ, double maxZ, double interval, int majorEvery)
        {
            if (!(interval > 0)) throw ToolException.Invalid("terrain: interval must be more than 0.");
            long k0 = (long)Math.Ceiling(minZ / interval - 1e-9), k1 = (long)Math.Floor(maxZ / interval + 1e-9);
            if (k1 - k0 + 1 > H6Numbers.MaxLevels)
                throw ToolException.Invalid($"terrain: {k1 - k0 + 1} contour levels with interval {interval} m (at most {H6Numbers.MaxLevels}): give a bigger interval.");
            var r = new List<(double, bool)>();
            for (long k = k0; k <= k1; k++) r.Add((Math.Round(k * interval, 9), majorEvery > 0 && k % majorEvery == 0));
            return r;
        }

        /// <summary>The contour lines of the TIN (level in the same units as Z), chained into polylines.</summary>
        public static List<ContourLine> Trace(Tin t, double interval, int majorEvery)
        {
            var result = new List<ContourLine>();
            foreach (var (level, major) in Levels(t.MinZ, t.MaxZ, interval, majorEvery))
            {
                // segments between edge crossings; a vertex exactly on the level counts as above it
                var pos = new Dictionary<(int, int), (double x, double y)>();
                var links = new Dictionary<(int, int), List<(int, int)>>();
                foreach (var tr in t.Triangles)
                {
                    var cross = new List<(int, int)>(2);
                    for (int k = 0; k < 3; k++)
                    {
                        int a = tr[k], b = tr[(k + 1) % 3];
                        bool aa = t.Z[a] >= level, bb = t.Z[b] >= level;
                        if (aa == bb) continue;
                        var key = a < b ? (a, b) : (b, a);
                        if (!pos.ContainsKey(key))
                        {
                            double f = (level - t.Z[a]) / (t.Z[b] - t.Z[a]);
                            pos[key] = (t.X[a] + f * (t.X[b] - t.X[a]), t.Y[a] + f * (t.Y[b] - t.Y[a]));
                        }
                        cross.Add(key);
                    }
                    if (cross.Count != 2) continue;
                    Link(links, cross[0], cross[1]);
                    Link(links, cross[1], cross[0]);
                }
                var used = new HashSet<(int, int)>();
                // open lines first (they start at an edge with one link), then the closed ones
                foreach (var start in links.Keys.Where(k => links[k].Count == 1).Concat(links.Keys.ToList()))
                {
                    if (used.Contains(start)) continue;
                    var line = new ContourLine { Level = level, Major = major };
                    var cur = start; (int, int)? prev = null;
                    while (true)
                    {
                        used.Add(cur);
                        line.Points.Add(pos[cur]);
                        (int, int)? next = null;
                        foreach (var nb in links[cur]) if (!used.Contains(nb) && (prev == null || !nb.Equals(prev.Value))) { next = nb; break; }
                        if (next == null)
                        {
                            if (links[cur].Count == 2 && links[cur].Contains(start) && line.Points.Count > 2) line.Closed = true;
                            break;
                        }
                        prev = cur; cur = next.Value;
                    }
                    if (line.Points.Count >= 2) result.Add(line);
                }
            }
            return result;
        }

        private static void Link(Dictionary<(int, int), List<(int, int)>> l, (int, int) a, (int, int) b)
        {
            if (!l.TryGetValue(a, out var list)) l[a] = list = new List<(int, int)>(2);
            list.Add(b);
        }

        /// <summary>Where to write the level of a line: the middle of its longest segment, text along it (kept readable).</summary>
        public static (double x, double y, double angle, double length) LabelSpot(IList<(double x, double y)> pts)
        {
            double best = -1, bx = 0, by = 0, ang = 0, total = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                double dx = pts[i + 1].x - pts[i].x, dy = pts[i + 1].y - pts[i].y, l = Math.Sqrt(dx * dx + dy * dy);
                total += l;
                if (l > best) { best = l; bx = (pts[i].x + pts[i + 1].x) / 2; by = (pts[i].y + pts[i + 1].y) / 2; ang = Math.Atan2(dy, dx); }
            }
            return (bx, by, Readable(ang), total);
        }

        /// <summary>An angle turned by 180° when the text would read upside down.</summary>
        public static double Readable(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a <= -Math.PI) a += 2 * Math.PI;
            if (a > Math.PI / 2 + 1e-9) a -= Math.PI;
            else if (a <= -Math.PI / 2 + 1e-9) a += Math.PI;
            return a;
        }
    }

    public sealed class Stake
    {
        /// <summary>Distance from the start of the alignment (m).</summary>
        public double S;
        /// <summary>Chainage (lý trình, m) = start chainage + S.</summary>
        public double Ch;
        public string Name;
        public string Kind; // km, h, detail, vertex, start, end
    }

    public static class Stations
    {
        /// <summary>"Km0+125.50": km and metres (DecDistance decimals).</summary>
        public static string Chainage(double ch, int decimals = -1)
        {
            if (decimals < 0) decimals = H6Numbers.DecDistance;
            double r = Math.Round(ch, decimals);
            string sign = r < 0 ? "-" : "";
            r = Math.Abs(r);
            double km = Math.Floor(r / H6Numbers.StakeKmM + 1e-12);
            double m = Math.Round(r - km * H6Numbers.StakeKmM, decimals);
            if (m >= H6Numbers.StakeKmM) { km += 1; m = 0; }
            string mf = m.ToString((decimals > 0 ? "000." + new string('0', decimals) : "000"), CultureInfo.InvariantCulture);
            return $"{sign}Km{km.ToString("0", CultureInfo.InvariantCulture)}+{mf}";
        }

        /// <summary>"+12.350" / "-0.20": a level with its sign.</summary>
        public static string Level(double z, int decimals)
        {
            double r = Math.Round(z, decimals);
            if (r == 0) r = 0; // no "-0.000"
            string f = r.ToString("0." + new string('0', Math.Max(decimals, 1)), CultureInfo.InvariantCulture);
            if (decimals == 0) f = r.ToString("0", CultureInfo.InvariantCulture);
            return r >= 0 ? "+" + f : f;
        }

        public static string Number(double v, int decimals) =>
            Math.Round(v, decimals).ToString(decimals > 0 ? "0." + new string('0', decimals) : "0", CultureInfo.InvariantCulture);

        private static bool IsMultiple(double v, double step) => step > 0 && Math.Abs(v / step - Math.Round(v / step)) * step < 1e-6;

        /// <summary>
        /// The stakes of an alignment of <paramref name="length"/> m: start, every <paramref name="step"/> m of chainage
        /// (Km at whole kilometres, H1…H9 at hundreds, C1, C2… between), the vertices (Đ1, Đ2…) and the end.
        /// </summary>
        public static List<Stake> Stakes(double length, IList<double> vertexS, double startCh, double step)
        {
            if (!(length > 0)) throw ToolException.Invalid("terrain: the alignment has no length.");
            if (!(step > 0)) throw ToolException.Invalid("terrain: step must be more than 0.");
            var list = new List<Stake>();
            double endCh = startCh + length;
            long k0 = (long)Math.Ceiling(startCh / step - 1e-9), k1 = (long)Math.Floor(endCh / step + 1e-9);
            if (k1 - k0 + 3 + (vertexS?.Count ?? 0) > H6Numbers.MaxStakes)
                throw ToolException.Invalid($"terrain: more than {H6Numbers.MaxStakes} stakes: give a bigger step, or part of the alignment.");
            for (long k = k0; k <= k1; k++)
            {
                double ch = Math.Round(k * step, 9);
                list.Add(new Stake { S = ch - startCh, Ch = ch });
            }
            if (list.Count == 0 || list[0].S > 1e-6) list.Insert(0, new Stake { S = 0, Ch = startCh, Kind = "start" });
            if (list[list.Count - 1].S < length - 1e-6) list.Add(new Stake { S = length, Ch = endCh, Kind = "end" });
            int v = 0;
            foreach (var s in (vertexS ?? new double[0]).Where(s => s > 1e-6 && s < length - 1e-6).OrderBy(s => s))
            {
                v++;
                var near = list.FirstOrDefault(x => Math.Abs(x.S - s) < 0.01);
                if (near != null) { near.Name = null; near.Kind = (near.Kind ?? "") + "+vertex" + v; continue; }
                list.Add(new Stake { S = s, Ch = startCh + s, Kind = "vertex" + v });
            }
            list = list.OrderBy(x => x.S).ToList();
            int detail = 0;
            foreach (var s in list)
            {
                string kind = s.Kind ?? "";
                string vertex = null;
                var m = Regex.Match(kind, @"vertex(\d+)");
                if (m.Success) vertex = "Đ" + m.Groups[1].Value;
                string regular;
                if (IsMultiple(s.Ch, H6Numbers.StakeKmM)) { regular = "Km" + Math.Round(s.Ch / H6Numbers.StakeKmM).ToString("0", CultureInfo.InvariantCulture); s.Kind = "km"; }
                else if (IsMultiple(s.Ch, H6Numbers.Stake100M))
                {
                    double inKm = s.Ch - Math.Floor(s.Ch / H6Numbers.StakeKmM + 1e-9) * H6Numbers.StakeKmM;
                    regular = "H" + Math.Round(inKm / H6Numbers.Stake100M).ToString("0", CultureInfo.InvariantCulture); s.Kind = "h";
                }
                else if (kind == "start") { regular = "Đầu"; }
                else if (kind == "end") { regular = "Cuối"; }
                else if (kind.StartsWith("vertex", StringComparison.Ordinal)) { regular = null; s.Kind = "vertex"; }
                else { detail++; regular = "C" + detail.ToString(CultureInfo.InvariantCulture); s.Kind = "detail"; }
                s.Name = regular == null ? vertex : (vertex != null ? regular + "/" + vertex : regular);
            }
            return list;
        }

        /// <summary>The datum (MSS): whole metres at least DatumMarginM below the lowest level.</summary>
        public static double Datum(IEnumerable<double?> levels, double margin = -1)
        {
            if (margin < 0) margin = H6Numbers.DatumMarginM;
            var v = levels.Where(z => z.HasValue).Select(z => z.Value).ToList();
            if (v.Count == 0) throw ToolException.Invalid("terrain: no stake is on the surface (the alignment is outside the survey points).");
            return Math.Floor(v.Min() - margin + 1e-9);
        }

        /// <summary>The design level at a chainage from [chainage, level] pairs (straight between them); null outside.</summary>
        public static double? DesignAt(IList<(double ch, double z)> design, double ch)
        {
            if (design == null || design.Count == 0) return null;
            var d = design.OrderBy(p => p.ch).ToList();
            if (ch < d[0].ch - 1e-6 || ch > d[d.Count - 1].ch + 1e-6) return null;
            for (int i = 0; i + 1 < d.Count; i++)
                if (ch <= d[i + 1].ch + 1e-9)
                {
                    double l = d[i + 1].ch - d[i].ch;
                    return l <= 0 ? d[i].z : d[i].z + (d[i + 1].z - d[i].z) * (ch - d[i].ch) / l;
                }
            return d[d.Count - 1].z;
        }

        /// <summary>Slopes (%) of the design between its points: (from ch, to ch, %).</summary>
        public static List<(double from, double to, double pct)> Grades(IList<(double ch, double z)> design)
        {
            var d = (design ?? new List<(double, double)>()).OrderBy(p => p.ch).ToList();
            var r = new List<(double, double, double)>();
            for (int i = 0; i + 1 < d.Count; i++)
            {
                double l = d[i + 1].ch - d[i].ch;
                if (l > 0) r.Add((d[i].ch, d[i + 1].ch, (d[i + 1].z - d[i].z) / l * 100));
            }
            return r;
        }
    }

    /// <summary>A piece of a section drawing, in drawing units: a line / polyline, or a text (middle-centre aligned).</summary>
    public sealed class Prim
    {
        public string Kind; // line, pline, text
        public string Layer;
        public List<(double x, double y)> Pts = new List<(double x, double y)>();
        public string Text;
        public double Height;
        /// <summary>Radians.</summary>
        public double Rotation;
        /// <summary>Text: 0 middle-centre, 1 middle-left, 2 middle-right.</summary>
        public int Align;

        public static Prim Line(string layer, double x1, double y1, double x2, double y2) =>
            new Prim { Kind = "line", Layer = layer, Pts = { (x1, y1), (x2, y2) } };
        public static Prim Txt(string layer, double x, double y, string text, double h, double rot = 0, int align = 0) =>
            new Prim { Kind = "text", Layer = layer, Pts = { (x, y) }, Text = text, Height = h, Rotation = rot, Align = align };
    }

    public sealed class SectionDrawing
    {
        public List<Prim> Prims = new List<Prim>();
        public double Width, Height, Datum;
    }

    public static class SectionLayout
    {
        private static string G => H6Numbers.LayerGrid.name;
        private static string T => H6Numbers.LayerText.name;

        /// <summary>
        /// The long section (trắc dọc) with its table, lower-left corner at (bx, by). Horizontal: true length (drawn at the
        /// horizontal print scale), vertical: exaggerated scaleH / scaleV times. Texts sized for the horizontal scale.
        /// </summary>
        public static SectionDrawing Profile(IList<Stake> stakes, IList<double?> ground, IList<double?> design, double bx, double by,
            int scaleH, int scaleV, double? datum, string title, IList<(double from, double to, double pct)> grades = null)
        {
            if (scaleH <= 0 || scaleV <= 0) throw ToolException.Invalid("terrain: scales must be more than 0.");
            double k = H6Numbers.UnitsPerMetre, ex = (double)scaleH / scaleV;
            double th = H6Numbers.TextMm * scaleH, rowH = H6Numbers.ProfileRowMm * scaleH, labelW = H6Numbers.ProfileLabelMm * scaleH;
            bool hasDesign = design != null && design.Any(z => z.HasValue);
            double dat = datum ?? Stations.Datum(ground.Concat(hasDesign ? design : new double?[0]));
            var rows = new List<string>();
            if (hasDesign) rows.Add("Cao độ thiết kế (m)");
            rows.Add("Cao độ tự nhiên (m)");
            if (hasDesign) rows.Add("Chênh cao TK - TN (m)");
            rows.Add("Khoảng cách lẻ (m)");
            rows.Add("Khoảng cách cộng dồn (m)");
            rows.Add("Tên cọc");
            int nr = rows.Count;
            double s0 = stakes[0].S, len = stakes[stakes.Count - 1].S - s0;
            double x0 = bx + labelW, x1 = x0 + len * k, top = by + nr * rowH;
            double X(double s) => x0 + (s - s0) * k;
            double Y(double z) => top + (z - dat) * k * ex;
            var d = new SectionDrawing { Datum = dat };
            var P = d.Prims;
            // the table: rows top to bottom, label column
            for (int i = 0; i <= nr; i++) P.Add(Prim.Line(G, bx, by + i * rowH, x1, by + i * rowH));
            P.Add(Prim.Line(G, bx, by, bx, top)); P.Add(Prim.Line(G, x0, by, x0, top)); P.Add(Prim.Line(G, x1, by, x1, top));
            for (int i = 0; i < nr; i++) P.Add(Prim.Txt(T, bx + th * 0.6, top - (i + 0.5) * rowH, rows[i], th, 0, 1));
            double RowY(string name) => top - (rows.IndexOf(name) + 0.5) * rowH;
            double maxY = top;
            for (int i = 0; i < stakes.Count; i++)
            {
                var st = stakes[i]; double x = X(st.S);
                double? g = ground[i], dz = hasDesign ? design[i] : null;
                double yTop = Math.Max(g.HasValue ? Y(g.Value) : top, dz.HasValue ? Y(dz.Value) : top);
                maxY = Math.Max(maxY, yTop);
                P.Add(Prim.Line(G, x, by, x, yTop));
                const double up = Math.PI / 2;
                P.Add(Prim.Txt(T, x - th * 0.7, RowY("Tên cọc"), st.Name, th, up));
                P.Add(Prim.Txt(T, x - th * 0.7, RowY("Khoảng cách cộng dồn (m)"), Stations.Number(st.S, H6Numbers.DecDistance), th, up));
                P.Add(Prim.Txt(T, x - th * 0.7, RowY("Cao độ tự nhiên (m)"), g.HasValue ? Stations.Number(g.Value, H6Numbers.DecSection) : "-", th, up));
                if (hasDesign)
                {
                    P.Add(Prim.Txt(T, x - th * 0.7, RowY("Cao độ thiết kế (m)"), dz.HasValue ? Stations.Number(dz.Value, H6Numbers.DecSection) : "-", th, up));
                    P.Add(Prim.Txt(T, x - th * 0.7, RowY("Chênh cao TK - TN (m)"), dz.HasValue && g.HasValue ? Stations.Level(dz.Value - g.Value, H6Numbers.DecSection) : "-", th, up));
                }
                if (i > 0)
                {
                    double xm = (X(stakes[i - 1].S) + x) / 2;
                    P.Add(Prim.Txt(T, xm, RowY("Khoảng cách lẻ (m)"), Stations.Number(st.S - stakes[i - 1].S, H6Numbers.DecDistance), th * 0.9, 0));
                }
            }
            // the datum line, the ground and the design lines (broken where a stake is off the surface)
            P.Add(Prim.Line(G, bx, top, x1, top));
            P.Add(Prim.Txt(T, bx + th * 0.6, top + th, "MSS " + Stations.Level(dat, H6Numbers.DecSection), th, 0, 1));
            AddRuns(P, H6Numbers.LayerGround.name, stakes, ground, X, Y);
            if (hasDesign)
            {
                AddRuns(P, H6Numbers.LayerDesign.name, stakes, design, X, Y);
                foreach (var gr in grades ?? new List<(double, double, double)>())
                {
                    double sa = gr.from - (stakes[0].Ch - stakes[0].S), sb = gr.to - (stakes[0].Ch - stakes[0].S);
                    if (sb < s0 - 1e-6 || sa > stakes[stakes.Count - 1].S + 1e-6) continue;
                    double xa = X(Math.Max(sa, s0)), xb = X(Math.Min(sb, stakes[stakes.Count - 1].S));
                    P.Add(Prim.Txt(T, (xa + xb) / 2, maxY + th * 2, "i = " + Stations.Number(gr.pct, H6Numbers.DecSlope) + "%", th));
                }
            }
            double titleY = maxY + th * (hasDesign ? 5 : 3);
            P.Add(Prim.Txt(T, (bx + x1) / 2, titleY, title, H6Numbers.BigTitleMm * scaleH));
            P.Add(Prim.Txt(T, (bx + x1) / 2, titleY - H6Numbers.BigTitleMm * scaleH * 1.4,
                $"Tỉ lệ ngang 1:{scaleH}, tỉ lệ đứng 1:{scaleV}", H6Numbers.TitleMm * scaleH));
            d.Width = x1 - bx; d.Height = titleY + H6Numbers.BigTitleMm * scaleH - by;
            return d;
        }

        private static void AddRuns(List<Prim> P, string layer, IList<Stake> stakes, IList<double?> z, Func<double, double> X, Func<double, double> Y)
        {
            Prim cur = null;
            for (int i = 0; i < stakes.Count; i++)
            {
                if (!z[i].HasValue) { cur = null; continue; }
                if (cur == null) { cur = new Prim { Kind = "pline", Layer = layer }; P.Add(cur); }
                cur.Pts.Add((X(stakes[i].S), Y(z[i].Value)));
            }
            P.RemoveAll(p => p.Kind == "pline" && p.Pts.Count < 2);
        }

        /// <summary>The ground points of a cross section kept where the slope changes (samples on one straight line dropped).</summary>
        public static List<(double off, double z)> Breakpoints(IList<(double off, double? z)> samples, double flat = -1)
        {
            if (flat < 0) flat = H6Numbers.CrossFlatM;
            var s = samples.Where(p => p.z.HasValue).Select(p => (p.off, z: p.z.Value)).ToList();
            if (s.Count <= 2) return s;
            var keep = new List<(double off, double z)> { s[0] };
            for (int i = 1; i + 1 < s.Count; i++)
            {
                var a = keep[keep.Count - 1]; var b = s[i + 1];
                double t = (s[i].off - a.off) / (b.off - a.off);
                double zl = a.z + t * (b.z - a.z);
                if (Math.Abs(s[i].z - zl) > flat) keep.Add(s[i]);
            }
            keep.Add(s[s.Count - 1]);
            return keep;
        }

        /// <summary>One cross section, the centre line at x = cx, the bottom of its table at y = by. Same scale both ways.</summary>
        public static SectionDrawing Cross(Stake st, IList<(double off, double z)> pts, double halfWidth, double cx, double by, int scale, double? datum)
        {
            double k = H6Numbers.UnitsPerMetre, th = H6Numbers.TextMm * scale, rowH = H6Numbers.CrossRowMm * scale, labelW = H6Numbers.CrossLabelMm * scale;
            var d = new SectionDrawing();
            if (pts.Count == 0) return d;
            double dat = datum ?? Stations.Datum(pts.Select(p => (double?)p.z));
            d.Datum = dat;
            double left = cx - halfWidth * k, right = cx + halfWidth * k, bx = left - labelW, top = by + 2 * rowH;
            double X(double off) => cx + off * k;
            double Y(double z) => top + (z - dat) * k;
            var P = d.Prims;
            for (int i = 0; i <= 2; i++) P.Add(Prim.Line(G, bx, by + i * rowH, right, by + i * rowH));
            P.Add(Prim.Line(G, bx, by, bx, top)); P.Add(Prim.Line(G, left, by, left, top)); P.Add(Prim.Line(G, right, by, right, top));
            P.Add(Prim.Txt(T, bx + th * 0.5, top - 0.5 * rowH, "Cao độ TN (m)", th, 0, 1));
            P.Add(Prim.Txt(T, bx + th * 0.5, top - 1.5 * rowH, "Khoảng cách (m)", th, 0, 1));
            double maxZ = pts.Max(p => p.z);
            for (int i = 0; i < pts.Count; i++)
            {
                double x = X(pts[i].off);
                P.Add(Prim.Line(G, x, by + rowH, x, Y(pts[i].z)));
                P.Add(Prim.Txt(T, x - th * 0.7, top - 0.5 * rowH, Stations.Number(pts[i].z, H6Numbers.DecSection), th * 0.9, Math.PI / 2));
                if (i > 0) P.Add(Prim.Txt(T, (X(pts[i - 1].off) + x) / 2, top - 1.5 * rowH, Stations.Number(pts[i].off - pts[i - 1].off, H6Numbers.DecDistance), th * 0.8));
            }
            P.Add(new Prim { Kind = "pline", Layer = H6Numbers.LayerGround.name, Pts = pts.Select(p => (X(p.off), Y(p.z))).ToList() });
            P.Add(Prim.Line(G, bx, top, right, top));
            double yc = Y(maxZ) + th * 2;
            P.Add(Prim.Line(H6Numbers.LayerDesign.name, cx, top, cx, yc)); // the centre line (tim tuyến)
            P.Add(Prim.Txt(T, bx + th * 0.5, top + th, "MSS " + Stations.Level(dat, H6Numbers.DecSection), th, 0, 1));
            P.Add(Prim.Txt(T, cx, yc + th * 2, $"Cọc {st.Name}  {Stations.Chainage(st.Ch)}", H6Numbers.TitleMm * scale));
            d.Width = right - bx; d.Height = yc + th * 3 + H6Numbers.TitleMm * scale - by;
            return d;
        }
    }
}
