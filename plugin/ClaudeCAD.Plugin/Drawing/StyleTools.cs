using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.4 — text and dimension styles.</summary>
    public static partial class DrawingService
    {
        private static void RegisterStyles(ToolDispatcher d)
        {
            d.Register("create_text_style", true, CreateTextStyle);
            d.Register("create_dim_style", true, CreateDimStyle);
            d.Register("set_current_styles", true, SetCurrentStyles);
            d.Register("list_styles", false, ListStyles);
        }

        internal static ObjectId TextStyleId(ToolContext ctx, string name)
        {
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            if (!tst.Has(name))
                throw ToolException.Invalid($"Text style '{name}' does not exist. Call create_text_style first, or list_styles to see existing styles.");
            return tst[name];
        }

        internal static ObjectId DimStyleId(ToolContext ctx, string name)
        {
            var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(name))
                throw ToolException.Invalid($"Dimension style '{name}' does not exist. Call create_dim_style first, or list_styles to see existing styles.");
            return dst[name];
        }

        private static object CreateTextStyle(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            ValidateSymbolName(name, "text style");
            string font = p.Str("font", "Arial").Trim();
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            bool existed = tst.Has(name);
            TextStyleTableRecord rec;
            ObjectId id;
            if (existed)
            {
                id = tst[name];
                rec = (TextStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                rec = new TextStyleTableRecord { Name = name };
                tst.UpgradeOpen();
                id = tst.Add(rec);
                ctx.Tr.AddNewlyCreatedDBObject(rec, true);
            }
            if (font.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            {
                rec.FileName = font;
                var big = p.Str("bigfont", null);
                if (!string.IsNullOrEmpty(big)) rec.BigFontFileName = big;
            }
            else
            {
                // TrueType (Unicode: correct Vietnamese diacritics)
                rec.Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(font, p.Bool("bold", false), p.Bool("italic", false), 0, 0);
            }
            rec.TextSize = p.Num("height", 0); // 0 = height asked per text
            rec.XScale = p.Positive("width_factor", 1);
            if (p.Has("oblique_angle")) rec.ObliquingAngle = Geometry.DegToRad(p.Num("oblique_angle"));
            return new Dictionary<string, object>
            {
                ["name"] = name,
                ["created"] = !existed,
                ["font"] = font,
                ["height"] = rec.TextSize,
                ["width_factor"] = rec.XScale,
                ["handles"] = new List<object> { Acad.H(id) },
            };
        }

        private static readonly Dictionary<string, string> ArrowBlocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["closed_filled"] = "", ["closedfilled"] = "", ["default"] = "",
            ["archtick"] = "_ARCHTICK", ["arch_tick"] = "_ARCHTICK", ["tick"] = "_ARCHTICK",
            ["oblique"] = "_OBLIQUE", ["dot"] = "_DOT", ["dotsmall"] = "_DOTSMALL", ["small_dot"] = "_DOTSMALL",
            ["open"] = "_OPEN", ["open30"] = "_OPEN30", ["open90"] = "_OPEN90", ["closed"] = "_CLOSED", ["closed_blank"] = "_CLOSEDBLANK",
            ["integral"] = "_INTEGRAL", ["none"] = "_NONE", ["origin"] = "_ORIGIN",
        };

        /// <summary>Gets (creating if needed) the arrow block for a DIMBLK name, the documented DIMBLK sysvar trick.</summary>
        private static ObjectId ArrowId(ToolContext ctx, string arrow)
        {
            if (!ArrowBlocks.TryGetValue(arrow.Trim(), out var blk))
                throw ToolException.Invalid($"Unknown arrow '{arrow}'. Use closed_filled, archtick (kiến trúc), oblique, dot, open, closed, integral, none.");
            if (blk == "") return ObjectId.Null;
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(blk))
            {
                string old = Convert.ToString(AcApp.GetSystemVariable("DIMBLK"));
                AcApp.SetSystemVariable("DIMBLK", blk);
                AcApp.SetSystemVariable("DIMBLK", string.IsNullOrEmpty(old) ? "." : old);
                bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            }
            return bt.Has(blk) ? bt[blk] : ObjectId.Null;
        }

        private static object CreateDimStyle(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            ValidateSymbolName(name, "dimension style");
            var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
            bool existed = dst.Has(name);
            DimStyleTableRecord rec;
            ObjectId id;
            if (existed)
            {
                id = dst[name];
                rec = (DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                rec = ctx.Db.GetDimstyleData(); // start from the current dimension variables
                rec.Name = name;
                dst.UpgradeOpen();
                id = dst.Add(rec);
                ctx.Tr.AddNewlyCreatedDBObject(rec, true);
                // metric defaults that make sense for mm drawings (overridden below by explicit params)
                rec.Dimlunit = 2;     // decimal
                rec.Dimdec = 0;       // whole mm
                rec.Dimzin = 8;       // suppress trailing zeros
                rec.Dimtad = 1;       // text above the dimension line
                rec.Dimtih = false;
                rec.Dimtoh = false;
                rec.Dimadec = 0;
            }

            double scale = p.Positive("scale", existed ? rec.Dimscale : 1);
            rec.Dimscale = scale;
            if (p.Has("text_height")) rec.Dimtxt = p.Positive("text_height");
            if (p.Has("arrow_size")) rec.Dimasz = p.Num("arrow_size");
            if (p.Has("extension_offset")) rec.Dimexo = p.Num("extension_offset");
            if (p.Has("extension_beyond")) rec.Dimexe = p.Num("extension_beyond");
            if (p.Has("text_gap")) rec.Dimgap = p.Num("text_gap");
            if (p.Has("dim_line_extension")) rec.Dimdle = p.Num("dim_line_extension");
            if (p.Has("baseline_spacing")) rec.Dimdli = p.Num("baseline_spacing");
            if (p.Has("decimals")) rec.Dimdec = p.Int("decimals", 0, 0, 8);
            if (p.Has("linear_scale")) rec.Dimlfac = p.Positive("linear_scale");
            if (p.Has("fixed_extension_length"))
            {
                rec.DimfxlenOn = true;
                rec.Dimfxlen = p.Positive("fixed_extension_length");
            }
            var textStyle = p.Str("text_style", null);
            if (!string.IsNullOrEmpty(textStyle)) rec.Dimtxsty = TextStyleId(ctx, textStyle);
            var arrow = p.Str("arrow", null);
            if (!string.IsNullOrEmpty(arrow))
            {
                var aid = ArrowId(ctx, arrow);
                rec.Dimblk = aid;
                rec.Dimblk1 = aid;
                rec.Dimblk2 = aid;
                rec.Dimsah = false;
                if (arrow.IndexOf("tick", StringComparison.OrdinalIgnoreCase) >= 0 || arrow.Equals("oblique", StringComparison.OrdinalIgnoreCase))
                    rec.Dimtsz = 0; // use the block, not the legacy tick size
            }
            var tpos = p.Str("text_position", null);
            if (tpos != null)
            {
                switch (tpos.ToLowerInvariant())
                {
                    case "above": rec.Dimtad = 1; break;
                    case "centered": case "center": rec.Dimtad = 0; break;
                    case "outside": rec.Dimtad = 2; break;
                    default: throw ToolException.Invalid("text_position must be above, centered or outside.");
                }
            }
            var c = p.ColorOpt("color");
            if (c != null) { var col = Acad.ToColor(c); rec.Dimclrd = col; rec.Dimclre = col; rec.Dimclrt = col; }

            return new Dictionary<string, object>
            {
                ["name"] = name,
                ["created"] = !existed,
                ["scale"] = Acad.R(rec.Dimscale),
                ["text_height"] = Acad.R(rec.Dimtxt),
                ["arrow_size"] = Acad.R(rec.Dimasz),
                ["printed_text_height_mm"] = Acad.R(rec.Dimtxt),
                ["model_text_height_mm"] = Acad.R(rec.Dimtxt * rec.Dimscale),
                ["handles"] = new List<object> { Acad.H(id) },
                ["note"] = "Sizes (text_height, arrow_size, offsets) are paper sizes in mm; AutoCAD multiplies them by 'scale' (DIMSCALE) in model space.",
            };
        }

        private static object SetCurrentStyles(ToolContext ctx, Params p)
        {
            var r = new Dictionary<string, object>();
            var ts = p.Str("text_style", null);
            if (!string.IsNullOrEmpty(ts))
            {
                ctx.Db.Textstyle = TextStyleId(ctx, ts);
                r["text_style"] = ts;
            }
            var ds = p.Str("dim_style", null);
            if (!string.IsNullOrEmpty(ds))
            {
                var id = DimStyleId(ctx, ds);
                var rec = (DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                ctx.Db.Dimstyle = id;
                ctx.Db.SetDimstyleData(rec);
                r["dim_style"] = ds;
            }
            var ms = p.Str("mleader_style", null);
            if (!string.IsNullOrEmpty(ms))
            {
                var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                if (!dict.Contains(ms)) throw ToolException.Invalid($"Multileader style '{ms}' does not exist.");
                ctx.Db.MLeaderstyle = dict.GetAt(ms);
                r["mleader_style"] = ms;
            }
            if (r.Count == 0) throw ToolException.Invalid("set_current_styles: give text_style and/or dim_style (or mleader_style).");
            return r;
        }

        private static object ListStyles(ToolContext ctx, Params p)
        {
            var text = new List<object>();
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            foreach (ObjectId id in tst)
            {
                var r = (TextStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (r.IsShapeFile || string.IsNullOrEmpty(r.Name)) continue;
                text.Add(new Dictionary<string, object>
                {
                    ["name"] = r.Name,
                    ["font"] = string.IsNullOrEmpty(r.FileName) ? r.Font.TypeFace : r.FileName,
                    ["height"] = Acad.R(r.TextSize),
                    ["width_factor"] = Acad.R(r.XScale),
                    ["current"] = ctx.Db.Textstyle == id,
                });
            }
            var dims = new List<object>();
            var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
            foreach (ObjectId id in dst)
            {
                var r = (DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                dims.Add(new Dictionary<string, object>
                {
                    ["name"] = r.Name,
                    ["text_height"] = Acad.R(r.Dimtxt),
                    ["arrow_size"] = Acad.R(r.Dimasz),
                    ["scale"] = Acad.R(r.Dimscale),
                    ["text_style"] = EntityInfo.StyleName(ctx, r.Dimtxsty),
                    ["current"] = ctx.Db.Dimstyle == id,
                });
            }
            var mls = new List<object>();
            var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict) mls.Add(e.Key);
            return new Dictionary<string, object> { ["text_styles"] = text, ["dim_styles"] = dims, ["mleader_styles"] = mls };
        }
    }
}
