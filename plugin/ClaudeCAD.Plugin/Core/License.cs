using System;
using System.Collections.Generic;
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
            "ljbDRSIINOaw3azt6IUoDpF1pvtGUg47JXKOfu0s3+m2qHZZ674NBPhR76FxWoawCeACM5mWpGi97pT8lmtq1RBfO/TOm7gAis5AOlVTgHZDlRMoWWT/bdSEottgLEkFdQV6UO5knWmE+E16KCPfGOYMv38ocSdpmMNpVZlgaS4KVXw3uX1X6OVKEm9rFxo9DYCclIGVze/ZAiQ8FtLNOewBpw0AYOMrOOENaz0gvK40KifL+UHum2m/LvYTF/KjiiKf1Tt48miUy/3Fcmm0B6vTDodXDd7450R5pl0ObiapOdpmkcsUT4+ch/ykBVu8Zs61M9KsEt1qVHX3VsXYuQ==";
        public const string PublicExponent = "AQAB";

        public const string DefaultContact = "Liên hệ Zalo owner: 0344644415";
        public const string Prefix = "CCL1";
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
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromBase64String(modulusB64), Exponent = Convert.FromBase64String(exponentB64) });
                    r.Ok = rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (CryptographicException) { r.Ok = false; }
            if (!r.Ok) r.Reason = "chữ ký giấy phép không hợp lệ";
            return r;
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

        /// <summary>{"ok":true,"license":"..."} / {"ok":false,"error":"..."} from the activation service.</summary>
        public static (bool ok, string license, string error, bool sent) ParseResponse(string json)
        {
            try
            {
                var d = JsonCodec.ParseObject(json ?? "");
                bool ok = d.TryGetValue("ok", out var o) && o is bool b && b;
                string lic = d.TryGetValue("license", out var l) ? l as string : null;
                string err = d.TryGetValue("error", out var e) ? e as string : null;
                bool sent = d.TryGetValue("sent", out var s) && s is bool sb && sb;
                return (ok, lic, err, sent);
            }
            catch
            {
                // Google's sign-in / error pages are HTML: the web app is not deployed for "Anyone"
                return (false, null, "máy chủ kích hoạt trả về nội dung lạ (web app chưa được triển khai cho \"Anyone\"?)", false);
            }
        }
    }
}
