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
        /// <summary>Layer of each handle (same order as Handles); empty when the selection was too large to analyse.</summary>
        public List<string> HandleLayers = new List<string>();
        public string Summary = "";
        public int Count => Handles.Count;
        /// <summary>Objects per layer, most first (empty when not analysed).</summary>
        public Dictionary<string, int> Layers => LayerSelection.CountByLayer(HandleLayers);
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

        /// <summary>Selections larger than this are sent by handle without opening every object.</summary>
        public const int DescribeLimit = 2000;
        /// <summary>"Chọn đối tượng" (explicit pick): objects are opened to read their layer up to this many.</summary>
        public const int PickDescribeLimit = 20000;

        internal static PickedSelection Describe(Document doc, IEnumerable<ObjectId> ids, int limit = DescribeLimit)
        {
            var sel = new PickedSelection();
            var byType = new Dictionary<string, int>();
            var all = ids as ObjectId[] ?? ids.ToArray();
            if (all.Length > limit)
            {
                // Ctrl+A on a big drawing: do not open thousands of objects on the UI thread
                foreach (var id in all) if (!id.IsNull && !id.IsErased) sel.Handles.Add(id.Handle.ToString());
                sel.Summary = sel.Handles.Count + " đối tượng";
                return sel;
            }
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in all)
                {
                    if (id.IsNull || id.IsErased) continue;
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    sel.Handles.Add(e.Handle.ToString());
                    sel.HandleLayers.Add(e.Layer);
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
            var sel = Describe(doc, res.Value.GetObjectIds(), PickDescribeLimit);
            var layers = sel.Layers;
            ed.WriteMessage($"\nClaudeCAD: đã chọn {sel.Count} đối tượng" + (layers.Count > 0 ? $" trên {layers.Count} layer ({string.Join(", ", layers.Keys.Take(5))}{(layers.Count > 5 ? ", …" : "")})" : "") + " để gửi kèm tin nhắn.\n");
            SelectionPicked?.Invoke(sel);
        }

        internal static void RunPoints()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var pts = new List<Point3d>();
            Point3d? lastUcs = null;
            while (true)
            {
                var o = new PromptPointOptions(pts.Count == 0 ? "\nChọn điểm cho Claude: " : "\nĐiểm tiếp theo (Enter để xong): ") { AllowNone = true };
                if (lastUcs.HasValue) { o.UseBasePoint = true; o.BasePoint = lastUcs.Value; } // BasePoint is in UCS
                var r = ed.GetPoint(o);
                if (r.Status != PromptStatus.OK) break;
                lastUcs = r.Value;
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
                DrawingService.ZoomTo(doc, new Point3d(mn.X - pad, mn.Y - pad, 0), new Point3d(mx.X + pad, mx.Y + pad, 0), 1.0, recordUndo: false);
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

        // ------------------------------------------------------------------ @ names (layers, blocks, styles)

        /// <summary>Names the user can insert with "@": layers, blocks, text / dimension styles, layouts.</summary>
        public static List<MentionItem> DrawingNames(Document doc)
        {
            var list = new List<MentionItem>();
            if (doc == null) return list;
            var db = doc.Database;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                void Table(ObjectId tableId, string kind, Func<SymbolTableRecord, bool> keep)
                {
                    var table = (SymbolTable)tr.GetObject(tableId, OpenMode.ForRead);
                    foreach (ObjectId id in table)
                    {
                        if (list.Count >= 5000) return;
                        if (!(tr.GetObject(id, OpenMode.ForRead) is SymbolTableRecord r) || r.IsErased || !keep(r)) continue;
                        list.Add(new MentionItem { Name = r.Name, Kind = kind });
                    }
                }
                Table(db.LayerTableId, "layer", r => !r.Name.Contains("|")); // xref layers are noise
                Table(db.BlockTableId, "block", r => r is BlockTableRecord b && !b.IsLayout && !b.IsAnonymous && !b.IsFromExternalReference && !b.Name.Contains("|"));
                Table(db.TextStyleTableId, "text_style", r => r.Name.Length > 0 && !r.Name.Contains("|"));
                Table(db.DimStyleTableId, "dim_style", r => !r.Name.Contains("|"));
                var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in layouts)
                    if (!e.Key.Equals("Model", StringComparison.OrdinalIgnoreCase)) list.Add(new MentionItem { Name = e.Key, Kind = "layout" });
                tr.Commit();
            }
            return list;
        }

        // ------------------------------------------------------------------ layer picker

        /// <summary>Every layer of the drawing with its color (RGB), state and number of objects in model/paper space.</summary>
        public static List<LayerRow> LayerRows(Document doc)
        {
            var rows = new List<LayerRow>();
            if (doc == null) return rows;
            var db = doc.Database;
            var counts = new Dictionary<ObjectId, int>();
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in bt)
                {
                    if (!(tr.GetObject(btrId, OpenMode.ForRead) is BlockTableRecord btr) || !btr.IsLayout) continue;
                    foreach (ObjectId id in btr)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || e is Viewport) continue;
                        counts[e.LayerId] = counts.TryGetValue(e.LayerId, out var n) ? n + 1 : 1;
                    }
                }
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is LayerTableRecord l) || l.IsErased) continue;
                    int rgb = 0xFFFFFF;
                    try { var c = l.Color.ColorValue; rgb = (c.R << 16) | (c.G << 8) | c.B; } catch { }
                    rows.Add(new LayerRow
                    {
                        Name = l.Name,
                        Rgb = rgb,
                        Count = counts.TryGetValue(id, out var n) ? n : 0,
                        On = !l.IsOff,
                        Frozen = l.IsFrozen,
                        Locked = l.IsLocked,
                        Current = id == db.Clayer,
                        Xref = l.IsDependent,
                    });
                }
                tr.Commit();
            }
            return rows.OrderBy(r => r.Xref).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ------------------------------------------------------------------ one point for a macro

        private static Action<Point3d> _onePoint;

        /// <summary>Asks for one point in the drawing (CLAUDECAD_MACROPT) and calls back with it (WCS).</summary>
        public static void PickOnePoint(Action<Point3d> then)
        {
            _onePoint = then;
            Start(OnePointCommand);
        }

        public const string OnePointCommand = "CLAUDECAD_MACROPT";

        internal static void RunOnePoint()
        {
            var cb = _onePoint;
            _onePoint = null;
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || cb == null) return;
            var r = doc.Editor.GetPoint(new PromptPointOptions("\nChọn điểm chèn cho macro (góc trái dưới): "));
            if (r.Status != PromptStatus.OK) return;
            cb(r.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem));
        }

        // ------------------------------------------------------------------ undo the last turn

        /// <summary>
        /// Queues one U (the last chat turn is one undo group). Refused while a command or
        /// prompt is active, where "_.U" would be typed into it (e.g. LINE's Undo option).
        /// </summary>
        public static string UndoOnce()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return "Không có bản vẽ nào đang mở.";
            if (!string.IsNullOrEmpty(doc.CommandInProgress) || !doc.Editor.IsQuiescent)
                return $"AutoCAD đang chạy lệnh {doc.CommandInProgress}; bấm Esc để thoát lệnh rồi thử lại.";
            try { Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView(); } catch { }
            doc.SendStringToExecute("_.U ", true, false, false);
            return null;
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

        [CommandMethod(Interaction.OnePointCommand, CommandFlags.Modal | CommandFlags.NoHistory)]
        public void OnePoint()
        {
            try { Interaction.RunOnePoint(); }
            catch (System.Exception ex) { Logger.Error("CLAUDECAD_MACROPT failed", ex); }
        }

        [CommandMethod(Interaction.PointCommand, CommandFlags.Modal | CommandFlags.NoHistory)]
        public void Points()
        {
            try { Interaction.RunPoints(); }
            catch (System.Exception ex) { Logger.Error("CLAUDECAD_POINT failed", ex); }
        }
    }
}
