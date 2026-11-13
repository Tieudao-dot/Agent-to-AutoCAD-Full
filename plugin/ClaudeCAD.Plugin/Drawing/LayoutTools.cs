using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Phase 2 — layouts / paper space, viewports and PDF plotting.</summary>
    public static partial class DrawingService
    {
        public const string DefaultPdfDevice = "DWG To PDF.pc3";
        public const string DefaultPdfMedia = "ISO_full_bleed_A3_(420.00_x_297.00_MM)";

        private static void RegisterLayouts(ToolDispatcher d)
        {
            d.Register("list_layouts", false, ListLayouts);
            d.Register(new ToolDef("create_layout", true, CreateLayout, noTransaction: true));
            d.Register(new ToolDef("set_current_layout", true, SetCurrentLayout, noTransaction: true));
            d.Register(new ToolDef("delete_layout", true, DeleteLayout, noTransaction: true));
            d.Register("add_viewport", true, AddViewport);
            d.Register("set_viewport", true, SetViewport);
            d.Register(new ToolDef("plot_pdf", false, PlotPdf, noTransaction: true));
            d.Register(new ToolDef("list_plot_media", false, ListPlotMedia, noTransaction: true));
        }

        // ------------------------------------------------------------------ layouts

        private static Layout FindLayout(Transaction tr, Database db, string name, OpenMode mode = OpenMode.ForRead)
        {
            var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict)
                if (e.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (Layout)tr.GetObject(e.Value, mode);
            throw ToolException.Invalid($"Layout '{name}' does not exist. Call list_layouts to see the layouts.");
        }

        private static object ListLayouts(ToolContext ctx, Params p)
        {
            var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.LayoutDictionaryId, OpenMode.ForRead);
            string current = LayoutManager.Current.CurrentLayout;
            var list = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var lo = (Layout)ctx.Tr.GetObject(e.Value, OpenMode.ForRead);
                var btr = (BlockTableRecord)ctx.Tr.GetObject(lo.BlockTableRecordId, OpenMode.ForRead);
                int viewports = 0, entities = 0;
                foreach (ObjectId id in btr)
                {
                    entities++;
                    if (id.ObjectClass.DxfName == "VIEWPORT") viewports++;
                }
                list.Add(new Dictionary<string, object>
                {
                    ["name"] = lo.LayoutName,
                    ["tab_order"] = lo.TabOrder,
                    ["current"] = lo.LayoutName.Equals(current, StringComparison.OrdinalIgnoreCase),
                    ["model"] = lo.ModelType,
                    ["device"] = lo.PlotConfigurationName,
                    ["media"] = lo.CanonicalMediaName,
                    ["paper_size_mm"] = new List<object> { Acad.R(lo.PlotPaperSize.X), Acad.R(lo.PlotPaperSize.Y) },
                    ["viewports"] = Math.Max(0, viewports - (lo.ModelType ? 0 : 1)), // minus the overall paper-space viewport
                    ["entities"] = entities,
                });
            }
            return new Dictionary<string, object> { ["current"] = current, ["layouts"] = list.OrderBy(o => (int)((Dictionary<string, object>)o)["tab_order"]).ToList() };
        }

        private static object CreateLayout(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            ValidateSymbolName(name, "layout");
            var lm = LayoutManager.Current;
            using (ctx.Doc.LockDocument())
            {
                if (lm.LayoutExists(name)) throw ToolException.Invalid($"Layout '{name}' already exists.");
                var copyFrom = p.Str("copy_from", null);
                if (!string.IsNullOrEmpty(copyFrom))
                {
                    if (!lm.LayoutExists(copyFrom)) throw ToolException.Invalid($"Layout '{copyFrom}' (copy_from) does not exist.");
                    lm.CopyLayout(copyFrom, name);
                }
                else lm.CreateLayout(name);
                if (p.Has("media") || p.Has("device"))
                {
                    using (var tr = ctx.Doc.TransactionManager.StartTransaction())
                    {
                        var lo = FindLayout(tr, ctx.Db, name, OpenMode.ForWrite);
                        var psv = PlotSettingsValidator.Current;
                        psv.SetPlotConfigurationName(lo, p.Str("device", DefaultPdfDevice), p.Str("media", DefaultPdfMedia));
                        psv.SetPlotPaperUnits(lo, PlotPaperUnit.Millimeters);
                        tr.Commit();
                    }
                }
                if (p.Bool("activate", false)) lm.CurrentLayout = name;
            }
            return new Dictionary<string, object> { ["name"] = name, ["created"] = true };
        }

        private static object SetCurrentLayout(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            var lm = LayoutManager.Current;
            if (!name.Equals("Model", StringComparison.OrdinalIgnoreCase) && !lm.LayoutExists(name))
                throw ToolException.Invalid($"Layout '{name}' does not exist. Call list_layouts.");
            using (ctx.Doc.LockDocument()) lm.CurrentLayout = name.Equals("model", StringComparison.OrdinalIgnoreCase) ? "Model" : name;
            return new Dictionary<string, object> { ["current"] = lm.CurrentLayout };
        }

        private static object DeleteLayout(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            if (name.Equals("Model", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid("The Model layout cannot be deleted.");
            var lm = LayoutManager.Current;
            if (!lm.LayoutExists(name)) throw ToolException.Invalid($"Layout '{name}' does not exist.");
            using (ctx.Doc.LockDocument()) lm.DeleteLayout(name);
            return new Dictionary<string, object> { ["deleted"] = name };
        }

        // ------------------------------------------------------------------ viewports

        /// <summary>"1:100", "1/100", 100 (denominator) or 0.01 (factor) → paper/model factor.</summary>
        internal static double ParseScale(object v, string param = "scale")
        {
            if (v is double d)
            {
                if (d <= 0) throw ToolException.Invalid($"'{param}' must be > 0.");
                return d >= 1 ? 1.0 / d : d;
            }
            if (v is string s)
            {
                var t = s.Trim().Replace(" ", "");
                var parts = t.Split(':', '/');
                if (parts.Length == 2
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && a > 0 && b > 0)
                    return a / b;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return ParseScale(n, param);
            }
            throw ToolException.Invalid($"'{param}' must be a scale like \"1:100\" (or 100, or 0.01).");
        }

        private static object AddViewport(ToolContext ctx, Params p)
        {
            string layout = p.Str("layout").Trim();
            if (layout.Equals("Model", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid("add_viewport: viewports go on a paper-space layout, not Model.");
            var space = ctx.SpaceFor(layout);
            double[] center;
            double w, h;
            if (p.Has("corner1"))
            {
                var c1 = p.Point("corner1");
                var c2 = p.Point("corner2");
                center = new[] { (c1[0] + c2[0]) / 2, (c1[1] + c2[1]) / 2, 0 };
                w = Math.Abs(c2[0] - c1[0]);
                h = Math.Abs(c2[1] - c1[1]);
            }
            else
            {
                center = p.Point("center");
                w = p.Positive("width");
                h = p.Positive("height");
            }
            var vp = new Viewport();
            vp.SetDatabaseDefaults(ctx.Db);
            vp.CenterPoint = Acad.P3(center);
            vp.Width = w;
            vp.Height = h;
            Acad.ApplyCommon(ctx, vp, p);
            var id = space.AppendEntity(vp);
            ctx.Tr.AddNewlyCreatedDBObject(vp, true);
            ApplyViewportSettings(ctx, vp, p);
            try { vp.On = true; } catch (Autodesk.AutoCAD.Runtime.Exception) { /* turned on when the layout is first displayed */ }
            var r = Acad.Handles(id);
            r["scale"] = $"1:{Acad.R(1 / vp.CustomScale)}";
            r["model_area_mm"] = new List<object> { Acad.R(w / vp.CustomScale), Acad.R(h / vp.CustomScale) };
            return r;
        }

        private static void ApplyViewportSettings(ToolContext ctx, Viewport vp, Params p)
        {
            if (p.Has("view_center"))
            {
                var vc = p.Point("view_center");
                vp.ViewDirection = Vector3d.ZAxis;
                vp.ViewTarget = Point3d.Origin;
                vp.ViewCenter = new Point2d(vc[0], vc[1]);
            }
            if (p.Has("scale")) vp.CustomScale = ParseScale(p.Get("scale"));
            var locked = p.BoolOpt("locked");
            if (locked.HasValue) vp.Locked = locked.Value;
            var freeze = p.StringsOpt("freeze_layers");
            if (freeze != null && freeze.Count > 0)
                vp.FreezeLayersInViewport(freeze.Select(n => Acad.LayerId(ctx, n)).ToList().GetEnumerator());
            var thaw = p.StringsOpt("thaw_layers");
            if (thaw != null && thaw.Count > 0)
                vp.ThawLayersInViewport(thaw.Select(n => Acad.LayerId(ctx, n)).ToList().GetEnumerator());
        }

        private static object SetViewport(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var e = Acad.GetEntity(ctx, h, true);
            if (!(e is Viewport vp)) throw ToolException.Invalid($"{h} is a {Acad.TypeName(e)}, not a viewport.");
            ApplyViewportSettings(ctx, vp, p);
            if (p.Has("width")) vp.Width = p.Positive("width");
            if (p.Has("height")) vp.Height = p.Positive("height");
            if (p.Has("center")) vp.CenterPoint = Acad.P3(p.Point("center"));
            var r = Acad.Handles(vp.ObjectId);
            r["scale"] = $"1:{Acad.R(1 / vp.CustomScale)}";
            return r;
        }

        // ------------------------------------------------------------------ plotting

        private static object ListPlotMedia(ToolContext ctx, Params p)
        {
            string device = p.Str("device", DefaultPdfDevice);
            var psv = PlotSettingsValidator.Current;
            using (var ps = new PlotSettings(true))
            {
                psv.SetPlotConfigurationName(ps, device, null);
                psv.RefreshLists(ps);
                var media = psv.GetCanonicalMediaNameList(ps).Cast<string>().ToList();
                string filter = p.Str("filter", null);
                var items = media
                    .Where(m => string.IsNullOrEmpty(filter) || m.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || psv.GetLocaleMediaName(ps, m).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(200)
                    .Select(m => (object)new Dictionary<string, object> { ["canonical"] = m, ["name"] = psv.GetLocaleMediaName(ps, m) })
                    .ToList();
                return new Dictionary<string, object>
                {
                    ["device"] = device,
                    ["media"] = items,
                    ["plot_styles"] = psv.GetPlotStyleSheetList().Cast<object>().ToList(),
                    ["devices"] = psv.GetPlotDeviceList().Cast<object>().ToList(),
                };
            }
        }

        private static object PlotPdf(ToolContext ctx, Params p)
        {
            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                throw new ToolException(ErrorCodes.AcadError, "AutoCAD is already plotting. Wait until it finishes and retry.");
            var lm = LayoutManager.Current;
            // nâng cấp đợt C: several layouts (or "all") in one multi-page PDF
            bool multi = p.Has("layouts");
            List<string> layouts;
            if (multi)
            {
                if (p.Has("layout")) throw ToolException.Invalid("plot_pdf: pass either 'layout' or 'layouts', not both.");
                layouts = Util2DLogic.ResolveLayouts(p.Strings("layouts", 1), LayoutTabOrder(ctx));
                if (layouts.Count > 1 && p.Str("area", "").Equals("window", StringComparison.OrdinalIgnoreCase))
                    throw ToolException.Invalid("plot_pdf: area \"window\" plots one layout; plot several layouts with area layout / extents.");
            }
            else
            {
                string one = p.Str("layout", lm.CurrentLayout);
                if (!one.Equals("Model", StringComparison.OrdinalIgnoreCase) && !lm.LayoutExists(one))
                    throw ToolException.Invalid($"Layout '{one}' does not exist. Call list_layouts.");
                layouts = new List<string> { one };
            }

            string output = p.Str("output", null);
            string drawingDir = SafeString(() => Path.GetDirectoryName(ctx.Doc.Name));
            if (string.IsNullOrEmpty(drawingDir) || !Directory.Exists(drawingDir)) drawingDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(output))
            {
                // the layout name may hold characters a file name cannot (\ / : * ? " < > |)
                output = Path.Combine(drawingDir, Util2DLogic.DefaultPdfName(Path.GetFileNameWithoutExtension(ctx.Doc.Name), layouts));
            }
            if (!output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid("plot_pdf: 'output' must end with .pdf.");
            string full;
            try
            {
                // Test & sửa đợt C (NC-4): a relative output goes next to the drawing, not into AutoCAD's own folder
                full = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(drawingDir, output));
            }
            catch (Exception) { throw ToolException.Invalid($"plot_pdf: '{output}' is not a valid file path."); }
            var badName = Util2DLogic.BadFileName(Path.GetFileName(full));
            if (badName != null) throw ToolException.Invalid($"plot_pdf: '{output}': {badName}. Give another file name ending in .pdf.");
            if (Directory.Exists(full)) throw ToolException.Invalid($"plot_pdf: '{output}' is a folder; give a file name ending in .pdf.");
            var outDir = Path.GetDirectoryName(full);
            if (!Directory.Exists(outDir)) throw ToolException.Invalid($"plot_pdf: folder '{outDir}' does not exist.");
            output = full;

            string device = p.Str("device", DefaultPdfDevice);
            // KT2-17: without 'media' each page keeps its layout's own paper when the PDF device has it (A1 and A3 layouts stay A1 and A3)
            string media = p.Str("media", null);
            var pageMedia = new List<string>();
            // KT2-16: remember the file before plotting, so an old PDF (or one open in a viewer) is not reported as the new plot
            bool existedBefore = File.Exists(output);
            DateTime? before = existedBefore ? File.GetLastWriteTimeUtc(output) : (DateTime?)null;
            if (existedBefore)
            {
                try { using (new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
                catch (IOException) { throw ToolException.Invalid(Util2DLogic.PdfLockedMessage(output)); }
                catch (UnauthorizedAccessException) { throw ToolException.Invalid(Util2DLogic.PdfLockedMessage(output)); }
            }
            string styleSheet = p.Str("plot_style", "monochrome.ctb");
            string scaleText = p.Str("scale", "fit");

            string previousLayout = lm.CurrentLayout;
            object oldBackground = AcApp.GetSystemVariable("BACKGROUNDPLOT");
            var settings = new List<PlotSettings>();
            using (ctx.Doc.LockDocument())
            {
                try
                {
                    AcApp.SetSystemVariable("BACKGROUNDPLOT", (short)0);
                    // a layout never opened has no viewport yet: open each once (as the single plot always did)
                    foreach (var name in layouts)
                        if (!lm.CurrentLayout.Equals(name, StringComparison.OrdinalIgnoreCase)) lm.CurrentLayout = name;
                    if (multi && !lm.CurrentLayout.Equals(previousLayout, StringComparison.OrdinalIgnoreCase)) lm.CurrentLayout = previousLayout;
                    using (var tr = ctx.Doc.TransactionManager.StartTransaction())
                    {
                        var infos = new List<PlotInfo>();
                        foreach (var name in layouts)
                        {
                            var lo = FindLayout(tr, ctx.Db, name);
                            string area = p.Str("area", lo.ModelType ? "extents" : "layout").ToLowerInvariant();
                            var ps = new PlotSettings(lo.ModelType);
                            settings.Add(ps);
                            pageMedia.Add(ConfigurePlot(p, lo, ps, device, media, styleSheet, area, scaleText));
                            var pi = new PlotInfo { Layout = lo.ObjectId, OverrideSettings = ps };
                            var piv = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
                            piv.Validate(pi);
                            infos.Add(pi);
                        }

                        using (var pe = PlotFactory.CreatePublishEngine())
                        {
                            pe.BeginPlot(null, null);
                            pe.BeginDocument(infos[0], ctx.Doc.Name, null, 1, true, output);
                            for (int i = 0; i < infos.Count; i++)
                            {
                                // Test & sửa đợt C (NC-3): AutoCAD plots the paper-space viewports of the current layout, so each
                                // page's layout is made current while it is plotted (restored in finally)
                                if (!lm.CurrentLayout.Equals(layouts[i], StringComparison.OrdinalIgnoreCase)) lm.CurrentLayout = layouts[i];
                                using (var ppi = new PlotPageInfo())
                                {
                                    pe.BeginPage(ppi, infos[i], i == infos.Count - 1, null);
                                    pe.BeginGenerateGraphics(null);
                                    pe.EndGenerateGraphics(null);
                                    pe.EndPage(null);
                                }
                            }
                            pe.EndDocument(null);
                            pe.EndPlot(null);
                        }
                        tr.Commit();
                    }
                }
                finally
                {
                    foreach (var ps in settings) try { ps.Dispose(); } catch { }
                    try { AcApp.SetSystemVariable("BACKGROUNDPLOT", oldBackground); } catch { }
                    try { if (!lm.CurrentLayout.Equals(previousLayout, StringComparison.OrdinalIgnoreCase)) lm.CurrentLayout = previousLayout; } catch { }
                }
            }
            bool existsAfter = File.Exists(output);
            bool written = Util2DLogic.PdfWritten(existedBefore, before, existsAfter, existsAfter ? File.GetLastWriteTimeUtc(output) : (DateTime?)null);
            var res = new Dictionary<string, object>
            {
                ["output"] = output,
                ["exists"] = written,
                ["media"] = pageMedia.Distinct().Count() == 1 ? (object)pageMedia[0] : pageMedia.Cast<object>().ToList(),
                ["scale"] = scaleText,
            };
            if (!written) res["warning"] = Util2DLogic.PdfNotWrittenMessage(existedBefore);
            if (multi) { res["layouts"] = layouts.Cast<object>().ToList(); res["pages"] = layouts.Count; }
            else res["layout"] = layouts[0];
            return res;
        }

        /// <summary>The layouts in tab order (Model first).</summary>
        private static List<string> LayoutTabOrder(ToolContext ctx)
        {
            // plot_pdf runs without the dispatcher's transaction
            var list = new List<(int order, string name)>();
            using (var tr = ctx.Db.TransactionManager.StartOpenCloseTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(ctx.Db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                {
                    var lo = (Layout)tr.GetObject(e.Value, OpenMode.ForRead);
                    list.Add((lo.TabOrder, lo.LayoutName));
                }
                tr.Commit();
            }
            return list.OrderBy(x => x.order).Select(x => x.name).ToList();
        }

        /// <summary>Device, paper, area, scale, centring, plot style and rotation of one page (from the layout's own settings).</summary>
        /// <returns>The paper used: 'media' when given, else the layout's own when the device has it (KT2-17), else A3.</returns>
        private static string ConfigurePlot(Params p, Layout lo, PlotSettings ps, string device, string media, string styleSheet, string area, string scaleText)
        {
            ps.CopyFrom(lo);
            var psv = PlotSettingsValidator.Current;
            if (media == null)
            {
                List<string> deviceMedia;
                try
                {
                    psv.SetPlotConfigurationName(ps, device, null);
                    psv.RefreshLists(ps);
                    deviceMedia = psv.GetCanonicalMediaNameList(ps).Cast<string>().ToList();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { deviceMedia = new List<string>(); }
                media = Util2DLogic.PickMedia(null, lo.CanonicalMediaName, deviceMedia, DefaultPdfMedia);
            }
            psv.SetPlotConfigurationName(ps, device, media);
            psv.SetPlotPaperUnits(ps, PlotPaperUnit.Millimeters);
            psv.RefreshLists(ps);
            switch (area)
            {
                case "layout": psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout); break;
                case "extents": psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents); break;
                case "display": psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Display); break;
                case "window":
                    var w1 = p.Point("window_p1");
                    var w2 = p.Point("window_p2");
                    psv.SetPlotWindowArea(ps, new Extents2d(Math.Min(w1[0], w2[0]), Math.Min(w1[1], w2[1]), Math.Max(w1[0], w2[0]), Math.Max(w1[1], w2[1])));
                    psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
                    break;
                default: throw ToolException.Invalid("plot_pdf: area must be layout, extents, display or window.");
            }
            if (scaleText.Equals("fit", StringComparison.OrdinalIgnoreCase))
            {
                psv.SetUseStandardScale(ps, true);
                psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
            }
            else
            {
                double f = ParseScale(scaleText);
                psv.SetUseStandardScale(ps, false);
                psv.SetCustomPrintScale(ps, new CustomScale(1, 1 / f));
            }
            psv.SetPlotCentered(ps, p.Bool("center", true));
            try { psv.SetCurrentStyleSheet(ps, styleSheet); }
            catch (Autodesk.AutoCAD.Runtime.Exception) { throw ToolException.Invalid($"plot_pdf: plot style '{styleSheet}' not found. Use list_plot_media to see the available .ctb/.stb files."); }
            if (p.Has("landscape")) psv.SetPlotRotation(ps, p.Bool("landscape", true) ? PlotRotation.Degrees090 : PlotRotation.Degrees000);
            return media;
        }
    }
}
