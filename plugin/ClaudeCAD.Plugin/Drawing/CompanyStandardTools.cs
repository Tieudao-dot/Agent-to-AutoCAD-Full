using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H4: company_standard — "học chuẩn công ty từ bản vẽ mẫu". The template (.dwg / .dwt / .dxf) is read
    /// through OtherDrawing (H3: never locked, never saved); what is learnt goes to %LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty\
    /// &lt;name&gt;\ (standard.json + a copy of the template, so title blocks and styles stay usable when the original is moved or
    /// changed). setup_standards / check_drawing / create_sheet take <c>company</c> to use it.
    /// </summary>
    public static partial class DrawingService
    {
        /// <summary>Where the standards are kept (a test may point it elsewhere).</summary>
        internal static string CompanyRoot = CompanyStandardLogic.DefaultRoot();

        private static void RegisterCompanyStandard(ToolDispatcher d)
        {
            // the drawing is never changed (its own files only): allowed in "Chỉ xem", never recorded in a macro
            d.Register(new ToolDef("company_standard", false, CompanyStandardTool, noTransaction: true));
        }

        internal static object CompanyStandardTool(ToolContext ctx, Params p)
        {
            const string tool = CompanyStandardLogic.Tool;
            var action = p.Str("action", "list").Trim().ToLowerInvariant();
            if (!CompanyStandardLogic.Actions.Contains(action))
                throw ToolException.Invalid($"{tool}: action must be one of {string.Join(", ", CompanyStandardLogic.Actions)}.");
            var root = CompanyRoot;
            switch (action)
            {
                case "list":
                {
                    var damaged = new List<string>();
                    var all = CompanyStandardLogic.List(root, damaged);
                    var res = new Dictionary<string, object>
                    {
                        ["standards"] = all.Select(s => (object)new Dictionary<string, object>
                        {
                            ["name"] = s.Name, ["source"] = s.Source, ["learned_at"] = s.LearnedAt, ["layers"] = s.Layers.Count,
                            ["title_blocks"] = s.TitleBlocks.Count, ["template_kept"] = s.TemplateKept,
                        }).ToList(),
                        ["folder"] = root,
                        ["note"] = all.Count == 0 ? "No company standard on this computer yet: learn one from the company's template file (action learn)." : "Use one with setup_standards / check_drawing / create_sheet company: <name>.",
                    };
                    // Test & sửa H4: a damaged one is named (learn it again or delete it), not hidden
                    if (damaged.Count > 0) res["damaged"] = damaged.Cast<object>().ToList();
                    return res;
                }
                case "show":
                {
                    var s = CompanyStandardLogic.Require(root, p.Str("name"), tool);
                    var r = CompanyStandardLogic.Summary(s, true);
                    if (s.TemplateKept && !File.Exists(CompanyStandardLogic.TemplatePath(root, s.Name)))
                        r["warning"] = "The copy of the template is missing: title blocks cannot be copied; layers and text styles still can.";
                    return r;
                }
                case "delete":
                {
                    // Test & sửa H4: a damaged standard (its standard.json unreadable) can still be deleted
                    string dname = CompanyStandardLogic.ValidName(p.Str("name"), tool);
                    string shown = CompanyStandardLogic.IsDamaged(root, dname) ? dname : CompanyStandardLogic.Require(root, dname, tool).Name;
                    RequireCard(ctx, p, tool, $"delete the company standard '{shown}' (its saved copy of the template too)", new[] { CompanyStandardLogic.Token("delete", shown) });
                    CompanyStandardLogic.Delete(root, shown);
                    return new Dictionary<string, object> { ["deleted"] = shown, ["note"] = "The template file itself was not touched." };
                }
                default:
                    return LearnStandard(ctx, p, root, tool);
            }
        }

        private static object LearnStandard(ToolContext ctx, Params p, string root, string tool)
        {
            var name = CompanyStandardLogic.ValidName(p.Str("name"), tool);
            var room = CompanyStandardLogic.RoomRefusal(root, name);
            if (room != null) throw ToolException.Invalid(room);
            bool damagedBefore = CompanyStandardLogic.IsDamaged(root, name);
            var before = damagedBefore ? null : CompanyStandardLogic.Load(root, name);
            if (before != null || damagedBefore)
                RequireCard(ctx, p, tool, before != null ? $"replace the company standard '{before.Name}' learnt before from {before.Source}" : $"replace the damaged company standard '{name}'",
                    new[] { CompanyStandardLogic.Token("replace", name) });

            var s = new CompanyStandard { Name = name, LearnedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
            Dictionary<string, object> info;
            using (var od = OtherDrawing.Open(ctx, p.Str("file"), tool))
            {
                s.Source = od.Path;
                s.MmPerUnit = od.MmPerUnit;
                try { s.Insunits = Convert.ToInt32(od.Db.Insunits); } catch (Exception) { }
                info = od.Info();
                using (var tr = od.Db.TransactionManager.StartTransaction())
                {
                    ReadTemplate(tr, od.Db, s);
                    tr.Commit();
                }
                // the copy of the template (its blocks and styles are cloned from it later); the original is only read
                var dir = CompanyStandardLogic.FolderOf(root, name);
                Directory.CreateDirectory(dir);
                var copy = CompanyStandardLogic.TemplatePath(root, name);
                long bytes = new FileInfo(od.Path).Length;
                if (bytes > (long)H4Numbers.MaxTemplateMb * 1024 * 1024)
                {
                    s.Notes.Add($"The template is {bytes / 1048576.0:0.#} MB (more than {H4Numbers.MaxTemplateMb} MB): no copy kept, so title blocks are not copied into sheets; layers and text styles still apply.");
                    // Test & sửa H4: the copy of a template learnt before under this name is not this template
                    try { if (File.Exists(copy)) File.Delete(copy); } catch (Exception) { }
                }
                else
                {
                    try
                    {
                        var tmp = copy + ".tmp";
                        if (od.Kind == "dxf") od.Db.SaveAs(tmp, DwgVersion.Current); // the side database, never the file
                        else CopyShared(od.Path, tmp); // Test & sửa H4: a template open in AutoCAD elsewhere is still copied
                        if (File.Exists(copy)) File.Delete(copy);
                        File.Move(tmp, copy);
                        s.TemplateKept = true;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Autodesk.AutoCAD.Runtime.Exception)
                    {
                        s.Notes.Add("The template could not be copied (" + ex.Message + "): title blocks are not copied into sheets.");
                        try { if (File.Exists(copy)) File.Delete(copy); } catch (Exception) { }
                    }
                }
            }
            CompanyStandardLogic.Save(root, s);
            var r = CompanyStandardLogic.Summary(s, false);
            r["template"] = info;
            r["replaced"] = before != null || damagedBefore;
            r["folder"] = CompanyStandardLogic.FolderOf(root, name);
            r["next"] = $"Tell the user what was learnt (counts, title blocks, paper). Use it with setup_standards / check_drawing / create_sheet company: \"{name}\". The template file was not changed.";
            return r;
        }

        /// <summary>
        /// Test & sửa H4: copies a file that another program may hold open for writing (AutoCAD keeps the drawing it edits open):
        /// File.Copy asks to deny other writers and fails then; this reads it sharing read / write / delete, like OtherDrawing.
        /// </summary>
        internal static void CopyShared(string from, string to)
        {
            using (var src = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var dst = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None))
                src.CopyTo(dst);
        }

        /// <summary>Layers, styles, layouts (paper, title block, viewport), units and the most used text heights of a template.</summary>
        private static void ReadTemplate(Transaction tr, Database db, CompanyStandard s)
        {
            string NameOfId(ObjectId id)
            {
                try { return id.IsNull ? "" : ((SymbolTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name; } catch (Exception) { return ""; }
            }

            var layers = new List<CompanyStandard.Layer>();
            foreach (ObjectId id in (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead))
            {
                var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (l.IsDependent || l.IsErased) continue; // an xref's layer belongs to the other drawing
                var c = l.Color;
                int lw = (int)l.LineWeight;
                layers.Add(new CompanyStandard.Layer
                {
                    Name = l.Name,
                    Color = CompanyStandardLogic.ColorText(c.IsByAci, c.IsByAci ? c.ColorIndex : 0, c.Red, c.Green, c.Blue),
                    Linetype = NameOfId(l.LinetypeObjectId),
                    Lineweight = lw >= 0 ? lw / 100.0 : -1,
                    Description = l.Description ?? "",
                    Plottable = l.IsPlottable,
                });
            }
            s.Layers = CompanyStandardLogic.Cap(layers, H4Numbers.MaxLayers, "layers", s.Notes);

            var texts = new List<CompanyStandard.TextStyle>();
            var heights = new List<double>();
            foreach (ObjectId id in (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead))
            {
                var t = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (t.IsDependent || t.IsShapeFile || string.IsNullOrEmpty(t.Name)) continue;
                texts.Add(new CompanyStandard.TextStyle
                {
                    Name = t.Name,
                    Font = !string.IsNullOrEmpty(t.Font.TypeFace) ? t.Font.TypeFace : t.FileName,
                    BigFont = t.BigFontFileName ?? "",
                    Height = t.TextSize,
                    WidthFactor = t.XScale,
                    Oblique = t.ObliquingAngle * 180 / Math.PI,
                });
                if (t.TextSize > 0) heights.Add(t.TextSize);
            }
            s.TextStyles = CompanyStandardLogic.Cap(texts, H4Numbers.MaxStyles, "text styles", s.Notes);

            var dims = new List<CompanyStandard.DimStyle>();
            foreach (ObjectId id in (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead))
            {
                var d = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (d.IsDependent || d.Name.Contains("$")) continue; // child styles (ISO-25$0) follow their parent
                dims.Add(new CompanyStandard.DimStyle { Name = d.Name, TextStyle = NameOfId(d.Dimtxsty), TextHeight = d.Dimtxt, ArrowSize = d.Dimasz, Scale = d.Dimscale });
            }
            s.DimStyles = CompanyStandardLogic.Cap(dims, H4Numbers.MaxStyles, "dimension styles", s.Notes);

            var mls = new List<CompanyStandard.MLeaderStyle>();
            var mld = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry de in mld)
            {
                if (!(tr.GetObject(de.Value, OpenMode.ForRead) is Autodesk.AutoCAD.DatabaseServices.MLeaderStyle m)) continue;
                mls.Add(new CompanyStandard.MLeaderStyle { Name = de.Key, TextStyle = NameOfId(m.TextStyleId), TextHeight = m.TextHeight, ArrowSize = m.ArrowSize, Scale = m.Scale });
            }
            s.MLeaderStyles = CompanyStandardLogic.Cap(mls, H4Numbers.MaxStyles, "multileader styles", s.Notes);

            s.CurrentTextStyle = NameOfId(db.Textstyle);
            s.CurrentDimStyle = NameOfId(db.Dimstyle);
            try { s.CurrentMLeaderStyle = mls.FirstOrDefault(m => mld.Contains(m.Name) && mld.GetAt(m.Name) == db.MLeaderstyle)?.Name ?? ""; } catch (Exception) { }

            // the text heights the template really uses (model space), with the fixed heights of its styles
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            int n = 0;
            foreach (ObjectId id in ms)
            {
                if (++n > G2Numbers.MaxScan) break;
                var e = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (e is DBText dt) heights.Add(dt.Height);
                else if (e is MText mt) heights.Add(mt.TextHeight);
            }
            s.TextHeights = CompanyStandardLogic.TopHeights(heights);

            // layouts: paper, the main viewport, title blocks (a block with attributes on the sheet)
            var lod = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry de in lod)
            {
                var lo = (Layout)tr.GetObject(de.Value, OpenMode.ForRead);
                if (lo.ModelType) continue;
                double k = lo.PlotPaperUnits == PlotPaperUnit.Inches ? 25.4 : 1;
                double w = lo.PlotPaperSize.X * k, h = lo.PlotPaperSize.Y * k;
                if (lo.PlotRotation == PlotRotation.Degrees090 || lo.PlotRotation == PlotRotation.Degrees270) { var t = w; w = h; h = t; }
                var (pn, land) = CompanyStandardLogic.PaperOf(w, h);
                var paper = new CompanyStandard.Paper { Layout = lo.LayoutName, Media = lo.CanonicalMediaName ?? "", Name = pn, W = Math.Round(w, 3), H = Math.Round(h, 3), Landscape = land };
                var space = (BlockTableRecord)tr.GetObject(lo.BlockTableRecordId, OpenMode.ForRead);
                bool first = true;
                double best = 0;
                foreach (ObjectId id in space)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (e is Viewport vp)
                    {
                        if (first) { first = false; continue; } // the paper-space viewport of the layout itself
                        if (vp.Width * vp.Height > best) { best = vp.Width * vp.Height; paper.Viewport = new[] { vp.CenterPoint.X, vp.CenterPoint.Y, vp.Width, vp.Height }; }
                    }
                    else if (e is BlockReference br && s.TitleBlocks.Count < H4Numbers.MaxTitleBlocks)
                    {
                        var def = (BlockTableRecord)tr.GetObject(br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord, OpenMode.ForRead);
                        if (!def.HasAttributeDefinitions || def.IsFromExternalReference || def.IsLayout) continue;
                        var tags = new List<string>();
                        foreach (ObjectId aid in def)
                            if (tr.GetObject(aid, OpenMode.ForRead) is AttributeDefinition ad && !ad.Constant) tags.Add(ad.Tag);
                        if (s.TitleBlocks.Any(t => t.Block == def.Name && t.Layout == lo.LayoutName)) continue;
                        s.TitleBlocks.Add(new CompanyStandard.TitleBlock
                        {
                            Block = def.Name, Layout = lo.LayoutName, Tags = tags,
                            X = br.Position.X, Y = br.Position.Y, Rotation = br.Rotation * 180 / Math.PI, Scale = br.ScaleFactors.X,
                        });
                    }
                }
                if (s.Papers.Count < H4Numbers.MaxTitleBlocks) s.Papers.Add(paper);
            }
        }

        // ------------------------------------------------------------ the copy of the template

        /// <summary>The template copy of a standard opened to read (null when it is gone: the caller says what cannot be done).</summary>
        private static OtherDrawing OpenTemplateCopy(ToolContext ctx, CompanyStandard s, string tool)
        {
            var copy = CompanyStandardLogic.TemplatePath(CompanyRoot, s.Name);
            if (!s.TemplateKept || !File.Exists(copy)) return null;
            return OtherDrawing.Open(ctx, copy, tool);
        }

        /// <summary>Clones records of the template copy into the drawing (owner = the drawing's table / dictionary).</summary>
        private static void CloneFrom(OtherDrawing od, ToolContext ctx, IEnumerable<ObjectId> ids, ObjectId owner, bool replace)
        {
            var col = new ObjectIdCollection();
            foreach (var id in ids) col.Add(id);
            if (col.Count == 0) return;
            var map = new IdMapping();
            od.Db.WblockCloneObjects(col, owner, map, replace ? DuplicateRecordCloning.Replace : DuplicateRecordCloning.Ignore, false);
        }

        // ------------------------------------------------------------ setup_standards company: <name>

        private static object SetupCompanyStandard(ToolContext ctx, Params p)
        {
            const string tool = "setup_standards";
            var s = CompanyStandardLogic.Require(CompanyRoot, p.Str("company"), tool);
            var overwrite = ApprovedOverwrites(p, tool, $"overwrite layers / styles that differ from the company standard '{s.Name}'");
            var result = new Dictionary<string, object> { ["standard"] = s.Name, ["company"] = true };
            var notes = new List<object>();
            var kept = new List<object>();
            var keptWhat = new List<object>();
            int created = 0, updated = 0, same = 0;

            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
            var mld = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);

            // what the drawing has, and what differs (an approved handle is replaced)
            var missingLayers = new List<string>();
            var replaceLayers = new List<string>();
            foreach (var l in s.Layers)
            {
                if (!lt.Has(l.Name)) { missingLayers.Add(l.Name); continue; }
                var have = (LayerTableRecord)ctx.Tr.GetObject(lt[l.Name], OpenMode.ForRead);
                var c = have.Color;
                int lw = (int)have.LineWeight;
                var diffs = CompanyStandardLogic.LayerDifferences(l, new CompanyStandard.Layer
                {
                    Color = CompanyStandardLogic.ColorText(c.IsByAci, c.IsByAci ? c.ColorIndex : 0, c.Red, c.Green, c.Blue),
                    Linetype = EntityInfo.StyleName(ctx, have.LinetypeObjectId), Lineweight = lw >= 0 ? lw / 100.0 : -1, Plottable = have.IsPlottable,
                });
                if (diffs.Count == 0) { same++; continue; }
                string h = Acad.H(lt[l.Name]);
                if (overwrite.Contains(h)) replaceLayers.Add(l.Name);
                else { kept.Add(h); keptWhat.Add($"layer {l.Name}: {string.Join(", ", diffs)}"); }
            }
            var missingText = s.TextStyles.Where(t => !tst.Has(t.Name)).Select(t => t.Name).ToList();
            var missingDim = s.DimStyles.Where(t => !dst.Has(t.Name)).Select(t => t.Name).ToList();
            var missingMl = s.MLeaderStyles.Where(t => !mld.Contains(t.Name)).Select(t => t.Name).ToList();
            same += s.TextStyles.Count - missingText.Count + s.DimStyles.Count - missingDim.Count + s.MLeaderStyles.Count - missingMl.Count;

            using (var od = OpenTemplateCopy(ctx, s, tool))
            {
                if (od != null)
                {
                    using (var tr = od.Db.TransactionManager.StartTransaction())
                    {
                        var slt = (LayerTable)tr.GetObject(od.Db.LayerTableId, OpenMode.ForRead);
                        var stst = (TextStyleTable)tr.GetObject(od.Db.TextStyleTableId, OpenMode.ForRead);
                        var sdst = (DimStyleTable)tr.GetObject(od.Db.DimStyleTableId, OpenMode.ForRead);
                        var smld = (DBDictionary)tr.GetObject(od.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                        // text styles first (dimension / multileader styles use them), then layers, dims, multileaders
                        CloneFrom(od, ctx, missingText.Where(stst.Has).Select(n => stst[n]), ctx.Db.TextStyleTableId, false);
                        CloneFrom(od, ctx, missingLayers.Where(slt.Has).Select(n => slt[n]), ctx.Db.LayerTableId, false);
                        // Test & sửa H4: a layer that differs gets the standard's properties set on it (not replaced by a clone:
                        // replacing layer 0, the current layer or a layer in use is refused or loses its objects' link);
                        // only a linetype the drawing lacks comes from the template
                        var dlt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
                        var sltt = (LinetypeTable)tr.GetObject(od.Db.LinetypeTableId, OpenMode.ForRead);
                        CloneFrom(od, ctx, replaceLayers.Select(n => s.Layers.First(x => x.Name == n).Linetype).Distinct(StringComparer.OrdinalIgnoreCase)
                            .Where(n => !string.IsNullOrEmpty(n) && !dlt.Has(n) && sltt.Has(n)).Select(n => sltt[n]), ctx.Db.LinetypeTableId, false);
                        CloneFrom(od, ctx, missingDim.Where(sdst.Has).Select(n => sdst[n]), ctx.Db.DimStyleTableId, false);
                        CloneFrom(od, ctx, missingMl.Where(smld.Contains).Select(n => smld.GetAt(n)), ctx.Db.MLeaderStyleDictionaryId, false);
                        tr.Commit();
                    }
                    foreach (var name in replaceLayers) ApplyLayer(ctx, s.Layers.First(x => x.Name == name), notes);
                    created = missingLayers.Count + missingText.Count + missingDim.Count + missingMl.Count;
                    updated = replaceLayers.Count;
                }
                else
                {
                    // the copy of the template is gone: layers and text styles from what was learnt; the rest is said
                    foreach (var name in missingLayers.Concat(replaceLayers)) ApplyLayer(ctx, s.Layers.First(x => x.Name == name), notes);
                    foreach (var name in missingText)
                    {
                        var t = s.TextStyles.First(x => x.Name == name);
                        CreateTextStyle(ctx, new Params("create_text_style", new Dictionary<string, object>
                        {
                            ["name"] = t.Name, ["font"] = string.IsNullOrEmpty(t.Font) ? F2Numbers.Font : StandardsLogic.FontKey(t.Font), ["height"] = t.Height, ["width_factor"] = t.WidthFactor,
                        }));
                    }
                    created = missingLayers.Count + missingText.Count;
                    updated = replaceLayers.Count;
                    if (missingDim.Count + missingMl.Count > 0)
                        notes.Add($"The copy of the template is missing: {missingDim.Count + missingMl.Count} dimension / multileader styles were not made ({string.Join(", ", missingDim.Concat(missingMl).Take(10))}). Learn the standard again from the template file.");
                }
            }

            // Test & sửa H4: a template in other units (metres…) has dimension / multileader styles sized for them
            double dmm = MmPerUnitOf(ctx, out _);
            if (Math.Abs(s.MmPerUnit - dmm) > 1e-9 && s.DimStyles.Count + s.MLeaderStyles.Count > 0)
                notes.Add($"The template is in other units ({s.MmPerUnit} mm per unit, this drawing {dmm}): its dimension / multileader styles are sized for those units (check text and arrow sizes, or set DIMSCALE); text heights were converted.");

            // current styles: those current in the template, when the drawing now has them
            if (p.Bool("make_current", true))
            {
                tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
                dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
                mld = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                if (s.CurrentTextStyle.Length > 0 && tst.Has(s.CurrentTextStyle)) ctx.Db.Textstyle = tst[s.CurrentTextStyle];
                if (s.CurrentDimStyle.Length > 0 && dst.Has(s.CurrentDimStyle))
                {
                    ctx.Db.Dimstyle = dst[s.CurrentDimStyle];
                    ctx.Db.SetDimstyleData((DimStyleTableRecord)ctx.Tr.GetObject(dst[s.CurrentDimStyle], OpenMode.ForRead));
                }
                if (s.CurrentMLeaderStyle.Length > 0 && mld.Contains(s.CurrentMLeaderStyle)) ctx.Db.MLeaderstyle = mld.GetAt(s.CurrentMLeaderStyle);
            }

            result["created"] = created;
            result["updated"] = updated;
            result["unchanged"] = same;
            result["current"] = new Dictionary<string, object> { ["text_style"] = s.CurrentTextStyle, ["dim_style"] = s.CurrentDimStyle, ["mleader_style"] = s.CurrentMLeaderStyle };
            if (s.TextHeights.Count > 0) result["text_heights"] = (CompanyStandardLogic.Heights(s, MmPerUnitOf(ctx, out _)) ?? new double[0]).Select(v => (object)Acad.R(v)).ToList();
            if (notes.Count > 0) result["notes"] = notes;
            if (kept.Count > 0)
            {
                result["kept_handles"] = kept;
                result["kept"] = keptWhat.Take(30).ToList();
                result["next"] = "Layers of the drawing that differ from the company standard were kept. Ask the user: request_confirmation with kept_handles and one Vietnamese sentence naming them, " +
                    "end the turn; after Đồng ý call setup_standards again with the same company and handles = kept_handles.";
            }
            ctx.NeedsRegen = true;
            return result;
        }

        /// <summary>The layer of the drawing (made when missing) gets the standard's colour, linetype, lineweight, plot and description.</summary>
        private static void ApplyLayer(ToolContext ctx, CompanyStandard.Layer l, List<object> notes)
        {
            var id = EnsureLayerF2(ctx, l.Name, 7, l.Plottable);
            var rec = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForWrite);
            rec.Color = ColorOfText(l.Color);
            try
            {
                var ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
                if (!ltt.Has(l.Linetype)) Acad.EnsureLinetype(ctx, l.Linetype);
                ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
                rec.LinetypeObjectId = ltt[l.Linetype];
            }
            catch (Exception) { notes.Add($"layer {l.Name}: linetype {l.Linetype} not found, its linetype was kept."); }
            rec.LineWeight = l.Lineweight >= 0 ? (LineWeight)Geometry.LineWeightFrom(l.Lineweight) : LineWeight.ByLineWeightDefault;
            rec.IsPlottable = l.Plottable;
            if (l.Description.Length > 0) rec.Description = l.Description;
        }

        private static Autodesk.AutoCAD.Colors.Color ColorOfText(string c)
        {
            var parts = (c ?? "7").Split(',');
            if (parts.Length == 3 && parts.All(x => byte.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                return Autodesk.AutoCAD.Colors.Color.FromRgb(byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture));
            short i = short.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 1 && v <= 255 ? v : (short)7;
            return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, i);
        }

        // ------------------------------------------------------------ create_sheet company: <name>

        /// <summary>
        /// The company title block of a sheet: its definition cloned from the template copy (an existing one of that name is
        /// used as is), inserted where it was on the template's layout. Null when the standard has no title block, or the copy
        /// of the template is gone (the reason in <paramref name="why"/>).
        /// </summary>
        private static ObjectId? CompanyTitleBlockDef(ToolContext ctx, CompanyStandard s, CompanyStandard.TitleBlock tb, out string why)
        {
            why = null;
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(tb.Block)) return bt[tb.Block];
            using (var od = OpenTemplateCopy(ctx, s, "create_sheet"))
            {
                if (od == null) { why = $"the copy of the template of '{s.Name}' is gone, so its title block {tb.Block} cannot be copied (learn the standard again)"; return null; }
                using (var tr = od.Db.TransactionManager.StartTransaction())
                {
                    var sbt = (BlockTable)tr.GetObject(od.Db.BlockTableId, OpenMode.ForRead);
                    if (!sbt.Has(tb.Block)) { why = $"the template copy has no block {tb.Block}"; tr.Commit(); return null; }
                    CloneFrom(od, ctx, new[] { sbt[tb.Block] }, ctx.Db.BlockTableId, false);
                    tr.Commit();
                }
            }
            bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            return bt.Has(tb.Block) ? bt[tb.Block] : (ObjectId?)null;
        }
    }
}
