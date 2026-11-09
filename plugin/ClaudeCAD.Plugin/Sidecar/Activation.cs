using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ClaudeCAD.Plugin.Core;
using Microsoft.Win32;

namespace ClaudeCAD.Plugin.Sidecar
{
    /// <summary>
    /// v0.9.4 per-machine activation on Windows: machine code, the saved licence, and the owner's activation
    /// service (Google Apps Script web app) that emails the owner a key ("request") and turns a right key into a
    /// signed licence ("activate"). The installer does the same; the plugin checks again at every AutoCAD start so
    /// copying the bundle folder or installing from the .zip does not skip the key.
    /// </summary>
    public static class Activation
    {
        private static string _machine;
        private static LicenseCheck _check;
        private static (string url, string contact)? _config;

        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD");
        public static string LicenseFile => Path.Combine(Root, "license.txt");
        private static string RequestStamp => Path.Combine(Root, "activation-request.txt");

        public static string MachineCode
        {
            get
            {
                if (_machine != null) return _machine;
                string guid = null;
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                        guid = k?.GetValue("MachineGuid") as string;
                }
                catch (Exception ex) { Logger.Warn("MachineGuid unreadable: " + ex.Message); }
                return _machine = LicenseLogic.MachineCode(guid, Environment.MachineName);
            }
        }

        public static LicenseCheck Check
        {
            get
            {
                if (_check != null) return _check;
                string text = null;
                try { if (File.Exists(LicenseFile)) text = File.ReadAllText(LicenseFile, Encoding.UTF8); }
                catch (Exception ex) { Logger.Warn("license unreadable: " + ex.Message); }
                _check = LicenseLogic.Verify(text, MachineCode);
                if (!_check.Ok) Logger.Info("not activated: " + _check.Reason);
                return _check;
            }
        }

        public static bool IsActivated => Check.Ok;

        public static string StatusText => IsActivated
            ? $"đã kích hoạt (mã máy {MachineCode}, từ {Check.Issued})"
            : $"chưa kích hoạt — {Check.Reason} (mã máy {MachineCode})";

        /// <summary>Contents\activation.json {"url", "contact"} written by make-bundle; CLAUDECAD_ACTIVATION_URL overrides the url.</summary>
        private static (string url, string contact) Config
        {
            get
            {
                if (_config.HasValue) return _config.Value;
                string url = null, contact = null;
                try
                {
                    var dir = PluginPaths.ContentsDirectory;
                    var file = dir == null ? null : Path.Combine(dir, "activation.json");
                    if (file != null && File.Exists(file))
                    {
                        var d = JsonCodec.ParseObject(File.ReadAllText(file, Encoding.UTF8));
                        url = d.TryGetValue("url", out var u) ? u as string : null;
                        contact = d.TryGetValue("contact", out var c) ? c as string : null;
                    }
                }
                catch (Exception ex) { Logger.Warn("activation.json unreadable: " + ex.Message); }
                var env = Environment.GetEnvironmentVariable("CLAUDECAD_ACTIVATION_URL");
                if (!string.IsNullOrWhiteSpace(env)) url = env.Trim();
                _config = (string.IsNullOrWhiteSpace(url) ? null : url.Trim(), string.IsNullOrWhiteSpace(contact) ? LicenseLogic.DefaultContact : contact.Trim());
                return _config.Value;
            }
        }

        public static string ServiceUrl => Config.url;
        public static string Contact => Config.contact;
        public static string DeviceName => Environment.MachineName;

