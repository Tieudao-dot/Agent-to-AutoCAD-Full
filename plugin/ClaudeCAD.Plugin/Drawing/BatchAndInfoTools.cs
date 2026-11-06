using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.8 batch_execute and §7.6 get_drawing_info.</summary>
    public static partial class DrawingService
    {
        private static void RegisterBatchAndInfo(ToolDispatcher d)
        {
            // writes=false: the dispatcher decides from the sub-calls (a read-only batch needs no lock / undo group)
            d.Register("batch_execute", false, BatchExecute);
            d.Register("get_drawing_info", false, GetDrawingInfo);
        }

        private static object BatchExecute(ToolContext ctx, Params p)
        {
            var calls = p.Array("calls");
            if (calls.Count == 0) throw ToolException.Invalid("batch_execute: 'calls' is empty.");
            if (calls.Count > ToolDispatcher.MaxBatchCalls)
                throw ToolException.Invalid($"batch_execute: {calls.Count} calls given, the limit is {ToolDispatcher.MaxBatchCalls}. Split into several batches.");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = ctx.Dispatcher.ExecuteBatch(ctx, calls);
            int ok = 0, failed = 0;
            var allHandles = new List<object>();
            var compact = new List<object>(results.Count);
            foreach (Dictionary<string, object> r in results)
            {
                if ((bool)r["ok"])
                {
                    ok++;
                    if (r["result"] is Dictionary<string, object> res && res.TryGetValue("handles", out var hs) && hs is IEnumerable<object> hl)
                        allHandles.AddRange(hl);
                }
                else failed++;
                compact.Add(r);
            }
            return new Dictionary<string, object>
            {
                ["ok_count"] = ok,
                ["error_count"] = failed,
                ["elapsed_ms"] = sw.ElapsedMilliseconds,
                ["handles"] = allHandles,
                ["results"] = compact,
            };
        }

        private static readonly Dictionary<int, string> UnitNames = new Dictionary<int, string>
        {
            [0] = "unitless", [1] = "inches", [2] = "feet", [4] = "millimeters", [5] = "centimeters", [6] = "meters", [14] = "decimeters",
        };

        private static object GetDrawingInfo(ToolContext ctx, Params p)
        {
            var db = ctx.Db;
            var byType = new Dictionary<string, int>();
            var byLayer = new Dictionary<string, int>();
            int count = 0;
            Extents3d? ext = null;
            const int extLimit = 100000;
            foreach (ObjectId id in ctx.ModelSpace)
            {
                var ent = ctx.Tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                count++;
                var t = Acad.TypeName(ent);
                byType[t] = byType.TryGetValue(t, out var n) ? n + 1 : 1;
                byLayer[ent.Layer] = byLayer.TryGetValue(ent.Layer, out var m) ? m + 1 : 1;
                if (count <= extLimit && !(ent is Xline) && !(ent is Ray))
                {
                    try
                    {
                        var e = ent.GeometricExtents;
                        if (ext.HasValue) { var x = ext.Value; x.AddExtents(e); ext = x; }
                        else ext = e;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { /* entities without extents */ }
                }
            }

            int insunits = (int)db.Insunits;
            string textStyle = "", dimStyle = "";
            try { textStyle = ((TextStyleTableRecord)ctx.Tr.GetObject(db.Textstyle, OpenMode.ForRead)).Name; } catch { }
            try { dimStyle = ((DimStyleTableRecord)ctx.Tr.GetObject(db.Dimstyle, OpenMode.ForRead)).Name; } catch { }
            var lt = (LayerTable)ctx.Tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int layerCount = 0;
            foreach (ObjectId _ in lt) layerCount++;
            var bt = (BlockTable)ctx.Tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            int blockCount = 0;
            foreach (ObjectId bid in bt)
            {
                var btr = (BlockTableRecord)ctx.Tr.GetObject(bid, OpenMode.ForRead);
                if (!btr.IsLayout && !btr.IsAnonymous && !btr.IsDependent) blockCount++;
            }

            string name = "";
            try { name = Path.GetFileName(ctx.Doc.Name); } catch { }

            var info = new Dictionary<string, object>
            {
                ["drawing"] = name,
                ["path"] = SafeString(() => ctx.Doc.Name),
                ["units"] = UnitNames.TryGetValue(insunits, out var un) ? un : "code " + insunits,
                ["insunits"] = insunits,
                ["measurement"] = db.Measurement == MeasurementValue.Metric ? "metric" : "imperial",
                ["entity_count"] = count,
                ["by_type"] = byType.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["by_layer"] = byLayer.OrderByDescending(kv => kv.Value).Take(50).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["extents"] = ext.HasValue
                    ? new Dictionary<string, object> { ["min"] = Acad.J(ext.Value.MinPoint), ["max"] = Acad.J(ext.Value.MaxPoint) }
                    : null,
                ["current_layer"] = Acad.LayerName(ctx, db.Clayer),
                ["current_text_style"] = textStyle,
                ["current_dim_style"] = dimStyle,
                ["layer_count"] = layerCount,
                ["block_definition_count"] = blockCount,
                ["ltscale"] = Acad.R(db.Ltscale),
                ["dimscale"] = Acad.R(db.Dimscale),
                ["textsize"] = Acad.R(db.Textsize),
                ["modified"] = SafeBool(() => System.Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("DBMOD")) != 0),
            };
            if (insunits != 4)
                info["warning"] = "Drawing units are not millimeters (INSUNITS != 4). Coordinates are still interpreted as drawing units = mm.";
            return info;
        }

        private static string SafeString(Func<string> f)
        {
            try { return f(); } catch { return null; }
        }

        private static object SafeBool(Func<bool> f)
        {
            try { return f(); } catch { return null; }
        }
    }
}
