using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Small dependency-free JSON reader/writer.
    ///
    /// We deliberately avoid Newtonsoft.Json: AutoCAD 2022 ships its own copy and
    /// other plugins load theirs, so a private dependency is a classic source of
    /// version conflicts inside acad.exe. The object model is plain .NET types:
    ///   object  -> Dictionary&lt;string, object&gt;
    ///   array   -> List&lt;object&gt;
    ///   string  -> string
    ///   number  -> double
    ///   bool    -> bool
    ///   null    -> null
    /// </summary>
    public static class JsonCodec
    {
        // ------------------------------------------------------------------ parse

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue(0);
            p.SkipWs();
            if (!p.End) throw p.Error("Unexpected trailing characters");
            return v;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            var v = Parse(text) as Dictionary<string, object>;
            if (v == null) throw new FormatException("JSON value is not an object");
            return v;
        }

        private sealed class Parser
        {
            private const int MaxDepth = 128;
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }
            public bool End => _i >= _s.Length;

            public FormatException Error(string msg) =>
                new FormatException(msg + " at position " + _i.ToString(CultureInfo.InvariantCulture));

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') _i++;
                    else break;
                }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("JSON nested too deeply");
                if (End) throw Error("Unexpected end of JSON");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("Expected '" + word + "'");
                _i += word.Length;
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; // {
                SkipWs();
                if (!End && _s[_i] == '}') { _i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (End || _s[_i] != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWs();
                    if (End || _s[_i] != ':') throw Error("Expected ':'");
                    _i++;
                    SkipWs();
                    d[key] = ReadValue(depth + 1);
                    SkipWs();
                    if (End) throw Error("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return d; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                var list = new List<object>();
                _i++; // [
                SkipWs();
                if (!End && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue(depth + 1));
                    SkipWs();
                    if (End) throw Error("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("Unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (!End && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E' || _s[_i] == '+' || _s[_i] == '-')) _i++;
                var token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    throw Error("Bad number '" + token + "'");
                return d;
            }
        }

        // ------------------------------------------------------------------ write

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v, int depth)
        {
            if (depth > 128) throw new InvalidOperationException("Object graph too deep to serialize");
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: WriteDouble(sb, d); return;
                case float f: WriteDouble(sb, f); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case short sh: sb.Append(sh.ToString(CultureInfo.InvariantCulture)); return;
                case byte by: sb.Append(by.ToString(CultureInfo.InvariantCulture)); return;
                case uint ui: sb.Append(ui.ToString(CultureInfo.InvariantCulture)); return;
                case ulong ul: sb.Append(ul.ToString(CultureInfo.InvariantCulture)); return;
                case char ch: WriteString(sb, ch.ToString()); return;
                case Enum en: WriteString(sb, en.ToString()); return;
                case IDictionary<string, object> dict:
                    {
                        sb.Append('{');
                        bool first = true;
                        foreach (var kv in dict)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteString(sb, kv.Key);
                            sb.Append(':');
                            Write(sb, kv.Value, depth + 1);
                        }
                        sb.Append('}');
                        return;
                    }
                case IDictionary legacy:
                    {
                        sb.Append('{');
                        bool first = true;
                        foreach (DictionaryEntry kv in legacy)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteString(sb, Convert.ToString(kv.Key, CultureInfo.InvariantCulture));
                            sb.Append(':');
                            Write(sb, kv.Value, depth + 1);
                        }
                        sb.Append('}');
                        return;
                    }
                case IEnumerable seq:
                    {
                        sb.Append('[');
                        bool first = true;
                        foreach (var item in seq)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Write(sb, item, depth + 1);
                        }
                        sb.Append(']');
                        return;
                    }
                default:
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                    return;
            }
        }

        private static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            // Round to 1e-9 to keep payloads short and stable (mm precision is plenty).
            double r = Math.Round(d, 9);
            if (r == Math.Floor(r) && Math.Abs(r) < 1e15) sb.Append(((long)r).ToString(CultureInfo.InvariantCulture));
            else sb.Append(r.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // U+2028/2029 are valid JSON but break some line-based readers.
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
