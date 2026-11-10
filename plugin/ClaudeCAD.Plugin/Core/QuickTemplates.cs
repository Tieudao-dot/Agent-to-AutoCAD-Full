using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>A "/name" quick prompt (feature 8).</summary>
    public sealed class QuickTemplate
    {
        public string Name;         // typed after "/", e.g. "luoi-truc"
        public string Title;        // shown in the popup
        public string Text;         // prompt with {{label=default}} placeholders
    }

    /// <summary>
    /// Loads quick prompts from templates\templates.json next to the bundle (shipped)
    /// and %AppData%\ClaudeCAD\templates.json (the user's own; same name overrides),
    /// so users add templates without rebuilding.
    /// </summary>
    public static class QuickTemplates
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{\s*([^=}]+?)\s*(?:=\s*([^}]*?))?\s*\}\}");

        public static List<QuickTemplate> Parse(string json)
        {
            var list = new List<QuickTemplate>();
            var root = JsonCodec.Parse(json);
            var items = root as List<object>;
            if (items == null && root is Dictionary<string, object> d && d.TryGetValue("templates", out var t)) items = t as List<object>;
            if (items == null) throw new FormatException("templates.json must be an array (or {\"templates\": [...]})");
            foreach (var o in items.OfType<Dictionary<string, object>>())
            {
                string S(string k) => o.TryGetValue(k, out var v) ? v as string : null;
                var name = (S("name") ?? "").Trim().TrimStart('/');
                var text = S("text");
                if (name.Length == 0 || string.IsNullOrEmpty(text)) continue;
                list.Add(new QuickTemplate { Name = name, Title = S("title") ?? name, Text = text });
            }
            return list;
        }

        /// <summary>Shipped templates first; user templates override by name and add new ones.</summary>
        public static List<QuickTemplate> Merge(IEnumerable<QuickTemplate> shipped, IEnumerable<QuickTemplate> user)
        {
            var map = new Dictionary<string, QuickTemplate>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var t in shipped.Concat(user))
            {
                if (!map.ContainsKey(t.Name)) order.Add(t.Name);
                map[t.Name] = t;
            }
            return order.Select(n => map[n]).ToList();
        }

        public static List<QuickTemplate> LoadAll(string shippedFile, string userFile)
        {
            var shipped = new List<QuickTemplate>();
            var user = new List<QuickTemplate>();
            try { if (shippedFile != null && File.Exists(shippedFile)) shipped = Parse(File.ReadAllText(shippedFile, Encoding.UTF8)); }
            catch (Exception ex) { Logger.Warn("templates.json (bundle) unreadable: " + ex.Message); }
            try { if (userFile != null && File.Exists(userFile)) user = Parse(File.ReadAllText(userFile, Encoding.UTF8)); }
            catch (Exception ex) { Logger.Warn("templates.json (user) unreadable: " + ex.Message); }
            return Merge(shipped, user);
        }

        /// <summary>
        /// Adds (or replaces, same name) a template in the user's templates.json ("Lưu thành mẫu lệnh").
        /// Returns the template actually stored (name slugified, unique).
        /// </summary>
        public static QuickTemplate SaveUser(string userFile, string title, string text, bool replace = false, IEnumerable<string> reserved = null)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Mẫu lệnh trống.");
            var list = new List<QuickTemplate>();
            if (File.Exists(userFile))
            {
                try { list = Parse(File.ReadAllText(userFile, Encoding.UTF8)); }
                catch (Exception ex)
                {
                    // keep the broken file for the user instead of failing every save
                    Logger.Warn("templates.json (user) unreadable, kept as .bak: " + ex.Message);
                    File.Copy(userFile, userFile + ".bak", true);
                    list = new List<QuickTemplate>();
                }
            }
            var taken = new HashSet<string>(reserved ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var baseName = SkillStore.Slugify(string.IsNullOrWhiteSpace(title) ? text : title);
            if (baseName.Length == 0) baseName = "mau";
            if (baseName.Length > 30) baseName = baseName.Substring(0, 30).TrimEnd('-');
            var name = baseName;
            if (!replace)
                for (int i = 2; taken.Contains(name) || list.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); i++) name = baseName + "-" + i;
            list.RemoveAll(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var tpl = new QuickTemplate { Name = name, Title = string.IsNullOrWhiteSpace(title) ? Shorten(text, 60) : title.Trim(), Text = text.Trim() };
            list.Add(tpl);
            Directory.CreateDirectory(Path.GetDirectoryName(userFile));
            var json = JsonCodec.Serialize(new Dictionary<string, object>
            {
                ["templates"] = list.Select(t => (object)new Dictionary<string, object> { ["name"] = t.Name, ["title"] = t.Title, ["text"] = t.Text }).ToList(),
            });
            WriteSafely(userFile, json);
            return tpl;
        }

        public static bool DeleteUser(string userFile, string name)
        {
            if (!File.Exists(userFile)) return false;
            var list = Parse(File.ReadAllText(userFile, Encoding.UTF8));
            if (list.RemoveAll(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) == 0) return false;
            var json = JsonCodec.Serialize(new Dictionary<string, object>
            {
                ["templates"] = list.Select(t => (object)new Dictionary<string, object> { ["name"] = t.Name, ["title"] = t.Title, ["text"] = t.Text }).ToList(),
            });
            WriteSafely(userFile, json);
            return true;
        }

        /// <summary>Temp file then replace: a crash or power cut while saving never leaves a half-written templates.json.</summary>
        private static void WriteSafely(string file, string text)
        {
            var tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(tmp, file, null, true);
            else File.Move(tmp, file);
        }

        private static string Shorten(string s, int n)
        {
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }

        public static string UserFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeCAD", "templates.json");

        /// <summary>Templates whose name or title contains the typed filter (after "/").</summary>
        public static List<QuickTemplate> Filter(IEnumerable<QuickTemplate> all, string typed)
        {
            var f = (typed ?? "").TrimStart('/').Trim();
            return all.Where(t => f.Length == 0
                    || t.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                    || t.Title.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(t => t.Name.StartsWith(f, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }

        /// <summary>Replaces {{label=default}} with the default value; returns the text and the range of the first value.</summary>
        public static string Expand(string text, out int firstStart, out int firstLength)
        {
            firstStart = -1;
            firstLength = 0;
            var sb = new StringBuilder();
            int last = 0;
            foreach (Match m in Placeholder.Matches(text))
            {
                sb.Append(text, last, m.Index - last);
                var value = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[1].Value;
                if (firstStart < 0) { firstStart = sb.Length; firstLength = value.Length; }
                sb.Append(value);
                last = m.Index + m.Length;
            }
            sb.Append(text, last, text.Length - last);
            return sb.ToString();
        }
    }

    /// <summary>Clipboard helpers (feature 7).</summary>
    public static class ClipboardText
    {
        /// <summary>
        /// Tab-separated text copied from Excel (≥ 2 rows, ≥ 2 columns) → Markdown table.
        /// Returns null when the text is not a table.
        /// </summary>
        public static string TsvToMarkdownTable(string text, out int rows, out int cols)
        {
            rows = cols = 0;
            if (string.IsNullOrEmpty(text) || text.IndexOf('\t') < 0) return null;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
            if (lines.Length < 2) return null;
            var cells = lines.Select(l => l.Split('\t').Select(c => c.Trim().Replace("|", "\\|")).ToList()).ToList();
            cols = cells.Max(r => r.Count);
            if (cols < 2) return null;
            int tabRows = cells.Count(r => r.Count >= 2);
            if (tabRows < lines.Length * 0.8) return null;
            foreach (var r in cells) while (r.Count < cols) r.Add("");
            rows = cells.Count;
            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", cells[0])).AppendLine(" |");
            sb.Append("|").Append(string.Join("|", Enumerable.Repeat(" --- ", cols))).AppendLine("|");
            for (int i = 1; i < cells.Count; i++) sb.Append("| ").Append(string.Join(" | ", cells[i])).AppendLine(" |");
            return sb.ToString().TrimEnd();
        }
    }
}
