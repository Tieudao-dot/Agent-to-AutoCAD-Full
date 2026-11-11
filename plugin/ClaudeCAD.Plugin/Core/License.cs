using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>Result of checking a saved licence against this machine.</summary>
    public sealed class LicenseCheck
    {
        public bool Ok;
        public string Reason = "";
        public string Machine = "";
        public string Issued = "";
    }

    /// <summary>Result of checking a signed device status (CCS1 line) against this machine and today.</summary>
    public sealed class StatusCheck
    {
        public bool Ok;            // signature valid and the line is for this machine
        public string Status = ""; // active | disabled | pending | offline (lower case)
        public string Machine = "";
        public string Date = "";   // yyyy-MM-dd (UTC) of the check the service signed
        public int AgeDays;        // how old that check is
        public bool Fresh;         // offline, or at most StatusMaxAgeDays old (and the clock was not set back)
        public bool ClockBack;     // this machine's clock is earlier than a time it already showed
        public string Reason = "";
    }

    /// <summary>
    /// Per-machine activation (v0.9.4), AutoCAD-free so it is unit-tested on CI.
    ///
    ///  * Machine code: CC-XXXX-XXXX-XXXX = the first 48 bits of SHA-256("ClaudeCAD|" + Windows MachineGuid, lower case).
    ///    The installer (Inno Setup, GetSHA256OfString) computes the same code.
    ///  * Key (what the owner sends by Zalo): 16 base32 characters made by the owner's activation service
    ///    (Google Apps Script) from a secret only that service knows — the plugin never checks a key itself.
    ///  * Licence (what the service returns for a right key, saved in %LocalAppData%\ClaudeCAD\license.txt):
    ///    "CCL1|machine|yyyy-MM-dd|base64url(RSA-SHA256 PKCS#1 v1.5 signature of the text before the last |)".
    ///    Checked offline with the public key below; only the service holds the private key.
    /// </summary>
    public static class LicenseLogic
    {
        // RSA-2048 public key of the activation service (activation/make-apps-script.mjs --print-public)
        public const string PublicModulus =
            "yony8l3I8ydAqJt3mEYhnHoRzi9udou/8YlRPTo9p8Wyv0DTp85FWlRcYWtqHUDDK7hJIe6oeE1GuX7qt9YwvNHlxFjUDLz0mwnNNg+2d+ve9G5HJYXD+brYhtYpKmfzsuE6fEsfo+nJEZEz1BFQ1QNw3n8pE6ABZMVtKf6/MwZhCVvgZ3VsOF0busb/LO6exE1cBJZfu8rUCFiaYuoFCzWK0tchPGDMy6T+nxcsO5mSGOrErp49cR2Q769TBVgghRA730F7uD84VE5OnE0rc5xBWK3vPopcU4H/VLf+hf5Ek2Bj4kDOjfj0bysMLcMbTXRhketDpsRaRnyY0rwXYQ==";
        public const string PublicExponent = "AQAB";

        public const string DefaultContact = "Liên hệ Zalo owner: 0344644415";
        public const string Prefix = "CCL1";
        public const string StatusPrefix = "CCS1";
        /// <summary>A machine may run on its last signed status for this many days without reaching the service.</summary>
        public const int StatusMaxAgeDays = 7;
        private const string Base32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>CC-XXXX-XXXX-XXXX from the Windows MachineGuid (or the computer name when there is none).</summary>
        public static string MachineCode(string machineGuid, string computerName = null)
        {
            var src = !string.IsNullOrWhiteSpace(machineGuid) ? machineGuid : (computerName ?? "");
            src = src.Trim().ToLowerInvariant();
            byte[] h;
            using (var sha = SHA256.Create()) h = sha.ComputeHash(Encoding.UTF8.GetBytes("ClaudeCAD|" + src));
            var hex = string.Concat(h.Take(6).Select(b => b.ToString("X2")));
            return "CC-" + hex.Substring(0, 4) + "-" + hex.Substring(4, 4) + "-" + hex.Substring(8, 4);
        }

        /// <summary>Key as typed or pasted (spaces, dashes, lower case, O for 0, I/L for 1) → 16 base32 characters.</summary>
        public static string NormalizeKey(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (var ch in s.ToUpperInvariant())
            {
                if (char.IsWhiteSpace(ch) || ch == '-') continue;
                sb.Append(ch == 'O' ? '0' : (ch == 'I' || ch == 'L') ? '1' : ch);
            }
            return sb.ToString();
        }

        public static bool IsShortKey(string s)
        {
            var k = NormalizeKey(s);
            return k.Length == 16 && k.All(c => Base32.IndexOf(c) >= 0);
        }

        /// <summary>An offline activation code (the whole licence line) pasted instead of a key.</summary>
        public static bool IsLicenseText(string s) => (s ?? "").Trim().StartsWith(Prefix + "|", StringComparison.Ordinal);

        /// <summary>A signed device-status line (CCS1|...), alone or pasted together with the licence.</summary>
        public static bool IsStatusText(string s) => (s ?? "").Trim().Contains(StatusPrefix + "|");

        public static LicenseCheck Verify(string license, string machine) => Verify(license, machine, PublicModulus, PublicExponent);

        public static LicenseCheck Verify(string license, string machine, string modulusB64, string exponentB64)
        {
            var r = new LicenseCheck();
            license = (license ?? "").Trim();
            if (license.Length == 0) { r.Reason = "máy này chưa được kích hoạt"; return r; }
            var parts = license.Split('|');
            if (parts.Length != 4 || parts[0] != Prefix) { r.Reason = "giấy phép không đúng định dạng"; return r; }
            r.Machine = parts[1];
            r.Issued = parts[2];
            if (!string.Equals(parts[1], machine, StringComparison.OrdinalIgnoreCase))
            {
                r.Reason = $"giấy phép của máy khác ({parts[1]}), máy này là {machine}";
                return r;
            }
            byte[] sig;
            try { sig = FromBase64Url(parts[3]); }
            catch { r.Reason = "chữ ký giấy phép hỏng"; return r; }
            var data = Encoding.UTF8.GetBytes(parts[0] + "|" + parts[1] + "|" + parts[2]);
            r.Ok = VerifyRsa(data, sig, modulusB64, exponentB64);
            if (!r.Ok) r.Reason = "chữ ký giấy phép không hợp lệ";
            return r;
        }

        /// <summary>
        /// Checks a signed device status "CCS1|machine|status|yyyy-MM-dd|sig" (same key and signature scheme as
        /// the licence). Ok = signed for this machine; Fresh = may still be trusted today (status "offline", for
        /// machines the owner exempted, never goes stale; anything else is stale after StatusMaxAgeDays days,
        /// so a disabled machine cannot dodge the lock by blocking the plugin's network).
        /// </summary>
        public static StatusCheck VerifyStatus(string line, string machine, DateTime utcNow) => VerifyStatus(line, machine, utcNow, PublicModulus, PublicExponent);

        /// <summary>latestSeenUtc: the latest time this machine's clock ever showed the plugin (null = unknown).</summary>
        public static StatusCheck VerifyStatus(string line, string machine, DateTime utcNow, DateTime? latestSeenUtc) =>
            VerifyStatus(line, machine, utcNow, PublicModulus, PublicExponent, latestSeenUtc);

        /// <summary>
        /// True when the clock is more than a day earlier than a time it already showed: someone set it back to
        /// stretch the offline window (a day of slack for time-zone / daylight-saving changes and small corrections).
        /// </summary>
        public static bool ClockRolledBack(DateTime utcNow, DateTime? latestSeenUtc) =>
            latestSeenUtc.HasValue && utcNow < latestSeenUtc.Value.AddDays(-1);

        public static StatusCheck VerifyStatus(string line, string machine, DateTime utcNow, string modulusB64, string exponentB64, DateTime? latestSeenUtc = null)
        {
            var r = new StatusCheck();
            line = (line ?? "").Trim();
            if (line.Length == 0) { r.Reason = "chưa kiểm tra được trạng thái máy với máy chủ lần nào"; return r; }
            var parts = line.Split('|');
            if (parts.Length != 5 || parts[0] != StatusPrefix) { r.Reason = "xác nhận trạng thái không đúng định dạng"; return r; }
            r.Machine = parts[1];
            r.Status = parts[2].Trim().ToLowerInvariant();
            r.Date = parts[3];
            if (!string.Equals(parts[1], machine, StringComparison.OrdinalIgnoreCase))
            {
                r.Reason = $"xác nhận trạng thái của máy khác ({parts[1]}), máy này là {machine}";
                return r;
            }
            byte[] sig;
            try { sig = FromBase64Url(parts[4]); }
            catch { r.Reason = "chữ ký trạng thái hỏng"; return r; }
            var data = Encoding.UTF8.GetBytes(line.Substring(0, line.LastIndexOf('|')));
            if (!VerifyRsa(data, sig, modulusB64, exponentB64)) { r.Reason = "chữ ký trạng thái không hợp lệ"; return r; }
            DateTime d;
            if (!DateTime.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d))
            { r.Reason = "ngày trong xác nhận trạng thái hỏng"; return r; }
            r.Ok = true;
            r.AgeDays = (int)(utcNow.Date - d.Date).TotalDays;
            r.Fresh = r.Status == "offline" || (r.AgeDays >= -1 && r.AgeDays <= StatusMaxAgeDays); // -1: time zones around midnight
            if (!r.Fresh) r.Reason = r.AgeDays > StatusMaxAgeDays
                ? $"lần kiểm tra trạng thái gần nhất đã {r.AgeDays} ngày trước (quá {StatusMaxAgeDays} ngày)"
                : "ngày trong xác nhận trạng thái nằm ở tương lai (đồng hồ máy sai?)";
            else if (r.Status != "offline" && ClockRolledBack(utcNow, latestSeenUtc))
            {
                // the owner's no-network pass never expires, so the clock does not matter for it
                r.Fresh = false;
                r.ClockBack = true;
                r.Reason = "đồng hồ máy bị chỉnh lùi so với lần dùng trước";
            }
            return r;
        }

        private static bool VerifyRsa(byte[] data, byte[] sig, string modulusB64, string exponentB64)
        {
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromBase64String(modulusB64), Exponent = Convert.FromBase64String(exponentB64) });
                    return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (CryptographicException) { return false; }
            catch (FormatException) { return false; }
        }

        public static byte[] FromBase64Url(string s)
        {
            s = (s ?? "").Trim().TrimEnd('=').Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new FormatException("bad base64url length");
            }
            return Convert.FromBase64String(s);
        }

        /// <summary>base?action=...&amp;k=v (UTF-8, percent-encoded); base may already carry a query.</summary>
        public static string BuildUrl(string baseUrl, IEnumerable<KeyValuePair<string, string>> query)
        {
            var sb = new StringBuilder(baseUrl ?? "");
            bool first = !(baseUrl ?? "").Contains("?");
            foreach (var kv in query)
            {
                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value ?? ""));
            }
            return sb.ToString();
        }

        /// <summary>{"ok":true,"license":"...","status":"..."} / {"ok":false,"error":"..."} from the activation service.</summary>
        public static (bool ok, string license, string error, bool sent, string status) ParseResponse(string json)
        {
            try
            {
                var d = JsonCodec.ParseObject(json ?? "");
                bool ok = d.TryGetValue("ok", out var o) && o is bool b && b;
                string lic = d.TryGetValue("license", out var l) ? l as string : null;
                string err = d.TryGetValue("error", out var e) ? e as string : null;
                bool sent = d.TryGetValue("sent", out var s) && s is bool sb && sb;
                string status = d.TryGetValue("status", out var st) ? st as string : null;
                return (ok, lic, err, sent, status);
            }
            catch
            {
                // Google's sign-in / error pages are HTML: the web app is not deployed for "Anyone"
                return (false, null, "máy chủ kích hoạt trả về nội dung lạ (web app chưa được triển khai cho \"Anyone\"?)", false, null);
            }
        }
    }
}
