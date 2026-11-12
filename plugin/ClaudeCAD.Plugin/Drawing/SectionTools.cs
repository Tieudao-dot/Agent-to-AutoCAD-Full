using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;
using BrEdge = Autodesk.AutoCAD.BoundaryRepresentation.Edge;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// 3D phase 3: 2D sections and projections of 3D solids, made with the .NET API only (no AutoCAD command):
    /// section_view cuts with Solid3d.GetSection (outline + hatch) and, on request, adds the lines seen behind the
    /// cut (Section.GenerateSectionGeometry, background only); projection_view makes an elevation / plan view
    /// (Section background from outside the model = hidden lines removed by AutoCAD; fallback: every edge projected).
    /// The result is new 2D lines / hatch on their own layers, placed beside the model (never on it). The solids are
    /// only read. Labelled ToolGroup.ThreeD. The math (frame, placement, loops) is SectionLogic in Core/FloorPlan.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterSections(ToolDispatcher d)
        {
            d.Register("section_view", true, SectionView);
            d.Register("projection_view", true, ProjectionView);
        }

        private static List<(string handle, Solid3d solid)> SolidsForView(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", SectionLogic.MaxSolids).Select(h => h.ToUpperInvariant()).Distinct().ToList();
            var list = new List<(string, Solid3d)>();
            foreach (var h in hs)
            {
                var e = Acad.GetEntity(ctx, h, false);
                if (!(e is Solid3d s)) throw ToolException.Invalid($"{p.Tool}: {h} is a {Acad.TypeName(e)}, not a 3D solid (list_entities with type 3DSOLID finds them).");
                list.Add((h, s));
            }
            return list;
        }

        /// <summary>xy box of everything in model space (the 2D result is placed outside it).</summary>
        private static (double[] min, double[] max)? ModelBox(ToolContext ctx)
        {
            Extents3d? all = null;
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e) || e.IsErased || e is Section) continue;
                var x = EntityInfo.Extents(e);
                if (!x.HasValue) continue;
                if (all.HasValue) { var t = all.Value; t.AddExtents(x.Value); all = t; } else all = x;
            }
            if (!all.HasValue) return null;
            return (new[] { all.Value.MinPoint.X, all.Value.MinPoint.Y }, new[] { all.Value.MaxPoint.X, all.Value.MaxPoint.Y });
        }

        private static Matrix3d FrameMatrix(PlaneDef pl, double[] u, double[] v) =>
            Matrix3d.AlignCoordinateSystem(P(pl.Point), Acad.V3(u), Acad.V3(v), Acad.V3(pl.Normal),
                Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);

        private static void DisposeAll(IEnumerable<DBObject> objs)
        {
            foreach (var o in objs) if (o != null && o.ObjectId.IsNull && !o.IsDisposed) o.Dispose();
        }

        /// <summary>Explodes a region (and the regions it explodes into) into curves.</summary>
        private static void ExplodeRegion(Region r, List<Curve> curves, List<string> notes)
        {
            var parts = new DBObjectCollection();
            r.Explode(parts);
            foreach (DBObject o in parts)
            {
                if (o is Region sub) { using (sub) ExplodeRegion(sub, curves, notes); }
                else if (o is Curve c) curves.Add(c);
                else o.Dispose();
            }
        }

        /// <summary>
        /// The lines a viewer sees behind the plane (looking along −normal), from AutoCAD's section generator with
        /// only the background on and hidden lines off. Returned in the 2D frame (projected onto the plane, then
        /// matrix m), not in the drawing. Any AutoCAD failure → empty list and a note; the cut is not affected.
        /// The Section object is added to the drawing only for its settings and erased in the same transaction.
        /// </summary>
        private static List<Curve> SectionBackground(ToolContext ctx, IList<(string handle, Solid3d solid)> solids, PlaneDef pl, double[] u, double[] v, Matrix3d m, Params layerP, List<string> notes)
        {
            var result = new List<Curve>();
            var raw = new List<Entity>();
            Section sec = null;
            try
            {
                var p0 = P(pl.Point);
                var pts = new Point3dCollection { p0, p0 + Acad.V3(u) * 1000 };
                var n = Acad.V3(pl.Normal);
                sec = new Section(pts, Acad.V3(v), -n);
                Acad.Add(ctx, sec, layerP);
                sec.State = SectionState.Plane;
                sec.ViewingDirection = -n;
                if (sec.Settings.IsNull) throw new InvalidOperationException("the section has no settings object");
                var ss = (SectionSettings)ctx.Tr.GetObject(sec.Settings, OpenMode.ForWrite);
                var t = SectionType.Section2d;
                ss.CurrentSectionType = t;
                ss.SetVisibility(t, SectionGeometry.IntersectionBoundary, false);
                ss.SetVisibility(t, SectionGeometry.IntersectionFill, false);
                ss.SetVisibility(t, SectionGeometry.BackgroundGeometry, true);
                ss.SetHiddenLine(t, SectionGeometry.BackgroundGeometry, false);
                ss.SetVisibility(t, SectionGeometry.ForegroundGeometry, false);
                ss.SetVisibility(t, SectionGeometry.CurveTangencyLines, false);
                foreach (var (_, s) in solids)
                {
                    // the managed parameter names are shifted against ObjectARX (boundary, fill, background,
                    // foreground, tangency): only the background is visible, so every array is taken the same way
                    sec.GenerateSectionGeometry(s, out Array a1, out Array a2, out Array a3, out Array a4, out Array a5);
                    foreach (var arr in new[] { a1, a2, a3, a4, a5 })
                        if (arr != null) foreach (var o in arr) if (o is Entity e) raw.Add(e);
                }
                using (var plane = AcPlane(pl))
                {
                    double tol = 1e-6 * Math.Max(1, SolidsSize(solids));
                    int dropped = 0, offPlane = 0;
                    foreach (var e in raw)
                    {
                        if (!(e is Curve c) || e is Hatch) { dropped++; continue; }
                        var ext = EntityInfo.Extents(c);
                        Curve flat = c;
                        if (ext.HasValue && (Math.Abs(plane.DistanceTo(ext.Value.MinPoint)) > tol || Math.Abs(plane.DistanceTo(ext.Value.MaxPoint)) > tol))
                        {
                            offPlane++;
                            try { flat = c.GetOrthoProjectedCurve(plane); }
                            catch (Autodesk.AutoCAD.Runtime.Exception) { dropped++; continue; }
                        }
                        flat.TransformBy(m);
                        result.Add(flat);
                    }
                    if (dropped > 0) notes.Add($"{dropped} behind piece(s) that are not curves were left out.");
                    if (offPlane > 0) notes.Add($"{offPlane} behind piece(s) came back in 3D and were projected onto the plane.");
                }
            }
            catch (Exception ex) when (!(ex is FatalToolException))
            {
                DisposeAll(result);
                result.Clear();
                notes.Add($"The lines behind the plane could not be made ({(ex is Autodesk.AutoCAD.Runtime.Exception ae ? ae.ErrorStatus.ToString() : ex.Message)}); the result has the cut only.");
            }
            finally
            {
                foreach (var e in raw) if (!result.Contains(e as Curve) && e.ObjectId.IsNull && !e.IsDisposed) e.Dispose();
                if (sec != null)
                {
                    if (!sec.ObjectId.IsNull) { if (!sec.IsErased) sec.Erase(); }
                    else sec.Dispose();
                }
            }
            return result;
        }

        private static double SolidsSize(IEnumerable<(string handle, Solid3d solid)> solids)
        {
            double size = 0;
            foreach (var (_, s) in solids)
            {
                var x = EntityInfo.Extents(s);
                if (x.HasValue) size = Math.Max(size, (x.Value.MaxPoint - x.Value.MinPoint).Length);
            }
            return size;
        }

        /// <summary>Every Brep edge of the solids projected onto the plane (no hidden-line removal), in the 2D frame.</summary>
        private static List<Curve> ProjectedEdges(IList<(string handle, Solid3d solid)> solids, PlaneDef pl, Matrix3d m, List<string> notes)
        {
            var result = new List<Curve>();
            var seen = new HashSet<string>();
            int failed = 0;
            using (var plane = AcPlane(pl))
            {
                foreach (var (_, s) in solids)
                {
                    using (var brep = new Brep(s))
                    {
                        foreach (BrEdge edge in brep.Edges)
                        {
                            using (edge)
                            {
                                Curve c = null, flat = null;
                                try
                                {
                                    var gc = edge.Curve;
                                    if (gc is ExternalCurve3d xc && xc.IsNativeCurve) gc = xc.NativeCurve;
                                    c = Curve.CreateFromGeCurve(gc);
                                    flat = c.GetOrthoProjectedCurve(plane);
                                    flat.TransformBy(m);
                                }
                                catch (Exception) { failed++; c?.Dispose(); flat?.Dispose(); continue; }
                                finally { if (c != null && !ReferenceEquals(c, flat) && !c.IsDisposed) c.Dispose(); }
                                double len;
                                try { len = flat.GetDistanceAtParameter(flat.EndParam) - flat.GetDistanceAtParameter(flat.StartParam); } catch (Exception) { len = 1; }
                                // an edge seen end-on is a point; an edge hidden exactly behind another is drawn once
                                string key = Key(flat);
                                if (Math.Abs(len) < 1e-6 || !seen.Add(key)) { flat.Dispose(); continue; }
                                result.Add(flat);
                            }
                        }
                    }
                }
            }
            if (failed > 0) notes.Add($"{failed} edge(s) could not be projected and were left out.");
            return result;
        }

        private static string Key(Curve c)
        {
            string P2s(Point3d p) => $"{Math.Round(p.X, 3)},{Math.Round(p.Y, 3)}";
            var a = P2s(c.StartPoint); var b = P2s(c.EndPoint);
            string mid = "";
            try { mid = P2s(c.GetPointAtParameter((c.StartParam + c.EndParam) / 2)); } catch (Exception) { }
            return c.GetType().Name + ":" + (string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a) + "|" + mid;
        }

        private static (double[] min, double[] max)? CurvesBox(IEnumerable<Curve> curves)
        {
            Extents3d? all = null;
            foreach (var c in curves)
            {
                var x = EntityInfo.Extents(c);
                if (!x.HasValue) continue;
                if (all.HasValue) { var t = all.Value; t.AddExtents(x.Value); all = t; } else all = x;
            }
            if (!all.HasValue) return null;
            return (new[] { all.Value.MinPoint.X, all.Value.MinPoint.Y }, new[] { all.Value.MaxPoint.X, all.Value.MaxPoint.Y });
        }

        /// <summary>Where the result goes: 'position' (lower-left corner) or beside the model; the displacement to get there.</summary>
        private static (Matrix3d move, double[] at, string warning) Placement(ToolContext ctx, Params p, (double[] min, double[] max) box, double mmPerUnit)
        {
            var model = ModelBox(ctx);
            double[] at;
            string warning = null;
            if (p.Has("position")) at = p.Point("position");
            else if (model.HasValue) at = SectionLogic.AutoPosition(model.Value.min, model.Value.max, 1000 / mmPerUnit);
            else at = new double[] { 0, 0 };
            var resMax = new[] { at[0] + box.max[0] - box.min[0], at[1] + box.max[1] - box.min[1] };
            if (p.Has("position") && model.HasValue && SectionLogic.CoversModel(at, resMax, model.Value.min, model.Value.max))
                warning = "The result overlaps the model area at this position; leave 'position' out to place it beside the model.";
            return (Matrix3d.Displacement(new Vector3d(at[0] - box.min[0], at[1] - box.min[1], 0)), at, warning);
        }

        private static ObjectId AddCurve(ToolContext ctx, Curve c, Params layerP)
        {
            try { return Acad.Add(ctx, c, layerP); }
            catch { if (c.ObjectId.IsNull) c.Dispose(); throw; }
        }

        /// <summary>A loop of lines / arcs as one closed polyline (vertices and bulges); null when it has other curves.</summary>
        private static (List<double[]> pts, List<double> bulges)? LoopAsPolyline(IList<Curve> curves, List<(int index, bool reversed)> loop)
        {
            var pts = new List<double[]>();
            var bulges = new List<double>();
            foreach (var (i, rev) in loop)
            {
                var c = curves[i];
                var start = rev ? c.EndPoint : c.StartPoint;
                pts.Add(new[] { start.X, start.Y });
                if (c is Line) bulges.Add(0);
                else if (c is Arc a)
                {
                    double b = Math.Tan(a.TotalAngle / 4) * (a.Normal.Z >= 0 ? 1 : -1);
                    bulges.Add(rev ? -b : b);
                }
                else return null;
            }
            return (pts, bulges);
        }

        internal static object SectionView(ToolContext ctx, Params p)
        {
            const string tool = "section_view";
            var solids = SolidsForView(ctx, p);
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            PlaneDef pl;
            if (p.Has("line"))
            {
                if (p.Has("plane_points") || p.Has("normal") || p.Has("plane")) throw ToolException.Invalid($"{tool}: give the plane one way: 'line' (a section line in plan) or plane_points / point + normal / point + plane.");
                var line = p.Points("line", 2, 2);
                pl = SectionLogic.PlaneFromLine(line[0], line[1], p.Bool("flip", false), tool);
            }
            else
            {
                pl = PlaneOf(p);
                if (p.Bool("flip", false)) pl = new PlaneDef { Point = pl.Point, Normal = SolidLogic.Scale(pl.Normal, -1) };
            }
            var (u, v) = SectionLogic.Frame(pl.Normal);
            var m = FrameMatrix(pl, u, v);
            string outlineLayer = p.Str("layer", SectionLogic.OutlineLayer), hatchLayer = p.Str("hatch_layer", SectionLogic.HatchLayer), behindLayer = p.Str("behind_layer", SectionLogic.BehindLayer);
            object hatchArg = p.Get("hatch");
            string pattern = hatchArg is bool hb ? (hb ? "ANSI31" : null) : hatchArg is string hs ? hs.Trim().ToUpperInvariant() : "ANSI31";
            double hatchScale = p.Positive("hatch_scale", SectionLogic.DefaultHatchScale);
            bool behind = p.Bool("show_behind", false);

            var notes = new List<string>();
            var cut = new List<Curve>();
            var behindCurves = new List<Curve>();
            var cutBy = new List<object>();
            double cutArea = 0;
            var layersCreated = new List<string>();
            try
            {
                using (var plane = AcPlane(pl))
                {
                    foreach (var (h, s) in solids)
                    {
                        Region reg;
                        try { reg = s.GetSection(plane); }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { reg = null; }
                        if (reg == null) continue;
                        using (reg)
                        {
                            double a = SafeArea(reg);
                            if (a <= 1e-9) continue;
                            reg.TransformBy(m);
                            cutArea += a;
                            cutBy.Add(h);
                            ExplodeRegion(reg, cut, notes);
                        }
                    }
                }
                if (behind)
                {
                    if (EnsureLayer(ctx, outlineLayer, 7)) layersCreated.Add(outlineLayer);
                    behindCurves = SectionBackground(ctx, solids, pl, u, v, m, LayerParams(tool, outlineLayer), notes);
                }
                if (cut.Count == 0 && behindCurves.Count == 0)
                    throw ToolException.Invalid($"{tool}: the plane does not cut any of the {solids.Count} solid(s). Nothing was drawn. Check the plane against get_solid_info min / max (a vertical cut at z between the floor and the wall top, or a section line across the walls).");
                var box = CurvesBox(cut.Concat(behindCurves)).Value;
                var (move, at, warning) = Placement(ctx, p, box, f);
                if (warning != null) notes.Add(warning);
                foreach (var c in cut.Concat(behindCurves)) c.TransformBy(move);
                if (cut.Count + behindCurves.Count > SectionLogic.MaxResultEntities)
                    throw ToolException.Invalid($"{tool}: the result would have {cut.Count + behindCurves.Count} pieces (more than {SectionLogic.MaxResultEntities}). Nothing was drawn. Cut fewer solids.");

                // outline: loops of lines / arcs → closed polylines; other curves as they are
                if (cut.Count > 0 && EnsureLayer(ctx, outlineLayer, 7)) layersCreated.Add(outlineLayer);
                var outP = LayerParams(tool, outlineLayer);
                double tol = 1e-6 * Math.Max(1, (box.max[0] - box.min[0]) + (box.max[1] - box.min[1]));
                var pieces = cut.Select((c, i) => new SectionLogic.Piece { A = new[] { c.StartPoint.X, c.StartPoint.Y }, B = new[] { c.EndPoint.X, c.EndPoint.Y }, Index = i }).ToList();
                var (loops, open) = SectionLogic.ChainLoops(pieces, Math.Max(tol, 1e-6));
                var outline = new List<string>();
                var hatchLoops = new List<(List<double[]> pts, List<double> bulges, ObjectIdCollection ids)>();
                var usedCut = new HashSet<int>();
                foreach (var loop in loops)
                {
                    var poly = LoopAsPolyline(cut, loop);
                    if (poly.HasValue)
                    {
                        var pline = Acad.MakePolyline(poly.Value.pts, true, poly.Value.bulges);
                        outline.Add(Acad.H(AddCurve(ctx, pline, outP)));
                        hatchLoops.Add((poly.Value.pts, poly.Value.bulges, null));
                        foreach (var (i, _) in loop) usedCut.Add(i);
                    }
                    else
                    {
                        var ids = new ObjectIdCollection();
                        foreach (var (i, _) in loop) { var id = AddCurve(ctx, cut[i], outP); ids.Add(id); outline.Add(Acad.H(id)); usedCut.Add(i); }
                        hatchLoops.Add((null, null, ids));
                    }
                }
                foreach (var i in open) { outline.Add(Acad.H(AddCurve(ctx, cut[i], outP))); usedCut.Add(i); }
                if (open.Count > 0) notes.Add($"{open.Count} cut piece(s) do not close into a loop; drawn, but not hatched.");
                // the loop pieces drawn as polylines are not needed any more
                foreach (var c in cut) if (c.ObjectId.IsNull && !c.IsDisposed) c.Dispose();

                var hatches = new List<string>();
                if (pattern != null && hatchLoops.Count > 0)
                {
                    if (EnsureLayer(ctx, hatchLayer, 8)) layersCreated.Add(hatchLayer);
                    var hatch = new Hatch();
                    var hid = Acad.Add(ctx, hatch, LayerParams(tool, hatchLayer));
                    try
                    {
                        hatch.Normal = Vector3d.ZAxis;
                        hatch.Elevation = 0;
                        hatch.PatternScale = hatchScale;
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
                        hatch.HatchStyle = HatchStyle.Normal; // nested loops (the inside of a ring) stay empty
                        foreach (var (pts, bulges, ids) in hatchLoops)
                        {
                            if (ids != null) { hatch.AppendLoop(HatchLoopTypes.Default, ids); continue; }
                            var p2 = new Point2dCollection();
                            var bs = new DoubleCollection();
                            for (int i = 0; i < pts.Count; i++) { p2.Add(new Point2d(pts[i][0], pts[i][1])); bs.Add(bulges[i]); }
                            p2.Add(new Point2d(pts[0][0], pts[0][1])); bs.Add(0);
                            hatch.AppendLoop(HatchLoopTypes.Polyline, p2, bs);
                        }
                        hatch.EvaluateHatch(true);
                        hatches.Add(Acad.H(hid));
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        hatch.Erase();
                        notes.Add($"The hatch could not be made ({ex.ErrorStatus}{(ex.ErrorStatus.ToString().Contains("Pattern") ? $": unknown pattern {pattern}, use ANSI31, ANSI37, AR-CONC, SOLID…" : "")}); the outline is drawn.");
                    }
                }

                var behindHandles = new List<string>();
                if (behindCurves.Count > 0)
                {
                    if (EnsureLayer(ctx, behindLayer, 9)) layersCreated.Add(behindLayer);
                    var bp = LayerParams(tool, behindLayer);
                    foreach (var c in behindCurves) behindHandles.Add(Acad.H(AddCurve(ctx, c, bp)));
                }
                else if (behind && notes.All(n => !n.StartsWith("The lines behind"))) notes.Add("Nothing is visible behind the plane.");

                var all = outline.Concat(hatches).Concat(behindHandles).Cast<object>().ToList();
                var r = new Dictionary<string, object>
                {
                    ["handles"] = all,
                    ["outline_handles"] = outline,
                    ["hatch_handles"] = hatches,
                    ["behind_handles"] = behindHandles,
                    ["cut_solids"] = cutBy,
                    ["cut_area_m2"] = Math.Round(cutArea * f * f / 1e6, 6),
                    ["loops"] = loops.Count,
                    ["position"] = new List<object> { Acad.R(at[0]), Acad.R(at[1]) },
                    ["size"] = new List<object> { Acad.R(box.max[0] - box.min[0]), Acad.R(box.max[1] - box.min[1]) },
                    ["frame"] = FrameInfo(pl, u, v, at, box),
                };
                if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
                if (notes.Count > 0) r["notes"] = notes;
                return r;
            }
            catch
            {
                DisposeAll(cut);
                DisposeAll(behindCurves);
                throw;
            }
        }

        /// <summary>How a model point maps onto the drawn result (so Claude can find a wall in the section).</summary>
        private static Dictionary<string, object> FrameInfo(PlaneDef pl, double[] u, double[] v, double[] at, (double[] min, double[] max) box) =>
            new Dictionary<string, object>
            {
                ["plane_point"] = pl.Point.Select(x => (object)Acad.R(x)).ToList(),
                ["normal"] = pl.Normal.Select(x => (object)Acad.R(x)).ToList(),
                ["right"] = u.Select(x => (object)Acad.R(x)).ToList(),
                ["up"] = v.Select(x => (object)Acad.R(x)).ToList(),
                ["rule"] = $"model point P is drawn at [{Acad.R(at[0] - box.min[0])} + (P − plane_point)·right, {Acad.R(at[1] - box.min[1])} + (P − plane_point)·up]",
            };

        private static object ProjectionView(ToolContext ctx, Params p)
        {
            const string tool = "projection_view";
            var solids = SolidsForView(ctx, p);
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var view = ViewOf(p, "front");
            string method = (p.Str("method", "auto") ?? "auto").Trim().ToLowerInvariant();
            if (method != "auto" && method != "edges") throw ToolException.Invalid($"{tool}: method must be \"auto\" (hidden lines removed by AutoCAD, edges if that fails) or \"edges\" (every edge, also hidden ones).");
            Extents3d? ext = null;
            foreach (var (_, s) in solids)
            {
                var x = EntityInfo.Extents(s);
                if (!x.HasValue) continue;
                if (ext.HasValue) { var t = ext.Value; t.AddExtents(x.Value); ext = t; } else ext = x;
            }
            if (!ext.HasValue) throw ToolException.Invalid($"{tool}: the solids have no extents. Nothing was drawn.");
            var pl = SectionLogic.ProjectionPlane(J(ext.Value.MinPoint), J(ext.Value.MaxPoint), view.Direction);
            var (u, v) = SectionLogic.Frame(pl.Normal);
            var m = FrameMatrix(pl, u, v);
            string layer = p.Str("layer", SectionLogic.ElevationLayer);
            var notes = new List<string>();
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, 7)) layersCreated.Add(layer);
            var lp = LayerParams(tool, layer);
            List<Curve> curves = null;
            bool hidden = false;
            string used = method;
            try
            {
                if (method == "auto")
                {
                    curves = SectionBackground(ctx, solids, pl, u, v, m, lp, notes);
                    hidden = curves.Count > 0;
                    used = "hidden_lines_removed";
                }
                if (curves == null || curves.Count == 0)
                {
                    if (method == "auto") notes.Add("AutoCAD gave no hidden-line view; every edge was projected instead (hidden edges are drawn too).");
                    curves = ProjectedEdges(solids, pl, m, notes);
                    used = "edges";
                }
                if (curves.Count == 0) throw ToolException.Invalid($"{tool}: no line to draw. Nothing was drawn.");
                if (curves.Count > SectionLogic.MaxResultEntities)
                    throw ToolException.Invalid($"{tool}: the view would have {curves.Count} lines (more than {SectionLogic.MaxResultEntities}). Nothing was drawn. Project fewer solids.");
                var box = CurvesBox(curves).Value;
                var (move, at, warning) = Placement(ctx, p, box, f);
                if (warning != null) notes.Add(warning);
                var handles = new List<object>();
                foreach (var c in curves)
                {
                    c.TransformBy(move);
                    handles.Add(Acad.H(AddCurve(ctx, c, lp)));
                }
                var r = new Dictionary<string, object>
                {
                    ["handles"] = handles,
                    ["view"] = view.Name,
                    ["method"] = used,
                    ["hidden_lines_removed"] = hidden,
                    ["position"] = new List<object> { Acad.R(at[0]), Acad.R(at[1]) },
                    ["size"] = new List<object> { Acad.R(box.max[0] - box.min[0]), Acad.R(box.max[1] - box.min[1]) },
                    ["frame"] = FrameInfo(pl, u, v, at, box),
                };
                if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
                if (notes.Count > 0) r["notes"] = notes;
                return r;
            }
            catch
            {
                if (curves != null) DisposeAll(curves);
                throw;
            }
        }
    }
}
