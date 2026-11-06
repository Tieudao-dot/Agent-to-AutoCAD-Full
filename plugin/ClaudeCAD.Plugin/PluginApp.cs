using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(ClaudeCAD.Plugin.PluginApp))]
[assembly: CommandClass(typeof(ClaudeCAD.Plugin.Commands))]
[assembly: CommandClass(typeof(ClaudeCAD.Plugin.Drawing.InteractionCommands))]

namespace ClaudeCAD.Plugin
{
    /// <summary>Entry point loaded by NETLOAD or the autoloader bundle.</summary>
    public sealed class PluginApp : IExtensionApplication
    {
        private static ChatController _controller;

        /// <summary>Ctrl+Shift+Space opens the palette (setting "Phím tắt").</summary>
        internal static bool HotkeyEnabled = true;

        internal static ChatController Controller
        {
            get
            {
                if (_controller == null) _controller = new ChatController();
                return _controller;
            }
        }

        public void Initialize()
        {
            try
            {
                MainThread.Initialize();
                Logger.Info($"ClaudeCAD {DrawingService.PluginVersion} loaded into AutoCAD {AcApp.Version}");
                AcApp.QuitWillStart += (s, e) => Shutdown();
                var settings = PaletteSettings.Load();
                HotkeyEnabled = settings.Hotkey;
                // manual edits are tracked from AutoCAD start, palette open or not
                DrawingHistory.Instance.NextToDrawing = settings.SnapshotNextToDrawing;
                DrawingHistory.Instance.Start();
                AcApp.PreTranslateMessage += OnPreTranslateMessage;
                var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nClaudeCAD đã nạp. Gõ CLAUDECHAT để mở khung chat.\n");
            }
            catch (System.Exception ex)
            {
                Logger.Error("Initialize failed", ex);
            }
        }

        public void Terminate() => Shutdown();

        private const int WM_KEYDOWN = 0x0100;
        private const int VK_SPACE = 0x20;

        /// <summary>Ctrl+Shift+Space anywhere in AutoCAD: open the chat palette and focus the input box.</summary>
        private static void OnPreTranslateMessage(object sender, PreTranslateMessageEventArgs e)
        {
            try
            {
                if (!HotkeyEnabled || e.Message.message != WM_KEYDOWN || e.Message.wParam.ToInt64() != VK_SPACE) return;
                if (System.Windows.Forms.Control.ModifierKeys != (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)) return;
                e.Handled = true;
                Controller.FocusInput();
            }
            catch (System.Exception ex)
            {
                Logger.Error("hotkey failed", ex);
            }
        }

        private static void Shutdown()
        {
            try
            {
                try { AcApp.PreTranslateMessage -= OnPreTranslateMessage; } catch { }
                _controller?.Dispose();
                _controller = null;
            }
            catch (System.Exception ex)
            {
                Logger.Error("Shutdown failed", ex);
            }
        }
    }

    public sealed class Commands
    {
        /// <summary>Opens the chat palette (spec §4).</summary>
        [CommandMethod("CLAUDECHAT", CommandFlags.Session)]
        public void ClaudeChat()
        {
            try
            {
                PluginApp.Controller.ShowPalette();
            }
            catch (System.Exception ex)
            {
                Logger.Error("CLAUDECHAT failed", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nClaudeCAD lỗi: " + ex.Message + "\n");
            }
        }

        /// <summary>Short command: open the palette with the caret in the input box (also Ctrl+Shift+Space).</summary>
        [CommandMethod("CC", CommandFlags.Session)]
        public void ClaudeChatShort()
        {
            try
            {
                PluginApp.Controller.FocusInput();
            }
            catch (System.Exception ex)
            {
                Logger.Error("CC failed", ex);
            }
        }

        /// <summary>Kills and restarts the sidecar (also resets the crash counter).</summary>
        [CommandMethod("CLAUDECHATRESTART", CommandFlags.Session)]
        public void Restart()
        {
            try
            {
                PluginApp.Controller.RestartSidecar();
            }
            catch (System.Exception ex)
            {
                Logger.Error("CLAUDECHATRESTART failed", ex);
            }
        }

        /// <summary>Phase 3: download and stage a newer ClaudeCAD bundle (installed when AutoCAD closes).</summary>
        [CommandMethod("CLAUDECHATUPDATE", CommandFlags.Session)]
        public void Update()
        {
            try
            {
                PluginApp.Controller.RunUpdate();
            }
            catch (System.Exception ex)
            {
                Logger.Error("CLAUDECHATUPDATE failed", ex);
            }
        }

        /// <summary>Opens the log folder in Explorer.</summary>
        [CommandMethod("CLAUDECHATLOG", CommandFlags.Session)]
        public void OpenLog()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + Logger.LogDirectory + "\"");
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nLog: " + Logger.LogDirectory + " (" + ex.Message + ")\n");
            }
        }
    }
}
