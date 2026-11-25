using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt G2 (docs/ban-giao/nang-cap-g2.md): check_drawing reads the whole drawing (model and layouts, at most
    /// G2Numbers.MaxScan objects) and returns the problems by group, each with its level, count and a few handles;
    /// fix_drawing repairs one group that can be repaired safely, only after the Đồng ý of a card (one token for that fix on
    /// exactly those objects), in the turn's undo group. The groups and numbers are in Core/DrawingCheck.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterDrawingCheck(ToolDispatcher d)
        {
            d.Register(new ToolDef("check_drawing", false, CheckDrawing));
            d.Register(new ToolDef("fix_drawing", true, FixDrawing));
        }

        private sealed class CheckScan
        {
            public readonly Dictionary<string, List<string>> Hits = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            public readonly Dictionary<string, SortedSet<string>> Layers = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            public readonly Dictionary<string, SortedSet<string>> Layouts = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            public readonly List<string> Notes = new List<string>();
            public string Prefix;
            public int? Scale;
            public double MmPerUnit = 1;
            public string Units;
            public int Scanned, Total, UntouchedLayouts;
            /// <summary>Sửa KFG-08 (lần 2): the default layouts with only viewports, named in the note (not checked as sheets).</summary>
            public readonly List<string> UnusedTabs = new List<string>();

            public void Hit(string group, ObjectId id, string layer = null, string layout = null)
            {
                if (!Hits.TryGetValue(group, out var l)) Hits[group] = l = new List<string>();
                l.Add(Acad.H(id));
                if (layer != null) { if (!Layers.TryGetValue(group, out var s)) Layers[group] = s = new SortedSet<string>(StringComparer.OrdinalIgnoreCase); s.Add(layer); }
                if (layout != null) { if (!Layouts.TryGetValue(group, out var s)) Layouts[group] = s = new SortedSet<string>(StringComparer.OrdinalIgnoreCase); s.Add(layout); }
            }

            public List<string> Of(string group) => Hits.TryGetValue(group, out var l) ? l : new List<string>();
        }

        private static string NameOf(ToolContext ctx, ObjectId id)
        {
            if (id.IsNull) return "";
            try
            {
                switch (ctx.Tr.GetObject(id, OpenMode.ForRead))
                {
                    case SymbolTableRecord r: return r.Name;
                    case MLeaderStyle m: return m.Name;
                    default: return "";
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return ""; }
        }

        /// <summary>Sửa KFG-07: the mark draw_hatch puts on a hatch it makes without a boundary on purpose (points, pick point without keep).</summary>
        internal const string PlainHatchKey = "CLAUDECAD_PLAIN_HATCH";

        internal static void MarkPlainHatch(ToolContext ctx, Hatch h)
        {
            if (h.Associative) return;
            if (h.ExtensionDictionary.IsNull) h.CreateExtensionDictionary();
            var ed = (DBDictionary)ctx.Tr.GetObject(h.ExtensionDictionary, OpenMode.ForWrite);
            if (ed.Contains(PlainHatchKey)) return;
            var xr = new Xrecord { Data = new ResultBuffer(new TypedValue((int)DxfCode.Int16, (short)1)) };
            ed.SetAt(PlainHatchKey, xr);
            ctx.Tr.AddNewlyCreatedDBObject(xr, true);
        }

        /// <summary>
        /// Sửa KFG-32: the mark of a line section_view / projection_view drew (XData, no extra object per line), so check_drawing
        /// never calls two edges of solids side by side a copy and fix_drawing never erases a line of the view, on any layer.
        /// </summary>
        internal const string ViewLineApp = "CLAUDECAD_VIEW";

        internal static void MarkViewLine(ToolContext ctx, Entity obj)
        {
            var rat = (RegAppTable)ctx.Tr.GetObject(ctx.Db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(ViewLineApp))
            {
                rat.UpgradeOpen();
                var r = new RegAppTableRecord { Name = ViewLineApp };
                rat.Add(r);
                ctx.Tr.AddNewlyCreatedDBObject(r, true);
            }
            if (!obj.IsWriteEnabled) obj.UpgradeOpen();
            obj.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, ViewLineApp), new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)1));
        }

        private static bool IsViewLine(Entity e)
        {
            try { using (var rb = e.GetXDataForApplication(ViewLineApp)) return rb != null; }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return false; }
        }

        private static bool IsPlainHatch(ToolContext ctx, Hatch h)
        {
            if (h.ExtensionDictionary.IsNull) return false;
            try { return ((DBDictionary)ctx.Tr.GetObject(h.ExtensionDictionary, OpenMode.ForRead)).Contains(PlainHatchKey); }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return false; }
        }

        private static string Txt(ToolContext ctx, Entity e) => e is AttributeDefinition ? null : e is DBText t ? t.TextString : e is MText m ? m.Text : null;

        /// <summary>The shape key of an object for "trùng" (null = not compared).</summary>
        /// <summary>
        /// Sửa KFG-15: the copy check of an object (null = not compared): lengths compared within the tolerance, angles / scales /
        /// bulges / texts exactly (rounded). Sửa KFG (nghi vấn G1 × G2): lines of the views drawn from the 3D model (A-SECT…,
        /// A-ELEV) are not compared: two solids side by side give the same edge twice, and erasing it would cut the drawing set.
        /// </summary>
        private static DrawingCheckLogic.DupItem DupItemOf(Entity e, string space)
        {
            if (Util2DLogic.IsDerivedViewLayer(e.Layer) || IsViewLine(e)) return null; // Sửa KFG-32: also on a layer of the user's choice
            double[] P(Autodesk.AutoCAD.Geometry.Point3d q) => new[] { q.X, q.Y, q.Z };
            string g = space + "|" + (e.Layer ?? "").ToUpperInvariant() + "|";
            DrawingCheckLogic.DupItem I(string kind, IEnumerable<double> nums, string exact = "") => new DrawingCheckLogic.DupItem { Group = g + kind + "|" + exact, Nums = nums.ToArray() };
            switch (e)
            {
                case Line l: return I("line", DrawingCheckLogic.LineNumbers(P(l.StartPoint), P(l.EndPoint)));
                case Arc a: return I("arc", new[] { a.Center.X, a.Center.Y, a.Center.Z, a.Radius }, DrawingCheckLogic.Exact(a.StartAngle, a.EndAngle));
                case Circle c: return I("circle", new[] { c.Center.X, c.Center.Y, c.Center.Z, c.Radius });
                case DBPoint pt: return I("point", P(pt.Position));
                case Polyline pl:
                {
                    var verts = new List<double[]>();
                    var bulges = new List<double>();
                    for (int i = 0; i < pl.NumberOfVertices; i++) { var v = pl.GetPoint2dAt(i); verts.Add(new[] { v.X, v.Y }); bulges.Add(pl.GetBulgeAt(i)); }
                    var nums = DrawingCheckLogic.PolylineNumbers(verts, bulges, pl.Closed); // x, y, bulge, x, y, bulge…
                    var xy = nums.Where((_, i) => i % 3 != 2).Concat(new[] { pl.Elevation });
                    var bs = nums.Where((_, i) => i % 3 == 2).ToArray();
                    return I("pline", xy, (pl.Closed ? "c" : "o") + DrawingCheckLogic.Exact(bs));
                }
                case DBText t: return I("text", P(t.Position).Concat(new[] { t.Height }), DrawingCheckLogic.Exact(t.Rotation) + "|" + t.TextString);
                case MText m: return I("mtext", P(m.Location).Concat(new[] { m.TextHeight }), DrawingCheckLogic.Exact(m.Rotation) + "|" + m.Contents);
                case BlockReference br:
                    return I("insert", P(br.Position), DrawingCheckLogic.Exact(br.Rotation, br.ScaleFactors.X, br.ScaleFactors.Y, br.ScaleFactors.Z) + "|" + br.BlockTableRecord.Handle);
                default: return null;
            }
        }

        /// <summary>Length of a curve (lines, polylines, arcs; a circle: its radius), or null when not a "dài 0" candidate.</summary>
        private static double? CurveSize(Entity e)
        {
            try
            {
                switch (e)
                {
                    case Line l: return l.Length;
                    case Arc a: return a.Length;
                    case Circle c: return c.Radius;
                    case Polyline pl: return pl.Length;
                    default: return null;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }
        }

        private static bool ImageFileFound(RasterImageDef def)
        {
            try
            {
                var f = def.ActiveFileName;
                if (string.IsNullOrEmpty(f)) f = def.SourceFileName;
                return !string.IsNullOrEmpty(f) && System.IO.File.Exists(f);
            }
            catch (Exception) { return false; }
        }

        private static bool Wanted(HashSet<string> only, params string[] groups) => only == null || groups.Any(only.Contains);

        /// <summary>The whole scan (only = the groups wanted; null = all).</summary>
        private static CheckScan ScanDrawing(ToolContext ctx, HashSet<string> only, bool layouts = true, ICollection<string> sheets = null)
        {
            var s = new CheckScan();
            s.MmPerUnit = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out s.Units);
            var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
            var styleNames = new List<string>();
            foreach (ObjectId id in tst) styleNames.Add(NameOf(ctx, id));
            s.Prefix = DrawingCheckLogic.StandardPrefix(styleNames);
            if (s.Prefix != null)
            {
                s.Scale = DrawingCheckLogic.DimStyleScale(s.Prefix, NameOf(ctx, ctx.Db.Dimstyle));
                if (s.Scale == null)
                {
                    var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in dst) { s.Scale = DrawingCheckLogic.DimStyleScale(s.Prefix, NameOf(ctx, id)); if (s.Scale != null) break; }
                }
            }
            double[] heights = s.Prefix != null && s.Scale != null ? DrawingCheckLogic.StandardHeights(s.Scale.Value, s.MmPerUnit) : null;
            double dupTol = G2Numbers.DuplicateTolMm / s.MmPerUnit, zeroTol = G2Numbers.ZeroLengthMm / s.MmPerUnit;

            var layerCache = new Dictionary<ObjectId, LayerTableRecord>();
            LayerTableRecord Layer(Entity e)
            {
                if (!layerCache.TryGetValue(e.LayerId, out var r)) layerCache[e.LayerId] = r = (LayerTableRecord)ctx.Tr.GetObject(e.LayerId, OpenMode.ForRead);
                return r;
            }

            // the spaces: model first, then each layout
            var spaces = new List<(BlockTableRecord btr, string layout, ObjectId layoutId)> { (ctx.ModelSpace, null, ObjectId.Null) };
            if (layouts)
            {
                var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry de in dict)
                {
                    var lo = (Layout)ctx.Tr.GetObject(de.Value, OpenMode.ForRead);
                    if (lo.ModelType) continue;
                    spaces.Add(((BlockTableRecord)ctx.Tr.GetObject(lo.BlockTableRecordId, OpenMode.ForRead), lo.LayoutName, lo.ObjectId));
                }
            }
            foreach (var sp in spaces) foreach (ObjectId _ in sp.btr) s.Total++;

            var dupKeys = new List<DrawingCheckLogic.DupItem>();
            var dupIds = new List<ObjectId>();
            var boxes = new List<(double[] min, double[] max)>();
            var boxIds = new List<ObjectId>();
            int walls = 0, dims = 0;
            var wallIds = new List<ObjectId>();
            bool stopped = false;

            foreach (var sp in spaces)
            {
                bool model = sp.layout == null;
                bool firstViewport = true, hasTitle = false;
                int others = 0, ownViewports = 0;
                // Sửa KFG (nghi vấn G2): the sheet's own viewport is the first of Layout.GetViewports when AutoCAD has it, else the first in the block
                ObjectId sheetVp = ObjectId.Null;
                if (!model)
                    try { var vps = ((Layout)ctx.Tr.GetObject(sp.layoutId, OpenMode.ForRead)).GetViewports(); if (vps != null && vps.Count > 0) sheetVp = vps[0]; }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { }
                var vpUnlocked = new List<ObjectId>();
                var vpScale = new List<ObjectId>();
                string spaceKey = model ? "M" : sp.btr.Handle.ToString();
                foreach (ObjectId id in sp.btr)
                {
                    if (stopped) break;
                    if (s.Scanned >= G2Numbers.MaxScan) { stopped = true; break; }
                    if (id.IsErased) continue;
                    if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                    s.Scanned++;
                    var ltr = Layer(e);
                    string layer = ltr.Name;

                    if (e is Viewport vp)
                    {
                        if (model) continue;
                        bool isSheet = sheetVp.IsNull ? firstViewport : id == sheetVp;
                        firstViewport = false;
                        if (isSheet) continue; // the sheet's own viewport
                        ownViewports++;
                        if (Wanted(only, "viewport_unlocked") && !vp.Locked) vpUnlocked.Add(id);
                        // test tổng thể G: a 3D viewport (the perspective sheet of create_drawing_set) has no print scale to check
                        if (Wanted(only, "viewport_scale") && DrawingCheckLogic.IsPlanView(vp.ViewDirection.X, vp.ViewDirection.Y, vp.ViewDirection.Z)
                            && !DrawingCheckLogic.StandardViewportScale(DrawingCheckLogic.ViewportDenominator(vp.CustomScale, s.MmPerUnit)))
                            vpScale.Add(id);
                        continue;
                    }
                    if (!model)
                    {
                        others++;
                        if (e is BlockReference || layer.StartsWith(F2Numbers.FrameLayer, StringComparison.OrdinalIgnoreCase)) hasTitle = true;
                    }

                    // layers
                    if (layer == "0") { if (Wanted(only, "layer_zero")) s.Hit("layer_zero", id, null, sp.layout); }
                    else if (string.Equals(layer, "Defpoints", StringComparison.OrdinalIgnoreCase)) { if (Wanted(only, "defpoints")) s.Hit("defpoints", id, null, sp.layout); }
                    // Test & sửa G5: the background picture of trace_image is not plotted on purpose
                    if ((ltr.IsOff || ltr.IsFrozen || !ltr.IsPlottable) && !string.Equals(layer, "Defpoints", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(layer, G5Numbers.Layer, StringComparison.OrdinalIgnoreCase) && Wanted(only, "hidden_layers"))
                        s.Hit("hidden_layers", id, layer);
                    if (s.Prefix != null && !DrawingCheckLogic.IsStandardLayer(layer) && Wanted(only, "nonstandard_layers")) s.Hit("nonstandard_layers", id, layer);

                    // properties (the standard sheet frame sets its own line weight on purpose)
                    if (Wanted(only, "not_bylayer") && !layer.StartsWith(F2Numbers.FrameLayer, StringComparison.OrdinalIgnoreCase)
                        && (!e.Color.IsByLayer || !string.Equals(e.Linetype, "ByLayer", StringComparison.OrdinalIgnoreCase) || e.LineWeight != LineWeight.ByLayer))
                        s.Hit("not_bylayer", id);

                    // text and dimensions
                    var txt = Txt(ctx, e);
                    if (txt != null)
                    {
                        if (string.IsNullOrWhiteSpace(txt)) { if (Wanted(only, "empty_text")) s.Hit("empty_text", id); }
                        else if (s.Prefix != null)
                        {
                            var style = NameOf(ctx, e is DBText t0 ? t0.TextStyleId : ((MText)e).TextStyleId);
                            if (!DrawingCheckLogic.IsStandardTextStyle(s.Prefix, style)) { if (Wanted(only, "nonstandard_styles")) s.Hit("nonstandard_styles", id); }
                            else if (model && heights != null && Wanted(only, "text_height")
                                     && DrawingCheckLogic.HeightOff(e is DBText t1 ? t1.Height : ((MText)e).TextHeight, heights, out _))
                                s.Hit("text_height", id);
                        }
                    }
                    if (e is Dimension dm)
                    {
                        dims++; // Sửa KFG-16: a dimension on a layout counts too
                        if (Wanted(only, "dim_override") && DrawingCheckLogic.HidesMeasurement(dm.DimensionText)) s.Hit("dim_override", id);
                        if (s.Prefix != null && Wanted(only, "nonstandard_styles") && !DrawingCheckLogic.IsStandardDimStyle(s.Prefix, NameOf(ctx, dm.DimensionStyle)))
                            s.Hit("nonstandard_styles", id);
                    }
                    if (e is MLeader ml && s.Prefix != null && Wanted(only, "nonstandard_styles") && !MLeaderStyleLogic.IsStandardName(NameOf(ctx, ml.MLeaderStyle)))
                        s.Hit("nonstandard_styles", id);
                    if (model && DrawingCheckLogic.IsWallLayer(layer) && e is Curve) { walls++; if (wallIds.Count < G2Numbers.ExamplesPerGroup) wallIds.Add(id); }

                    // geometry
                    // a zero-length object is never counted as a copy (the same answer whichever groups are asked)
                    if (CurveSize(e) is double len && len < zeroTol) { if (Wanted(only, "zero_length")) s.Hit("zero_length", id); }
                    else if (Wanted(only, "duplicates")) { var k = DupItemOf(e, spaceKey); if (k != null) { dupKeys.Add(k); dupIds.Add(id); } }
                    if (model && Wanted(only, "far_away") && !(e is Xline) && !(e is Ray))
                    {
                        try { var ex = e.GeometricExtents; boxes.Add((new[] { ex.MinPoint.X, ex.MinPoint.Y }, new[] { ex.MaxPoint.X, ex.MaxPoint.Y })); boxIds.Add(id); }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { }
                    }
                    if (e is Hatch h)
                    {
                        bool broken;
                        try { broken = h.NumberOfLoops == 0; } catch (Autodesk.AutoCAD.Runtime.Exception) { broken = true; }
                        if (broken) { if (Wanted(only, "hatch_broken")) s.Hit("hatch_broken", id); }
                        // Sửa KFG-07: the hatches of the views from the model (A-SECT-PAT) and those drawn without a boundary on purpose
                        // (draw_hatch by points / pick point) never had one to lose
                        else if (!h.Associative && !Util2DLogic.IsDerivedViewLayer(layer) && !IsPlainHatch(ctx, h) && Wanted(only, "hatch_unassociated"))
                            s.Hit("hatch_unassociated", id);
                    }

                    // blocks and references
                    if (e is BlockReference br)
                    {
                        var btr = (BlockTableRecord)ctx.Tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        if (btr.IsFromExternalReference && btr.XrefStatus != XrefStatus.Resolved)
                        {
                            // G2T-3: unloaded on purpose is not "mất file"
                            if (Wanted(only, "missing_refs") && DrawingCheckLogic.XrefMissing(btr.XrefStatus.ToString())) s.Hit("missing_refs", id);
                        }
                        else
                        {
                            string bname = br.IsDynamicBlock ? NameOf(ctx, br.DynamicBlockTableRecord) : btr.Name;
                            if (DrawingCheckLogic.IsUnnamedBlock(bname) && Wanted(only, "unnamed_blocks")) s.Hit("unnamed_blocks", id);
                            string attrGroup = model ? "empty_attributes" : "title_empty";
                            if (Wanted(only, attrGroup))
                            {
                                foreach (ObjectId aid in br.AttributeCollection)
                                {
                                    if (aid.IsErased || !(ctx.Tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)) continue;
                                    if (!ar.Invisible && !ar.IsConstant && string.IsNullOrWhiteSpace(ar.TextString)) { s.Hit(attrGroup, id, null, sp.layout); break; }
                                }
                            }
                        }
                    }
                    if (e is RasterImage img && Wanted(only, "missing_refs"))
                    {
                        bool missing;
                        // G2T-3: an image the user unloaded whose file is still there is not "mất file"
                        try
                        {
                            var def = ctx.Tr.GetObject(img.ImageDefId, OpenMode.ForRead) as RasterImageDef;
                            missing = def == null || (!def.IsLoaded && !ImageFileFound(def));
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { missing = true; }
                        if (missing) s.Hit("missing_refs", id);
                    }
                }
                if (model) continue;
                // an untouched default layout (Layout1 with only AutoCAD's own viewport) is not a sheet of the user's
                // Sửa KFG-08: a viewport the user put there makes it a sheet, whatever its name
                // Sửa KFG-08 (lần 2, luồng chính chọn): a default layout with nothing but viewports (AutoCAD makes one when the tab
                // is first opened) is an unused tab: named in a note, not reported; any other object, or the user naming it in
                // 'sheets', makes it a sheet checked in full
                bool asked = sheets != null && sheets.Contains(sp.layout, StringComparer.OrdinalIgnoreCase);
                if (DrawingCheckLogic.IsUnusedTab(sp.layout, others, asked))
                {
                    s.UntouchedLayouts++;
                    if (ownViewports > 0) s.UnusedTabs.Add(sp.layout);
                    continue;
                }
                foreach (var v in vpUnlocked) s.Hit("viewport_unlocked", v, null, sp.layout);
                foreach (var v in vpScale) s.Hit("viewport_scale", v, null, sp.layout);
                if (!stopped && (others > 0 || ownViewports > 0) && !hasTitle && Wanted(only, "no_title_block"))
                    s.Hit("no_title_block", sp.layoutId, null, sp.layout);
            }

            if (Wanted(only, "duplicates"))
                foreach (int i in DrawingCheckLogic.DuplicateIndices(dupKeys, dupTol)) s.Hit("duplicates", dupIds[i]);
            if (Wanted(only, "far_away"))
                foreach (int i in DrawingCheckLogic.FarAway(boxes, s.MmPerUnit)) s.Hit("far_away", boxIds[i]);
            if (Wanted(only, "no_dimensions") && walls > 0 && dims == 0 && !stopped)
                foreach (var w in wallIds) s.Hit("no_dimensions", w);

            if (s.Prefix == null)
                s.Notes.Add("The drawing does not use the standard of setup_standards (no KT-ARIAL / VN-ARIAL text style): layer names, styles and text heights were not checked; its own layers and styles are not errors.");
            else if (s.Scale == null)
                s.Notes.Add($"No dimension style {s.Prefix}-<scale> in the drawing: the text heights were not checked.");
            if (s.UnusedTabs.Count > 0)
                s.Notes.Add(DrawingCheckLogic.UnusedTabsNote(s.UnusedTabs));
            else if (s.UntouchedLayouts > 0) s.Notes.Add($"{s.UntouchedLayouts} empty default layout(s) (Layout1…) were not checked as sheets.");
            if (stopped) s.Notes.Add($"Only the first {G2Numbers.MaxScan} objects were read ({s.Scanned} of {s.Total}); groups that need the whole drawing (no_dimensions, title blocks of the later layouts) may be incomplete.");
            return s;
        }

        internal static object CheckDrawing(ToolContext ctx, Params p)
        {
            HashSet<string> only = null;
            var groupsAsked = p.StringsOpt("groups");
            if (groupsAsked != null)
            {
                only = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in groupsAsked)
                {
                    var def = DrawingCheckLogic.Find(g) ?? throw ToolException.Invalid($"check_drawing: unknown group '{g}' ({string.Join(", ", DrawingCheckLogic.Groups.Select(x => x.Id))}).");
                    only.Add(def.Id);
                }
            }
            int examples = p.Int("examples", G2Numbers.ExamplesPerGroup, 1, 50);
            var s = ScanDrawing(ctx, only, p.Bool("layouts", true), p.StringsOpt("sheets"));

            var groups = new List<Dictionary<string, object>>();
            var summary = new Dictionary<string, object> { ["nen_sua"] = 0, ["can_nhac"] = 0, ["goi_y"] = 0 };
            foreach (var def in DrawingCheckLogic.Groups)
            {
                var hs = s.Of(def.Id);
                if (hs.Count == 0) continue;
                int count = def.Id == "no_dimensions" ? 1 : hs.Count;
                var g = new Dictionary<string, object>
                {
                    ["id"] = def.Id,
                    ["name"] = def.Name,
                    ["level"] = def.Level,
                    ["count"] = count,
                    ["examples"] = hs.Take(examples).Cast<object>().ToList(),
                    ["fix"] = def.Fixable ? def.Id : null,
                    ["hint"] = def.Hint,
                };
                if (s.Layers.TryGetValue(def.Id, out var ls)) g["layers"] = ls.Take(30).Cast<object>().ToList();
                if (s.Layouts.TryGetValue(def.Id, out var lo)) g["layouts"] = lo.Take(30).Cast<object>().ToList();
                if (def.Id == "text_height" && s.Scale != null)
                    g["standard_heights"] = DrawingCheckLogic.StandardHeights(s.Scale.Value, s.MmPerUnit).Select(v => (object)Acad.R(v)).ToList();
                groups.Add(g);
                string key = DrawingCheckLogic.LevelKey(def.Level);
                summary[key] = (int)summary[key] + 1;
            }
            var res = new Dictionary<string, object>
            {
                ["scanned"] = s.Scanned,
                ["total"] = s.Total,
                ["complete"] = s.Scanned >= s.Total,
                ["units"] = s.Units,
                ["standard"] = s.Prefix,
                ["groups"] = groups,
                ["summary"] = summary,
            };
            if (s.Scale != null) res["scale"] = "1:" + s.Scale.Value;
            if (s.Notes.Count > 0) res["notes"] = s.Notes.Cast<object>().ToList();
            res["next"] = groups.Count == 0
                ? "No problem found. Tell the user in one sentence (what was checked, scanned N of M)."
                : "Nothing was changed. Summarise for the user by level (nên sửa first): the group name, count, one example (zoom_to its handle if they want to see it). " +
                  "For a group with fix, offer to repair it: call fix_drawing with that fix (layer_zero / defpoints also need the layer the user chooses); it asks for the card itself.";
            if (p.Has("excel"))
            {
                if (groups.Count == 0) res["excel_note"] = "No problem found, so no Excel file was written.";
                else ExportExcel(ctx, p, "check_drawing", () => DrawingCheckLogic.ExcelRows(groups), res);
            }
            return res;
        }

        internal static object FixDrawing(ToolContext ctx, Params p)
        {
            string fix = p.Str("fix").Trim().ToLowerInvariant();
            var def = DrawingCheckLogic.Find(fix);
            if (def == null || !def.Fixable)
                throw ToolException.Invalid($"fix_drawing: 'fix' must be one of {string.Join(", ", DrawingCheckLogic.FixableIds)}; the other groups are only reported.");
            string layer = p.Str("layer", null)?.Trim();
            var err = DrawingCheckLogic.TargetLayerError(fix, layer);
            if (err != null) throw ToolException.Invalid(err);
            ObjectId targetLayer = ObjectId.Null;
            if (layer != null && (fix == "layer_zero" || fix == "defpoints"))
            {
                targetLayer = Acad.LayerId(ctx, layer);
                var tl = (LayerTableRecord)ctx.Tr.GetObject(targetLayer, OpenMode.ForRead);
                if (tl.IsLocked) throw new ToolException(ErrorCodes.Locked, $"fix_drawing: layer '{tl.Name}' is locked. Ask the user before unlocking it, or choose another layer.");
                // Sửa KFG-18: never onto a layer that does not print (off, frozen, not plotted: X-ANH-GOC…)
                var stateErr = DrawingCheckLogic.TargetLayerStateError(fix, tl.Name, tl.IsOff, tl.IsFrozen, tl.IsPlottable);
                if (stateErr != null) throw ToolException.Invalid(stateErr);
                layer = tl.Name;
            }
            else layer = null;

            // Sửa KFG-17: the same spaces as the check the user saw (layouts: false = model only)
            var s = ScanDrawing(ctx, new HashSet<string>(StringComparer.Ordinal) { fix }, p.Bool("layouts", true), p.StringsOpt("sheets"));
            var found = s.Of(fix);
            if (p.Has("handles"))
            {
                var asked = new HashSet<string>(p.Handles("handles", G2Numbers.MaxFix), StringComparer.OrdinalIgnoreCase);
                found = found.Where(asked.Contains).ToList();
            }

            // what each object becomes (styles: only where the standard style exists), objects on locked layers left out
            ObjectId textStyle = ObjectId.Null, dimStyle = ObjectId.Null, mlStyle = ObjectId.Null;
            if (fix == "nonstandard_styles")
            {
                var tst = (TextStyleTable)ctx.Tr.GetObject(ctx.Db.TextStyleTableId, OpenMode.ForRead);
                string tsName = StandardsLogic.TextStyleName(s.Prefix);
                if (tst.Has(tsName)) textStyle = tst[tsName];
                if (s.Scale != null)
                {
                    var dst = (DimStyleTable)ctx.Tr.GetObject(ctx.Db.DimStyleTableId, OpenMode.ForRead);
                    string dsName = StandardsLogic.DimStyleName(s.Prefix, s.Scale.Value, s.MmPerUnit);
                    if (dst.Has(dsName)) dimStyle = dst[dsName];
                    var mld = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    string mlName = MLeaderStyleLogic.Name(s.Prefix, s.Scale.Value, s.MmPerUnit);
                    if (mld.Contains(mlName)) mlStyle = mld.GetAt(mlName);
                }
            }
            var plan = new List<Entity>();
            var locked = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            int lockedCount = 0, noStyle = 0;
            foreach (var h in found)
            {
                var e = Acad.GetEntity(ctx, h, false);
                var ltr = (LayerTableRecord)ctx.Tr.GetObject(e.LayerId, OpenMode.ForRead);
                if (ltr.IsLocked) { locked.Add(ltr.Name); lockedCount++; continue; }
                if (fix == "nonstandard_styles")
                {
                    bool ok = (e is DBText || e is MText) ? !textStyle.IsNull : e is Dimension ? !dimStyle.IsNull : e is MLeader ? !mlStyle.IsNull : false;
                    if (!ok) { noStyle++; continue; }
                }
                plan.Add(e);
                if (plan.Count >= G2Numbers.MaxFix) break;
            }
            var res = new Dictionary<string, object> { ["fix"] = fix };
            // G2T-4: a drawing larger than the scan: say so, never cut silently
            if (s.Scanned < s.Total)
                res["scanned_note"] = $"Only the first {s.Scanned} of {s.Total} objects were looked at; objects of this group beyond them are not in this fix. Run check_drawing again afterwards.";
            if (lockedCount > 0) res["locked"] = new Dictionary<string, object> { ["count"] = lockedCount, ["layers"] = locked.Take(20).Cast<object>().ToList(), ["note"] = "Left unchanged (locked layers). Ask the user before unlocking." };
            if (noStyle > 0) res["no_standard_style"] = $"{noStyle} object(s) kept their style: the matching style of the standard is not in the drawing (run setup_standards first).";
            if (plan.Count == 0)
            {
                res["fixed"] = 0;
                res["note"] = "Nothing to fix (the group is empty now, or every object was left out above).";
                return res;
            }
            var handles = plan.Select(e => e.Handle.ToString()).ToList();
            string what = $"repair '{def.Name}' on {plan.Count} object(s)" + (layer != null ? $" (move them to layer '{layer}')" : "");
            RequireCard(ctx, p, "fix_drawing", what, new[] { DrawingCheckLogic.FixToken(fix, layer, handles) });

            ctx.RequireWritable("fix the drawing");
            foreach (var e in plan)
            {
                e.UpgradeOpen();
                switch (fix)
                {
                    case "layer_zero":
                    case "defpoints": e.LayerId = targetLayer; break;
                    case "not_bylayer":
                        e.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                        e.Linetype = "ByLayer";
                        e.LineWeight = LineWeight.ByLayer;
                        break;
                    case "nonstandard_styles":
                        if (e is DBText t) t.TextStyleId = textStyle;
                        else if (e is MText m) m.TextStyleId = textStyle;
                        else if (e is Dimension d) d.DimensionStyle = dimStyle;
                        else if (e is MLeader ml) ml.MLeaderStyle = mlStyle;
                        break;
                    case "dim_override": ((Dimension)e).DimensionText = ""; break;
                    case "viewport_unlocked": ((Viewport)e).Locked = true; break;
                    case "empty_text":
                    case "duplicates":
                    case "zero_length": e.Erase(); break;
                }
            }
            res["fixed"] = plan.Count;
            res["handles"] = handles.Take(50).Cast<object>().ToList();
            if (found.Count > plan.Count + lockedCount + noStyle) res["more"] = $"Only {G2Numbers.MaxFix} objects per call: call fix_drawing again for the rest (a new card).";
            res["undo"] = "One U undoes the whole turn.";
            return res;
        }
    }
}
