using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>One tool call in a turn's activity log (feature 12).</summary>
    public sealed class ToolLogEntry
    {
        public string Tool;
        public string State;     // running | done | error
        public long Ms;
        public string Summary;
        public string Error;
    }

    /// <summary>One message in the palette history.</summary>
    public sealed class ChatEntry
    {
        public string Role;      // user | assistant | note | error
        public string Text;
        public DateTime Time = DateTime.Now;
        public List<ToolLogEntry> Tools = new List<ToolLogEntry>();
        /// <summary>Handles touched by the turn (assistant entries).</summary>
        public List<string> Handles = new List<string>();
        public int ImageCount;
        public int SelectionCount;
    }

    /// <summary>
    /// Chat history of one drawing, persisted in %LocalAppData%\ClaudeCAD\chats (feature 17),
    /// together with the Agent SDK session id so the conversation can be resumed.
    /// </summary>
    public sealed class ChatTranscript
    {
        public const int MaxEntries = 400;

        public string DrawingKey = "";
        public string DrawingName = "";
        public string SessionId;
        public List<ChatEntry> Entries = new List<ChatEntry>();

        public static string KeyFor(string drawingPath)
        {
            if (string.IsNullOrEmpty(drawingPath)) return "(none)";
            return drawingPath.Trim().ToLowerInvariant();
        }

        public static string FileNameFor(string key)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? ""));
                var sb = new StringBuilder("chat-");
                for (int i = 0; i < 10; i++) sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.Append(".json").ToString();
            }
        }

        public void Trim()
        {
            if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
        }

        // ------------------------------------------------------------------ JSON

        public string ToJson()
        {
            var d = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["drawing_key"] = DrawingKey,
                ["drawing_name"] = DrawingName,
                ["session_id"] = SessionId,
                ["entries"] = Entries.Select(e => (object)new Dictionary<string, object>
                {
                    ["role"] = e.Role,
                    ["text"] = e.Text,
                    ["time"] = e.Time.ToString("o", CultureInfo.InvariantCulture),
                    ["images"] = e.ImageCount,
                    ["selection"] = e.SelectionCount,
                    ["handles"] = e.Handles.Cast<object>().ToList(),
                    ["tools"] = e.Tools.Select(t => (object)new Dictionary<string, object>
                    {
                        ["tool"] = t.Tool, ["state"] = t.State, ["ms"] = t.Ms, ["summary"] = t.Summary, ["error"] = t.Error,
                    }).ToList(),
                }).ToList(),
            };
            return JsonCodec.Serialize(d);
        }

        public static ChatTranscript FromJson(string json)
        {
            var d = JsonCodec.ParseObject(json);
            string S(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v as string : null;
            double N(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is double x ? x : 0;
            var t = new ChatTranscript
            {
                DrawingKey = S(d, "drawing_key") ?? "",
                DrawingName = S(d, "drawing_name") ?? "",
                SessionId = S(d, "session_id"),
            };
            if (d.TryGetValue("entries", out var ev) && ev is List<object> list)
            {
                foreach (var o in list.OfType<Dictionary<string, object>>())
                {
                    var e = new ChatEntry
                    {
                        Role = S(o, "role") ?? "note",
                        Text = S(o, "text") ?? "",
                        ImageCount = (int)N(o, "images"),
                        SelectionCount = (int)N(o, "selection"),
                    };
                    if (DateTime.TryParse(S(o, "time"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var tm)) e.Time = tm;
                    if (o.TryGetValue("handles", out var hv) && hv is List<object> hl) e.Handles = hl.OfType<string>().ToList();
                    if (o.TryGetValue("tools", out var tv) && tv is List<object> tl)
                        foreach (var to in tl.OfType<Dictionary<string, object>>())
                            e.Tools.Add(new ToolLogEntry { Tool = S(to, "tool"), State = S(to, "state"), Ms = (long)N(to, "ms"), Summary = S(to, "summary"), Error = S(to, "error") });
                    t.Entries.Add(e);
                }
            }
            return t;
        }

        // ------------------------------------------------------------------ export (feature 18)

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ClaudeCAD — " + (string.IsNullOrEmpty(DrawingName) ? "chat" : DrawingName));
            sb.AppendLine();
            foreach (var e in Entries)
            {
                string who = e.Role == "user" ? "Người dùng" : e.Role == "assistant" ? "Claude" : e.Role == "error" ? "Lỗi" : "Ghi chú";
                sb.Append("## ").Append(who).Append(" · ").AppendLine(e.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                sb.AppendLine();
                if (e.SelectionCount > 0) sb.AppendLine($"_Kèm {e.SelectionCount} đối tượng đang chọn_").AppendLine();
                if (e.ImageCount > 0) sb.AppendLine($"_Kèm {e.ImageCount} ảnh_").AppendLine();
                sb.AppendLine(e.Text);
                sb.AppendLine();
                if (e.Tools.Count > 0)
                {
                    sb.AppendLine("<details><summary>Thao tác (" + e.Tools.Count + ")</summary>");
                    sb.AppendLine();
                    foreach (var t in e.Tools)
                        sb.AppendLine($"- `{t.Tool}` {(t.State == "error" ? "lỗi: " + t.Error : t.Summary ?? "")} ({t.Ms} ms)");
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Reads / writes transcripts; failures never throw (history is a convenience).</summary>
    public sealed class ChatStore
    {
        private readonly string _dir;
        private readonly object _io = new object();

        public ChatStore(string dir)
        {
            _dir = dir;
        }

        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "chats");

        public ChatTranscript Load(string key, string name)
        {
            try
            {
                var file = Path.Combine(_dir, ChatTranscript.FileNameFor(key));
                if (File.Exists(file))
                {
                    var t = ChatTranscript.FromJson(File.ReadAllText(file, Encoding.UTF8));
                    if (t.DrawingKey == key) { t.DrawingName = name; return t; }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("chat history unreadable: " + ex.Message);
            }
            return new ChatTranscript { DrawingKey = key, DrawingName = name };
        }

        public void Save(ChatTranscript t)
        {
            try
            {
                lock (_io)
                {
                    Directory.CreateDirectory(_dir);
                    t.Trim();
                    var file = Path.Combine(_dir, ChatTranscript.FileNameFor(t.DrawingKey));
                    var tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    string json;
                    lock (t) json = t.ToJson();
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
                    if (File.Exists(file)) File.Replace(tmp, file, null, true);
                    else File.Move(tmp, file);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("chat history not saved: " + ex.Message);
            }
        }

        public void Delete(string key)
        {
            try
            {
                var file = Path.Combine(_dir, ChatTranscript.FileNameFor(key));
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }
        }
    }
}
