using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c.md): 2D utilities, all in the 2D group. dim_floor_plan (chains of
    /// dimensions around a plan), block_schedule / area_schedule (counts and areas, optionally as a table), find_replace_text
    /// (preview, then replace only what the user approved on the card). draw_hatch pick_point is in DrawTools.cs, plot_pdf
    /// layouts in LayoutTools.cs. The logic is in Core/Util2D.cs (Util2DLogic), unit-tested without AutoCAD. Every write
    /// runs in the turn's transaction and undo group, so one U removes the whole turn.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterUtil2D(ToolDispatcher d)
        {
            d.Register("dim_floor_plan", true, DimFloorPlan);
            // the schedules only read unless they insert a table; find_replace_text only reads without handles (preview)
            d.Register(new ToolDef("block_schedule", true, BlockSchedule, readsWhen: p => !p.Has("table_position")));
            d.Register(new ToolDef("area_schedule", true, AreaSchedule, readsWhen: p => !p.Has("table_position")));
            d.Register(new ToolDef("find_replace_text", true, FindReplaceText, readsWhen: p => !p.Has("handles")));
        }

        private const int MaxPlanDims = 600;

        // ------------------------------------------------------------------ dim_floor_plan

        /// <summary>Straight pieces of lines / polylines / arcs (chords) of the given entities.</summary>
        private static List<Util2DLogic.Seg> Segments(ToolContext ctx, IEnumerable<Entity> ents)
        {
            var segs = new List<Util2DLogic.Seg>();
            void Add(Point3d a, Point3d b) { if (a.DistanceTo(b) > Geometry.Eps) segs.Add(new Util2DLogic.Seg(a.X, a.Y, b.X, b.Y)); }
            foreach (var e in ents)
            {
                switch (e)
                {
                    case Line l: Add(l.StartPoint, l.EndPoint); break;
                    case Arc a: Add(a.StartPoint, a.EndPoint); break;
                    case Polyline pl:
                        int n = pl.Closed ? pl.NumberOfVertices : pl.NumberOfVertices - 1;
                        for (int i = 0; i < n; i++) Add(pl.GetPoint3dAt(i), pl.GetPoint3dAt((i + 1) % pl.NumberOfVertices));
                        break;
                }
            }
            return segs;
        }

        /// <summary>Entities by handles, or on layers (wildcards) in model space; frozen / off layers are skipped.</summary>
        private static List<Entity> Pick(ToolContext ctx, Params p, string handlesKey, string layersKey, IList<string> defaultLayers)
        {
            if (p.Has(handlesKey)) return p.Handles(handlesKey, 20000).Select(h => Acad.GetEntity(ctx, h, false)).ToList();
            var layers = p.StringsOpt(layersKey) ?? defaultLayers;
            if (layers == null || layers.Count == 0) return new List<Entity>();
            var outList = new List<Entity>();
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.IsErased || !(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                if (!layers.Any(l => WildcardMatch(e.Layer, l))) continue;
                var ltr = (LayerTableRecord)ctx.Tr.GetObject(e.LayerId, OpenMode.ForRead);
                if (ltr.IsFrozen || ltr.IsOff) continue;
                outList.Add(e);
            }
            return outList;
        }

        private static object DimFloorPlan(ToolContext ctx, Params p)
        {
            var walls = Segments(ctx, Pick(ctx, p, "wall_handles", "wall_layers", new[] { "A-WALL" }));
            if (walls.Count == 0)
                throw ToolException.Invalid("dim_floor_plan: no wall lines / polylines found (default layer A-WALL). Pass wall_layers (e.g. [\"TUONG\"]) or wall_handles; list_layers shows the layers.");
            var axes = Segments(ctx, Pick(ctx, p, "axis_handles", "axis_layers", null));
            var sides = p.StringsOpt("sides") ?? Util2DLogic.AllSides.ToList();
            double size = Math.Max(walls.Max(s => Math.Max(s.X1, s.X2)) - walls.Min(s => Math.Min(s.X1, s.X2)), walls.Max(s => Math.Max(s.Y1, s.Y2)) - walls.Min(s => Math.Min(s.Y1, s.Y2)));
            double offset = p.Positive("offset", Util2DLogic.DefaultDimOffset(ctx.Db.Dimscale, size));
            double spacing = p.Positive("spacing", offset);
            // NC-5: in a plan drawn in metres 1 unit is a whole metre; the tolerance follows the plan size
            var specs = Util2DLogic.FloorPlanDims(walls, axes, sides, offset, spacing, Util2DLogic.PieceTolerance(size));
            if (specs.Count > MaxPlanDims)
                throw ToolException.Invalid($"dim_floor_plan: {specs.Count} dimensions (more than {MaxPlanDims}). Dimension fewer sides, or pass wall_handles of the outer walls only.");

            var ids = new List<ObjectId>();
            var perSide = new Dictionary<string, object>();
            foreach (var s in specs)
            {
                var dim = new RotatedDimension(s.Vertical ? Math.PI / 2 : 0, Acad.P3(s.P1), Acad.P3(s.P2), Acad.P3(s.DimLine), "", ObjectId.Null);
                ids.Add(AddDim(ctx, dim, p));
                var key = s.Side + "_" + s.Row;
                perSide[key] = perSide.TryGetValue(key, out var n) ? (int)n + 1 : 1;
            }
            var res = Acad.Handles(ids);
            res["count"] = ids.Count;
            res["by_side_row"] = perSide;
            res["offset"] = Acad.R(offset);
            res["spacing"] = Acad.R(spacing);
            res["dim_style"] = EntityInfo.DimStyleName(ctx, p.Has("dim_style") ? DimStyleId(ctx, p.Str("dim_style")) : ctx.Db.Dimstyle);
            if (axes.Count == 0) res["note"] = "No axis lines given (axis_layers / axis_handles): rows are wall corners / openings and the overall size.";
            return res;
        }

        // ------------------------------------------------------------------ schedules (tables)

        private static double TableTextHeight(ToolContext ctx, Params p) =>
            p.Positive("text_height", Math.Max(ctx.Db.Dimscale > 0 ? ctx.Db.Dimscale : 1, 1) * 2.5);

        /// <summary>Inserts the schedule as a TABLE through add_table (same table style, same code path).</summary>
        private static Dictionary<string, object> InsertSchedule(ToolContext ctx, Params p, List<List<object>> data, double[] widthsInText, string defaultTitle)
        {
            double th = TableTextHeight(ctx, p);
            var d = new Dictionary<string, object>
            {
                ["position"] = Params.ToJsonPoint(p.Point("table_position")),
                ["data"] = data.Select(r => (object)r).ToList(),
                ["title"] = p.Str("title", defaultTitle),
                ["text_height"] = th,
                ["col_widths"] = widthsInText.Select(w => (object)(w * th)).ToList(),
            };
            foreach (var k in new[] { "layer", "color", "layout" }) if (p.Has(k)) d[k] = p.Get(k);
            return (Dictionary<string, object>)AddTable(ctx, new Params("add_table", d));
        }

        private static IEnumerable<Entity> SpaceEntities(ToolContext ctx, Params p)
        {
            foreach (ObjectId id in ctx.SpaceFor(p.Str("layout", null)))
            {
                if (id.IsErased) continue;
                if (ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e) yield return e;
            }
        }

        private static object BlockSchedule(ToolContext ctx, Params p)
        {
            var names = p.StringsOpt("names");
            var layers = p.StringsOpt("layers");
            string byAttr = p.Str("group_by_attribute", null);
            var items = new List<Util2DLogic.BlockItem>();
            var constants = new Dictionary<ObjectId, Dictionary<string, string>>();
            foreach (var e in SpaceEntities(ctx, p))
            {
                if (!(e is BlockReference br) || e is Table) continue;
                if (layers != null && !layers.Any(l => WildcardMatch(e.Layer, l))) continue;
                var btr = (BlockTableRecord)ctx.Tr.GetObject(br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord, OpenMode.ForRead);
                if (btr.IsFromExternalReference || btr.IsLayout || btr.Name.StartsWith("*", StringComparison.Ordinal)) continue;
                if (names != null && !names.Any(n => WildcardMatch(btr.Name, n))) continue;
                var it = new Util2DLogic.BlockItem { Name = btr.Name, Layer = e.Layer };
                if (!string.IsNullOrWhiteSpace(byAttr))
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        if (!(ctx.Tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)) continue;
                        string s;
                        if (ar.IsMTextAttribute) { using (var m = ar.MTextAttribute) s = m.Text; }
                        else s = ar.TextString;
                        it.Attributes[ar.Tag] = s;
                    }
                // NC-2: a constant attribute lives in the block definition, not in the reference's attribute collection
                if (!string.IsNullOrWhiteSpace(byAttr))
                    foreach (var kv in ConstantAttributes(ctx, br.BlockTableRecord, constants))
                        if (!it.Attributes.ContainsKey(kv.Key)) it.Attributes[kv.Key] = kv.Value;
                items.Add(it);
            }
            var rows = Util2DLogic.CountBlocks(items, byAttr);
            var res = new Dictionary<string, object>
            {
                ["total"] = items.Count,
                ["rows"] = rows.Select(r =>
                {
                    var d = new Dictionary<string, object> { ["name"] = r.Name, ["count"] = r.Count };
                    if (!string.IsNullOrWhiteSpace(byAttr)) d["value"] = r.Value;
                    return (object)d;
                }).ToList(),
            };
            if (!string.IsNullOrWhiteSpace(byAttr) && rows.All(r => string.IsNullOrEmpty(r.Value)) && rows.Count > 0)
                res["note"] = $"No block has an attribute '{byAttr}' (or it is empty). get_block_attributes on one block shows its tags.";
            if (p.Has("table_position"))
            {
                if (rows.Count == 0) throw ToolException.Invalid("block_schedule: no blocks matched, so no table was inserted.");
                var widths = string.IsNullOrWhiteSpace(byAttr) ? new[] { 5.0, 24, 9 } : new[] { 5.0, 24, 14, 9 };
                var t = InsertSchedule(ctx, p, Util2DLogic.BlockTable(rows, byAttr), widths, "THỐNG KÊ BLOCK");
                res["handles"] = t["handles"];
            }
            return res;
        }

        /// <summary>Tag → value of the constant attribute definitions of a block (cached per definition).</summary>
        private static Dictionary<string, string> ConstantAttributes(ToolContext ctx, ObjectId btrId, Dictionary<ObjectId, Dictionary<string, string>> cache)
        {
            if (cache.TryGetValue(btrId, out var d)) return d;
            d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var btr = (BlockTableRecord)ctx.Tr.GetObject(btrId, OpenMode.ForRead);
            if (btr.HasAttributeDefinitions)
                foreach (ObjectId id in btr)
                    if (ctx.Tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition ad && ad.Constant && !d.ContainsKey(ad.Tag))
                        d[ad.Tag] = ad.TextString;
            cache[btrId] = d;
            return d;
        }

        /// <summary>Points around a closed curve: polyline vertices (arc segments sampled), others sampled by length.</summary>
        private static List<double[]> Outline(Curve c)
        {
            var pts = new List<double[]>();
            try
            {
                if (c is Polyline pl && Enumerable.Range(0, pl.NumberOfVertices).All(i => Math.Abs(pl.GetBulgeAt(i)) < 1e-12))
                {
                    int nv = pl.NumberOfVertices;
                    // closed by its ends (NC-6): the repeated first point would shift the centre used to find twins
                    if (nv > 3 && !pl.Closed && pl.GetPoint3dAt(0).DistanceTo(pl.GetPoint3dAt(nv - 1)) < 1e-6) nv--;
                    for (int i = 0; i < nv; i++) { var q = pl.GetPoint3dAt(i); pts.Add(new[] { q.X, q.Y }); }
                    return pts;
                }
                double len = c.GetDistanceAtParameter(c.EndParam);
                const int n = 96;
                for (int i = 0; i < n; i++) { var q = c.GetPointAtDist(len * i / n); pts.Add(new[] { q.X, q.Y }); }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }
            return pts;
        }

        private static bool ClosedByEnds(Curve c) =>
            c is Polyline pl && !pl.Closed && pl.NumberOfVertices > 3 && pl.GetPoint3dAt(0).DistanceTo(pl.GetPoint3dAt(pl.NumberOfVertices - 1)) < 1e-6;

        private static List<double[]> HatchOutline(Hatch h)
        {
            try
            {
                int best = 0;
                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    var t = h.LoopTypeAt(i);
                    if ((t & HatchLoopTypes.External) != 0 || (t & HatchLoopTypes.Outermost) != 0) { best = i; break; }
                }
                var loop = h.GetLoopAt(best);
                var pts = new List<double[]>();
                if (loop.IsPolyline)
                {
                    foreach (BulgeVertex v in loop.Polyline) pts.Add(new[] { v.Vertex.X, v.Vertex.Y });
                }
                else
                {
                    foreach (Curve2d c in loop.Curves)
                    {
                        var iv = c.GetInterval();
                        for (int k = 0; k < 8; k++) { var q = c.EvaluatePoint(iv.LowerBound + (iv.UpperBound - iv.LowerBound) * k / 8); pts.Add(new[] { q.X, q.Y }); }
                    }
                }
                return pts.Count >= 3 ? pts : null;
            }
            catch (Exception) { return null; }
        }

        private static List<double[]> BoxOutline(Entity e)
        {
            var ext = EntityInfo.Extents(e);
            if (!ext.HasValue) return null;
            var a = ext.Value.MinPoint;
            var b = ext.Value.MaxPoint;
            return new List<double[]> { new[] { a.X, a.Y }, new[] { b.X, a.Y }, new[] { b.X, b.Y }, new[] { a.X, b.Y } };
        }

        private static object AreaSchedule(ToolContext ctx, Params p)
        {
            string groupBy = p.Str("group_by", "layer").Trim().ToLowerInvariant();
            if (groupBy != "layer" && groupBy != "room") throw ToolException.Invalid("area_schedule: group_by must be layer or room.");
            var layers = p.StringsOpt("layers");
            double minArea = p.Num("min_area", 0) * 1e6; // m² → mm²
            int decimals = p.Int("decimals", 2, 0, 4);
            var handles = p.Has("handles") ? new HashSet<string>(p.Handles("handles", 20000), StringComparer.OrdinalIgnoreCase) : null;
            var curves = new List<Util2DLogic.AreaItem>();
            var fills = new List<Util2DLogic.AreaItem>();
            var skipped = new List<string>();
            var texts = new List<Util2DLogic.TextItem>();
            var textLayers = p.StringsOpt("text_layers");
            foreach (var e in SpaceEntities(ctx, p))
            {
                if (groupBy == "room" && (e is DBText || e is MText) && (textLayers == null || textLayers.Any(l => WildcardMatch(e.Layer, l))))
                {
                    var ext = EntityInfo.Extents(e);
                    var pos = ext.HasValue
                        ? new[] { (ext.Value.MinPoint.X + ext.Value.MaxPoint.X) / 2, (ext.Value.MinPoint.Y + ext.Value.MaxPoint.Y) / 2 }
                        : e is DBText t0 ? new[] { t0.Position.X, t0.Position.Y } : new[] { ((MText)e).Location.X, ((MText)e).Location.Y };
                    texts.Add(new Util2DLogic.TextItem { Text = e is DBText t1 ? t1.TextString : ((MText)e).Text, Position = pos });
                }
                if (handles != null ? !handles.Contains(e.Handle.ToString()) : layers != null && !layers.Any(l => WildcardMatch(e.Layer, l))) continue;
                Util2DLogic.AreaItem it = null;
                string h = e.Handle.ToString();
                switch (e)
                {
                    case Hatch hatch:
                        it = new Util2DLogic.AreaItem { Handle = h, Type = "HATCH", Layer = e.Layer, Area = EntityInfo.SafeArea(hatch), Outline = HatchOutline(hatch) ?? BoxOutline(e) };
                        break;
                    case Region region:
                        it = new Util2DLogic.AreaItem { Handle = h, Type = "REGION", Layer = e.Layer, Area = region.Area, Outline = BoxOutline(e) };
                        break;
                    // NC-6: a polyline whose last vertex sits on the first one is closed too (drawn without the Closed flag)
                    case Curve c when c.Closed || c is Circle || (c is Ellipse el && el.Closed) || ClosedByEnds(c):
                        if (c is Line || c is Arc) break;
                        double a;
                        try { a = c.Area; } catch (Autodesk.AutoCAD.Runtime.Exception) { skipped.Add($"{h}: area could not be computed"); break; }
                        it = new Util2DLogic.AreaItem { Handle = h, Type = Acad.TypeName(e), Layer = e.Layer, Area = a, Outline = Outline(c) ?? BoxOutline(e) };
                        break;
                    case Curve oc when handles != null:
                        skipped.Add($"{h}: open {Acad.TypeName(oc)}, no area");
                        break;
                }
                if (it == null) continue;
                if (Math.Abs(it.Area) <= minArea || Math.Abs(it.Area) < Geometry.Eps) { skipped.Add($"{h}: area below min_area"); continue; }
                (it.Type == "HATCH" || it.Type == "REGION" ? fills : curves).Add(it);
            }
            var areas = Util2DLogic.Dedupe(curves.Concat(fills).ToList(), skipped);
            if (areas.Count == 0)
                throw ToolException.Invalid("area_schedule: no closed areas found (closed polylines, circles, ellipses, hatches, regions)" +
                                            (layers != null ? $" on layers {string.Join(", ", layers)}" : "") + ". Pass layers or handles.");
            var rows = Util2DLogic.AreaRows(areas, texts, groupBy);
            double total = rows.Sum(r => r.Area);
            var res = new Dictionary<string, object>
            {
                ["group_by"] = groupBy,
                ["count"] = areas.Count,
                ["total_m2"] = Math.Round(total / 1e6, decimals),
                ["rows"] = rows.Select(r =>
                {
                    var d = new Dictionary<string, object> { ["name"] = r.Name, ["area_m2"] = Math.Round(r.Area / 1e6, decimals), ["count"] = r.Count };
                    if (groupBy == "room") { d["layer"] = r.Layer; d["object"] = r.Handles.FirstOrDefault(); }
                    return (object)d;
                }).ToList(),
            };
            if (skipped.Count > 0) res["skipped"] = skipped.Take(50).Cast<object>().ToList();
            var outer = Util2DLogic.Containers(areas);
            if (outer.Count > 0 && p.Has("table_position") && !p.Bool("allow_nested", false))
                throw ToolException.Invalid($"area_schedule: {outer.Count} area(s) contain other areas of the list ({string.Join(", ", outer.Take(20))}), e.g. a building " +
                                            "outline around the rooms, so the total would count them twice. No table was inserted. Ask the user: leave them out " +
                                            "(handles or layers without them) or keep them (allow_nested: true).");
            if (outer.Count > 0)
            {
                res["contains_others"] = outer.Take(50).Cast<object>().ToList();
                res["warning"] = "These areas contain other areas of the list (e.g. a building outline around the rooms): their area is counted on top. " +
                                 "Ask the user, then call again with handles (or layers) without them.";
            }
            if (groupBy == "room" && rows.Any(r => r.Name.Length == 0))
                res["note"] = "Some areas have no text inside (no room name): they are listed as \"(chưa có tên)\".";
            if (p.Has("table_position"))
            {
                var widths = groupBy == "room" ? new[] { 5.0, 22, 12, 12 } : new[] { 5.0, 18, 9, 12 };
                var t = InsertSchedule(ctx, p, Util2DLogic.AreaTable(rows, groupBy, decimals), widths, groupBy == "room" ? "BẢNG DIỆN TÍCH PHÒNG" : "BẢNG DIỆN TÍCH THEO LAYER");
                res["handles"] = t["handles"];
            }
            return res;
        }

        // ------------------------------------------------------------------ find_replace_text

        /// <summary>One text holder a replace would change: its text before and after, and how to write it back.</summary>
        private sealed class TextHit
        {
            public Entity Ent;
            public string Type;
            public string Tag;
            public string Owner;
            public string Before;
            public string After;
            public int Count;
            public int Row = -1, Col = -1;
        }

        private static IEnumerable<TextHit> TextHits(ToolContext ctx, Entity e, string find, string repl, bool matchCase, bool wholeWord, ISet<string> types)
        {
            // NC-1: codes (\P, %%c, {\C1;...}) are never cut; in MTEXT-format text the replacement is escaped
            bool fmt = Util2DLogic.SafeForMText(find);
            TextHit Hit(Entity ent, string type, string s, bool mtext, string tag = null, string owner = null)
            {
                var after = Util2DLogic.ReplaceFormatted(s, find, repl, matchCase, wholeWord, mtext, out int n);
                return n > 0 ? new TextHit { Ent = ent, Type = type, Before = s, After = after, Count = n, Tag = tag, Owner = owner } : null;
            }
            var hits = new List<TextHit>();
            switch (e)
            {
                case DBText t when types.Contains("text"):
                    hits.Add(Hit(t, "TEXT", t.TextString, false));
                    break;
                case MText mt when types.Contains("mtext") && Util2DLogic.SafeForMText(find):
                    hits.Add(Hit(mt, "MTEXT", mt.Contents, true));
                    break;
                case Dimension dim when types.Contains("dimension") && !string.IsNullOrEmpty(dim.DimensionText):
                    hits.Add(Hit(dim, "DIMENSION", dim.DimensionText, fmt));
                    break;
                case MLeader ml when types.Contains("mleader") && ml.ContentType == ContentType.MTextContent && Util2DLogic.SafeForMText(find):
                    using (var m = ml.MText) if (m != null) hits.Add(Hit(ml, "MULTILEADER", m.Contents, true));
                    break;
                case Table tb when types.Contains("table"):
                    for (int r = 0; r < tb.Rows.Count; r++)
                        for (int c = 0; c < tb.Columns.Count; c++)
                        {
                            var h = Hit(tb, "TABLE_CELL", SafeCellText(tb, r, c), fmt);
                            if (h != null) { h.Row = r; h.Col = c; hits.Add(h); }
                        }
                    break;
                case BlockReference br when types.Contains("attribute"):
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        if (!(ctx.Tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)) continue;
                        if (ar.IsMTextAttribute)
                        {
                            if (!Util2DLogic.SafeForMText(find)) continue;
                            using (var m = ar.MTextAttribute) hits.Add(Hit(ar, "ATTRIBUTE", m.Contents, true, ar.Tag, br.Handle.ToString()));
                        }
                        else hits.Add(Hit(ar, "ATTRIBUTE", ar.TextString, false, ar.Tag, br.Handle.ToString()));
                    }
                    break;
            }
            return hits.Where(h => h != null);
        }

        private static readonly string[] ReplaceTypes = { "text", "mtext", "attribute", "dimension", "mleader", "table" };

        private static object FindReplaceText(ToolContext ctx, Params p)
        {
            string find = p.Str("find");
            if (find.Length == 0) throw ToolException.Invalid("find_replace_text: 'find' is empty.");
            string repl = p.Has("replace") ? Convert.ToString(p.Get("replace"), CultureInfo.InvariantCulture) : throw ToolException.Invalid("find_replace_text: 'replace' is required (\"\" deletes the found text).");
            bool matchCase = p.Bool("match_case", false);
            bool wholeWord = p.Bool("whole_word", false);
            var types = new HashSet<string>((p.StringsOpt("types") ?? ReplaceTypes.ToList()).Select(t => t.Trim().ToLowerInvariant()));
            foreach (var t in types) if (!ReplaceTypes.Contains(t)) throw ToolException.Invalid($"find_replace_text: unknown type '{t}' ({string.Join(", ", ReplaceTypes)}).");
            var layers = p.StringsOpt("layers");

            if (!p.Has("handles"))
            {
                // preview: nothing changes
                int limit = p.Int("limit", 30, 1, 500);
                var all = new List<TextHit>();
                foreach (var e in SpaceEntities(ctx, p))
                {
                    if (layers != null && !layers.Any(l => WildcardMatch(e.Layer, l))) continue;
                    all.AddRange(TextHits(ctx, e, find, repl, matchCase, wholeWord, types));
                }
                var objs = all.Select(h => h.Ent.Handle.ToString()).Distinct().ToList();
                var res = new Dictionary<string, object>
                {
                    ["objects"] = objs.Count,
                    ["replacements"] = all.Sum(h => h.Count),
                    ["examples"] = all.Take(limit).Select(h =>
                    {
                        var d = new Dictionary<string, object> { ["handle"] = h.Ent.Handle.ToString(), ["type"] = h.Type, ["layer"] = h.Ent.Layer, ["before"] = Params.Truncate(h.Before, 200), ["after"] = Params.Truncate(h.After, 200) };
                        if (h.Tag != null) { d["tag"] = h.Tag; d["block_handle"] = h.Owner; }
                        if (h.Row >= 0) { d["row"] = h.Row; d["col"] = h.Col; }
                        return (object)d;
                    }).ToList(),
                    ["match_handles"] = objs.Take(Util2DLogic.MaxReplaceHandles).Cast<object>().ToList(),
                };
                if (objs.Count == 0) res["note"] = "Nothing matches. Nothing to replace.";
                else
                    res["next"] = "Nothing was changed. Call request_confirmation with match_handles and one Vietnamese sentence (count, find → replace, one example), end the turn; after Đồng ý call find_replace_text again with handles = match_handles.";
                if (!Util2DLogic.SafeForMText(find)) res["mtext_skipped"] = "find contains \\ { or }: MTEXT / multileader / multi-line attributes were not searched (formatting codes).";
                if (objs.Count > Util2DLogic.MaxReplaceHandles) res["note"] = $"More than {Util2DLogic.MaxReplaceHandles} objects match: narrow with layers or types.";
                return res;
            }

            // replace: only objects the user approved on the confirmation card (user_confirmed, set by the assistant)
            var hs = p.Handles("handles", Util2DLogic.MaxReplaceHandles);
            var approved = SolidLogic.ConfirmedHandles(p.Get(SolidLogic.UserConfirmedParam));
            var notApproved = Util2DLogic.NotApproved(hs, approved);
            if (notApproved.Count > 0) throw new ToolException(ErrorCodes.InvalidParams, Util2DLogic.ReplaceCardMessage(notApproved.Count));

            var changed = new List<object>();
            var unchanged = new List<object>();
            int replacements = 0;
            foreach (var h in hs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var ent = Acad.GetEntity(ctx, h, false);
                // an attribute handle (from the preview) is changed directly; a block handle changes its attributes
                var hits = ent is AttributeReference ar0
                    ? (types.Contains("attribute") ? AttributeHits(ar0, find, repl, matchCase, wholeWord) : new List<TextHit>())
                    : TextHits(ctx, ent, find, repl, matchCase, wholeWord, types).ToList();
                if (hits.Count == 0) { unchanged.Add(h); continue; }
                foreach (var hit in hits)
                {
                    var w = ctx.Tr.GetObject(hit.Ent.ObjectId, OpenMode.ForWrite) as Entity;
                    if (w is AttributeReference) Acad.EnsureLayerUnlocked(ctx, (Entity)ctx.Tr.GetObject(w.OwnerId, OpenMode.ForRead), hit.Owner ?? h);
                    Acad.EnsureLayerUnlocked(ctx, w, w.Handle.ToString());
                    WriteText(w, hit);
                    replacements += hit.Count;
                }
                changed.Add(h);
            }
            ctx.NeedsRegen = true;
            var r2 = new Dictionary<string, object> { ["changed"] = changed, ["replacements"] = replacements };
            if (unchanged.Count > 0) r2["unchanged"] = unchanged;
            return r2;
        }

        private static List<TextHit> AttributeHits(AttributeReference ar, string find, string repl, bool matchCase, bool wholeWord)
        {
            string s;
            if (ar.IsMTextAttribute)
            {
                if (!Util2DLogic.SafeForMText(find)) return new List<TextHit>();
                using (var m = ar.MTextAttribute) s = m.Contents;
            }
            else s = ar.TextString;
            var after = Util2DLogic.ReplaceFormatted(s, find, repl, matchCase, wholeWord, ar.IsMTextAttribute, out int n);
            return n == 0 ? new List<TextHit>() : new List<TextHit> { new TextHit { Ent = ar, Type = "ATTRIBUTE", Tag = ar.Tag, Owner = ar.OwnerId.Handle.ToString(), Before = s, After = after, Count = n } };
        }

        private static void WriteText(Entity w, TextHit hit)
        {
            switch (w)
            {
                case AttributeReference ar:
                    if (ar.IsMTextAttribute)
                    {
                        var m = ar.MTextAttribute;
                        m.Contents = hit.After;
                        ar.MTextAttribute = m;
                        ar.UpdateMTextAttribute();
                    }
                    else ar.TextString = hit.After;
                    break;
                case DBText t: t.TextString = hit.After; break;
                case MText mt: mt.Contents = hit.After; break;
                case Dimension dim: dim.DimensionText = hit.After; break;
                case MLeader ml:
                    var mm = ml.MText;
                    mm.Contents = hit.After;
                    ml.MText = mm;
                    break;
                case Table tb: tb.Cells[hit.Row, hit.Col].TextString = hit.After; break;
            }
        }
    }
}
