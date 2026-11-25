using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt G5 (25/11/2026, docs/ban-giao/nang-cap-g5-test.md): UCS points of trace_image, the look drawn from the
    /// picture file, the file kind by its content, and the background picture in check_drawing / create_drawing_set.
    /// </summary>
    public class NangCapG5TestSuaTests
    {
        private static string Plugin(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray())));

        // ------------------------------------------------------------ TG5-1: points in the user's UCS

        [Fact]
        public void Trace_points_are_read_and_given_back_in_the_ucs()
        {
            var t = Plugin("Drawing", "TraceTools.cs");
            Assert.Contains("var q1 = p.Point(\"p1\");", t);
            Assert.Contains("var q2 = p.Point(\"p2\");", t);
            Assert.Contains("var q = p.Point(\"insert\");", t);
            Assert.DoesNotContain("new List<object> { Acad.R(", t); // every point out through Acad.J (UCS)
            Assert.Contains("UcsScope.ConvertInput(\"p1\"", t);       // the four corners of a look in the UCS
            Assert.Equal(UcsMode.Ref, UcsLogic.ModeOf("trace_image", new Dictionary<string, object> { ["action"] = "scale" }));
            Assert.Equal(ParamKind.Point, UcsLogic.KindOf("trace_image", "p1"));
            Assert.Equal(ParamKind.Point, UcsLogic.KindOf("trace_image", "insert"));
            // a UCS turned 30°: the WCS corners of a UCS rectangle make a larger box, the dispatcher boxes min / max back
            var f = new UcsFrame(new double[] { 1000, 500, 0 }, new[] { Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0 }, new[] { -Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6), 0 });
            var w = new[] { f.ToWcs(new double[] { 0, 0, 0 }), f.ToWcs(new double[] { 2000, 0, 0 }), f.ToWcs(new double[] { 2000, 1000, 0 }), f.ToWcs(new double[] { 0, 1000, 0 }) };
            Assert.True(w.Max(p => p[0]) - w.Min(p => p[0]) > 2000);
        }

        // ------------------------------------------------------------ TG5-2: the look drawn from the picture file

        [Fact]
        public void The_picture_file_lands_on_its_place_in_the_look()
        {
            // a 400 × 200 px picture 8000 × 4000 wide at (1000, 2000), looked at in the window (0, 0)–(10000, 10000) of 1000 × 1000 px
            var m = TraceLogic.PictureToLook(new double[] { 1000, 2000 }, new double[] { 8000, 0 }, new double[] { 0, 4000 }, 400, 200, new double[] { 0, 0 }, new double[] { 10000, 10000 }, 1000, 1000);
            var tl = TraceLogic.Apply(m, 0, 0);     // top-left of the file = (1000, 6000) → px (100, 400)
            var br = TraceLogic.Apply(m, 400, 200); // bottom-right = (9000, 2000) → px (900, 800)
            Assert.Equal(100, tl[0], 6); Assert.Equal(400, tl[1], 6);
            Assert.Equal(900, br[0], 6); Assert.Equal(800, br[1], 6);
            // turned 90° (straighten): the file's top edge runs down the look
            var r = TraceLogic.PictureToLook(new double[] { 5000, 1000 }, new double[] { 0, 8000 }, new double[] { -4000, 0 }, 400, 200, new double[] { 0, 0 }, new double[] { 10000, 10000 }, 1000, 1000);
            var a = TraceLogic.Apply(r, 0, 0);   // o + v = (1000, 1000) → (100, 900)
            var b = TraceLogic.Apply(r, 400, 0); // o + u + v = (1000, 9000) → (100, 100)
            Assert.Equal(100, a[0], 6); Assert.Equal(900, a[1], 6);
            Assert.Equal(100, b[0], 6); Assert.Equal(100, b[1], 6);
        }

        [Fact]
        public void Decode_width_follows_the_part_shown_and_stays_bounded()
        {
            Assert.Equal(1280, TraceLogic.DecodeWidth(4000, 1, 1024));     // the whole picture: about the look's size
            Assert.Equal(4000, TraceLogic.DecodeWidth(4000, 0.1, 1024));   // a tenth of it: the full file
            Assert.Equal(TraceLogic.MaxDecodePx, TraceLogic.DecodeWidth(20000, 0.05, 1568)); // a huge scan: capped
            Assert.Equal(300, TraceLogic.DecodeWidth(300, 1, 1024));       // never more than the file
        }

        [Fact]
        public void Background_is_found_cleared_and_an_empty_look_is_blank()
        {
            int w = 20, h = 10;
            var px = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i += 4) { px[i] = 33; px[i + 1] = 40; px[i + 2] = 50; px[i + 3] = 255; }
            var bg = TraceLogic.Background(px, w, h);
            Assert.Equal(new[] { 33, 40, 50 }, bg);
            Assert.Equal(0, TraceLogic.DrawnShare(px, w, h, bg, 2, 2, 18, 8));
            // a drawn line inside the picture's box
            for (int x = 4; x < 16; x++) { int i = (5 * w + x) * 4; px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; }
            Assert.True(TraceLogic.DrawnShare(px, w, h, bg, 2, 2, 18, 8) > TraceLogic.BlankShare);
            TraceLogic.ClearBackground(px, bg);
            Assert.Equal(0, px[3]);                    // background: transparent
            Assert.Equal(255, px[(5 * w + 4) * 4 + 3]); // the line stays
        }

        [Fact]
        public void The_look_is_made_from_the_file_and_says_where_from()
        {
            var t = Plugin("Drawing", "TraceTools.cs");
            Assert.Contains("var look = ComposeLook(cap, picFile, po, pu, pv, picW, picH, min, max);", t);
            Assert.Contains("[\"picture_from\"] = look.From", t);
            Assert.Contains("[\"autocad_drew_picture\"] = look.AutocadDrew", t);
            Assert.Contains("res[\"picture_visible\"] = false;", t);
            Assert.Contains("Do not trace from it and do not guess", t);
            // the file is opened shared (AutoCAD keeps it open) and decoded to a bounded width
            Assert.Contains("FileShare.ReadWrite", t);
            Assert.Contains("bi.DecodePixelWidth = dw;", t);
        }

        // ------------------------------------------------------------ TG5-3: the file kind by its content

        private static byte[] B(params int[] b) => b.Select(x => (byte)x).ToArray();

        private static readonly byte[] Heic = B(0, 0, 0, 0x18).Concat(Encoding.ASCII.GetBytes("ftypheic")).Concat(B(0, 0, 0, 0)).ToArray();

        [Fact]
        public void A_file_is_what_its_content_says()
        {
            Assert.Equal("raster", TraceLogic.KindFromContent(B(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A)));
            Assert.Equal("raster", TraceLogic.KindFromContent(B(0xFF, 0xD8, 0xFF, 0xE0)));
            Assert.Equal("raster", TraceLogic.KindFromContent(Encoding.ASCII.GetBytes("BM6\0\0\0")));
            Assert.Equal("raster", TraceLogic.KindFromContent(B(0x49, 0x49, 0x2A, 0x00)));
            Assert.Equal("raster", TraceLogic.KindFromContent(Encoding.ASCII.GetBytes("GIF89a")));
            Assert.Equal("pdf", TraceLogic.KindFromContent(Encoding.ASCII.GetBytes("\r\n%PDF-1.7\n")));
            Assert.Equal("webp", TraceLogic.KindFromContent(Encoding.ASCII.GetBytes("RIFF").Concat(B(0x10, 0, 0, 0)).Concat(Encoding.ASCII.GetBytes("WEBPVP8 ")).ToArray()));
            Assert.Equal("heic", TraceLogic.KindFromContent(Heic));
            Assert.Null(TraceLogic.KindFromContent(Encoding.ASCII.GetBytes("password=secret")));
            Assert.Null(TraceLogic.KindFromContent(B(1, 2)));

            TraceLogic.CheckContent(@"D:\a.png", "raster", B(0x89, 0x50, 0x4E, 0x47));
            var heic = Assert.Throws<ToolException>(() => TraceLogic.CheckContent(@"D:\IMG_1.png", "raster", Heic));
            Assert.Contains("HEIC", heic.Message);
            Assert.Contains("JPG", heic.Message);
            var txt = Assert.Throws<ToolException>(() => TraceLogic.CheckContent(@"C:\Users\x\notes.jpg", "raster", Encoding.ASCII.GetBytes("hello world")));
            Assert.Contains("not a picture or PDF", txt.Message);
            Assert.Throws<ToolException>(() => TraceLogic.CheckContent(@"D:\scan.png", "raster", Encoding.ASCII.GetBytes("%PDF-1.4")));
            Assert.Contains("TraceLogic.CheckContent(path, kind, head.Take(got).ToArray(), tool);", Plugin("Drawing", "TraceTools.cs"));
        }

        [Fact]
        public void Nothing_is_typed_on_the_command_line()
        {
            var t = Plugin("Drawing", "TraceTools.cs");
            Assert.DoesNotContain("SendStringToExecute", t);
            Assert.DoesNotContain("SendCommand", t);
            Assert.DoesNotContain("Command(", t);
        }

        // ------------------------------------------------------------ TG5-4: the background picture and the other tools

        [Fact]
        public void The_background_picture_is_no_problem_for_check_drawing_or_the_drawing_set()
        {
            Assert.True(DrawingCheckLogic.IsStandardLayer(G5Numbers.Layer));
            Assert.True(DrawingCheckLogic.IsStandardLayer("x-anh-goc"));
            Assert.Contains("!string.Equals(layer, G5Numbers.Layer, StringComparison.OrdinalIgnoreCase) && Wanted(only, \"hidden_layers\")", Plugin("Drawing", "DrawingCheckTools.cs"));
            Assert.Contains("G5Numbers.Layer }, StringComparer.OrdinalIgnoreCase); // Test & sửa G5", Plugin("Drawing", "DrawingSetTools.cs"));
        }
    }
}
