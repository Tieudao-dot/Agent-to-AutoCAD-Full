using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H8 (docs/ban-giao/nang-cap-h8.md): structure.
    /// build_footing (3D / Full): pad and strip footings with their lean concrete (the concrete is scheduled by volume_schedule).
    /// draw_rebar (2D / Full): a beam (elevation + section), a column section, a slab panel: the main bars the user gives (never
    /// chosen by the plugin), sample stirrups / ties / slab bars, cover / anchorage / laps / hooks of H8Numbers; each bar kind has
    /// a mark (bubble) and a record (mark, Ø, shape, length, count) for rebar_schedule (2D / Full): the schedule by mark, totals
    /// per diameter and grade, the table in the drawing and / or Excel. The plugin draws and counts; it never designs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterStructure(ToolDispatcher d)
        {
            d.Register(new ToolDef("build_footing", true, BuildFooting));
            d.Register(new ToolDef("draw_rebar", true, DrawRebar));
            // a read unless it puts the table in the drawing
            d.Register(new ToolDef("rebar_schedule", true, RebarSchedule, readsWhen: p => !p.Has("table_position")));
        }

        // ------------------------------------------------------------ build_footing

        internal static object BuildFooting(ToolContext ctx, Params p)
        {
            const string tool = RebarLogic.FootingTool;
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var kind = (p.Str("kind", "pad") ?? "pad").Trim().ToLowerInvariant();
            if (kind != "pad" && kind != "strip") throw ToolException.Invalid($"{tool}: kind must be pad (móng đơn) or strip (móng băng).");
            double bottom = p.Num("bottom_z", 0) / f;
            bool lean = p.Bool("lean", true);
            var notes = new List<string>();
            var used = new List<string>();
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, H8Numbers.FootingLayer, H8Numbers.FootingColor)) layersCreated.Add(H8Numbers.FootingLayer);
            if (lean && EnsureLayer(ctx, H8Numbers.LeanLayer, H8Numbers.LeanColor)) layersCreated.Add(H8Numbers.LeanLayer);
            var footP = LayerParams(tool, H8Numbers.FootingLayer);
            var leanP = LayerParams(tool, H8Numbers.LeanLayer);
            var handles = new List<object>();
            double vol = 0;
            double Mm(string key, double def) { if (!p.Has(key)) used.Add(key); return p.Num(key, def); }
            if (kind == "pad")
            {
                var pos = p.Points("positions", 1, H8Numbers.MaxFootings);
                var size = p.NumbersOpt("size");
                double bx = size != null && size.Count >= 1 ? size[0] : H8Numbers.PadSize, ly = size != null && size.Count >= 2 ? size[1] : bx;
                if (size == null) used.Add("size");
                double hb = Mm("height", H8Numbers.PadHeight);
                if (bx <= 0 || ly <= 0 || hb <= 0) throw ToolException.Invalid($"{tool}: size and height must be more than 0 (mm).");
                var col = p.NumbersOpt("column");
                double? ph = p.Has("pedestal_height") ? p.Num("pedestal_height") : (double?)null;
                if (col != null && col.Count >= 2 && !ph.HasValue) throw ToolException.Invalid($"{tool}: give pedestal_height (mm, the neck from the base to the column foot) with column.");
                foreach (var q in pos)
                {
                    double x = q[0], y = q[1];
                    if (lean)
                    {
                        var l = MakeBox((bx + 2 * H8Numbers.LeanMargin) / f, (ly + 2 * H8Numbers.LeanMargin) / f, H8Numbers.LeanThickness / f, null,
                            new[] { x, y, bottom - H8Numbers.LeanThickness / f / 2 }, 0, tool);
                        vol += VolumeOf(l);
                        handles.Add(Acad.H(AddSolid(ctx, l, leanP)));
                    }
                    var basis = MakeBox(bx / f, ly / f, hb / f, null, new[] { x, y, bottom + hb / f / 2 }, 0, tool);
                    if (col != null && col.Count >= 2)
                    {
                        using (var neck = MakeBox(col[0] / f, col[1] / f, ph.Value / f, null, new[] { x, y, bottom + hb / f + ph.Value / f / 2 }, 0, tool))
                            BooleanInto(basis, neck, BooleanOperationType.BoolUnite);
                    }
                    vol += VolumeOf(basis);
                    handles.Add(Acad.H(AddSolid(ctx, basis, footP)));
                }
            }
            else
            {
                var pts = p.Points("path_points", 2, Solid3Logic.MaxBeamSegments + 1).Select(q => new[] { q[0], q[1] }).ToList();
                double w = Mm("width", H8Numbers.StripWidth), hs = Mm("height", H8Numbers.StripHeight);
                // KTP-18: rib false = no rib, true = the sample rib; else [width, height]
                var ribFlag = p.Get("rib") as bool?;
                var rib = ribFlag.HasValue ? null : p.NumbersOpt("rib");
                double rw = rib != null && rib.Count >= 1 ? rib[0] : H8Numbers.RibWidth, rh = rib != null && rib.Count >= 2 ? rib[1] : H8Numbers.RibHeight;
                bool withRib = ribFlag ?? (!p.Has("rib") || (rib != null && rib.Count >= 2));
                if (withRib && rib == null) used.Add("rib");
                var closed = p.Bool("closed", false);
                if (lean)
                {
                    var lay = Solid3Logic.Beam(pts, new List<double>(), closed, (w + 2 * H8Numbers.LeanMargin) / f, "path_points", tool);
                    foreach (var id in UniteOutlines(ctx, tool, lay.Pieces, bottom - H8Numbers.LeanThickness / f, H8Numbers.LeanThickness / f, leanP, "path_points", "lean concrete", out var v1)) { handles.Add(Acad.H(id)); vol += v1; }
                }
                var b0 = Solid3Logic.Beam(pts, new List<double>(), closed, w / f, "path_points", tool);
                foreach (var id in UniteOutlines(ctx, tool, b0.Pieces, bottom, hs / f, footP, "path_points", "strip footing", out var v2)) { handles.Add(Acad.H(id)); vol += v2; }
                if (withRib)
                {
                    var r0 = Solid3Logic.Beam(pts, new List<double>(), closed, rw / f, "path_points", tool);
                    foreach (var id in UniteOutlines(ctx, tool, r0.Pieces, bottom + hs / f, rh / f, footP, "path_points", "footing rib", out var v3)) { handles.Add(Acad.H(id)); vol += v3; }
                }
            }
            var res = new Dictionary<string, object>
            {
                ["handles"] = handles,
                ["kind"] = kind,
                ["layers"] = lean ? new List<object> { H8Numbers.FootingLayer, H8Numbers.LeanLayer } : new List<object> { H8Numbers.FootingLayer },
                ["volume_m3"] = Math.Round(Solid3Logic.M3(vol, f), 3),
                ["note"] = "A drawing aid: the engineer decides the footing sizes (the plugin does no structural design). volume_schedule gives the concrete by layer.",
            };
            if (used.Count > 0) res["sample_sizes"] = $"Sample sizes used for {string.Join(", ", used)} (cỡ mẫu, kỹ sư quyết): tell the user.";
            if (layersCreated.Count > 0) res["layers_created"] = layersCreated;
            return res;
        }

        // ------------------------------------------------------------ draw_rebar

        private static void WriteRebar(Transaction tr, Entity e, RebarMark m)
        {
            if (e.ExtensionDictionary.IsNull) e.CreateExtensionDictionary();
            var ed = (DBDictionary)tr.GetObject(e.ExtensionDictionary, OpenMode.ForWrite);
            var xr = new Xrecord();
            ed.SetAt(RebarLogic.MarkKey, xr);
            tr.AddNewlyCreatedDBObject(xr, true);
            xr.Data = new ResultBuffer(new TypedValue((int)DxfCode.Int32, m.Mark), new TypedValue((int)DxfCode.Real, m.Dia),
                new TypedValue((int)DxfCode.Text, m.Shape ?? ""), new TypedValue((int)DxfCode.Text, m.Element ?? ""),
                new TypedValue((int)DxfCode.Real, m.LengthMm), new TypedValue((int)DxfCode.Int32, m.Count));
        }

        private static RebarMark ReadRebar(Transaction tr, Entity e)
        {
            try
            {
                if (e.ExtensionDictionary.IsNull) return null;
                var ed = (DBDictionary)tr.GetObject(e.ExtensionDictionary, OpenMode.ForRead);
                if (!ed.Contains(RebarLogic.MarkKey)) return null;
                var v = ((Xrecord)tr.GetObject(ed.GetAt(RebarLogic.MarkKey), OpenMode.ForRead)).Data?.AsArray();
                if (v == null || v.Length < 6) return null;
                return new RebarMark
                {
                    Mark = Convert.ToInt32(v[0].Value), Dia = Convert.ToDouble(v[1].Value), Shape = (string)v[2].Value, Element = (string)v[3].Value,
                    LengthMm = Convert.ToDouble(v[4].Value), Count = Convert.ToInt32(v[5].Value),
                };
            }
            catch (Exception) { return null; }
        }

        /// <summary>The marks already in the drawing (model space).</summary>
        private static List<int> ExistingMarks(ToolContext ctx)
        {
            var r = new List<int>();
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Entity e)) continue;
                var m = ReadRebar(ctx.Tr, e);
                if (m != null) r.Add(m.Mark);
            }
            return r;
        }

        private sealed class RebarPen
        {
            public ToolContext Ctx;
            public ObjectId Conc, Bar, TextL;
            public double F, TextH, Bubble;
            public List<object> Handles = new List<object>();

            public ObjectId Add(Entity e, ObjectId layer)
            {
                e.SetDatabaseDefaults(Ctx.Db);
                e.LayerId = layer;
                var id = Acad.Add(Ctx, e, null, defaults: false);
                Handles.Add(Acad.H(id));
                return id;
            }

            public void Rect(double x, double y, double w, double h, ObjectId layer) =>
                Add(Acad.MakePolyline(new List<double[]> { new[] { x, y }, new[] { x + w, y }, new[] { x + w, y + h }, new[] { x, y + h } }, true), layer);

            public void Line(double x0, double y0, double x1, double y1, ObjectId layer) => Add(new Line(new Point3d(x0, y0, 0), new Point3d(x1, y1, 0)), layer);

            /// <summary>A bar seen end on: a filled dot of its diameter (a donut).</summary>
            public ObjectId Dot(double x, double y, double d)
            {
                var pl = Acad.MakePolyline(new List<double[]> { new[] { x - d / 4, y }, new[] { x + d / 4, y } }, true, new List<double> { 1, 1 }, d / 2);
                return Add(pl, Bar);
            }

            public void Text(double x, double y, string s, double rot = 0)
            {
                var t = new DBText();
                t.SetDatabaseDefaults(Ctx.Db);
                t.TextStyleId = Ctx.Db.Textstyle;
                t.TextString = s;
                t.Height = TextH;
                t.Rotation = rot;
                t.Position = new Point3d(x, y, 0);
                Add(t, TextL);
            }

            /// <summary>A bar mark: its number in a bubble, the label beside it.</summary>
            public void Mark(double x, double y, int mark, string label)
            {
                Add(new Circle(new Point3d(x, y, 0), Vector3d.ZAxis, Bubble / 2), TextL);
                var t = new DBText();
                t.SetDatabaseDefaults(Ctx.Db);
                t.TextStyleId = Ctx.Db.Textstyle;
                t.TextString = mark.ToString(System.Globalization.CultureInfo.InvariantCulture);
                t.Height = TextH;
                var c = new Point3d(x, y, 0);
                t.Justify = AttachmentPoint.MiddleCenter;
                t.AlignmentPoint = c;
                t.Position = c;
                Add(t, TextL);
                t.AdjustAlignment(Ctx.Db);
                Text(x + Bubble * 0.75, y - TextH / 2, label);
            }
        }

        internal static object DrawRebar(ToolContext ctx, Params p)
        {
            const string tool = RebarLogic.DrawTool;
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var element = (p.Str("element") ?? "").Trim().ToLowerInvariant();
            if (element != "beam" && element != "column" && element != "slab") throw ToolException.Invalid($"{tool}: element must be beam, column or slab.");
            var at = p.Point("position");
            double x0 = at[0], y0 = at[1];
            int scale = p.Has("scale") ? Convert.ToInt32(StandardsLogic.ScaleDenominator(p.Get("scale"), tool)) : element == "column" ? H8Numbers.SectionScale : H8Numbers.ElevationScale;
            var warnings = new List<string>();
            var sample = new List<string>();
            var pen = new RebarPen
            {
                Ctx = ctx, F = f, TextH = H8Numbers.TextHeightPaper * scale / f, Bubble = H8Numbers.MarkBubblePaper * scale / f,
                Conc = EnsureLayerF2(ctx, H8Numbers.ConcreteLayer, H8Numbers.ConcreteColor), Bar = EnsureLayerF2(ctx, H8Numbers.RebarLayer, H8Numbers.RebarColor),
                TextL = EnsureLayerF2(ctx, H8Numbers.TextLayer, H8Numbers.TextColor),
            };
            var existing = ExistingMarks(ctx);
            int mark = p.Has("start_mark") ? p.Int("start_mark", 1, 1, 9999) : RebarLogic.NextMark(existing);
            List<RebarLogic.BarKind> kinds;
            // the record of a bar kind sits on its first drawn bar
            void Record(ObjectId id, RebarLogic.BarKind k, int m) =>
                WriteRebar(ctx.Tr, (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite), new RebarMark { Mark = m, Dia = k.Dia, Shape = k.Shape, Element = k.Element, LengthMm = k.LengthMm, Count = k.Count });
            double U(double mm) => mm / f;

            if (element == "beam")
            {
                double b = p.Positive("width"), h = p.Positive("height"), span = p.Positive("span");
                var bottom = RebarLogic.ParseBars(p.Str("bottom"), "bottom (the bottom main bars)", tool);
                var top = RebarLogic.ParseBars(p.Str("top"), "top (the top main bars)", tool);
                double sd = H8Numbers.StirrupDia, se = H8Numbers.StirrupEndSpacing, sm = H8Numbers.StirrupMidSpacing;
                if (p.Has("stirrup")) { var st = RebarLogic.ParseSpaced(p.Str("stirrup"), "stirrup", tool); sd = st.dia; se = st.spacing; sm = p.Has("stirrup_middle") ? RebarLogic.ParseSpaced(p.Str("stirrup_middle"), "stirrup_middle", tool).spacing : st.spacing; }
                else sample.Add($"stirrups Ø{RebarLogic.Num(sd)} a{RebarLogic.Num(se)} near the supports / a{RebarLogic.Num(sm)} in the middle");
                kinds = RebarLogic.BeamBars(b, h, span, bottom, top, sd, se, sm, p.Has("anchor") ? p.Num("anchor") : (double?)null, warnings);
                double cover = RebarLogic.Cover("beam", Math.Max(bottom.d, top.d));
                // elevation: the beam (span × h) between two supports, the bars running into them
                pen.Rect(x0, y0, U(span), U(h), pen.Conc);
                double anchorB = (kinds[0].LengthMm - span - 2 * RebarLogic.AnchorEnd(bottom.d)) / 2, anchorT = (kinds[1].LengthMm - span - 2 * RebarLogic.AnchorEnd(top.d)) / 2;
                double yb = y0 + U(cover + sd + bottom.d / 2), yt = y0 + U(h - cover - sd - top.d / 2);
                // a ribbed bar ends in its L bend of 10d (the bottom bar bends up, the top bar down); a plain bar's hook is counted, not drawn
                List<double[]> MainBar(double y, double a, double d, double dir)
                {
                    var v = new List<double[]> { new[] { x0 - U(a), y }, new[] { x0 + U(span + a), y } };
                    if (!RebarLogic.Plain(d))
                    {
                        double leg = U(H8Numbers.RibbedBendFactor * d) * dir;
                        v.Insert(0, new[] { x0 - U(a), y + leg });
                        v.Add(new[] { x0 + U(span + a), y + leg });
                    }
                    return v;
                }
                var idB = pen.Add(Acad.MakePolyline(MainBar(yb, anchorB, bottom.d, 1), false), pen.Bar);
                var idT = pen.Add(Acad.MakePolyline(MainBar(yt, anchorT, top.d, -1), false), pen.Bar);
                Record(idB, kinds[0], mark); Record(idT, kinds[1], mark + 1);
                var pos = RebarLogic.StirrupPositions(span, se, sm);
                ObjectId first = ObjectId.Null;
                int drawn = 0;
                foreach (var sx in pos)
                {
                    if (drawn++ >= H8Numbers.MaxStirrupLines) break;
                    var id = pen.Add(new Line(new Point3d(x0 + U(sx), y0 + U(cover), 0), new Point3d(x0 + U(sx), y0 + U(h - cover), 0)), pen.Bar);
                    if (first.IsNull) first = id;
                }
                Record(first, kinds[2], mark + 2);
                pen.Mark(x0 + U(span / 2), y0 - pen.Bubble * 1.5, mark, RebarLogic.Label(kinds[0].Count, kinds[0].Dia, kinds[0].LengthMm));
                pen.Mark(x0 + U(span / 2), y0 + U(h) + pen.Bubble * 1.5, mark + 1, RebarLogic.Label(kinds[1].Count, kinds[1].Dia, kinds[1].LengthMm));
                pen.Mark(x0 + U(span / 8), y0 + U(h) + pen.Bubble * 3, mark + 2, $"Ø{RebarLogic.Num(sd)}a{RebarLogic.Num(se)}/a{RebarLogic.Num(sm)} L={RebarLogic.Num(RebarLogic.RoundLength(kinds[2].LengthMm))} ({kinds[2].Count} cái)");
                // the section beside it. Test & sửa H8: drawn enlarged scale / SectionScale times, so printed with the elevation at
                // 1:scale it reads at its own 1:SectionScale (the label said 1:20 while it was drawn at the elevation's scale)
                double k = (double)scale / H8Numbers.SectionScale;
                double S(double mm) => U(mm) * k;
                double sx0 = x0 + U(span) + U(Math.Max(1000, 3 * h)), sy0 = y0;
                pen.Rect(sx0, sy0, S(b), S(h), pen.Conc);
                pen.Rect(sx0 + S(cover + sd / 2), sy0 + S(cover + sd / 2), S(b - 2 * cover - sd), S(h - 2 * cover - sd), pen.Bar);
                foreach (var bx in RebarLogic.BarPositions(bottom.n, b, cover, sd, bottom.d)) pen.Dot(sx0 + S(bx), sy0 + S(cover + sd + bottom.d / 2), S(bottom.d));
                foreach (var bx in RebarLogic.BarPositions(top.n, b, cover, sd, top.d)) pen.Dot(sx0 + S(bx), sy0 + S(h - cover - sd - top.d / 2), S(top.d));
                pen.Text(sx0, sy0 - pen.TextH * 3, $"MẶT CẮT 1:{H8Numbers.SectionScale}  {RebarLogic.Num(b)}×{RebarLogic.Num(h)}  {kinds[0].Label} dưới, {kinds[1].Label} trên");
            }
            else if (element == "column")
            {
                double b = p.Positive("width"), h = p.Positive("height"), len = p.Positive("length");
                var bars = RebarLogic.ParseBars(p.Str("bars"), "bars (the column bars)", tool);
                double td = H8Numbers.TieDia, ts = H8Numbers.TieSpacing;
                if (p.Has("tie")) { var t = RebarLogic.ParseSpaced(p.Str("tie"), "tie", tool); td = t.dia; ts = t.spacing; }
                else sample.Add($"ties Ø{RebarLogic.Num(td)} a{RebarLogic.Num(ts)}");
                kinds = RebarLogic.ColumnBarKinds(b, h, len, bars, td, ts, p.Has("lap") ? p.Num("lap") : (double?)null, warnings);
                double cover = RebarLogic.Cover("column", bars.d);
                pen.Rect(x0, y0, U(b), U(h), pen.Conc);
                var tieId = pen.Add(Acad.MakePolyline(new List<double[]> { new[] { x0 + U(cover + td / 2), y0 + U(cover + td / 2) }, new[] { x0 + U(b - cover - td / 2), y0 + U(cover + td / 2) },
                    new[] { x0 + U(b - cover - td / 2), y0 + U(h - cover - td / 2) }, new[] { x0 + U(cover + td / 2), y0 + U(h - cover - td / 2) } }, true), pen.Bar);
                ObjectId firstBar = ObjectId.Null;
                foreach (var q in RebarLogic.ColumnBars(bars.n, b, h, cover, td, bars.d))
                {
                    var id = pen.Dot(x0 + U(q[0]), y0 + U(q[1]), U(bars.d));
                    if (firstBar.IsNull) firstBar = id;
                }
                Record(firstBar, kinds[0], mark);
                Record(tieId, kinds[1], mark + 1);
                pen.Mark(x0 + U(b) + pen.Bubble, y0 + U(h) * 0.75, mark, RebarLogic.Label(kinds[0].Count, kinds[0].Dia, kinds[0].LengthMm));
                pen.Mark(x0 + U(b) + pen.Bubble, y0 + U(h) * 0.25, mark + 1, $"Ø{RebarLogic.Num(td)}a{RebarLogic.Num(ts)} L={RebarLogic.Num(RebarLogic.RoundLength(kinds[1].LengthMm))} ({kinds[1].Count} cái)");
                pen.Text(x0, y0 - pen.TextH * 3, $"CỘT {RebarLogic.Num(b)}×{RebarLogic.Num(h)} 1:{scale}  {kinds[0].Label}");
            }
            else
            {
                double w = p.Positive("width"), l = p.Positive("length");
                (double d, double s) bx = p.Has("bars_x") ? RebarLogic.ParseSpaced(p.Str("bars_x"), "bars_x", tool) : (H8Numbers.SlabBarDia, H8Numbers.SlabBarSpacing);
                (double d, double s) by = p.Has("bars_y") ? RebarLogic.ParseSpaced(p.Str("bars_y"), "bars_y", tool) : bx;
                if (!p.Has("bars_x")) sample.Add($"slab bars Ø{RebarLogic.Num(H8Numbers.SlabBarDia)} a{RebarLogic.Num(H8Numbers.SlabBarSpacing)}");
                kinds = RebarLogic.SlabBars(w, l, bx, by);
                double c = RebarLogic.Cover("slab", Math.Max(bx.d, by.d));
                pen.Rect(x0, y0, U(w), U(l), pen.Conc);
                var idx = pen.Add(new Line(new Point3d(x0 + U(c), y0 + U(l / 3), 0), new Point3d(x0 + U(w - c), y0 + U(l / 3), 0)), pen.Bar);
                var idy = pen.Add(new Line(new Point3d(x0 + U(w / 3), y0 + U(c), 0), new Point3d(x0 + U(w / 3), y0 + U(l - c), 0)), pen.Bar);
                Record(idx, kinds[0], mark); Record(idy, kinds[1], mark + 1);
                pen.Mark(x0 + U(w * 0.55), y0 + U(l / 3) + pen.Bubble, mark, $"{RebarLogic.SpacedLabel(bx.d, bx.s)} L={RebarLogic.Num(RebarLogic.RoundLength(kinds[0].LengthMm))} ({kinds[0].Count} thanh)");
                pen.Mark(x0 + U(w / 3) + pen.Bubble, y0 + U(l * 0.7), mark + 1, $"{RebarLogic.SpacedLabel(by.d, by.s)} L={RebarLogic.Num(RebarLogic.RoundLength(kinds[1].LengthMm))} ({kinds[1].Count} thanh)");
            }

            if (p.Has("start_mark"))
            {
                var taken = Enumerable.Range(mark, kinds.Count).Where(existing.Contains).ToList();
                if (taken.Count > 0)
                    warnings.Add($"Bar mark(s) {string.Join(", ", taken)} were already in the drawing: rebar_schedule keeps different bar kinds on their own rows but the marks repeat. Tell the user, or redraw with another start_mark.");
            }
            foreach (var k in kinds)
            {
                var wl = RebarLogic.WithLaps(k.LengthMm, k.Dia, k.Element);
                if (wl.splices > 0)
                    warnings.Add($"{k.Label} L={RebarLogic.Num(RebarLogic.RoundLength(k.LengthMm))} is longer than a stock bar of {RebarLogic.Num(H8Numbers.StockLengthMm)} mm: rebar_schedule adds {wl.splices} lap(s) of {RebarLogic.Num(RebarLogic.RoundLength(wl.lapEach))} mm per bar.");
            }

            var res = new Dictionary<string, object>
            {
                ["handles"] = pen.Handles,
                ["element"] = element,
                ["scale"] = "1:" + scale,
                ["bars"] = kinds.Select((k, i) => (object)new Dictionary<string, object>
                {
                    ["mark"] = mark + i, ["role"] = k.Role, ["label"] = k.Label, ["diameter"] = k.Dia, ["grade"] = RebarLogic.Grade(k.Dia),
                    ["length_mm"] = RebarLogic.RoundLength(k.LengthMm), ["count"] = k.Count,
                }).ToList(),
                ["layers"] = new List<object> { H8Numbers.ConcreteLayer, H8Numbers.RebarLayer, H8Numbers.TextLayer },
                ["note"] = "A drawing aid from the bars the user gave: the plugin does no structural design. Cover, anchorage, laps and hooks are the defaults; the engineer checks them against the TCVN in force. rebar_schedule gives the table.",
            };
            if (sample.Count > 0) res["sample_bars"] = "Sample bars used (thép mẫu): " + string.Join("; ", sample) + ". Tell the user.";
            if (warnings.Count > 0) res["warnings"] = warnings.Cast<object>().ToList();
            return res;
        }

        // ------------------------------------------------------------ rebar_schedule

        internal static object RebarSchedule(ToolContext ctx, Params p)
        {
            const string tool = RebarLogic.ScheduleTool;
            var marks = new List<RebarMark>();
            if (p.Has("handles"))
            {
                foreach (var h in p.Handles("handles", 20000))
                {
                    var m = ReadRebar(ctx.Tr, Acad.GetEntity(ctx, h, false));
                    if (m != null) marks.Add(m);
                }
            }
            else
                foreach (ObjectId id in ctx.ModelSpace)
                    if (ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Entity e && ReadRebar(ctx.Tr, e) is RebarMark m) marks.Add(m);
            var warnings = new List<string>();
            var rows = RebarLogic.Schedule(marks, warnings);
            var waste = RebarLogic.WasteShare(p.Has("waste_percent") ? p.Num("waste_percent") : (double?)null, tool);
            var res = new Dictionary<string, object>
            {
                ["rows"] = rows.Select(r => (object)new Dictionary<string, object>
                {
                    ["mark"] = r.Mark, ["element"] = r.Element, ["shape"] = r.Shape, ["diameter"] = r.Dia, ["grade"] = r.Grade, ["length_mm"] = r.LengthMm,
                    ["count"] = r.Count, ["total_m"] = Math.Round(r.TotalM, 2), ["kg"] = r.Kg, ["splices"] = r.Splices,
                }).ToList(),
                ["total_kg"] = Math.Round(rows.Sum(r => r.Kg), H8Numbers.WeightDecimals),
                ["note"] = "Counted from the drawing (bars drawn by draw_rebar), not a structural design; weight 0.00617 × d² kg/m; no waste unless asked.",
            };
            if (rows.Count == 0) { res["note"] = "No bar drawn by draw_rebar in this drawing."; return res; }
            if (warnings.Count > 0) res["warnings"] = warnings.Cast<object>().ToList();
            if (waste.HasValue) res["waste_percent"] = waste.Value * 100;
            var table = RebarLogic.ScheduleTable(rows, waste);
            if (p.Has("table_position"))
            {
                var widths = waste.HasValue ? new[] { 5.0, 7, 12, 7, 8, 8, 6, 8, 9, 11, 7 } : new[] { 5.0, 7, 12, 7, 8, 8, 6, 8, 9, 7 };
                var t = InsertSchedule(ctx, p, table, widths, "BẢNG THỐNG KÊ THÉP");
                res["handles"] = t["handles"];
            }
            ExportExcel(ctx, p, tool, () => table, res);
            return res;
        }
    }
}
