using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Sidecar
{
    /// <summary>
    /// Phase 3 auto-update.
    ///
    /// 1. Once a day (when the palette opens) fetch the update manifest (latest.json)
    ///    from CLAUDECAD_UPDATE_URL or Contents\update-config.json {"manifest_url": ...}.
    /// 2. If its version is newer, tell the user in the palette.
    /// 3. CLAUDECHATUPDATE downloads the zip, verifies SHA-256, extracts it to a staging
    ///    folder and starts a small script that copies it over the installed bundle
    ///    after AutoCAD exits (the loaded dll is locked while AutoCAD runs).
    /// Private hosting: set CLAUDECAD_UPDATE_TOKEN; it is sent as a Bearer token.
    /// </summary>
    public static class UpdateService
    {
        private static UpdateManifest _available;

        public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version;

        private static string StateDir
        {
            get
            {
                var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "updates");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// <summary>Folder that holds PackageContents.xml, or null when running from a dev build (NETLOAD).</summary>
        public static string InstalledBundle
        {
            get
            {
                var dir = new DirectoryInfo(PluginPaths.DllDirectory);
                for (int i = 0; dir != null && i < 4; i++, dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "PackageContents.xml"))) return dir.FullName;
                return null;
            }
        }

        /// <summary>
        /// The edition of the installed bundle (Contents\edition.json, or the update link of a bundle without it; see
        /// EditionLogic): shown in the self-test and the bug report. None for a dev build (NETLOAD).
        /// </summary>
        public static InstalledEdition BundleEdition
        {
            get
            {
                var bundle = InstalledBundle;
                if (bundle == null) return InstalledEdition.None;
                string Read(string name)
                {
                    try
                    {
                        var f = Path.Combine(bundle, "Contents", name);
                        return File.Exists(f) ? File.ReadAllText(f) : null;
                    }
                    catch { return null; }
                }
                return EditionLogic.Detect(true, Read(EditionLogic.EditionFile), Read(EditionLogic.UpdateConfigFile));
            }
        }

        public static string ManifestUrl
        {
            get
            {
#if BAN_THU_NOI_BO
                return null; // bản thử nội bộ: no automatic update, nothing downloaded or installed over it
#else
                var env = Environment.GetEnvironmentVariable("CLAUDECAD_UPDATE_URL");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                var bundle = InstalledBundle;
                if (bundle == null) return null;
                var cfg = Path.Combine(bundle, "Contents", "update-config.json");
                if (!File.Exists(cfg)) return null;
                try
                {
                    var d = JsonCodec.ParseObject(File.ReadAllText(cfg, Encoding.UTF8));
                    return d.TryGetValue("manifest_url", out var u) ? u as string : null;
                }
                catch (Exception ex)
                {
                    Logger.Warn("update-config.json unreadable: " + ex.Message);
                    return null;
                }
#endif
            }
        }

        private static HttpClient NewClient()
        {
#if ACAD_R24
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#endif
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeCAD-Updater/" + CurrentVersion);
            var token = Environment.GetEnvironmentVariable("CLAUDECAD_UPDATE_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim());
            return c;
        }

        /// <summary>Background daily check; calls notify(message) on any thread when an update exists.</summary>
        public static void CheckInBackground(Action<string> notify, bool force = false)
        {
            var url = ManifestUrl;
            if (string.IsNullOrEmpty(url)) return;
            var stamp = Path.Combine(StateDir, "last-check.txt");
            DateTime? last = null;
            try
            {
                if (File.Exists(stamp) && DateTime.TryParse(File.ReadAllText(stamp), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) last = t;
            }
            catch { }
            if (!force && !UpdateLogic.ShouldCheck(last, DateTime.UtcNow)) return;

            Task.Run(async () =>
            {
                try
                {
                    using (var c = NewClient())
                    {
                        var json = await c.GetStringAsync(url).ConfigureAwait(false);
                        var m = UpdateLogic.ParseManifest(json);
                        File.WriteAllText(stamp, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                        if (UpdateLogic.IsNewer(CurrentVersion, m.Version))
                        {
                            _available = m;
                            Logger.Info($"update available: {m.Version}");
                            notify?.Invoke($"Có bản cập nhật ClaudeCAD {m.Version} (đang dùng {CurrentVersion}). Gõ CLAUDECHATUPDATE để tải; bản mới được cài khi đóng AutoCAD." +
                                (string.IsNullOrEmpty(m.Notes) ? "" : "\n" + m.Notes));
                        }
                        else if (force) notify?.Invoke($"ClaudeCAD {CurrentVersion} là bản mới nhất.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("update check failed: " + ex.Message);
                    if (force) notify?.Invoke("Không kiểm tra được cập nhật: " + ex.Message);
                }
            });
        }

        /// <summary>CLAUDECHATUPDATE: download, verify, stage and schedule the update.</summary>
        public static async Task<string> DownloadAndStageAsync()
        {
#if BAN_THU_NOI_BO
            await Task.CompletedTask.ConfigureAwait(false);
            return "Bản thử nội bộ không tự cập nhật. Khi có bản chính thức, chạy bộ cài của bản đó (cài đè lên, giữ lịch sử chat và cài đặt).";
#else
            var bundle = InstalledBundle;
            if (bundle == null) return "Plugin đang chạy từ bản build dev (NETLOAD), không có bundle để cập nhật. Dùng git pull + build lại.";
            var url = ManifestUrl;
            if (string.IsNullOrEmpty(url)) return "Chưa cấu hình nguồn cập nhật (CLAUDECAD_UPDATE_URL hoặc Contents\\update-config.json).";

            using (var c = NewClient())
            {
                var m = _available ?? UpdateLogic.ParseManifest(await c.GetStringAsync(url).ConfigureAwait(false));
                if (!UpdateLogic.IsNewer(CurrentVersion, m.Version)) return $"ClaudeCAD {CurrentVersion} là bản mới nhất.";

                var zip = Path.Combine(StateDir, $"ClaudeCAD-{m.Version}.zip");
                using (var resp = await c.GetAsync(m.Url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();
                    using (var fs = File.Create(zip)) await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
                }
                string hash;
                using (var fs = File.OpenRead(zip)) hash = UpdateLogic.Sha256Hex(fs);
                if (!hash.Equals(m.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(zip);
                    return $"Tải bản cập nhật bị sai checksum (SHA-256 {hash} ≠ {m.Sha256}); đã hủy.";
                }

                var stageRoot = Path.Combine(StateDir, "stage");
                if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
                ZipFile.ExtractToDirectory(zip, stageRoot);
                var staged = Directory.GetFiles(stageRoot, "PackageContents.xml", SearchOption.AllDirectories)
                    .Select(Path.GetDirectoryName).OrderBy(p => p.Length).FirstOrDefault();
                if (staged == null) return "Gói cập nhật không chứa PackageContents.xml; đã hủy.";

                var script = Path.Combine(StateDir, "apply-update.cmd");
                File.WriteAllText(script, UpdateLogic.ApplyScript(Process.GetCurrentProcess().Id, staged, bundle, Path.Combine(StateDir, "apply-update.log")), Encoding.ASCII);
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
                Logger.Info($"update {m.Version} staged at {staged}; applier waiting for AutoCAD to exit");
                return $"Đã tải ClaudeCAD {m.Version}. Đóng AutoCAD để cài; lần mở sau sẽ dùng bản mới (skill cậu đã sửa được giữ nguyên).";
            }
#endif
        }
    }
}
