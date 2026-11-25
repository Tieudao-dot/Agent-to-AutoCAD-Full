using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt G4: a real render (named_view action render). AutoCAD has no programming interface to render, so the
    /// plugin types -RENDER (render to a temporary PNG) and returns at once; the assistant then asks render_status every
    /// couple of seconds (AutoCAD stays free, the palette shows the progress) until the file is written, the user stops
    /// it (Dừng in the chat, or Esc), or the max wait passes. Anything but a finished render ends with the picture of F3
    /// (Capture.Render, said as a capture) or nothing when the user stopped. Background, lighting and the view are changed
    /// for the render only (undo recording off) and put back when it ends.
    /// </summary>
    public static partial class DrawingService
    {
        private sealed class RenderJob
        {
            public Document Doc;
            public string File, Temp, ViewName;
            public RenderLogic.Quality Quality;
            public int Width, Height, Estimate;
            public DateTime Started;
            public long LastSize = -1;
            public DateTime LastSizeChange;
            public volatile bool Cancelled, Failed;
            public string Error;
            public Vector3d Direction;
            public string Background, Lighting, LightWords;
            public List<string> Changes = new List<string>();
            // what to put back
            public ObjectId OldBackground = ObjectId.Null, MadeBackground = ObjectId.Null;
            public bool BackgroundChanged;
            public bool? OldDefaultLight, OldSun;
            public ViewTableRecord OldView;
        }

        private static RenderJob _render;

        private static string RenderToken() => Guid.NewGuid().ToString("N").Substring(0, 12);

        internal static object StartRender(ToolContext ctx, Params p, string name, string tool)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            if (_render != null)
            {
                if ((DateTime.UtcNow - _render.Started).TotalSeconds <= _render.Quality.MaxWaitSeconds + 60)
                    throw ToolException.Invalid($"{tool}: a render is already running (started {(int)(DateTime.UtcNow - _render.Started).TotalSeconds} s ago). Wait for it, or the user stops it (Dừng / Esc).");
                FinishRender(_render); // stale (AutoCAD never answered): put back and forget
            }
            if (Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE")) != 1)
                throw ToolException.Invalid($"{tool}: render works on the Model tab. Switch to Model with set_current_layout {{\"name\": \"Model\"}} first.");
            if (!string.IsNullOrEmpty(doc.CommandInProgress) || !doc.Editor.IsQuiescent)
                throw ToolException.Invalid($"{tool}: AutoCAD is running the command {doc.CommandInProgress}. Ask the user to finish it or press Esc, then try again.");

            var q = RenderLogic.QualityOf(p.Str("quality", null), tool);
            var (w, h) = RenderLogic.Size(p.NumOpt("width"), p.NumOpt("height"), tool);
            string background = RenderLogic.OneOf(p.Str("background", null), RenderLogic.Backgrounds, G4Numbers.DefaultBackground, "background", tool);
            string lighting = RenderLogic.OneOf(p.Str("lighting", null), RenderLogic.Lightings, G4Numbers.DefaultLighting, "lighting", tool);
            bool readOnly = ctx.Dispatcher?.ReadOnlyTurn == true;
            var notes = new List<string>();
            if (readOnly && (background != "drawing" || lighting != "drawing"))
            {
                background = "drawing";
                lighting = "drawing";
                notes.Add("\"Chỉ xem\" is on: the drawing's own background and lighting are used (no setting changed).");
            }

            ViewTableRecord named = null;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                if (name != null)
                {
                    var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                    if (!vt.Has(name) || vt[name].IsErased) throw ToolException.Invalid($"{tool}: no view named '{name}'. Call named_view action list.");
                    named = (ViewTableRecord)tr.GetObject(vt[name], OpenMode.ForRead);
                    if (named.IsPaperspaceView) throw ToolException.Invalid($"{tool}: '{name}' is a paper-space (layout) view; render a model view.");
                }
                tr.Commit();
            }

            var file = MaterialLogic.ImagePath(p.Str("output", null), DrawingFolder(ctx), DrawingStem(ctx), RenderLogic.Tail(name));
            if (!Directory.Exists(Path.GetDirectoryName(file))) throw ToolException.Invalid($"{tool}: folder '{Path.GetDirectoryName(file)}' does not exist.");
            RequireOverwriteCard(ctx, p, tool, file, File.Exists(file), $"{tool}: '{file}' already exists. Pass replace: true to overwrite it (ask the user first), or another output.", "replace", "overwrite");

            var job = new RenderJob
            {
                Doc = doc, File = file, Temp = RenderLogic.TempFile(Path.GetTempPath(), RenderToken()), ViewName = name,
                Quality = q, Width = w, Height = h, Estimate = RenderLogic.Estimate(q, w, h), Started = DateTime.UtcNow,
                Background = background, Lighting = lighting,
            };
            try
            {
                using (doc.LockDocument())
                {
                    if (named != null)
                    {
                        using (var cur = doc.Editor.GetCurrentView()) job.OldView = (ViewTableRecord)cur.Clone();
                        doc.Editor.SetCurrentView(named);
                        job.Changes.Add($"the view '{name}' is shown for the render, the user's view comes back after");
                    }
                    using (var cur = doc.Editor.GetCurrentView()) job.Direction = cur.ViewDirection;
                    PrepareScene(doc, job);
                }
            }
            catch (Exception ex)
            {
                FinishRender(job);
                if (ex is ToolException) throw;
                throw ToolException.Invalid($"{tool}: could not prepare the render ({ex.Message}). Nothing was changed; save a picture with named_view action image instead.");
            }

            _render = job;
            doc.CommandCancelled += OnRenderCancelled;
            doc.CommandFailed += OnRenderFailed;
            doc.UnknownCommand += OnRenderUnknown;
            try { if (File.Exists(job.Temp)) File.Delete(job.Temp); } catch (Exception) { }
            doc.SendStringToExecute(RenderLogic.CommandScript(q, w, h, job.Temp), true, false, false);

            var r = new Dictionary<string, object>
            {
                ["render_started"] = true,
                ["file"] = file,
                ["quality"] = q.Key,
                ["width"] = w,
                ["height"] = h,
                ["estimate_seconds"] = job.Estimate,
                ["max_wait_seconds"] = q.MaxWaitSeconds,
                ["background"] = background,
                ["lighting"] = job.LightWords,
            };
            if (job.Changes.Count > 0) r["changed_for_render"] = new List<object>(job.Changes);
            if (notes.Count > 0) r["notes"] = new List<object>(notes);
            return r;
        }

        /// <summary>Background and lighting of the current model viewport, for the render only (undo recording off).</summary>
        private static void PrepareScene(Document doc, RenderJob job)
        {
            var db = doc.Database;
            int lights = 0;
            bool hasSun = false, sunOn = false, defaultOn = true;
            doc.Editor.UpdateTiledViewportsInDatabase();
            db.DisableUndoRecording(true);
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                        if (id.ObjectClass.Name == "AcDbLight" && tr.GetObject(id, OpenMode.ForRead) is Light lt && lt.IsOn) lights++;
                    var vtr = (ViewportTableRecord)tr.GetObject(db.CurrentViewportTableRecordId, OpenMode.ForRead);
                    defaultOn = vtr.DefaultLightingOn;
                    Sun sun = null;
                    if (!vtr.SunId.IsNull) { sun = tr.GetObject(vtr.SunId, OpenMode.ForRead) as Sun; hasSun = sun != null; sunOn = sun?.IsOn == true; }
                    job.LightWords = RenderLogic.LightWords(job.Lighting, lights, sunOn, hasSun);
                    var (wantDefault, wantSun) = RenderLogic.LightPlan(job.Lighting, lights, sunOn, hasSun, defaultOn);
                    if (wantDefault.HasValue && wantDefault.Value != defaultOn)
                    {
                        vtr.UpgradeOpen();
                        job.OldDefaultLight = defaultOn;
                        vtr.DefaultLightingOn = wantDefault.Value;
                        job.Changes.Add(wantDefault.Value ? "AutoCAD default light on for the render" : "default light off for the render");
                    }
                    if (wantSun.HasValue && sun != null && wantSun.Value != sunOn)
                    {
                        sun.UpgradeOpen();
                        job.OldSun = sunOn;
                        sun.IsOn = wantSun.Value;
                        job.Changes.Add(wantSun.Value ? "the sun on for the render" : "the sun off for the render");
                    }
                    if (job.Background != "drawing")
                    {
                        var dict = (DBDictionary)tr.GetObject(Background.GetBackgroundDictionaryId(db, true), OpenMode.ForWrite);
                        Background bg;
                        if (job.Background == "sky")
                            bg = new GradientBackground { ColorTop = Rgb(G4Numbers.SkyTop), ColorMiddle = Rgb(G4Numbers.SkyMiddle), ColorBottom = Rgb(G4Numbers.SkyBottom) };
                        else
                            bg = new SolidBackground { Color = Rgb(G4Numbers.White) };
                        string key = "ClaudeCAD-render-" + RenderToken();
                        job.MadeBackground = dict.SetAt(key, bg);
                        tr.AddNewlyCreatedDBObject(bg, true);
                        vtr.UpgradeOpen();
                        job.OldBackground = vtr.Background;
                        vtr.Background = job.MadeBackground;
                        job.BackgroundChanged = true;
                        job.Changes.Add((job.Background == "sky" ? "a sky background" : "a white background") + " for the render");
                    }
                    tr.Commit();
                }
            }
            finally
            {
                db.DisableUndoRecording(false);
            }
            doc.Editor.UpdateTiledViewportsFromDatabase();
        }

        private static EntityColor Rgb(int[] c) => new EntityColor((byte)c[0], (byte)c[1], (byte)c[2]);

        /// <summary>Puts back the background, lighting and view of a render, and forgets it.</summary>
        private static void FinishRender(RenderJob job)
        {
            if (job == null) return;
            if (ReferenceEquals(_render, job)) _render = null;
            var doc = job.Doc;
            try
            {
                doc.CommandCancelled -= OnRenderCancelled;
                doc.CommandFailed -= OnRenderFailed;
                doc.UnknownCommand -= OnRenderUnknown;
            }
            catch (Exception) { }
            if (doc == null || doc.IsDisposed) return;
            try
            {
                using (doc.LockDocument())
                {
                    var db = doc.Database;
                    if (job.BackgroundChanged || job.OldDefaultLight.HasValue || job.OldSun.HasValue)
                    {
                        doc.Editor.UpdateTiledViewportsInDatabase();
                        db.DisableUndoRecording(true);
                        try
                        {
                            using (var tr = db.TransactionManager.StartTransaction())
                            {
                                var vtr = (ViewportTableRecord)tr.GetObject(db.CurrentViewportTableRecordId, OpenMode.ForWrite);
                                if (job.OldDefaultLight.HasValue) vtr.DefaultLightingOn = job.OldDefaultLight.Value;
                                if (job.OldSun.HasValue && !vtr.SunId.IsNull && tr.GetObject(vtr.SunId, OpenMode.ForWrite) is Sun sun) sun.IsOn = job.OldSun.Value;
                                if (job.BackgroundChanged)
                                {
                                    if (vtr.Background == job.MadeBackground) vtr.Background = job.OldBackground;
                                    if (!job.MadeBackground.IsNull && !job.MadeBackground.IsErased)
                                    {
                                        var o = tr.GetObject(job.MadeBackground, OpenMode.ForWrite);
                                        var dict = (DBDictionary)tr.GetObject(o.OwnerId, OpenMode.ForWrite);
                                        dict.Remove(job.MadeBackground);
                                        o.Erase();
                                    }
                                }
                                tr.Commit();
                            }
                        }
                        finally
                        {
                            db.DisableUndoRecording(false);
                        }
                        doc.Editor.UpdateTiledViewportsFromDatabase();
                    }
                    if (job.OldView != null) { doc.Editor.SetCurrentView(job.OldView); job.OldView.Dispose(); job.OldView = null; }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("render: putting back the scene failed", ex);
            }
        }

        private static bool IsRender(string globalName) => (globalName ?? "").TrimStart('_', '.', '-').Equals("RENDER", StringComparison.OrdinalIgnoreCase);

        private static void OnRenderCancelled(object sender, CommandEventArgs e) { if (_render != null && IsRender(e.GlobalCommandName)) _render.Cancelled = true; }

        private static void OnRenderFailed(object sender, CommandEventArgs e)
        {
            if (_render != null && IsRender(e.GlobalCommandName)) { _render.Failed = true; _render.Error = "the RENDER command failed"; }
        }

        private static void OnRenderUnknown(object sender, UnknownCommandEventArgs e)
        {
            if (_render != null && IsRender(e.GlobalCommandName)) { _render.Failed = true; _render.Error = "this AutoCAD has no RENDER command"; }
        }

        /// <summary>The Stop button of the chat (or render_stop): stops the render, puts the scene back, writes nothing.</summary>
        public static bool StopRender()
        {
            var job = _render;
            if (job == null) return false;
            job.Cancelled = true;
            try
            {
                if (IsRender(job.Doc?.CommandInProgress)) job.Doc.SendStringToExecute("\x03\x03", true, false, false);
            }
            catch (Exception) { }
            FinishRender(job);
            return true;
        }

        internal static object RenderStatus(ToolContext ctx, Params p, string tool, bool stop)
        {
            var job = _render;
            if (job == null) return new Dictionary<string, object> { ["running"] = false, ["note"] = "No render is running." };
            if (stop)
            {
                StopRender();
                return new Dictionary<string, object> { ["running"] = false, ["cancelled"] = true, ["note"] = "The render was stopped; no file was written. Background, lighting and view are as before." };
            }
            double elapsed = (DateTime.UtcNow - job.Started).TotalSeconds;
            long size = 0;
            try { if (File.Exists(job.Temp)) size = new FileInfo(job.Temp).Length; } catch (Exception) { }
            if (size != job.LastSize) { job.LastSize = size; job.LastSizeChange = DateTime.UtcNow; }
            double same = (DateTime.UtcNow - job.LastSizeChange).TotalSeconds;
            var state = RenderLogic.StateOf(elapsed, job.Quality.MaxWaitSeconds, size, job.LastSize, size > 0 ? same : 0, job.Cancelled, job.Failed);
            switch (state)
            {
                case RenderLogic.State.Running:
                    return new Dictionary<string, object>
                    {
                        ["running"] = true,
                        ["elapsed_seconds"] = (int)elapsed,
                        ["estimate_seconds"] = job.Estimate,
                        ["max_wait_seconds"] = job.Quality.MaxWaitSeconds,
                        ["progress"] = RenderLogic.Progress(elapsed, job.Estimate, job.Quality.MaxWaitSeconds),
                    };
                case RenderLogic.State.Done:
                {
                    FinishRender(job);
                    try { File.Copy(job.Temp, job.File, true); }
                    catch (Exception ex) { throw ToolException.Invalid($"{tool}: the render is finished but '{job.File}' could not be written ({ex.Message}). The picture is at '{job.Temp}'."); }
                    try { File.Delete(job.Temp); } catch (Exception) { }
                    return new Dictionary<string, object>
                    {
                        ["running"] = false,
                        ["method"] = "render",
                        ["file"] = job.File,
                        ["width"] = job.Width,
                        ["height"] = job.Height,
                        ["quality"] = job.Quality.Key,
                        ["seconds"] = (int)elapsed,
                        ["lighting"] = job.LightWords,
                        ["background"] = job.Background,
                        ["note"] = "A real AutoCAD render. Background, lighting and view are back as before. Tell the user where the file is.",
                    };
                }
                case RenderLogic.State.Cancelled:
                    FinishRender(job);
                    return new Dictionary<string, object> { ["running"] = false, ["cancelled"] = true, ["note"] = "The user stopped the render (Esc); no file was written. Background, lighting and view are as before." };
                default: // too long / failed: stop and save the picture of F3 instead
                {
                    if (IsRender(job.Doc?.CommandInProgress)) { try { job.Doc.SendStringToExecute("\x03\x03", true, false, false); } catch (Exception) { } }
                    FinishRender(job);
                    string why = RenderLogic.FallbackReason(state, job.Error);
                    return RenderFallback(job, why, (int)elapsed, tool);
                }
            }
        }

        /// <summary>The way back: the off-screen picture of named_view image (Realistic), written to the same file, said as a capture.</summary>
        private static object RenderFallback(RenderJob job, string why, int elapsed, string tool)
        {
            var v = SolidLogic.Custom(new[] { job.Direction.X, job.Direction.Y, job.Direction.Z }, tool);
            int side = Math.Min(Capture.MaxSize, Math.Max(job.Width, job.Height));
            var r = Capture.Render(job.Doc, null, null, side, "extents", v, "Realistic");
            try { File.WriteAllBytes(job.File, r.Png); }
            catch (Exception ex) { throw ToolException.Invalid($"{tool}: {why}, and the picture could not be written to '{job.File}' ({ex.Message})."); }
            return new Dictionary<string, object>
            {
                ["running"] = false,
                ["method"] = "capture",
                ["reason"] = why,
                ["file"] = job.File,
                ["width"] = r.Width,
                ["height"] = r.Height,
                ["seconds"] = elapsed,
                ["note"] = "NOT a render: " + why + ". Saved the off-screen picture (Realistic visual style, the whole model) instead. Tell the user plainly that this file is a capture, not a render.",
            };
        }

        /// <summary>
        /// The self-test of G4 (no real render: it would only start after the self-test command): the white background and
        /// default light set for a render and put back exactly, no background left in the drawing, and the way back (a capture).
        /// </summary>
        internal static List<string> SelfTestRenderScene(Document doc, string png, out string info)
        {
            var errs = new List<string>();
            var db = doc.Database;
            ObjectId Bg() { using (var tr = db.TransactionManager.StartOpenCloseTransaction()) { var v = (ViewportTableRecord)tr.GetObject(db.CurrentViewportTableRecordId, OpenMode.ForRead); var b = v.Background; tr.Commit(); return b; } }
            int Count() { using (var tr = db.TransactionManager.StartOpenCloseTransaction()) { var d = (DBDictionary)tr.GetObject(Background.GetBackgroundDictionaryId(db, true), OpenMode.ForRead); int n = d.Count; tr.Commit(); return n; } }
            var before = Bg();
            int bgCount;
            using (doc.LockDocument()) bgCount = Count();
            var job = new RenderJob { Doc = doc, File = png, Quality = RenderLogic.QualityOf("low"), Width = 640, Height = 360, Background = "white", Lighting = "default", Started = DateTime.UtcNow };
            using (doc.LockDocument())
            {
                using (var cur = doc.Editor.GetCurrentView()) job.Direction = cur.ViewDirection;
                PrepareScene(doc, job);
            }
            if (!job.BackgroundChanged || Bg() != job.MadeBackground) errs.Add("nền trắng không được đặt cho khung nhìn");
            FinishRender(job);
            if (Bg() != before) errs.Add("nền của khung nhìn không trở lại như cũ");
            using (doc.LockDocument()) if (Count() != bgCount) errs.Add("còn sót nền tạm trong bản vẽ");
            if (File.Exists(png)) File.Delete(png);
            var fb = (Dictionary<string, object>)RenderFallback(job, "self-test", 0, "named_view");
            if ((string)fb["method"] != "capture" || !File.Exists(png) || new FileInfo(png).Length < 1000) errs.Add("đường lui: không có ảnh chụp");
            info = $"đổi tạm: {string.Join(", ", job.Changes)}; đường lui ảnh {fb["width"]}×{fb["height"]}";
            return errs;
        }

        private static string DrawingFolder(ToolContext ctx)
        {
            string dir = null;
            try { dir = Path.GetDirectoryName(ctx.Doc.Name); } catch (Exception) { }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return dir;
        }

        private static string DrawingStem(ToolContext ctx)
        {
            try { return Path.GetFileNameWithoutExtension(ctx.Doc.Name); } catch (Exception) { return null; }
        }
    }
}
