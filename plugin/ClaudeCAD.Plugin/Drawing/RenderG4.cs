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
            /// <summary>Test & sửa G4: when the RENDER command ended (CommandEnded), or MinValue while it runs.</summary>
            public DateTime EndedAt = DateTime.MinValue;
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
        /// <summary>
        /// Test & sửa G4: the last answer of a finished render. A render_status the assistant gave up waiting for (AutoCAD busy
        /// rendering) may have finished the job meanwhile: the next render_status still gets that answer (file, render / capture).
        /// </summary>
        private static Dictionary<string, object> _lastRenderResult;

        private static Dictionary<string, object> Remember(Dictionary<string, object> r) { _lastRenderResult = r; return r; }

        /// <summary>Test & sửa G4: the folder of the temporary render file: no space / accent in its path (typed at the command line).</summary>
        private static string RenderTempDir()
        {
            var cands = new List<string> { Path.GetTempPath() };
            try
            {
                var pd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClaudeCAD", "render");
                if (RenderLogic.SafeTempDir(new[] { pd }) != null) { Directory.CreateDirectory(pd); cands.Add(pd); }
            }
            catch (Exception) { }
            return RenderLogic.SafeTempDir(cands);
        }

        /// <summary>
        /// Sửa KFG (nghi vấn G4): a render stopped / given up may still write its temporary file after the plugin let it go;
        /// such files older than RenderLogic.OldTempHours are removed before the next render.
        /// </summary>
        private static void CleanOldRenderFiles(string dir)
        {
            try
            {
                if (dir == null || !Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "claudecad-render-*.png"))
                    try { if (RenderLogic.IsOldTemp(File.GetLastWriteTimeUtc(f), DateTime.UtcNow)) File.Delete(f); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        private static string RenderToken() => Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>
        /// KFG-01: a copy of a saved model view, made inside the transaction (the record itself is closed once it ends and
        /// cannot be used for SetCurrentView then). The caller disposes it.
        /// </summary>
        internal static ViewTableRecord NamedViewCopy(Database db, string name, string tool)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var vt = (ViewTable)tr.GetObject(db.ViewTableId, OpenMode.ForRead);
                if (!vt.Has(name) || vt[name].IsErased) throw ToolException.Invalid($"{tool}: no view named '{name}'. Call named_view action list.");
                var rec = (ViewTableRecord)tr.GetObject(vt[name], OpenMode.ForRead);
                if (rec.IsPaperspaceView) throw ToolException.Invalid($"{tool}: '{name}' is a paper-space (layout) view; render a model view.");
                var copy = (ViewTableRecord)rec.Clone();
                tr.Commit();
                return copy;
            }
        }

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

            ViewTableRecord named = name != null ? NamedViewCopy(db, name, tool) : null;

            var file = MaterialLogic.ImagePath(p.Str("output", null), DrawingFolder(ctx), DrawingStem(ctx), RenderLogic.Tail(name));
            if (!Directory.Exists(Path.GetDirectoryName(file))) throw ToolException.Invalid($"{tool}: folder '{Path.GetDirectoryName(file)}' does not exist.");
            RequireOverwriteCard(ctx, p, tool, file, File.Exists(file), $"{tool}: '{file}' already exists. Pass replace: true to overwrite it (ask the user first), or another output.", "replace", "overwrite");

            _lastRenderResult = null;
            var tempDir = RenderTempDir();
            CleanOldRenderFiles(tempDir);
            var job = new RenderJob
            {
                Doc = doc, File = file, Temp = RenderLogic.TempFile(tempDir ?? Path.GetTempPath(), RenderToken()), ViewName = name,
                Quality = q, Width = w, Height = h, Estimate = RenderLogic.Estimate(q, w, h), Started = DateTime.UtcNow,
                Background = background, Lighting = lighting,
            };
            if (tempDir == null)
            {
                // Test & sửa G4: no folder whose path can be typed safely (spaces / accents): no -RENDER, the picture at once
                using (var cur = doc.Editor.GetCurrentView()) job.Direction = (named?.ViewDirection) ?? cur.ViewDirection;
                try { return RenderFallback(job, "no temporary folder without spaces or accents for the render file", 0, tool); }
                finally { named?.Dispose(); }
            }
            try
            {
                using (doc.LockDocument())
                {
                    if (named != null)
                    {
                        using (var cur = doc.Editor.GetCurrentView()) job.OldView = (ViewTableRecord)cur.Clone();
                        // the view for the render only: not an undo step (U never jumps to it)
                        db.DisableUndoRecording(true);
                        try { doc.Editor.SetCurrentView(named); }
                        finally { db.DisableUndoRecording(false); }
                        job.Changes.Add($"the view '{name}' is shown for the render, the user's view comes back after");
                    }
                    using (var cur = doc.Editor.GetCurrentView()) job.Direction = cur.ViewDirection;
                    PrepareScene(doc, job);
                }
            }
            catch (Exception ex)
            {
                named?.Dispose();
                FinishRender(job);
                if (ex is ToolException) throw;
                throw ToolException.Invalid($"{tool}: could not prepare the render ({ex.Message}). Nothing was changed; save a picture with named_view action image instead.");
            }

            named?.Dispose();
            _render = job;
            doc.CommandCancelled += OnRenderCancelled;
            doc.CommandFailed += OnRenderFailed;
            doc.UnknownCommand += OnRenderUnknown;
            doc.CommandEnded += OnRenderEnded;
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
                    lights = LightsIn(tr, ms, new Dictionary<ObjectId, int>(), 0);
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

        /// <summary>Sửa KFG (nghi vấn G4): lights that are on, also inside blocks (nested up to 8 levels; each block counted once per insert).</summary>
        private static int LightsIn(Transaction tr, BlockTableRecord btr, Dictionary<ObjectId, int> cache, int depth)
        {
            if (cache.TryGetValue(btr.ObjectId, out var known)) return known;
            int n = 0;
            cache[btr.ObjectId] = 0; // a block that holds itself counts nothing
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.Name == "AcDbLight") { if (tr.GetObject(id, OpenMode.ForRead) is Light lt && lt.IsOn) n++; }
                else if (depth < 8 && id.ObjectClass.Name == "AcDbBlockReference" && tr.GetObject(id, OpenMode.ForRead) is BlockReference br && br.Visible)
                    n += LightsIn(tr, (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead), cache, depth + 1);
                if (n > 10000) break;
            }
            cache[btr.ObjectId] = n;
            return n;
        }

        private static EntityColor Rgb(int[] c) => new EntityColor((byte)c[0], (byte)c[1], (byte)c[2]);

        /// <summary>Puts back the background, lighting and view of a render, and forgets it.</summary>
        private static void FinishRender(RenderJob job, bool keepTemp = false)
        {
            if (job == null) return;
            if (ReferenceEquals(_render, job)) _render = null;
            var doc = job.Doc;
            try
            {
                doc.CommandCancelled -= OnRenderCancelled;
                doc.CommandFailed -= OnRenderFailed;
                doc.UnknownCommand -= OnRenderUnknown;
                doc.CommandEnded -= OnRenderEnded;
            }
            catch (Exception) { }
            // Test & sửa G4: a cancelled / failed render leaves no temporary file behind
            if (!keepTemp && !string.IsNullOrEmpty(job.Temp)) { try { if (File.Exists(job.Temp)) File.Delete(job.Temp); } catch (Exception) { } }
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
                                    if (job.MadeBackground.IsValid && !job.MadeBackground.IsErased)
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
                }
            }
            catch (Exception ex)
            {
                Logger.Error("render: putting back the scene failed", ex);
            }
            // Test & sửa G4: the view comes back even when putting back the background / light failed
            try
            {
                if (job.OldView != null)
                    using (doc.LockDocument())
                    {
                        doc.Database.DisableUndoRecording(true);
                        try { doc.Editor.SetCurrentView(job.OldView); }
                        finally { doc.Database.DisableUndoRecording(false); }
                    }
            }
            catch (Exception ex) { Logger.Error("render: putting back the view failed", ex); }
            finally { if (job.OldView != null) { job.OldView.Dispose(); job.OldView = null; } }
        }

        private static bool IsRender(string globalName) => (globalName ?? "").TrimStart('_', '.', '-').Equals("RENDER", StringComparison.OrdinalIgnoreCase);

        private static void OnRenderCancelled(object sender, CommandEventArgs e) { if (_render != null && IsRender(e.GlobalCommandName)) _render.Cancelled = true; }

        private static void OnRenderFailed(object sender, CommandEventArgs e)
        {
            if (_render != null && IsRender(e.GlobalCommandName)) { _render.Failed = true; _render.Error = "the RENDER command failed"; }
        }

        private static void OnRenderUnknown(object sender, UnknownCommandEventArgs e)
        {
            var job = _render;
            if (job == null) return;
            if (IsRender(e.GlobalCommandName)) { job.Failed = true; job.Error = "this AutoCAD has no RENDER command"; }
            // Test & sửa G4: one of our answers typed at the command prompt: -RENDER asked its questions in another order
            else if (RenderLogic.IsOurAnswer(e.GlobalCommandName, job.Quality, job.Width, job.Height, job.Temp))
            {
                job.Failed = true;
                job.Error = "-RENDER asked its questions in another order than the plugin expects (an answer reached the command line)";
            }
        }

        private static void OnRenderEnded(object sender, CommandEventArgs e)
        {
            var job = _render;
            if (job != null && IsRender(e.GlobalCommandName) && job.EndedAt == DateTime.MinValue) job.EndedAt = DateTime.UtcNow;
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
            if (job == null)
            {
                // Test & sửa G4: the answer of a render that ended while nobody was asking (a status that timed out)
                if (!stop && _lastRenderResult != null) { var last = _lastRenderResult; _lastRenderResult = null; return last; }
                return new Dictionary<string, object> { ["running"] = false, ["note"] = "No render is running." };
            }
            if (stop)
            {
                StopRender();
                _lastRenderResult = null;
                return new Dictionary<string, object> { ["running"] = false, ["cancelled"] = true, ["note"] = "The render was stopped; no file was written. Background, lighting and view are as before." };
            }
            double elapsed = (DateTime.UtcNow - job.Started).TotalSeconds;
            long size = 0;
            try { if (File.Exists(job.Temp)) size = new FileInfo(job.Temp).Length; } catch (Exception) { }
            if (size != job.LastSize) { job.LastSize = size; job.LastSizeChange = DateTime.UtcNow; }
            double same = (DateTime.UtcNow - job.LastSizeChange).TotalSeconds;
            double endedFor = job.EndedAt == DateTime.MinValue ? -1 : (DateTime.UtcNow - job.EndedAt).TotalSeconds;
            var state = RenderLogic.StateOf(elapsed, job.Quality.MaxWaitSeconds, size, job.LastSize, size > 0 ? same : 0, job.Cancelled, job.Failed, endedFor, job.Estimate);
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
                    FinishRender(job, keepTemp: true);
                    try { File.Copy(job.Temp, job.File, true); }
                    catch (Exception ex) { throw ToolException.Invalid($"{tool}: the render is finished but '{job.File}' could not be written ({ex.Message}). The picture is at '{job.Temp}'."); }
                    try { File.Delete(job.Temp); } catch (Exception) { }
                    return Remember(new Dictionary<string, object>
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
                    });
                }
                case RenderLogic.State.Cancelled:
                    FinishRender(job);
                    return Remember(new Dictionary<string, object> { ["running"] = false, ["cancelled"] = true, ["note"] = "The user stopped the render (Esc); no file was written. Background, lighting and view are as before." });
                default: // too long / failed: stop and save the picture of F3 instead
                {
                    if (IsRender(job.Doc?.CommandInProgress)) { try { job.Doc.SendStringToExecute("\x03\x03", true, false, false); } catch (Exception) { } }
                    FinishRender(job);
                    string why = RenderLogic.FallbackReason(state, job.Error);
                    return Remember((Dictionary<string, object>)RenderFallback(job, why, (int)elapsed, tool));
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
            // KFG-01: a saved view used after its transaction (as StartRender does): shown, then the user's view back
            string vname = "CCAD-SELFTEST-G4-" + DateTime.Now.ToString("HHmmss");
            try
            {
                NamedView(new ToolContext(doc, null, true, null), new Params("named_view", new Dictionary<string, object> { ["action"] = "save", ["name"] = vname }));
                using (var copy = NamedViewCopy(doc.Database, vname, "named_view"))
                using (doc.LockDocument())
                using (var cur = doc.Editor.GetCurrentView())
                {
                    var old = (ViewTableRecord)cur.Clone();
                    doc.Editor.SetCurrentView(copy);
                    doc.Editor.SetCurrentView(old);
                    old.Dispose();
                }
            }
            catch (Exception ex) { errs.Add("góc nhìn đã lưu (KFG-01): " + ex.Message); }
            finally
            {
                try
                {
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var vt = (ViewTable)tr.GetObject(doc.Database.ViewTableId, OpenMode.ForRead);
                        if (vt.Has(vname)) tr.GetObject(vt[vname], OpenMode.ForWrite).Erase();
                        tr.Commit();
                    }
                }
                catch (Exception) { }
            }
            info = $"đổi tạm: {string.Join(", ", job.Changes)}; đường lui ảnh {fb["width"]}×{fb["height"]}; góc nhìn đã lưu dùng được";
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
