using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    // =====================================================================================
    // Bảng thay đổi của lượt (feature "Thêm 15 · Sửa 3 · Xóa 0")
    // =====================================================================================

    /// <summary>
    /// Net changes of one chat turn, fed from the database events (main thread).
    /// Created-then-erased objects (temporary geometry) count as nothing; a new object
    /// that is modified afterwards stays "added".
    /// </summary>
    public sealed class ChangeTracker
    {
        private readonly HashSet<string> _added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _modified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _erased = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        public const int MaxTracked = 100000;

        public void Clear()
        {
            lock (_lock) { _added.Clear(); _modified.Clear(); _erased.Clear(); }
        }

        private int Total => _added.Count + _modified.Count + _erased.Count;

        public void Appended(string h)
        {
            lock (_lock)
            {
                if (Total >= MaxTracked) return;
                if (_erased.Remove(h)) { _modified.Add(h); return; } // erased then restored = changed
                _added.Add(h);
            }
        }

        public void Modified(string h)
        {
            lock (_lock)
            {
                if (Total >= MaxTracked) return;
                if (!_added.Contains(h) && !_erased.Contains(h)) _modified.Add(h);
            }
        }

        public void Erased(string h, bool erased)
        {
            lock (_lock)
            {
                if (!erased) { Appended(h); return; } // un-erase (UNDO of an erase)
                if (_added.Remove(h)) return;         // temporary object of the same turn
                _modified.Remove(h);
                if (Total < MaxTracked) _erased.Add(h);
            }
        }

        public bool IsEmpty { get { lock (_lock) return Total == 0; } }

        /// <summary>Snapshot: (kind, handle) with kind = added | modified | erased.</summary>
        public List<(string kind, string handle)> Snapshot()
        {
            lock (_lock)
            {
                return _added.Select(h => ("added", h))
                    .Concat(_modified.Select(h => ("modified", h)))
                    .Concat(_erased.Select(h => ("erased", h)))
                    .ToList();
            }
        }
    }

    public sealed class ChangeRow
    {
        public string Kind;      // added | modified | erased
        public string Layer;
        public int Count;
        /// <summary>Up to <see cref="ChangeSummary.MaxHandlesPerRow"/> handles (for "zoom").</summary>
        public List<string> Handles = new List<string>();
    }

    /// <summary>Per-turn summary shown as a card and stored with the assistant message.</summary>
    public sealed class ChangeSummary
    {
        public const int MaxHandlesPerRow = 2000;
        public int Added, Modified, Erased;
        public List<ChangeRow> Rows = new List<ChangeRow>();

        public bool IsEmpty => Added + Modified + Erased == 0;

        public string Headline => $"Thêm {Added} · Sửa {Modified} · Xóa {Erased}";

        public static string KindLabel(string kind) => kind == "added" ? "Thêm" : kind == "modified" ? "Sửa" : "Xóa";

        /// <summary>Groups (kind, layer, handle) into rows: kinds in order added, modified, erased; layers by count.</summary>
        public static ChangeSummary Build(IEnumerable<(string kind, string layer, string handle)> items)
        {
            var s = new ChangeSummary();
            var order = new[] { "added", "modified", "erased" };
            var list = items.ToList();
            foreach (var kind in order)
            {
                var ofKind = list.Where(i => i.kind == kind).ToList();
                if (kind == "added") s.Added = ofKind.Count;
                else if (kind == "modified") s.Modified = ofKind.Count;
                else s.Erased = ofKind.Count;
                foreach (var g in ofKind.GroupBy(i => string.IsNullOrEmpty(i.layer) ? "0" : i.layer, StringComparer.OrdinalIgnoreCase)
                                        .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    s.Rows.Add(new ChangeRow
                    {
                        Kind = kind,
                        Layer = g.Key,
                        Count = g.Count(),
                        Handles = g.Select(i => i.handle).Take(MaxHandlesPerRow).ToList(),
                    });
                }
            }
            return s;
        }

        public Dictionary<string, object> ToDict() => new Dictionary<string, object>
        {
            ["added"] = Added,
            ["modified"] = Modified,
            ["erased"] = Erased,
            ["rows"] = Rows.Select(r => (object)new Dictionary<string, object>
            {
                ["kind"] = r.Kind, ["layer"] = r.Layer, ["count"] = r.Count, ["handles"] = r.Handles.Cast<object>().ToList(),
            }).ToList(),
        };

        public static ChangeSummary FromDict(Dictionary<string, object> d)
        {
            if (d == null) return null;
            int N(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is double x ? (int)x : 0;
            var s = new ChangeSummary { Added = N(d, "added"), Modified = N(d, "modified"), Erased = N(d, "erased") };
            if (d.TryGetValue("rows", out var rv) && rv is List<object> rl)
                foreach (var o in rl.OfType<Dictionary<string, object>>())
                    s.Rows.Add(new ChangeRow
                    {
                        Kind = o.TryGetValue("kind", out var k) ? k as string : "added",
                        Layer = o.TryGetValue("layer", out var l) ? l as string : "0",
                        Count = N(o, "count"),
                        Handles = o.TryGetValue("handles", out var h) && h is List<object> hl ? hl.OfType<string>().ToList() : new List<string>(),
                    });
            return s;
        }
    }

    // =====================================================================================
    // Chọn layer để chat chỉnh sửa
    // =====================================================================================

    /// <summary>One layer of the drawing for the palette's layer picker.</summary>
    public sealed class LayerRow
    {
        public string Name;
        public int Rgb = 0xFFFFFF;
        public int Count;
        public bool On = true, Frozen, Locked, Current, Xref;

        public string State
        {
            get
            {
                var s = new List<string>();
                if (Current) s.Add("hiện hành");
                if (!On) s.Add("tắt");
                if (Frozen) s.Add("đóng băng");
                if (Locked) s.Add("khóa");
                if (Xref) s.Add("xref");
                return string.Join(", ", s);
            }
        }
    }

    public static class LayerSelection
    {
        /// <summary>Objects per layer, most first (ties by name).</summary>
        public static Dictionary<string, int> CountByLayer(IEnumerable<string> layers)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in layers ?? Enumerable.Empty<string>())
            {
                var k = string.IsNullOrEmpty(l) ? "0" : l;
                d[k] = d.TryGetValue(k, out var n) ? n + 1 : 1;
            }
            return d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Handles whose layer is not excluded (layers unknown → everything is kept).</summary>
        public static List<string> Filter(IList<string> handles, IList<string> layers, ICollection<string> excluded)
        {
            if (excluded == null || excluded.Count == 0 || layers == null || layers.Count != handles.Count) return handles.ToList();
            var ex = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
            var r = new List<string>();
            for (int i = 0; i < handles.Count; i++) if (!ex.Contains(string.IsNullOrEmpty(layers[i]) ? "0" : layers[i])) r.Add(handles[i]);
            return r;
        }

        /// <summary>"A-WALL, A-DOOR, A-DIM +2".</summary>
        public static string Label(IEnumerable<string> names, int max = 3)
        {
            var l = names.ToList();
            return string.Join(", ", l.Take(max)) + (l.Count > max ? $" +{l.Count - max}" : "");
        }

        /// <summary>Layers whose name contains every word of the query (case-insensitive).</summary>
        public static List<LayerRow> Search(IEnumerable<LayerRow> rows, string query)
        {
            var words = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return rows.Where(r => words.All(w => r.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }
    }

    // =====================================================================================
    // @ — gõ tên layer / block / kiểu chữ có trong bản vẽ
    // =====================================================================================

    public sealed class MentionItem
    {
        public string Name;
        public string Kind;      // layer | block | text_style | dim_style | layout

        public string KindLabel =>
            Kind == "layer" ? "layer" : Kind == "block" ? "block" : Kind == "text_style" ? "kiểu chữ" : Kind == "dim_style" ? "kiểu kích thước" : Kind == "layout" ? "layout" : Kind;
    }

    public static class Mentions
    {
        /// <summary>
        /// True when the caret sits right after "@word" (the @ at the start or after a space /
        /// bracket, so e-mail addresses do not trigger it). query = text typed after the @.
        /// </summary>
        public static bool TryGetToken(string text, int caret, out int start, out string query)
        {
            start = -1;
            query = null;
            if (string.IsNullOrEmpty(text) || caret <= 0 || caret > text.Length) return false;
            int i = caret - 1;
            while (i >= 0 && text[i] != '@')
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == ',' || c == ';' || c == '"' || c == '(' || c == ')' || c == '[' || c == ']') return false;
                i--;
            }
            if (i < 0) return false;
            if (i > 0 && !(char.IsWhiteSpace(text[i - 1]) || text[i - 1] == '(' || text[i - 1] == '[' || text[i - 1] == '"')) return false;
            start = i;
            query = text.Substring(i + 1, caret - i - 1);
            return query.Length <= 64;
        }

        /// <summary>Names that start with the query first, then names that contain it.</summary>
        public static List<MentionItem> Filter(IEnumerable<MentionItem> items, string query, int max = 40)
        {
            var q = query ?? "";
            return items
                .Where(m => q.Length == 0 || m.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(m => m.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(m => KindOrder(m.Kind))
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        private static int KindOrder(string k) => k == "layer" ? 0 : k == "block" ? 1 : k == "text_style" ? 2 : k == "dim_style" ? 3 : 4;

        /// <summary>Replaces "@query" with the name ("quoted" if it contains spaces); returns the new text and caret.</summary>
        public static string Insert(string text, int start, int caret, string name, out int newCaret)
        {
            var token = name.IndexOf(' ') >= 0 ? "\"" + name + "\"" : name;
            var after = caret < text.Length && !char.IsWhiteSpace(text[caret]) ? " " : "";
            var result = text.Substring(0, start) + token + after + text.Substring(caret);
            newCaret = start + token.Length + after.Length;
            if (after.Length == 0 && (newCaret >= result.Length || !char.IsWhiteSpace(result[newCaret])))
            {
                result = result.Insert(newCaret, " ");
                newCaret++;
            }
            return result;
        }
    }

    // =====================================================================================
    // Ghi chú dự án: gửi kèm mọi tin nhắn
    // =====================================================================================

    public static class ProjectNotes
    {
        /// <summary>Shared notes of a project folder (all drawings in it), next to the DWG files.</summary>
        public const string FolderFileName = "ClaudeCAD-ghichu.md";
        public const int MaxChars = 4000;

        public static string FolderFile(string drawingPath)
        {
            if (string.IsNullOrEmpty(drawingPath)) return null;
            try
            {
                var dir = Path.GetDirectoryName(drawingPath);
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FolderFileName);
            }
            catch { return null; }
        }

        public static string LoadFolder(string drawingPath)
        {
            var f = FolderFile(drawingPath);
            try { return f != null && File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : ""; }
            catch (Exception ex) { Logger.Warn("project notes unreadable: " + ex.Message); return ""; }
        }

        /// <summary>Writes (or deletes when empty) the folder notes file. Throws on I/O errors.</summary>
        public static void SaveFolder(string drawingPath, string text)
        {
            var f = FolderFile(drawingPath) ?? throw new InvalidOperationException("Bản vẽ chưa được lưu nên chưa có thư mục dự án.");
            text = (text ?? "").Trim();
            if (text.Length == 0) { if (File.Exists(f)) File.Delete(f); return; }
            File.WriteAllText(f, Clip(text) + "\n", new UTF8Encoding(false));
        }

        public static string Clip(string s)
        {
            s = (s ?? "").Trim();
            return s.Length <= MaxChars ? s : s.Substring(0, MaxChars);
        }

        /// <summary>Notes for the prompt: project folder first, then this drawing; null when both are empty.</summary>
        public static string Combine(string folderNotes, string drawingNotes)
        {
            var parts = new List<string>();
            var f = Clip(folderNotes);
            var d = Clip(drawingNotes);
            if (f.Length > 0) parts.Add("Ghi chú dự án (cả thư mục):\n" + f);
            if (d.Length > 0) parts.Add("Ghi chú riêng bản vẽ này:\n" + d);
            return parts.Count == 0 ? null : string.Join("\n\n", parts);
        }
    }
}
