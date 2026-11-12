using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.5 — modification. Every handler returns the handles it created/changed.</summary>
    public static partial class DrawingService
    {
        public const int MaxEraseHandles = 1000; // spec §10 hard stop
        public const int MaxModifyHandles = 5000;
        public const int MaxArrayCopies = 10000;

        private static void RegisterModify(ToolDispatcher d)
        {
            d.Register("move_entities", true, MoveEntities);
            d.Register("copy_entities", true, CopyEntities);
            d.Register("rotate_entities", true, RotateEntities);
            d.Register("scale_entities", true, ScaleEntities);
            d.Register("mirror_entities", true, MirrorEntities);
            d.Register("offset_entity", true, OffsetEntity);
            d.Register("array_rect", true, ArrayRect);
            d.Register("array_polar", true, ArrayPolar);
            d.Register("fillet_entities", true, FilletEntities);
            d.Register("chamfer_entities", true, ChamferEntities);
            d.Register("explode_entities", true, ExplodeEntities);
            d.Register("join_entities", true, JoinEntities);
            d.Register("erase_entities", true, EraseEntities);
            d.Register("set_entity_props", true, SetEntityProps);
            d.Register("edit_text", true, EditText);
            d.Register("edit_polyline", true, EditPolyline);
        }

        // ------------------------------------------------------------------ transforms

        private static List<Entity> OpenForWrite(ToolContext ctx, IEnumerable<string> handles) =>
            handles.Select(h => Acad.GetEntity(ctx, h, true)).ToList();

        private static object Transform(ToolContext ctx, Params p, Matrix3d m)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            var ents = OpenForWrite(ctx, hs);
            foreach (var e in ents) e.TransformBy(m);
            return new Dictionary<string, object> { ["handles"] = hs.Cast<object>().ToList(), ["count"] = ents.Count };
        }

        private static object MoveEntities(ToolContext ctx, Params p)
        {
            Vector3d v = p.Has("displacement") ? Acad.V3(p.Point("displacement")) : Acad.P3(p.Point("to")) - Acad.P3(p.Point("from"));
            return Transform(ctx, p, Matrix3d.Displacement(v));
        }

        private static object RotateEntities(ToolContext ctx, Params p) =>
            Transform(ctx, p, Matrix3d.Rotation(Geometry.DegToRad(p.Num("angle")), Vector3d.ZAxis, Acad.P3(p.Point("base"))));

        private static object ScaleEntities(ToolContext ctx, Params p) =>
            Transform(ctx, p, Matrix3d.Scaling(p.Positive("factor"), Acad.P3(p.Point("base"))));

        private static object MirrorEntities(ToolContext ctx, Params p)
        {
            var a = Acad.P3(p.Point("axis_p1"));
            var b = Acad.P3(p.Point("axis_p2"));
            if (a.DistanceTo(b) < Geometry.Eps) throw ToolException.Invalid("mirror_entities: axis_p1 and axis_p2 must differ.");
            var m = Matrix3d.Mirroring(new Line3d(a, b));
            bool keep = p.Bool("keep_original", true);
            var hs = p.Handles("handles", MaxModifyHandles);
            List<ObjectId> targets;
            if (keep)
            {
                targets = CloneEntities(ctx, hs, Matrix3d.Identity);
            }
            else
            {
                targets = hs.Select(h => Acad.IdFromHandle(ctx.Db, h)).ToList();
            }
            bool mirrText = Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("MIRRTEXT")) != 0;
            foreach (var id in targets)
            {
                var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                if (!keep) Acad.EnsureLayerUnlocked(ctx, e, e.Handle.ToString());
                e.TransformBy(m);
                if (!mirrText && e is DBText t)
                {
                    // like the MIRROR command with MIRRTEXT=0: keep text readable
                    t.IsMirroredInX = false;
                    t.IsMirroredInY = false;
                }
            }
            var r = Acad.Handles(targets);
            r["kept_original"] = keep;
            return r;
        }

        /// <summary>Deep-clones the entities (keeps block attributes, xdata...) and applies a transform.</summary>
        internal static List<ObjectId> CloneEntities(ToolContext ctx, IList<string> handles, Matrix3d m)
        {
            ctx.RequireWritable("copy");
            var ids = new ObjectIdCollection();
            foreach (var h in handles) ids.Add(Acad.IdFromHandle(ctx.Db, h));
            var map = new IdMapping();
            var ownerId = ((Entity)ctx.Tr.GetObject(ids[0], OpenMode.ForRead)).BlockId;
            ctx.Db.DeepCloneObjects(ids, ownerId, map, false);
            var result = new List<ObjectId>();
            foreach (ObjectId src in ids)
            {
                var pair = map[src];
                if (!pair.IsCloned) continue;
                var e = (Entity)ctx.Tr.GetObject(pair.Value, OpenMode.ForWrite);
                if (!m.IsEqualTo(Matrix3d.Identity)) e.TransformBy(m);
                result.Add(pair.Value);
            }
            return result;
        }

        private static object CopyEntities(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            var moves = new List<Vector3d>();
            if (p.Has("displacements"))
                moves.AddRange(p.Points("displacements", 1, MaxArrayCopies).Select(Acad.V3));
            else
                moves.Add(Acad.P3(p.Point("to")) - Acad.P3(p.Point("from")));
            if ((long)moves.Count * hs.Count > MaxArrayCopies * 10L)
                throw new ToolException(ErrorCodes.TooManyEntities, $"copy_entities would create {moves.Count * hs.Count} entities; the limit is {MaxArrayCopies * 10}.");
            var copies = new List<object>();
            var all = new List<object>();
            foreach (var v in moves)
            {
                var ids = CloneEntities(ctx, hs, Matrix3d.Displacement(v));
                var list = ids.Select(Acad.H).Cast<object>().ToList();
                copies.Add(list);
                all.AddRange(list);
            }
            return new Dictionary<string, object> { ["handles"] = all, ["copies"] = copies };
        }

        private static object ArrayRect(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            int rows = p.Int("rows", 1, 1, 1000);
            int cols = p.Int("cols", 1, 1, 1000);
            double rs = p.Num("row_spacing", 0);
            double cs = p.Num("col_spacing", 0);
            double ang = Geometry.DegToRad(p.Num("angle", 0));
            if ((long)rows * cols * hs.Count > MaxArrayCopies)
                throw new ToolException(ErrorCodes.TooManyEntities, $"array_rect would create {(long)rows * cols * hs.Count} entities; the limit is {MaxArrayCopies}.");
            var ux = new Vector3d(Math.Cos(ang), Math.Sin(ang), 0);
            var uy = new Vector3d(-Math.Sin(ang), Math.Cos(ang), 0);
            var all = new List<object>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (r == 0 && c == 0) continue; // the original is the first item
                    var v = ux * (c * cs) + uy * (r * rs);
                    all.AddRange(CloneEntities(ctx, hs, Matrix3d.Displacement(v)).Select(Acad.H));
                }
            return new Dictionary<string, object>
            {
                ["handles"] = all,
                ["items"] = rows * cols,
                ["note"] = "The originals are item [row 0, col 0]; returned handles are the new copies.",
            };
        }

        private static object ArrayPolar(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            var center = Acad.P3(p.Point("center"));
            int count = p.Int("count", 0, 2, 10000);
            double fill = p.Num("fill_angle", 360);
            bool rotate = p.Bool("rotate_items", true);
            if ((long)count * hs.Count > MaxArrayCopies)
                throw new ToolException(ErrorCodes.TooManyEntities, $"array_polar would create {(long)count * hs.Count} entities; the limit is {MaxArrayCopies}.");
            double step = Math.Abs(Math.Abs(fill) - 360) < 1e-9 ? 360.0 / count : fill / (count - 1);
            // reference point for non-rotated items: bbox center of the selection
            Point3d refPt = center;
            if (!rotate)
            {
                Extents3d? ext = null;
                foreach (var h in hs)
                {
                    var e = EntityInfo.Extents(Acad.GetEntity(ctx, h, false));
                    if (e.HasValue) { if (ext.HasValue) { var x = ext.Value; x.AddExtents(e.Value); ext = x; } else ext = e; }
                }
                if (ext.HasValue) refPt = ext.Value.MinPoint + (ext.Value.MaxPoint - ext.Value.MinPoint) / 2;
            }
            var all = new List<object>();
            for (int i = 1; i < count; i++)
            {
                double a = Geometry.DegToRad(step * i);
                Matrix3d m;
                if (rotate) m = Matrix3d.Rotation(a, Vector3d.ZAxis, center);
                else m = Matrix3d.Displacement(refPt.TransformBy(Matrix3d.Rotation(a, Vector3d.ZAxis, center)) - refPt);
                all.AddRange(CloneEntities(ctx, hs, m).Select(Acad.H));
            }
            return new Dictionary<string, object> { ["handles"] = all, ["items"] = count, ["angle_step"] = Acad.R(step) };
        }

        // ------------------------------------------------------------------ offset

        private static object OffsetEntity(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            double dist = p.Positive("distance");
            var side = Acad.P3(p.Point("side_point"));
            var ent = Acad.GetEntity(ctx, h, false);
            if (!(ent is Curve curve) || ent is Xline || ent is Ray)
                throw ToolException.Invalid($"offset_entity: {Acad.TypeName(ent)} cannot be offset; use a line, polyline, arc, circle, ellipse or spline.");

            DBObjectCollection best = null;
            double bestDist = double.MaxValue;
            foreach (var d in new[] { dist, -dist })
            {
                DBObjectCollection res;
                try { res = curve.GetOffsetCurves(d); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }
                double dd = double.MaxValue;
                foreach (DBObject o in res)
                    if (o is Curve c)
                    {
                        try { dd = Math.Min(dd, c.GetClosestPointTo(side, false).DistanceTo(side)); } catch { }
                    }
                if (dd < bestDist)
                {
                    if (best != null) foreach (DBObject o in best) o.Dispose();
                    best = res;
                    bestDist = dd;
                }
                else foreach (DBObject o in res) o.Dispose();
            }
            if (best == null || best.Count == 0)
                throw ToolException.Invalid($"offset_entity: AutoCAD could not offset {h} by {dist} (distance too large for the curvature?).");
            var ids = new List<ObjectId>();
            foreach (DBObject o in best)
            {
                var e = (Entity)o;
                e.SetPropertiesFrom(ent);
                ids.Add(Acad.Add(ctx, e, null, defaults: false, owner: ent.BlockId));
            }
            return Acad.Handles(ids);
        }

        // ------------------------------------------------------------------ fillet / chamfer

        private static (Line, Line) TwoLines(ToolContext ctx, Params p, string tool)
        {
            var h1 = p.Handle("handle1");
            var h2 = p.Handle("handle2");
            if (h1.Equals(h2, StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid($"{tool}: handle1 and handle2 must be different lines.");
            var e1 = Acad.GetEntity(ctx, h1, true);
            var e2 = Acad.GetEntity(ctx, h2, true);
            if (!(e1 is Line l1) || !(e2 is Line l2))
                throw ToolException.Invalid($"{tool}: v1 supports two LINE entities (got {Acad.TypeName(e1)} and {Acad.TypeName(e2)}). " +
                    "For polylines, explode_entities first or redraw the corner with draw_polyline bulges.");
            return (l1, l2);
        }

        private static double[] D(Point3d p) => new[] { p.X, p.Y, p.Z };

        private static object FilletEntities(ToolContext ctx, Params p)
        {
            var (l1, l2) = TwoLines(ctx, p, "fillet_entities");
            var r = Geometry.FilletLines(D(l1.StartPoint), D(l1.EndPoint), D(l2.StartPoint), D(l2.EndPoint), p.Num("radius", 0));
            l1.StartPoint = Acad.P3(r.Line1Start); l1.EndPoint = Acad.P3(r.Line1End);
            l2.StartPoint = Acad.P3(r.Line2Start); l2.EndPoint = Acad.P3(r.Line2End);
            var ids = new List<ObjectId> { l1.ObjectId, l2.ObjectId };
            if (r.ArcCenter != null)
            {
                var arc = new Arc(Acad.P3(r.ArcCenter), Vector3d.ZAxis, r.ArcRadius, r.ArcStart, r.ArcEnd);
                arc.SetDatabaseDefaults(ctx.Db);
                arc.SetPropertiesFrom(l1);
                var id = Acad.Add(ctx, arc, null, defaults: false, owner: l1.BlockId);
                ids.Add(id);
            }
            var res = Acad.Handles(ids);
            if (r.ArcCenter != null) res["arc_handle"] = Acad.H(ids[2]);
            return res;
        }

        private static object ChamferEntities(ToolContext ctx, Params p)
        {
            var (l1, l2) = TwoLines(ctx, p, "chamfer_entities");
            double d1 = p.Num("dist1");
            double d2 = p.Num("dist2", d1);
            var r = Geometry.ChamferLines(D(l1.StartPoint), D(l1.EndPoint), D(l2.StartPoint), D(l2.EndPoint), d1, d2);
            l1.StartPoint = Acad.P3(r.Line1Start); l1.EndPoint = Acad.P3(r.Line1End);
            l2.StartPoint = Acad.P3(r.Line2Start); l2.EndPoint = Acad.P3(r.Line2End);
            var ids = new List<ObjectId> { l1.ObjectId, l2.ObjectId };
            if (r.ChamferStart != null)
            {
                var ln = new Line(Acad.P3(r.ChamferStart), Acad.P3(r.ChamferEnd));
                ln.SetDatabaseDefaults(ctx.Db);
                ln.SetPropertiesFrom(l1);
                var id = Acad.Add(ctx, ln, null, defaults: false, owner: l1.BlockId);
                ids.Add(id);
            }
            return Acad.Handles(ids);
        }

        // ------------------------------------------------------------------ explode / join / erase

        private static object ExplodeEntities(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            RequireCardForOlderSolids(ctx, p, "explode_entities", hs);
            var created = new List<ObjectId>();
            var skipped = new List<object>();
            foreach (var h in hs)
            {
                var e = Acad.GetEntity(ctx, h, true);
                var parts = new DBObjectCollection();
                try { e.Explode(parts); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    skipped.Add(new Dictionary<string, object> { ["handle"] = h, ["reason"] = ex.ErrorStatus.ToString() });
                    continue;
                }
                if (parts.Count == 0) { skipped.Add(new Dictionary<string, object> { ["handle"] = h, ["reason"] = "nothing to explode" }); continue; }
                var owner = (BlockTableRecord)ctx.Tr.GetObject(e.BlockId, OpenMode.ForWrite);
                foreach (DBObject o in parts)
                {
                    var part = (Entity)o;
                    var id = owner.AppendEntity(part);
                    ctx.Tr.AddNewlyCreatedDBObject(part, true);
                    created.Add(id);
                }
                e.Erase();
            }
            var r = Acad.Handles(created);
            if (skipped.Count > 0) r["skipped"] = skipped;
            return r;
        }

        /// <summary>
        /// 3D solids, surfaces and meshes that were in the drawing before this turn (also those Claude built in an earlier
        /// turn) are exploded or erased only with the Đồng ý button of a confirmation card that listed them (decided
        /// 12/11/2026: C1, C2), like the 3D booleans / slice / fillet. The assistant's gate only sees handles, so the check is
        /// here; the assistant passes the approved handles in user_confirmed. Throws before anything changes. 2D objects:
        /// the usual rule only (the assistant asks above 20 objects).
        /// </summary>
        private static void RequireCardForOlderSolids(ToolContext ctx, Params p, string tool, List<string> hs)
        {
            var confirmed = SolidLogic.ConfirmedHandles(p.Get(SolidLogic.UserConfirmedParam));
            var items = hs.Select(h => (h, IsExplodeConfirmEntity(Acad.GetEntity(ctx, h, false)), ctx.CreatedThisTurn(h))).ToList();
            var need = SolidLogic.SolidsNeedingCard(items, confirmed);
            if (need.Count > 0) throw new ToolException(ErrorCodes.InvalidParams, SolidLogic.CardConfirmMessage(tool, need));
        }

        /// <summary>3D solid, ACIS body, surface or mesh: the entity's DatabaseServices type or one of its base types (SolidLogic.ExplodeConfirmTypes).</summary>
        private static bool IsExplodeConfirmEntity(Entity e)
        {
            var names = new List<string>();
            for (var t = e?.GetType(); t != null && t != typeof(Entity); t = t.BaseType)
                if (t.Namespace == "Autodesk.AutoCAD.DatabaseServices") names.Add(t.Name);
            return SolidLogic.IsExplodeConfirmType(names);
        }

        private static object JoinEntities(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            if (hs.Count < 2) throw ToolException.Invalid("join_entities: give at least 2 handles (touching lines, arcs or polylines).");
            var ents = OpenForWrite(ctx, hs);
            foreach (var e in ents)
                if (!(e is Line) && !(e is Arc) && !(e is Polyline))
                    throw ToolException.Invalid($"join_entities: {Acad.TypeName(e)} {e.Handle} cannot be joined into a polyline (only LINE, ARC, LWPOLYLINE).");

            Polyline pl;
            ObjectId plId;
            int startIdx;
            if (ents[0] is Polyline existing)
            {
                pl = existing;
                plId = existing.ObjectId;
                startIdx = 1;
            }
            else
            {
                pl = ToPolyline(ents[0]);
                pl.SetDatabaseDefaults(ctx.Db);
                pl.SetPropertiesFrom(ents[0]);
                plId = Acad.Add(ctx, pl, null, defaults: false, owner: ents[0].BlockId);
                ents[0].Erase();
                startIdx = 1;
            }
            // Join repeatedly so the order of the handles does not matter.
            var pending = ents.Skip(startIdx).ToList();
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                foreach (var e in pending.ToList())
                {
                    try
                    {
                        pl.JoinEntity(e);
                        e.Erase();
                        pending.Remove(e);
                        progress = true;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // not touching yet; retry after others are joined
                    }
                }
            }
            var r = Acad.Handles(plId);
            r["joined"] = ents.Count - pending.Count;
            r["closed"] = pl.Closed;
            if (pending.Count > 0)
                r["not_joined"] = pending.Select(e => (object)e.Handle.ToString()).ToList();
            return r;
        }

        private static Polyline ToPolyline(Entity e)
        {
            var pl = new Polyline();
            switch (e)
            {
                case Line l:
                    pl.AddVertexAt(0, new Point2d(l.StartPoint.X, l.StartPoint.Y), 0, 0, 0);
                    pl.AddVertexAt(1, new Point2d(l.EndPoint.X, l.EndPoint.Y), 0, 0, 0);
                    pl.Elevation = l.StartPoint.Z;
                    break;
                case Arc a:
                    double sweep = Geometry.NormalizeRad(a.EndAngle - a.StartAngle);
                    pl.AddVertexAt(0, new Point2d(a.StartPoint.X, a.StartPoint.Y), Math.Tan(sweep / 4), 0, 0);
                    pl.AddVertexAt(1, new Point2d(a.EndPoint.X, a.EndPoint.Y), 0, 0, 0);
                    pl.Elevation = a.Center.Z;
                    break;
            }
            return pl;
        }

        private static object EraseEntities(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxEraseHandles);
            RequireCardForOlderSolids(ctx, p, "erase_entities", hs); // C1 (KT-12): no copy → subtract on the copy → erase the original either
            var ents = OpenForWrite(ctx, hs);
            foreach (var e in ents) e.Erase();
            return new Dictionary<string, object> { ["erased"] = ents.Count, ["handles"] = hs.Cast<object>().ToList() };
        }

        // ------------------------------------------------------------------ properties / text / polyline

        private static object SetEntityProps(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            if (!p.Has("layer") && !p.Has("color") && !p.Has("linetype") && !p.Has("lineweight") && !p.Has("linetype_scale"))
                throw ToolException.Invalid("set_entity_props: give at least one of layer, color, linetype, lineweight, linetype_scale.");
            var ents = OpenForWrite(ctx, hs);
            foreach (var e in ents) Acad.ApplyCommon(ctx, e, p);
            return new Dictionary<string, object> { ["count"] = ents.Count, ["handles"] = hs.Cast<object>().ToList() };
        }

        private static string Replace(string s, string find, string with, bool matchCase, out int n)
        {
            n = 0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(find)) return s;
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var sb = new System.Text.StringBuilder();
            int i = 0, j;
            while ((j = s.IndexOf(find, i, cmp)) >= 0)
            {
                sb.Append(s, i, j - i).Append(with);
                i = j + find.Length;
                n++;
            }
            sb.Append(s, i, s.Length - i);
            return sb.ToString();
        }

        private static object EditText(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var ent = Acad.GetEntity(ctx, h, true);
            string text = p.Str("text", null);
            string find = p.Str("find", null);
            string with = p.Str("replace_with", null);
            bool matchCase = p.Bool("match_case", false);
            if (find != null && with == null) throw ToolException.Invalid("edit_text: 'find' needs 'replace_with'.");
            double? height = p.Has("height") ? p.Positive("height") : (double?)null;
            double? rotation = p.NumOpt("rotation");
            if (text == null && find == null && !height.HasValue && !rotation.HasValue)
                throw ToolException.Invalid("edit_text: give 'text', or 'find'+'replace_with', and/or 'height' / 'rotation'.");
            int replaced = 0;

            string Apply(string current, bool mtext)
            {
                if (text != null) return mtext ? text.Replace("\r\n", "\\P").Replace("\n", "\\P") : text;
                if (find != null) return Replace(current, find, with, matchCase, out replaced);
                return current;
            }

            switch (ent)
            {
                case AttributeReference ar:
                    if (ar.IsMTextAttribute)
                    {
                        using (var m = ar.MTextAttribute)
                        {
                            m.Contents = Apply(m.Contents, true);
                            ar.MTextAttribute = m;
                        }
                    }
                    else ar.TextString = Apply(ar.TextString, false);
                    if (height.HasValue) ar.Height = height.Value;
                    break;
                case DBText t:
                    t.TextString = Apply(t.TextString, false);
                    if (height.HasValue) t.Height = height.Value;
                    if (rotation.HasValue) t.Rotation = Geometry.DegToRad(rotation.Value);
                    break;
                case MText mt:
                    mt.Contents = Apply(mt.Contents, true);
                    if (height.HasValue)
                    {
                        mt.TextHeight = height.Value;
                        // inline height overrides (\H) would otherwise win
                        mt.Contents = System.Text.RegularExpressions.Regex.Replace(mt.Contents, @"\\H[0-9.]+x?;", "");
                    }
                    if (rotation.HasValue) mt.Rotation = Geometry.DegToRad(rotation.Value);
                    break;
                case Dimension dim:
                    if (text != null) dim.DimensionText = text; // "" = measured value, "<>" = placeholder for it
                    else if (find != null)
                    {
                        string cur = string.IsNullOrEmpty(dim.DimensionText) ? "<>" : dim.DimensionText;
                        dim.DimensionText = Replace(cur, find, with, matchCase, out replaced);
                    }
                    if (height.HasValue) dim.Dimtxt = height.Value;
                    break;
                case MLeader ml when ml.ContentType == ContentType.MTextContent:
                    using (var mm = ml.MText)
                    {
                        mm.Contents = Apply(mm.Contents, true);
                        if (height.HasValue) { mm.TextHeight = height.Value; ml.TextHeight = height.Value; }
                        ml.MText = mm;
                    }
                    break;
                case Table tb:
                    int row = p.Int("row"), col = p.Int("col");
                    if (row < 0 || row >= tb.Rows.Count || col < 0 || col >= tb.Columns.Count)
                        throw ToolException.Invalid($"edit_text: table {h} has {tb.Rows.Count} rows x {tb.Columns.Count} cols; row/col out of range.");
                    var cell = tb.Cells[row, col];
                    cell.TextString = Apply(cell.TextString, false);
                    if (height.HasValue) cell.TextHeight = height.Value;
                    break;
                default:
                    throw ToolException.Invalid($"edit_text: {Acad.TypeName(ent)} {h} has no editable text (TEXT, MTEXT, attribute, dimension, multileader, table cell).");
            }
            var r = new Dictionary<string, object> { ["handles"] = new List<object> { h } };
            if (find != null) r["replacements"] = replaced;
            return r;
        }

        private static object EditPolyline(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var ent = Acad.GetEntity(ctx, h, true);
            if (!(ent is Polyline pl)) throw ToolException.Invalid($"edit_polyline: {h} is a {Acad.TypeName(ent)}, not an LWPOLYLINE.");
            if (p.Has("vertices"))
            {
                var verts = p.Points("vertices", 2);
                var bulges = p.NumbersOpt("bulges");
                int old = pl.NumberOfVertices;
                for (int i = 0; i < verts.Count; i++)
                    pl.AddVertexAt(old + i, Acad.P2(verts[i]), bulges != null && i < bulges.Count ? bulges[i] : 0, 0, 0);
                for (int i = 0; i < old; i++) pl.RemoveVertexAt(0);
            }
            else if (p.Has("bulges"))
            {
                var bulges = p.Numbers("bulges");
                for (int i = 0; i < Math.Min(bulges.Count, pl.NumberOfVertices); i++) pl.SetBulgeAt(i, bulges[i]);
            }
            var closed = p.BoolOpt("closed");
            if (closed.HasValue) pl.Closed = closed.Value;
            if (p.Has("width")) pl.ConstantWidth = p.Num("width");
            var r = Acad.Handles(pl.ObjectId);
            r["vertices"] = pl.NumberOfVertices;
            r["closed"] = pl.Closed;
            return r;
        }
    }
}
