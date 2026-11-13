using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt B (13/11/2026, docs/ban-giao/nang-cap-b.md): STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE (2D group),
    /// GROUP and MATCHPROP (every plan). The geometry is in Core/Edit2D.cs (Edit2DLogic), unit-tested without AutoCAD.
    /// Every handler runs in the turn's transaction and undo group, so one U removes the whole turn.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterEdit2D(ToolDispatcher d)
        {
            d.Register("stretch_entities", true, StretchEntities);
            d.Register("break_entity", true, BreakEntity);
            d.Register("lengthen_entity", true, LengthenEntity);
            d.Register("divide_entity", true, DivideEntity);
            d.Register("group_entities", true, GroupEntities);
            d.Register("match_properties", true, MatchProperties);
        }

        private static double[] D2(Point3d p) => new[] { p.X, p.Y, p.Z };

        // ------------------------------------------------------------------ STRETCH

        internal static object StretchEntities(ToolContext ctx, Params p)
        {
            var window = Edit2DLogic.StretchWindow(p.PointOpt("corner1"), p.PointOpt("corner2"), p.Has("polygon") ? p.Points("polygon", 3, 1000) : null);
            Vector3d v = p.Has("displacement") ? Acad.V3(p.Point("displacement")) : Acad.P3(p.Point("to")) - Acad.P3(p.Point("from"));
            if (v.Length < Geometry.Eps) throw ToolException.Invalid("stretch_entities: the displacement is zero.");
            bool named = p.Has("handles");
            double minX = window.Min(q => q[0]), maxX = window.Max(q => q[0]), minY = window.Min(q => q[1]), maxY = window.Max(q => q[1]);

            IEnumerable<ObjectId> candidates;
            if (named) candidates = p.Handles("handles", MaxModifyHandles).Select(h => Acad.IdFromHandle(ctx.Db, h)).ToList();
            else candidates = Acad.ModelSpaceIds(ctx);

            var plan = new List<(ObjectId id, bool all, IntegerCollection idx)>();
            var skipped = new List<object>();
            int lockedSkipped = 0;
            foreach (var id in candidates)
            {
                var ent = ctx.Tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (ent == null) continue;
                if (!named)
                {
                    var ext = EntityInfo.Extents(ent);
                    if (!ext.HasValue || ext.Value.MaxPoint.X < minX || ext.Value.MinPoint.X > maxX || ext.Value.MaxPoint.Y < minY || ext.Value.MinPoint.Y > maxY) continue;
                    var ltr = (LayerTableRecord)ctx.Tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    if (ltr.IsFrozen) continue;
                    if (ltr.IsLocked) { lockedSkipped++; continue; }
                }
                var pts = new Point3dCollection();
                try { ent.GetStretchPoints(pts); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }
                var list = pts.Cast<Point3d>().Select(D2).ToList();
                if (list.Count == 0) continue;
                var kind = Edit2DLogic.Classify(list, window, out var inside);
                if (kind == Edit2DLogic.StretchKind.None) continue;
                if (kind == Edit2DLogic.StretchKind.Some && IsExplodeConfirmEntity(ent))
                {
                    skipped.Add(new Dictionary<string, object> { ["handle"] = ent.Handle.ToString(), ["reason"] = "3D object only partly inside the window (moved only when wholly inside; use the 3D tools to reshape it)" });
                    continue;
                }
                plan.Add((id, kind == Edit2DLogic.StretchKind.All, new IntegerCollection(inside.ToArray())));
            }
            if (!named && plan.Count > Edit2DLogic.MaxStretchUnasked)
            {
                var hs = plan.Select(x => x.id.Handle.ToString()).ToList();
                throw ToolException.Invalid($"stretch_entities: the window would change {plan.Count} objects (more than {Edit2DLogic.MaxStretchUnasked}). " +
                    "Tell the user how many and ask; after they agree, call again with these handles: " +
                    string.Join(", ", hs.Take(200)) + (hs.Count > 200 ? $" … (+{hs.Count - 200}; use list_entities with the window)" : "") + ".");
            }
            var moved = new List<object>();
            var stretched = new List<object>();
            foreach (var (id, all, idx) in plan)
            {
                var ent = Acad.GetEntity(ctx, id.Handle.ToString(), true);
                if (all) { ent.TransformBy(Matrix3d.Displacement(v)); moved.Add(Acad.H(id)); }
                else { ent.MoveStretchPointsAt(idx, v); stretched.Add(Acad.H(id)); }
            }
            var r = new Dictionary<string, object>
            {
                ["handles"] = moved.Concat(stretched).ToList(),
                ["stretched"] = stretched,
                ["moved"] = moved,
            };
            if (skipped.Count > 0) r["skipped"] = skipped;
            if (lockedSkipped > 0) r["locked_layer_skipped"] = lockedSkipped;
            if (plan.Count == 0) r["note"] = "Nothing had a vertex / end point inside the window: nothing changed.";
            return r;
        }

        // ------------------------------------------------------------------ BREAK

        private static Curve BreakableCurve(ToolContext ctx, string h, string tool, bool forWrite)
        {
            var ent = Acad.GetEntity(ctx, h, forWrite);
            if (!(ent is Curve c) || ent is Xline || ent is Ray)
                throw ToolException.Invalid($"{tool}: {Acad.TypeName(ent)} {h} is not a curve it can work on (lines, arcs, circles, polylines, ellipses, splines).");
            return c;
        }

        private static double ParamAt(Curve c, double[] pt) => c.GetParameterAtPoint(c.GetClosestPointTo(Acad.P3(pt), false));

        internal static object BreakEntity(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var c = BreakableCurve(ctx, h, "break_entity", true);
            double a = ParamAt(c, p.Point("point"));
            double? b = p.Has("point2") ? ParamAt(c, p.Point("point2")) : (double?)null;
            var plan = Edit2DLogic.Break(c.Closed, c.StartParam, c.EndParam, a, b);
            var pieces = c.GetSplitCurves(new DoubleCollection(plan.SplitAt.ToArray()));
            Curve remove = null;
            if (plan.RemoveMid.HasValue)
            {
                var mid = c.GetPointAtParameter(plan.RemoveMid.Value);
                double best = double.MaxValue;
                foreach (DBObject o in pieces)
                {
                    var pc = (Curve)o;
                    double dd = pc.GetClosestPointTo(mid, false).DistanceTo(mid);
                    if (dd < best) { best = dd; remove = pc; }
                }
            }
            var created = new List<ObjectId>();
            foreach (DBObject o in pieces)
            {
                var pc = (Curve)o;
                if (ReferenceEquals(pc, remove)) { pc.Dispose(); continue; }
                pc.SetPropertiesFrom(c);
                created.Add(Acad.Add(ctx, pc, null, defaults: false, owner: c.BlockId));
            }
            if (created.Count == 0) throw ToolException.Invalid("break_entity: nothing would be left of the curve; use erase_entities.");
            c.Erase();
            var r = Acad.Handles(created);
            r["erased"] = h;
            r["note"] = plan.RemoveMid.HasValue ? "The part between the two points was removed; the rest has new handles (original erased)."
                                                : "The curve was split in two at the point; both parts have new handles (original erased).";
            return r;
        }

        // ------------------------------------------------------------------ LENGTHEN

        internal static object LengthenEntity(ToolContext ctx, Params p)
        {
            const string tool = "lengthen_entity";
            var h = p.Handle("handle");
            var ent = Acad.GetEntity(ctx, h, true);
            string mode = p.Str("mode", "delta");
            double value = p.Num("value");
            bool atEnd;
            if (p.Has("near_point"))
            {
                var c0 = ent as Curve ?? throw ToolException.Invalid($"{tool}: {Acad.TypeName(ent)} {h} cannot be lengthened (lines, arcs, open polylines).");
                var np = Acad.P3(p.Point("near_point"));
                atEnd = c0.EndPoint.DistanceTo(np) <= c0.StartPoint.DistanceTo(np);
            }
            else
            {
                var which = p.Str("end", "end").Trim().ToLowerInvariant();
                if (which != "end" && which != "start") throw ToolException.Invalid($"{tool}: 'end' must be start or end.");
                atEnd = which == "end";
            }
            double before, after;
            switch (ent)
            {
                case Line l:
                {
                    before = l.Length;
                    after = Edit2DLogic.NewLength(mode, value, before, tool);
                    var (s, e) = Edit2DLogic.LengthenSegment(D2(l.StartPoint), D2(l.EndPoint), after - before, atEnd, tool);
                    l.StartPoint = Acad.P3(s); l.EndPoint = Acad.P3(e);
                    break;
                }
                case Arc arc:
                {
                    before = arc.Length;
                    after = Edit2DLogic.NewLength(mode, value, before, tool);
                    var (s, e) = Edit2DLogic.LengthenArc(arc.StartAngle, arc.EndAngle, arc.Radius, after - before, atEnd, tool);
                    arc.StartAngle = s; arc.EndAngle = e;
                    break;
                }
                case Polyline pl:
                {
                    if (pl.Closed) throw ToolException.Invalid($"{tool}: {h} is a closed polyline; it has no end to lengthen.");
                    int n = pl.NumberOfVertices;
                    if (n < 2) throw ToolException.Invalid($"{tool}: the polyline has no segment.");
                    before = pl.Length;
                    after = Edit2DLogic.NewLength(mode, value, before, tool);
                    double d = after - before;
                    int seg = atEnd ? n - 2 : 0;
                    var s0 = pl.GetPoint2dAt(seg); var e0 = pl.GetPoint2dAt(seg + 1);
                    double[] s1 = { s0.X, s0.Y }, e1 = { e0.X, e0.Y };
                    double bulge = pl.GetBulgeAt(seg);
                    double[] ns, ne; double nb = 0;
                    if (Math.Abs(bulge) < 1e-12) (ns, ne) = Edit2DLogic.LengthenSegment(s1, e1, d, atEnd, tool);
                    else (ns, ne, nb) = Edit2DLogic.LengthenBulge(s1, e1, bulge, d, atEnd, tool);
                    pl.SetPointAt(seg, Acad.P2(ns));
                    pl.SetPointAt(seg + 1, Acad.P2(ne));
                    pl.SetBulgeAt(seg, nb);
                    break;
                }
                default:
                    throw ToolException.Invalid($"{tool}: {Acad.TypeName(ent)} {h} cannot be lengthened (lines, arcs and open polylines; splines / ellipses: not supported).");
            }
            var r = Acad.Handles(ent.ObjectId);
            r["length_before"] = Acad.R(before);
            r["length"] = Acad.R(((Curve)ent).GetDistanceAtParameter(((Curve)ent).EndParam));
            r["changed_end"] = atEnd ? "end" : "start";
            return r;
        }

        // ------------------------------------------------------------------ DIVIDE / MEASURE

        internal static object DivideEntity(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var c = BreakableCurve(ctx, h, "divide_entity", false);
            double length = c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam);
            int? count = p.Has("count") ? p.Int("count") : (int?)null;
            double? seg = p.Has("segment_length") ? p.Num("segment_length") : (double?)null;
            string from = p.Str("from", "start").Trim().ToLowerInvariant();
            if (from != "start" && from != "end") throw ToolException.Invalid("divide_entity: 'from' must be start or end.");
            var stations = Edit2DLogic.Stations(length, c.Closed, count, seg, from == "end");
            string blockName = p.Str("block_name", null);
            bool align = p.Bool("align_block", true);
            ObjectId btrId = ObjectId.Null;
            BlockTableRecord btr = null;
            if (!string.IsNullOrWhiteSpace(blockName))
            {
                btrId = BlockDefId(ctx, blockName.Trim());
                btr = (BlockTableRecord)ctx.Tr.GetObject(btrId, OpenMode.ForRead);
                if (btr.IsLayout) throw ToolException.Invalid($"divide_entity: '{blockName}' is a layout, not a block.");
            }
            var ids = new List<ObjectId>();
            foreach (var s in stations)
            {
                var pt = c.GetPointAtDist(s);
                if (btr != null)
                {
                    double rot = 0;
                    if (align)
                    {
                        var dv = c.GetFirstDerivative(c.GetParameterAtDistance(s));
                        rot = Math.Atan2(dv.Y, dv.X);
                    }
                    var br = new BlockReference(pt, btrId) { Rotation = rot };
                    ids.Add(Acad.Add(ctx, br, p, owner: c.BlockId));
                    AddAttributes(ctx, br, btr, null);
                }
                else ids.Add(Acad.Add(ctx, new DBPoint(pt), p, owner: c.BlockId));
            }
            var r = Acad.Handles(ids);
            r["marks"] = ids.Count;
            r["curve_length"] = Acad.R(length);
            if (count.HasValue) r["part_length"] = Acad.R(length / count.Value);
            if (btr == null)
            {
                int pdmode = 0;
                try { pdmode = Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("PDMODE")); } catch { }
                if (pdmode == 0 || pdmode == 1)
                    r["note"] = $"Point style PDMODE is {pdmode}: the points are {(pdmode == 0 ? "single dots" : "invisible")}. Tell the user they can change the point style (PTYPE), or use block_name.";
            }
            return r;
        }

        // ------------------------------------------------------------------ GROUP

        private static object GroupEntities(ToolContext ctx, Params p)
        {
            string action = p.Str("action", "create").Trim().ToLowerInvariant();
            var gd = (DBDictionary)ctx.Tr.GetObject(ctx.Db.GroupDictionaryId, OpenMode.ForRead);
            if (action == "create")
            {
                var hs = p.Handles("handles", MaxModifyHandles);
                if (hs.Count < 1) throw ToolException.Invalid("group_entities: give the handles to group.");
                var ents = hs.Select(h => Acad.GetEntity(ctx, h, false)).ToList();
                if (ents.Select(e => e.BlockId).Distinct().Count() > 1)
                    throw ToolException.Invalid("group_entities: the objects are in different spaces (model / layouts); a group must be in one space.");
                string name = p.Str("name", null)?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    int i = 1;
                    while (gd.Contains("CLAUDE_GROUP_" + i)) i++;
                    name = "CLAUDE_GROUP_" + i;
                }
                else
                {
                    try { SymbolUtilityServices.ValidateSymbolName(name, false); }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { throw ToolException.Invalid($"group_entities: '{name}' is not a valid group name (no < > / \\ \" : ; ? * | , = `)."); }
                    if (gd.Contains(name)) throw ToolException.Invalid($"group_entities: a group named '{name}' already exists; choose another name, or ungroup it first.");
                }
                ctx.RequireWritable("group");
                gd.UpgradeOpen();
                var g = new Group(p.Str("description", "") ?? "", true);
                gd.SetAt(name, g);
                ctx.Tr.AddNewlyCreatedDBObject(g, true);
                g.Append(new ObjectIdCollection(ents.Select(e => e.ObjectId).ToArray()));
                return new Dictionary<string, object> { ["group"] = name, ["count"] = ents.Count, ["members"] = hs.Cast<object>().ToList() };
            }
            if (action == "ungroup")
            {
                var groups = new Dictionary<ObjectId, string>();
                string name = p.Str("name", null)?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    if (!gd.Contains(name)) throw ToolException.Invalid($"group_entities: there is no group named '{name}'.");
                    groups[gd.GetAt(name)] = name;
                }
                else
                {
                    foreach (var h in p.Handles("handles", MaxModifyHandles))
                    {
                        var e = Acad.GetEntity(ctx, h, false);
                        var reactors = e.GetPersistentReactorIds();
                        if (reactors == null) continue;
                        foreach (ObjectId rid in reactors)
                            if (!rid.IsErased && ctx.Tr.GetObject(rid, OpenMode.ForRead) is Group g0) groups[rid] = g0.Name;
                    }
                }
                if (groups.Count == 0) throw ToolException.Invalid("group_entities: these objects are in no group.");
                ctx.RequireWritable("ungroup");
                gd.UpgradeOpen();
                foreach (var kv in groups)
                {
                    var g = (Group)ctx.Tr.GetObject(kv.Key, OpenMode.ForWrite);
                    if (gd.Contains(kv.Value)) gd.Remove(kv.Key);
                    g.Erase();
                }
                return new Dictionary<string, object> { ["ungrouped"] = groups.Values.Cast<object>().ToList(), ["note"] = "Only the groups were removed; the objects are unchanged." };
            }
            throw ToolException.Invalid("group_entities: 'action' must be create or ungroup.");
        }

        // ------------------------------------------------------------------ MATCHPROP

        private static double? ThicknessOf(Entity e)
        {
            switch (e)
            {
                case Line l: return l.Thickness;
                case Circle c: return c.Thickness;
                case Arc a: return a.Thickness;
                case Polyline pl: return pl.Thickness;
                case DBText t: return t.Thickness;
                case DBPoint pt: return pt.Thickness;
                default: return null;
            }
        }

        private static bool SetThickness(Entity e, double v)
        {
            switch (e)
            {
                case Line l: l.Thickness = v; return true;
                case Circle c: c.Thickness = v; return true;
                case Arc a: a.Thickness = v; return true;
                case Polyline pl: pl.Thickness = v; return true;
                case DBText t: t.Thickness = v; return true;
                case DBPoint pt: pt.Thickness = v; return true;
                default: return false;
            }
        }

        private static object MatchProperties(ToolContext ctx, Params p)
        {
            var src = Acad.GetEntity(ctx, p.Handle("source"), false);
            var hs = p.Handles("handles", MaxModifyHandles).Where(h => !h.Equals(src.Handle.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (hs.Count == 0) throw ToolException.Invalid("match_properties: give the target handles (other than the source).");
            var props = Edit2DLogic.MatchProps(ctx.Dispatcher?.Plan, p.StringsOpt("properties"), out var skipped);
            var ents = OpenForWrite(ctx, hs);
            int textDone = 0, dimDone = 0, thickDone = 0;
            double? srcThick = ThicknessOf(src);
            foreach (var t in ents)
            {
                foreach (var prop in props)
                {
                    switch (prop)
                    {
                        case "layer": t.LayerId = src.LayerId; break;
                        case "color": t.Color = src.Color; break;
                        case "linetype": t.LinetypeId = src.LinetypeId; break;
                        case "lineweight": t.LineWeight = src.LineWeight; break;
                        case "linetype_scale": t.LinetypeScale = src.LinetypeScale; break;
                        case "transparency": t.Transparency = src.Transparency; break;
                        case "thickness": if (srcThick.HasValue && SetThickness(t, srcThick.Value)) thickDone++; break;
                        case "text":
                            if (src is DBText st && t is DBText tt) { tt.TextStyleId = st.TextStyleId; tt.Height = st.Height; tt.WidthFactor = st.WidthFactor; tt.Oblique = st.Oblique; textDone++; }
                            else if (src is MText sm && t is MText tm) { tm.TextStyleId = sm.TextStyleId; tm.TextHeight = sm.TextHeight; textDone++; }
                            else if (src is DBText st2 && t is MText tm2) { tm2.TextStyleId = st2.TextStyleId; tm2.TextHeight = st2.Height; textDone++; }
                            else if (src is MText sm2 && t is DBText tt2) { tt2.TextStyleId = sm2.TextStyleId; tt2.Height = sm2.TextHeight; textDone++; }
                            break;
                        case "dimension":
                            if (src is Dimension sd && t is Dimension td) { td.DimensionStyle = sd.DimensionStyle; dimDone++; }
                            break;
                    }
                }
            }
            var r = new Dictionary<string, object>
            {
                ["count"] = ents.Count,
                ["handles"] = hs.Cast<object>().ToList(),
                ["copied"] = props.Cast<object>().ToList(),
            };
            if (props.Contains("text")) r["text_matched"] = textDone;
            if (props.Contains("dimension")) r["dimension_matched"] = dimDone;
            if (props.Contains("thickness") && srcThick.HasValue) r["thickness_matched"] = thickDone;
            if (skipped.Count > 0) r["skipped"] = skipped.Select(s => (object)(s + " (this machine's plan has no 2D)")).ToList();
            return r;
        }
    }
}
