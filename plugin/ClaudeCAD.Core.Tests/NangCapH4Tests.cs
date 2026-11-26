using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt H4 (docs/ban-giao/nang-cap-h4.md): company_standard — names, the store, JSON, checks, wiring.</summary>
    public class NangCapH4Tests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "claudecad-h4-" + Guid.NewGuid().ToString("N"));

        public void Dispose() { try { Directory.Delete(_root, true); } catch (Exception) { } }

        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static CompanyStandard Sample(string name = "Công ty ABC") => new CompanyStandard
        {
            Name = name, Source = @"C:\mau\abc.dwt", LearnedAt = "2026-11-26 09:00", Insunits = 4, MmPerUnit = 1, TemplateKept = true,
            CurrentTextStyle = "ABC-ARIAL", CurrentDimStyle = "ABC-100", CurrentMLeaderStyle = "ABC-ML",
            Layers =
            {
                new CompanyStandard.Layer { Name = "ABC-TUONG", Color = "1", Linetype = "Continuous", Lineweight = 0.35, Description = "Tường" },
                new CompanyStandard.Layer { Name = "ABC-TRUC", Color = "255,128,0", Linetype = "CENTER", Lineweight = -1, Plottable = false },
            },
            TextStyles = { new CompanyStandard.TextStyle { Name = "ABC-ARIAL", Font = "Arial", Height = 0, WidthFactor = 0.8 } },
            DimStyles = { new CompanyStandard.DimStyle { Name = "ABC-100", TextStyle = "ABC-ARIAL", TextHeight = 2.5, ArrowSize = 1.5, Scale = 100 } },
            MLeaderStyles = { new CompanyStandard.MLeaderStyle { Name = "ABC-ML", TextStyle = "ABC-ARIAL", TextHeight = 2.5, ArrowSize = 2, Scale = 100 } },
            TitleBlocks = { new CompanyStandard.TitleBlock { Block = "ABC-KHUNG", Layout = "A3", Tags = { "TEN_DA", "NGAY" }, X = 10, Y = 5, Rotation = 0, Scale = 1 } },
            Papers = { new CompanyStandard.Paper { Layout = "A3", Media = "ISO_full_bleed_A3_(420.00_x_297.00_MM)", Name = "A3", W = 420, H = 297, Landscape = true, Viewport = new double[] { 200, 160, 360, 250 } } },
            TextHeights = { 250, 350, 500 },
            Notes = { "x" },
        };

        [Fact]
        public void Names_are_checked_and_give_a_folder_without_accents()
        {
            Assert.Equal("Công ty ABC", CompanyStandardLogic.ValidName("  Công   ty ABC "));
            Assert.Throws<ToolException>(() => CompanyStandardLogic.ValidName(""));
            Assert.Throws<ToolException>(() => CompanyStandardLogic.ValidName("a/b"));
            Assert.Throws<ToolException>(() => CompanyStandardLogic.ValidName(new string('x', H4Numbers.MaxNameLength + 1)));
            Assert.Equal("cong-ty-abc", CompanyStandardLogic.Slug("Công ty ABC"));
            Assert.Equal("dong-a", CompanyStandardLogic.Slug("Đông Á"));
            Assert.Equal("chuan", CompanyStandardLogic.Slug("!!!"));
        }

        [Fact]
        public void The_store_is_per_user_outside_the_install_folder()
        {
            var root = CompanyStandardLogic.DefaultRoot();
            Assert.EndsWith(Path.Combine("ClaudeCAD", "chuan-cong-ty"), root);
            Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), root);
        }

        [Fact]
        public void A_standard_survives_save_and_load_with_every_field()
        {
            CompanyStandardLogic.Save(_root, Sample());
            var b = CompanyStandardLogic.Load(_root, "cong ty abc"); // the folder decides: accents / case as typed do not matter
            Assert.NotNull(b);
            Assert.Equal(CompanyStandardLogic.ToJson(Sample()), CompanyStandardLogic.ToJson(b));
            Assert.Equal("255,128,0", b.Layers[1].Color);
            Assert.False(b.Layers[1].Plottable);
            Assert.Equal(new double[] { 200, 160, 360, 250 }, b.Papers[0].Viewport);
            Assert.Equal(new[] { "TEN_DA", "NGAY" }, b.TitleBlocks[0].Tags);
            Assert.Single(CompanyStandardLogic.List(_root));
            Assert.True(CompanyStandardLogic.Delete(_root, "Công ty ABC"));
            Assert.Empty(CompanyStandardLogic.List(_root));
            Assert.False(CompanyStandardLogic.Delete(_root, "Công ty ABC"));
            Assert.Contains("None is learnt", Assert.Throws<ToolException>(() => CompanyStandardLogic.Require(_root, "ABC", "setup_standards")).Message);
        }

        [Fact]
        public void At_most_MaxStandards_but_the_same_name_can_be_learnt_again()
        {
            for (int i = 0; i < H4Numbers.MaxStandards; i++) CompanyStandardLogic.Save(_root, Sample("Cty " + i));
            Assert.NotNull(CompanyStandardLogic.RoomRefusal(_root, "Cty mới"));
            Assert.Null(CompanyStandardLogic.RoomRefusal(_root, "cty 3"));
            Assert.Contains("Cty 0", Assert.Throws<ToolException>(() => CompanyStandardLogic.Require(_root, "khac", "check_drawing")).Message);
        }

        [Fact]
        public void A_damaged_file_is_skipped_by_the_list()
        {
            CompanyStandardLogic.Save(_root, Sample());
            Directory.CreateDirectory(Path.Combine(_root, "hong"));
            File.WriteAllText(Path.Combine(_root, "hong", CompanyStandardLogic.DataFile), "{ not json");
            Assert.Single(CompanyStandardLogic.List(_root));
        }

        [Fact]
        public void Paper_heights_and_caps()
        {
            Assert.Equal(("A3", true), CompanyStandardLogic.PaperOf(420, 297));
            Assert.Equal(("A3", false), CompanyStandardLogic.PaperOf(297.5, 419));
            Assert.Equal(("A1", true), CompanyStandardLogic.PaperOf(841, 594));
            Assert.Null(CompanyStandardLogic.PaperOf(500, 300).name);
            var top = CompanyStandardLogic.TopHeights(new double[] { 2.5, 2.5, 3.5, 2.5, 5, 3.5, 0, -1, double.NaN });
            Assert.Equal(new double[] { 2.5, 3.5, 5 }, top);
            Assert.Equal(H4Numbers.MaxHeights, CompanyStandardLogic.TopHeights(Enumerable.Range(1, 40).Select(i => (double)i)).Count);
            var notes = new List<string>();
            Assert.Equal(3, CompanyStandardLogic.Cap(Enumerable.Range(0, 5).ToList(), 3, "layers", notes).Count);
            Assert.Contains("5 layers", notes.Single());
        }

        [Fact]
        public void Checks_with_a_company_standard()
        {
            var s = Sample();
            Assert.True(CompanyStandardLogic.HasLayer(s, "abc-tuong"));
            Assert.False(CompanyStandardLogic.HasLayer(s, "A-WALL")); // the Vietnamese table is not the company's
            foreach (var l in new[] { "0", "Defpoints", "XREF|A-WALL", G5Numbers.Layer, "A-SECT" }) Assert.True(CompanyStandardLogic.HasLayer(s, l), l);
            Assert.True(CompanyStandardLogic.HasTextStyle(s, "abc-arial"));
            Assert.False(CompanyStandardLogic.HasTextStyle(s, "VN-ARIAL"));
            Assert.True(CompanyStandardLogic.HasDimStyle(s, "ABC-100"));
            Assert.True(CompanyStandardLogic.HasMLeaderStyle(s, "ABC-ML"));
            // the template in mm, the drawing in metres: heights / 1000
            Assert.Equal(new[] { 0.25, 0.35, 0.5 }, CompanyStandardLogic.Heights(s, 1000).Select(h => Math.Round(h, 9)));
            Assert.Null(CompanyStandardLogic.Heights(new CompanyStandard(), 1));
            var diffs = CompanyStandardLogic.LayerDifferences(s.Layers[0], new CompanyStandard.Layer { Color = "7", Linetype = "continuous", Lineweight = 0.35 });
            Assert.Equal(new[] { "color 7 → 1" }, diffs);
            Assert.Equal("255,1,2", CompanyStandardLogic.ColorText(false, 0, 255, 1, 2));
            Assert.Equal("3", CompanyStandardLogic.ColorText(true, 3, 0, 0, 0));
        }

        [Fact]
        public void Card_tokens_are_per_action_and_per_standard()
        {
            Assert.Equal(CompanyStandardLogic.Token("delete", "Công ty ABC"), CompanyStandardLogic.Token("delete", "cong ty abc"));
            Assert.NotEqual(CompanyStandardLogic.Token("delete", "ABC"), CompanyStandardLogic.Token("replace", "ABC"));
            Assert.NotEqual(CompanyStandardLogic.Token("delete", "ABC"), CompanyStandardLogic.Token("delete", "XYZ"));
            Assert.Matches("^[0-9A-F]{16}$", CompanyStandardLogic.Token("delete", "ABC"));
        }

        [Fact]
        public void A_template_dwt_is_read_as_a_dwg()
        {
            Assert.Equal("dwg", OtherDrawingLogic.KindOf(@"C:\mau\abc.dwt", true, 100, "company_standard"));
            Assert.Equal("dwg", OtherDrawingLogic.KindOf(@"C:\mau\abc.DWT", true, 100, "company_standard"));
            Assert.Equal("dxf", OtherDrawingLogic.KindOf(@"C:\mau\abc.dxf", true, 100, "company_standard"));
        }

        [Fact]
        public void Wiring_plan_labels_macro_and_the_three_tools()
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("company_standard"));
            Assert.False(PlanLogic.Allowed(PlanLogic.Plan3D, "company_standard"));
            Assert.True(PlanLogic.Allowed(PlanLogic.PlanFull, "company_standard"));
            Assert.False(MacroLogic.ShouldRecord("company_standard", false));
            var t = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "CompanyStandardTools.cs");
            Assert.Contains("d.Register(new ToolDef(\"company_standard\", false, CompanyStandardTool, noTransaction: true));", t);
            Assert.Contains("OtherDrawing.Open(ctx, p.Str(\"file\"), tool)", t); // the shared reader of H3, not its own
            Assert.Contains("CompanyStandardLogic.Token(\"delete\", s.Name)", t);
            Assert.Contains("CompanyStandardLogic.Token(\"replace\", name)", t);
            Assert.DoesNotContain("OpenMode.ForWrite", t.Substring(t.IndexOf("private static void ReadTemplate", StringComparison.Ordinal), t.IndexOf("// ------------------------------------------------------------ the copy of the template", StringComparison.Ordinal) - t.IndexOf("private static void ReadTemplate", StringComparison.Ordinal)));
            Assert.Contains("RegisterCompanyStandard(d);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingService.cs"));
            Assert.Contains("[\"company_standard\"] = \"Chuẩn công ty\"", Src("plugin", "ClaudeCAD.Plugin", "UI", "ToolLabels.cs"));
            var st = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");
            Assert.Contains("if (p.Has(\"company\")) return SetupCompanyStandard(ctx, p);", st);
            Assert.Contains("CompanyTitleBlockDef(c, company, cTitle, out cWhy)", st);
            var ck = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "DrawingCheckTools.cs");
            Assert.Contains("CompanyStandardLogic.Require(CompanyRoot, p.Str(\"company\"), \"check_drawing\")", ck);
            Assert.Contains("def.Fixable && !(company != null && def.NeedsStandard)", ck);
        }
    }
}
