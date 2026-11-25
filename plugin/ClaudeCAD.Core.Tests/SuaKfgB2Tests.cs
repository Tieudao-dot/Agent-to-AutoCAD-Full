using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa KFG phần G4 + G5, lượt 2 (KFG-28, 29, 31, 35, 36, 37 và nhận xét G5).</summary>
    public class SuaKfgB2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Trace() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs");

        // ------------------------------------------------------------ KFG-28: device prefixes

        [Theory]
        [InlineData(@"\\.\UNC\evil\x\a.png", @"\\EVIL\X")]
        [InlineData(@"\\?\UNC\evil\x\a.png", @"\\EVIL\X")]
        [InlineData(@"\\.\unc\evil\x\a.png", @"\\EVIL\X")]
        public void Unc_device_forms_are_network_paths(string path, string share) => Assert.Equal(share, TraceLogic.UncShare(path));

        [Theory]
        [InlineData(@"\\?\C:\scan\a.png", @"C:\scan\a.png")]
        [InlineData(@"\\.\Z:\a.png", @"Z:\a.png")]
        [InlineData(@"\\.\UNC\nas\duan\a.png", @"\\nas\duan\a.png")]
        [InlineData(@"C:\a.png", @"C:\a.png")]
        [InlineData(@"\\?\GLOBALROOT\Device\Mup\evil\x\a.png", null)]
        [InlineData(@"\\.\pipe\x", null)]
        [InlineData(@"\\?\Volume{0b1a0000-0000-0000-0000-100000000000}\a.png", null)]
        [InlineData(@"\\.\PhysicalDrive0", null)]
        public void Device_prefixes_become_plain_paths_or_are_refused(string path, string plain) => Assert.Equal(plain, TraceLogic.PlainPath(path));

        [Fact]
        public void Kfg28_every_device_form_follows_the_rule()
        {
            const string drawing = @"\\nas\duan\ban-ve\nha.dwg";
            Assert.Contains("another computer", TraceLogic.PathRefusal(@"\\.\UNC\evil\x\a.png", drawing, false, false));
            Assert.Contains("device path", TraceLogic.PathRefusal(@"\\?\GLOBALROOT\Device\Mup\evil\x\a.png", drawing, false, false));
            Assert.Contains("device path", TraceLogic.PathRefusal(@"\\.\pipe\x", drawing, false, false));
            // \\?\Z:\ with Z: a network drive: its drive is checked (the caller reads the drive of the plain path)
            Assert.Contains("network drive", TraceLogic.PathRefusal(@"\\?\Z:\a.png", @"C:\nha.dwg", true, false));
            Assert.Null(TraceLogic.PathRefusal(@"\\?\C:\scan\a.png", @"C:\nha.dwg", false, true));
            // the same share as the drawing, written as \\.\UNC\: allowed
            Assert.Null(TraceLogic.PathRefusal(@"\\.\UNC\nas\duan\scan\a.png", drawing, false, false));
            var t = Trace();
            Assert.Contains("var plain = TraceLogic.PlainPath(path);", t);
            Assert.Contains("var root = Path.GetPathRoot(plain);", t);
        }

        // ------------------------------------------------------------ KFG-29: files the user dragged from the network

        [Fact]
        public void Kfg29_a_network_file_the_user_attached_is_copied_here_and_the_advice_has_no_loop()
        {
            Assert.True(TraceLogic.NeedsLocalCopy(@"\\nas\anh\scan.jpg", false));
            Assert.True(TraceLogic.NeedsLocalCopy(@"Z:\scan.jpg", true));
            Assert.True(TraceLogic.NeedsLocalCopy(@"\\?\GLOBALROOT\x", false));
            Assert.False(TraceLogic.NeedsLocalCopy(@"C:\Users\a\scan.jpg", false));
            Assert.Contains("the chat copies it to this computer", TraceLogic.Advice);
            Assert.Contains(TraceLogic.Advice, TraceLogic.PathRefusal(@"\\other\s\a.png", @"C:\nha.dwg", false, false));
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            // lượt 3 (KFG-40): nothing is copied when the message is sent; the copy happens at trace_image attach
            Assert.Contains("UI.ChatAttachments.UserFile(i.SourcePath)", c);
            var a = Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatAttachments.cs");
            Assert.Contains("public static string CopyForTrace(string path)", a);
            Assert.StartsWith("tep-", TraceLogic.NetworkCopyName(@"\\nas\a.png", 1, 1));
            Assert.Contains("Directory.GetFiles(Folder, \"tep-*.*\")", a);
            // a whole network copy is not called "pasted ≤ 1568 px"
            Assert.True(TraceLogic.IsPastedName(@"C:\x\anh-0123.png"));
            Assert.False(TraceLogic.IsPastedName(@"C:\x\tep-0123.png"));
            Assert.DoesNotContain("kéo vào khung chat. File cùng", Src("docs", "checklist-dot-g5.md"));
        }

        [Fact]
        public void A_relative_path_is_refused_before_any_file_is_touched()
        {
            Assert.True(TraceLogic.IsAbsoluteDrivePath(@"C:\a.png"));
            Assert.False(TraceLogic.IsAbsoluteDrivePath("scan.png"));
            Assert.False(TraceLogic.IsAbsoluteDrivePath(@"C:scan.png"));
            var t = Trace();
            int check = t.IndexOf("is not a full path.", StringComparison.Ordinal);
            int exists = t.IndexOf("exists = File.Exists(path);", StringComparison.Ordinal);
            int first = t.IndexOf("CheckPathAllowed(ctx, path, chatDir, tool); // the raw path", StringComparison.Ordinal);
            Assert.True(check > 0 && first > 0 && exists > first, "the rule runs on the raw path before File.Exists");
        }

        [Fact]
        public void A_drawing_on_a_mapped_drive_gets_a_clear_refusal()
        {
            var r = TraceLogic.PathRefusal(@"\\nas\duan\anh\a.png", @"Z:\ban-ve\nha.dwg", false, false);
            Assert.Contains("drive Z:", r);
            Assert.Contains("give its path on Z:", r);
        }

        // ------------------------------------------------------------ KFG-31: the copy of the view is disposed on a refusal

        [Fact]
        public void Kfg31_the_view_copy_is_disposed_when_the_card_refuses()
        {
            var g = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderG4.cs");
            int copy = g.IndexOf("ViewTableRecord named = name != null ? NamedViewCopy(db, name, tool) : null;", StringComparison.Ordinal);
            int card = g.IndexOf("RequireOverwriteCard(ctx, p, tool, file", copy, StringComparison.Ordinal);
            int dispose = g.IndexOf("named?.Dispose(); // KFG-31", card, StringComparison.Ordinal);
            Assert.True(copy > 0 && card > copy && dispose > card);
            Assert.True(g.IndexOf("try", copy, StringComparison.Ordinal) < card, "the card is inside the try");
        }

        // ------------------------------------------------------------ KFG-35: page count with a time limit

        [Fact]
        public void Kfg35_a_huge_pdf_is_not_counted_for_long()
        {
            // 15 000 objects and no readable /Root: the slow pattern of the page objects
            var sb = new StringBuilder("%PDF-1.7\n");
            for (int i = 1; i <= 15000; i++) sb.Append(i).Append(" 0 obj << /Length 10 /Filter /FlateDecode >> stream ").Append('x', 2000).Append(" endstream endobj\n");
            var big = Encoding.ASCII.GetBytes(sb.ToString());
            var sw = Stopwatch.StartNew();
            int n = TraceLogic.PdfPageCount(big);
            sw.Stop();
            Assert.True(sw.Elapsed.TotalSeconds < TraceLogic.PdfScanSeconds + 1.5, $"{sw.Elapsed.TotalSeconds:0.0} s");
            Assert.Equal(0, n);
            // a normal PDF is still counted
            Assert.Equal(2, TraceLogic.PdfPageCount(Encoding.ASCII.GetBytes("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj 2 0 obj << /Type /Pages /Count 2 >> endobj trailer << /Root 1 0 R >>")));
        }

        // ------------------------------------------------------------ KFG-36: default place in a turned UCS

        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(90)]
        [InlineData(-45)]
        public void Kfg36_the_picture_lies_right_of_the_drawing_in_the_ucs(double angle)
        {
            double[] min = { 0, 0 }, max = { 20000, 10000 };
            var at = TraceLogic.InsertPointInUcs(min, max, 1, new double[] { 0, 0 }, angle);
            double c = Math.Cos(angle * Math.PI / 180), s = Math.Sin(angle * Math.PI / 180);
            double U(double x, double y) => x * c + y * s;
            // every corner of the drawing has a UCS x smaller than the picture's (gap 5 m): a 10 m square picture along the UCS never overlaps
            var corners = new[] { (0.0, 0.0), (20000.0, 0.0), (20000.0, 10000.0), (0.0, 10000.0) };
            double umaxDrawing = corners.Max(q => U(q.Item1, q.Item2));
            Assert.Equal(umaxDrawing + G5Numbers.GapMm, U(at[0], at[1]), 6);
            if (angle == 0) Assert.Equal(TraceLogic.InsertPoint(min, max, 1), at);
            Assert.Equal(new double[] { 0, 0 }, TraceLogic.InsertPointInUcs(null, null, 1, new double[] { 5, 5 }, 30));
            Assert.Contains("TraceLogic.InsertPointInUcs(", Trace());
        }

        // ------------------------------------------------------------ KFG-37: PDF corners after scale / straighten and in info

        [Fact]
        public void Kfg37_pdf_corners_follow_the_picture()
        {
            var t = Trace();
            Assert.Contains("pdfFrame = new[] { at.X, at.Y, w * c, w * sn, -h * sn, h * c };", t);
            Assert.Contains("var frameNow = MoveFrame(mark?.PdfFrame, move);", t);
            Assert.Contains("corners = Corners(e, frameNow);", t);
            Assert.Contains("var cn = Corners(e, m?.PdfFrame);", t);
            Assert.DoesNotContain("[\"min\"] = Acad.J(new Point3d(ext.MinPoint.X, ext.MinPoint.Y, 0)),", t);
        }
    }
}
