using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Everything a tool handler needs: the document, its database and the one
    /// open transaction of the current request. Handlers never commit or abort —
    /// the dispatcher owns the transaction.
    /// </summary>
    public sealed class ToolContext
    {
        public Document Doc { get; }
        public Database Db { get; }
        public Editor Ed { get; }
        public Transaction Tr { get; internal set; }
        public bool Writable { get; }
        public ToolDispatcher Dispatcher { get; }

        /// <summary>Set by handlers that changed the display; the dispatcher regens once at the end.</summary>
        public bool NeedsRegen { get; set; }

        /// <summary>True while executing inside batch_execute.</summary>
        public bool InBatch { get; internal set; }

        private BlockTableRecord _modelSpace;

        public ToolContext(Document doc, Transaction tr, bool writable, ToolDispatcher dispatcher)
        {
            Doc = doc;
            Db = doc.Database;
            Ed = doc.Editor;
            Tr = tr;
            Writable = writable;
            Dispatcher = dispatcher;
        }

        public BlockTableRecord ModelSpace
        {
            get
            {
                if (_modelSpace == null || _modelSpace.IsDisposed)
                {
                    var bt = (BlockTable)Tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                    _modelSpace = (BlockTableRecord)Tr.GetObject(bt[BlockTableRecord.ModelSpace],
                        Writable ? OpenMode.ForWrite : OpenMode.ForRead);
                }
                return _modelSpace;
            }
        }

        internal void ResetCache() => _modelSpace = null;

        /// <summary>The entity was added in this turn (or this macro run); false when unknown, so the caller asks first.</summary>
        public bool CreatedThisTurn(string handle) => Dispatcher != null && Dispatcher.CreatedThisTurn(handle);

        /// <summary>
        /// Owner block of new entities: model space, or the paper space of a named
        /// layout (phase 2: drawing title blocks / notes on layouts).
        /// </summary>
        public BlockTableRecord SpaceFor(string layout)
        {
            if (string.IsNullOrEmpty(layout) || layout.Equals("Model", StringComparison.OrdinalIgnoreCase)) return ModelSpace;
            var dict = (DBDictionary)Tr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict)
            {
                if (!e.Key.Equals(layout, StringComparison.OrdinalIgnoreCase)) continue;
                var lo = (Layout)Tr.GetObject(e.Value, OpenMode.ForRead);
                return (BlockTableRecord)Tr.GetObject(lo.BlockTableRecordId, Writable ? OpenMode.ForWrite : OpenMode.ForRead);
            }
            throw new ToolException(ErrorCodes.InvalidParams, $"Layout '{layout}' does not exist. Call list_layouts, or create_layout first.");
        }

        public void RequireWritable(string tool)
        {
            if (!Writable) throw new ToolException(ErrorCodes.AcadError, tool + " needs write access but was dispatched read-only (plugin bug).");
        }

        public static string HandleOf(ObjectId id) => id.Handle.ToString();
    }
}
