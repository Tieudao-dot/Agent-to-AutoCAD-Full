using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Nâng cấp đợt F1, việc 2: the "Cả hai / 2D / 3D" work mode of a Full machine (chủ dự án chốt 14/11/2026).</summary>
    public class NangCapF1ModeTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Theory]
        [InlineData(null, "both")]
        [InlineData("", "both")]
        [InlineData("BOTH", "both")]
        [InlineData(" 2D ", "2d")]
        [InlineData("3d", "3d")]
        [InlineData("2d3d", "both")]
        [InlineData("4d", "both")]
        public void Mode_is_normalized(string raw, string want) => Assert.Equal(want, WorkModeLogic.Normalize(raw));

        [Fact]
        public void Only_a_full_machine_shows_the_switch()
        {
            Assert.True(WorkModeLogic.ShowsSwitch("2d3d"));
            Assert.False(WorkModeLogic.ShowsSwitch("2d"));
            Assert.False(WorkModeLogic.ShowsSwitch("3d"));
            Assert.False(WorkModeLogic.ShowsSwitch(null)); // no plan line: the default (2D) plan
            Assert.False(WorkModeLogic.ShowsSwitch("abc"));
        }

        [Fact]
        public void The_mode_narrows_only_a_full_machine()
        {
            Assert.Equal("2d3d", WorkModeLogic.EffectivePlan("2d3d", "both"));
            Assert.Equal("2d", WorkModeLogic.EffectivePlan("2d3d", "2d"));
            Assert.Equal("3d", WorkModeLogic.EffectivePlan("2d3d", "3d"));
            // a stale "3d" setting on a 2D machine never opens or closes anything
            Assert.Equal("2d", WorkModeLogic.EffectivePlan("2d", "3d"));
            Assert.Equal("3d", WorkModeLogic.EffectivePlan("3d", "2d"));
            Assert.Null(WorkModeLogic.Limiting("2d3d", "both"));
            Assert.Equal("2d", WorkModeLogic.Limiting("2d3d", "2d"));
            Assert.Null(WorkModeLogic.Limiting("2d", "2d"));
            Assert.Null(WorkModeLogic.Limiting("3d", "3d"));
        }

        [Fact]
        public void A_mode_has_exactly_the_tools_of_that_plan()
        {
            Assert.Equal(PlanLogic.AllowedTools("2d"), PlanLogic.AllowedTools(WorkModeLogic.EffectivePlan("2d3d", "2d")));
            Assert.Equal(PlanLogic.AllowedTools("3d"), PlanLogic.AllowedTools(WorkModeLogic.EffectivePlan("2d3d", "3d")));
            Assert.Equal(PlanLogic.AllowedTools("2d3d"), PlanLogic.AllowedTools(WorkModeLogic.EffectivePlan("2d3d", "both")));
            Assert.True(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "draw_solid"));
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "2d"), "draw_solid"));
            Assert.False(PlanLogic.Allowed(WorkModeLogic.EffectivePlan("2d3d", "3d"), "add_text"));
        }

        [Fact]
        public void A_refused_tool_says_switch_the_mode_never_buy_a_plan()
        {
            var m = WorkModeLogic.BlockedMessage("draw_solid", "2d");
            Assert.Contains("Đang ở chế độ 2D, đổi sang 3D hoặc Cả hai", m);
            Assert.Contains("do not mention plans, buying or upgrading", m);
            Assert.DoesNotContain("plugin owner", m);
            Assert.DoesNotContain("liên hệ", m);
            var t = WorkModeLogic.BlockedMessage("add_text", "3d");
            Assert.Contains("Đang ở chế độ 3D, đổi sang 2D hoặc Cả hai", t);
            var p = WorkModeLogic.BlockedParamMessage("insert_block", "attributes", "3d");
            Assert.Contains("chế độ 3D", p);
            Assert.DoesNotContain("plugin owner", p);
            // the plan's own message still invites the owner (a real 2D / 3D machine)
            Assert.Contains("plugin owner", PlanLogic.BlockedMessage("draw_solid", "2d", ""));
        }

        [Fact]
        public void Labels_and_the_note_of_a_switch()
        {
            Assert.Equal("Cả hai", WorkModeLogic.Label("both"));
            Assert.Equal("2D", WorkModeLogic.Label("2d"));
            Assert.Equal("3D", WorkModeLogic.Label("3d"));
            Assert.Contains("từ tin nhắn sau", WorkModeLogic.ChangedNote("2d", true));
            Assert.Contains("lượt đang chạy giữ chế độ cũ", WorkModeLogic.ChangedNote("2d", true));
            Assert.Contains("từ tin nhắn tới", WorkModeLogic.ChangedNote("3d", false));
            Assert.Contains("đủ lệnh 2D và 3D", WorkModeLogic.ChangedNote("both", false));
            Assert.Contains("ít token hơn", WorkModeLogic.Tip("both"));
        }

        [Fact]
        public void The_choice_is_kept_between_autocad_sessions()
        {
            var s = new PaletteSettings();
            Assert.Equal("both", s.WorkMode); // default: Cả hai
            s.WorkMode = "3d";
            var back = PaletteSettings.FromJson(s.ToJson());
            Assert.Equal("3d", back.WorkMode);
            var path = Path.Combine(Path.GetTempPath(), "claudecad-f1-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                s.Save(path);
                Assert.Equal("3d", PaletteSettings.Load(path).WorkMode);
            }
            finally { File.Delete(path); }
            // an older settings file (no work_mode) and a bad value: Cả hai
            Assert.Equal("both", PaletteSettings.FromJson("{\"scale\":\"1:100\"}").WorkMode);
            Assert.Equal("both", PaletteSettings.FromJson("{\"work_mode\":\"xyz\"}").WorkMode);
            Assert.Equal("3d", s.Clone().WorkMode);
        }

        [Fact]
        public void Controller_sends_the_mode_once_per_message_and_macros_keep_the_whole_plan()
        {
            var cc = Src("plugin", "ClaudeCAD.Plugin", "ChatController.cs");
            Assert.Contains("_turnWorkMode = WorkModeLogic.Normalize(_settings.WorkMode);", cc);
            Assert.Contains("var servicePlan = WorkModeLogic.EffectivePlan(Activation.Plan, _turnWorkMode);", cc);
            Assert.Contains("if (limitingMode != null) ctx[\"work_mode\"] = limitingMode;", cc);
            Assert.Contains("public string Plan => _macroRunning ? Activation.Plan : WorkModeLogic.EffectivePlan(Activation.Plan, _turnWorkMode ?? _settings.WorkMode);", cc);
            Assert.Contains("public string WorkMode => _macroRunning ? null :", cc);
            Assert.Contains("Ui.SetWorkMode(shown && WorkModeLogic.ShowsSwitch(Activation.Plan)", cc);
            Assert.Contains("Ui.AddNote(WorkModeLogic.ChangedNote(m, _busy));", cc);
            Assert.Contains("ui.WorkModeChanged += m =>", cc);
            var d = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "ToolDispatcher.cs");
            Assert.Contains("WorkModeLogic.BlockedMessage(tool, _host.WorkMode)", d);
            Assert.Contains("WorkModeLogic.BlockedParamMessage(tool, param, _host.WorkMode)", d);
        }

        [Fact]
        public void The_switch_sits_in_the_top_row_with_a_line_icon()
        {
            var ui = Src("plugin", "ClaudeCAD.Plugin", "UI", "ChatControl.cs");
            Assert.Contains("_modeBtn = BuildModeButton();", ui);
            Assert.Contains("top.Children.Add(_modeBtn);", ui);
            Assert.Contains("Icons.WithText(\"mode\"", ui);
            var icons = Src("plugin", "ClaudeCAD.Plugin", "UI", "Icons.cs");
            Assert.Contains("[\"mode\"] = (", icons);
        }
    }
}
