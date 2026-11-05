using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(ClaudeCAD.Plugin.PluginApp))]
[assembly: CommandClass(typeof(ClaudeCAD.Plugin.Commands))]

namespace ClaudeCAD.Plugin
{
    /// <summary>Entry point loaded by NETLOAD or the autoloader bundle.</summary>
    public sealed class PluginApp : IExtensionApplication
    {
        private static ChatController _controller;

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
                var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nClaudeCAD đã nạp. Gõ CLAUDECHAT để mở khung chat.\n");
            }
            catch (System.Exception ex)
            {
                Logger.Error("Initialize failed", ex);
            }
        }

        public void Terminate() => Shutdown();

        private static void Shutdown()
        {
            try
            {
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
