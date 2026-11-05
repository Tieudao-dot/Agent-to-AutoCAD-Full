using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Temporarily turns undo recording off, so palette-side view changes (Xem kết quả,
    /// handle links) do not push an undo step between the chat turn and "Hoàn tác lượt này".
    /// </summary>
    internal sealed class UndoSuppressor : IDisposable
    {
        private readonly Database _db;
        private readonly bool _active;

        public UndoSuppressor(Database db, bool active)
        {
            _db = db;
            _active = active;
            if (_active) _db.DisableUndoRecording(true);
        }

        public void Dispose()
        {
            if (_active) _db.DisableUndoRecording(false);
        }
    }

    /// <summary>Spec §7.7 — view and document.</summary>
    public static partial class DrawingService
    {
        private static void RegisterView(ToolDispatcher d, ITurnHost host)
        {
            d.Register(new ToolDef("zoom_extents", true, ZoomExtents, noTransaction: true));
            d.Register(new ToolDef("zoom_window", true, ZoomWindowTool, noTransaction: true));
            d.Register(new ToolDef("regen", false, (ctx, p) => { ctx.Ed.Regen(); return new Dictionary<string, object> { ["ok"] = true }; }, noTransaction: true));
            d.Register(new ToolDef("save_drawing", false, SaveDrawing, noTransaction: true));
            d.Register(new ToolDef("purge_unused", true, PurgeUnused, batchForbidden: true));
            d.Register(new ToolDef("undo_last_turn", false, (ctx, p) => UndoLastTurn(ctx, host), noTransaction: true));
        }

        /// <summary>Sets the current view to a WCS window (plan view assumed, like ZOOM W).</summary>
        private static void ZoomTo(ToolContext ctx, Point3d min, Point3d max, double margin) => ZoomTo(ctx.Doc, min, max, margin);

        /// <summary>Shared with the palette (Xem kết quả / handle links).</summary>
        internal static void ZoomTo(Document doc, Point3d min, Point3d max, double margin, bool recordUndo = true)
        {
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (new UndoSuppressor(doc.Database, !recordUndo))
            using (var view = ed.GetCurrentView())
            {
                var wcs2dcs = (Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target)
                    * Matrix3d.Displacement(view.Target - Point3d.Origin)
                    * Matrix3d.PlaneToWorld(view.ViewDirection)).Inverse();
                var ext = new Extents3d(min, max);
                ext.TransformBy(wcs2dcs);
                double w = Math.Max(ext.MaxPoint.X - ext.MinPoint.X, 1) * margin;
                double h = Math.Max(ext.MaxPoint.Y - ext.MinPoint.Y, 1) * margin;
                view.CenterPoint = new Point2d((ext.MinPoint.X + ext.MaxPoint.X) / 2, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
                // keep the window's aspect ratio inside the viewport
                double ratio = view.Width / Math.Max(view.Height, 1e-9);
                if (w / h > ratio) { view.Width = w; view.Height = w / ratio; }
                else { view.Height = h; view.Width = h * ratio; }
                ed.SetCurrentView(view);
            }
        }

        private static object ZoomExtents(ToolContext ctx, Params p)
        {
            Extents3d? ext = null;
            using (var tr = ctx.Doc.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    var x = EntityInfo.Extents(e);
                    if (!x.HasValue) continue;
                    if (ext.HasValue) { var a = ext.Value; a.AddExtents(x.Value); ext = a; } else ext = x;
                }
                tr.Commit();
            }
            if (!ext.HasValue) return new Dictionary<string, object> { ["ok"] = true, ["note"] = "Drawing is empty; view unchanged." };
            ZoomTo(ctx, ext.Value.MinPoint, ext.Value.MaxPoint, 1.05);
            return new Dictionary<string, object> { ["ok"] = true, ["min"] = Acad.J(ext.Value.MinPoint), ["max"] = Acad.J(ext.Value.MaxPoint) };
        }

        private static object ZoomWindowTool(ToolContext ctx, Params p)
        {
            var a = p.Point("p1");
            var b = p.Point("p2");
            if (Math.Abs(a[0] - b[0]) < Geometry.Eps && Math.Abs(a[1] - b[1]) < Geometry.Eps) throw ToolException.Invalid("zoom_window: p1 and p2 must differ.");
            ZoomTo(ctx, new Point3d(Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), 0), new Point3d(Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), 0), 1.0);
            return new Dictionary<string, object> { ["ok"] = true };
        }

        private static object SaveDrawing(ToolContext ctx, Params p)
        {
            dynamic acadDoc = ctx.Doc.GetAcadDocument();
            var path = p.Str("path", null);
            bool named = Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("DWGTITLED")) != 0;
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                    throw ToolException.Invalid("save_drawing: 'path' must end with .dwg.");
                acadDoc.SaveAs(path);
                return new Dictionary<string, object> { ["saved"] = true, ["path"] = path };
            }
            if (!named)
                throw ToolException.Invalid("save_drawing: this drawing has never been saved. Ask the user for a full file path (…\\name.dwg) and pass it as 'path'.");
            acadDoc.Save();
            return new Dictionary<string, object> { ["saved"] = true, ["path"] = ctx.Doc.Name };
        }

        private static object PurgeUnused(ToolContext ctx, Params p)
        {
            var db = ctx.Db;
            var tables = new (string kind, ObjectId tableId)[]
            {
                ("blocks", db.BlockTableId), ("layers", db.LayerTableId), ("linetypes", db.LinetypeTableId),
                ("text_styles", db.TextStyleTableId), ("dim_styles", db.DimStyleTableId),
            };
            var purged = new Dictionary<string, List<object>>();
            int total = 0;
            // repeat: purging a block can make its layers/linetypes purgeable
            for (int pass = 0; pass < 5; pass++)
            {
                int thisPass = 0;
                foreach (var (kind, tableId) in tables)
                {
                    var ids = new ObjectIdCollection();
                    var table = (SymbolTable)ctx.Tr.GetObject(tableId, OpenMode.ForRead);
                    foreach (ObjectId id in table)
                    {
                        var rec = ctx.Tr.GetObject(id, OpenMode.ForRead);
                        if (rec is BlockTableRecord btr && (btr.IsLayout || btr.IsAnonymous || btr.IsFromExternalReference)) continue;
                        ids.Add(id);
                    }
                    db.Purge(ids);
                    foreach (ObjectId id in ids)
                    {
                        var rec = (SymbolTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                        if (!purged.ContainsKey(kind)) purged[kind] = new List<object>();
                        purged[kind].Add(rec.Name);
                        rec.Erase();
                        thisPass++;
                    }
                }
                total += thisPass;
                if (thisPass == 0) break;
            }
            var r = new Dictionary<string, object> { ["purged_count"] = total };
            foreach (var kv in purged) r[kv.Key] = kv.Value;
            return r;
        }

        private static object UndoLastTurn(ToolContext ctx, ITurnHost host)
        {
            if (host.Undo.IsOpen)
                throw ToolException.Invalid("undo_last_turn: this turn has already changed the drawing. Call undo_last_turn first in a turn, before any drawing change, or tell the user to type U.");
            // Queue a single U: it reverts the previous turn's undo group once AutoCAD is idle.
            ctx.Doc.SendStringToExecute("_.U ", true, false, false);
            return new Dictionary<string, object>
            {
                ["queued"] = true,
                ["note"] = "AutoCAD will run U right after this turn's tool call; the previous chat turn is undone as one group. Do not draw anything else in this turn.",
            };
        }
    }
}
