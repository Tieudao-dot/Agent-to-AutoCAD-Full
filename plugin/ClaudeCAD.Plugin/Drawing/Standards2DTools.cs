using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt F2 (25/11/2026): the 2D tools used every day. setup_standards and create_sheet are ToolGroup.TwoD
    /// (text / dimensions / layouts), insert_library_block and layer_state are Common like the block and layer tools
    /// (Core/Plans.cs). Numbers and geometry: Core/Standards2D.cs. Overwriting what exists (a style that differs, a layer
    /// state of the same name, deleting a layer state) needs the Đồng ý button of a card listing its handles
    /// (user_confirmed, like find_replace_text).
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterStandards2D(ToolDispatcher d)
        {
            d.Register("setup_standards", true, SetupStandards);
            d.Register(new ToolDef("create_sheet", true, CreateSheet, noTransaction: true));
            d.Register(new ToolDef("insert_library_block", true, InsertLibraryBlock, readsWhen: p => !p.Has("name")));
            d.Register(new ToolDef("layer_state", true, LayerState, readsWhen: LayerStateReads));
        }

        /// <summary>F2-test (point 6): "list", "liệt kê" or no action only read (the same normalisation as the handler).</summary>
        private static bool LayerStateReads(Params p)
        {
            try { return LayerStateLogic.NormalizeAction(p.Str("action", "list")) == "list"; }
            catch (ToolException) { return false; } // a bad action: the handler refuses it
        }

        /// <summary>
        /// F2-test (point 2): SetDatabaseDefaults copies the current color / linetype / lineweight (CECOLOR…); the objects
        /// the F2 tools build take them from their layer (or block reference) instead.
        /// </summary>
        private static void ByLayer(Entity e)
        {
            e.ColorIndex = 256;
            e.Linetype = "ByLayer";
            e.LineWeight = LineWeight.ByLayer;
        }

        // ------------------------------------------------------------------ shared helpers

        private static double MmPerUnitOf(ToolContext ctx, out string unitName) =>
            FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out unitName);

        /// <summary>The handles of the call that the user approved on the card; refuses the call when some are not.</summary>
        private static HashSet<string> ApprovedOverwrites(Params p, string tool, string what)
        {
            if (!p.Has("handles")) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var asked = p.Handles("handles", 50).Select(h => h.Trim().ToUpperInvariant()).Distinct().ToList();
            var approved = SolidLogic.ConfirmedHandles(p.Get(SolidLogic.UserConfirmedParam));
            var not = Util2DLogic.NotApproved(asked, approved);
            if (not.Count > 0) throw new ToolException(ErrorCodes.InvalidParams, F2Confirm.CardMessage(tool, what, not));
            return new HashSet<string>(asked, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Creates a layer when missing (never changes an existing one). Returns its id.</summary>
        private static ObjectId EnsureLayerF2(ToolContext ctx, string name, int color, bool plottable = true)
        {
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name) && !lt[name].IsErased) return lt[name];
            ValidateSymbolName(name, "layer");
            var rec = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)color),
                IsPlottable = plottable,
            };
            lt.UpgradeOpen();
            var id = lt.Add(rec);
            ctx.Tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static ObjectId EnsureTextStyleF2(ToolContext ctx, string name)
        {
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(name)) return tst[name];
            CreateTextStyle(ctx, new Params("create_text_style", new Dictionary<string, object>
            {
                ["name"] = name, ["font"] = F2Numbers.Font, ["width_factor"] = F2Numbers.WidthFactor,
            }));
            return TextStyleId(ctx, name);
        }

        // ------------------------------------------------------------------ setup_standards

        private static Dictionary<string, object> ReadTextStyle(ToolContext ctx, string name, out ObjectId id)
        {
            id = ObjectId.Null;
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            if (!tst.Has(name)) return null;
            id = tst[name];
            var r = (TextStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
            return new Dictionary<string, object>
            {
                // F2-test: a TrueType style made by STYLE keeps both "arial.ttf" and "Arial": compare the type face then
                ["font"] = StandardsLogic.FontKey(!string.IsNullOrEmpty(r.Font.TypeFace) ? r.Font.TypeFace : r.FileName),
                ["height"] = r.TextSize,
                ["width_factor"] = r.XScale,
            };
        }

        private static string ArrowNameOf(ToolContext ctx, ObjectId blk)
        {
            if (blk.IsNull) return "closed_filled";
            var btr = (BlockTableRecord)ctx.Tr.GetObject(blk, OpenMode.ForRead);
            return btr.Name.TrimStart('_').ToLowerInvariant();
        }

        private static Dictionary<string, object> ReadDimStyle(ToolContext ctx, string name, out ObjectId id)
        {
            id = ObjectId.Null;
            var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(name)) return null;
            id = dst[name];
            var r = (DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
            return new Dictionary<string, object>
            {
                ["scale"] = r.Dimscale,
                ["text_height"] = r.Dimtxt,
                ["arrow"] = ArrowNameOf(ctx, r.Dimblk),
                ["arrow_size"] = r.Dimasz,
                ["extension_offset"] = r.Dimexo,
                ["extension_beyond"] = r.Dimexe,
                ["dim_line_extension"] = r.Dimdle,
                ["text_gap"] = r.Dimgap,
                ["decimals"] = (double)r.Dimdec,
                ["text_style"] = EntityInfo.StyleName(ctx, r.Dimtxsty),
                ["text_position"] = r.Dimtad == 1 ? "above" : r.Dimtad == 0 ? "centered" : "outside",
            };
        }

        /// <summary>Creates a missing style, leaves an equal one, updates an approved one, keeps (reports) one that differs.</summary>
        private static Dictionary<string, object> ApplyStyle(ToolContext ctx, Dictionary<string, object> wanted, Dictionary<string, object> existing,
            ObjectId existingId, HashSet<string> overwrite, Func<ToolContext, Params, object> create, string tool)
        {
            string name = (string)wanted["name"];
            var r = new Dictionary<string, object> { ["name"] = name };
            if (existing == null)
            {
                var made = (Dictionary<string, object>)create(ctx, new Params(tool, wanted));
                r["status"] = "created";
                r["handle"] = ((List<object>)made["handles"])[0];
                return r;
            }
            string h = Acad.H(existingId);
            r["handle"] = h;
            var diffs = StandardsLogic.Differences(wanted, existing);
            if (diffs.Count == 0) { r["status"] = "unchanged"; return r; }
            if (overwrite.Contains(h))
            {
                create(ctx, new Params(tool, wanted));
                r["status"] = "updated";
                r["changed"] = diffs;
                return r;
            }
            r["status"] = "kept";
            r["differences"] = diffs;
            return r;
        }

        internal static object SetupStandards(ToolContext ctx, Params p)
        {
            // nâng cấp đợt H4: the company standard learnt from a template, instead of the Vietnamese one
            if (p.Has("company")) return SetupCompanyStandard(ctx, p);
            string standard = p.Str("standard", "");
            int n = StandardsLogic.ScaleDenominator(p.Get("scale"));
            double f = MmPerUnitOf(ctx, out var unitName);
            string prefix = StandardsLogic.Prefix(standard);
            var overwrite = ApprovedOverwrites(p, "setup_standards", "overwrite existing styles that differ");

            var tsWanted = StandardsLogic.TextStyleParams(prefix);
            var ts = ApplyStyle(ctx, tsWanted, ReadTextStyle(ctx, (string)tsWanted["name"], out var tsId), tsId, overwrite, CreateTextStyle, "create_text_style");
            var dsWanted = StandardsLogic.DimStyleParams(prefix, n, f);
            var ds = ApplyStyle(ctx, dsWanted, ReadDimStyle(ctx, (string)dsWanted["name"], out var dsId), dsId, overwrite, CreateDimStyle, "create_dim_style");
            // nâng cấp đợt F5: the multileader style of the standard (its text style, scaled like the dimension style)
            var ml = ApplyMLeaderStyle(ctx, MLeaderStyleLogic.For(prefix, n, f), overwrite);

            // linetypes: load the missing ones (an existing linetype is never redefined)
            var ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
            var loaded = new List<object>();
            var had = new List<object>();
            var missing = new List<object>();
            foreach (var lt in F2Numbers.Linetypes)
            {
                if (ltt.Has(lt)) { had.Add(lt); continue; }
                try { Acad.EnsureLinetype(ctx, lt); loaded.Add(lt); }
                catch (ToolException) { missing.Add(lt); }
                ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
            }

            // LTSCALE only while it is still AutoCAD's default 1 (PSLTSCALE 0: the same dashes in layout viewports)
            double ltWanted = StandardsLogic.LtScale(n, f), ltNow = ctx.Db.Ltscale;
            var ltRes = new Dictionary<string, object> { ["value"] = Acad.R(ltNow), ["recommended"] = Acad.R(ltWanted) };
            if (Math.Abs(ltNow - ltWanted) <= 1e-9 * Math.Max(1, ltWanted)) ltRes["status"] = "unchanged";
            else if (StandardsLogic.ShouldSetLtScale(ltNow))
            {
                ctx.Db.Ltscale = ltWanted;
                ctx.Db.Psltscale = false;
                ctx.NeedsRegen = true;
                ltRes["value"] = Acad.R(ltWanted);
                ltRes["status"] = "set";
                ltRes["psltscale"] = 0;
            }
            else ltRes["status"] = "kept (the user set LTSCALE; tell them the recommended value, they can type LTSCALE to change it)";

            // current styles: only ours with the wanted values (a kept style that differs is not made current)
            if (p.Bool("make_current", true))
            {
                if ((string)ts["status"] != "kept") ctx.Db.Textstyle = TextStyleId(ctx, (string)ts["name"]);
                if ((string)ds["status"] != "kept")
                {
                    var id = DimStyleId(ctx, (string)ds["name"]);
                    ctx.Db.Dimstyle = id;
                    ctx.Db.SetDimstyleData((DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead));
                }
                if ((string)ml["status"] != "kept") ctx.Db.MLeaderstyle = MLeaderStyleIdOf(ctx, (string)ml["name"]); // nâng cấp đợt F5
            }

            var result = new Dictionary<string, object>
            {
                ["standard"] = prefix,
                ["scale"] = "1:" + n.ToString(CultureInfo.InvariantCulture),
                ["units"] = unitName,
                ["text_style"] = ts,
                ["dim_style"] = ds,
                ["mleader_style"] = ml, // nâng cấp đợt F5: add_mleader without a style uses it (current)
                ["linetypes"] = new Dictionary<string, object> { ["loaded"] = loaded, ["already_in_drawing"] = had, ["not_found"] = missing },
                ["ltscale"] = ltRes,
                ["text_heights"] = StandardsLogic.TextHeights(n, f),
                ["note"] = $"text_heights are in drawing units for 1:{n} ({F2Numbers.TextTitle} / {F2Numbers.TextRoom} / {F2Numbers.TextNote} / {F2Numbers.TextSmall} mm on paper): use them for add_text / add_mtext with style {ts["name"]}.",
            };
            var kept = new[] { ts, ds, ml }.Where(x => (string)x["status"] == "kept").Select(x => (object)x["handle"]).ToList();
            if (kept.Count > 0)
            {
                result["kept_handles"] = kept;
                result["next"] = "Existing styles with these names differ and were kept (not overwritten, not made current). Ask the user: request_confirmation with kept_handles " +
                    "and one Vietnamese sentence naming the styles and the differences, end the turn; after Đồng ý call setup_standards again with handles = kept_handles.";
            }
            return result;
        }

        // ------------------------------------------------------------------ nâng cấp đợt F5: multileader style

        private static ObjectId MLeaderStyleIdOf(ToolContext ctx, string name)
        {
            var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
            return dict.Contains(name) ? dict.GetAt(name) : ObjectId.Null;
        }

        private static void SetMLeaderStyle(ToolContext ctx, MLeaderStyle st, MLeaderStyleLogic.Wanted w)
        {
            st.ContentType = ContentType.MTextContent;
            st.TextStyleId = TextStyleId(ctx, w.TextStyle);
            st.TextHeight = w.TextHeight;
            st.ArrowSize = w.ArrowSize;
            st.ArrowSymbolId = ObjectId.Null; // closed filled arrow
            st.EnableLanding = true;
            st.EnableDogleg = true;
            st.LandingGap = w.LandingGap;
            st.DoglegLength = w.DoglegLength;
            st.Scale = w.Scale;
        }

        /// <summary>created / unchanged / updated (the card approved its handle) / kept (differs, not approved), like ApplyStyle.</summary>
        private static Dictionary<string, object> ApplyMLeaderStyle(ToolContext ctx, MLeaderStyleLogic.Wanted w, HashSet<string> overwrite)
        {
            var r = new Dictionary<string, object> { ["name"] = w.Name };
            var id = MLeaderStyleIdOf(ctx, w.Name);
            if (id.IsNull)
            {
                var st = new MLeaderStyle();
                SetMLeaderStyle(ctx, st, w);
                id = st.PostMLeaderStyleToDb(ctx.Db, w.Name);
                ctx.Tr.AddNewlyCreatedDBObject(st, true);
                r["status"] = "created";
                r["handle"] = Acad.H(id);
                return r;
            }
            string h = Acad.H(id);
            r["handle"] = h;
            var ex = (MLeaderStyle)ctx.Tr.GetObject(id, OpenMode.ForRead);
            var diffs = MLeaderStyleLogic.Differences(w, EntityInfo.StyleName(ctx, ex.TextStyleId), ex.Scale, ex.TextHeight, ex.ArrowSize, ex.LandingGap, ex.DoglegLength);
            if (diffs.Count == 0) { r["status"] = "unchanged"; return r; }
            if (overwrite.Contains(h))
            {
                ex.UpgradeOpen();
                SetMLeaderStyle(ctx, ex, w);
                r["status"] = "updated";
                r["changed"] = diffs;
                return r;
            }
            r["status"] = "kept";
            r["differences"] = diffs;
            return r;
        }

        // ------------------------------------------------------------------ create_sheet

        private static ObjectId EnsureTitleBlockDef(ToolContext ctx)
        {
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(F2Numbers.TitleBlockName)) return bt[F2Numbers.TitleBlockName];
            var styleId = EnsureTextStyleF2(ctx, F2Numbers.TitleTextStyle);
            var tb = SheetLogic.BuildTitleBlock();
            var btr = new BlockTableRecord { Name = F2Numbers.TitleBlockName, Origin = Point3d.Origin, Comments = LibraryBlocks.DefinitionNote + ": khung tên" };
            bt.UpgradeOpen();
            var id = bt.Add(btr);
            ctx.Tr.AddNewlyCreatedDBObject(btr, true);
            void Put(Entity e)
            {
                e.SetDatabaseDefaults(ctx.Db);
                ByLayer(e);
                e.Layer = "0";
                btr.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
            }
            var outline = Acad.MakePolyline(new List<double[]> { new[] { -tb.W, 0.0 }, new[] { 0.0, 0.0 }, new[] { 0.0, tb.H }, new[] { -tb.W, tb.H } }, true);
            Put(outline);
            outline.LineWeight = (LineWeight)Geometry.LineWeightFrom(F2Numbers.FrameLineweight);
            foreach (var ln in tb.Lines)
            {
                var l = new Line(new Point3d(ln[0], ln[1], 0), new Point3d(ln[2], ln[3], 0));
                Put(l);
                l.LineWeight = (LineWeight)Geometry.LineWeightFrom(F2Numbers.TitleInnerLineweight);
            }
            foreach (var c in tb.Cells)
            {
                var t = new DBText();
                Put(t);
                t.TextStyleId = styleId;
                t.Height = F2Numbers.TitleLabelHeight;
                t.TextString = c.Label;
                t.Justify = AttachmentPoint.MiddleLeft;
                t.AlignmentPoint = new Point3d(c.LabelAt[0], c.LabelAt[1], 0);
                try { t.AdjustAlignment(ctx.Db); } catch (Autodesk.AutoCAD.Runtime.Exception) { /* placed on the next regen */ }
                var a = new AttributeDefinition();
                Put(a);
                a.TextStyleId = styleId;
                a.Height = c.ValueHeight;
                a.Tag = c.Tag;
                a.Prompt = c.Label;
                a.TextString = "";
                a.Position = new Point3d(c.ValueAt[0], c.ValueAt[1], 0);
                a.Justify = AttachmentPoint.MiddleLeft;
                a.AlignmentPoint = new Point3d(c.ValueAt[0], c.ValueAt[1], 0);
            }
            return id;
        }

        /// <summary>First free "BV-01", "BV-02"… layout name.</summary>
        private static string FreeSheetName(LayoutManager lm)
        {
            for (int i = 1; i < 1000; i++)
            {
                var n = F2Numbers.SheetNamePrefix + i.ToString("00", CultureInfo.InvariantCulture);
                if (!lm.LayoutExists(n)) return n;
            }
            throw ToolException.Invalid("create_sheet: give a layout name.");
        }

        internal static object CreateSheet(ToolContext ctx, Params p)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            // nâng cấp đợt H4: the company standard's paper and title block
            CompanyStandard company = p.Has("company") ? CompanyStandardLogic.Require(CompanyRoot, p.Str("company"), "create_sheet") : null;
            var cTitle = company?.TitleBlocks.FirstOrDefault();
            var cPaper = company == null ? null : company.Papers.FirstOrDefault(x => cTitle != null && x.Layout == cTitle.Layout) ?? company.Papers.FirstOrDefault(x => x.Name != null);
            string paper = SheetLogic.NormalizePaper(p.Str("paper", cPaper?.Name ?? "A3"));
            bool? land = cPaper != null && cPaper.Name != null && !p.Has("paper") ? cPaper.Landscape : (bool?)null;
            var orient = p.Str("orientation", null);
            if (!string.IsNullOrEmpty(orient))
            {
                if (orient.Equals("landscape", StringComparison.OrdinalIgnoreCase) || orient.Equals("ngang", StringComparison.OrdinalIgnoreCase)) land = true;
                else if (orient.Equals("portrait", StringComparison.OrdinalIgnoreCase) || orient.Equals("dung", StringComparison.OrdinalIgnoreCase) || orient.Equals("đứng", StringComparison.OrdinalIgnoreCase)) land = false;
                else throw ToolException.Invalid("create_sheet: orientation must be landscape or portrait.");
            }
            var sheet = SheetLogic.Build(paper, land);
            // the main viewport of the company's layout, when it is on the same paper
            bool cVp = cPaper?.Viewport != null && cPaper.Name == paper && cPaper.Landscape == sheet.Landscape && cTitle != null
                && company.TemplateKept && File.Exists(CompanyStandardLogic.TemplatePath(CompanyRoot, company.Name));
            if (cVp)
            {
                var v = cPaper.Viewport;
                sheet.VpX0 = v[0] - v[2] / 2; sheet.VpX1 = v[0] + v[2] / 2; sheet.VpY0 = v[1] - v[3] / 2; sheet.VpY1 = v[1] + v[3] / 2;
            }
            double f;
            string unitName;
            using (var tr0 = doc.TransactionManager.StartTransaction())
            {
                f = MmPerUnitOf(new ToolContext(doc, tr0, false, ctx.Dispatcher), out unitName);
                tr0.Abort();
            }

            // what the viewport shows: view_center (current UCS) or the middle of the drawing extents
            bool extOk = db.Extmax.X > db.Extmin.X && db.Extmax.Y > db.Extmin.Y && db.Extmax.X < 1e20;
            double[] center = p.PointOpt("view_center");
            if (center == null) center = extOk ? new[] { (db.Extmin.X + db.Extmax.X) / 2, (db.Extmin.Y + db.Extmax.Y) / 2, 0 } : new[] { 0.0, 0, 0 };
            double ew = extOk ? db.Extmax.X - db.Extmin.X : 0, eh = extOk ? db.Extmax.Y - db.Extmin.Y : 0;
            // F2-test: a UCS turned in plan turns the viewport with it: size (and centre) the drawing in that UCS
            var ucs0 = UcsScope.Input;
            if (extOk && ucs0 != null && ucs0.ZUp)
            {
                var (bmin, bmax) = ucs0.BoxFromWcs(new[] { db.Extmin.X, db.Extmin.Y, db.Extmin.Z }, new[] { db.Extmax.X, db.Extmax.Y, db.Extmax.Z });
                ew = bmax[0] - bmin[0];
                eh = bmax[1] - bmin[1];
                if (!p.Has("view_center")) center = ucs0.ToWcs(new[] { (bmin[0] + bmax[0]) / 2, (bmin[1] + bmax[1]) / 2, bmin[2] });
            }
            bool scaleGiven = p.Has("scale");
            int n = scaleGiven ? StandardsLogic.ScaleDenominator(p.Get("scale"), "create_sheet")
                : extOk ? SheetLogic.FitScale(sheet, f, ew, eh) : F2Numbers.DefaultScale;
            var values = SheetLogic.FieldValues(p.ObjOpt("fields")?.Raw, n, paper, DateTime.Today);

            var lm = LayoutManager.Current;
            string name;
            var result = new Dictionary<string, object>();
            using (doc.LockDocument())
            {
                name = p.Has("name") ? p.Str("name").Trim() : FreeSheetName(lm);
                ValidateSymbolName(name, "layout");
                if (lm.LayoutExists(name)) throw ToolException.Invalid($"Layout '{name}' already exists. Use another name (create_sheet never changes an existing layout).");
                lm.CreateLayout(name);
                try
                {
                    string media;
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var lo = FindLayout(tr, db, name, OpenMode.ForWrite);
                        var psv = Autodesk.AutoCAD.DatabaseServices.PlotSettingsValidator.Current;
                        psv.SetPlotConfigurationName(lo, DefaultPdfDevice, null);
                        psv.RefreshLists(lo);
                        var list = psv.GetCanonicalMediaNameList(lo).Cast<string>().ToList();
                        media = sheet.Media;
                        var swapped = SheetLogic.MediaName(sheet.H, sheet.W, paper);
                        bool rotate = false;
                        if (!list.Contains(media))
                        {
                            if (list.Contains(swapped)) rotate = true;
                            else throw ToolException.Invalid($"create_sheet: {DefaultPdfDevice} has no paper {media}. Check list_plot_media.");
                        }
                        psv.SetPlotConfigurationName(lo, DefaultPdfDevice, rotate ? swapped : media);
                        psv.SetPlotPaperUnits(lo, PlotPaperUnit.Millimeters);
                        if (rotate) psv.SetPlotRotation(lo, PlotRotation.Degrees090);
                        psv.SetPlotType(lo, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                        psv.SetUseStandardScale(lo, true);
                        psv.SetStdScaleType(lo, StdScaleType.StdScale1To1);
                        if (rotate) media = swapped;
                        tr.Commit();
                    }

                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var c = new ToolContext(doc, tr, true, ctx.Dispatcher);
                        var lo = FindLayout(tr, db, name, OpenMode.ForWrite);
                        // initialize the new layout now (as opening its tab would) and drop the default viewport AutoCAD
                        // may add (LAYOUTCREATEVIEWPORT): the sheet gets exactly one model viewport, ours
                        try
                        {
                            lo.Initialize();
                            var vps = lo.GetViewports();
                            for (int i = 1; i < vps.Count; i++) tr.GetObject(vps[i], OpenMode.ForWrite).Erase();
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { /* not initialized here: AutoCAD does it when the tab opens */ }
                        var space = (BlockTableRecord)tr.GetObject(lo.BlockTableRecordId, OpenMode.ForWrite);
                        var frameLayer = EnsureLayerF2(c, F2Numbers.FrameLayer, F2Numbers.FrameLayerColor);
                        var vpLayer = EnsureLayerF2(c, F2Numbers.ViewportLayer, F2Numbers.ViewportLayerColor, plottable: false);
                        string cWhy = null;
                        ObjectId? cDef = cTitle != null ? CompanyTitleBlockDef(c, company, cTitle, out cWhy) : null;
                        if (company != null && cTitle == null) cWhy = $"the company standard '{company.Name}' has no title block (no block with attributes on a layout of the template)";
                        var tbId = cDef ?? EnsureTitleBlockDef(c);

                        var frame = Acad.MakePolyline(new List<double[]>
                        {
                            new[] { sheet.X0, sheet.Y0 }, new[] { sheet.X1, sheet.Y0 }, new[] { sheet.X1, sheet.Y1 }, new[] { sheet.X0, sheet.Y1 },
                        }, true);
                        frame.SetDatabaseDefaults(db);
                        ByLayer(frame);
                        frame.LayerId = frameLayer;
                        frame.LineWeight = (LineWeight)Geometry.LineWeightFrom(F2Numbers.FrameLineweight);
                        // a company title block carries its own frame: the Vietnamese frame is only drawn without one
                        ObjectId frameId = ObjectId.Null;
                        if (cDef == null) { frameId = space.AppendEntity(frame); tr.AddNewlyCreatedDBObject(frame, true); }
                        else frame.Dispose();

                        var br = cDef != null
                            ? new BlockReference(new Point3d(cTitle.X, cTitle.Y, 0), tbId) { Rotation = cTitle.Rotation * Math.PI / 180, ScaleFactors = new Scale3d(cTitle.Scale) }
                            : new BlockReference(new Point3d(sheet.X1, sheet.Y0, 0), tbId);
                        br.SetDatabaseDefaults(db);
                        ByLayer(br);
                        br.LayerId = frameLayer; // Test & sửa H4: the company title block too (not the current layer)
                        var brId = space.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);
                        var tbtr = (BlockTableRecord)tr.GetObject(tbId, OpenMode.ForRead);
                        var objValues = values.ToDictionary(kv => kv.Key, kv => (object)kv.Value, StringComparer.OrdinalIgnoreCase);
                        // nâng cấp đợt H4: the company block's own tags (attributes: tag → text)
                        var given = p.ObjOpt("attributes")?.Raw;
                        if (given != null) foreach (var kv in given) objValues[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
                        var atts = AddAttributes(c, br, tbtr, objValues);

                        var vp = new Viewport();
                        vp.SetDatabaseDefaults(db);
                        ByLayer(vp);
                        vp.CenterPoint = new Point3d(sheet.VpCenter[0], sheet.VpCenter[1], 0);
                        vp.Width = sheet.VpW;
                        vp.Height = sheet.VpH;
                        vp.LayerId = vpLayer;
                        var vpId = space.AppendEntity(vp);
                        tr.AddNewlyCreatedDBObject(vp, true);
                        vp.ViewDirection = Vector3d.ZAxis;
                        vp.ViewTarget = Point3d.Origin;
                        // the current UCS turned in plan: the viewport turns with it (like PLAN), so the UCS x axis is horizontal on the sheet
                        var ucs = UcsScope.Input;
                        double turn = ucs != null && ucs.ZUp ? ucs.PlanAngleDeg * Math.PI / 180 : 0;
                        if (Math.Abs(turn) > 1e-12)
                        {
                            vp.TwistAngle = (2 * Math.PI - turn) % (2 * Math.PI);
                            var dcs = new Point3d(center[0], center[1], 0).TransformBy(Matrix3d.Rotation(-turn, Vector3d.ZAxis, Point3d.Origin));
                            vp.ViewCenter = new Point2d(dcs.X, dcs.Y);
                        }
                        else vp.ViewCenter = new Point2d(center[0], center[1]);
                        vp.CustomScale = SheetLogic.CustomScale(n, f);
                        vp.Locked = p.Bool("locked", true);
                        try { vp.On = true; } catch (Autodesk.AutoCAD.Runtime.Exception) { /* turned on when the layout is first displayed */ }
                        tr.Commit();

                        var area = SheetLogic.ModelArea(sheet, n, f);
                        result["layout"] = name;
                        result["paper"] = paper + (sheet.Landscape ? " ngang" : " đứng");
                        result["media"] = media;
                        result["sheet_mm"] = new List<object> { sheet.W, sheet.H };
                        result["frame_mm"] = new List<object> { sheet.X0, sheet.Y0, sheet.X1, sheet.Y1 };
                        if (!frameId.IsNull) result["frame_handle"] = Acad.H(frameId);
                        if (company != null)
                        {
                            result["company"] = company.Name;
                            if (cDef != null) result["company_title_block"] = new Dictionary<string, object> { ["block"] = cTitle.Block, ["tags"] = cTitle.Tags.Cast<object>().ToList(), ["viewport_from_template"] = cVp };
                            else result["company_note"] = "The Vietnamese frame and title block were used: " + cWhy + ".";
                            if (cPaper != null && cPaper.Name == null) result["company_paper_note"] = $"The template's paper ({cPaper.Media}, {cPaper.W} × {cPaper.H} mm) is not A0 … A4: {paper} was used.";
                            // Test & sửa H4: the company title block sits where it was on the template's paper: on another paper it is off the sheet
                            else if (cDef != null && cPaper != null && (cPaper.Name != paper || cPaper.Landscape != sheet.Landscape))
                                result["company_paper_note"] = $"The company title block was placed as on the template's {cPaper.Name} {(cPaper.Landscape ? "landscape" : "portrait")} sheet, but this sheet is {paper} {(sheet.Landscape ? "landscape" : "portrait")}: it may sit off the frame. Tell the user; use the template's paper unless they want this one.";
                        }
                        result["title_block_handle"] = Acad.H(brId);
                        if (cDef == null) result["title_fields"] = values.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                        else
                        {
                            // Sửa KTH-11: the company block's own fields as written (not the Vietnamese ones it does not have)
                            result["filled"] = atts.Where(kv => !string.IsNullOrWhiteSpace(kv.Value.TextString)).ToDictionary(kv => kv.Key, kv => (object)kv.Value.TextString);
                            var empty = atts.Where(kv => string.IsNullOrWhiteSpace(kv.Value.TextString)).Select(kv => (object)kv.Key).ToList();
                            if (empty.Count > 0) result["empty_fields"] = empty;
                            var unknown = (given ?? new Dictionary<string, object>()).Keys.Where(k => !atts.ContainsKey(k)).Cast<object>().ToList();
                            if (unknown.Count > 0) result["unknown_attributes"] = unknown;
                            if (p.Has("fields")) result["fields_note"] = $"'fields' are the Vietnamese title block's; the company block {cTitle.Block} has its own fields ({string.Join(", ", cTitle.Tags)}): fill them with 'attributes' (tag → text).";
                        }
                        result["viewport_handle"] = Acad.H(vpId);
                        result["scale"] = "1:" + n.ToString(CultureInfo.InvariantCulture) + (scaleGiven ? "" : " (chosen: the drawing fits)");
                        result["view_center"] = Acad.J(new Point3d(center[0], center[1], 0));
                        result["model_area"] = new List<object> { Acad.R(area[0]), Acad.R(area[1]) };
                        result["units"] = unitName;
                        if (turn != 0) result["viewport_twist_deg"] = Acad.R(-turn * 180 / Math.PI);
                        if (extOk && !SheetLogic.Fits(sheet, n, f, ew, eh))
                            result["warning"] = $"The drawing ({Acad.R(ew)} × {Acad.R(eh)}) is bigger than the viewport at 1:{n} ({Acad.R(area[0])} × {Acad.R(area[1])}): " +
                                $"part of it is outside. 1:{SheetLogic.FitScale(sheet, f, ew, eh)} fits this paper, or use a bigger paper.";
                        var longOnes = cDef != null ? new List<string>() : SheetLogic.TooLong(SheetLogic.BuildTitleBlock(), values);
                        if (longOnes.Count > 0) result["long_fields"] = longOnes.Cast<object>().ToList();
                        result["next"] = $"Change a title field later with set_block_attributes on {Acad.H(brId)} (tags {string.Join(", ", cDef != null ? cTitle.Tags : SheetLogic.Fields.Select(x => x.tag))}). Print with plot_pdf {{\"layout\": \"{name}\"}}.";
                        if (cDef == null && atts.Count == 0) result["warning_title"] = "The title block has no fields (a block named " + F2Numbers.TitleBlockName + " already existed in the drawing and was used as is).";
                    }
                }
                catch
                {
                    try { lm.DeleteLayout(name); } catch (Exception) { /* undo removes it too */ }
                    throw;
                }
                if (p.Bool("activate", false)) lm.CurrentLayout = name;
            }
            return result;
        }

        // ------------------------------------------------------------------ insert_library_block

        private static ObjectId EnsureLibraryDef(ToolContext ctx, LibItem it, double w, double d, double f, out bool reused)
        {
            string name = LibraryBlocks.BlockName(it, w, d);
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            // kiểm tra độc lập đợt F: a block of the same name drawn at another size (another drawing unit, the user's own
            // drawing) is not reused: the library one goes under "<name>-TV" (still CUA-DI-… / CUA-SO-… for build_3d_from_plan)
            var wantBox = LibraryBlocks.Box(LibraryBlocks.Build(it, w, d));
            if (bt.Has(name) && !SameSize(ctx, bt[name], (wantBox[2] - wantBox[0]) / f, (wantBox[3] - wantBox[1]) / f))
                name = LibraryBlocks.OtherSizeName(name);
            reused = bt.Has(name);
            if (reused) return bt[name];
            var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin, Comments = LibraryBlocks.DefinitionNote + ": " + it.Name };
            bt.UpgradeOpen();
            var id = bt.Add(btr);
            ctx.Tr.AddNewlyCreatedDBObject(btr, true);
            Point3d P(double[] q) => new Point3d(q[0] / f, q[1] / f, 0);
            foreach (var pr in LibraryBlocks.Build(it, w, d))
            {
                Entity e;
                switch (pr.Kind)
                {
                    case "line": e = new Line(P(pr.Pts[0]), P(pr.Pts[1])); break;
                    case "circle": e = new Circle(P(pr.Pts[0]), Vector3d.ZAxis, pr.R / f); break;
                    case "arc": e = new Arc(P(pr.Pts[0]), pr.R / f, pr.A0 * Math.PI / 180, pr.A1 * Math.PI / 180); break;
                    case "ellipse":
                    {
                        bool alongX = pr.Rx >= pr.Ry;
                        var major = alongX ? new Vector3d(pr.Rx / f, 0, 0) : new Vector3d(0, pr.Ry / f, 0);
                        double ratio = alongX ? pr.Ry / pr.Rx : pr.Rx / pr.Ry;
                        e = new Ellipse(P(pr.Pts[0]), Vector3d.ZAxis, major, ratio, 0, 2 * Math.PI);
                        break;
                    }
                    default:
                        e = Acad.MakePolyline(pr.Pts.Select(q => new[] { q[0] / f, q[1] / f }).ToList(), pr.Closed, pr.Bulges);
                        break;
                }
                e.SetDatabaseDefaults(ctx.Db);
                ByLayer(e);
                e.Layer = "0"; // takes the layer / color of the block reference
                btr.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
            }
            return id;
        }

        /// <summary>The block definition's extents are w × h drawing units (within LibraryBlocks.SizeTol), or it has no extents.</summary>
        private static bool SameSize(ToolContext ctx, ObjectId btrId, double w, double h)
        {
            var btr = (BlockTableRecord)ctx.Tr.GetObject(btrId, OpenMode.ForRead);
            Extents3d? box = null;
            foreach (ObjectId id in btr)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e) || e is AttributeDefinition) continue;
                var x = EntityInfo.Extents(e);
                if (!x.HasValue) continue;
                if (box.HasValue) { var b = box.Value; b.AddExtents(x.Value); box = b; } else box = x;
            }
            if (!box.HasValue) return true;
            double bw = box.Value.MaxPoint.X - box.Value.MinPoint.X, bh = box.Value.MaxPoint.Y - box.Value.MinPoint.Y;
            return LibraryBlocks.SameSize(bw, bh, w, h);
        }

        internal static object InsertLibraryBlock(ToolContext ctx, Params p)
        {
            if (!p.Has("name"))
                return new Dictionary<string, object>
                {
                    ["count"] = LibraryBlocks.Items.Count,
                    ["blocks"] = LibraryBlocks.Catalog(),
                    ["note"] = "Sizes in mm: [default, min, max]. Insert with insert_library_block {name, position, width?, depth?, rotation?, flip?}.",
                };
            var it = LibraryBlocks.Require(p.Str("name"));
            var (w, d) = LibraryBlocks.Sizes(it, p.NumOpt("width"), p.NumOpt("depth"));
            double f = MmPerUnitOf(ctx, out var unitName);
            var defId = EnsureLibraryDef(ctx, it, w, d, f, out bool reused);
            string layer = p.Str("layer", it.Layer);
            EnsureLayerF2(ctx, layer, F2Numbers.LibraryLayerColors.TryGetValue(layer, out var col) ? col : 7);
            var props = new Dictionary<string, object>(p.Raw) { ["layer"] = layer };
            var pp = new Params("insert_library_block", props);
            var positions = p.Has("positions") ? p.Points("positions", 1, MaxArrayCopies) : new List<double[]> { p.Point("position") };
            double rot = Geometry.DegToRad(p.Num("rotation", 0));
            var scale = p.Bool("flip", false) ? new Scale3d(-1, 1, 1) : new Scale3d(1);
            var ids = new List<ObjectId>();
            foreach (var pos in positions)
            {
                var br = new BlockReference(Acad.P3(pos), defId) { ScaleFactors = scale, Rotation = rot };
                // kiểm tra độc lập đợt F: the reference itself ByLayer too (not the current colour), like the frame / title block
                br.SetDatabaseDefaults(ctx.Db);
                ByLayer(br);
                ids.Add(Acad.Add(ctx, br, pp, defaults: false));
            }
            var r = Acad.Handles(ids);
            r["block"] = LibraryBlocks.BlockName(it, w, d);
            r["label"] = it.Name;
            r[it.WidthMeaning.Replace(' ', '_') + "_mm"] = w;
            if (it.HasDepth) r[it.DepthMeaning.Replace(' ', '_') + "_mm"] = d;
            r["layer"] = layer;
            r["base_point"] = it.Base;
            if (reused) r["note"] = "The drawing already had a block with this name and size; it was inserted as it is (not redefined).";
            if (f != 1) r["units"] = unitName;
            return r;
        }

        // ------------------------------------------------------------------ layer_state

        private const LayerStateMasks StateMask = LayerStateMasks.On | LayerStateMasks.Frozen | LayerStateMasks.Locked | LayerStateMasks.Plot
            | LayerStateMasks.Color | LayerStateMasks.LineType | LayerStateMasks.LineWeight;

        private static Dictionary<string, ObjectId> LayerStates(ToolContext ctx)
        {
            var d = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            var dictId = ctx.Db.LayerStateManager.LayerStatesDictionaryId(false);
            if (dictId.IsNull) return d;
            var dict = (DBDictionary)ctx.Tr.GetObject(dictId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict) d[e.Key] = e.Value;
            return d;
        }

        private static Dictionary<string, string> LayerSnapshot(ToolContext ctx)
        {
            var s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in lt)
            {
                var r = (LayerTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (r.IsErased) continue;
                s[r.Name] = $"{r.IsOff}|{r.IsFrozen}|{r.IsLocked}|{r.IsPlottable}|{r.Color.ColorIndex}|{r.Color.ColorMethod}|{r.LinetypeObjectId}|{r.LineWeight}";
            }
            return s;
        }

        internal static object LayerState(ToolContext ctx, Params p)
        {
            string action = LayerStateLogic.NormalizeAction(p.Str("action", "list"));
            var lsm = ctx.Db.LayerStateManager;
            var states = LayerStates(ctx);
            if (action == "list")
            {
                var list = states.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv =>
                {
                    var d = new Dictionary<string, object> { ["name"] = kv.Key, ["handle"] = Acad.H(kv.Value) };
                    string desc = null;
                    try { desc = lsm.GetLayerStateDescription(kv.Key); } catch (Autodesk.AutoCAD.Runtime.Exception) { }
                    if (!string.IsNullOrEmpty(desc)) d["description"] = desc;
                    return (object)d;
                }).ToList();
                return new Dictionary<string, object> { ["count"] = list.Count, ["states"] = list };
            }
            string name = LayerStateLogic.CheckName(p.Str("name", null));
            bool exists = states.TryGetValue(name, out var stateId);
            switch (action)
            {
                case "save":
                {
                    if (exists)
                    {
                        string h = Acad.H(stateId);
                        var ok = ApprovedOverwrites(p, "layer_state", $"overwrite the existing layer state '{name}'");
                        if (!ok.Contains(h)) throw new ToolException(ErrorCodes.InvalidParams, F2Confirm.CardMessage("layer_state", $"overwrite the existing layer state '{name}'", new[] { h }));
                        lsm.DeleteLayerState(name);
                    }
                    lsm.SaveLayerState(name, StateMask, ObjectId.Null);
                    var desc = p.Str("description", null);
                    if (!string.IsNullOrEmpty(desc)) lsm.SetLayerStateDescription(name, desc);
                    var now = LayerStates(ctx);
                    return new Dictionary<string, object>
                    {
                        ["saved"] = name,
                        ["overwritten"] = exists,
                        ["handle"] = now.TryGetValue(name, out var nid) ? Acad.H(nid) : null,
                        ["layers"] = LayerSnapshot(ctx).Count,
                        ["remembers"] = LayerStateLogic.Saved.Cast<object>().ToList(),
                    };
                }
                case "restore":
                {
                    if (!exists) throw ToolException.Invalid($"layer_state: no layer state '{name}'. States: {(states.Count == 0 ? "(none)" : string.Join(", ", states.Keys))}.");
                    var before = LayerSnapshot(ctx);
                    lsm.RestoreLayerState(name, ObjectId.Null, 0, StateMask);
                    var after = LayerSnapshot(ctx);
                    var changed = after.Where(kv => before.TryGetValue(kv.Key, out var b) && b != kv.Value).Select(kv => (object)kv.Key).ToList();
                    ctx.NeedsRegen = true;
                    return new Dictionary<string, object>
                    {
                        ["restored"] = name,
                        ["changed_layers"] = changed.Count,
                        ["layers"] = changed.Take(50).ToList(),
                        ["note"] = "Layers made after the state was saved keep their settings. undo_last_turn brings the previous settings back.",
                    };
                }
                default: // delete
                {
                    if (!exists) throw ToolException.Invalid($"layer_state: no layer state '{name}'.");
                    string h = F5Confirm.DeleteLayerStateToken(Acad.H(stateId)); // KFG-09: its own token, not the overwrite's
                    var ok = ApprovedOverwrites(p, "layer_state", $"delete the layer state '{name}'");
                    if (!ok.Contains(h)) throw new ToolException(ErrorCodes.InvalidParams, F2Confirm.CardMessage("layer_state", $"delete the layer state '{name}'", new[] { h }));
                    lsm.DeleteLayerState(name);
                    return new Dictionary<string, object> { ["deleted"] = name };
                }
            }
        }
    }
}
