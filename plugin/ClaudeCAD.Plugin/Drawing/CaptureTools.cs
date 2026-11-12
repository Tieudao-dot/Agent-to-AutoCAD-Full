using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsSystem;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using GsView = Autodesk.AutoCAD.GraphicsSystem.View;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>A rendered picture of the drawing (PNG) and the WCS window it shows.</summary>
    public sealed class CaptureResult
    {
        public byte[] Png;
        public int Width, Height;
        public Point3d Min, Max;
        public string Region;    // view | extents | window | handles
        /// <summary>3D picture: the view preset ("sw_iso", "front"... "custom") and visual style; null = plan view.</summary>
        public string View3d;
        public double[] Direction;
        public string VisualStyle;
        /// <summary>Width (mm) the picture covers on the screen plane (3D views; plan views use Max.X - Min.X).</summary>
        public double ScreenWidth;
        public double MmPerPixel => Width <= 0 ? 0 : View3d != null ? ScreenWidth / Width : (Max.X - Min.X) / Width;
    }

    /// <summary>
    /// "Claude tự nhìn bản vẽ": renders model space into an off-screen graphics device, so
    /// the user's own view is never changed (no zoom, no undo step), and returns a PNG.
    /// </summary>
    public static class Capture
    {
        public const int DefaultSize = 1024;
        public const int MinSize = 384;
        public const int MaxSize = 1568;   // Claude downsizes larger images anyway

        /// <summary>
        /// Renders model space. min/max = WCS window (plan) or 3D box (view3d); null = the current view (or the drawing
        /// extents in a layout; with view3d: the model extents). view3d: look along that direction instead of from
        /// the top; visualStyle: dictionary name of a visual style ("Conceptual"...), null = the viewport's own.
        /// </summary>
        public static CaptureResult Render(Document doc, Point3d? min, Point3d? max, int longSide, string region,
            ViewPreset view3d = null, string visualStyle = null)
        {
            if (view3d != null || visualStyle != null)
                return Render3d(doc, min, max, longSide, region, view3d ?? SolidLogic.Preset("top", "capture_view"), visualStyle);

            longSide = Math.Max(MinSize, Math.Min(MaxSize, longSide <= 0 ? DefaultSize : longSide));
            var db = doc.Database;
            if (!min.HasValue)
            {
                bool inModel = Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE")) == 1 || Convert.ToInt32(AcApp.GetSystemVariable("CVPORT")) > 1;
                if (inModel && CurrentViewWindow(doc, out var vmin, out var vmax)) { min = vmin; max = vmax; region = region ?? "view"; }
                else
                {
                    var ext = ModelExtents(doc);
                    if (!ext.HasValue) throw ToolException.Invalid("capture_view: model space is empty; there is nothing to look at.");
                    min = Pad(ext.Value, out var mx); max = mx;
                    region = "extents";
                }
            }
            var a = min.Value;
            var b = max.Value;
            double w = Math.Max(b.X - a.X, 1), h = Math.Max(b.Y - a.Y, 1);
            int pw, ph;
            if (w >= h) { pw = longSide; ph = Math.Max(64, (int)Math.Round(longSide * h / w)); }
            else { ph = longSide; pw = Math.Max(64, (int)Math.Round(longSide * w / h)); }

            var gsm = doc.GraphicsManager;
            var descriptor = new KernelDescriptor();
            descriptor.addRequirement(Autodesk.AutoCAD.UniqueString.Intern("3D Drawing"));
            var kernel = Manager.AcquireGraphicsKernel(descriptor);
            byte[] png;
            try
            {
                using (var view = new GsView())
                {
                    int cvport = Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"));
                    try { gsm.SetViewFromViewport(view, cvport > 1 ? cvport : 2); }
                    catch (Exception ex) { Logger.Warn("capture: SetViewFromViewport failed: " + ex.Message); }
                    // always a plan (top) view of the window
                    view.SetView(new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, 1), new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0), Vector3d.YAxis, w, h);
                    using (var dev = gsm.CreateAutoCADOffScreenDevice(kernel))
                    {
                        dev.OnSize(new System.Drawing.Size(pw, ph));
                        dev.DeviceRenderType = RendererType.Default;
                        dev.BackgroundColor = System.Drawing.Color.FromArgb(33, 40, 48);
                        dev.Add(view);
                        dev.Update();
                        using (var model = gsm.CreateAutoCADModel(kernel))
                        using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            view.Add(ms, model);
                            view.ZoomExtents(new Point3d(a.X, a.Y, 0), new Point3d(b.X, b.Y, 0));
                            dev.Update();
                            using (var bmp = view.GetSnapshot(new System.Drawing.Rectangle(0, 0, pw, ph)))
                            using (var stream = new MemoryStream())
                            {
                                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                png = stream.ToArray();
                            }
                            view.EraseAll();
                            tr.Commit();
                        }
                        dev.Erase(view);
                    }
                }
            }
            finally
            {
                // one reference per capture_view (every visual check) otherwise
                try { Manager.ReleaseGraphicsKernel(kernel); } catch (Exception ex) { Logger.Warn("capture: kernel not released: " + ex.Message); }
            }
            return new CaptureResult { Png = png, Width = pw, Height = ph, Min = a, Max = b, Region = region ?? "window" };
        }

        /// <summary>A 3D picture of the box min–max (default: model extents) seen along v, in a visual style.</summary>
        private static CaptureResult Render3d(Document doc, Point3d? min, Point3d? max, int longSide, string region, ViewPreset v, string visualStyle)
        {
            longSide = Math.Max(MinSize, Math.Min(MaxSize, longSide <= 0 ? DefaultSize : longSide));
            var db = doc.Database;
            if (!min.HasValue || !max.HasValue)
            {
                var ext = ModelExtents(doc);
                if (!ext.HasValue) throw ToolException.Invalid("capture_view: model space is empty; there is nothing to look at.");
                min = ext.Value.MinPoint; max = ext.Value.MaxPoint;
                region = "extents";
            }
            var a = min.Value;
            var b = max.Value;
            var (sw, sh, c) = SolidLogic.ProjectedSize(new[] { a.X, a.Y, a.Z }, new[] { b.X, b.Y, b.Z }, v);
            double w = Math.Max(sw, 1) * 1.1, h = Math.Max(sh, 1) * 1.1;
            int pw, ph;
            if (w >= h) { pw = longSide; ph = Math.Max(64, (int)Math.Round(longSide * h / w)); }
            else { ph = longSide; pw = Math.Max(64, (int)Math.Round(longSide * w / h)); }
            var target = new Point3d(c[0], c[1], c[2]);
            double dist = Math.Max(1000, 4 * (b - a).Length);
            var dir = new Vector3d(v.Direction[0], v.Direction[1], v.Direction[2]).GetNormal();
            var (_, upAxis) = SolidLogic.ScreenAxes(v);
            var up = new Vector3d(upAxis[0], upAxis[1], upAxis[2]);

            var gsm = doc.GraphicsManager;
            var descriptor = new KernelDescriptor();
            descriptor.addRequirement(Autodesk.AutoCAD.UniqueString.Intern("3D Drawing"));
            var kernel = Manager.AcquireGraphicsKernel(descriptor);
            byte[] png;
            try
            {
                using (var view = new GsView())
                {
                    int cvport = Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"));
                    try { gsm.SetViewFromViewport(view, cvport > 1 ? cvport : 2); }
                    catch (Exception ex) { Logger.Warn("capture: SetViewFromViewport failed: " + ex.Message); }
                    if (visualStyle != null)
                    {
                        using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                        {
                            view.VisualStyleId = DrawingService.VisualStyleId(db, tr, visualStyle, "capture_view");
                            tr.Commit();
                        }
                    }
                    view.SetView(target + dir * dist, target, up, w, h);
                    using (var dev = gsm.CreateAutoCADOffScreenDevice(kernel))
                    {
                        dev.OnSize(new System.Drawing.Size(pw, ph));
                        dev.DeviceRenderType = RendererType.Default;
                        dev.BackgroundColor = System.Drawing.Color.FromArgb(33, 40, 48);
                        dev.Add(view);
                        dev.Update();
                        using (var model = gsm.CreateAutoCADModel(kernel))
                        using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            view.Add(ms, model);
                            view.ZoomExtents(a, b);
                            dev.Update();
                            using (var bmp = view.GetSnapshot(new System.Drawing.Rectangle(0, 0, pw, ph)))
                            using (var stream = new MemoryStream())
                            {
                                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                png = stream.ToArray();
                            }
                            view.EraseAll();
                            tr.Commit();
                        }
                        dev.Erase(view);
                    }
                }
            }
            finally
            {
                try { Manager.ReleaseGraphicsKernel(kernel); } catch (Exception ex) { Logger.Warn("capture: kernel not released: " + ex.Message); }
            }
            return new CaptureResult
            {
                Png = png, Width = pw, Height = ph, Min = a, Max = b, Region = region ?? "window",
                View3d = v.Name, Direction = v.Direction, VisualStyle = visualStyle, ScreenWidth = w,
            };
        }

        /// <summary>WCS window of the current model-space view (plan view assumed, like ZoomTo).</summary>
        public static bool CurrentViewWindow(Document doc, out Point3d min, out Point3d max)
        {
            min = max = Point3d.Origin;
            try
            {
                using (var view = doc.Editor.GetCurrentView())
                {
                    var dcs2wcs = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target)
                        * Matrix3d.Displacement(view.Target - Point3d.Origin)
                        * Matrix3d.PlaneToWorld(view.ViewDirection);
                    var c = view.CenterPoint;
                    var p1 = new Point3d(c.X - view.Width / 2, c.Y - view.Height / 2, 0).TransformBy(dcs2wcs);
                    var p2 = new Point3d(c.X + view.Width / 2, c.Y + view.Height / 2, 0).TransformBy(dcs2wcs);
                    min = new Point3d(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), 0);
                    max = new Point3d(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y), 0);
                    return max.X - min.X > 1e-6 && max.Y - min.Y > 1e-6;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("capture: current view unavailable: " + ex.Message);
                return false;
            }
        }

        public static Extents3d? ModelExtents(Document doc)
        {
            Extents3d? ext = null;
            var db = doc.Database;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || !e.Visible) continue;
                    var x = EntityInfo.Extents(e);
                    if (!x.HasValue) continue;
                    if (ext.HasValue) { var t = ext.Value; t.AddExtents(x.Value); ext = t; } else ext = x;
                }
                tr.Commit();
            }
            return ext;
        }

        /// <summary>Extents grown by 5 % (at least 300 mm) so objects on the border stay visible.</summary>
        public static Point3d Pad(Extents3d e, out Point3d max)
        {
            var mn = e.MinPoint;
            var mx = e.MaxPoint;
            double pad = Math.Max(300, 0.05 * Math.Max(mx.X - mn.X, mx.Y - mn.Y));
            max = new Point3d(mx.X + pad, mx.Y + pad, 0);
            return new Point3d(mn.X - pad, mn.Y - pad, 0);
        }

        public static Dictionary<string, object> ToJson(CaptureResult r) => r.View3d != null ? ToJson3d(r) : new Dictionary<string, object>
        {
            ["image_png_base64"] = Convert.ToBase64String(r.Png),
            ["width"] = r.Width,
            ["height"] = r.Height,
            ["region"] = r.Region,
            ["min"] = Acad.J(r.Min),
            ["max"] = Acad.J(r.Max),
            ["mm_per_pixel"] = Acad.R(r.MmPerPixel),
            ["note"] = "Plan view of model space on a dark background (ACI 7 shows white). Image x grows to the right (+X), y grows upwards (+Y); " +
                       "pixel (0,0) is the top-left corner = WCS [min.x, max.y].",
        };

        private static Dictionary<string, object> ToJson3d(CaptureResult r) => new Dictionary<string, object>
        {
            ["image_png_base64"] = Convert.ToBase64String(r.Png),
            ["width"] = r.Width,
            ["height"] = r.Height,
            ["region"] = r.Region,
            ["view"] = r.View3d,
            ["direction"] = r.Direction.Select(x => (object)Acad.R(x)).ToList(),
            ["visual_style"] = r.VisualStyle ?? "viewport",
            ["min"] = Acad.J3(r.Min),
            ["max"] = Acad.J3(r.Max),
            ["mm_per_pixel"] = Acad.R(r.MmPerPixel),
            ["note"] = $"Parallel 3D view '{r.View3d}' of the box min–max (Z up on screen except top/bottom views), dark background. " +
                       "Distances in the picture are foreshortened: read sizes with get_solid_info / get_bounding_box, not from the picture.",
        };
    }

    public static partial class DrawingService
    {
        private static void RegisterCapture(ToolDispatcher d)
        {
            // read-only: renders off-screen, the user's view is not touched
            d.Register(new ToolDef("capture_view", false, CaptureView, noTransaction: true));
        }

        private static object CaptureView(ToolContext ctx, Params p)
        {
            int size = p.Has("size") ? (int)p.Num("size") : Capture.DefaultSize;
            Point3d? min = null, max = null;
            string region = null;
            // 3D look (phase 1): a view direction and / or a visual style; capture_view stays in the Common group
            var view3d = DrawingService.ViewOf(p, null);
            string style = p.Has("visual_style") ? SolidLogic.VisualStyleName(p.Str("visual_style"), "capture_view") : null;
            bool is3d = view3d != null || style != null;
            if (p.Has("handles"))
            {
                Extents3d? ext = null;
                int found = 0;
                using (var tr = ctx.Doc.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (var h in p.Handles("handles", 5000))
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
                if (found == 0) throw new ToolException(ErrorCodes.EntityNotFound, "capture_view: none of the handles exists. Call list_entities to get current handles.");
                if (!ext.HasValue) throw ToolException.Invalid("capture_view: the objects have no extents (empty text?). Use p1/p2 instead.");
                if (is3d)
                {
                    var r3 = Capture.Render(ctx.Doc, ext.Value.MinPoint, ext.Value.MaxPoint, size, "handles", view3d, style);
                    return Capture.ToJson(r3);
                }
                var mn = ext.Value.MinPoint;
                var mx = ext.Value.MaxPoint;
                double pad = Math.Max(500, 0.15 * Math.Max(mx.X - mn.X, mx.Y - mn.Y));
                min = new Point3d(mn.X - pad, mn.Y - pad, 0);
                max = new Point3d(mx.X + pad, mx.Y + pad, 0);
                region = "handles";
            }
            else if (p.Has("p1") || p.Has("p2"))
            {
                var a = p.Point("p1");
                var b = p.Point("p2");
                if (is3d)
                {
                    var lo = new Point3d(Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Min(a[2], b[2]));
                    var hi = new Point3d(Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), Math.Max(a[2], b[2]));
                    return Capture.ToJson(Capture.Render(ctx.Doc, lo, hi, size, "window", view3d, style));
                }
                if (Math.Abs(a[0] - b[0]) < 1 || Math.Abs(a[1] - b[1]) < 1) throw ToolException.Invalid("capture_view: p1 and p2 must span a window (differ in x and y).");
                min = new Point3d(Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), 0);
                max = new Point3d(Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), 0);
                region = "window";
            }
            else if (is3d)
            {
                // a 3D look without handles / window: the whole model
                return Capture.ToJson(Capture.Render(ctx.Doc, null, null, size, "extents", view3d, style));
            }
            else if (p.Bool("extents", false))
            {
                var ext = Capture.ModelExtents(ctx.Doc);
                if (!ext.HasValue) throw ToolException.Invalid("capture_view: model space is empty; there is nothing to look at.");
                min = Capture.Pad(ext.Value, out var mx);
                max = mx;
                region = "extents";
            }
            var r = Capture.Render(ctx.Doc, min, max, size, region);
            return Capture.ToJson(r);
        }
    }
}
