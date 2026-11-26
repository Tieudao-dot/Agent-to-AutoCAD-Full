using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H9: the language of the palette ("vi" default, "en"). Every Vietnamese text a person sees goes
    /// through <see cref="Tr(string)"/>: the Vietnamese text is the key, <c>LangEn.cs</c> holds its English. A text
    /// with no English yet stays Vietnamese (a test lists them). Texts drawn into the drawing (table heads, title
    /// blocks, sheet names, stake names, layer descriptions…) never go through it: they stay Vietnamese in both
    /// languages (chủ dự án chốt 26/11/2026).
    /// </summary>
    public static partial class L
    {
        public const string Vi = "vi";
        public const string En = "en";
        public static readonly string[] Languages = { Vi, En };

        private static string _lang = Vi;

        /// <summary>Raised after the language changed (the palette rebuilds its texts).</summary>
        public static event Action Changed;

        public static string Lang
        {
            get => _lang;
            set
            {
                var v = Normalize(value);
                if (v == _lang) return;
                _lang = v;
                Changed?.Invoke();
            }
        }

        public static bool IsEnglish => _lang == En;

        public static string Normalize(string lang) =>
            string.Equals(lang?.Trim(), En, StringComparison.OrdinalIgnoreCase) ? En : Vi;

        /// <summary>The label of a language in the ⚙ list (always in its own language).</summary>
        public static string Label(string lang) => Normalize(lang) == En ? "English" : "Tiếng Việt";

        /// <summary>The text in the current language (the Vietnamese itself when no English is known).</summary>
        public static string Tr(string vi)
        {
            if (vi == null || _lang == Vi) return vi;
            return EnTable.TryGetValue(vi, out var en) ? en : vi;
        }

        /// <summary>A text with blanks ({0}, {1:N0}…) filled in after the translation.</summary>
        public static string Tr(string vi, params object[] args)
        {
            var f = Tr(vi);
            if (args == null || args.Length == 0) return f;
            try
            {
                return string.Format(CultureInfo.CurrentCulture, f, args);
            }
            catch (FormatException)
            {
                // a broken translation never breaks the palette: the Vietnamese template is known good
                return string.Format(CultureInfo.CurrentCulture, vi, args);
            }
        }

        private static Dictionary<string, string> _reverse;

        /// <summary>
        /// A whole text shown in one language, in the other one (null = not a known text: leave it). Used when the
        /// language changes while the palette is open.
        /// </summary>
        public static string Retranslate(string text, string from, string to)
        {
            if (string.IsNullOrEmpty(text)) return null;
            from = Normalize(from);
            to = Normalize(to);
            if (from == to) return null;
            if (from == Vi) return EnTable.TryGetValue(text, out var en) ? en : null;
            if (_reverse == null)
            {
                var r = new Dictionary<string, string>();
                foreach (var kv in EnTable)
                    if (!r.ContainsKey(kv.Value)) r[kv.Value] = kv.Key;
                _reverse = r;
            }
            return _reverse.TryGetValue(text, out var vi) ? vi : null;
        }

        /// <summary>True when the text has an English entry (tests).</summary>
        public static bool HasEnglish(string vi) => vi != null && EnTable.ContainsKey(vi);

        /// <summary>Every key of the English table (tests).</summary>
        public static IEnumerable<string> Keys => EnTable.Keys;

        /// <summary>The English of a key, null when there is none (tests).</summary>
        public static string English(string vi) => vi != null && EnTable.TryGetValue(vi, out var en) ? en : null;
    }
}
