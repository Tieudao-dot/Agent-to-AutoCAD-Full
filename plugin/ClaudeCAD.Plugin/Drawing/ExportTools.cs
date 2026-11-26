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
    /// Nâng cấp đợt H2: export_drawing — the drawing (or a part) as DXF, as a DWG of an older version (to send to partners
    /// on an older AutoCAD), or its 3D solids as FBX (3ds Max, Blender; 3D / Full). DXF / DWG are written from a copy of the
    /// database (Wblock): the open drawing keeps its file, name and version. FBX is written by the plugin itself from the
    /// meshes of the solids (no FBXEXPORT command whose questions could differ between versions).
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterExport(ToolDispatcher d)
        {
            // reads the drawing only (like export_stl): allowed in "Chỉ xem", never recorded in a macro
            d.Register(new ToolDef("export_drawing", false, ExportDrawing, noTransaction: true));
        }

        internal static object ExportDrawing(ToolContext ctx, Params p)
        {
            const string tool = "export_drawing";
            var doc = ctx.Doc;
            var db = ctx.Db;
            var format = ExportLogic.FormatOf(p.Str("format"), tool);
            var (year, acad) = format == "fbx" ? ("", "") : ExportLogic.VersionOf(p.Str("version", null), tool);
            bool part = p.Has("handles") || p.Has("layers");
            // Test & sửa H2: both would export the handles only, silently leaving the layers out
            if (p.Has("handles") && p.Has("layers")) throw ToolException.Invalid($"{tool}: give handles or layers, not both.");
            string openPath = null;
            try { openPath = doc.IsNamedDrawing ? doc.Name : null; } catch (Exception) { }
            var file = ExportLogic.OutputPath(p.Str("output", null), format, DrawingFolder(ctx), DrawingStem(ctx), year, part, openPath, tool);
            // the network rule of trace_image (G5): this computer, or the network folder of the open drawing
            var refusal = TraceLogic.PathRefusal(file, openPath, OnNetworkDrive(file), SameDrive(file, openPath), tool);
            if (refusal != null) throw ToolException.Invalid(ExportLogic.WriteRefusal(refusal));
            if (Directory.Exists(file)) throw ToolException.Invalid($"{tool}: '{file}' is a folder; give a file name ending in .{format}.");
            if (!Directory.Exists(Path.GetDirectoryName(file))) throw ToolException.Invalid($"{tool}: folder '{Path.GetDirectoryName(file)}' does not exist.");
            RequireOverwriteCard(ctx, p, tool, file, File.Exists(file), $"{tool}: '{file}' already exists. Ask the user, then call again with overwrite: true, or give another file name.", "overwrite");

            if (format == "fbx") return ExportFbx(ctx, p, file, tool);

            var version = (DwgVersion)Enum.Parse(typeof(DwgVersion), acad);
            var temp = Path.Combine(Path.GetTempPath(), "claudecad-export-" + Guid.NewGuid().ToString("N").Substring(0, 12) + "." + format);
            int count = 0, layouts = 0;
            try
            {
                using (doc.LockDocument())
                {
                    Database copy;
                    if (part)
                    {
                        var ids = new ObjectIdCollection();
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            foreach (var id in ExportIds(new ToolContext(doc, tr, false, ctx.Dispatcher), p, tool)) ids.Add(id);
                            tr.Commit();
                        }
                        if (ids.Count == 0) throw ToolException.Invalid($"{tool}: nothing to export{(p.Has("layers") ? " on those layers" : "")}.");
                        count = ids.Count;
                        // the chosen objects at their own coordinates (base point = the origin)
                        copy = db.Wblock(ids, Point3d.Origin);
                    }
                    else copy = db.Wblock();
                    using (copy)
                    {
                        if (!part) layouts = LayoutCount(copy);
                        if (format == "dxf") copy.DxfOut(temp, H2Numbers.DxfPrecision, version);
                        else copy.SaveAs(temp, version);
                    }
                }
                if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw ToolException.Invalid($"{tool}: AutoCAD wrote no file. Nothing was written to '{file}'.");
                try { File.Copy(temp, file, true); }
                catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not write '{file}' ({ex.Message}; is it open in another program?)."); }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw ToolException.Invalid($"{tool}: AutoCAD could not save the {format.ToUpperInvariant()} {year} ({ex.ErrorStatus}). Nothing was written to '{file}'.");
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
            }
            var r = new Dictionary<string, object>
            {
                ["file"] = file,
                ["format"] = format,
                ["version"] = year,
                ["opens_in"] = ExportLogic.Covers(year),
                ["size_kb"] = Acad.R(new FileInfo(file).Length / 1024.0),
                ["note"] = ExportLogic.Note(format, year, part) + " Tell the user where the file is.",
            };
            if (part) r["objects"] = count;
            else r["layouts"] = layouts;
            return r;
        }

        /// <summary>The objects of a part: handles, or every object of model space on the layers.</summary>
        private static List<ObjectId> ExportIds(ToolContext ctx, Params p, string tool)
        {
            var ids = new List<ObjectId>();
            if (p.Has("handles"))
            {
                foreach (var h in p.Handles("handles", 20000).Select(x => x.ToUpperInvariant()).Distinct())
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    if (e.OwnerId != SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db)) throw ToolException.Invalid($"{tool}: {h} is not in model space; a part is taken from model space.");
                    ids.Add(e.ObjectId);
                }
                return ids;
            }
            var layers = p.Strings("layers", 1);
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.IsErased) continue;
                if (ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e && FloorPlanLogic.LayerMatches(e.Layer, layers)) ids.Add(id);
            }
            return ids;
        }

        private static int LayoutCount(Database db)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var d = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    int n = d.Count;
                    tr.Commit();
                    return n;
                }
            }
            catch (Exception) { return 0; }
        }

        private static bool SameDrive(string file, string drawing)
        {
            try { return drawing != null && string.Equals(Path.GetPathRoot(file), Path.GetPathRoot(drawing), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        /// <summary>The 3D solids as meshes (one per solid, its colour), FBX 7.4, millimetres.</summary>
        private static object ExportFbx(ToolContext ctx, Params p, string file, string tool)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(db.Insunits), out _);
            var meshes = new List<FbxWriter.Mesh>();
            var temp = Path.Combine(Path.GetTempPath(), "claudecad-fbx-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            int triangles = 0;
            var empty = new List<string>();
            List<string> handles;
            Directory.CreateDirectory(temp);
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var rctx = new ToolContext(doc, tr, false, ctx.Dispatcher);
                    var solids = SolidsOf(rctx, p, H2Numbers.MaxFbxSolids, tool, out handles);
                    if (solids.Count == 0) throw ToolException.Invalid($"{tool}: no 3D solid to export{(p.Has("layers") ? " on those layers" : "")}. FBX takes 3D solids; for 2D drawings use format dxf or dwg.");
                    Extents3d? all = null;
                    foreach (var s in solids)
                    {
                        var x = EntityInfo.Extents(s);
                        if (!x.HasValue) continue;
                        if (all.HasValue) { var t = all.Value; t.AddExtents(x.Value); all = t; } else all = x;
                    }
                    if (!all.HasValue) throw ToolException.Invalid($"{tool}: the solids have no extents (empty).");
                    // like export_stl: a copy moved into the positive octant (what STLOUT needs), moved back in the mesh
                    var min = all.Value.MinPoint;
                    var max = all.Value.MaxPoint;
                    var shift = Solid3Logic.StlShift(new[] { min.X * f, min.Y * f, min.Z * f }, new[] { max.X * f, max.Y * f, max.Z * f });
                    var m = Matrix3d.Displacement(new Vector3d(shift[0], shift[1], shift[2])) * Matrix3d.Scaling(f, Point3d.Origin);
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    for (int i = 0; i < solids.Count; i++)
                    {
                        var stl = Path.Combine(temp, i + ".stl");
                        var c = (Solid3d)solids[i].Clone();
                        ms.AppendEntity(c);
                        tr.AddNewlyCreatedDBObject(c, true);
                        c.TransformBy(m);
                        try { c.StlOut(stl, false); }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not mesh {handles[i]} ({ex.ErrorStatus}). Nothing was written."); }
                        if (!File.Exists(stl)) throw ToolException.Invalid($"{tool}: AutoCAD made no mesh for {handles[i]}. Nothing was written.");
                        var tris = Solid3Logic.ReadStl(File.ReadAllBytes(stl)).Select(t => Unshift(t, shift)).ToList();
                        var mesh = FbxWriter.FromTriangles(solids[i].Layer + " " + handles[i], tris);
                        // Test & sửa H2: a solid AutoCAD meshes to nothing (too small / broken) is left out and named, not written empty
                        if (mesh.Triangles.Count == 0) { empty.Add(handles[i]); continue; }
                        var rgb = SolidRgb(tr, solids[i]);
                        mesh.R = rgb[0] / 255.0; mesh.G = rgb[1] / 255.0; mesh.B = rgb[2] / 255.0;
                        triangles += mesh.Triangles.Count;
                        meshes.Add(mesh);
                    }
                    tr.Abort(); // the moved copies are never kept: the drawing is not changed
                }
                if (meshes.Count == 0) throw ToolException.Invalid($"{tool}: AutoCAD made no triangles for the solids ({string.Join(", ", empty.Take(20))}). Nothing was written.");
                var bytes = FbxWriter.Write(meshes);
                try { File.WriteAllBytes(file, bytes); }
                catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not write '{file}' ({ex.Message})."); }
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch (Exception) { }
            }
            var res = new Dictionary<string, object>
            {
                ["file"] = file,
                ["format"] = "fbx",
                ["solids"] = meshes.Count,
                ["triangles"] = triangles,
                ["size_kb"] = Acad.R(new FileInfo(file).Length / 1024.0),
                ["note"] = ExportLogic.Note("fbx", "", p.Has("handles") || p.Has("layers")) + " Tell the user where the file is.",
            };
            if (empty.Count > 0) res["left_out"] = empty.Take(50).Cast<object>().ToList();
            return res;
        }

        private static float[] Unshift(float[] t, double[] shift)
        {
            var r = (float[])t.Clone();
            for (int k = 0; k < 3; k++)
                for (int a = 0; a < 3; a++) r[3 + k * 3 + a] = (float)(t[3 + k * 3 + a] - shift[a]);
            return r;
        }

        /// <summary>The colour of a solid (its own, else its layer's), RGB.</summary>
        private static int[] SolidRgb(Transaction tr, Entity e)
        {
            try
            {
                var c = e.Color;
                if (c.IsByLayer || c.IsByBlock)
                    c = ((LayerTableRecord)tr.GetObject(e.LayerId, OpenMode.ForRead)).Color;
                var v = c.ColorValue;
                return new int[] { v.R, v.G, v.B };
            }
            catch (Exception) { return new[] { 204, 204, 204 }; }
        }
    }
}
