using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Phase 2 — external references.</summary>
    public static partial class DrawingService
    {
        private static void RegisterXrefs(ToolDispatcher d)
        {
            d.Register("attach_xref", true, AttachXref);
            d.Register("list_xrefs", false, ListXrefs);
            d.Register(new ToolDef("reload_xref", true, (ctx, p) => XrefOp(ctx, p, "reload"), noTransaction: true));
            d.Register(new ToolDef("detach_xref", true, (ctx, p) => XrefOp(ctx, p, "detach"), noTransaction: true));
            d.Register(new ToolDef("bind_xref", true, (ctx, p) => XrefOp(ctx, p, "bind"), noTransaction: true));
        }

        private static object AttachXref(ToolContext ctx, Params p)
        {
            string path = p.Str("path").Trim().Trim('"');
            if (!path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid("attach_xref: 'path' must be a .dwg file.");
            if (!File.Exists(path)) throw ToolException.Invalid($"attach_xref: file not found: {path}. Ask the user for the correct full path.");
            string name = p.Str("name", Path.GetFileNameWithoutExtension(path));
            ValidateSymbolName(name, "xref");
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(name)) throw ToolException.Invalid($"attach_xref: a block or xref named '{name}' already exists. Pass another 'name'.");

            bool overlay = p.Bool("overlay", true);
            ObjectId xid = overlay ? ctx.Db.OverlayXref(path, name) : ctx.Db.AttachXref(path, name);
            if (xid.IsNull) throw new ToolException(ErrorCodes.AcadError, $"attach_xref: AutoCAD could not attach {path}.");
            var pos = p.PointOpt("position") ?? new[] { 0.0, 0, 0 };
            var br = new BlockReference(Acad.P3(pos), xid)
            {
                ScaleFactors = new Autodesk.AutoCAD.Geometry.Scale3d(p.Positive("scale", 1)),
                Rotation = Geometry.DegToRad(p.Num("rotation", 0)),
            };
            var id = Acad.Add(ctx, br, p);
            var r = Acad.Handles(id);
            r["name"] = name;
            r["overlay"] = overlay;
            return r;
        }

        private static object ListXrefs(ToolContext ctx, Params p)
        {
            var list = new List<object>();
            var graph = ctx.Db.GetHostDwgXrefGraph(true);
            var status = new Dictionary<ObjectId, string>();
            for (int i = 0; i < graph.NumNodes; i++)
            {
                var node = graph.GetXrefNode(i);
                if (!node.BlockTableRecordId.IsNull) status[node.BlockTableRecordId] = node.XrefStatus.ToString();
            }
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (!btr.IsFromExternalReference) continue;
                int refs = 0;
                try { refs = btr.GetBlockReferenceIds(true, false).Count; } catch { }
                list.Add(new Dictionary<string, object>
                {
                    ["name"] = btr.Name,
                    ["path"] = btr.PathName,
                    ["overlay"] = btr.IsFromOverlayReference,
                    ["status"] = status.TryGetValue(id, out var s) ? s : "Unknown",
                    ["references"] = refs,
                });
            }
            return new Dictionary<string, object> { ["count"] = list.Count, ["xrefs"] = list };
        }

        private static object XrefOp(ToolContext ctx, Params p, string op)
        {
            var names = p.Strings("names", 1);
            var ids = new ObjectIdCollection();
            using (ctx.Doc.LockDocument())
            {
                using (var tr = ctx.Doc.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
                    foreach (var n in names)
                    {
                        if (!bt.Has(n)) throw ToolException.Invalid($"{op}_xref: '{n}' is not in this drawing. Call list_xrefs.");
                        var btr = (BlockTableRecord)tr.GetObject(bt[n], OpenMode.ForRead);
                        if (!btr.IsFromExternalReference) throw ToolException.Invalid($"{op}_xref: '{n}' is a normal block, not an xref.");
                        ids.Add(bt[n]);
                    }
                    tr.Commit();
                }
                switch (op)
                {
                    case "reload": ctx.Db.ReloadXrefs(ids); break;
                    case "detach": foreach (ObjectId id in ids) ctx.Db.DetachXref(id); break;
                    case "bind": ctx.Db.BindXrefs(ids, p.Bool("insert", true)); break;
                }
            }
            return new Dictionary<string, object> { ["done"] = op, ["names"] = names };
        }
    }
}
