using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>Spec §7.3 — blocks and attributes.</summary>
    public static partial class DrawingService
    {
        private static void RegisterBlocks(ToolDispatcher d)
        {
            d.Register("define_block", true, DefineBlock);
            d.Register("insert_block", true, InsertBlock);
            d.Register("list_block_defs", false, ListBlockDefs);
            d.Register("get_block_attributes", false, GetBlockAttributes);
            d.Register("set_block_attributes", true, SetBlockAttributes);
            d.Register("set_dynamic_props", true, SetDynamicProps);
        }

        internal static ObjectId BlockDefId(ToolContext ctx, string name)
        {
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(name))
                throw new ToolException(ErrorCodes.BlockNotFound,
                    $"Block '{name}' is not defined in this drawing. Call list_block_defs to see existing blocks, or define_block to create it from entities.");
            return bt[name];
        }

        private static object DefineBlock(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            ValidateSymbolName(name, "block");
            var basePt = Acad.P3(p.Point("base_point"));
            var hs = p.Has("entity_handles") ? p.Handles("entity_handles", MaxModifyHandles) : new List<string>();
            var atts = p.Has("attributes") ? p.Array("attributes") : new List<object>();
            if (hs.Count == 0 && atts.Count == 0) throw ToolException.Invalid("define_block: give entity_handles (and/or attributes).");
            bool convert = p.Bool("replace_with_insert", true);
            bool overwrite = p.Bool("overwrite", false);

            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(name) && !overwrite)
                throw ToolException.Invalid($"Block '{name}' already exists. Use another name, insert_block to use it, or overwrite=true to redefine it.");

            BlockTableRecord btr;
            ObjectId btrId;
            if (bt.Has(name))
            {
                btrId = bt[name];
                btr = (BlockTableRecord)ctx.Tr.GetObject(btrId, OpenMode.ForWrite);
                foreach (ObjectId old in btr) ctx.Tr.GetObject(old, OpenMode.ForWrite).Erase();
            }
            else
            {
                btr = new BlockTableRecord { Name = name };
                bt.UpgradeOpen();
                btrId = bt.Add(btr);
                ctx.Tr.AddNewlyCreatedDBObject(btr, true);
            }
            btr.Origin = Point3d.Origin;
            var desc = p.Str("description", null);
            if (desc != null) btr.Comments = desc;

            // clone the source entities into the definition, moved so that base_point becomes the block origin
            var toOrigin = Matrix3d.Displacement(Point3d.Origin - basePt);
            if (hs.Count > 0)
            {
                var ids = new ObjectIdCollection();
                foreach (var h in hs) ids.Add(Acad.IdFromHandle(ctx.Db, h));
                var map = new IdMapping();
                ctx.Db.DeepCloneObjects(ids, btrId, map, false);
                foreach (ObjectId src in ids)
                {
                    var pair = map[src];
                    if (pair.IsCloned) ((Entity)ctx.Tr.GetObject(pair.Value, OpenMode.ForWrite)).TransformBy(toOrigin);
                }
            }
            int attCount = 0;
            foreach (var a in atts)
            {
                if (!(a is Dictionary<string, object> ad)) throw ToolException.Invalid("define_block: attributes must be objects {tag, position, height, prompt?, default?}.");
                var ap = new Params("define_block.attributes", ad);
                var def = new AttributeDefinition();
                def.SetDatabaseDefaults(ctx.Db);
                def.Tag = ap.Str("tag").ToUpperInvariant().Replace(' ', '_');
                def.Prompt = ap.Str("prompt", def.Tag);
                def.TextString = ap.Str("default", "");
                def.Height = ap.Positive("height", ctx.Db.Textsize);
                def.Position = Acad.P3(ap.Point("position")).TransformBy(toOrigin);
                var just = ParseJustify(ap.Str("justify", null), AttachmentPoint.BaseLeft);
                if (just != AttachmentPoint.BaseLeft) { def.Justify = just; def.AlignmentPoint = def.Position; }
                def.Invisible = ap.Bool("invisible", false);
                var st = ap.Str("style", null);
                if (!string.IsNullOrEmpty(st)) def.TextStyleId = TextStyleId(ctx, st);
                btr.AppendEntity(def);
                ctx.Tr.AddNewlyCreatedDBObject(def, true);
                attCount++;
            }

            var result = new Dictionary<string, object> { ["name"] = name, ["entities"] = hs.Count, ["attributes"] = attCount };
            if (convert && hs.Count > 0)
            {
                foreach (var h in hs) Acad.GetEntity(ctx, h, true).Erase();
                var br = new BlockReference(basePt, btrId);
                var id = Acad.Add(ctx, br, null);
                AddAttributes(ctx, br, btr, null);
                result["handles"] = new List<object> { Acad.H(id) };
                result["note"] = "The original entities were replaced by one block reference at base_point (like AutoCAD BLOCK > Convert).";
            }
            else result["handles"] = new List<object>();
            return result;
        }

        private static Dictionary<string, AttributeReference> AddAttributes(ToolContext ctx, BlockReference br, BlockTableRecord btr, Dictionary<string, object> values)
        {
            var created = new Dictionary<string, AttributeReference>(StringComparer.OrdinalIgnoreCase);
            if (!btr.HasAttributeDefinitions) return created;
            foreach (ObjectId id in btr)
            {
                if (!(ctx.Tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition def) || def.Constant) continue;
                var ar = new AttributeReference();
                ar.SetAttributeFromBlock(def, br.BlockTransform);
                ar.TextString = def.TextString;
                if (values != null)
                {
                    var key = values.Keys.FirstOrDefault(k => k.Equals(def.Tag, StringComparison.OrdinalIgnoreCase));
                    if (key != null) ar.TextString = Convert.ToString(values[key], CultureInfo.InvariantCulture);
                }
                br.AttributeCollection.AppendAttribute(ar);
                ctx.Tr.AddNewlyCreatedDBObject(ar, true);
                created[def.Tag] = ar;
            }
            return created;
        }

        private static object InsertBlock(ToolContext ctx, Params p)
        {
            string name = p.Str("name").Trim();
            var btrId = BlockDefId(ctx, name);
            var btr = (BlockTableRecord)ctx.Tr.GetObject(btrId, OpenMode.ForRead);
            if (btr.IsLayout) throw ToolException.Invalid($"'{name}' is a layout, not a block.");
            var positions = p.Has("positions") ? p.Points("positions", 1, MaxArrayCopies) : new List<double[]> { p.Point("position") };
            Scale3d scale;
            var sv = p.Get("scale");
            if (sv is List<object>) { var s = p.Point("scale"); scale = new Scale3d(s[0], s[1], s[2] == 0 ? 1 : s[2]); }
            else { double s = p.Positive("scale", 1); scale = new Scale3d(s); }
            double rot = Geometry.DegToRad(p.Num("rotation", 0));
            var values = p.ObjOpt("attributes")?.Raw;
            var dynProps = p.ObjOpt("dynamic_props")?.Raw; // phase 2: set dynamic properties at insertion
            var dynErrors = new List<object>();

            var ids = new List<ObjectId>();
            var unknown = new List<object>();
            foreach (var pos in positions)
            {
                var br = new BlockReference(Acad.P3(pos), btrId) { ScaleFactors = scale, Rotation = rot };
                ids.Add(Acad.Add(ctx, br, p));
                var created = AddAttributes(ctx, br, btr, values);
                if (dynProps != null && br.IsDynamicBlock)
                {
                    foreach (var kv in dynProps)
                        foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
                            if (prop.PropertyName.Equals(kv.Key, StringComparison.OrdinalIgnoreCase) && !prop.ReadOnly)
                            {
                                try { prop.Value = ConvertDynValue(prop, kv.Value); }
                                catch (Exception ex) { dynErrors.Add($"{kv.Key}: {ex.Message}"); }
                            }
                }
                if (values != null && ids.Count == 1)
                    unknown.AddRange(values.Keys.Where(k => !created.ContainsKey(k)));
            }
            var r = Acad.Handles(ids);
            if (unknown.Count > 0) r["unknown_attribute_tags"] = unknown;
            if (dynErrors.Count > 0) r["dynamic_prop_errors"] = dynErrors;
            return r;
        }

        private static object ListBlockDefs(ToolContext ctx, Params p)
        {
            var filter = p.Str("filter", null);
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            var list = new List<object>();
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout || btr.IsAnonymous || btr.IsErased) continue;
                if (!string.IsNullOrEmpty(filter) && btr.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && !WildcardMatch(btr.Name, filter)) continue;
                int count = 0;
                var tags = new List<object>();
                foreach (ObjectId eid in btr)
                {
                    count++;
                    if (btr.HasAttributeDefinitions && ctx.Tr.GetObject(eid, OpenMode.ForRead) is AttributeDefinition ad) tags.Add(ad.Tag);
                }
                int refs = 0;
                try { refs = btr.GetBlockReferenceIds(true, false).Count; } catch { }
                var d = new Dictionary<string, object>
                {
                    ["name"] = btr.Name,
                    ["entities"] = count,
                    ["references"] = refs,
                };
                if (tags.Count > 0) d["attribute_tags"] = tags;
                if (btr.IsDynamicBlock) d["dynamic"] = true;
                if (btr.IsFromExternalReference) d["xref"] = true;
                if (!string.IsNullOrEmpty(btr.Comments)) d["description"] = btr.Comments;
                list.Add(d);
            }
            return new Dictionary<string, object> { ["count"] = list.Count, ["blocks"] = list };
        }

        private static BlockReference GetBlockRef(ToolContext ctx, string h, bool forWrite)
        {
            var e = Acad.GetEntity(ctx, h, forWrite);
            if (!(e is BlockReference br) || e is Table)
                throw ToolException.Invalid($"{h} is a {Acad.TypeName(e)}, not a block reference (INSERT).");
            return br;
        }

        private static object GetBlockAttributes(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var br = GetBlockRef(ctx, h, false);
            var atts = new Dictionary<string, object>();
            var attHandles = new Dictionary<string, object>();
            foreach (ObjectId aid in br.AttributeCollection)
            {
                if (!(ctx.Tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)) continue;
                atts[ar.Tag] = ar.IsMTextAttribute ? ar.MTextAttribute.Text : ar.TextString;
                attHandles[ar.Tag] = ar.Handle.ToString();
            }
            var r = new Dictionary<string, object>
            {
                ["handle"] = h,
                ["name"] = EntityInfo.BlockName(ctx, br),
                ["attributes"] = atts,
                ["attribute_handles"] = attHandles,
            };
            if (br.IsDynamicBlock) r["dynamic_properties"] = DynamicProps(br);
            return r;
        }

        private static List<object> DynamicProps(BlockReference br)
        {
            var list = new List<object>();
            foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
            {
                if (string.IsNullOrEmpty(prop.PropertyName) || prop.PropertyName == "Origin") continue;
                var d = new Dictionary<string, object>
                {
                    ["name"] = prop.PropertyName,
                    ["value"] = prop.Value is Point3d pt ? (object)Acad.J(pt) : prop.Value,
                    ["read_only"] = prop.ReadOnly,
                    ["units"] = prop.UnitsType.ToString(),
                };
                var allowed = prop.GetAllowedValues();
                if (allowed != null && allowed.Length > 0) d["allowed_values"] = allowed.Cast<object>().ToList();
                if (!string.IsNullOrEmpty(prop.Description)) d["description"] = prop.Description;
                list.Add(d);
            }
            return list;
        }

        private static object SetBlockAttributes(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var br = GetBlockRef(ctx, h, true);
            var values = p.Obj("attributes").Raw;
            var done = new List<object>();
            foreach (ObjectId aid in br.AttributeCollection)
            {
                if (!(ctx.Tr.GetObject(aid, OpenMode.ForWrite) is AttributeReference ar)) continue;
                var key = values.Keys.FirstOrDefault(k => k.Equals(ar.Tag, StringComparison.OrdinalIgnoreCase));
                if (key == null) continue;
                var text = Convert.ToString(values[key], CultureInfo.InvariantCulture);
                if (ar.IsMTextAttribute)
                {
                    var m = ar.MTextAttribute;
                    m.Contents = text.Replace("\n", "\\P");
                    ar.MTextAttribute = m;
                }
                else ar.TextString = text;
                done.Add(ar.Tag);
            }
            var missing = values.Keys.Where(k => !done.Any(t => ((string)t).Equals(k, StringComparison.OrdinalIgnoreCase))).Cast<object>().ToList();
            var r = new Dictionary<string, object> { ["handles"] = new List<object> { h }, ["updated"] = done };
            if (missing.Count > 0) r["unknown_tags"] = missing;
            return r;
        }

        private static object SetDynamicProps(ToolContext ctx, Params p)
        {
            var h = p.Handle("handle");
            var br = GetBlockRef(ctx, h, true);
            if (!br.IsDynamicBlock) throw ToolException.Invalid($"{h} is not a dynamic block reference.");
            var props = p.Obj("props").Raw;
            var done = new List<object>();
            var errors = new List<object>();
            foreach (var kv in props)
            {
                DynamicBlockReferenceProperty target = null;
                foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
                    if (prop.PropertyName.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) { target = prop; break; }
                if (target == null) { errors.Add($"{kv.Key}: no such property"); continue; }
                if (target.ReadOnly) { errors.Add($"{kv.Key}: read-only"); continue; }
                try
                {
                    target.Value = ConvertDynValue(target, kv.Value);
                    done.Add(target.PropertyName);
                }
                catch (Exception ex)
                {
                    errors.Add($"{kv.Key}: {ex.Message}");
                }
            }
            var r = new Dictionary<string, object> { ["handles"] = new List<object> { h }, ["updated"] = done, ["properties"] = DynamicProps(br) };
            if (errors.Count > 0) r["errors"] = errors;
            return r;
        }

        private static object ConvertDynValue(DynamicBlockReferenceProperty prop, object v)
        {
            var current = prop.Value;
            switch (current)
            {
                case double _: return Convert.ToDouble(v, CultureInfo.InvariantCulture) * (prop.UnitsType == DynamicBlockReferencePropertyUnitsType.Angular ? Math.PI / 180 : 1);
                case short _: return Convert.ToInt16(v, CultureInfo.InvariantCulture);
                case int _: return Convert.ToInt32(v, CultureInfo.InvariantCulture);
                case string _: return Convert.ToString(v, CultureInfo.InvariantCulture);
                case Point3d _:
                    if (Params.TryPoint(v, out var pt)) return Acad.P3(pt);
                    throw new ArgumentException("expected a point [x, y]");
                default: return v;
            }
        }
    }
}
