using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>One skill in the plugin's skill folder, merged with its registry entry.</summary>
    public sealed class SkillInfo
    {
        public string Name;
        public string Title;            // first "# heading" of SKILL.md, else the name
        public string Description;
        public bool Enabled = true;
        public bool Builtin;            // shipped sample skill
        public DateTime? Added;
        public string Source;           // file the user imported it from
        public string Folder;
        public string File;
        public string Error;            // invalid SKILL.md (shown in the list, not loaded)
    }

    public enum SkillImportStatus { Added, Updated, AlreadyAdded, Conflict, Invalid }

    public sealed class SkillImportResult
    {
        public SkillImportStatus Status;
        public string Name;
        public string Message;
    }

    /// <summary>
    /// The skills added to the plugin through "+ Add skill": each skill is a folder
    /// &lt;root&gt;\&lt;name&gt;\SKILL.md, and &lt;root&gt;\skills.json records which skills were
    /// added, when, from which file, and whether they are enabled. The sidecar reads the
    /// same registry and loads only enabled skills.
    /// </summary>
    public sealed class SkillStore
    {
        public const string RegistryFileName = "skills.json";
        public const int MaxDescription = 1024;
        public static readonly string[] BuiltinNames = { "ve-kien-truc", "ve-mep", "ve-ha-tang" };
        private static readonly Regex NameRx = new Regex("^[a-z0-9][a-z0-9-]{0,63}$");
        private static readonly string[] CopyExtensions = { ".md", ".txt", ".csv", ".json" };

        public string Root { get; }
        public string RegistryPath => Path.Combine(Root, RegistryFileName);

        public SkillStore(string root)
        {
            Root = root;
        }

        // ------------------------------------------------------------------ parsing

        /// <summary>
        /// Reads the YAML frontmatter (name, description). Returns the error to show the
        /// user when the file cannot be a skill.
        /// </summary>
        public static (string name, string description, string error) ParseFrontmatter(string md)
        {
            if (string.IsNullOrWhiteSpace(md)) return (null, null, "File trống.");
            var text = md.TrimStart('﻿').Replace("\r\n", "\n");
            if (!text.StartsWith("---\n")) return (null, null, "File chưa có phần đầu (frontmatter) với name và description.");
            int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end < 0) return (null, null, "Phần đầu (frontmatter) chưa được đóng bằng dòng ---.");
            string name = null, desc = null;
            foreach (var raw in text.Substring(4, end - 4).Split('\n'))
            {
                var line = raw.TrimEnd();
                int colon = line.IndexOf(':');
                if (colon <= 0 || char.IsWhiteSpace(line[0])) continue;
                var key = line.Substring(0, colon).Trim().ToLowerInvariant();
                var value = line.Substring(colon + 1).Trim().Trim('"', '\'');
                if (key == "name") name = value;
                else if (key == "description") desc = value;
            }
            if (string.IsNullOrWhiteSpace(desc))
                return (name, null, "Thiếu dòng description (mô tả khi nào dùng skill) — Claude dựa vào dòng này để biết lúc nào nạp skill.");
            if (desc.Length < 20) return (name, desc, "Dòng description quá ngắn: hãy viết rõ khi nào dùng skill và các từ khóa (ít nhất 20 ký tự).");
            if (desc.Length > MaxDescription) return (name, desc, $"Dòng description dài {desc.Length} ký tự, tối đa {MaxDescription}.");
            return (name, desc, null);
        }

        /// <summary>"Vẽ Kết Cấu (v2).md" → "ve-ket-cau-v2".</summary>
        public static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var normalized = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c) && c < 128) sb.Append(char.ToLowerInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            return slug.Length > 64 ? slug.Substring(0, 64).TrimEnd('-') : slug;
        }

        public static bool IsValidName(string n) => !string.IsNullOrEmpty(n) && NameRx.IsMatch(n);

        private static string TitleOf(string md, string fallback)
        {
            foreach (var line in md.Replace("\r\n", "\n").Split('\n'))
                if (line.StartsWith("# ")) return line.Substring(2).Trim();
            return fallback;
        }

        // ------------------------------------------------------------------ registry

        private sealed class Entry
        {
            public string Name;
            public bool Enabled = true;
            public DateTime? Added;
            public string Source;
        }

        private Dictionary<string, Entry> LoadRegistry()
        {
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            try
            {
                if (!System.IO.File.Exists(RegistryPath)) return map;
                var d = JsonCodec.ParseObject(System.IO.File.ReadAllText(RegistryPath, Encoding.UTF8));
                if (!(d.TryGetValue("skills", out var sv) && sv is List<object> list)) return map;
                foreach (var o in list.OfType<Dictionary<string, object>>())
                {
                    var e = new Entry { Name = o.TryGetValue("name", out var n) ? n as string : null };
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    if (o.TryGetValue("enabled", out var en) && en is bool b) e.Enabled = b;
                    if (o.TryGetValue("added", out var ad) && ad is string a && DateTime.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) e.Added = t;
                    if (o.TryGetValue("source", out var src)) e.Source = src as string;
                    map[e.Name] = e;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("skills.json unreadable, rebuilding: " + ex.Message);
            }
            return map;
        }

        private void SaveRegistry(IEnumerable<Entry> entries)
        {
            Directory.CreateDirectory(Root);
            var d = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["note"] = "Danh sách skill của ClaudeCAD (quản lý bằng nút Skill / + Add skill trên khung chat).",
                ["skills"] = entries.OrderBy(e => e.Name, StringComparer.Ordinal).Select(e => (object)new Dictionary<string, object>
                {
                    ["name"] = e.Name,
                    ["enabled"] = e.Enabled,
                    ["added"] = e.Added?.ToString("o", CultureInfo.InvariantCulture),
                    ["source"] = e.Source,
                }).ToList(),
            };
            var tmp = RegistryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            System.IO.File.WriteAllText(tmp, JsonCodec.Serialize(d), new UTF8Encoding(false));
            if (System.IO.File.Exists(RegistryPath)) System.IO.File.Replace(tmp, RegistryPath, null, true);
            else System.IO.File.Move(tmp, RegistryPath);
        }

        // ------------------------------------------------------------------ queries

        /// <summary>All skill folders (with SKILL.md) merged with the registry; new folders are registered as enabled.</summary>
        public List<SkillInfo> Scan()
        {
            var result = new List<SkillInfo>();
            if (string.IsNullOrEmpty(Root) || !Directory.Exists(Root)) return result;
            var reg = LoadRegistry();
            bool changed = false;
            foreach (var dir in Directory.GetDirectories(Root).OrderBy(d => d, StringComparer.Ordinal))
            {
                var folder = Path.GetFileName(dir);
                if (folder.StartsWith(".") || folder.StartsWith("_")) continue;
                var file = Path.Combine(dir, "SKILL.md");
                if (!System.IO.File.Exists(file)) continue;
                var info = new SkillInfo { Name = folder, Folder = dir, File = file, Builtin = BuiltinNames.Contains(folder) };
                try
                {
                    var md = System.IO.File.ReadAllText(file, Encoding.UTF8);
                    var (name, desc, error) = ParseFrontmatter(md);
                    info.Description = desc;
                    info.Title = TitleOf(md, folder);
                    if (error != null) info.Error = error;
                    else if (name != folder) info.Error = $"name trong file là \"{name}\" nhưng thư mục là \"{folder}\"; hai tên phải giống nhau.";
                }
                catch (Exception ex)
                {
                    info.Error = "Không đọc được SKILL.md: " + ex.Message;
                }
                if (!reg.TryGetValue(folder, out var e))
                {
                    e = new Entry { Name = folder, Enabled = true, Added = info.Builtin ? (DateTime?)null : System.IO.File.GetCreationTime(file) };
                    reg[folder] = e;
                    changed = true;
                }
                info.Enabled = e.Enabled;
                info.Added = e.Added;
                info.Source = e.Source;
                result.Add(info);
            }
            // forget registry entries whose folder is gone
            foreach (var gone in reg.Keys.Where(k => !result.Any(r => r.Name == k)).ToList()) { reg.Remove(gone); changed = true; }
            if (changed)
            {
                try { SaveRegistry(reg.Values); }
                catch (Exception ex) { Logger.Warn("skills.json not saved: " + ex.Message); }
            }
            return result;
        }

        public bool IsWritable()
        {
            try
            {
                Directory.CreateDirectory(Root);
                var probe = Path.Combine(Root, ".write-test-" + Guid.NewGuid().ToString("N"));
                System.IO.File.WriteAllText(probe, "x");
                System.IO.File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ changes

        /// <summary>
        /// Adds a skill from a .md file chosen by the user. A file named SKILL.md brings its
        /// whole folder (reference files); any other .md becomes &lt;name&gt;\SKILL.md.
        /// overwrite=false returns Conflict when a different skill of that name exists.
        /// </summary>
        public SkillImportResult Import(string mdPath, bool overwrite)
        {
            string md;
            try { md = System.IO.File.ReadAllText(mdPath, Encoding.UTF8); }
            catch (Exception ex) { return Invalid(Path.GetFileName(mdPath), "Không đọc được file: " + ex.Message); }

            var (fmName, desc, error) = ParseFrontmatter(md);
            if (error != null) return Invalid(Path.GetFileName(mdPath), error);

            bool isSkillFile = Path.GetFileName(mdPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase);
            string fallback = isSkillFile ? Path.GetFileName(Path.GetDirectoryName(mdPath)) : Path.GetFileNameWithoutExtension(mdPath);
            string name = !string.IsNullOrWhiteSpace(fmName) ? fmName.Trim() : Slugify(fallback);
            if (!IsValidName(name))
            {
                var slug = Slugify(name);
                if (!IsValidName(slug)) return Invalid(Path.GetFileName(mdPath), $"Tên skill \"{name}\" không hợp lệ: chỉ dùng chữ thường không dấu, số và gạch nối, ví dụ ve-ket-cau.");
                name = slug;
            }

            var destDir = Path.Combine(Root, name);
            var destFile = Path.Combine(destDir, "SKILL.md");
            bool exists = System.IO.File.Exists(destFile);
            if (exists && SamePath(destFile, mdPath))
            {
                SetEnabled(name, true);
                return new SkillImportResult { Status = SkillImportStatus.AlreadyAdded, Name = name, Message = $"Skill \"{name}\" đã có trong plugin (đã bật)." };
            }
            if (exists && !overwrite)
                return new SkillImportResult { Status = SkillImportStatus.Conflict, Name = name, Message = $"Plugin đã có skill \"{name}\"." };

            Directory.CreateDirectory(destDir);
            // frontmatter name must match the folder (Agent Skills format)
            var content = EnsureName(md, name);
            if (isSkillFile)
            {
                var srcDir = Path.GetDirectoryName(Path.GetFullPath(mdPath));
                foreach (var f in Directory.GetFiles(srcDir))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (!CopyExtensions.Contains(ext) || Path.GetFileName(f).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)) continue;
                    if (new FileInfo(f).Length > 2_000_000) continue;
                    System.IO.File.Copy(f, Path.Combine(destDir, Path.GetFileName(f)), true);
                }
            }
            System.IO.File.WriteAllText(destFile, content, new UTF8Encoding(false));

            var reg = LoadRegistry();
            reg[name] = new Entry { Name = name, Enabled = true, Added = DateTime.Now, Source = Path.GetFullPath(mdPath) };
            SaveRegistry(reg.Values);
            return new SkillImportResult
            {
                Status = exists ? SkillImportStatus.Updated : SkillImportStatus.Added,
                Name = name,
                Message = exists ? $"Đã cập nhật skill \"{name}\"." : $"Đã thêm skill \"{name}\".",
            };
        }

        private static SkillImportResult Invalid(string file, string why) =>
            new SkillImportResult { Status = SkillImportStatus.Invalid, Name = file, Message = $"Không thêm được \"{file}\": {why}" };

        private static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>Sets "name: x" in the frontmatter (adds it when missing).</summary>
        public static string EnsureName(string md, string name)
        {
            var text = md.TrimStart('﻿').Replace("\r\n", "\n");
            int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
            var head = text.Substring(4, end - 4);
            var lines = head.Split('\n').ToList();
            int idx = lines.FindIndex(l => l.TrimStart().StartsWith("name:", StringComparison.OrdinalIgnoreCase) && !char.IsWhiteSpace(l.Length > 0 ? l[0] : 'x'));
            if (idx >= 0) lines[idx] = "name: " + name;
            else lines.Insert(0, "name: " + name);
            return "---\n" + string.Join("\n", lines) + text.Substring(end);
        }

        public void SetEnabled(string name, bool enabled)
        {
            var reg = LoadRegistry();
            if (!reg.TryGetValue(name, out var e)) e = reg[name] = new Entry { Name = name, Added = DateTime.Now };
            e.Enabled = enabled;
            SaveRegistry(reg.Values);
        }

        public void Delete(string name)
        {
            if (!IsValidName(name)) throw new ArgumentException("invalid skill name");
            var dir = Path.Combine(Root, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var reg = LoadRegistry();
            if (reg.Remove(name)) SaveRegistry(reg.Values);
        }

        public string SkillFile(string name) => Path.Combine(Root, name, "SKILL.md");
    }
}
