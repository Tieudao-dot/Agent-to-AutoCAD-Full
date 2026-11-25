using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F5: ⚙ "Nạp gọn lệnh", the overwrite cards, the multileader style of the standard.</summary>
    public class NangCapF5Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Plugin(params string[] parts) => Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray());

        // ------------------------------------------------------------ 1. ⚙ Nạp gọn lệnh

        [Fact]
        public void The_setting_is_on_by_default_and_remembered()
        {
            Assert.True(new PaletteSettings().DeferTools);
            Assert.True(PaletteSettings.FromJson("{}").DeferTools);
            var s = new PaletteSettings { DeferTools = false };
            Assert.False(PaletteSettings.FromJson(s.ToJson()).DeferTools);
            Assert.False(s.Clone().DeferTools);
            Assert.Contains("Bật:", F5Numbers.DeferToolsTip);
            Assert.Contains("Tắt:", F5Numbers.DeferToolsTip);
            Assert.Contains("cuộc chat mới", F5Numbers.DeferToolsTip); // bản chốt đợt F: was "tin nhắn tới" (fixed per chat now)
        }

        [Fact]
        public void The_palette_sends_it_and_prewarms_again_when_it_changes()
        {
            var ui = Plugin("UI", "ChatControl.cs");
            Assert.Contains("_deferTools = CheckRow(work, F5Numbers.DeferToolsLabel, F5Numbers.DeferToolsTip);", ui);
            Assert.Contains("_settings.DeferTools = _deferTools.IsChecked == true;", ui);
            var c = Plugin("ChatController.cs");
            Assert.Contains("if (!ChatDefer()) ctx[\"defer_tools\"] = false;", c); // bản chốt đợt F: the chat's own way
            Assert.Contains("if (oldDefer != s.DeferTools && !_busy) SchedulePrewarm();", c);
        }

        // ------------------------------------------------------------ 2. overwrite cards

        [Fact]
        public void File_tokens_are_stable_handle_like_and_case_free()
        {
            var a = F5Confirm.FileToken(@"D:\Bao cao\phong.xlsx");
            Assert.Matches("^[0-9A-F]{16}$", a);
            Assert.Equal(a, F5Confirm.FileToken(@"d:\bao cao\PHONG.xlsx"));
            Assert.Equal(a, F5Confirm.FileToken("D:/Bao cao/phong.xlsx"));
            Assert.NotEqual(a, F5Confirm.FileToken(@"D:\Bao cao\phong2.xlsx"));
            Assert.Empty(F5Confirm.Missing(new[] { a }, new List<object> { a.ToLowerInvariant() }));
            Assert.Equal(new[] { a }, F5Confirm.Missing(new[] { a, a }, null));
            Assert.Equal(new[] { a }, F5Confirm.Missing(new[] { a }, new List<object> { "2A" }));
        }

        [Fact]
        public void The_refusal_is_a_card_request_the_assistant_recognises()
        {
            var m = F5Confirm.CardMessage("export_stl", "overwrite the existing file 'x.stl'", new[] { "ABCDEF0123456789" });
            Assert.StartsWith("Confirmation required", m);
            Assert.Contains("[\"ABCDEF0123456789\"]", m);
            Assert.Contains("confirm_handles", m);
            Assert.Contains("request_confirmation", m);
            Assert.Contains("Nothing was changed", m);
        }

        [Fact]
        public void Every_listed_case_asks_first_and_a_macro_keeps_the_old_rules()
        {
            Assert.Contains("RequireCard(ctx, p, \"plot_pdf\", $\"overwrite the existing file '{output}'\", new[] { F5Confirm.FileToken(output) });", Plugin("Drawing", "LayoutTools.cs"));
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, exists,", Plugin("Drawing", "Solid3Tools.cs"));
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, full, File.Exists(full),", Plugin("Drawing", "ExcelExport.cs"));
            var r = Plugin("Drawing", "RenderTools.cs");
            Assert.Contains("RequireOverwriteCard(ctx, p, tool, file, File.Exists(file),", r);
            Assert.Contains("RequireCard(ctx, p, tool, $\"overwrite the saved view '{name}'\", new[] { Acad.H(vt[name]) });", r);
            // Test & sửa F5: deleting has its own token (the overwrite approval of the same view no longer deletes it)
            Assert.Contains("RequireCard(ctx, p, tool, $\"delete the saved view '{name}'\", new[] { F5Confirm.DeleteViewToken(Acad.H(vt[name])) });", r);
            // Sửa KFG-10: the token of a whole layer holds the layer and the material (was the layer's handle only)
            Assert.Contains("lids.Select(l => F5Confirm.LayerMaterialToken(Acad.H(l), name))", r);
            // an existing material whose color / opacity changes (every object using it): the card; a new material: no card
            Assert.Contains("if (!matId.IsNull) RequireCard(ctx, p, tool, $\"change the color / opacity of the existing material", r);
            var h = Plugin("Drawing", "ConfirmF5.cs");
            Assert.Contains("if (ctx.Dispatcher?.MacroRunning == true) return;", h);
            Assert.Contains("if (!exists) return;", h);
            Assert.Contains("bool MacroRunning { get; }", Plugin("Drawing", "ToolDispatcher.cs"));
            Assert.Contains("public bool MacroRunning => _macroRunning;", Plugin("ChatController.cs"));
            // a replayed macro never writes the Excel file again (F4), so no card can hang there
            Assert.Contains("block_schedule", string.Join(",", MacroLogic.ExcelTools));
        }

        // ------------------------------------------------------------ 3. multileader

        [Theory]
        [InlineData("KT", 100, 1, "KT-ML-100")]
        [InlineData("VN", 50, 1, "VN-ML-50")]
        [InlineData("KT", 100, 1000, "KT-ML-100-M")]
        public void Multileader_style_name_follows_the_dimension_style(string prefix, int scale, double f, string name)
        {
            Assert.Equal(name, MLeaderStyleLogic.Name(prefix, scale, f));
            Assert.True(MLeaderStyleLogic.IsStandardName(name));
        }

        [Fact]
        public void Multileader_sizes_are_paper_mm_scaled_like_the_dimensions()
        {
            var w = MLeaderStyleLogic.For("KT", 100, 1);
            Assert.Equal("KT-ARIAL", w.TextStyle);
            Assert.Equal(100, w.Scale);
            Assert.Equal(F2Numbers.TextNote, w.TextHeight);
            Assert.Equal(2.5, w.ArrowSize);
            Assert.Equal(1, w.LandingGap);
            Assert.Equal(2.5, w.DoglegLength);
            Assert.Equal(250, MLeaderStyleLogic.DrawingHeight(w.TextHeight, w.Scale), 9);
            // a drawing in metres: 1:100 → scale 0.1, the note is 0.25 m
            var m = MLeaderStyleLogic.For("KT", 100, 1000);
            Assert.Equal(0.25, MLeaderStyleLogic.DrawingHeight(m.TextHeight, m.Scale), 9);
            Assert.Equal(2.5, MLeaderStyleLogic.DrawingHeight(2.5, 0));
        }

        [Fact]
        public void An_existing_style_that_differs_is_reported_not_overwritten()
        {
            var w = MLeaderStyleLogic.For("KT", 100, 1);
            Assert.Empty(MLeaderStyleLogic.Differences(w, "kt-arial", 100, 2.5, 2.5, 1, 2.5));
            var d = MLeaderStyleLogic.Differences(w, "Standard", 1, 4, 4, 2, 8);
            Assert.Equal(6, d.Count);
            var src = Plugin("Drawing", "Standards2DTools.cs");
            Assert.Contains("var kept = new[] { ts, ds, ml }.Where(", src);
            Assert.Contains("[\"mleader_style\"] = ml,", src);
            Assert.Contains("ctx.Db.MLeaderstyle = MLeaderStyleIdOf(ctx, (string)ml[\"name\"]);", src);
        }

        [Theory]
        [InlineData("Standard", false)]
        [InlineData("KT-ML-100", true)]
        [InlineData("VN-ML-200-CM", true)]
        [InlineData("My notes", false)]
        [InlineData("KT-100", false)]
        public void Only_the_standard_styles_change_the_text_of_add_mleader(string name, bool standard)
        {
            Assert.Equal(standard, MLeaderStyleLogic.IsStandardName(name));
            Assert.Contains("if (mls != null && !MLeaderStyleLogic.IsStandardName(mls.Name)) mls = null;", Plugin("Drawing", "AnnotationTools.cs"));
        }

        [Fact]
        public void Add_mleader_stays_a_2d_tool_that_follows_the_ucs()
        {
            Assert.Equal(ToolGroup.TwoD, PlanLogic.GroupOf("add_mleader"));
            Assert.Contains("add_mleader", UcsLogic.CreateTools);
            Assert.Contains("CCAD-F5-", Plugin("SelfTest.cs"));
        }
    }
}
