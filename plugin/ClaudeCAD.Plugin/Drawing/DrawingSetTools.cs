using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt G1: create_drawing_set — "ra bộ hồ sơ". Plans (a horizontal cut of the model per storey, or the 2D drawing),
    /// elevations, sections and a perspective, each on its own sheet with the F2 title block, numbered KT-01…, and one PDF if
    /// asked. The views are made by the tools that exist (section_view, projection_view), the sheets by create_sheet, the PDF
    /// by plot_pdf; each part is made only when the plan has that tool's group (Core/DrawingSet.cs, DrawingSetLogic), so the
    /// set never gives away a part of another plan. A second run replaces the set it made before only after the card.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterDrawingSet(ToolDispatcher d)
        {
            d.Register(new ToolDef("create_drawing_set", true, CreateDrawingSet, noTransaction: true)); // = DrawingSetLogic.Tool
        }

        private sealed class SolidInfo
        {
            public string Handle, Layer;
            public double[] Min, Max;        // UCS box
            public double[] WMin, WMax;      // WCS box
        }

        private static double[] Arr(object o, int min, string what)
        {
            if (!(o is IList<object> l) || l.Count < min || l.Count > 3) throw ToolException.Invalid($"{DrawingSetLogic.Tool}: {what} must be a point [x, y].");
            return l.Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
        }

        private static double[] Ucs3(double[] p, double z = 0) => new[] { p[0], p[1], p.Length > 2 ? p[2] : z };

        internal static object CreateDrawingSet(ToolContext ctx, Params p)
        {
            const string tool = DrawingSetLogic.Tool;
            var doc = ctx.Doc;
            var db = ctx.Db;
            var notes = new List<string>();
            string plan = ctx.Dispatcher?.Plan ?? PlanLogic.PlanFull;
            string mode = ctx.Dispatcher?.WorkMode;
            var frame = UcsScope.Input;
            if (frame != null && !frame.ZUp)
            {
                notes.Add("The current UCS does not lie flat (z is not up): the set uses the world axes (front = looking +Y).");
                frame = null;
            }
            double[] ToW(double[] u) => frame == null ? u : frame.ToWcs(u);
            double[] VecW(double[] u) => frame == null ? u : frame.VecToWcs(u);

            // ---- the call
            var req = new DrawingSetLogic.Request
            {
                Kinds = p.StringsOpt("kinds"),
                Elevations = p.StringsOpt("elevations"),
                PlanSource = p.Str("plan_source", "auto"),
                Prefix = p.Str("prefix", G1Numbers.SheetPrefix),
            };
            if (p.Has("sections"))
            {
                req.Sections = new List<(double[], double[], string)>();
                foreach (var o in p.Array("sections"))
                {
                    if (!(o is Dictionary<string, object> d) || !d.TryGetValue("line", out var lo) || !(lo is IList<object> ll) || ll.Count != 2)
                        throw ToolException.Invalid($"{tool}: each section is {{\"line\": [[x1, y1], [x2, y2]], \"name\"?}} (points in the current UCS).");
                    req.Sections.Add((Arr(ll[0], 2, "a section point"), Arr(ll[1], 2, "a section point"), d.TryGetValue("name", out var nm) ? nm as string : null));
                }
            }
            if (p.Has("plan_areas"))
            {
                req.PlanAreas = new List<(double[], double[], string)>();
                foreach (var o in p.Array("plan_areas"))
                {
                    if (!(o is Dictionary<string, object> d) || !d.TryGetValue("min", out var a) || !d.TryGetValue("max", out var b))
                        throw ToolException.Invalid($"{tool}: each plan area is {{\"min\": [x, y], \"max\": [x, y], \"title\"?}} (current UCS).");
                    req.PlanAreas.Add((Arr(a, 2, "min"), Arr(b, 2, "max"), d.TryGetValue("title", out var t) ? t as string : null));
                }
            }
            int givenScale = p.Has("scale") ? StandardsLogic.ScaleDenominator(p.Get("scale"), tool) : 0;
            string paper = SheetLogic.NormalizePaper(p.Str("paper", "A3"));
            string orientation = p.Str("orientation", null);
            bool wantPdf = p.Bool("pdf", false);
            var userFields = new Dictionary<string, object>(p.ObjOpt("fields")?.Raw ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
            foreach (var k in new[] { "title", "sheet_no", "scale", "paper" }) userFields.Remove(k);
            // Test & sửa G1: an unknown field made every create_sheet fail after the old set was already removed
            var badFields = DrawingSetLogic.UnknownFields(userFields.Keys);
            if (badFields.Count > 0)
                throw ToolException.Invalid($"{tool}: unknown title field(s) {string.Join(", ", badFields)}. Fields: {string.Join(", ", SheetLogic.Fields.Select(x => x.field))}. Nothing was changed.");

            // ---- what the drawing has
            double f;
            var solids = new List<SolidInfo>();
            double[] min2 = null, max2 = null;          // 2D drawing box (UCS)
            double allMaxX = double.NegativeInfinity, allMinY = double.PositiveInfinity;
            var layoutNames = new List<string>();
            var layoutHandles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // KFG-24
            int aliveViews = 0; // KFG-21: objects of the last set's views still in the drawing
            var solidLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DrawingSetLogic.Record last;
            var generated = new HashSet<string>(new[] { SectionLogic.OutlineLayer, SectionLogic.HatchLayer, SectionLogic.BehindLayer, SectionLogic.ElevationLayer,
                G5Numbers.Layer }, StringComparer.OrdinalIgnoreCase); // Test & sửa G5: the traced picture is not part of the 2D plan
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var c = new ToolContext(doc, tr, false, ctx.Dispatcher);
                f = MmPerUnitOf(c, out _);
                last = ReadSetRecord(tr, db);
                var lastHandles = new HashSet<string>(last.Handles, StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId id in c.ModelSpace)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || e.IsErased) continue;
                    var x = EntityInfo.Extents(e);
                    if (!x.HasValue) continue;
                    allMaxX = Math.Max(allMaxX, x.Value.MaxPoint.X);
                    allMinY = Math.Min(allMinY, x.Value.MinPoint.Y);
                    double[] wmin = { x.Value.MinPoint.X, x.Value.MinPoint.Y, x.Value.MinPoint.Z }, wmax = { x.Value.MaxPoint.X, x.Value.MaxPoint.Y, x.Value.MaxPoint.Z };
                    var (bmin, bmax) = frame == null ? (wmin, wmax) : frame.BoxFromWcs(wmin, wmax);
                    if (e is Solid3d)
                    {
                        solids.Add(new SolidInfo { Handle = Acad.H(id), Layer = e.Layer, Min = bmin, Max = bmax, WMin = wmin, WMax = wmax });
                        solidLayers.Add(e.Layer);
                        continue;
                    }
                    if (lastHandles.Contains(Acad.H(id))) { aliveViews++; continue; }
                    // KFG-22: a schedule / estimate table beside the plan is not part of the plan's frame
                    if (e is Autodesk.AutoCAD.DatabaseServices.Surface || e is Body || e is Autodesk.AutoCAD.DatabaseServices.Section || e is Table || generated.Contains(e.Layer)) continue;
                    if (min2 == null) { min2 = new[] { bmin[0], bmin[1] }; max2 = new[] { bmax[0], bmax[1] }; }
                    else { min2[0] = Math.Min(min2[0], bmin[0]); min2[1] = Math.Min(min2[1], bmin[1]); max2[0] = Math.Max(max2[0], bmax[0]); max2[1] = Math.Max(max2[1], bmax[1]); }
                }
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry de in dict)
                    if (!de.Key.Equals("Model", StringComparison.OrdinalIgnoreCase)) { layoutNames.Add(de.Key); layoutHandles[de.Key] = Acad.H(de.Value); }
                tr.Abort();
            }
            if (solids.Count > SectionLogic.MaxSolids)
            {
                notes.Add($"The drawing has {solids.Count} 3D solids; the views use the first {SectionLogic.MaxSolids} (section_view / projection_view limit).");
                solids = solids.Take(SectionLogic.MaxSolids).ToList();
            }
            if (solids.Count > 0)
            {
                req.SolidMin = new[] { solids.Min(s => s.Min[0]), solids.Min(s => s.Min[1]), solids.Min(s => s.Min[2]) };
                req.SolidMax = new[] { solids.Max(s => s.Max[0]), solids.Max(s => s.Max[1]), solids.Max(s => s.Max[2]) };
                var walls = solids.Where(s => DrawingSetLogic.IsWallLayer(s.Layer)).Select(s => (s.Min[2], s.Max[2] - s.Min[2])).ToList();
                req.Levels = DrawingSetLogic.StoreyLevels(walls, f);
                int storeys = DrawingSetLogic.StoreyCount(walls, f);
                if (storeys > req.Levels.Count)
                    notes.Add($"The model has {storeys} storeys; the set has plans of the lowest {req.Levels.Count} (at most {G1Numbers.MaxLevels}).");
            }
            req.Min2d = min2;
            req.Max2d = max2;
            var ab = DrawingSetLogic.Abilities(plan, solids.Count > 0, min2 != null);
            var set = DrawingSetLogic.Build(req, ab, mode, plan);
            if (set.Sheets.Count == 0)
                throw ToolException.Invalid($"{tool}: nothing of the set can be made here: " +
                    string.Join("; ", set.Missing.Select(kv => kv.Key + ": " + kv.Value)) + ". Nothing was changed.");
            bool? land = null;
            if (!string.IsNullOrEmpty(orientation))
            {
                var o = orientation.Trim().ToLowerInvariant();
                if (o == "landscape" || o == "ngang") land = true;
                else if (o == "portrait" || o == "dung" || o == "đứng") land = false;
                else throw ToolException.Invalid($"{tool}: orientation must be landscape or portrait.");
            }
            var sheet = SheetLogic.Build(paper, land);
            DrawingSetLogic.ChooseScales(set, sheet, f, givenScale);
            foreach (var t in DrawingSetLogic.NotFitting(set, sheet, f))
                notes.Add($"{t}: the view is larger than the sheet at its scale, so the sheet shows only its middle (give a larger scale or paper).");
            bool makeSheets = ab.Sheets;
            if (!makeSheets) notes.Add(DrawingSetLogic.SheetsMissing(mode, plan) + ": the views are drawn in model space only.");
            if (wantPdf && !(makeSheets && ab.Pdf)) { notes.Add("No PDF: " + DrawingSetLogic.SheetsMissing(mode, plan) + "."); wantPdf = false; }

            // ---- the set made before, the user's own layouts, the card
            var wanted = makeSheets ? set.Sheets.Select(s => s.Number).ToList() : new List<string>();
            var clashes = DrawingSetLogic.Clashes(wanted, layoutHandles, last);
            if (clashes.Count > 0)
                throw ToolException.Invalid($"{tool}: layout(s) {string.Join(", ", clashes)} already exist and are not from the set made before (the user's own; never touched). Nothing was changed. Use another prefix (e.g. \"KT2\").");
            var oldLayouts = DrawingSetLogic.OurLayouts(layoutHandles, last);
            // KFG-21: a set whose sheets and views the user already erased by hand is not asked about
            bool hadSet = oldLayouts.Count > 0 || aliveViews > 0;
            string pdfPath = null;
            if (wantPdf)
            {
                string output = p.Str("pdf_output", DrawingSetLogic.PdfName(SafeString(() => doc.Name)));
                if (!output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid($"{tool}: pdf_output must end with .pdf.");
                string dir = SafeString(() => Path.GetDirectoryName(doc.Name));
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                try { pdfPath = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(dir, output)); }
                catch (Exception) { throw ToolException.Invalid($"{tool}: '{output}' is not a valid file path."); }
            }
            if (hadSet && ctx.Dispatcher?.MacroRunning == true)
                throw ToolException.Invalid($"{tool}: this drawing already has a drawing set (layouts {string.Join(", ", oldLayouts)}); a macro does not replace it. Run it from the chat (a card asks first).");
            var tokens = new List<string>();
            var what = new List<string>();
            if (hadSet)
            {
                tokens.Add(DrawingSetLogic.ReplaceToken(last.Layouts, last.Handles)); // Test & sửa G1: with the views it drew
                what.Add($"replace the drawing set made before (layouts {string.Join(", ", oldLayouts.DefaultIfEmpty("—"))} and the {aliveViews} lines / hatches of the views it drew; edits made on them are lost)"); // KFG-20
            }
            if (pdfPath != null && File.Exists(pdfPath) && ctx.Dispatcher?.MacroRunning == true)
            {
                // nghi vấn G1: a macro cannot answer the card, so an existing PDF is kept (the sheets are still made)
                notes.Add($"The PDF '{pdfPath}' already exists: a macro does not overwrite it. Print with plot_pdf from the chat (a card asks first).");
                pdfPath = null;
                wantPdf = false;
            }
            if (pdfPath != null && File.Exists(pdfPath))
            {
                tokens.Add(F5Confirm.FileToken(pdfPath));
                what.Add($"overwrite the existing file '{pdfPath}'");
            }
            if (tokens.Count > 0) RequireCard(ctx, p, tool, string.Join(" and ", what), tokens);

            var result = new Dictionary<string, object>();
            var record = new DrawingSetLogic.Record();
            using (doc.LockDocument())
            try
            {
                if (hadSet) RemoveSet(ctx, last, oldLayouts);

                // ---- the 2D views in model space, in a row right of everything
                var views = set.Sheets.Where(s => s.Kind == SheetKind.Elevation || s.Kind == SheetKind.Section || (s.Kind == SheetKind.Plan && s.Level >= 0)).ToList();
                var wmin = solids.Count > 0 ? new[] { solids.Min(s => s.WMin[0]), solids.Min(s => s.WMin[1]), solids.Min(s => s.WMin[2]) } : null;
                var wmax = solids.Count > 0 ? new[] { solids.Max(s => s.WMax[0]), solids.Max(s => s.WMax[1]), solids.Max(s => s.WMax[2]) } : null;
                // slots sized by what is drawn in the WCS (a plan cut of a turned house is as wide as its WCS box)
                var slotViews = views.Select(s => s.Kind == SheetKind.Plan && wmin != null
                    ? new SetSheet { Scale = s.Scale, W = Math.Max(s.W, wmax[0] - wmin[0]), H = Math.Max(s.H, wmax[1] - wmin[1]) }
                    : new SetSheet { Scale = s.Scale, W = Math.Max(s.W, s.H), H = Math.Max(s.W, s.H) }).ToList();
                double startX = (double.IsInfinity(allMaxX) ? 0 : allMaxX) + G1Numbers.ViewGapPaperMm * views.Select(v => v.Scale).DefaultIfEmpty(100).Max() / f;
                var slots = DrawingSetLogic.Slots(slotViews, sheet, f, startX, double.IsInfinity(allMinY) ? 0 : allMinY);
                var centers = new Dictionary<SetSheet, double[]>();
                var skipped = new List<object>();
                var handlesAll = solids.Select(s => (object)s.Handle).ToList();
                for (int i = 0; i < views.Count; i++)
                {
                    var s = views[i];
                    var vp = new Dictionary<string, object>
                    {
                        ["handles"] = handlesAll,
                        ["position"] = new List<object> { slots[i][0], slots[i][1] },
                    };
                    string sub;
                    if (s.Kind == SheetKind.Elevation)
                    {
                        sub = DrawingSetLogic.ViewTool;
                        vp["direction"] = VecW(DrawingSetLogic.ElevationDirection(s.Direction)).Select(v => (object)v).ToList();
                    }
                    else if (s.Kind == SheetKind.Section)
                    {
                        sub = DrawingSetLogic.CutTool;
                        var a = ToW(Ucs3(s.A, req.SolidMin[2])); var b = ToW(Ucs3(s.B, req.SolidMin[2]));
                        vp["line"] = new List<object> { new List<object> { a[0], a[1] }, new List<object> { b[0], b[1] } };
                        vp["show_behind"] = true;
                    }
                    else
                    {
                        sub = DrawingSetLogic.CutTool;
                        var cz = s.LevelZ + G1Numbers.PlanCutHeightMm / f;
                        var at = ToW(new[] { (req.SolidMin[0] + req.SolidMax[0]) / 2, (req.SolidMin[1] + req.SolidMax[1]) / 2, cz });
                        vp["point"] = at.Select(v => (object)v).ToList();
                        vp["plane"] = "xy";
                    }
                    try
                    {
                        Dictionary<string, object> r;
                        using (UcsScope.Suspend())
                        using (var tr = doc.TransactionManager.StartTransaction())
                        {
                            var c = new ToolContext(doc, tr, true, ctx.Dispatcher);
                            var sp = new Params(sub, vp);
                            r = (Dictionary<string, object>)(sub == DrawingSetLogic.ViewTool ? ProjectionView(c, sp) : SectionView(c, sp));
                            tr.Commit();
                        }
                        var pos = ((IList<object>)r["position"]).Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
                        var size = ((IList<object>)r["size"]).Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
                        centers[s] = new[] { pos[0] + size[0] / 2, pos[1] + size[1] / 2 };
                        foreach (var h in (IList<object>)r["handles"]) record.Handles.Add(Convert.ToString(h, CultureInfo.InvariantCulture));
                        if (r.TryGetValue("notes", out var nn) && nn is IList<string> ns) foreach (var n in ns.Take(3)) notes.Add(s.Title + ": " + n);
                    }
                    catch (ToolException ex)
                    {
                        skipped.Add(new Dictionary<string, object> { ["title"] = s.Title, ["reason"] = ex.Message });
                    }
                }

                // ---- the sheets
                var made = new List<object>();
                var madeLayouts = new List<string>();
                if (makeSheets)
                {
                    foreach (var s in set.Sheets)
                    {
                        double[] center;
                        if (s.Kind == SheetKind.Plan && s.Level < 0) center = ToW(Ucs3(new[] { (s.A[0] + s.B[0]) / 2, (s.A[1] + s.B[1]) / 2 }));
                        else if (s.Kind == SheetKind.Perspective) center = new double[] { 0, 0, 0 };
                        else if (!centers.TryGetValue(s, out center)) continue; // its view could not be drawn (skipped)
                        var fields = new Dictionary<string, object>(userFields) { ["title"] = s.Title, ["sheet_no"] = s.Number };
                        if (s.Kind == SheetKind.Perspective) fields["scale"] = G1Numbers.NoScaleText;
                        var cp = new Dictionary<string, object>
                        {
                            ["name"] = s.Number,
                            ["paper"] = paper,
                            ["scale"] = (double)(s.Scale > 0 ? s.Scale : G1Numbers.DefaultScale),
                            ["view_center"] = new List<object> { center[0], center[1], 0.0 },
                            ["fields"] = fields,
                            ["locked"] = true,
                        };
                        if (land.HasValue) cp["orientation"] = land.Value ? "landscape" : "portrait";
                        try
                        {
                            Dictionary<string, object> r;
                            using (UcsScope.Suspend()) r = (Dictionary<string, object>)CreateSheet(ctx, new Params(DrawingSetLogic.SheetTool, cp));
                            madeLayouts.Add(s.Number);
                            record.Layouts.Add(s.Number);
                            AdjustSheetViewport(ctx, s, (string)r["viewport_handle"], frame, wmin, wmax, solidLayers, notes);
                            var row = new Dictionary<string, object>
                            {
                                ["layout"] = s.Number, ["title"] = s.Title, ["kind"] = s.Kind.ToString().ToLowerInvariant(),
                                ["scale"] = s.Kind == SheetKind.Perspective ? G1Numbers.NoScaleText : "1:" + s.Scale.ToString(CultureInfo.InvariantCulture),
                                ["title_block_handle"] = r["title_block_handle"],
                            };
                            if (r.TryGetValue("warning", out var w)) row["warning"] = w;
                            made.Add(row);
                        }
                        catch (ToolException ex)
                        {
                            skipped.Add(new Dictionary<string, object> { ["title"] = s.Title, ["reason"] = ex.Message });
                        }
                    }
                }
                else
                {
                    foreach (var s in views.Where(centers.ContainsKey))
                        made.Add(new Dictionary<string, object> { ["title"] = s.Title, ["kind"] = s.Kind.ToString().ToLowerInvariant(), ["in_model_space_at"] = UcsLogic.OutPoint(new[] { centers[s][0], centers[s][1], 0 }).Take(2).Select(v => (object)Acad.R(v)).ToList() });
                }
                WriteSetRecord(doc, db, record);

                result["sheets"] = made;
                result["plan_source"] = set.PlanSource ?? "—";
                if (skipped.Count > 0) result["skipped"] = skipped;
                if (set.Missing.Count > 0) result["missing"] = set.Missing.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                if (hadSet) result["replaced"] = oldLayouts.Cast<object>().ToList();
                if (made.Count == 0) result["warning"] = "No view could be drawn: see skipped.";

                // ---- one PDF of the whole set
                if (wantPdf && madeLayouts.Count > 0)
                {
                    var pp = new Dictionary<string, object>
                    {
                        ["layouts"] = madeLayouts.Cast<object>().ToList(),
                        ["output"] = pdfPath,
                    };
                    if (p.Has(SolidLogic.UserConfirmedParam)) pp[SolidLogic.UserConfirmedParam] = p.Get(SolidLogic.UserConfirmedParam);
                    try
                    {
                        var r = (Dictionary<string, object>)PlotPdf(ctx, new Params(DrawingSetLogic.PdfTool, pp));
                        result["pdf"] = r.TryGetValue("output", out var o) ? o : pdfPath;
                    }
                    catch (ToolException ex)
                    {
                        result["pdf_error"] = ex.Message;
                    }
                }
            }
            catch
            {
                // nghi vấn G1: an AutoCAD error part way (after the old set was removed): what was made, and what is left of the
                // old set, stay in the record, so the next run (or its card) still knows them
                var keep = new DrawingSetLogic.Record();
                keep.Layouts.AddRange(record.Layouts.Concat(last.Layouts).Distinct(StringComparer.OrdinalIgnoreCase));
                keep.Handles.AddRange(record.Handles.Concat(last.Handles).Distinct(StringComparer.OrdinalIgnoreCase));
                try { WriteSetRecord(doc, db, keep); } catch (Exception) { }
                throw;
            }
            if (notes.Count > 0) result["notes"] = notes.Take(20).Cast<object>().ToList();
            result["next"] = "Change a title field with set_block_attributes on title_block_handle. Running create_drawing_set again replaces this set (after a card).";
            return result;
        }

        /// <summary>The viewport of one sheet: a plan turns with a turned UCS; the perspective looks south-west at the model.</summary>
        private static void AdjustSheetViewport(ToolContext ctx, SetSheet s, string vpHandle, UcsFrame frame, double[] wmin, double[] wmax, HashSet<string> solidLayers, List<string> notes)
        {
            var doc = ctx.Doc;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var c = new ToolContext(doc, tr, true, ctx.Dispatcher);
                if (!(Acad.GetEntity(c, vpHandle, true) is Viewport vp)) { tr.Abort(); return; }
                var lt = (LayerTable)tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
                var freeze = new List<ObjectId>();
                if (s.Kind == SheetKind.Perspective && wmin != null)
                {
                    var dirU = DrawingSetLogic.PerspectiveDirection;
                    var dir = frame == null ? dirU : frame.VecToWcs(dirU);
                    vp.Locked = false;
                    vp.ViewDirection = new Vector3d(dir[0], dir[1], dir[2]);
                    vp.ViewTarget = new Point3d((wmin[0] + wmax[0]) / 2, (wmin[1] + wmax[1]) / 2, (wmin[2] + wmax[2]) / 2);
                    vp.ViewCenter = new Point2d(0, 0);
                    if (frame != null && Math.Abs(frame.PlanAngleDeg) > 1e-9) vp.TwistAngle = 0;
                    vp.CustomScale = DrawingSetLogic.FitCustomScale(DrawingSetLogic.Corners(wmin, wmax), dir, vp.Width, vp.Height);
                    try
                    {
                        var vs = (DBDictionary)tr.GetObject(ctx.Db.VisualStyleDictionaryId, OpenMode.ForRead);
                        if (vs.Contains(G1Numbers.PerspectiveVisualStyle))
                        {
                            vp.VisualStyleId = vs.GetAt(G1Numbers.PerspectiveVisualStyle);
                            vp.ShadePlot = ShadePlotType.AsDisplayed;
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) { notes.Add($"{s.Title}: visual style not set ({ex.ErrorStatus})."); }
                    foreach (ObjectId id in lt)
                    {
                        var l = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (!solidLayers.Contains(l.Name) && l.Name != "0") freeze.Add(id);
                    }
                    vp.Locked = true;
                }
                else if (s.Kind == SheetKind.Plan)
                {
                    double turn = frame != null ? frame.PlanAngleDeg * Math.PI / 180 : 0;
                    if (Math.Abs(turn) > 1e-12)
                    {
                        bool locked = vp.Locked;
                        vp.Locked = false;
                        var wc = vp.ViewCenter; // the WCS centre create_sheet set (no twist yet)
                        vp.TwistAngle = (2 * Math.PI - turn) % (2 * Math.PI);
                        var dcs = new Point3d(wc.X, wc.Y, 0).TransformBy(Matrix3d.Rotation(-turn, Vector3d.ZAxis, Point3d.Origin));
                        vp.ViewCenter = new Point2d(dcs.X, dcs.Y);
                        vp.Locked = locked;
                    }
                    // the 2D plan: the solids built over it stay out of its sheet
                    if (s.Level < 0)
                        foreach (var n in solidLayers) if (lt.Has(n) && n != "0") freeze.Add(lt[n]);
                }
                if (freeze.Count > 0)
                {
                    try { vp.FreezeLayersInViewport(freeze.GetEnumerator()); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) { notes.Add($"{s.Title}: layers not frozen in the viewport ({ex.ErrorStatus})."); }
                }
                tr.Commit();
            }
        }

        /// <summary>Deletes the layouts and erases the views of the set made before (only what is still there).</summary>
        private static void RemoveSet(ToolContext ctx, DrawingSetLogic.Record last, List<string> oldLayouts)
        {
            var lm = LayoutManager.Current;
            if (oldLayouts.Contains(lm.CurrentLayout, StringComparer.OrdinalIgnoreCase)) lm.CurrentLayout = "Model";
            // AutoCAD keeps at least one paper layout: when the old set is all there is, AutoCAD's default one stays behind
            bool others;
            using (var tr0 = ctx.Doc.TransactionManager.StartOpenCloseTransaction())
            {
                var dict = (DBDictionary)tr0.GetObject(ctx.Db.LayoutDictionaryId, OpenMode.ForRead);
                others = dict.Cast<DBDictionaryEntry>().Any(e => !e.Key.Equals("Model", StringComparison.OrdinalIgnoreCase) && !oldLayouts.Contains(e.Key, StringComparer.OrdinalIgnoreCase));
                tr0.Commit();
            }
            if (!others && oldLayouts.Count > 0 && !lm.LayoutExists("Layout1")) lm.CreateLayout("Layout1");
            foreach (var l in oldLayouts) if (lm.LayoutExists(l)) lm.DeleteLayout(l);
            using (var tr = ctx.Doc.TransactionManager.StartTransaction())
            {
                foreach (var h in last.Handles)
                {
                    try
                    {
                        var id = ctx.Db.GetObjectId(false, new Handle(Convert.ToInt64(h, 16)), 0);
                        if (id.IsNull || id.IsErased) continue;
                        if (tr.GetObject(id, OpenMode.ForWrite) is Entity e && e.OwnerId == SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db)) e.Erase();
                    }
                    catch (Exception) { /* gone, or not a handle any more */ }
                }
                tr.Commit();
            }
        }

        private static DrawingSetLogic.Record ReadSetRecord(Transaction tr, Database db)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(DrawingSetLogic.RecordKey)) return new DrawingSetLogic.Record();
            if (!(tr.GetObject(nod.GetAt(DrawingSetLogic.RecordKey), OpenMode.ForRead) is Xrecord xr) || xr.Data == null) return new DrawingSetLogic.Record();
            return DrawingSetLogic.Decode(xr.Data.AsArray().Select(tv => tv.Value as string));
        }

        private static void WriteSetRecord(Document doc, Database db, DrawingSetLogic.Record r)
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                // KFG-24: keep each sheet's layout handle, so a sheet the user renamed (or a new one with the old name) is told apart
                var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                r.LayoutHandles.Clear();
                foreach (var name in r.Layouts) r.LayoutHandles.Add(layouts.Contains(name) ? Acad.H(layouts.GetAt(name)) : "");
                var rb = new ResultBuffer(DrawingSetLogic.Encode(r).Select(s => new TypedValue((int)DxfCode.Text, s)).ToArray());
                if (nod.Contains(DrawingSetLogic.RecordKey))
                {
                    var xr = (Xrecord)tr.GetObject(nod.GetAt(DrawingSetLogic.RecordKey), OpenMode.ForWrite);
                    xr.Data = rb;
                }
                else
                {
                    var xr = new Xrecord { Data = rb };
                    nod.SetAt(DrawingSetLogic.RecordKey, xr);
                    tr.AddNewlyCreatedDBObject(xr, true);
                }
                tr.Commit();
            }
        }
    }
}
