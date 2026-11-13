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
    /// 3D phase 1 (phase 2 is Solid2Tools.cs): solid primitives, extrude / revolve of 2D profiles, booleans, 3D views, visual styles and the
    /// solid query. Every tool here is labelled ToolGroup.ThreeD (Core/Plans.cs). Z is up, units are mm (WCS).
    /// The AutoCAD-free math (presets, checks, expected volumes) is SolidLogic in Core/Solids.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterSolids(ToolDispatcher d)
        {
            // nâng cấp đợt A (13/11/2026): one tool for the six primitives; the old names stay registered as aliases so
            // macros recorded with them still run (Claude only sees draw_solid)
            d.Register("draw_solid", true, DrawSolid);
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

        /// <summary>
        /// Builds a solid that is not in the drawing yet; disposes it when the build throws. A DBObject outside the
        /// database must be disposed on the main thread: left to the garbage collector it can crash AutoCAD.
        /// </summary>
        internal static Solid3d Build(Action<Solid3d> build)
        {
            var s = new Solid3d();
            try { build(s); return s; }
            catch { s.Dispose(); throw; }
        }

        /// <summary>Appends a new solid (Acad.Add); disposes it when it could not be appended (locked layer...).</summary>
        private static ObjectId AddSolid(ToolContext ctx, Solid3d s, Params p)
        {
            try { return Acad.Add(ctx, s, p); }
            catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
        }

        /// <summary>radius, or diameter / 2; a clear error when neither is given.</summary>
        private static double RadiusOf(Params p)
        {
            if (p.Has("radius")) return p.Positive("radius");
            if (p.Has("diameter")) return p.Positive("diameter") / 2;
            throw ToolException.Invalid($"{p.Tool}: pass 'radius' (or 'diameter') in mm.");
        }

        /// <summary>Box with its corner (min x, y, z before rotation) at corner, or centred on center; rotation about Z (degrees).</summary>
        internal static Solid3d MakeBox(double length, double width, double height, double[] corner, double[] center, double rotationDeg)
        {
            SolidLogic.CheckBox(length, width, height);
            var c = center ?? SolidLogic.BoxCenter(corner, length, width, height, rotationDeg);
            return Build(s =>
            {
                s.CreateBox(length, width, height); // centroid at the origin
                s.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin) * Matrix3d.Rotation(Geometry.DegToRad(rotationDeg), Vector3d.ZAxis, Point3d.Origin));
            });
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
            return Build(s =>
            {
                s.CreateFrustum(h, radius, radius, topRadius);
                PlaceOnAxis(s, h / 2, basePt, axis, 0);
            });
        }

        /// <summary>draw_solid: shape = box / cylinder / sphere / wedge / torus / pyramid, then the parameters of that shape.</summary>
        private static object DrawSolid(ToolContext ctx, Params p)
        {
            var shape = SolidLogic.NormalizeShape(p.Has("shape") ? p.Str("shape") : null);
            SolidLogic.CheckShapeParams(shape, p.Keys);
            switch (shape)
            {
                case "box": return DrawBox(ctx, p);
                case "cylinder": return DrawCylinder(ctx, p);
                case "sphere": return DrawSphere(ctx, p);
                case "wedge": return DrawWedge(ctx, p);
                case "torus": return DrawTorus(ctx, p);
                default: return DrawPyramid(ctx, p);
            }
        }

        private static object DrawBox(ToolContext ctx, Params p)
        {
            double l = p.Positive("length"), w = p.Positive("width"), h = p.Num("height");
            double[] corner = p.PointOpt("corner"), center = p.PointOpt("center");
            if (corner == null && center == null) corner = new double[] { 0, 0, 0 };
            if (corner != null && center != null) throw ToolException.Invalid($"{p.Tool}: pass either 'corner' or 'center', not both.");
            // a negative height builds the box downwards from the corner
            if (h < 0 && corner != null) { corner = new[] { corner[0], corner[1], SolidLogic.Z3(corner) + h }; h = -h; }
            var s = MakeBox(l, w, h, corner, center, p.Num("rotation", 0));
            return SolidResult(ctx, s, p);
        }

        private static object DrawCylinder(ToolContext ctx, Params p)
        {
            var (b, axis) = AxisOf(p);
            double r = RadiusOf(p);
            double top = p.Has("top_radius") ? p.Num("top_radius") : r;
            return SolidResult(ctx, MakeFrustum(b, axis, r, top), p);
        }

        private static object DrawSphere(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            double r = RadiusOf(p);
            var s = Build(x =>
            {
                x.CreateSphere(r);
                x.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin));
            });
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
            var c = SolidLogic.BoxCenter(corner, l, w, h, rot);
            var s = Build(x =>
            {
                x.CreateWedge(l, w, h);
                // bounding box centre to the origin; the centroid of a right wedge then sits at (±l/6, 0, ±h/6)
                Recentre(x);
                var cg = x.MassProperties.Centroid;
                // right angle on top (upside down): turn it over about X (keeps the slope along X)
                if (cg.Z > 1e-6 * h) { x.TransformBy(Matrix3d.Rotation(Math.PI, Vector3d.XAxis, Point3d.Origin)); cg = x.MassProperties.Centroid; }
                // tall face to -X
                if (cg.X > 1e-6 * l) { x.TransformBy(Matrix3d.Rotation(Math.PI, Vector3d.ZAxis, Point3d.Origin)); cg = x.MassProperties.Centroid; }
                if (!SolidLogic.WedgeCentroidOk(new[] { cg.X, cg.Y, cg.Z }, l, w, h))
                    throw new ToolException(ErrorCodes.AcadError,
                        $"{p.Tool}: this AutoCAD built the wedge in an unexpected orientation (centroid {cg.X:0.#}, {cg.Y:0.#}, {cg.Z:0.#}); nothing was drawn. Build it with extrude of a triangle profile instead.");
                x.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin) * Matrix3d.Rotation(Geometry.DegToRad(rot), Vector3d.ZAxis, Point3d.Origin));
            });
            return SolidResult(ctx, s, p);
        }

        /// <summary>Moves a solid so the centre of its bounding box is the origin.</summary>
        private static void Recentre(Solid3d s)
        {
            var e = s.GeometricExtents;
            var mid = new Point3d((e.MinPoint.X + e.MaxPoint.X) / 2, (e.MinPoint.Y + e.MaxPoint.Y) / 2, (e.MinPoint.Z + e.MaxPoint.Z) / 2);
            s.TransformBy(Matrix3d.Displacement(Point3d.Origin - mid));
        }

        private static object DrawTorus(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            double R = p.Positive("radius"), r = p.Positive("tube_radius");
            SolidLogic.CheckTorus(R, r);
            var s = Build(x =>
            {
                x.CreateTorus(R, r);
                x.TransformBy(Matrix3d.Displacement(P(c) - Point3d.Origin));
            });
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
            double rot = p.Num("rotation", 0);
            var s = Build(x =>
            {
                x.CreatePyramid(h, sides, r, top);
                PlaceOnAxis(x, h / 2, b, axis, rot);
            });
            return SolidResult(ctx, s, p);
        }

        /// <summary>Appends a new solid and returns its handle, volume and bounding box (so Claude can check it at once).</summary>
        private static object SolidResult(ToolContext ctx, Solid3d s, Params p)
        {
            var id = AddSolid(ctx, s, p);
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
            if (!p.Has("profile_points") && !p.Has("profile_handles"))
                throw ToolException.Invalid($"{p.Tool}: pass the profile as 'profile_points' (a closed polygon) or 'profile_handles' (closed curves / regions).");
            var regions = new List<Region>();
            try
            {
                if (p.Has("profile_points"))
                {
                    var pts = p.Points("profile_points", 3, 5000);
                    var bulges = p.NumbersOpt("profile_bulges");
                    var pl = Acad.MakePolyline(pts, true, bulges);
                    temp.Add(pl);
                    curves.Add(pl);
                    SamplePoints(pl, vertices);
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
                        SamplePoints(c, vertices);
                    }
                }
                regions.AddRange(temp.OfType<Region>());
                if (curves.Count > 0)
                {
                    DBObjectCollection made;
                    try { made = Region.CreateFromCurves(curves); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        throw ToolException.Invalid($"{p.Tool}: the profile is not a closed, flat, non-self-intersecting loop ({ex.ErrorStatus}). Close the polyline, join the pieces, or draw it again with profile_points.");
                    }
                    foreach (DBObject o in made) if (o is Region r) regions.Add(r); else o.Dispose();
                }
                if (regions.Count == 0) throw ToolException.Invalid($"{p.Tool}: no closed area found in the profile. Use closed curves (closed polyline, circle...).");
            }
            catch
            {
                foreach (var r in regions) if (!temp.Contains(r)) r.Dispose();
                foreach (var t in temp) t.Dispose();
                throw;
            }
            foreach (var t in temp) if (!(t is Region)) t.Dispose();
            return regions;
        }

        /// <summary>
        /// Points along a profile curve for the revolve axis check: the vertices plus points inside every segment, so
        /// a bulge, an arc, a circle or a spline that bulges over the axis is caught too (KT-30).
        /// </summary>
        private static void SamplePoints(Curve c, List<double[]> outPoints)
        {
            try
            {
                if (c is Polyline poly)
                    for (int i = 0; i < poly.NumberOfVertices; i++) { var v = poly.GetPoint3dAt(i); outPoints.Add(new[] { v.X, v.Y, v.Z }); }
                double a = c.StartParam, b = c.EndParam;
                const int n = 96;
                for (int i = 0; i <= n; i++)
                {
                    var pt = c.GetPointAtParameter(a + (b - a) * i / n);
                    outPoints.Add(new[] { pt.X, pt.Y, pt.Z });
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // a curve that cannot be evaluated: fall back to its end points, AutoCAD still checks the revolve itself
                try { outPoints.Add(new[] { c.StartPoint.X, c.StartPoint.Y, c.StartPoint.Z }); outPoints.Add(new[] { c.EndPoint.X, c.EndPoint.Y, c.EndPoint.Z }); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { }
            }
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
                    // a flat profile whose normal points down (clockwise lines / arcs, mirrored curves) would go down:
                    // positive height always means +Z for profiles in the XY plane
                    double hh = SolidLogic.ExtrudeHeightAlongNormal(h, reg.Normal.Z);
                    try { s.Extrude(reg, hh, Geometry.DegToRad(taper)); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        s.Dispose();
                        throw ToolException.Invalid($"extrude: AutoCAD could not extrude the profile ({ex.ErrorStatus}). A large taper_angle that makes the sides meet before the full height is a common cause: use a smaller angle or height.");
                    }
                    // like every new object: 'layer' / 'color' when given, else the current layer and ByLayer
                    // (not the profile's layer: the profile is often a construction line on another layer)
                    ids.Add(AddSolid(ctx, s, p));
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
                foreach (var reg in regions)
                {
                    // checked in the region's own plane (a profile drawn on a vertical plane too)
                    var crossing = SolidLogic.ProfileCrossesAxis(vertices, a, b, new[] { reg.Normal.X, reg.Normal.Y, reg.Normal.Z });
                    if (crossing != null) throw ToolException.Invalid(crossing);
                    var s = new Solid3d();
                    try { s.Revolve(reg, P(a), Acad.V3(dir), angle); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        s.Dispose();
                        throw ToolException.Invalid($"revolve: AutoCAD could not revolve the profile ({ex.ErrorStatus}). The axis must lie in the profile's plane and must not cross the profile.");
                    }
                    ids.Add(AddSolid(ctx, s, p));
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

        /// <summary>BooleanInto for the tools: an AutoCAD modelling failure becomes an error Claude can act on (nothing is changed).</summary>
        private static void BooleanChecked(Solid3d target, Solid3d tool, BooleanOperationType op, string toolName, string targetHandle, string otherHandle)
        {
            try { BooleanInto(target, tool, op); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw ToolException.Invalid($"{toolName}: AutoCAD could not combine {targetHandle} with {otherHandle} ({ex.ErrorStatus}). " +
                    "Nothing was changed. Faces that only touch or nearly coincide are a common cause: make the cutting solid a little bigger (e.g. 10 mm through the wall) and retry.");
            }
        }

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
                BooleanChecked(target, other, op, p.Tool, hs[0], h);
                if (!other.IsErased) other.Erase();
                erased.Add(h);
                // KT-28: an empty intersection after any step (not only the last) is "no overlap", not a modelling error
                if (op == BooleanOperationType.BoolIntersect && IsEmpty(target))
                    throw ToolException.Invalid(SolidLogic.EmptyIntersectionMessage(p.Tool, hs.TakeWhile(x => x != h).Concat(new[] { h }).ToList()));
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
                    using (var copy = (Solid3d)src.Clone()) BooleanChecked(target, copy, BooleanOperationType.BoolSubtract, "solid_subtract", targetHandle, h);
                }
                else
                {
                    var tool = SolidForWrite(ctx, h, "solid_subtract");
                    BooleanChecked(target, tool, BooleanOperationType.BoolSubtract, "solid_subtract", targetHandle, h);
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
            // 3D phase 2: the edge / face lists with indices for fillet_edges, chamfer_edges, shell_solid
            bool listEdges = p.Bool("edges", false), listFaces = p.Bool("faces", false);
            int maxItems = p.Int("max_items", Solid2Logic.DefaultListItems, 1, Solid2Logic.MaxListItems);
            var handles = p.Handles("handles", 200);
            if ((listEdges || listFaces) && handles.Count > 10)
                throw ToolException.Invalid("get_solid_info: list edges / faces of at most 10 solids per call (the lists are long).");
            foreach (var h in handles)
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
                    if (listEdges || listFaces)
                    {
                        try { AddTopologyLists(row, ReadTopology(s), listEdges, listFaces, maxItems); }
                        catch (Exception ex) when (!(ex is ToolException)) { row["topology_error"] = ex.Message; }
                    }
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
        internal static void ApplyView3d(Document doc, ViewPreset v, Point3d? min, Point3d? max, double twist = 0)
        {
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (new UndoSuppressor(doc.Database, true))
            using (var view = ed.GetCurrentView())
            {
                view.ViewDirection = Acad.V3(v.Direction);
                view.ViewTwist = twist;
                view.PerspectiveEnabled = false;
                if (min.HasValue && max.HasValue)
                {
                    var a = min.Value; var b = max.Value;
                    var (w, h, c) = SolidLogic.ProjectedSize(new[] { a.X, a.Y, a.Z }, new[] { b.X, b.Y, b.Z }, v);
                    w = Math.Max(w, 1) * 1.1; h = Math.Max(h, 1) * 1.1;
                    // a twisted view turns the framed box on screen: frame its diagonal both ways
                    if (Math.Abs(twist) > 1e-9) { double d = Math.Sqrt(w * w + h * h); w = d; h = d; }
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
                Extents3d? ext;
                if (p.Has("handles"))
                {
                    // KT-29: same answers as capture_view when the handles are wrong
                    ext = HandlesExtents(ctx, p.Handles("handles", 5000), out int found);
                    if (found == 0) throw new ToolException(ErrorCodes.EntityNotFound, "set_view_3d: none of the handles exists. Call list_entities to get current handles.");
                    if (!ext.HasValue) throw ToolException.Invalid("set_view_3d: the objects have no extents (empty text?). Pass other handles or leave 'handles' out to frame the whole model.");
                }
                else ext = Capture.ModelExtents(ctx.Doc);
                if (ext.HasValue) { min = ext.Value.MinPoint; max = ext.Value.MaxPoint; }
            }
            ApplyView3d(ctx.Doc, v, min, max);
            return new Dictionary<string, object> { ["ok"] = true, ["view"] = v.Name, ["direction"] = v.Direction.Select(x => (object)Acad.R(x)).ToList() };
        }

        internal static Extents3d? HandlesExtents(ToolContext ctx, IEnumerable<string> handles) => HandlesExtents(ctx, handles, out _);

        /// <summary>Extents of the objects that exist; found = how many of the handles name an existing entity.</summary>
        internal static Extents3d? HandlesExtents(ToolContext ctx, IEnumerable<string> handles, out int found)
        {
            Extents3d? ext = null;
            found = 0;
            using (var tr = ctx.Doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles)
                {
                    ObjectId id;
                    try { id = Acad.IdFromHandle(ctx.Db, h); } catch (ToolException) { continue; }
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    found++;
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
            var entries = new List<(string name, string type, bool internalUse)>();
            var ids = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            foreach (DBDictionaryEntry e in dict)
            {
                string type = null;
                bool internalUse = false;
                try
                {
                    if (tr.GetObject(e.Value, OpenMode.ForRead) is DBVisualStyle vs) { type = vs.Type.ToString(); internalUse = vs.InternalUseOnly; }
                }
                catch (Exception ex) { Logger.Warn($"visual style {e.Key} not read: {ex.Message}"); }
                entries.Add((e.Key, type, internalUse));
                ids[e.Key] = e.Value;
            }
            // by name, then by type (localized AutoCAD / templates name them in their own language)
            var name = SolidLogic.PickVisualStyle(entries, acadName);
            if (name != null && ids.TryGetValue(name, out var id)) return id;
            var shown = entries.Where(x => !x.internalUse).Select(x => x.name).Take(20);
            throw ToolException.Invalid($"{tool}: this drawing has no visual style '{acadName}'. Its styles: {string.Join(", ", shown)}.");
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
