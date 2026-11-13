using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt D (13/11/2026): beams along a path, PRESSPULL, STL export and the volume schedule by layer
    /// (labelled ToolGroup.ThreeD, Core/Plans.cs). The table of volume_schedule / check_interference is text: the plan
    /// check refuses table_position without 2D (PlanLogic.TextParams), so on the 3D plan they only return numbers.
    /// The AutoCAD-free part is Solid3Logic in Core/Solids3.cs; the curved walls are in PlanTo3dTools.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterSolids3(ToolDispatcher d)
        {
            d.Register("build_beam", true, BuildBeam);
            d.Register("presspull", true, Presspull);
            // writes a file, not the drawing: no undo step, allowed in read-only mode (like plot_pdf)
            d.Register(new ToolDef("export_stl", false, ExportStl, noTransaction: true));
            // only reads unless it inserts a table
            d.Register(new ToolDef("volume_schedule", true, VolumeSchedule, readsWhen: p => !p.Has("table_position")));
        }

        // ================================================================= beams

        private sealed class BeamPath
        {
            public List<double[]> Points = new List<double[]>();
            public List<double> Bulges = new List<double>();
            public bool Closed;
            public double Z;
            public string From;
        }

        private static void CheckLevel(IEnumerable<double> zs, string what, string tool)
        {
            var list = zs.ToList();
            if (list.Count == 0) return;
            double tol = 1e-6 * Math.Max(1, list.Max(v => Math.Abs(v)));
            if (list.Any(v => Math.Abs(v - list[0]) > tol))
                throw ToolException.Invalid($"{tool}: {what} is not level (its points have different z); only level beams are built. Nothing was built. Pass path_points [[x, y], ...] and top_z.");
        }

        private static BeamPath ReadBeamPath(ToolContext ctx, string h, string tool)
        {
            var e = Acad.GetEntity(ctx, h, false);
            var bp = new BeamPath { From = h };
            switch (e)
            {
                case Line l:
                    CheckLevel(new[] { l.StartPoint.Z, l.EndPoint.Z }, h, tool);
                    bp.Points.Add(new[] { l.StartPoint.X, l.StartPoint.Y });
                    bp.Points.Add(new[] { l.EndPoint.X, l.EndPoint.Y });
                    bp.Bulges.AddRange(new[] { 0.0, 0.0 });
                    bp.Z = l.StartPoint.Z;
                    break;
                case Arc a:
                {
                    var mid = a.GetPointAtParameter((a.StartParam + a.EndParam) / 2);
                    CheckLevel(new[] { a.StartPoint.Z, a.EndPoint.Z, mid.Z }, h, tool);
                    var arc = Solid3Logic.ArcFrom3Points(new[] { a.StartPoint.X, a.StartPoint.Y }, new[] { mid.X, mid.Y }, new[] { a.EndPoint.X, a.EndPoint.Y });
                    if (arc == null) throw ToolException.Invalid($"{tool}: {h} is a degenerate arc.");
                    var s0 = new Point3d(arc.Cx + arc.R * Math.Cos(arc.A0), arc.Cy + arc.R * Math.Sin(arc.A0), a.StartPoint.Z);
                    double b = Math.Tan(arc.Sweep / 4) * (s0.DistanceTo(a.StartPoint) < 1e-6 * Math.Max(1, arc.R) ? 1 : -1);
                    bp.Points.Add(new[] { a.StartPoint.X, a.StartPoint.Y });
                    bp.Points.Add(new[] { a.EndPoint.X, a.EndPoint.Y });
                    bp.Bulges.AddRange(new[] { b, 0.0 });
                    bp.Z = a.StartPoint.Z;
                    break;
                }
                case Polyline pl:
                {
                    // bulges are counter-clockwise about the polyline's normal: a polyline seen from below turns the other way
                    double sign = pl.Normal.Z < 0 ? -1 : 1;
                    if (Math.Abs(pl.Normal.Z) < 1 - 1e-9) throw ToolException.Invalid($"{tool}: {h} is not drawn flat in plan (its plane is tilted). Nothing was built.");
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var v = pl.GetPoint3dAt(i);
                        bp.Points.Add(new[] { v.X, v.Y });
                        bp.Bulges.Add(pl.GetBulgeAt(i) * sign);
                    }
                    bp.Closed = pl.Closed;
                    bp.Z = pl.GetPoint3dAt(0).Z;
                    break;
                }
                default:
                    throw ToolException.Invalid($"{tool}: {h} is a {Acad.TypeName(e)}; a beam path must be a line, an arc or a lightweight polyline (straight and arc segments).");
            }
            return bp;
        }

        /// <summary>A closed outline as a region (the caller disposes it).</summary>
        private static Region OutlineRegion(Outline2d o, double z, string tool, string from)
        {
            var pts = o.Points.Select(q => new[] { q[0], q[1], z }).ToList();
            using (var pl = Acad.MakePolyline(pts, true, o.Bulges))
            {
                DBObjectCollection made;
                try { made = Region.CreateFromCurves(new DBObjectCollection { pl }); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not make a closed outline for {from} ({ex.ErrorStatus}). Nothing was built."); }
                Region reg = null;
                foreach (DBObject x in made) if (reg == null && x is Region r) reg = r; else x.Dispose();
                if (reg == null) throw ToolException.Invalid($"{tool}: AutoCAD could not make a closed outline for {from}. Nothing was built.");
                return reg;
            }
        }

        /// <summary>The outlines extruded from bottom to top (drawing units) and united into one solid (pieces AutoCAD cannot unite stay separate).</summary>
        private static List<ObjectId> UniteOutlines(ToolContext ctx, string tool, IEnumerable<Outline2d> outlines, double bottom, double height, Params layerP, string from, string what, out double volume)
        {
            Solid3d first = null;
            var ids = new List<ObjectId>();
            volume = 0;
            foreach (var o in outlines)
            {
                Solid3d s;
                using (var reg = OutlineRegion(o, 0, tool, from)) s = ExtrudeFlat(tool, reg, height, bottom, false, from, what);
                if (first == null) { ids.Add(AddSolid(ctx, s, layerP)); first = s; continue; }
                try { BooleanInto(first, s, BooleanOperationType.BoolUnite); s.Dispose(); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { ids.Add(AddSolid(ctx, s, layerP)); }
                catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
            }
            foreach (var id in ids) volume += VolumeOf((Solid3d)ctx.Tr.GetObject(id, OpenMode.ForRead));
            return ids;
        }

        internal static object BuildBeam(ToolContext ctx, Params p)
        {
            const string tool = "build_beam";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            double wmm = p.Num("width", Solid3Logic.DefaultBeamWidth), hmm = p.Num("height", Solid3Logic.DefaultBeamHeight);
            Solid3Logic.CheckBeam(wmm, hmm, tool);
            if (p.Has("path_handles") == p.Has("path_points"))
                throw ToolException.Invalid($"{tool}: pass either 'path_handles' (lines / arcs / polylines, one beam each) or 'path_points' [[x, y], ...], not both.");
            var paths = new List<BeamPath>();
            if (p.Has("path_points"))
            {
                var pts = p.Points("path_points", 2, Solid3Logic.MaxBeamSegments + 1);
                var zs = pts.Select(q => q.Length > 2 ? q[2] : 0).ToList();
                CheckLevel(zs, "path_points", tool);
                paths.Add(new BeamPath
                {
                    Points = pts.Select(q => new[] { q[0], q[1] }).ToList(),
                    Bulges = p.NumbersOpt("path_bulges") ?? new List<double>(),
                    Closed = p.Bool("closed", false),
                    Z = zs[0],
                    From = "path_points",
                });
            }
            else
            {
                var hs = p.Handles("path_handles", Solid3Logic.MaxBeamPaths).Select(x => x.ToUpperInvariant()).Distinct().ToList();
                foreach (var h in hs) paths.Add(ReadBeamPath(ctx, h, tool));
                if (p.Has("closed")) foreach (var bp in paths) bp.Closed = bp.Closed || p.Bool("closed", false);
            }
            double? topZ = p.NumOpt("top_z"), slab = p.NumOpt("slab_thickness");
            if (slab.HasValue && (slab.Value < 0 || slab.Value > FloorPlanLogic.MaxSlabThickness))
                throw ToolException.Invalid($"{tool}: slab_thickness must be 0–{FloorPlanLogic.MaxSlabThickness} mm.");
            // every layout first (a bad path stops the call before anything is built)
            var layouts = paths.Select(bp => Solid3Logic.Beam(bp.Points, bp.Bulges, bp.Closed, wmm / f, bp.From, tool)).ToList();
            string layer = p.Str("layer", Solid3Logic.BeamLayer);
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, 4)) layersCreated.Add(layer);
            var layerP = LayerParams(tool, layer);
            var beams = new List<object>();
            var handles = new List<object>();
            double total = 0;
            double top0 = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                double top = Solid3Logic.BeamTop(topZ.HasValue ? topZ.Value / f : (double?)null, paths[i].Z, slab.HasValue ? slab.Value / f : (double?)null);
                if (i == 0) top0 = top;
                var ids = UniteOutlines(ctx, tool, layouts[i].Pieces, top - hmm / f, hmm / f, layerP, paths[i].From, "beam", out var vol);
                total += vol;
                var row = new Dictionary<string, object>
                {
                    ["handle"] = Acad.H(ids[0]),
                    ["from"] = paths[i].From,
                    ["length_m"] = Math.Round(layouts[i].Length * f / 1000, 3),
                    ["volume_m3"] = Math.Round(Solid3Logic.M3(vol, f), 6),
                    ["top_z_mm"] = Acad.R(top * f),
                };
                if (ids.Count > 1) row["separate_pieces"] = ids.Skip(1).Select(x => (object)Acad.H(x)).ToList();
                AddExtents(row, (Entity)ctx.Tr.GetObject(ids[0], OpenMode.ForRead));
                beams.Add(row);
                handles.AddRange(ids.Select(x => (object)Acad.H(x)));
            }
            var r = new Dictionary<string, object>
            {
                ["handles"] = handles,
                ["beams"] = beams,
                ["count"] = beams.Count,
                ["width_mm"] = wmm,
                ["height_mm"] = hmm,
                ["layer"] = layer,
                ["total_volume_m3"] = Math.Round(Solid3Logic.M3(total, f), 6),
                ["note"] = $"Beams {wmm} × {hmm} mm centred on the path, top at top_z{(slab.HasValue ? " minus slab_thickness" : "")} (first beam: {Acad.R(top0 * f)} mm), " +
                           "square ends, corners closed; the path objects were only read. Check with capture_view (sw_iso, conceptual).",
            };
            if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
            return r;
        }

        // ================================================================= PRESSPULL

        private static object Presspull(ToolContext ctx, Params p)
        {
            const string tool = "presspull";
            bool faceMode = p.Has("handle"), profileMode = p.Has("profile_points") || p.Has("profile_handles");
            if (faceMode == profileMode)
                throw ToolException.Invalid($"{tool}: pass either 'handle' + faces / face_filter (pull faces of a solid) or a profile (profile_points / profile_handles: a closed area, optionally with 'target'), not both.");
            double dist = p.Num("distance");
            if (Math.Abs(dist) < 1e-9) throw ToolException.Invalid($"{tool}: distance must not be 0 (positive = out of the face / up, negative = in / down).");
            if (faceMode)
            {
                if (p.Has("target") || p.Has("mode")) throw ToolException.Invalid($"{tool}: 'target' and 'mode' go with a profile; pulling faces of 'handle' always moves those faces.");
                var h = p.Handle("handle").ToUpperInvariant();
                var s = SolidForWrite(ctx, h, tool);
                var t = ReadTopology(s);
                var faces = PickFaces(p, t, h);
                if (faces.Count == 0) throw ToolException.Invalid($"{tool}: pick the faces to pull: face_filter (e.g. [\"top\"]) or faces + topology_id from get_solid_info (faces: true).");
                double before = VolumeOf(s);
                try { s.ExtrudeFaces(faces.Select(i => t.FaceIds[i]).ToArray(), dist, 0); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    throw ToolException.Invalid($"{tool}: AutoCAD could not pull the face(s) of {h} by {dist} ({ex.ErrorStatus}). Nothing was changed. " +
                        "Pushing a face in further than the solid is deep, or faces that cannot move together, are common causes.");
                }
                double after = VolumeOf(s);
                if (s.IsNull || after <= 1e-9) throw ToolException.Invalid($"{tool}: pulling {h} by {dist} would leave nothing. Nothing was changed.");
                var r = ChangedResult(h, s, before);
                r["faces_pulled"] = faces.Count;
                r["mode"] = after >= before ? "add" : "subtract";
                return r;
            }

            string mode = Solid3Logic.NormalizePresspullMode(p.Str("mode", "auto"), tool);
            string target = p.Has("target") ? p.Handle("target").ToUpperInvariant() : null;
            Solid3d ts = target != null ? SolidForWrite(ctx, target, tool) : null;
            var regions = ProfileRegions(ctx, p, out var sources, out _);
            var pieces = new List<Solid3d>();
            try
            {
                foreach (var reg in regions)
                {
                    var s = new Solid3d();
                    pieces.Add(s);
                    try { s.Extrude(reg, SolidLogic.ExtrudeHeightAlongNormal(dist, reg.Normal.Z), 0); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not pull the profile ({ex.ErrorStatus}). Nothing was changed."); }
                }
            }
            catch { foreach (var s in pieces) s.Dispose(); throw; }
            finally { foreach (var r in regions) r.Dispose(); }

            if (ts == null || mode == "new")
            {
                var ids = new List<ObjectId>();
                foreach (var s in pieces) ids.Add(AddSolid(ctx, s, p));
                EraseProfiles(ctx, p, sources);
                var res = (Dictionary<string, object>)SolidsResult(ids, pieces);
                res["mode"] = "new";
                return res;
            }
            double before2 = VolumeOf(ts);
            var modes = new List<object>();
            try
            {
                foreach (var s in pieces)
                {
                    var m = Solid3Logic.ResolvePresspull(mode, true, mode == "auto" ? OverlapVolume(ts, s) : 0, VolumeOf(s), tool);
                    BooleanChecked(ts, s, m == "subtract" ? BooleanOperationType.BoolSubtract : BooleanOperationType.BoolUnite, tool, target, "the pulled profile");
                    modes.Add(m);
                }
            }
            finally { foreach (var s in pieces) s.Dispose(); }
            if (ts.IsNull || VolumeOf(ts) <= 1e-9) throw ToolException.Invalid($"{tool}: the cut would leave nothing of {target}. Nothing was changed.");
            EraseProfiles(ctx, p, sources);
            var r2 = ChangedResult(target, ts, before2);
            r2["mode"] = modes.Distinct().Count() == 1 ? modes[0] : modes;
            return r2;
        }

        // ================================================================= STL

        private static List<Solid3d> SolidsOf(ToolContext ctx, Params p, int max, string tool, out List<string> handles)
        {
            var list = new List<Solid3d>();
            handles = new List<string>();
            if (p.Has("handles"))
            {
                foreach (var h in p.Handles("handles", max).Select(x => x.ToUpperInvariant()).Distinct())
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    if (!(e is Solid3d s)) throw ToolException.Invalid($"{tool}: {h} is a {Acad.TypeName(e)}, not a 3D solid.");
                    list.Add(s); handles.Add(h);
                }
                return list;
            }
            var layers = p.Has("layers") ? p.Strings("layers", 1) : null;
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.IsErased || id.ObjectClass.DxfName != "3DSOLID") continue;
                var s = ctx.Tr.GetObject(id, OpenMode.ForRead) as Solid3d;
                if (s == null || s.IsNull) continue;
                if (layers != null && !FloorPlanLogic.LayerMatches(s.Layer, layers)) continue;
                if (list.Count >= max) throw ToolException.Invalid($"{tool}: more than {max} solids; pass 'handles' or 'layers' to take a part of the model.");
                list.Add(s); handles.Add(Acad.H(id));
            }
            return list;
        }

        private static object ExportStl(ToolContext ctx, Params p)
        {
            const string tool = "export_stl";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out var unitName);
            string drawingDir = SafeString(() => Path.GetDirectoryName(ctx.Doc.Name));
            if (string.IsNullOrEmpty(drawingDir) || !Directory.Exists(drawingDir)) drawingDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string output = p.Str("output", null);
            if (string.IsNullOrEmpty(output)) output = Path.Combine(drawingDir, Solid3Logic.DefaultStlName(Path.GetFileNameWithoutExtension(ctx.Doc.Name)));
            if (!output.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid($"{tool}: 'output' must end with .stl.");
            string full;
            try { full = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(drawingDir, output)); }
            catch (Exception) { throw ToolException.Invalid($"{tool}: '{output}' is not a valid file path."); }
            var badName = Util2DLogic.BadFileName(Path.GetFileName(full));
            if (badName != null) throw ToolException.Invalid($"{tool}: '{output}': {badName}. Give another file name ending in .stl.");
            if (Directory.Exists(full)) throw ToolException.Invalid($"{tool}: '{output}' is a folder; give a file name ending in .stl.");
            var outDir = Path.GetDirectoryName(full);
            if (!Directory.Exists(outDir)) throw ToolException.Invalid($"{tool}: folder '{outDir}' does not exist.");
            bool ascii = p.Bool("ascii", false);
            bool exists = File.Exists(full);
            if (exists && !p.Bool("overwrite", false))
                throw ToolException.Invalid($"{tool}: '{full}' already exists. Ask the user, then call again with overwrite: true, or give another file name.");

            var tris = new List<float[]>();
            var temp = Path.Combine(Path.GetTempPath(), "claudecad-stl-" + Guid.NewGuid().ToString("N"));
            List<string> handles;
            double[] shift;
            double[] size;
            using (ctx.Doc.LockDocument())
            using (var tr = ctx.Db.TransactionManager.StartTransaction())
            {
                var rctx = new ToolContext(ctx.Doc, tr, false, ctx.Dispatcher);
                var solids = SolidsOf(rctx, p, Solid3Logic.MaxStlSolids, tool, out handles);
                if (solids.Count == 0) throw ToolException.Invalid($"{tool}: no 3D solid to export{(p.Has("layers") ? " on those layers" : "")}.");
                Extents3d? all = null;
                foreach (var s in solids)
                {
                    var x = EntityInfo.Extents(s);
                    if (!x.HasValue) continue;
                    if (all.HasValue) { var t = all.Value; t.AddExtents(x.Value); all = t; } else all = x;
                }
                if (!all.HasValue) throw ToolException.Invalid($"{tool}: the solids have no extents (empty).");
                // STL has no units: write mm; slicers expect the model in the positive octant
                var min = all.Value.MinPoint; var max = all.Value.MaxPoint;
                shift = Solid3Logic.PositiveShift(new[] { min.X * f, min.Y * f, min.Z * f });
                size = new[] { (max.X - min.X) * f, (max.Y - min.Y) * f, (max.Z - min.Z) * f };
                bool move = Math.Abs(f - 1) > 1e-12 || shift.Any(v => v != 0);
                var m = Matrix3d.Displacement(new Vector3d(shift[0], shift[1], shift[2])) * Matrix3d.Scaling(f, Point3d.Origin);
                Directory.CreateDirectory(temp);
                try
                {
                    var ms = move ? (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db), OpenMode.ForWrite) : null;
                    for (int i = 0; i < solids.Count; i++)
                    {
                        var file = Path.Combine(temp, i + ".stl");
                        try
                        {
                            if (move)
                            {
                                // a moved / scaled copy, only inside this transaction: it is aborted below, the drawing is not changed
                                var c = (Solid3d)solids[i].Clone();
                                ms.AppendEntity(c);
                                tr.AddNewlyCreatedDBObject(c, true);
                                c.TransformBy(m);
                                c.StlOut(file, false);
                            }
                            else solids[i].StlOut(file, false);
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not write {handles[i]} as STL ({ex.ErrorStatus}). Nothing was written."); }
                        if (!File.Exists(file)) throw ToolException.Invalid($"{tool}: AutoCAD wrote no STL for {handles[i]}. Nothing was written.");
                        tris.AddRange(Solid3Logic.ReadStl(File.ReadAllBytes(file)));
                    }
                }
                finally
                {
                    tr.Abort();
                    try { Directory.Delete(temp, true); } catch (Exception) { }
                }
            }
            if (tris.Count == 0) throw ToolException.Invalid($"{tool}: the solids gave no triangles. Nothing was written.");
            try
            {
                if (ascii) File.WriteAllText(full, Solid3Logic.WriteAsciiStl(tris, Path.GetFileNameWithoutExtension(full)));
                else File.WriteAllBytes(full, Solid3Logic.WriteBinaryStl(tris, "ClaudeCAD STL, mm"));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw ToolException.Invalid($"{tool}: could not write '{full}' ({ex.Message}). Close it in other programs or choose another folder.");
            }
            var r = new Dictionary<string, object>
            {
                ["output"] = full,
                ["exists"] = File.Exists(full),
                ["format"] = ascii ? "ascii" : "binary",
                ["solids"] = handles.Count,
                ["triangles"] = tris.Count,
                ["units"] = "mm",
                ["size_mm"] = size.Select(v => (object)Acad.R(v)).ToList(),
            };
            if (exists) r["overwritten"] = true;
            if (shift.Any(v => v != 0)) r["moved_by_mm"] = shift.Select(v => (object)Acad.R(v)).ToList();
            if (Math.Abs(f - 1) > 1e-12) r["scaled"] = $"drawing units ({unitName}) × {f} → mm";
            r["note"] = "The drawing was not changed. STL has no units: tell the user the file is in mm" +
                        (shift.Any(v => v != 0) ? " and the model was moved into the positive octant (moved_by_mm)." : ".");
            return r;
        }

        // ================================================================= volume schedule

        private static object VolumeSchedule(ToolContext ctx, Params p)
        {
            const string tool = "volume_schedule";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            string mode = (p.Str("overlap", "deduct") ?? "deduct").Trim().ToLowerInvariant();
            if (mode != "deduct" && mode != "none") throw ToolException.Invalid($"{tool}: overlap must be \"deduct\" (default: a common part is counted once, in the column / beam) or \"none\" (only warn).");
            int decimals = p.Int("decimals", 3, 0, 6);
            bool withArea = p.Bool("surface_area", true);
            var solids = SolidsOf(ctx, p, Solid3Logic.MaxScheduleSolids, tool, out var handles);
            if (solids.Count == 0) throw ToolException.Invalid($"{tool}: no 3D solid found{(p.Has("layers") ? " on those layers" : "")}.");
            var vs = new List<Solid3Logic.VolSolid>();
            var boxes = new List<Extents3d?>();
            for (int i = 0; i < solids.Count; i++)
            {
                double area = 0;
                if (withArea) { try { area = solids[i].Area; } catch (Autodesk.AutoCAD.Runtime.Exception) { } }
                vs.Add(new Solid3Logic.VolSolid { Handle = handles[i], Layer = solids[i].Layer, Volume = VolumeOf(solids[i]), Area = area, Order = i });
                boxes.Add(EntityInfo.Extents(solids[i]));
            }
            // overlaps: pairs whose boxes overlap, then AutoCAD's clash test, then the common volume (capped)
            var overlaps = new List<Solid3Logic.VolOverlap>();
            int candidates = 0, measured = 0;
            bool truncated = false;
            for (int i = 0; i < solids.Count && !truncated; i++)
                for (int j = i + 1; j < solids.Count; j++)
                {
                    var a = boxes[i]; var b = boxes[j];
                    if (!a.HasValue || !b.HasValue || Disjoint(a.Value, b.Value)) continue;
                    if (++candidates > Solid3Logic.MaxScheduleOverlapPairs) { truncated = true; break; }
                    bool hit;
                    try { hit = solids[i].CheckInterference(solids[j]); }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { overlaps.Add(new Solid3Logic.VolOverlap { A = handles[i], B = handles[j], Volume = null }); continue; }
                    if (!hit) continue;
                    measured++;
                    overlaps.Add(new Solid3Logic.VolOverlap { A = handles[i], B = handles[j], Volume = OverlapVolume(solids[i], solids[j]) });
                }
            var res = Solid3Logic.Schedule(vs, overlaps, p.Has("priority") ? p.Strings("priority", 1) : null, mode);
            if (truncated) res.Warnings.Add($"More than {Solid3Logic.MaxScheduleOverlapPairs} pairs of solids with touching bounding boxes: only the first were checked for overlaps; run it per layer / part (layers or handles) for exact numbers.");
            int dp = decimals;
            var r = new Dictionary<string, object>
            {
                ["solids"] = solids.Count,
                ["rows"] = res.Rows.Select(x =>
                {
                    var d = new Dictionary<string, object>
                    {
                        ["layer"] = x.Layer, ["count"] = x.Count,
                        ["volume_m3"] = Math.Round(Solid3Logic.M3(x.Gross, f), dp),
                        ["deducted_m3"] = Math.Round(Solid3Logic.M3(x.Deducted, f), dp),
                        ["net_m3"] = Math.Round(Solid3Logic.M3(x.Net, f), dp),
                    };
                    if (withArea) d["area_m2"] = Math.Round(Solid3Logic.M2(x.Area, f), dp);
                    return (object)d;
                }).ToList(),
                ["total_volume_m3"] = Math.Round(Solid3Logic.M3(res.Rows.Sum(x => x.Gross), f), dp),
                ["total_net_m3"] = Math.Round(Solid3Logic.M3(res.Rows.Sum(x => x.Net), f), dp),
                ["overlap_total_m3"] = Math.Round(Solid3Logic.M3(res.OverlapTotal, f), dp),
                ["overlap"] = mode,
            };
            if (withArea) r["total_area_m2"] = Math.Round(Solid3Logic.M2(res.Rows.Sum(x => x.Area), f), dp);
            if (res.Deductions.Count > 0)
            {
                r["deductions"] = res.Deductions.Take(50).Select(x => (object)new Dictionary<string, object>
                {
                    ["from"] = x.From, ["kept_in"] = x.Kept, ["volume_m3"] = Math.Round(Solid3Logic.M3(x.Volume, f), 6),
                }).ToList();
                r["rule"] = "A common part is counted once: columns keep their full volume, then beams, walls, slabs (or the priority layers in order); the lower one loses it.";
            }
            if (res.Warnings.Count > 0) r["warnings"] = res.Warnings;
            if (p.Has("table_position"))
            {
                var t = InsertSchedule(ctx, p, Solid3Logic.VolumeTable(res, decimals, f, withArea), withArea ? new[] { 5.0, 16, 7, 11, 13, 13, 13 } : new[] { 5.0, 16, 7, 11, 13, 13 }, "BẢNG KHỐI LƯỢNG 3D");
                r["handles"] = t["handles"];
            }
            return r;
        }
    }
}
