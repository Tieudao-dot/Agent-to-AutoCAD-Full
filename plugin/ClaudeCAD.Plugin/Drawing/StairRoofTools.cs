using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt E (13/11/2026): build_stair (straight / L / U stairs) and build_roof (flat / gable / hip roofs),
    /// labelled ToolGroup.ThreeD (Core/Plans.cs). Both only add new solids (no confirmation card); the outlines they
    /// read (stair well, house outline) are not changed. The geometry is StairLogic / RoofLogic (Core/StairRoof.cs, in
    /// mm); here it is scaled by INSUNITS and made with AutoCAD solids.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterStairRoof(ToolDispatcher d)
        {
            d.Register("build_stair", true, BuildStair);
            d.Register("build_roof", true, BuildRoof);
        }

        /// <summary>A Prism3 (mm) as a solid in drawing units (f mm per unit), not in the drawing yet.</summary>
        internal static Solid3d MakePrism(Prism3 pm, double f, string tool, string what)
        {
            var (org, w) = pm.Normalized();
            var pts = pm.Profile.Select(q => new[] { q[0] / f, q[1] / f, 0.0 }).ToList();
            Region reg = null;
            using (var pl = Acad.MakePolyline(pts, true))
            {
                DBObjectCollection made;
                try { made = Region.CreateFromCurves(new DBObjectCollection { pl }); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"{tool}: AutoCAD could not make the outline of the {what} ({ex.ErrorStatus}). Nothing was built."); }
                foreach (DBObject x in made) if (reg == null && x is Region r) reg = r; else x.Dispose();
            }
            if (reg == null) throw ToolException.Invalid($"{tool}: AutoCAD could not make the outline of the {what}. Nothing was built.");
            var s = new Solid3d();
            try
            {
                double depth = pm.Depth / f;
                s.Extrude(reg, depth, 0);
                // the extrusion follows the region normal: bring it to z = 0 … depth whichever way it went
                var ext = s.GeometricExtents;
                if (ext.MinPoint.Z < -depth / 2) s.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, depth)));
                s.TransformBy(Matrix3d.AlignCoordinateSystem(
                    Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                    new Point3d(org[0] / f, org[1] / f, org[2] / f),
                    new Vector3d(pm.U[0], pm.U[1], pm.U[2]), new Vector3d(pm.V[0], pm.V[1], pm.V[2]), new Vector3d(w[0], w[1], w[2])));
                return s;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                s.Dispose();
                throw ToolException.Invalid($"{tool}: AutoCAD could not build the {what} ({ex.ErrorStatus}). Nothing was built.");
            }
            catch { s.Dispose(); throw; }
            finally { reg.Dispose(); }
        }

        /// <summary>The prisms united into one solid on the layer (pieces AutoCAD cannot unite stay separate).</summary>
        private static List<ObjectId> UnitePrisms(ToolContext ctx, IEnumerable<Prism3> prisms, double f, Params layerP, string tool)
        {
            Solid3d first = null;
            var ids = new List<ObjectId>();
            foreach (var pm in prisms)
            {
                var s = MakePrism(pm, f, tool, pm.Part);
                if (first == null) { ids.Add(AddSolid(ctx, s, layerP)); first = s; continue; }
                try { BooleanInto(first, s, BooleanOperationType.BoolUnite); s.Dispose(); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { ids.Add(AddSolid(ctx, s, layerP)); }
                catch { if (s.ObjectId.IsNull) s.Dispose(); throw; }
            }
            return ids;
        }

        /// <summary>The points (WCS) of a closed lightweight polyline with straight segments.</summary>
        private static List<Point3d> ClosedOutline(ToolContext ctx, string h, string tool, string what)
        {
            var e = Acad.GetEntity(ctx, h, false);
            if (!(e is Polyline pl))
                throw ToolException.Invalid($"{tool}: {h} is a {Acad.TypeName(e)}; the {what} must be a closed lightweight polyline with straight segments.");
            if (Math.Abs(pl.Normal.Z) < 1 - 1e-9) throw ToolException.Invalid($"{tool}: {h} is not drawn flat in plan (its plane is tilted).");
            var pts = new List<Point3d>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                if (Math.Abs(pl.GetBulgeAt(i)) > 1e-9 && (pl.Closed || i < pl.NumberOfVertices - 1))
                    throw ToolException.Invalid($"{tool}: {h} has arc segments; the {what} must have straight segments only. Nothing was built.");
                pts.Add(pl.GetPoint3dAt(i));
            }
            bool closed = pl.Closed || (pts.Count > 2 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6 * Math.Max(1, pts[0].GetAsVector().Length));
            if (!closed) throw ToolException.Invalid($"{tool}: {h} is not closed; the {what} must be a closed polyline.");
            return pts;
        }

        // ================================================================= stairs

        internal static object BuildStair(ToolContext ctx, Params p)
        {
            const string tool = "build_stair";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var o = new StairLogic.Options
            {
                Type = p.Str("type", "straight"),
                Height = p.Num("height", 0),
                Riser = p.Num("riser", StairLogic.DefaultRiser),
                Tread = p.Num("tread", StairLogic.DefaultTread),
                Width = p.Num("width", StairLogic.DefaultWidth),
                Waist = p.Num("waist", StairLogic.DefaultWaist),
                Gap = p.Num("gap", StairLogic.DefaultGap),
                Landing = p.NumOpt("landing"),
                FirstRisers = p.Has("first_flight_risers") ? p.Int("first_flight_risers") : (int?)null,
                Turn = p.Str("turn", "left"),
                Railing = p.Str("railing", "none"),
            };
            if (p.Has("well_handle") && p.Has("well_points")) throw ToolException.Invalid($"{tool}: pass 'well_handle' or 'well_points', not both.");
            bool hasWell = p.Has("well_handle") || p.Has("well_points");
            if (!p.Has("start") && !hasWell)
                throw ToolException.Invalid($"{tool}: pass 'start' (the middle of the bottom edge of the first step, + 'direction') or the stair well ('well_handle' / 'well_points'). Nothing was built.");
            var lay = StairLogic.Build(o, tool);
            List<double[]> well = null;
            double wellZ = 0;
            string wellFrom = null;
            if (p.Has("well_handle"))
            {
                wellFrom = p.Handle("well_handle").ToUpperInvariant();
                var pts = ClosedOutline(ctx, wellFrom, tool, "stair well");
                well = pts.Select(q => new[] { q.X * f, q.Y * f }).ToList();
                wellZ = pts[0].Z;
            }
            else if (p.Has("well_points"))
            {
                var pts = p.Points("well_points", 3, 200);
                well = pts.Select(q => new[] { q[0] * f, q[1] * f }).ToList();
                wellZ = pts[0].Length > 2 ? pts[0][2] : 0;
                wellFrom = "well_points";
            }
            StairLogic.Frame frame;
            double startZ = 0;
            if (p.Has("start"))
            {
                var s0 = p.Point("start");
                startZ = s0.Length > 2 ? s0[2] : 0;
                frame = StairLogic.FrameFromStart(new[] { s0[0] * f, s0[1] * f }, p.Num("direction", 0), o);
                if (well != null)
                {
                    var bad = StairLogic.Outside(lay, frame, RoofLogic.Clean(well));
                    if (bad.Count > 0)
                        throw ToolException.Invalid($"{tool}: the stair does not fit the well {wellFrom}: {string.Join(", ", bad)} stick(s) out. The stair needs {Math.Round(lay.Length)} × {Math.Round(lay.Breadth)} mm ({lay.Risers} risers of {Math.Round(lay.RiserHeight, 1)} mm, tread {o.Tread}). Nothing was built. Move the start / direction, or leave 'start' out so the stair is fitted into the well, or change width / tread / type.");
                }
            }
            else
            {
                frame = StairLogic.FitInWell(lay, well, o, p.Has("turn"));
                if (frame == null)
                {
                    var c = RoofLogic.Clean(well);
                    double wx = c.Max(q => q[0]) - c.Min(q => q[0]), wy = c.Max(q => q[1]) - c.Min(q => q[1]);
                    throw ToolException.Invalid($"{tool}: the {(o.Type == "straight" ? "straight" : o.Type.ToUpperInvariant())} stair does not fit the well {wellFrom} (about {Math.Round(Math.Max(wx, wy))} × {Math.Round(Math.Min(wx, wy))} mm): it needs {Math.Round(lay.Length)} × {Math.Round(lay.Breadth)} mm " +
                                                $"({lay.Risers} risers of {Math.Round(lay.RiserHeight, 1)} mm, tread {o.Tread}, width {o.Width}{(o.Type == "u" ? $", gap {o.Gap}" : "")}). Nothing was built. Tell the user; options: another type (L / U), a narrower width, a shorter tread, a bigger well.");
                }
                if (o.Type != "straight" && StairLogic.TurnOf(frame) != o.Turn)
                {
                    // the well decided the turn: lay out again so the railing sides follow it
                    o.Turn = StairLogic.TurnOf(frame);
                    lay = StairLogic.Build(o, tool);
                }
            }
            double baseMm = p.Has("base_z") ? p.Num("base_z") : (p.Has("start") ? startZ : wellZ) * f;
            string layer = p.Str("layer", StairLogic.Layer3d);
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, 6)) layersCreated.Add(layer);
            var ids = UnitePrisms(ctx, StairLogic.Place(lay.Pieces, frame, baseMm), f, LayerParams(tool, layer), tool);
            var stair = (Solid3d)ctx.Tr.GetObject(ids[0], OpenMode.ForRead);
            double vol = ids.Sum(id => VolumeOf((Solid3d)ctx.Tr.GetObject(id, OpenMode.ForRead)));
            var r = Acad.Handles(ids[0]);
            r["handles"] = ids.Select(x => (object)Acad.H(x)).ToList();
            AddSolidNumbers(r, stair);
            r["volume_m3"] = Math.Round(Solid3Logic.M3(vol, f), 6);
            r["type"] = o.Type;
            r["risers"] = lay.Risers;
            r["riser_mm"] = Math.Round(lay.RiserHeight, 1);
            r["tread_mm"] = o.Tread;
            r["width_mm"] = o.Width;
            r["height_mm"] = o.Height;
            r["flights"] = lay.Flights.Select(x => (object)new Dictionary<string, object> { ["risers"] = x.Risers, ["run_mm"] = Math.Round(x.Run), ["from_z_mm"] = Math.Round(baseMm + x.BaseZ, 1) }).ToList();
            if (o.Type != "straight")
            {
                r["turn"] = o.Turn;
                r["landing_mm"] = o.Landing ?? o.Width;
                r["landing_z_mm"] = Math.Round(baseMm + lay.Flights[1].BaseZ, 1);
            }
            r["footprint_mm"] = new List<object> { Math.Round(lay.Length), Math.Round(lay.Breadth) };
            var startPt = new[] { frame.Origin[0] + frame.Y[0] * o.Width / 2, frame.Origin[1] + frame.Y[1] * o.Width / 2 };
            r["start"] = new List<object> { Acad.R(startPt[0] / f), Acad.R(startPt[1] / f), Acad.R(baseMm / f) };
            r["direction_deg"] = Math.Round((Math.Atan2(frame.X[1], frame.X[0]) * 180 / Math.PI + 360) % 360, 3);
            r["base_z_mm"] = Math.Round(baseMm, 1);
            r["layer"] = layer;
            if (wellFrom != null) r["well"] = wellFrom;
            if (ids.Count > 1) r["separate_pieces"] = ids.Skip(1).Select(x => (object)Acad.H(x)).ToList();
            if (lay.Rail.Count > 0)
            {
                string railLayer = p.Str("rail_layer", RailingLogic.Layer3d);
                if (EnsureLayer(ctx, railLayer, 3)) layersCreated.Add(railLayer);
                var rids = UnitePrisms(ctx, StairLogic.Place(lay.Rail, frame, baseMm), f, LayerParams(tool, railLayer), tool);
                // every new solid in "handles" (Claude may change what it built in this turn without a card)
                ((List<object>)r["handles"]).AddRange(rids.Select(x => (object)Acad.H(x)));
                r["railing"] = new Dictionary<string, object>
                {
                    ["handles"] = rids.Select(x => (object)Acad.H(x)).ToList(),
                    ["sides"] = o.Railing,
                    ["layer"] = railLayer,
                    ["height_mm"] = StairLogic.RailHeight,
                };
            }
            if (lay.Warnings.Count > 0) r["warnings"] = lay.Warnings;
            r["note"] = $"{lay.Risers} risers of {Math.Round(lay.RiserHeight, 1)} mm (the height split evenly), tread {o.Tread} mm, waist {o.Waist} mm; " +
                        $"the top step reaches the upper floor ({StairLogic.TopLedge} mm onto it)." +
                        (lay.Rail.Count > 0 ? " Railing: handrail + posts along the flights (not around the landing)." : "") +
                        " Tell the user the riser count and height. Check with capture_view (sw_iso, conceptual).";
            if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
            return r;
        }

        // ================================================================= roofs

        /// <summary>Wall solids (on the wall layers) whose plan box overlaps the box (drawing units).</summary>
        private static List<Extents3d> WallBoxes(ToolContext ctx, IList<string> layers, double[] box)
        {
            var res = new List<Extents3d>();
            foreach (ObjectId id in ctx.ModelSpace)
            {
                if (id.IsErased || id.ObjectClass.DxfName != "3DSOLID") continue;
                var s = ctx.Tr.GetObject(id, OpenMode.ForRead) as Solid3d;
                if (s == null || s.IsNull || !FloorPlanLogic.LayerMatches(s.Layer, layers)) continue;
                var ext = EntityInfo.Extents(s);
                if (!ext.HasValue) continue;
                var e = ext.Value;
                if (box != null && (e.MaxPoint.X < box[0] || e.MinPoint.X > box[2] || e.MaxPoint.Y < box[1] || e.MinPoint.Y > box[3])) continue;
                res.Add(e);
            }
            return res;
        }

        internal static object BuildRoof(ToolContext ctx, Params p)
        {
            const string tool = "build_roof";
            double f = FloorPlanLogic.MmPerUnit(Convert.ToInt32(ctx.Db.Insunits), out _);
            var o = new RoofLogic.Options
            {
                Type = p.Str("type", "hip"),
                Thickness = p.Num("thickness", RoofLogic.DefaultThickness),
                SlopeDeg = p.Num("slope", RoofLogic.DefaultSlopeDeg),
                Overhang = p.NumOpt("overhang"),
                Ridge = p.Str("ridge", "long"),
            };
            RoofLogic.Check(o, tool);
            int sources = (p.Has("outline_handle") ? 1 : 0) + (p.Has("outline_points") ? 1 : 0) + (p.Bool("from_walls", false) ? 1 : 0);
            if (sources != 1)
                throw ToolException.Invalid($"{tool}: pass one of 'outline_handle' (a closed polyline around the outer face of the walls), 'outline_points' [[x, y], ...] or from_walls: true. Nothing was built.");
            var wallLayers = p.Has("wall_layers") ? p.Strings("wall_layers", 1) : new List<string> { FloorPlanLogic.WallLayer3d };
            List<double[]> outline;
            double outlineZ = 0;
            string from;
            if (p.Has("outline_handle"))
            {
                from = p.Handle("outline_handle").ToUpperInvariant();
                var pts = ClosedOutline(ctx, from, tool, "house outline");
                outline = pts.Select(q => new[] { q.X, q.Y }).ToList();
                outlineZ = pts[0].Z;
            }
            else if (p.Has("outline_points"))
            {
                from = "outline_points";
                var pts = p.Points("outline_points", 3, RoofLogic.MaxPoints);
                outline = pts.Select(q => new[] { q[0], q[1] }).ToList();
                outlineZ = pts[0].Length > 2 ? pts[0][2] : 0;
            }
            else
            {
                from = "walls";
                var walls = WallBoxes(ctx, wallLayers, null);
                if (walls.Count == 0)
                    throw ToolException.Invalid($"{tool}: no wall solids on {string.Join(", ", wallLayers)}. Build the walls first (build_3d_from_plan), pass wall_layers, or give outline_handle / outline_points. Nothing was built.");
                double x0 = walls.Min(e => e.MinPoint.X), y0 = walls.Min(e => e.MinPoint.Y), x1 = walls.Max(e => e.MaxPoint.X), y1 = walls.Max(e => e.MaxPoint.Y);
                outline = new List<double[]> { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
            }
            // the wall top: given, or the highest wall solid under the outline
            double wallTopMm;
            string topFrom;
            if (p.Has("wall_top_z")) { wallTopMm = p.Num("wall_top_z"); topFrom = "wall_top_z"; }
            else
            {
                var box = new[] { outline.Min(q => q[0]), outline.Min(q => q[1]), outline.Max(q => q[0]), outline.Max(q => q[1]) };
                var walls = WallBoxes(ctx, wallLayers, box);
                if (walls.Count > 0) { wallTopMm = walls.Max(e => e.MaxPoint.Z) * f; topFrom = $"top of the {walls.Count} wall solid(s) on {string.Join(", ", wallLayers)}"; }
                else { wallTopMm = outlineZ * f; topFrom = "outline level (no wall solid found under it)"; }
            }
            var lay = RoofLogic.Build(outline.Select(q => new[] { q[0] * f, q[1] * f }).ToList(), wallTopMm, o, tool);
            string layer = p.Str("layer", RoofLogic.Layer3d);
            var layersCreated = new List<string>();
            if (EnsureLayer(ctx, layer, 30)) layersCreated.Add(layer);
            var roof = MakePrism(lay.Base, f, tool, "roof");
            try
            {
                foreach (var c in lay.Outer)
                {
                    using (var cut = MakePrism(c, f, tool, "roof plane")) BooleanInto(roof, cut, BooleanOperationType.BoolIntersect);
                    if (IsEmpty(roof)) throw ToolException.Invalid($"{tool}: the roof came out empty (AutoCAD). Nothing was built.");
                }
                if (lay.Inner.Count > 0)
                {
                    using (var inner = MakePrism(lay.Inner[0], f, tool, "roof underside"))
                    {
                        foreach (var c in lay.Inner.Skip(1))
                            using (var cut = MakePrism(c, f, tool, "roof underside")) BooleanInto(inner, cut, BooleanOperationType.BoolIntersect);
                        BooleanInto(roof, inner, BooleanOperationType.BoolSubtract);
                    }
                    if (IsEmpty(roof)) throw ToolException.Invalid($"{tool}: the roof came out empty (AutoCAD). Nothing was built.");
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                roof.Dispose();
                throw ToolException.Invalid($"{tool}: AutoCAD could not cut the roof planes ({ex.ErrorStatus}). Nothing was built.");
            }
            catch { roof.Dispose(); throw; }
            var id = AddSolid(ctx, roof, LayerParams(tool, layer));
            var r = Acad.Handles(id);
            AddSolidNumbers(r, roof);
            r["volume_m3"] = Math.Round(Solid3Logic.M3(VolumeOf(roof), f), 6);
            r["type"] = o.Type;
            r["thickness_mm"] = o.Thickness;
            if (o.Type != "flat") r["slope_deg"] = o.SlopeDeg;
            r["overhang_mm"] = lay.Overhang;
            r["wall_top_z_mm"] = Math.Round(wallTopMm, 1);
            r["wall_top_from"] = topFrom;
            r["eave_z_mm"] = Math.Round(lay.EaveZ, 1);
            r["top_z_mm"] = Math.Round(lay.TopZ, 1);
            if (lay.RidgeLength > 0) r["ridge_length_m"] = Math.Round(lay.RidgeLength / 1000, 3);
            r["plan_area_m2"] = Math.Round(lay.PlanArea / 1e6, 3);
            r["roof_area_m2"] = Math.Round(lay.RoofArea / 1e6, 3);
            r["outline"] = from;
            r["layer"] = layer;
            if (lay.Notes.Count > 0) r["notes"] = lay.Notes;
            r["note"] = (o.Type == "flat"
                            ? $"Flat roof slab {o.Thickness} mm on the walls"
                            : $"{(o.Type == "hip" ? "Hip" : "Gable")} roof, {o.SlopeDeg}°, slab {o.Thickness} mm, eaves {lay.Overhang} mm past the walls; the underside meets the wall top at the wall line") +
                        $" (wall top {Math.Round(wallTopMm)} mm: {topFrom}). The outline was only read. Check with capture_view (sw_iso, conceptual).";
            if (layersCreated.Count > 0) r["layers_created"] = layersCreated;
            return r;
        }
    }
}
