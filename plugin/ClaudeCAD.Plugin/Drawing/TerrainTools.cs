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
    /// Nâng cấp đợt H6 (hạ tầng): <c>terrain</c>. import_points (survey sheet CSV / TXT / .xlsx → points with their level,
    /// VN-2000 axes swapped), contours (TIN of the points, major every n-th), stations (chainage ticks on the plan),
    /// profile (long section along an alignment), cross_sections (at the stakes). Only the drawing changes (undo: U), no
    /// file is written, so there is no card. The survey points are DBPoints on C-SURVEY-PT at their level (Z); the
    /// surface is made from them each call. Numbers: Core/Terrain.cs H6Numbers (the standard ones wait for approval).
    /// </summary>
    public static partial class DrawingService
    {
        public static readonly string[] TerrainActions = { "import_points", "contours", "stations", "profile", "cross_sections" };

        private static void RegisterTerrain(ToolDispatcher d)
        {
            d.Register(new ToolDef("terrain", true, TerrainTool));
        }

        internal static object TerrainTool(ToolContext ctx, Params p)
        {
            const string tool = "terrain";
            var action = p.Str("action").Trim().ToLowerInvariant();
            if (!TerrainActions.Contains(action)) throw ToolException.Invalid($"{tool}: action must be one of {string.Join(", ", TerrainActions)}.");
            ctx.RequireWritable(tool);
            // Test & sửa H6: metres → drawing units from the drawing's units (a drawing in metres is common for civil works)
            double mmPerUnit = 1;
            try { mmPerUnit = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _); } catch (Exception) { }
            using (H6Numbers.ForDrawing(mmPerUnit))
            {
                switch (action)
                {
                    case "import_points": return ImportSurvey(ctx, p);
                    case "contours": return DrawContours(ctx, p);
                    case "stations": return DrawStations(ctx, p);
                    case "profile": return DrawProfile(ctx, p);
                    default: return DrawCrossSections(ctx, p);
                }
            }
        }

        private static int PlanScale(Params p) => p.Int("scale", H6Numbers.PlanScaleDefault, 1, 100000);

        // ------------------------------------------------------------------ import_points

        private static object ImportSurvey(ToolContext ctx, Params p)
        {
            const string tool = "terrain";
            var path = (p.Str("file") ?? "").Trim().Trim('"');
            CheckPathAllowed(ctx, path, AttachmentFolder(), tool);
            var plain = TraceLogic.PlainPath(path) ?? path;
            var rows = SurveyReader.ReadFile(Path.GetFullPath(plain));
            SurveyReader.Columns given = null;
            if (p.Get("columns") is Dictionary<string, object> c)
            {
                int Col(string k) => c.TryGetValue(k, out var v) && v is double d && d >= 1 && d <= 200 ? (int)d - 1 : -1;
                given = new SurveyReader.Columns { Name = Col("name"), X = Col("x"), Y = Col("y"), Z = Col("z"), Code = Col("code") };
                if (given.X < 0 || given.Y < 0) throw ToolException.Invalid($"{tool}: columns needs x and y (column numbers from 1).");
            }
            var f = SurveyReader.Interpret(rows, given);
            if (f.Points.Count == 0)
                throw ToolException.Invalid($"{tool}: no point read from the file ({f.Columns}). {string.Join("; ", f.Skipped.Take(5))}. Give columns {{name, x, y, z}} (column numbers).");
            bool swap = p.Bool("swap_axes", H6Numbers.AxisSwap);
            // the origin: real coordinates (10^9 mm) are kept only when asked
            double x0 = 0, y0 = 0; string originNote = null;
            var o = p.Get("origin");
            if (o is string os && os.Trim().ToLowerInvariant() == "auto") { (x0, y0) = SurveyReader.AutoOrigin(f.Points); originNote = "auto"; }
            else if (o is string os2 && os2.Trim().ToLowerInvariant() == "real") { }
            else if (Params.TryPoint(o, out var op)) { x0 = op[0]; y0 = op[1]; originNote = "given"; }
            else if (o != null) throw ToolException.Invalid($"{tool}: origin is \"auto\", \"real\" or [X, Y] in metres (survey axes).");
            else if (SurveyReader.IsReal(f.Points))
                throw ToolException.Invalid($"{tool}: the points have real VN-2000 coordinates (hundreds of thousands of metres). Ask the user once: " +
                    "draw at the real coordinates (origin \"real\"), or from a local origin (origin \"auto\" = the lowest X / Y rounded down to 100 m, or [X, Y]). Nothing was drawn.");
            int scale = PlanScale(p);
            double th = H6Numbers.Paper(H6Numbers.TextMm, scale);
            bool labels = p.Bool("labels", true), names = p.Bool("names", true);
            var lp = EnsureLayerH6(ctx, p.Str("points_layer", H6Numbers.LayerPoint.name), H6Numbers.LayerPoint.color);
            var ll = EnsureLayerH6(ctx, H6Numbers.LayerLevel.name, H6Numbers.LayerLevel.color);
            var ln = EnsureLayerH6(ctx, H6Numbers.LayerPointName.name, H6Numbers.LayerPointName.color);
            var xy = f.Points.Select(pt => { var d = SurveyReader.ToDrawing(pt, x0, y0, swap); return (d.x, d.y); }).ToList();
            var dups = new HashSet<int>(SurveyReader.Duplicates(xy, H6Numbers.SamePointM * H6Numbers.UnitsPerMetre));
            int drawn = 0;
            for (int i = 0; i < f.Points.Count; i++)
            {
                if (dups.Contains(i)) continue;
                var sp = f.Points[i];
                var d = SurveyReader.ToDrawing(sp, x0, y0, swap);
                var pt = new DBPoint(new Point3d(d.x, d.y, sp.Z.HasValue ? d.z : 0));
                pt.SetDatabaseDefaults(ctx.Db);
                pt.LayerId = lp;
                Acad.Add(ctx, pt, null, defaults: false);
                if (labels && sp.Z.HasValue) AddH6Text(ctx, ll, d.x + th * 0.4, d.y - th * 0.6, Stations.Level(sp.Z.Value, H6Numbers.DecPoint), th, 0, 1);
                if (names) AddH6Text(ctx, ln, d.x + th * 0.4, d.y + th * 0.6, sp.Name, th * 0.8, 0, 1);
                drawn++;
            }
            if (originNote != null)
            {
                var nl = EnsureLayerH6(ctx, H6Numbers.LayerNote.name, H6Numbers.LayerNote.color);
                double minx = xy.Min(v => v.x), miny = xy.Min(v => v.y);
                AddH6Text(ctx, nl, minx, miny - th * 4, $"Gốc tọa độ: X = {x0.ToString("0.000", CultureInfo.InvariantCulture)}, Y = {y0.ToString("0.000", CultureInfo.InvariantCulture)} (VN-2000)", th, 0, 1);
            }
            var r = new Dictionary<string, object>
            {
                ["points"] = drawn,
                ["read_rows"] = rows.Count,
                ["columns"] = f.Columns,
                ["with_level"] = f.HasZ,
                ["axes"] = swap ? "VN-2000: X (North) → drawing y, Y (East) → drawing x, metres × 1000 (only the axes are swapped, no projection change)" : "not swapped: X → drawing x, Y → drawing y, metres × 1000",
                ["origin"] = new List<object> { x0, y0 },
                ["layer"] = p.Str("points_layer", H6Numbers.LayerPoint.name),
                ["note"] = "Points are at their level (Z). terrain contours / profile / cross_sections read them from this layer. Remind the user this is a drawing aid: the engineer checks it.",
            };
            if (originNote != null) r["origin_note"] = "A note 'Gốc tọa độ: …' was written under the points.";
            if (f.Skipped.Count > 0) { r["skipped"] = f.Skipped.Count; r["skipped_rows"] = f.Skipped.Take(20).Cast<object>().ToList(); }
            if (dups.Count > 0) r["duplicates_dropped"] = dups.Count;
            if (f.Warnings.Count > 0) r["warnings"] = f.Warnings.Cast<object>().ToList();
            if (!f.HasZ) r["no_level"] = "The file has no level (Z) column: the points are at level 0, no contours / sections can be made from them.";
            return r;
        }

        // ------------------------------------------------------------------ the surface

        private static Tin SurveyTin(ToolContext ctx, Params p, out int count)
        {
            var layer = p.Str("points_layer", H6Numbers.LayerPoint.name);
            var pts = new List<(double x, double y, double z)>();
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.ObjectClass.DxfName != "POINT") continue;
                var e = (DBPoint)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (!string.Equals(e.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
                pts.Add((e.Position.X, e.Position.Y, e.Position.Z));
                if (pts.Count > H6Numbers.MaxPoints)
                    throw ToolException.Invalid($"terrain: more than {H6Numbers.MaxPoints} points on layer {layer}.");
            }
            if (pts.Count < 3) throw ToolException.Invalid($"terrain: {pts.Count} survey points on layer {layer}: import them first (terrain import_points), or give points_layer.");
            var dup = new HashSet<int>(SurveyReader.Duplicates(pts.Select(q => (q.x, q.y)).ToList(), H6Numbers.SamePointM * H6Numbers.UnitsPerMetre));
            pts = pts.Where((q, i) => !dup.Contains(i)).ToList();
            count = pts.Count;
            double maxEdge = p.Has("max_edge") ? p.Num("max_edge") * H6Numbers.UnitsPerMetre : -1;
            if (maxEdge < 0 && p.Has("max_edge")) throw ToolException.Invalid("terrain: max_edge is 0 (keep every triangle) or a length in metres.");
            return Tin.Build(pts, maxEdge);
        }

        private static object DrawContours(ToolContext ctx, Params p)
        {
            int scale = PlanScale(p);
            double interval = p.Has("interval") ? p.Positive("interval") : H6Numbers.ContourIntervalFor(scale);
            int major = p.Int("major_every", H6Numbers.MajorEvery, 0, 100);
            var tin = SurveyTin(ctx, p, out int n);
            double k = H6Numbers.UnitsPerMetre, th = H6Numbers.Paper(H6Numbers.TextMm, scale);
            var lines = Contouring.Trace(tin, interval * k, major);
            var lmin = EnsureLayerH6(ctx, H6Numbers.LayerMinor.name, H6Numbers.LayerMinor.color);
            var lmaj = EnsureLayerH6(ctx, H6Numbers.LayerMajor.name, H6Numbers.LayerMajor.color, LineWeight.LineWeight035);
            bool labels = p.Bool("labels", true);
            int minor = 0, majors = 0, labelled = 0;
            foreach (var c in lines)
            {
                var pl = new Polyline(c.Points.Count);
                for (int i = 0; i < c.Points.Count; i++) pl.AddVertexAt(i, new Point2d(c.Points[i].x, c.Points[i].y), 0, 0, 0);
                pl.Closed = c.Closed;
                pl.Elevation = c.Level;
                pl.SetDatabaseDefaults(ctx.Db);
                pl.LayerId = c.Major ? lmaj : lmin;
                Acad.Add(ctx, pl, null, defaults: false);
                if (c.Major) majors++; else minor++;
                if (labels && c.Major)
                {
                    var s = Contouring.LabelSpot(c.Points);
                    if (s.length >= th * 8)
                    {
                        AddH6Text(ctx, lmaj, s.x, s.y, Stations.Number(c.Level / k, H6Numbers.DecSection), th, s.angle, 0);
                        labelled++;
                    }
                }
            }
            return new Dictionary<string, object>
            {
                ["points"] = n,
                ["triangles"] = tin.Triangles.Count,
                ["triangles_dropped"] = tin.Dropped,
                ["interval_m"] = interval,
                ["levels_m"] = new List<object> { Math.Round(tin.MinZ / k, 3), Math.Round(tin.MaxZ / k, 3) },
                ["minor"] = minor, ["major"] = majors, ["labelled"] = labelled,
                ["layers"] = new List<object> { H6Numbers.LayerMinor.name, H6Numbers.LayerMajor.name },
                ["note"] = "Straight lines between the triangle edges (no smoothing); each polyline is at its level. " +
                    (tin.Dropped > 0 ? $"{tin.Dropped} long triangles at the edge of the survey were left out (max_edge 0 keeps them). " : "") +
                    "Calling again adds new lines (erase the old ones first). A drawing aid: the engineer checks it.",
            };
        }

        // ------------------------------------------------------------------ the alignment

        private sealed class Alignment
        {
            public Curve Curve; public double Length; public List<double> Vertices = new List<double>();
            public double Start; public string Handle;
            public (Point3d p, Vector3d t) At(double s)
            {
                s = Math.Max(0, Math.Min(Length, s));
                var pt = Curve.GetPointAtDist(s);
                var d = Curve.GetFirstDerivative(Curve.GetParameterAtPoint(pt));
                var t = new Vector3d(d.X, d.Y, 0);
                t = t.Length > 1e-12 ? t.GetNormal() : Vector3d.XAxis;
                return (new Point3d(pt.X, pt.Y, 0), t);
            }
        }

        private static Alignment GetAlignment(ToolContext ctx, Params p)
        {
            var h = p.Str("alignment_handle");
            var e = Acad.GetEntity(ctx, h, false);
            if (!(e is Curve c) || e is Ray || e is Xline)
                throw ToolException.Invalid($"terrain: alignment {h} is a {e.GetType().Name}; give the handle of the centre line (polyline / line / arc).");
            var a = new Alignment { Curve = c, Handle = h, Start = p.Num("start_chainage", 0) };
            a.Length = c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam);
            a.Length /= H6Numbers.UnitsPerMetre;
            if (c is Polyline pl)
                for (int i = 1; i < pl.NumberOfVertices - 1; i++) a.Vertices.Add(pl.GetDistanceAtParameter(i) / H6Numbers.UnitsPerMetre);
            if (!(a.Length > 0)) throw ToolException.Invalid($"terrain: alignment {h} has no length.");
            return a;
        }

        private static object DrawStations(ToolContext ctx, Params p)
        {
            var a = GetAlignment(ctx, p);
            int scale = PlanScale(p);
            double step = p.Has("step") ? p.Positive("step") : H6Numbers.PlanTickM;
            var stakes = Stations.Stakes(a.Length, a.Vertices, a.Start, step);
            double k = H6Numbers.UnitsPerMetre, th = H6Numbers.Paper(H6Numbers.TextMm, scale), half = H6Numbers.Paper(H6Numbers.PlanTickMm, scale) / 2;
            var ls = EnsureLayerH6(ctx, H6Numbers.LayerStation.name, H6Numbers.LayerStation.color);
            foreach (var st in stakes)
            {
                var (pt, t) = a.At(st.S * k);
                var nrm = new Vector3d(-t.Y, t.X, 0);
                var line = new Line(pt - nrm * half, pt + nrm * half);
                line.SetDatabaseDefaults(ctx.Db); line.LayerId = ls;
                Acad.Add(ctx, line, null, defaults: false);
                var tp = pt + nrm * (half + th * 0.4);
                double rot = Contouring.Readable(Math.Atan2(nrm.Y, nrm.X));
                // Test & sửa H6: "C1, C2…" are the 20 m detail stakes of the sections; a plan tick at another step shows its
                // chainage only, so a name never means two places
                string label = Stations.PlanLabel(st, step);
                AddH6Text(ctx, ls, tp.X, tp.Y, label, th, rot, Math.Abs(rot - Math.Atan2(nrm.Y, nrm.X)) < 1e-9 ? 1 : 2);
            }
            return new Dictionary<string, object>
            {
                ["stakes"] = stakes.Count, ["length_m"] = Math.Round(a.Length, 3),
                ["from"] = Stations.Chainage(a.Start), ["to"] = Stations.Chainage(a.Start + a.Length), ["step_m"] = step,
                ["layer"] = H6Numbers.LayerStation.name,
            };
        }

        private static List<(double ch, double z)> DesignParam(Params p)
        {
            if (!p.Has("design")) return null;
            if (!(p.Get("design") is List<object> l) || l.Count < 2) throw ToolException.Invalid("terrain: design is [[chainage m, level m], …] with at least 2 points.");
            var r = new List<(double, double)>();
            foreach (var v in l)
            {
                if (!Params.TryPoint(v, out var q)) throw ToolException.Invalid("terrain: each design point is [chainage m, level m].");
                r.Add((q[0], q[1]));
            }
            return r;
        }

        private static (double x, double y) PlaceBelow(ToolContext ctx, Params p, double gap)
        {
            if (Params.TryPoint(p.Get("position"), out var pos)) return (pos[0], pos[1]);
            try
            {
                var ext = ctx.Db.Extmax; var mn = ctx.Db.Extmin;
                if (ext.X > mn.X) return (mn.X, mn.Y - gap);
            }
            catch (Exception) { }
            return (0, 0);
        }

        private static object DrawProfile(ToolContext ctx, Params p)
        {
            var a = GetAlignment(ctx, p);
            var tin = SurveyTin(ctx, p, out _);
            int sh = p.Int("scale_h", H6Numbers.ProfileScaleH, 1, 100000), sv = p.Int("scale_v", H6Numbers.ProfileScaleV, 1, 100000);
            double step = p.Has("step") ? p.Positive("step") : H6Numbers.StakeDetailM;
            var stakes = Stations.Stakes(a.Length, a.Vertices, a.Start, step);
            double k = H6Numbers.UnitsPerMetre;
            var ground = stakes.Select(s => { var (pt, _) = a.At(s.S * k); var z = tin.ZAt(pt.X, pt.Y); return z.HasValue ? z / k : null; }).ToList();
            var design = DesignParam(p);
            var dz = design == null ? null : stakes.Select(s => Stations.DesignAt(design, s.Ch)).ToList();
            double? datum = p.NumOpt("datum");
            var all = ground.Concat(dz ?? new List<double?>()).Where(z => z.HasValue).Select(z => z.Value).ToList();
            if (all.Count == 0) throw ToolException.Invalid("terrain: no stake of the alignment is on the surface (the alignment is outside the survey points).");
            if (datum.HasValue && datum.Value > all.Min()) throw ToolException.Invalid($"terrain: datum {datum} is above the lowest level {all.Min():0.00}: give a lower datum or none.");
            var (bx, by) = PlaceBelow(ctx, p, H6Numbers.Paper(H6Numbers.GapMm, sh) * 4);
            var title = p.Str("title", "TRẮC DỌC TUYẾN");
            var draw = SectionLayout.Profile(stakes, ground, dz, bx, by, sh, sv, datum, title, design == null ? null : Stations.Grades(design));
            if (!Params.TryPoint(p.Get("position"), out _))
            {
                // below the drawing: the frame goes down by its height
                foreach (var pr in draw.Prims) for (int i = 0; i < pr.Pts.Count; i++) pr.Pts[i] = (pr.Pts[i].x, pr.Pts[i].y - draw.Height);
                by -= draw.Height;
            }
            DrawPrims(ctx, draw.Prims);
            var off = stakes.Where((s, i) => !ground[i].HasValue).Select(s => s.Name).ToList();
            var r = new Dictionary<string, object>
            {
                ["stakes"] = stakes.Count, ["length_m"] = Math.Round(a.Length, 3),
                ["datum"] = "MSS " + Stations.Level(draw.Datum, H6Numbers.DecSection),
                ["scale"] = $"1:{sh} horizontal, 1:{sv} vertical (drawn at true horizontal length, vertical × {(double)sh / sv:0.##})",
                ["position"] = new List<object> { bx, by }, ["size"] = new List<object> { Math.Round(draw.Width), Math.Round(draw.Height) },
                ["table"] = stakes.Select((s, i) => (object)new Dictionary<string, object>
                {
                    ["stake"] = s.Name, ["chainage"] = Stations.Chainage(s.Ch),
                    ["ground"] = ground[i].HasValue ? (object)Math.Round(ground[i].Value, H6Numbers.DecSection) : null,
                    ["design"] = dz != null && dz[i].HasValue ? (object)Math.Round(dz[i].Value, H6Numbers.DecSection) : null,
                }).Take(60).ToList(),
                ["note"] = "Ground levels are read on the surface of the survey points at each stake (straight between stakes). No cut / fill volumes. A drawing aid: the engineer checks it.",
            };
            if (off.Count > 0) r["off_surface"] = off.Take(30).Cast<object>().ToList();
            return r;
        }

        private static object DrawCrossSections(ToolContext ctx, Params p)
        {
            var a = GetAlignment(ctx, p);
            var tin = SurveyTin(ctx, p, out _);
            int sc = p.Int("scale_cross", H6Numbers.CrossScale, 1, 100000);
            double half = p.Has("half_width") ? p.Positive("half_width") : H6Numbers.CrossHalfWidthM;
            double step = p.Has("step") ? p.Positive("step") : H6Numbers.StakeDetailM;
            var stakes = Stations.Stakes(a.Length, a.Vertices, a.Start, step);
            if (p.Has("stakes"))
            {
                var want = new HashSet<string>((p.Get("stakes") as List<object> ?? new List<object>()).Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)), StringComparer.OrdinalIgnoreCase);
                stakes = stakes.Where(s => want.Contains(s.Name)).ToList();
                if (stakes.Count == 0) throw ToolException.Invalid("terrain: none of the stakes named is on the alignment (names like Km0, H1, C3, Đ1).");
            }
            if (stakes.Count > H6Numbers.MaxSections)
                throw ToolException.Invalid($"terrain: {stakes.Count} cross sections, at most {H6Numbers.MaxSections} in one call: give stakes, a bigger step or part of the alignment (start_chainage).");
            double k = H6Numbers.UnitsPerMetre, ds = H6Numbers.CrossSampleM;
            var sections = new List<(Stake st, List<(double off, double z)> pts)>();
            var missing = new List<string>();
            foreach (var st in stakes)
            {
                var (pt, t) = a.At(st.S * k);
                var left = new Vector3d(-t.Y, t.X, 0);
                var samples = new List<(double off, double? z)>();
                int m = (int)Math.Ceiling(half / ds - 1e-9);
                for (int i = -m; i <= m; i++)
                {
                    double off = Math.Max(-half, Math.Min(half, i * ds));
                    var q = pt - left * (off * k); // offsets: left of the centre line negative (drawn on the left)
                    var z = tin.ZAt(q.X, q.Y);
                    samples.Add((off, z.HasValue ? z / k : null));
                }
                var bp = SectionLayout.Breakpoints(samples);
                if (bp.Count < 2) { missing.Add(st.Name); continue; }
                sections.Add((st, bp));
            }
            if (sections.Count == 0) throw ToolException.Invalid("terrain: no cross section is on the surface (the alignment is outside the survey points).");
            // one grid: SectionsPerColumn per column, top to bottom, by chainage
            var (bx, by) = PlaceBelow(ctx, p, H6Numbers.Paper(H6Numbers.GapMm, sc) * 4);
            var drawn = sections.Select(s => SectionLayout.Cross(s.st, s.pts, half, 0, 0, sc, null)).ToList();
            double cellW = drawn.Max(d => d.Width) + H6Numbers.Paper(H6Numbers.GapMm, sc), cellH = drawn.Max(d => d.Height) + H6Numbers.Paper(H6Numbers.GapMm, sc);
            int per = Math.Max(1, H6Numbers.SectionsPerColumn);
            double labelW = H6Numbers.Paper(H6Numbers.CrossLabelMm, sc);
            for (int i = 0; i < drawn.Count; i++)
            {
                int col = i / per, row = i % per;
                double cx = bx + labelW + half * k + col * cellW, cy = by - (row + 1) * cellH;
                var d = SectionLayout.Cross(sections[i].st, sections[i].pts, half, cx, cy, sc, null);
                DrawPrims(ctx, d.Prims);
            }
            var r = new Dictionary<string, object>
            {
                ["sections"] = sections.Count,
                ["scale"] = $"1:{sc} (both directions)", ["half_width_m"] = half,
                ["position"] = new List<object> { bx, by }, ["per_column"] = per,
                ["stakes"] = sections.Select(s => (object)$"{s.st.Name} {Stations.Chainage(s.st.Ch)}").Take(60).ToList(),
                ["note"] = "Ground from the surface of the survey points, kept where the slope changes. Left of the centre line (looking along the chainage) is drawn on the left. No cut / fill. A drawing aid: the engineer checks it.",
            };
            if (missing.Count > 0) r["off_surface"] = missing.Take(30).Cast<object>().ToList();
            return r;
        }

        // ------------------------------------------------------------------ drawing helpers

        private static void DrawPrims(ToolContext ctx, IEnumerable<Prim> prims)
        {
            var layers = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            ObjectId L(string name)
            {
                if (layers.TryGetValue(name, out var id)) return id;
                int color = name == H6Numbers.LayerGround.name ? H6Numbers.LayerGround.color : name == H6Numbers.LayerDesign.name ? H6Numbers.LayerDesign.color
                    : name == H6Numbers.LayerText.name ? H6Numbers.LayerText.color : H6Numbers.LayerGrid.color;
                var lw = name == H6Numbers.LayerGround.name || name == H6Numbers.LayerDesign.name ? LineWeight.LineWeight035 : LineWeight.ByLineWeightDefault;
                return layers[name] = EnsureLayerH6(ctx, name, color, lw);
            }
            foreach (var pr in prims)
            {
                var lid = L(pr.Layer);
                if (pr.Kind == "text") { AddH6Text(ctx, lid, pr.Pts[0].x, pr.Pts[0].y, pr.Text, pr.Height, pr.Rotation, pr.Align); continue; }
                Entity e;
                if (pr.Kind == "line") e = new Line(new Point3d(pr.Pts[0].x, pr.Pts[0].y, 0), new Point3d(pr.Pts[1].x, pr.Pts[1].y, 0));
                else
                {
                    var pl = new Polyline(pr.Pts.Count);
                    for (int i = 0; i < pr.Pts.Count; i++) pl.AddVertexAt(i, new Point2d(pr.Pts[i].x, pr.Pts[i].y), 0, 0, 0);
                    e = pl;
                }
                e.SetDatabaseDefaults(ctx.Db);
                e.LayerId = lid;
                Acad.Add(ctx, e, null, defaults: false);
            }
        }

        private static void AddH6Text(ToolContext ctx, ObjectId layer, double x, double y, string text, double h, double rot, int align)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = new DBText();
            t.SetDatabaseDefaults(ctx.Db);
            t.TextStyleId = ctx.Db.Textstyle;
            t.TextString = text;
            t.Height = h;
            t.Rotation = rot;
            t.LayerId = layer;
            var pos = new Point3d(x, y, 0);
            t.Position = pos;
            t.Justify = align == 1 ? AttachmentPoint.MiddleLeft : align == 2 ? AttachmentPoint.MiddleRight : AttachmentPoint.MiddleCenter;
            t.AlignmentPoint = pos;
            Acad.Add(ctx, t, null, defaults: false);
            t.AdjustAlignment(ctx.Db);
        }

        /// <summary>Creates a layer when missing (an existing one is never changed).</summary>
        private static ObjectId EnsureLayerH6(ToolContext ctx, string name, int color, LineWeight lw = LineWeight.ByLineWeightDefault)
        {
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name) && !lt[name].IsErased) return lt[name];
            ValidateSymbolName(name, "layer");
            var rec = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)color),
                LineWeight = lw,
            };
            lt.UpgradeOpen();
            var id = lt.Add(rec);
            ctx.Tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }
    }
}
