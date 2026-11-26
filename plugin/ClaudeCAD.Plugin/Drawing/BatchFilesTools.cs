using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H5: batch_files — "làm nhiều file một lúc" (a folder or a list of drawings): check (check_drawing, also
    /// against a company standard), blocks (block counts), standards (setup_standards on each file: backup first), pdf
    /// (plot_pdf of each file's layouts). The plugin does ONE file per call (action run, index): never many drawings in memory,
    /// AutoCAD free between files, the assistant's Dừng stops between two files (a file is written to a temporary file and
    /// swapped in, never left half written). plan lists the files and the card token; finish writes the Excel summary.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterBatchFiles(ToolDispatcher d)
        {
            // never changes the open drawing (files next to it): a read for the dispatcher; Chỉ xem is checked per task
            d.Register(new ToolDef("batch_files", false, BatchFiles, noTransaction: true));
        }

        private sealed class BatchPlan
        {
            public string Task, Root, Settings, Token;
            public List<string> Files = new List<string>();
            public int More;
        }

        private static readonly string[] SettingKeys = { "task", "company", "scale", "standard", "groups", "layouts", "make_current", "recursive" };

        private static BatchPlan PlanOf(ToolContext ctx, Params p, string tool)
        {
            var b = new BatchPlan { Task = BatchLogic.TaskOf(p.Str("task")) };
            ctx.Dispatcher?.RequireToolInPlan(BatchLogic.SubTool(b.Task)); // the same plan / work-mode gate as one drawing
            if (BatchLogic.Writes(b.Task) && ctx.Dispatcher?.ViewOnly == true)
                throw new ToolException(ErrorCodes.ReadOnly, $"{tool}: \"Chỉ xem\" is on: the files cannot be changed. Checking, counting and PDF still work.");
            var files = p.StringsOpt("files");
            string folder = p.Str("folder", null);
            if (folder != null) { folder = folder.Trim().Trim('"'); DrawingService.CheckPathAllowed(ctx, folder, AttachmentFolder(), tool); }
            if ((files == null || files.Count == 0) && (folder == null || !Directory.Exists(folder)))
                throw ToolException.Invalid($"{tool}: folder '{folder}' does not exist. Give the folder of the drawings (or files).");
            if (files != null) foreach (var f in files) DrawingService.CheckPathAllowed(ctx, f, AttachmentFolder(), tool);
            b.Root = folder != null ? Path.GetFullPath(folder) : null;
            b.Files = BatchLogic.Collect(b.Task, b.Root, files?.Select(f => Path.GetFullPath(f.Trim().Trim('"'))).ToList(), p.Bool("recursive", false),
                d => { try { return Directory.GetFiles(d); } catch (Exception) { return new string[0]; } },
                d => { try { return Directory.GetDirectories(d); } catch (Exception) { return new string[0]; } }, out b.More);
            if (b.Files.Count == 0) throw ToolException.Invalid($"{tool}: no drawing ({(BatchLogic.Takes(b.Task, "a.dxf") ? ".dwg / .dxf" : ".dwg")}) in '{folder}'{(p.Bool("recursive", false) ? " and its sub-folders" : " (recursive: true also reads the sub-folders)")}.");
            b.Settings = BatchLogic.Settings(p.Raw, SettingKeys);
            b.Token = BatchLogic.Token(b.Task, b.Settings, b.Files);
            if (b.Task == "standards" && p.Has("company")) CompanyStandardLogic.Require(CompanyRoot, p.Str("company"), tool);
            if (b.Task == "check" && p.Has("company")) CompanyStandardLogic.Require(CompanyRoot, p.Str("company"), tool);
            return b;
        }

        /// <summary>The documents open in this AutoCAD by file (to skip a file being written, or print it where it is).</summary>
        private static Document OpenHere(string path)
        {
            foreach (Document d in AcApp.DocumentManager)
            {
                try { if (d.IsNamedDrawing && OtherDrawingLogic.SameFile(d.Name, path)) return d; } catch (Exception) { }
            }
            return null;
        }

        private static string SkipOf(string task, string f)
        {
            var fi = new FileInfo(f);
            bool exists = fi.Exists;
            var here = exists ? OpenHere(f) : null;
            bool modified = false;
            try { modified = here != null && here == AcApp.DocumentManager.MdiActiveDocument && Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0; } catch (Exception) { }
            bool lockFile = exists && (File.Exists(Path.ChangeExtension(f, ".dwl")) || File.Exists(Path.ChangeExtension(f, ".dwl2")));
            return BatchLogic.SkipReason(task, exists, exists ? fi.Length : 0, exists && fi.IsReadOnly, lockFile, here != null, modified);
        }

        internal static object BatchFiles(ToolContext ctx, Params p)
        {
            const string tool = BatchLogic.Tool;
            var action = p.Str("action", "plan").Trim().ToLowerInvariant();
            if (!BatchLogic.Actions.Contains(action)) throw ToolException.Invalid($"{tool}: action must be one of {string.Join(", ", BatchLogic.Actions)}.");
            var b = PlanOf(ctx, p, tool);
            string what = $"{TaskWords(b.Task)} {b.Files.Count} drawing(s){(b.Root != null ? " in " + b.Root : "")}" +
                (BatchLogic.Writes(b.Task) ? $" (each file is copied to {H5Numbers.BackupFolder} first)" : "");

            if (action == "plan")
            {
                var skipped = new List<object>();
                int pdfExisting = 0;
                foreach (var f in b.Files)
                {
                    var why = SkipOf(b.Task, f);
                    if (why != null) skipped.Add(new Dictionary<string, object> { ["file"] = BatchLogic.Show(b.Root, f), ["reason"] = why });
                    if (b.Task == "pdf" && File.Exists(BatchLogic.PdfOf(f))) pdfExisting++;
                }
                var r = new Dictionary<string, object>
                {
                    ["task"] = b.Task,
                    ["count"] = b.Files.Count,
                    ["files"] = b.Files.Take(H5Numbers.MaxListed).Select(f => (object)BatchLogic.Show(b.Root, f)).ToList(),
                    ["will_skip"] = skipped.Take(H5Numbers.MaxListed).ToList(),
                    ["stamp"] = BatchLogic.Stamp(DateTime.Now),
                    ["confirm_token"] = b.Token,
                };
                if (b.More > 0) r["not_included"] = $"{b.More} more drawing(s): at most {H5Numbers.MaxFiles} in one run (run again on a sub-folder or a list of files).";
                if (b.Files.Count > H5Numbers.MaxListed) r["files_more"] = b.Files.Count - H5Numbers.MaxListed;
                if (BatchLogic.Writes(b.Task)) r["backup_folder"] = Path.Combine(b.Root ?? Path.GetDirectoryName(b.Files[0]), H5Numbers.BackupFolder);
                if (pdfExisting > 0) r["pdf_overwrite"] = pdfExisting;
                r["card"] = $"Ask the user with request_confirmation (handles = [confirm_token]) in one Vietnamese sentence: {what}" +
                    (pdfExisting > 0 ? $", {pdfExisting} PDF(s) already there are replaced" : "") + $", {skipped.Count} skipped. Then call again with confirm_handles.";
                return r;
            }

            // run / finish: only after the card of this exact run (task, settings, files)
            RequireCard(ctx, p, tool, what, new[] { b.Token });
            var stamp = p.Str("stamp", null);
            if (!BatchLogic.ValidStamp(stamp)) throw ToolException.Invalid($"{tool}: 'stamp' must be the stamp of the plan (yyyyMMdd-HHmmss).");

            if (action == "finish") return FinishBatch(ctx, p, b, stamp, tool);

            int index = p.Int("index", 0, 0, b.Files.Count - 1);
            var file = b.Files[index];
            var row = new Dictionary<string, object> { ["file"] = BatchLogic.Show(b.Root, file), ["index"] = index, ["of"] = b.Files.Count };
            var sw = Stopwatch.StartNew();
            var skip = SkipOf(b.Task, file);
            if (skip != null) { row["status"] = "skipped"; row["reason"] = skip; return row; }
            try
            {
                switch (b.Task)
                {
                    case "check": BatchCheck(ctx, p, file, row, tool); break;
                    case "blocks": BatchBlocks(ctx, file, row, tool); break;
                    case "standards": BatchStandards(ctx, p, b, stamp, file, row, tool); break;
                    default: BatchPdf(ctx, p, file, row, tool); break;
                }
                row["status"] = "ok";
            }
            catch (ToolException ex) { row["status"] = "error"; row["reason"] = ex.Message; }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) { row["status"] = "error"; row["reason"] = "AutoCAD: " + ex.ErrorStatus; }
            catch (IOException ex) { row["status"] = "error"; row["reason"] = ex.Message; }
            catch (UnauthorizedAccessException ex) { row["status"] = "error"; row["reason"] = ex.Message; }
            row["seconds"] = Math.Round(sw.Elapsed.TotalSeconds, 1);
            return row;
        }

        private static string TaskWords(string task) =>
            task == "check" ? "check" : task == "blocks" ? "count the blocks of" : task == "standards" ? "set up the standard styles in" : "print to PDF";

        // ------------------------------------------------------------ the four tasks (one file each)

        private static void BatchCheck(ToolContext ctx, Params p, string file, Dictionary<string, object> row, string tool)
        {
            var company = p.Has("company") ? CompanyStandardLogic.Require(CompanyRoot, p.Str("company"), tool) : null;
            HashSet<string> only = null;
            var groups = p.StringsOpt("groups");
            if (groups != null) only = new HashSet<string>(groups.Select(g => (DrawingCheckLogic.Find(g) ?? throw ToolException.Invalid($"{tool}: unknown group '{g}'.")).Id), StringComparer.Ordinal);
            using (var od = OtherDrawing.Open(ctx, file, tool))
            using (var tr = od.Db.TransactionManager.StartTransaction())
            {
                var side = new ToolContext(ctx.Doc, od.Db, tr, false, ctx.Dispatcher);
                var s = ScanDrawing(side, only, p.Bool("layouts", true), null, company);
                int fix = 0, consider = 0, hint = 0;
                var top = new List<(string name, int n)>();
                foreach (var def in DrawingCheckLogic.Groups)
                {
                    int n = s.Of(def.Id).Count;
                    if (n == 0) continue;
                    if (def.Id == "no_dimensions") n = 1;
                    if (def.Level == G2Numbers.LevelFix) fix++; else if (def.Level == G2Numbers.LevelConsider) consider++; else hint++;
                    top.Add((def.Name, n));
                }
                row["nen_sua"] = fix; row["can_nhac"] = consider; row["goi_y"] = hint;
                row["note"] = top.Count == 0 ? "Không thấy lỗi" : string.Join("; ", top.Take(H5Numbers.TopGroups).Select(t => $"{t.name} ({t.n})"));
                if (s.Scanned < s.Total) row["note"] += $" (đọc {s.Scanned} / {s.Total} đối tượng)";
                tr.Commit();
            }
        }

        private static void BatchBlocks(ToolContext ctx, string file, Dictionary<string, object> row, string tool)
        {
            var counts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int total = 0;
            using (var od = OtherDrawing.Open(ctx, file, tool))
            using (var tr = od.Db.TransactionManager.StartTransaction())
            {
                foreach (var e in od.ModelEntities(tr, G2Numbers.MaxScan, out _))
                {
                    if (!(e is BlockReference br)) continue;
                    var btr = (BlockTableRecord)tr.GetObject(br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord, OpenMode.ForRead);
                    if (btr.IsFromExternalReference || btr.IsAnonymous || btr.IsLayout) continue;
                    counts[btr.Name] = (counts.TryGetValue(btr.Name, out var c) ? Convert.ToInt32(c) : 0) + 1;
                    total++;
                }
                tr.Commit();
            }
            row["blocks"] = total;
            row["counts"] = counts;
            row["note"] = string.Join("; ", counts.OrderByDescending(kv => Convert.ToInt32(kv.Value)).Take(5).Select(kv => $"{kv.Key} × {kv.Value}"));
        }

        /// <summary>setup_standards on a file: backup, read it into memory, set up, write a temporary copy, swap it in.</summary>
        private static void BatchStandards(ToolContext ctx, Params p, BatchPlan b, string stamp, string file, Dictionary<string, object> row, string tool)
        {
            var backup = BatchLogic.BackupPath(b.Root, stamp, file);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(file, backup, false); // a second pass of the same run keeps the first backup
            row["backup"] = backup;
            var sub = new Dictionary<string, object>();
            foreach (var k in new[] { "company", "scale", "standard", "make_current" }) if (p.Has(k)) sub[k] = p.Get(k);
            var temp = Path.Combine(Path.GetDirectoryName(file), ".claudecad-tam-" + Guid.NewGuid().ToString("N").Substring(0, 10) + ".dwg");
            var keepWorking = HostApplicationServices.WorkingDatabase;
            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(file, FileShare.Read, true, "");
                    db.CloseInput(true); // in memory: the file is free for the swap
                    var version = db.OriginalFileVersion;
                    Dictionary<string, object> r;
                    HostApplicationServices.WorkingDatabase = db;
                    try
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var side = new ToolContext(ctx.Doc, db, tr, true, ctx.Dispatcher);
                            var sp = new Params("setup_standards", sub);
                            r = (Dictionary<string, object>)(sp.Has("company") ? SetupCompanyStandard(side, sp) : SetupStandards(side, sp));
                            tr.Commit();
                        }
                    }
                    finally { HostApplicationServices.WorkingDatabase = keepWorking; }
                    db.SaveAs(temp, version); // the same DWG version as before
                    row["note"] = StandardsNote(r);
                }
                try { File.Replace(temp, file, null); }
                catch (PlatformNotSupportedException) { File.Copy(temp, file, true); File.Delete(temp); }
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
            }
        }

        private static string StandardsNote(Dictionary<string, object> r)
        {
            if (r.ContainsKey("company"))
                return $"tạo {r["created"]}, cập nhật {r["updated"]}, giữ nguyên {r["unchanged"]}" + (r.ContainsKey("kept") ? $"; {((List<object>)r["kept"]).Count} layer khác chuẩn giữ nguyên (không thẻ cho từng file)" : "");
            var parts = new List<string>();
            foreach (var k in new[] { "text_style", "dim_style", "mleader_style" })
                if (r.TryGetValue(k, out var v) && v is Dictionary<string, object> d) parts.Add($"{d["name"]}: {d["status"]}");
            if (r.ContainsKey("kept_handles")) parts.Add("kiểu khác chuẩn giữ nguyên (không thẻ cho từng file)");
            return string.Join("; ", parts);
        }

        /// <summary>plot_pdf of a file: opened read-only in AutoCAD (or the open tab), every layout into one PDF next to it, closed.</summary>
        private static void BatchPdf(ToolContext ctx, Params p, string file, Dictionary<string, object> row, string tool)
        {
            var dm = AcApp.DocumentManager;
            var before = dm.MdiActiveDocument;
            var here = OpenHere(file);
            Document doc = here;
            try
            {
                if (doc == null) doc = DocumentCollectionExtension.Open(dm, file, true);
                dm.MdiActiveDocument = doc;
                var pdf = BatchLogic.PdfOf(file);
                var q = new Dictionary<string, object>
                {
                    ["output"] = pdf,
                    ["layouts"] = p.StringsOpt("layouts")?.Cast<object>().ToList() ?? new List<object> { "all" },
                    // the run's card covered replacing the PDFs next to the drawings
                    [SolidLogic.UserConfirmedParam] = new List<object> { F5Confirm.FileToken(Path.GetFullPath(pdf)) },
                };
                var r = (Dictionary<string, object>)PlotPdf(new ToolContext(doc, null, false, ctx.Dispatcher), new Params("plot_pdf", q));
                row["pdf"] = r.TryGetValue("output", out var o) ? o : pdf;
                row["note"] = r.TryGetValue("layouts", out var l) && l is List<object> ls ? $"{ls.Count} layout" : "";
            }
            finally
            {
                try { if (doc != null && here == null && !doc.IsDisposed) DocumentExtension.CloseAndDiscard(doc); } catch (Exception) { }
                try { if (before != null && !before.IsDisposed) dm.MdiActiveDocument = before; } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------ the summary

        private static object FinishBatch(ToolContext ctx, Params p, BatchPlan b, string stamp, string tool)
        {
            var rows = (p.Has("rows") ? p.Array("rows") : new List<object>()).OfType<Dictionary<string, object>>().ToList();
            var t = BatchLogic.Totals(rows);
            var res = new Dictionary<string, object> { ["task"] = b.Task, ["ok"] = t["ok"], ["error"] = t["error"], ["skipped"] = t["skipped"] };
            if (p.Bool("excel", false))
            {
                var folder = b.Root ?? Path.GetDirectoryName(b.Files[0]);
                var path = BatchLogic.SummaryPath(folder, b.Task, stamp);
                var sheets = BatchLogic.SummaryRows(b.Task, rows).Select(r => (IList<object>)r).ToList();
                try
                {
                    XlsxWriter.Write(path, sheets, "Tong hop");
                    if (b.Task == "blocks")
                    {
                        var bpath = Path.Combine(folder, $"tong-hop-block-{stamp}.xlsx");
                        XlsxWriter.Write(bpath, BatchLogic.BlockRows(rows).Select(r => (IList<object>)r).ToList(), "Block");
                        res["excel_blocks"] = bpath;
                    }
                }
                catch (IOException ex) { throw ToolException.Invalid($"{tool}: could not write '{path}' ({ex.Message})."); }
                catch (UnauthorizedAccessException) { throw ToolException.Invalid($"{tool}: no permission to write in '{folder}'."); }
                res["excel"] = path;
            }
            if (BatchLogic.Writes(b.Task)) res["backup_folder"] = Path.Combine(b.Root ?? Path.GetDirectoryName(b.Files[0]), H5Numbers.BackupFolder, stamp);
            return res;
        }
    }
}
