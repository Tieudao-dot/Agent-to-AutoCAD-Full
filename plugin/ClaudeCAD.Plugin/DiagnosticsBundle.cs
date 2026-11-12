using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using ClaudeCAD.Plugin.Sidecar;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin
{
    /// <summary>
    /// "Gửi báo cáo lỗi": one zip on the Desktop with what is needed to find an installation or runtime
    /// problem on another computer — machine / AutoCAD / plugin info, the assistant and sign-in state,
    /// the self-test reports, recent plugin and assistant logs and the installer log. Chat history,
    /// drawings, project notes and keys are NOT included; logs are scrubbed of keys and tokens.
    /// </summary>
    public static class DiagnosticsBundle
    {
        public static string Create(string info)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) desktop = Logger.LogDirectory;
            var zipPath = Path.Combine(desktop, $"ClaudeCAD-baocao-{Environment.MachineName}-{DateTime.Now:yyyyMMdd-HHmm}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                Add(zip, "thong-tin.txt", info);
                var logDir = Logger.LogDirectory;
                if (Directory.Exists(logDir))
                {
                    var files = Directory.GetFiles(logDir).Select(f => (f, File.GetLastWriteTime(f))).ToList();
                    foreach (var f in Diagnostics.RecentFiles(files.Where(x => Path.GetFileName(x.Item1).StartsWith("selftest-", StringComparison.OrdinalIgnoreCase)), DateTime.Now, 30, 3))
                        AddFile(zip, "tu-kiem-tra/" + Path.GetFileName(f), f);
                    foreach (var f in Diagnostics.RecentFiles(files.Where(x => x.Item1.EndsWith(".log", StringComparison.OrdinalIgnoreCase)), DateTime.Now, Diagnostics.LogDays, 12))
                        AddFile(zip, "log/" + Path.GetFileName(f), f);
                }
                try
                {
                    var temp = Path.GetTempPath();
                    var setupLogs = Directory.GetFiles(temp, "Setup Log*.txt")
                        .Where(f => Diagnostics.IsClaudeCadSetupLog(Path.GetFileName(f), Head(f)))
                        .Select(f => (f, File.GetLastWriteTime(f)));
                    foreach (var f in Diagnostics.RecentFiles(setupLogs, DateTime.Now, 60, 3))
                        AddFile(zip, "cai-dat/" + Path.GetFileName(f), f);
                }
                catch (Exception ex) { Add(zip, "cai-dat/khong-doc-duoc.txt", ex.Message); }
            }
            return zipPath;
        }

        private static string Head(string f)
        {
            try
            {
                using (var r = new StreamReader(new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    var buf = new char[4000];
                    int n = r.Read(buf, 0, buf.Length);
                    return new string(buf, 0, n);
                }
            }
            catch { return ""; }
        }

        private static void Add(ZipArchive zip, string name, string text)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(true))) w.Write(Diagnostics.Redact(text ?? ""));
        }

        private static void AddFile(ZipArchive zip, string name, string path)
        {
            try { Add(zip, name, Diagnostics.StripChatContent(Diagnostics.ReadTail(path))); }
            catch (Exception ex) { Add(zip, name + ".loi.txt", "Không đọc được: " + ex.Message); }
        }

        /// <summary>Machine, AutoCAD, plugin, paths, assistant and sign-in state (no chat content).</summary>
        public static string Info(ChatController c, AuthStatus auth)
        {
            var sb = new StringBuilder();
            sb.AppendLine(ProductInfo.DisplayName + " — báo cáo lỗi " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Máy: " + Environment.MachineName + " · Windows " + Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " x64" : ""));
            try { sb.AppendLine("AutoCAD: " + AcApp.Version + " · " + Convert.ToString(AcApp.GetSystemVariable("ACADVER")) + " · " + Convert.ToString(AcApp.GetSystemVariable("PRODUCT"))); } catch { }
            sb.AppendLine("Plugin: " + DrawingService.PluginVersion + " · edition " + EditionLogic.Label(UpdateService.BundleEdition) + " · bản " + DrawingService.PluginBuild + " · runtime " + DrawingService.RuntimeName);
            sb.AppendLine("Thư mục plugin: " + PluginPaths.DllDirectory);
            sb.AppendLine("Contents: " + (PluginPaths.ContentsDirectory ?? "(không phải bản cài)"));
            sb.AppendLine("Node: " + (PluginPaths.NodeExecutable ?? "KHÔNG THẤY"));
            sb.AppendLine("Trợ lý: " + (PluginPaths.AgentEntry ?? "KHÔNG THẤY"));
            sb.AppendLine("Claude Code đi kèm: " + (PluginPaths.ClaudeExecutable ?? "KHÔNG THẤY"));
            sb.AppendLine("Skill: " + (PluginPaths.SkillsDirectory ?? "KHÔNG THẤY"));
            sb.AppendLine("Tài khoản: " + (auth?.Label ?? "?") + (string.IsNullOrEmpty(auth?.Method) ? "" : " (" + auth.Method + ")"));
            sb.AppendLine("Biến ANTHROPIC_API_KEY: " + (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) ? "không" : "có (đã ẩn)") + " · API key lưu trong plugin: " + (ApiKeyStore.HasKey ? "có (đã ẩn)" : "không"));
            try { sb.AppendLine("TRUSTEDPATHS: " + Convert.ToString(AcApp.GetSystemVariable("TRUSTEDPATHS"))); } catch { }
            try { sb.AppendLine("SECURELOAD: " + Convert.ToString(AcApp.GetSystemVariable("SECURELOAD"))); } catch { }
            sb.AppendLine();
            sb.AppendLine("Trạng thái:");
            foreach (var (name, ok, detail) in c.Diagnostics()) sb.AppendLine($"  {(ok ? "✓" : "✗")} {name}: {detail}");
            sb.AppendLine();
            sb.AppendLine("Không kèm: lịch sử chat, bản vẽ, ghi chú dự án, macro, API key / token (log đã được lọc).");
            return sb.ToString();
        }
    }
}
