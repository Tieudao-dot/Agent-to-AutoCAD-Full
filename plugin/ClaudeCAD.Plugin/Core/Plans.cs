using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>Which service plan a tool belongs to.</summary>
    public enum ToolGroup
    {
        /// <summary>Every plan: reading, layers, view, transforms, blocks, xrefs and the basic curves 3D needs as profiles.</summary>
        Common,
        /// <summary>2D drafting: text, dimensions, hatch, layouts, plotting, 2D editing.</summary>
        TwoD,
        /// <summary>3D modelling (solids, views, visual styles).</summary>
        ThreeD,
    }

    /// <summary>
    /// Service plans per machine (2D / 3D / Full), AutoCAD-free so it is unit-tested on CI. The owner picks the
    /// plan in the "Gói" column of the device sheet; the activation service signs it ("CCF1|machine|plan|date|sig",
    /// see LicenseLogic.VerifyPlan) and the dispatcher refuses every tool outside the plan. The assistant only
    /// hides those tools to save tokens: the real gate is ToolDispatcher.
    /// </summary>
    public static class PlanLogic
    {
        public const string Plan2D = "2d";
        public const string Plan3D = "3d";
        public const string PlanFull = "2d3d";
        /// <summary>A machine whose service sent no (valid) plan line: what the plugin did before plans existed.</summary>
        /// <remarks>2D for new machines, empty and mistyped cells: decided by the owner on 12/11/2026 (GOI_MAC_DINH in the script).</remarks>
        public const string DefaultPlan = Plan2D;

        /// <summary>"2d" / "3d" / "2d3d"; anything else (null, typos, a newer plan name) is the default plan.</summary>
        public static string Normalize(string plan)
        {
            var s = (plan ?? "").Trim().ToLowerInvariant();
            return s == Plan2D || s == Plan3D || s == PlanFull ? s : DefaultPlan;
        }

        public static bool IsKnown(string plan)
        {
            var s = (plan ?? "").Trim().ToLowerInvariant();
            return s == Plan2D || s == Plan3D || s == PlanFull;
        }

        public static bool Has2D(string plan) { var p = Normalize(plan); return p == Plan2D || p == PlanFull; }
        public static bool Has3D(string plan) { var p = Normalize(plan); return p == Plan3D || p == PlanFull; }

        /// <summary>What the palette shows: "2D", "3D", "Full".</summary>
        public static string Label(string plan)
        {
            switch (Normalize(plan))
            {
                case Plan3D: return "3D";
                case PlanFull: return "Full";
                default: return "2D";
            }
        }

        private static readonly Dictionary<string, ToolGroup> _groups = Build();

        private static Dictionary<string, ToolGroup> Build()
        {
            var d = new Dictionary<string, ToolGroup>(StringComparer.Ordinal);
            void Add(ToolGroup g, string names)
            {
                foreach (var n in names.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) d.Add(n, g);
            }
            Add(ToolGroup.Common,
                // system, reading, measuring
                "ping get_drawing_info list_entities get_entity count_entities find_text get_bounding_box measure_area measure_distance " +
                "list_layers list_layouts list_styles list_block_defs list_xrefs get_block_attributes get_dynamic_props " +
                // layers, view, file, history, self-check
                "create_layer set_current_layer set_layer_props zoom_extents zoom_window regen capture_view save_drawing undo_last_turn " +
                "purge_unused batch_execute " +
                // transforms and properties
                "move_entities copy_entities rotate_entities scale_entities mirror_entities erase_entities array_rect array_polar set_entity_props " +
                // blocks and xrefs
                // (block attributes are text: their parameters need 2D, see TextParams; set_block_attributes is 2D)
                "define_block insert_block set_dynamic_props reset_dynamic_block attach_xref detach_xref bind_xref reload_xref " +
                // nâng cấp đợt B (Drawing/Edit2DTools.cs): every plan; match_properties copies text / dimension properties only with 2D (TextValues)
                "group_entities match_properties " +
                // basic curves: 3D uses them as profiles and paths
                "draw_line draw_polyline draw_circle draw_arc draw_spline draw_ellipse draw_rectangle draw_polygon draw_point draw_ray draw_xline");
            Add(ToolGroup.TwoD,
                "add_text add_mtext edit_text add_mleader add_table set_block_attributes " +
                "add_dim_linear add_dim_aligned add_dim_angular add_dim_radial add_dim_diameter add_dim_ordinate add_dim_baseline add_dim_continue " +
                "draw_hatch draw_wipeout draw_revcloud " +
                "create_layout delete_layout set_current_layout add_viewport set_viewport plot_pdf list_plot_media " +
                "create_text_style create_dim_style set_current_styles " +
                "offset_entity fillet_entities chamfer_entities trim_entities extend_entities join_entities explode_entities edit_polyline " +
                // nâng cấp đợt B (Drawing/Edit2DTools.cs): STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE
                "stretch_entities break_entity lengthen_entity divide_entity");
            // 3D phases 1, 2 and 3 (Drawing/SolidTools.cs, Solid2Tools.cs, PlanTo3dTools.cs, SectionTools.cs): only the 3D and Full plans
            Add(ToolGroup.ThreeD,
                // draw_solid (nâng cấp đợt A) replaces the six primitives for Claude; their old names stay for recorded macros
                "draw_solid draw_box draw_cylinder draw_sphere draw_wedge draw_torus draw_pyramid extrude revolve " +
                "solid_union solid_subtract solid_intersect set_view_3d set_visual_style get_solid_info " +
                // 3D phase 2 (Drawing/Solid2Tools.cs)
                "sweep loft slice_solid fillet_edges chamfer_edges shell_solid rotate_3d mirror_3d align set_ucs reset_ucs check_interference " +
                // 3D phase 3 (Drawing/PlanTo3dTools.cs, SectionTools.cs): their 2D lines / hatch are results of a 3D tool
                "build_3d_from_plan section_view projection_view " +
                // nâng cấp đợt A (Drawing/PlanTo3dTools.cs): railing along a path
                "build_railing");
            return d;
        }

        /// <summary>Every labelled tool. A test fails when a registered tool is missing here.</summary>
        public static IReadOnlyDictionary<string, ToolGroup> Groups => _groups;

        /// <summary>The group of a tool, or null for a tool nobody labelled.</summary>
        public static ToolGroup? GroupOf(string tool) =>
            tool != null && _groups.TryGetValue(tool, out var g) ? g : (ToolGroup?)null;

        /// <summary>A tool nobody labelled is only open to the full plan, so a forgotten label never gives a tool away.</summary>
        public static bool Allowed(string plan, string tool)
        {
            switch (GroupOf(tool))
            {
                case ToolGroup.Common: return true;
                case ToolGroup.TwoD: return Has2D(plan);
                case ToolGroup.ThreeD: return Has3D(plan);
                default: return Normalize(plan) == PlanFull;
            }
        }

        /// <summary>
        /// Parameters of Common tools that write text, so they need 2D (decided 12/11/2026, C3): block attribute
        /// definitions of define_block and attribute values of insert_block. A block without attributes stays Common.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> TextParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["define_block"] = "attributes",
            ["insert_block"] = "attributes",
        };

        /// <summary>
        /// Parameters of Common tools that are 3D (nâng cấp đợt A, 13/11/2026): the levels of array_rect (copies stacked
        /// along Z, like ARRAYRECT's levels). levels 1 (or none) stays Common.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ThreeDParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["array_rect"] = "levels",
        };

        /// <summary>The 3D parameter of this call the plan does not have (array_rect levels above 1 on a plan without 3D), or null.</summary>
        public static string Blocked3dParam(string plan, string tool, IDictionary<string, object> p)
        {
            if (tool == null || p == null || Has3D(plan) || !ThreeDParams.TryGetValue(tool, out var name)) return null;
            if (!p.TryGetValue(name, out var v) || v == null) return null;
            try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) > 1 ? name : null; }
            catch (Exception) { return name; }
        }

        /// <summary>
        /// Values of a Common tool's list parameter that are text (nâng cấp đợt B, 13/11/2026): match_properties copying
        /// text / dimension properties needs 2D. Copying layer, colour... stays Common; with no list named, the plugin
        /// leaves the text ones out by itself on a plan without 2D (Edit2DLogic.MatchProps).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string param, string[] values)> TextValues =
            new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
            {
                ["match_properties"] = ("properties", new[] { "text", "dimension" }),
            };

        private static string BlockedTextValue(string plan, string tool, IDictionary<string, object> p)
        {
            if (tool == null || p == null || Has2D(plan) || !TextValues.TryGetValue(tool, out var tv)) return null;
            if (!p.TryGetValue(tv.param, out var v) || v == null) return null;
            IEnumerable<object> items = v is string s1 ? new object[] { s1 } : v is System.Collections.IEnumerable e ? e.Cast<object>() : new object[] { v };
            return items.Any(x => tv.values.Contains((Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture) ?? "").Trim().ToLowerInvariant())) ? tv.param : null;
        }

        /// <summary>The parameter of this call the plan does not have (block attributes without 2D, array levels without 3D), or null.</summary>
        public static string BlockedParam(string plan, string tool, IDictionary<string, object> p)
        {
            var three = Blocked3dParam(plan, tool, p);
            if (three != null) return three;
            var textValue = BlockedTextValue(plan, tool, p);
            if (textValue != null) return textValue;
            if (tool == null || p == null || Has2D(plan) || !TextParams.TryGetValue(tool, out var name)) return null;
            if (!p.TryGetValue(name, out var v) || v == null) return null;
            if (v is System.Collections.ICollection c && c.Count == 0) return null;
            if (v is string s && s.Length == 0) return null;
            return name;
        }

        /// <summary>The whole call: the tool and its parameters belong to the plan.</summary>
        public static bool CallAllowed(string plan, string tool, IDictionary<string, object> p) =>
            Allowed(plan, tool) && BlockedParam(plan, tool, p) == null;

        /// <summary>Error text for Claude when a parameter (block attributes) is outside the machine's plan.</summary>
        public static string BlockedParamMessage(string tool, string param, string plan, string contact) =>
            TextValues.TryGetValue(tool ?? "", out var tv) && tv.param == param
                ? $"{tool}: this machine's plan is \"{Label(plan)}\", which has no text or dimensions, and '{param}' asks for " +
                  $"{string.Join(" / ", tv.values)} properties from the 2D group. Leave them out (layer, color, linetype... are copied on every plan), " +
                  $"or tell the user in Vietnamese that this machine uses the \"{Label(plan)}\" plan and that the plugin owner can open the 2D group" +
                  (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).")
            : ThreeDParams.TryGetValue(tool ?? "", out var p3) && p3 == param
                ? $"{tool}: this machine's plan is \"{Label(plan)}\", which has no 3D, and '{param}' (copies stacked along Z) belongs to the 3D group. " +
                  "Leave levels out (one level), or tell the user in Vietnamese that this machine uses the " +
                  $"\"{Label(plan)}\" plan and that the plugin owner can open the 3D group" + (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).")
                : $"{tool}: this machine's plan is \"{Label(plan)}\", which has no text, and '{param}' (block attributes) is text from the 2D group. " +
            "Define or insert the block without attributes, or tell the user in Vietnamese that this machine uses the " +
            $"\"{Label(plan)}\" plan and that the plugin owner can open the 2D group" +
            (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).");

        /// <summary>Tools of a call list (a macro) outside the plan, each once, in order; an empty list = the calls may run.</summary>
        public static List<string> BlockedCalls(string plan, IEnumerable<(string tool, IDictionary<string, object> p)> calls)
        {
            var list = new List<string>();
            foreach (var (tool, p) in calls)
            {
                var bp = Allowed(plan, tool) ? BlockedParam(plan, tool, p) : null;
                string what = !Allowed(plan, tool) ? tool : bp == null ? null
                    : tool + (Blocked3dParam(plan, tool, p) != null ? " (số tầng)" : BlockedTextValue(plan, tool, p) != null ? " (thuộc tính chữ / kích thước)" : " (thuộc tính block)");
                if (what != null && !list.Contains(what)) list.Add(what);
            }
            return list;
        }

        /// <summary>What the palette says when a macro uses tools outside the plan (it is not run at all).</summary>
        public static string MacroBlockedText(string macro, string plan, IList<string> blocked, string contact)
        {
            var shown = blocked.Take(5).ToList();
            var more = blocked.Count > shown.Count ? $" và {blocked.Count - shown.Count} lệnh khác" : "";
            return $"Không chạy macro \"{macro}\": máy này dùng gói {Label(plan)}, mà macro có lệnh ngoài gói ({string.Join(", ", shown)}{more}). " +
                   "Bản vẽ không bị thay đổi. Liên hệ chủ plugin để mở thêm gói" + (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).");
        }

        public static List<string> AllowedTools(string plan) =>
            _groups.Keys.Where(t => Allowed(plan, t)).OrderBy(t => t, StringComparer.Ordinal).ToList();

        /// <summary>Error text for Claude when a tool is outside the machine's plan.</summary>
        public static string BlockedMessage(string tool, string plan, string contact)
        {
            var g = GroupOf(tool);
            var need = g == ToolGroup.ThreeD ? "3D" : g == ToolGroup.TwoD ? "2D" : "Full";
            return $"{tool}: this machine's plan is \"{Label(plan)}\" and this tool belongs to the {need} group, which the plan does not include. " +
                   "Do not try other tools to get the same result. Tell the user in Vietnamese that this machine uses the " +
                   $"\"{Label(plan)}\" plan and that the plugin owner can open the {need} group" +
                   (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).");
        }
    }
}
