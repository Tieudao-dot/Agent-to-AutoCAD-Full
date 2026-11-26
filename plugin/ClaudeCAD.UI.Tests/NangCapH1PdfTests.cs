using System;
using System.IO;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using Xunit;
using Xunit.Abstractions;

namespace ClaudeCAD.UI.Tests
{
    /// <summary>
    /// Nâng cấp đợt H1: on a real Windows (CI), the PDF reader of Windows renders a page of a PDF to a PNG through the
    /// Windows PowerShell script of trace_image (Core/PdfRender.cs, Drawing/PdfPageImage.cs), path with spaces / Vietnamese.
    /// </summary>
    public class NangCapH1PdfTests
    {
        private readonly ITestOutputHelper _out;
        public NangCapH1PdfTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void A_pdf_page_is_rendered_to_a_png_by_windows()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ccad h1 thử " + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(dir);
            var pdf = Path.Combine(dir, "bản vẽ mẫu.pdf");
            File.WriteAllBytes(pdf, PdfRenderLogic.SamplePdf());
            var png = PdfPageImage.Render(pdf, 1, 1024, out var why);
            _out.WriteLine(png ?? ("not rendered: " + why));
            Assert.True(png != null, "not rendered: " + why);
            var head = new byte[24];
            using (var fs = File.OpenRead(png)) fs.Read(head, 0, head.Length);
            var size = PdfRenderLogic.PngSize(head);
            Assert.True(size.HasValue, "not a PNG");
            Assert.Equal(1024, size.Value.w);
            Assert.InRange(size.Value.h, 1400, 1500); // A4 portrait: 1024 × 842 / 595 ≈ 1449
            // a page that is not there: no picture, a reason
            Assert.Null(PdfPageImage.Render(pdf, 5, 1024, out var why5));
            Assert.False(string.IsNullOrEmpty(why5));
            // Test & sửa H1: the same page again is the same render; a broken PDF says why; no half render is left behind
            Assert.Equal(png, PdfPageImage.Render(pdf, 1, 1024, out _));
            var broken = Path.Combine(dir, "hỏng.pdf");
            File.WriteAllBytes(broken, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x31, 0x20 });
            Assert.Null(PdfPageImage.Render(broken, 1, 1024, out var whyBroken));
            _out.WriteLine("broken: " + whyBroken);
            Assert.False(string.IsNullOrEmpty(whyBroken));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(png), "*" + PdfRenderLogic.PartialMark + "*"));
        }
    }
}
