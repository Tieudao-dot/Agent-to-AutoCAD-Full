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
                "define_block insert_block set_block_attributes set_dynamic_props reset_dynamic_block attach_xref detach_xref bind_xref reload_xref " +
                // basic curves: 3D uses them as profiles and paths
                "draw_line draw_polyline draw_circle draw_arc draw_spline draw_ellipse draw_rectangle draw_polygon draw_point draw_ray draw_xline");
            Add(ToolGroup.TwoD,
                "add_text add_mtext edit_text add_mleader add_table " +
                "add_dim_linear add_dim_aligned add_dim_angular add_dim_radial add_dim_diameter add_dim_ordinate add_dim_baseline add_dim_continue " +
                "draw_hatch draw_wipeout draw_revcloud " +
                "create_layout delete_layout set_current_layout add_viewport set_viewport plot_pdf list_plot_media " +
                "create_text_style create_dim_style set_current_styles " +
                "offset_entity fillet_entities chamfer_entities trim_entities extend_entities join_entities explode_entities edit_polyline");
            // 3D phase 1 (Drawing/SolidTools.cs): only the 3D and Full plans
            Add(ToolGroup.ThreeD,
                "draw_box draw_cylinder draw_sphere draw_wedge draw_torus draw_pyramid extrude revolve " +
                "solid_union solid_subtract solid_intersect set_view_3d set_visual_style get_solid_info");
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
