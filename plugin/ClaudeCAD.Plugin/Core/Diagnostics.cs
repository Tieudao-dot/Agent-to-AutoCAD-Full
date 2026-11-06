using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>Rules for the "Gửi báo cáo lỗi" zip, independent of AutoCAD (unit-tested).</summary>
    public static class Diagnostics
    {
        public const int LogDays = 3;
        public const int MaxLogBytes = 8 * 1024 * 1024;

        private static readonly Regex[] Secrets =
        {
            new Regex(@"sk-ant-[A-Za-z0-9_\-]{8,}", RegexOptions.Compiled),                      // Anthropic keys / tokens
            new Regex(@"(?i)(bearer\s+)[A-Za-z0-9._\-]{16,}", RegexOptions.Compiled),            // Authorization headers
            new Regex(@"(?i)(""?(?:access|refresh)_?token""?\s*[:=]\s*""?)[^"",\s]{8,}", RegexOptions.Compiled),
            new Regex(@"(?i)(ANTHROPIC_API_KEY\s*[=:]\s*)\S+", RegexOptions.Compiled),
            new Regex(@"(?i)(CLAUDE_CODE_OAUTH_TOKEN\s*[=:]\s*)\S+", RegexOptions.Compiled),
        };

        /// <summary>Removes API keys and tokens from a text before it leaves the computer.</summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            foreach (var rx in Secrets)
                text = rx.Replace(text, m => m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1].Value + "***" : "sk-ant-***");
            return text;
        }

        /// <summary>Log files of the last <paramref name="days"/> days (by name date or write time), newest first.</summary>
        public static List<string> RecentFiles(IEnumerable<(string path, DateTime written)> files, DateTime now, int days, int max)
        {
            return files.Where(f => now - f.written <= TimeSpan.FromDays(days))
                        .OrderByDescending(f => f.written)
                        .Take(max)
                        .Select(f => f.path)
                        .ToList();
        }

        /// <summary>Inno Setup writes "Setup Log yyyy-mm-dd #nnn.txt" to %TEMP%; only ClaudeCAD's are kept.</summary>
        public static bool IsClaudeCadSetupLog(string fileName, string firstLines) =>
            fileName != null && fileName.StartsWith("Setup Log", StringComparison.OrdinalIgnoreCase)
            && (firstLines ?? "").IndexOf("ClaudeCAD", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Reads at most the last <paramref name="maxBytes"/> of a log (big logs keep their end, where the error is).</summary>
        public static string ReadTail(string path, int maxBytes = MaxLogBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, fs.Length - maxBytes);
                fs.Seek(start, SeekOrigin.Begin);
                using (var r = new StreamReader(fs))
                {
                    var text = r.ReadToEnd();
                    return start > 0 ? "[… phần đầu log đã bỏ bớt …]\n" + text : text;
                }
            }
        }
    }
}
