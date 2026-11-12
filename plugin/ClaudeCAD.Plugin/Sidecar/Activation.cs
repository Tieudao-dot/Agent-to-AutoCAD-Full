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
    /// service (Google Apps Script web app + Google Sheet "Thiết bị") that emails the owner a key ("request"),
    /// turns a right key into a signed licence ("activate") and answers which machines the owner allows
    /// ("status": signed CCS1 line, saved in status.txt). The installer does the same; the plugin checks again at
    /// every AutoCAD start so copying the bundle folder or installing from the .zip does not skip the key, and it
    /// re-checks the owner's Active/Disabled decision at start and every ~6 hours — a machine that cannot reach
    /// the service keeps running on its last signed status for at most LicenseLogic.StatusMaxAgeDays days.
    /// </summary>
    public static class Activation
    {
        private static string _machine;
        private static LicenseCheck _check;
        private static StatusCheck _status;
        private static PlanCheck _plan;
        private static DateTime _statusDay; // AgeDays/Fresh were computed for this UTC day
        private static (string url, string contact)? _config;

        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD");
        public static string LicenseFile => Path.Combine(Root, "license.txt");
        public static string StatusFile => Path.Combine(Root, "status.txt");
        /// <summary>Signed service plan (CCF1 line) from the last answer of the service.</summary>
        public static string PlanFile => Path.Combine(Root, "plan.txt");
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

        /// <summary>The last signed status from the owner's device list (status.txt), checked against today.</summary>
        public static StatusCheck DeviceStatus
        {
            get
            {
                if (_status != null && _statusDay == DateTime.UtcNow.Date) return _status; // Fresh depends on the day
                string text = null;
                try { if (File.Exists(StatusFile)) text = File.ReadAllText(StatusFile, Encoding.UTF8); }
                catch (Exception ex) { Logger.Warn("status unreadable: " + ex.Message); }
                var now = DateTime.UtcNow;
                var seen = LatestSeenUtc();
                _statusDay = now.Date;
                _status = LicenseLogic.VerifyStatus(text, MachineCode, now, seen);
                if (_status.ClockBack) Logger.Warn($"clock set back: now {now:o}, already seen {seen:o}");
                else RememberSeen(now, reset: false);
                return _status;
            }
        }

        /// <summary>The last signed plan line (plan.txt), checked against this machine.</summary>
        public static PlanCheck PlanLine
        {
            get
            {
                if (_plan != null) return _plan;
                string text = null;
                try { if (File.Exists(PlanFile)) text = File.ReadAllText(PlanFile, Encoding.UTF8); }
                catch (Exception ex) { Logger.Warn("plan unreadable: " + ex.Message); }
                _plan = LicenseLogic.VerifyPlan(text, MachineCode);
                if (!_plan.Ok && !string.IsNullOrWhiteSpace(text)) Logger.Warn("plan line not used: " + _plan.Reason);
                return _plan;
            }
        }

        /// <summary>
        /// Service plan of this machine: "2d", "3d" or "2d3d" (PlanLogic). The dispatcher refuses every tool outside
        /// it. A build without an activation service has no device list: every tool, like before plans existed.
        /// </summary>
        public static string Plan => ServiceUrl == null ? PlanLogic.PlanFull : LicenseLogic.EffectivePlan(PlanLine, DeviceStatus);

        public static string PlanLabel => PlanLogic.Label(Plan);

        // ---- clock set back (stretching the 7-day offline window): the latest time seen, kept in two places

        private const string SeenRegKey = @"Software\ClaudeCAD\Activation";
        private static string SeenFile => Path.Combine(Root, "clock.txt");

        /// <summary>The latest time this machine's clock showed the plugin (file and registry, whichever is later).</summary>
        private static DateTime? LatestSeenUtc()
        {
            DateTime? best = null;
            void Take(string s)
            {
                if (DateTime.TryParse((s ?? "").Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t))
                {
                    t = t.ToUniversalTime();
                    if (!best.HasValue || t > best.Value) best = t;
                }
            }
            try { if (File.Exists(SeenFile)) Take(File.ReadAllText(SeenFile)); } catch { }
            try { using (var k = Registry.CurrentUser.OpenSubKey(SeenRegKey)) Take(k?.GetValue("LastSeenUtc") as string); } catch { }
            return best;
        }

        /// <summary>Moves the mark forward (reset = a fresh answer from the service: trust this clock again).</summary>
        private static void RememberSeen(DateTime utcNow, bool reset)
        {
            var prior = reset ? null : LatestSeenUtc();
            if (prior.HasValue && prior.Value >= utcNow) return;
            var text = utcNow.ToString("o", CultureInfo.InvariantCulture);
            try { Directory.CreateDirectory(Root); File.WriteAllText(SeenFile, text); } catch (Exception ex) { Logger.Warn("clock mark unsaved: " + ex.Message); }
            try { using (var k = Registry.CurrentUser.CreateSubKey(SeenRegKey)) k?.SetValue("LastSeenUtc", text); } catch (Exception ex) { Logger.Warn("clock mark unsaved: " + ex.Message); }
        }

        /// <summary>
        /// The owner's Active/Disabled decision, as far as this machine can know it. A build without an
        /// activation service has no device list: the licence alone decides, like before.
        /// </summary>
        public static bool DeviceAllowed
        {
            get
            {
                if (ServiceUrl == null) return true;
                var st = DeviceStatus;
                return st.Ok && st.Fresh && (st.Status == "active" || st.Status == "offline");
            }
        }

        /// <summary>The one gate the palette uses: a valid licence AND the owner still allows this machine.</summary>
        public static bool IsAllowed => IsActivated && DeviceAllowed;

        /// <summary>Why the palette is locked although the licence is valid (only meaningful when !DeviceAllowed).</summary>
        public static string BlockedMessage
        {
            get
            {
                var st = DeviceStatus;
                if (st.Ok && st.Status == "disabled") return "Máy này đã bị chủ plugin tạm khóa. " + Contact + ".";
                if (st.Ok && st.Status == "pending") return "Máy này đang chờ chủ plugin duyệt lại. " + Contact + " nếu chờ lâu.";
                if (st.Ok && st.ClockBack) return "Đồng hồ máy đang chậm hơn lần dùng trước. Chỉnh lại ngày giờ Windows cho đúng, kết nối mạng rồi bấm \"Kiểm tra lại\".";
                if (st.Ok && !st.Fresh) return $"Plugin đã quá {LicenseLogic.StatusMaxAgeDays} ngày không kiểm tra được với chủ plugin ({st.Reason}). Kết nối mạng rồi bấm \"Kiểm tra lại\".";
                return $"Chưa xác nhận được máy này với chủ plugin ({st.Reason}). Kết nối mạng rồi bấm \"Kiểm tra lại\".";
            }
        }

        public static string StatusText
        {
            get
            {
                if (!IsActivated) return $"chưa kích hoạt — {Check.Reason} (mã máy {MachineCode})";
                if (!DeviceAllowed) return $"bị tạm khóa (mã máy {MachineCode}) — {BlockedMessage}";
                var st = DeviceStatus;
                var device = ServiceUrl == null ? "" : st.Status == "offline" ? " · miễn kiểm tra từ xa" : $" · được phép dùng (kiểm tra {st.Date})";
                var plan = ServiceUrl == null ? "" : $" · gói {PlanLabel}";
                return $"đã kích hoạt (mã máy {MachineCode}, từ {Check.Issued}){device}{plan}";
            }
        }

        /// <summary>
        /// The activation service {"url", "contact"}: built into the dll from activation/activation.json (a release
        /// always has the url there, so editing or deleting Contents\activation.json on a user's machine cannot
        /// switch the owner's device check off). The Contents file is only read by a build without a built-in url;
        /// CLAUDECAD_ACTIVATION_URL replaces the url (tests) but can never remove it.
        /// </summary>
        private static (string url, string contact) Config
        {
            get
            {
                if (_config.HasValue) return _config.Value;
                string url = null, contact = null;
                void Read(string json)
                {
                    var d = JsonCodec.ParseObject(json);
                    var u = d.TryGetValue("url", out var uv) ? uv as string : null;
                    var c = d.TryGetValue("contact", out var cv) ? cv as string : null;
                    if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(u)) url = u.Trim();
                    if (string.IsNullOrWhiteSpace(contact) && !string.IsNullOrWhiteSpace(c)) contact = c.Trim();
                }
                try
                {
                    using (var s = typeof(Activation).Assembly.GetManifestResourceStream("ClaudeCAD.activation.json"))
                        if (s != null) using (var r = new StreamReader(s, Encoding.UTF8)) Read(r.ReadToEnd());
                }
                catch (Exception ex) { Logger.Warn("built-in activation config unreadable: " + ex.Message); }
                try
                {
                    var dir = PluginPaths.ContentsDirectory;
                    var file = dir == null ? null : Path.Combine(dir, "activation.json");
                    if (file != null && File.Exists(file)) Read(File.ReadAllText(file, Encoding.UTF8)); // fills only what is still missing
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

        /// <summary>Writes a verified CCS1 line to status.txt and refreshes the cached status.</summary>
        private static void SaveStatus(string line, StatusCheck verified)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(StatusFile, line.Trim(), new UTF8Encoding(false));
            _status = verified;
            _statusDay = DateTime.UtcNow.Date;
        }

        /// <summary>
        /// Writes a verified CCF1 line to plan.txt. A line that does not verify is ignored (the old plan stays until
        /// the status line outdates it, see LicenseLogic.EffectivePlan). Returns true when it was saved.
        /// </summary>
        private static bool SavePlan(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var pc = LicenseLogic.VerifyPlan(line, MachineCode);
            if (!pc.Ok) { Logger.Warn("plan line refused: " + pc.Reason); return false; }
            var current = PlanLine;
            if (!LicenseLogic.AcceptPlan(pc, current))
            {
                Logger.Warn($"plan line refused: dated {pc.Date}, older than the saved one ({current.Date})");
                return false;
            }
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(PlanFile, line.Trim(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Logger.Warn("plan unsaved: " + ex.Message); }
            _plan = pc;
            return true;
        }

        /// <summary>
        /// Asks the owner's device list whether this machine is still allowed (signed answer, saved for the
        /// offline window). An Active machine gets its licence with the answer, so approving in the Google Sheet
        /// activates without a key. Returns a sentence for the palette; the caller reads IsAllowed afterwards.
        /// </summary>
        public static async Task<(bool ok, string message)> CheckStatusAsync()
        {
            if (ServiceUrl == null) return (true, "Bản plugin này không dùng máy chủ kích hoạt.");
            var url = LicenseLogic.BuildUrl(ServiceUrl, new[]
            {
                new KeyValuePair<string, string>("action", "status"),
                new KeyValuePair<string, string>("machine", MachineCode),
                new KeyValuePair<string, string>("ver", UpdateService.CurrentVersion?.ToString(3) ?? ""),
            });
            try
            {
                using (var c = NewClient())
                {
                    var r = LicenseLogic.ParseResponse(await c.GetStringAsync(url).ConfigureAwait(false));
                    if (!r.ok || string.IsNullOrEmpty(r.status))
                        return (false, "Không kiểm tra được trạng thái máy: " + (r.error ?? "máy chủ không trả lời đúng."));
                    var st = LicenseLogic.VerifyStatus(r.status, MachineCode, DateTime.UtcNow);
                    if (!st.Ok) return (false, "Trả lời của máy chủ không hợp lệ: " + st.Reason + ".");
                    // a fresh answer dated by the service: this clock is right again (a clock that was wrong-ahead recovers)
                    if (st.Fresh) RememberSeen(DateTime.UtcNow, reset: true);
                    SaveStatus(r.status, st);
                    SavePlan(r.plan); // an older service sends no plan line: the default plan applies
                    if (!string.IsNullOrEmpty(r.license))
                    {
                        var lic = LicenseLogic.Verify(r.license, MachineCode);
                        if (lic.Ok)
                        {
                            Directory.CreateDirectory(Root);
                            File.WriteAllText(LicenseFile, r.license.Trim(), new UTF8Encoding(false));
                            _check = lic;
                        }
                    }
                    Logger.Info("device status: " + st.Status + " (" + MachineCode + ")");
                    if (st.Status == "disabled") return (true, BlockedMessage);
                    if (st.Status == "active") return (true, IsActivated
                        ? "Máy này đang được chủ plugin cho phép dùng."
                        : "Máy đã được duyệt nhưng chưa lưu được giấy phép — thử lại.");
                    return (true, "Máy này đang chờ chủ plugin duyệt. " + Contact + " để được duyệt nhanh.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("status check failed: " + ex.Message);
                return (false, "Không kết nối được máy chủ kích hoạt (kiểm tra mạng rồi thử lại).");
            }
        }

        /// <summary>Key (online) or offline activation code → saved licence. Returns a sentence for the palette.</summary>
        public static async Task<(bool ok, string message)> ActivateAsync(string input)
        {
            input = (input ?? "").Trim();
            string license;
            if (LicenseLogic.IsLicenseText(input) || LicenseLogic.IsStatusText(input) || LicenseLogic.IsPlanText(input))
            {
                // offline activation: a CCL1| licence line, optionally with the CCS1|...|offline| no-network pass
                // and the CCF1| plan line of the machine
                license = null;
                string statusLine = null, planLine = null;
                foreach (var tok in input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (tok.StartsWith(LicenseLogic.Prefix + "|", StringComparison.Ordinal)) license = tok;
                    else if (tok.StartsWith(LicenseLogic.StatusPrefix + "|", StringComparison.Ordinal)) statusLine = tok;
                    else if (tok.StartsWith(LicenseLogic.PlanPrefix + "|", StringComparison.Ordinal)) planLine = tok;
                }
                if (planLine != null)
                {
                    var pc = LicenseLogic.VerifyPlan(planLine, MachineCode);
                    if (!pc.Ok) return (false, "Dòng CCF1 (gói) không dùng được: " + pc.Reason + ".");
                    if (!SavePlan(planLine))
                        return (false, $"Dòng gói này ngày {pc.Date}, cũ hơn dòng gói máy đang có ({PlanLine.Date}): không dùng được. Dán dòng gói mới nhất trong email của chủ plugin.");
                    if (license == null && statusLine == null)
                        return IsActivated
                            ? (true, "Đã lưu gói " + PlanLogic.Label(pc.Plan) + " cho máy này.")
                            : (false, "Đã lưu dòng gói, còn thiếu dòng giấy phép CCL1| — dán đủ các dòng trong email của chủ plugin.");
                }
                if (statusLine != null)
                {
                    var st = LicenseLogic.VerifyStatus(statusLine, MachineCode, DateTime.UtcNow);
                    if (!st.Ok) return (false, "Dòng CCS1 (mã miễn kiểm tra) không dùng được: " + st.Reason + ".");
                    SaveStatus(statusLine, st);
                    if (license == null)
                        return IsActivated && DeviceAllowed
                            ? (true, "Đã lưu mã miễn kiểm tra cho máy này.")
                            : (false, "Đã lưu dòng CCS1, còn thiếu dòng giấy phép CCL1| — dán cả hai dòng trong email của chủ plugin.");
                }
                if (license == null) return (false, "Thiếu dòng giấy phép CCL1| trong mã dán vào.");
            }
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
                        if (!string.IsNullOrEmpty(r.status))
                        {
                            // the service marks the machine Active with the key: keep its signed answer
                            var st = LicenseLogic.VerifyStatus(r.status, MachineCode, DateTime.UtcNow);
                            if (st.Ok) SaveStatus(r.status, st);
                        }
                        SavePlan(r.plan);
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
