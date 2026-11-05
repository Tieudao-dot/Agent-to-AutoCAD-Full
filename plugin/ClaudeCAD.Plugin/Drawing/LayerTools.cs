using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.4 — layers (styles are in StyleTools).</summary>
    public static partial class DrawingService
    {
        private static void RegisterLayers(ToolDispatcher d)
        {
            d.Register("create_layer", true, CreateLayer);
            d.Register("list_layers", false, ListLayers);
            d.Register("set_current_layer", true, SetCurrentLayer);
            d.Register("set_layer_props", true, SetLayerProps);
        }

        private static object CreateLayer(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            ValidateSymbolName(name, "layer");
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            bool existed = lt.Has(name);
            LayerTableRecord ltr;
            ObjectId id;
            if (existed)
            {
                id = lt[name];
                ltr = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                ltr = new LayerTableRecord { Name = name };
                lt.UpgradeOpen();
                id = lt.Add(ltr);
                ctx.Tr.AddNewlyCreatedDBObject(ltr, true);
            }
            ApplyLayerProps(ctx, ltr, p);
            var r = LayerInfo(ctx, ltr);
            r["created"] = !existed;
            r["handles"] = new List<object> { Acad.H(id) };
            if (existed) r["note"] = "Layer already existed; the given properties were applied.";
            return r;
        }

        private static void ApplyLayerProps(ToolContext ctx, LayerTableRecord ltr, Params p)
        {
            var color = p.ColorOpt("color");
            if (color != null)
            {
                if (!(color is int)) throw ToolException.Invalid("layer color must be an ACI index 1-255.");
                ltr.Color = Acad.ToColor(color);
            }
            var lt = p.Str("linetype", null);
            if (!string.IsNullOrEmpty(lt)) ltr.LinetypeObjectId = Acad.EnsureLinetype(ctx, lt);
            if (p.Has("lineweight"))
            {
                int lw = Geometry.LineWeightFrom(p.Get("lineweight"));
                if (lw == -1 || lw == -2) throw ToolException.Invalid("layer lineweight cannot be ByLayer/ByBlock; use mm (e.g. 0.25) or \"default\".");
                ltr.LineWeight = (LineWeight)lw;
            }
            var plottable = p.BoolOpt("plottable");
            if (plottable.HasValue) ltr.IsPlottable = plottable.Value;
            var on = p.BoolOpt("on");
            if (on.HasValue) ltr.IsOff = !on.Value;
            var frozen = p.BoolOpt("frozen");
            if (frozen.HasValue)
            {
                if (frozen.Value && ctx.Db.Clayer == ltr.ObjectId)
                    throw ToolException.Invalid($"Layer '{ltr.Name}' is the current layer and cannot be frozen. Call set_current_layer to switch to another layer first.");
                ltr.IsFrozen = frozen.Value;
            }
            var locked = p.BoolOpt("locked");
            if (locked.HasValue) ltr.IsLocked = locked.Value;
            var desc = p.Str("description", null);
            if (desc != null) ltr.Description = desc;
        }

        private static Dictionary<string, object> LayerInfo(ToolContext ctx, LayerTableRecord ltr)
        {
            string lt = "";
            try { lt = ((LinetypeTableRecord)ctx.Tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead)).Name; } catch { }
            int lw = (int)ltr.LineWeight;
            return new Dictionary<string, object>
            {
                ["name"] = ltr.Name,
                ["color"] = Acad.FromColor(ltr.Color),
                ["linetype"] = lt,
                ["lineweight"] = lw < 0 ? (object)"default" : lw / 100.0,
                ["on"] = !ltr.IsOff,
                ["frozen"] = ltr.IsFrozen,
                ["locked"] = ltr.IsLocked,
                ["plottable"] = ltr.IsPlottable,
                ["current"] = ctx.Db.Clayer == ltr.ObjectId,
            };
        }

        private static object ListLayers(ToolContext ctx, Params p)
        {
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            var filter = p.Str("filter", null);
            var list = new List<object>();
            foreach (ObjectId id in lt)
            {
                var ltr = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (ltr.IsErased) continue;
                if (!string.IsNullOrEmpty(filter) && ltr.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && !WildcardMatch(ltr.Name, filter)) continue;
                list.Add(LayerInfo(ctx, ltr));
            }
            return new Dictionary<string, object>
            {
                ["count"] = list.Count,
                ["current"] = Acad.LayerName(ctx, ctx.Db.Clayer),
                ["layers"] = list,
            };
        }

        private static object SetCurrentLayer(ToolContext ctx, Params p)
        {
            var id = Acad.LayerId(ctx, p.Str("name").Trim());
            var ltr = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            if (ltr.IsFrozen) ltr.IsFrozen = false; // a frozen layer cannot be current
            ctx.Db.Clayer = id;
            return new Dictionary<string, object> { ["current"] = ltr.Name, ["handles"] = new List<object> { Acad.H(id) } };
        }

        private static object SetLayerProps(ToolContext ctx, Params p)
        {
            var name = p.Str("name").Trim();
            var id = Acad.LayerId(ctx, name);
            var ltr = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            var newName = p.Str("new_name", null);
            if (!string.IsNullOrEmpty(newName) && newName != ltr.Name)
            {
                if (ltr.Name == "0" || ltr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase))
                    throw ToolException.Invalid($"Layer '{ltr.Name}' cannot be renamed.");
                ValidateSymbolName(newName, "layer");
                var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(newName)) throw ToolException.Invalid($"A layer named '{newName}' already exists.");
                ltr.Name = newName;
            }
            ApplyLayerProps(ctx, ltr, p);
            var r = LayerInfo(ctx, ltr);
            r["handles"] = new List<object> { Acad.H(id) };
            ctx.NeedsRegen = p.Has("frozen") || p.Has("on");
            return r;
        }

        internal static void ValidateSymbolName(string name, string what)
        {
            try
            {
                SymbolUtilityServices.ValidateSymbolName(name, false);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                throw ToolException.Invalid($"'{name}' is not a valid {what} name (no < > / \\ \" : ; ? * | , = ` characters).");
            }
        }

        internal static bool WildcardMatch(string text, string pattern)
        {
            if (pattern.IndexOfAny(new[] { '*', '?' }) < 0) return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
            var rx = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(text, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
