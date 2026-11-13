using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>One recorded drawing tool call of a macro.</summary>
    public sealed class MacroCall
    {
        public string Tool;
        public Dictionary<string, object> Params = new Dictionary<string, object>();
        /// <summary>Handles the call returned when it was recorded (used to remap later calls).</summary>
        public List<string> ResultHandles = new List<string>();
    }

    /// <summary>
    /// A chat turn saved as a macro (palette feature "Lưu thành macro"): the drawing tool
    /// calls Claude made, replayed directly by the plugin without Claude (no tokens).
    /// </summary>
    public sealed class Macro
    {
        public string Name = "";
        public string Description = "";
        public DateTime Created = DateTime.Now;
        public string Drawing = "";
        public List<MacroCall> Calls = new List<MacroCall>();
        /// <summary>Lower-left corner of what the macro drew (WCS mm); null = cannot be placed at a picked point.</summary>
        public double[] BasePoint;

        public string ToJson()
        {
            var d = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["name"] = Name,
                ["description"] = Description,
                ["created"] = Created.ToString("o", CultureInfo.InvariantCulture),
                ["drawing"] = Drawing,
                ["base_point"] = BasePoint?.Cast<object>().ToList(),
                ["calls"] = Calls.Select(c => (object)new Dictionary<string, object>
                {
                    ["tool"] = c.Tool,
                    ["params"] = c.Params,
                    ["result_handles"] = c.ResultHandles.Cast<object>().ToList(),
                }).ToList(),
            };
            return JsonCodec.Serialize(d);
        }

        public static Macro FromJson(string json)
        {
            var d = JsonCodec.ParseObject(json);
            string S(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v as string : null;
            var m = new Macro { Name = S(d, "name") ?? "", Description = S(d, "description") ?? "", Drawing = S(d, "drawing") ?? "" };
            if (DateTime.TryParse(S(d, "created"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) m.Created = t;
            if (d.TryGetValue("base_point", out var bp) && bp is List<object> bl && bl.Count >= 2 && bl.All(x => x is double))
                m.BasePoint = bl.Cast<double>().ToArray();
            if (d.TryGetValue("calls", out var cv) && cv is List<object> cl)
            {
                foreach (var o in cl.OfType<Dictionary<string, object>>())
                {
                    var tool = S(o, "tool");
                    if (string.IsNullOrEmpty(tool)) continue;
                    var c = new MacroCall { Tool = tool };
                    if (o.TryGetValue("params", out var pv) && pv is Dictionary<string, object> pd) c.Params = pd;
                    if (o.TryGetValue("result_handles", out var hv) && hv is List<object> hl) c.ResultHandles = hl.OfType<string>().ToList();
                    m.Calls.Add(c);
                }
            }
            return m;
        }
    }

    /// <summary>Recording and replay rules, independent of AutoCAD (unit-tested).</summary>
    public static class MacroLogic
    {
        /// <summary>Tools never recorded: view changes, the UCS (a working plane; every tool takes WCS), file operations, undo, purge.</summary>
        public static readonly HashSet<string> NotRecorded = new HashSet<string>(StringComparer.Ordinal)
        {
            "zoom_extents", "zoom_window", "set_view_3d", "set_visual_style", "set_ucs", "reset_ucs", "regen", "save_drawing", "purge_unused", "undo_last_turn", "batch_execute",
        };

        public const int MaxCalls = 2000;

        /// <summary>
        /// Tools of the macro outside this machine's plan (e.g. recorded on Full, run on 2D / 3D). The macro is then
        /// not run at all: running only the calls in the plan would leave half a drawing.
        /// </summary>
        public static List<string> BlockedCalls(Macro m, string plan) =>
            PlanLogic.BlockedCalls(plan, (m?.Calls ?? new List<MacroCall>()).Select(c => (c.Tool, (IDictionary<string, object>)c.Params)));

        public static bool ShouldRecord(string tool, bool writes) => writes && !string.IsNullOrEmpty(tool) && !NotRecorded.Contains(tool);

        /// <summary>The "handles" array of a tool result (strings only).</summary>
        public static List<string> ResultHandles(object result)
        {
            if (result is Dictionary<string, object> d && d.TryGetValue("handles", out var h) && h is List<object> l)
                return l.OfType<string>().ToList();
            return new List<string>();
        }

        /// <summary>Deep copy of a JSON value (params are recorded before the next call can change anything).</summary>
        public static object DeepCopy(object v)
        {
            switch (v)
            {
                case Dictionary<string, object> d: return d.ToDictionary(kv => kv.Key, kv => DeepCopy(kv.Value));
                case List<object> l: return l.Select(DeepCopy).ToList();
                default: return v;
            }
        }

        /// <summary>
        /// Deep copy where the values of handle parameters (handle / handles / *_handle(s)) that are recorded
        /// handles are replaced by the handles created on replay. Other strings (texts, layer names) are kept
        /// even if they happen to look like a handle.
        /// </summary>
        public static object Remap(object v, IDictionary<string, string> map) => Remap(v, map, false);

        private static object Remap(object v, IDictionary<string, string> map, bool handleValue)
        {
            switch (v)
            {
                case string s: return handleValue && map.TryGetValue(s, out var n) ? n : s;
                case Dictionary<string, object> d: return d.ToDictionary(kv => kv.Key, kv => Remap(kv.Value, map, IsHandleKey(kv.Key)));
                case List<object> l: return l.Select(x => Remap(x, map, handleValue)).ToList();
                default: return v;
            }
        }

        /// <summary>
        /// handle / handles / *_handle(s) / handle1, handle2 (fillet_entities, chamfer_entities) / target, subtract
        /// (solid_subtract: without them a replayed subtract would cut the recorded solids, not the new ones) / source
        /// (match_properties, nâng cấp đợt B).
        /// </summary>
        public static bool IsHandleKey(string k) =>
            k == "handle" || k == "handles" || k == "target" || k == "subtract" || k == "source" || k.EndsWith("_handle", StringComparison.Ordinal) || k.EndsWith("_handles", StringComparison.Ordinal) ||
            (k.Length > 6 && k.StartsWith("handle", StringComparison.Ordinal) && k.Substring(6).All(char.IsDigit));

        /// <summary>
        /// Handles created by an earlier call of the macro that this call uses but that were not re-created
        /// on replay (that call failed). Sending them unchanged would hit the user's own objects, so the
        /// call must be skipped.
        /// </summary>
        public static List<string> Unresolved(Dictionary<string, object> p, ISet<string> createdByMacro, IDictionary<string, string> map) =>
            HandleParams(p).Where(h => createdByMacro.Contains(h) && !map.ContainsKey(h)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>After a replayed call: recorded handle k ↦ new handle k (first mapping wins).</summary>
        public static void Learn(IDictionary<string, string> map, IList<string> recorded, IList<string> replayed)
        {
            int n = Math.Min(recorded.Count, replayed.Count);
            for (int i = 0; i < n; i++)
                if (!map.ContainsKey(recorded[i])) map[recorded[i]] = replayed[i];
        }

        /// <summary>
        /// Handles the macro uses but did not create itself (objects that existed before the turn).
        /// Replaying the macro in another drawing fails for the calls that use them.
        /// </summary>
        public static List<string> ExternalHandles(Macro m)
        {
            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var external = new List<string>();
            foreach (var c in m.Calls)
            {
                foreach (var h in HandleParams(c.Params))
                    if (!created.Contains(h) && !external.Contains(h, StringComparer.OrdinalIgnoreCase)) external.Add(h);
                foreach (var h in c.ResultHandles) created.Add(h);
            }
            return external;
        }

        /// <summary>The 3D tools that change a solid's shape: from a turn they always need the confirmation card (decided 12/11/2026).</summary>
        public static readonly HashSet<string> AlwaysConfirmTools = new HashSet<string>(StringComparer.Ordinal)
        {
            "solid_union", "solid_subtract", "solid_intersect", "slice_solid", "fillet_edges", "chamfer_edges", "shell_solid",
            // Test & sửa đợt D (ND-3): PRESSPULL on a solid that existed before (handle, target)
            "presspull",
        };

        /// <summary>
        /// The solids a call of an AlwaysConfirmTools tool changes. presspull: the solid whose faces it pulls (handle) and the
        /// target unless mode "new" (its profile curves are only read, or erased by the usual count rule); others: every handle param.
        /// </summary>
        public static IEnumerable<string> SolidEditHandles(string tool, Dictionary<string, object> p)
        {
            if (tool != "presspull") return HandleParams(p);
            var hs = new List<string>();
            if (p.TryGetValue("handle", out var h) && h is string hh) hs.Add(hh);
            bool isNew = p.TryGetValue("mode", out var m) && m is string ms && ms.Trim().Equals("new", StringComparison.OrdinalIgnoreCase);
            if (!isNew && p.TryGetValue("target", out var t) && t is string th) hs.Add(th);
            return hs;
        }

        /// <summary>
        /// KT-10: calls of the macro that change the shape of solids the macro did not build itself ("tool (handles)"). A macro
        /// has no confirmation card, so such a macro is not run (the 3D booleans / slice / fillet / chamfer / shell of
        /// objects that already exist go through Claude, who asks with the card). explode / erase of older solids are refused
        /// by the plugin call by call (no user_confirmed in a macro).
        /// </summary>
        public static List<string> ExternalSolidEdits(Macro m)
        {
            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var found = new List<string>();
            foreach (var c in m?.Calls ?? new List<MacroCall>())
            {
                if (AlwaysConfirmTools.Contains(c.Tool ?? ""))
                {
                    var ext = SolidEditHandles(c.Tool, c.Params ?? new Dictionary<string, object>()).Where(h => !created.Contains(h)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (ext.Count > 0) found.Add($"{c.Tool} ({string.Join(", ", ext.Take(5))}{(ext.Count > 5 ? ", …" : "")})");
                }
                foreach (var h in c.ResultHandles ?? new List<string>()) created.Add(h);
            }
            return found;
        }

        /// <summary>What the palette says when ExternalSolidEdits refuses a macro (Vietnamese, for the user).</summary>
        public static string ExternalSolidEditsMessage(List<string> edits) =>
            "Macro này sửa khối 3D có sẵn trong bản vẽ (" + string.Join("; ", edits.Take(3)) + (edits.Count > 3 ? "; …" : "") + "). " +
            "Chạy macro không có thẻ xác nhận nên tớ không chạy. Nhờ Claude làm việc này (Claude hỏi bằng thẻ xác nhận), " +
            "hoặc lưu lại macro chỉ sửa khối do chính macro dựng.";

        /// <summary>Values of params named handle / handles / *_handle(s) (where tools take entity handles).</summary>
        public static IEnumerable<string> HandleParams(Dictionary<string, object> p)
        {
            foreach (var kv in p)
            {
                bool named = IsHandleKey(kv.Key);
                if (named)
                {
                    if (kv.Value is string s) yield return s;
                    else if (kv.Value is List<object> l) foreach (var x in l.OfType<string>()) yield return x;
                }
                else if (kv.Value is Dictionary<string, object> nested)
                    foreach (var x in HandleParams(nested)) yield return x;
            }
        }

        /// <summary>Handles created by the macro (results that did not appear in any earlier param), in order.</summary>
        public static List<string> CreatedHandles(Macro m)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var created = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in m.Calls)
            {
                foreach (var h in HandleParams(c.Params)) used.Add(h);
                foreach (var h in c.ResultHandles)
                    if (!used.Contains(h) && seen.Add(h)) created.Add(h);
            }
            return created;
        }

        /// <summary>True when any string anywhere in the value is one of the handles.</summary>
        public static bool References(object v, ISet<string> handles)
        {
            if (handles.Count == 0) return false;
            switch (v)
            {
                case string str: return handles.Contains(str);
                case Dictionary<string, object> d: return d.Values.Any(x => References(x, handles));
                case List<object> l: return l.Any(x => References(x, handles));
                default: return false;
            }
        }

        /// <summary>
        /// Groups the calls into batches that can run as one batch_execute: a call starts a new batch when it
        /// uses a handle created inside the current batch (unknown until the batch returns), when its tool
        /// cannot run in a batch, or when the batch is full. Single-call groups run on their own.
        /// </summary>
        public static List<List<int>> PlanBatches(IList<MacroCall> calls, Func<string, bool> batchable, int max = 500)
        {
            var groups = new List<List<int>>();
            var cur = new List<int>();
            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Flush()
            {
                if (cur.Count > 0) groups.Add(cur);
                cur = new List<int>();
                created.Clear();
            }
            for (int i = 0; i < calls.Count; i++)
            {
                var c = calls[i];
                if (!batchable(c.Tool))
                {
                    Flush();
                    groups.Add(new List<int> { i });
                    continue;
                }
                if (cur.Count >= max || HandleParams(c.Params).Any(created.Contains)) Flush();
                cur.Add(i);
                foreach (var h in c.ResultHandles) created.Add(h);
            }
            Flush();
            return groups;
        }

        /// <summary>Short Vietnamese list of what the macro does, e.g. "12 lệnh: draw_line ×8, add_text ×4".</summary>
        public static string Describe(Macro m)
        {
            var groups = m.Calls.GroupBy(c => c.Tool).OrderByDescending(g => g.Count()).Take(4)
                .Select(g => $"{g.Key} ×{g.Count()}");
            return $"{m.Calls.Count} lệnh: {string.Join(", ", groups)}{(m.Calls.Select(c => c.Tool).Distinct().Count() > 4 ? ", …" : "")}";
        }
    }

    /// <summary>Collects the drawing tool calls of the running chat turn (thread-safe).</summary>
    public sealed class MacroRecorder
    {
        private readonly object _lock = new object();
        private List<MacroCall> _calls = new List<MacroCall>();
        private bool _overflow;

        public void Clear()
        {
            lock (_lock) { _calls = new List<MacroCall>(); _overflow = false; }
        }

        public void Record(string tool, Dictionary<string, object> p, object result)
        {
            lock (_lock)
            {
                if (_calls.Count >= MacroLogic.MaxCalls) { _overflow = true; return; }
                var copy = (MacroLogic.DeepCopy(p ?? new Dictionary<string, object>()) as Dictionary<string, object>) ?? new Dictionary<string, object>();
                copy.Remove(SolidLogic.UserConfirmedParam); // KT-10: the card's yes belongs to that turn, never to a replay
                copy.Remove(Edit2DLogic.WindowLimitParam); // NB-1: the turn's 20-object budget belongs to that turn too
                _calls.Add(new MacroCall
                {
                    Tool = tool,
                    Params = copy,
                    ResultHandles = MacroLogic.ResultHandles(result),
                });
            }
        }

        /// <summary>Calls of the turn; null when nothing was recorded or the turn was too long.</summary>
        public List<MacroCall> Take()
        {
            lock (_lock)
            {
                var r = _overflow || _calls.Count == 0 ? null : _calls;
                _calls = new List<MacroCall>();
                _overflow = false;
                return r;
            }
        }
    }

    /// <summary>Macros as JSON files in the plugin's macros folder (next to skills).</summary>
    public sealed class MacroStore
    {
        public string Root { get; }

        public MacroStore(string root)
        {
            Root = root;
        }

        public static string FileName(string name)
        {
            var slug = SkillStore.Slugify(name);
            return (slug.Length > 0 ? slug : "macro") + ".json";
        }

        public List<Macro> List()
        {
            var list = new List<Macro>();
            try
            {
                if (!Directory.Exists(Root)) return list;
                foreach (var f in Directory.GetFiles(Root, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var m = Macro.FromJson(File.ReadAllText(f, Encoding.UTF8));
                        if (string.IsNullOrEmpty(m.Name)) m.Name = Path.GetFileNameWithoutExtension(f);
                        list.Add(m);
                    }
                    catch (Exception ex) { Logger.Warn($"macro {Path.GetFileName(f)} unreadable: {ex.Message}"); }
                }
            }
            catch (Exception ex) { Logger.Warn("macros folder unreadable: " + ex.Message); }
            return list;
        }

        public bool Exists(string name) => File.Exists(Path.Combine(Root, FileName(name)));

        public string Save(Macro m)
        {
            if (string.IsNullOrWhiteSpace(m.Name)) throw new ArgumentException("Tên macro không được để trống.");
            Directory.CreateDirectory(Root);
            var file = Path.Combine(Root, FileName(m.Name));
            File.WriteAllText(file, m.ToJson(), new UTF8Encoding(false));
            return file;
        }

        public Macro Load(string name)
        {
            var file = Path.Combine(Root, FileName(name));
            return File.Exists(file) ? Macro.FromJson(File.ReadAllText(file, Encoding.UTF8)) : null;
        }

        public bool Delete(string name)
        {
            var file = Path.Combine(Root, FileName(name));
            if (!File.Exists(file)) return false;
            File.Delete(file);
            return true;
        }
    }
}
