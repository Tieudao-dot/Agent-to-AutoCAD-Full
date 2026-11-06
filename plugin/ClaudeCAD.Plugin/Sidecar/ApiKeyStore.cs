using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Sidecar
{
    /// <summary>
    /// "Dùng API key": an Anthropic API key pasted in the palette, stored encrypted for the current Windows
    /// user (DPAPI) in %LocalAppData%\ClaudeCAD\apikey.bin and passed to the assistant as ANTHROPIC_API_KEY.
    /// It takes precedence over the Claude login, like the environment variable does.
    /// </summary>
    public static class ApiKeyStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ClaudeCAD.ApiKey.v1");

        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "apikey.bin");

        public static bool HasKey => File.Exists(FilePath);

        public static void Save(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length < 20) throw new ArgumentException("API key quá ngắn.");
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllBytes(FilePath, data);
        }

        /// <summary>The saved key, or null (none / unreadable on this Windows account).</summary>
        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex)
            {
                Logger.Warn("saved API key unreadable: " + ex.Message);
                return null;
            }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (Exception ex) { Logger.Warn("API key not deleted: " + ex.Message); }
        }

        /// <summary>"sk-ant-api03-…AbCd" → "sk-ant-…AbCd" for display.</summary>
        public static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var tail = key.Length > 4 ? key.Substring(key.Length - 4) : key;
            return (key.StartsWith("sk-ant-", StringComparison.Ordinal) ? "sk-ant-" : "") + "…" + tail;
        }
    }
}
