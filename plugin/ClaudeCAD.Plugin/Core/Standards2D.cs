using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F2 (25/11/2026), AutoCAD-free part of the 2D daily tools: setup_standards (text / dimension styles,
    /// linetypes matched to the print scale), create_sheet (a paper layout with border, title block and a scaled
    /// viewport), insert_library_block (blocks drawn by the project's own code) and layer_state (named layer states).
    /// Every number a customer sees is a named constant in this file (F2Numbers) so the owner can change it in one place.
    /// </summary>
    public static class F2Numbers
    {
        // ---------------------------------------------------------------- styles (setup_standards), paper mm
        /// <summary>Text height of dimensions on paper (mm).</summary>
        public const double DimTextHeight = 2.5;
        /// <summary>Architectural tick size on paper (mm).</summary>
        public const double DimArrowSize = 1.2;
        /// <summary>Gap between the object and the extension line (DIMEXO, paper mm).</summary>
        public const double DimExtOffset = 2;
        /// <summary>Extension line beyond the dimension line (DIMEXE, paper mm).</summary>
        public const double DimExtBeyond = 1.5;
        /// <summary>Dimension line beyond the extension lines with ticks (DIMDLE, paper mm).</summary>
        public const double DimLineExtension = 1;
        /// <summary>Gap between the dimension text and the dimension line (DIMGAP, paper mm).</summary>
        public const double DimTextGap = 0.8;
        /// <summary>Arrow kind of the dimension style.</summary>
        public const string DimArrow = "archtick";
        /// <summary>Font of the text styles (TrueType, Vietnamese diacritics).</summary>
        public const string Font = "Arial";
        /// <summary>Width factor of the text styles.</summary>
        public const double WidthFactor = 0.85;
        /// <summary>Recommended text heights on paper (mm): titles, room names, notes, small notes.</summary>
        public const double TextTitle = 5, TextRoom = 3.5, TextNote = 2.5, TextSmall = 1.8;
        /// <summary>LTSCALE = print scale denominator × this (acadiso.lin patterns, PSLTSCALE 0).</summary>
        public const double LtScaleFactor = 0.5;
        /// <summary>Linetypes loaded by setup_standards (acadiso.lin).</summary>
        public static readonly string[] Linetypes = { "CENTER", "DASHED", "HIDDEN", "PHANTOM", "DASHDOT" };
        /// <summary>Print scale when neither the call nor the palette gives one.</summary>
        public const int DefaultScale = 100;

        // ---------------------------------------------------------------- sheets (create_sheet), paper mm
        /// <summary>Binding margin (left) and the other three margins of the drawing frame.</summary>
        public const double MarginLeft = 25, MarginOther = 5;
        /// <summary>Line weight of the frame and of the title block's outline / inner lines (mm).</summary>
        public const double FrameLineweight = 0.5, TitleInnerLineweight = 0.25;
        /// <summary>Title block width (mm) and its row heights from the top (mm).</summary>
        public const double TitleWidth = 180;
        public static readonly double[] TitleRows = { 8, 8, 8, 12, 8, 8 };
        /// <summary>Width of the label column of the first four rows (mm).</summary>
        public const double TitleLabelWidth = 30;
        /// <summary>Cell widths of the two bottom rows (mm, sum = TitleWidth).</summary>
        public static readonly double[] TitleBottomCells = { 22, 38, 22, 38, 22, 38 };
        /// <summary>Text heights in the title block (mm): labels, values, the drawing title.</summary>
        public const double TitleLabelHeight = 2, TitleValueHeight = 2.5, TitleMainHeight = 3.5;
        /// <summary>Gap between the frame (or the title block) and the viewport (mm).</summary>
        public const double ViewportGap = 5;
        /// <summary>Layers of the frame / title block (printed) and of the viewport border (not printed), with ACI colors.</summary>
        public const string FrameLayer = "KHUNG", ViewportLayer = "KHUNG-VP";
        /// <summary>Name of a new sheet without a name: BV-01, BV-02… (F2-test: was a literal in the plugin).</summary>
        public const string SheetNamePrefix = "BV-";
        public const int FrameLayerColor = 7, ViewportLayerColor = 8;
        /// <summary>Text style of the title block and the title block block name.</summary>
        public const string TitleTextStyle = "VN-KHUNG", TitleBlockName = "KHUNG-TEN-VN";
        /// <summary>Paper sizes offered (ISO 216, mm, portrait).</summary>
        public static readonly (string name, double w, double h)[] Papers =
        {
            ("A0", 841, 1189), ("A1", 594, 841), ("A2", 420, 594), ("A3", 297, 420), ("A4", 210, 297),
        };
        /// <summary>Standard scales tried when the call gives none (the smallest where the drawing fits).</summary>
        public static readonly int[] StandardScales = { 1, 2, 5, 10, 20, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000, 2000, 5000 };

        // ---------------------------------------------------------------- library blocks: default layers and colors
        public const string DoorLayer = "A-DOOR", WindowLayer = "A-WIND", FixtureLayer = "A-FIXT", FurnitureLayer = "A-FURN", PlantLayer = "L-PLNT";
        public static readonly Dictionary<string, int> LibraryLayerColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [DoorLayer] = 4, [WindowLayer] = 5, [FixtureLayer] = 6, [FurnitureLayer] = 8, [PlantLayer] = 3,
        };
    }

    /// <summary>setup_standards: the styles of a standard at a print scale, and what differs in an existing style.</summary>
    public static class StandardsLogic
    {
        /// <summary>Style name prefix: the ve-kien-truc skill's names (KT-ARIAL, KT-100), else VN-.</summary>
        public static string Prefix(string standard) =>
            string.Equals((standard ?? "").Trim(), "ve-kien-truc", StringComparison.OrdinalIgnoreCase) ? "KT" : "VN";

        /// <summary>"1:100", "1/100", 100 or 0.01 → 100. Null / empty → the default. Range 1..10000.</summary>
        public static int ScaleDenominator(object v, string tool = "setup_standards")
        {
            if (v == null || (v is string e && e.Trim().Length == 0)) return F2Numbers.DefaultScale;
            double d;
            if (v is string s)
            {
                var t = s.Trim().Replace(" ", "");
                var parts = t.Split(':', '/');
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && a > 0 && b > 0)
                    d = b / a < 1 ? -1 : b / a; // an enlargement (2:1) is not a drawing scale here
                else if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) d = -1;
                else if (d > 0 && d < 1) d = 1 / d;
            }
            else
            {
                d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (d > 0 && d < 1) d = 1 / d;
            }
            if (!(d >= 1) || d > 10000 || double.IsNaN(d))
                throw ToolException.Invalid($"{tool}: 'scale' must be a print scale like \"1:100\" (or 100), from 1:1 to 1:10000.");
            return (int)Math.Round(d);
        }

        public static string TextStyleName(string prefix) => prefix + "-ARIAL";

        /// <summary>KT-100; a drawing in metres gets its own name (KT-100-M) so a mm style of the same scale is not mixed up.</summary>
        public static string DimStyleName(string prefix, int scale, double mmPerUnit) =>
            $"{prefix}-{scale}" + UnitSuffix(mmPerUnit);

        /// <summary>
        /// F2-test (point 3): one key for a font name — "Arial", "arial.ttf" and "C:\\Windows\\Fonts\\ARIAL.TTF" are the same
        /// font (TrueType / OpenType extension and folder dropped, case kept for display only by callers). A .shx name keeps
        /// its extension so a shape font never equals a TrueType one.
        /// </summary>
        public static string FontKey(string font)
        {
            var s = (font ?? "").Trim();
            int slash = Math.Max(s.LastIndexOf('\\'), s.LastIndexOf('/'));
            if (slash >= 0) s = s.Substring(slash + 1);
            foreach (var ext in new[] { ".ttf", ".ttc", ".otf" })
                if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(0, s.Length - ext.Length); break; }
            return s;
        }

        /// <summary>F2-test: the unit of the drawing in the style name (dm / inch / foot drawings were named "-CM").</summary>
        public static string UnitSuffix(double mmPerUnit)
        {
            bool Is(double v) => Math.Abs(mmPerUnit - v) < 1e-9;
            if (Is(1)) return "";
            if (Is(10)) return "-CM";
            if (Is(25.4)) return "-IN";
            if (Is(100)) return "-DM";
            if (Is(304.8)) return "-FT";
            if (Is(1000)) return "-M";
            return mmPerUnit >= 1000 ? "-M" : mmPerUnit >= 10 ? "-CM" : "";
        }

        /// <summary>Params of create_text_style.</summary>
        public static Dictionary<string, object> TextStyleParams(string prefix) => new Dictionary<string, object>
        {
            ["name"] = TextStyleName(prefix), ["font"] = F2Numbers.Font, ["height"] = 0.0, ["width_factor"] = F2Numbers.WidthFactor,
        };

        /// <summary>Decimals shown by the dimension style: whole mm, 1 in cm, 2 in dm / m.</summary>
        public static int Decimals(double mmPerUnit) => mmPerUnit >= 100 ? 2 : mmPerUnit >= 10 ? 1 : 0;

        /// <summary>Params of create_dim_style (DIMSCALE = scale in drawing units: 100 for 1:100 in mm, 0.1 in m).</summary>
        public static Dictionary<string, object> DimStyleParams(string prefix, int scale, double mmPerUnit) => new Dictionary<string, object>
        {
            ["name"] = DimStyleName(prefix, scale, mmPerUnit),
            ["scale"] = scale / mmPerUnit,
            ["text_height"] = F2Numbers.DimTextHeight,
            ["arrow"] = F2Numbers.DimArrow,
            ["arrow_size"] = F2Numbers.DimArrowSize,
            ["extension_offset"] = F2Numbers.DimExtOffset,
            ["extension_beyond"] = F2Numbers.DimExtBeyond,
            ["dim_line_extension"] = F2Numbers.DimLineExtension,
            ["text_gap"] = F2Numbers.DimTextGap,
            ["decimals"] = (double)Decimals(mmPerUnit), // Params reads numbers as double
            ["text_style"] = TextStyleName(prefix),
            ["text_position"] = "above",
        };

        /// <summary>Recommended text heights in drawing units for this scale.</summary>
        public static Dictionary<string, object> TextHeights(int scale, double mmPerUnit)
        {
            double k = scale / mmPerUnit;
            return new Dictionary<string, object>
            {
                ["title"] = Math.Round(F2Numbers.TextTitle * k, 6),
                ["room_name"] = Math.Round(F2Numbers.TextRoom * k, 6),
                ["note"] = Math.Round(F2Numbers.TextNote * k, 6),
                ["small"] = Math.Round(F2Numbers.TextSmall * k, 6),
            };
        }

        public static double LtScale(int scale, double mmPerUnit) => scale * F2Numbers.LtScaleFactor / mmPerUnit;

        /// <summary>
        /// The properties of an existing style that differ from the wanted ones ("width_factor 1 → 0.85"). Numbers
        /// within a relative 1e-6, text case-insensitively. A key the existing style does not report is not compared.
        /// </summary>
        public static List<string> Differences(IDictionary<string, object> wanted, IDictionary<string, object> existing)
        {
            var out1 = new List<string>();
            foreach (var kv in wanted)
            {
                if (kv.Key == "name" || existing == null || !existing.TryGetValue(kv.Key, out var have)) continue;
                if (Same(kv.Value, have)) continue;
                out1.Add($"{kv.Key} {Show(have)} → {Show(kv.Value)}");
            }
            return out1;
        }

        private static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (IsNum(a) && IsNum(b))
            {
                double x = Convert.ToDouble(a, CultureInfo.InvariantCulture), y = Convert.ToDouble(b, CultureInfo.InvariantCulture);
                return Math.Abs(x - y) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
            }
            return string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture)?.Trim(), Convert.ToString(b, CultureInfo.InvariantCulture)?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNum(object o) => o is double || o is int || o is float || o is long || o is decimal || o is short;

        private static string Show(object o) => IsNum(o) ? Math.Round(Convert.ToDouble(o, CultureInfo.InvariantCulture), 6).ToString(CultureInfo.InvariantCulture) : Convert.ToString(o, CultureInfo.InvariantCulture);

        /// <summary>LTSCALE: set only while it is still AutoCAD's default 1 (else the user's choice is kept and reported).</summary>
        public static bool ShouldSetLtScale(double current) => Math.Abs(current - 1) < 1e-9;
    }

    /// <summary>
    /// Refusals of F2 calls that would overwrite something existing (a style that differs, a layer state of the same
    /// name, deleting a layer state): like find_replace_text, only the Đồng ý button of a card listing the handles allows it.
    /// </summary>
    public static class F2Confirm
    {
        public static string CardMessage(string tool, string what, IEnumerable<string> handles) =>
            $"Confirmation required: {tool} would {what} (handles {string.Join(", ", handles)}), which is not on a confirmation card the user approved. " +
            "Nothing was changed. Call request_confirmation with exactly these handles and one sentence in the user's language saying what will be overwritten or deleted, " +
            $"and end your turn. After the user presses Đồng ý, call {tool} again with the same parameters and handles = those handles. A typed \"ok\" does not count.";
    }

    /// <summary>create_sheet: paper, frame, title block, viewport and scale.</summary>
    public static class SheetLogic
    {
        public sealed class Sheet
        {
            public string Paper;
            public bool Landscape;
            public double W, H;
            /// <summary>Frame corners (paper mm).</summary>
            public double X0, Y0, X1, Y1;
            /// <summary>Title block: width, height; its base point is its bottom-right corner = the frame's bottom-right corner.</summary>
            public double TitleW, TitleH;
            /// <summary>Viewport rectangle (paper mm).</summary>
            public double VpX0, VpY0, VpX1, VpY1;
            public double VpW => VpX1 - VpX0;
            public double VpH => VpY1 - VpY0;
            public double[] VpCenter => new[] { (VpX0 + VpX1) / 2, (VpY0 + VpY1) / 2 };
            public string Media => SheetLogic.MediaName(W, H, Paper);
        }

        public static string NormalizePaper(string paper)
        {
            var p = (paper ?? "A3").Trim().ToUpperInvariant();
            foreach (var x in F2Numbers.Papers) if (x.name == p) return p;
            throw ToolException.Invalid($"create_sheet: paper must be one of {string.Join(", ", F2Numbers.Papers.Select(x => x.name))}.");
        }

        /// <summary>A4 is portrait by default, the bigger sizes landscape.</summary>
        public static bool DefaultLandscape(string paper) => NormalizePaper(paper) != "A4";

        /// <summary>"ISO_full_bleed_A3_(420.00_x_297.00_MM)" — the DWG To PDF.pc3 canonical name (width × height as drawn).</summary>
        public static string MediaName(double w, double h, string paper) =>
            string.Format(CultureInfo.InvariantCulture, "ISO_full_bleed_{0}_({1:0.00}_x_{2:0.00}_MM)", paper, w, h);

        public static Sheet Build(string paper, bool? landscape)
        {
            var name = NormalizePaper(paper);
            var size = F2Numbers.Papers.First(x => x.name == name);
            bool land = landscape ?? DefaultLandscape(name);
            double w = land ? size.h : size.w, h = land ? size.w : size.h;
            var s = new Sheet
            {
                Paper = name, Landscape = land, W = w, H = h,
                X0 = F2Numbers.MarginLeft, Y0 = F2Numbers.MarginOther, X1 = w - F2Numbers.MarginOther, Y1 = h - F2Numbers.MarginOther,
                TitleW = F2Numbers.TitleWidth, TitleH = F2Numbers.TitleRows.Sum(),
            };
            if (s.X1 - s.X0 < s.TitleW - 1e-9) throw ToolException.Invalid($"create_sheet: the title block ({s.TitleW} mm) does not fit {name}.");
            double g = F2Numbers.ViewportGap;
            s.VpX0 = s.X0 + g; s.VpX1 = s.X1 - g;
            s.VpY0 = s.Y0 + s.TitleH + g; s.VpY1 = s.Y1 - g;
            return s;
        }

        /// <summary>Viewport CustomScale (paper mm per drawing unit): 1:100 in a mm drawing = 0.01, in a m drawing = 10.</summary>
        public static double CustomScale(int scale, double mmPerUnit) => mmPerUnit / scale;

        /// <summary>Model size (drawing units) the viewport shows at this scale.</summary>
        public static double[] ModelArea(Sheet s, int scale, double mmPerUnit) =>
            new[] { s.VpW / CustomScale(scale, mmPerUnit), s.VpH / CustomScale(scale, mmPerUnit) };

        /// <summary>Whether a model box w × h (drawing units) fits the viewport at this scale.</summary>
        public static bool Fits(Sheet s, int scale, double mmPerUnit, double w, double h)
        {
            var a = ModelArea(s, scale, mmPerUnit);
            return w <= a[0] + 1e-9 && h <= a[1] + 1e-9;
        }

        /// <summary>The smallest standard scale (most detail) at which a model box fits; the largest offered when none does.</summary>
        public static int FitScale(Sheet s, double mmPerUnit, double w, double h)
        {
            foreach (var n in F2Numbers.StandardScales) if (Fits(s, n, mmPerUnit, w, h)) return n;
            return F2Numbers.StandardScales[F2Numbers.StandardScales.Length - 1];
        }

        public sealed class Cell
        {
            public string Label;
            public string Tag;
            public string Field;
            /// <summary>Label position and value position (MiddleLeft, relative to the title block base = bottom-right corner).</summary>
            public double[] LabelAt, ValueAt;
            public double ValueWidth, ValueHeight;
        }

        public sealed class TitleBlock
        {
            public double W, H;
            /// <summary>Lines (x0, y0, x1, y1), relative to the base; the outline is the rectangle (-W, 0)–(0, H).</summary>
            public List<double[]> Lines = new List<double[]>();
            public List<Cell> Cells = new List<Cell>();
        }

        /// <summary>The fields of the title block: call key → attribute tag, label.</summary>
        public static readonly (string field, string tag, string label)[] Fields =
        {
            ("company", "DON_VI", "ĐƠN VỊ TK"),
            ("project", "CONG_TRINH", "CÔNG TRÌNH"),
            ("item", "HANG_MUC", "HẠNG MỤC"),
            ("title", "TEN_BAN_VE", "TÊN BẢN VẼ"),
            ("drawn_by", "NGUOI_VE", "NGƯỜI VẼ"),
            ("checked_by", "KIEM_TRA", "KIỂM TRA"),
            ("date", "NGAY", "NGÀY"),
            ("scale", "TI_LE", "TỈ LỆ"),
            ("sheet_no", "SO_BAN_VE", "SỐ BẢN VẼ"),
            ("paper", "KHO_GIAY", "KHỔ GIẤY"),
        };

        public static TitleBlock BuildTitleBlock()
        {
            var tb = new TitleBlock { W = F2Numbers.TitleWidth, H = F2Numbers.TitleRows.Sum() };
            double x0 = -tb.W;
            // row tops, from the top down
            var tops = new List<double>();
            double y = tb.H;
            foreach (var rh in F2Numbers.TitleRows) { tops.Add(y); y -= rh; }
            // horizontal lines between rows
            for (int i = 1; i < F2Numbers.TitleRows.Length; i++) tb.Lines.Add(new[] { x0, tops[i], 0, tops[i] });
            int bigRows = F2Numbers.TitleRows.Length - 2;
            // label column of the first rows
            tb.Lines.Add(new[] { x0 + F2Numbers.TitleLabelWidth, tops[bigRows], x0 + F2Numbers.TitleLabelWidth, tb.H });
            for (int r = 0; r < bigRows; r++)
            {
                double mid = tops[r] - F2Numbers.TitleRows[r] / 2;
                var f = Fields[r];
                tb.Cells.Add(new Cell
                {
                    Label = f.label, Tag = f.tag, Field = f.field,
                    LabelAt = new[] { x0 + 1.5, mid }, ValueAt = new[] { x0 + F2Numbers.TitleLabelWidth + 2, mid },
                    ValueWidth = tb.W - F2Numbers.TitleLabelWidth - 4,
                    ValueHeight = f.field == "title" ? F2Numbers.TitleMainHeight : F2Numbers.TitleValueHeight,
                });
            }
            // the two bottom rows: label | value | label | value | label | value
            int fi = bigRows;
            for (int r = bigRows; r < F2Numbers.TitleRows.Length; r++)
            {
                double top = tops[r], bot = top - F2Numbers.TitleRows[r], mid = (top + bot) / 2;
                double x = x0;
                var cells = F2Numbers.TitleBottomCells;
                for (int c = 0; c < cells.Length; c++)
                {
                    if (c > 0) tb.Lines.Add(new[] { x, bot, x, top });
                    if (c % 2 == 1)
                    {
                        var f = Fields[fi++];
                        tb.Cells.Add(new Cell
                        {
                            Label = f.label, Tag = f.tag, Field = f.field,
                            LabelAt = new[] { x - cells[c - 1] + 1.5, mid }, ValueAt = new[] { x + 1.5, mid },
                            ValueWidth = cells[c] - 3, ValueHeight = F2Numbers.TitleValueHeight,
                        });
                    }
                    x += cells[c];
                }
            }
            return tb;
        }

        /// <summary>Field values of a call (keys of Fields), with the defaults: date today, scale, paper; unknown keys are errors.</summary>
        public static Dictionary<string, string> FieldValues(IDictionary<string, object> given, int scale, string paper, DateTime today)
        {
            var known = new HashSet<string>(Fields.Select(f => f.field), StringComparer.OrdinalIgnoreCase);
            var tags = Fields.ToDictionary(f => f.field, f => f.tag, StringComparer.OrdinalIgnoreCase);
            var outv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NGAY"] = today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                ["TI_LE"] = "1:" + scale.ToString(CultureInfo.InvariantCulture),
                ["KHO_GIAY"] = paper,
            };
            if (given != null)
                foreach (var kv in given)
                {
                    if (!known.Contains(kv.Key))
                        throw ToolException.Invalid($"create_sheet: unknown title field '{kv.Key}'. Fields: {string.Join(", ", Fields.Select(f => f.field))}.");
                    outv[tags[kv.Key]] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
                }
            return outv;
        }

        /// <summary>Values probably wider than their cell (Arial, width 0.85: about 0.8 × height per character).</summary>
        public static List<string> TooLong(TitleBlock tb, IDictionary<string, string> values)
        {
            var outl = new List<string>();
            foreach (var c in tb.Cells)
                if (values.TryGetValue(c.Tag, out var v) && v != null && v.Length * c.ValueHeight * 0.8 * F2Numbers.WidthFactor > c.ValueWidth)
                    outl.Add(c.Field);
            return outl;
        }
    }

    /// <summary>One primitive of a library block (mm, relative to the block's base point).</summary>
    public sealed class LibPrim
    {
        public string Kind; // line, pline, circle, arc, ellipse
        public List<double[]> Pts = new List<double[]>();
        public List<double> Bulges;
        public bool Closed;
        public double R, A0, A1; // circle / arc: center Pts[0], radius, angles in degrees
        public double Rx, Ry;    // ellipse: center Pts[0], half sizes along x / y

        public static LibPrim Line(double x0, double y0, double x1, double y1) =>
            new LibPrim { Kind = "line", Pts = { new[] { x0, y0 }, new[] { x1, y1 } } };
        public static LibPrim Rect(double x0, double y0, double x1, double y1) =>
            new LibPrim { Kind = "pline", Closed = true, Pts = { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } } };
        public static LibPrim Circle(double cx, double cy, double r) => new LibPrim { Kind = "circle", Pts = { new[] { cx, cy } }, R = r };
        public static LibPrim Arc(double cx, double cy, double r, double a0, double a1) => new LibPrim { Kind = "arc", Pts = { new[] { cx, cy } }, R = r, A0 = a0, A1 = a1 };
        public static LibPrim Ellipse(double cx, double cy, double rx, double ry) => new LibPrim { Kind = "ellipse", Pts = { new[] { cx, cy } }, Rx = rx, Ry = ry };

        /// <summary>A closed rectangle with rounded corners (radius r) as a polyline with bulges.</summary>
        public static LibPrim RoundRect(double x0, double y0, double x1, double y1, double r)
        {
            r = Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2);
            double b = Math.Tan(Math.PI / 8); // 90° arc
            return new LibPrim
            {
                Kind = "pline", Closed = true,
                Pts =
                {
                    new[] { x0 + r, y0 }, new[] { x1 - r, y0 }, new[] { x1, y0 + r }, new[] { x1, y1 - r },
                    new[] { x1 - r, y1 }, new[] { x0 + r, y1 }, new[] { x0, y1 - r }, new[] { x0, y0 + r },
                },
                Bulges = new List<double> { 0, b, 0, b, 0, b, 0, b },
            };
        }

        /// <summary>Bounding box (min x, min y, max x, max y).</summary>
        public double[] Box()
        {
            switch (Kind)
            {
                case "circle": return new[] { Pts[0][0] - R, Pts[0][1] - R, Pts[0][0] + R, Pts[0][1] + R };
                case "ellipse": return new[] { Pts[0][0] - Rx, Pts[0][1] - Ry, Pts[0][0] + Rx, Pts[0][1] + Ry };
                case "arc":
                {
                    var xs = new List<double>(); var ys = new List<double>();
                    void Add(double deg) { xs.Add(Pts[0][0] + R * Math.Cos(deg * Math.PI / 180)); ys.Add(Pts[0][1] + R * Math.Sin(deg * Math.PI / 180)); }
                    double a1 = A1 < A0 ? A1 + 360 : A1;
                    Add(A0); Add(a1);
                    for (double q = Math.Ceiling(A0 / 90) * 90; q < a1; q += 90) Add(q);
                    return new[] { xs.Min(), ys.Min(), xs.Max(), ys.Max() };
                }
                default:
                    return new[] { Pts.Min(p => p[0]), Pts.Min(p => p[1]), Pts.Max(p => p[0]), Pts.Max(p => p[1]) };
            }
        }
    }

    /// <summary>One library block: defaults and limits of its sizes (mm), how it is drawn and where its base point is.</summary>
    public sealed class LibItem
    {
        public string Key, Name, Category, BlockPrefix, Layer, Base;
        public string[] Aliases = new string[0];
        public double Width, MinWidth, MaxWidth;
        /// <summary>0 = the block has no depth parameter.</summary>
        public double Depth, MinDepth, MaxDepth;
        public string WidthMeaning = "width", DepthMeaning = "depth";
        public Func<double, double, List<LibPrim>> Draw;
        public bool HasDepth => Depth > 0;
    }

    /// <summary>
    /// The block library of insert_library_block: plan symbols drawn by this code (no third-party block files). Doors and
    /// windows are named CUA-DI-* / CUA-SO-* like the ve-kien-truc skill, so build_3d_from_plan recognizes them.
    /// </summary>
    public static class LibraryBlocks
    {
        public const string DefinitionNote = "ClaudeCAD block library";

        public static readonly IReadOnlyList<LibItem> Items = BuildItems();

        private static List<LibItem> BuildItems()
        {
            var l = new List<LibItem>();
            // doors: base point = hinge on the wall face, the opening along +x, the leaf open 90° towards +y
            l.Add(new LibItem
            {
                Key = "cua_di", Name = "Cửa đi 1 cánh", Category = "cửa", BlockPrefix = "CUA-DI", Layer = F2Numbers.DoorLayer,
                Aliases = new[] { "cua di", "cua di 1 canh", "door" }, Width = 900, MinWidth = 500, MaxWidth = 1500,
                Base = "hinge; opening along +x, leaf open 90° to +y",
                Draw = (w, d) => new List<LibPrim> { LibPrim.Line(0, 0, 0, w), LibPrim.Arc(0, 0, w, 0, 90) },
            });
            l.Add(new LibItem
            {
                Key = "cua_di_2_canh", Name = "Cửa đi 2 cánh", Category = "cửa", BlockPrefix = "CUA-DI-2C", Layer = F2Numbers.DoorLayer,
                Aliases = new[] { "cua 2 canh", "double door" }, Width = 1400, MinWidth = 1000, MaxWidth = 3000,
                Base = "left jamb; opening along +x, leaves open 90° to +y",
                Draw = (w, d) => new List<LibPrim>
                {
                    LibPrim.Line(0, 0, 0, w / 2), LibPrim.Arc(0, 0, w / 2, 0, 90),
                    LibPrim.Line(w, 0, w, w / 2), LibPrim.Arc(w, 0, w / 2, 90, 180),
                },
            });
            l.Add(new LibItem
            {
                Key = "cua_truot", Name = "Cửa trượt 2 cánh", Category = "cửa", BlockPrefix = "CUA-DI-TRUOT", Layer = F2Numbers.DoorLayer,
                Aliases = new[] { "cua lua", "sliding door" }, Width = 1800, MinWidth = 1000, MaxWidth = 4000,
                Depth = 220, MinDepth = 100, MaxDepth = 400, DepthMeaning = "wall thickness",
                Base = "corner of the opening on one wall face; opening along +x, wall thickness along +y",
                Draw = (w, d) =>
                {
                    double t = Math.Min(40, d / 4), m = d / 2, lap = Math.Min(50, w / 10);
                    return new List<LibPrim>
                    {
                        LibPrim.Line(0, 0, 0, d), LibPrim.Line(w, 0, w, d),
                        LibPrim.Rect(0, m - t, w / 2 + lap, m), LibPrim.Rect(w / 2 - lap, m, w, m + t),
                    };
                },
            });
            l.Add(new LibItem
            {
                Key = "cua_so", Name = "Cửa sổ", Category = "cửa", BlockPrefix = "CUA-SO", Layer = F2Numbers.WindowLayer,
                Aliases = new[] { "window" }, Width = 1200, MinWidth = 300, MaxWidth = 4000,
                Depth = 220, MinDepth = 100, MaxDepth = 400, DepthMeaning = "wall thickness",
                Base = "corner of the opening on one wall face; opening along +x, wall thickness along +y",
                Draw = (w, d) => new List<LibPrim> { LibPrim.Rect(0, 0, w, d), LibPrim.Line(0, d / 3, w, d / 3), LibPrim.Line(0, 2 * d / 3, w, 2 * d / 3) },
            });
            // sanitary: base point = middle of the back (wall) side, the fixture towards +y
            l.Add(new LibItem
            {
                Key = "bon_cau", Name = "Bồn cầu", Category = "vệ sinh", BlockPrefix = "TB-BON-CAU", Layer = F2Numbers.FixtureLayer,
                Aliases = new[] { "xi bet", "toilet", "wc" }, Width = 400, MinWidth = 300, MaxWidth = 600, Depth = 700, MinDepth = 500, MaxDepth = 900,
                Base = "middle of the wall side; fixture towards +y",
                Draw = (w, d) =>
                {
                    double tank = d * 0.26, ry = (d - tank) / 2;
                    return new List<LibPrim>
                    {
                        LibPrim.RoundRect(-w / 2, 0, w / 2, tank, Math.Min(30, tank / 4)),
                        LibPrim.Ellipse(0, tank + ry, w * 0.45, ry), LibPrim.Ellipse(0, tank + ry, w * 0.3, ry * 0.65),
                    };
                },
            });
            l.Add(new LibItem
            {
                Key = "lavabo", Name = "Chậu rửa mặt (lavabo)", Category = "vệ sinh", BlockPrefix = "TB-LAVABO", Layer = F2Numbers.FixtureLayer,
                Aliases = new[] { "chau rua mat", "chau rua", "basin", "sink" }, Width = 500, MinWidth = 350, MaxWidth = 1200, Depth = 420, MinDepth = 300, MaxDepth = 600,
                Base = "middle of the wall side; fixture towards +y",
                Draw = (w, d) => new List<LibPrim>
                {
                    LibPrim.RoundRect(-w / 2, 0, w / 2, d, Math.Min(40, d / 6)),
                    LibPrim.Ellipse(0, d * 0.55, w * 0.35, d * 0.3), LibPrim.Circle(0, d * 0.55, Math.Min(20, d * 0.05)),
                },
            });
            l.Add(new LibItem
            {
                Key = "bon_tam", Name = "Bồn tắm", Category = "vệ sinh", BlockPrefix = "TB-BON-TAM", Layer = F2Numbers.FixtureLayer,
                Aliases = new[] { "bathtub" }, Width = 1700, MinWidth = 1200, MaxWidth = 2200, Depth = 750, MinDepth = 600, MaxDepth = 1000,
                Base = "lower-left corner; length along +x, width along +y",
                Draw = (w, d) => new List<LibPrim>
                {
                    LibPrim.Rect(0, 0, w, d), LibPrim.RoundRect(60, 60, w - 60, d - 60, Math.Min(150, (d - 120) / 2)),
                    LibPrim.Circle(160, d / 2, 30),
                },
            });
            l.Add(new LibItem
            {
                Key = "khay_tam", Name = "Khay tắm đứng", Category = "vệ sinh", BlockPrefix = "TB-KHAY-TAM", Layer = F2Numbers.FixtureLayer,
                Aliases = new[] { "voi sen", "shower" }, Width = 900, MinWidth = 700, MaxWidth = 1600, Depth = 900, MinDepth = 700, MaxDepth = 1600,
                Base = "lower-left corner",
                Draw = (w, d) => new List<LibPrim> { LibPrim.Rect(0, 0, w, d), LibPrim.Line(0, 0, w, d), LibPrim.Line(0, d, w, 0), LibPrim.Circle(w / 2, d / 2, 50) },
            });
            // furniture: base point = lower-left corner (bed / sofa / chair: the back side at +y)
            l.Add(new LibItem
            {
                Key = "giuong", Name = "Giường", Category = "nội thất", BlockPrefix = "NT-GIUONG", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "giuong ngu", "bed" }, Width = 1600, MinWidth = 800, MaxWidth = 2200, Depth = 2000, MinDepth = 1800, MaxDepth = 2200,
                Base = "lower-left corner (foot end); head board at +y",
                Draw = (w, d) =>
                {
                    var r = new List<LibPrim> { LibPrim.Rect(0, 0, w, d), LibPrim.Rect(0, d - 50, w, d), LibPrim.Line(0, d * 0.62, w, d * 0.62) };
                    int n = w >= 1200 ? 2 : 1;
                    double gap = 100, pw = (w - (n + 1) * gap) / n;
                    for (int i = 0; i < n; i++)
                        r.Add(LibPrim.RoundRect(gap + i * (pw + gap), d - 50 - 80 - 350, gap + i * (pw + gap) + pw, d - 50 - 80, 60));
                    return r;
                },
            });
            l.Add(new LibItem
            {
                Key = "sofa", Name = "Sofa", Category = "nội thất", BlockPrefix = "NT-SOFA", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "ghe sofa" }, Width = 2000, MinWidth = 1200, MaxWidth = 3500, Depth = 850, MinDepth = 700, MaxDepth = 1100,
                Base = "lower-left corner; back at +y",
                Draw = (w, d) =>
                {
                    double arm = 150, back = 200;
                    var r = new List<LibPrim>
                    {
                        LibPrim.RoundRect(0, 0, w, d, 40), LibPrim.Rect(arm, d - back, w - arm, d - 20),
                        LibPrim.Line(arm, 0, arm, d - back), LibPrim.Line(w - arm, 0, w - arm, d - back),
                    };
                    int seats = Math.Max(1, (int)Math.Round((w - 2 * arm) / 600));
                    for (int i = 1; i < seats; i++) { double x = arm + i * (w - 2 * arm) / seats; r.Add(LibPrim.Line(x, 20, x, d - back)); }
                    return r;
                },
            });
            l.Add(new LibItem
            {
                Key = "ban", Name = "Bàn", Category = "nội thất", BlockPrefix = "NT-BAN", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "ban an", "ban lam viec", "table" }, Width = 1200, MinWidth = 400, MaxWidth = 3000, Depth = 800, MinDepth = 400, MaxDepth = 1500,
                Base = "lower-left corner",
                Draw = (w, d) => new List<LibPrim> { LibPrim.Rect(0, 0, w, d) },
            });
            l.Add(new LibItem
            {
                Key = "ghe", Name = "Ghế", Category = "nội thất", BlockPrefix = "NT-GHE", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "chair" }, Width = 450, MinWidth = 350, MaxWidth = 700, Depth = 450, MinDepth = 350, MaxDepth = 700,
                Base = "lower-left corner; back at +y",
                Draw = (w, d) => new List<LibPrim> { LibPrim.RoundRect(0, 0, w, d, 40), LibPrim.Rect(0, d - 80, w, d) },
            });
            l.Add(new LibItem
            {
                Key = "tu", Name = "Tủ (quần áo / kệ)", Category = "nội thất", BlockPrefix = "NT-TU", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "tu quan ao", "tu ao", "wardrobe", "cabinet" }, Width = 1200, MinWidth = 300, MaxWidth = 4000, Depth = 600, MinDepth = 300, MaxDepth = 800,
                Base = "lower-left corner (front side); back at +y",
                Draw = (w, d) => new List<LibPrim> { LibPrim.Rect(0, 0, w, d), LibPrim.Line(0, 0, w, d), LibPrim.Line(0, d, w, 0) },
            });
            l.Add(new LibItem
            {
                Key = "bep", Name = "Bếp nấu 2 vùng", Category = "nội thất", BlockPrefix = "NT-BEP", Layer = F2Numbers.FurnitureLayer,
                Aliases = new[] { "bep nau", "bep tu", "bep ga", "hob" }, Width = 700, MinWidth = 500, MaxWidth = 1000, Depth = 450, MinDepth = 350, MaxDepth = 600,
                Base = "lower-left corner (front side)",
                Draw = (w, d) =>
                {
                    double r = Math.Min(w / 4, d / 2) * 0.7;
                    return new List<LibPrim> { LibPrim.Rect(0, 0, w, d), LibPrim.Circle(w * 0.27, d / 2, r), LibPrim.Circle(w * 0.73, d / 2, r) };
                },
            });
            // planting: base point = center of the tree; width = canopy diameter
            l.Add(new LibItem
            {
                Key = "cay", Name = "Cây (mặt bằng)", Category = "cây", BlockPrefix = "CAY", Layer = F2Numbers.PlantLayer,
                Aliases = new[] { "cay xanh", "tree" }, Width = 3000, MinWidth = 500, MaxWidth = 15000, WidthMeaning = "canopy diameter",
                Base = "center of the tree",
                Draw = (w, d) =>
                {
                    double r = w / 2;
                    var star = new LibPrim { Kind = "pline", Closed = true };
                    for (int i = 0; i < 16; i++)
                    {
                        double a = i * Math.PI / 8, rr = i % 2 == 0 ? 0.8 * r : 0.6 * r;
                        star.Pts.Add(new[] { rr * Math.Cos(a), rr * Math.Sin(a) });
                    }
                    return new List<LibPrim> { LibPrim.Circle(0, 0, r), star, LibPrim.Circle(0, 0, Math.Max(50, r * 0.06)) };
                },
            });
            return l;
        }

        /// <summary>Lower case, no Vietnamese marks, words joined by "_" ("Cửa đi 2 cánh" → "cua_di_2_canh").</summary>
        public static string Normalize(string s)
        {
            if (s == null) return "";
            var d = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }
            var t = sb.ToString();
            while (t.Contains("__")) t = t.Replace("__", "_");
            return t.Trim('_');
        }

        public static LibItem Find(string name)
        {
            var n = Normalize(name);
            if (n.Length == 0) return null;
            return Items.FirstOrDefault(i => i.Key == n)
                ?? Items.FirstOrDefault(i => Normalize(i.Name) == n || i.Aliases.Any(a => Normalize(a) == n));
        }

        public static LibItem Require(string name) =>
            Find(name) ?? throw ToolException.Invalid(
                $"insert_library_block: '{name}' is not in the library. Names: {string.Join(", ", Items.Select(i => i.Key))} (call it without name to list them with sizes).");

        /// <summary>Width / depth of a call (defaults, limits), rounded to whole mm.</summary>
        public static (double w, double d) Sizes(LibItem it, double? width, double? depth)
        {
            double w = Math.Round(width ?? it.Width);
            if (w < it.MinWidth || w > it.MaxWidth)
                throw ToolException.Invalid($"insert_library_block: {it.Key} {it.WidthMeaning} must be {it.MinWidth}–{it.MaxWidth} mm (got {w}).");
            double d = 0;
            if (it.HasDepth)
            {
                d = Math.Round(depth ?? it.Depth);
                if (d < it.MinDepth || d > it.MaxDepth)
                    throw ToolException.Invalid($"insert_library_block: {it.Key} {it.DepthMeaning} must be {it.MinDepth}–{it.MaxDepth} mm (got {d}).");
            }
            else if (depth.HasValue) throw ToolException.Invalid($"insert_library_block: {it.Key} has no depth (only {it.WidthMeaning}).");
            return (w, d);
        }

        /// <summary>Block name: CUA-DI-900, CUA-SO-1200 (wall 220), CUA-SO-1200x110, TB-BON-CAU-400x700, CAY-3000.</summary>
        public static string BlockName(LibItem it, double w, double d)
        {
            string W = w.ToString("0", CultureInfo.InvariantCulture), D = d.ToString("0", CultureInfo.InvariantCulture);
            if (!it.HasDepth) return $"{it.BlockPrefix}-{W}";
            // doors / windows keep the skill's short name at the default wall
            if (it.Category == "cửa" && Math.Abs(d - it.Depth) < 0.5) return $"{it.BlockPrefix}-{W}";
            return $"{it.BlockPrefix}-{W}x{D}";
        }

        public static List<LibPrim> Build(LibItem it, double w, double d) => it.Draw(w, d);

        /// <summary>Relative size difference under which an existing block of the same name is taken as the library one.</summary>
        public const double SizeTol = 0.02;

        /// <summary>Kiểm tra độc lập đợt F: an existing block w × h is the library block wantW × wantH (2 %, either orientation).</summary>
        public static bool SameSize(double w, double h, double wantW, double wantH)
        {
            bool Near(double a, double b) => Math.Abs(a - b) <= SizeTol * Math.Max(Math.Abs(b), 1e-9);
            return (Near(w, wantW) && Near(h, wantH)) || (Near(w, wantH) && Near(h, wantW));
        }

        /// <summary>The name of the library block when the drawing has another block of its name at another size.</summary>
        public static string OtherSizeName(string name) => name + "-TV";

        /// <summary>Box of all primitives (min x, min y, max x, max y).</summary>
        public static double[] Box(IEnumerable<LibPrim> prims)
        {
            var b = prims.Select(p => p.Box()).ToList();
            return new[] { b.Min(x => x[0]), b.Min(x => x[1]), b.Max(x => x[2]), b.Max(x => x[3]) };
        }

        /// <summary>The listing of insert_library_block (no name).</summary>
        public static List<object> Catalog() => Items.Select(i =>
        {
            var d = new Dictionary<string, object>
            {
                ["name"] = i.Key, ["label"] = i.Name, ["category"] = i.Category,
                [i.WidthMeaning.Replace(' ', '_') + "_mm"] = new List<object> { i.Width, i.MinWidth, i.MaxWidth },
                ["base_point"] = i.Base, ["layer"] = i.Layer,
            };
            if (i.HasDepth) d[i.DepthMeaning.Replace(' ', '_') + "_mm"] = new List<object> { i.Depth, i.MinDepth, i.MaxDepth };
            return (object)d;
        }).ToList();
    }

    /// <summary>layer_state: actions and the properties a state saves.</summary>
    public static class LayerStateLogic
    {
        public static readonly string[] Actions = { "save", "restore", "list", "delete" };
        /// <summary>What a state remembers of each layer (AutoCAD LayerStateMasks).</summary>
        public static readonly string[] Saved = { "on", "frozen", "locked", "plottable", "color", "linetype", "lineweight" };

        public static string NormalizeAction(string a)
        {
            var s = (a ?? "").Trim().ToLowerInvariant();
            switch (s)
            {
                case "luu": case "lưu": return "save";
                case "goi": case "gọi": case "goi lai": case "gọi lại": return "restore";
                case "liet ke": case "liệt kê": return "list";
                case "xoa": case "xóa": return "delete";
            }
            if (Actions.Contains(s)) return s;
            throw ToolException.Invalid($"layer_state: action must be one of {string.Join(", ", Actions)}.");
        }

        /// <summary>Name rules of AutoCAD symbol names, trimmed; at most 255 characters.</summary>
        public static string CheckName(string name)
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0) throw ToolException.Invalid("layer_state: 'name' is required for save / restore / delete.");
            if (n.Length > 255 || n.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
                throw ToolException.Invalid($"layer_state: '{n}' is not a valid name (no < > / \\ \" : ; ? * | , = `, at most 255 characters).");
            return n;
        }
    }
}
