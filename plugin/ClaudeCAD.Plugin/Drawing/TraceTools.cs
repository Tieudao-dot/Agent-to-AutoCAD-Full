using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt G5 (gói 2D / Full): trace_image — "vẽ lại bản này" from a photo, a scan or a PDF scan. attach puts the
    /// picture (IMAGEATTACH) or the PDF page (PDFATTACH) under the drawing on its own locked, non-plotted layer; scale makes a
    /// known length real; straighten turns a skewed photo; view gives Claude a look of the picture (and what was drawn on it)
    /// with a grid of drawing coordinates; info lists the pictures. Claude then draws with the usual tools on top.
    /// </summary>
    public static partial class DrawingService
    {
        private const string TraceKey = "CLAUDECAD_TRACE";

        private static void RegisterTrace(ToolDispatcher d)
        {
            // its own transactions: view renders off-screen like capture_view (no open transaction)
            d.Register(new ToolDef("trace_image", true, TraceImage, noTransaction: true,
                readsWhen: p => TraceLogic.Reads((p.Str("action", "") ?? "").Trim().ToLowerInvariant())));
        }

        internal static object TraceImage(ToolContext ctx, Params p)
        {
            const string tool = "trace_image";
            var action = TraceLogic.NormalizeAction(p.Str("action"));
            var frame = ctx.Dispatcher?.CurrentFrame ?? UcsFrame.World;
            if (!frame.ZUp) throw ToolException.Invalid($"{tool}: the UCS is tilted out of plan. Tracing works in plan: call reset_ucs (or a UCS only moved / turned in plan) first.");
            if (Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("TILEMODE")) != 1)
                throw ToolException.Invalid($"{tool}: works on the Model tab. Switch with set_current_layout {{\"name\": \"Model\"}} first.");
            switch (action)
            {
                case "attach": return TraceAttach(ctx, p, tool);
                case "scale": return TraceTransform(ctx, p, tool, true);
                case "straighten": return TraceTransform(ctx, p, tool, false);
                case "view": return TraceView(ctx, p, tool, frame);
                default: return TraceInfo(ctx, tool);
            }
        }

        private static double MmPerUnitOf(Database db) => FloorPlanLogic.MmPerUnit(Convert.ToInt32(db.Insunits), out _);

        // ================================================================= attach

        private static object TraceAttach(ToolContext ctx, Params p, string tool)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            string path = (p.Str("path", null) ?? "").Trim().Trim('"');
            string chatDir = AttachmentFolder();
            CheckPathAllowed(ctx, path, chatDir, tool); // the raw path (before Windows touches it)
            bool exists = false;
            long bytes = 0;
            try { path = Path.GetFullPath(path); exists = File.Exists(path); if (exists) bytes = new FileInfo(path).Length; } catch (Exception) { }
            CheckPathAllowed(ctx, path, chatDir, tool); // and the full path
            string kind = TraceLogic.KindOf(path, exists, bytes, tool);
            // Test & sửa G5: the content must be what the name says (a phone photo saved as .png may be WebP / HEIC)
            var head = new byte[1024];
            int got = 0;
            try { using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) got = fs.Read(head, 0, head.Length); }
            catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not read '{Path.GetFileName(path)}' ({ex.Message})."); }
            TraceLogic.CheckContent(path, kind, head.Take(got).ToArray(), tool);
            int page = 1, pages = 0;
            if (kind == "pdf")
            {
                try { pages = TraceLogic.PdfPageCount(File.ReadAllBytes(path)); } catch (Exception) { }
                page = TraceLogic.PageOf(p.Has("page") ? (int?)(int)p.Num("page") : null, pages, tool);
            }
            // a picture pasted into the chat lives in the palette's own folder: a copy next to the drawing keeps it with the drawing
            string source = path;
            if (chatDir != null && TraceLogic.IsInside(path, chatDir)) // Sửa KFG: the folder with its separator
            {
                // Sửa KFG (nghi vấn G5): a drawing never saved has no folder; the copy would stay in Documents when it is saved elsewhere
                if (!ctx.Doc.IsNamedDrawing)
                    throw ToolException.Invalid($"{tool}: the drawing is not saved yet, so the pasted picture has no folder to be kept in. Ask the user to save the drawing (Ctrl+S) first, then attach again.");
                var dest = Path.Combine(DrawingFolder(ctx), TraceLogic.CopyName(DrawingStem(ctx), path, F5Confirm.FileToken(path).Substring(0, 8)));
                try { if (!File.Exists(dest)) File.Copy(path, dest); source = dest; }
                catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not copy the picture next to the drawing ({ex.Message})."); }
            }
            double mpu = MmPerUnitOf(db);
            Point3d at;
            if (p.Has("insert")) { var q = p.Point("insert"); at = new Point3d(q[0], q[1], 0); } // Test & sửa G5: a UCS point
            else
            {
                var ext = Capture.ModelExtents(doc);
                var ip = TraceLogic.InsertPoint(ext.HasValue ? new[] { ext.Value.MinPoint.X, ext.Value.MinPoint.Y } : null, ext.HasValue ? new[] { ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y } : null, mpu);
                at = new Point3d(ip[0], ip[1], 0);
            }
            double width = TraceLogic.DefaultWidth(mpu);
            string handle;
            double w, h;
            int pxW = 0, pxH = 0;
            (Point3d min, Point3d max) corners;
            var frame = ctx.Dispatcher?.CurrentFrame ?? UcsFrame.World;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var tctx = new ToolContext(doc, tr, true, ctx.Dispatcher);
                var layerId = EnsureLayerF2(tctx, G5Numbers.Layer, G5Numbers.LayerColor, false);
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                Entity ent;
                if (kind == "raster")
                {
                    var dictId = RasterImageDef.GetImageDictionary(db);
                    if (dictId.IsNull) dictId = RasterImageDef.CreateImageDictionary(db);
                    var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
                    var def = new RasterImageDef { SourceFileName = source };
                    try { def.Load(); }
                    catch (Exception ex) { def.Dispose(); throw ToolException.Invalid($"{tool}: AutoCAD could not read the picture '{Path.GetFileName(source)}' ({ex.Message}). Ask the user to save it again as JPG or PNG."); }
                    string key = RasterImageDef.SuggestName(dict, source);
                    var defId = dict.SetAt(key, def);
                    tr.AddNewlyCreatedDBObject(def, true);
                    pxW = (int)def.Size.X;
                    pxH = (int)def.Size.Y;
                    if (pxW <= 0 || pxH <= 0) throw ToolException.Invalid($"{tool}: the picture has no size; ask the user for another file.");
                    w = width;
                    h = width * pxH / pxW;
                    var img = new RasterImage { ImageDefId = defId, ShowImage = true };
                    img.SetDatabaseDefaults(db);
                    // KFG-05: along the axes of the user's UCS (a UCS turned in plan: the picture turned with it)
                    var ux = new Vector3d(frame.X[0], frame.X[1], 0).GetNormal();
                    var uy = new Vector3d(frame.Y[0], frame.Y[1], 0).GetNormal();
                    img.Orientation = new CoordinateSystem3d(at, ux * w, uy * h);
                    img.LayerId = layerId;
                    ms.AppendEntity(img);
                    tr.AddNewlyCreatedDBObject(img, true);
                    img.AssociateRasterDef(def);
                    ent = img;
                }
                else
                {
                    string dkey = UnderlayDefinition.GetDictionaryKey(typeof(PdfDefinition));
                    var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                    DBDictionary pdfDict;
                    if (nod.Contains(dkey)) pdfDict = (DBDictionary)tr.GetObject(nod.GetAt(dkey), OpenMode.ForWrite);
                    else { pdfDict = new DBDictionary(); nod.SetAt(dkey, pdfDict); tr.AddNewlyCreatedDBObject(pdfDict, true); }
                    var def = new PdfDefinition { SourceFileName = source, ItemName = page.ToString(CultureInfo.InvariantCulture) };
                    string name = Path.GetFileNameWithoutExtension(source) + "-" + page.ToString(CultureInfo.InvariantCulture);
                    for (int i = 2; pdfDict.Contains(name); i++) name = Path.GetFileNameWithoutExtension(source) + "-" + page.ToString(CultureInfo.InvariantCulture) + "-" + i.ToString(CultureInfo.InvariantCulture);
                    var defId = pdfDict.SetAt(name, def);
                    tr.AddNewlyCreatedDBObject(def, true);
                    try { def.Load(null); }
                    catch (Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not open page {page} of '{Path.GetFileName(source)}' ({ex.Message}; a password-protected PDF cannot be used)."); }
                    var r = new PdfReference { DefinitionId = defId, Position = at, ScaleFactors = new Scale3d(1) };
                    r.SetDatabaseDefaults(db);
                    r.LayerId = layerId;
                    ms.AppendEntity(r);
                    tr.AddNewlyCreatedDBObject(r, true);
                    // the page at scale 1 → the default width
                    var e0 = r.GeometricExtents;
                    double w0 = e0.MaxPoint.X - e0.MinPoint.X;
                    if (w0 > 1e-9) r.TransformBy(Matrix3d.Scaling(width / w0, at));
                    var e1 = r.GeometricExtents;
                    w = e1.MaxPoint.X - e1.MinPoint.X;
                    h = e1.MaxPoint.Y - e1.MinPoint.Y;
                    // KFG-05: turned with a UCS turned in plan (around its lower-left corner)
                    if (!frame.IsWorld && Math.Abs(frame.PlanAngleDeg) > 1e-9) r.TransformBy(Matrix3d.Rotation(frame.PlanAngleDeg * Math.PI / 180, Vector3d.ZAxis, at));
                    ent = r;
                }
                MarkTrace(tr, ent, source, page, false, 0);
                handle = Acad.H(ent.ObjectId);
                corners = Corners(ent);
                if (ent is PdfReference && !frame.IsWorld && Math.Abs(frame.PlanAngleDeg) > 1e-9)
                {
                    var c = Math.Cos(frame.PlanAngleDeg * Math.PI / 180);
                    var sn = Math.Sin(frame.PlanAngleDeg * Math.PI / 180);
                    corners = (at, at + new Vector3d(w * c - h * sn, w * sn + h * c, 0));
                }
                // locked: Claude's lines never move it; it shows faded under the drawing
                var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                ltr.IsLocked = true;
                tr.Commit();
            }
            var res = new Dictionary<string, object>
            {
                ["handle"] = handle,
                ["kind"] = kind,
                ["file"] = source,
                ["layer"] = G5Numbers.Layer,
                // Test & sửa G5: points out in the user's UCS (the dispatcher boxes min / max in it), like the grid labels
                ["min"] = Acad.J(corners.min),
                ["max"] = Acad.J(corners.max),
                ["scaled"] = false,
                ["note"] = "Attached (locked, not plotted). It is NOT to scale yet: its width was set to " + G5Numbers.DefaultWidthMm.ToString("0", CultureInfo.InvariantCulture) +
                           " mm only to show it. Look at it with action view, find a length you know (a dimension written on the scan, or ask the user), then action scale. " + TraceLogic.DraftNote,
            };
            // Sửa KFG-13: a pasted picture is the copy sent to Claude (≤ 1568 px): small text of a big scan may be lost
            if (chatDir != null && TraceLogic.IsInside(path, chatDir))
                res["pasted_note"] = $"This picture was pasted in the chat, so it is kept at most {G5Numbers.PastedMaxSide} px wide. For a large scan with small text, ask the user to drag the file itself into the chat (or give its path) for full detail.";
            if (kind == "raster") { res["pixels"] = new List<object> { pxW, pxH }; }
            else
            {
                res["page"] = page;
                if (pages > 0) res["pages"] = pages;
                // Sửa KFG-11: a PDF whose pages could not be counted (compressed): said, so Claude checks it is the right page
                else res["pages_note"] = $"The number of pages of this PDF could not be read; page {page} was attached. Tell the user, and ask which page if the PDF may have several.";
                res["pdf_note"] = "If this PDF was made by a CAD program (vector lines, not a scan), the PDFIMPORT command of AutoCAD turns its lines into drawing objects directly: suggest it to the user instead of tracing.";
            }
            return res;
        }

        /// <summary>
        /// Sửa KFG-05: the corners of a traced picture whose UCS images are its lower-left and upper-right corners in the UCS: a
        /// raster picture by its orientation (origin, origin + width + height), a PDF by its box.
        /// </summary>
        private static (Point3d min, Point3d max) Corners(Entity e)
        {
            if (e is RasterImage img)
            {
                var cs = img.Orientation;
                return (cs.Origin, cs.Origin + cs.Xaxis + cs.Yaxis);
            }
            var x = e.GeometricExtents;
            return (new Point3d(x.MinPoint.X, x.MinPoint.Y, 0), new Point3d(x.MaxPoint.X, x.MaxPoint.Y, 0));
        }

        /// <summary>Sửa KFG (nghi vấn G5, bảo mật): no file of another computer of the network (Windows may send the sign-in there).</summary>
        private static void CheckPathAllowed(ToolContext ctx, string path, string chatDir, string tool)
        {
            if (string.IsNullOrWhiteSpace(path) || (chatDir != null && TraceLogic.IsInside(path, chatDir))) return;
            string drawing = null;
            try { drawing = ctx.Doc.IsNamedDrawing ? ctx.Doc.Name : null; } catch (Exception) { }
            bool network = false, same = false;
            try
            {
                if (TraceLogic.UncShare(path) == null && Path.IsPathRooted(path))
                {
                    var root = Path.GetPathRoot(path);
                    if (!string.IsNullOrEmpty(root) && root.Length >= 2 && root[1] == ':')
                    {
                        network = new DriveInfo(root.Substring(0, 1)).DriveType == DriveType.Network;
                        same = drawing != null && string.Equals(Path.GetPathRoot(drawing), root, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception) { }
            var refusal = TraceLogic.PathRefusal(path, drawing, network, same, tool);
            if (refusal != null) throw ToolException.Invalid(refusal);
        }

        private static string AttachmentFolder()
        {
            try { return Path.GetFullPath(UI.ChatAttachments.Folder); } catch (Exception) { return null; }
        }

        private static void MarkTrace(Transaction tr, Entity ent, string file, int page, bool scaled, double realUnits)
        {
            if (ent.ExtensionDictionary.IsNull) ent.CreateExtensionDictionary();
            var ed = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForWrite);
            Xrecord xr;
            if (ed.Contains(TraceKey)) xr = (Xrecord)tr.GetObject(ed.GetAt(TraceKey), OpenMode.ForWrite);
            else { xr = new Xrecord(); ed.SetAt(TraceKey, xr); tr.AddNewlyCreatedDBObject(xr, true); }
            xr.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, file ?? ""),
                new TypedValue((int)DxfCode.Int32, page),
                new TypedValue((int)DxfCode.Int16, (short)(scaled ? 1 : 0)),
                new TypedValue((int)DxfCode.Real, realUnits));
        }

        private sealed class TraceMark { public string File; public int Page; public bool Scaled; public double Real; }

        private static TraceMark ReadMark(Transaction tr, Entity ent)
        {
            if (ent.ExtensionDictionary.IsNull) return null;
            var ed = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead);
            if (!ed.Contains(TraceKey)) return null;
            var v = ((Xrecord)tr.GetObject(ed.GetAt(TraceKey), OpenMode.ForRead)).Data?.AsArray();
            if (v == null || v.Length < 4) return null;
            return new TraceMark { File = v[0].Value as string, Page = Convert.ToInt32(v[1].Value), Scaled = Convert.ToInt32(v[2].Value) == 1, Real = Convert.ToDouble(v[3].Value) };
        }

        /// <summary>The picture the call means: its handle, else the only traced picture of the drawing.</summary>
        private static ObjectId TraceTarget(Transaction tr, Database db, Params p, string tool)
        {
            if (p.Has("handle"))
            {
                var id = Acad.IdFromHandle(db, p.Str("handle"));
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (!(e is RasterImage) && !(e is PdfReference)) throw ToolException.Invalid($"{tool}: {p.Str("handle")} is not a picture or PDF attached with trace_image.");
                // KFG-06: a picture of the user (logo, xref image…) is never scaled / turned / looked at as a trace
                if (ReadMark(tr, e) == null) throw ToolException.Invalid($"{tool}: {p.Str("handle")} is a picture of the drawing that trace_image did not attach; it is left as it is. Attach the scan with action attach (action info lists the traced pictures).");
                return id;
            }
            var all = TracePictures(tr, db);
            if (all.Count == 1) return all[0];
            if (all.Count == 0) throw ToolException.Invalid($"{tool}: no picture is attached yet. Call trace_image action attach with the file path first.");
            throw ToolException.Invalid($"{tool}: {all.Count} pictures are attached; give handle (call action info).");
        }

        private static List<ObjectId> TracePictures(Transaction tr, Database db)
        {
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            var r = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                if (id.ObjectClass.DxfName != "IMAGE" && id.ObjectClass.DxfName != "PDFUNDERLAY") continue;
                if (tr.GetObject(id, OpenMode.ForRead) is Entity e && ReadMark(tr, e) != null) r.Add(id);
            }
            return r;
        }

        // ================================================================= scale / straighten

        private static object TraceTransform(ToolContext ctx, Params p, string tool, bool scale)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            var q1 = p.Point("p1"); // Test & sửa G5: UCS points (the grid labels) → WCS
            var q2 = p.Point("p2");
            var p1 = new Point3d(q1[0], q1[1], 0);
            var p2 = new Point3d(q2[0], q2[1], 0);
            double mpu = MmPerUnitOf(db);
            double factor = 1, turn = 0, real = 0;
            if (scale)
            {
                real = TraceLogic.ToUnits(p.Num("distance"), p.Str("unit", "mm"), mpu, tool);
                factor = TraceLogic.ScaleFactor(new[] { p1.X, p1.Y }, new[] { p2.X, p2.Y }, real, tool);
            }
            else
            {
                var fr = ctx.Dispatcher?.CurrentFrame ?? UcsFrame.World;
                turn = TraceLogic.StraightenAngle(new[] { p1.X, p1.Y }, new[] { p2.X, p2.Y }, p.Str("axis", "auto"), fr.IsWorld ? 0 : fr.PlanAngleDeg, tool); // KFG-05: the UCS axis
            }
            string handle;
            (Point3d min, Point3d max) corners;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var id = TraceTarget(tr, db, p, tool);
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var mark = ReadMark(tr, e);
                // the picture's own layer is locked on purpose: unlocked for this change only
                var ltr = (LayerTableRecord)tr.GetObject(e.LayerId, OpenMode.ForWrite);
                bool wasLocked = ltr.IsLocked;
                ltr.IsLocked = false;
                e.UpgradeOpen();
                e.TransformBy(scale ? Matrix3d.Scaling(factor, p1) : Matrix3d.Rotation(turn, Vector3d.ZAxis, p1));
                if (scale && mark != null) MarkTrace(tr, e, mark.File, mark.Page, true, real);
                ltr.IsLocked = wasLocked;
                corners = Corners(e);
                handle = Acad.H(id);
                tr.Commit();
            }
            var r = new Dictionary<string, object>
            {
                ["handle"] = handle,
                ["min"] = Acad.J(corners.min),
                ["max"] = Acad.J(corners.max),
            };
            if (scale)
            {
                r["scale_factor"] = Acad.R(factor);
                r["scaled"] = true;
                r["note"] = $"The picture is now to scale: p1–p2 = {p.Num("distance").ToString("0.###", CultureInfo.InvariantCulture)} {p.Str("unit", "mm")}. Check a second known length with action view before drawing. " + TraceLogic.DraftNote;
            }
            else
            {
                r["turned_degrees"] = Acad.R(turn * 180 / Math.PI);
                r["note"] = "Turned around p1. Scale it (action scale) after straightening, then look again (action view).";
            }
            return r;
        }

        // ================================================================= view

        private static object TraceView(ToolContext ctx, Params p, string tool, UcsFrame frame)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            Point3d min, max;
            string handle;
            bool scaled;
            // Test & sửa G5: the picture file and where it lies, to draw the look from the file itself
            string picFile = null;
            double[] po = null, pu = null, pv = null;
            double picW = 0, picH = 0;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var id = TraceTarget(tr, db, p, tool);
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                scaled = ReadMark(tr, e)?.Scaled == true;
                if (e is RasterImage img && tr.GetObject(img.ImageDefId, OpenMode.ForRead) is RasterImageDef rdef)
                {
                    var cs = img.Orientation;
                    po = new[] { cs.Origin.X, cs.Origin.Y };
                    pu = new[] { cs.Xaxis.X, cs.Xaxis.Y };
                    pv = new[] { cs.Yaxis.X, cs.Yaxis.Y };
                    picW = rdef.Size.X;
                    picH = rdef.Size.Y;
                    picFile = SafeString(() => rdef.ActiveFileName);
                    if (string.IsNullOrEmpty(picFile) || !File.Exists(picFile)) picFile = SafeString(() => rdef.SourceFileName);
                }
                var ext = e.GeometricExtents;
                handle = Acad.H(id);
                min = ext.MinPoint;
                max = ext.MaxPoint;
                tr.Commit();
            }
            if (p.Has("p1") && p.Has("p2"))
            {
                // Test & sửa G5: p1 / p2 are opposite corners in the UCS: the WCS box of the four corners
                var a = p.Numbers("p1");
                var b = p.Numbers("p2");
                var cs4 = new[] { new[] { a[0], a[1] }, new[] { b[0], a[1] }, new[] { b[0], b[1] }, new[] { a[0], b[1] } }
                    .Select(c => UcsScope.ConvertInput("p1", new[] { c[0], c[1], 0.0 })).ToList();
                min = new Point3d(cs4.Min(c => c[0]), cs4.Min(c => c[1]), 0);
                max = new Point3d(cs4.Max(c => c[0]), cs4.Max(c => c[1]), 0);
                if (Math.Abs(a[0] - b[0]) < 1e-9 || Math.Abs(a[1] - b[1]) < 1e-9) throw ToolException.Invalid($"{tool}: p1 and p2 must be two opposite corners of the part to look at.");
            }
            int size = p.Has("size") ? (int)p.Num("size") : G5Numbers.ViewSize;
            size = Math.Max(Capture.MinSize, Math.Min(G5Numbers.MaxViewSize, size));
            var cap = Capture.Render(doc, min, max, size, "window");
            double span = Math.Max(cap.Max.X - cap.Min.X, cap.Max.Y - cap.Min.Y);
            double step = p.Has("grid_step") ? p.Num("grid_step") : TraceLogic.GridStep(span, MmPerUnitOf(db));
            bool grid = p.Bool("grid", true);
            byte[] png = cap.Png;
            // Test & sửa G5: the picture comes from its file (AutoCAD's off-screen look may leave images out), what was drawn on top
            var look = ComposeLook(cap, picFile, po, pu, pv, picW, picH, min, max);
            png = look.Png;
            int lines = 0;
            if (grid)
            {
                var g = TraceLogic.Grid(new[] { cap.Min.X, cap.Min.Y }, new[] { cap.Max.X, cap.Max.Y }, cap.Width, cap.Height, step,
                    frame.IsWorld ? new double[] { 0, 0 } : new[] { frame.O[0], frame.O[1] }, frame.IsWorld ? 0 : frame.PlanAngleDeg);
                lines = g.Count;
                png = DrawGrid(png, g);
            }
            var res = new Dictionary<string, object>
            {
                ["image_png_base64"] = Convert.ToBase64String(png),
                ["handle"] = handle,
                ["width"] = cap.Width,
                ["height"] = cap.Height,
                // the part shown (corners; points of results come back in the user's UCS like the grid labels)
                ["window"] = new Dictionary<string, object>
                {
                    ["min"] = Acad.J(new Point3d(cap.Min.X, cap.Min.Y, 0)),
                    ["max"] = Acad.J(new Point3d(cap.Max.X, cap.Max.Y, 0)),
                },
                ["grid_step"] = Acad.R(step),
                ["grid_lines"] = lines,
                ["units_per_pixel"] = Acad.R((cap.Max.X - cap.Min.X) / Math.Max(1, cap.Width)),
                ["scaled"] = scaled,
                ["note"] = (grid ? "Grid labels are the coordinates the drawing tools take (current UCS, drawing units). " : "") +
                           (scaled ? "" : "The picture is NOT to scale yet (action scale). ") +
                           "Look closer at a part with p1 / p2 instead of a bigger size. " + TraceLogic.DraftNote,
                ["picture_from"] = look.From,
                ["autocad_drew_picture"] = look.AutocadDrew,
            };
            if (look.Blank)
            {
                res["picture_visible"] = false;
                res["warning"] = "This look shows NO picture (AutoCAD did not draw it in the off-screen view, and a PDF page cannot be drawn from its file). " +
                                 "Do not trace from it and do not guess: tell the user, and ask them to export the page as PNG / JPG and attach that instead.";
            }
            return res;
        }

        private sealed class LookResult { public byte[] Png; public string From; public bool Blank, AutocadDrew; }

        /// <summary>
        /// Test & sửa G5: the look of a traced picture. A raster picture is drawn from its file (where it lies in the window),
        /// with AutoCAD's look on top where something is drawn (its background made transparent), so the scan shows whether
        /// or not AutoCAD draws images off screen. A PDF (or a file that cannot be read): AutoCAD's look alone, and Blank when
        /// it shows nothing over the picture.
        /// </summary>
        private static LookResult ComposeLook(CaptureResult cap, string picFile, double[] po, double[] pu, double[] pv, double picW, double picH, Point3d pmin, Point3d pmax)
        {
            var conv = new FormatConvertedBitmap(Decode(cap.Png), PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            var bg = TraceLogic.LookBackgroundBgr; // Sửa KFG: the known background, never guessed from the border (zoomed inside a page)
            double[] wmin = { cap.Min.X, cap.Min.Y }, wmax = { cap.Max.X, cap.Max.Y };
            // is there anything drawn by AutoCAD over the picture? (≈ 0: it left the image out of its off-screen look)
            double sx = (wmax[0] - wmin[0]) / w, sy = (wmax[1] - wmin[1]) / h;
            double share = TraceLogic.DrawnShare(px, w, h, bg, (pmin.X - wmin[0]) / sx, (wmax[1] - pmax.Y) / sy, (pmax.X - wmin[0]) / sx, (wmax[1] - pmin.Y) / sy);
            bool drew = share >= TraceLogic.BlankShare;
            if (picFile != null && po != null && picW > 0 && picH > 0 && File.Exists(picFile))
            {
                try
                {
                    double uLen = Math.Sqrt(pu[0] * pu[0] + pu[1] * pu[1]);
                    int dw = TraceLogic.DecodeWidth(picW, (wmax[0] - wmin[0]) / Math.Max(1e-9, uLen), w);
                    var bi = new BitmapImage();
                    using (var fs = new FileStream(picFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bi.DecodePixelWidth = dw;
                        bi.StreamSource = fs;
                        bi.EndInit();
                    }
                    bi.Freeze();
                    var m = TraceLogic.PictureToLook(po, pu, pv, bi.PixelWidth, bi.PixelHeight, wmin, wmax, w, h);
                    TraceLogic.ClearBackground(px, bg);
                    var top = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)bg[2], (byte)bg[1], (byte)bg[0])), null, new Rect(0, 0, w, h));
                        dc.PushTransform(new MatrixTransform(m[0], m[1], m[2], m[3], m[4], m[5]));
                        dc.DrawImage(bi, new Rect(0, 0, bi.PixelWidth, bi.PixelHeight));
                        dc.Pop();
                        dc.DrawImage(top, new Rect(0, 0, w, h));
                    }
                    return new LookResult { Png = EncodePng(dv, w, h), From = "file", AutocadDrew = drew };
                }
                catch (Exception ex) { Logger.Warn("trace_image: look from the file failed: " + ex.Message); }
            }
            return new LookResult { Png = cap.Png, From = "autocad", Blank = !drew, AutocadDrew = drew };
        }

        private static BitmapSource Decode(byte[] png)
        {
            using (var ms = new MemoryStream(png))
                return new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }

        private static byte[] EncodePng(DrawingVisual dv, int w, int h)
        {
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var o = new MemoryStream())
            {
                enc.Save(o);
                return o.ToArray();
            }
        }

        /// <summary>The grid drawn over the look: thin cyan lines, their value at the top / left edge.</summary>
        private static byte[] DrawGrid(byte[] png, List<TraceLogic.GridLine> lines)
        {
            BitmapSource src;
            using (var ms = new MemoryStream(png))
            {
                var dec = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                src = dec.Frames[0];
            }
            int w = src.PixelWidth, h = src.PixelHeight;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(src, new Rect(0, 0, w, h));
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 200, 255)), 1);
                pen.Freeze();
                var face = new Typeface("Segoe UI");
                var back = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0));
                back.Freeze();
                foreach (var l in lines)
                {
                    dc.DrawLine(pen, new System.Windows.Point(l.X1, l.Y1), new System.Windows.Point(l.X2, l.Y2));
                    var ft = new FormattedText((l.IsX ? "x=" : "y=") + l.Label, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, face, 11, Brushes.Yellow, 1.0);
                    // the end nearest the top (x lines) / the left (y lines)
                    bool firstEnd = l.IsX ? l.Y1 <= l.Y2 : l.X1 <= l.X2;
                    double x = firstEnd ? l.X1 : l.X2, y = firstEnd ? l.Y1 : l.Y2;
                    x = Math.Max(0, Math.Min(w - ft.Width - 2, x + 2));
                    y = Math.Max(0, Math.Min(h - ft.Height - 1, y + 1));
                    dc.DrawRectangle(back, null, new Rect(x - 1, y, ft.Width + 2, ft.Height));
                    dc.DrawText(ft, new System.Windows.Point(x, y));
                }
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var o = new MemoryStream())
            {
                enc.Save(o);
                return o.ToArray();
            }
        }

        // ================================================================= self-test

        /// <summary>
        /// The self-test of G5: a made-up 400 × 200 px plan (white with a black frame) attached, scaled so its width is 8 m,
        /// looked at (the picture must show in the look: AutoCAD's off-screen view draws images), listed, then erased.
        /// </summary>
        internal static List<string> SelfTestTrace(Document doc, string dir, out string info)
        {
            var errs = new List<string>();
            string png = Path.Combine(dir, "g5-trace.png");
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 400, 200));
                dc.DrawRectangle(null, new Pen(Brushes.Black, 8), new Rect(20, 20, 360, 160));
            }
            var rtb = new RenderTargetBitmap(400, 200, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var f = File.Create(png)) enc.Save(f);
            var ctx = new ToolContext(doc, null, true, null);
            Dictionary<string, object> Call(Dictionary<string, object> q) => (Dictionary<string, object>)TraceImage(ctx, new Params("trace_image", q));
            var a = Call(new Dictionary<string, object> { ["action"] = "attach", ["path"] = png, ["insert"] = new List<object> { 300000.0, 0.0 } });
            string h = (string)a["handle"];
            var min = (List<object>)a["min"];
            var max = (List<object>)a["max"];
            double x0 = Convert.ToDouble(min[0]), x1 = Convert.ToDouble(max[0]), y0 = Convert.ToDouble(min[1]);
            var s = Call(new Dictionary<string, object> { ["action"] = "scale", ["handle"] = h, ["p1"] = new List<object> { x0, y0 }, ["p2"] = new List<object> { x1, y0 }, ["distance"] = 8.0, ["unit"] = "m" });
            var smax = (List<object>)s["max"];
            double wantW = 8000 / MmPerUnitOf(doc.Database);
            if (Math.Abs(Convert.ToDouble(smax[0]) - x0 - wantW) > 1e-3 * wantW) errs.Add($"tỉ lệ: rộng {Convert.ToDouble(smax[0]) - x0:0.##} (cần {wantW:0.##})");
            var v = Call(new Dictionary<string, object> { ["action"] = "view", ["handle"] = h });
            var bytes = Convert.FromBase64String((string)v["image_png_base64"]);
            int bright = 0, total = 0;
            using (var ms = new MemoryStream(bytes))
            {
                var fr = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                var conv = new FormatConvertedBitmap(fr, PixelFormats.Bgra32, null, 0);
                int stride = conv.PixelWidth * 4;
                var px = new byte[stride * conv.PixelHeight];
                conv.CopyPixels(px, stride, 0);
                for (int i = 0; i < px.Length; i += 4 * 7) { total++; if (px[i] > 200 && px[i + 1] > 200 && px[i + 2] > 200) bright++; }
            }
            if (bright < total / 10) errs.Add("ảnh xem không thấy ảnh nền (cả từ file lẫn từ AutoCAD)");
            var inf = Call(new Dictionary<string, object> { ["action"] = "info" });
            if (Convert.ToInt32(inf["count"]) < 1) errs.Add("info: không thấy ảnh đã gắn");
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var e = (Entity)tr.GetObject(Acad.IdFromHandle(doc.Database, h), OpenMode.ForRead);
                var ltr = (LayerTableRecord)tr.GetObject(e.LayerId, OpenMode.ForWrite);
                if (!ltr.IsLocked) errs.Add("layer ảnh nền không khóa");
                if (ltr.IsPlottable) errs.Add("layer ảnh nền vẫn in");
                bool was = ltr.IsLocked;
                ltr.IsLocked = false;
                e.UpgradeOpen();
                e.Erase();
                ltr.IsLocked = was;
                tr.Commit();
            }
            // Test & sửa G5: the look comes from the file; say whether AutoCAD itself drew the picture off screen (checklist G5)
            if (!Equals(v["picture_from"], "file")) errs.Add($"ảnh xem không dựng từ file ảnh gốc ({v["picture_from"]})");
            info = $"ảnh {string.Join("×", (List<object>)a["pixels"])} px, rộng 8 m sau khi đặt tỉ lệ, lưới {v["grid_lines"]} đường, {bright * 100 / Math.Max(1, total)}% điểm sáng, " +
                   (Equals(v["autocad_drew_picture"], true) ? "AutoCAD có vẽ ảnh nền khi chụp" : "AutoCAD KHÔNG vẽ ảnh nền khi chụp (ảnh xem lấy từ file)");
            return errs;
        }

        // ================================================================= info

        private static object TraceInfo(ToolContext ctx, string tool)
        {
            var db = ctx.Db;
            var list = new List<object>();
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in TracePictures(tr, db))
                {
                    var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                    var m = ReadMark(tr, e);
                    var ext = e.GeometricExtents;
                    list.Add(new Dictionary<string, object>
                    {
                        ["handle"] = Acad.H(id),
                        ["kind"] = e is PdfReference ? "pdf" : "raster",
                        ["file"] = m?.File,
                        ["page"] = m?.Page ?? 1,
                        ["scaled"] = m?.Scaled == true,
                        ["min"] = Acad.J(new Point3d(ext.MinPoint.X, ext.MinPoint.Y, 0)),
                        ["max"] = Acad.J(new Point3d(ext.MaxPoint.X, ext.MaxPoint.Y, 0)),
                    });
                }
                tr.Commit();
            }
            return new Dictionary<string, object> { ["pictures"] = list, ["count"] = list.Count, ["layer"] = G5Numbers.Layer };
        }
    }
}
