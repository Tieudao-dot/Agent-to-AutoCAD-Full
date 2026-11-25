using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Test & sửa đợt F5 (25/11/2026, docs/ban-giao/nang-cap-f5-test.md): the confirmation cards cannot be got round, the
    /// merge with the fixes after the independent check (KTF) kept both sides.
    /// </summary>
    public class NangCapF5TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        private static string Plugin(params string[] parts) => Src(new[] { "plugin", "ClaudeCAD.Plugin" }.Concat(parts).ToArray());

        // ------------------------------------------------------------ TF5-1: one approval, one job

        [Fact]
        public void Deleting_a_view_has_its_own_token_so_an_overwrite_approval_does_not_delete()
        {
            const string h = "2F4A";
            var del = F5Confirm.DeleteViewToken(h);
            Assert.NotEqual(h, del);
            Assert.Equal(16, del.Length);
            Assert.True(del.All(Uri.IsHexDigit));
            Assert.Equal(del, F5Confirm.DeleteViewToken("2f4a")); // case-free like the handles
            Assert.NotEqual(del, F5Confirm.DeleteViewToken("2F4B"));
            Assert.NotEqual(del, F5Confirm.FileToken(h));
            // approved for "overwrite view 2F4A" (its handle): deleting still needs its own card
            Assert.Equal(new[] { del }, F5Confirm.Missing(new[] { del }, new List<object> { h }));
            Assert.Empty(F5Confirm.Missing(new[] { h }, new List<object> { h }));
            var r = Plugin("Drawing", "RenderTools.cs");
            Assert.Contains("new[] { F5Confirm.DeleteViewToken(Acad.H(vt[name])) }", r);
            Assert.Contains("$\"overwrite the saved view '{name}'\", new[] { Acad.H(vt[name]) }", r);
        }

        // ------------------------------------------------------------ TF5-2: one file, one token

        [Fact]
        public void Spellings_of_one_file_give_one_token()
        {
            var dir = Path.GetTempPath();
            string a = Path.GetFullPath(Path.Combine(dir, "a.png"));
            string b = Path.GetFullPath(Path.Combine(dir, ".", "sub", "..", "a.png"));
            Assert.Equal(F5Confirm.FileToken(a), F5Confirm.FileToken(b));
            Assert.Equal(F5Confirm.FileToken(@"D:\Bao cao\a.xlsx"), F5Confirm.FileToken("d:/BAO CAO/A.XLSX"));
            Assert.NotEqual(F5Confirm.FileToken(@"D:\Bao cao\a.xlsx"), F5Confirm.FileToken(@"D:\Bao cao\a.pdf"));
            var c = Plugin("Drawing", "ConfirmF5.cs");
            Assert.Contains("norm = System.IO.Path.GetFullPath(fullPath);", c);
            Assert.Contains("F5Confirm.FileToken(norm)", c);
        }

        [Fact]
        public void A_token_made_up_by_claude_is_not_an_approval()
        {
            // the plugin only accepts user_confirmed (added by the assistant from the approved card), never confirm_handles
            var tok = F5Confirm.FileToken(@"D:\x.pdf");
            Assert.Equal(new[] { tok }, F5Confirm.Missing(new[] { tok }, null));
            Assert.Equal(new[] { tok }, F5Confirm.Missing(new[] { tok }, new List<object> { "0000000000000000" }));
            Assert.Contains("p.Get(SolidLogic.UserConfirmedParam)", Plugin("Drawing", "ConfirmF5.cs"));
        }

        // ------------------------------------------------------------ the card outside a macro: old flags do not pass

        [Fact]
        public void The_old_overwrite_and_replace_flags_only_count_in_a_macro_replay()
        {
            var c = Plugin("Drawing", "ConfirmF5.cs");
            int macro = c.IndexOf("if (ctx.Dispatcher?.MacroRunning == true)", c.IndexOf("RequireOverwriteCard(", StringComparison.Ordinal), StringComparison.Ordinal);
            int flags = c.IndexOf("oldFlags.Any(f => p.Bool(f, false))", StringComparison.Ordinal);
            int card = c.IndexOf("RequireCard(ctx, p, tool, $\"overwrite the existing file", StringComparison.Ordinal);
            Assert.True(macro > 0 && macro < flags && flags < card, "flags are read only inside the macro branch, the card after it");
            // a macro replay refuses with the old message (never waits for a card it cannot show)
            Assert.Contains("if (!oldFlags.Any(f => p.Bool(f, false))) throw ToolException.Invalid(oldRefusal);", c);
        }

        // ------------------------------------------------------------ the merge with the KTF fixes

        [Fact]
        public void Excel_export_keeps_the_ktf_fixes_and_asks_only_for_a_file_it_will_write()
        {
            var x = Plugin("Drawing", "ExcelExport.cs");
            int forExcel = x.IndexOf("XlsxLogic.ForExcel(table())", StringComparison.Ordinal);
            int empty = x.IndexOf("if (dataRows == 0)", StringComparison.Ordinal);
            int card = x.IndexOf("RequireOverwriteCard(ctx, p, tool, full", StringComparison.Ordinal);
            int write = x.IndexOf("XlsxWriter.Write(full", StringComparison.Ordinal);
            Assert.True(forExcel > 0 && forExcel < empty && empty < card && card < write, "numbers, then no-rows refusal, then the card, then the write");
            Assert.Contains("[\"rows\"] = dataRows", x);
            Assert.Contains("File.Replace(temp, path, null)", Plugin("Core", "Xlsx.cs"));
        }

        [Fact]
        public void The_other_ktf_fixes_survived_the_merge()
        {
            Assert.Contains("Acad.ExtentsInUcs(e, WinFrame)", Plugin("Drawing", "QueryTools.cs"));
            Assert.Contains("UcsFrame.UcsBoxInside(ub.Value.min, ub.Value.max, amin, amax)", Plugin("Drawing", "PlanTo3dTools.cs"));
            Assert.Contains("UcsScope.Enter(frame, UcsMode.Ref, def.Name, mmPerUnit)", Plugin("Drawing", "ToolDispatcher.cs"));
            Assert.Contains("SchedulePrewarm(); // KTF (nghi vấn F4): the other drawing's chat waits ready with its own skills", Plugin("ChatController.cs"));
            Assert.Equal(ParamKind.Height, UcsLogic.KindOf("build_beam", "top_z"));
            Assert.Equal("VN-ML-100-DM", MLeaderStyleLogic.Name("VN", 100, 100)); // F5 names use the KTF unit suffixes
            Assert.True(MLeaderStyleLogic.IsStandardName("VN-ML-100-DM"));
        }

        [Fact]
        public void A_material_named_like_a_preset_stays_the_users_and_a_layer_material_asks_first()
        {
            var r = Plugin("Drawing", "RenderTools.cs");
            Assert.Contains("a material of that very name already in the drawing", r);
            Assert.Contains("give the material to every object of the layer(s)", r);
            Assert.Contains("change the color / opacity of the existing material", r);
        }
    }
}
