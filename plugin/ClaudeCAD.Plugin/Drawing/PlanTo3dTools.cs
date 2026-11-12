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
            d.Register("build_3d_from_plan", true, Build3dFromPlan);
        }

        private sealed class PlanInput
        {
            public List<PlanSeg> Faces = new List<PlanSeg>();
            public List<PlanSeg> Centers = new List<PlanSeg>();
            public List<PlanSymbol> Symbols = new List<PlanSymbol>();
            public List<(ObjectId id, string handle)> Slabs = new List<(ObjectId, string)>();
            public List<PlanSkip> Skipped = new List<PlanSkip>();
            public int Read, Ignored;
            /// <summary>Existing solids on the result layers (rerun check).</summary>
            public List<(string handle, Extents3d ext)> Existing = new List<(string, Extents3d)>();
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
                    if (t <= 0 && cw > 0) t = cw;
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

        private static void ReadPlan(ToolContext ctx, Params p, PlanInput input, double mmPerUnit, string wall3d, string slab3d)
        {
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
                if (e is Solid3d && (string.Equals(layer, wall3d, StringComparison.OrdinalIgnoreCase) || string.Equals(layer, slab3d, StringComparison.OrdinalIgnoreCase)))
                {
                    var x = EntityInfo.Extents(e);
                    if (x.HasValue) input.Existing.Add((Acad.H(id), x.Value));
                    continue;
                }
                if (e is Solid3d || e is Autodesk.AutoCAD.DatabaseServices.Surface) continue;
                string kind = null;
                if (!explicitWalls && FloorPlanLogic.LayerMatches(layer, wallLayers)) kind = "wall";
                else if (!explicitDoors && FloorPlanLogic.LayerMatches(layer, doorLayers)) kind = "door";
                else if (!explicitWindows && FloorPlanLogic.LayerMatches(layer, windowLayers)) kind = "window";
                else if (!explicitSlabs && FloorPlanLogic.LayerMatches(layer, slabLayers)) kind = "slab";
                else if (e is BlockReference br0)
                {
                    // the skill's door / window blocks (CUA-DI-900, CUA-SO-1200) on any layer
                    var bk = FloorPlanLogic.KindOfBlockName(EntityInfo.BlockName(ctx, br0));
                    if (bk == "door" && !explicitDoors) kind = "door";
                    if (bk == "window" && !explicitWindows) kind = "window";
                }
                if (kind == null) continue;
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
            var hmm = new FloorPlanLogic.Heights
            {
                Wall = p.Num("wall_height", FloorPlanLogic.DefaultWallHeight),
                Door = p.Num("door_height", FloorPlanLogic.DefaultDoorHeight),
                Sill = p.Num("window_sill", FloorPlanLogic.DefaultSill),
                Window = p.Num("window_height", FloorPlanLogic.DefaultWindowHeight),
                Slab = p.Num("slab_thickness", FloorPlanLogic.DefaultSlabThickness),
            };
            FloorPlanLogic.CheckHeights(hmm, tool);
            double minT = p.Num("min_thickness", FloorPlanLogic.DefaultMinThickness), maxT = p.Num("max_thickness", FloorPlanLogic.DefaultMaxThickness);
            if (minT <= 0 || maxT < minT || maxT > FloorPlanLogic.MaxThicknessLimit)
                throw ToolException.Invalid($"{tool}: need 0 < min_thickness ≤ max_thickness ≤ {FloorPlanLogic.MaxThicknessLimit} mm.");
            var hu = new FloorPlanLogic.Heights { Wall = hmm.Wall / f, Door = hmm.Door / f, Sill = hmm.Sill / f, Window = hmm.Window / f, Slab = hmm.Slab / f };
            double baseZ = p.Num("base_z", 0);
            string slabMode = (p.Str("slab", "auto") ?? "auto").Trim().ToLowerInvariant();
            if (slabMode != "auto" && slabMode != "none" && slabMode != "bbox") throw ToolException.Invalid($"{tool}: slab must be \"auto\" (slab layers / slab_handles), \"bbox\" (one slab under the walls' bounding box) or \"none\".");
            string onExisting = (p.Str("on_existing", "stop") ?? "stop").Trim().ToLowerInvariant();
            if (onExisting != "stop" && onExisting != "add") throw ToolException.Invalid($"{tool}: on_existing must be \"stop\" (default) or \"add\".");
            bool dryRun = p.Bool("dry_run", false);
            string wall3d = p.Str("wall_layer_3d", FloorPlanLogic.WallLayer3d), slab3d = p.Str("slab_layer_3d", FloorPlanLogic.SlabLayer3d);
            var opt = PlanOptions.ForUnits(f, minT, maxT);

            var input = new PlanInput();
            ReadPlan(ctx, p, input, f, wall3d, slab3d);
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

            var report = Report(res, input, f, unitName, hmm, baseZ);
            if (res.Walls.Count == 0 && input.Slabs.Count == 0 && slabMode != "bbox")
            {
                throw ToolException.Invalid($"{tool}: no wall recognised. Nothing was built. Read: {input.Read} objects on the wall / door / window / slab layers " +
                    $"({string.Join(", ", LayerList(p, "wall_layers", FloorPlanLogic.DefaultWallLayers))} for walls). " +
                    "Check the layer names with list_layers and pass wall_layers, or pass wall_handles (two face lines per wall) / centerline_handles + thickness." +
                    (res.Skipped.Count > 0 ? $" Skipped: {string.Join("; ", res.Skipped.Take(5).Select(s => $"{s.Handle} {s.Reason}"))}." : "") +
                    (f == 1 && unitName.Contains("unitless") ? " The drawing has no units set (INSUNITS = 0): it was read as mm; a plan drawn in metres needs INSUNITS = 6." : ""));
            }
            var boxes = FloorPlanLogic.Boxes(res, hu);
            if (boxes.Count > FloorPlanLogic.MaxBoxes)
                throw ToolException.Invalid($"{tool}: {boxes.Count} wall pieces (more than {FloorPlanLogic.MaxBoxes}). Nothing was built. Pass area_min / area_max.");
            report["pieces"] = boxes.Count;
            if (dryRun)
            {
                report["dry_run"] = true;
                report["note"] = "Dry run: nothing was built. Check walls / openings / skipped, then call again without dry_run (add 'openings' for what was missed).";
                return report;
            }

            // rerun check: solids already on the result layers over this plan
            var fp = FloorPlanLogic.Footprint(res);
            if (fp.HasValue && onExisting == "stop")
            {
                double z0 = baseZ - hu.Slab, z1 = baseZ + hu.Wall;
                var dup = input.Existing.Where(x =>
                    FloorPlanLogic.BoxesOverlap(new[] { x.ext.MinPoint.X, x.ext.MinPoint.Y }, new[] { x.ext.MaxPoint.X, x.ext.MaxPoint.Y }, fp.Value.min, fp.Value.max) &&
                    x.ext.MinPoint.Z < z1 && x.ext.MaxPoint.Z > z0).Select(x => x.handle).ToList();
                if (dup.Count > 0)
                    throw ToolException.Invalid($"{tool}: this plan was already built: {dup.Count} solid(s) on {wall3d} / {slab3d} over it ({string.Join(", ", dup.Take(20))}{(dup.Count > 20 ? ", …" : "")}). " +
                        "Nothing was built. Ask the user: erase the old solids first (erase_entities, needs their yes), build another storey (base_z), or build again anyway with on_existing \"add\".");
            }

            var created = new List<string>();
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, wall3d, 7)) layersCreated.Add(wall3d);
            var wallP = LayerParams(tool, wall3d);
            var notes = new List<string>();
            double piecesVolume = 0;
            Solid3d walls = null;
            ObjectId wallsId = ObjectId.Null;
            int unionFailed = 0;
            var extra = new List<string>();
            foreach (var b in boxes)
            {
                var s = MakeBox(b.Length, b.Width, b.Height, null, new[] { b.Center[0], b.Center[1], baseZ + b.Center[2] }, b.RotationDeg);
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
                    extra.Add(Acad.H(AddSolid(ctx, s, wallP)));
                }
                catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
            }
            if (walls != null)
            {
                created.Add(Acad.H(wallsId));
                created.AddRange(extra);
                var wr = new Dictionary<string, object> { ["handle"] = Acad.H(wallsId), ["layer"] = wall3d };
                AddSolidNumbers(wr, walls);
                double vol = VolumeOf(walls) + extra.Sum(h => VolumeOf((Solid3d)Acad.GetEntity(ctx, h, false)));
                wr["volume_m3"] = Math.Round(vol * f * f * f / 1e9, 6);
                wr["pieces_volume_m3"] = Math.Round(piecesVolume * f * f * f / 1e9, 6);
                if (extra.Count > 0) { wr["separate_pieces"] = extra; notes.Add($"AutoCAD could not unite {unionFailed} wall piece(s); they are separate solids on {wall3d}."); }
                report["wall_solid"] = wr;
            }

            // slabs: top at base_z, slab_thickness down
            var slabs = new List<object>();
            if (slabMode != "none" && hmm.Slab > 0)
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
                    else
                    {
                        foreach (var (id, h) in input.Slabs)
                        {
                            var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForRead);
                            if (e is Region reg) { regions.Add(((Region)reg.Clone(), h)); continue; }
                            try
                            {
                                foreach (DBObject o in Region.CreateFromCurves(new DBObjectCollection { e })) if (o is Region r) regions.Add((r, h)); else o.Dispose();
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {
                                res.Skipped.Add(new PlanSkip(h, Acad.TypeName(e), $"slab outline could not become a region ({ex.ErrorStatus}: self-crossing or not flat): no slab from it"));
                            }
                        }
                    }
                    // a smaller outline inside a bigger one is a hole (stair, shaft)
                    regions = regions.OrderByDescending(x => SafeArea(x.r)).ToList();
                    var holes = new HashSet<int>();
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (holes.Contains(i)) continue;
                        for (int j = i + 1; j < regions.Count; j++)
                        {
                            if (holes.Contains(j)) continue;
                            if (!Inside(regions[j].r, regions[i].r)) continue;
                            using (var cut = (Region)regions[j].r.Clone()) regions[i].r.BooleanOperation(BooleanOperationType.BoolSubtract, cut);
                            holes.Add(j);
                        }
                    }
                    if (regions.Count > holes.Count && EnsureLayer(ctx, slab3d, 8)) layersCreated.Add(slab3d);
                    var slabP = LayerParams(tool, slab3d);
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (holes.Contains(i)) continue;
                        var reg = regions[i].r;
                        var s = new Solid3d();
                        try
                        {
                            s.Extrude(reg, -hu.Slab, 0);
                            // the outline may lie at any z (or have a downward normal): put the top at base_z
                            var ext = s.GeometricExtents;
                            s.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, baseZ - ext.MaxPoint.Z)));
                            ext = s.GeometricExtents;
                            if (Math.Abs(ext.MaxPoint.Z - ext.MinPoint.Z - hu.Slab) > 1e-6 * Math.Max(1, hu.Slab))
                                throw ToolException.Invalid($"{tool}: the slab outline {regions[i].src} is not horizontal (flat in XY). Nothing was built.");
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            s.Dispose();
                            throw ToolException.Invalid($"{tool}: AutoCAD could not build the slab from {regions[i].src} ({ex.ErrorStatus}). Nothing was built.");
                        }
                        catch { s.Dispose(); throw; }
                        var sid = AddSolid(ctx, s, slabP);
                        created.Add(Acad.H(sid));
                        var sr = new Dictionary<string, object> { ["handle"] = Acad.H(sid), ["from"] = regions[i].src, ["area_m2"] = Math.Round(SafeArea(reg) * f * f / 1e6, 4) };
                        AddSolidNumbers(sr, s);
                        sr["volume_m3"] = Math.Round(VolumeOf(s) * f * f * f / 1e9, 6);
                        slabs.Add(sr);
                    }
                }
                finally { foreach (var x in regions) x.r.Dispose(); }
                if (slabs.Count == 0 && slabMode == "auto")
                    notes.Add($"No slab: no closed outline on {string.Join(", ", LayerList(p, "slab_layers", FloorPlanLogic.DefaultSlabLayers))} and no slab_handles. Ask the user for the slab outline, or call again with slab \"bbox\" (one slab under the walls' bounding box) if that is what they want.");
            }
            report["slabs"] = slabs;
            report["skipped"] = SkipList(res.Skipped);
            report["skipped_count"] = res.Skipped.Count;
            report["handles"] = created.Cast<object>().ToList();
            if (layersCreated.Count > 0) report["layers_created"] = layersCreated;
            notes.Add("Plan objects were only read, not changed. Self-check now: set_view_3d sw_iso with these handles, set_visual_style conceptual, capture_view; compare volume_m3 / min / max with the plan.");
            report["notes"] = notes;
            return report;
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
            };
            var fp = FloorPlanLogic.Footprint(res);
            if (fp.HasValue) { r["plan_min"] = P2(fp.Value.min, f); r["plan_max"] = P2(fp.Value.max, f); }
            if (res.OpenGaps.Count > 0) r["open_gaps_note"] = "Gaps between wall parts with no door / window symbol: left open over the full height. Ask the user if they are doors / windows (pass them in 'openings').";
            return r;
        }
    }
}
