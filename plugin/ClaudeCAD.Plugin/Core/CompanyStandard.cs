using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H4: the numbers of "học chuẩn công ty từ bản vẽ mẫu" (company_standard), one place each
    /// (docs/ban-giao/nang-cap-h4.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class H4Numbers
    {
        /// <summary>Most company standards kept on one computer.</summary>
        public const int MaxStandards = 20;
        /// <summary>Most layers learnt from one template (a template with more is cut, and the result says so).</summary>
        public const int MaxLayers = 1000;
        /// <summary>Most text / dimension / multileader styles of each kind learnt from one template.</summary>
        public const int MaxStyles = 100;
        /// <summary>Most title blocks (blocks with attributes on a layout) remembered.</summary>
        public const int MaxTitleBlocks = 10;
        /// <summary>Most distinct text heights remembered (the most used ones of the template).</summary>
        public const int MaxHeights = 12;
        /// <summary>Largest template file copied into the standard (MB): a bigger one is learnt but its blocks are not kept.</summary>
        public const int MaxTemplateMb = 50;
        /// <summary>Longest name of a standard.</summary>
        public const int MaxNameLength = 60;
        /// <summary>Paper size match (mm) when naming the paper of a layout A0 … A4.</summary>
        public const double PaperTolMm = 2;
    }

    /// <summary>Nâng cấp đợt H4: a company standard learnt from a template drawing (.dwg / .dwt).</summary>
    public sealed class CompanyStandard
    {
        public sealed class Layer
        {
            public string Name = "", Color = "7", Linetype = "Continuous", Description = "";
            /// <summary>mm; −1 = default.</summary>
            public double Lineweight = -1;
            public bool Plottable = true;
        }

        public sealed class TextStyle
        {
            public string Name = "", Font = "", BigFont = "";
            public double Height, WidthFactor = 1, Oblique;
        }

        public sealed class DimStyle
        {
            public string Name = "", TextStyle = "";
            public double TextHeight, ArrowSize, Scale = 1;
        }

        public sealed class MLeaderStyle
        {
            public string Name = "", TextStyle = "";
            public double TextHeight, ArrowSize, Scale = 1;
        }

        public sealed class TitleBlock
        {
            public string Block = "", Layout = "";
            public List<string> Tags = new List<string>();
            /// <summary>Insertion on the paper (mm of the layout), rotation (degrees), scale.</summary>
            public double X, Y, Rotation, Scale = 1;
        }

        public sealed class Paper
        {
            public string Layout = "", Media = "";
            /// <summary>A0 … A4 when the size is one of them, else null.</summary>
            public string Name;
            public double W, H;
            public bool Landscape = true;
            /// <summary>The main viewport on the paper (centre, size, mm), when the layout has one.</summary>
            public double[] Viewport;
        }

        public string Name = "", Source = "", LearnedAt = "";
        /// <summary>The styles current in the template (made current by setup_standards with this standard).</summary>
        public string CurrentTextStyle = "", CurrentDimStyle = "", CurrentMLeaderStyle = "";
        public int Insunits = 4;
        public double MmPerUnit = 1;
        public List<Layer> Layers = new List<Layer>();
        public List<TextStyle> TextStyles = new List<TextStyle>();
        public List<DimStyle> DimStyles = new List<DimStyle>();
        public List<MLeaderStyle> MLeaderStyles = new List<MLeaderStyle>();
        public List<TitleBlock> TitleBlocks = new List<TitleBlock>();
        public List<Paper> Papers = new List<Paper>();
        /// <summary>Text heights of the template (its drawing units), most used first.</summary>
        public List<double> TextHeights = new List<double>();
        /// <summary>The template was copied into the standard's folder (its blocks and styles can be cloned).</summary>
        public bool TemplateKept;
        /// <summary>What was left out when learning (too many layers…).</summary>
        public List<string> Notes = new List<string>();
    }

    /// <summary>Nâng cấp đợt H4, AutoCAD-free: names, the store on this computer, JSON, and the checks with a standard.</summary>
    public static class CompanyStandardLogic
    {
        public const string Tool = "company_standard";
        public const string FolderName = "chuan-cong-ty";
        public const string DataFile = "standard.json";
        public const string TemplateFile = "mau.dwg";
        public static readonly string[] Actions = { "learn", "list", "show", "delete" };

        /// <summary>%LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty: per user, outside the install folder (kept by a reinstall / update).</summary>
        public static string DefaultRoot() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", FolderName);

        public static string ValidName(string name, string tool = Tool)
        {
            var s = Regex.Replace((name ?? "").Trim(), @"\s+", " ");
            if (s.Length == 0) throw ToolException.Invalid($"{tool}: give the standard a name (e.g. the company's name).");
            if (s.Length > H4Numbers.MaxNameLength) throw ToolException.Invalid($"{tool}: the name is longer than {H4Numbers.MaxNameLength} characters.");
            if (s.Any(ch => ch < 32 || "\\/:*?\"<>|".IndexOf(ch) >= 0)) throw ToolException.Invalid($"{tool}: the name '{s}' has a character a file name cannot have (\\ / : * ? \" < > |).");
            return s;
        }

        /// <summary>The folder of a standard: its name without accents, lower case, '-' between words ("Công ty ABC" → "cong-ty-abc").</summary>
        public static string Slug(string name)
        {
            var k = EstimateLogic.Key(name);
            var s = Regex.Replace(k, "[^a-z0-9]+", "-").Trim('-');
            if (s.Length == 0) return "chuan";
            // Test & sửa H4: a name Windows keeps for devices (CON, NUL, COM1…) cannot be a folder
            return Regex.IsMatch(s, "^(con|prn|aux|nul|com[0-9]|lpt[0-9])$") ? s + "-chuan" : s;
        }

        public static string FolderOf(string root, string name) => Path.Combine(root, Slug(name));

        // ------------------------------------------------------------ the store

        public static List<CompanyStandard> List(string root) => List(root, null);

        /// <summary>The readable standards; the folders whose standard.json is damaged go to <paramref name="damaged"/>.</summary>
        public static List<CompanyStandard> List(string root, List<string> damaged)
        {
            var list = new List<CompanyStandard>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return list;
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var f = Path.Combine(dir, DataFile);
                if (!File.Exists(f)) continue;
                try { list.Add(FromJson(File.ReadAllText(f, Encoding.UTF8))); }
                catch (Exception) { damaged?.Add(Path.GetFileName(dir)); }
            }
            return list;
        }

        /// <summary>
        /// The standard of that name (accents, case and spaces as written do not matter: the folder decides), or null. Test & sửa
        /// H4: a damaged standard.json is a clear refusal (learn it again or delete it), not a plugin error.
        /// </summary>
        public static CompanyStandard Load(string root, string name, string tool = Tool)
        {
            var f = Path.Combine(FolderOf(root, name), DataFile);
            if (!File.Exists(f)) return null;
            try { return FromJson(File.ReadAllText(f, Encoding.UTF8)); }
            catch (Exception ex) when (!(ex is ToolException))
            {
                throw ToolException.Invalid($"{tool}: the saved company standard '{name}' is damaged ({Path.Combine(FolderOf(root, name), DataFile)}: {ex.Message}). " +
                    "Learn it again from the template (company_standard learn with the same name) or delete it (company_standard delete).");
            }
        }

        /// <summary>Test & sửa H4: the folder of that name holds a standard.json that cannot be read.</summary>
        public static bool IsDamaged(string root, string name)
        {
            var f = Path.Combine(FolderOf(root, name), DataFile);
            if (!File.Exists(f)) return false;
            try { FromJson(File.ReadAllText(f, Encoding.UTF8)); return false; } catch (Exception) { return true; }
        }

        public static CompanyStandard Require(string root, string name, string tool)
        {
            var s = Load(root, ValidName(name, tool), tool);
            if (s != null) return s;
            var names = List(root).Select(x => x.Name).ToList();
            throw ToolException.Invalid($"{tool}: no company standard '{name}'. " + (names.Count == 0
                ? "None is learnt on this computer yet: company_standard action learn with the template file first."
                : "Learnt here: " + string.Join(", ", names) + "."));
        }

        public static void Save(string root, CompanyStandard s)
        {
            var dir = FolderOf(root, s.Name);
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, DataFile + ".tmp");
            File.WriteAllText(tmp, ToJson(s), new UTF8Encoding(false));
            var f = Path.Combine(dir, DataFile);
            if (File.Exists(f)) File.Delete(f);
            File.Move(tmp, f);
        }

        public static bool Delete(string root, string name)
        {
            var dir = FolderOf(root, name);
            if (!Directory.Exists(dir)) return false;
            Directory.Delete(dir, true);
            return true;
        }

        public static string TemplatePath(string root, string name) => Path.Combine(FolderOf(root, name), TemplateFile);

        /// <summary>A new standard may be added (the limit), or one of the same folder is replaced.</summary>
        public static string RoomRefusal(string root, string name)
        {
            var damaged = new List<string>();
            var all = List(root, damaged);
            if (all.Any(x => Slug(x.Name) == Slug(name)) || damaged.Contains(Slug(name))) return null;
            // Test & sửa H4: a damaged standard still takes a place until it is learnt again or deleted
            return all.Count + damaged.Count >= H4Numbers.MaxStandards
                ? $"{Tool}: {all.Count + damaged.Count} company standards are kept on this computer (at most {H4Numbers.MaxStandards}). Delete one first (company_standard action delete): {string.Join(", ", all.Select(x => x.Name))}."
                : null;
        }

        /// <summary>The card token of replacing / deleting a standard (FNV-1a 64, 16 hex digits, never a handle of the drawing).</summary>
        public static string Token(string action, string name)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes("company-standard:" + action + ":" + Slug(name))) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>Property differences of a layer ("color 7 → 1"), empty when the drawing's layer is as the standard says.</summary>
        public static List<string> LayerDifferences(CompanyStandard.Layer want, CompanyStandard.Layer have)
        {
            var d = new List<string>();
            if (!string.Equals(want.Color, have.Color, StringComparison.OrdinalIgnoreCase)) d.Add($"color {have.Color} → {want.Color}");
            if (!string.Equals(want.Linetype, have.Linetype, StringComparison.OrdinalIgnoreCase)) d.Add($"linetype {have.Linetype} → {want.Linetype}");
            if (Math.Abs(want.Lineweight - have.Lineweight) > 1e-6) d.Add($"lineweight {have.Lineweight} → {want.Lineweight}");
            if (want.Plottable != have.Plottable) d.Add($"plot {(have.Plottable ? "yes" : "no")} → {(want.Plottable ? "yes" : "no")}");
            return d;
        }

        // ------------------------------------------------------------ learning helpers

        /// <summary>A0 … A4 and landscape for a paper size in mm (either way round), or null name.</summary>
        public static (string name, bool landscape) PaperOf(double w, double h)
        {
            bool land = w >= h;
            double a = Math.Min(w, h), b = Math.Max(w, h);
            foreach (var p in F2Numbers.Papers)
                if (Math.Abs(p.w - a) <= H4Numbers.PaperTolMm && Math.Abs(p.h - b) <= H4Numbers.PaperTolMm) return (p.name, land);
            return (null, land);
        }

        /// <summary>The most used heights (rounded to 1e-6, at most MaxHeights), most used first.</summary>
        public static List<double> TopHeights(IEnumerable<double> heights)
        {
            return (heights ?? Enumerable.Empty<double>()).Where(h => h > 0 && !double.IsInfinity(h) && !double.IsNaN(h))
                .Select(h => Math.Round(h, 6)).GroupBy(h => h).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Take(H4Numbers.MaxHeights).Select(g => g.Key).ToList();
        }

        /// <summary>Keeps at most max items, and a note saying how many were left out.</summary>
        public static List<T> Cap<T>(List<T> items, int max, string what, List<string> notes)
        {
            if (items.Count <= max) return items;
            notes.Add($"{items.Count} {what} in the template: the first {max} were learnt.");
            return items.Take(max).ToList();
        }

        // ------------------------------------------------------------ checks (check_drawing with a company standard)

        /// <summary>A layer of the standard, or one never counted (0, Defpoints, an xref's, the trace picture, views from the model).</summary>
        public static bool HasLayer(CompanyStandard s, string layer)
        {
            if (string.IsNullOrEmpty(layer) || layer.Contains("|") || layer == "0") return true;
            if (string.Equals(layer, "Defpoints", StringComparison.OrdinalIgnoreCase) || string.Equals(layer, G5Numbers.Layer, StringComparison.OrdinalIgnoreCase)) return true;
            if (Util2DLogic.IsDerivedViewLayer(layer)) return true;
            return s.Layers.Any(l => string.Equals(l.Name, layer, StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasTextStyle(CompanyStandard s, string style) => s.TextStyles.Any(t => string.Equals(t.Name, style, StringComparison.OrdinalIgnoreCase));
        public static bool HasDimStyle(CompanyStandard s, string style) => s.DimStyles.Any(t => string.Equals(t.Name, style, StringComparison.OrdinalIgnoreCase));
        public static bool HasMLeaderStyle(CompanyStandard s, string style) => s.MLeaderStyles.Any(t => string.Equals(t.Name, style, StringComparison.OrdinalIgnoreCase));

        /// <summary>The standard's text heights in this drawing's units (the template's units converted), or null when it has none.</summary>
        public static double[] Heights(CompanyStandard s, double drawingMmPerUnit)
        {
            if (s.TextHeights.Count == 0 || !(drawingMmPerUnit > 0)) return null;
            double k = s.MmPerUnit / drawingMmPerUnit;
            return s.TextHeights.Select(h => h * k).ToArray();
        }

        /// <summary>A layer's colour as the standard writes it: "7" (ACI) or "255,128,0" (true colour).</summary>
        public static string ColorText(bool aci, int index, int r, int g, int b) =>
            aci ? index.ToString(CultureInfo.InvariantCulture) : $"{r},{g},{b}";

        /// <summary>What the chat shows of a standard (counts and the first names).</summary>
        public static Dictionary<string, object> Summary(CompanyStandard s, bool full)
        {
            int n = full ? 200 : 15;
            var d = new Dictionary<string, object>
            {
                ["name"] = s.Name,
                ["source"] = s.Source,
                ["learned_at"] = s.LearnedAt,
                ["units_mm_per_unit"] = s.MmPerUnit,
                ["layers"] = s.Layers.Count,
                ["text_styles"] = s.TextStyles.Select(t => (object)t.Name).ToList(),
                ["dim_styles"] = s.DimStyles.Select(t => (object)t.Name).ToList(),
                ["mleader_styles"] = s.MLeaderStyles.Select(t => (object)t.Name).ToList(),
                ["title_blocks"] = s.TitleBlocks.Select(t => (object)$"{t.Block} ({t.Layout}; {string.Join(", ", t.Tags.Take(20))})").ToList(),
                ["papers"] = s.Papers.Select(p => (object)$"{p.Layout}: {(p.Name ?? p.Media)} {(p.Landscape ? "ngang" : "đứng")}").ToList(),
                ["text_heights"] = s.TextHeights.Select(h => (object)h).ToList(),
                ["template_kept"] = s.TemplateKept,
            };
            d["layer_names"] = s.Layers.Take(n).Select(l => (object)(full ? $"{l.Name} (màu {l.Color}, {l.Linetype}, {(l.Lineweight < 0 ? "mặc định" : l.Lineweight.ToString(CultureInfo.InvariantCulture) + " mm")}{(l.Plottable ? "" : ", không in")}{(l.Description.Length > 0 ? ", " + l.Description : "")})" : l.Name)).ToList();
            if (s.Layers.Count > n) d["layer_names_more"] = s.Layers.Count - n;
            if (s.Notes.Count > 0) d["notes"] = s.Notes.Cast<object>().ToList();
            return d;
        }

        // ------------------------------------------------------------ JSON

        private static double D(object o, double def = 0) => o == null ? def : Convert.ToDouble(o, CultureInfo.InvariantCulture);
        private static string S(object o, string def = "") => o == null ? def : Convert.ToString(o, CultureInfo.InvariantCulture);
        private static List<object> L(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
        private static object G(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v : null;

        public static string ToJson(CompanyStandard s)
        {
            var d = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["name"] = s.Name, ["source"] = s.Source, ["learned_at"] = s.LearnedAt,
                ["insunits"] = s.Insunits, ["mm_per_unit"] = s.MmPerUnit, ["template_kept"] = s.TemplateKept,
                ["current_text_style"] = s.CurrentTextStyle, ["current_dim_style"] = s.CurrentDimStyle, ["current_mleader_style"] = s.CurrentMLeaderStyle,
                ["layers"] = s.Layers.Select(l => (object)new Dictionary<string, object>
                {
                    ["name"] = l.Name, ["color"] = l.Color, ["linetype"] = l.Linetype, ["lineweight"] = l.Lineweight, ["description"] = l.Description, ["plottable"] = l.Plottable,
                }).ToList(),
                ["text_styles"] = s.TextStyles.Select(t => (object)new Dictionary<string, object>
                {
                    ["name"] = t.Name, ["font"] = t.Font, ["bigfont"] = t.BigFont, ["height"] = t.Height, ["width_factor"] = t.WidthFactor, ["oblique"] = t.Oblique,
                }).ToList(),
                ["dim_styles"] = s.DimStyles.Select(t => (object)new Dictionary<string, object>
                {
                    ["name"] = t.Name, ["text_style"] = t.TextStyle, ["text_height"] = t.TextHeight, ["arrow_size"] = t.ArrowSize, ["scale"] = t.Scale,
                }).ToList(),
                ["mleader_styles"] = s.MLeaderStyles.Select(t => (object)new Dictionary<string, object>
                {
                    ["name"] = t.Name, ["text_style"] = t.TextStyle, ["text_height"] = t.TextHeight, ["arrow_size"] = t.ArrowSize, ["scale"] = t.Scale,
                }).ToList(),
                ["title_blocks"] = s.TitleBlocks.Select(t => (object)new Dictionary<string, object>
                {
                    ["block"] = t.Block, ["layout"] = t.Layout, ["tags"] = t.Tags.Cast<object>().ToList(), ["x"] = t.X, ["y"] = t.Y, ["rotation"] = t.Rotation, ["scale"] = t.Scale,
                }).ToList(),
                ["papers"] = s.Papers.Select(p => (object)new Dictionary<string, object>
                {
                    ["layout"] = p.Layout, ["media"] = p.Media, ["paper"] = p.Name, ["w"] = p.W, ["h"] = p.H, ["landscape"] = p.Landscape,
                    ["viewport"] = p.Viewport?.Cast<object>().ToList(),
                }).ToList(),
                ["text_heights"] = s.TextHeights.Cast<object>().ToList(),
                ["notes"] = s.Notes.Cast<object>().ToList(),
            };
            return JsonCodec.Serialize(d);
        }

        public static CompanyStandard FromJson(string json)
        {
            var d = JsonCodec.ParseObject(json);
            var s = new CompanyStandard
            {
                Name = S(G(d, "name")), Source = S(G(d, "source")), LearnedAt = S(G(d, "learned_at")),
                Insunits = (int)D(G(d, "insunits"), 4), MmPerUnit = D(G(d, "mm_per_unit"), 1), TemplateKept = G(d, "template_kept") is bool b && b,
                CurrentTextStyle = S(G(d, "current_text_style")), CurrentDimStyle = S(G(d, "current_dim_style")), CurrentMLeaderStyle = S(G(d, "current_mleader_style")),
            };
            foreach (Dictionary<string, object> x in L(d, "layers").OfType<Dictionary<string, object>>())
                s.Layers.Add(new CompanyStandard.Layer
                {
                    Name = S(G(x, "name")), Color = S(G(x, "color"), "7"), Linetype = S(G(x, "linetype"), "Continuous"),
                    Lineweight = D(G(x, "lineweight"), -1), Description = S(G(x, "description")), Plottable = !(G(x, "plottable") is bool p) || p,
                });
            foreach (Dictionary<string, object> x in L(d, "text_styles").OfType<Dictionary<string, object>>())
                s.TextStyles.Add(new CompanyStandard.TextStyle
                {
                    Name = S(G(x, "name")), Font = S(G(x, "font")), BigFont = S(G(x, "bigfont")), Height = D(G(x, "height")), WidthFactor = D(G(x, "width_factor"), 1), Oblique = D(G(x, "oblique")),
                });
            foreach (Dictionary<string, object> x in L(d, "dim_styles").OfType<Dictionary<string, object>>())
                s.DimStyles.Add(new CompanyStandard.DimStyle { Name = S(G(x, "name")), TextStyle = S(G(x, "text_style")), TextHeight = D(G(x, "text_height")), ArrowSize = D(G(x, "arrow_size")), Scale = D(G(x, "scale"), 1) });
            foreach (Dictionary<string, object> x in L(d, "mleader_styles").OfType<Dictionary<string, object>>())
                s.MLeaderStyles.Add(new CompanyStandard.MLeaderStyle { Name = S(G(x, "name")), TextStyle = S(G(x, "text_style")), TextHeight = D(G(x, "text_height")), ArrowSize = D(G(x, "arrow_size")), Scale = D(G(x, "scale"), 1) });
            foreach (Dictionary<string, object> x in L(d, "title_blocks").OfType<Dictionary<string, object>>())
                s.TitleBlocks.Add(new CompanyStandard.TitleBlock
                {
                    Block = S(G(x, "block")), Layout = S(G(x, "layout")), Tags = L(x, "tags").Select(t => S(t)).ToList(),
                    X = D(G(x, "x")), Y = D(G(x, "y")), Rotation = D(G(x, "rotation")), Scale = D(G(x, "scale"), 1),
                });
            foreach (Dictionary<string, object> x in L(d, "papers").OfType<Dictionary<string, object>>())
            {
                var vp = L(x, "viewport");
                s.Papers.Add(new CompanyStandard.Paper
                {
                    Layout = S(G(x, "layout")), Media = S(G(x, "media")), Name = G(x, "paper") == null ? null : S(G(x, "paper")),
                    W = D(G(x, "w")), H = D(G(x, "h")), Landscape = !(G(x, "landscape") is bool l) || l,
                    Viewport = vp.Count == 4 ? vp.Select(v => D(v)).ToArray() : null,
                });
            }
            s.TextHeights = L(d, "text_heights").Select(v => D(v)).ToList();
            s.Notes = L(d, "notes").Select(v => S(v)).ToList();
            return s;
        }
    }
}
