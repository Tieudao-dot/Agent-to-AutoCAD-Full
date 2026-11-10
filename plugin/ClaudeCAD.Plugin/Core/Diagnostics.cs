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
            new Regex(@"(?i)(ANTHROPIC_AUTH_TOKEN\s*[=:]\s*)\S+", RegexOptions.Compiled),                 // gateway users
            new Regex(@"(?i)(x-api-key\s*[:=]\s*)\S{8,}", RegexOptions.Compiled),                         // other key formats
            new Regex(@"(?i)(""(?:api[_-]?key|id_?token|client_secret|password|secret)""\s*:\s*"")[^""]{4,}", RegexOptions.Compiled),
            new Regex(@"(?i)(https?://[^\s:/@]+:)[^\s@/]+(?=@)", RegexOptions.Compiled),                    // user:password@host
        };

        private static readonly Regex JsonType = new Regex(@"""type""\s*:\s*""([A-Za-z_]+)""", RegexOptions.Compiled);
        private static readonly Regex JsonTool = new Regex(@"""tool""\s*:\s*""([A-Za-z_]+)""", RegexOptions.Compiled);
        private static readonly Regex JsonId = new Regex(@"""id""\s*:\s*""([A-Za-z0-9_\-]{1,40})""", RegexOptions.Compiled);
        private static readonly Regex JsonOk = new Regex(@"""ok""\s*:\s*(true|false)", RegexOptions.Compiled);
        private static readonly Regex JsonCode = new Regex(@"""code""\s*:\s*""([A-Za-z_]+)""", RegexOptions.Compiled);
        // plugin log: "12:00:01.234 IPC   -> {...}" ; sidecar log: "... -> {...}" / "... <- {...}"
        // body = a JSON object ({"...): a line already summarized ("{tool_call add_text id=req-6} [..]") is left alone
        private static readonly Regex MessageLine = new Regex(@"^(?<head>.*?(?:IPC\s+\S+|->|<-)) (?<body>\{"".*)$", RegexOptions.Compiled);

        /// <summary>
        /// One IPC / sidecar JSON message → what is needed to debug it (type, tool, id, ok / error code, size), without
        /// its content: user prompts, Claude's replies, project notes, texts and coordinates of the drawing stay out of
        /// logs and bug reports.
        /// </summary>
        public static string SummarizeMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return json ?? "";
            var parts = new List<string>();
            var t = JsonType.Match(json); if (t.Success) parts.Add(t.Groups[1].Value);
            var tool = JsonTool.Match(json); if (tool.Success) parts.Add(tool.Groups[1].Value);
            var id = JsonId.Match(json); if (id.Success) parts.Add("id=" + id.Groups[1].Value);
            var ok = JsonOk.Match(json); if (ok.Success) parts.Add("ok=" + ok.Groups[1].Value);
            var code = JsonCode.Match(json); if (code.Success) parts.Add("code=" + code.Groups[1].Value);
            return "{" + string.Join(" ", parts) + "} [" + json.Length + " ký tự, nội dung đã ẩn]";
        }

        /// <summary>Bug report: message lines of the plugin and sidecar logs (older versions logged them in full) are summarized.</summary>
        public static string StripChatContent(string log)
        {
            if (string.IsNullOrEmpty(log)) return log ?? "";
            var lines = log.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var m = MessageLine.Match(lines[i].TrimEnd('\r'));
                if (m.Success) lines[i] = m.Groups["head"].Value + " " + SummarizeMessage(m.Groups["body"].Value);
            }
            return string.Join("\n", lines);
        }

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
