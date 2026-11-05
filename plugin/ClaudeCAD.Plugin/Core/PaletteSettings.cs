using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>Per-user palette settings and input history (%LocalAppData%\ClaudeCAD\settings.json).</summary>
    public sealed class PaletteSettings
    {
        public const int MaxHistory = 100;

        public string Standard = "";        // "" = automatic, else skill name (feature 9)
        public string Scale = "";           // "" = automatic, else "1:100"
        public string Model = "";           // "" = sidecar default (feature 14)
        public string Effort = "";          // "" = model default
        public double FontSize = 13;        // feature 19
        public string Theme = "auto";       // auto | dark | light
        public bool Notify = true;          // feature 15
        public List<string> History = new List<string>(); // feature 10, newest last

        public static readonly string[] Standards = { "", "ve-kien-truc", "ve-mep", "ve-ha-tang" };
        public static readonly string[] Scales = { "", "1:20", "1:50", "1:100", "1:200", "1:500", "1:1000" };
        public static readonly string[] Models = { "", "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5" };
        public static readonly string[] Efforts = { "", "low", "medium", "high", "xhigh" };

        public static string StandardLabel(string s)
        {
            switch (s)
            {
                case "ve-kien-truc": return "Kiến trúc";
                case "ve-mep": return "Điện nước";
                case "ve-ha-tang": return "Hạ tầng";
                default: return "Tự động";
            }
        }

        public static string ModelLabel(string m)
        {
            switch (m)
            {
                case "claude-opus-5-5": return "Opus 5.5";
                case "claude-sonnet-5-5": return "Sonnet 5.5";
                case "claude-haiku-5-5": return "Haiku 5.5";
                default: return "Mặc định";
            }
        }

        public void AddHistory(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            History.RemoveAll(h => h == text);
            History.Add(text);
            if (History.Count > MaxHistory) History.RemoveRange(0, History.Count - MaxHistory);
        }

        public double ClampFont(double v) => Math.Max(10, Math.Min(22, v));

        /// <summary>Independent copy (the palette edits its own copy; the controller compares old vs new).</summary>
        public PaletteSettings Clone() => FromJson(ToJson());

        public string ToJson() => JsonCodec.Serialize(new Dictionary<string, object>
        {
            ["standard"] = Standard, ["scale"] = Scale, ["model"] = Model, ["effort"] = Effort,
            ["font_size"] = FontSize, ["theme"] = Theme, ["notify"] = Notify,
            ["history"] = History.Cast<object>().ToList(),
        });

        public static PaletteSettings FromJson(string json)
        {
            var d = JsonCodec.ParseObject(json);
            var s = new PaletteSettings();
            string Str(string k, string def) => d.TryGetValue(k, out var v) && v is string x ? x : def;
            s.Standard = Str("standard", "");
            if (!Standards.Contains(s.Standard)) s.Standard = "";
            s.Scale = Str("scale", "");
            s.Model = Str("model", "");
            s.Effort = Str("effort", "");
            if (!Efforts.Contains(s.Effort)) s.Effort = "";
            s.Theme = Str("theme", "auto");
            if (d.TryGetValue("font_size", out var fs) && fs is double f) s.FontSize = s.ClampFont(f);
            if (d.TryGetValue("notify", out var n) && n is bool b) s.Notify = b;
            if (d.TryGetValue("history", out var h) && h is List<object> hl) s.History = hl.OfType<string>().Take(MaxHistory).ToList();
            return s;
        }

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "settings.json");

        public static PaletteSettings Load(string path = null)
        {
            path = path ?? DefaultPath;
            try
            {
                if (File.Exists(path)) return FromJson(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Logger.Warn("settings unreadable, using defaults: " + ex.Message);
            }
            return new PaletteSettings();
        }

        public void Save(string path = null)
        {
            path = path ?? DefaultPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Warn("settings not saved: " + ex.Message);
            }
        }
    }

    /// <summary>Up/Down recall of sent messages (feature 10).</summary>
    public sealed class InputHistoryCursor
    {
        private readonly List<string> _items;
        private int _index;
        private string _draft = "";

        public InputHistoryCursor(List<string> items)
        {
            _items = items;
            _index = items.Count;
        }

        public void Reset() => _index = _items.Count;

        /// <summary>Previous entry; the first call remembers the unsent draft.</summary>
        public string Previous(string current)
        {
            if (_items.Count == 0) return null;
            if (_index == _items.Count) _draft = current ?? "";
            if (_index == 0) return _items[0];
            _index--;
            return _items[_index];
        }

        public string Next()
        {
            if (_index >= _items.Count) return null;
            _index++;
            return _index == _items.Count ? _draft : _items[_index];
        }
    }
}
