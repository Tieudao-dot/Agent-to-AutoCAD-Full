using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Windows notification when a long turn finishes while the user works in another
    /// application (feature 15). Uses a tray balloon, which Windows 10/11 shows as a toast.
    /// </summary>
    public static class Notifier
    {
        private static NotifyIcon _icon;
        private static Timer _hide;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /// <summary>True when the foreground window belongs to another process (AutoCAD and its palettes are in-process).</summary>
        public static bool AutoCadInBackground()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return true;
                GetWindowThreadProcessId(hwnd, out var pid);
                return pid != (uint)Process.GetCurrentProcess().Id;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Must be called on the main (UI) thread.</summary>
        public static void Show(string title, string text)
        {
            try
            {
                if (_icon == null)
                {
                    _icon = new NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "ClaudeCAD" };
                    _hide = new Timer { Interval = 10000 };
                    _hide.Tick += (s, e) => { _hide.Stop(); if (_icon != null) _icon.Visible = false; };
                }
                _icon.Visible = true;
                _icon.ShowBalloonTip(6000, title, text.Length > 200 ? text.Substring(0, 197) + "…" : text, ToolTipIcon.Info);
                _hide.Stop();
                _hide.Start();
            }
            catch (Exception ex)
            {
                Logger.Warn("notification failed: " + ex.Message);
            }
        }

        public static void Dispose()
        {
            try { _icon?.Dispose(); } catch { }
            _icon = null;
        }
    }
}
