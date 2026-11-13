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
            // nâng cấp đợt A: levels stacked along Z (ARRAYRECT's levels); labelled 3D (PlanLogic.ThreeDParams)
            int levels = p.Int("levels", 1, 1, 1000);
            double rs = p.Num("row_spacing", 0);
            double cs = p.Num("col_spacing", 0);
            double ls = p.Num("level_spacing", 0);
            if (levels > 1 && Math.Abs(ls) < Geometry.Eps)
                throw ToolException.Invalid("array_rect: levels > 1 needs 'level_spacing' (the distance between levels along Z, mm; negative = downwards).");
            double ang = Geometry.DegToRad(p.Num("angle", 0));
            long total = (long)rows * cols * levels * hs.Count;
            if (total > MaxArrayCopies)
                throw new ToolException(ErrorCodes.TooManyEntities, $"array_rect would create {total} entities; the limit is {MaxArrayCopies}.");
            var ux = new Vector3d(Math.Cos(ang), Math.Sin(ang), 0);
            var uy = new Vector3d(-Math.Sin(ang), Math.Cos(ang), 0);
            var all = new List<object>();
            for (int l = 0; l < levels; l++)
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (l == 0 && r == 0 && c == 0) continue; // the original is the first item
                        var v = ux * (c * cs) + uy * (r * rs) + Vector3d.ZAxis * (l * ls);
                        all.AddRange(CloneEntities(ctx, hs, Matrix3d.Displacement(v)).Select(Acad.H));
                    }
            var res = new Dictionary<string, object>
            {
                ["handles"] = all,
                ["items"] = rows * cols * levels,
                ["note"] = levels > 1
                    ? "The originals are item [level 0, row 0, col 0]; returned handles are the new copies, level by level."
                    : "The originals are item [row 0, col 0]; returned handles are the new copies.",
            };
            if (levels > 1) res["levels"] = levels;
            return res;
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
            // nâng cấp đợt B: both_sides makes a copy on each side (no side_point needed)
            bool both = p.Bool("both_sides", false);
            Edit2DLogic.CheckOffsetSides(both, p.Has("side_point"));
            var ent = Acad.GetEntity(ctx, h, false);
            if (!(ent is Curve curve) || ent is Xline || ent is Ray)
                throw ToolException.Invalid($"offset_entity: {Acad.TypeName(ent)} cannot be offset; use a line, polyline, arc, circle, ellipse or spline.");

            var chosen = new List<DBObjectCollection>();
            var failed = 0;
            if (both)
            {
                foreach (var d in new[] { dist, -dist })
                {
                    try { var res = curve.GetOffsetCurves(d); if (res.Count > 0) chosen.Add(res); else failed++; }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { failed++; }
                }
                if (chosen.Count == 0)
                    throw ToolException.Invalid($"offset_entity: AutoCAD could not offset {h} by {dist} on either side (distance too large for the curvature?).");
            }
            else
            {
                var side = Acad.P3(p.Point("side_point"));
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
                chosen.Add(best);
            }
            var ids = new List<ObjectId>();
            foreach (var set in chosen)
                foreach (DBObject o in set)
                {
                    var e = (Entity)o;
                    e.SetPropertiesFrom(ent);
                    ids.Add(Acad.Add(ctx, e, null, defaults: false, owner: ent.BlockId));
                }
            var r = Acad.Handles(ids);
            if (both)
            {
                r["sides"] = chosen.Count;
                if (failed > 0) r["note"] = "Only one side could be offset (the distance is too large on the other side, e.g. inside a small circle or arc).";
            }
            return r;
        }

        // ------------------------------------------------------------------ fillet / chamfer

        private static double[] D(Point3d p) => new[] { p.X, p.Y, p.Z };

        /// <summary>
        /// The two objects of fillet / chamfer (nâng cấp đợt B): two lines (as before), a line and an arc or two arcs, or a
        /// polyline alone (handle2 left out: every corner, or 'vertex').
        /// </summary>
        private static (Entity e1, Entity e2) CornerObjects(ToolContext ctx, Params p, string tool)
        {
            var h1 = p.Handle("handle1");
            var e1 = Acad.GetEntity(ctx, h1, true);
            if (!p.Has("handle2"))
            {
                if (!(e1 is Polyline))
                    throw ToolException.Invalid($"{tool}: give handle2 (the second line or arc); handle1 alone only works for an LWPOLYLINE (all its corners, or 'vertex').");
                return (e1, null);
            }
            var h2 = p.Handle("handle2");
            if (h1.Equals(h2, StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid($"{tool}: handle1 and handle2 must be different objects (for a polyline's corners leave handle2 out).");
            var e2 = Acad.GetEntity(ctx, h2, true);
            if (!(e1 is Line || e1 is Arc) || !(e2 is Line || e2 is Arc))
                throw ToolException.Invalid($"{tool}: two objects must be LINEs or ARCs (got {Acad.TypeName(e1)} and {Acad.TypeName(e2)}). " +
                    "For a polyline's corners give handle1 = the polyline and leave handle2 out (optionally 'vertex').");
            foreach (var a in new[] { e1, e2 }.OfType<Arc>())
                if (!a.Normal.IsParallelTo(Vector3d.ZAxis))
                    throw ToolException.Invalid($"{tool}: arc {a.Handle} is not in the XY plane.");
            return (e1, e2);
        }

        private static Edit2DLogic.Prim PrimOf(Entity e)
        {
            if (e is Line l) return Edit2DLogic.Prim.Line(D(l.StartPoint), D(l.EndPoint));
            var a = (Arc)e;
            // an arc whose normal is -Z runs clockwise in the WCS: its OCS angles are mirrored (Edit2DLogic.WcsArcAngles)
            var (s, e2) = Edit2DLogic.WcsArcAngles(a.StartAngle, a.EndAngle, a.Normal.Z < 0);
            return Edit2DLogic.Prim.Arc(D(a.Center), a.Radius, s, e2);
        }

        private static void ApplyPrim(Entity e, Edit2DLogic.Prim k)
        {
            if (e is Line l)
            {
                double z = l.StartPoint.Z;
                l.StartPoint = new Point3d(k.A[0], k.A[1], z);
                l.EndPoint = new Point3d(k.B[0], k.B[1], z);
                return;
            }
            var a = (Arc)e;
            var (s, e2) = Edit2DLogic.WcsArcAngles(k.S, k.E, a.Normal.Z < 0); // the map is its own inverse
            a.StartAngle = s; a.EndAngle = e2;
        }

        /// <summary>Fillet / chamfer of a polyline's corners: the vertices are rewritten in place (same handle).</summary>
        private static object PolylineCorners(Polyline pl, Params p, string tool, bool chamfer)
        {
            int n = pl.NumberOfVertices;
            var v = new List<double[]>();
            var bulges = new List<double>();
            for (int i = 0; i < n; i++) { var q = pl.GetPoint2dAt(i); v.Add(new[] { q.X, q.Y }); bulges.Add(pl.GetBulgeAt(i)); }
            int? vertex = p.Has("vertex") ? p.Int("vertex") : (int?)null;
            double d1 = chamfer ? p.Num("dist1") : 0;
            double d2 = chamfer ? p.Num("dist2", d1) : 0;
            var r = Edit2DLogic.PolylineCorners(v, bulges, pl.Closed, vertex, chamfer, chamfer ? 0 : p.Num("radius", 0), d1, d2, tool);
            // keep a constant width; per-vertex widths cannot follow the new vertices
            double w = n > 0 ? pl.GetStartWidthAt(0) : 0;
            bool constant = Enumerable.Range(0, n).All(i => Math.Abs(pl.GetStartWidthAt(i) - w) < 1e-9 && Math.Abs(pl.GetEndWidthAt(i) - w) < 1e-9);
            for (int i = 0; i < r.Vertices.Count; i++) pl.AddVertexAt(n + i, Acad.P2(r.Vertices[i]), r.Bulges[i], constant ? w : 0, constant ? w : 0);
            for (int i = 0; i < n; i++) pl.RemoveVertexAt(0);
            var res = Acad.Handles(pl.ObjectId);
            res["corners"] = r.Done.Count;
            res["vertices"] = pl.NumberOfVertices;
            if (r.Skipped.Count > 0) res["skipped"] = r.Skipped.Cast<object>().ToList();
            res["note"] = "Polyline changed in place (same handle). Vertex indices after the corners have shifted.";
            return res;
        }

        internal static object FilletEntities(ToolContext ctx, Params p)
        {
            var (e1, e2) = CornerObjects(ctx, p, "fillet_entities");
            if (e2 == null) return PolylineCorners((Polyline)e1, p, "fillet_entities", false);
            double radius = p.Num("radius", 0);
            if (!(e1 is Line l1) || !(e2 is Line l2)) return ArcCorner(ctx, e1, e2, p, "fillet_entities", false);
            var r = Geometry.FilletLines(D(l1.StartPoint), D(l1.EndPoint), D(l2.StartPoint), D(l2.EndPoint), radius);
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

        /// <summary>Fillet / chamfer when an arc is involved (Edit2DLogic.FilletArcs / ChamferArcs).</summary>
        private static object ArcCorner(ToolContext ctx, Entity e1, Entity e2, Params p, string tool, bool chamfer)
        {
            Edit2DLogic.ArcCornerResult r;
            if (chamfer)
            {
                double d1 = p.Num("dist1");
                r = Edit2DLogic.ChamferArcs(PrimOf(e1), PrimOf(e2), d1, p.Num("dist2", d1), tool);
            }
            else r = Edit2DLogic.FilletArcs(PrimOf(e1), PrimOf(e2), p.Num("radius", 0), tool);
            ApplyPrim(e1, r.New1);
            ApplyPrim(e2, r.New2);
            var ids = new List<ObjectId> { e1.ObjectId, e2.ObjectId };
            double z = e1 is Line ln ? ln.StartPoint.Z : ((Arc)e1).Center.Z;
            Entity extra = null;
            if (r.Fillet != null) extra = new Arc(new Point3d(r.Fillet.C[0], r.Fillet.C[1], z), Vector3d.ZAxis, r.Fillet.R, r.Fillet.S, r.Fillet.E);
            else if (r.ChamferStart != null) extra = new Line(new Point3d(r.ChamferStart[0], r.ChamferStart[1], z), new Point3d(r.ChamferEnd[0], r.ChamferEnd[1], z));
            if (extra != null)
            {
                extra.SetDatabaseDefaults(ctx.Db);
                extra.SetPropertiesFrom(e1);
                ids.Add(Acad.Add(ctx, extra, null, defaults: false, owner: e1.BlockId));
            }
            var res = Acad.Handles(ids);
            if (r.Fillet != null) res["arc_handle"] = Acad.H(ids[2]);
            return res;
        }

        private static object ChamferEntities(ToolContext ctx, Params p)
        {
            var (e1, e2) = CornerObjects(ctx, p, "chamfer_entities");
            if (e2 == null) return PolylineCorners((Polyline)e1, p, "chamfer_entities", true);
            if (!(e1 is Line l1) || !(e2 is Line l2)) return ArcCorner(ctx, e1, e2, p, "chamfer_entities", true);
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

        /// <summary>For CLAUDECHATSELFTEST (nâng cấp đợt B: join with a tolerance).</summary>
        internal static object JoinForSelfTest(ToolContext ctx, Params p) => JoinEntities(ctx, p);

        private static object JoinEntities(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", MaxModifyHandles);
            if (hs.Count < 2) throw ToolException.Invalid("join_entities: give at least 2 handles (touching lines, arcs or polylines).");
            // nâng cấp đợt B: gaps up to 'tolerance' are bridged by a straight segment (like PEDIT Join, fuzz distance + Add)
            double tol = p.Num("tolerance", 0);
            if (tol < 0) throw ToolException.Invalid("join_entities: 'tolerance' must be >= 0.");
            var bridged = new List<object>();
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
                // Test & sửa đợt B (NB-2): bridge a small gap only when no touching object is left to join, one gap at a
                // time and the smallest gap first, so a gap never takes the end where a touching object belongs
                if (!progress && tol > 0)
                    foreach (var e in pending.OrderBy(x => BridgeGap(pl, x, tol)).ToList())
                        if (TryBridge(pl, e, tol, out var gap))
                        {
                            e.Erase();
                            pending.Remove(e);
                            progress = true;
                            bridged.Add(new Dictionary<string, object> { ["handle"] = e.Handle.ToString(), ["gap"] = Acad.R(gap) });
                            break;
                        }
            }
            var r = Acad.Handles(plId);
            r["joined"] = ents.Count - pending.Count;
            r["closed"] = pl.Closed;
            if (pending.Count > 0)
                r["not_joined"] = pending.Select(e => (object)e.Handle.ToString()).ToList();
            if (bridged.Count > 0) r["bridged"] = bridged;
            if (tol > 0 && !pl.Closed && pl.NumberOfVertices > 2)
            {
                double endGap = pl.StartPoint.DistanceTo(pl.EndPoint);
                if (endGap > Geometry.Eps && endGap <= tol)
                    r["note"] = $"The two ends are {Acad.R(endGap)} apart (within the tolerance) but the polyline is left open; edit_polyline closed: true closes it.";
            }
            return r;
        }

        /// <summary>The gap TryBridge would close to this object (infinity when none).</summary>
        private static double BridgeGap(Polyline pl, Entity e, double tol)
        {
            if (pl.Closed || !(e is Curve c)) return double.PositiveInfinity;
            var b = Edit2DLogic.Bridge(D(pl.StartPoint), D(pl.EndPoint), D(c.StartPoint), D(c.EndPoint), tol);
            return b == null ? double.PositiveInfinity : b.Value.gap;
        }

        /// <summary>Bridges a gap of at most tol between the polyline and the curve by a straight vertex, then joins; undone when the join still fails.</summary>
        private static bool TryBridge(Polyline pl, Entity e, double tol, out double gap)
        {
            gap = 0;
            if (pl.Closed || !(e is Curve c)) return false; // a closed polyline joins nothing (and its closing bulge must stay)
            var b = Edit2DLogic.Bridge(D(pl.StartPoint), D(pl.EndPoint), D(c.StartPoint), D(c.EndPoint), tol);
            if (b == null) return false;
            gap = b.Value.gap;
            var target = b.Value.objStart ? c.StartPoint : c.EndPoint;
            var pt = new Point2d(target.X, target.Y);
            int n = pl.NumberOfVertices;
            int added;
            double lastBulge = pl.GetBulgeAt(n - 1);
            if (b.Value.plStart) { pl.AddVertexAt(0, pt, 0, 0, 0); added = 0; }
            else { pl.SetBulgeAt(n - 1, 0); pl.AddVertexAt(n, pt, 0, 0, 0); added = n; }
            try { pl.JoinEntity(e); return true; }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                pl.RemoveVertexAt(added);
                pl.SetBulgeAt(n - 1, lastBulge);
                return false;
            }
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
