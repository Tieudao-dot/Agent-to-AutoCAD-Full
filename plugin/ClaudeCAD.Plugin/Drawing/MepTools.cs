using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H7 (docs/ban-giao/nang-cap-h7.md): MEP pipes and ducts in 3D.
    /// draw_mep: a route of round pipes (or rectangular ducts) of the size the user gives, elbows at the corners, a tee where it
    /// starts on another pipe, on the layer of its system (P-SUPL-3D…); a drain slopes. Each solid carries a mark (system, kind,
    /// size, length, route) for mep_report: the schedule by system and size (Excel on request) and the check against beams /
    /// columns / other pipes (clash, or closer than the clearance). Sizes are only drawn, never worked out (no hydraulics).
    /// The logic is Core/Mep.cs; every number is H7Numbers.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterMep(ToolDispatcher d)
        {
            d.Register(new ToolDef("draw_mep", true, DrawMep));
            // reads the drawing (the Excel file is a file next to it, with the overwrite card)
            d.Register(new ToolDef("mep_report", false, MepReport));
        }

        // ------------------------------------------------------------ the mark on each solid

        private sealed class MepRecord
        {
            public MepMark Mark = new MepMark();
            public string Route, Link;
            public double[] A, B; // a pipe's axis (WCS), for a tee
        }

        private static void WriteMep(Transaction tr, Entity ent, MepRecord r)
        {
            if (ent.ExtensionDictionary.IsNull) ent.CreateExtensionDictionary();
            var ed = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForWrite);
            var xr = new Xrecord();
            ed.SetAt(MepLogic.MarkKey, xr);
            tr.AddNewlyCreatedDBObject(xr, true);
            var a = r.A ?? new double[3]; var b = r.B ?? new double[3];
            xr.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, r.Mark.System ?? ""), new TypedValue((int)DxfCode.Text, r.Mark.Kind ?? ""),
                new TypedValue((int)DxfCode.Text, r.Mark.Shape ?? ""), new TypedValue((int)DxfCode.Real, r.Mark.Size1),
                new TypedValue((int)DxfCode.Real, r.Mark.Size2), new TypedValue((int)DxfCode.Real, r.Mark.LengthMm),
                new TypedValue((int)DxfCode.Text, r.Route ?? ""), new TypedValue((int)DxfCode.Text, r.Link ?? ""),
                new TypedValue((int)DxfCode.Real, a[0]), new TypedValue((int)DxfCode.Real, a[1]), new TypedValue((int)DxfCode.Real, a[2]),
                new TypedValue((int)DxfCode.Real, b[0]), new TypedValue((int)DxfCode.Real, b[1]), new TypedValue((int)DxfCode.Real, b[2]));
        }

        private static MepRecord ReadMep(Transaction tr, Entity ent)
        {
            try
            {
                if (ent.ExtensionDictionary.IsNull) return null;
                var ed = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead);
                if (!ed.Contains(MepLogic.MarkKey)) return null;
                var v = ((Xrecord)tr.GetObject(ed.GetAt(MepLogic.MarkKey), OpenMode.ForRead)).Data?.AsArray();
                if (v == null || v.Length < 14) return null;
                double D(int i) => Convert.ToDouble(v[i].Value);
                return new MepRecord
                {
                    Mark = new MepMark { System = (string)v[0].Value, Kind = (string)v[1].Value, Shape = (string)v[2].Value, Size1 = D(3), Size2 = D(4), LengthMm = D(5) },
                    Route = (string)v[6].Value, Link = (string)v[7].Value,
                    A = new[] { D(8), D(9), D(10) }, B = new[] { D(11), D(12), D(13) },
                };
            }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------ draw_mep

        internal static object DrawMep(ToolContext ctx, Params p)
        {
            const string tool = MepLogic.Tool;
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var sys = MepLogic.SystemOf(p.Str("system"), tool);
            var (sa, sb) = MepLogic.SizeOf(sys, p.Has("diameter") ? p.Num("diameter") : (double?)null, p.Has("width") ? p.Num("width") : (double?)null,
                p.Has("height") ? p.Num("height") : (double?)null, p.Str("use", null), tool);
            var pts = p.Points("points", 2, H7Numbers.MaxPoints);
            var raw = p.Get("points") as List<object>;
            bool given3d = raw != null && raw.All(x => x is List<object> l && l.Count >= 3);
            // KTP-29: all the points in plan (x, y) or all with their height (x, y, z); a mix, or z with elevation / under_beams, is refused
            if (!given3d && raw != null && raw.Any(x => x is List<object> l && l.Count >= 3))
                throw ToolException.Invalid($"{tool}: some points have a height (x, y, z) and some not (x, y): give all of them as x, y, z, or all as x, y with elevation.");
            if (given3d && (p.Has("elevation") || p.Has("under_beams")))
                throw ToolException.Invalid($"{tool}: the points already give their heights (x, y, z): leave out elevation and under_beams, or give the points in plan (x, y).");
            var warnings = new List<string>();

            // heights: the points' own z, or one elevation (mm, the centre line), or under the beams; a drain falls by its slope
            double half = (sys.Rect ? sb : sa) / 2 / f;
            // approved 26/11/2026: a drain's default slope is never under the least slope of its size (D60 2.5 %, D110 2 %)
            double? slope = sys.Slopes ? p.Num("slope", MepLogic.DefaultSlope(sa)) : (p.Has("slope") ? p.Num("slope") : (double?)null);
            if (slope.HasValue && (slope < 0 || slope > 100)) throw ToolException.Invalid($"{tool}: slope must be 0 – 100 (%).");
            if (given3d && p.Has("slope")) throw ToolException.Invalid($"{tool}: the points already give their heights (x, y, z): leave out slope, or give the points in plan (x, y) with elevation and slope.");
            if (!given3d)
            {
                // Test & sửa H7: plan points take one height: a UCS tilted out of plan would put them on its slanted plane
                var frame = ctx.Dispatcher?.CurrentFrame ?? UcsFrame.World;
                if (!frame.ZUp) throw ToolException.Invalid($"{tool}: the UCS is tilted out of plan: give each point as x, y, z, or call reset_ucs (or a UCS only moved / turned in plan) first.");
                double z0;
                if (p.Has("elevation")) z0 = p.Num("elevation") / f;
                else if (p.Bool("under_beams", false)) z0 = UnderBeams(ctx, pts, half, f, warnings, tool);
                else throw ToolException.Invalid($"{tool}: the points are in plan (x, y): give elevation (mm, height of the centre line) or under_beams: true, or give each point as x, y, z.");
                pts = slope.HasValue && slope > 0 ? MepLogic.Sloped(pts, z0, slope.Value) : pts.Select(q => new[] { q[0], q[1], z0 }).ToList();
            }
            double? realSlope = null;
            if (sys.Slopes && given3d)
            {
                // Test & sửa H7: the points give the heights: the warning reads their own fall, not the default slope
                var (least, rises) = MepLogic.SlopesOf(pts);
                realSlope = least;
                if (rises) warnings.Add("A part of the drain rises towards its end (the first point should be the high end): tell the user.");
                var min = MepLogic.MinSlope(sa);
                if (least.HasValue && !rises && min.HasValue && least < min - 1e-9)
                    warnings.Add($"The flattest part falls {MepLogic.Num(least.Value)}%, less than {min}% for D{MepLogic.Num(sa)}: tell the user (the drain may not empty well).");
            }
            else if (sys.Slopes && slope.HasValue)
            {
                var min = MepLogic.MinSlope(sa);
                if (min.HasValue && slope < min - 1e-9) warnings.Add($"Slope {slope}% is less than {min}% for D{MepLogic.Num(sa)}: tell the user (the drain may not empty well).");
            }

            // a tee: the route starts on another pipe drawn by draw_mep
            MepRecord main = null;
            string connectTo = p.Str("connect_to", null);
            double[] teeAt = null;
            if (connectTo != null)
            {
                var me = Acad.GetEntity(ctx, connectTo, false);
                main = ReadMep(ctx.Tr, me);
                if (main == null || main.Mark.Kind != "pipe") throw ToolException.Invalid($"{tool}: connect_to {connectTo} is not a straight pipe drawn by draw_mep.");
                // KTP-16: a branch joins a pipe of its own system (a supply branch never goes into a drain)
                if (main.Mark.System != sys.Key)
                    throw ToolException.Invalid($"{tool}: connect_to {connectTo} is a {MepLogic.SystemOf(main.Mark.System, tool).Name} pipe ({main.Mark.System}), not {sys.Name} ({sys.Key}): a branch joins a pipe of its own system.");
                var (at, _) = MepLogic.Nearest(main.A, main.B, pts[0]);
                double mainR = (main.Mark.Shape == "rect" ? Math.Max(main.Mark.Size1, main.Mark.Size2) : main.Mark.Size1) / 2 / f;
                if (MepLogic.Dist(at, pts[0]) > mainR + 1e-6 * Math.Max(1, mainR))
                    throw ToolException.Invalid($"{tool}: the first point is not on pipe {connectTo} (it is {Math.Round(MepLogic.Dist(at, pts[0]) * f)} mm from its centre line; "
                        + $"there the centre line is at x {Math.Round(at[0] * f)}, y {Math.Round(at[1] * f)}, z {Math.Round(at[2] * f)} mm, a sloping pipe is lower further on): start the route on that pipe.");
                var dir = MepLogic.Unit(MepLogic.Sub(pts[1], at));
                teeAt = at;
                pts[0] = MepLogic.Add(at, MepLogic.Mul(dir, mainR)); // the branch starts at the main pipe's side
            }

            double bend = MepLogic.BendRadius(sys, sa, f, p.Has("bend_radius") ? p.Num("bend_radius") : (double?)null);
            var pieces = MepLogic.Layout(pts, bend, tool);
            // KTP-17: the bend radius must be more than half the section in the plane of each bend (else the inside folds over)
            double need = pieces.Where(x => x.Kind == "elbow").Select(x => MepLogic.HalfInBendPlane(x, sys.Rect, sa / f, sb / f)).DefaultIfEmpty(0).Max();
            if (need > 0 && bend <= need * (1 + 1e-9))
            {
                if (p.Has("bend_radius"))
                    throw ToolException.Invalid($"{tool}: bend_radius {MepLogic.Num(bend * f)} mm is not more than half the section in the plane of a bend ({MepLogic.Num(need * f)} mm): give a bend_radius over {MepLogic.Num(need * f)} mm.");
                // a duct turning up / down across its taller side: the default radius takes that side (DuctBendFactor × it)
                bend = H7Numbers.DuctBendFactor * 2 * need;
                warnings.Add($"The bend radius is {MepLogic.Num(bend * f)} mm (the side of the duct across the bend), not the usual {MepLogic.Num(H7Numbers.DuctBendFactor * sa)} mm (its width).");
                pieces = MepLogic.Layout(pts, bend, tool);
            }
            if (sys.Rect && pieces.Any(x => x.Twist))
                warnings.Add("The duct runs in two directions round a riser: its section turns at the top of the riser (a real duct needs a transition piece there). Tell the user.");
            string layer = p.Str("layer", sys.Layer3d);
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, sys.Color)) layersCreated.Add(layer);
            var layerP = LayerParams(tool, layer);
            string route = Guid.NewGuid().ToString("N").Substring(0, 12);
            var handles = new List<object>();
            double lengthMm = 0;
            int elbows = 0;
            foreach (var pc in pieces)
            {
                var solid = pc.Kind == "pipe" ? PipeSolid(pc.A, pc.B, sys.Rect, sa / f, sb / f, 1, pc.W) : ElbowSolid(pc, sys.Rect, sa / f, sb / f);
                var id = AddSolid(ctx, solid, layerP);
                var ent = (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                WriteMep(ctx.Tr, ent, new MepRecord
                {
                    Mark = new MepMark { System = sys.Key, Kind = pc.Kind, Shape = sys.Rect ? "rect" : "round", Size1 = sa, Size2 = sb, LengthMm = pc.Length * f },
                    Route = route, Link = main?.Route, A = pc.A, B = pc.B,
                });
                handles.Add(Acad.H(id));
                lengthMm += pc.Length * f;
                if (pc.Kind == "elbow") elbows++;
            }
            string tee = null;
            if (teeAt != null)
            {
                var ax = MepLogic.Unit(MepLogic.Sub(main.B, main.A));
                double teeLen = H7Numbers.TeeLengthFactor * (sys.Rect ? sa : sa) / f;
                bool mainRect = main.Mark.Shape == "rect";
                var t = PipeSolid(MepLogic.Sub(teeAt, MepLogic.Mul(ax, teeLen / 2)), MepLogic.Add(teeAt, MepLogic.Mul(ax, teeLen / 2)), mainRect,
                    main.Mark.Size1 / f, main.Mark.Size2 / f, H7Numbers.FittingOversize);
                var id = AddSolid(ctx, t, layerP);
                WriteMep(ctx.Tr, (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite), new MepRecord
                {
                    Mark = new MepMark { System = sys.Key, Kind = "tee", Shape = sys.Rect ? "rect" : "round", Size1 = sa, Size2 = sb, LengthMm = 0 },
                    Route = route, Link = main.Route,
                });
                tee = Acad.H(id);
                handles.Add(tee);
            }
            var res = new Dictionary<string, object>
            {
                ["handles"] = handles,
                ["system"] = sys.Key,
                ["size_mm"] = sys.Rect ? $"{MepLogic.Num(sa)} × {MepLogic.Num(sb)}" : "D" + MepLogic.Num(sa),
                ["layer"] = layer,
                ["pipes"] = pieces.Count(x => x.Kind == "pipe"),
                ["elbows"] = elbows,
                ["length_m"] = Math.Round(lengthMm / 1000, H7Numbers.LengthDecimals),
                ["start_z_mm"] = Acad.R(pts[0][2] * f),
                ["end_z_mm"] = Acad.R(pts[pts.Count - 1][2] * f),
                ["bend_radius_mm"] = Acad.R(bend * f),
                ["note"] = "Drawn at the size given (the plugin does not size pipes or work out flows). Check with capture_view (sw_iso); mep_report gives the schedule and the check against beams / columns.",
            };
            if (slope.HasValue && slope > 0 && !given3d) res["slope_percent"] = slope.Value;
            if (realSlope.HasValue) res["least_slope_percent"] = Math.Round(realSlope.Value, 2);
            if (tee != null) res["tee"] = tee;
            if (layersCreated.Count > 0) res["layers_created"] = layersCreated;
            if (warnings.Count > 0) res["warnings"] = warnings.Cast<object>().ToList();
            return res;
        }

        /// <summary>A straight round pipe (or rectangular duct) A → B (drawing units), its section × oversize.</summary>
        private static Solid3d PipeSolid(double[] a, double[] b, bool rect, double s1, double s2, double oversize, double[] carriedW = null)
        {
            var dir = MepLogic.Sub(b, a);
            double len = MepLogic.Norm(dir);
            var (w, h) = MepLogic.SectionOf(dir, carriedW); // a duct keeps the width axis of the route (no twist on a riser)
            var z = MepLogic.Unit(dir);
            var mid = MepLogic.Mul(MepLogic.Add(a, b), 0.5);
            return Build(s =>
            {
                if (rect) s.CreateBox(s1 * oversize, s2 * oversize, len);
                else s.CreateFrustum(len, s1 / 2 * oversize, s1 / 2 * oversize, s1 / 2 * oversize);
                s.TransformBy(Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                    new Point3d(mid[0], mid[1], mid[2]), V(w), V(h), V(z)));
            });
        }

        /// <summary>An elbow: its section at the start, turned about the elbow's axis through its centre.</summary>
        private static Solid3d ElbowSolid(MepPiece e, bool rect, double s1, double s2)
        {
            var (w, h) = MepLogic.SectionOf(e.StartDir, e.W);
            var c = new Point3d(e.Start[0], e.Start[1], e.Start[2]);
            var curves = new DBObjectCollection();
            if (rect)
            {
                Point3d Corner(double a, double b) => c + V(w) * (a * s1 / 2) + V(h) * (b * s2 / 2);
                var q = new[] { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) };
                for (int i = 0; i < 4; i++) curves.Add(new Line(q[i], q[(i + 1) % 4]));
            }
            else curves.Add(new Circle(c, V(e.StartDir), s1 / 2));
            try
            {
                var regions = Region.CreateFromCurves(curves);
                using (var region = (Region)regions[0])
                {
                    for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                    return Build(s => s.Revolve(region, new Point3d(e.Center[0], e.Center[1], e.Center[2]), V(e.Axis), e.Angle));
                }
            }
            finally { foreach (DBObject o in curves) o.Dispose(); }
        }

        private static Vector3d V(double[] v) => new Vector3d(v[0], v[1], v[2]);

        /// <summary>
        /// under_beams: the centre height so the top of the pipe is TopBelowBeamMm under the lowest beam over the route (or
        /// TopBelowSlabMm under the lowest slab when there is no beam).
        /// </summary>
        private static double UnderBeams(ToolContext ctx, IList<double[]> plan, double half, double f, List<string> warnings, string tool)
        {
            double x0 = plan.Min(q => q[0]), x1 = plan.Max(q => q[0]), y0 = plan.Min(q => q[1]), y1 = plan.Max(q => q[1]);
            double? beam = null, slab = null;
            var beamBottoms = new List<double>();
            int n = 0;
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!id.ObjectClass.IsDerivedFrom(SolidClass)) continue; // 2D lines and texts are not counted (only solids)
                if (++n > H7Numbers.MaxSolids) break;
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Solid3d s)) continue;
                var ex = EntityInfo.Extents(s);
                if (!ex.HasValue) continue;
                var mn = ex.Value.MinPoint; var mx = ex.Value.MaxPoint;
                if (mx.X < x0 || mn.X > x1 || mx.Y < y0 || mn.Y > y1) continue;
                // KTP (nghi vấn): an L route's box holds beams that no segment passes under: only a beam over a segment counts
                bool over = false;
                for (int i = 1; i < plan.Count && !over; i++)
                {
                    double sx0 = Math.Min(plan[i - 1][0], plan[i][0]), sx1 = Math.Max(plan[i - 1][0], plan[i][0]);
                    double sy0 = Math.Min(plan[i - 1][1], plan[i][1]), sy1 = Math.Max(plan[i - 1][1], plan[i][1]);
                    over = !(mx.X < sx0 || mn.X > sx1 || mx.Y < sy0 || mn.Y > sy1);
                }
                if (!over) continue;
                var l = (s.Layer ?? "").ToUpperInvariant();
                if (l.Contains("BEAM") || l.Contains("DAM")) { beam = beam.HasValue ? Math.Min(beam.Value, mn.Z) : mn.Z; beamBottoms.Add(mn.Z); }
                else if (l.Contains("SLAB") || l.Contains("SAN")) slab = slab.HasValue ? Math.Min(slab.Value, mn.Z) : mn.Z;
            }
            if (beam.HasValue)
            {
                // Test & sửa H7: beams of two floors over the route: the lowest one is used; say so (the pipe may belong upstairs)
                if (beamBottoms.Max() - beamBottoms.Min() > 1000 / f)
                    warnings.Add($"Beams at several heights over the route (bottoms {string.Join(", ", beamBottoms.Select(z => Math.Round(z * f)).Distinct().OrderBy(z => z).Take(5))} mm): the lowest was used. On another floor give elevation instead.");
                return beam.Value - H7Numbers.TopBelowBeamMm / f - half;
            }
            if (slab.HasValue)
            {
                warnings.Add("No beam over the route: the pipe goes under the slab.");
                return slab.Value - H7Numbers.TopBelowSlabMm / f - half;
            }
            throw ToolException.Invalid($"{tool}: no beam or slab solid over the route (layers with BEAM / DAM / SLAB / SAN): give elevation (mm) instead.");
        }

        // ------------------------------------------------------------ mep_report

        internal static object MepReport(ToolContext ctx, Params p)
        {
            const string tool = MepLogic.ReportTool;
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var action = (p.Str("action", "schedule") ?? "schedule").Trim().ToLowerInvariant();
            if (action != "schedule" && action != "clash") throw ToolException.Invalid($"{tool}: action must be schedule or clash.");
            var only = p.StringsOpt("systems")?.Select(x => MepLogic.SystemOf(x, tool).Key).ToHashSet();
            var mep = new List<(string h, Solid3d s, MepRecord r)>();
            var others = new List<(string h, Solid3d s)>();
            int read = 0;
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!id.ObjectClass.IsDerivedFrom(SolidClass)) continue; // only the 3D solids count towards the limit
                if (++read > H7Numbers.MaxSolids) break;
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead, false) is Solid3d s)) continue;
                var r = ReadMep(ctx.Tr, s);
                if (r != null) { if (only == null || only.Contains(r.Mark.System)) mep.Add((Acad.H(id), s, r)); }
                else others.Add((Acad.H(id), s));
            }
            var res = new Dictionary<string, object> { ["action"] = action, ["mep_solids"] = mep.Count };
            if (read > H7Numbers.MaxSolids) res["note_limit"] = $"Only the first {H7Numbers.MaxSolids} 3D solids of the drawing were read.";
            if (action == "schedule")
            {
                var rows = MepLogic.Schedule(mep.Select(x => x.r.Mark));
                var waste = MepLogic.WasteShare(p.Has("waste_percent") ? p.Num("waste_percent") : (double?)null, tool);
                res["rows"] = rows.Select(r => (object)new Dictionary<string, object>
                {
                    ["system"] = r.System, ["size"] = r.Size, ["length_m"] = Math.Round(r.LengthM, H7Numbers.LengthDecimals),
                    ["pipes"] = r.Pipes, ["elbows"] = r.Elbows, ["tees"] = r.Tees,
                }).ToList();
                if (waste.HasValue) res["waste_percent"] = waste.Value * 100;
                if (rows.Count == 0) res["note"] = "No pipe or duct drawn by draw_mep in this drawing.";
                else ExportExcel(ctx, p, tool, () => MepLogic.ScheduleTable(rows, waste), res);
                return res;
            }

            // clash: each pipe against beams / columns (or other_handles) and against the pipes of other routes
            double clearance = (p.Has("clearance") ? p.Num("clearance") : H7Numbers.ClearanceMm) / f;
            var targets = new List<(string h, Solid3d s, MepRecord r)>();
            if (p.Has("other_handles"))
            {
                foreach (var h in p.Handles("other_handles", H7Numbers.MaxSolids).Select(x => x.ToUpperInvariant()).Distinct())
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    if (!(e is Solid3d sd)) throw ToolException.Invalid($"{tool}: {h} is a {Acad.TypeName(e)}, not a 3D solid.");
                    targets.Add((h, sd, ReadMep(ctx.Tr, sd)));
                }
            }
            else targets.AddRange(others.Where(o => MepLogic.IsStructureLayer(o.s.Layer)).Select(o => (o.h, o.s, (MepRecord)null)));
            if (p.Bool("pipes", true)) targets.AddRange(mep);
            var found = new List<object>();
            int clashes = 0, close = 0, checkedPairs = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in mep)
            {
                var ea = EntityInfo.Extents(a.s);
                if (!ea.HasValue) continue;
                foreach (var b in targets)
                {
                    if (b.h == a.h) continue;
                    if (b.r != null && (b.r.Route == a.r.Route || b.r.Link == a.r.Route || a.r.Link == b.r.Route)) continue; // one route / a branch on its main
                    var key = string.CompareOrdinal(a.h, b.h) < 0 ? a.h + "|" + b.h : b.h + "|" + a.h;
                    if (!seen.Add(key)) continue;
                    var eb = EntityInfo.Extents(b.s);
                    if (!eb.HasValue) continue;
                    double gap = MepLogic.BoxGap(P3(ea.Value.MinPoint), P3(ea.Value.MaxPoint), P3(eb.Value.MinPoint), P3(eb.Value.MaxPoint));
                    if (gap >= clearance) continue;
                    // KTP-14: two routes meeting end to end (one going on from the other, a size change) are joined, not close
                    if (IsStraight(a.r) && IsStraight(b.r) && MepLogic.EndToEnd(a.r.A, a.r.B, b.r.A, b.r.B, H7Numbers.JoinToleranceMm / f)) continue;
                    double boxGap = gap;
                    // a straight pipe: the gap from its axis (a slanted pipe's box is much bigger than the pipe). This one gates the
                    // solid check, so a duct reaches by half its diagonal here (it never skips a real clash)
                    if (IsStraight(a.r))
                        gap = Math.Max(boxGap, IsStraight(b.r)
                            ? MepLogic.PipePipeGap(a.r.A, a.r.B, Reach(a.r, f), b.r.A, b.r.B, Reach(b.r, f))
                            : MepLogic.PipeBoxGap(a.r.A, a.r.B, Reach(a.r, f), P3(eb.Value.MinPoint), P3(eb.Value.MaxPoint)));
                    if (gap >= clearance) continue;
                    checkedPairs++;
                    bool hit;
                    try { hit = a.s.CheckInterference(b.s); } catch (Autodesk.AutoCAD.Runtime.Exception) { hit = false; }
                    if (hit) { var v = OverlapVolume(a.s, b.s); if (!v.HasValue || v.Value <= 0) hit = false; }
                    // KTP-13: no overlap: the gap reported is from the real section (a duct under a beam: half its height up)
                    if (!hit && IsStraight(a.r))
                        gap = Math.Max(boxGap, IsStraight(b.r)
                            ? MepLogic.SectionSectionGap(a.r.A, a.r.B, a.r.Mark.Shape == "rect", a.r.Mark.Size1 / f, a.r.Mark.Size2 / f, b.r.A, b.r.B, b.r.Mark.Shape == "rect", b.r.Mark.Size1 / f, b.r.Mark.Size2 / f)
                            : MepLogic.SectionBoxGap(a.r.A, a.r.B, a.r.Mark.Shape == "rect", a.r.Mark.Size1 / f, a.r.Mark.Size2 / f, P3(eb.Value.MinPoint), P3(eb.Value.MaxPoint)));
                    var kind = MepLogic.ClashKind(hit, gap, clearance);
                    if (kind == null) continue;
                    if (kind == "clash") clashes++; else close++;
                    if (found.Count < H7Numbers.MaxClashRows)
                        found.Add(new Dictionary<string, object>
                        {
                            ["pipe"] = a.h, ["system"] = a.r.Mark.System, ["size"] = MepLogic.SizeText(a.r.Mark), ["with"] = b.h, ["with_layer"] = b.s.Layer,
                            ["kind"] = kind, ["gap_mm"] = kind == "clash" ? 0 : Acad.R(gap * f),
                        });
                }
            }
            res["clashes"] = clashes;
            res["close"] = close;
            res["clearance_mm"] = Acad.R(clearance * f);
            res["pairs_checked"] = checkedPairs;
            res["found"] = found;
            res["note"] = "clash = the solids overlap; close = closer than the clearance (a straight pipe: from its centre line to the beam's box or the other pipe; elbows and tees: between boxes). Beams / columns are the solids on layers with BEAM / COL / DAM / COT / S- unless other_handles is given.";
            if (found.Count > 0)
                ExportExcel(ctx, p, tool, () =>
                {
                    var t = new List<List<object>> { new List<object> { "Ống", "Hệ", "Cỡ", "Với", "Layer", "Loại", "Khe hở (mm)" } };
                    foreach (Dictionary<string, object> r in found)
                        t.Add(new List<object> { r["pipe"], MepLogic.SystemName((string)r["system"]), r["size"], r["with"], r["with_layer"], (string)r["kind"] == "clash" ? "Chạm" : "Sát", r["gap_mm"] });
                    return t;
                }, res, F4Numbers.MepClashKey);
            return res;
        }

        private static double[] P3(Point3d q) => new[] { q.X, q.Y, q.Z };

        private static readonly Autodesk.AutoCAD.Runtime.RXClass SolidClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Solid3d));

        private static bool IsStraight(MepRecord r) => r != null && r.Mark.Kind == "pipe" && r.A != null && r.B != null && MepLogic.Dist(r.A, r.B) > 0;

        /// <summary>The half size of a pipe's section (drawing units): its radius; a duct, half its diagonal (on the safe side).</summary>
        private static double Reach(MepRecord r, double f) =>
            (r.Mark.Shape == "rect" ? Math.Sqrt(r.Mark.Size1 * r.Mark.Size1 + r.Mark.Size2 * r.Mark.Size2) : r.Mark.Size1) / 2 / f;
    }
}
