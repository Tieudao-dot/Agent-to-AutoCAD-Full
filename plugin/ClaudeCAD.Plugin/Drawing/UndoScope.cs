using System;
using Autodesk.AutoCAD.ApplicationServices;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Groups every change made during one chat turn into a single undo group so
    /// that typing U once reverts the whole turn (spec §4, §10, acceptance #8).
    ///
    /// Mechanism: AcadDocument.StartUndoMark()/EndUndoMark() (the COM equivalent of
    /// UNDO BEGIN / UNDO END). The group is opened lazily right before the first
    /// write of the turn — never for read-only turns, otherwise an empty group would
    /// sit on top of the undo stack and the next U would "undo nothing". It is closed
    /// when the turn ends (turn_end, cancel, disconnect, error).
    /// Each tool call still uses its own DocumentLock + Transaction; AutoCAD nests
    /// those per-lock undo records inside the open group.
    /// All methods must run on the AutoCAD main thread.
    /// </summary>
    public sealed class UndoScope
    {
        private Document _doc;

        public bool IsOpen => _doc != null;

        /// <summary>The document whose group is open (null when closed).</summary>
        public Document Document => _doc;

        public void EnsureStarted(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (_doc == doc) return;
            if (_doc != null) End(); // different drawing: close the old group first

            try
            {
                using (doc.LockDocument())
                {
                    dynamic acadDoc = doc.GetAcadDocument();
                    acadDoc.StartUndoMark();
                }
                _doc = doc;
                Logger.Info("Undo group opened for " + SafeName(doc));
            }
            catch (Exception ex)
            {
                // Not fatal: the turn still works, it just needs several U to revert.
                Logger.Error("StartUndoMark failed; turn will not be grouped", ex);
                _doc = null;
            }
        }

        public void End()
        {
            var doc = _doc;
            _doc = null;
            if (doc == null) return;
            try
            {
                if (doc.IsDisposed) return;
                using (doc.LockDocument())
                {
                    dynamic acadDoc = doc.GetAcadDocument();
                    acadDoc.EndUndoMark();
                }
                Logger.Info("Undo group closed for " + SafeName(doc));
            }
            catch (Exception ex)
            {
                Logger.Error("EndUndoMark failed", ex);
            }
        }

        /// <summary>Forget the group without touching the document (document is being destroyed).</summary>
        public void Abandon(Document doc)
        {
            if (_doc == doc) _doc = null;
        }

        private static string SafeName(Document d)
        {
            try { return d.Name; } catch { return "(document)"; }
        }
    }
}
