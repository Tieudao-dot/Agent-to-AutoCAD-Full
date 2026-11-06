using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Phase 2 — geometrically exact trim / extend and dynamic-block extras.</summary>
    public static partial class DrawingService
    {
        private static void RegisterTrimExtend(ToolDispatcher d)
        {
            d.Register("trim_entities", true, TrimEntities);
            d.Register("extend_entities", true, ExtendEntities);
            d.Register("get_dynamic_props", false, (ctx, p) =>
            {
                var br = GetBlockRef(ctx, p.Handle("handle"), false);
                if (!br.IsDynamicBlock) throw ToolException.Invalid($"{p.Handle("handle")} is not a dynamic block reference.");
                return new Dictionary<string, object> { ["name"] = EntityInfo.BlockName(ctx, br), ["properties"] = DynamicProps(br) };
            });
            d.Register("reset_dynamic_block", true, (ctx, p) =>
            {
                var hs = p.Handles("handles", MaxModifyHandles);
                foreach (var h in hs)
                {
                    var br = GetBlockRef(ctx, h, true);
                    if (br.IsDynamicBlock) br.ResetBlock();
                }
                return new Dictionary<string, object> { ["handles"] = hs.Cast<object>().ToList() };
            });
        }

        private static List<Entity> Boundaries(ToolContext ctx, Params p, string param, ISet<string> exclude)
        {
            var list = new List<Entity>();
            foreach (var h in p.Handles(param, MaxModifyHandles))
            {
                if (exclude.Contains(h)) continue;
                list.Add(Acad.GetEntity(ctx, h, false));
            }
            if (list.Count == 0) throw ToolException.Invalid($"'{param}' must contain at least one entity other than the ones being changed.");
            return list;
        }

        /// <summary>
        /// TRIM: each target curve is split at its intersections with the cutting edges and
        /// the piece nearest to its pick point is removed (like picking that piece in TRIM).
        /// </summary>
        private static object TrimEntities(ToolContext ctx, Params p)
        {
            var targets = p.Handles("handles", 500);
            var picks = p.Points("pick_points", 1);
            if (picks.Count != 1 && picks.Count != targets.Count)
                throw ToolException.Invalid("trim_entities: give one pick point per handle (or a single one for all): a point ON the part to remove.");
            var exclude = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
            var cutters = Boundaries(ctx, p, "cutting_handles", exclude);

            var created = new List<ObjectId>();
            var report = new List<object>();
            for (int i = 0; i < targets.Count; i++)
            {
                var h = targets[i];
                var ent = Acad.GetEntity(ctx, h, true);
                if (!(ent is Curve c) || ent is Xline || ent is Ray)
                    throw ToolException.Invalid($"trim_entities: {Acad.TypeName(ent)} {h} cannot be trimmed (lines, arcs, circles, polylines, ellipses, splines).");
                var pick = Acad.P3(picks.Count == 1 ? picks[0] : picks[i]);

                var pts = new Point3dCollection();
                foreach (var cut in cutters) c.IntersectWith(cut, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                var prms = new List<double>();
                foreach (Point3d pt in pts)
                {
                    double prm;
                    try { prm = c.GetParameterAtPoint(c.GetClosestPointTo(pt, false)); } catch { continue; }
                    if (!c.Closed && (Math.Abs(prm - c.StartParam) < 1e-9 || Math.Abs(prm - c.EndParam) < 1e-9)) continue;
                    if (!prms.Any(x => Math.Abs(x - prm) < 1e-9)) prms.Add(prm);
                }
                prms.Sort();
                int need = c.Closed ? 2 : 1;
                if (prms.Count < need)
                {
                    report.Add(new Dictionary<string, object> { ["handle"] = h, ["trimmed"] = false, ["reason"] = c.Closed ? "a closed curve needs at least 2 intersections with the cutting edges" : "no intersection with the cutting edges" });
                    continue;
                }

                var pieces = c.GetSplitCurves(new DoubleCollection(prms.ToArray()));
                Curve remove = null;
                double best = double.MaxValue;
                foreach (DBObject o in pieces)
                {
                    var pc = (Curve)o;
                    double dd = pc.GetClosestPointTo(pick, false).DistanceTo(pick);
                    if (dd < best) { best = dd; remove = pc; }
                }
                var kept = new List<object>();
                foreach (DBObject o in pieces)
                {
                    var pc = (Curve)o;
                    if (ReferenceEquals(pc, remove)) { pc.Dispose(); continue; }
                    var space = (BlockTableRecord)ctx.Tr.GetObject(c.OwnerId, OpenMode.ForWrite);
                    pc.SetPropertiesFrom(c);
                    var nid = space.AppendEntity(pc);
                    ctx.Tr.AddNewlyCreatedDBObject(pc, true);
                    created.Add(nid);
                    kept.Add(Acad.H(nid));
                }
                c.Erase();
                report.Add(new Dictionary<string, object> { ["handle"] = h, ["trimmed"] = true, ["new_handles"] = kept });
            }
            var r = Acad.Handles(created);
            r["results"] = report;
            r["note"] = "Trimmed entities are replaced by new pieces with new handles (originals erased).";
            return r;
        }

        /// <summary>EXTEND: lengthens an open curve end to the nearest boundary it would reach.</summary>
        private static object ExtendEntities(ToolContext ctx, Params p)
        {
            var targets = p.Handles("handles", 500);
            var exclude = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
            var bounds = Boundaries(ctx, p, "boundary_handles", exclude);
            var picks = p.Has("pick_points") ? p.Points("pick_points", 1) : null;
            string which = p.Str("end", "auto").ToLowerInvariant();
            if (which != "auto" && which != "start" && which != "end" && which != "both")
                throw ToolException.Invalid("extend_entities: 'end' must be auto, start, end or both.");

            var report = new List<object>();
            var changed = new List<ObjectId>();
            for (int i = 0; i < targets.Count; i++)
            {
                var h = targets[i];
                var ent = Acad.GetEntity(ctx, h, true);
                if (!(ent is Curve c) || c.Closed || ent is Xline || ent is Ray)
                    throw ToolException.Invalid($"extend_entities: {Acad.TypeName(ent)} {h} cannot be extended (open lines, arcs, polylines).");

                var pts = new Point3dCollection();
                foreach (var b in bounds) c.IntersectWith(b, Intersect.ExtendThis, pts, IntPtr.Zero, IntPtr.Zero);
                var cands = pts.Cast<Point3d>().Select(pt => new[] { pt.X, pt.Y, pt.Z }).ToList();
                var s = new[] { c.StartPoint.X, c.StartPoint.Y, c.StartPoint.Z };
                var e = new[] { c.EndPoint.X, c.EndPoint.Y, c.EndPoint.Z };
                bool OnCurve(double[] q) => c.GetClosestPointTo(Acad.P3(q), false).DistanceTo(Acad.P3(q)) < 1e-6;

                var ends = new List<bool>(); // true = start
                if (which == "start") ends.Add(true);
                else if (which == "end") ends.Add(false);
                else if (which == "both") { ends.Add(true); ends.Add(false); }
                else if (picks != null)
                {
                    var pk = picks.Count == 1 ? picks[0] : picks[Math.Min(i, picks.Count - 1)];
                    ends.Add(Geometry.Dist(pk, s) <= Geometry.Dist(pk, e));
                }

                var done = new List<object>();
                if (ends.Count == 0)
                {
                    // auto without a pick: extend whichever end reaches a boundary with the shorter extension
                    var ps = Geometry.ExtensionTarget(cands, s, e, OnCurve);
                    var pe = Geometry.ExtensionTarget(cands, e, s, OnCurve);
                    if (ps != null && (pe == null || Geometry.Dist(ps, s) <= Geometry.Dist(pe, e))) ends.Add(true);
                    else if (pe != null) ends.Add(false);
                }
                foreach (var atStart in ends)
                {
                    var target = atStart ? Geometry.ExtensionTarget(cands, s, e, OnCurve) : Geometry.ExtensionTarget(cands, e, s, OnCurve);
                    if (target == null) continue;
                    c.Extend(atStart, Acad.P3(target));
                    done.Add(new Dictionary<string, object> { ["end"] = atStart ? "start" : "end", ["to"] = Params.ToJsonPoint(target) });
                }
                if (done.Count > 0) changed.Add(c.ObjectId);
                report.Add(new Dictionary<string, object> { ["handle"] = h, ["extended"] = done.Count > 0, ["ends"] = done });
            }
            var r = Acad.Handles(changed);
            r["results"] = report;
            return r;
        }
    }
}
