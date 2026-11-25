using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2.md): standard styles, sheets with a title block, library
    /// blocks, layer states. AutoCAD-free logic (Core/Standards2D.cs) checked here; the AutoCAD part in the sources.
    /// </summary>
    public class NangCapF2Tests
    {
        public static readonly string[] TwoDTools = { "setup_standards", "create_sheet" };
        public static readonly string[] CommonTools = { "insert_library_block", "layer_state" };

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");

        // ------------------------------------------------------------ plans, work modes, UCS, labels

        [Fact]
        public void New_tools_are_labelled_registered_and_named()
        {
            foreach (var t in TwoDTools) Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(t));
            foreach (var t in CommonTools) Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf(t));
            foreach (var t in TwoDTools.Concat(CommonTools)) Assert.Contains($"\"{t}\"", Tools);
            Assert.Contains("RegisterStandards2D(d)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            var labels = Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs");
            foreach (var t in TwoDTools.Concat(CommonTools)) Assert.Contains($"[\"{t}\"]", labels);
            var st = Src("plugin", "ClaudeCAD.Plugin", "SelfTest.cs");
            Assert.Contains("Nâng cấp đợt F2", st);
            foreach (var f in new[] { "SetupStandards", "CreateSheet", "InsertLibraryBlock", "LayerState" }) Assert.Contains("DrawingService." + f, st);
        }

        [Theory]
        [InlineData("2d", true, true)]
        [InlineData("3d", false, true)]
        [InlineData("2d3d", true, true)]
        public void Plans_open_the_tools_like_text_layouts_blocks_and_layers(string plan, bool twoD, bool common)
        {
            foreach (var t in TwoDTools) Assert.Equal(twoD, PlanLogic.Allowed(plan, t));
            foreach (var t in CommonTools) Assert.Equal(common, PlanLogic.Allowed(plan, t));
            // the same groups as the tools they sit next to
            Assert.Equal(PlanLogic.Allowed(plan, "create_layout"), PlanLogic.Allowed(plan, "create_sheet"));
            Assert.Equal(PlanLogic.Allowed(plan, "create_dim_style"), PlanLogic.Allowed(plan, "setup_standards"));
            Assert.Equal(PlanLogic.Allowed(plan, "insert_block"), PlanLogic.Allowed(plan, "insert_library_block"));
            Assert.Equal(PlanLogic.Allowed(plan, "set_layer_props"), PlanLogic.Allowed(plan, "layer_state"));
        }

        [Fact]
        public void Work_modes_of_a_full_machine_load_the_right_new_tools()
        {
            var two = WorkModeLogic.EffectivePlan("2d3d", "2d");
            var three = WorkModeLogic.EffectivePlan("2d3d", "3d");
            var both = WorkModeLogic.EffectivePlan("2d3d", "both");
            foreach (var t in TwoDTools) { Assert.True(PlanLogic.Allowed(two, t)); Assert.False(PlanLogic.Allowed(three, t)); Assert.True(PlanLogic.Allowed(both, t)); }
            foreach (var t in CommonTools) { Assert.True(PlanLogic.Allowed(two, t)); Assert.True(PlanLogic.Allowed(three, t)); Assert.True(PlanLogic.Allowed(both, t)); }
        }

        [Fact]
        public void Ucs_modes_of_the_new_tools()
        {
            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("insert_library_block", new Dictionary<string, object> { ["name"] = "cua_di" }));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("setup_standards", null));
            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("layer_state", null));
            // the viewport centre is a model point given in the current UCS
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("create_sheet", null));
            Assert.Equal(ParamKind.Point, UcsLogic.KindOf("create_sheet", "view_center"));
            // the viewport turns with a UCS turned in plan
            Assert.Contains("vp.TwistAngle = (2 * Math.PI - turn) % (2 * Math.PI)", Tools);
            Assert.Contains("Matrix3d.Rotation(-turn, Vector3d.ZAxis, Point3d.Origin)", Tools);
        }

        // ------------------------------------------------------------ setup_standards

        [Theory]
        [InlineData("1:100", 100)]
        [InlineData("1/50", 50)]
        [InlineData(" 1 : 200 ", 200)]
        [InlineData("100", 100)]
        [InlineData("0.01", 100)]
        public void Scale_strings(string s, int n) => Assert.Equal(n, StandardsLogic.ScaleDenominator(s));

        [Fact]
        public void Scale_numbers_defaults_and_errors()
        {
            Assert.Equal(100, StandardsLogic.ScaleDenominator(100.0));
            Assert.Equal(20, StandardsLogic.ScaleDenominator(0.05));
            Assert.Equal(F2Numbers.DefaultScale, StandardsLogic.ScaleDenominator(null));
            Assert.Equal(F2Numbers.DefaultScale, StandardsLogic.ScaleDenominator(""));
            foreach (var bad in new object[] { "abc", "1:0", 0.0, -5.0, 20000.0, "2:1" })
                Assert.Throws<ToolException>(() => StandardsLogic.ScaleDenominator(bad));
        }

        [Fact]
        public void Standard_names_follow_the_skill()
        {
            Assert.Equal("KT", StandardsLogic.Prefix("ve-kien-truc"));
            Assert.Equal("KT", StandardsLogic.Prefix(" VE-KIEN-TRUC "));
            Assert.Equal("VN", StandardsLogic.Prefix(""));
            Assert.Equal("VN", StandardsLogic.Prefix(null));
            Assert.Equal("VN", StandardsLogic.Prefix("ve-mep"));
            Assert.Equal("KT-ARIAL", StandardsLogic.TextStyleName("KT"));
            Assert.Equal("KT-100", StandardsLogic.DimStyleName("KT", 100, 1));
            Assert.Equal("VN-100-M", StandardsLogic.DimStyleName("VN", 100, 1000));
            Assert.Equal("VN-50-CM", StandardsLogic.DimStyleName("VN", 50, 10));
            // the ve-kien-truc skill's own numbers (section 4)
            var skill = Src("skills", "ve-kien-truc", "SKILL.md");
            Assert.Contains("KT-ARIAL", skill);
            Assert.Contains("setup_standards", skill);
        }

        [Fact]
        public void Dim_style_is_sized_for_the_drawing_units()
        {
            var mm = StandardsLogic.DimStyleParams("VN", 100, 1);
            Assert.Equal(100.0, (double)mm["scale"]);
            Assert.Equal(0.0, (double)mm["decimals"]);
            Assert.Equal("VN-ARIAL", mm["text_style"]);
            Assert.Equal(F2Numbers.DimTextHeight, (double)mm["text_height"]);
            var m = StandardsLogic.DimStyleParams("VN", 100, 1000);
            Assert.Equal(0.1, (double)m["scale"], 9);
            Assert.Equal(2.0, (double)m["decimals"]);
            // every number is a double: Params.Num refuses ints
            foreach (var kv in mm) Assert.False(kv.Value is int, kv.Key);
            foreach (var kv in StandardsLogic.TextStyleParams("VN")) Assert.False(kv.Value is int, kv.Key);
        }

        [Fact]
        public void Text_heights_and_ltscale_per_scale_and_units()
        {
            var mm = StandardsLogic.TextHeights(100, 1);
            Assert.Equal(350.0, (double)mm["room_name"]);
            Assert.Equal(250.0, (double)mm["note"]);
            Assert.Equal(500.0, (double)mm["title"]);
            var m = StandardsLogic.TextHeights(100, 1000);
            Assert.Equal(0.35, (double)m["room_name"], 9);
            Assert.Equal(50.0, StandardsLogic.LtScale(100, 1), 9);
            Assert.Equal(0.05, StandardsLogic.LtScale(100, 1000), 9);
            Assert.True(StandardsLogic.ShouldSetLtScale(1));
            Assert.False(StandardsLogic.ShouldSetLtScale(50));
        }

        [Fact]
        public void A_second_run_finds_nothing_to_change_and_a_changed_style_is_listed()
        {
            var wanted = StandardsLogic.DimStyleParams("KT", 100, 1);
            // what ReadDimStyle reports for a style made by the first run (numbers back as doubles, names in any case)
            var read = wanted.ToDictionary(kv => kv.Key, kv => kv.Value is string s ? (object)s.ToUpperInvariant() : kv.Value);
            read["decimals"] = 0.0;
            Assert.Empty(StandardsLogic.Differences(wanted, read));
            read["scale"] = 50.0;
            read["arrow"] = "closed_filled";
            var d = StandardsLogic.Differences(wanted, read);
            Assert.Equal(2, d.Count);
            Assert.Contains("scale 50 → 100", d);
            Assert.Contains(d, x => x.StartsWith("arrow closed_filled → archtick"));
            // keys the existing style does not report are not compared; null style: nothing compared
            Assert.Empty(StandardsLogic.Differences(wanted, new Dictionary<string, object>()));
            var ts = StandardsLogic.TextStyleParams("VN");
            Assert.Single(StandardsLogic.Differences(ts, new Dictionary<string, object> { ["font"] = "Arial", ["height"] = 0.0, ["width_factor"] = 1.0 }));
        }

        [Fact]
        public void Setup_standards_never_overwrites_without_the_card()
        {
            var t = Tools;
            Assert.Contains("ApprovedOverwrites(p, \"setup_standards\"", t);
            Assert.Contains("r[\"status\"] = \"kept\"", t);
            // a kept style is not made current; linetypes are only loaded when missing; LTSCALE only from the default 1
            Assert.Contains("if ((string)ts[\"status\"] != \"kept\")", t);
            Assert.Contains("if (ltt.Has(lt)) { had.Add(lt); continue; }", t);
            Assert.Contains("StandardsLogic.ShouldSetLtScale(ltNow)", t);
            var msg = F2Confirm.CardMessage("setup_standards", "overwrite", new[] { "1A" });
            Assert.StartsWith("Confirmation required", msg); // the assistant's refusedForConfirmation
            Assert.Contains("1A", msg);
        }

        // ------------------------------------------------------------ create_sheet

        [Theory]
        [InlineData("A0", 1189, 841)]
        [InlineData("A1", 841, 594)]
        [InlineData("A2", 594, 420)]
        [InlineData("A3", 420, 297)]
        [InlineData("A4", 210, 297)]
        public void Papers_default_orientation_frame_title_block_and_viewport(string paper, double w, double h)
        {
            var s = SheetLogic.Build(paper, null);
            Assert.Equal(w, s.W);
            Assert.Equal(h, s.H);
            Assert.Equal(F2Numbers.MarginLeft, s.X0);
            Assert.Equal(F2Numbers.MarginOther, s.Y0);
            Assert.Equal(w - F2Numbers.MarginOther, s.X1);
            Assert.Equal(h - F2Numbers.MarginOther, s.Y1);
            // the title block (bottom-right corner on the frame corner) and the viewport are inside the frame and do not overlap
            Assert.True(s.X1 - s.TitleW >= s.X0 - 1e-9);
            Assert.True(s.VpX0 >= s.X0 && s.VpX1 <= s.X1 && s.VpY1 <= s.Y1 && s.VpY0 >= s.Y0 + s.TitleH);
            Assert.True(s.VpW > 100 && s.VpH > 100);
            Assert.StartsWith("ISO_full_bleed_" + paper + "_(", s.Media);
        }

        [Fact]
        public void A3_landscape_is_the_media_of_create_layout_and_orientation_can_be_forced()
        {
            Assert.Equal("ISO_full_bleed_A3_(420.00_x_297.00_MM)", SheetLogic.Build("a3", null).Media);
            Assert.Contains("DefaultPdfMedia = \"ISO_full_bleed_A3_(420.00_x_297.00_MM)\"", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs"));
            var a4 = SheetLogic.Build("A4", true);
            Assert.Equal(297, a4.W);
            Assert.Equal("ISO_full_bleed_A4_(297.00_x_210.00_MM)", a4.Media);
            var a1 = SheetLogic.Build("A1", false);
            Assert.Equal(594, a1.W);
            Assert.Throws<ToolException>(() => SheetLogic.Build("A5", null));
            Assert.Throws<ToolException>(() => SheetLogic.Build("Letter", null));
        }

        [Fact]
        public void Viewport_scale_follows_the_drawing_units()
        {
            Assert.Equal(0.01, SheetLogic.CustomScale(100, 1), 12);
            Assert.Equal(10.0, SheetLogic.CustomScale(100, 1000), 12);
            var s = SheetLogic.Build("A3", null);
            var mm = SheetLogic.ModelArea(s, 100, 1);
            var m = SheetLogic.ModelArea(s, 100, 1000);
            Assert.Equal(s.VpW * 100, mm[0], 6);
            Assert.Equal(mm[0] / 1000, m[0], 9);
        }

        [Fact]
        public void Fit_scale_is_the_most_detailed_standard_scale_that_fits()
        {
            var s = SheetLogic.Build("A3", null); // viewport 380 × 225 mm
            Assert.Equal(380, s.VpW, 9);
            Assert.Equal(225, s.VpH, 9);
            Assert.Equal(50, SheetLogic.FitScale(s, 1, 12000, 9000));     // 1:25 shows 9500 × 5625
            Assert.Equal(50, SheetLogic.FitScale(s, 1000, 12, 9));        // the same house in metres
            Assert.Equal(100, SheetLogic.FitScale(s, 1, 30000, 20000));
            Assert.True(SheetLogic.Fits(s, 100, 1, 38000, 22500));
            Assert.False(SheetLogic.Fits(s, 100, 1, 38001, 100));
            Assert.Equal(F2Numbers.StandardScales.Last(), SheetLogic.FitScale(s, 1, 1e9, 1e9));
        }

        [Fact]
        public void Title_block_has_every_field_inside_its_cells()
        {
            var tb = SheetLogic.BuildTitleBlock();
            Assert.Equal(F2Numbers.TitleWidth, tb.W);
            Assert.Equal(F2Numbers.TitleRows.Sum(), tb.H);
            Assert.Equal(F2Numbers.TitleWidth, F2Numbers.TitleBottomCells.Sum(), 9);
            Assert.Equal(SheetLogic.Fields.Length, tb.Cells.Count);
            Assert.Equal(SheetLogic.Fields.Select(f => f.tag).OrderBy(x => x), tb.Cells.Select(c => c.Tag).OrderBy(x => x));
            foreach (var c in tb.Cells)
            {
                foreach (var pt in new[] { c.LabelAt, c.ValueAt })
                {
                    Assert.InRange(pt[0], -tb.W, 0);
                    Assert.InRange(pt[1], 0, tb.H);
                }
                Assert.True(c.ValueAt[0] > c.LabelAt[0], c.Tag);
                Assert.True(c.ValueWidth > 10, c.Tag);
            }
            foreach (var l in tb.Lines)
            {
                Assert.InRange(l[0], -tb.W - 1e-9, 1e-9);
                Assert.InRange(l[2], -tb.W - 1e-9, 1e-9);
                Assert.InRange(l[1], -1e-9, tb.H + 1e-9);
                Assert.InRange(l[3], -1e-9, tb.H + 1e-9);
            }
            // no two values in the same place
            Assert.Equal(tb.Cells.Count, tb.Cells.Select(c => $"{c.ValueAt[0]:0.###},{c.ValueAt[1]:0.###}").Distinct().Count());
            Assert.Contains(tb.Cells, c => c.Tag == "TEN_BAN_VE" && c.ValueHeight == F2Numbers.TitleMainHeight);
        }

        [Fact]
        public void Title_fields_get_their_defaults_and_unknown_fields_are_refused()
        {
            var v = SheetLogic.FieldValues(new Dictionary<string, object> { ["project"] = "NHÀ PHỐ 3 TẦNG", ["title"] = "MẶT BẰNG TẦNG 1" }, 100, "A3", new DateTime(2026, 11, 25));
            Assert.Equal("25/11/2026", v["NGAY"]);
            Assert.Equal("1:100", v["TI_LE"]);
            Assert.Equal("A3", v["KHO_GIAY"]);
            Assert.Equal("NHÀ PHỐ 3 TẦNG", v["CONG_TRINH"]);
            Assert.Equal("MẶT BẰNG TẦNG 1", v["TEN_BAN_VE"]);
            var own = SheetLogic.FieldValues(new Dictionary<string, object> { ["date"] = "01/12/2026", ["scale"] = "1:100 / 1:50" }, 100, "A1", DateTime.Today);
            Assert.Equal("01/12/2026", own["NGAY"]);
            Assert.Equal("1:100 / 1:50", own["TI_LE"]);
            Assert.Throws<ToolException>(() => SheetLogic.FieldValues(new Dictionary<string, object> { ["owner"] = "x" }, 100, "A3", DateTime.Today));
            var tb = SheetLogic.BuildTitleBlock();
            Assert.Empty(SheetLogic.TooLong(tb, v));
            var longOne = new Dictionary<string, string> { ["SO_BAN_VE"] = new string('X', 40) };
            Assert.Equal(new[] { "sheet_no" }, SheetLogic.TooLong(tb, longOne));
        }

        [Fact]
        public void Create_sheet_never_touches_an_existing_layout()
        {
            var t = Tools;
            Assert.Contains("if (lm.LayoutExists(name)) throw ToolException.Invalid", t);
            Assert.Contains("lm.DeleteLayout(name)", t); // a failure removes the half-made layout
            Assert.Contains("IsPlottable = plottable", t); // viewport layer not printed
            Assert.Contains("SheetLogic.CustomScale(n, f)", t);
        }

        // ------------------------------------------------------------ library blocks

        [Fact]
        public void Library_has_unique_names_and_every_block_draws_at_its_sizes()
        {
            var items = LibraryBlocks.Items;
            Assert.Equal(15, items.Count);
            Assert.Equal(items.Count, items.Select(i => i.Key).Distinct().Count());
            Assert.Equal(items.Count, items.Select(i => i.BlockPrefix).Distinct().Count());
            foreach (var it in items)
            {
                foreach (var (w, d) in new[] { (it.Width, it.Depth), (it.MinWidth, it.MinDepth), (it.MaxWidth, it.MaxDepth) })
                {
                    var prims = LibraryBlocks.Build(it, w, d);
                    Assert.NotEmpty(prims);
                    var b = LibraryBlocks.Box(prims);
                    double bw = b[2] - b[0], bh = b[3] - b[1];
                    Assert.True(bw > 0 && bh > 0, it.Key);
                    // the block is as wide as asked (doors: the opening; tree: the canopy); no bigger than its sizes
                    Assert.Equal(w, bw, 6);
                    double expectH = it.Key.StartsWith("cua_di") ? (it.Key == "cua_di_2_canh" ? w / 2 : w) : it.HasDepth ? d : w;
                    Assert.Equal(expectH, bh, 6);
                }
                Assert.Equal(it.Key, LibraryBlocks.Find(it.Key).Key);
                Assert.Equal(it.Key, LibraryBlocks.Find(it.Name).Key);
            }
        }

        [Theory]
        [InlineData("Cửa đi", "cua_di")]
        [InlineData("cửa đi 2 cánh", "cua_di_2_canh")]
        [InlineData("Bồn cầu", "bon_cau")]
        [InlineData("WC", "bon_cau")]
        [InlineData("chậu rửa mặt", "lavabo")]
        [InlineData("giường ngủ", "giuong")]
        [InlineData("cây xanh", "cay")]
        [InlineData("CUA-SO", "cua_so")]
        public void Library_names_in_vietnamese(string name, string key) => Assert.Equal(key, LibraryBlocks.Find(name).Key);

        [Fact]
        public void Library_sizes_and_block_names()
        {
            var door = LibraryBlocks.Require("cua_di");
            Assert.Equal((900.0, 0.0), LibraryBlocks.Sizes(door, null, null));
            Assert.Equal("CUA-DI-900", LibraryBlocks.BlockName(door, 900, 0));
            Assert.Equal("CUA-DI-800", LibraryBlocks.BlockName(door, LibraryBlocks.Sizes(door, 799.6, null).w, 0));
            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, 3000, null));
            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, null, 220)); // a door has no depth
            var win = LibraryBlocks.Require("cua_so");
            Assert.Equal("CUA-SO-1200", LibraryBlocks.BlockName(win, 1200, 220));
            Assert.Equal("CUA-SO-1200x110", LibraryBlocks.BlockName(win, 1200, 110));
            Assert.Equal("TB-BON-CAU-400x700", LibraryBlocks.BlockName(LibraryBlocks.Require("bon_cau"), 400, 700));
            Assert.Equal("CAY-3000", LibraryBlocks.BlockName(LibraryBlocks.Require("cay"), 3000, 0));
            Assert.Throws<ToolException>(() => LibraryBlocks.Require("thang may"));
            Assert.Null(LibraryBlocks.Find(""));
            // build_3d_from_plan reads the library's doors and windows by their names
            Assert.Equal("door", FloorPlanLogic.KindOfBlockName("CUA-DI-900"));
            Assert.Equal("door", FloorPlanLogic.KindOfBlockName("CUA-DI-2C-1400"));
            Assert.Equal("door", FloorPlanLogic.KindOfBlockName("CUA-DI-TRUOT-1800"));
            Assert.Equal("window", FloorPlanLogic.KindOfBlockName("CUA-SO-1200x110"));
            Assert.Null(FloorPlanLogic.KindOfBlockName("TB-BON-CAU-400x700"));
        }

        [Fact]
        public void Door_symbol_is_the_skill_symbol_and_the_catalog_lists_everything()
        {
            var prims = LibraryBlocks.Build(LibraryBlocks.Require("cua_di"), 900, 0);
            // leaf: a line of the door width from the hinge, perpendicular to the wall; a 90° arc of radius W
            Assert.Contains(prims, p => p.Kind == "line" && p.Pts[0][0] == 0 && p.Pts[0][1] == 0 && p.Pts[1][1] == 900);
            Assert.Contains(prims, p => p.Kind == "arc" && p.R == 900 && p.A0 == 0 && p.A1 == 90);
            var cat = LibraryBlocks.Catalog();
            Assert.Equal(LibraryBlocks.Items.Count, cat.Count);
            var bed = (Dictionary<string, object>)cat.First(c => (string)((Dictionary<string, object>)c)["name"] == "giuong");
            Assert.True(bed.ContainsKey("width_mm") && bed.ContainsKey("depth_mm"));
            var tree = (Dictionary<string, object>)cat.First(c => (string)((Dictionary<string, object>)c)["name"] == "cay");
            Assert.True(tree.ContainsKey("canopy_diameter_mm") && !tree.ContainsKey("depth_mm"));
            // the agent's description lists the same names
            var ts = Src("agent", "src", "tools", "standards2d.ts");
            Assert.Contains(string.Join(", ", LibraryBlocks.Items.Select(i => i.Key)), ts);
        }

        [Fact]
        public void Library_blocks_are_drawn_by_our_code_and_never_redefined()
        {
            var t = Tools;
            Assert.Contains("reused = bt.Has(name);", t);
            Assert.Contains("if (reused) return bt[name];", t);
            Assert.Contains("q[0] / f", t); // mm → drawing units
            Assert.DoesNotContain("ReadDwgFile", t);
            Assert.DoesNotContain("WblockCloneObjects", t);
            Assert.Contains("e.Layer = \"0\"", t);
        }

        // ------------------------------------------------------------ layer states

        [Theory]
        [InlineData("save", "save")]
        [InlineData("Lưu", "save")]
        [InlineData("gọi lại", "restore")]
        [InlineData("RESTORE", "restore")]
        [InlineData("liệt kê", "list")]
        [InlineData("xóa", "delete")]
        public void Layer_state_actions(string a, string want) => Assert.Equal(want, LayerStateLogic.NormalizeAction(a));

        [Fact]
        public void Layer_state_names_and_confirmation()
        {
            Assert.Throws<ToolException>(() => LayerStateLogic.NormalizeAction("rename"));
            Assert.Equal("In ấn", LayerStateLogic.CheckName("  In ấn "));
            Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(""));
            Assert.Throws<ToolException>(() => LayerStateLogic.CheckName("a/b"));
            Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(new string('x', 256)));
            var t = Tools;
            // overwrite (save over the same name) and delete need the card; restore reports the layers it changed
            Assert.Contains("$\"overwrite the existing layer state '{name}'\"", t);
            Assert.Contains("$\"delete the layer state '{name}'\"", t);
            Assert.Contains("lsm.RestoreLayerState(name, ObjectId.Null, 0, StateMask)", t);
            Assert.Contains("[\"changed_layers\"] = changed.Count", t);
            Assert.Equal(7, LayerStateLogic.Saved.Length);
        }

        // ------------------------------------------------------------ the numbers are in one place

        [Fact]
        public void Numbers_the_customer_sees_live_in_F2Numbers()
        {
            var t = Tools;
            // the plugin part takes its sizes from F2Numbers / the Core logic, not from literals
            foreach (var lit in new[] { "2.5", "1.2", "0.85", "25.0", "\"A-DOOR\"", "\"KHUNG\"", "\"KT-" })
                Assert.DoesNotContain(lit, t);
            Assert.Contains("F2Numbers.", t);
        }
    }
}
