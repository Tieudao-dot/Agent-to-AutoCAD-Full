using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    // =====================================================================================
    // "Biên bản" of a drawing: what each object looked like after the last chat turn.
    // Compared with the drawing later (another AutoCAD session, another computer) to tell
    // Claude what the user changed by hand in between.
    // =====================================================================================

    public sealed class SnapshotEntry
    {
        public string Type;
        public string Layer;
        /// <summary>Short checksum of the entity's properties (geometry, layer, color, text...).</summary>
        public string Sig;
    }

    public sealed class DrawingSnapshot
    {
        public const int Version = 1;
        /// <summary>Above this many entities only layer counts + the objects Claude touched are kept.</summary>
        public const int FullLimit = 20000;
        public const int MaxTracked = 20000;

        public string DrawingKey = "";
        public DateTime Time = DateTime.Now;
        /// <summary>true = every entity of model/paper space is listed; false = large drawing (counts + tracked only).</summary>
        public bool Full = true;
        public int EntityCount;
        public Dictionary<string, SnapshotEntry> Entries = new Dictionary<string, SnapshotEntry>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> LayerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public byte[] ToBytes()
        {
            var d = new Dictionary<string, object>
            {
                ["version"] = Version,
                ["drawing_key"] = DrawingKey,
                ["time"] = Time.ToString("o", CultureInfo.InvariantCulture),
                ["full"] = Full,
                ["entity_count"] = EntityCount,
                ["layers"] = LayerCounts.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                // compact rows: [handle, type, layer, sig]
                ["entries"] = Entries.Select(kv => (object)new List<object> { kv.Key, kv.Value.Type, kv.Value.Layer, kv.Value.Sig }).ToList(),
            };
            var json = Encoding.UTF8.GetBytes(JsonCodec.Serialize(d));
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) gz.Write(json, 0, json.Length);
                return ms.ToArray();
            }
        }

        public static DrawingSnapshot FromBytes(byte[] data)
        {
            string json;
            using (var ms = new MemoryStream(data))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8)) json = r.ReadToEnd();
            var d = JsonCodec.ParseObject(json);
            var s = new DrawingSnapshot
            {
                DrawingKey = d.TryGetValue("drawing_key", out var k) ? k as string ?? "" : "",
                Full = !d.TryGetValue("full", out var f) || !(f is bool fb) || fb,
                EntityCount = d.TryGetValue("entity_count", out var c) && c is double cd ? (int)cd : 0,
            };
            if (d.TryGetValue("time", out var t) && t is string ts && DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var tm)) s.Time = tm;
            if (d.TryGetValue("layers", out var lv) && lv is Dictionary<string, object> ld)
                foreach (var kv in ld) if (kv.Value is double n) s.LayerCounts[kv.Key] = (int)n;
            if (d.TryGetValue("entries", out var ev) && ev is List<object> el)
                foreach (var row in el.OfType<List<object>>())
                    if (row.Count >= 4 && row[0] is string h)
                        s.Entries[h] = new SnapshotEntry { Type = row[1] as string ?? "", Layer = row[2] as string ?? "0", Sig = row[3] as string ?? "" };
            return s;
        }

        /// <summary>64-bit FNV-1a of the parts → 16 hex characters.</summary>
        public static string Sig(IEnumerable<string> parts)
        {
            ulong h = 14695981039346656037UL;
            foreach (var p in parts)
            {
                foreach (var ch in p ?? "")
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
                h ^= 0x1F; // separator
                h *= 1099511628211UL;
            }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>Number formatting for signatures: rounded to 0.01 mm so float noise is not a "change".</summary>
        public static string N(double v)
        {
            var r = Math.Round(v, 2);
            if (r == 0) r = 0; // no "-0"
            return r.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Differences between an old snapshot and the drawing now.</summary>
    public sealed class SnapshotDiff
    {
        public List<(string kind, string layer, string handle)> Items = new List<(string, string, string)>();
        /// <summary>Large drawings: layers whose entity count changed (old → new).</summary>
        public List<(string layer, int before, int after)> LayerCountChanges = new List<(string, int, int)>();

        public bool IsEmpty => Items.Count == 0 && LayerCountChanges.Count == 0;

        public static SnapshotDiff Compare(DrawingSnapshot before, DrawingSnapshot now)
        {
            var d = new SnapshotDiff();
            if (before == null || now == null) return d;
            if (before.Full && now.Full)
            {
                foreach (var kv in now.Entries)
                {
                    if (!before.Entries.TryGetValue(kv.Key, out var old)) d.Items.Add(("added", kv.Value.Layer, kv.Key));
                    else if (old.Sig != kv.Value.Sig || old.Layer != kv.Value.Layer) d.Items.Add(("modified", kv.Value.Layer, kv.Key));
                }
                foreach (var kv in before.Entries)
                    if (!now.Entries.ContainsKey(kv.Key)) d.Items.Add(("erased", kv.Value.Layer, kv.Key));
                return d;
            }
            // large drawing: per-object only for the objects listed in the old snapshot, plus layer counts
            foreach (var kv in before.Entries)
            {
                if (!now.Entries.TryGetValue(kv.Key, out var cur)) d.Items.Add(("erased", kv.Value.Layer, kv.Key));
                else if (cur.Sig != kv.Value.Sig || cur.Layer != kv.Value.Layer) d.Items.Add(("modified", cur.Layer, kv.Key));
            }
            foreach (var layer in before.LayerCounts.Keys.Union(now.LayerCounts.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                before.LayerCounts.TryGetValue(layer, out var a);
                now.LayerCounts.TryGetValue(layer, out var b);
                if (a != b) d.LayerCountChanges.Add((layer, a, b));
            }
            return d;
        }
    }

    /// <summary>What the user changed outside the chat, as sent to Claude with the next message.</summary>
    public static class ManualChangeReport
    {
        public const int MaxHandles = 300;

        /// <summary>
        /// Context object for the sidecar: text = Vietnamese summary by kind and layer, handles per kind
        /// (capped), more = handles left out. since = "session" (this AutoCAD session) or "previous" (an
        /// earlier session / another computer). Null when nothing changed.
        /// </summary>
        public static Dictionary<string, object> Build(IEnumerable<(string kind, string layer, string handle)> items,
            IEnumerable<(string layer, int before, int after)> layerCounts, string since)
        {
            var list = items?.ToList() ?? new List<(string, string, string)>();
            var counts = layerCounts?.ToList() ?? new List<(string, int, int)>();
            if (list.Count == 0 && counts.Count == 0) return null;
            var parts = new List<string>();
            foreach (var kind in new[] { "modified", "erased", "added" })
            {
                var ofKind = list.Where(i => i.kind == kind).ToList();
                if (ofKind.Count == 0) continue;
                var byLayer = ofKind.GroupBy(i => string.IsNullOrEmpty(i.layer) ? "0" : i.layer, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToList();
                var layers = string.Join(", ", byLayer.Take(5).Select(g => $"{g.Key} {g.Count()}")) + (byLayer.Count > 5 ? ", …" : "");
                parts.Add($"{Label(kind)} {ofKind.Count} ({layers})");
            }
            if (counts.Count > 0)
                parts.Add("số đối tượng theo layer: " + string.Join(", ", counts.Take(10).Select(c => $"{c.layer} {c.before}→{c.after}")) + (counts.Count > 10 ? ", …" : ""));
            var r = new Dictionary<string, object> { ["text"] = string.Join("; ", parts), ["since"] = since };
            int budget = MaxHandles, more = 0;
            foreach (var kind in new[] { "modified", "erased", "added" })
            {
                var hs = list.Where(i => i.kind == kind).Select(i => i.handle).ToList();
                if (hs.Count == 0) continue;
                var take = hs.Take(Math.Max(0, budget)).ToList();
                budget -= take.Count;
                more += hs.Count - take.Count;
                if (take.Count > 0) r[kind] = take.Cast<object>().ToList();
            }
            if (more > 0) r["more"] = more;
            return r;
        }

        public static string Label(string kind) => kind == "added" ? "thêm" : kind == "modified" ? "sửa" : "xóa";

        /// <summary>One-line note for the palette, e.g. "sửa 12, xóa 3, thêm 40".</summary>
        public static string Short(IEnumerable<(string kind, string layer, string handle)> items, int layerChanges)
        {
            var list = items.ToList();
            var parts = new[] { "modified", "erased", "added" }
                .Select(k => (k, n: list.Count(i => i.kind == k))).Where(x => x.n > 0).Select(x => $"{Label(x.k)} {x.n}").ToList();
            if (layerChanges > 0) parts.Add($"{layerChanges} layer đổi số lượng");
            return string.Join(", ", parts);
        }
    }

    // =====================================================================================
    // Version history: a copy of the drawing before chat changes it, at most every 30 min.
    // =====================================================================================

    public sealed class VersionInfo
    {
        public string Id;          // file name without extension, e.g. v-20261106-140512
        public DateTime Time;
        public string Label;       // the chat request that triggered the copy
        public long Bytes;
    }

    public sealed class VersionStore
    {
        public const int Keep = 10;
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        public string Dir { get; }

        public VersionStore(string root, string drawingKey)
        {
            Dir = Path.Combine(root, ChatTranscript.FileNameFor(drawingKey).Replace("chat-", "v-").Replace(".json", ""));
        }

        private string IndexFile => Path.Combine(Dir, "versions.json");

        public string FileOf(VersionInfo v) => Path.Combine(Dir, v.Id + ".dwg");

        public List<VersionInfo> List()
        {
            var list = new List<VersionInfo>();
            try
            {
                if (!File.Exists(IndexFile)) return list;
                var root = JsonCodec.ParseObject(File.ReadAllText(IndexFile, Encoding.UTF8));
                if (root.TryGetValue("versions", out var vv) && vv is List<object> vl)
                    foreach (var o in vl.OfType<Dictionary<string, object>>())
                    {
                        var v = new VersionInfo
                        {
                            Id = o.TryGetValue("id", out var i) ? i as string : null,
                            Label = o.TryGetValue("label", out var l) ? l as string ?? "" : "",
                            Bytes = o.TryGetValue("bytes", out var b) && b is double bd ? (long)bd : 0,
                        };
                        if (o.TryGetValue("time", out var t) && t is string ts && DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var tm)) v.Time = tm;
                        if (!string.IsNullOrEmpty(v.Id) && File.Exists(FileOf(v))) list.Add(v);
                    }
            }
            catch (Exception ex) { Logger.Warn("versions index unreadable: " + ex.Message); }
            return list.OrderByDescending(v => v.Time).ToList();
        }

        /// <summary>A new copy is due when none exists yet or the newest is at least 30 minutes old.</summary>
        public static bool IsDue(DateTime? newest, DateTime now) => !newest.HasValue || now - newest.Value >= Interval;

        public bool IsDue(DateTime now) => IsDue(List().Select(v => (DateTime?)v.Time).FirstOrDefault(), now);

        /// <summary>Path for a new copy (the caller writes the DWG there, then calls Register).</summary>
        public string NewFile(DateTime now, out string id)
        {
            Directory.CreateDirectory(Dir);
            id = "v-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var file = Path.Combine(Dir, id + ".dwg");
            for (int n = 2; File.Exists(file); n++) { id = "v-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + n; file = Path.Combine(Dir, id + ".dwg"); }
            return file;
        }

        /// <summary>Adds the copy to the index and deletes the oldest beyond <see cref="Keep"/>.</summary>
        public void Register(string id, DateTime time, string label)
        {
            var list = List();
            var v = new VersionInfo { Id = id, Time = time, Label = Shorten(label), Bytes = 0 };
            try { v.Bytes = new FileInfo(FileOf(v)).Length; } catch { }
            list.Insert(0, v);
            foreach (var old in list.Skip(Keep).ToList())
            {
                try { File.Delete(FileOf(old)); } catch { }
                list.Remove(old);
            }
            Save(list);
        }

        public bool Delete(string id)
        {
            var list = List();
            var v = list.FirstOrDefault(x => x.Id == id);
            if (v == null) return false;
            try { File.Delete(FileOf(v)); } catch { }
            list.Remove(v);
            Save(list);
            return true;
        }

        private void Save(List<VersionInfo> list)
        {
            Directory.CreateDirectory(Dir);
            var json = JsonCodec.Serialize(new Dictionary<string, object>
            {
                ["versions"] = list.Select(v => (object)new Dictionary<string, object>
                {
                    ["id"] = v.Id, ["time"] = v.Time.ToString("o", CultureInfo.InvariantCulture), ["label"] = v.Label, ["bytes"] = v.Bytes,
                }).ToList(),
            });
            File.WriteAllText(IndexFile, json, new UTF8Encoding(false));
        }

        private static string Shorten(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= 80 ? s : s.Substring(0, 79) + "…";
        }
    }
}
