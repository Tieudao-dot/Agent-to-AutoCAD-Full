using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.1 — geometry creation. Every handler returns {"handles": [...]}.</summary>
    public static partial class DrawingService
    {
        private static void RegisterDraw(ToolDispatcher d)
        {
            d.Register("draw_line", true, DrawLine);
            d.Register("draw_polyline", true, DrawPolyline);
            d.Register("draw_circle", true, DrawCircle);
            d.Register("draw_arc", true, DrawArc);
            d.Register("draw_ellipse", true, DrawEllipse);
            d.Register("draw_spline", true, DrawSpline);
            d.Register("draw_rectangle", true, DrawRectangle);
            d.Register("draw_polygon", true, DrawPolygon);
            d.Register("draw_point", true, DrawPoint);
            d.Register("draw_xline", true, (ctx, p) => DrawInfinite(ctx, p, false));
            d.Register("draw_ray", true, (ctx, p) => DrawInfinite(ctx, p, true));
            d.Register("draw_hatch", true, DrawHatch);
            d.Register("draw_wipeout", true, DrawWipeout);
            d.Register("draw_revcloud", true, DrawRevcloud);
        }

        private static object DrawLine(ToolContext ctx, Params p)
        {
            var s = p.Point("start");
            var e = p.Point("end");
            if (Geometry.Dist(s, e) < Geometry.Eps) throw ToolException.Invalid("draw_line: 'start' and 'end' are the same point.");
            return Acad.Handles(Acad.Add(ctx, new Line(Acad.P3(s), Acad.P3(e)), p));
        }

        private static object DrawPolyline(ToolContext ctx, Params p)
        {
            var verts = p.Points("vertices", 2);
            bool closed = p.Bool("closed", false);
            var bulges = p.NumbersOpt("bulges");
            if (bulges != null && bulges.Count != verts.Count && bulges.Count != verts.Count - 1)
                throw ToolException.Invalid($"draw_polyline: 'bulges' must have one value per segment ({verts.Count - 1} open / {verts.Count} closed), got {bulges.Count}.");
            double width = p.Num("width", 0);
            if (width < 0) throw ToolException.Invalid("draw_polyline: 'width' must be >= 0.");
            var pl = Acad.MakePolyline(verts, closed, bulges, width);
            return Acad.Handles(Acad.Add(ctx, pl, p));
        }

        private static object DrawCircle(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            double r = p.Has("radius") ? p.Positive("radius") : p.Positive("diameter") / 2;
            return Acad.Handles(Acad.Add(ctx, new Circle(Acad.P3(c), Vector3d.ZAxis, r), p));
        }

        private static object DrawArc(ToolContext ctx, Params p)
        {
            Arc arc;
            if (p.Has("p1") || p.Has("p2") || p.Has("p3"))
            {
                var (center, radius, start, end) = Geometry.ArcFrom3Points(p.Point("p1"), p.Point("p2"), p.Point("p3"));
                arc = new Arc(Acad.P3(center), Vector3d.ZAxis, radius, start, end);
            }
            else
            {
                var c = p.Point("center");
                double r = p.Positive("radius");
                double a1 = Geometry.DegToRad(p.Num("start_angle"));
                double a2 = Geometry.DegToRad(p.Num("end_angle"));
                if (Math.Abs(Geometry.NormalizeRad(a2 - a1)) < Geometry.Eps)
                    throw ToolException.Invalid("draw_arc: start_angle and end_angle are equal; use draw_circle for a full circle.");
                arc = new Arc(Acad.P3(c), Vector3d.ZAxis, r, a1, a2);
            }
            return Acad.Handles(Acad.Add(ctx, arc, p));
        }

        private static object DrawEllipse(ToolContext ctx, Params p)
        {
            var c = p.Point("center");
            var end = p.Point("major_axis_end");
            double ratio = p.Positive("ratio");
            if (ratio > 1) throw ToolException.Invalid("draw_ellipse: 'ratio' (minor/major) must be in (0, 1]. Swap the axes if the minor axis is longer.");
            var major = new Vector3d(end[0] - c[0], end[1] - c[1], 0);
            if (major.Length < Geometry.Eps) throw ToolException.Invalid("draw_ellipse: 'major_axis_end' must differ from 'center'.");
            double a1 = Geometry.DegToRad(p.Num("start_angle", 0));
            double a2 = Geometry.DegToRad(p.Num("end_angle", 360));
            var el = new Ellipse(Acad.P3(c), Vector3d.ZAxis, major, ratio, a1, a2);
            return Acad.Handles(Acad.Add(ctx, el, p));
        }

        private static object DrawSpline(ToolContext ctx, Params p)
        {
            var pts = p.Points("fit_points", 2);
            if (p.Bool("closed", false) && Geometry.Dist(pts[0], pts[pts.Count - 1]) > Geometry.Eps) pts.Add(pts[0]);
            var col = new Point3dCollection();
            foreach (var pt in pts) col.Add(Acad.P3(pt));
            var sp = new Spline(col, 4, 0.0);
            return Acad.Handles(Acad.Add(ctx, sp, p));
        }

        private static object DrawRectangle(ToolContext ctx, Params p)
        {
            List<double[]> verts;
            if (p.Has("corner1") || p.Has("corner2"))
                verts = Geometry.Rectangle(p.Point("corner1"), p.Point("corner2"));
            else
                verts = Geometry.Rectangle(p.Point("corner"), p.Num("width"), p.Num("height"), p.Num("rotation", 0));
            var pl = Acad.MakePolyline(verts, true, null, p.Num("width_line", 0));
            return Acad.Handles(Acad.Add(ctx, pl, p));
        }

        private static object DrawPolygon(ToolContext ctx, Params p)
        {
            var verts = Geometry.RegularPolygon(p.Point("center"), p.Int("sides"), p.Positive("radius"),
                p.Bool("inscribed", true), p.Num("rotation", 0));
            return Acad.Handles(Acad.Add(ctx, Acad.MakePolyline(verts, true), p));
        }

        private static object DrawPoint(ToolContext ctx, Params p)
        {
            if (p.Has("positions"))
            {
                var ids = p.Points("positions", 1, 5000).Select(pt => Acad.Add(ctx, new DBPoint(Acad.P3(pt)), p)).ToList();
                return Acad.Handles(ids);
            }
            return Acad.Handles(Acad.Add(ctx, new DBPoint(Acad.P3(p.Point("position"))), p));
        }

        private static object DrawInfinite(ToolContext ctx, Params p, bool ray)
        {
            var b = p.Point("base");
            double[] dirVec = null;
            if (p.Has("direction")) dirVec = p.Point("direction");
            else if (p.Has("through")) { var t = p.Point("through"); dirVec = new[] { t[0] - b[0], t[1] - b[1], t[2] - b[2] }; }
            var dir = Geometry.Direction(dirVec, p.NumOpt("angle"));
            Curve c;
            if (ray) c = new Ray { BasePoint = Acad.P3(b), UnitDir = Acad.V3(dir) };
            else c = new Xline { BasePoint = Acad.P3(b), UnitDir = Acad.V3(dir) };
            return Acad.Handles(Acad.Add(ctx, c, p));
        }

        private static object DrawHatch(ToolContext ctx, Params p)
        {
            string pattern = p.Str("pattern", "SOLID").Trim().ToUpperInvariant();
            double scale = p.Positive("scale", 1);
            double angle = Geometry.DegToRad(p.Num("angle", 0));
            var handles = p.Has("boundary_handles") ? p.Handles("boundary_handles") : null;
            // nâng cấp đợt C: pick an internal point (like HATCH > Pick points); the boundary is traced around it
            bool pick = handles == null && p.Has("pick_point");
            var points = handles == null && !pick ? p.Points("boundary_points", 3) : null;
            var seed = pick ? p.Point("pick_point") : null;

            var hatch = new Hatch();
            var id = Acad.Add(ctx, hatch, p); // must be database-resident before loops are added
            hatch.Normal = Vector3d.ZAxis;
            hatch.Elevation = 0;
            hatch.PatternScale = scale;
            hatch.PatternAngle = angle;
            try
            {
                hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                throw ToolException.Invalid($"draw_hatch: unknown pattern '{pattern}'. Use SOLID or a standard acadiso.pat name (ANSI31, ANSI32, AR-CONC, AR-SAND, AR-BRSTD, GRAVEL, NET, DOTS...).");
            }

            if (pick)
            {
                // the traced loops become boundary objects of the hatch; without keep_boundary they are erased again
                // (a non-associative hatch, like HATCH with "Retain boundaries" off)
                bool keep = p.Bool("keep_boundary", false);
                hatch.Associative = keep && p.Bool("associative", true);
                DBObjectCollection traced = null;
                try { traced = ctx.Ed.TraceBoundary(new Point3d(seed[0], seed[1], 0), p.Bool("islands", true)); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { traced = null; }
                var loops = new List<Curve>();
                if (traced != null)
                    foreach (DBObject o in traced)
                    {
                        if (o is Curve cv) loops.Add(cv);
                        else o.Dispose(); // never leave a non-database object to the finalizer
                    }
                if (loops.Count == 0) throw ToolException.Invalid(Util2DLogic.NoBoundaryMessage(seed));
                var areas = loops.Select(c => { try { return c.Area; } catch (Autodesk.AutoCAD.Runtime.Exception) { return 0.0; } }).ToList();
                int outer = Util2DLogic.OuterLoopIndex(areas);
                var boundaryIds = new List<ObjectId>();
                try
                {
                    for (int i = 0; i < loops.Count; i++) boundaryIds.Add(Acad.Add(ctx, loops[i], p));
                }
                catch
                {
                    for (int i = boundaryIds.Count; i < loops.Count; i++) loops[i].Dispose();
                    throw;
                }
                hatch.AppendLoop(HatchLoopTypes.Outermost | HatchLoopTypes.External, new ObjectIdCollection { boundaryIds[outer] });
                for (int i = 0; i < loops.Count; i++)
                    if (i != outer) hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { boundaryIds[i] });
                hatch.EvaluateHatch(true);
                // kept boundaries are new objects of this turn too (the hatch first)
                var res = keep ? Acad.Handles(new[] { id }.Concat(boundaryIds)) : Acad.Handles(id);
                res["islands"] = loops.Count - 1;
                res["area"] = Acad.R(EntityInfo.SafeArea(hatch));
                if (!keep) foreach (var b in boundaryIds) ((Entity)ctx.Tr.GetObject(b, OpenMode.ForWrite)).Erase();
                return res;
            }

            if (handles != null)
            {
                hatch.Associative = p.Bool("associative", true);
                foreach (var h in handles)
                {
                    var bid = Acad.IdFromHandle(ctx.Db, h);
                    var ent = ctx.Tr.GetObject(bid, OpenMode.ForRead) as Curve;
                    if (ent == null || !ent.Closed && !(ent is Circle) && !(ent is Ellipse))
                        throw ToolException.Invalid($"draw_hatch: boundary {h} must be a closed curve (closed polyline, circle, ellipse, closed spline).");
                    hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { bid });
                }
            }
            else
            {
                var bulges = p.NumbersOpt("boundary_bulges");
                var pts2 = new Point2dCollection();
                var bs = new DoubleCollection();
                for (int i = 0; i < points.Count; i++)
                {
                    pts2.Add(Acad.P2(points[i]));
                    bs.Add(bulges != null && i < bulges.Count ? bulges[i] : 0);
                }
                if (Geometry.Dist(points[0], points[points.Count - 1]) > Geometry.Eps)
                {
                    pts2.Add(Acad.P2(points[0]));
                    bs.Add(0);
                }
                hatch.AppendLoop(HatchLoopTypes.Polyline, pts2, bs);
            }
            hatch.EvaluateHatch(true);
            return Acad.Handles(id);
        }

        private static object DrawWipeout(ToolContext ctx, Params p)
        {
            var pts = p.Points("points", 3);
            var col = new Point2dCollection();
            foreach (var pt in pts) col.Add(Acad.P2(pt));
            if (Geometry.Dist(pts[0], pts[pts.Count - 1]) > Geometry.Eps) col.Add(Acad.P2(pts[0]));
            var w = new Wipeout();
            w.SetDatabaseDefaults(ctx.Db);
            w.SetFrom(col, Vector3d.ZAxis);
            return Acad.Handles(Acad.Add(ctx, w, p, defaults: false));
        }

        private static object DrawRevcloud(ToolContext ctx, Params p)
        {
            var poly = p.Points("points", 3);
            double arc = p.Positive("arc_length", 500);
            var (verts, bulges) = Geometry.RevCloud(poly, arc);
            var pl = Acad.MakePolyline(verts, true, bulges);
            return Acad.Handles(Acad.Add(ctx, pl, p));
        }
    }
}
