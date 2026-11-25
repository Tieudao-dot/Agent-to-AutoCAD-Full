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
            bool exists = false;
            long bytes = 0;
            try { path = Path.GetFullPath(path); exists = File.Exists(path); if (exists) bytes = new FileInfo(path).Length; } catch (Exception) { }
            string kind = TraceLogic.KindOf(path, exists, bytes, tool);
            int page = 1, pages = 0;
            if (kind == "pdf")
            {
                try { pages = TraceLogic.PdfPageCount(File.ReadAllBytes(path)); } catch (Exception) { }
                page = TraceLogic.PageOf(p.Has("page") ? (int?)(int)p.Num("page") : null, pages, tool);
            }
            // a picture pasted into the chat lives in the palette's own folder: a copy next to the drawing keeps it with the drawing
            string source = path;
            string chatDir = AttachmentFolder();
            if (chatDir != null && path.StartsWith(chatDir, StringComparison.OrdinalIgnoreCase))
            {
                var dest = Path.Combine(DrawingFolder(ctx), TraceLogic.CopyName(DrawingStem(ctx), path, F5Confirm.FileToken(path).Substring(0, 8)));
                try { if (!File.Exists(dest)) File.Copy(path, dest); source = dest; }
                catch (Exception ex) { throw ToolException.Invalid($"{tool}: could not copy the picture next to the drawing ({ex.Message})."); }
            }
            double mpu = MmPerUnitOf(db);
            Point3d at;
            if (p.Has("insert")) { var q = p.Numbers("insert"); at = new Point3d(q[0], q[1], 0); }
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
                    img.Orientation = new CoordinateSystem3d(at, new Vector3d(w, 0, 0), new Vector3d(0, h, 0));
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
                    ent = r;
                }
                MarkTrace(tr, ent, source, page, false, 0);
                handle = Acad.H(ent.ObjectId);
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
                ["min"] = new List<object> { Acad.R(at.X), Acad.R(at.Y) },
                ["max"] = new List<object> { Acad.R(at.X + w), Acad.R(at.Y + h) },
                ["scaled"] = false,
                ["note"] = "Attached (locked, not plotted). It is NOT to scale yet: its width was set to " + G5Numbers.DefaultWidthMm.ToString("0", CultureInfo.InvariantCulture) +
                           " mm only to show it. Look at it with action view, find a length you know (a dimension written on the scan, or ask the user), then action scale. " + TraceLogic.DraftNote,
            };
            if (kind == "raster") { res["pixels"] = new List<object> { pxW, pxH }; }
            else
            {
                res["page"] = page;
                if (pages > 0) res["pages"] = pages;
                res["pdf_note"] = "If this PDF was made by a CAD program (vector lines, not a scan), the PDFIMPORT command of AutoCAD turns its lines into drawing objects directly: suggest it to the user instead of tracing.";
            }
            return res;
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
            var q1 = p.Numbers("p1");
            var q2 = p.Numbers("p2");
            var p1 = new Point3d(q1[0], q1[1], 0);
            var p2 = new Point3d(q2[0], q2[1], 0);
            double mpu = MmPerUnitOf(db);
            double factor = 1, turn = 0, real = 0;
            if (scale)
            {
                real = TraceLogic.ToUnits(p.Num("distance"), p.Str("unit", "mm"), mpu, tool);
                factor = TraceLogic.ScaleFactor(new[] { p1.X, p1.Y }, new[] { p2.X, p2.Y }, real, tool);
            }
            else turn = TraceLogic.StraightenAngle(new[] { p1.X, p1.Y }, new[] { p2.X, p2.Y }, p.Str("axis", "auto"), tool);
            string handle;
            Extents3d ext;
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
                ext = e.GeometricExtents;
                handle = Acad.H(id);
                tr.Commit();
            }
            var r = new Dictionary<string, object>
            {
                ["handle"] = handle,
                ["min"] = new List<object> { Acad.R(ext.MinPoint.X), Acad.R(ext.MinPoint.Y) },
                ["max"] = new List<object> { Acad.R(ext.MaxPoint.X), Acad.R(ext.MaxPoint.Y) },
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
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var id = TraceTarget(tr, db, p, tool);
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                scaled = ReadMark(tr, e)?.Scaled == true;
                var ext = e.GeometricExtents;
                handle = Acad.H(id);
                min = ext.MinPoint;
                max = ext.MaxPoint;
                tr.Commit();
            }
            if (p.Has("p1") && p.Has("p2"))
            {
                var a = p.Numbers("p1");
                var b = p.Numbers("p2");
                min = new Point3d(Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), 0);
                max = new Point3d(Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), 0);
                if (max.X - min.X < 1e-9 || max.Y - min.Y < 1e-9) throw ToolException.Invalid($"{tool}: p1 and p2 must be two opposite corners of the part to look at.");
            }
            int size = p.Has("size") ? (int)p.Num("size") : G5Numbers.ViewSize;
            size = Math.Max(Capture.MinSize, Math.Min(G5Numbers.MaxViewSize, size));
            var cap = Capture.Render(doc, min, max, size, "window");
            double span = Math.Max(cap.Max.X - cap.Min.X, cap.Max.Y - cap.Min.Y);
            double step = p.Has("grid_step") ? p.Num("grid_step") : TraceLogic.GridStep(span, MmPerUnitOf(db));
            bool grid = p.Bool("grid", true);
            byte[] png = cap.Png;
            int lines = 0;
            if (grid)
            {
                var g = TraceLogic.Grid(new[] { cap.Min.X, cap.Min.Y }, new[] { cap.Max.X, cap.Max.Y }, cap.Width, cap.Height, step,
                    frame.IsWorld ? new double[] { 0, 0 } : new[] { frame.O[0], frame.O[1] }, frame.IsWorld ? 0 : frame.PlanAngleDeg);
                lines = g.Count;
                png = DrawGrid(cap.Png, g);
            }
            return new Dictionary<string, object>
            {
                ["image_png_base64"] = Convert.ToBase64String(png),
                ["handle"] = handle,
                ["width"] = cap.Width,
                ["height"] = cap.Height,
                // the part shown (corners; points of results come back in the user's UCS like the grid labels)
                ["window"] = new Dictionary<string, object>
                {
                    ["min"] = new List<object> { Acad.R(cap.Min.X), Acad.R(cap.Min.Y) },
                    ["max"] = new List<object> { Acad.R(cap.Max.X), Acad.R(cap.Max.Y) },
                },
                ["grid_step"] = Acad.R(step),
                ["grid_lines"] = lines,
                ["units_per_pixel"] = Acad.R((cap.Max.X - cap.Min.X) / Math.Max(1, cap.Width)),
                ["scaled"] = scaled,
                ["note"] = (grid ? "Grid labels are the coordinates the drawing tools take (current UCS, drawing units). " : "") +
                           (scaled ? "" : "The picture is NOT to scale yet (action scale). ") +
                           "Look closer at a part with p1 / p2 instead of a bigger size. " + TraceLogic.DraftNote,
            };
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
            if (bright < total / 10) errs.Add("ảnh xem không thấy ảnh nền (AutoCAD không vẽ ảnh trong ảnh chụp ngoài màn hình)");
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
            info = $"ảnh {string.Join("×", (List<object>)a["pixels"])} px, rộng 8 m sau khi đặt tỉ lệ, lưới {v["grid_lines"]} đường, {bright * 100 / Math.Max(1, total)}% điểm sáng";
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
                        ["min"] = new List<object> { Acad.R(ext.MinPoint.X), Acad.R(ext.MinPoint.Y) },
                        ["max"] = new List<object> { Acad.R(ext.MaxPoint.X), Acad.R(ext.MaxPoint.Y) },
                    });
                }
                tr.Commit();
            }
            return new Dictionary<string, object> { ["pictures"] = list, ["count"] = list.Count, ["layer"] = G5Numbers.Layer };
        }
    }
}
