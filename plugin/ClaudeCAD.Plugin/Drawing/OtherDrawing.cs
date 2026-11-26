using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H3 (shared with H4): another drawing file opened only to read it, next to the open drawing. The file is
    /// read into memory by a side database (ReadDwgFile with OpenForReadAndAllShare, then CloseInput: it is released at once,
    /// never locked, never saved) or, for a .dxf, imported into an empty side database. Its path follows the rule of
    /// trace_image (a drive of this computer, a file attached in the chat, or the same network folder as the open drawing).
    /// Errors come back as ToolException messages for Claude (Core/OtherDrawing.cs): in use, newer AutoCAD, damaged.
    /// <code>using (var od = OtherDrawing.Open(ctx, path, "compare_drawings")) { foreach (var e in od.ModelEntities(tr)) … }</code>
    /// </summary>
    internal sealed class OtherDrawing : IDisposable
    {
        public Database Db { get; private set; }
        public string Path { get; private set; }
        public string Kind { get; private set; }
        /// <summary>DWG file code ("AC1032"), null for a DXF.</summary>
        public string Code { get; private set; }
        /// <summary>The file is also open in this AutoCAD: what was read is its last saved state.</summary>
        public bool OpenHere { get; private set; }
        public bool OpenHereModified { get; private set; }

        public double MmPerUnit
        {
            get { try { return FloorPlanLogic.MmPerUnit(Convert.ToInt32(Db.Insunits), out _); } catch (Exception) { return 1; } }
        }

        /// <summary>The drawing format code of this AutoCAD ("AC1032").</summary>
        public static string OurCode
        {
            get
            {
                try
                {
                    var names = Enum.GetNames(typeof(DwgVersion));
                    return OtherDrawingLogic.NewestCode(names, n => (int)Enum.Parse(typeof(DwgVersion), n), (int)DwgVersion.Current);
                }
                catch (Exception) { return null; }
            }
        }

        public static OtherDrawing Open(ToolContext ctx, string path, string tool)
        {
            path = (path ?? "").Trim().Trim('"');
            DrawingService.CheckPathAllowed(ctx, path, DrawingService.AttachmentFolder(), tool);
            var plain = TraceLogic.PlainPath(path) ?? path;
            bool exists = File.Exists(plain);
            long bytes = exists ? new FileInfo(plain).Length : 0;
            string kind = OtherDrawingLogic.KindOf(path, exists, bytes, tool);
            string full = System.IO.Path.GetFullPath(plain);
            string ours = OurCode;
            string code = null;
            if (kind == "dwg") code = RequireReadableHeader(full, ours, tool);
            var od = new OtherDrawing { Path = full, Kind = kind, Code = code };
            foreach (Document d in AcApp.DocumentManager)
            {
                try
                {
                    if (d.IsNamedDrawing && OtherDrawingLogic.SameFile(d.Name, full))
                    {
                        od.OpenHere = true;
                        od.OpenHereModified = Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0 && d == AcApp.DocumentManager.MdiActiveDocument;
                    }
                }
                catch (Exception) { }
            }
            var db = new Database(false, true);
            try
            {
                if (kind == "dwg")
                {
                    db.ReadDwgFile(full, FileOpenMode.OpenForReadAndAllShare, true, "");
                    db.CloseInput(true); // everything in memory: the file is released now
                }
                else
                {
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ClaudeCAD-dxfin.log");
                    db.DxfIn(full, log);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                db.Dispose();
                throw ToolException.Invalid(OtherDrawingLogic.ReadFailure(full, ex.ErrorStatus.ToString(), code, ours, tool));
            }
            catch (Exception ex) when (!(ex is ToolException))
            {
                db.Dispose();
                throw ToolException.Invalid(OtherDrawingLogic.ReadFailure(full, ex.GetType().Name + ": " + ex.Message, code, ours, tool));
            }
            od.Db = db;
            return od;
        }

        /// <summary>
        /// The file code of a DWG read from its first bytes without locking it, or a refusal (not a DWG, saved by a newer AutoCAD,
        /// locked / no permission), before AutoCAD reads the file. Test tổng thể đợt H: also used by batch_files before it writes
        /// a file (one message for every tool that reads another drawing).
        /// </summary>
        internal static string RequireReadableHeader(string full, string ours, string tool)
        {
            var head = new byte[6];
            int got = 0;
            try { using (var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) got = fs.Read(head, 0, head.Length); }
            catch (IOException ex) { throw ToolException.Invalid(OtherDrawingLogic.ReadFailure(full, "FileSharingViolation: " + ex.Message, null, ours, tool)); }
            catch (UnauthorizedAccessException ex) { throw ToolException.Invalid(OtherDrawingLogic.ReadFailure(full, "AccessDenied: " + ex.Message, null, ours, tool)); }
            var code = got == 6 ? OtherDrawingLogic.DwgCode(head) : null;
            var refusal = OtherDrawingLogic.HeaderRefusal(full, code, ours, tool);
            if (refusal != null) throw ToolException.Invalid(refusal);
            return code;
        }

        /// <summary>The objects of its model space, at most <paramref name="limit"/> (and how many there are), in a read transaction.</summary>
        public List<Entity> ModelEntities(Transaction tr, int limit, out int total)
        {
            var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var list = new List<Entity>();
            total = 0;
            foreach (ObjectId id in ms)
            {
                total++;
                if (list.Count >= limit) continue;
                if (tr.GetObject(id, OpenMode.ForRead, false) is Entity e) list.Add(e);
            }
            return list;
        }

        /// <summary>A few words for the result: file, release, units, and the note when it is open here with unsaved changes.</summary>
        public Dictionary<string, object> Info()
        {
            var d = new Dictionary<string, object>
            {
                ["file"] = Path,
                ["format"] = Kind == "dxf" ? "DXF" : Code + " (" + OtherDrawingLogic.ReleaseOf(Code) + ")",
                ["units_mm_per_unit"] = MmPerUnit,
            };
            if (OpenHere) d["note"] = OpenHereModified
                ? "The file is also open in this AutoCAD with unsaved changes: its last saved state was read."
                : "The file is also open in this AutoCAD: its saved state was read.";
            return d;
        }

        public void Dispose()
        {
            try { Db?.Dispose(); } catch (Exception) { }
            Db = null;
        }
    }
}
