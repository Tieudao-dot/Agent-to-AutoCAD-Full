using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt F4: block_schedule, area_schedule (đợt C), volume_schedule and check_interference (đợt D) also write
    /// their table to an .xlsx file with 'excel' (true = default name next to the drawing, or a file name / path). The
    /// file rules are those of export_stl / plot_pdf: next to the drawing, an existing file is never replaced unless the
    /// call says overwrite: true (after asking the user). The plan is that of the schedule (the 3D plan exports the 3D
    /// numbers too, without a table in the drawing). The .xlsx is written by Core/Xlsx.cs (no Excel needed).
    /// </summary>
    public static partial class DrawingService
    {
        /// <summary>Writes the table when the call asks for it; adds "excel" (path, rows) to the result. Returns false when not asked.</summary>
        internal static bool ExportExcel(ToolContext ctx, Params p, string tool, Func<List<List<object>>> table, Dictionary<string, object> result)
        {
            var requested = XlsxLogic.Requested(p.Get("excel"), tool);
            if (requested == null) return false;
            var data = table();
            if (data == null || data.Count <= 1)
                throw ToolException.Invalid($"{tool}: nothing to export (no rows), so no Excel file was written.");
            string docName = SafeString(() => ctx.Doc.Name);
            string drawingDir = SafeString(() => Path.GetDirectoryName(ctx.Doc.Name));
            if (string.IsNullOrEmpty(drawingDir) || !Directory.Exists(drawingDir)) drawingDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var full = XlsxLogic.Resolve(requested, drawingDir, docName, tool, Directory.Exists, Directory.Exists);
            if (File.Exists(full) && !p.Bool("overwrite", false)) throw ToolException.Invalid(XlsxLogic.ExistsMessage(tool, full));
            var rows = data.Select(r => (IList<object>)r).ToList();
            try { XlsxWriter.Write(full, rows, F4Numbers.SheetName.TryGetValue(tool, out var sheet) ? sheet : tool); }
            catch (IOException ex) { throw ToolException.Invalid($"{tool}: could not write '{full}' ({ex.Message}). If it is open in Excel, ask the user to close it, then call again."); }
            catch (UnauthorizedAccessException) { throw ToolException.Invalid($"{tool}: no permission to write '{full}'. Give another folder or file name in 'excel'."); }
            result["excel"] = new Dictionary<string, object> { ["path"] = full, ["rows"] = data.Count - 1 };
            return true;
        }
    }
}
