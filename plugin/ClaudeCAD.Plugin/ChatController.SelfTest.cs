using System;
using System.Collections.Generic;
using System.IO;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;

namespace ClaudeCAD.Plugin
{
    /// <summary>
    /// CLAUDECHATSELFTEST: the checks that need the controller's own state (the real palette, the chat's
    /// skills, the queued message). They run on AutoCAD's main thread and put everything back as it was.
    /// </summary>
    public sealed partial class ChatController
    {
        public List<(string name, bool ok, string detail)> UiSelfChecks()
        {
            var list = new List<(string, bool, string)>();
            void Add(string name, Func<SelfTestRules.Verdict> check)
            {
                try
                {
                    var v = check();
                    list.Add((name, v.Ok, v.Detail));
                }
                catch (Exception ex)
                {
                    // one broken check must not hide the others
                    Logger.Error("selftest: " + name, ex);
                    list.Add((name, false, ex.GetType().Name + ": " + ex.Message));
                }
            }

            Add("Style giao diện của khung chat (nút phẳng, màu theo theme)", StyleCheck);
            Add("Thu gọn rồi mở lại khung chat (không mất tin nhắn)", CompactCheck);
            Add("Hàng chờ gửi (gõ thêm ý khi Claude đang làm)", QueueCheck);
            Add("Skill theo đoạn chat", SkillCheck);
            return list;
        }

        private SelfTestRules.Verdict StyleCheck()
        {
            var probe = Ui.ProbeForSelfTest();
            if (!Theme.StylesLoaded) return new SelfTestRules.Verdict(false, "không nạp được style → khung chat dùng nút mặc định của Windows (xem log: «palette styles not loaded»)");
            if (!probe.StylesMerged) return new SelfTestRules.Verdict(false, "style đã nạp nhưng chưa gắn vào khung chat");
            if (!probe.BrushesSet) return new SelfTestRules.Verdict(false, "khung chat thiếu màu nền / màu chữ của theme");
            return new SelfTestRules.Verdict(true, "đã nạp và gắn vào khung chat · giao diện đang đặt: " + (string.IsNullOrEmpty(_settings.Theme) ? "theo AutoCAD" : _settings.Theme));
        }

        private SelfTestRules.Verdict CompactCheck()
        {
            // 1) a throw-away control with known messages: the user's chat is not used as test data
            var trial = new ChatControl();
            const int sent = 3;
            int empty = trial.ProbeForSelfTest().Messages;
            for (int i = 1; i <= sent; i++) trial.AddUserMessage(new ChatEntry { Role = "user", Text = "tự kiểm tra " + i });
            var t0 = trial.ProbeForSelfTest();
            if (t0.Messages - empty != sent) return new SelfTestRules.Verdict(false, $"khung thử: thêm {sent} tin nhưng chỉ thấy {t0.Messages - empty}");
            trial.SetCompact(true);
            var t1 = trial.ProbeForSelfTest();
            trial.SetCompact(false);
            var t2 = trial.ProbeForSelfTest();
            var v = SelfTestRules.CheckCompact(t0.Messages, t1.Messages, t2.Messages, t1.Compact && !t1.ConversationShown, !t2.Compact && t2.ConversationShown);
            if (!v.Ok) return new SelfTestRules.Verdict(false, "khung thử: " + v.Detail);

            // 2) the real palette: there and back, so it ends in the state the user left it in
            var ui = Ui;
            bool was = ui.IsCompact;
            var size0 = _palette.CurrentSize;
            var p0 = ui.ProbeForSelfTest();
            ui.SetCompact(!was);
            var p1 = ui.ProbeForSelfTest();
            ui.SetCompact(was);
            var p2 = ui.ProbeForSelfTest();
            var size1 = _palette.CurrentSize;
            var compact = was ? p2 : p1;   // the probe taken while compact
            var full = was ? p1 : p2;      // the probe taken while expanded
            v = SelfTestRules.CheckCompact(p0.Messages, p1.Messages, p2.Messages,
                compact.Compact && !compact.ConversationShown, !full.Compact && full.ConversationShown);
            if (!v.Ok) return new SelfTestRules.Verdict(false, "khung chat thật: " + v.Detail);
            if (p2.Compact != was) return new SelfTestRules.Verdict(false, "khung chat thật không về lại trạng thái " + (was ? "thu gọn" : "mở rộng") + " ban đầu");

            string where = !_palette.Created ? "khung chat chưa mở trong phiên này nên chưa thử đổi kích thước palette"
                : !_palette.Floating ? "palette đang neo: cột neo giữ nguyên"
                : $"palette nổi {size0.Width}×{size0.Height}";
            if (_palette.Created && _palette.Floating && _palette.InView
                && !SelfTestRules.SameSize(size0.Width, size0.Height, size1.Width, size1.Height))
                return new SelfTestRules.Verdict(false, $"palette không về kích thước cũ: {size0.Width}×{size0.Height} → {size1.Width}×{size1.Height}");
            return new SelfTestRules.Verdict(true, $"khung thử {sent} tin + khung chat thật ({p0.Messages} mục) còn nguyên · {where}");
        }

        private SelfTestRules.Verdict QueueCheck()
        {
            var trial = new ChatControl();
            trial.SetQueued("tự kiểm tra");
            bool shows = trial.ProbeForSelfTest().QueuedShown;
            trial.SetQueued(null);
            bool hides = !trial.ProbeForSelfTest().QueuedShown;
            return SelfTestRules.CheckQueue(_busy, _queued != null, shows, hides);
        }

        private SelfTestRules.Verdict SkillCheck()
        {
            string root = _skillStore?.Root ?? SkillsRoot;
            bool exists = !string.IsNullOrEmpty(root) && Directory.Exists(root);
            var library = !exists ? new List<SkillInfo>() : (_skillStore ?? new SkillStore(root)).Scan();
            var chosen = SkillSelection.Choice(_transcript?.Skills, _settings.LastSkills, library);
            var used = SkillSelection.ForChat(_transcript?.Skills, _settings.LastSkills, library);
            return SelfTestRules.CheckSkills(exists, root, library, chosen, used);
        }
    }
}
