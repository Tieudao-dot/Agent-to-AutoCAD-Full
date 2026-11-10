using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using ClaudeCAD.Plugin.UI;
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
                TrustBundleFolder();
                ScheduleStatusTray();
                ScheduleFirstStart();
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

        /// <summary>
        /// Adds the bundle's Contents folder to TRUSTEDPATHS (the installer does it too), so AutoCAD stops asking
        /// whether to load the plugin. Only for an installed bundle (ApplicationPlugins), never for a dev build.
        /// </summary>
        private static void TrustBundleFolder()
        {
            try
            {
                var contents = Sidecar.PluginPaths.ContentsDirectory;
                if (contents == null || contents.IndexOf("ApplicationPlugins", StringComparison.OrdinalIgnoreCase) < 0) return;
                var entry = System.IO.Path.Combine(contents, "...");
                var current = Convert.ToString(AcApp.GetSystemVariable("TRUSTEDPATHS")) ?? "";
                foreach (var part in current.Split(';'))
                    if (string.Equals(part.Trim().TrimEnd('\\'), entry, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(part.Trim().TrimEnd('\\'), contents.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                AcApp.SetSystemVariable("TRUSTEDPATHS", string.IsNullOrWhiteSpace(current) ? entry : current.TrimEnd(';') + ";" + entry);
                Logger.Info("TRUSTEDPATHS += " + entry);
            }
            catch (System.Exception ex)
            {
                Logger.Warn("TRUSTEDPATHS not updated: " + ex.Message);
            }
        }

        /// <summary>
        /// ClaudeCAD's icon in the status bar tray, from AutoCAD start (the status bar only exists once
        /// AutoCAD is idle). A click opens the palette, creating the controller on demand.
        /// </summary>
        private static void ScheduleStatusTray()
        {
            try
            {
                StatusTray.Clicked += OnTrayClicked;
                int tries = 0;
                EventHandler onIdle = null;
                onIdle = (s, e) =>
                {
                    // give up quietly after a while: the icon is a convenience, never a reason to keep polling
                    if (StatusTray.Install() || ++tries > 200) AcApp.Idle -= onIdle;
                };
                AcApp.Idle += onIdle;
            }
            catch (System.Exception ex)
            {
                Logger.Warn("status bar icon not scheduled: " + ex.Message);
            }
        }

        private static void OnTrayClicked()
        {
            try { Controller.ShowPalette(); }
            catch (System.Exception ex) { Logger.Error("open from the status bar failed", ex); }
        }

        private static string FirstStartFlag =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "first-start.done");

        /// <summary>First AutoCAD start after installing: open the palette with the "Bắt đầu" card once AutoCAD is idle.</summary>
        private static void ScheduleFirstStart()
        {
            try
            {
                if (System.IO.File.Exists(FirstStartFlag) || Sidecar.PluginPaths.ContentsDirectory == null) return;
                EventHandler onIdle = null;
                onIdle = (s, e) =>
                {
                    if (AcApp.DocumentManager.MdiActiveDocument == null) return; // wait for a drawing (start tab)
                    AcApp.Idle -= onIdle;
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FirstStartFlag));
                        System.IO.File.WriteAllText(FirstStartFlag, DateTime.Now.ToString("o"));
                        Controller.ShowWelcome(autoLogin: true);
                    }
                    catch (System.Exception ex) { Logger.Error("first start failed", ex); }
                };
                AcApp.Idle += onIdle;
            }
            catch (System.Exception ex)
            {
                Logger.Warn("first start not scheduled: " + ex.Message);
            }
        }

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
                try { StatusTray.Clicked -= OnTrayClicked; StatusTray.Remove(); } catch { }
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

        /// <summary>Self-test on a temporary drawing: off-screen picture, undo group, events, snapshot, versions, connection.</summary>
        [CommandMethod("CLAUDECHATSELFTEST", CommandFlags.Session)]
        public void SelfTestCommand()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ed?.WriteMessage("\nClaudeCAD: đang tự kiểm tra trên một bản vẽ tạm (bản vẽ của cậu không bị đụng tới)...\n");
                var summary = SelfTest.Run();
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nClaudeCAD tự kiểm tra: " + summary + "\n");
            }
            catch (System.Exception ex)
            {
                Logger.Error("CLAUDECHATSELFTEST failed", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nClaudeCAD tự kiểm tra lỗi: " + ex.Message + "\n");
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
