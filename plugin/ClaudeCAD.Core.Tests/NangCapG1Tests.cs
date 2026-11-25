using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Nâng cấp đợt G1 (docs/ban-giao/nang-cap-g1.md): create_drawing_set — what a set has on each plan, the sheets and their
    /// order / numbers / titles, the scales, where the views are drawn, the record of the last set and its card.
    /// </summary>
    public class NangCapG1Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        /// <summary>A 12 × 9 m house of two storeys (3.3 m), mm drawing.</summary>
        private static DrawingSetLogic.Request House(int levels = 2) => new DrawingSetLogic.Request
        {
            SolidMin = new double[] { 0, 0, -120 },
            SolidMax = new double[] { 12000, 9000, 3300 * levels },
            Min2d = new double[] { 0, 0 },
            Max2d = new double[] { 12000, 9000 },
            Levels = Enumerable.Range(0, levels).Select(i => i * 3300.0).ToList(),
        };

        private static SetAbilities On(string plan, bool solids = true, bool content2d = true) => DrawingSetLogic.Abilities(plan, solids, content2d);

        [Fact]
        public void A_full_machine_gets_plans_per_storey_four_elevations_two_sections_and_a_perspective_in_that_order()
        {
            var set = DrawingSetLogic.Build(House(), On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            Assert.Empty(set.Missing);
            Assert.Equal("3d", set.PlanSource);
            Assert.Equal(new[] { "MẶT BẰNG TẦNG 1", "MẶT BẰNG TẦNG 2", "MẶT ĐỨNG TRƯỚC", "MẶT ĐỨNG SAU", "MẶT ĐỨNG BÊN TRÁI", "MẶT ĐỨNG BÊN PHẢI", "MẶT CẮT A-A", "MẶT CẮT B-B", "PHỐI CẢNH" },
                set.Sheets.Select(s => s.Title).ToArray());
            Assert.Equal(Enumerable.Range(1, 9).Select(i => "KT-0" + i).ToArray(), set.Sheets.Select(s => s.Number).ToArray());
            Assert.Equal(3300, set.Sheets[1].LevelZ);
            // one storey: just "MẶT BẰNG"
            Assert.Equal("MẶT BẰNG", DrawingSetLogic.Build(House(1), On(PlanLogic.PlanFull), null, PlanLogic.PlanFull).Sheets[0].Title);
            Assert.Equal("KT-10", DrawingSetLogic.SheetNumber("KT", 10));
        }

        [Fact]
        public void Each_plan_gets_only_its_own_parts_and_says_what_is_missing()
        {
            // 2D machine: the 2D plan on a sheet; no views of the solids
            var two = DrawingSetLogic.Build(House(), On(PlanLogic.Plan2D), null, PlanLogic.Plan2D);
            Assert.Equal("2d", two.PlanSource);
            Assert.Single(two.Sheets);
            Assert.Equal(SheetKind.Plan, two.Sheets[0].Kind);
            foreach (var k in new[] { "elevation", "section", "perspective" }) Assert.Contains("3D plan", two.Missing[k]);
            // 3D machine: the views (in model space), no perspective sheet
            var three = DrawingSetLogic.Build(House(), On(PlanLogic.Plan3D), null, PlanLogic.Plan3D);
            Assert.Equal(8, three.Sheets.Count);
            Assert.Contains("2D plan", three.Missing["perspective"]);
            Assert.False(On(PlanLogic.Plan3D).Sheets);
            Assert.False(On(PlanLogic.Plan2D).Views3d);
            // a Full machine in a work mode: the way out is the mode switch, never a plan
            var mode2 = DrawingSetLogic.Build(House(), On(PlanLogic.Plan2D), "2d", PlanLogic.Plan2D);
            Assert.Contains("switch the mode", mode2.Missing["elevation"]);
            Assert.DoesNotContain("plan (this machine", mode2.Missing["elevation"]);
            // the parts are gated by the very tools that make them
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(DrawingSetLogic.SheetTool));
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf(DrawingSetLogic.PdfTool));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(DrawingSetLogic.ViewTool));
            Assert.Equal(ToolGroup.ThreeD, PlanLogic.GroupOf(DrawingSetLogic.CutTool));
            Assert.Equal(ToolGroup.Common, PlanLogic.GroupOf("create_drawing_set"));
        }

        [Fact]
        public void A_drawing_without_solids_gets_its_2d_plan_and_nothing_at_all_is_refused()
        {
            var r = House();
            r.SolidMin = r.SolidMax = null;
            r.Levels.Clear();
            var set = DrawingSetLogic.Build(r, On(PlanLogic.PlanFull, solids: false), null, PlanLogic.PlanFull);
            Assert.Single(set.Sheets);
            Assert.Equal("2d", set.PlanSource);
            Assert.Contains("no 3D solids", set.Missing["elevation"]);
            var none = DrawingSetLogic.Build(r, On(PlanLogic.PlanFull, solids: false, content2d: false), null, PlanLogic.PlanFull);
            Assert.Empty(none.Sheets);
            Assert.Contains("neither", none.Missing["plan"]);
            // 3D machine, no solids: nothing (2D plan needs sheets)
            Assert.Empty(DrawingSetLogic.Build(r, On(PlanLogic.Plan3D, solids: false), null, PlanLogic.Plan3D).Sheets);
        }

        [Fact]
        public void The_user_chooses_kinds_elevations_sections_plan_areas_and_prefix()
        {
            var r = House();
            r.Kinds = new List<string> { "elevation", "section" };
            r.Elevations = new List<string> { "front", "left" };
            r.Sections = new List<(double[], double[], string)> { (new double[] { 6000, -500 }, new double[] { 6000, 9500 }, "MẶT CẮT 1-1") };
            r.Prefix = "KT2";
            var set = DrawingSetLogic.Build(r, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            Assert.Equal(new[] { "MẶT ĐỨNG TRƯỚC", "MẶT ĐỨNG BÊN TRÁI", "MẶT CẮT 1-1" }, set.Sheets.Select(s => s.Title).ToArray());
            Assert.Equal("KT2-01", set.Sheets[0].Number);
            Assert.Equal(9000, set.Sheets[1].W); // left: the depth of the house
            Assert.Equal(9000, set.Sheets[2].W); // the cut runs along y
            // 2D plans drawn side by side
            var r2 = House();
            r2.Kinds = new List<string> { "plan" };
            r2.PlanSource = "2d";
            r2.PlanAreas = new List<(double[], double[], string)> { (new double[] { 0, 0 }, new double[] { 12000, 9000 }, null), (new double[] { 15000, 0 }, new double[] { 27000, 9000 }, "MẶT BẰNG MÁI") };
            var s2 = DrawingSetLogic.Build(r2, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            Assert.Equal(new[] { "MẶT BẰNG TẦNG 1", "MẶT BẰNG MÁI" }, s2.Sheets.Select(s => s.Title).ToArray());
            Assert.Equal(-1, s2.Sheets[0].Level);
            // bad input refused before anything is made
            Assert.Throws<ToolException>(() => DrawingSetLogic.CheckKinds(new[] { "roof" }));
            Assert.Throws<ToolException>(() => DrawingSetLogic.CheckElevations(new[] { "north" }));
            Assert.Throws<ToolException>(() => DrawingSetLogic.CheckPrefix("KT 1"));
            Assert.Throws<ToolException>(() => DrawingSetLogic.CheckPrefix(""));
            var bad = House(); bad.PlanSource = "photo";
            Assert.Throws<ToolException>(() => DrawingSetLogic.Build(bad, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull));
            var many = House();
            many.Sections = Enumerable.Range(0, 30).Select(i => (new double[] { i * 100, 0 }, new double[] { i * 100, 9000 }, (string)null)).ToList();
            Assert.Throws<ToolException>(() => DrawingSetLogic.Build(many, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull));
        }

        [Fact]
        public void Default_sections_cross_the_middle_long_side_first()
        {
            var s = DrawingSetLogic.DefaultSections(new double[] { 0, 0, 0 }, new double[] { 12000, 9000, 3000 });
            Assert.Equal(2, s.Count);
            Assert.Equal(4500, s[0].a[1]); Assert.Equal(4500, s[0].b[1]); // A-A along x (the long side)
            Assert.True(s[0].a[0] < 0 && s[0].b[0] > 12000);
            Assert.Equal(6000, s[1].a[0]); Assert.Equal(6000, s[1].b[0]);
            var tall = DrawingSetLogic.DefaultSections(new double[] { 0, 0, 0 }, new double[] { 6000, 15000, 3000 });
            Assert.Equal(3000, tall[0].a[0]); // along y first
            Assert.Equal("A", DrawingSetLogic.SectionLetter(0));
            Assert.Equal("B", DrawingSetLogic.SectionLetter(1));
        }

        [Fact]
        public void Storeys_come_from_the_wall_bottoms_and_a_lintel_piece_starts_no_storey()
        {
            var walls = new List<(double, double)> { (0, 3180), (3300, 3180), (3300.4, 3180), (2100, 600), (6600, 3180) };
            Assert.Equal(new double[] { 0, 3300, 6600 }, DrawingSetLogic.StoreyLevels(walls, 1).ToArray());
            // a metre drawing: the same house in m
            var m = walls.Select(w => (w.Item1 / 1000, w.Item2 / 1000)).ToList();
            Assert.Equal(new double[] { 0, 3.3, 6.6 }, DrawingSetLogic.StoreyLevels(m, 1000).ToArray());
            Assert.Equal(G1Numbers.MaxLevels, DrawingSetLogic.StoreyLevels(Enumerable.Range(0, 20).Select(i => (i * 3300.0, 3000.0)), 1).Count);
            Assert.True(DrawingSetLogic.IsWallLayer("A-WALL-3D"));
            Assert.True(DrawingSetLogic.IsWallLayer("tuong-3d"));
            Assert.False(DrawingSetLogic.IsWallLayer("A-SLAB-3D"));
        }

        [Fact]
        public void Plans_share_one_scale_elevations_and_sections_another_never_below_1_100()
        {
            var sheet = SheetLogic.Build("A3", null);
            var set = DrawingSetLogic.Build(House(), On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(set, sheet, 1, 0);
            Assert.All(set.Sheets.Where(s => s.Kind != SheetKind.Perspective), s => Assert.Equal(100, s.Scale));
            Assert.Equal(0, set.Sheets.Single(s => s.Kind == SheetKind.Perspective).Scale);
            // a 40 m building does not fit A3 at 1:100: the smallest standard scale that fits
            var big = House();
            big.SolidMax = new double[] { 40000, 9000, 6600 };
            var b = DrawingSetLogic.Build(big, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(b, sheet, 1, 0);
            int n = b.Sheets[0].Scale;
            Assert.True(n > 100 && F2Numbers.StandardScales.Contains(n));
            Assert.True(SheetLogic.Fits(sheet, n, 1, 40000, 9000));
            Assert.All(b.Sheets.Where(s => s.Kind == SheetKind.Plan), s => Assert.Equal(n, s.Scale));
            // the scale given is used for all
            DrawingSetLogic.ChooseScales(b, sheet, 1, 50);
            Assert.All(b.Sheets.Where(s => s.Kind != SheetKind.Perspective), s => Assert.Equal(50, s.Scale));
            // metre drawing: the same choice
            var m = House();
            m.SolidMin = new double[] { 0, 0, 0 }; m.SolidMax = new double[] { 12, 9, 6.6 };
            var ms = DrawingSetLogic.Build(m, On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(ms, sheet, 1000, 0);
            Assert.Equal(100, ms.Sheets[0].Scale);
        }

        [Fact]
        public void The_views_are_drawn_in_a_row_right_of_everything_with_no_neighbour_on_a_sheet()
        {
            var sheet = SheetLogic.Build("A3", null);
            var set = DrawingSetLogic.Build(House(), On(PlanLogic.PlanFull), null, PlanLogic.PlanFull);
            DrawingSetLogic.ChooseScales(set, sheet, 1, 0);
            var views = set.Sheets.Where(s => s.Kind != SheetKind.Perspective).ToList();
            var slots = DrawingSetLogic.Slots(views, sheet, 1, 20000, 0);
            Assert.Equal(views.Count, slots.Count);
            Assert.True(slots[0][0] >= 20000);
            for (int i = 0; i + 1 < views.Count; i++)
            {
                // the window of sheet i (centred on view i, as wide as its viewport shows) ends before view i + 1 starts
                var area = SheetLogic.ModelArea(sheet, views[i].Scale, 1);
                double cx = slots[i][0] + views[i].W / 2;
                Assert.True(cx + area[0] / 2 < slots[i + 1][0], $"view {i + 1} shows on sheet {i}");
                Assert.True(cx - area[0] / 2 >= 20000 - 1e-6);
            }
        }

        [Fact]
        public void The_perspective_fills_its_viewport()
        {
            var corners = DrawingSetLogic.Corners(new double[] { 0, 0, 0 }, new double[] { 12000, 9000, 6600 });
            Assert.Equal(8, corners.Count);
            double s = DrawingSetLogic.FitCustomScale(corners, DrawingSetLogic.PerspectiveDirection, 380, 230);
            var (u, v) = SectionLogic.Frame(DrawingSetLogic.PerspectiveDirection);
            double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
            double w = corners.Max(q => Dot(q, u)) - corners.Min(q => Dot(q, u)), h = corners.Max(q => Dot(q, v)) - corners.Min(q => Dot(q, v));
            Assert.True(w * s <= 380 + 1e-6 && h * s <= 230 + 1e-6);
            Assert.True(Math.Max(w * s / 380, h * s / 230) > G1Numbers.PerspectiveFill - 1e-6);
        }

        [Fact]
        public void The_last_set_is_remembered_and_replacing_it_needs_its_own_card()
        {
            var rec = new DrawingSetLogic.Record { Layouts = { "KT-01", "KT-02" }, Handles = { "2A", "2B" } };
            var back = DrawingSetLogic.Decode(DrawingSetLogic.Encode(rec));
            Assert.Equal(rec.Layouts, back.Layouts);
            Assert.Equal(rec.Handles, back.Handles);
            Assert.Empty(DrawingSetLogic.Decode(null).Layouts);
            // one token for the set, whatever the order / case; another set, another token; never a file's token
            Assert.Equal(DrawingSetLogic.ReplaceToken(new[] { "KT-01", "KT-02" }), DrawingSetLogic.ReplaceToken(new[] { "kt-02", "KT-01" }));
            Assert.NotEqual(DrawingSetLogic.ReplaceToken(new[] { "KT-01" }), DrawingSetLogic.ReplaceToken(new[] { "KT-01", "KT-02" }));
            Assert.Matches("^[0-9A-F]{16}$", DrawingSetLogic.ReplaceToken(new[] { "KT-01" }));
            Assert.NotEqual(F5Confirm.FileToken("KT-01"), DrawingSetLogic.ReplaceToken(new[] { "KT-01" }));
            // the user's own layout with a number of the set is a clash; the last set's are not
            Assert.Equal(new[] { "KT-03" }, DrawingSetLogic.Clashes(new[] { "KT-01", "KT-02", "KT-03" }, new[] { "KT-01", "kt-03", "Layout1" }, rec));
            Assert.Empty(DrawingSetLogic.Clashes(new[] { "KT-01" }, new[] { "KT-01" }, rec));
            Assert.Equal("nha-pho-bo-ho-so.pdf", DrawingSetLogic.PdfName(@"C:\du an\nha-pho.dwg"));
            Assert.Equal("ban-ve-bo-ho-so.pdf", DrawingSetLogic.PdfName(""));
        }

        [Fact]
        public void The_plugin_runs_the_plan_with_the_existing_tools_under_their_rules()
        {
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingSetTools.cs");
            // the parts are made by the tools that exist, in WCS (the set itself reads the UCS)
            foreach (var call in new[] { "ProjectionView(c, sp)", "SectionView(c, sp)", "CreateSheet(ctx, new Params(DrawingSetLogic.SheetTool", "PlotPdf(ctx, new Params(DrawingSetLogic.PdfTool" })
                Assert.Contains(call, t);
            Assert.Contains("using (UcsScope.Suspend())", t);
            // the card before replacing the last set or the PDF; a macro never replaces a set
            Assert.Contains("RequireCard(ctx, p, tool, string.Join(\" and \", what), tokens)", t);
            Assert.Contains("DrawingSetLogic.ReplaceToken(last.Layouts, last.Handles)", t); // Test & sửa G1: the token also covers the views drawn
            Assert.Contains("F5Confirm.FileToken(pdfPath)", t);
            Assert.Contains("MacroRunning == true)\n                throw", t.Replace("\r\n", "\n"));
            // the plan of the machine (with the F1 work mode) decides the parts
            Assert.Contains("ctx.Dispatcher?.Plan", t);
            Assert.Contains("ctx.Dispatcher?.WorkMode", t);
            // a write (read-only mode refuses it), its own undo step via the dispatcher, Ref (reads the UCS)
            Assert.Contains("new ToolDef(\"create_drawing_set\", true, CreateDrawingSet, noTransaction: true)", t);
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("create_drawing_set", null));
            Assert.Contains("[\"create_drawing_set\"]", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            Assert.Contains("RegisterDrawingSet(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
        }
    }
}
