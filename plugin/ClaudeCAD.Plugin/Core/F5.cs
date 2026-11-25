using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt F5: the numbers and words the user meets, one place each (docs/ban-giao/nang-cap-f5.md, "Con số chờ chủ
    /// dự án duyệt").
    /// </summary>
    public static class F5Numbers
    {
        /// <summary>Multileader of the standard: sizes on paper (mm), scaled by the drawing scale like the dimension style.</summary>
        public const double MLeaderTextHeight = F2Numbers.TextNote;   // 2.5: the note height of the standard
        public const double MLeaderArrowSize = 2.5;
        public const double MLeaderLandingGap = 1;
        public const double MLeaderDoglegLength = 2.5;

        /// <summary>The ⚙ row of the deferred tools (F4) and its one-line explanation.</summary>
        public const string DeferToolsLabel = "Nạp gọn lệnh";
        public const string DeferToolsTip =
            "Bật: mở chat nhanh và nhẹ hơn, Claude tự tìm lệnh ít dùng khi cần. Tắt: nạp đủ mô tả mọi lệnh như trước (tốn token hơn). " +
            "Áp dụng từ tin nhắn tới, đoạn chat giữ nguyên.";
    }

    /// <summary>
    /// Nâng cấp đợt F5: overwriting a file (PDF, STL, PNG, Excel), overwriting / deleting a saved view and a material for a whole
    /// layer need the Đồng ý of a confirmation card, like the F2 overwrites. The plugin refuses with the handles to put on the
    /// card (a view / layer: its own handle; a file: a token made from its full path), the assistant passes the approved ones as
    /// user_confirmed when Claude calls again with confirm_handles. A macro replay keeps the rules of before (no card).
    /// </summary>
    public static class F5Confirm
    {
        /// <summary>The parameter Claude repeats after Đồng ý (the assistant turns the approved ones into user_confirmed).</summary>
        public const string Param = "confirm_handles";

        /// <summary>A stable 16-hex-digit handle-like token of a file (FNV-1a 64 of the full path, case-free).</summary>
        public static string FileToken(string fullPath) =>
            Fnv("file:" + (fullPath ?? "").Trim().Replace('/', '\\').ToUpperInvariant());

        /// <summary>
        /// Test & sửa F5: the token of deleting a saved view. Overwriting it is approved with its own handle; deleting needs
        /// this other token, so the Đồng ý of "overwrite view X" never lets the same turn delete X (and the reverse).
        /// </summary>
        public static string DeleteViewToken(string viewHandle) => Fnv("delete-view:" + (viewHandle ?? "").Trim().ToUpperInvariant());

        private static string Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(s))
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>The tokens of the call that are not on the approved card.</summary>
        public static List<string> Missing(IEnumerable<string> tokens, object userConfirmed)
        {
            var ok = SolidLogic.ConfirmedHandles(userConfirmed);
            return tokens.Select(t => t.ToUpperInvariant()).Distinct().Where(t => !ok.Contains(t)).ToList();
        }

        public static string CardMessage(string tool, string what, IEnumerable<string> handles) =>
            $"Confirmation required: {tool} would {what}, which is not on a confirmation card the user approved. Nothing was changed. " +
            $"Call request_confirmation with handles [{string.Join(", ", handles.Select(h => "\"" + h + "\""))}] and one Vietnamese sentence saying what will be overwritten or deleted, " +
            $"and end your turn. After the user presses Đồng ý, call {tool} again with the same parameters and {Param} = those handles. A typed \"ok\" does not count.";
    }

    /// <summary>Nâng cấp đợt F5: the multileader style of setup_standards (with the text and dimension styles).</summary>
    public static class MLeaderStyleLogic
    {
        /// <summary>KT-ML-100 (drawing in metres: KT-ML-100-M), next to the dimension style KT-100.</summary>
        public static string Name(string prefix, int scale, double mmPerUnit) =>
            StandardsLogic.DimStyleName(prefix + "-ML", scale, mmPerUnit);

        public sealed class Wanted
        {
            public string Name, TextStyle;
            /// <summary>Overall scale (like DIMSCALE): paper mm → drawing units.</summary>
            public double Scale, TextHeight, ArrowSize, LandingGap, DoglegLength;
        }

        public static Wanted For(string prefix, int scale, double mmPerUnit) => new Wanted
        {
            Name = Name(prefix, scale, mmPerUnit),
            TextStyle = StandardsLogic.TextStyleName(prefix),
            Scale = scale / mmPerUnit,
            TextHeight = F5Numbers.MLeaderTextHeight,
            ArrowSize = F5Numbers.MLeaderArrowSize,
            LandingGap = F5Numbers.MLeaderLandingGap,
            DoglegLength = F5Numbers.MLeaderDoglegLength,
        };

        /// <summary>A multileader style made by setup_standards ("&lt;prefix&gt;-ML-&lt;scale&gt;", optional unit suffix -M / -CM / -IN / -DM / -FT).</summary>
        public static bool IsStandardName(string name) =>
            !string.IsNullOrEmpty(name) && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9]+-ML-[0-9]+(-(M|CM|IN|DM|FT))?$");

        /// <summary>Text height in drawing units of a (non-annotative) multileader style: paper height × its overall scale (0 = 1).</summary>
        public static double DrawingHeight(double styleHeight, double styleScale) => styleHeight * (styleScale > 0 ? styleScale : 1);

        /// <summary>Differences between an existing style and the wanted one (empty = the same).</summary>
        public static List<string> Differences(Wanted w, string textStyle, double scale, double textHeight, double arrowSize, double landingGap, double dogleg)
        {
            var d = new List<string>();
            bool Near(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(1, Math.Abs(b));
            if (!string.Equals(textStyle, w.TextStyle, StringComparison.OrdinalIgnoreCase)) d.Add($"text style {textStyle} (standard {w.TextStyle})");
            if (!Near(scale, w.Scale)) d.Add($"scale {scale:0.###} (standard {w.Scale:0.###})");
            if (!Near(textHeight, w.TextHeight)) d.Add($"text height {textHeight:0.###} (standard {w.TextHeight:0.###})");
            if (!Near(arrowSize, w.ArrowSize)) d.Add($"arrow size {arrowSize:0.###} (standard {w.ArrowSize:0.###})");
            if (!Near(landingGap, w.LandingGap)) d.Add($"landing gap {landingGap:0.###} (standard {w.LandingGap:0.###})");
            if (!Near(dogleg, w.DoglegLength)) d.Add($"dogleg {dogleg:0.###} (standard {w.DoglegLength:0.###})");
            return d;
        }
    }
}
