using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Filter shared by list_entities / count_entities (and erase/set props by filter later).</summary>
    public sealed class EntityFilter
    {
        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["POLYLINE"] = new[] { "LWPOLYLINE", "POLYLINE2D", "POLYLINE3D" },
            ["PLINE"] = new[] { "LWPOLYLINE", "POLYLINE2D", "POLYLINE3D" },
            ["DIM"] = new[] { "DIMENSION" },
            ["BLOCK"] = new[] { "INSERT" },
            ["BLOCKREFERENCE"] = new[] { "INSERT" },
            ["MLEADER"] = new[] { "MULTILEADER" },
            ["DBTEXT"] = new[] { "TEXT" },
            ["ANYTEXT"] = new[] { "TEXT", "MTEXT" },
        };

        public HashSet<string> Types;
        public List<string> Layers;
        public double[] WinMin, WinMax;
        public bool Crossing;
        public int? Color;

        public static EntityFilter From(Params p)
        {
            var f = new EntityFilter();
            var types = p.StringsOpt("types");
            if (types != null && types.Count > 0)
            {
                f.Types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    if (Aliases.TryGetValue(t.Trim(), out var mapped)) foreach (var m in mapped) f.Types.Add(m);
                    else f.Types.Add(t.Trim().ToUpperInvariant());
                }
            }
            var layers = p.StringsOpt("layers");
            if (layers != null && layers.Count > 0) f.Layers = layers;
            var win = p.ObjOpt("window");
            if (win != null)
            {
                var a = win.Point("p1");
                var b = win.Point("p2");
                f.WinMin = new[] { Math.Min(a[0], b[0]), Math.Min(a[1], b[1]) };
                f.WinMax = new[] { Math.Max(a[0], b[0]), Math.Max(a[1], b[1]) };
                f.Crossing = win.Bool("crossing", false);
            }
            var c = p.ColorOpt("color");
            if (c is int ci) f.Color = ci;
            return f;
        }

        public bool Match(ToolContext ctx, Entity e)
        {
            if (Types != null && !Types.Contains(Acad.TypeName(e))) return false;
            if (Layers != null && !Layers.Any(l => DrawingService.WildcardMatch(e.Layer, l))) return false;
            if (Color.HasValue && EntityInfo.EffectiveAci(ctx, e) != Color.Value) return false;
            if (WinMin != null)
            {
                var ext = EntityInfo.Extents(e);
                if (!ext.HasValue) return false;
                var mn = ext.Value.MinPoint;
                var mx = ext.Value.MaxPoint;
                bool inside = mn.X >= WinMin[0] && mn.Y >= WinMin[1] && mx.X <= WinMax[0] && mx.Y <= WinMax[1];
                if (Crossing)
                {
                    bool overlaps = !(mx.X < WinMin[0] || mn.X > WinMax[0] || mx.Y < WinMin[1] || mn.Y > WinMax[1]);
                    if (!overlaps) return false;
                }
                else if (!inside) return false;
            }
            return true;
        }

        public IEnumerable<Entity> Apply(ToolContext ctx)
        {
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.IsErased) continue;
                if (ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e && Match(ctx, e)) yield return e;
            }
        }
    }

    /// <summary>Spec §7.6 — read and query.</summary>
    public static partial class DrawingService
    {
        public const int DefaultListLimit = 200;
        public const int MaxListLimit = 2000;

        private static void RegisterQuery(ToolDispatcher d)
        {
            d.Register("list_entities", false, ListEntities);
            d.Register("count_entities", false, CountEntities);
            d.Register("get_entity", false, GetEntity);
            d.Register("find_text", false, FindText);
            d.Register("measure_distance", false, MeasureDistance);
            d.Register("measure_area", false, MeasureArea);
            d.Register("get_bounding_box", false, GetBoundingBox);
        }

        private static object ListEntities(ToolContext ctx, Params p)
        {
            var f = EntityFilter.From(p);
            int limit = p.Int("limit", DefaultListLimit, 1, MaxListLimit);
            int offset = p.Int("offset", 0, 0, int.MaxValue);
            var items = new List<object>();
            int total = 0;
            foreach (var e in f.Apply(ctx))
            {
                if (total >= offset && items.Count < limit) items.Add(EntityInfo.Summary(ctx, e));
                total++;
            }
            var r = new Dictionary<string, object>
            {
                ["total"] = total,
                ["returned"] = items.Count,
                ["offset"] = offset,
                ["entities"] = items,
            };
            if (offset + items.Count < total)
                r["note"] = $"Only {items.Count} of {total} returned. Use offset={offset + items.Count} for the next page, or narrow the filter.";
            return r;
        }

        private static object CountEntities(ToolContext ctx, Params p)
        {
            var f = EntityFilter.From(p);
            var byType = new Dictionary<string, int>();
            var byLayer = new Dictionary<string, int>();
            int total = 0;
            foreach (var e in f.Apply(ctx))
            {
                total++;
                var t = Acad.TypeName(e);
                byType[t] = byType.TryGetValue(t, out var n) ? n + 1 : 1;
                byLayer[e.Layer] = byLayer.TryGetValue(e.Layer, out var m) ? m + 1 : 1;
            }
            return new Dictionary<string, object>
            {
                ["total"] = total,
                ["by_type"] = byType.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["by_layer"] = byLayer.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            };
        }

        private static object GetEntity(ToolContext ctx, Params p)
        {
            if (p.Has("handles"))
            {
                var hs = p.Handles("handles", 100);
                return new Dictionary<string, object>
                {
                    ["entities"] = hs.Select(h => (object)EntityInfo.Detail(ctx, Acad.GetEntity(ctx, h, false))).ToList(),
                };
            }
            return EntityInfo.Detail(ctx, Acad.GetEntity(ctx, p.Handle("handle"), false));
        }

        private static object FindText(ToolContext ctx, Params p)
        {
            string query = p.Str("query");
            bool matchCase = p.Bool("match_case", false);
            bool wholeWord = p.Bool("whole_word", false);
            int limit = p.Int("limit", 500, 1, 5000);
            var layers = p.StringsOpt("layers");
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var hits = new List<object>();
            int total = 0;

            bool Matches(string s)
            {
                if (string.IsNullOrEmpty(s)) return false;
                int i = s.IndexOf(query, cmp);
                if (!wholeWord) return i >= 0;
                while (i >= 0)
                {
                    bool leftOk = i == 0 || !char.IsLetterOrDigit(s[i - 1]);
                    bool rightOk = i + query.Length >= s.Length || !char.IsLetterOrDigit(s[i + query.Length]);
                    if (leftOk && rightOk) return true;
                    i = s.IndexOf(query, i + 1, cmp);
                }
                return false;
            }

            void Hit(Entity e, string type, string text, Point3d pos, string owner = null, string tag = null)
            {
                total++;
                if (hits.Count >= limit) return;
                var d = new Dictionary<string, object>
                {
                    ["handle"] = e.Handle.ToString(),
                    ["type"] = type,
                    ["text"] = text,
                    ["layer"] = e.Layer,
                    ["position"] = Acad.J(pos),
                };
                if (owner != null) d["block_handle"] = owner;
                if (tag != null) d["tag"] = tag;
                hits.Add(d);
            }

            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                if (layers != null && !layers.Any(l => WildcardMatch(e.Layer, l))) continue;
                switch (e)
                {
                    case DBText t when Matches(t.TextString):
                        Hit(t, "TEXT", t.TextString, t.Position);
                        break;
                    case MText mt when Matches(mt.Text):
                        Hit(mt, "MTEXT", mt.Text, mt.Location);
                        break;
                    case MLeader ml when ml.ContentType == ContentType.MTextContent && ml.MText != null && Matches(ml.MText.Text):
                        Hit(ml, "MULTILEADER", ml.MText.Text, ml.MText.Location);
                        break;
                    case Dimension dim when Matches(dim.DimensionText):
                        Hit(dim, "DIMENSION", dim.DimensionText, dim.TextPosition);
                        break;
                    case Table tb:
                        for (int r = 0; r < tb.Rows.Count; r++)
                            for (int c = 0; c < tb.Columns.Count; c++)
                            {
                                string s = SafeCellText(tb, r, c);
                                if (Matches(s))
                                {
                                    Hit(tb, "TABLE_CELL", s, tb.Position);
                                    if (hits.Count > 0 && hits[hits.Count - 1] is Dictionary<string, object> last && (string)last["handle"] == tb.Handle.ToString())
                                    {
                                        last["row"] = r;
                                        last["col"] = c;
                                    }
                                }
                            }
                        break;
                    case BlockReference br:
                        foreach (ObjectId aid in br.AttributeCollection)
                        {
                            if (!(ctx.Tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)) continue;
                            string s = ar.IsMTextAttribute ? ar.MTextAttribute.Text : ar.TextString;
                            if (Matches(s)) Hit(ar, "ATTRIBUTE", s, ar.Position, br.Handle.ToString(), ar.Tag);
                        }
                        break;
                }
            }
            var res = new Dictionary<string, object> { ["total"] = total, ["matches"] = hits };
            if (total > hits.Count) res["note"] = $"Only the first {hits.Count} of {total} matches returned; narrow with 'layers'.";
            return res;
        }

        internal static string SafeCellText(Table tb, int r, int c)
        {
            try { return tb.Cells[r, c].TextString; } catch { return null; }
        }

        private static object MeasureDistance(ToolContext ctx, Params p)
        {
            var a = p.Point("p1");
            var b = p.Point("p2");
            double dx = b[0] - a[0], dy = b[1] - a[1], dz = b[2] - a[2];
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return new Dictionary<string, object>
            {
                ["distance"] = Acad.R(dist),
                ["distance_m"] = Acad.R(dist / 1000),
                ["dx"] = Acad.R(dx),
                ["dy"] = Acad.R(dy),
                ["dz"] = Acad.R(dz),
                ["angle"] = Acad.Deg(Geometry.NormalizeRad(Math.Atan2(dy, dx))),
            };
        }

        private static object MeasureArea(ToolContext ctx, Params p)
        {
            double area, perimeter;
            string source;
            if (p.Has("handle"))
            {
                var h = p.Handle("handle");
                var e = Acad.GetEntity(ctx, h, false);
                switch (e)
                {
                    case Hatch hatch:
                        area = hatch.Area;
                        perimeter = double.NaN;
                        source = "HATCH";
                        break;
                    case Region region:
                        area = region.Area;
                        perimeter = region.Perimeter;
                        source = "REGION";
                        break;
                    case Curve c:
                        if (!c.Closed && !(c is Circle) && !(c is Ellipse && ((Ellipse)c).Closed))
                            throw ToolException.Invalid($"measure_area: entity {h} is an open {Acad.TypeName(e)}; area needs a closed curve (or pass 'points').");
                        area = c.Area;
                        perimeter = c.GetDistanceAtParameter(c.EndParam);
                        source = Acad.TypeName(e);
                        break;
                    default:
                        throw ToolException.Invalid($"measure_area: {Acad.TypeName(e)} {h} has no area. Use a closed polyline, circle, ellipse, hatch or region.");
                }
            }
            else
            {
                var pts = p.Points("points", 3);
                area = Math.Abs(Geometry.SignedArea(pts));
                perimeter = Geometry.Perimeter(pts);
                source = "points";
            }
            return new Dictionary<string, object>
            {
                ["source"] = source,
                ["area_mm2"] = Acad.R(area),
                ["area_m2"] = Math.Round(area / 1e6, 4),
                ["perimeter_mm"] = double.IsNaN(perimeter) ? null : (object)Acad.R(perimeter),
                ["perimeter_m"] = double.IsNaN(perimeter) ? null : (object)Math.Round(perimeter / 1000, 4),
            };
        }

        private static object GetBoundingBox(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", 10000);
            Extents3d? all = null;
            var each = new List<object>();
            foreach (var h in hs)
            {
                var e = Acad.GetEntity(ctx, h, false);
                var ext = EntityInfo.Extents(e);
                if (!ext.HasValue)
                {
                    each.Add(new Dictionary<string, object> { ["handle"] = h, ["bbox"] = null });
                    continue;
                }
                if (all.HasValue) { var x = all.Value; x.AddExtents(ext.Value); all = x; } else all = ext;
                if (hs.Count <= 50)
                    each.Add(new Dictionary<string, object> { ["handle"] = h, ["min"] = Acad.J(ext.Value.MinPoint), ["max"] = Acad.J(ext.Value.MaxPoint) });
            }
            if (!all.HasValue) throw ToolException.Invalid("get_bounding_box: none of the entities has finite extents (xlines/rays).");
            var mn = all.Value.MinPoint;
            var mx = all.Value.MaxPoint;
            var r = new Dictionary<string, object>
            {
                ["min"] = Acad.J(mn),
                ["max"] = Acad.J(mx),
                ["width"] = Acad.R(mx.X - mn.X),
                ["height"] = Acad.R(mx.Y - mn.Y),
                ["center"] = Acad.J(new Point3d((mn.X + mx.X) / 2, (mn.Y + mx.Y) / 2, (mn.Z + mx.Z) / 2)),
            };
            if (each.Count > 0) r["entities"] = each;
            return r;
        }
    }
}
