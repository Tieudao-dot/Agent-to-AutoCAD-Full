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
                "stretch_entities break_entity lengthen_entity divide_entity " +
                // nâng cấp đợt C (Drawing/Util2DTools.cs): plan dimensions, block / area schedules, find & replace of text
                "dim_floor_plan block_schedule area_schedule find_replace_text");
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
                "build_railing " +
                // nâng cấp đợt D (Drawing/Solid3Tools.cs): beams, PRESSPULL, STL export, volume schedule by layer
                "build_beam presspull export_stl volume_schedule " +
                // nâng cấp đợt E (Drawing/StairRoofTools.cs): stairs, roofs
                "build_stair build_roof " +
                // nâng cấp đợt F3 (Drawing/RenderTools.cs): materials, named views (+ a PNG with the materials)
                "set_material named_view");
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
            // nâng cấp đợt D (phương án A, 13/11/2026): a TABLE in the drawing is text, so 2D; on the 3D plan the 3D
            // schedules only return their numbers to Claude (shown in the chat), the table is inserted on Full only
            ["volume_schedule"] = "table_position",
            ["check_interference"] = "table_position",
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
                : param == "table_position"
                ? $"{tool}: this machine's plan is \"{Label(plan)}\", which has no text, and '{param}' inserts a table (text, 2D group) into the drawing. " +
                  "Call it without table_position (the numbers come back to you) and show them to the user in the chat, or tell the user in Vietnamese that a table in the drawing needs the " +
                  $"\"Full\" plan and that the plugin owner can open the 2D group" + (string.IsNullOrWhiteSpace(contact) ? "." : $" ({contact}).")
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
                    : tool + (Blocked3dParam(plan, tool, p) != null ? " (số tầng)" : BlockedTextValue(plan, tool, p) != null ? " (thuộc tính chữ / kích thước)"
                        : TextParams.TryGetValue(tool, out var tp) && tp == "table_position" ? " (bảng trong bản vẽ)" : " (thuộc tính block)");
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

    /// <summary>
    /// Nâng cấp đợt F1, việc 2 (chủ dự án chốt 14/11/2026): on a Full machine the user picks a work mode in the palette —
    /// "Cả hai" (default), "2D" or "3D". 2D / 3D load only the tools and rules of that group (the very sets of the 2D / 3D
    /// plans), so every chat opening costs fewer tokens. A machine on the 2D or 3D plan has no switch.
    /// </summary>
    public static class WorkModeLogic
    {
        public const string Both = "both";
        public const string TwoD = "2d";
        public const string ThreeD = "3d";
        public static readonly string[] Modes = { Both, TwoD, ThreeD };

        /// <summary>"both" / "2d" / "3d"; anything else (an old settings file, a typo) is "both".</summary>
        public static string Normalize(string mode)
        {
            var s = (mode ?? "").Trim().ToLowerInvariant();
            return s == TwoD || s == ThreeD ? s : Both;
        }

        /// <summary>The switch is shown only on a Full machine.</summary>
        public static bool ShowsSwitch(string plan) => PlanLogic.IsKnown(plan) && PlanLogic.Normalize(plan) == PlanLogic.PlanFull;

        /// <summary>
        /// The plan the chat runs with: on Full the chosen mode's plan ("2d" / "3d"), else the machine's plan unchanged
        /// (a 2D / 3D machine never gets more or less than its plan from a stale setting).
        /// </summary>
        public static string EffectivePlan(string plan, string mode)
        {
            if (!ShowsSwitch(plan)) return plan;
            var m = Normalize(mode);
            return m == Both ? PlanLogic.PlanFull : m;
        }

        /// <summary>The mode that limits this Full machine's chat ("2d" / "3d"), null when nothing is limited.</summary>
        public static string Limiting(string plan, string mode)
        {
            if (!ShowsSwitch(plan)) return null;
            var m = Normalize(mode);
            return m == Both ? null : m;
        }

        /// <summary>What the switch shows: "Cả hai", "2D", "3D".</summary>
        public static string Label(string mode)
        {
            switch (Normalize(mode))
            {
                case TwoD: return "2D";
                case ThreeD: return "3D";
                default: return "Cả hai";
            }
        }

        public static string Tip(string mode) =>
            "Chế độ làm việc (máy gói Full): " + Label(mode) + ". \"Cả hai\" có đủ lệnh 2D và 3D; chọn \"2D\" hoặc \"3D\" để Claude chỉ nạp lệnh của nhóm đó " +
            "(mỗi lần mở chat tốn ít token hơn). Đổi chế độ áp dụng từ tin nhắn sau, đoạn chat giữ nguyên.";

        /// <summary>The line the palette adds when the mode changes (it applies from the next message).</summary>
        public static string ChangedNote(string mode, bool busy) =>
            $"Đã chuyển sang chế độ {Label(mode)}" +
            (Normalize(mode) == Both ? " (đủ lệnh 2D và 3D)" : $" (Claude chỉ dùng lệnh {Label(mode)})") +
            (busy ? ": áp dụng từ tin nhắn sau, lượt đang chạy giữ chế độ cũ." : ": áp dụng từ tin nhắn tới, đoạn chat giữ nguyên.");

        /// <summary>The other mode that has the tool ("3D" for a 3D tool in the 2D mode).</summary>
        private static string OtherMode(string mode) => Normalize(mode) == TwoD ? "3D" : "2D";

        /// <summary>
        /// Error text for Claude when a tool is outside the chosen mode of a Full machine: no plan talk, no invitation to buy
        /// or upgrade (the machine has Full) — the user only switches the mode.
        /// </summary>
        public static string BlockedMessage(string tool, string mode)
        {
            var label = Label(mode);
            var other = OtherMode(mode);
            return $"{tool}: the user set the palette to the {label} work mode, which leaves the {other} tools out (this machine's plan is Full: it has every tool). " +
                   "Do not try other tools to get the same result and do not mention plans, buying or upgrading. Tell the user in Vietnamese: " +
                   $"\"Đang ở chế độ {label}, đổi sang {other} hoặc Cả hai (nút chế độ trên khung chat) để {(other == "3D" ? "vẽ 3D" : "vẽ 2D")}.\" Then do what the {label} mode allows.";
        }

        /// <summary>
        /// Test & sửa đợt F1: why match_properties left the text / dimension properties out: the machine's plan (2D / 3D
        /// machine, exactly as before F1) or the 3D work mode of a Full machine (switch the mode, never a plan).
        /// </summary>
        public static string NoTwoDReason(string workMode) =>
            Normalize(workMode) == ThreeD
                ? " (outside the 3D work mode the user chose; the 2D or Cả hai mode has it: do not mention plans)"
                : " (this machine's plan has no 2D)";

        /// <summary>A parameter outside the mode (block attributes in 3D, array levels in 2D…): the same wording.</summary>
        public static string BlockedParamMessage(string tool, string param, string mode) =>
            $"{tool}: '{param}' is outside the {Label(mode)} work mode the user chose (this machine's plan is Full). " +
            $"Do not mention plans or upgrades; tell the user in Vietnamese: \"Đang ở chế độ {Label(mode)}, đổi sang {OtherMode(mode)} hoặc Cả hai (nút chế độ trên khung chat) để dùng phần này.\"";
    }
}
