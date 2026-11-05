using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>A set of entities picked or currently selected in the drawing.</summary>
    public sealed class PickedSelection
    {
        public List<string> Handles = new List<string>();
        public string Summary = "";
        public int Count => Handles.Count;
    }

    /// <summary>
    /// Palette ↔ drawing interaction (features 1-5): picking objects / points through
    /// helper commands, tracking the current PICKFIRST selection, zoom + temporary
    /// highlight of handles, and undoing the last chat turn.
    /// All public methods are called on the AutoCAD main thread.
    /// </summary>
    public static class Interaction
    {
        public const string PickCommand = "CLAUDECAD_PICK";
        public const string PointCommand = "CLAUDECAD_POINT";

        /// <summary>Raised when CLAUDECAD_PICK finished with a selection.</summary>
        public static event Action<PickedSelection> SelectionPicked;
        /// <summary>Raised when CLAUDECAD_POINT finished with one or more points.</summary>
        public static event Action<List<Point3d>> PointsPicked;
        /// <summary>Raised when the PICKFIRST selection of the active drawing changes.</summary>
        public static event Action<PickedSelection> ImpliedSelectionChanged;

        private static readonly HashSet<Document> Watched = new HashSet<Document>();
        private static List<ObjectId> _highlighted = new List<ObjectId>();
        private static Document _highlightDoc;
        private static System.Windows.Forms.Timer _unhighlightTimer;

        // ------------------------------------------------------------------ helper commands

        /// <summary>Starts a helper command in the active drawing and gives AutoCAD the focus.</summary>
        public static void Start(string command)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try { Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView(); } catch { /* focus is a nicety */ }
            doc.SendStringToExecute("_" + command + " ", true, false, false);
        }

        internal static PickedSelection Describe(Document doc, IEnumerable<ObjectId> ids)
        {
            var sel = new PickedSelection();
            var byType = new Dictionary<string, int>();
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in ids)
                {
                    if (id.IsNull || id.IsErased) continue;
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    sel.Handles.Add(e.Handle.ToString());
                    var t = Acad.TypeName(e);
                    byType[t] = byType.TryGetValue(t, out var n) ? n + 1 : 1;
                }
                tr.Commit();
            }
            sel.Summary = string.Join(", ", byType.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Value} {kv.Key}"))
                + (byType.Count > 4 ? ", …" : "");
            return sel;
        }

        internal static void RunPick()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var opts = new PromptSelectionOptions { MessageForAdding = "\nChọn đối tượng gửi cho Claude (Enter để xong): " };
            var res = ed.GetSelection(opts);
            if (res.Status != PromptStatus.OK || res.Value == null) return;
            var sel = Describe(doc, res.Value.GetObjectIds());
            ed.WriteMessage($"\nClaudeCAD: đã chọn {sel.Count} đối tượng để gửi kèm tin nhắn.\n");
            SelectionPicked?.Invoke(sel);
        }

        internal static void RunPoints()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var pts = new List<Point3d>();
            while (true)
            {
                var o = new PromptPointOptions(pts.Count == 0 ? "\nChọn điểm cho Claude: " : "\nĐiểm tiếp theo (Enter để xong): ") { AllowNone = true };
                if (pts.Count > 0) { o.UseBasePoint = true; o.BasePoint = pts[pts.Count - 1]; }
                var r = ed.GetPoint(o);
                if (r.Status != PromptStatus.OK) break;
                // GetPoint returns UCS; the protocol is WCS
                pts.Add(r.Value.TransformBy(ed.CurrentUserCoordinateSystem));
                if (pts.Count >= 50) break;
            }
            if (pts.Count > 0) PointsPicked?.Invoke(pts);
        }

        /// <summary>"[12400, 5600]" (rounded to 0.1 mm), several points separated by ", ".</summary>
        public static string FormatPoints(IEnumerable<Point3d> pts) =>
            string.Join(", ", pts.Select(p =>
                $"[{Math.Round(p.X, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}, {Math.Round(p.Y, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}]"));

        // ------------------------------------------------------------------ PICKFIRST tracking

        public static void Watch(Document doc)
        {
            if (doc == null || Watched.Contains(doc)) return;
            Watched.Add(doc);
            doc.ImpliedSelectionChanged += OnImpliedSelectionChanged;
        }

        public static void Unwatch(Document doc)
        {
            if (doc == null || !Watched.Remove(doc)) return;
            try { doc.ImpliedSelectionChanged -= OnImpliedSelectionChanged; } catch { }
        }

        private static void OnImpliedSelectionChanged(object sender, EventArgs e)
        {
            try
            {
                var doc = sender as Document;
                if (doc == null || doc != AcApp.DocumentManager.MdiActiveDocument) return;
                var res = doc.Editor.SelectImplied();
                var ids = res.Status == PromptStatus.OK && res.Value != null ? res.Value.GetObjectIds() : new ObjectId[0];
                ImpliedSelectionChanged?.Invoke(Describe(doc, ids));
            }
            catch (System.Exception ex)
            {
                Logger.Warn("implied selection read failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ zoom + highlight

        /// <summary>Zooms to the handles that still exist and highlights them for a few seconds. Returns how many were found.</summary>
        public static int ZoomAndHighlight(IEnumerable<string> handles, int seconds = 4)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;
            ClearHighlight();
            var ids = new List<ObjectId>();
            Extents3d? ext = null;
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles.Distinct(StringComparer.OrdinalIgnoreCase).Take(5000))
                {
                    ObjectId id;
                    try { id = Acad.IdFromHandle(doc.Database, h); } catch (ToolException) { continue; }
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    ids.Add(id);
                    var x = EntityInfo.Extents(e);
                    if (x.HasValue) { if (ext.HasValue) { var a = ext.Value; a.AddExtents(x.Value); ext = a; } else ext = x; }
                }
                tr.Commit();
            }
            if (ids.Count == 0) return 0;
            if (ext.HasValue)
            {
                var mn = ext.Value.MinPoint;
                var mx = ext.Value.MaxPoint;
                // small objects: show some context around them
                double pad = Math.Max(500, 0.15 * Math.Max(mx.X - mn.X, mx.Y - mn.Y));
                DrawingService.ZoomTo(doc, new Point3d(mn.X - pad, mn.Y - pad, 0), new Point3d(mx.X + pad, mx.Y + pad, 0), 1.0);
            }
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in ids) ((Entity)tr.GetObject(id, OpenMode.ForRead)).Highlight();
                tr.Commit();
            }
            _highlighted = ids;
            _highlightDoc = doc;
            if (_unhighlightTimer == null)
            {
                _unhighlightTimer = new System.Windows.Forms.Timer();
                _unhighlightTimer.Tick += (s, e) => { _unhighlightTimer.Stop(); ClearHighlight(); };
            }
            _unhighlightTimer.Interval = Math.Max(1, seconds) * 1000;
            _unhighlightTimer.Start();
            try { doc.Editor.UpdateScreen(); } catch { }
            return ids.Count;
        }

        public static void ClearHighlight()
        {
            var doc = _highlightDoc;
            var ids = _highlighted;
            _highlighted = new List<ObjectId>();
            _highlightDoc = null;
            if (doc == null || doc.IsDisposed || ids.Count == 0) return;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (var id in ids)
                        if (!id.IsErased && tr.GetObject(id, OpenMode.ForRead) is Entity e) e.Unhighlight();
                    tr.Commit();
                }
                doc.Editor.UpdateScreen();
            }
            catch (System.Exception ex)
            {
                Logger.Warn("unhighlight failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ undo the last turn

        /// <summary>Queues one U (the last chat turn is one undo group).</summary>
        public static bool UndoOnce()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            doc.SendStringToExecute("_.U ", true, false, false);
            return true;
        }
    }

    /// <summary>Helper commands started by the palette buttons.</summary>
    public sealed class InteractionCommands
    {
        [CommandMethod(Interaction.PickCommand, CommandFlags.Modal | CommandFlags.NoHistory)]
        public void Pick()
        {
            try { Interaction.RunPick(); }
            catch (System.Exception ex) { Logger.Error("CLAUDECAD_PICK failed", ex); }
        }

        [CommandMethod(Interaction.PointCommand, CommandFlags.Modal | CommandFlags.NoHistory)]
        public void Points()
        {
            try { Interaction.RunPoints(); }
            catch (System.Exception ex) { Logger.Error("CLAUDECAD_POINT failed", ex); }
        }
    }
}
