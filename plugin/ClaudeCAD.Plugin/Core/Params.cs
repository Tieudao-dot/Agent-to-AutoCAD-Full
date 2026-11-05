using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Typed, validating accessor over the "params" object of a tool call.
    /// Every failure throws <see cref="ToolException"/> with INVALID_PARAMS and a
    /// message that names the parameter and the expected shape, so Claude can fix
    /// the call by itself.
    /// </summary>
    public sealed class Params
    {
        private readonly Dictionary<string, object> _d;
        public string Tool { get; }

        public Params(string tool, Dictionary<string, object> d)
        {
            Tool = tool ?? "";
            _d = d ?? new Dictionary<string, object>();
        }

        public Dictionary<string, object> Raw => _d;

        public bool Has(string name) => _d.TryGetValue(name, out var v) && v != null;

        public object Get(string name) => _d.TryGetValue(name, out var v) ? v : null;

        private ToolException Bad(string name, string expected, object got = null)
        {
            string gotText = got == null ? "missing" : "got " + JsonCodec.Serialize(got);
            if (gotText.Length > 120) gotText = gotText.Substring(0, 120) + "...";
            return ToolException.Invalid($"{Tool}: parameter '{name}' must be {expected} ({gotText}).");
        }

        // ---------------------------------------------------------------- scalars

        public string Str(string name)
        {
            var v = Get(name);
            if (v is string s && s.Length > 0) return s;
            throw Bad(name, "a non-empty string", v);
        }

        public string Str(string name, string def)
        {
            var v = Get(name);
            if (v == null) return def;
            if (v is string s) return s;
            throw Bad(name, "a string", v);
        }

        public double Num(string name)
        {
            var v = Get(name);
            if (v is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
            if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p)) return p;
            throw Bad(name, "a number", v);
        }

        public double Num(string name, double def) => Has(name) ? Num(name) : def;

        public double? NumOpt(string name) => Has(name) ? Num(name) : (double?)null;

        public double Positive(string name)
        {
            double d = Num(name);
            if (d <= 0) throw Bad(name, "a number > 0", d);
            return d;
        }

        public double Positive(string name, double def) => Has(name) ? Positive(name) : def;

        public int Int(string name)
        {
            double d = Num(name);
            if (Math.Abs(d - Math.Round(d)) > 1e-9) throw Bad(name, "an integer", d);
            return (int)Math.Round(d);
        }

        public int Int(string name, int def) => Has(name) ? Int(name) : def;

        public int Int(string name, int def, int min, int max)
        {
            int i = Int(name, def);
            if (i < min || i > max) throw Bad(name, $"an integer between {min} and {max}", i);
            return i;
        }

        public bool Bool(string name, bool def)
        {
            var v = Get(name);
            if (v == null) return def;
            if (v is bool b) return b;
            if (v is string s)
            {
                if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            throw Bad(name, "true or false", v);
        }

        public bool? BoolOpt(string name) => Has(name) ? Bool(name, false) : (bool?)null;

        // ---------------------------------------------------------------- points

        /// <summary>Converts a JSON value to [x,y,z]; missing z is 0 (spec §6).</summary>
        public static bool TryPoint(object v, out double[] pt)
        {
            pt = null;
            if (v is List<object> list && (list.Count == 2 || list.Count == 3))
            {
                var r = new double[3];
                for (int i = 0; i < list.Count; i++)
                {
                    if (!(list[i] is double d) || double.IsNaN(d) || double.IsInfinity(d)) return false;
                    r[i] = d;
                }
                pt = r;
                return true;
            }
            if (v is Dictionary<string, object> o && o.TryGetValue("x", out var xo) && o.TryGetValue("y", out var yo)
                && xo is double x && yo is double y)
            {
                double z = o.TryGetValue("z", out var zo) && zo is double zz ? zz : 0.0;
                pt = new[] { x, y, z };
                return true;
            }
            return false;
        }

        public double[] Point(string name)
        {
            var v = Get(name);
            if (TryPoint(v, out var pt)) return pt;
            throw Bad(name, "a point [x, y] or [x, y, z] in mm", v);
        }

        public double[] PointOpt(string name) => Has(name) ? Point(name) : null;

        public List<double[]> Points(string name, int min = 1, int max = int.MaxValue)
        {
            var v = Get(name);
            if (!(v is List<object> list)) throw Bad(name, $"an array of points [[x,y],...] (at least {min})", v);
            if (list.Count < min || list.Count > max)
                throw Bad(name, max == int.MaxValue ? $"an array with at least {min} points" : $"an array with {min}..{max} points", v);
            var r = new List<double[]>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!TryPoint(list[i], out var pt)) throw Bad($"{name}[{i}]", "a point [x, y] or [x, y, z]", list[i]);
                r.Add(pt);
            }
            return r;
        }

        public List<double> Numbers(string name, int min = 0)
        {
            var v = Get(name);
            if (!(v is List<object> list) || list.Count < min) throw Bad(name, "an array of numbers", v);
            var r = new List<double>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is double d)) throw Bad($"{name}[{i}]", "a number", list[i]);
                r.Add(d);
            }
            return r;
        }

        public List<double> NumbersOpt(string name) => Has(name) ? Numbers(name) : null;

        public List<string> Strings(string name, int min = 0)
        {
            var v = Get(name);
            if (v is string single && min <= 1) return new List<string> { single };
            if (!(v is List<object> list) || list.Count < min) throw Bad(name, min > 0 ? $"an array of at least {min} strings" : "an array of strings", v);
            var r = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is string s)) throw Bad($"{name}[{i}]", "a string", list[i]);
                r.Add(s);
            }
            return r;
        }

        public List<string> StringsOpt(string name) => Has(name) ? Strings(name) : null;

        /// <summary>Entity handles: non-empty array of hex strings, de-duplicated, order kept.</summary>
        public List<string> Handles(string name, int max = int.MaxValue)
        {
            var list = Strings(name, 1);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var r = new List<string>();
            foreach (var h in list)
            {
                var t = (h ?? "").Trim();
                if (!IsHex(t)) throw Bad(name, "an array of entity handles (hex strings like \"2F4A\")", h);
                if (seen.Add(t)) r.Add(t);
            }
            if (r.Count > max)
                throw new ToolException(ErrorCodes.TooManyEntities,
                    $"{Tool}: {r.Count} handles given, the limit is {max} per call. Split the work into several calls.");
            return r;
        }

        public string Handle(string name)
        {
            var h = Str(name).Trim();
            if (!IsHex(h)) throw Bad(name, "an entity handle (hex string like \"2F4A\")", h);
            return h;
        }

        public static bool IsHex(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 16) return false;
            foreach (char c in s)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        public Params Obj(string name)
        {
            var v = Get(name);
            if (v is Dictionary<string, object> d) return new Params(Tool, d);
            throw Bad(name, "an object", v);
        }

        public Params ObjOpt(string name) => Has(name) ? Obj(name) : null;

        public List<object> Array(string name)
        {
            var v = Get(name);
            if (v is List<object> l) return l;
            throw Bad(name, "an array", v);
        }

        /// <summary>Color: ACI 1..255 (int), or "bylayer"/"byblock". Returns null if absent.</summary>
        public object ColorOpt(string name = "color")
        {
            var v = Get(name);
            if (v == null) return null;
            if (v is double d)
            {
                int aci = (int)Math.Round(d);
                if (Math.Abs(d - aci) < 1e-9 && aci >= 0 && aci <= 256) return aci;
            }
            if (v is string s)
            {
                var t = s.Trim().ToLowerInvariant();
                if (t == "bylayer" || t == "byblock") return t;
                if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 && n <= 256) return n;
            }
            throw Bad(name, "an ACI color index 1-255 or \"bylayer\"/\"byblock\"", v);
        }

        public IEnumerable<string> Keys => _d.Keys;

        public override string ToString() => JsonCodec.Serialize(_d);

        public static Params Empty(string tool) => new Params(tool, new Dictionary<string, object>());

        public static string Truncate(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + $"...(+{s.Length - max} chars)";
        }

        public static List<object> ToJsonPoint(double[] p)
        {
            if (p == null) return null;
            return p.Length >= 3 && Math.Abs(p[2]) > 1e-12
                ? new List<object> { p[0], p[1], p[2] }
                : new List<object> { p[0], p[1] };
        }

        public static List<object> ToJsonList<T>(IEnumerable<T> items) => items.Cast<object>().ToList();
    }
}