        private static HttpClient NewClient()
        {
#if ACAD_R24
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#endif
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) }; // Apps Script redirects once (script.googleusercontent.com)
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeCAD/" + UpdateService.CurrentVersion);
            return c;
        }

        /// <summary>True when no request was sent from this machine in the last day (auto-request on opening the palette).</summary>
        public static bool ShouldAutoRequest
        {
            get
            {
                try
                {
                    if (!File.Exists(RequestStamp)) return true;
                    var t = DateTime.Parse(File.ReadAllText(RequestStamp).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    return DateTime.UtcNow - t > TimeSpan.FromDays(1);
                }
                catch { return true; }
            }
        }

        /// <summary>Asks the service to email the owner this machine's key. Returns a sentence for the palette.</summary>
        public static async Task<(bool ok, string message)> RequestAsync()
        {
            if (ServiceUrl == null) return (false, "Bản plugin này chưa có máy chủ kích hoạt. " + Contact + " và gửi mã máy " + MachineCode + ".");
            var url = LicenseLogic.BuildUrl(ServiceUrl, new[]
            {
                new KeyValuePair<string, string>("action", "request"),
                new KeyValuePair<string, string>("machine", MachineCode),
                new KeyValuePair<string, string>("device", DeviceName),
                new KeyValuePair<string, string>("user", Environment.UserName),
                new KeyValuePair<string, string>("from", "plugin " + UpdateService.CurrentVersion),
            });
            try
            {
                using (var c = NewClient())
                {
                    var r = LicenseLogic.ParseResponse(await c.GetStringAsync(url).ConfigureAwait(false));
                    if (!r.ok) return (false, "Không gửi được yêu cầu: " + r.error);
                    try { Directory.CreateDirectory(Root); File.WriteAllText(RequestStamp, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)); } catch { }
                    Logger.Info("activation requested for " + MachineCode);
                    return (true, "Đã gửi yêu cầu kích hoạt cho chủ plugin. " + Contact + " để nhận key.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("activation request failed: " + ex.Message);
                return (false, "Không gửi được yêu cầu (kiểm tra mạng). " + Contact + " và gửi mã máy " + MachineCode + ".");
            }
        }

        /// <summary>Key (online) or offline activation code → saved licence. Returns a sentence for the palette.</summary>
        public static async Task<(bool ok, string message)> ActivateAsync(string input)
        {
            input = (input ?? "").Trim();
            string license;
            if (LicenseLogic.IsLicenseText(input)) license = input;
            else if (LicenseLogic.IsShortKey(input))
            {
                if (ServiceUrl == null) return (false, "Bản plugin này chưa có máy chủ kích hoạt — xin chủ plugin mã kích hoạt offline (một dòng dài bắt đầu bằng CCL1|).");
                var url = LicenseLogic.BuildUrl(ServiceUrl, new[]
                {
                    new KeyValuePair<string, string>("action", "activate"),
                    new KeyValuePair<string, string>("machine", MachineCode),
                    new KeyValuePair<string, string>("key", LicenseLogic.NormalizeKey(input)),
                });
                try
                {
                    using (var c = NewClient())
                    {
                        var r = LicenseLogic.ParseResponse(await c.GetStringAsync(url).ConfigureAwait(false));
                        if (!r.ok || string.IsNullOrEmpty(r.license)) return (false, r.error ?? "Key không đúng.");
                        license = r.license;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("activation failed: " + ex.Message);
                    return (false, "Không kết nối được máy chủ kích hoạt (kiểm tra mạng rồi thử lại). Máy không có mạng: xin chủ plugin mã kích hoạt offline.");
                }
            }
            else return (false, "Key gồm 16 ký tự dạng XXXX-XXXX-XXXX-XXXX (hoặc mã offline bắt đầu bằng CCL1|).");

            var check = LicenseLogic.Verify(license, MachineCode);
            if (!check.Ok) return (false, "Mã kích hoạt không dùng được: " + check.Reason + ".");
            Directory.CreateDirectory(Root);
            File.WriteAllText(LicenseFile, license.Trim(), new UTF8Encoding(false));
            _check = check;
            Logger.Info("activated " + MachineCode);
            return (true, "Đã kích hoạt ClaudeCAD cho máy này (vĩnh viễn). Cập nhật bản mới không cần nhập lại key.");
        }
    }
}
