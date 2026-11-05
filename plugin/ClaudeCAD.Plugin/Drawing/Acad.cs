using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>AutoCAD-side helpers shared by all tool handlers.</summary>
    public static class Acad
    {
        public static Point3d P3(double[] p) => new Point3d(p[0], p[1], p.Length > 2 ? p[2] : 0);
        public static Point2d P2(double[] p) => new Point2d(p[0], p[1]);
        public static Vector3d V3(double[] v) => new Vector3d(v[0], v[1], v.Length > 2 ? v[2] : 0);

        public static List<object> J(Point3d p) =>
            Math.Abs(p.Z) > 1e-9 ? new List<object> { R(p.X), R(p.Y), R(p.Z) } : new List<object> { R(p.X), R(p.Y) };

        public static List<object> J(Point2d p) => new List<object> { R(p.X), R(p.Y) };

        public static List<object> J(Vector3d v) => new List<object> { R(v.X), R(v.Y), R(v.Z) };

        /// <summary>Round to 1e-6 mm to keep payloads short.</summary>
        public static double R(double d) => Math.Round(d, 6);

        public static double Deg(double rad) => R(Geometry.RadToDeg(rad));

        public static string H(ObjectId id) => id.Handle.ToString();

        public static Dictionary<string, object> Handles(params ObjectId[] ids) =>
            new Dictionary<string, object> { ["handles"] = ids.Select(H).Cast<object>().ToList() };

        public static Dictionary<string, object> Handles(IEnumerable<ObjectId> ids) =>
            new Dictionary<string, object> { ["handles"] = ids.Select(H).Cast<object>().ToList() };

        // ---------------------------------------------------------------- lookup

        public static ObjectId IdFromHandle(Database db, string handle)
        {
            long value;
            try
            {
                value = Convert.ToInt64(handle, 16);
            }
            catch
            {
                throw ToolException.Invalid($"'{handle}' is not a valid handle (hex string).");
            }
            if (!db.TryGetObjectId(new Handle(value), out var id) || id.IsNull || id.IsErased)
                throw new ToolException(ErrorCodes.EntityNotFound,
                    $"No entity with handle {handle} (erased or never existed). Call list_entities to get current handles.");
            return id;
        }

        public static Entity GetEntity(ToolContext ctx, string handle, bool forWrite)
        {
            var id = IdFromHandle(ctx.Db, handle);
            var obj = ctx.Tr.GetObject(id, OpenMode.ForRead, false);
            if (!(obj is Entity ent))
                throw new ToolException(ErrorCodes.EntityNotFound, $"Handle {handle} is a {obj.GetType().Name}, not a drawing entity.");
            if (forWrite)
            {
                ctx.RequireWritable("open for write");
                EnsureLayerUnlocked(ctx, ent, handle);
                ent.UpgradeOpen();
            }
            return ent;
        }

        public static void EnsureLayerUnlocked(ToolContext ctx, Entity ent, string handle)
        {
            var ltr = (LayerTableRecord)ctx.Tr.GetObject(ent.LayerId, OpenMode.ForRead);
            if (ltr.IsLocked)
                throw new ToolException(ErrorCodes.Locked,
                    $"Entity {handle} is on locked layer '{ltr.Name}'. Ask the user before unlocking it with set_layer_props {{\"name\": \"{ltr.Name}\", \"locked\": false}}.");
        }

        public static ObjectId LayerId(ToolContext ctx, string name)
        {
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(name))
                throw new ToolException(ErrorCodes.LayerNotFound,
                    $"Layer '{name}' does not exist. Call create_layer first, or list_layers to see existing layers.");
            var id = lt[name];
            if (id.IsErased)
                throw new ToolException(ErrorCodes.LayerNotFound, $"Layer '{name}' was erased. Call create_layer to recreate it.");
            return id;
        }

        /// <summary>Returns the linetype id, loading it from acadiso.lin / acad.lin if missing.</summary>
        public static ObjectId EnsureLinetype(ToolContext ctx, string name)
        {
            var ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
            if (ltt.Has(name)) return ltt[name];
            if (name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) return ctx.Db.ByLayerLinetype;
            if (name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) return ctx.Db.ByBlockLinetype;
            ctx.RequireWritable("load linetype");
            foreach (var file in new[] { "acadiso.lin", "acad.lin" })
            {
                try
                {
                    ctx.Db.LoadLineTypeFile(name, file);
                    ltt = (LinetypeTable)ctx.Tr.GetObject(ctx.Db.LinetypeTableId, OpenMode.ForRead);
                    if (ltt.Has(name)) return ltt[name];
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // try next file
                }
            }
            throw ToolException.Invalid($"Linetype '{name}' is not in the drawing nor in acadiso.lin. Use a standard name such as CONTINUOUS, DASHED, HIDDEN, CENTER, DASHDOT, PHANTOM.");
        }

        public static Color ToColor(object c)
        {
            switch (c)
            {
                case "bylayer": return Color.FromColorIndex(ColorMethod.ByLayer, 256);
                case "byblock": return Color.FromColorIndex(ColorMethod.ByBlock, 0);
                case int i when i == 256: return Color.FromColorIndex(ColorMethod.ByLayer, 256);
                case int i when i == 0: return Color.FromColorIndex(ColorMethod.ByBlock, 0);
                case int i: return Color.FromColorIndex(ColorMethod.ByAci, (short)i);
                default: throw ToolException.Invalid("color must be ACI 1-255 or \"bylayer\".");
            }
        }

        public static object FromColor(Color c)
        {
            if (c.IsByLayer) return "bylayer";
            if (c.IsByBlock) return "byblock";
            if (c.IsByAci) return (int)c.ColorIndex;
            return $"rgb({c.Red},{c.Green},{c.Blue})";
        }

        /// <summary>Applies the common optional properties: layer, color, linetype, lineweight.</summary>
        public static void ApplyCommon(ToolContext ctx, Entity ent, Params p)
        {
            var layer = p.Str("layer", null);
            if (!string.IsNullOrEmpty(layer)) ent.LayerId = LayerId(ctx, layer);
            var color = p.ColorOpt("color");
            if (color != null) ent.Color = ToColor(color);
            var lt = p.Str("linetype", null);
            if (!string.IsNullOrEmpty(lt)) ent.LinetypeId = EnsureLinetype(ctx, lt);
            if (p.Has("lineweight")) ent.LineWeight = (LineWeight)Geometry.LineWeightFrom(p.Get("lineweight"));
            if (p.Has("linetype_scale")) ent.LinetypeScale = p.Positive("linetype_scale");
        }

        /// <summary>
        /// Appends a new entity with default + common properties.
        /// defaults=false: the caller already called SetDatabaseDefaults / SetPropertiesFrom
        /// (styles, heights, source properties must not be reset).
        /// owner: target block (e.g. the space of a source entity); default = model space
        /// or the paper space named by the "layout" parameter.
        /// </summary>
        public static ObjectId Add(ToolContext ctx, Entity ent, Params p, bool defaults = true, ObjectId owner = default)
        {
            ctx.RequireWritable("add entity");
            if (defaults) ent.SetDatabaseDefaults(ctx.Db);
            if (p != null) ApplyCommon(ctx, ent, p);
            var ltr = (LayerTableRecord)ctx.Tr.GetObject(ent.LayerId, OpenMode.ForRead);
            if (ltr.IsLocked)
                throw new ToolException(ErrorCodes.Locked, $"Layer '{ltr.Name}' is locked; cannot draw on it. Use another layer or ask the user to unlock it.");
            BlockTableRecord space;
            if (!owner.IsNull) space = (BlockTableRecord)ctx.Tr.GetObject(owner, OpenMode.ForWrite);
            else space = p != null && p.Has("layout") ? ctx.SpaceFor(p.Str("layout", null)) : ctx.ModelSpace;
            var id = space.AppendEntity(ent);
            ctx.Tr.AddNewlyCreatedDBObject(ent, true);
            return id;
        }

        public static Polyline MakePolyline(IList<double[]> verts, bool closed, IList<double> bulges = null, double width = 0)
        {
            var pl = new Polyline(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                double b = bulges != null && i < bulges.Count ? bulges[i] : 0;
                pl.AddVertexAt(i, P2(verts[i]), b, width, width);
            }
            pl.Closed = closed;
            if (verts.Count > 0 && verts[0].Length > 2) pl.Elevation = verts[0][2];
            return pl;
        }

        public static IEnumerable<ObjectId> ModelSpaceIds(ToolContext ctx)
        {
            foreach (ObjectId id in ctx.ModelSpace) yield return id;
        }

        public static string TypeName(Entity e)
        {
            // DXF-like names Claude can filter on: LINE, LWPOLYLINE, CIRCLE, ARC, TEXT, MTEXT, INSERT, DIMENSION...
            switch (e)
            {
                case Line _: return "LINE";
                case Polyline _: return "LWPOLYLINE";
                case Polyline2d _: return "POLYLINE2D";
                case Polyline3d _: return "POLYLINE3D";
                case Circle _: return "CIRCLE";
                case Arc _: return "ARC";
                case Ellipse _: return "ELLIPSE";
                case Spline _: return "SPLINE";
                case DBPoint _: return "POINT";
                case Xline _: return "XLINE";
                case Ray _: return "RAY";
                case Hatch _: return "HATCH";
                case Wipeout _: return "WIPEOUT";
                case DBText _: return "TEXT";
                case MText _: return "MTEXT";
                case Table _: return "TABLE"; // Table derives from BlockReference
                case BlockReference _: return "INSERT";
                case Dimension _: return "DIMENSION";
                case MLeader _: return "MULTILEADER";
                case Leader _: return "LEADER";
                case Solid _: return "SOLID";
                case Region _: return "REGION";
                default: return e.GetRXClass().DxfName ?? e.GetType().Name.ToUpperInvariant();
            }
        }

        public static string LayerName(ToolContext ctx, ObjectId layerId) =>
            ((LayerTableRecord)ctx.Tr.GetObject(layerId, OpenMode.ForRead)).Name;
    }
}
