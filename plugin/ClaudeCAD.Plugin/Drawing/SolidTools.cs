using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// 3D phase 1: solid primitives, extrude / revolve of 2D profiles, booleans, 3D views, visual styles and the
    /// solid query. Every tool here is labelled ToolGroup.ThreeD (Core/Plans.cs). Z is up, units are mm (WCS).
    /// The AutoCAD-free math (presets, checks, expected volumes) is SolidLogic in Core/Solids.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterSolids(ToolDispatcher d)
        {
            d.Register("draw_box", true, DrawBox);
            d.Register("draw_cylinder", true, DrawCylinder);
            d.Register("draw_sphere", true, DrawSphere);
            d.Register("draw_wedge", true, DrawWedge);
            d.Register("draw_torus", true, DrawTorus);
            d.Register("draw_pyramid", true, DrawPyramid);
            d.Register("extrude", true, Extrude);
            d.Register("revolve", true, Revolve);
            d.Register("solid_union", true, (ctx, p) => SolidBoolean(ctx, p, BooleanOperationType.BoolUnite));
            d.Register("solid_subtract", true, SolidSubtract);
            d.Register("solid_intersect", true, (ctx, p) => SolidBoolean(ctx, p, BooleanOperationType.BoolIntersect));
            d.Register("get_solid_info", false, GetSolidInfo);
            // view only, like the zooms: no undo step, allowed in "Chỉ xem" (ToolDispatcher.ReadOnlyAllowed)
            d.Register(new ToolDef("set_view_3d", true, SetView3d, noTransaction: true));
            d.Register(new ToolDef("set_visual_style", true, SetVisualStyle, noTransaction: true));
        }

        // ================================================================= primitives (also used by the self-test)

        internal static Point3d P(double[] p) => Acad.P3(p);

        /// <summary>Box with its corner (min x, y, z before rotation) at corner, or centred on center; rotation about Z (degrees).</summary>
        internal static Solid3d MakeBox(double length, double width, double height, double[] corner, double[] center, double rotationDeg)
        {
            SolidLogic.CheckBox(length, width, height);
            var s = new Solid3d();
            s.CreateBox(length, width, height); // centroid at the origin
            var c = center ?? SolidLogic.BoxCenter(corner, length, width, height, rotationDeg);
            s.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin) * Matrix3d.Rotation(Geometry.DegToRad(rotationDeg), Vector3d.ZAxis, Point3d.Origin));
            return s;
        }

        /// <summary>
        /// Moves a solid built around the origin (axis = Z) so its base centre sits on base and its axis follows axis
        /// (length = height). Solid3d builds frustums / pyramids centred at half the height.
        /// </summary>
        private static void PlaceOnAxis(Solid3d s, double halfHeight, double[] basePt, Vector3d axis, double rotationDeg)
        {
            var z = axis.GetNormal();
            var x = z.GetPerpendicularVector().GetNormal();
            // keep +X for an axis along ±Z so a rotation of 0 means the same as in the plan
            if (Math.Abs(z.X) < 1e-9 && Math.Abs(z.Y) < 1e-9) x = Vector3d.XAxis;
            var y = z.CrossProduct(x).GetNormal();
            x = y.CrossProduct(z).GetNormal();
            var toBase = Matrix3d.Displacement(new Vector3d(0, 0, halfHeight));
            var spin = Matrix3d.Rotation(Geometry.DegToRad(rotationDeg), Vector3d.ZAxis, Point3d.Origin);
            var align = Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, P(basePt), x, y, z);
            s.TransformBy(align * spin * toBase);
        }

        /// <summary>The axis of a cylinder / cone / pyramid: base → top_center, or 'height' along +Z (negative = downwards).</summary>
        private static (double[] basePt, Vector3d axis) AxisOf(Params p)
        {
            var b = p.Point("base");
            Vector3d axis;
            if (p.Has("top_center"))
            {
                var t = p.Point("top_center");
                axis = new Vector3d(t[0] - b[0], t[1] - b[1], t[2] - b[2]);
                if (axis.Length < Geometry.Eps) throw ToolException.Invalid($"{p.Tool}: 'top_center' must differ from 'base'.");
            }
            else
            {
                double h = p.Num("height");
                if (Math.Abs(h) < Geometry.Eps) throw ToolException.Invalid($"{p.Tool}: 'height' must not be 0 (negative = downwards).");
                axis = new Vector3d(0, 0, h);
            }
            return (b, axis);
        }

        internal static Solid3d MakeFrustum(double[] basePt, Vector3d axis, double radius, double topRadius)
        {
            double h = axis.Length;
            SolidLogic.CheckFrustum(radius, topRadius, h);
            var s = new Solid3d();
            s.CreateFrustum(h, radius, radius, topRadius);
            PlaceOnAxis(s, h / 2, basePt, axis, 0);
            return s;
        }

        private static object DrawBox(ToolContext ctx, Params p)
        {
            double l = p.Positive("length"), w = p.Positive("width"), h = p.Num("height");
            double[] corner = p.PointOpt("corner"), center = p.PointOpt("center");
            if (corner == null && center == null) corner = new double[] { 0, 0, 0 };
            if (corner != null && center != null) throw ToolException.Invalid("draw_box: pass either 'corner' or 'center', not both.");
            // a negative height builds the box downwards from the corner
            if (h < 0 && corner != null) { corner = new[] { corner[0], corner[1], SolidLogic.Z3(corner) + h }; h = -h; }
            var s = MakeBox(l, w, h, corner, center, p.Num("rotation", 0));
            return SolidResult(ctx, s, p);
        }

        private static object DrawCylinder(ToolContext ctx, Params p)
        {
            var (b, axis) = AxisOf(p);
            double r = p.Has("radius") ? p.Positive("radius") : p.Positive("diameter") / 2;
            double top = p.Has("top_radius") ? p.Num("top_radius") : r;
            return SolidResult(ctx, MakeFrustum(b, axis, r, top), p);
        }

        private static object DrawSphere(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            double r = p.Has("radius") ? p.Positive("radius") : p.Positive("diameter") / 2;
            var s = new Solid3d();
            s.CreateSphere(r);
            s.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin));
            return SolidResult(ctx, s, p);
        }

        /// <summary>
        /// Wedge on a length × width base: the tall vertical face (height) stands at the corner side (local x = 0)
        /// and the slope goes down to height 0 at x = length. CreateWedge's own orientation is not documented, so it
        /// is read from the centroid (a third of the length from the tall face) and turned if needed.
        /// </summary>
        private static object DrawWedge(ToolContext ctx, Params p)
        {
            double l = p.Positive("length"), w = p.Positive("width"), h = p.Positive("height");
            double[] corner = p.PointOpt("corner") ?? new double[] { 0, 0, 0 };
            double rot = p.Num("rotation", 0);
            var s = new Solid3d();
            s.CreateWedge(l, w, h);
            // bounding box centre to the origin, then the tall face to -X
            var e = s.GeometricExtents;
            var mid = new Point3d((e.MinPoint.X + e.MaxPoint.X) / 2, (e.MinPoint.Y + e.MaxPoint.Y) / 2, (e.MinPoint.Z + e.MaxPoint.Z) / 2);
            s.TransformBy(Matrix3d.Displacement(Point3d.Origin - mid));
            var cg = s.MassProperties.Centroid;
            if (cg.X > 1e-6) s.TransformBy(Matrix3d.Rotation(Math.PI, Vector3d.ZAxis, Point3d.Origin));
            var c = SolidLogic.BoxCenter(corner, l, w, h, rot);
            s.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin) * Matrix3d.Rotation(Geometry.DegToRad(rot), Vector3d.ZAxis, Point3d.Origin));
            return SolidResult(ctx, s, p);
        }

        private static object DrawTorus(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            double R = p.Positive("radius"), r = p.Positive("tube_radius");
            SolidLogic.CheckTorus(R, r);
            var s = new Solid3d();
            s.CreateTorus(R, r);
            s.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin));
            return SolidResult(ctx, s, p);
        }

        private static object DrawPyramid(ToolContext ctx, Params p)
        {
            var (b, axis) = AxisOf(p);
            int sides = p.Int("sides", 4);
            double r = p.Positive("radius");
            double top = p.Num("top_radius", 0);
            double h = axis.Length;
            SolidLogic.CheckPyramid(sides, r, top, h);
            var s = new Solid3d();
            s.CreatePyramid(h, sides, r, top);
            PlaceOnAxis(s, h / 2, b, axis, p.Num("rotation", 0));
            return SolidResult(ctx, s, p);
        }

        /// <summary>Appends a new solid and returns its handle, volume and bounding box (so Claude can check it at once).</summary>
        private static object SolidResult(ToolContext ctx, Solid3d s, Params p)
        {
            var id = Acad.Add(ctx, s, p);
            var r = Acad.Handles(id);
            AddSolidNumbers(r, s);
            return r;
        }

        private static void AddSolidNumbers(Dictionary<string, object> r, Solid3d s)
        {
            try
            {
                double v = s.MassProperties.Volume;
                r["volume"] = Acad.R(v);
                r["volume_m3"] = Math.Round(SolidLogic.ToCubicMetres(v), 6);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { }
            var ext = EntityInfo.Extents(s);
            if (ext.HasValue)
            {
                r["min"] = Acad.J3(ext.Value.MinPoint);
                r["max"] = Acad.J3(ext.Value.MaxPoint);
            }
        }

        // ================================================================= extrude / revolve

        /// <summary>
        /// The regions of a profile: profile_points (a closed polygon made here, not added to the drawing) or
        /// profile_handles (closed curves, or lines / arcs forming closed loops). The caller disposes them.
        /// </summary>
        private static List<Region> ProfileRegions(ToolContext ctx, Params p, out List<ObjectId> sources, out List<double[]> vertices)
        {
            sources = new List<ObjectId>();
            vertices = new List<double[]>();
            var curves = new DBObjectCollection();
            var temp = new List<DBObject>();
            if (p.Has("profile_points") && p.Has("profile_handles"))
                throw ToolException.Invalid($"{p.Tool}: pass either 'profile_points' or 'profile_handles', not both.");
            if (p.Has("profile_points"))
            {
                var pts = p.Points("profile_points", 3, 5000);
                var bulges = p.NumbersOpt("profile_bulges");
                var pl = Acad.MakePolyline(pts, true, bulges);
                temp.Add(pl);
                curves.Add(pl);
                vertices.AddRange(pts);
            }
            else
            {
                foreach (var h in p.Handles("profile_handles", 500))
                {
                    var id = Acad.IdFromHandle(ctx.Db, h);
                    var obj = ctx.Tr.GetObject(id, OpenMode.ForRead);
                    if (obj is Region reg)
                    {
                        var copy = (Region)reg.Clone();
                        temp.Add(copy);
                        sources.Add(id);
                        continue; // a region is used as it is
                    }
                    if (!(obj is Curve c))
                        throw ToolException.Invalid($"{p.Tool}: {h} is a {obj.GetType().Name}; a profile must be a closed curve (polyline, circle, ellipse, spline), lines / arcs forming a closed loop, or a region.");
                    curves.Add(c);
                    sources.Add(id);
                    if (c is Polyline poly)
                        for (int i = 0; i < poly.NumberOfVertices; i++) { var v = poly.GetPoint3dAt(i); vertices.Add(new[] { v.X, v.Y, v.Z }); }
                    else { vertices.Add(new[] { c.StartPoint.X, c.StartPoint.Y, c.StartPoint.Z }); vertices.Add(new[] { c.EndPoint.X, c.EndPoint.Y, c.EndPoint.Z }); }
                }
            }
            var regions = temp.OfType<Region>().ToList();
            if (curves.Count > 0)
            {
                DBObjectCollection made;
                try { made = Region.CreateFromCurves(curves); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    foreach (var t in temp) t.Dispose();
                    throw ToolException.Invalid($"{p.Tool}: the profile is not a closed, flat, non-self-intersecting loop ({ex.ErrorStatus}). Close the polyline, join the pieces, or draw it again with profile_points.");
                }
                foreach (DBObject o in made) if (o is Region r) regions.Add(r); else o.Dispose();
            }
            foreach (var t in temp) if (!(t is Region)) t.Dispose();
            if (regions.Count == 0) throw ToolException.Invalid($"{p.Tool}: no closed area found in the profile. Use closed curves (closed polyline, circle...).");
            return regions;
        }

        private static void EraseProfiles(ToolContext ctx, Params p, List<ObjectId> sources)
        {
            if (!p.Bool("erase_profile", false)) return;
            foreach (var id in sources)
            {
                var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForRead);
                Acad.EnsureLayerUnlocked(ctx, e, Acad.H(id));
                e.UpgradeOpen();
                e.Erase();
            }
        }

        private static object Extrude(ToolContext ctx, Params p)
        {
            double h = p.Num("height");
            double taper = p.Num("taper_angle", 0);
            SolidLogic.CheckExtrude(h, taper);
            var regions = ProfileRegions(ctx, p, out var sources, out _);
            var ids = new List<ObjectId>();
            var solids = new List<Solid3d>();
            try
            {
                foreach (var reg in regions)
                {
                    var s = new Solid3d();
                    try { s.Extrude(reg, h, Geometry.DegToRad(taper)); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        s.Dispose();
                        throw ToolException.Invalid($"extrude: AutoCAD could not extrude the profile ({ex.ErrorStatus}). A large taper_angle that makes the sides meet before the full height is a common cause: use a smaller angle or height.");
                    }
                    // the profile layer / colour are the defaults of the new solid when no layer is given
                    ids.Add(Acad.Add(ctx, s, p));
                    solids.Add(s);
                }
            }
            finally { foreach (var r in regions) r.Dispose(); }
            EraseProfiles(ctx, p, sources);
            return SolidsResult(ids, solids);
        }

        private static object Revolve(ToolContext ctx, Params p)
        {
            var a = p.Point("axis_start");
            var b = p.Point("axis_end");
            var (dir, angle) = SolidLogic.RevolveAxis(a, b, p.Num("angle", 360));
            var regions = ProfileRegions(ctx, p, out var sources, out var vertices);
            var ids = new List<ObjectId>();
            var solids = new List<Solid3d>();
            try
            {
                var crossing = SolidLogic.ProfileCrossesAxis(vertices, a, b);
                if (crossing != null) throw ToolException.Invalid(crossing);
                foreach (var reg in regions)
                {
                    var s = new Solid3d();
                    try { s.Revolve(reg, P(a), Acad.V3(dir), angle); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        s.Dispose();
                        throw ToolException.Invalid($"revolve: AutoCAD could not revolve the profile ({ex.ErrorStatus}). The axis must lie in the profile's plane and must not cross the profile.");
                    }
                    ids.Add(Acad.Add(ctx, s, p));
                    solids.Add(s);
                }
            }
            finally { foreach (var r in regions) r.Dispose(); }
            EraseProfiles(ctx, p, sources);
            return SolidsResult(ids, solids);
        }

        private static object SolidsResult(List<ObjectId> ids, List<Solid3d> solids)
        {
            var r = Acad.Handles(ids);
            double total = 0;
            foreach (var s in solids)
            {
                try { total += s.MassProperties.Volume; } catch (Autodesk.AutoCAD.Runtime.Exception) { }
            }
            r["volume"] = Acad.R(total);
            r["volume_m3"] = Math.Round(SolidLogic.ToCubicMetres(total), 6);
            if (solids.Count > 0)
            {
                Extents3d? ext = null;
                foreach (var s in solids)
                {
                    var x = EntityInfo.Extents(s);
                    if (!x.HasValue) continue;
                    if (ext.HasValue) { var t = ext.Value; t.AddExtents(x.Value); ext = t; } else ext = x;
                }
                if (ext.HasValue) { r["min"] = Acad.J3(ext.Value.MinPoint); r["max"] = Acad.J3(ext.Value.MaxPoint); }
            }
            return r;
        }

        // ================================================================= booleans

        private static Solid3d SolidForWrite(ToolContext ctx, string handle, string tool)
        {
            var e = Acad.GetEntity(ctx, handle, false);
            if (!(e is Solid3d s))
                throw ToolException.Invalid($"{tool}: {handle} is a {Acad.TypeName(e)}, not a 3D solid. Use extrude / revolve or a draw_box-like tool to make solids first.");
            Acad.EnsureLayerUnlocked(ctx, e, handle);
            s.UpgradeOpen();
            return s;
        }

        /// <summary>The boolean of two solids, into target (also used by the self-test). The tool solid is left empty.</summary>
        internal static void BooleanInto(Solid3d target, Solid3d tool, BooleanOperationType op) => target.BooleanOperation(op, tool);

        private static bool IsEmpty(Solid3d s)
        {
            try { return s.IsNull || s.MassProperties.Volume <= 1e-9; }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return true; }
        }

        /// <summary>solid_union / solid_intersect: every solid of 'handles' into the first one; the others are erased.</summary>
        private static object SolidBoolean(ToolContext ctx, Params p, BooleanOperationType op)
        {
            var hs = p.Handles("handles", 500).Select(h => h.ToUpperInvariant()).ToList();
            if (hs.Distinct().Count() != hs.Count) throw ToolException.Invalid($"{p.Tool}: the same handle is listed twice.");
            if (hs.Count < 2) throw ToolException.Invalid($"{p.Tool}: needs at least 2 solids in 'handles'.");
            var target = SolidForWrite(ctx, hs[0], p.Tool);
            var erased = new List<object>();
            foreach (var h in hs.Skip(1))
            {
                var other = SolidForWrite(ctx, h, p.Tool);
                BooleanInto(target, other, op);
                if (!other.IsErased) other.Erase();
                erased.Add(h);
            }
            if (IsEmpty(target))
                throw ToolException.Invalid(op == BooleanOperationType.BoolIntersect
                    ? $"{p.Tool}: the solids do not overlap; the intersection is empty. Nothing was changed."
                    : $"{p.Tool}: the result is empty. Nothing was changed.");
            var r = new Dictionary<string, object> { ["handle"] = hs[0], ["handles"] = new List<object> { hs[0] }, ["erased"] = erased };
            AddSolidNumbers(r, target);
            return r;
        }

        /// <summary>solid_subtract: the 'subtract' solids are cut out of 'target'; they are erased unless keep_tools.</summary>
        private static object SolidSubtract(ToolContext ctx, Params p)
        {
            var targetHandle = p.Str("target").ToUpperInvariant();
            var tools = p.Handles("subtract", 500).Select(h => h.ToUpperInvariant()).ToList();
            if (tools.Contains(targetHandle)) throw ToolException.Invalid("solid_subtract: 'target' must not be in 'subtract'.");
            if (tools.Distinct().Count() != tools.Count) throw ToolException.Invalid("solid_subtract: the same handle is listed twice in 'subtract'.");
            bool keep = p.Bool("keep_tools", false);
            var target = SolidForWrite(ctx, targetHandle, "solid_subtract");
            double before = 0;
            try { before = target.MassProperties.Volume; } catch (Autodesk.AutoCAD.Runtime.Exception) { }
            var erased = new List<object>();
            foreach (var h in tools)
            {
                if (keep)
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    if (!(e is Solid3d src)) throw ToolException.Invalid($"solid_subtract: {h} is a {Acad.TypeName(e)}, not a 3D solid.");
                    using (var copy = (Solid3d)src.Clone()) BooleanInto(target, copy, BooleanOperationType.BoolSubtract);
                }
                else
                {
                    var tool = SolidForWrite(ctx, h, "solid_subtract");
                    BooleanInto(target, tool, BooleanOperationType.BoolSubtract);
                    if (!tool.IsErased) tool.Erase();
                    erased.Add(h);
                }
            }
            if (IsEmpty(target)) throw ToolException.Invalid("solid_subtract: nothing would be left of the target. Nothing was changed.");
            var r = new Dictionary<string, object> { ["handle"] = targetHandle, ["handles"] = new List<object> { targetHandle }, ["erased"] = erased };
            AddSolidNumbers(r, target);
            if (r.TryGetValue("volume", out var after) && after is double va && Math.Abs(va - before) < 1e-6 * Math.Max(before, 1))
                r["note"] = "The volume did not change: the subtracted solids do not overlap the target.";
            return r;
        }

        // ================================================================= query

        private static object GetSolidInfo(ToolContext ctx, Params p)
        {
            var list = new List<object>();
            double total = 0;
            foreach (var h in p.Handles("handles", 200))
            {
                var e = Acad.GetEntity(ctx, h, false);
                var row = new Dictionary<string, object> { ["handle"] = h.ToUpperInvariant(), ["type"] = Acad.TypeName(e), ["layer"] = Acad.LayerName(ctx, e.LayerId) };
                if (e is Solid3d s)
                {
                    try
                    {
                        var mp = s.MassProperties;
                        row["volume"] = Acad.R(mp.Volume);
                        row["volume_m3"] = Math.Round(SolidLogic.ToCubicMetres(mp.Volume), 6);
                        row["centroid"] = Acad.J3(mp.Centroid);
                        total += mp.Volume;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) { row["volume_error"] = ex.ErrorStatus.ToString(); }
                    var ext = EntityInfo.Extents(s);
                    if (ext.HasValue)
                    {
                        var a = ext.Value.MinPoint; var b = ext.Value.MaxPoint;
                        row["min"] = Acad.J3(a);
                        row["max"] = Acad.J3(b);
                        row["size"] = new List<object> { Acad.R(b.X - a.X), Acad.R(b.Y - a.Y), Acad.R(b.Z - a.Z) };
                    }
                    try
                    {
                        using (var brep = new Brep(s))
                        {
                            row["faces"] = CountOf(brep.Faces);
                            row["edges"] = CountOf(brep.Edges);
                            row["vertices"] = CountOf(brep.Vertices);
                        }
                    }
                    catch (Exception ex) { row["topology_error"] = ex.Message; }
                }
                else
                {
                    row["note"] = "Not a 3D solid (get_entity / get_bounding_box describe other objects).";
                }
                list.Add(row);
            }
            return new Dictionary<string, object>
            {
                ["solids"] = list,
                ["total_volume"] = Acad.R(total),
                ["total_volume_m3"] = Math.Round(SolidLogic.ToCubicMetres(total), 6),
            };
        }

        private static int CountOf(System.Collections.IEnumerable items)
        {
            int n = 0;
            foreach (var _ in items) n++;
            return n;
        }

        // ================================================================= views

        private static bool InModelSpace() =>
            Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE")) == 1 || Convert.ToInt32(AcApp.GetSystemVariable("CVPORT")) > 1;

        /// <summary>The preset of 'view' or the custom 'direction' (default: sw_iso).</summary>
        internal static ViewPreset ViewOf(Params p, string def)
        {
            if (p.Has("direction")) return SolidLogic.Custom(p.Point("direction"), p.Tool);
            if (p.Has("view") || def != null) return SolidLogic.Preset(p.Has("view") ? p.Str("view") : def, p.Tool);
            return null;
        }

        /// <summary>
        /// Points the current model-space view along v and frames min–max (null = keep the target and size).
        /// No undo step (like ZoomTo).
        /// </summary>
        internal static void ApplyView3d(Document doc, ViewPreset v, Point3d? min, Point3d? max)
        {
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (new UndoSuppressor(doc.Database, true))
            using (var view = ed.GetCurrentView())
            {
                view.ViewDirection = Acad.V3(v.Direction);
                view.ViewTwist = 0;
                view.PerspectiveEnabled = false;
                if (min.HasValue && max.HasValue)
                {
                    var a = min.Value; var b = max.Value;
                    var (w, h, c) = SolidLogic.ProjectedSize(new[] { a.X, a.Y, a.Z }, new[] { b.X, b.Y, b.Z }, v);
                    w = Math.Max(w, 1) * 1.1; h = Math.Max(h, 1) * 1.1;
                    view.Target = P(c);
                    view.CenterPoint = new Point2d(0, 0);
                    double ratio = view.Width / Math.Max(view.Height, 1e-9);
                    if (w / h > ratio) { view.Width = w; view.Height = w / ratio; }
                    else { view.Height = h; view.Width = h * ratio; }
                }
                ed.SetCurrentView(view);
            }
        }

        private static object SetView3d(ToolContext ctx, Params p)
        {
            if (!InModelSpace()) throw ToolException.Invalid("set_view_3d: the Model tab is not active. Switch to Model with set_current_layout {\"name\": \"Model\"} first.");
            var v = ViewOf(p, "sw_iso");
            Point3d? min = null, max = null;
            if (p.Bool("zoom", true))
            {
                var ext = p.Has("handles") ? HandlesExtents(ctx, p.Handles("handles", 5000)) : Capture.ModelExtents(ctx.Doc);
                if (ext.HasValue) { min = ext.Value.MinPoint; max = ext.Value.MaxPoint; }
            }
            ApplyView3d(ctx.Doc, v, min, max);
            return new Dictionary<string, object> { ["ok"] = true, ["view"] = v.Name, ["direction"] = v.Direction.Select(x => (object)Acad.R(x)).ToList() };
        }

        internal static Extents3d? HandlesExtents(ToolContext ctx, IEnumerable<string> handles)
        {
            Extents3d? ext = null;
            using (var tr = ctx.Doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles)
                {
                    ObjectId id;
                    try { id = Acad.IdFromHandle(ctx.Db, h); } catch (ToolException) { continue; }
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    var x = EntityInfo.Extents(e);
                    if (x.HasValue) { if (ext.HasValue) { var t = ext.Value; t.AddExtents(x.Value); ext = t; } else ext = x; }
                }
                tr.Commit();
            }
            return ext;
        }

        /// <summary>The id of a visual style of the drawing by its dictionary name ("Conceptual"...).</summary>
        internal static ObjectId VisualStyleId(Database db, Transaction tr, string acadName, string tool)
        {
            var dict = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict)
                if (e.Key.Equals(acadName, StringComparison.OrdinalIgnoreCase)) return e.Value;
            throw ToolException.Invalid($"{tool}: this drawing has no visual style '{acadName}' (VISUALSTYLES lists them).");
        }

        /// <summary>Sets the visual style of the current model-space viewport. No undo step (a view setting).</summary>
        internal static void ApplyVisualStyle(Document doc, string acadName, string tool)
        {
            var ed = doc.Editor;
            var db = doc.Database;
            using (doc.LockDocument())
            using (new UndoSuppressor(db, true))
            {
                ed.UpdateTiledViewportsInDatabase();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var vsId = VisualStyleId(db, tr, acadName, tool);
                    var vp = tr.GetObject(ed.ActiveViewportId, OpenMode.ForWrite);
                    if (vp is ViewportTableRecord vtr) vtr.VisualStyleId = vsId;
                    else if (vp is Viewport pvp) pvp.VisualStyleId = vsId;
                    else throw ToolException.Invalid($"{tool}: no active viewport.");
                    tr.Commit();
                }
                ed.UpdateTiledViewportsFromDatabase();
            }
        }

        private static object SetVisualStyle(ToolContext ctx, Params p)
        {
            if (!InModelSpace()) throw ToolException.Invalid("set_visual_style: the Model tab is not active. Switch to Model with set_current_layout {\"name\": \"Model\"} first.");
            var name = SolidLogic.VisualStyleName(p.Str("style"));
            ApplyVisualStyle(ctx.Doc, name, "set_visual_style");
            return new Dictionary<string, object> { ["ok"] = true, ["style"] = name };
        }
    }
}
