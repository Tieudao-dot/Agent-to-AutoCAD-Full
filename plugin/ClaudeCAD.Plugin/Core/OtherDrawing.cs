using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H3 (shared with H4): numbers of "open another drawing only to read it" (Drawing/OtherDrawing.cs).
    /// </summary>
    public static class OtherDrawingNumbers
    {
        /// <summary>The biggest drawing file read (MB): a bigger one is refused with a clear message (memory of AutoCAD).</summary>
        public const int MaxFileMb = 200;
    }

    /// <summary>
    /// Nâng cấp đợt H3 (shared with H4): the AutoCAD-free part of reading another DWG / DXF file next to the open drawing: the
    /// kind of file, the release of a DWG from its first bytes, and the messages for Claude when it cannot be read (in use,
    /// newer than this AutoCAD, damaged, not a drawing). The file itself is opened by Drawing/OtherDrawing.cs, read only.
    /// </summary>
    public static class OtherDrawingLogic
    {
        /// <summary>DWG file codes (the first 6 bytes) and the AutoCAD release that first saved them.</summary>
        public static readonly (string code, string release)[] Releases =
        {
            ("AC1009", "AutoCAD R11 / R12"), ("AC1012", "AutoCAD R13"), ("AC1014", "AutoCAD R14"), ("AC1015", "AutoCAD 2000"),
            ("AC1018", "AutoCAD 2004"), ("AC1021", "AutoCAD 2007"), ("AC1024", "AutoCAD 2010"), ("AC1027", "AutoCAD 2013"),
            ("AC1032", "AutoCAD 2018"),
        };

        /// <summary>"dwg" / "dxf", else a refusal (null path, missing file, other extension, empty or too big).</summary>
        public static string KindOf(string path, bool exists, long bytes, string tool)
        {
            if (string.IsNullOrWhiteSpace(path)) throw ToolException.Invalid($"{tool}: give the full path of the other drawing (.dwg or .dxf).");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".dwg" && ext != ".dxf")
            {
                string hint = ext == ".dwl" || ext == ".dwl2" ? " That is AutoCAD's lock file: give the .dwg next to it." :
                    ext == ".bak" || ext == ".sv$" ? " That is a backup: ask the user to rename a copy of it to .dwg first." :
                    " Ask the user for the .dwg (or .dxf) file.";
                throw ToolException.Invalid($"{tool}: '{Path.GetFileName(path)}' ({(ext.Length == 0 ? "no extension" : ext)}) is not a drawing.{hint}");
            }
            if (!exists) throw ToolException.Invalid($"{tool}: no file '{path}'. Ask the user for the right path (or to drag the file into the chat).");
            if (bytes <= 0) throw ToolException.Invalid($"{tool}: '{Path.GetFileName(path)}' is empty.");
            if (bytes > (long)OtherDrawingNumbers.MaxFileMb * 1024 * 1024)
                throw ToolException.Invalid($"{tool}: '{Path.GetFileName(path)}' is {bytes / 1048576.0:0.#} MB, more than {OtherDrawingNumbers.MaxFileMb} MB: too big to read next to the open drawing.");
            return ext.Substring(1);
        }

        /// <summary>The file code of a DWG ("AC1032") from its first bytes, or null when the bytes are not a DWG header.</summary>
        public static string DwgCode(byte[] head)
        {
            if (head == null || head.Length < 6) return null;
            var s = Encoding.ASCII.GetString(head, 0, 6);
            if (s[0] != 'A' || s[1] != 'C' || !s.Skip(2).All(char.IsDigit)) return null;
            return s;
        }

        /// <summary>"AutoCAD 2018" for a known code, "a newer AutoCAD" above the newest known, "an old AutoCAD" below.</summary>
        public static string ReleaseOf(string code)
        {
            if (code == null) return "an unknown program";
            foreach (var (c, r) in Releases) if (c == code) return r;
            return string.CompareOrdinal(code, Releases[Releases.Length - 1].code) > 0 ? "a newer AutoCAD" : "an old AutoCAD";
        }

        /// <summary>True when the file code is newer than the drawing format of the running AutoCAD (codes compare as text).</summary>
        public static bool IsNewer(string fileCode, string ourCode) =>
            fileCode != null && ourCode != null && string.CompareOrdinal(fileCode, ourCode) > 0;

        /// <summary>Refusal of a .dwg whose header is not a drawing, or that this AutoCAD cannot open (null = go on).</summary>
        public static string HeaderRefusal(string path, string code, string ourCode, string tool)
        {
            string name = Path.GetFileName(path);
            if (code == null) return $"{tool}: '{name}' does not start like a DWG file (damaged, or another kind of file renamed .dwg). Ask the user to open it in AutoCAD and save it again.";
            if (IsNewer(code, ourCode))
                return $"{tool}: '{name}' was saved by a newer AutoCAD ({code}) than this one can read ({ourCode}). Ask the user to save it from the newer AutoCAD with \"Save As\" an older format (AutoCAD 2018 drawing).";
            return null;
        }

        /// <summary>
        /// The message for an AutoCAD error while reading the file, from the name of its ErrorStatus: in use / no permission,
        /// newer, or damaged (anything else).
        /// </summary>
        public static string ReadFailure(string path, string status, string code, string ourCode, string tool)
        {
            string name = Path.GetFileName(path);
            string s = status ?? "";
            if (Has(s, "Sharing") || Has(s, "Locked") || Has(s, "AccessErr") || Has(s, "AccessDenied") || Has(s, "NoPermission"))
                return $"{tool}: '{name}' is open and locked by another program or person, or this user may not read it ({s}). Ask the user to close it there (or copy the file) and try again. Nothing was changed.";
            if (IsNewer(code, ourCode) || Has(s, "HigherVersion") || Has(s, "NewerVersion") || Has(s, "FileVersion"))
                return $"{tool}: '{name}' was saved by a newer AutoCAD ({code ?? "?"}) than this one can read ({ourCode ?? "?"}). Ask the user to save it as an older format (AutoCAD 2018 drawing).";
            return $"{tool}: '{name}' could not be read ({(s.Length == 0 ? "unknown error" : s)}): it may be damaged. Ask the user to open it in AutoCAD (RECOVER) and save it again. Nothing was changed.";
        }

        private static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The same file as the open drawing (full paths, case and slashes ignored).</summary>
        public static bool SameFile(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            string N(string p) => p.Trim().Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
            return N(a) == N(b);
        }

        /// <summary>The newest code a list of DwgVersion enum names gives ("AC1032" from "AC1015", …, "AC1032", "Current").</summary>
        public static string NewestCode(string[] enumNames, Func<string, int> valueOf, int current)
        {
            var codes = (enumNames ?? new string[0]).Where(n => n != null && n.Length == 6 && DwgCode(Encoding.ASCII.GetBytes(n)) != null).ToList();
            var same = codes.Where(n => valueOf(n) == current).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (same.Count > 0) return same[same.Count - 1];
            return codes.OrderBy(n => n, StringComparer.Ordinal).LastOrDefault();
        }

        public static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture);
    }
}
