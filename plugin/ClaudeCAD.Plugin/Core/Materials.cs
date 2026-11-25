using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F3, AutoCAD-free part of set_material and named_view: the preset materials (colour + opacity, all
    /// numbers in one place for the owner to change), the checks of the parameters and the PNG path of a picture.
    /// The materials are plain AutoCAD materials (diffuse colour, opacity; no texture image): the Realistic visual style
    /// shows them, in the viewport and in named_view image / capture_view visual_style realistic.
    /// </summary>
    public static class MaterialLogic
    {
        public sealed class Preset
        {
            public string Key, Name;
            public int R, G, B;
            /// <summary>0–100 %, 100 = opaque.</summary>
            public double Opacity = 100;
            public string[] Aliases = new string[0];
        }

        /// <summary>
        /// The preset materials. Colours are what the Realistic visual style shows (no texture): chosen by usual looks,
        /// waiting for the owner's review (docs/ban-giao/nang-cap-f3.md, "Con số chờ chủ dự án duyệt").
        /// </summary>
        public static readonly IReadOnlyList<Preset> Presets = new List<Preset>
        {
            new Preset { Key = "concrete", Name = "Bê tông", R = 165, G = 165, B = 160, Aliases = new[] { "bê tông", "be tong", "betong" } },
            new Preset { Key = "brick", Name = "Gạch", R = 170, G = 74, B = 50, Aliases = new[] { "gạch", "gach", "tường gạch" } },
            new Preset { Key = "plaster", Name = "Vữa trắng", R = 236, G = 232, B = 222, Aliases = new[] { "vữa", "vua", "sơn trắng", "son trang", "trát" } },
            new Preset { Key = "glass", Name = "Kính", R = 150, G = 200, B = 220, Opacity = 35, Aliases = new[] { "kính", "kinh" } },
            new Preset { Key = "wood", Name = "Gỗ", R = 150, G = 100, B = 60, Aliases = new[] { "gỗ", "go" } },
            new Preset { Key = "roof_tile", Name = "Ngói", R = 180, G = 80, B = 45, Aliases = new[] { "ngói", "ngoi", "tile", "roof tile" } },
            new Preset { Key = "steel", Name = "Thép", R = 120, G = 130, B = 140, Aliases = new[] { "thép", "thep", "metal" } },
            new Preset { Key = "stone", Name = "Đá", R = 140, G = 135, B = 125, Aliases = new[] { "đá", "da", "granite" } },
        };

        public static readonly string[] PresetKeys = Presets.Select(p => p.Key).ToArray();

        /// <summary>Material names AutoCAD keeps in every drawing; "ByLayer" / "ByBlock" reset an object.</summary>
        public static readonly string[] Builtin = { "ByLayer", "ByBlock", "Global" };

        /// <summary>
        /// Test & sửa đợt F3: Global is the default material of every layer, so its colour / opacity is never changed
        /// (that would recolour the whole drawing in the Realistic style). It can still be given to objects / layers.
        /// </summary>
        public static bool IsProtected(string name) => string.Equals((name ?? "").Trim(), "Global", StringComparison.OrdinalIgnoreCase);

        /// <summary>The preset of a key, Vietnamese name or alias (case / spacing free), or null.</summary>
        public static Preset Find(string name)
        {
            var s = (name ?? "").Trim().ToLowerInvariant().Replace("-", " ").Replace("_", " ");
            if (s.Length == 0) return null;
            foreach (var p in Presets)
                if (p.Key.Replace("_", " ") == s || p.Name.ToLowerInvariant() == s || p.Aliases.Contains(s)) return p;
            return null;
        }

        /// <summary>A material name AutoCAD accepts (symbol name rules), else the reason.</summary>
        public static string BadName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "the material name is empty";
            if (name.Length > 255) return "the material name is longer than 255 characters";
            var bad = name.Where(ch => ch < 32 || "<>/\\\":;?*|,=`".IndexOf(ch) >= 0).Distinct().ToList();
            if (bad.Count > 0) return "the material name contains " + string.Join(" ", bad.Select(ch => ch < 32 ? "a control character" : ch.ToString()));
            return null;
        }

        /// <summary>Checks a custom colour [r, g, b] (0–255) and opacity (0–100 %).</summary>
        public static void CheckLook(double[] rgb, double? opacity, string tool = "set_material")
        {
            if (rgb != null)
            {
                if (rgb.Length != 3 || rgb.Any(v => double.IsNaN(v) || v < 0 || v > 255 || Math.Abs(v - Math.Round(v)) > 1e-9))
                    throw ToolException.Invalid($"{tool}: color must be [r, g, b] with whole numbers 0–255.");
            }
            if (opacity.HasValue && !(opacity.Value >= 0 && opacity.Value <= 100))
                throw ToolException.Invalid($"{tool}: opacity must be 0–100 (%, 100 = opaque; got {opacity}).");
        }

        // ------------------------------------------------------------ named views

        public static readonly string[] ViewActions = { "save", "restore", "list", "delete", "image" };

        /// <summary>Long side (px) of a picture saved by named_view image (the largest the off-screen capture renders).</summary>
        public const int DefaultImageSize = 1568;

        /// <summary>A view name AutoCAD accepts, else the reason.</summary>
        public static string BadViewName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "the view name is empty";
            if (name.Length > 255) return "the view name is longer than 255 characters";
            var bad = name.Where(ch => ch < 32 || "<>/\\\":;?*|,=`".IndexOf(ch) >= 0).Distinct().ToList();
            if (bad.Count > 0) return "the view name contains " + string.Join(" ", bad.Select(ch => ch < 32 ? "a control character" : ch.ToString()));
            return null;
        }

        public static string NormalizeAction(string a)
        {
            var s = (a ?? "").Trim().ToLowerInvariant();
            if (!ViewActions.Contains(s)) throw ToolException.Invalid($"named_view: action must be one of {string.Join(", ", ViewActions)} (got '{a}').");
            return s;
        }

        /// <summary>
        /// The full path of the PNG of named_view image: a relative path goes next to the drawing (like plot_pdf); default
        /// "&lt;drawing&gt;-&lt;view&gt;.png". The folder must exist; the name must end in .png.
        /// </summary>
        public static string ImagePath(string output, string drawingDir, string drawingName, string viewName)
        {
            if (string.IsNullOrEmpty(output))
            {
                var stem = string.IsNullOrWhiteSpace(drawingName) ? "ban-ve" : drawingName;
                var tail = string.IsNullOrWhiteSpace(viewName) ? "3d" : viewName;
                output = SafeFile(stem + "-" + tail) + ".png";
            }
            if (!output.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid("named_view: 'output' must end with .png.");
            string full;
            try { full = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(drawingDir ?? "", output)); }
            catch (Exception) { throw ToolException.Invalid($"named_view: '{output}' is not a valid file path."); }
            var bad = Util2DLogic.BadFileName(Path.GetFileName(full));
            if (bad != null) throw ToolException.Invalid($"named_view: '{output}': {bad}. Give another file name ending in .png.");
            return full;
        }

        private static string SafeFile(string s) =>
            new string(s.Select(ch => ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0 ? '_' : ch).ToArray()).Trim().TrimEnd('.');
    }
}
