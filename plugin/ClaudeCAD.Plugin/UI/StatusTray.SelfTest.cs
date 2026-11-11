using System;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>CLAUDECHATSELFTEST: is the status bar icon really there?</summary>
    public static partial class StatusTray
    {
        /// <summary>
        /// Checks the icon without changing what it shows: added to AutoCAD's tray, has a picture and a
        /// tooltip, and every state can be drawn for both color themes.
        /// </summary>
        public static SelfTestRules.Verdict SelfCheck()
        {
            if (_gaveUp) return new SelfTestRules.Verdict(false, "AutoCAD không cho thêm icon vào thanh trạng thái (xem log: «status bar icon not added»)");
            if (_item == null) return new SelfTestRules.Verdict(false, "icon chưa được gắn (thanh trạng thái chưa sẵn sàng lúc AutoCAD mở) — đóng mở lại AutoCAD rồi thử lại");
            var bar = AcApp.StatusBar;
            if (bar == null) return new SelfTestRules.Verdict(false, "AutoCAD không có thanh trạng thái (STATUSBAR đang tắt?)");
            if (!bar.TrayItems.Contains(_item)) return new SelfTestRules.Verdict(false, "icon đã tạo nhưng không còn trên thanh trạng thái");
            if (_item.Icon == null) return new SelfTestRules.Verdict(false, "icon trên thanh trạng thái không có hình");
            if (string.IsNullOrWhiteSpace(_item.ToolTipText)) return new SelfTestRules.Verdict(false, "icon không có tooltip");

            int drawn = 0;
            foreach (State state in Enum.GetValues(typeof(State)))
                foreach (bool dark in new[] { true, false })
                {
                    if (IconFor(state, dark) == null) return new SelfTestRules.Verdict(false, $"không vẽ được icon trạng thái {state} ({(dark ? "tối" : "sáng")})");
                    drawn++;
                }
            return new SelfTestRules.Verdict(true,
                $"đã gắn{(_item.Visible ? "" : " (đang ẩn)")} · trạng thái {_state} · vẽ được {drawn} icon (4 trạng thái × 2 theme) · tooltip «{_item.ToolTipText}»");
        }
    }
}
