using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// The verdicts of CLAUDECHATSELFTEST that do not need AutoCAD: SelfTest.cs gathers the facts inside
    /// AutoCAD (versions, theme, palette state), these rules turn them into ✓ / ✗ with a reason. Kept apart
    /// so they are unit-tested on CI.
    /// </summary>
    public static class SelfTestRules
    {
        /// <summary>One self-test line: passed or not, and why.</summary>
        public struct Verdict
        {
            public bool Ok;
            public string Detail;
            public Verdict(bool ok, string detail) { Ok = ok; Detail = detail ?? ""; }
        }

        // ================================================================= 3D: volume of a built solid

        /// <summary>✓ when the solid's volume is within 0.1 % of the expected one (mm³; shown in m³ and mm³).</summary>
        public static Verdict CheckVolume(string what, double actual, double expected)
        {
            var text = string.Format(CultureInfo.InvariantCulture, "{0}: thể tích {1:0.######} m³ (cần {2:0.######} m³)",
                what, actual / 1e9, expected / 1e9);
            if (double.IsNaN(actual) || actual <= 0) return new Verdict(false, what + ": khối rỗng, không đo được thể tích");
            return SolidLogic.VolumeMatches(actual, expected) ? new Verdict(true, text) : new Verdict(false, text + " — lệch quá 0,1 %");
        }

        // ================================================================= build ↔ AutoCAD series

        public const string Net48 = "net48";
        public const string Net8 = "net8";
        public const string Net10 = "net10";

        /// <summary>
        /// The build an AutoCAD release must load: R24.1–R24.3 (2022–2024) .NET Framework 4.8, R25.x
        /// (2025 / 2026) .NET 8, R26.0 (2027) .NET 10. null = a release the plugin was never checked on.
        /// </summary>
        public static string ExpectedBuild(int major, int minor)
        {
            if (major == 24 && minor >= 1 && minor <= 3) return Net48;
            if (major == 25 && minor >= 0 && minor <= 1) return Net8;
            if (major == 26 && minor == 0) return Net10;
            return null;
        }

        /// <summary>"2022–2024", "2025–2026", "2027" for a build name; "?" otherwise.</summary>
        public static string YearsOf(string build)
        {
            switch (build)
            {
                case Net48: return "2022–2024";
                case Net8: return "2025–2026";
                case Net10: return "2027";
                default: return "?";
            }
        }

        /// <summary>Sub-folder of the bundle's Contents the build is installed in ("" = Contents itself).</summary>
        public static string FolderOf(string build)
        {
            switch (build)
            {
                case Net8: return "2025";
                case Net10: return "2027";
                default: return "";
            }
        }

        /// <summary>
        /// Build name from the dll's TargetFrameworkAttribute (".NETFramework,Version=v4.8",
        /// ".NETCoreApp,Version=v8.0"); null when it is something else.
        /// </summary>
        public static string BuildOf(string targetFramework)
        {
            if (string.IsNullOrWhiteSpace(targetFramework)) return null;
            var parts = targetFramework.Split(',');
            string family = parts[0].Trim();
            string version = parts.Skip(1).Select(p => p.Trim())
                .Where(p => p.StartsWith("Version=", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Substring("Version=".Length).TrimStart('v', 'V'))
                .FirstOrDefault() ?? "";
            int major = 0;
            int.TryParse(version.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
            if (family.Equals(".NETFramework", StringComparison.OrdinalIgnoreCase)) return major == 4 ? Net48 : null;
            if (family.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase))
                return major == 8 ? Net8 : major == 10 ? Net10 : null;
            return null;
        }

        /// <summary>
        /// Is the loaded dll the one this AutoCAD should load? <paramref name="dllPath"/> is only used to
        /// say where it was loaded from (a NETLOAD from a build folder is fine).
        /// </summary>
        public static Verdict CheckBuild(int acadMajor, int acadMinor, string targetFramework, int runtimeMajor, string dllPath)
        {
            string acad = $"AutoCAD R{acadMajor}.{acadMinor}";
            string loaded = BuildOf(targetFramework);
            string expected = ExpectedBuild(acadMajor, acadMinor);
            string from = DescribePath(dllPath, loaded);
            if (loaded == null)
                return new Verdict(false, $"{acad}: không nhận ra bản build của DLL ({(string.IsNullOrWhiteSpace(targetFramework) ? "không có TargetFramework" : targetFramework)}){from}");
            if (expected == null)
                return new Verdict(false, $"{acad} chưa nằm trong danh sách đã kiểm (2022–2027); đang nạp bản {loaded} cho AutoCAD {YearsOf(loaded)}{from}");
            if (loaded != expected)
                return new Verdict(false, $"{acad} cần bản {expected} (AutoCAD {YearsOf(expected)}) nhưng đang nạp bản {loaded} (AutoCAD {YearsOf(loaded)}){from} — cài lại bằng ClaudeCAD-Setup.exe");
            // A .NET 8 build keeps working after AutoCAD 2025 / 2026 move to .NET 10; anything older than the build cannot load it at all.
            int need = loaded == Net48 ? 4 : loaded == Net8 ? 8 : 10;
            if (runtimeMajor < need)
                return new Verdict(false, $"{acad}: bản {loaded} cần .NET {need} trở lên, AutoCAD đang chạy .NET {runtimeMajor}{from}");
            string runtime = loaded == Net48 ? ".NET Framework 4.8" : ".NET " + runtimeMajor;
            return new Verdict(true, $"{acad} · bản {loaded} (AutoCAD {YearsOf(loaded)}) · chạy trên {runtime}{from}");
        }

        private static string DescribePath(string dllPath, string build)
        {
            if (string.IsNullOrWhiteSpace(dllPath)) return "";
            string note = "";
            try
            {
                string norm = dllPath.Replace('/', '\\');
                bool bundle = norm.IndexOf(".bundle\\", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!bundle) note = " (không nằm trong gói cài: nạp tay bằng NETLOAD?)";
                else if (build != null)
                {
                    var parts = norm.Split('\\');
                    string folder = parts.Length >= 2 ? parts[parts.Length - 2] : "";
                    string want = FolderOf(build);
                    bool right = want.Length == 0 ? folder.Equals("Contents", StringComparison.OrdinalIgnoreCase) : folder == want;
                    if (!right) note = $" (thư mục «{folder}» không phải chỗ của bản {build}: cần Contents{(want.Length == 0 ? "" : "\\" + want)})";
                }
            }
            catch { }
            return " · " + dllPath + note;
        }

        // ================================================================= theme

        /// <summary>WCAG contrast ratio (1 … 21) of two sRGB colors.</summary>
        public static double Contrast(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            double a = Luminance(r1, g1, b1), b = Luminance(r2, g2, b2);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        private static double Luminance(byte r, byte g, byte b)
        {
            double C(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
        }

        /// <summary>Below this the chat text would be hard to read on the palette background.</summary>
        public const double MinTextContrast = 3.0;

        /// <summary>
        /// What AutoCAD's palette theme gave the chat palette. <paramref name="read"/> false = the theme API
        /// was not reachable (the palette then uses its built-in colors: works, but does not match AutoCAD).
        /// <paramref name="contrast"/> is text on background, 0 when one of the two colors was not read.
        /// </summary>
        public static Verdict CheckTheme(bool read, bool? themeDark, bool colorThemeDark, int colors, int totalColors,
            string font, double fontSize, double contrast, bool subscribed)
        {
            string variable = "COLORTHEME = " + (colorThemeDark ? "0 (tối)" : "1 (sáng)");
            if (!read)
                return new Verdict(false, $"không đọc được theme palette của AutoCAD ({variable}) → khung chat dùng màu dự phòng, có thể lệch với Properties");
            if (colors <= 0)
                return new Verdict(false, $"theme đọc được nhưng không có màu nào ({variable}) → khung chat dùng màu dự phòng");
            if (contrast > 0 && contrast < MinTextContrast)
                return new Verdict(false, $"chữ khó đọc: tương phản chữ / nền chỉ {contrast.ToString("0.0", CultureInfo.InvariantCulture)} (cần ≥ {MinTextContrast.ToString("0.0", CultureInfo.InvariantCulture)})");
            var parts = new List<string>
            {
                themeDark == null ? "theme không nói tối / sáng" : themeDark.Value ? "theme tối" : "theme sáng",
                variable,
                $"{colors}/{totalColors} màu",
                string.IsNullOrWhiteSpace(font) ? "font: không có (dùng font mặc định)" : $"font {font}" + (fontSize > 0 ? " " + fontSize.ToString("0.#", CultureInfo.InvariantCulture) : ""),
            };
            if (contrast > 0) parts.Add("tương phản chữ " + contrast.ToString("0.0", CultureInfo.InvariantCulture));
            parts.Add(subscribed ? "đổi theme: theo sự kiện của AutoCAD" : "đổi theme: theo biến COLORTHEME");
            if (themeDark != null && themeDark.Value != colorThemeDark) parts.Add("lưu ý: theme palette và COLORTHEME đang khác nhau");
            return new Verdict(true, string.Join(" · ", parts));
        }

        // ================================================================= compact mode

        /// <summary>
        /// Collapse then expand: the conversation must be hidden while compact, shown again after, and no
        /// message may be lost on the way.
        /// </summary>
        public static Verdict CheckCompact(int before, int whileCompact, int after, bool hiddenWhileCompact, bool shownAfter)
        {
            if (whileCompact != before || after != before)
                return new Verdict(false, $"mất tin nhắn khi thu gọn: trước {before}, lúc thu gọn {whileCompact}, mở lại {after}");
            if (!hiddenWhileCompact) return new Verdict(false, "bấm thu gọn nhưng đoạn chat vẫn hiện");
            if (!shownAfter) return new Verdict(false, "mở lại nhưng đoạn chat không hiện");
            return new Verdict(true, $"{before} tin nhắn còn nguyên");
        }

        /// <summary>
        /// A floating palette must come back to the size it had. <paramref name="tolerance"/> px covers
        /// DPI rounding.
        /// </summary>
        public static bool SameSize(int w1, int h1, int w2, int h2, int tolerance = 4) =>
            Math.Abs(w1 - w2) <= tolerance && Math.Abs(h1 - h2) <= tolerance;

        // ================================================================= queued message

        /// <summary>A message waits only while a turn runs; one left over with no turn would never be sent.</summary>
        public static Verdict CheckQueue(bool busy, bool queued, bool rowShows, bool rowHides)
        {
            if (!rowShows) return new Verdict(false, "hàng «Chờ gửi» không hiện khi có tin chờ");
            if (!rowHides) return new Verdict(false, "hàng «Chờ gửi» không ẩn khi bỏ tin chờ");
            if (queued && !busy) return new Verdict(false, "có tin đang chờ gửi nhưng không có lượt nào chạy (tin sẽ không tự gửi) — bấm «Bỏ» rồi gửi lại");
            return new Verdict(true, queued ? "đang giữ 1 tin chờ lượt hiện tại xong" : "hàng chờ trống, hiện / ẩn đúng");
        }

        // ================================================================= skills of the chat

        /// <summary>
        /// The library must be readable and the shipped skills intact; a broken skill the user added is
        /// reported but does not fail the test (it is simply not loaded). <paramref name="chosen"/> is what the
        /// chat asked for, <paramref name="used"/> what it really gets.
        /// </summary>
        public static Verdict CheckSkills(bool folderExists, string folder, IEnumerable<SkillInfo> library,
            IEnumerable<string> chosen, IEnumerable<string> used)
        {
            if (!folderExists) return new Verdict(false, "không thấy thư mục skill: " + (string.IsNullOrEmpty(folder) ? "(chưa xác định)" : folder));
            var lib = (library ?? Enumerable.Empty<SkillInfo>()).ToList();
            var brokenBuiltin = lib.Where(k => k.Builtin && k.Error != null).Select(k => k.Name + " (" + k.Error + ")").ToList();
            if (brokenBuiltin.Count > 0) return new Verdict(false, "skill mẫu đi kèm bị hỏng: " + string.Join("; ", brokenBuiltin) + " — cài lại plugin");
            var usedList = (used ?? Enumerable.Empty<string>()).ToList();
            var readable = new HashSet<string>(lib.Where(k => k.Error == null).Select(k => k.Name), StringComparer.Ordinal);
            var ghost = usedList.Where(n => !readable.Contains(n)).ToList();
            if (ghost.Count > 0) return new Verdict(false, "đoạn chat đang dùng skill không đọc được: " + string.Join(", ", ghost));
            var text = $"thư viện {readable.Count} skill · đoạn chat này dùng {usedList.Count}" + (usedList.Count > 0 ? " (" + string.Join(", ", usedList) + ")" : "");
            var missing = (chosen ?? Enumerable.Empty<string>()).Where(n => !usedList.Contains(n)).ToList();
            if (missing.Count > 0) text += " · đã chọn nhưng không còn trong thư viện: " + string.Join(", ", missing);
            var brokenOwn = lib.Where(k => !k.Builtin && k.Error != null).Select(k => k.Name).ToList();
            if (brokenOwn.Count > 0) text += " · lưu ý: skill lỗi, không được nạp: " + string.Join(", ", brokenOwn);
            return new Verdict(true, text);
        }

        // ================================================================= the user's drawing

        /// <summary>
        /// The drawing that was open when the test started must be exactly as it was: no new handle, same
        /// number of objects, same "modified" flag (DBMOD; -1 = could not be read, not compared).
        /// </summary>
        public static Verdict CheckUntouched(string name, string handseedBefore, string handseedAfter,
            long countBefore, long countAfter, int dbmodBefore, int dbmodAfter)
        {
            string file = string.IsNullOrEmpty(name) ? "bản vẽ" : Path.GetFileName(name.Replace('\\', '/'));
            if (!string.Equals(handseedBefore, handseedAfter, StringComparison.OrdinalIgnoreCase))
                return new Verdict(false, $"{file}: có đối tượng mới được tạo trong lúc kiểm tra (handle kế tiếp {handseedBefore} → {handseedAfter}) — đừng lưu, gõ U rồi báo lại");
            if (countBefore != countAfter)
                return new Verdict(false, $"{file}: số đối tượng đổi {countBefore} → {countAfter} — đừng lưu, gõ U rồi báo lại");
            if (dbmodBefore >= 0 && dbmodAfter >= 0 && dbmodBefore != dbmodAfter)
                return new Verdict(false, $"{file}: trạng thái «đã sửa» đổi (DBMOD {dbmodBefore} → {dbmodAfter}) dù không thêm / bớt đối tượng nào");
            return new Verdict(true, $"{file}: {countAfter} đối tượng, không thêm / bớt / sửa gì" + (dbmodBefore < 0 || dbmodAfter < 0 ? " (không đọc được DBMOD, chỉ so số đối tượng)" : ""));
        }
    }
}
