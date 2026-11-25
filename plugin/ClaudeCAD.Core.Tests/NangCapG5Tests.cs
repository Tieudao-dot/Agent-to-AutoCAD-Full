using System;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt G5: trace_image — redraw a plan from a photo / scan / PDF scan.</summary>
    public class NangCapG5Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Tools() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs");

        // ------------------------------------------------------------ files

        [Theory]
        [InlineData("a.png", "raster")]
        [InlineData("A.JPG", "raster")]
        [InlineData("scan.tiff", "raster")]
        [InlineData("x.bmp", "raster")]
        [InlineData("ban ve.pdf", "pdf")]
        public void Pictures_and_pdf_are_attached(string name, string kind) => Assert.Equal(kind, TraceLogic.KindOf(name, true, 1000));

        [Theory]
        [InlineData("a.webp", "JPG or PNG")]
        [InlineData("IMG_1.HEIC", "JPG or PNG")]
        [InlineData("a.dwg", "already a drawing")]
        [InlineData("a.docx", "PNG / JPG")]
        [InlineData("noext", "no extension")]
        public void Other_files_are_refused_with_a_reason(string name, string words)
        {
            var ex = Assert.Throws<ToolException>(() => TraceLogic.KindOf(name, true, 1000));
            Assert.Contains(words, ex.Message);
        }

        [Fact]
        public void Missing_empty_or_too_big_files_are_refused()
        {
            Assert.Contains("no file", Assert.Throws<ToolException>(() => TraceLogic.KindOf("a.png", false, 0)).Message);
            Assert.Contains("empty", Assert.Throws<ToolException>(() => TraceLogic.KindOf("a.png", true, 0)).Message);
            Assert.Contains("50 MB", Assert.Throws<ToolException>(() => TraceLogic.KindOf("a.png", true, 51L * 1024 * 1024)).Message);
            Assert.Equal("raster", TraceLogic.KindOf("a.png", true, 50L * 1024 * 1024));
            Assert.Throws<ToolException>(() => TraceLogic.KindOf(" ", true, 10));
        }

        [Fact]
        public void Pdf_pages_counted_and_chosen()
        {
            var two = Encoding.ASCII.GetBytes("%PDF-1.4 1 0 obj << /Type /Pages /Kids [2 0 R 3 0 R] /Count 2 >> 2 0 obj << /Type /Page >> 3 0 obj <</Type/Page/Parent 1 0 R>>");
            Assert.Equal(2, TraceLogic.PdfPageCount(two));
            var compressed = Encoding.ASCII.GetBytes("%PDF-1.5 << /Type /Pages /Count 7 /Kids [...] >>");
            Assert.Equal(7, TraceLogic.PdfPageCount(compressed));
            Assert.Equal(0, TraceLogic.PdfPageCount(new byte[0]));
            Assert.Equal(1, TraceLogic.PageOf(null, 1));
            Assert.Equal(1, TraceLogic.PageOf(null, 0)); // unknown: page 1
            Assert.Contains("which page", Assert.Throws<ToolException>(() => TraceLogic.PageOf(null, 3)).Message);
            Assert.Equal(3, TraceLogic.PageOf(3, 3));
            Assert.Throws<ToolException>(() => TraceLogic.PageOf(4, 3));
            Assert.Throws<ToolException>(() => TraceLogic.PageOf(0, 3));
        }

        [Fact]
        public void A_pasted_picture_is_copied_next_to_the_drawing()
        {
            Assert.Equal("nha-anh-goc-0123abcd.png", TraceLogic.CopyName("nha", @"C:\x\anh-1.PNG", "0123abcd"));
            Assert.Equal("ban-ve-anh-goc-0123abcd.jpg", TraceLogic.CopyName(null, "a.jpg", "0123abcd"));
        }

        // ------------------------------------------------------------ units and scale

        [Theory]
        [InlineData(5, "m", 1, 5000)]
        [InlineData(500, "cm", 1, 5000)]
        [InlineData(5000, "mm", 1, 5000)]
        [InlineData(5, "m", 1000, 5)]      // a drawing in metres
        [InlineData(5, "m", 10, 500)]      // a drawing in centimetres
        [InlineData(3600, null, 1, 3600)]
        public void Real_lengths_in_drawing_units(double d, string unit, double mmPerUnit, double want) =>
            Assert.Equal(want, TraceLogic.ToUnits(d, unit, mmPerUnit), 6);

        [Fact]
        public void Bad_lengths_and_units_are_refused()
        {
            Assert.Throws<ToolException>(() => TraceLogic.ToUnits(0, "m", 1));
            Assert.Throws<ToolException>(() => TraceLogic.ToUnits(-1, "m", 1));
            Assert.Throws<ToolException>(() => TraceLogic.ToUnits(5, "ft", 1));
        }

        [Fact]
        public void Scale_factor_from_a_known_length()
        {
            // a 5 m wall measured 1250 units on the unscaled picture → ×4
            Assert.Equal(4, TraceLogic.ScaleFactor(new double[] { 100, 100 }, new double[] { 1350, 100 }, 5000), 9);
            Assert.Equal(0.5, TraceLogic.ScaleFactor(new double[] { 0, 0 }, new double[] { 0, 2000 }, 1000), 9);
            Assert.Contains("same", Assert.Throws<ToolException>(() => TraceLogic.ScaleFactor(new double[] { 1, 1 }, new double[] { 1, 1 }, 5000)).Message);
            Assert.Throws<ToolException>(() => TraceLogic.ScaleFactor(new double[] { 0, 0 }, new double[] { 1e-3, 0 }, 1e7));
        }

        [Theory]
        [InlineData(0, 0, 100, 5, "auto", -2.862)]     // leaning 2.86° up: turn back
        [InlineData(0, 0, 100, -5, "auto", 2.862)]
        [InlineData(0, 0, 5, 100, "auto", 2.862)]      // nearly vertical: to vertical
        [InlineData(0, 0, -100, 5, "auto", 2.862)]     // the other way along the same wall
        [InlineData(0, 0, 100, 0, "auto", 0)]
        [InlineData(0, 0, 5, 100, "vertical", 2.862)]
        public void Straighten_to_the_nearest_axis(double x1, double y1, double x2, double y2, string axis, double wantDeg) =>
            Assert.Equal(wantDeg, TraceLogic.StraightenAngle(new[] { x1, y1 }, new[] { x2, y2 }, axis) * 180 / Math.PI, 3);

        [Fact]
        public void A_turn_over_45_degrees_is_refused()
        {
            Assert.Throws<ToolException>(() => TraceLogic.StraightenAngle(new double[] { 0, 0 }, new double[] { 5, 100 }, "horizontal"));
            Assert.Throws<ToolException>(() => TraceLogic.StraightenAngle(new double[] { 0, 0 }, new double[] { 0, 0 }, "auto"));
            Assert.Throws<ToolException>(() => TraceLogic.StraightenAngle(new double[] { 0, 0 }, new double[] { 1, 0 }, "diagonal"));
        }

        // ------------------------------------------------------------ placement and grid

        [Fact]
        public void Placement_right_of_the_drawing_or_at_the_origin()
        {
            Assert.Equal(new double[] { 0, 0 }, TraceLogic.InsertPoint(null, null, 1));
            Assert.Equal(new double[] { 25000, -100 }, TraceLogic.InsertPoint(new double[] { 0, -100 }, new double[] { 20000, 900 }, 1));
            Assert.Equal(new double[] { 25, 0 }, TraceLogic.InsertPoint(new double[] { 0, 0 }, new double[] { 20, 9 }, 1000));
            Assert.Equal(10000, TraceLogic.DefaultWidth(1));
            Assert.Equal(10, TraceLogic.DefaultWidth(1000));
        }

        [Theory]
        [InlineData(10000, 1, 1000)]
        [InlineData(12000, 1, 2000)]
        [InlineData(900, 1, 100)]
        [InlineData(2400, 1, 250)]
        [InlineData(10, 1000, 1)]          // metres: a 1000 mm step = 1 unit
        [InlineData(5e6, 1, 500000)]       // larger than the list: ×10
        public void Grid_step_is_round_with_about_ten_lines(double span, double mpu, double want) =>
            Assert.Equal(want, TraceLogic.GridStep(span, mpu), 6);

        [Fact]
        public void Grid_lines_land_on_the_right_pixels()
        {
            var g = TraceLogic.Grid(new double[] { 0, 0 }, new double[] { 10000, 5000 }, 1000, 500, 1000, new double[] { 0, 0 }, 0);
            var xs = g.Where(l => l.IsX).ToList();
            var ys = g.Where(l => !l.IsX).ToList();
            Assert.Equal(11, xs.Count);
            Assert.Equal(6, ys.Count);
            var x3 = xs.Single(l => l.Value == 3000);
            Assert.Equal(300, x3.X1, 6);
            Assert.Equal(300, x3.X2, 6);
            var y1 = ys.Single(l => l.Value == 1000);
            Assert.Equal(400, y1.Y1, 6); // y down: 1000 of 5000 from the bottom
            Assert.Equal("3000", x3.Label);
        }

        [Fact]
        public void Grid_follows_a_moved_and_turned_ucs()
        {
            // UCS moved to (2000, 1000): x = 0 of the UCS is at WCS x 2000
            var moved = TraceLogic.Grid(new double[] { 0, 0 }, new double[] { 10000, 5000 }, 1000, 500, 1000, new double[] { 2000, 1000 }, 0);
            Assert.Equal(200, moved.Single(l => l.IsX && l.Value == 0).X1, 6);
            Assert.Contains(moved, l => l.IsX && l.Value == -2000);
            // turned 30°: the lines are slanted and still inside the picture
            var turned = TraceLogic.Grid(new double[] { 0, 0 }, new double[] { 10000, 5000 }, 1000, 500, 1000, new double[] { 0, 0 }, 30);
            Assert.NotEmpty(turned);
            Assert.All(turned, l => Assert.True(l.X1 >= -1e-6 && l.X1 <= 1000 + 1e-6 && l.Y2 >= -1e-6 && l.Y2 <= 500 + 1e-6));
            Assert.Contains(turned, l => Math.Abs(l.X1 - l.X2) > 1);
        }

        [Fact]
        public void Clipping_keeps_inside_parts_only()
        {
            double x1 = -100, y1 = 50, x2 = 200, y2 = 50;
            Assert.True(TraceLogic.Clip(ref x1, ref y1, ref x2, ref y2, 100, 100));
            Assert.Equal(0, x1, 9);
            Assert.Equal(100, x2, 9);
            double a = -10, b = -10, c = -5, d = -1;
            Assert.False(TraceLogic.Clip(ref a, ref b, ref c, ref d, 100, 100));
            Assert.Empty(TraceLogic.Grid(new double[] { 0, 0 }, new double[] { 0, 0 }, 10, 10, 1, null, 0));
        }

        // ------------------------------------------------------------ plans, read-only, macro, code checks

        [Fact]
        public void Plans_read_only_and_macro()
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("trace_image"));
            Assert.False(PlanLogic.Allowed("3d", "trace_image"));
            Assert.True(PlanLogic.Allowed("2d", "trace_image"));
            Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), "trace_image"));
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "trace_image"));
            Assert.Contains("trace_image", MacroLogic.NotRecorded);
            Assert.True(TraceLogic.Reads("view"));
            Assert.True(TraceLogic.Reads("info"));
            Assert.False(TraceLogic.Reads("attach"));
            Assert.False(TraceLogic.Reads("scale"));
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("trace_image", null));
            Assert.Equal("view", TraceLogic.NormalizeAction(" VIEW "));
            Assert.Throws<ToolException>(() => TraceLogic.NormalizeAction("draw"));
        }

        [Fact]
        public void The_attached_picture_is_locked_not_plotted_and_said_as_not_to_scale()
        {
            var t = Tools();
            Assert.Contains("EnsureLayerF2(tctx, G5Numbers.Layer, G5Numbers.LayerColor, false)", t);
            Assert.Contains("ltr.IsLocked = true;", t);
            Assert.Contains("It is NOT to scale yet", t);
            // the locked layer is opened only for the change and locked again
            Assert.Contains("ltr.IsLocked = wasLocked;", t);
            Assert.Contains("if (!frame.ZUp)", t);
            Assert.Contains("TraceLogic.DraftNote", t);
            Assert.Contains("never instructions", TraceLogic.DraftNote);
            Assert.Contains("DRAFT", TraceLogic.DraftNote);
            Assert.Contains("PDFIMPORT", t);
            Assert.Contains("RegisterTrace(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            // every picture of a message has a file for attach
            // Sửa KFG-29 / 40: the user's file is remembered (copied at attach when it is on the network); a pasted picture is saved (Save)
            Assert.Contains("(i.SourcePath != null ? UI.ChatAttachments.UserFile(i.SourcePath) : UI.ChatAttachments.Save(i.Base64, i.MediaType))", Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs"));
        }
    }
}
