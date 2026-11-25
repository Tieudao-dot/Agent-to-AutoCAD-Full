using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Material = Autodesk.AutoCAD.DatabaseServices.Material;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt F3 (25/11/2026): set_material (preset or custom materials on solids / layers) and named_view (save,
    /// restore, list, delete views by name; image = a PNG file of the model with its materials). Labelled 3D. Chosen as
    /// the lowest risk on AutoCAD 2022–2027 and all three builds: plain Material objects of the drawing (diffuse colour +
    /// opacity, no texture file) and the off-screen picture capture_view already makes, in the Realistic visual style;
    /// no RENDER command (asynchronous, its own window and settings, differs between versions).
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterRender(ToolDispatcher d)
        {
            d.Register(new ToolDef("set_material", true, SetMaterial, readsWhen: p => p.Bool("list", false)));
            // its own transactions: an image renders off-screen like capture_view (no open transaction)
            d.Register(new ToolDef("named_view", true, NamedView, noTransaction: true,
                readsWhen: p => { var a = (p.Str("action", "") ?? "").Trim().ToLowerInvariant(); return a == "list" || a == "image"; }));
        }

        // ================================================================= materials

        internal static object SetMaterial(ToolContext ctx, Params p)
        {
            const string tool = "set_material";
            var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MaterialDictionaryId, OpenMode.ForRead);
            if (p.Bool("list", false))
            {
                var names = new List<object>();
                foreach (DBDictionaryEntry e in dict) names.Add(e.Key);
                return new Dictionary<string, object>
                {
                    ["materials"] = names,
                    ["presets"] = MaterialLogic.Presets.Select(q => (object)new Dictionary<string, object> { ["key"] = q.Key, ["name"] = q.Name }).ToList(),
                };
            }
            string requested = p.Str("material");
            bool hasHandles = p.Has("handles"), hasLayers = p.Has("layers");
            if (!hasHandles && !hasLayers) throw ToolException.Invalid($"{tool}: pass handles (solids / objects) and / or layers to give the material to, or list: true.");
            double[] rgb = p.Has("color") ? p.Numbers("color").ToArray() : null;
            double? opacity = p.NumOpt("opacity");
            MaterialLogic.CheckLook(rgb, opacity, tool);

            // the material: ByLayer / ByBlock, an existing one, a preset (made once), or a new custom one with color
            var preset = MaterialLogic.Find(requested);
            string name = preset?.Name ?? requested.Trim();
            bool reset = MaterialLogic.Builtin.Take(2).Any(b => b.Equals(name, StringComparison.OrdinalIgnoreCase));
            ObjectId matId = ObjectId.Null;
            bool created = false;
            if (!reset)
            {
                var bad = MaterialLogic.BadName(name);
                if (bad != null) throw ToolException.Invalid($"{tool}: '{requested}': {bad}.");
                if ((rgb != null || opacity.HasValue) && MaterialLogic.IsProtected(name))
                    throw ToolException.Invalid($"{tool}: the Global material is the default of every layer; its color / opacity is not changed (it would recolour the whole drawing). Nothing was changed. Give a new material name with color instead.");
                if (dict.Contains(name)) matId = dict.GetAt(name);
                if (matId.IsNull || rgb != null || opacity.HasValue)
                {
                    if (matId.IsNull && preset == null && rgb == null)
                        throw ToolException.Invalid($"{tool}: no material '{requested}' in the drawing. Use a preset ({string.Join(", ", MaterialLogic.PresetKeys)}), list: true, or give color [r, g, b] to make it.");
                    created = matId.IsNull;
                    matId = MakeMaterial(ctx, dict, name, rgb ?? (preset != null ? new double[] { preset.R, preset.G, preset.B } : null), opacity ?? preset?.Opacity, matId, tool);
                }
            }

            var changed = new List<object>();
            var skipped = new List<object>();
            if (hasHandles)
                foreach (var h in p.Handles("handles", MaxModifyHandles))
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    var ltr = (LayerTableRecord)ctx.Tr.GetObject(e.LayerId, OpenMode.ForRead);
                    if (ltr.IsLocked) { skipped.Add(new Dictionary<string, object> { ["handle"] = h, ["reason"] = $"on locked layer {ltr.Name}" }); continue; }
                    e.UpgradeOpen();
                    if (reset) e.Material = MaterialLogic.Builtin.First(b => b.Equals(name, StringComparison.OrdinalIgnoreCase));
                    else e.MaterialId = matId;
                    changed.Add(h);
                }
            var layersDone = new List<object>();
            if (hasLayers)
                foreach (var ln in p.Strings("layers", 1))
                {
                    var lid = Acad.LayerId(ctx, ln);
                    var ltr = (LayerTableRecord)ctx.Tr.GetObject(lid, OpenMode.ForWrite);
                    // a layer cannot be ByLayer: "ByLayer" / "ByBlock" on a layer puts back the default Global
                    ltr.MaterialId = reset ? dict.GetAt("Global") : matId;
                    layersDone.Add(ltr.Name);
                }
            var r = new Dictionary<string, object> { ["material"] = name, ["handles"] = changed };
            if (created) r["created"] = true;
            if (layersDone.Count > 0) r["layers"] = layersDone;
            if (skipped.Count > 0) r["skipped"] = skipped;
            r["note"] = "Materials show in the Realistic visual style: look with capture_view visual_style realistic, or save a picture with named_view action image. Objects whose material is ByLayer take the layer's.";
            return r;
        }

        /// <summary>Makes (or updates) a plain material: diffuse / ambient colour and opacity; maps taken from Global.</summary>
        private static ObjectId MakeMaterial(ToolContext ctx, DBDictionary dict, string name, double[] rgb, double? opacityPct, ObjectId existing, string tool)
        {
            if (!dict.Contains("Global")) throw ToolException.Invalid($"{tool}: the drawing has no Global material to copy the settings from. Nothing was changed.");
            var global = (Material)ctx.Tr.GetObject(dict.GetAt("Global"), OpenMode.ForRead);
            Material mat;
            bool isNew = existing.IsNull;
            if (isNew) mat = new Material { Name = name };
            else mat = (Material)ctx.Tr.GetObject(existing, OpenMode.ForWrite);
            if (rgb != null)
            {
                var col = new MaterialColor(Method.Override, 1.0, new EntityColor((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]));
                mat.Diffuse = new MaterialDiffuseComponent(col, global.Diffuse.Map);
                mat.Ambient = col;
            }
            if (opacityPct.HasValue) mat.Opacity = new MaterialOpacityComponent(opacityPct.Value / 100.0, global.Opacity.Map);
            if (!isNew) return existing;
            dict.UpgradeOpen();
            var id = dict.SetAt(name, mat);
            ctx.Tr.AddNewlyCreatedDBObject(mat, true);
            return id;
        }

        // ================================================================= named views

        internal static object NamedView(ToolContext ctx, Params p)
        {
            const string tool = "named_view";
            var action = MaterialLogic.NormalizeAction(p.Str("action"));
            var doc = ctx.Doc;
            var db = ctx.Db;
            string name = p.Has("name") ? p.Str("name").Trim() : null;
            if (name != null)
            {
                var bad = MaterialLogic.BadViewName(name);
                if (bad != null) throw ToolException.Invalid($"{tool}: '{name}': {bad}.");
            }
            if ((action == "save" || action == "restore" || action == "delete") && name == null) throw ToolException.Invalid($"{tool}: action {action} needs a name.");

            if (action == "list")
            {
                var views = new List<object>();
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                    foreach (ObjectId id in vt)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is ViewTableRecord v) || v.IsErased) continue;
                        views.Add(new Dictionary<string, object>
                        {
                            ["name"] = v.Name,
                            ["direction"] = new List<object> { Acad.R(v.ViewDirection.X), Acad.R(v.ViewDirection.Y), Acad.R(v.ViewDirection.Z) },
                            ["paper_space"] = v.IsPaperspaceView,
                        });
                    }
                    tr.Commit();
                }
                return new Dictionary<string, object> { ["views"] = views, ["count"] = views.Count };
            }

            if (action == "image") return ViewImage(ctx, p, name, tool);

            if (!InModelSpace()) throw ToolException.Invalid($"{tool}: the Model tab is not active. Switch to Model with set_current_layout {{\"name\": \"Model\"}} first.");
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                bool exists = vt.Has(name) && !vt[name].IsErased;
                object result;
                switch (action)
                {
                    case "save":
                    {
                        if (exists && !p.Bool("replace", false))
                            throw ToolException.Invalid($"{tool}: a view named '{name}' already exists. Pass replace: true to overwrite it (ask the user first), or another name.");
                        ViewTableRecord rec;
                        if (exists) rec = (ViewTableRecord)tr.GetObject(vt[name], OpenMode.ForWrite);
                        else rec = new ViewTableRecord { Name = name };
                        using (var cur = doc.Editor.GetCurrentView())
                        {
                            rec.CenterPoint = cur.CenterPoint;
                            rec.Height = cur.Height;
                            rec.Width = cur.Width;
                            rec.Target = cur.Target;
                            rec.ViewDirection = cur.ViewDirection;
                            rec.ViewTwist = cur.ViewTwist;
                            rec.LensLength = cur.LensLength;
                            rec.PerspectiveEnabled = cur.PerspectiveEnabled;
                            rec.FrontClipDistance = cur.FrontClipDistance;
                            rec.BackClipDistance = cur.BackClipDistance;
                            rec.FrontClipEnabled = cur.FrontClipEnabled;
                            rec.BackClipEnabled = cur.BackClipEnabled;
                            try { if (!cur.VisualStyleId.IsNull) rec.VisualStyleId = cur.VisualStyleId; } catch (Autodesk.AutoCAD.Runtime.Exception) { }
                        }
                        if (!exists)
                        {
                            vt.UpgradeOpen();
                            vt.Add(rec);
                            tr.AddNewlyCreatedDBObject(rec, true);
                        }
                        result = new Dictionary<string, object> { ["name"] = name, ["saved"] = true, ["replaced"] = exists, ["direction"] = new List<object> { Acad.R(rec.ViewDirection.X), Acad.R(rec.ViewDirection.Y), Acad.R(rec.ViewDirection.Z) } };
                        break;
                    }
                    case "restore":
                    {
                        if (!exists) throw ToolException.Invalid($"{tool}: no view named '{name}'. Call named_view action list.");
                        var rec = (ViewTableRecord)tr.GetObject(vt[name], OpenMode.ForRead);
                        if (rec.IsPaperspaceView) throw ToolException.Invalid($"{tool}: '{name}' is a paper-space (layout) view; open its layout instead.");
                        doc.Editor.SetCurrentView(rec);
                        result = new Dictionary<string, object> { ["name"] = name, ["restored"] = true };
                        break;
                    }
                    default: // delete
                    {
                        if (!exists) throw ToolException.Invalid($"{tool}: no view named '{name}'. Call named_view action list.");
                        tr.GetObject(vt[name], OpenMode.ForWrite).Erase();
                        result = new Dictionary<string, object> { ["name"] = name, ["deleted"] = true };
                        break;
                    }
                }
                tr.Commit();
                return result;
            }
        }

        /// <summary>A PNG file of the model (off-screen, the user's view unchanged): from a saved view's direction or a preset, Realistic by default.</summary>
        private static object ViewImage(ToolContext ctx, Params p, string name, string tool)
        {
            ViewPreset v;
            if (name != null)
            {
                Vector3d dir;
                using (var tr = ctx.Db.TransactionManager.StartOpenCloseTransaction())
                {
                    var vt = (ViewTable)tr.GetObject(ctx.Db.ViewTableId, OpenMode.ForRead);
                    if (!vt.Has(name) || vt[name].IsErased) throw ToolException.Invalid($"{tool}: no view named '{name}'. Call named_view action list.");
                    dir = ((ViewTableRecord)tr.GetObject(vt[name], OpenMode.ForRead)).ViewDirection;
                    tr.Commit();
                }
                v = SolidLogic.Custom(new[] { dir.X, dir.Y, dir.Z }, tool);
            }
            else v = ViewOf(p, "sw_iso");
            string style = SolidLogic.VisualStyleName(p.Str("visual_style", "realistic"), tool);
            int size = p.Has("size") ? (int)p.Num("size") : MaterialLogic.DefaultImageSize;
            Point3d? min = null, max = null;
            if (p.Has("handles"))
            {
                var ext = HandlesExtents(ctx, p.Handles("handles", 5000), out int found);
                if (found == 0) throw new ToolException(ErrorCodes.EntityNotFound, $"{tool}: none of the handles exists. Call list_entities to get current handles.");
                if (ext.HasValue) { min = ext.Value.MinPoint; max = ext.Value.MaxPoint; }
            }
            string drawingDir = null;
            try { drawingDir = Path.GetDirectoryName(ctx.Doc.Name); } catch (Exception) { }
            if (string.IsNullOrEmpty(drawingDir) || !Directory.Exists(drawingDir)) drawingDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string stem = null;
            try { stem = Path.GetFileNameWithoutExtension(ctx.Doc.Name); } catch (Exception) { }
            var file = MaterialLogic.ImagePath(p.Str("output", null), drawingDir, stem, name ?? v.Name);
            if (!Directory.Exists(Path.GetDirectoryName(file))) throw ToolException.Invalid($"{tool}: folder '{Path.GetDirectoryName(file)}' does not exist.");
            if (File.Exists(file) && !p.Bool("replace", false))
                throw ToolException.Invalid($"{tool}: '{file}' already exists. Pass replace: true to overwrite it (ask the user first), or another output.");
            var r = Capture.Render(ctx.Doc, min, max, size, min.HasValue ? "handles" : "extents", v, style);
            try { File.WriteAllBytes(file, r.Png); }
            catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not write '{file}' ({ex.Message})."); }
            return new Dictionary<string, object>
            {
                ["file"] = file,
                ["width"] = r.Width,
                ["height"] = r.Height,
                ["view"] = name ?? v.Name,
                ["visual_style"] = style,
                ["note"] = "Saved off-screen (the user's view is unchanged). Materials show in the Realistic style; tell the user where the file is.",
            };
        }
    }
}
