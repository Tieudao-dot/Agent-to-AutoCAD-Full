using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Entity → JSON. <see cref="Summary"/> is the compact form used by list_entities
    /// (handle, type, layer, a few key numbers) so Claude's context does not overflow;
    /// <see cref="Detail"/> is the full geometry for get_entity.
    /// </summary>
    public static class EntityInfo
    {
        public const int TextPreview = 120;

        public static Dictionary<string, object> Summary(ToolContext ctx, Entity e)
        {
            var d = new Dictionary<string, object>
            {
                ["handle"] = e.Handle.ToString(),
                ["type"] = Acad.TypeName(e),
                ["layer"] = e.Layer,
            };
            if (!e.Color.IsByLayer) d["color"] = Acad.FromColor(e.Color);
            switch (e)
            {
                case Line l:
                    d["start"] = Acad.J(l.StartPoint);
                    d["end"] = Acad.J(l.EndPoint);
                    d["length"] = Acad.R(l.Length);
                    break;
                case Polyline pl:
                    d["vertices"] = pl.NumberOfVertices;
                    d["closed"] = pl.Closed;
                    d["length"] = Acad.R(pl.Length);
                    if (pl.Closed) d["area"] = Acad.R(pl.Area);
                    d["first"] = Acad.J(pl.GetPoint2dAt(0));
                    break;
                case Circle c:
                    d["center"] = Acad.J(c.Center);
                    d["radius"] = Acad.R(c.Radius);
                    break;
                case Arc a:
                    d["center"] = Acad.J(a.Center);
                    d["radius"] = Acad.R(a.Radius);
                    d["start_angle"] = Acad.Deg(a.StartAngle);
                    d["end_angle"] = Acad.Deg(a.EndAngle);
                    break;
                case Ellipse el:
                    d["center"] = Acad.J(el.Center);
                    d["major_radius"] = Acad.R(el.MajorRadius);
                    d["ratio"] = Acad.R(el.RadiusRatio);
                    break;
                case DBText t:
                    d["text"] = Params.Truncate(t.TextString, TextPreview);
                    d["height"] = Acad.R(t.Height);
                    d["position"] = Acad.J(t.Position);
                    break;
                case MText mt:
                    d["text"] = Params.Truncate(mt.Text, TextPreview);
                    d["height"] = Acad.R(mt.TextHeight);
                    d["position"] = Acad.J(mt.Location);
                    break;
                case Dimension dim:
                    d["dim_type"] = dim.GetType().Name;
                    d["measurement"] = Acad.R(SafeMeasurement(dim));
                    if (!string.IsNullOrEmpty(dim.DimensionText)) d["text_override"] = dim.DimensionText;
                    break;
                case Table tb:
                    d["rows"] = tb.Rows.Count;
                    d["cols"] = tb.Columns.Count;
                    d["position"] = Acad.J(tb.Position);
                    break;
                case BlockReference br:
                    d["name"] = BlockName(ctx, br);
                    d["position"] = Acad.J(br.Position);
                    d["rotation"] = Acad.Deg(br.Rotation);
                    var s = br.ScaleFactors;
                    if (Math.Abs(s.X - 1) > 1e-9 || Math.Abs(s.Y - 1) > 1e-9) d["scale"] = new List<object> { Acad.R(s.X), Acad.R(s.Y), Acad.R(s.Z) };
                    if (br.AttributeCollection.Count > 0) d["attributes"] = br.AttributeCollection.Count;
                    break;
                case Hatch h:
                    d["pattern"] = h.PatternName;
                    d["area"] = Acad.R(SafeArea(h));
                    break;
                case DBPoint pt:
                    d["position"] = Acad.J(pt.Position);
                    break;
                case Xline x:
                    d["base"] = Acad.J(x.BasePoint);
                    d["direction"] = Acad.J(x.UnitDir);
                    break;
                case Ray r:
                    d["base"] = Acad.J(r.BasePoint);
                    d["direction"] = Acad.J(r.UnitDir);
                    break;
                case MLeader ml:
                    if (ml.ContentType == ContentType.MTextContent && ml.MText != null) d["text"] = Params.Truncate(ml.MText.Text, TextPreview);
                    break;
                case Spline sp:
                    d["fit_points"] = sp.NumFitPoints;
                    d["closed"] = sp.Closed;
                    break;
            }
            return d;
        }

        public static Dictionary<string, object> Detail(ToolContext ctx, Entity e)
        {
            var d = Summary(ctx, e);
            d["color"] = Acad.FromColor(e.Color);
            d["linetype"] = e.Linetype;
            d["lineweight"] = (int)e.LineWeight < 0 ? (object)e.LineWeight.ToString() : (int)e.LineWeight / 100.0;
            var ext = Extents(e);
            if (ext.HasValue) d["bbox"] = new Dictionary<string, object> { ["min"] = Acad.J(ext.Value.MinPoint), ["max"] = Acad.J(ext.Value.MaxPoint) };

            switch (e)
            {
                case Polyline pl:
                    var verts = new List<object>();
                    var bulges = new List<object>();
                    bool anyBulge = false;
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        verts.Add(Acad.J(pl.GetPoint2dAt(i)));
                        double b = pl.GetBulgeAt(i);
                        bulges.Add(Acad.R(b));
                        if (Math.Abs(b) > 1e-12) anyBulge = true;
                    }
                    d["vertices"] = verts;
                    if (anyBulge) d["bulges"] = bulges;
                    d["elevation"] = Acad.R(pl.Elevation);
                    if (Math.Abs(pl.ConstantWidth) > 0) d["width"] = Acad.R(pl.ConstantWidth);
                    d.Remove("first");
                    break;
                case Arc a:
                    d["length"] = Acad.R(a.Length);
                    d["start"] = Acad.J(a.StartPoint);
                    d["end"] = Acad.J(a.EndPoint);
                    break;
                case Circle c:
                    d["area"] = Acad.R(c.Area);
                    d["circumference"] = Acad.R(c.Circumference);
                    break;
                case Ellipse el:
                    d["major_axis"] = Acad.J(el.MajorAxis);
                    d["start_angle"] = Acad.Deg(el.StartAngle);
                    d["end_angle"] = Acad.Deg(el.EndAngle);
                    break;
                case DBText t:
                    d["text"] = t.TextString;
                    d["rotation"] = Acad.Deg(t.Rotation);
                    d["style"] = StyleName(ctx, t.TextStyleId);
                    d["justify"] = t.Justify.ToString();
                    d["width_factor"] = Acad.R(t.WidthFactor);
                    break;
                case MText mt:
                    d["text"] = mt.Text;
                    d["contents"] = mt.Contents;
                    d["width"] = Acad.R(mt.Width);
                    d["rotation"] = Acad.Deg(mt.Rotation);
                    d["style"] = StyleName(ctx, mt.TextStyleId);
                    d["attachment"] = mt.Attachment.ToString();
                    break;
                case Dimension dim:
                    d["dim_style"] = DimStyleName(ctx, dim.DimensionStyle);
                    d["text_position"] = Acad.J(dim.TextPosition);
                    AddDimGeometry(d, dim);
                    break;
                case BlockReference br when !(br is Table):
                    var atts = new Dictionary<string, object>();
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        var ar = ctx.Tr.GetObject(aid, OpenMode.ForRead) as AttributeReference;
                        if (ar != null) atts[ar.Tag] = ar.IsMTextAttribute ? ar.MTextAttribute.Contents : ar.TextString;
                    }
                    if (atts.Count > 0) d["attributes"] = atts;
                    d["scale"] = new List<object> { Acad.R(br.ScaleFactors.X), Acad.R(br.ScaleFactors.Y), Acad.R(br.ScaleFactors.Z) };
                    if (br.IsDynamicBlock) d["dynamic"] = true;
                    break;
                case Spline sp:
                    var fps = new List<object>();
                    for (int i = 0; i < sp.NumFitPoints; i++) fps.Add(Acad.J(sp.GetFitPointAt(i)));
                    if (fps.Count > 0) d["fit_points"] = fps;
                    break;
                case Hatch h:
                    d["scale"] = Acad.R(h.PatternScale);
                    d["angle"] = Acad.Deg(h.PatternAngle);
                    d["associative"] = h.Associative;
                    d["loops"] = h.NumberOfLoops;
                    break;
                case Line l:
                    d["angle"] = Acad.Deg(l.Angle);
                    break;
            }
            return d;
        }

        private static void AddDimGeometry(Dictionary<string, object> d, Dimension dim)
        {
            switch (dim)
            {
                case RotatedDimension r:
                    d["p1"] = Acad.J(r.XLine1Point); d["p2"] = Acad.J(r.XLine2Point); d["dim_line_point"] = Acad.J(r.DimLinePoint);
                    d["rotation"] = Acad.Deg(r.Rotation);
                    break;
                case AlignedDimension a:
                    d["p1"] = Acad.J(a.XLine1Point); d["p2"] = Acad.J(a.XLine2Point); d["dim_line_point"] = Acad.J(a.DimLinePoint);
                    break;
                case RadialDimension rd:
                    d["center"] = Acad.J(rd.Center); d["chord_point"] = Acad.J(rd.ChordPoint);
                    break;
                case DiametricDimension dd:
                    d["chord_point"] = Acad.J(dd.ChordPoint); d["far_chord_point"] = Acad.J(dd.FarChordPoint);
                    break;
                case OrdinateDimension od:
                    d["feature_point"] = Acad.J(od.DefiningPoint); d["leader_end"] = Acad.J(od.LeaderEndPoint); d["use_x"] = od.UsingXAxis;
                    break;
                case Point3AngularDimension p3:
                    d["center"] = Acad.J(p3.CenterPoint); d["p1"] = Acad.J(p3.XLine1Point); d["p2"] = Acad.J(p3.XLine2Point);
                    break;
            }
        }

        public static double SafeMeasurement(Dimension d)
        {
            try { return d.Measurement; } catch { return double.NaN; }
        }

        public static double SafeArea(Hatch h)
        {
            try { return h.Area; } catch { return double.NaN; }
        }

        public static Extents3d? Extents(Entity e)
        {
            if (e is Xline || e is Ray) return null;
            try { return e.GeometricExtents; }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return null; }
        }

        public static string BlockName(ToolContext ctx, BlockReference br)
        {
            try
            {
                var id = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                return ((BlockTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead)).Name;
            }
            catch { return br.Name; }
        }

        public static string StyleName(ToolContext ctx, ObjectId id)
        {
            try { return ((TextStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead)).Name; } catch { return ""; }
        }

        public static string DimStyleName(ToolContext ctx, ObjectId id)
        {
            try { return ((DimStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead)).Name; } catch { return ""; }
        }

        /// <summary>Effective ACI color (ByLayer resolved through the layer), -1 if not ACI.</summary>
        public static int EffectiveAci(ToolContext ctx, Entity e)
        {
            var c = e.Color;
            if (c.IsByLayer)
            {
                var ltr = (LayerTableRecord)ctx.Tr.GetObject(e.LayerId, OpenMode.ForRead);
                c = ltr.Color;
            }
            return c.IsByAci ? c.ColorIndex : -1;
        }
    }
}
