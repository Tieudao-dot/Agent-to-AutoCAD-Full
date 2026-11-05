using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.2 — text, dimensions, multileaders, tables.</summary>
    public static partial class DrawingService
    {
        private static void RegisterAnnotation(ToolDispatcher d)
        {
            d.Register("add_text", true, AddText);
            d.Register("add_mtext", true, AddMText);
            d.Register("add_dim_linear", true, AddDimLinear);
            d.Register("add_dim_aligned", true, AddDimAligned);
            d.Register("add_dim_angular", true, AddDimAngular);
            d.Register("add_dim_radial", true, (ctx, p) => AddDimRadial(ctx, p, false));
            d.Register("add_dim_diameter", true, (ctx, p) => AddDimRadial(ctx, p, true));
            d.Register("add_dim_ordinate", true, AddDimOrdinate);
            d.Register("add_dim_continue", true, (ctx, p) => AddDimChain(ctx, p, false));
            d.Register("add_dim_baseline", true, (ctx, p) => AddDimChain(ctx, p, true));
            d.Register("add_mleader", true, AddMLeader);
            d.Register("add_table", true, AddTable);
        }

        // ------------------------------------------------------------------ text

        private static readonly Dictionary<string, AttachmentPoint> Justify = new Dictionary<string, AttachmentPoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["left"] = AttachmentPoint.BaseLeft, ["center"] = AttachmentPoint.BaseCenter, ["right"] = AttachmentPoint.BaseRight,
            ["base_left"] = AttachmentPoint.BaseLeft, ["base_center"] = AttachmentPoint.BaseCenter, ["base_right"] = AttachmentPoint.BaseRight,
            ["middle"] = AttachmentPoint.MiddleCenter, ["middle_center"] = AttachmentPoint.MiddleCenter,
            ["middle_left"] = AttachmentPoint.MiddleLeft, ["middle_right"] = AttachmentPoint.MiddleRight,
            ["top_left"] = AttachmentPoint.TopLeft, ["top_center"] = AttachmentPoint.TopCenter, ["top_right"] = AttachmentPoint.TopRight,
            ["bottom_left"] = AttachmentPoint.BottomLeft, ["bottom_center"] = AttachmentPoint.BottomCenter, ["bottom_right"] = AttachmentPoint.BottomRight,
            ["tl"] = AttachmentPoint.TopLeft, ["tc"] = AttachmentPoint.TopCenter, ["tr"] = AttachmentPoint.TopRight,
            ["ml"] = AttachmentPoint.MiddleLeft, ["mc"] = AttachmentPoint.MiddleCenter, ["mr"] = AttachmentPoint.MiddleRight,
            ["bl"] = AttachmentPoint.BottomLeft, ["bc"] = AttachmentPoint.BottomCenter, ["br"] = AttachmentPoint.BottomRight,
        };

        private static AttachmentPoint ParseJustify(string s, AttachmentPoint def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            if (Justify.TryGetValue(s.Trim().Replace('-', '_').Replace(' ', '_'), out var a)) return a;
            throw ToolException.Invalid($"Unknown justify '{s}'. Use left, center, right, middle_center, top_left, top_center, top_right, middle_left, middle_right, bottom_left, bottom_center, bottom_right.");
        }

        private static double DefaultTextHeight(ToolContext ctx, ObjectId styleId)
        {
            var st = (TextStyleTableRecord)ctx.Tr.GetObject(styleId, OpenMode.ForRead);
            return st.TextSize > 0 ? st.TextSize : ctx.Db.Textsize;
        }

        private static object AddText(ToolContext ctx, Params p)
        {
            var pos = Acad.P3(p.Point("position"));
            string text = p.Str("text");
            if (text.Contains("\n")) throw ToolException.Invalid("add_text: single-line TEXT cannot contain line breaks; use add_mtext.");
            var styleName = p.Str("style", null);
            var styleId = string.IsNullOrEmpty(styleName) ? ctx.Db.Textstyle : TextStyleId(ctx, styleName);
            var t = new DBText
            {
                TextString = text,
                TextStyleId = styleId,
            };
            t.SetDatabaseDefaults(ctx.Db);
            t.TextStyleId = styleId;
            t.Height = p.Has("height") ? p.Positive("height") : DefaultTextHeight(ctx, styleId);
            t.Rotation = Geometry.DegToRad(p.Num("rotation", 0));
            if (p.Has("width_factor")) t.WidthFactor = p.Positive("width_factor");
            var just = ParseJustify(p.Str("justify", null), AttachmentPoint.BaseLeft);
            t.Position = pos;
            if (just != AttachmentPoint.BaseLeft)
            {
                t.Justify = just;
                t.AlignmentPoint = pos;
            }
            var id = Acad.Add(ctx, t, p);
            if (just != AttachmentPoint.BaseLeft) t.AdjustAlignment(ctx.Db);
            return Acad.Handles(id);
        }

        private static object AddMText(ToolContext ctx, Params p)
        {
            var styleName = p.Str("style", null);
            var styleId = string.IsNullOrEmpty(styleName) ? ctx.Db.Textstyle : TextStyleId(ctx, styleName);
            var mt = new MText();
            mt.SetDatabaseDefaults(ctx.Db);
            mt.TextStyleId = styleId;
            mt.Location = Acad.P3(p.Point("position"));
            mt.TextHeight = p.Has("height") ? p.Positive("height") : DefaultTextHeight(ctx, styleId);
            mt.Width = p.Num("width", 0);
            mt.Attachment = ParseJustify(p.Str("justify", p.Str("attachment", null)), AttachmentPoint.TopLeft);
            mt.Rotation = Geometry.DegToRad(p.Num("rotation", 0));
            mt.Contents = p.Str("text").Replace("\r\n", "\\P").Replace("\n", "\\P");
            return Acad.Handles(Acad.Add(ctx, mt, p));
        }

        // ------------------------------------------------------------------ dimensions

        private static ObjectId DimStyleFor(ToolContext ctx, Params p)
        {
            var s = p.Str("dim_style", null);
            return string.IsNullOrEmpty(s) ? ctx.Db.Dimstyle : DimStyleId(ctx, s);
        }

        private static string DimText(Params p) => p.Str("text", "");

        private static ObjectId AddDim(ToolContext ctx, Dimension dim, Params p)
        {
            var id = Acad.Add(ctx, dim, p);
            dim.DimensionStyle = DimStyleFor(ctx, p);
            if (p.Has("text_height")) dim.Dimtxt = p.Positive("text_height");
            dim.RecomputeDimensionBlock(true);
            return id;
        }

        private static object DimResult(ObjectId id, Dimension dim)
        {
            var r = Acad.Handles(id);
            r["measurement"] = Acad.R(EntityInfo.SafeMeasurement(dim));
            return r;
        }

        private static object AddDimLinear(ToolContext ctx, Params p)
        {
            var p1 = p.Point("p1");
            var p2 = p.Point("p2");
            var dl = p.Point("dim_line_point");
            double rot;
            if (p.Has("rotation")) rot = Geometry.DegToRad(p.Num("rotation"));
            else
            {
                switch (p.Str("orientation", "auto").ToLowerInvariant())
                {
                    case "horizontal": rot = 0; break;
                    case "vertical": rot = Math.PI / 2; break;
                    case "auto":
                        // like DIMLINEAR: dim line above/below the points -> horizontal; left/right -> vertical
                        bool outsideY = dl[1] > Math.Max(p1[1], p2[1]) || dl[1] < Math.Min(p1[1], p2[1]);
                        bool outsideX = dl[0] > Math.Max(p1[0], p2[0]) || dl[0] < Math.Min(p1[0], p2[0]);
                        rot = !outsideY && outsideX ? Math.PI / 2 : 0;
                        break;
                    default: throw ToolException.Invalid("orientation must be auto, horizontal or vertical.");
                }
            }
            var dim = new RotatedDimension(rot, Acad.P3(p1), Acad.P3(p2), Acad.P3(dl), DimText(p), ObjectId.Null);
            return DimResult(AddDim(ctx, dim, p), dim);
        }

        private static object AddDimAligned(ToolContext ctx, Params p)
        {
            var dim = new AlignedDimension(Acad.P3(p.Point("p1")), Acad.P3(p.Point("p2")), Acad.P3(p.Point("dim_line_point")), DimText(p), ObjectId.Null);
            return DimResult(AddDim(ctx, dim, p), dim);
        }

        private static object AddDimAngular(ToolContext ctx, Params p)
        {
            Dimension dim;
            if (p.Has("line1_handle") || p.Has("line2_handle"))
            {
                var l1 = Acad.GetEntity(ctx, p.Handle("line1_handle"), false) as Line;
                var l2 = Acad.GetEntity(ctx, p.Handle("line2_handle"), false) as Line;
                if (l1 == null || l2 == null) throw ToolException.Invalid("add_dim_angular: line1_handle and line2_handle must be LINE entities.");
                var arc = p.PointOpt("arc_point");
                Point3d arcPt;
                if (arc != null) arcPt = Acad.P3(arc);
                else
                {
                    var c = Geometry.IntersectLines(D(l1.StartPoint), D(l1.EndPoint), D(l2.StartPoint), D(l2.EndPoint));
                    if (c == null) throw ToolException.Invalid("add_dim_angular: the lines are parallel.");
                    var m1 = l1.GetPointAtDist(l1.Length / 2);
                    var m2 = l2.GetPointAtDist(l2.Length / 2);
                    arcPt = new Point3d((m1.X + m2.X) / 2, (m1.Y + m2.Y) / 2, 0);
                }
                dim = new LineAngularDimension2(l1.StartPoint, l1.EndPoint, l2.StartPoint, l2.EndPoint, arcPt, DimText(p), ObjectId.Null);
            }
            else
            {
                var center = p.Point("center");
                var a = p.Point("p1");
                var b = p.Point("p2");
                var arc = p.PointOpt("arc_point");
                Point3d arcPt;
                if (arc != null) arcPt = Acad.P3(arc);
                else
                {
                    double r = 0.6 * Math.Min(Geometry.Dist(center, a), Geometry.Dist(center, b));
                    double ang1 = Math.Atan2(a[1] - center[1], a[0] - center[0]);
                    double sweep = Geometry.NormalizeRad(Math.Atan2(b[1] - center[1], b[0] - center[0]) - ang1);
                    double mid = ang1 + sweep / 2;
                    arcPt = new Point3d(center[0] + r * Math.Cos(mid), center[1] + r * Math.Sin(mid), 0);
                }
                dim = new Point3AngularDimension(Acad.P3(center), Acad.P3(a), Acad.P3(b), arcPt, DimText(p), ObjectId.Null);
            }
            return DimResult(AddDim(ctx, dim, p), dim);
        }

        private static object AddDimRadial(ToolContext ctx, Params p, bool diameter)
        {
            var h = p.Handle("entity_handle");
            var e = Acad.GetEntity(ctx, h, false);
            Point3d center;
            double radius;
            switch (e)
            {
                case Circle c: center = c.Center; radius = c.Radius; break;
                case Arc a: center = a.Center; radius = a.Radius; break;
                default: throw ToolException.Invalid($"{(diameter ? "add_dim_diameter" : "add_dim_radial")}: {h} must be a CIRCLE or ARC.");
            }
            double ang = Geometry.DegToRad(p.Num("angle", e is Arc arc ? Geometry.RadToDeg((arc.StartAngle + Geometry.NormalizeRad(arc.EndAngle - arc.StartAngle) / 2)) : 45));
            var chord = new Point3d(center.X + radius * Math.Cos(ang), center.Y + radius * Math.Sin(ang), center.Z);
            double leader = p.Num("leader_length", radius * 0.5);
            Dimension dim;
            if (diameter)
            {
                var far = new Point3d(center.X - radius * Math.Cos(ang), center.Y - radius * Math.Sin(ang), center.Z);
                dim = new DiametricDimension(chord, far, leader, DimText(p), ObjectId.Null);
            }
            else dim = new RadialDimension(center, chord, leader, DimText(p), ObjectId.Null);
            return DimResult(AddDim(ctx, dim, p), dim);
        }

        private static object AddDimOrdinate(ToolContext ctx, Params p)
        {
            var dim = new OrdinateDimension(p.Bool("use_x", true), Acad.P3(p.Point("feature_point")), Acad.P3(p.Point("leader_end")), DimText(p), ObjectId.Null);
            if (p.Has("origin")) dim.Origin = Acad.P3(p.Point("origin"));
            return DimResult(AddDim(ctx, dim, p), dim);
        }

        /// <summary>DIMCONTINUE / DIMBASELINE from an existing linear or aligned dimension.</summary>
        private static object AddDimChain(ToolContext ctx, Params p, bool baseline)
        {
            string tool = baseline ? "add_dim_baseline" : "add_dim_continue";
            var baseDim = Acad.GetEntity(ctx, p.Handle("base_dim_handle"), false);
            var pts = p.Points("points", 1, 500);
            Point3d x1, x2, dl;
            double rot;
            bool aligned;
            switch (baseDim)
            {
                case RotatedDimension r: x1 = r.XLine1Point; x2 = r.XLine2Point; dl = r.DimLinePoint; rot = r.Rotation; aligned = false; break;
                case AlignedDimension a:
                    x1 = a.XLine1Point; x2 = a.XLine2Point; dl = a.DimLinePoint; aligned = true;
                    rot = Math.Atan2(x2.Y - x1.Y, x2.X - x1.X);
                    break;
                default: throw ToolException.Invalid($"{tool}: base_dim_handle must be a linear (rotated) or aligned dimension.");
            }
            var styleId = ((Dimension)baseDim).DimensionStyle;
            var style = (DimStyleTableRecord)ctx.Tr.GetObject(styleId, OpenMode.ForRead);
            double spacing = p.Num("spacing", style.Dimdli * style.Dimscale);
            // normal of the dimension line, pointing from the measured points towards the dim line
            var dir = new Vector3d(Math.Cos(rot), Math.Sin(rot), 0);
            var nrm = new Vector3d(-dir.Y, dir.X, 0);
            if ((dl - x1).DotProduct(nrm) < 0) nrm = -nrm;

            var ids = new List<ObjectId>();
            Point3d prev = x2;
            var curDl = dl;
            foreach (var raw in pts)
            {
                var next = Acad.P3(raw);
                Point3d a1 = baseline ? x1 : prev;
                if (baseline) curDl = curDl + nrm * spacing;
                Dimension d;
                if (aligned) d = new AlignedDimension(a1, next, curDl, "", styleId);
                else d = new RotatedDimension(rot, a1, next, curDl, "", styleId);
                var id = Acad.Add(ctx, d, null);
                d.SetPropertiesFrom(baseDim);
                Acad.ApplyCommon(ctx, d, p);
                d.DimensionStyle = styleId;
                d.RecomputeDimensionBlock(true);
                ids.Add(id);
                prev = next;
            }
            return Acad.Handles(ids);
        }

        // ------------------------------------------------------------------ multileader / table

        private static object AddMLeader(ToolContext ctx, Params p)
        {
            var arrow = Acad.P3(p.Point("arrow_point"));
            var textPt = Acad.P3(p.Point("text_point"));
            var ml = new MLeader();
            ml.SetDatabaseDefaults(ctx.Db);
            var styleName = p.Str("style", null);
            if (!string.IsNullOrEmpty(styleName))
            {
                var dict = (DBDictionary)ctx.Tr.GetObject(ctx.Db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                if (!dict.Contains(styleName)) throw ToolException.Invalid($"Multileader style '{styleName}' does not exist.");
                ml.MLeaderStyle = dict.GetAt(styleName);
            }
            ml.ContentType = ContentType.MTextContent;
            var mt = new MText();
            mt.SetDatabaseDefaults(ctx.Db);
            mt.Contents = p.Str("text").Replace("\r\n", "\\P").Replace("\n", "\\P");
            mt.Location = textPt;
            var textStyle = p.Str("text_style", null);
            if (!string.IsNullOrEmpty(textStyle)) mt.TextStyleId = TextStyleId(ctx, textStyle);
            if (p.Has("height"))
            {
                mt.TextHeight = p.Positive("height");
                ml.TextHeight = mt.TextHeight;
            }
            ml.MText = mt;
            int idx = ml.AddLeaderLine(textPt);
            ml.AddFirstVertex(idx, arrow);
            if (p.Has("arrow_size")) ml.ArrowSize = p.Positive("arrow_size");
            return Acad.Handles(Acad.Add(ctx, ml, p));
        }

        private static object AddTable(ToolContext ctx, Params p)
        {
            var data = p.Has("data") ? p.Array("data") : new List<object>();
            int rows = p.Int("rows", data.Count, 1, 1000);
            int cols = p.Int("cols", data.Count > 0 && data[0] is List<object> r0 ? r0.Count : 1, 1, 100);
            var widths = p.NumbersOpt("col_widths");
            double rowHeight = p.Positive("row_height", 0 + (p.Has("text_height") ? p.Positive("text_height") * 2.2 : 800));
            string title = p.Str("title", null);
            int titleRows = string.IsNullOrEmpty(title) ? 0 : 1;

            var tb = new Table();
            tb.SetDatabaseDefaults(ctx.Db);
            tb.TableStyle = ctx.Db.Tablestyle;
            tb.Position = Acad.P3(p.Point("position"));
            tb.SetSize(rows + titleRows, cols);
            tb.SetRowHeight(rowHeight);
            for (int c = 0; c < cols; c++)
                tb.Columns[c].Width = widths != null && c < widths.Count ? widths[c] : (widths != null && widths.Count > 0 ? widths[widths.Count - 1] : 2000);

            double? th = p.Has("text_height") ? p.Positive("text_height") : (double?)null;
            if (titleRows == 1)
            {
                try { tb.MergeCells(CellRange.Create(tb, 0, 0, 0, cols - 1)); } catch { }
                tb.Cells[0, 0].TextString = title;
                if (th.HasValue) tb.Cells[0, 0].TextHeight = th.Value * 1.2;
            }
            else
            {
                // the Standard table style merges row 0 as a title: unmerge so every cell holds data
                try { tb.UnmergeCells(CellRange.Create(tb, 0, 0, 0, cols - 1)); } catch { }
            }
            for (int r = 0; r < rows; r++)
            {
                var row = r < data.Count ? data[r] as List<object> : null;
                for (int c = 0; c < cols; c++)
                {
                    var cell = tb.Cells[r + titleRows, c];
                    var v = row != null && c < row.Count ? row[c] : null;
                    cell.TextString = v == null ? "" : (v is double dd ? dd.ToString(System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                    if (th.HasValue) cell.TextHeight = th.Value;
                    if (p.Has("alignment")) cell.Alignment = ParseCellAlignment(p.Str("alignment"));
                }
            }
            var id = Acad.Add(ctx, tb, p);
            tb.GenerateLayout();
            var res = Acad.Handles(id);
            res["rows"] = rows + titleRows;
            res["cols"] = cols;
            return res;
        }

        private static CellAlignment ParseCellAlignment(string s)
        {
            switch (s.Trim().ToLowerInvariant().Replace('-', '_'))
            {
                case "left": case "middle_left": return CellAlignment.MiddleLeft;
                case "right": case "middle_right": return CellAlignment.MiddleRight;
                case "center": case "middle_center": return CellAlignment.MiddleCenter;
                case "top_left": return CellAlignment.TopLeft;
                case "top_center": return CellAlignment.TopCenter;
                default: throw ToolException.Invalid("table alignment must be left, center, right, top_left or top_center.");
            }
        }
    }
}
