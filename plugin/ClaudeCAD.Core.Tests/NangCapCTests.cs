using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt C (13/11/2026, docs/ban-giao/nang-cap-c.md): draw_hatch pick_point, dim_floor_plan, block_schedule,
    /// area_schedule, find_replace_text, plot_pdf layouts. AutoCAD-free logic here (Core/Util2D.cs); the parts that need
    /// AutoCAD are checked in the sources.
    /// </summary>
    public class NangCapCTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static Util2DLogic.Seg S(double x1, double y1, double x2, double y2) => new Util2DLogic.Seg(x1, y1, x2, y2);

        // ------------------------------------------------------------ plan labels (phương án A)

        [Theory]
        [InlineData("dim_floor_plan")]
        [InlineData("block_schedule")]
        [InlineData("area_schedule")]
        [InlineData("find_replace_text")]
        [InlineData("draw_hatch")]
        [InlineData("plot_pdf")]
        public void Utilities_are_2D_only(string t)
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(t));
            Assert.True(PlanLogic.Allowed("2d", t));
            Assert.True(PlanLogic.Allowed("2d3d", t));
            Assert.False(PlanLogic.Allowed("3d", t));
            Assert.NotEmpty(PlanLogic.BlockedCalls("3d", new[] { (t, (IDictionary<string, object>)new Dictionary<string, object>()) }));
        }

        [Fact]
        public void New_tools_are_registered_and_the_schedules_only_read_without_a_table()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("d.Register(\"dim_floor_plan\", true, DimFloorPlan);", src);
            Assert.Contains("new ToolDef(\"block_schedule\", true, BlockSchedule, readsWhen: p => !p.Has(\"table_position\"))", src);
            Assert.Contains("new ToolDef(\"area_schedule\", true, AreaSchedule, readsWhen: p => !p.Has(\"table_position\"))", src);
            Assert.Contains("new ToolDef(\"find_replace_text\", true, FindReplaceText, readsWhen: p => !p.Has(\"handles\"))", src);
            Assert.Contains("RegisterUtil2D(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }

        // ------------------------------------------------------------ hatch by a picked point

        [Fact]
        public void Outer_loop_is_the_largest()
        {
            Assert.Equal(1, Util2DLogic.OuterLoopIndex(new[] { 10.0, -400, 30 }));
            Assert.Equal(0, Util2DLogic.OuterLoopIndex(new[] { 5.0 }));
            Assert.Equal(-1, Util2DLogic.OuterLoopIndex(new double[0]));
            Assert.Contains("no closed boundary", Util2DLogic.NoBoundaryMessage(new[] { 1.5, 2.0 }));
        }

        [Fact]
        public void Hatch_pick_point_traces_and_cleans_up()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawTools.cs");
            Assert.Contains("ctx.Ed.TraceBoundary(", src);
            Assert.Contains("Util2DLogic.NoBoundaryMessage(seed)", src);
            Assert.Contains("o.Dispose()", src); // non-database objects never reach the finalizer
            Assert.Contains("hatch.Associative = keep && p.Bool(\"associative\", true);", src);
            Assert.Contains("if (!keep) foreach (var b in boundaryIds)", src);
            // the old forms are untouched: boundary_handles first, then boundary_points
            Assert.Contains("bool pick = handles == null && p.Has(\"pick_point\");", src);
        }

        // ------------------------------------------------------------ dim_floor_plan

        /// <summary>10 × 6 m, walls 200 thick (outer + inner outline), a 900 door in the bottom wall at x 1000–1900.</summary>
        private static List<Util2DLogic.Seg> Plan()
        {
            return new List<Util2DLogic.Seg>
            {
                // outer face: bottom split by the door
                S(0, 0, 1000, 0), S(1900, 0, 10000, 0), S(10000, 0, 10000, 6000), S(10000, 6000, 0, 6000), S(0, 6000, 0, 0),
                // inner face
                S(200, 200, 1000, 200), S(1900, 200, 9800, 200), S(9800, 200, 9800, 5800), S(9800, 5800, 200, 5800), S(200, 5800, 200, 200),
                // door jambs
                S(1000, 0, 1000, 200), S(1900, 0, 1900, 200),
                // an inner wall (hidden from every side)
                S(5000, 200, 5000, 5800), S(5200, 200, 5200, 5800),
            };
        }

        [Fact]
        public void Bottom_chain_has_corners_and_door_then_overall()
        {
            var dims = Util2DLogic.FloorPlanDims(Plan(), null, new[] { "bottom" }, 800, 600);
            var detail = dims.Where(d => d.Row == "detail").ToList();
            Assert.Equal(new[] { 0.0, 1000, 1900 }, detail.Select(d => d.P1[0]).ToArray());
            Assert.Equal(new[] { 1000.0, 1900, 10000 }, detail.Select(d => d.P2[0]).ToArray());
            Assert.All(detail, d => { Assert.Equal(-800, d.DimLine[1], 6); Assert.False(d.Vertical); Assert.Equal(0, d.P1[1], 6); });
            var overall = Assert.Single(dims, d => d.Row == "overall");
            Assert.Equal(0, overall.P1[0], 6);
            Assert.Equal(10000, overall.P2[0], 6);
            Assert.Equal(-1400, overall.DimLine[1], 6);
        }

        [Fact]
        public void A_side_without_openings_keeps_only_the_overall_row()
        {
            var dims = Util2DLogic.FloorPlanDims(Plan(), null, new[] { "top", "left", "right" }, 800, 600);
            Assert.All(dims, d => Assert.Equal("overall", d.Row));
            var top = Assert.Single(dims, d => d.Side == "top");
            Assert.Equal(6800, top.DimLine[1], 6);
            var left = Assert.Single(dims, d => d.Side == "left");
            Assert.True(left.Vertical);
            Assert.Equal(-800, left.DimLine[0], 6);
            Assert.Equal(new[] { 0.0, 6000 }, new[] { left.P1[1], left.P2[1] });
            Assert.Equal(10800, dims.Single(d => d.Side == "right").DimLine[0], 6);
        }

        [Fact]
        public void Axes_make_their_own_row_between_detail_and_overall()
        {
            var axes = new List<Util2DLogic.Seg> { S(100, -300, 100, 6300), S(5100, -300, 5100, 6300), S(9900, -300, 9900, 6300), S(-300, 100, 10300, 100) };
            var dims = Util2DLogic.FloorPlanDims(Plan(), axes, new[] { "bottom", "left" }, 800, 600);
            var ax = dims.Where(d => d.Side == "bottom" && d.Row == "axes").ToList();
            Assert.Equal(2, ax.Count);
            Assert.Equal(new[] { 100.0, 5100 }, ax.Select(d => d.P1[0]).ToArray());
            Assert.Equal(-300, ax[0].P1[1], 6); // the axis end toward the side
            Assert.Equal(-1400, ax[0].DimLine[1], 6);
            Assert.Equal(-2000, dims.Single(d => d.Side == "bottom" && d.Row == "overall").DimLine[1], 6);
            // only one horizontal axis: no row on the left
            Assert.DoesNotContain(dims, d => d.Side == "left" && d.Row == "axes");
        }

        [Fact]
        public void Hidden_and_visible_vertices()
        {
            var w = Plan();
            Assert.True(Util2DLogic.Visible(new[] { 0.0, 0 }, w, "bottom"));
            Assert.True(Util2DLogic.Visible(new[] { 0.0, 6000 }, w, "bottom")); // along the left wall line
            Assert.False(Util2DLogic.Visible(new[] { 200.0, 200 }, w, "bottom")); // behind the outer face
            Assert.True(Util2DLogic.Visible(new[] { 1000.0, 200 }, w, "bottom")); // door jamb
            Assert.False(Util2DLogic.Visible(new[] { 5000.0, 5800 }, w, "top"));
            Assert.False(Util2DLogic.Visible(new[] { 5000.0, 5800 }, w, "left"));
        }

        [Fact]
        public void Floor_plan_errors_and_default_offset()
        {
            Assert.Throws<ToolException>(() => Util2DLogic.FloorPlanDims(new List<Util2DLogic.Seg>(), null, null, 800, 800));
            Assert.Throws<ToolException>(() => Util2DLogic.FloorPlanDims(Plan(), null, new[] { "front" }, 800, 800));
            Assert.Throws<ToolException>(() => Util2DLogic.FloorPlanDims(Plan(), null, null, 0, 800));
            Assert.Equal(7, Util2DLogic.FloorPlanDims(Plan(), null, null, 800, 600).Count); // bottom 3 + 1, top, left, right 1 each
            Assert.Equal(800, Util2DLogic.DefaultDimOffset(100, 10000), 6);
            Assert.Equal(500, Util2DLogic.DefaultDimOffset(1, 10000), 6);
            Assert.Equal(8, Util2DLogic.DefaultDimOffset(0, 0), 6);
        }

        // ------------------------------------------------------------ block_schedule

        private static Util2DLogic.BlockItem B(string name, string code = null)
        {
            var b = new Util2DLogic.BlockItem { Name = name, Layer = "0" };
            if (code != null) b.Attributes["MA"] = code;
            return b;
        }

        [Fact]
        public void Blocks_counted_by_name_and_by_attribute()
        {
            var items = new[] { B("CUA", "D10"), B("CUA", "D2"), B("CUA", "D2"), B("BAN"), B("CUA") };
            var byName = Util2DLogic.CountBlocks(items, null);
            Assert.Equal(new[] { "BAN", "CUA" }, byName.Select(r => r.Name).ToArray());
            Assert.Equal(new[] { 1, 4 }, byName.Select(r => r.Count).ToArray());
            Assert.All(byName, r => Assert.Null(r.Value));

            var byCode = Util2DLogic.CountBlocks(items, "ma"); // tag in any case
            Assert.Equal(new[] { "BAN|", "CUA|", "CUA|D2", "CUA|D10" }, byCode.Select(r => r.Name + "|" + r.Value).ToArray());
            Assert.Equal(new[] { 1, 1, 2, 1 }, byCode.Select(r => r.Count).ToArray());

            var table = Util2DLogic.BlockTable(byCode, "ma");
            Assert.Equal(new object[] { "STT", "Tên block", "MA", "Số lượng" }, table[0]);
            Assert.Equal(new object[] { "2", "CUA", "(trống)", "1" }, table[2]);
            Assert.Equal(new object[] { "", "Tổng", "", "5" }, table[table.Count - 1]);
            Assert.Equal(new object[] { "STT", "Tên block", "Số lượng" }, Util2DLogic.BlockTable(byName, null)[0]);
        }

        [Fact]
        public void Natural_order_of_codes()
        {
            var c = Util2DLogic.NaturalComparer.Instance;
            Assert.True(c.Compare("D2", "D10") < 0);
            Assert.True(c.Compare("d2", "D3") < 0);
            Assert.True(c.Compare("S01", "S1") == 0);
            Assert.True(c.Compare("A", "AB") < 0);
        }

        [Fact]
        public void Block_schedule_skips_xrefs_layouts_and_anonymous_blocks()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("btr.IsFromExternalReference || btr.IsLayout || btr.Name.StartsWith(\"*\"", src);
            Assert.Contains("br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord", src);
            Assert.Contains("!(e is BlockReference br) || e is Table", src);
        }

        // ------------------------------------------------------------ area_schedule

        private static Util2DLogic.AreaItem R(string h, string layer, double x0, double y0, double x1, double y1, string type = "LWPOLYLINE") =>
            new Util2DLogic.AreaItem
            {
                Handle = h, Layer = layer, Type = type, Area = (x1 - x0) * (y1 - y0),
                Outline = new List<double[]> { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } },
            };

        [Fact]
        public void Areas_by_layer_and_total()
        {
            var a = new List<Util2DLogic.AreaItem> { R("1", "A-AREA", 0, 0, 4000, 3000), R("2", "A-AREA", 4000, 0, 7000, 3000), R("3", "WC", 0, 3000, 2000, 4500) };
            var rows = Util2DLogic.AreaRows(a, null, "layer");
            Assert.Equal(new[] { "A-AREA", "WC" }, rows.Select(r => r.Name).ToArray());
            Assert.Equal(21e6, rows[0].Area, 3);
            Assert.Equal(2, rows[0].Count);
            var t = Util2DLogic.AreaTable(rows, "layer", 2);
            Assert.Equal(new object[] { "STT", "Layer", "Số vùng", "Diện tích (m²)" }, t[0]);
            Assert.Equal(new object[] { "1", "A-AREA", "2", "21,00" }, t[1]);
            Assert.Equal(new object[] { "", "Tổng", "3", "24,00" }, t[3]);
            Assert.Throws<ToolException>(() => Util2DLogic.AreaRows(a, null, "floor"));
        }

        [Fact]
        public void Rooms_are_named_by_the_text_inside_the_innermost_area()
        {
            var a = new List<Util2DLogic.AreaItem> { R("H", "A-AREA", 0, 0, 10000, 6000), R("K", "A-AREA", 0, 0, 4000, 3000), R("L", "A-AREA", 6000, 0, 10000, 3000) };
            var texts = new List<Util2DLogic.TextItem>
            {
                new Util2DLogic.TextItem { Text = "P.KHÁCH", Position = new[] { 2000.0, 1500 } },
                new Util2DLogic.TextItem { Text = "S=12m²", Position = new[] { 2000.0, 1000 } },
                new Util2DLogic.TextItem { Text = "SẢNH", Position = new[] { 5000.0, 4500 } },
                new Util2DLogic.TextItem { Text = "ngoài", Position = new[] { 20000.0, 0 } },
            };
            var names = Util2DLogic.RoomNames(a, texts);
            Assert.Equal("P.KHÁCH S=12m²", names["K"]);
            Assert.Equal("SẢNH", names["H"]);
            Assert.Equal("", names["L"]);
            var rows = Util2DLogic.AreaRows(a, texts, "room");
            Assert.Equal(new[] { "P.KHÁCH S=12m²", "SẢNH", "" }, rows.Select(r => r.Name).ToArray()); // unnamed last
            var t = Util2DLogic.AreaTable(rows, "room", 1);
            Assert.Equal(new object[] { "3", "(chưa có tên)", "A-AREA", "12,0" }, t[3]);
            Assert.Equal("84,0", t[4][3]); // nested areas are each counted (the hall outline too)
            Assert.Equal(new[] { "H" }, Util2DLogic.Containers(a).ToArray()); // reported, so Claude asks before counting it
        }

        [Fact]
        public void A_table_with_nested_areas_waits_for_the_user()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("outer.Count > 0 && p.Has(\"table_position\") && !p.Bool(\"allow_nested\", false)", src);
            Assert.True(src.IndexOf("allow_nested", StringComparison.Ordinal) < src.IndexOf("InsertSchedule(ctx, p, Util2DLogic.AreaTable", StringComparison.Ordinal));
        }

        [Fact]
        public void A_hatch_on_its_room_polyline_counts_once()
        {
            var skipped = new List<string>();
            var items = new List<Util2DLogic.AreaItem> { R("P1", "A-AREA", 0, 0, 4000, 3000), R("P2", "A-AREA", 5000, 0, 9000, 3000), R("H1", "A-AREA", 0, 0, 4000, 3000, "HATCH") };
            var kept = Util2DLogic.Dedupe(items, skipped);
            Assert.Equal(new[] { "P1", "P2" }, kept.Select(k => k.Handle).ToArray()); // same area elsewhere (P2) is a different room
            Assert.Single(skipped);
            Assert.Contains("H1", skipped[0]);
        }

        [Fact]
        public void Point_in_polygon_and_text_cleanup()
        {
            var sq = new List<double[]> { new[] { 0.0, 0 }, new[] { 10.0, 0 }, new[] { 10.0, 10 }, new[] { 0.0, 10 } };
            Assert.True(Util2DLogic.Inside(sq, new[] { 5.0, 5 }));
            Assert.True(Util2DLogic.Inside(sq, new[] { 10.0, 5 })); // on the edge
            Assert.False(Util2DLogic.Inside(sq, new[] { 11.0, 5 }));
            var l = new List<double[]> { new[] { 0.0, 0 }, new[] { 10.0, 0 }, new[] { 10.0, 4 }, new[] { 4.0, 4 }, new[] { 4.0, 10 }, new[] { 0.0, 10 } };
            Assert.False(Util2DLogic.Inside(l, new[] { 7.0, 7 }));
            Assert.Equal("P. NGỦ 1", Util2DLogic.CleanText("P.\\PNGỦ  1 "));
            Assert.Equal("12,35", Util2DLogic.M2(12345678, 2));
        }

        // ------------------------------------------------------------ find_replace_text

        [Fact]
        public void Replace_counts_and_respects_case_and_whole_words()
        {
            Assert.Equal("PHÒNG NGỦ 1, PHÒNG NGỦ 2", Util2DLogic.Replace("P.NGU 1, p.ngu 2", "P.NGU", "PHÒNG NGỦ", false, false, out int n));
            Assert.Equal(2, n);
            Assert.Equal("P.NGU 1, PHÒNG NGỦ 2", Util2DLogic.Replace("P.NGU 1, p.ngu 2", "p.ngu", "PHÒNG NGỦ", true, false, out n));
            Assert.Equal(1, n);
            Assert.Equal("X D10 X", Util2DLogic.Replace("D1 D10 D1", "D1", "X", false, true, out n));
            Assert.Equal(2, n);
            Assert.Equal("aaa", Util2DLogic.Replace("aaa", "b", "c", false, false, out n));
            Assert.Equal(0, n);
            Assert.Equal("bb", Util2DLogic.Replace("aaaa", "aa", "b", false, false, out n)); // no overlap
            Assert.Equal(2, n);
            Assert.Equal("1", Util2DLogic.Replace("TẦNG 1", "TẦNG ", "", false, false, out n)); // "" deletes
            Assert.Equal("aXa", Util2DLogic.Replace("aXa", "x", "Y", true, false, out n));
        }

        [Fact]
        public void Mtext_codes_are_never_cut_and_only_approved_handles_change()
        {
            Assert.True(Util2DLogic.SafeForMText("P.NGU"));
            Assert.False(Util2DLogic.SafeForMText("\\P"));
            Assert.False(Util2DLogic.SafeForMText("{"));
            var approved = SolidLogic.ConfirmedHandles(new List<object> { "a1", "B2" });
            Assert.Empty(Util2DLogic.NotApproved(new[] { "A1", "b2" }, approved));
            Assert.Equal(new[] { "C3" }, Util2DLogic.NotApproved(new[] { "A1", "c3", "C3" }, approved).ToArray());
            Assert.Equal(new[] { "A1" }, Util2DLogic.NotApproved(new[] { "A1" }, SolidLogic.ConfirmedHandles(true)).ToArray());
            Assert.StartsWith("Confirmation required", Util2DLogic.ReplaceCardMessage(3));
        }

        [Fact]
        public void Find_replace_plugin_side_rules()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            // the preview lists match_handles (never "handles": the assistant would take them as created objects)
            Assert.Contains("[\"match_handles\"]", src);
            Assert.DoesNotContain("res[\"handles\"] = objs", src);
            // replacing checks the card first, before anything changes, and locked layers
            int check = src.IndexOf("Util2DLogic.NotApproved(hs, approved)", StringComparison.Ordinal);
            int write = src.IndexOf("WriteText(w, hit);", StringComparison.Ordinal);
            Assert.True(check > 0 && write > check);
            Assert.Contains("var lockedHere = Util2DLogic.LockedHits(", src); // KT2-02 + KT2-32: every text checked before any write, with handle + layer
            // a macro replay has no card: Macros.cs strips user_confirmed
            Assert.Contains("copy.Remove(SolidLogic.UserConfirmedParam)", Src("plugin", "ClaudeCAD.Plugin", "Core", "Macros.cs"));
        }

        // ------------------------------------------------------------ plot_pdf layouts

        [Theory]
        [InlineData("A1: Mặt bằng / tầng 1", "A1_ Mặt bằng _ tầng 1")]
        [InlineData("CON", "_CON")]
        [InlineData("nul.txt", "_nul.txt")]
        [InlineData("Bản vẽ...", "Bản vẽ")]
        [InlineData("  ", "layout")]
        [InlineData("a*b?c\"d<e>f|g", "a_b_c_d_e_f_g")]
        public void File_names_are_windows_safe(string name, string expected) =>
            Assert.Equal(expected, Util2DLogic.SafeFileName(name));

        [Fact]
        public void Layouts_resolved_in_order_or_all_in_tab_order()
        {
            var tabs = new[] { "Model", "A1", "A2", "MB tầng 2" };
            Assert.Equal(new[] { "A1", "A2", "MB tầng 2" }, Util2DLogic.ResolveLayouts(new[] { "all" }, tabs));
            Assert.Equal(new[] { "A1", "A2", "MB tầng 2" }, Util2DLogic.ResolveLayouts(new[] { "*" }, tabs));
            Assert.Equal(new[] { "A2", "A1" }, Util2DLogic.ResolveLayouts(new[] { "a2", "A1", "A2" }, tabs));
            Assert.Equal(new[] { "Model", "A1" }, Util2DLogic.ResolveLayouts(new[] { "model", "A1" }, tabs));
            var ex = Assert.Throws<ToolException>(() => Util2DLogic.ResolveLayouts(new[] { "A1", "A9" }, tabs));
            Assert.Contains("A9", ex.Message);
            Assert.Throws<ToolException>(() => Util2DLogic.ResolveLayouts(new[] { "all" }, new[] { "Model" }));
            Assert.Throws<ToolException>(() => Util2DLogic.ResolveLayouts(new string[0], tabs));
            Assert.Equal("Nha-3-layouts.pdf", Util2DLogic.DefaultPdfName("Nha", new[] { "A1", "A2", "A3" }));
            Assert.Equal("Nha-A1_ MB.pdf", Util2DLogic.DefaultPdfName("Nha", new[] { "A1: MB" }));
        }

        [Fact]
        public void Multi_page_plot_uses_one_document()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs");
            Assert.Contains("Util2DLogic.ResolveLayouts(p.Strings(\"layouts\", 1), LayoutTabOrder(ctx))", src);
            Assert.Contains("Util2DLogic.DefaultPdfName(", src);
            Assert.Equal(1, CountOf(src, "pe.BeginDocument("));
            Assert.Contains("pe.BeginPage(ppi, infos[i], i == infos.Count - 1, null);", src);
            Assert.Contains("foreach (var ps in settings) try { ps.Dispose(); } catch { }", src);
        }

        private static int CountOf(string s, string what)
        {
            int n = 0;
            for (int i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + 1, StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
