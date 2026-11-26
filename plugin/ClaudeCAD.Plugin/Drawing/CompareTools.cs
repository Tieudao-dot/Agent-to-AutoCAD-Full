using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H3 (docs/ban-giao/nang-cap-h3.md): compare_drawings compares the open drawing with another file (an
    /// older / newer version), read only through Drawing/OtherDrawing.cs. Objects of model space are matched by handle (both
    /// from one file) or by geometry, and reported added / removed / changed (geometry, layer, properties, text, block,
    /// attributes; 3D solids by box and volume on a plan with 3D). mark: revision clouds on layer X-SO-SANH (green added, red
    /// removed, yellow changed; deleting the layer removes them); more than H3Numbers.CardAboveClouds clouds ask the card.
    /// excel: the whole list in .xlsx. The other file is never changed. The logic is Core/DrawingCompare.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterCompare(ToolDispatcher d)
        {
            // it writes only when it marks the differences
            d.Register(new ToolDef("compare_drawings", true, CompareDrawings, readsWhen: p => !p.Bool("mark", false)));
        }

        internal static object CompareDrawings(ToolContext ctx, Params p)
        {
            const string tool = DrawingCompareLogic.Tool;
            string path = p.Str("file");
            string plan = ctx.Dispatcher?.Plan;
            bool with2d = plan == null || PlanLogic.Allowed(plan, "check_drawing");
            bool with3d = plan == null || PlanLogic.Allowed(plan, "volume_schedule");
            bool otherIsNew = string.Equals((p.Str("other_is", "old") ?? "old").Trim(), "new", StringComparison.OrdinalIgnoreCase);
            bool mark = p.Bool("mark", false);
            string openName = SafeString(() => ctx.Doc.Name);

            double openMm = 1;
            int openIns = 0;
            try { openIns = Convert.ToInt32(ctx.Db.Insunits); openMm = FloorPlanLogic.MmPerUnit(openIns, out _); } catch (Exception) { }
            double tol = p.Num("tolerance", H3Numbers.ToleranceMm) / openMm;
            if (!(tol > 0)) throw ToolException.Invalid($"{tool}: tolerance must be more than 0 (mm).");

            var open = new List<CmpItem>();
            var other = new List<CmpItem>();
            int openTotal = 0, otherTotal;
            var notes = new List<string>();
            Dictionary<string, object> otherInfo;
            string otherFull;
            using (var od = OtherDrawing.Open(ctx, path, tool))
            {
                if (OtherDrawingLogic.SameFile(od.Path, openName))
                    throw ToolException.Invalid($"{tool}: '{od.Path}' is the open drawing itself. Ask the user which other file (older / newer version) to compare with.");
                otherInfo = od.Info();
                otherFull = od.Path;
                int otherIns = openIns;
                try { otherIns = Convert.ToInt32(od.Db.Insunits); } catch (Exception) { }
                // Test & sửa H3: a drawing without a unit is never converted (DrawingCompareLogic.UnitFactor)
                double f = DrawingCompareLogic.UnitFactor(openIns, otherIns, out var unitNote);
                if (unitNote != null) notes.Add(unitNote);
                using (var tr = od.Db.TransactionManager.StartTransaction())
                {
                    var ents = od.ModelEntities(tr, H3Numbers.MaxObjects, out otherTotal);
                    foreach (var e in ents) { var it = ItemOf(tr, e, f, with2d, with3d); if (it != null) other.Add(it); }
                    tr.Commit();
                }
            }
            var ms = (BlockTableRecord)ctx.Tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                openTotal++;
                if (openTotal > H3Numbers.MaxObjects) continue;
                if (ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Entity e) { var it = ItemOf(ctx.Tr, e, 1, with2d, with3d); if (it != null) open.Add(it); }
            }
            if (openTotal > H3Numbers.MaxObjects || otherTotal > H3Numbers.MaxObjects)
                notes.Add($"Only the first {H3Numbers.MaxObjects} objects of each drawing were compared (open drawing {openTotal}, other file {otherTotal}): the result is partial.");
            if (!with2d) notes.Add("This plan compares 3D solids only (2D objects are left out).");
            if (!with3d) notes.Add("3D solids are not compared on this plan.");

            // Test & sửa H3: in auto, handle only when it does not find more differences than geometry (one template, two drawings)
            var rows = DrawingCompareLogic.Run(other, open, p.Str("match", "auto"), tol, otherIsNew, out var mode);
            var counts = DrawingCompareLogic.Counts(rows);
            var res = new Dictionary<string, object>
            {
                ["other"] = otherInfo,
                ["match"] = mode,
                ["match_note"] = mode == "handle"
                    ? $"Matched by handle: the two drawings come from one file ({DrawingCompareLogic.SameOriginShare(other, open):P0} of the handles of the other file are here)."
                    : "Matched by geometry (kind, position and size within the tolerance): the two drawings are not copies of one file (or share handles only by chance), or match was asked.",
                ["other_is"] = otherIsNew ? "new" : "old",
                ["tolerance_mm"] = tol * openMm,
                ["compared"] = new Dictionary<string, object> { ["open"] = open.Count, ["other"] = other.Count, ["open_total"] = openTotal, ["other_total"] = otherTotal },
                ["added"] = counts["added"], ["removed"] = counts["removed"], ["changed"] = counts["changed"],
                ["differences"] = rows.Take(H3Numbers.MaxRows).Select(r => (object)new Dictionary<string, object>
                {
                    ["change"] = r.Change, ["kind"] = r.Kind, ["layer"] = r.Layer, ["handle"] = r.Handle, ["other_handle"] = r.OtherHandle,
                    ["what"] = r.What.Cast<object>().ToList(),
                }).ToList(),
            };
            if (rows.Count > H3Numbers.MaxRows) notes.Add($"{rows.Count} differences, the first {H3Numbers.MaxRows} listed (excel has them all).");

            if (mark && rows.Count > 0)
            {
                var clouds = rows.Count > H3Numbers.MaxClouds * 10 ? null : DrawingCompareLogic.Clouds(rows, openMm);
                if (clouds == null || clouds.Count > H3Numbers.MaxClouds)
                    notes.Add($"Too many differences to ring with clouds (more than {H3Numbers.MaxClouds} clouds): nothing was drawn. Use excel for the list, or compare a smaller part.");
                else
                {
                    // Test & sửa H3: a locked / frozen layer is refused before the card is asked; earlier clouds and an off layer are said
                    var layers = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
                    if (layers.Has(H3Numbers.Layer) && !layers[H3Numbers.Layer].IsErased)
                    {
                        var had = (LayerTableRecord)ctx.Tr.GetObject(layers[H3Numbers.Layer], OpenMode.ForRead);
                        if (had.IsLocked || had.IsFrozen) throw ToolException.Invalid($"{tool}: layer {H3Numbers.Layer} is locked or frozen: ask the user to unlock / thaw it (or delete it) and call again.");
                        if (had.IsOff) notes.Add($"Layer {H3Numbers.Layer} is turned off: the clouds are drawn but not seen until the user turns it on.");
                        int earlier = 0;
                        foreach (ObjectId id in ms)
                            if (ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Entity me && string.Equals(me.Layer, H3Numbers.Layer, StringComparison.OrdinalIgnoreCase)) earlier++;
                        if (earlier > 0) notes.Add($"{earlier} objects of an earlier compare are still on layer {H3Numbers.Layer} (not removed): the new clouds are drawn over them. Tell the user; deleting the layer's objects first gives a clean mark.");
                    }
                    if (clouds.Count > H3Numbers.CardAboveClouds)
                        RequireCard(ctx, p, tool, $"draw {clouds.Count} revision clouds on layer {H3Numbers.Layer}", new[] { DrawingCompareLogic.MarkToken(otherFull, clouds.Count) });
                    var layerId = EnsureLayerF2(ctx, H3Numbers.Layer, 7, true);
                    var made = new List<object>();
                    foreach (var c in clouds)
                    {
                        var poly = new List<double[]> { new[] { c.Min[0], c.Min[1] }, new[] { c.Max[0], c.Min[1] }, new[] { c.Max[0], c.Max[1] }, new[] { c.Min[0], c.Max[1] } };
                        var (verts, bulges) = Geometry.RevCloud(poly, DrawingCompareLogic.ArcLength(c, openMm));
                        var pl = Acad.MakePolyline(verts, true, bulges);
                        pl.SetDatabaseDefaults(ctx.Db);
                        pl.LayerId = layerId;
                        pl.ColorIndex = c.Color;
                        made.Add(Acad.Add(ctx, pl, null, false).Handle.ToString());
                    }
                    res["marks"] = new Dictionary<string, object>
                    {
                        ["layer"] = H3Numbers.Layer, ["clouds"] = made.Count, ["handles"] = made.Take(50).ToList(),
                        ["colors"] = "green = added, red = removed, yellow = changed (delete layer " + H3Numbers.Layer + " to remove them all)",
                    };
                }
            }
            if (p.Has("excel"))
            {
                if (rows.Count == 0) res["excel_note"] = "No difference found, so no Excel file was written.";
                else ExportExcel(ctx, p, tool, () => DrawingCompareLogic.ExcelRows(rows), res);
            }
            if (notes.Count > 0) res["notes"] = notes.Cast<object>().ToList();
            return res;
        }

        /// <summary>One object as a compare item (sizes times f into the open drawing's units), or null when it is not compared.</summary>
        private static CmpItem ItemOf(Transaction tr, Entity e, double f, bool with2d, bool with3d)
        {
            if (string.Equals(e.Layer, H3Numbers.Layer, StringComparison.OrdinalIgnoreCase)) return null; // the marks of an earlier compare
            bool solid = e is Solid3d;
            if (solid ? !with3d : !with2d) return null;
            double[] P(Point3d q) => new[] { q.X * f, q.Y * f, q.Z * f };
            var it = new CmpItem { Handle = e.Handle.ToString(), Layer = e.Layer };
            try { var x = e.GeometricExtents; it.Min = P(x.MinPoint); it.Max = P(x.MaxPoint); } catch (Exception) { }
            string R(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
            switch (e)
            {
                case Line l:
                    it.Kind = "line"; it.Nums = P(l.StartPoint).Concat(P(l.EndPoint)).ToArray(); it.Alt = P(l.EndPoint).Concat(P(l.StartPoint)).ToArray(); break;
                case Arc a:
                    it.Kind = "arc"; it.Nums = P(a.Center).Concat(new[] { a.Radius * f }).ToArray(); it.Exact = R(a.StartAngle) + "," + R(a.EndAngle); break;
                case Circle c:
                    it.Kind = "circle"; it.Nums = P(c.Center).Concat(new[] { c.Radius * f }).ToArray(); break;
                case Polyline pl:
                {
                    it.Kind = "pline";
                    var xy = new List<double>(); var b = new List<string>();
                    var rx = new List<double>(); var rb = new List<string>();
                    int n = pl.NumberOfVertices;
                    for (int i = 0; i < n; i++) { var v = pl.GetPoint2dAt(i); xy.Add(v.X * f); xy.Add(v.Y * f); b.Add(R(pl.GetBulgeAt(i) + 0.0)); }
                    for (int i = n - 1; i >= 0; i--) { var v = pl.GetPoint2dAt(i); rx.Add(v.X * f); rx.Add(v.Y * f); rb.Add(R((i > 0 ? -pl.GetBulgeAt(i - 1) : 0) + 0.0)); }
                    xy.Add(pl.Elevation * f); rx.Add(pl.Elevation * f);
                    it.Nums = xy.ToArray();
                    it.Exact = (pl.Closed ? "c|" : "o|") + string.Join(",", b);
                    if (!pl.Closed && string.Join(",", rb) == string.Join(",", b)) it.Alt = rx.ToArray(); // the same bulges read backwards
                    it.Sizes["width"] = pl.ConstantWidth * f; // Test & sửa H3: within the tolerance
                    break;
                }
                case DBText t:
                    it.Kind = "text"; it.Nums = P(t.Position).Concat(new[] { t.Height * f }).ToArray(); it.Exact = R(t.Rotation);
                    it.Props["text"] = t.TextString; it.Props["style"] = t.TextStyleName; break;
                case MText m:
                    it.Kind = "mtext"; it.Nums = P(m.Location).Concat(new[] { m.TextHeight * f }).ToArray(); it.Exact = R(m.Rotation);
                    it.Props["text"] = m.Contents; it.Props["style"] = m.TextStyleName; break;
                case BlockReference br:
                {
                    it.Kind = "insert"; it.Nums = P(br.Position);
                    it.Exact = R(br.Rotation) + "," + R(br.ScaleFactors.X) + "," + R(br.ScaleFactors.Y) + "," + R(br.ScaleFactors.Z);
                    string name = br.Name;
                    try { if (br.IsDynamicBlock) name = ((BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name; } catch (Exception) { }
                    it.Props["block"] = name;
                    var attrs = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    foreach (ObjectId aid in br.AttributeCollection)
                        if (tr.GetObject(aid, OpenMode.ForRead, false) is AttributeReference ar) attrs[ar.Tag.ToUpperInvariant()] = ar.TextString;
                    if (attrs.Count > 0) it.Props["attributes"] = string.Join("; ", attrs.Select(kv => kv.Key + "=" + kv.Value));
                    break;
                }
                case Dimension d:
                    it.Kind = "dim"; it.Nums = P(d.TextPosition);
                    // Test & sửa H3: a length within the tolerance (no "3000 → 3000.0001" from rounding / units); an angle as text
                    try
                    {
                        bool angle = d is LineAngularDimension2 || d is Point3AngularDimension;
                        if (angle) it.Props["value"] = R(d.Measurement);
                        else it.Sizes["value"] = d.Measurement * f;
                    }
                    catch (Exception) { }
                    it.Props["text"] = d.DimensionText; break;
                case Hatch h:
                    it.Kind = "hatch"; it.Nums = it.Min != null ? it.Min.Concat(it.Max).ToArray() : new double[0];
                    it.Props["pattern"] = h.PatternName;
                    try { it.Areas["area"] = h.Area * f * f; } catch (Exception) { } // Test & sửa H3: like a volume, not as text
                    break;
                case Solid3d s:
                    it.Kind = "solid"; it.Nums = it.Min != null ? it.Min.Concat(it.Max).ToArray() : new double[0];
                    try { it.Volume = s.MassProperties.Volume * f * f * f; } catch (Exception) { }
                    break;
                default:
                    it.Kind = e.GetType().Name.ToLowerInvariant();
                    it.Nums = it.Min != null ? it.Min.Concat(it.Max).ToArray() : new double[0];
                    break;
            }
            it.Props["color"] = e.Color.ToString();
            it.Props["linetype"] = e.Linetype;
            it.Props["lineweight"] = e.LineWeight.ToString();
            return it;
        }
    }
}
