using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt G4: the numbers and words the user meets with a real render (named_view action render), one place each
    /// (docs/ban-giao/nang-cap-g4.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class G4Numbers
    {
        /// <summary>Default size of a rendered picture (px): Full HD, the usual size of a slide / screen.</summary>
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        /// <summary>Smallest / largest side Claude may ask (px).</summary>
        public const int MinSide = 64;
        public const int MaxSide = 4096;

        public const string DefaultQuality = "medium";
        public const string DefaultBackground = "white";
        public const string DefaultLighting = "auto";

        /// <summary>Background colours (RGB): white, and the sky gradient (top, middle, bottom).</summary>
        public static readonly int[] White = { 255, 255, 255 };
        public static readonly int[] SkyTop = { 110, 160, 215 };
        public static readonly int[] SkyMiddle = { 185, 210, 235 };
        public static readonly int[] SkyBottom = { 240, 240, 240 };

        /// <summary>How often the assistant asks AutoCAD whether the render is finished (ms; not seen by the user).</summary>
        public const int PollMs = 2000;
        /// <summary>Seconds the output file must stay the same size before it counts as written (not seen).</summary>
        public const double StableSeconds = 1.5;
        /// <summary>
        /// Test & sửa G4: seconds after the RENDER command ended with no picture written before it counts as failed (the
        /// way back starts then, not at the max wait). Not seen by the user.
        /// </summary>
        public const double EndGraceSeconds = 20;
    }

    /// <summary>
    /// Nâng cấp đợt G4, AutoCAD-free part of the real render: qualities, sizes, the -RENDER answers, the time estimate, the
    /// decision "still running / done / too long" and the way back to the picture of F3 (a capture, said as such).
    /// </summary>
    public static class RenderLogic
    {
        public sealed class Quality
        {
            public string Key, Name, Preset;
            /// <summary>Rough seconds for a Full HD picture (the estimate shown to the user).</summary>
            public int EstimateSeconds;
            /// <summary>After this many seconds the render is stopped and a capture is saved instead.</summary>
            public int MaxWaitSeconds;
        }

        /// <summary>The three qualities: AutoCAD's own render presets Low / Medium / High.</summary>
        public static readonly IReadOnlyList<Quality> Qualities = new List<Quality>
        {
            new Quality { Key = "low", Name = "Nhanh (nháp)", Preset = "Low", EstimateSeconds = 20, MaxWaitSeconds = 120 },
            new Quality { Key = "medium", Name = "Vừa", Preset = "Medium", EstimateSeconds = 60, MaxWaitSeconds = 300 },
            new Quality { Key = "high", Name = "Đẹp (chậm)", Preset = "High", EstimateSeconds = 180, MaxWaitSeconds = 900 },
        };

        /// <summary>The actions of a real render (all read the drawing only: its settings come back when the render ends).</summary>
        public static bool IsRenderAction(string a) => a == "render" || a == "render_status" || a == "render_stop";

        public static readonly string[] Backgrounds = { "white", "sky", "drawing" };
        public static readonly string[] Lightings = { "auto", "default", "sun", "drawing" };

        public static Quality QualityOf(string key, string tool = "named_view")
        {
            var s = (key ?? G4Numbers.DefaultQuality).Trim().ToLowerInvariant();
            var q = Qualities.FirstOrDefault(x => x.Key == s);
            if (q == null) throw ToolException.Invalid($"{tool}: quality must be one of {string.Join(", ", Qualities.Select(x => x.Key))} (got '{key}').");
            return q;
        }

        public static string OneOf(string value, string[] allowed, string fallback, string what, string tool = "named_view")
        {
            var s = (value ?? fallback).Trim().ToLowerInvariant();
            if (!allowed.Contains(s)) throw ToolException.Invalid($"{tool}: {what} must be one of {string.Join(", ", allowed)} (got '{value}').");
            return s;
        }

        /// <summary>Width × height of the render (defaults Full HD; one side given: the other keeps 16:9).</summary>
        public static (int w, int h) Size(double? width, double? height, string tool = "named_view")
        {
            int Check(double v, string n)
            {
                if (double.IsNaN(v) || v < G4Numbers.MinSide || v > G4Numbers.MaxSide || Math.Abs(v - Math.Round(v)) > 1e-9)
                    throw ToolException.Invalid($"{tool}: {n} must be a whole number of pixels {G4Numbers.MinSide}–{G4Numbers.MaxSide} (got {v}).");
                return (int)Math.Round(v);
            }
            const double ratio = (double)G4Numbers.DefaultWidth / G4Numbers.DefaultHeight;
            if (width == null && height == null) return (G4Numbers.DefaultWidth, G4Numbers.DefaultHeight);
            if (width != null && height != null) return (Check(width.Value, "width"), Check(height.Value, "height"));
            if (width != null)
            {
                int w = Check(width.Value, "width");
                return (w, Math.Max(G4Numbers.MinSide, Math.Min(G4Numbers.MaxSide, (int)Math.Round(w / ratio))));
            }
            int h = Check(height.Value, "height");
            return (Math.Max(G4Numbers.MinSide, Math.Min(G4Numbers.MaxSide, (int)Math.Round(h * ratio))), h);
        }

        /// <summary>Rough seconds: the Full HD estimate of the quality scaled by the pixels, 5 s at least, never past the max wait.</summary>
        public static int Estimate(Quality q, int w, int h)
        {
            double f = (double)w * h / (G4Numbers.DefaultWidth * (double)G4Numbers.DefaultHeight);
            return (int)Math.Max(5, Math.Min(q.MaxWaitSeconds, Math.Round(q.EstimateSeconds * f)));
        }

        /// <summary>
        /// The answers typed to -RENDER (AutoCAD 2016+ renderer): preset, destination Render Window, width, height, save to a
        /// file Yes, the file. Each answer ends with Enter ("\n") so a path with spaces stays one answer; "_" = the English
        /// keyword on any language of AutoCAD. Kept here, one place, if a version asks in another order.
        /// </summary>
        public static string CommandScript(Quality q, int w, int h, string file) =>
            "_-RENDER\n_" + q.Preset + "\n_R\n" + w.ToString(CultureInfo.InvariantCulture) + "\n" + h.ToString(CultureInfo.InvariantCulture) +
            "\n_Y\n" + (file ?? "").Replace("\"", "") + "\n";

        /// <summary>The temporary file AutoCAD renders into (copied to the user's file once finished): no spaces in its own name.</summary>
        public static string TempFile(string tempDir, string token) => Path.Combine(tempDir ?? "", "claudecad-render-" + token + ".png");

        public enum State { Running, Done, TooLong, Cancelled, Failed, NoFile }

        /// <summary>
        /// Where a render is: the file written and its size unchanged for StableSeconds = done; the user pressed Esc / the
        /// command failed = cancelled / failed; past the max wait = too long (stop, save a capture instead); else running.
        /// </summary>
        public static State StateOf(double elapsed, int maxWait, long fileSize, long sizeBefore, double sameSizeFor, bool cancelled, bool failed) =>
            StateOf(elapsed, maxWait, fileSize, sizeBefore, sameSizeFor, cancelled, failed, -1);

        /// <summary>
        /// Test & sửa G4: endedFor = seconds since the RENDER command ended (negative: still running). Ended with no picture
        /// (none, or an empty file) for EndGraceSeconds = NoFile: the way back starts at once instead of at the max wait.
        /// </summary>
        public static State StateOf(double elapsed, int maxWait, long fileSize, long sizeBefore, double sameSizeFor, bool cancelled, bool failed, double endedFor)
        {
            if (fileSize > 0 && fileSize == sizeBefore && sameSizeFor >= G4Numbers.StableSeconds) return State.Done;
            if (cancelled) return State.Cancelled;
            if (failed) return State.Failed;
            if (endedFor >= G4Numbers.EndGraceSeconds && fileSize <= 0) return State.NoFile;
            if (elapsed > maxWait) return State.TooLong;
            return State.Running;
        }

        /// <summary>
        /// Test & sửa G4: an "unknown command" AutoCAD reports during a render that is one of the answers typed to -RENDER
        /// (not the command itself): -RENDER ended before all its answers were used, so it asked its questions in another
        /// order than CommandScript expects. The render is given up at once (the way back), not after the max wait.
        /// </summary>
        public static bool IsOurAnswer(string unknown, Quality q, int w, int h, string file)
        {
            var u = (unknown ?? "").Trim().TrimStart('_').Trim();
            if (u.Length == 0) return false;
            var answers = new[] { q.Preset, "R", w.ToString(CultureInfo.InvariantCulture), h.ToString(CultureInfo.InvariantCulture), "Y", (file ?? "").Replace("\"", "") };
            return answers.Any(a => a.Length > 0 && (string.Equals(u, a, StringComparison.OrdinalIgnoreCase)
                || (a.Length > 3 && a.StartsWith(u, StringComparison.OrdinalIgnoreCase) && u.Length >= 3)));
        }

        /// <summary>
        /// Test & sửa G4: a folder for the temporary render file whose path is typed safely at the command line: ASCII letters,
        /// digits and \ / : . _ - ~ only (a space would end the answer and type the rest as commands; accents may not survive).
        /// The first candidate that qualifies, or null (then no render is tried: the picture of F3 at once).
        /// </summary>
        public static string SafeTempDir(IEnumerable<string> candidates)
        {
            foreach (var c in candidates ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                if (c.All(ch => ch < 128 && (char.IsLetterOrDigit(ch) || "\\/:._-~".IndexOf(ch) >= 0))) return c;
            }
            return null;
        }

        /// <summary>One Vietnamese line of progress for the palette ("Đang render 20 s / khoảng 60 s").</summary>
        public static string Progress(double elapsed, int estimate, int maxWait)
        {
            int e = (int)Math.Max(0, Math.Round(elapsed));
            if (e <= estimate) return $"đang render {e} s / khoảng {estimate} s";
            return $"đang render {e} s (lâu hơn dự kiến; tự dừng ở {maxWait} s)";
        }

        /// <summary>Why the picture is a capture and not a render, in a sentence Claude passes on.</summary>
        public static string FallbackReason(State s, string error = null)
        {
            switch (s)
            {
                case State.TooLong: return "the render took longer than the maximum wait and was stopped";
                case State.NoFile: return "the RENDER command ended without writing the picture" + (string.IsNullOrWhiteSpace(error) ? "" : " (" + error + ")");
                case State.Failed: return "AutoCAD could not render (" + (string.IsNullOrWhiteSpace(error) ? "the RENDER command failed" : error) + ")";
                default: return string.IsNullOrWhiteSpace(error) ? "the render did not run" : error;
            }
        }

        /// <summary>
        /// Lighting of the render: "auto" = the drawing's own lights / sun when it has some, else AutoCAD's default light
        /// (turned on for the render only); "default" = default light; "sun" = the drawing's sun on (its own date / time;
        /// default light when the drawing has no sun); "drawing" = as it is. Returns (default light on?, sun on?) wanted, null = leave.
        /// </summary>
        public static (bool? defaultLight, bool? sun) LightPlan(string lighting, int userLights, bool sunOn, bool hasSun, bool defaultLightOn)
        {
            switch (lighting)
            {
                case "drawing": return (null, null);
                case "default": return (defaultLightOn ? (bool?)null : true, sunOn ? false : (bool?)null);
                case "sun":
                    if (!hasSun) return (defaultLightOn ? (bool?)null : true, null);
                    return (defaultLightOn ? false : (bool?)null, sunOn ? (bool?)null : true);
                default: // auto
                    if (userLights > 0 || sunOn) return (null, null);
                    return (defaultLightOn ? (bool?)null : true, null);
            }
        }

        /// <summary>The words of the lighting used, for the result.</summary>
        public static string LightWords(string lighting, int userLights, bool sunOn, bool hasSun)
        {
            switch (lighting)
            {
                case "drawing": return "the drawing's own lighting (unchanged)";
                case "default": return "AutoCAD default light";
                case "sun": return hasSun ? "the drawing's sun (its own date and time)" : "AutoCAD default light (the drawing has no sun yet)";
                default:
                    if (userLights > 0) return $"the drawing's {userLights} light(s)" + (sunOn ? " and sun" : "");
                    if (sunOn) return "the drawing's sun";
                    return "AutoCAD default light (the drawing has no light)";
            }
        }

        /// <summary>The default PNG name of a render: "&lt;drawing&gt;-&lt;view&gt;-render.png" (the view: the saved one, else "hien-tai").</summary>
        public static string Tail(string viewName) => (string.IsNullOrWhiteSpace(viewName) ? "hien-tai" : viewName.Trim()) + "-render";
    }

    /// <summary>
    /// Nâng cấp đợt G4: the friendly material names mapped to materials of the Autodesk library already in the drawing. AutoCAD
    /// has no programming interface to copy a material out of the library: a library material is used when the user (or the
    /// template) brought it into the drawing (Materials Browser); otherwise the simple material of F3, and the result says so.
    /// </summary>
    public static class MaterialLibraryLogic
    {
        /// <summary>English words of the library names (case-free, whole words) per preset key.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Words = new Dictionary<string, string[]>
        {
            ["concrete"] = new[] { "concrete" },
            ["brick"] = new[] { "brick" },
            ["plaster"] = new[] { "plaster", "paint", "stucco" },
            ["glass"] = new[] { "glass", "glazing" },
            ["wood"] = new[] { "wood", "oak", "walnut", "maple", "cherry", "pine" },
            ["roof_tile"] = new[] { "roofing", "shingles", "roof" },
            ["steel"] = new[] { "steel", "metal", "aluminum", "aluminium" },
            ["stone"] = new[] { "stone", "granite", "marble" },
        };

        /// <summary>
        /// The drawing material taken for a preset: the first by name (ordinal, case-free) whose name holds one of the words
        /// as a whole word, never AutoCAD's own (ByLayer / ByBlock / Global) nor one of the simple preset materials; else null.
        /// </summary>
        public static string Match(string presetKey, IEnumerable<string> drawingMaterials)
        {
            if (presetKey == null || !Words.TryGetValue(presetKey, out var words)) return null;
            var simple = new HashSet<string>(MaterialLogic.Presets.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            return (drawingMaterials ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n) && !MaterialLogic.Builtin.Contains(n, StringComparer.OrdinalIgnoreCase) && !simple.Contains(n.Trim()))
                .Where(n => words.Any(w => HasWord(n, w)))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool HasWord(string name, string word)
        {
            var parts = name.ToLowerInvariant().Split(new[] { ' ', '-', '_', ',', '.', '(', ')', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Contains(word);
        }

        /// <summary>The folders the Autodesk material library installs to (Common Files\Autodesk Shared\Materials).</summary>
        public static IEnumerable<string> Folders(string commonFiles, string commonFilesX86)
        {
            foreach (var root in new[] { commonFilesX86, commonFiles }.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
                yield return Path.Combine(root, "Autodesk Shared", "Materials");
        }

        /// <summary>The words of the result: which way the material came and how to get the library one.</summary>
        public static string Note(string source, bool libraryInstalled, string presetName, string materialName = null)
        {
            // Test & sửa G4: the match is by name only (a user's own material named "Brick wall" matches too): say so
            if (source == "library")
                return "Used the material " + (string.IsNullOrWhiteSpace(materialName) ? "" : "'" + materialName + "' ") +
                       "already in the drawing whose name matches (usually an Autodesk library material brought in from the Materials Browser). It was used as it is, not changed.";
            if (libraryInstalled)
                return $"Simple material '{presetName}' (colour only): the Autodesk library is installed but none of its {presetName} materials is in this drawing. " +
                       "To use one: open the Materials Browser (MATBROWSEROPEN), drag a material into the drawing, then call set_material again.";
            return $"Simple material '{presetName}' (colour only): the Autodesk material library is not installed on this computer.";
        }
    }
}
