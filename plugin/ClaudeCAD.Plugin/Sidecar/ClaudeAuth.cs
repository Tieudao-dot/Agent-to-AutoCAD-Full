using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Sidecar
{
    /// <summary>Result of "claude auth status".</summary>
    public sealed class AuthStatus
    {
        /// <summary>null = could not be determined (Claude Code binary missing or failed).</summary>
        public bool? LoggedIn;
        public string Method = "";
        public string Error;

        public string Label =>
            LoggedIn == true ? (Method == "api_key" ? "đã có ANTHROPIC_API_KEY" : "đã đăng nhập Claude")
            : LoggedIn == false ? "chưa đăng nhập Claude"
            : "chưa kiểm tra được" + (string.IsNullOrEmpty(Error) ? "" : $" ({Error})");
    }

    /// <summary>
    /// Sign-in with the Claude Code binary bundled in the Agent SDK: the user never installs Claude Code or
    /// opens a terminal. "Đăng nhập Claude" opens a console window running "claude auth login", which opens the
    /// browser; the credentials land in %USERPROFILE%\.claude where the assistant reads them.
    /// </summary>
    public static class ClaudeAuth
    {
        public static Task<AuthStatus> StatusAsync() => Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
                return new AuthStatus { LoggedIn = true, Method = "api_key" };
            var exe = PluginPaths.ClaudeExecutable;
            if (exe == null) return new AuthStatus { Error = "không thấy Claude Code đi kèm plugin" };
            try
            {
                var psi = new ProcessStartInfo(exe, "auth status --json")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return new AuthStatus { Error = "hết thời gian chờ" }; }
                    return Parse(outTask.Result);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("claude auth status failed: " + ex.Message);
                return new AuthStatus { Error = ex.Message };
            }
        });

        /// <summary>Parses {"loggedIn": true, "authMethod": "oauth_token", ...}.</summary>
        public static AuthStatus Parse(string json)
        {
            try
            {
                var start = json?.IndexOf('{') ?? -1;
                if (start < 0) return new AuthStatus { Error = "kết quả không đọc được" };
                var d = JsonCodec.ParseObject(json.Substring(start));
                return new AuthStatus
                {
                    LoggedIn = d.TryGetValue("loggedIn", out var l) && l is bool b ? b : (bool?)null,
                    Method = d.TryGetValue("authMethod", out var m) ? m as string ?? "" : "",
                };
            }
            catch (Exception ex)
            {
                return new AuthStatus { Error = ex.Message };
            }
        }

        /// <summary>
        /// Opens a console window with "claude auth login --claudeai" (it opens the browser). done(status) is called
        /// on a worker thread when the window closes. False when the binary is missing.
        /// </summary>
        public static bool StartLogin(Action<AuthStatus> done)
        {
            var exe = PluginPaths.ClaudeExecutable;
            if (exe == null) return false;
            var psi = new ProcessStartInfo(exe, "auth login --claudeai") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
            var p = Process.Start(psi);
            if (p == null) return false;
            Task.Run(async () =>
            {
                try { p.WaitForExit(); } catch { }
                try { p.Dispose(); } catch { }
                done?.Invoke(await StatusAsync().ConfigureAwait(false));
            });
            return true;
        }
    }
}
