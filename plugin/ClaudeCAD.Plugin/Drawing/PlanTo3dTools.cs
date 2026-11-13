using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// 3D phase 3: build_3d_from_plan builds walls (with door / window openings) and slabs from a 2D floor plan.
    /// It only READS the user's 2D objects: nothing of the plan is changed or erased. Recognition is FloorPlanLogic
    /// (Core/FloorPlan.cs, unit-tested); here the drawing is read and the solids are made. Labelled ToolGroup.ThreeD.
    /// Coordinates are WCS drawing units; heights / thicknesses are mm and scaled by INSUNITS.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterPlanTo3d(ToolDispatcher d)
        {
            // dry_run only reads: no version copy, no undo group, not recorded, allowed in "Chỉ xem" (KT-15)
            d.Register(new ToolDef("build_3d_from_plan", true, Build3dFromPlan, readsWhen: p => p.Bool("dry_run", false)));
            // nâng cấp đợt A: railing (lan can) along a path
            d.Register("build_railing", true, BuildRailing);
        }

        /// <summary>
        /// build_railing: a railing along a path (an existing line / polyline, or points), from RailingLogic boxes united
        /// into one solid on A-RAIL-3D. The path object is only read. Heights / sizes are mm, scaled by INSUNITS.
        /// </summary>
        internal static object BuildRailing(ToolContext ctx, Params p)
        {
            const string tool = "build_railing";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out var unitName);
            if (p.Has("path_handle") == p.Has("path_points"))
                throw ToolException.Invalid($"{tool}: pass either 'path_handle' (a line / polyline) or 'path_points' [[x, y], ...], not both.");
            var pts = new List<double[]>();
            bool closed = p.Bool("closed", false);
            double z = 0;
            string from = null;
            // only level railings: every point of the path at one z (stair railings come with the stairs)
            void Level(IEnumerable<double> zs, string what)
            {
                var list = zs.ToList();
                double tol = 1e-6 * Math.Max(1, list.Max(v => Math.Abs(v)));
                if (list.Any(v => Math.Abs(v - list[0]) > tol))
                    throw ToolException.Invalid($"{tool}: {what} is not level (its points have different z); only level railings are built. Nothing was built. Pass path_points [[x, y], ...] and base_z.");
            }
            if (p.Has("path_points"))
            {
                pts = p.Points("path_points", 2, RailingLogic.MaxPoints);
                var zs = pts.Where(q => q.Length > 2).Select(q => q[2]).ToList();
                if (zs.Count > 0)
                {
                    if (zs.Count != pts.Count) zs.Add(0); // points without a z are at z = 0
                    Level(zs, "the path");
                    z = pts[0].Length > 2 ? pts[0][2] : 0;
                }
            }
            else
            {
                from = p.Handle("path_handle").ToUpperInvariant();
                var e = Acad.GetEntity(ctx, from, false);
                var path = new List<Point3d>();
                switch (e)
                {
                    case Line l:
                        path.Add(l.StartPoint);
                        path.Add(l.EndPoint);
                        break;
                    case Polyline pl:
                    {
                        int n = pl.NumberOfVertices, segs = pl.Closed ? n : n - 1;
                        for (int i = 0; i < n; i++)
                        {
                            if (i < segs && Math.Abs(pl.GetBulgeAt(i)) > 1e-9)
                                throw ToolException.Invalid($"{tool}: {from} has arc segments; only straight segments are supported. Nothing was built. Pass path_points with short straight steps along the curve.");
                            path.Add(pl.GetPoint3dAt(i)); // WCS
                        }
                        closed = closed || pl.Closed;
                        break;
                    }
                    default:
                        throw ToolException.Invalid($"{tool}: {from} is a {Acad.TypeName(e)}; the path must be a line or a lightweight polyline with straight segments. Read its points with get_entity and pass them as path_points.");
                }
                if (path.Count < 2) throw ToolException.Invalid($"{tool}: {from} has fewer than 2 points.");
                Level(path.Select(q => q.Z), from);
                pts = path.Select(q => new[] { q.X, q.Y }).ToList();
                z = path[0].Z;
            }
            if (p.Has("base_z")) z = p.Num("base_z") / f;
            var o = new RailingLogic.Options
            {
                Height = p.Num("height", RailingLogic.DefaultHeight),
                PostSpacing = p.Num("post_spacing", RailingLogic.DefaultPostSpacing),
                Infill = p.Str("infill", "bars"),
            };
            // the layout is in mm: path points scaled to mm, boxes scaled back to drawing units
            var mmPts = pts.Select(q => new[] { q[0] * f, q[1] * f }).ToList();
            var lay = RailingLogic.Build(mmPts, closed, o, tool);
            string layer = p.Str("layer", RailingLogic.Layer3d);
            var layerP = LayerParams(tool, layer);
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, 3)) layersCreated.Add(layer);
            Solid3d rail = null;
            ObjectId railId = ObjectId.Null;
            var extra = new List<string>();
            foreach (var b in lay.Boxes)
            {
                var s = MakeBox(b.Length / f, b.Width / f, b.Height / f, null, new[] { b.Center[0] / f, b.Center[1] / f, z + b.Center[2] / f }, b.RotationDeg, tool);
                if (rail == null) { railId = AddSolid(ctx, s, layerP); rail = s; continue; }
                try { BooleanInto(rail, s, BooleanOperationType.BoolUnite); s.Dispose(); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { extra.Add(Acad.H(AddSolid(ctx, s, layerP))); }
                catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
            }
            var r = Acad.Handles(railId);
            var hs = new List<object> { Acad.H(railId) };
            hs.AddRange(extra);
            r["handles"] = hs;
            AddSolidNumbers(r, rail);
            double vol = VolumeOf(rail) + extra.Sum(h => VolumeOf((Solid3d)Acad.GetEntity(ctx, h, false)));
            r["volume_m3"] = Math.Round(vol * f * f * f / 1e9, 6);
            r["layer"] = layer;
            r["length_m"] = Math.Round(lay.Length / 1000, 3);
            r["height_mm"] = o.Height;
            r["infill"] = o.Infill;
            r["posts"] = lay.Posts;
            if (lay.Bars > 0) r["bars"] = lay.Bars;
            if (lay.Panels > 0) r["panels"] = lay.Panels;
            if (from != null) r["path"] = from;
            if (extra.Count > 0) r["separate_pieces"] = extra;
            if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
            r["note"] = $"Railing {o.Height} mm high (top rail {RailingLogic.RailSize} mm, posts {RailingLogic.PostSize} mm at most {o.PostSpacing} mm apart" +
                        (o.Infill == "bars" ? $", bars {RailingLogic.BarSize} mm with gaps ≤ {RailingLogic.MaxBarGap} mm" : o.Infill == "panel" ? $", panels {RailingLogic.PanelThickness} mm" : "") +
                        "), centred on the path; the path object was only read.";
            return r;
        }

        private sealed class PlanInput
        {
            public List<PlanSeg> Faces = new List<PlanSeg>();
            public List<PlanSeg> Centers = new List<PlanSeg>();
            public List<PlanSymbol> Symbols = new List<PlanSymbol>();
            public List<(ObjectId id, string handle)> Slabs = new List<(ObjectId, string)>();
            public List<PlanSkip> Skipped = new List<PlanSkip>();
            public int Read, Ignored;
            /// <summary>Polylines with a width below min_thickness (a print line weight) read as plain face lines.</summary>
            public int ThinWidePolylines;
            public PlanOptions Options = new PlanOptions();
            /// <summary>Existing solids on the result layers (rerun check).</summary>
            public List<Existing3d> Existing = new List<Existing3d>();
            /// <summary>Closed column outlines (nâng cấp đợt A).</summary>
            public List<(ObjectId id, string handle)> Columns = new List<(ObjectId, string)>();
            /// <summary>Objects on layers that were not read, per layer: for the layer guess (nâng cấp đợt A).</summary>
            public Dictionary<string, int> Unread = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> LayerList(Params p, string name, string[] def) =>
            p.Has(name) ? p.Strings(name, 1) : def.ToList();

        private static bool InArea(Extents3d? ext, double[] amin, double[] amax)
        {
            if (amin == null) return true;
            if (!ext.HasValue) return false;
            var e = ext.Value;
            return e.MinPoint.X >= amin[0] && e.MinPoint.Y >= amin[1] && e.MaxPoint.X <= amax[0] && e.MaxPoint.Y <= amax[1];
        }

        /// <summary>Straight pieces of a wall curve (face lines, or centrelines with a thickness); curved pieces are skipped.</summary>
        private static void ReadWallCurve(ToolContext ctx, Entity e, string h, PlanInput input, double centerThickness)
        {
            void Add(Point3d a, Point3d b, double t)
            {
                var s = new PlanSeg(a.X, a.Y, b.X, b.Y, h) { Thickness = t };
                if (t > 0) input.Centers.Add(s); else input.Faces.Add(s);
            }
            switch (e)
            {
                case Line l:
                    Add(l.StartPoint, l.EndPoint, centerThickness);
                    return;
                case Polyline pl:
                {
                    double t = centerThickness;
                    // a wide polyline is a wall drawn by its centreline (thickness = width)
                    double cw = 0;
                    try { cw = pl.ConstantWidth; } catch (Autodesk.AutoCAD.Runtime.Exception) { cw = -1; }
                    if (t <= 0 && cw > 0)
                    {
                        // a width below min_thickness is a print line weight (a face line), not a wall thickness
                        var role = FloorPlanLogic.WidePolylineRole(cw, input.Options);
                        if (role == "skip")
                        {
                            input.Skipped.Add(new PlanSkip(h, "LWPOLYLINE", $"width {Acad.R(cw)} is more than max_thickness: not read as a wall; raise max_thickness, or pass it in centerline_handles with a thickness"));
                            return;
                        }
                        if (role == "wall") t = cw; else input.ThinWidePolylines++;
                    }
                    else if (t <= 0 && cw < 0)
                    {
                        input.Skipped.Add(new PlanSkip(h, "LWPOLYLINE", "polyline with varying segment widths: not read; give it a constant width, or pass it in centerline_handles with a thickness"));
                        return;
                    }
                    int n = pl.NumberOfVertices;
                    int segs = pl.Closed ? n : n - 1;
                    bool curved = false;
                    for (int i = 0; i < segs; i++)
                    {
                        if (Math.Abs(pl.GetBulgeAt(i)) > 1e-9) { curved = true; continue; }
                        Add(pl.GetPoint3dAt(i), pl.GetPoint3dAt((i + 1) % n), t);
                    }
                    if (curved) input.Skipped.Add(new PlanSkip(h, "LWPOLYLINE", "has arc segments (curved wall): those segments are not built; build curved walls with extrude / sweep"));
                    return;
                }
                case Polyline2d p2:
                {
                    var pts = new List<Point3d>();
                    bool curved = false;
                    foreach (ObjectId vid in p2)
                    {
                        var v = ctx.Tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
                        if (v == null || v.VertexType == Vertex2dType.SplineControlVertex) continue;
                        if (Math.Abs(v.Bulge) > 1e-9) curved = true;
                        pts.Add(v.Position);
                    }
                    if (curved || p2.PolyType != Poly2dType.SimplePoly)
                    {
                        input.Skipped.Add(new PlanSkip(h, "POLYLINE", "old-style 2D polyline with arcs or curve fitting: not read; convert it to a lightweight polyline with straight segments"));
                        return;
                    }
                    for (int i = 0; i + 1 < pts.Count; i++) Add(pts[i], pts[i + 1], centerThickness);
                    if (p2.Closed && pts.Count > 2) Add(pts[pts.Count - 1], pts[0], centerThickness);
                    return;
                }
                case Arc _:
                case Circle _:
                case Ellipse _:
                case Spline _:
                    input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), "curved wall: not built (only straight walls are recognised); build it with extrude or sweep"));
                    return;
                case BlockReference br:
                    input.Skipped.Add(new PlanSkip(h, "INSERT", "a block / xref on a wall layer: lines inside blocks are not read; pass the wall lines with wall_handles, or explode a copy of the block"));
                    return;
                case Hatch _:
                case DBText _:
                case MText _:
                case Dimension _:
                    input.Ignored++;
                    return;
                default:
                    input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), "not a line or polyline: not a wall"));
                    return;
            }
        }

        private static bool IsXref(ToolContext ctx, BlockReference br)
        {
            try
            {
                var btr = (BlockTableRecord)ctx.Tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                return btr.IsFromExternalReference || btr.IsDependent;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return false; }
        }

        private static void AddSymbol(ToolContext ctx, Entity e, string h, string kind, PlanInput input)
        {
            if (e is BlockReference br && IsXref(ctx, br))
            {
                input.Skipped.Add(new PlanSkip(h, "INSERT", $"an xref on a {kind} layer: not read as a {kind}"));
                return;
            }
            var ext = EntityInfo.Extents(e);
            if (!ext.HasValue)
            {
                input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), $"{kind} symbol without extents: not read"));
                return;
            }
            input.Symbols.Add(new PlanSymbol
            {
                Kind = kind,
                Min = new[] { ext.Value.MinPoint.X, ext.Value.MinPoint.Y },
                Max = new[] { ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y },
                Sources = { h },
            });
        }

        private static bool IsClosedArea(Entity e) =>
            e is Region || e is Circle || (e is Ellipse el && Math.Abs(el.EndParam - el.StartParam - 2 * Math.PI) < 1e-9) ||
            (e is Curve c && (c.Closed || (!(e is Line) && c.StartPoint.DistanceTo(c.EndPoint) < 1e-9 * Math.Max(1, c.StartPoint.GetAsVector().Length))));

        private static void ReadPlan(ToolContext ctx, Params p, PlanInput input, double mmPerUnit, string wall3d, string slab3d, string column3d, bool readColumns)
        {
            var columnLayers = LayerList(p, "column_layers", FloorPlanLogic.DefaultColumnLayers);
            bool explicitColumns = p.Has("column_handles");
            var wallLayers = LayerList(p, "wall_layers", FloorPlanLogic.DefaultWallLayers);
            var doorLayers = LayerList(p, "door_layers", FloorPlanLogic.DefaultDoorLayers);
            var windowLayers = LayerList(p, "window_layers", FloorPlanLogic.DefaultWindowLayers);
            var slabLayers = LayerList(p, "slab_layers", FloorPlanLogic.DefaultSlabLayers);
            double[] amin = p.PointOpt("area_min"), amax = p.PointOpt("area_max");
            if ((amin == null) != (amax == null)) throw ToolException.Invalid("build_3d_from_plan: give both area_min and area_max (the plan area), or neither.");
            if (amin != null) { var a = new[] { Math.Min(amin[0], amax[0]), Math.Min(amin[1], amax[1]) }; amax = new[] { Math.Max(amin[0], amax[0]), Math.Max(amin[1], amax[1]) }; amin = a; }

            bool explicitWalls = p.Has("wall_handles") || p.Has("centerline_handles");
            bool explicitDoors = p.Has("door_handles"), explicitWindows = p.Has("window_handles"), explicitSlabs = p.Has("slab_handles");
            double ct = 0;
            if (p.Has("centerline_handles"))
            {
                double tmm = p.Positive("thickness");
                if (tmm > FloorPlanLogic.MaxThicknessLimit) throw ToolException.Invalid($"build_3d_from_plan: thickness must be at most {FloorPlanLogic.MaxThicknessLimit} mm.");
                ct = tmm / mmPerUnit;
            }
            else if (p.Has("thickness")) throw ToolException.Invalid("build_3d_from_plan: 'thickness' goes with centerline_handles (walls drawn by their centreline). Face-line walls get their thickness from the distance between the lines.");

            void Count()
            {
                if (++input.Read > FloorPlanLogic.MaxSourceEntities)
                    throw ToolException.Invalid($"build_3d_from_plan: more than {FloorPlanLogic.MaxSourceEntities} plan objects. Nothing was built. Pass area_min / area_max to build one part of the plan at a time, or pass the handles.");
            }

            void Explicit(string name, Action<Entity, string> use)
            {
                if (!p.Has(name)) return;
                foreach (var h in p.Handles(name, FloorPlanLogic.MaxSourceEntities))
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    Count();
                    use(e, h.ToUpperInvariant());
                }
            }
            Explicit("wall_handles", (e, h) => ReadWallCurve(ctx, e, h, input, 0));
            Explicit("centerline_handles", (e, h) => ReadWallCurve(ctx, e, h, input, ct));
            Explicit("door_handles", (e, h) => AddSymbol(ctx, e, h, "door", input));
            Explicit("window_handles", (e, h) => AddSymbol(ctx, e, h, "window", input));
            void AddColumn(Entity e, string h)
            {
                if (!IsClosedArea(e)) { input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), "on a column layer but not a closed outline (closed polyline, rectangle, circle, region): no column from it")); return; }
                var x = EntityInfo.Extents(e);
                var why = x.HasValue ? FloorPlanLogic.ColumnSkipReason((x.Value.MaxPoint.X - x.Value.MinPoint.X) * mmPerUnit, (x.Value.MaxPoint.Y - x.Value.MinPoint.Y) * mmPerUnit) : "no extents";
                if (why != null && !explicitColumns) { input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), why)); return; }
                if (input.Columns.Count >= FloorPlanLogic.MaxColumns)
                    throw ToolException.Invalid($"build_3d_from_plan: more than {FloorPlanLogic.MaxColumns} columns. Nothing was built. Pass area_min / area_max.");
                input.Columns.Add((e.ObjectId, h));
            }
            if (readColumns) Explicit("column_handles", AddColumn);
            Explicit("slab_handles", (e, h) =>
            {
                if (IsClosedArea(e)) input.Slabs.Add((e.ObjectId, h));
                else input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), "slab outline is not a closed curve / region: no slab from it"));
            });

            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e) || e.IsErased) continue;
                string layer = Acad.LayerName(ctx, e.LayerId);
                // the result layers: earlier runs (rerun check), never read as plan
                if (e is Solid3d && (string.Equals(layer, wall3d, StringComparison.OrdinalIgnoreCase) || string.Equals(layer, slab3d, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(layer, column3d, StringComparison.OrdinalIgnoreCase)))
                {
                    var x = EntityInfo.Extents(e);
                    if (x.HasValue)
                        input.Existing.Add(new Existing3d
                        {
                            Handle = Acad.H(id), IsSlab = string.Equals(layer, slab3d, StringComparison.OrdinalIgnoreCase),
                            // a solid on the column layer (and not also the wall / slab layer) is only compared with columns
                            IsColumn = !string.Equals(layer, wall3d, StringComparison.OrdinalIgnoreCase) && !string.Equals(layer, slab3d, StringComparison.OrdinalIgnoreCase),
                            Min = new[] { x.Value.MinPoint.X, x.Value.MinPoint.Y, x.Value.MinPoint.Z },
                            Max = new[] { x.Value.MaxPoint.X, x.Value.MaxPoint.Y, x.Value.MaxPoint.Z },
                        });
                    continue;
                }
                if (e is Solid3d || e is Autodesk.AutoCAD.DatabaseServices.Surface) continue;
                string kind = null;
                // the skill's door / window blocks (CUA-DI-900, CUA-SO-1200) by name first, on any layer — also on
                // the wall layer, where a block would otherwise be skipped as "a block on a wall layer" (KT-20)
                string byName = e is BlockReference brn ? FloorPlanLogic.KindOfBlockName(EntityInfo.BlockName(ctx, brn)) : null;
                if (byName == "door" && !explicitDoors) kind = "door";
                else if (byName == "window" && !explicitWindows) kind = "window";
                else if (byName != null) continue; // named in door_handles / window_handles instead
                else if (!explicitWalls && FloorPlanLogic.LayerMatches(layer, wallLayers)) kind = "wall";
                else if (!explicitDoors && FloorPlanLogic.LayerMatches(layer, doorLayers)) kind = "door";
                else if (!explicitWindows && FloorPlanLogic.LayerMatches(layer, windowLayers)) kind = "window";
                else if (!explicitSlabs && FloorPlanLogic.LayerMatches(layer, slabLayers)) kind = "slab";
                else if (readColumns && !explicitColumns && FloorPlanLogic.LayerMatches(layer, columnLayers)) kind = "column";
                if (kind == null)
                {
                    // for the layer guess: what else is on the plan (only inside the area)
                    if (amin == null || InArea(EntityInfo.Extents(e), amin, amax)) input.Unread[layer] = input.Unread.TryGetValue(layer, out var n) ? n + 1 : 1;
                    continue;
                }
                if (!InArea(EntityInfo.Extents(e), amin, amax)) continue;
                Count();
                string h = Acad.H(id);
                switch (kind)
                {
                    case "wall": ReadWallCurve(ctx, e, h, input, 0); break;
                    case "door": case "window":
                        if (e is Hatch || e is DBText || e is MText || e is Dimension) { input.Ignored++; break; }
                        AddSymbol(ctx, e, h, kind, input);
                        break;
                    case "slab":
                        if (IsClosedArea(e)) input.Slabs.Add((id, h));
                        else if (e is Hatch || e is DBText || e is MText || e is Dimension) input.Ignored++;
                        else input.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), "on a slab layer but not a closed curve / region: no slab from it"));
                        break;
                    case "column":
                        if (e is Hatch || e is DBText || e is MText || e is Dimension) { input.Ignored++; break; }
                        AddColumn(e, h);
                        break;
                }
            }
            if (input.Faces.Count + input.Centers.Count > FloorPlanLogic.MaxSegments)
                throw ToolException.Invalid($"build_3d_from_plan: {input.Faces.Count + input.Centers.Count} wall line pieces (more than {FloorPlanLogic.MaxSegments}). Nothing was built. Pass area_min / area_max to build one part at a time.");
            if (input.Symbols.Count > FloorPlanLogic.MaxSymbols)
                throw ToolException.Invalid($"build_3d_from_plan: {input.Symbols.Count} door / window symbols (more than {FloorPlanLogic.MaxSymbols}). Nothing was built. Pass area_min / area_max.");
        }

        /// <summary>Creates a result layer with a colour when it is missing (the plan's layers are never changed).</summary>
        private static bool EnsureLayer(ToolContext ctx, string name, short aci)
        {
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name) && !lt[name].IsErased) return false;
            ValidateSymbolName(name, "layer");
            var ltr = new LayerTableRecord { Name = name, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, aci) };
            lt.UpgradeOpen();
            lt.Add(ltr);
            ctx.Tr.AddNewlyCreatedDBObject(ltr, true);
            return true;
        }

        private static Params LayerParams(string tool, string layer) =>
            new Params(tool, new Dictionary<string, object> { ["layer"] = layer });

        internal static object Build3dFromPlan(ToolContext ctx, Params p)
        {
            const string tool = "build_3d_from_plan";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out var unitName);
            string slabMode = (p.Str("slab", "auto") ?? "auto").Trim().ToLowerInvariant();
            if (slabMode != "auto" && slabMode != "none" && slabMode != "bbox") throw ToolException.Invalid($"{tool}: slab must be \"auto\" (slab layers / slab_handles), \"bbox\" (one slab under the walls' bounding box) or \"none\".");
            double slabMm = p.Num("slab_thickness", FloorPlanLogic.DefaultSlabThickness);
            // base_z is mm like the heights (the skill gives storey levels in mm), scaled by INSUNITS (KT-16)
            double baseZmm = p.Num("base_z", 0);
            // nâng cấp đợt A: several storeys at once ("dựng 3 tầng, mỗi tầng cao 3,3 m"), walls = storey − slab (L24)
            int storeyCount = p.Int("storeys", 1, 1, FloorPlanLogic.MaxStoreys);
            bool roof = p.Bool("roof_slab", false);
            var levels = FloorPlanLogic.Storeys(storeyCount, p.NumOpt("storey_height"), p.NumOpt("wall_height"),
                slabMode == "none" ? 0 : Math.Max(0, slabMm), baseZmm, roof, tool);
            var hmm = new FloorPlanLogic.Heights
            {
                Wall = levels[0].WallHeight,
                Door = p.Num("door_height", FloorPlanLogic.DefaultDoorHeight),
                Sill = p.Num("window_sill", FloorPlanLogic.DefaultSill),
                Window = p.Num("window_height", FloorPlanLogic.DefaultWindowHeight),
                Slab = slabMm,
            };
            FloorPlanLogic.CheckHeights(hmm, tool);
            if (roof && (slabMode == "none" || hmm.Slab <= 0)) throw ToolException.Invalid($"{tool}: roof_slab needs a slab (slab \"auto\" / \"bbox\" and slab_thickness > 0).");
            double minT = p.Num("min_thickness", FloorPlanLogic.DefaultMinThickness), maxT = p.Num("max_thickness", FloorPlanLogic.DefaultMaxThickness);
            if (minT <= 0 || maxT < minT || maxT > FloorPlanLogic.MaxThicknessLimit)
                throw ToolException.Invalid($"{tool}: need 0 < min_thickness ≤ max_thickness ≤ {FloorPlanLogic.MaxThicknessLimit} mm.");
            var hu = new FloorPlanLogic.Heights { Wall = hmm.Wall / f, Door = hmm.Door / f, Sill = hmm.Sill / f, Window = hmm.Window / f, Slab = hmm.Slab / f };
            string onExisting = (p.Str("on_existing", "stop") ?? "stop").Trim().ToLowerInvariant();
            if (onExisting != "stop" && onExisting != "add") throw ToolException.Invalid($"{tool}: on_existing must be \"stop\" (default) or \"add\".");
            string colMode = (p.Str("columns", "auto") ?? "auto").Trim().ToLowerInvariant();
            if (colMode != "auto" && colMode != "none") throw ToolException.Invalid($"{tool}: columns must be \"auto\" (column layers / column_handles) or \"none\".");
            bool dryRun = p.Bool("dry_run", false);
            string wall3d = p.Str("wall_layer_3d", FloorPlanLogic.WallLayer3d), slab3d = p.Str("slab_layer_3d", FloorPlanLogic.SlabLayer3d);
            string col3d = p.Str("column_layer_3d", FloorPlanLogic.ColumnLayer3d);
            var opt = PlanOptions.ForUnits(f, minT, maxT);

            var input = new PlanInput { Options = opt };
            ReadPlan(ctx, p, input, f, wall3d, slab3d, col3d, colMode == "auto");
            var res = FloorPlanLogic.Recognize(input.Faces, input.Centers, input.Symbols, opt);
            res.Skipped.InsertRange(0, input.Skipped);

            // openings the caller gives (after reading the report)
            var givenErrors = new List<string>();
            if (p.Has("openings"))
            {
                var list = p.Array("openings");
                if (list.Count > FloorPlanLogic.MaxOpeningsGiven) throw ToolException.Invalid($"{tool}: at most {FloorPlanLogic.MaxOpeningsGiven} openings.");
                for (int i = 0; i < list.Count; i++)
                {
                    if (!(list[i] is Dictionary<string, object> d)) throw ToolException.Invalid($"{tool}: openings[{i}] must be an object {{\"kind\", \"at\", \"width\"...}}.");
                    var op = new Params(tool, d);
                    var kind = op.Str("kind").Trim().ToLowerInvariant();
                    if (kind != "door" && kind != "window") throw ToolException.Invalid($"{tool}: openings[{i}].kind must be \"door\" or \"window\".");
                    var at = op.Point("at");
                    double w = op.Num("width", 0) / f;
                    double? bottom = null, top = null;
                    if (kind == "window")
                    {
                        double sill = op.Num("sill", hmm.Sill), height = op.Num("height", hmm.Window);
                        if (sill < 0 || height <= 0 || sill + height > hmm.Wall) throw ToolException.Invalid($"{tool}: openings[{i}]: sill + height must fit in the wall height {hmm.Wall}.");
                        bottom = sill / f; top = (sill + height) / f;
                    }
                    else
                    {
                        double height = op.Num("height", hmm.Door);
                        if (height <= 0 || height > hmm.Wall) throw ToolException.Invalid($"{tool}: openings[{i}].height must be > 0 and at most the wall height {hmm.Wall}.");
                        bottom = 0; top = height / f;
                    }
                    var err = FloorPlanLogic.AddGivenOpening(res, kind, at, w, bottom, top, opt);
                    if (err != null) givenErrors.Add($"openings[{i}]: {err}");
                }
                if (givenErrors.Count > 0) throw ToolException.Invalid($"{tool}: {string.Join("; ", givenErrors)}. Nothing was built. Check the points with the walls of a dry_run.");
            }
            FloorPlanLogic.ResolveHeights(res, hu);

            var report = Report(res, input, f, unitName, hmm, baseZmm);
            var notes = new List<string>();
            // nâng cấp đợt A: layers whose names look like walls / doors / windows... but were not read
            var guesses = LayerGuesses(p, input, colMode == "auto");
            if (guesses.Count > 0)
            {
                report["layer_guess"] = guesses;
                notes.Add("layer_guess lists layers whose NAMES look like plan parts but were not read. Do not build from a guess: tell the user the mapping " +
                          "(layer → tường / cửa đi / cửa sổ / sàn / cột) and ask with request_confirmation (write every layer name on the card), end the turn, and build with " +
                          "wall_layers / door_layers / window_layers / slab_layers / column_layers only after their yes (a build with layers the user did not name is refused until then).");
            }
            if (input.ThinWidePolylines > 0)
                notes.Add($"{input.ThinWidePolylines} polyline(s) with a width below min_thickness were read as plain wall face lines (the width taken as a print line weight, not a wall thickness).");
            if (res.Walls.Count == 0 && input.Slabs.Count == 0 && input.Columns.Count == 0 && slabMode != "bbox")
            {
                throw ToolException.Invalid($"{tool}: no wall recognised. Nothing was built. Read: {input.Read} objects on the wall / door / window / slab / column layers " +
                    $"({string.Join(", ", LayerList(p, "wall_layers", FloorPlanLogic.DefaultWallLayers))} for walls). " +
                    (guesses.Count > 0
                        ? $"Layers that look like plan parts: {string.Join("; ", guesses.Take(10).Cast<Dictionary<string, object>>().Select(g => $"{g["layer"]} → {g["kind"]} ({g["objects"]} objects)"))}. " +
                          "That is only a guess from the names: check it with a dry_run that passes these layers (wall_layers / door_layers / window_layers / slab_layers / column_layers), " +
                          "then ask the user to confirm the mapping (request_confirmation with every layer name, then end the turn) before building. "
                        : "Check the layer names with list_layers and pass wall_layers, or pass wall_handles (two face lines per wall) / centerline_handles + thickness.") +
                    (res.Skipped.Count > 0 ? $" Skipped: {string.Join("; ", res.Skipped.Take(5).Select(s => $"{s.Handle} {s.Reason}"))}." : "") +
                    (f == 1 && unitName.Contains("unitless") ? " The drawing has no units set (INSUNITS = 0): it was read as mm; a plan drawn in metres needs INSUNITS = 6." : ""));
            }
            // several storeys with walls of storey − slab but no slab outline: the storeys would stand slab_thickness apart
            string noSlab = FloorPlanLogic.StoreysWithoutSlab(levels.Count, slabMode, input.Slabs.Count, hmm.Slab, tool);
            if (noSlab != null)
            {
                if (!dryRun) throw ToolException.Invalid(noSlab + " Nothing was built.");
                notes.Add(noSlab);
            }
            var boxes = FloorPlanLogic.Boxes(res, hu);
            if (boxes.Count > FloorPlanLogic.MaxBoxes)
                throw ToolException.Invalid($"{tool}: {boxes.Count} wall pieces (more than {FloorPlanLogic.MaxBoxes}). Nothing was built. Pass area_min / area_max.");
            report["pieces"] = boxes.Count;

            // nâng cấp đợt A: the column outlines in plan (drawing units) and their sizes (mm)
            var colOutlines = new List<(double[] min, double[] max)>();
            var colSizes = new List<(double dx, double dy)>();
            foreach (var (id, _) in input.Columns)
            {
                var x = EntityInfo.Extents((Entity)ctx.Tr.GetObject(id, OpenMode.ForRead));
                if (!x.HasValue) continue;
                colOutlines.Add((new[] { x.Value.MinPoint.X, x.Value.MinPoint.Y }, new[] { x.Value.MaxPoint.X, x.Value.MaxPoint.Y }));
                colSizes.Add(((x.Value.MaxPoint.X - x.Value.MinPoint.X) * f, (x.Value.MaxPoint.Y - x.Value.MinPoint.Y) * f));
            }
            report["columns_count"] = input.Columns.Count;
            if (levels.Count > 1)
            {
                report["storeys"] = storeyCount;
                report["storey_height_mm"] = Acad.R(levels[1].BaseZ - levels[0].BaseZ);
                report["levels_mm"] = levels.Select(l => (object)new Dictionary<string, object> { ["storey"] = l.Number, ["base_z"] = Acad.R(l.BaseZ), ["roof"] = l.RoofOnly }).ToList();
            }

            // rerun check of every storey BEFORE anything is built: solids of an earlier run on the result layers at that level
            var fp = FloorPlanLogic.Footprint(res);
            var dup = new List<string>();
            var dupLevels = new List<string>(); // the storeys of this call that are already there
            var noBoxes = new List<PlanBox>();
            foreach (var lv in levels)
            {
                double bz = lv.BaseZ / f;
                var rerun = ExistingOver(ctx, input, lv.RoofOnly ? noBoxes : boxes, fp, slabMode, bz, hu);
                var again = new List<string>(rerun.Rerun);
                if (!lv.RoofOnly) again.AddRange(FloorPlanLogic.ColumnRerun(input.Existing, colOutlines, bz, hu.Wall, opt.JoinTol));
                if (again.Count > 0) dupLevels.Add(lv.RoofOnly ? "roof slab" : lv.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var h in again) if (!dup.Contains(h)) dup.Add(h);
                if (rerun.SlabIntoWallsBelow.Count > 0)
                    notes.Add((levels.Count > 1 ? $"The slab of storey {lv.Number} (base_z {Acad.R(lv.BaseZ)} mm; " : "This storey's slab (") +
                        $"{Acad.R(hmm.Slab)} mm under base_z) cuts {Acad.R(rerun.SlabIntoWallsBy * f)} mm into {rerun.SlabIntoWallsBelow.Count} wall solid(s) of the storey below " +
                        $"({string.Join(", ", rerun.SlabIntoWallsBelow.Take(10))}): that is not a rerun, but the walls below should be storey height − slab thickness high. Never erase the storey below for this.");
            }
            // several storeys: say which ones are there, so only the missing ones are built (base_z of the first missing storey)
            string atLevel = levels.Count > 1
                ? $"storey(s) {string.Join(", ", dupLevels)} of this call (storey 1 = base_z {Acad.R(baseZmm)} mm; to build only the missing storeys pass the base_z of the first missing one and the number left)"
                : $"this level (base_z {Acad.R(baseZmm)} mm)";
            string resultLayers = input.Columns.Count > 0 ? $"{wall3d} / {slab3d} / {col3d}" : $"{wall3d} / {slab3d}";
            if (dryRun)
            {
                report["dry_run"] = true;
                report["note"] = "Dry run: nothing was built. Check walls / openings / skipped, then call again without dry_run (add 'openings' for what was missed).";
                if (input.Columns.Count > 0)
                    report["columns"] = new Dictionary<string, object>
                    {
                        ["count"] = input.Columns.Count,
                        ["height_mm"] = hmm.Wall,
                        ["sizes_mm"] = SizeList(colSizes),
                        ["sources"] = input.Columns.Take(50).Select(c => (object)c.handle).ToList(),
                    };
                if (dup.Count > 0)
                {
                    report["existing_solids"] = dup.Take(FloorPlanLogic.MaxReportItems).Cast<object>().ToList();
                    notes.Add($"{dup.Count} solid(s) of an earlier run are already on {resultLayers} at {atLevel} over this plan: building again stops unless the user chooses (a different base_z for another storey, on_existing \"add\", or erasing exactly those solids).");
                }
                if (notes.Count > 0) report["notes"] = notes;
                return report;
            }
            if (dup.Count > 0 && onExisting == "stop")
                throw ToolException.Invalid($"{tool}: this plan was already built at {atLevel}: {dup.Count} solid(s) on {resultLayers} over it at the same height ({string.Join(", ", dup.Take(20))}{(dup.Count > 20 ? ", …" : "")}). " +
                    "A storey below or beside only touching this one does not count. Nothing was built. Ask the user: another storey (a different base_z), build again anyway with on_existing \"add\", or erase exactly these solids first (erase_entities, needs their yes). Never erase other storeys.");

            var created = new List<string>();
            var layersCreated = new List<string>();
            var built = new List<(FloorPlanLogic.Storey lv, LevelParts parts)>();
            LevelParts first = null;
            foreach (var lv in levels)
            {
                LevelParts parts;
                if (first == null)
                {
                    // the first storey is built from the plan
                    double bz = lv.BaseZ / f;
                    parts = first = new LevelParts();
                    BuildWalls(ctx, tool, boxes, bz, f, wall3d, parts, created, layersCreated, notes);
                    BuildColumns(ctx, tool, input, bz, hu.Wall, f, col3d, parts, created, layersCreated, res.Skipped);
                    if (slabMode != "none" && hmm.Slab > 0)
                    {
                        BuildSlabs(ctx, tool, input, res, fp, slabMode, bz, hu.Slab, f, slab3d, parts, created, layersCreated);
                        if (parts.Slabs.Count == 0 && slabMode == "auto")
                            notes.Add($"No slab: no closed outline on {string.Join(", ", LayerList(p, "slab_layers", FloorPlanLogic.DefaultSlabLayers))} and no slab_handles. Ask the user for the slab outline, or call again with slab \"bbox\" (one slab under the walls' bounding box) if that is what they want.");
                    }
                }
                else
                {
                    // the storeys above (and the roof slab) are copies of the first storey's solids moved up: the same plan and
                    // heights give the same solids, so nothing is united a second time
                    parts = CopyLevel(ctx, tool, first, (lv.BaseZ - levels[0].BaseZ) / f, lv.RoofOnly, wall3d, col3d, slab3d, created);
                }
                built.Add((lv, parts));
            }
            if (built.Count == 1)
            {
                // one storey: the fields as before
                if (first.WallInfo != null) report["wall_solid"] = first.WallInfo;
                if (first.Columns.Count > 0) report["columns"] = ColumnsReport(first, colSizes, hmm.Wall, f);
                report["slabs"] = first.Slabs.Select(x => (object)x.info).ToList();
            }
            else
            {
                // several storeys: one entry per storey (and the roof), and the totals
                report["levels"] = built.Select(x => (object)LevelReport(x.lv, x.parts, f)).ToList();
                if (first.Columns.Count > 0) report["columns"] = new Dictionary<string, object> { ["per_storey"] = first.Columns.Count, ["height_mm"] = hmm.Wall, ["sizes_mm"] = SizeList(colSizes) };
                double k = f * f * f / 1e9;
                report["total_volume_m3"] = new Dictionary<string, object>
                {
                    ["walls"] = Math.Round(built.Sum(x => x.parts.WallVolume) * k, 6),
                    ["columns"] = Math.Round(built.Sum(x => x.parts.Columns.Sum(c => c.volume)) * k, 6),
                    ["slabs"] = Math.Round(built.Sum(x => x.parts.Slabs.Sum(sl => sl.volume)) * k, 6),
                };
            }
            report["skipped"] = SkipList(res.Skipped);
            report["skipped_count"] = res.Skipped.Count;
            if (created.Count == 0)
                throw ToolException.Invalid($"{tool}: nothing to build (no wall piece{(input.Columns.Count > 0 ? ", no column" : "")}{(slabMode == "none" || hmm.Slab <= 0 ? "; slab is off" : " and no slab outline could be used")}). Nothing was built." +
                    (res.Skipped.Count > 0 ? $" Skipped: {string.Join("; ", res.Skipped.Take(5).Select(x => $"{x.Handle} {x.Reason}"))}." : ""));
            report["handles"] = created.Cast<object>().ToList();
            if (layersCreated.Count > 0) report["layers_created"] = layersCreated;
            notes.Add("Plan objects were only read, not changed. Self-check now: set_view_3d sw_iso with these handles, set_visual_style conceptual, capture_view; compare volume_m3 / min / max with the plan.");
            report["notes"] = notes;
            return report;
        }

        /// <summary>The solids of one storey (nâng cấp đợt A): the first storey is built from the plan, the storeys above are copies moved up.</summary>
        private sealed class LevelParts
        {
            /// <summary>The united wall solid first, then the pieces AutoCAD could not unite.</summary>
            public List<ObjectId> Walls = new List<ObjectId>();
            public Dictionary<string, object> WallInfo;
            /// <summary>Volume of the wall solid(s), drawing units³.</summary>
            public double WallVolume;
            public List<(ObjectId id, string from, double volume)> Columns = new List<(ObjectId, string, double)>();
            public List<(ObjectId id, Dictionary<string, object> info, double volume)> Slabs = new List<(ObjectId, Dictionary<string, object>, double)>();
        }

        private static void AddExtents(Dictionary<string, object> r, Entity e)
        {
            var ext = EntityInfo.Extents(e);
            if (!ext.HasValue) return;
            r["min"] = Acad.J3(ext.Value.MinPoint);
            r["max"] = Acad.J3(ext.Value.MaxPoint);
        }

        /// <summary>Column sizes in plan with their counts: [{"size": [220, 220], "count": 12}, …].</summary>
        private static List<object> SizeList(IEnumerable<(double dx, double dy)> sizesMm) =>
            FloorPlanLogic.ColumnSizeSummary(sizesMm)
                .Select(x => (object)new Dictionary<string, object> { ["size"] = new List<object> { x.a, x.b }, ["count"] = x.count })
                .ToList();

        private static Dictionary<string, object> ColumnsReport(LevelParts parts, IEnumerable<(double dx, double dy)> sizesMm, double heightMm, double f)
        {
            var d = new Dictionary<string, object>
            {
                ["count"] = parts.Columns.Count,
                ["volume_m3"] = Math.Round(parts.Columns.Sum(c => c.volume) * f * f * f / 1e9, 6),
                ["handles"] = parts.Columns.Take(FloorPlanLogic.MaxReportItems).Select(c => (object)Acad.H(c.id)).ToList(),
            };
            if (sizesMm != null) { d["height_mm"] = heightMm; d["sizes_mm"] = SizeList(sizesMm); }
            return d;
        }

        private static Dictionary<string, object> LevelReport(FloorPlanLogic.Storey lv, LevelParts parts, double f)
        {
            var d = new Dictionary<string, object> { ["storey"] = lv.Number, ["base_z_mm"] = Acad.R(lv.BaseZ) };
            if (lv.RoofOnly) d["roof"] = true;
            if (parts.WallInfo != null) d["wall_solid"] = parts.WallInfo;
            if (parts.Columns.Count > 0) d["columns"] = ColumnsReport(parts, null, 0, f);
            d["slabs"] = parts.Slabs.Select(x => (object)x.info).ToList();
            return d;
        }

        /// <summary>The wall boxes of the first storey united into one solid (pieces AutoCAD cannot unite stay separate).</summary>
        private static void BuildWalls(ToolContext ctx, string tool, List<PlanBox> boxes, double baseZ, double f, string wall3d, LevelParts parts,
            List<string> created, List<string> layersCreated, List<string> notes)
        {
            if (boxes.Count == 0) return;
            var wallP = LayerParams(tool, wall3d);
            if (EnsureLayer(ctx, wall3d, 7)) layersCreated.Add(wall3d);
            double piecesVolume = 0;
            Solid3d walls = null;
            ObjectId wallsId = ObjectId.Null;
            int unionFailed = 0;
            var extra = new List<ObjectId>();
            foreach (var b in boxes)
            {
                Solid3d s;
                try { s = MakeBox(b.Length, b.Width, b.Height, null, new[] { b.Center[0], b.Center[1], baseZ + b.Center[2] }, b.RotationDeg); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    throw ToolException.Invalid($"{tool}: AutoCAD could not build a wall piece of wall {b.Wall} ({Acad.R(b.Length * f)} × {Acad.R(b.Width * f)} × {Acad.R(b.Height * f)} mm, {ex.ErrorStatus}). Nothing was built.");
                }
                piecesVolume += b.Volume;
                if (walls == null)
                {
                    wallsId = AddSolid(ctx, s, wallP);
                    walls = s;
                    continue;
                }
                try
                {
                    BooleanInto(walls, s, BooleanOperationType.BoolUnite);
                    s.Dispose(); // emptied by the union, never in the drawing
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // AutoCAD refused this union: keep the piece as its own solid on the same layer
                    unionFailed++;
                    extra.Add(AddSolid(ctx, s, wallP));
                }
                catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
            }
            parts.Walls.Add(wallsId);
            parts.Walls.AddRange(extra);
            created.AddRange(parts.Walls.Select(Acad.H));
            var wr = new Dictionary<string, object> { ["handle"] = Acad.H(wallsId), ["layer"] = wall3d };
            AddSolidNumbers(wr, walls);
            parts.WallVolume = VolumeOf(walls) + extra.Sum(id => VolumeOf((Solid3d)ctx.Tr.GetObject(id, OpenMode.ForRead)));
            wr["volume_m3"] = Math.Round(parts.WallVolume * f * f * f / 1e9, 6);
            wr["pieces_volume_m3"] = Math.Round(piecesVolume * f * f * f / 1e9, 6);
            if (extra.Count > 0)
            {
                wr["separate_pieces"] = extra.Select(id => (object)Acad.H(id)).ToList();
                notes.Add($"AutoCAD could not unite {unionFailed} wall piece(s); they are separate solids on {wall3d}.");
            }
            parts.WallInfo = wr;
        }

        private static void SkipOnce(List<PlanSkip> skipped, PlanSkip s)
        {
            if (!skipped.Any(x => x.Handle == s.Handle && x.Reason == s.Reason)) skipped.Add(s);
        }

        /// <summary>Regions of closed outlines (the caller disposes them); outlines that cannot become a region are skipped.</summary>
        private static List<(Region r, string src)> OutlineRegions(ToolContext ctx, IEnumerable<(ObjectId id, string handle)> outlines, List<PlanSkip> skipped, string what)
        {
            var regions = new List<(Region r, string src)>();
            try
            {
                foreach (var (id, h) in outlines)
                {
                    var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForRead);
                    if (e is Region reg) { regions.Add(((Region)reg.Clone(), h)); continue; }
                    try
                    {
                        foreach (DBObject o in Region.CreateFromCurves(new DBObjectCollection { e })) if (o is Region r) regions.Add((r, h)); else o.Dispose();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        SkipOnce(skipped, new PlanSkip(h, Acad.TypeName(e), $"{what} outline could not become a region ({ex.ErrorStatus}: self-crossing or not flat): no {what} from it"));
                    }
                }
            }
            catch { foreach (var x in regions) x.r.Dispose(); throw; }
            return regions;
        }

        /// <summary>
        /// Extrudes a region by height (drawing units) and puts its bottom at z0, or its top at z0 when topAt. The outline
        /// may lie at any z (or have a downward normal); an outline that is not flat in XY is refused.
        /// </summary>
        private static Solid3d ExtrudeFlat(string tool, Region reg, double height, double z0, bool topAt, string src, string what)
        {
            var s = new Solid3d();
            try
            {
                s.Extrude(reg, -height, 0);
                var ext = s.GeometricExtents;
                s.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, topAt ? z0 - ext.MaxPoint.Z : z0 - ext.MinPoint.Z)));
                ext = s.GeometricExtents;
                if (Math.Abs(ext.MaxPoint.Z - ext.MinPoint.Z - height) > 1e-6 * Math.Max(1, height))
                    throw ToolException.Invalid($"{tool}: the {what} outline {src} is not horizontal (flat in XY). Nothing was built.");
                return s;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                s.Dispose();
                throw ToolException.Invalid($"{tool}: AutoCAD could not build the {what} from {src} ({ex.ErrorStatus}). Nothing was built.");
            }
            catch { s.Dispose(); throw; }
        }

        /// <summary>Columns of the first storey: each closed outline extruded from base_z by the wall height, one solid each.</summary>
        private static void BuildColumns(ToolContext ctx, string tool, PlanInput input, double baseZ, double height, double f, string col3d, LevelParts parts,
            List<string> created, List<string> layersCreated, List<PlanSkip> skipped)
        {
            if (input.Columns.Count == 0) return;
            var regions = OutlineRegions(ctx, input.Columns, skipped, "column");
            try
            {
                if (regions.Count > 0 && EnsureLayer(ctx, col3d, 1)) layersCreated.Add(col3d);
                var colP = LayerParams(tool, col3d);
                foreach (var (reg, src) in regions)
                {
                    var s = ExtrudeFlat(tool, reg, height, baseZ, false, src, "column");
                    var id = AddSolid(ctx, s, colP);
                    created.Add(Acad.H(id));
                    parts.Columns.Add((id, src, VolumeOf(s)));
                }
            }
            finally { foreach (var x in regions) x.r.Dispose(); }
        }

        /// <summary>Slabs of the first storey: top at base_z, slab thickness down; outlines inside others are holes (KT-22).</summary>
        private static void BuildSlabs(ToolContext ctx, string tool, PlanInput input, PlanResult res, (double[] min, double[] max)? fp, string slabMode,
            double baseZ, double slabT, double f, string slab3d, LevelParts parts, List<string> created, List<string> layersCreated)
        {
            var regions = new List<(Region r, string src)>();
            try
            {
                if (slabMode == "bbox")
                {
                    if (!fp.HasValue) throw ToolException.Invalid($"{tool}: slab \"bbox\" needs recognised walls.");
                    var a = fp.Value.min; var c = fp.Value.max;
                    using (var pl = Acad.MakePolyline(new List<double[]> { new[] { a[0], a[1] }, new[] { c[0], a[1] }, new[] { c[0], c[1] }, new[] { a[0], c[1] } }, true))
                    {
                        var col = new DBObjectCollection { pl };
                        foreach (DBObject o in Region.CreateFromCurves(col)) if (o is Region r) regions.Add((r, "bbox")); else o.Dispose();
                    }
                }
                else regions = OutlineRegions(ctx, input.Slabs, res.Skipped, "slab");
                // a smaller outline inside a bigger one is a hole (stair, shaft)
                regions = regions.OrderByDescending(x => SafeArea(x.r)).ToList();
                var holes = new HashSet<int>();
                // the same outline twice (drawn twice, or on A-SLAB and A-FLOR) gives one slab, not two on top of each other
                for (int i = 0; i < regions.Count; i++)
                    for (int j = i + 1; j < regions.Count && !holes.Contains(i); j++)
                        if (!holes.Contains(j) && SameRegion(regions[i].r, regions[j].r))
                        {
                            holes.Add(j);
                            res.Skipped.Add(new PlanSkip(regions[j].src, "SLAB", $"the same outline as {regions[i].src}: one slab built"));
                        }
                // nesting (KT-22): even depth = a slab, odd = a hole in its parent; an island inside a hole is a slab again
                var live = Enumerable.Range(0, regions.Count).Where(i => !holes.Contains(i)).ToList();
                var (depth, parent) = FloorPlanLogic.SlabNesting(live.Select(i => SafeArea(regions[i].r)).ToList(),
                    (a, b) => Inside(regions[live[a]].r, regions[live[b]].r));
                for (int a = 0; a < live.Count; a++)
                {
                    if (depth[a] % 2 == 1) { holes.Add(live[a]); continue; }
                    for (int b = 0; b < live.Count; b++)
                    {
                        if (parent[b] != a) continue;
                        using (var cut = (Region)regions[live[b]].r.Clone()) regions[live[a]].r.BooleanOperation(BooleanOperationType.BoolSubtract, cut);
                    }
                }
                if (regions.Count > holes.Count && EnsureLayer(ctx, slab3d, 8)) layersCreated.Add(slab3d);
                var slabP = LayerParams(tool, slab3d);
                for (int i = 0; i < regions.Count; i++)
                {
                    if (holes.Contains(i)) continue;
                    var reg = regions[i].r;
                    var s = ExtrudeFlat(tool, reg, slabT, baseZ, true, regions[i].src, "slab");
                    var sid = AddSolid(ctx, s, slabP);
                    created.Add(Acad.H(sid));
                    var sr = new Dictionary<string, object> { ["handle"] = Acad.H(sid), ["from"] = regions[i].src, ["area_m2"] = Math.Round(SafeArea(reg) * f * f / 1e6, 4) };
                    AddSolidNumbers(sr, s);
                    double vol = VolumeOf(s);
                    sr["volume_m3"] = Math.Round(vol * f * f * f / 1e9, 6);
                    parts.Slabs.Add((sid, sr, vol));
                }
            }
            finally { foreach (var x in regions) x.r.Dispose(); }
        }

        /// <summary>
        /// A storey above the first (nâng cấp đợt A): copies of the first storey's solids moved up by dz (drawing units).
        /// The roof level copies the slabs only. The copies get the same numbers as their originals (min / max moved up).
        /// </summary>
        private static LevelParts CopyLevel(ToolContext ctx, string tool, LevelParts first, double dz, bool roofOnly, string wall3d, string col3d, string slab3d, List<string> created)
        {
            var parts = new LevelParts();
            var up = Matrix3d.Displacement(new Vector3d(0, 0, dz));
            Solid3d Copy(ObjectId src, string layer, out ObjectId id)
            {
                var c = (Solid3d)((Solid3d)ctx.Tr.GetObject(src, OpenMode.ForRead)).Clone();
                try { c.TransformBy(up); }
                catch { c.Dispose(); throw; }
                id = AddSolid(ctx, c, LayerParams(tool, layer));
                created.Add(Acad.H(id));
                return c;
            }
            if (!roofOnly && first.Walls.Count > 0)
            {
                Solid3d main = null;
                foreach (var src in first.Walls)
                {
                    var c = Copy(src, wall3d, out var id);
                    if (main == null) main = c;
                    parts.Walls.Add(id);
                }
                var wr = new Dictionary<string, object> { ["handle"] = Acad.H(parts.Walls[0]), ["layer"] = wall3d };
                AddExtents(wr, main);
                foreach (var key in new[] { "volume", "volume_m3", "pieces_volume_m3" })
                    if (first.WallInfo.TryGetValue(key, out var v)) wr[key] = v;
                if (parts.Walls.Count > 1) wr["separate_pieces"] = parts.Walls.Skip(1).Select(id => (object)Acad.H(id)).ToList();
                parts.WallInfo = wr;
                parts.WallVolume = first.WallVolume;
            }
            if (!roofOnly)
                foreach (var (src, from, volume) in first.Columns)
                {
                    Copy(src, col3d, out var id);
                    parts.Columns.Add((id, from, volume));
                }
            foreach (var (src, info, volume) in first.Slabs)
            {
                var c = Copy(src, slab3d, out var id);
                var sr = new Dictionary<string, object> { ["handle"] = Acad.H(id) };
                foreach (var key in new[] { "from", "area_m2", "volume", "volume_m3" })
                    if (info.TryGetValue(key, out var v)) sr[key] = v;
                AddExtents(sr, c);
                parts.Slabs.Add((id, sr, volume));
            }
            return parts;
        }

        /// <summary>
        /// Layers that were not read but whose names look like plan parts (FloorPlanLogic.GuessLayerKind), most objects
        /// first. A kind the caller gave by handles, or turned off, is not guessed.
        /// </summary>
        private static List<object> LayerGuesses(Params p, PlanInput input, bool columns)
        {
            var off = new HashSet<string>(StringComparer.Ordinal);
            if (p.Has("wall_handles") || p.Has("centerline_handles")) off.Add("wall");
            if (p.Has("door_handles")) off.Add("door");
            if (p.Has("window_handles")) off.Add("window");
            if (p.Has("slab_handles") || (p.Str("slab", "auto") ?? "auto").Trim().ToLowerInvariant() != "auto") off.Add("slab");
            if (p.Has("column_handles") || !columns) off.Add("column");
            return input.Unread
                .Select(kv => (layer: kv.Key, n: kv.Value, kind: FloorPlanLogic.GuessLayerKind(kv.Key)))
                .Where(x => x.kind != null && !off.Contains(x.kind))
                .OrderByDescending(x => x.n).ThenBy(x => x.layer, StringComparer.OrdinalIgnoreCase)
                .Take(FloorPlanLogic.MaxLayerGuesses)
                .Select(x => (object)new Dictionary<string, object>
                {
                    ["layer"] = x.layer, ["kind"] = x.kind, ["objects"] = x.n,
                    ["use"] = x.kind == "railing" ? "build_railing path_handle" : x.kind + "_layers",
                })
                .ToList();
        }

        /// <summary>Rerun check (FloorPlanLogic.CheckRerun): solids of an earlier run at this level over the new walls / slab outlines.</summary>
        private static RerunCheck ExistingOver(ToolContext ctx, PlanInput input, List<PlanBox> boxes, (double[] min, double[] max)? fp, string slabMode, double baseZ, FloorPlanLogic.Heights hu)
        {
            var slabBoxes = new List<(double[] min, double[] max)>();
            if (slabMode == "bbox" && fp.HasValue) slabBoxes.Add(fp.Value);
            else if (slabMode == "auto")
                foreach (var (id, _) in input.Slabs)
                {
                    var x = EntityInfo.Extents((Entity)ctx.Tr.GetObject(id, OpenMode.ForRead));
                    if (x.HasValue) slabBoxes.Add((new[] { x.Value.MinPoint.X, x.Value.MinPoint.Y }, new[] { x.Value.MaxPoint.X, x.Value.MaxPoint.Y }));
                }
            double slabT = slabMode == "none" ? 0 : hu.Slab;
            return FloorPlanLogic.CheckRerun(input.Existing, boxes, slabBoxes, baseZ, hu.Wall, slabT, input.Options.JoinTol);
        }

        /// <summary>Two regions with the same area and box (the same slab outline drawn twice).</summary>
        private static bool SameRegion(Region a, Region b)
        {
            double aa = SafeArea(a), ab = SafeArea(b);
            if (aa <= 0 || Math.Abs(aa - ab) > 1e-6 * aa) return false;
            var x = a.GeometricExtents; var y = b.GeometricExtents;
            // in plan only: the same outline drawn at another elevation is still the same slab (its top goes to base_z)
            double tol = 1e-6 * Math.Max(1, (x.MaxPoint - x.MinPoint).Length);
            return Math.Abs(x.MinPoint.X - y.MinPoint.X) <= tol && Math.Abs(x.MinPoint.Y - y.MinPoint.Y) <= tol &&
                   Math.Abs(x.MaxPoint.X - y.MaxPoint.X) <= tol && Math.Abs(x.MaxPoint.Y - y.MaxPoint.Y) <= tol;
        }

        private static double SafeArea(Region r)
        {
            try { return r.Area; } catch (Autodesk.AutoCAD.Runtime.Exception) { return 0; }
        }

        /// <summary>inner lies inside outer: subtracting it from a copy of outer removes exactly its area.</summary>
        private static bool Inside(Region inner, Region outer)
        {
            double ai = SafeArea(inner), ao = SafeArea(outer);
            if (ai <= 0 || ai >= ao) return false;
            var a = inner.GeometricExtents; var b = outer.GeometricExtents;
            if (a.MinPoint.X < b.MinPoint.X || a.MinPoint.Y < b.MinPoint.Y || a.MaxPoint.X > b.MaxPoint.X || a.MaxPoint.Y > b.MaxPoint.Y) return false;
            using (var copy = (Region)outer.Clone())
            using (var cut = (Region)inner.Clone())
            {
                try { copy.BooleanOperation(BooleanOperationType.BoolSubtract, cut); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { return false; }
                return Math.Abs(SafeArea(copy) - (ao - ai)) <= 1e-6 * ao;
            }
        }

        private static List<object> SkipList(List<PlanSkip> skipped) =>
            skipped.Take(FloorPlanLogic.MaxReportItems).Select(s => (object)new Dictionary<string, object> { ["handle"] = s.Handle, ["type"] = s.Type, ["reason"] = s.Reason }).ToList();

        private static List<object> P2(double[] p, double f) => new List<object> { Acad.R(p[0]), Acad.R(p[1]) };

        /// <summary>What was recognised, for Claude to check and to ask the user about.</summary>
        private static Dictionary<string, object> Report(PlanResult res, PlanInput input, double f, string unitName, FloorPlanLogic.Heights hmm, double baseZ)
        {
            var walls = new List<object>();
            for (int i = 0; i < res.Walls.Count && walls.Count < FloorPlanLogic.MaxReportItems; i++)
            {
                var w = res.Walls[i];
                walls.Add(new Dictionary<string, object>
                {
                    ["i"] = i,
                    ["axis_start"] = P2(w.At(w.Start), f),
                    ["axis_end"] = P2(w.At(w.End), f),
                    ["thickness_mm"] = Acad.R(w.Thickness * f),
                    ["length_mm"] = Acad.R((w.End - w.Start) * f),
                    ["parts"] = w.Solid.Count,
                    ["from"] = w.Mode,
                    ["sources"] = w.Sources.Take(10).Cast<object>().ToList(),
                });
            }
            var openings = res.Openings.Take(FloorPlanLogic.MaxReportItems).Select(op =>
            {
                var w = res.Walls[op.Wall];
                return (object)new Dictionary<string, object>
                {
                    ["kind"] = op.Kind,
                    ["wall"] = op.Wall,
                    ["center"] = P2(w.At((op.S0 + op.S1) / 2), f),
                    ["width_mm"] = Acad.R(op.Width * f),
                    ["bottom_mm"] = Acad.R(op.Bottom * f),
                    ["top_mm"] = Acad.R(op.Top * f),
                    ["from"] = op.From,
                    ["sources"] = op.Sources.Take(10).Cast<object>().ToList(),
                };
            }).ToList();
            var gaps = res.OpenGaps.Take(FloorPlanLogic.MaxReportItems).Select(g =>
            {
                var w = res.Walls[g.wall];
                return (object)new Dictionary<string, object> { ["wall"] = g.wall, ["center"] = P2(w.At((g.s0 + g.s1) / 2), f), ["width_mm"] = Acad.R((g.s1 - g.s0) * f) };
            }).ToList();
            var r = new Dictionary<string, object>
            {
                ["units"] = unitName,
                ["heights_mm"] = new Dictionary<string, object> { ["wall"] = hmm.Wall, ["door"] = hmm.Door, ["window_sill"] = hmm.Sill, ["window"] = hmm.Window, ["slab"] = hmm.Slab, ["base_z"] = baseZ },
                ["read_objects"] = input.Read,
                ["walls_count"] = res.Walls.Count,
                ["walls"] = walls,
                ["thickness_summary"] = FloorPlanLogic.ThicknessSummary(res, f).Select(t => (object)new Dictionary<string, object> { ["thickness_mm"] = t.thickness, ["length_m"] = Math.Round(t.length / 1000, 3) }).ToList(),
                ["openings_count"] = res.Openings.Count,
                ["openings"] = openings,
                ["open_gaps"] = gaps,
                ["caps"] = res.Caps,
                ["junctions_filled"] = res.JunctionsFilled,
                ["ignored"] = input.Ignored,
                // in every report, dry run too: Claude reads it before building
                ["skipped"] = SkipList(res.Skipped),
                ["skipped_count"] = res.Skipped.Count,
            };
            var fp = FloorPlanLogic.Footprint(res);
            if (fp.HasValue) { r["plan_min"] = P2(fp.Value.min, f); r["plan_max"] = P2(fp.Value.max, f); }
            if (res.OpenGaps.Count > 0) r["open_gaps_note"] = "Gaps between wall parts with no door / window symbol: left open over the full height. Ask the user if they are doors / windows (pass them in 'openings').";
            return r;
        }
    }
}
