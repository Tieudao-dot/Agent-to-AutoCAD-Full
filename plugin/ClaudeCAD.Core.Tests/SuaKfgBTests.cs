using System;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Sửa KFG phần G4 + G5 + F5 (docs/ban-giao/nang-cap-g-sua-kfg-b.md): the bugs of the independent check of đợt F + G.</summary>
    public class SuaKfgBTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string G4() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderG4.cs");
        private static string Trace() => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs");

        // ------------------------------------------------------------ KFG-01: the saved view is a copy made in the transaction

        [Fact]
        public void Kfg01_the_saved_view_is_copied_inside_its_transaction()
        {
            var g = G4();
            int copy = g.IndexOf("var copy = (ViewTableRecord)rec.Clone();", StringComparison.Ordinal);
            int commit = g.IndexOf("tr.Commit();", copy, StringComparison.Ordinal);
            Assert.True(copy > 0 && commit > copy, "Clone before the transaction ends");
            Assert.Contains("ViewTableRecord named = name != null ? NamedViewCopy(db, name, tool) : null;", g);
            // no use of a record read in a transaction already closed
            Assert.DoesNotContain("named = (ViewTableRecord)tr.GetObject(", g);
            // the self-test now goes through the branch with a name
            Assert.Contains("using (var copy = NamedViewCopy(doc.Database, vname, \"named_view\"))", g);
        }

        [Fact]
        public void The_view_for_the_render_is_not_an_undo_step()
        {
            var g = G4();
            Assert.Contains("try { doc.Editor.SetCurrentView(named); }", g);
            Assert.Contains("try { doc.Editor.SetCurrentView(job.OldView); }", g);
            int off = g.IndexOf("try { doc.Editor.SetCurrentView(named); }", StringComparison.Ordinal);
            Assert.True(g.LastIndexOf("db.DisableUndoRecording(true);", off, StringComparison.Ordinal) > off - 200);
        }

        // ------------------------------------------------------------ KFG-04: auto lights

        [Fact]
        public void Kfg04_auto_with_the_drawings_lights_turns_the_default_light_off()
        {
            // the drawing's 2 lights, default light on: off for the render (AutoCAD ignores the lights otherwise)
            Assert.Equal(((bool?)false, (bool?)null), RenderLogic.LightPlan("auto", 2, false, false, true));
            // the sun on, default light on: off too
            Assert.Equal(((bool?)false, (bool?)null), RenderLogic.LightPlan("auto", 0, true, true, true));
            // already off: nothing to change
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("auto", 2, false, false, false));
            // no light, no sun: the default light (as before)
            Assert.Equal(((bool?)true, (bool?)null), RenderLogic.LightPlan("auto", 0, false, false, false));
            Assert.Equal(((bool?)null, (bool?)null), RenderLogic.LightPlan("auto", 0, false, false, true));
            Assert.Contains("2 light", RenderLogic.LightWords("auto", 2, false, false));
            // lights inside blocks are counted
            Assert.Contains("lights = LightsIn(tr, ms, new Dictionary<ObjectId, int>(), 0);", G4());
        }

        // ------------------------------------------------------------ render that goes on after the command ended

        [Fact]
        public void The_wait_after_the_command_ended_is_at_least_the_estimate()
        {
            var high = RenderLogic.QualityOf("high");
            int est = RenderLogic.Estimate(high, 1920, 1080); // 180 s
            Assert.Equal(180, RenderLogic.EndGrace(est));
            Assert.Equal(G4Numbers.EndGraceSeconds, RenderLogic.EndGrace(5));
            // 60 s after the command ended, a high render with no file yet: still running (was "no file" at 20 s)
            Assert.Equal(RenderLogic.State.Running, RenderLogic.StateOf(70, high.MaxWaitSeconds, 0, 0, 0, false, false, 60, est));
            Assert.Equal(RenderLogic.State.NoFile, RenderLogic.StateOf(200, high.MaxWaitSeconds, 0, 0, 0, false, false, 181, est));
            // "unknown command" (another order of questions) still gives up at once
            Assert.Equal(RenderLogic.State.Failed, RenderLogic.StateOf(5, high.MaxWaitSeconds, 0, 0, 0, false, true, 1, est));
            Assert.Contains("job.Cancelled, job.Failed, endedFor, job.Estimate);", G4());
        }

        [Fact]
        public void Old_temporary_render_files_are_removed()
        {
            var now = new DateTime(2026, 11, 26, 3, 0, 0, DateTimeKind.Utc);
            Assert.True(RenderLogic.IsOldTemp(now.AddHours(-2), now));
            Assert.False(RenderLogic.IsOldTemp(now.AddMinutes(-10), now));
            Assert.Contains("CleanOldRenderFiles(tempDir);", G4());
        }

        // ------------------------------------------------------------ KFG-10: material card tokens hold their parameters

        [Fact]
        public void Kfg10_material_tokens_hold_the_layer_and_material_and_the_look()
        {
            var a = F5Confirm.LayerMaterialToken("2A", "Gạch");
            Assert.Matches("^[0-9A-F]{16}$", a);
            Assert.Equal(a, F5Confirm.LayerMaterialToken("2a", " gạch "));
            Assert.NotEqual(a, F5Confirm.LayerMaterialToken("2A", "Kính"));
            Assert.NotEqual(a, F5Confirm.LayerMaterialToken("2B", "Gạch"));
            var red = F5Confirm.MaterialLookToken("1F", new double[] { 200, 0, 0 }, null);
            Assert.NotEqual(red, F5Confirm.MaterialLookToken("1F", new double[] { 0, 0, 200 }, null));
            Assert.NotEqual(red, F5Confirm.MaterialLookToken("1F", new double[] { 200, 0, 0 }, 50));
            Assert.NotEqual(red, F5Confirm.MaterialLookToken("2F", new double[] { 200, 0, 0 }, null));
            Assert.Equal(red, F5Confirm.MaterialLookToken("1f", new double[] { 200, 0, 0 }, null));
            // never the plain handle of a layer / material (that let another material / colour through)
            Assert.NotEqual("2A", a);
            var r = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "RenderTools.cs");
            Assert.Contains("new[] { F5Confirm.MaterialLookToken(Acad.H(matId), rgb, opacity) }", r);
            Assert.Contains("lids.Select(l => F5Confirm.LayerMaterialToken(Acad.H(l), name))", r);
        }

        // ------------------------------------------------------------ KFG-05: UCS turned in plan

        [Fact]
        public void Kfg05_straighten_in_a_turned_ucs()
        {
            // UCS turned 30°, a wall along its x axis (WCS points): nothing to turn (was −30°)
            Assert.Equal(0, TraceLogic.StraightenAngle(new double[] { 0, 0 }, new double[] { 866.03, 500 }, "horizontal", 30) * 180 / Math.PI, 2);
            // the same wall 2° off the UCS axis: −2°
            double a = (30 + 2) * Math.PI / 180;
            Assert.Equal(-2, TraceLogic.StraightenAngle(new double[] { 0, 0 }, new[] { 1000 * Math.Cos(a), 1000 * Math.Sin(a) }, "auto", 30) * 180 / Math.PI, 6);
            // World (0°): as before
            Assert.Equal(-2.862, TraceLogic.StraightenAngle(new double[] { 0, 0 }, new double[] { 100, 5 }, "auto", 0) * 180 / Math.PI, 3);
            var t = Trace();
            Assert.Contains("img.Orientation = new CoordinateSystem3d(at, ux * w, uy * h);", t);
            Assert.Contains("fr.IsWorld ? 0 : fr.PlanAngleDeg, tool); // KFG-05", t);
            Assert.Contains("[\"min\"] = Acad.J(corners.min),", t);
        }

        // ------------------------------------------------------------ KFG-06: only pictures trace_image attached

        [Fact]
        public void Kfg06_a_picture_of_the_user_is_never_scaled_or_turned()
        {
            var t = Trace();
            int target = t.IndexOf("private static ObjectId TraceTarget", StringComparison.Ordinal);
            int mark = t.IndexOf("if (ReadMark(tr, e) == null) throw ToolException.Invalid(", target, StringComparison.Ordinal);
            int ret = t.IndexOf("return id;", target, StringComparison.Ordinal);
            Assert.True(target > 0 && mark > target && mark < ret, "the mark is checked before the handle is used");
        }

        // ------------------------------------------------------------ KFG-11: PDF pages

        private static byte[] Pdf(string s) => Encoding.ASCII.GetBytes(s);

        [Fact]
        public void Kfg11_pages_of_a_pdf_saved_again_are_counted_once()
        {
            // two pages, then an incremental save appending both page objects again (and a new catalog / tree)
            var incremental = Pdf(
                "%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >> endobj\n" +
                "3 0 obj << /Type /Page /Parent 2 0 R >> endobj\n4 0 obj << /Type /Page /Parent 2 0 R >> endobj\ntrailer << /Root 1 0 R >>\n" +
                "3 0 obj << /Type /Page /Parent 2 0 R /Rotate 90 >> endobj\n4 0 obj << /Type /Page /Parent 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >> endobj\ntrailer << /Root 1 0 R /Prev 9 >>");
            Assert.Equal(2, TraceLogic.PdfPageCount(incremental));
            Assert.Throws<ToolException>(() => TraceLogic.PageOf(3, TraceLogic.PdfPageCount(incremental)));
            // a page removed by the later save: the last tree wins
            var removed = Pdf("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj 2 0 obj << /Type /Pages /Count 3 >> endobj trailer << /Root 1 0 R >> 2 0 obj << /Type /Pages /Count 2 >> endobj trailer << /Root 1 0 R >>");
            Assert.Equal(2, TraceLogic.PdfPageCount(removed));
            // no tree readable, page objects each once
            var pagesOnly = Pdf("5 0 obj << /Type /Page >> endobj 6 0 obj << /Type /Page >> endobj 5 0 obj << /Type /Page /Rotate 90 >> endobj");
            Assert.Equal(2, TraceLogic.PdfPageCount(pagesOnly));
            // a compressed PDF: 0 = unknown, and the result says so
            Assert.Equal(0, TraceLogic.PdfPageCount(Pdf("%PDF-1.5\n7 0 obj << /Type /ObjStm /N 5 /Filter /FlateDecode >> stream xyz endstream endobj trailer << /Root 9 0 R >>")));
            Assert.Contains("res[\"pages_note\"]", Trace());
        }

        // ------------------------------------------------------------ KFG-14 (Trace.cs): AVIF, and BigTIFF

        [Fact]
        public void Kfg14_avif_is_said_as_avif_and_bigtiff_goes_to_autocad()
        {
            var avif = new byte[] { 0, 0, 0, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66, 0, 0, 0, 0 };
            Assert.Equal("avif", TraceLogic.KindFromContent(avif));
            var ex = Assert.Throws<ToolException>(() => TraceLogic.CheckContent("a.png", "raster", avif));
            Assert.Contains("AVIF", ex.Message);
            Assert.Contains("JPG or PNG", Assert.Throws<ToolException>(() => TraceLogic.KindOf("a.avif", true, 10)).Message);
            Assert.Equal("raster", TraceLogic.KindFromContent(new byte[] { 0x49, 0x49, 0x2B, 0x00, 8, 0, 0, 0 }));
            Assert.Equal("raster", TraceLogic.KindFromContent(new byte[] { 0x4D, 0x4D, 0x00, 0x2B, 0, 8, 0, 0 }));
        }

        // ------------------------------------------------------------ network paths (nghi vấn, bảo mật)

        [Theory]
        [InlineData(@"\\server\share\x.png", @"\\SERVER\SHARE")]
        [InlineData("//server/share/a/x.png", @"\\SERVER\SHARE")]
        [InlineData(@"\\?\UNC\server\share\x.png", @"\\SERVER\SHARE")]
        [InlineData(@"\\?\C:\x.png", null)]
        [InlineData(@"C:\scan\x.png", null)]
        [InlineData("", null)]
        public void Network_share_of_a_path(string path, string share) => Assert.Equal(share, TraceLogic.UncShare(path));

        [Fact]
        public void Only_this_computer_the_chat_or_the_drawings_network_folder()
        {
            Assert.Null(TraceLogic.PathRefusal(@"C:\scan\x.png", @"D:\du-an\nha.dwg", false, false));
            Assert.Null(TraceLogic.PathRefusal(@"\\nas\du-an\scan\x.png", @"\\NAS\du-an\ban-ve\nha.dwg", false, false));
            Assert.Contains("another computer", TraceLogic.PathRefusal(@"\\other\c$\x.png", @"\\nas\du-an\nha.dwg", false, false));
            Assert.Contains("another computer", TraceLogic.PathRefusal(@"\\attacker\s\x.png", null, false, false));
            Assert.Contains("another computer", TraceLogic.PathRefusal(@"\\nas\other\x.png", @"\\nas\du-an\nha.dwg", false, false));
            Assert.Contains("network drive", TraceLogic.PathRefusal(@"Z:\x.png", @"C:\nha.dwg", true, false));
            Assert.Null(TraceLogic.PathRefusal(@"Z:\x.png", @"Z:\nha.dwg", true, true));
            var t = Trace();
            Assert.True(t.Split(new[] { "CheckPathAllowed(ctx, path, chatDir, tool);" }, StringSplitOptions.None).Length - 1 >= 2, "checked before and after the full path");
        }

        [Fact]
        public void The_chat_folder_is_compared_with_its_separator()
        {
            Assert.True(TraceLogic.IsInside(@"C:\a\anh-dinh-kem\anh-1.png", @"C:\a\anh-dinh-kem"));
            Assert.True(TraceLogic.IsInside(@"C:\a\anh-dinh-kem\anh-1.png", @"C:\a\anh-dinh-kem\"));
            Assert.False(TraceLogic.IsInside(@"C:\a\anh-dinh-kem-2\anh-1.png", @"C:\a\anh-dinh-kem"));
            Assert.False(TraceLogic.IsInside(null, @"C:\a"));
            Assert.DoesNotContain("path.StartsWith(chatDir", Trace());
            // a pasted picture into a drawing never saved: refused, never left in Documents
            Assert.Contains("if (!ctx.Doc.IsNamedDrawing)", Trace());
        }

        [Fact]
        public void A_look_inside_the_picture_is_not_taken_as_blank()
        {
            Assert.Equal(new[] { 48, 40, 33 }, TraceLogic.LookBackgroundBgr);
            Assert.Contains("var bg = TraceLogic.LookBackgroundBgr;", Trace());
            // a look zoomed inside a white page: every pixel white, nothing is background → drawn share 1 (never blank)
            var white = Enumerable.Repeat((byte)255, 10 * 10 * 4).ToArray();
            Assert.Equal(1, TraceLogic.DrawnShare(white, 10, 10, TraceLogic.LookBackgroundBgr, 0, 0, 9, 9), 9);
            Assert.Contains("System.Drawing.Color.FromArgb(33, 40, 48)", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CaptureTools.cs"));
        }

        // ------------------------------------------------------------ KFG-13: pasted pictures

        [Fact]
        public void Kfg13_pasted_pictures_only_for_2d_and_cleaned()
        {
            var now = new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc);
            Assert.True(TraceLogic.IsOldPasted(now.AddDays(-8), now));
            Assert.False(TraceLogic.IsOldPasted(now.AddDays(-1), now));
            var c = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("PlanLogic.Allowed(WorkModeLogic.EffectivePlan(Activation.Plan, WorkModeLogic.Normalize(_settings.WorkMode)), \"trace_image\")", c);
            Assert.Contains("var path = tracePaths ? i.SourcePath ?? UI.ChatAttachments.Save(i.Base64, i.MediaType) : null;", c);
            Assert.Contains("CleanOld();", Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatAttachments.cs"));
            Assert.Contains("res[\"pasted_note\"]", Trace());
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("3d", null), "trace_image"));
        }
    }
}
