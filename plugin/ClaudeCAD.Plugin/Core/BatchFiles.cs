using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H5: the numbers of "làm nhiều file một lúc" (batch_files), one place each
    /// (docs/ban-giao/nang-cap-h5.md, "Con số chờ chủ dự án duyệt").
    /// </summary>
    public static class H5Numbers
    {
        /// <summary>Most drawings in one run (a folder with more: the first ones by name, and the result says how many were left).</summary>
        public const int MaxFiles = 200;
        /// <summary>Deepest sub-folder level read with recursive (the folder itself is level 0).</summary>
        public const int MaxDepth = 5;
        /// <summary>File names listed in the plan / the chat (the rest are counted).</summary>
        public const int MaxListed = 30;
        /// <summary>The backup folder made inside the chosen folder before any file is written.</summary>
        public const string BackupFolder = "_sao-luu-claudecad";
        /// <summary>Group names written per file in the summary of a check.</summary>
        public const int TopGroups = 3;
    }

    /// <summary>Nâng cấp đợt H5, AutoCAD-free part of batch_files: tasks, the list of files, the card token, backups, the summary.</summary>
    public static class BatchLogic
    {
        public const string Tool = "batch_files";
        public static readonly string[] Tasks = { "check", "blocks", "standards", "pdf" };
        public static readonly string[] Actions = { "plan", "run", "finish" };

        public static string TaskOf(string task)
        {
            var t = (task ?? "").Trim().ToLowerInvariant();
            if (!Tasks.Contains(t)) throw ToolException.Invalid($"{Tool}: task must be one of {string.Join(", ", Tasks)} (got '{task}').");
            return t;
        }

        /// <summary>The tool whose plan / work mode a task needs (the same gate as calling it on one drawing).</summary>
        public static string SubTool(string task)
        {
            switch (task)
            {
                case "check": return "check_drawing";
                case "blocks": return "block_schedule";
                case "standards": return "setup_standards";
                default: return "plot_pdf";
            }
        }

        /// <summary>The task writes the drawing files (backup first, never in Chỉ xem).</summary>
        public static bool Writes(string task) => task == "standards";

        /// <summary>DXF files are read for the read-only tasks; written and printed files are DWG.</summary>
        public static bool Takes(string task, string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".dwg" || (ext == ".dxf" && (task == "check" || task == "blocks"));
        }

        /// <summary>
        /// The drawings of a run, sorted by path: the listed files, else the folder (and its sub-folders to MaxDepth with
        /// recursive; the backup folder is never read). At most MaxFiles; <paramref name="more"/> = how many were left out.
        /// </summary>
        public static List<string> Collect(string task, string folder, IList<string> files, bool recursive,
            Func<string, IEnumerable<string>> filesOf, Func<string, IEnumerable<string>> dirsOf, out int more)
        {
            more = 0;
            var all = new List<string>();
            if (files != null && files.Count > 0)
            {
                foreach (var f in files.Select(x => (x ?? "").Trim().Trim('"')).Where(x => x.Length > 0))
                {
                    if (!Takes(task, f)) throw ToolException.Invalid($"{Tool}: '{Path.GetFileName(f)}' is not a {(task == "check" || task == "blocks" ? ".dwg / .dxf" : ".dwg")} file.");
                    if (!all.Contains(f, StringComparer.OrdinalIgnoreCase)) all.Add(f);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(folder)) throw ToolException.Invalid($"{Tool}: give the folder (or the list of files).");
                void Walk(string dir, int depth)
                {
                    foreach (var f in filesOf(dir).Where(x => Takes(task, x))) all.Add(f);
                    if (!recursive || depth >= H5Numbers.MaxDepth) return;
                    foreach (var d in dirsOf(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        var name = Path.GetFileName(d.TrimEnd('\\', '/'));
                        if (name.Equals(H5Numbers.BackupFolder, StringComparison.OrdinalIgnoreCase) || name.StartsWith(".", StringComparison.Ordinal)) continue;
                        Walk(d, depth + 1);
                    }
                }
                Walk(folder, 0);
            }
            var sorted = all.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            if (sorted.Count > H5Numbers.MaxFiles) { more = sorted.Count - H5Numbers.MaxFiles; sorted = sorted.Take(H5Numbers.MaxFiles).ToList(); }
            return sorted;
        }

        /// <summary>Why a file is left out before anything runs, or null. Writes also skip read-only and locked files.</summary>
        public static string SkipReason(string task, bool exists, long bytes, bool readOnly, bool lockFile, bool openHere, bool openHereModified)
        {
            if (!exists) return "the file does not exist";
            if (bytes <= 0) return "the file is empty";
            if (bytes > (long)OtherDrawingNumbers.MaxFileMb * 1024 * 1024) return $"bigger than {OtherDrawingNumbers.MaxFileMb} MB";
            if (Writes(task))
            {
                if (readOnly) return "the file is read-only";
                if (openHere) return "the drawing is open in this AutoCAD: save and close it, or set it up there with setup_standards";
                if (lockFile) return "the drawing is open by someone else (its .dwl lock file is there)";
            }
            if (task == "pdf" && openHereModified) return "the drawing is open here with unsaved changes: save it first (or print it there with plot_pdf)";
            return null;
        }

        /// <summary>The card token of a run: task, settings and files (FNV-1a 64), never a handle of the drawing.</summary>
        public static string Token(string task, string settings, IEnumerable<string> files)
        {
            var key = "batch:" + task + "|" + (settings ?? "") + "|" + string.Join("|", files.Select(f => f.Trim().ToUpperInvariant()).OrderBy(f => f, StringComparer.Ordinal));
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(key)) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>The settings that change what a run does (part of its token): sorted key=value of the task's parameters.</summary>
        public static string Settings(IDictionary<string, object> p, params string[] keys) =>
            string.Join(";", keys.OrderBy(k => k, StringComparer.Ordinal).Where(k => p != null && p.ContainsKey(k) && p[k] != null)
                .Select(k => k + "=" + (p[k] is IEnumerable<object> e && !(p[k] is string) ? string.Join(",", e) : Convert.ToString(p[k], CultureInfo.InvariantCulture))));

        public static string Stamp(DateTime t) => t.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        public static bool ValidStamp(string s) => s != null && s.Length == 15 && DateTime.TryParseExact(s, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        /// <summary>Where the backup of a file goes: &lt;root&gt;\_sao-luu-claudecad\&lt;stamp&gt;\&lt;path under root&gt; (or its name).</summary>
        public static string BackupPath(string root, string stamp, string file)
        {
            string rel = Path.GetFileName(file);
            if (!string.IsNullOrEmpty(root))
            {
                var r = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                var f = Path.GetFullPath(file);
                if (f.StartsWith(r, StringComparison.OrdinalIgnoreCase)) rel = f.Substring(r.Length);
            }
            var baseDir = string.IsNullOrEmpty(root) ? Path.GetDirectoryName(file) : root;
            return Path.Combine(baseDir, H5Numbers.BackupFolder, stamp, rel);
        }

        /// <summary>The PDF of a drawing: next to it, same name.</summary>
        public static string PdfOf(string file) => Path.ChangeExtension(file, ".pdf");

        /// <summary>The Excel summary of a run in the folder (never an existing file: the stamp is in its name).</summary>
        public static string SummaryPath(string folder, string task, string stamp) =>
            Path.Combine(folder, $"tong-hop-{task}-{stamp}.xlsx");

        /// <summary>A file name as the chat shows it: the path under the folder, else the full path.</summary>
        public static string Show(string root, string file)
        {
            if (string.IsNullOrEmpty(root)) return file;
            var r = root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return file.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? file.Substring(r.Length) : file;
        }

        /// <summary>The summary table of a run (header, one row per file, the totals row).</summary>
        public static List<List<object>> SummaryRows(string task, IList<Dictionary<string, object>> rows)
        {
            var head = new List<object> { "File", "Kết quả", "Ghi chú" };
            if (task == "check") head.AddRange(new object[] { "Nên sửa", "Cân nhắc", "Gợi ý" });
            if (task == "blocks") head.Add("Số block");
            var outl = new List<List<object>> { head };
            foreach (var r in rows ?? new List<Dictionary<string, object>>())
            {
                var line = new List<object> { G(r, "file"), StatusText(G(r, "status") as string), G(r, "note") ?? G(r, "reason") ?? "" };
                if (task == "check") { line.Add(N(r, "nen_sua")); line.Add(N(r, "can_nhac")); line.Add(N(r, "goi_y")); }
                if (task == "blocks") line.Add(N(r, "blocks"));
                outl.Add(line);
            }
            var t = Totals(rows);
            outl.Add(new List<object> { "Tổng", $"{t["ok"]} xong, {t["error"]} lỗi, {t["skipped"]} bỏ qua", "" });
            return outl;
        }

        /// <summary>blocks: one row per (file, block name, count), for the Excel sheet of the block counts.</summary>
        public static List<List<object>> BlockRows(IList<Dictionary<string, object>> rows)
        {
            var outl = new List<List<object>> { new List<object> { "File", "Block", "Số lượng" } };
            foreach (var r in rows ?? new List<Dictionary<string, object>>())
                if (r.TryGetValue("counts", out var c) && c is Dictionary<string, object> d)
                    foreach (var kv in d.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) outl.Add(new List<object> { G(r, "file"), kv.Key, N(d, kv.Key) });
            return outl;
        }

        public static Dictionary<string, int> Totals(IEnumerable<Dictionary<string, object>> rows)
        {
            var t = new Dictionary<string, int> { ["ok"] = 0, ["error"] = 0, ["skipped"] = 0 };
            foreach (var r in rows ?? Enumerable.Empty<Dictionary<string, object>>())
            {
                var s = G(r, "status") as string;
                if (s != null && t.ContainsKey(s)) t[s]++;
            }
            return t;
        }

        public static string StatusText(string s) => s == "ok" ? "Xong" : s == "skipped" ? "Bỏ qua" : s == "error" ? "Lỗi" : s ?? "";

        private static object G(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        private static double N(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;
    }
}
