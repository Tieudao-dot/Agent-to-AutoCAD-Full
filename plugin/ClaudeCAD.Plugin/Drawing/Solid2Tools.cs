using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using Brep = Autodesk.AutoCAD.BoundaryRepresentation.Brep;
using BrEdge = Autodesk.AutoCAD.BoundaryRepresentation.Edge;
using BrFace = Autodesk.AutoCAD.BoundaryRepresentation.Face;
using BrContainment = Autodesk.AutoCAD.BoundaryRepresentation.PointContainment;
using BrLoop = Autodesk.AutoCAD.BoundaryRepresentation.BoundaryLoop;
using GeSurface = Autodesk.AutoCAD.Geometry.Surface;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// 3D phase 2: sweep / loft, slice, fillet / chamfer edges, shell, 3D rotate / mirror / align, UCS, interference
    /// and the edge / face lists of get_solid_info. Labelled ToolGroup.ThreeD (Core/Plans.cs). Every coordinate is WCS
    /// mm like the rest of the tools (also after set_ucs). The AutoCAD-free part is Solid2Logic in Core/Solids2.cs.
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterSolids2(ToolDispatcher d)
        {
            d.Register("sweep", true, Sweep);
            d.Register("loft", true, Loft);
            d.Register("slice_solid", true, SliceSolidTool);
            d.Register("fillet_edges", true, FilletEdgesTool);
            d.Register("chamfer_edges", true, ChamferEdgesTool);
            d.Register("shell_solid", true, ShellSolidTool);
            d.Register("rotate_3d", true, Rotate3d);
            d.Register("mirror_3d", true, Mirror3d);
            d.Register("align", true, AlignTool);
            // nâng cấp đợt D: with table_position (Full only, PlanLogic.TextParams) it also inserts the clash table
            d.Register(new ToolDef("check_interference", true, CheckInterferenceTool, readsWhen: p => !p.Has("table_position")));
            // the UCS is a drawing setting, not an object: no transaction, not in a batch, not recorded in macros; U undoes it with the turn
            d.Register(new ToolDef("set_ucs", true, SetUcs, noTransaction: true));
            d.Register(new ToolDef("reset_ucs", true, ResetUcs, noTransaction: true));
        }

        // ================================================================= topology (Brep)

        /// <summary>The edges and faces of a solid in Brep order, their subentity ids and the topology id.</summary>
        internal sealed class SolidTopology
        {
            public List<EdgeDesc> Edges = new List<EdgeDesc>();
            public List<FaceDesc> Faces = new List<FaceDesc>();
            public List<SubentityId> EdgeIds = new List<SubentityId>();
            public List<SubentityId> FaceIds = new List<SubentityId>();
            public double[] Min;
            public double[] Max;
            public string Id;
        }

        private static double[] J(Point3d p) => new[] { p.X, p.Y, p.Z };

        internal static SolidTopology ReadTopology(Solid3d s)
        {
            var t = new SolidTopology();
            var ext = s.GeometricExtents;
            t.Min = J(ext.MinPoint);
            t.Max = J(ext.MaxPoint);
            var edgeIndex = new Dictionary<SubentityId, int>();
            using (var brep = new Brep(s))
            {
                foreach (BrEdge e in brep.Edges)
                {
                    using (e)
                    {
                        var d = new EdgeDesc { Index = t.Edges.Count };
                        ReadEdge(e, d);
                        var id = e.SubentityPath.SubentId;
                        edgeIndex[id] = d.Index;
                        t.Edges.Add(d);
                        t.EdgeIds.Add(id);
                    }
                }
                foreach (BrFace f in brep.Faces)
                {
                    using (f)
                    {
                        var d = new FaceDesc { Index = t.Faces.Count };
                        var pts = new List<double[]>();
                        try
                        {
                            foreach (BrLoop loop in f.Loops)
                            {
                                using (loop)
                                {
                                    foreach (BrEdge le in loop.Edges)
                                    {
                                        using (le)
                                        {
                                            if (!edgeIndex.TryGetValue(le.SubentityPath.SubentId, out var ei)) continue;
                                            var ed = t.Edges[ei];
                                            if (!ed.Faces.Contains(d.Index)) ed.Faces.Add(d.Index);
                                            if (ed.Start != null) pts.Add(ed.Start);
                                            if (ed.End != null) pts.Add(ed.End);
                                            if (ed.Mid != null) pts.Add(ed.Mid);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { Logger.Warn("face loops not read: " + ex.Message); }
                        ReadFace(brep, f, d, pts);
                        t.Faces.Add(d);
                        t.FaceIds.Add(f.SubentityPath.SubentId);
                    }
                }
            }
            t.Id = Solid2Logic.TopologyId(t.Edges, t.Faces, t.Min);
            return t;
        }

        private static void ReadEdge(BrEdge e, EdgeDesc d)
        {
            Curve3d c = null;
            try { c = e.Curve; } catch (Exception) { }
            try
            {
                d.Start = J(e.Vertex1.Point);
                d.End = J(e.Vertex2.Point);
            }
            catch (Exception)
            {
                // a closed edge (full circle) may have no vertex
                try { if (c != null) { d.Start = J(c.StartPoint); d.End = J(c.EndPoint); } } catch (Exception) { }
            }
            if (c is ExternalCurve3d xc) d.Straight = xc.IsLineSegment || xc.IsLine;
            else d.Straight = c is LinearEntity3d;
            try
            {
                if (c != null)
                {
                    var iv = c.GetInterval();
                    d.Mid = J(c.EvaluatePoint((iv.LowerBound + iv.UpperBound) / 2));
                }
            }
            catch (Exception) { }
            if (d.Mid == null && d.Start != null && d.End != null && d.Straight)
                d.Mid = new[] { (d.Start[0] + d.End[0]) / 2, (d.Start[1] + d.End[1]) / 2, (d.Start[2] + d.End[2]) / 2 };
            if (d.Start == null && d.Mid != null) { d.Start = d.Mid; d.End = d.Mid; }
            c?.Dispose();
        }

        private static void ReadFace(Brep brep, BrFace f, FaceDesc d, List<double[]> pts)
        {
            try { d.Area = f.GetArea(); } catch (Exception) { }
            if (pts.Count > 0)
            {
                d.Min = new[] { pts.Min(p => p[0]), pts.Min(p => p[1]), pts.Min(p => p[2]) };
                d.Max = new[] { pts.Max(p => p[0]), pts.Max(p => p[1]), pts.Max(p => p[2]) };
            }
            else
            {
                try
                {
                    var bb = f.BoundBlock;
                    d.Min = J(bb.GetMinimumPoint());
                    d.Max = J(bb.GetMaximumPoint());
                }
                catch (Exception) { }
            }
            if (d.Min != null) d.Center = new[] { (d.Min[0] + d.Max[0]) / 2, (d.Min[1] + d.Max[1]) / 2, (d.Min[2] + d.Max[2]) / 2 };
            GeSurface surf = null;
            try
            {
                surf = f.Surface;
                Vector3d? n = null;
                if (surf is ExternalBoundedSurface xs && xs.IsPlane)
                {
                    d.Planar = true;
                    try { if (xs.BaseSurface is PlanarEntity pe) n = pe.Normal; } catch (Exception) { }
                    if (!n.HasValue && d.Center != null)
                    {
                        try { using (var pos = surf.GetClosestPointTo(new Point3d(d.Center[0], d.Center[1], d.Center[2]))) n = pos.GetNormal(); } catch (Exception) { }
                    }
                }
                else if (surf is PlanarEntity pe2) { d.Planar = true; n = pe2.Normal; }
                if (n.HasValue && n.Value.Length > 1e-12)
                {
                    var v = n.Value.GetNormal();
                    if (!f.IsOrientToSurface) v = v.Negate();
                    // the outward side: just outside the face must not be inside the solid
                    if (d.Center != null)
                    {
                        try
                        {
                            double step = Math.Max(Solid2Logic.Tolerance(d.Min, d.Max) * 10, 1e-2);
                            var c = new Point3d(d.Center[0], d.Center[1], d.Center[2]);
                            using (brep.GetPointContainment(c + v * step, out var plus))
                            using (brep.GetPointContainment(c - v * step, out var minus))
                            {
                                if (plus == BrContainment.Inside && minus == BrContainment.Outside) v = v.Negate();
                            }
                        }
                        catch (Exception) { }
                    }
                    d.Normal = new[] { v.X, v.Y, v.Z };
                }
                else d.Planar = false;
            }
            catch (Exception ex) { Logger.Warn("face surface not read: " + ex.Message); }
            finally { surf?.Dispose(); }
        }

        /// <summary>The edge / face lists of get_solid_info (at most max of each).</summary>
        private static void AddTopologyLists(Dictionary<string, object> row, SolidTopology t, bool edges, bool faces, int max)
        {
            row["topology_id"] = t.Id;
            if (edges)
            {
                var axisOf = new Dictionary<int, string>();
                foreach (var a in new[] { "x", "y", "z" })
                    foreach (var i in Solid2Logic.FilterEdges(t.Edges, new[] { "parallel_" + a }, t.Min, t.Max, "get_solid_info")) axisOf[i] = a;
                row["edge_list"] = t.Edges.Take(max).Select(e =>
                {
                    var o = new Dictionary<string, object>
                    {
                        ["i"] = e.Index,
                        ["type"] = e.Straight ? "line" : "curve",
                        ["start"] = e.Start != null ? R3(e.Start) : null,
                        ["end"] = e.End != null ? R3(e.End) : null,
                        ["faces"] = e.Faces.Cast<object>().ToList(),
                    };
                    if (!e.Straight && e.Mid != null) o["mid"] = R3(e.Mid);
                    if (axisOf.TryGetValue(e.Index, out var ax)) o["parallel"] = ax;
                    return (object)o;
                }).ToList();
                if (t.Edges.Count > max) row["edge_list_truncated"] = $"first {max} of {t.Edges.Count} edges; use edge_filter in fillet_edges / chamfer_edges, or a larger max_items";
            }
            if (faces)
            {
                var sideOf = new Dictionary<int, string>();
                foreach (var s in Solid2Logic.FaceSides)
                    foreach (var i in Solid2Logic.FilterFaces(t.Faces, new[] { s }, t.Min, t.Max, "get_solid_info")) if (!sideOf.ContainsKey(i)) sideOf[i] = s;
                row["face_list"] = t.Faces.Take(max).Select(f =>
                {
                    var o = new Dictionary<string, object>
                    {
                        ["i"] = f.Index,
                        ["type"] = f.Planar ? "plane" : "curved",
                        ["center"] = f.Center != null ? R3(f.Center) : null,
                        ["area"] = Acad.R(f.Area),
                    };
                    if (f.Normal != null) o["normal"] = f.Normal.Select(x => (object)Math.Round(x, 4)).ToList();
                    if (sideOf.TryGetValue(f.Index, out var side)) o["side"] = side;
                    return (object)o;
                }).ToList();
                if (t.Faces.Count > max) row["face_list_truncated"] = $"first {max} of {t.Faces.Count} faces; use face_filter, or a larger max_items";
            }
        }

        private static List<object> R3(double[] p) => new List<object> { Acad.R(p[0]), Acad.R(p[1]), Acad.R(p[2]) };

        // ================================================================= selection of edges / faces

        private static List<int> IntList(Params p, string name)
        {
            var nums = p.Numbers(name);
            var r = new List<int>(nums.Count);
            foreach (var x in nums)
            {
                if (Math.Abs(x - Math.Round(x)) > 1e-9) throw ToolException.Invalid($"{p.Tool}: '{name}' takes whole indices from get_solid_info (got {x}).");
                r.Add((int)Math.Round(x));
            }
            return r;
        }

        /// <summary>Indices from 'edges' (with topology_id) or 'edge_filter'.</summary>
        private static List<int> PickEdges(Params p, SolidTopology t, string handle)
        {
            bool byIndex = p.Has("edges"), byFilter = p.Has("edge_filter");
            if (byIndex && byFilter) throw ToolException.Invalid($"{p.Tool}: pass either 'edges' (indices) or 'edge_filter', not both.");
            if (!byIndex && !byFilter) throw ToolException.Invalid($"{p.Tool}: pass 'edges' (indices from get_solid_info with edges: true, plus its topology_id) or 'edge_filter' (e.g. [\"parallel_z\"]).");
            if (byIndex)
            {
                RequireTopology(p, t, handle);
                return Solid2Logic.CheckIndices(IntList(p, "edges"), t.Edges.Count, "edge", p.Tool, Solid2Logic.MaxEdgeSelection);
            }
            var picked = Solid2Logic.FilterEdges(t.Edges, p.Strings("edge_filter", 1), t.Min, t.Max, p.Tool);
            if (picked.Count == 0) throw ToolException.Invalid($"{p.Tool}: no edge of {handle} matches edge_filter {string.Join(" + ", p.Strings("edge_filter", 1))}. Nothing was changed. Call get_solid_info with edges: true to see its edges.");
            if (picked.Count > Solid2Logic.MaxEdgeSelection) throw ToolException.Invalid($"{p.Tool}: the filter picks {picked.Count} edges; at most {Solid2Logic.MaxEdgeSelection} per call.");
            return picked;
        }

        /// <summary>Indices from 'faces' (with topology_id) or 'face_filter'; none = an empty list.</summary>
        private static List<int> PickFaces(Params p, SolidTopology t, string handle)
        {
            bool byIndex = p.Has("faces"), byFilter = p.Has("face_filter");
            if (byIndex && byFilter) throw ToolException.Invalid($"{p.Tool}: pass either 'faces' (indices) or 'face_filter', not both.");
            if (byIndex)
            {
                // faces: [] = a closed shell: no index, so no topology_id needed
                if (p.Numbers("faces").Count == 0) return new List<int>();
                RequireTopology(p, t, handle);
                return Solid2Logic.CheckIndices(IntList(p, "faces"), t.Faces.Count, "face", p.Tool, Solid2Logic.MaxFaceSelection);
            }
            if (!byFilter) return new List<int>();
            var picked = Solid2Logic.FilterFaces(t.Faces, p.Strings("face_filter", 1), t.Min, t.Max, p.Tool);
            if (picked.Count == 0) throw ToolException.Invalid($"{p.Tool}: no flat face of {handle} lies on the {string.Join(" / ", p.Strings("face_filter", 1))} side of its bounding box. Nothing was changed. Call get_solid_info with faces: true and pass 'faces'.");
            return picked;
        }

        private static void RequireTopology(Params p, SolidTopology t, string handle)
        {
            if (!p.Has("topology_id")) throw ToolException.Invalid($"{p.Tool}: indices need the 'topology_id' get_solid_info returned with them (it proves the solid has not changed since).");
            if (!string.Equals(p.Str("topology_id"), t.Id, StringComparison.OrdinalIgnoreCase)) throw ToolException.Invalid(Solid2Logic.StaleMessage(p.Tool, handle));
        }

        private static double VolumeOf(Solid3d s)
        {
            try { return s.MassProperties.Volume; } catch (Autodesk.AutoCAD.Runtime.Exception) { return 0; }
        }

        private static Dictionary<string, object> ChangedResult(string handle, Solid3d s, double before)
        {
            var r = new Dictionary<string, object> { ["handle"] = handle, ["handles"] = new List<object> { handle } };
            AddSolidNumbers(r, s);
            r["volume_before"] = Acad.R(before);
            return r;
        }

        // ================================================================= fillet / chamfer / shell

        /// <summary>FilletEdges with one radius (also used by the self-test). An AutoCAD refusal becomes INVALID_PARAMS.</summary>
        internal static void FilletSolid(Solid3d s, IList<SubentityId> ids, double radius, string tool)
        {
            Solid2Logic.CheckRadius(radius, "radius", tool);
            var rad = new DoubleCollection();
            var s0 = new DoubleCollection();
            var s1 = new DoubleCollection();
            foreach (var _ in ids) { rad.Add(radius); s0.Add(0); s1.Add(0); }
            try { s.FilletEdges(ids.ToArray(), rad, s0, s1); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw ToolException.Invalid($"{tool}: AutoCAD could not fillet these edges with radius {radius} ({ex.ErrorStatus}). Nothing was changed. " +
                    "A radius as large as (or larger than) a face next to the edge is the common cause: use a smaller radius, or fewer edges.");
            }
            if (s.IsNull || VolumeOf(s) <= 1e-9) throw ToolException.Invalid($"{tool}: the fillet left no solid. Nothing was changed. Use a smaller radius.");
        }

        private static object FilletEdgesTool(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle").ToUpperInvariant();
            double r = p.Positive("radius");
            var s = SolidForWrite(ctx, h, p.Tool);
            var t = ReadTopology(s);
            var picked = PickEdges(p, t, h);
            double before = VolumeOf(s);
            FilletSolid(s, picked.Select(i => t.EdgeIds[i]).ToList(), r, p.Tool);
            var res = ChangedResult(h, s, before);
            res["edges_filleted"] = picked.Count;
            return res;
        }

        private static object ChamferEdgesTool(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle").ToUpperInvariant();
            double d1 = p.Positive("distance");
            double d2 = p.Has("distance2") ? p.Positive("distance2") : d1;
            var s = SolidForWrite(ctx, h, p.Tool);
            var t = ReadTopology(s);
            var picked = PickEdges(p, t, h);
            int? baseFace = null;
            if (p.Has("base_face"))
            {
                RequireTopology(p, t, h);
                baseFace = Solid2Logic.CheckIndices(new[] { p.Int("base_face") }, t.Faces.Count, "face", p.Tool, 1)[0];
            }
            else if (Math.Abs(d1 - d2) > 1e-9)
                throw ToolException.Invalid($"{p.Tool}: with two different distances pass base_face (the face 'distance' is measured on; get_solid_info lists the faces of each edge).");
            double before = VolumeOf(s);
            var groups = Solid2Logic.ChamferGroups(t.Edges, picked, baseFace, p.Tool);
            double tol = Math.Max(Solid2Logic.Tolerance(t.Min, t.Max) * 10, 1e-3);
            var cur = t;
            // later groups are found again by geometry: ChamferEdges renumbers the edges and faces it changed
            var plan = groups.Select(g => (face: t.Faces[g.face], edges: g.edges.Select(i => t.Edges[i]).ToList())).ToList();
            for (int gi = 0; gi < plan.Count; gi++)
            {
                var (face, edges) = plan[gi];
                List<int> idx;
                int fIdx;
                if (gi == 0) { idx = groups[0].edges; fIdx = groups[0].face; }
                else
                {
                    cur = ReadTopology(s);
                    idx = new List<int>();
                    foreach (var e in edges)
                    {
                        var m = Solid2Logic.MatchEdge(e, cur.Edges, tol);
                        if (m == null) throw ToolException.Invalid($"{p.Tool}: after chamfering the first edges AutoCAD changed the others too much to find them again. Nothing was changed. Chamfer the edges of one face at a time (base_face), or use fillet_edges.");
                        idx.Add(m.Value);
                    }
                    var common = idx.Select(i => cur.Edges[i].Faces).Aggregate((a, b) => a.Intersect(b).ToList());
                    int? nf = common.Cast<int?>().FirstOrDefault(fi => SameFace(face, cur.Faces[fi.Value], tol)) ?? common.Cast<int?>().FirstOrDefault();
                    if (nf == null) throw ToolException.Invalid($"{p.Tool}: the edges no longer share a face after the first chamfer. Nothing was changed. Chamfer them in smaller groups.");
                    fIdx = nf.Value;
                }
                try { s.ChamferEdges(idx.Select(i => cur.EdgeIds[i]).ToArray(), cur.FaceIds[fIdx], d1, d2); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    throw ToolException.Invalid($"{p.Tool}: AutoCAD could not chamfer these edges by {d1}{(d2 != d1 ? " × " + d2 : "")} ({ex.ErrorStatus}). Nothing was changed. " +
                        "A distance as large as a face next to the edge is the common cause: use a smaller distance.");
                }
            }
            if (s.IsNull || VolumeOf(s) <= 1e-9) throw ToolException.Invalid($"{p.Tool}: the chamfer left no solid. Nothing was changed.");
            var res = ChangedResult(h, s, before);
            res["edges_chamfered"] = picked.Count;
            return res;
        }

        private static bool SameFace(FaceDesc a, FaceDesc b, double tol)
        {
            if (a.Planar != b.Planar) return false;
            if (!a.Planar || a.Normal == null || b.Normal == null || a.Center == null || b.Center == null) return true;
            if (Math.Abs(Math.Abs(SolidLogic.Dot(a.Normal, b.Normal)) - 1) > 1e-6) return false;
            return Math.Abs(SolidLogic.Dot(SolidLogic.Sub(b.Center, a.Center), a.Normal)) <= tol;
        }

        private static object ShellSolidTool(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle").ToUpperInvariant();
            double th = p.Positive("thickness");
            var s = SolidForWrite(ctx, h, p.Tool);
            var t = ReadTopology(s);
            var open = PickFaces(p, t, h);
            if (open.Count >= t.Faces.Count) throw ToolException.Invalid($"{p.Tool}: cannot open every face of {h}; leave at least one closed.");
            double before = VolumeOf(s);
            double size = Math.Min(t.Max[0] - t.Min[0], Math.Min(t.Max[1] - t.Min[1], t.Max[2] - t.Min[2]));
            // a closed shell has two walls across the smallest size, an open one at least one
            if (th * (open.Count == 0 ? 2 : 1) >= size) throw ToolException.Invalid($"{p.Tool}: thickness {th} is too large for {h} (its smallest size is {Acad.R(size)} mm); nothing would be left inside. Nothing was changed.");
            // AcDbMgd.xml: a negative offset shells inward (the outer size stays). The faces are the solid's own (the ids
            // get_solid_info listed); a result that grew or kept its volume is refused and the turn's transaction undoes it.
            try { s.ShellBody(open.Select(i => t.FaceIds[i]).ToArray(), Solid2Logic.ShellOffset(th)); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                throw ToolException.Invalid($"{p.Tool}: AutoCAD could not shell {h} with thickness {th} ({ex.ErrorStatus}). Nothing was changed. " +
                    "A wall thicker than a narrow part of the solid, or faces that cannot be opened together, are common causes: use a thinner wall or other faces.");
            }
            double after = VolumeOf(s);
            var ext = s.GeometricExtents;
            if (s.IsNull || !Solid2Logic.ShellLooksInward(before, after, t.Min, t.Max, J(ext.MinPoint), J(ext.MaxPoint), th))
                throw ToolException.Invalid($"{p.Tool}: AutoCAD did not hollow {h} inward (volume {Acad.R(before)} → {Acad.R(after)} mm³, or the outer size changed). Nothing was changed. Use a thinner wall or other faces.");
            var res = ChangedResult(h, s, before);
            res["faces_opened"] = open.Count;
            return res;
        }

        // ================================================================= slice

        private static PlaneDef PlaneOf(Params p) =>
            Solid2Logic.Plane(p.Has("plane_points") ? p.Points("plane_points", 3, 3) : null, p.PointOpt("point"), p.PointOpt("normal"), p.Has("plane") ? p.Str("plane") : null, p.Tool);

        private static Plane AcPlane(PlaneDef pl) => new Plane(P(pl.Point), Acad.V3(pl.Normal));

        /// <summary>
        /// Slices s with the plane. keep "positive" / "negative": s keeps that side; "both": s keeps the positive side and
        /// the negative half is returned (not in the drawing yet). Also used by the self-test.
        /// </summary>
        internal static Solid3d SliceSolid(Solid3d s, PlaneDef pl, string keep, string tool)
        {
            var ext = s.GeometricExtents;
            if (!Solid2Logic.PlaneCutsBox(pl, J(ext.MinPoint), J(ext.MaxPoint)))
                throw ToolException.Invalid($"{tool}: the plane does not pass through the solid (its bounding box lies on one side). Nothing was changed.");
            double before = VolumeOf(s);
            Solid3d other = null;
            try
            {
                if (keep == "both")
                {
                    using (var plane = AcPlane(pl)) other = s.Slice(plane, true);
                }
                else
                {
                    var n = keep == "negative" ? SolidLogic.Scale(pl.Normal, -1) : pl.Normal;
                    using (var plane = AcPlane(new PlaneDef { Point = pl.Point, Normal = n })) s.Slice(plane);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                other?.Dispose();
                throw ToolException.Invalid($"{tool}: AutoCAD could not slice the solid with this plane ({ex.ErrorStatus}). Nothing was changed. A plane that only touches a face or an edge is a common cause: move it a little.");
            }
            double after = VolumeOf(s), otherV = other != null ? VolumeOf(other) : 0;
            bool cut = keep == "both" ? other != null && after > 1e-9 && otherV > 1e-9 : after > 1e-9 && after < before * (1 - 1e-9);
            if (!cut)
            {
                other?.Dispose();
                throw ToolException.Invalid($"{tool}: the plane does not cut the solid into two parts{(keep != "both" ? $" (or nothing is left on the {keep} side)" : "")}. Nothing was changed.");
            }
            return other;
        }

        private static object SliceSolidTool(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle").ToUpperInvariant();
            var pl = PlaneOf(p);
            var keep = Solid2Logic.SliceKeep(p.Has("keep") ? p.Str("keep") : null, p.Tool);
            var s = SolidForWrite(ctx, h, p.Tool);
            double before = VolumeOf(s);
            var other = SliceSolid(s, pl, keep, p.Tool);
            var handles = new List<object> { h };
            var pieces = new List<object>();
            var a = new Dictionary<string, object> { ["handle"] = h, ["side"] = keep == "both" ? "positive" : keep };
            AddSolidNumbers(a, s);
            pieces.Add(a);
            if (other != null)
            {
                other.SetPropertiesFrom(s);
                ObjectId id;
                try { id = Acad.Add(ctx, other, null, false, s.BlockId); }
                catch { if (other.ObjectId.IsNull) other.Dispose(); throw; }
                var nh = Acad.H(id);
                handles.Add(nh);
                var b = new Dictionary<string, object> { ["handle"] = nh, ["side"] = "negative" };
                AddSolidNumbers(b, other);
                pieces.Add(b);
            }
            return new Dictionary<string, object>
            {
                ["handle"] = h,
                ["handles"] = handles,
                ["created"] = handles.Skip(1).ToList(),
                ["pieces"] = pieces,
                ["volume_before"] = Acad.R(before),
            };
        }

        // ================================================================= sweep / loft

        /// <summary>Centre of a planar region (area centroid; the bounding box centre when AutoCAD cannot tell).</summary>
        private static Point3d RegionCentre(Region r)
        {
            try
            {
                using (var plane = r.GetPlane())
                {
                    var cs = plane.GetCoordinateSystem();
                    Point3d o = cs.Origin;
                    Vector3d xa = cs.Xaxis, ya = cs.Yaxis;
                    var ap = r.AreaProperties(ref o, ref xa, ref ya);
                    return o + xa * ap.Centroid.X + ya * ap.Centroid.Y;
                }
            }
            catch (Exception)
            {
                var e = r.GeometricExtents;
                return new Point3d((e.MinPoint.X + e.MaxPoint.X) / 2, (e.MinPoint.Y + e.MaxPoint.Y) / 2, (e.MinPoint.Z + e.MaxPoint.Z) / 2);
            }
        }

        /// <summary>
        /// The path of a sweep: path_handle (a curve of the drawing) or path_points (a polyline made here). A 3D
        /// polyline is appended to the drawing for the sweep and erased right after (Polyline3d vertices need a database).
        /// </summary>
        private static Curve SweepPath(ToolContext ctx, Params p, List<Entity> temp)
        {
            if (p.Has("path_handle") && p.Has("path_points")) throw ToolException.Invalid("sweep: pass either 'path_handle' or 'path_points', not both.");
            if (p.Has("path_handle"))
            {
                var h = p.Handle("path_handle");
                var e = Acad.GetEntity(ctx, h, false);
                if (!(e is Curve c)) throw ToolException.Invalid($"sweep: path {h} is a {Acad.TypeName(e)}; the path must be a line, arc, circle, polyline, 3D polyline, spline or helix.");
                return c;
            }
            if (!p.Has("path_points")) throw ToolException.Invalid("sweep: pass the path as 'path_handle' (an existing curve) or 'path_points' ([[x, y, z], ...]).");
            var pts = p.Points("path_points", 2, 5000);
            double z0 = SolidLogic.Z3(pts[0]);
            if (pts.All(q => Math.Abs(SolidLogic.Z3(q) - z0) < 1e-9))
                return AddTemp(ctx, p, Acad.MakePolyline(pts, false), temp);
            var col = new Point3dCollection();
            foreach (var q in pts) col.Add(P(q));
            return AddTemp(ctx, p, new Polyline3d(Poly3dType.SimplePoly, col, false), temp);
        }

        /// <summary>
        /// A helper curve (sweep path_points, loft sections_points) appended to the drawing for the API call and erased
        /// right after (DropTemp): CreateSweptSolid / CreateLoftedSolid then get curves like the user's own (database
        /// resident; a Polyline3d needs it for its vertices). It goes on the result's layer / space (p), so a locked
        /// current layer does not block a solid drawn on another layer.
        /// </summary>
        private static T AddTemp<T>(ToolContext ctx, Params p, T e, List<Entity> temp) where T : Entity
        {
            try { Acad.Add(ctx, e, p); }
            catch { if (e.ObjectId.IsNull) e.Dispose(); throw; }
            temp.Add(e);
            return e;
        }

        private static void DropTemp(List<Entity> temp)
        {
            foreach (var e in temp)
            {
                if (e.ObjectId.IsNull) e.Dispose();
                else if (!e.IsErased) e.Erase();
            }
        }

        private static object Sweep(ToolContext ctx, Params p)
        {
            double twist = p.Num("twist", 0), scale = p.Num("scale", 1);
            Solid2Logic.CheckSweep(twist, scale);
            bool align = p.Bool("align", true), bank = p.Bool("bank", false);
            var temp = new List<Entity>();
            var ids = new List<ObjectId>();
            var solids = new List<Solid3d>();
            List<Region> regions = null;
            List<ObjectId> sources;
            try
            {
                var path = SweepPath(ctx, p, temp);
                regions = ProfileRegions(ctx, p, out sources, out _);
                foreach (var reg in regions)
                {
                    var b = new SweepOptionsBuilder
                    {
                        Align = align ? SweepOptionsAlignOption.AlignSweepEntityToPath : SweepOptionsAlignOption.NoAlignment,
                        BasePoint = RegionCentre(reg),
                        Bank = bank,
                        TwistAngle = Geometry.DegToRad(twist),
                        ScaleFactor = scale,
                        CheckIntersections = true,
                    };
                    var s = new Solid3d();
                    try { s.CreateSweptSolid(reg, path, b.ToSweepOptions()); }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        s.Dispose();
                        throw ToolException.Invalid($"sweep: AutoCAD could not sweep the profile along the path ({ex.ErrorStatus}). Nothing was changed. " +
                            "A profile wider than a tight bend of the path (the solid would cut itself), a path that crosses itself, or a profile in the plane of the path are common causes.");
                    }
                    if (s.IsNull || VolumeOf(s) <= 1e-9) { s.Dispose(); throw ToolException.Invalid("sweep: the sweep made no solid (an open profile makes a surface, not a solid). Nothing was changed. Use a closed profile."); }
                    ids.Add(AddSolid(ctx, s, p));
                    solids.Add(s);
                }
            }
            finally
            {
                if (regions != null) foreach (var r in regions) r.Dispose();
                DropTemp(temp);
            }
            EraseProfiles(ctx, p, sources);
            return SolidsResult(ids, solids);
        }

        /// <summary>The sections of a loft: section_handles (closed curves, in order) or sections_points (closed polygons made here).</summary>
        private static List<Entity> LoftSections(ToolContext ctx, Params p, List<Entity> temp, List<ObjectId> sources)
        {
            if (p.Has("section_handles") && p.Has("sections_points")) throw ToolException.Invalid("loft: pass either 'section_handles' or 'sections_points', not both.");
            var list = new List<Entity>();
            if (p.Has("section_handles"))
            {
                var hs = p.Handles("section_handles", Solid2Logic.MaxLoftSections);
                if (hs.Count < Solid2Logic.MinLoftSections) throw ToolException.Invalid($"loft: needs at least {Solid2Logic.MinLoftSections} sections.");
                foreach (var h in hs)
                {
                    var e = Acad.GetEntity(ctx, h, false);
                    if (!(e is Curve c) || !c.Closed)
                        throw ToolException.Invalid($"loft: section {h} is {(e is Curve ? "an open curve" : "a " + Acad.TypeName(e))}; a solid loft needs closed curves (closed polyline, circle, ellipse, closed spline).");
                    list.Add(c);
                    sources.Add(e.ObjectId);
                }
                return list;
            }
            if (!p.Has("sections_points")) throw ToolException.Invalid("loft: pass the sections as 'section_handles' (closed curves, bottom to top) or 'sections_points' (closed polygons).");
            var arr = p.Array("sections_points");
            if (arr.Count < Solid2Logic.MinLoftSections || arr.Count > Solid2Logic.MaxLoftSections)
                throw ToolException.Invalid($"loft: needs {Solid2Logic.MinLoftSections} to {Solid2Logic.MaxLoftSections} sections.");
            for (int i = 0; i < arr.Count; i++)
            {
                var sp = new Params("loft", new Dictionary<string, object> { ["section"] = arr[i] });
                var pts = sp.Points("section", 3, 5000);
                double z0 = SolidLogic.Z3(pts[0]);
                if (pts.Any(q => Math.Abs(SolidLogic.Z3(q) - z0) > 1e-6))
                    throw ToolException.Invalid($"loft: sections_points[{i}] is not flat: every point of a section needs the same z (sections are horizontal; draw tilted sections first and use section_handles).");
                list.Add(AddTemp(ctx, p, Acad.MakePolyline(pts, true), temp));
            }
            return list;
        }

        private static object Loft(ToolContext ctx, Params p)
        {
            var temp = new List<Entity>();
            var sources = new List<ObjectId>();
            Solid3d s = null;
            try
            {
                var sections = LoftSections(ctx, p, temp, sources);
                var guides = new List<Entity>();
                if (p.Has("guide_handles"))
                    foreach (var h in p.Handles("guide_handles", 50))
                    {
                        var e = Acad.GetEntity(ctx, h, false);
                        if (!(e is Curve)) throw ToolException.Invalid($"loft: guide {h} is a {Acad.TypeName(e)}, not a curve.");
                        guides.Add(e);
                    }
                Entity path = null;
                if (p.Has("path_handle"))
                {
                    if (guides.Count > 0) throw ToolException.Invalid("loft: use either guide_handles or path_handle, not both.");
                    var e = Acad.GetEntity(ctx, p.Handle("path_handle"), false);
                    if (!(e is Curve)) throw ToolException.Invalid($"loft: path {p.Handle("path_handle")} is a {Acad.TypeName(e)}, not a curve.");
                    path = e;
                }
                bool closed = p.Bool("closed", false);
                if (closed && sections.Count < 3) throw ToolException.Invalid("loft: closed needs at least 3 sections.");
                var b = new LoftOptionsBuilder
                {
                    Ruled = p.Bool("ruled", false),
                    Closed = closed,
                    NoTwist = true,
                    AlignDirection = true,
                    Simplify = true,
                    ArcLengthParam = false,
                    NormalOption = LoftOptionsNormalOption.NoNormal,
                };
                s = new Solid3d();
                try { s.CreateLoftedSolid(sections.ToArray(), guides.ToArray(), path, b.ToLoftOptions()); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    throw ToolException.Invalid($"loft: AutoCAD could not loft these sections ({ex.ErrorStatus}). Nothing was changed. " +
                        "Sections that cross each other, are not in order (bottom to top), or would twist the solid are common causes; guides must touch every section.");
                }
                if (s.IsNull || VolumeOf(s) <= 1e-9) throw ToolException.Invalid("loft: the loft made no solid. Nothing was changed. Use closed sections.");
                var id = AddSolid(ctx, s, p);
                var made = s;
                s = null;
                EraseProfiles(ctx, p, sources);
                return SolidsResult(new List<ObjectId> { id }, new List<Solid3d> { made });
            }
            finally
            {
                if (s != null && s.ObjectId.IsNull) s.Dispose();
                DropTemp(temp);
            }
        }

        // ================================================================= transforms

        private static Matrix3d M(double[] rowMajor) => new Matrix3d(rowMajor);

        private static object Rotate3d(ToolContext ctx, Params p)
        {
            var a = p.Point("axis_start");
            var b = p.Point("axis_end");
            var (dir, ang) = Solid2Logic.RotationAxis(a, b, p.Num("angle"));
            var r = (Dictionary<string, object>)Transform(ctx, p, Matrix3d.Rotation(ang, Acad.V3(dir), P(a)));
            return r;
        }

        private static object Mirror3d(ToolContext ctx, Params p)
        {
            var pl = PlaneOf(p);
            bool keep = p.Bool("keep_original", true);
            var hs = p.Handles("handles", MaxModifyHandles);
            Matrix3d m;
            using (var plane = AcPlane(pl)) m = Matrix3d.Mirroring(plane);
            List<ObjectId> targets = keep ? CloneEntities(ctx, hs, Matrix3d.Identity) : hs.Select(h => Acad.IdFromHandle(ctx.Db, h)).ToList();
            bool mirrText = Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("MIRRTEXT")) != 0;
            foreach (var id in targets)
            {
                var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                if (!keep) Acad.EnsureLayerUnlocked(ctx, e, e.Handle.ToString());
                e.TransformBy(m);
                if (!mirrText && e is DBText t) { t.IsMirroredInX = false; t.IsMirroredInY = false; }
            }
            var r = Acad.Handles(targets);
            r["kept_original"] = keep;
            return r;
        }

        private static object AlignTool(ToolContext ctx, Params p)
        {
            var src = p.Points("source_points", 1, 3);
            var dst = p.Points("dest_points", 1, 3);
            bool scale = p.Bool("scale", false);
            if (scale && src.Count < 2) throw ToolException.Invalid("align: scale needs 2 or 3 point pairs.");
            var m = Solid2Logic.AlignMatrix(src, dst, scale);
            return Transform(ctx, p, CheckedMatrix(m, src, "align"));
        }

        /// <summary>
        /// Matrix3d(double[]) is documented only as "values in an array": check it maps the points as the row-major
        /// math (Solid2Logic.Apply) says before anything moves; the transposed reading is accepted, anything else refused.
        /// </summary>
        private static Matrix3d CheckedMatrix(double[] rowMajor, IList<double[]> pts, string tool)
        {
            bool Same(Matrix3d mx)
            {
                foreach (var q in pts)
                {
                    var want = Solid2Logic.Apply(rowMajor, q);
                    var got = P(q).TransformBy(mx);
                    double scale = Math.Max(1, SolidLogic.Len(want));
                    if (Math.Abs(got.X - want[0]) > 1e-6 * scale || Math.Abs(got.Y - want[1]) > 1e-6 * scale || Math.Abs(got.Z - want[2]) > 1e-6 * scale) return false;
                }
                return true;
            }
            var m = M(rowMajor);
            if (Same(m)) return m;
            var tr = new double[16];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) tr[j * 4 + i] = rowMajor[i * 4 + j];
            var mt = M(tr);
            if (Same(mt)) { Logger.Warn($"{tool}: Matrix3d(double[]) reads column-major; transposed"); return mt; }
            throw ToolException.Invalid($"{tool}: internal check of the transform failed; nothing was moved. Send the plugin log to the developer.");
        }

        // ================================================================= interference

        /// <summary>
        /// Overlap volume of two solids (0 = they only touch); null when AutoCAD could not compute the common volume
        /// (KT-24: that is no proof of "no clash"). The solids are not changed. Also used by the self-test.
        /// </summary>
        internal static double? OverlapVolume(Solid3d a, Solid3d b)
        {
            using (var ca = (Solid3d)a.Clone())
            using (var cb = (Solid3d)b.Clone())
            {
                try { ca.BooleanOperation(BooleanOperationType.BoolIntersect, cb); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }
                if (ca.IsNull) return 0;
                try { return ca.MassProperties.Volume; }
                catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }
            }
        }

        private static object CheckInterferenceTool(ToolContext ctx, Params p)
        {
            var hs = p.Handles("handles", Solid2Logic.MaxInterferenceSolids).Select(x => x.ToUpperInvariant()).Distinct().ToList();
            var others = p.Has("other_handles") ? p.Handles("other_handles", Solid2Logic.MaxInterferenceSolids).Select(x => x.ToUpperInvariant()).Distinct().ToList() : null;
            if (others == null && hs.Count < 2) throw ToolException.Invalid("check_interference: needs at least 2 solids in 'handles' (or 'other_handles' to check against).");
            var solids = new Dictionary<string, Solid3d>(StringComparer.OrdinalIgnoreCase);
            var boxes = new Dictionary<string, Extents3d?>(StringComparer.OrdinalIgnoreCase);
            Solid3d Get(string h)
            {
                if (solids.TryGetValue(h, out var s)) return s;
                var e = Acad.GetEntity(ctx, h, false);
                if (!(e is Solid3d sd)) throw ToolException.Invalid($"check_interference: {h} is a {Acad.TypeName(e)}, not a 3D solid.");
                solids[h] = sd;
                boxes[h] = EntityInfo.Extents(sd);
                return sd;
            }
            var pairs = Solid2Logic.InterferencePairs(hs, others);
            // KT-25: the bounding-box test is cheap; only the pairs that pass it cost AutoCAD real work, and they are capped
            var candidates = new List<(string a, string b)>();
            foreach (var (a, b) in pairs)
            {
                Get(a); Get(b);
                var ea = boxes[a]; var eb = boxes[b];
                if (ea.HasValue && eb.HasValue && Disjoint(ea.Value, eb.Value)) continue;
                candidates.Add((a, b));
            }
            var hits = new List<object>();
            var clashRows = new List<Solid3Logic.ClashRow>();
            var unconfirmedPairs = new List<object>();
            int touching = 0, clashes = 0, measured = 0, unmeasured = 0, unconfirmed = 0;
            double total = 0;
            int compared = Math.Min(candidates.Count, Solid2Logic.MaxInterferenceCandidates);
            for (int i = 0; i < compared; i++)
            {
                var (a, b) = candidates[i];
                var sa = solids[a]; var sb = solids[b];
                bool hit;
                try { hit = sa.CheckInterference(sb); }
                catch (Autodesk.AutoCAD.Runtime.Exception ex) { throw ToolException.Invalid($"check_interference: AutoCAD could not compare {a} and {b} ({ex.ErrorStatus})."); }
                if (!hit) continue;
                // the overlap volume is measured for the first MaxInterferencePairs flagged pairs only (two copies and a
                // boolean each). KT-40: a pair past that limit is neither a clash nor safe: it is "unconfirmed" (it may only touch).
                bool tried = measured < Solid2Logic.MaxInterferencePairs;
                double? v = tried ? OverlapVolume(sa, sb) : (double?)null;
                if (tried) measured++;
                var kind = Solid2Logic.InterferenceKind(tried, v);
                if (kind == "touching") { touching++; continue; }
                if (kind == "unconfirmed")
                {
                    unconfirmed++;
                    if (unconfirmedPairs.Count < Solid2Logic.MaxInterferencePairs)
                        unconfirmedPairs.Add(new Dictionary<string, object> { ["a"] = a, ["b"] = b });
                    continue;
                }
                clashes++;
                if (kind == "clash") total += v.Value;
                else unmeasured++;
                if (hits.Count < Solid2Logic.MaxInterferencePairs)
                {
                    var row = new Dictionary<string, object> { ["a"] = a, ["b"] = b, ["layer_a"] = sa.Layer, ["layer_b"] = sb.Layer };
                    clashRows.Add(new Solid3Logic.ClashRow { A = a, LayerA = sa.Layer, B = b, LayerB = sb.Layer, Volume = kind == "clash" ? v : null });
                    if (kind == "clash") { row["volume"] = Acad.R(v.Value); row["volume_m3"] = Math.Round(SolidLogic.ToCubicMetres(v.Value), 6); }
                    else
                    {
                        row["volume"] = null;
                        row["note"] = "AutoCAD reports a clash but could not measure the common volume (often faces that coincide): treat it as a clash.";
                    }
                    hits.Add(row);
                }
            }
            var r = new Dictionary<string, object>
            {
                ["interfere"] = clashes > 0,
                ["pairs_checked"] = pairs.Count - candidates.Count + compared,
                ["clashes"] = hits,
                ["clash_count"] = clashes,
                ["total_overlap_m3"] = Math.Round(SolidLogic.ToCubicMetres(total), 6),
            };
            if (touching > 0) r["touching_pairs"] = touching;
            if (unmeasured > 0) r["unmeasured_clashes"] = unmeasured;
            if (unconfirmed > 0)
            {
                r["unconfirmed_count"] = unconfirmed;
                r["unconfirmed_pairs"] = unconfirmedPairs;
                r["unconfirmed_note"] = Solid2Logic.InterferenceUnconfirmedNote(unconfirmed);
            }
            if (clashes > hits.Count) r["note"] = $"only the first {Solid2Logic.MaxInterferencePairs} clashes are listed";
            if (candidates.Count > compared)
            {
                r["truncated"] = true;
                r["truncated_note"] = Solid2Logic.InterferenceTruncatedNote(candidates.Count, compared);
            }
            if (p.Has("table_position"))
            {
                if (clashRows.Count == 0) r["table_note"] = "No clash: no table was inserted.";
                else
                {
                    int dec = p.Int("decimals", 3, 0, 6);
                    var t = InsertSchedule(ctx, p, Solid3Logic.ClashTable(clashRows, dec), new[] { 5.0, 9, 14, 9, 14, 13 }, "BẢNG VA CHẠM KHỐI 3D");
                    r["handles"] = t["handles"];
                    if (clashes > clashRows.Count) r["table_note"] = $"The table lists the first {clashRows.Count} of {clashes} clashes.";
                }
            }
            return r;
        }

        private static bool Disjoint(Extents3d a, Extents3d b) =>
            a.MaxPoint.X < b.MinPoint.X || b.MaxPoint.X < a.MinPoint.X ||
            a.MaxPoint.Y < b.MinPoint.Y || b.MaxPoint.Y < a.MinPoint.Y ||
            a.MaxPoint.Z < b.MinPoint.Z || b.MaxPoint.Z < a.MinPoint.Z;

        // ================================================================= UCS

        private static Dictionary<string, object> UcsResult(Matrix3d m)
        {
            var cs = m.CoordinateSystem3d;
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["origin"] = Acad.J3(cs.Origin),
                ["x_axis"] = new List<object> { Math.Round(cs.Xaxis.X, 6), Math.Round(cs.Xaxis.Y, 6), Math.Round(cs.Xaxis.Z, 6) },
                ["y_axis"] = new List<object> { Math.Round(cs.Yaxis.X, 6), Math.Round(cs.Yaxis.Y, 6), Math.Round(cs.Yaxis.Z, 6) },
                ["z_axis"] = new List<object> { Math.Round(cs.Zaxis.X, 6), Math.Round(cs.Zaxis.Y, 6), Math.Round(cs.Zaxis.Z, 6) },
                ["note"] = "Only the user's working plane changed. Every tool still takes and returns WCS coordinates.",
            };
        }

        private static object SetUcs(ToolContext ctx, Params p)
        {
            var doc = ctx.Doc;
            var ed = doc.Editor;
            int ways = (p.Has("preset") ? 1 : 0) + (p.Has("name") ? 1 : 0) + (p.Has("x_axis") || p.Has("y_axis") ? 1 : 0);
            if (ways > 1) throw ToolException.Invalid("set_ucs: pass one of preset, name or x_axis + y_axis (with an optional origin).");
            if (ways == 0 && !p.Has("origin")) throw ToolException.Invalid("set_ucs: pass preset (top, front, right…), name (a saved UCS), x_axis + y_axis, or just origin to move the current UCS.");
            Point3d origin;
            Vector3d x, y;
            var cur = ed.CurrentUserCoordinateSystem.CoordinateSystem3d;
            using (doc.LockDocument())
            {
                if (p.Has("name"))
                {
                    var name = Solid2Logic.UcsName(p.Str("name"));
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var table = (UcsTable)tr.GetObject(doc.Database.UcsTableId, OpenMode.ForRead);
                        if (!table.Has(name))
                        {
                            var names = new List<string>();
                            foreach (ObjectId id in table) names.Add(((UcsTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
                            throw ToolException.Invalid($"set_ucs: this drawing has no saved UCS '{name}'. Saved UCSs: {(names.Count == 0 ? "none" : string.Join(", ", names.Take(30)))}.");
                        }
                        var rec = (UcsTableRecord)tr.GetObject(table[name], OpenMode.ForRead);
                        origin = rec.Origin; x = rec.XAxis; y = rec.YAxis;
                        tr.Commit();
                    }
                    if (p.Has("origin")) origin = P(p.Point("origin"));
                }
                else
                {
                    if (p.Has("preset"))
                    {
                        var (px, py) = Solid2Logic.UcsPreset(p.Str("preset"));
                        x = Acad.V3(px); y = Acad.V3(py);
                    }
                    else if (p.Has("x_axis") || p.Has("y_axis"))
                    {
                        if (!p.Has("x_axis") || !p.Has("y_axis")) throw ToolException.Invalid("set_ucs: x_axis and y_axis go together.");
                        var (ux, uy, _) = Solid2Logic.UcsAxes(p.Point("x_axis"), p.Point("y_axis"));
                        x = Acad.V3(ux); y = Acad.V3(uy);
                    }
                    else { x = cur.Xaxis; y = cur.Yaxis; }
                    origin = p.Has("origin") ? P(p.Point("origin")) : (p.Has("preset") ? Point3d.Origin : cur.Origin);
                }
                x = x.GetNormal();
                y = y.GetNormal();
                var z = x.CrossProduct(y).GetNormal();
                var m = Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, origin, x, y, z);
                string saved = null;
                if (p.Has("save_as"))
                {
                    saved = Solid2Logic.UcsName(p.Str("save_as"));
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var table = (UcsTable)tr.GetObject(doc.Database.UcsTableId, OpenMode.ForRead);
                        if (table.Has(saved)) throw ToolException.Invalid($"set_ucs: a UCS named '{saved}' is already saved in this drawing; pick another save_as name (set_ucs with name restores it).");
                        table.UpgradeOpen();
                        var rec = new UcsTableRecord { Name = saved, Origin = origin, XAxis = x, YAxis = y };
                        table.Add(rec);
                        tr.AddNewlyCreatedDBObject(rec, true);
                        tr.Commit();
                    }
                }
                ed.CurrentUserCoordinateSystem = m;
                if (p.Bool("plan_view", false) && InModelSpace())
                {
                    var ext = Capture.ModelExtents(doc);
                    var view = SolidLogic.Custom(new[] { z.X, z.Y, z.Z }, "set_ucs");
                    // KT-26: turn the view so the UCS x axis points right on screen. The presets keep twist 0 (already right).
                    double twist = 0;
                    if (!p.Has("preset"))
                    {
                        var (right, up) = SolidLogic.ScreenAxes(view);
                        twist = Solid2Logic.PlanTwist(new[] { x.X, x.Y, x.Z }, right, up);
                    }
                    ApplyView3d(doc, view, ext?.MinPoint, ext?.MaxPoint, twist);
                }
                var r = UcsResult(m);
                if (saved != null) r["saved_as"] = saved;
                return r;
            }
        }

        private static object ResetUcs(ToolContext ctx, Params p)
        {
            var doc = ctx.Doc;
            var ed = doc.Editor;
            var before = ed.CurrentUserCoordinateSystem.CoordinateSystem3d;
            using (doc.LockDocument()) ed.CurrentUserCoordinateSystem = Matrix3d.Identity;
            var r = UcsResult(Matrix3d.Identity);
            r["previous_origin"] = Acad.J3(before.Origin);
            r["note"] = "The UCS is World again (like the UCS command's World option).";
            return r;
        }
    }
}
