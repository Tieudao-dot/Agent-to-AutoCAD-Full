using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Sidecar
{
    public enum SidecarState { Stopped, Starting, Running, Restarting, Failed }

    /// <summary>
    /// Spawns and supervises <c>node agent/dist/index.js --pipe &lt;name&gt;</c> (spec §4).
    /// Restarts it after a crash at most <see cref="MaxRestarts"/> times in a row
    /// (the counter resets once a process has stayed up for a minute), reports state
    /// changes to the UI and kills the process when AutoCAD closes.
    /// </summary>
    public sealed class SidecarManager : IDisposable
    {
        public const int MaxRestarts = 3;
        private static readonly TimeSpan StableAfter = TimeSpan.FromMinutes(1);

        private readonly string _pipeName;
        private readonly object _gate = new object();
        private readonly JobObject _job = new JobObject();
        private Process _proc;
        private DateTime _startedAt;
        private int _restarts;
        private bool _stopping;

        public SidecarState State { get; private set; } = SidecarState.Stopped;

        /// <summary>(state, human readable Vietnamese message). Raised on arbitrary threads.</summary>
        public event Action<SidecarState, string> StateChanged;

        public SidecarManager(string pipeName)
        {
            _pipeName = pipeName;
        }

        public bool IsRunning
        {
            get
            {
                lock (_gate) return _proc != null && !SafeHasExited(_proc);
            }
        }

        /// <summary>Starts the sidecar if it is not running. Safe to call repeatedly.</summary>
        public void EnsureStarted()
        {
            lock (_gate)
            {
                if (_proc != null && !SafeHasExited(_proc)) return;
                if (State == SidecarState.Failed || State == SidecarState.Restarting) return; // New / CLAUDECHATRESTART resets
                _stopping = false;
                StartLocked(SidecarState.Starting);
            }
        }

        /// <summary>Manual restart (e.g. from the palette after a hard failure).</summary>
        public void Restart()
        {
            lock (_gate)
            {
                _stopping = true;
                KillLocked();
                _stopping = false;
                _restarts = 0;
                StartLocked(SidecarState.Starting);
            }
        }

        private static bool SafeHasExited(Process p)
        {
            try { return p.HasExited; } catch { return true; }
        }

        private void StartLocked(SidecarState state)
        {
            string node, entry;
            try
            {
                node = PluginPaths.NodeExecutable;
                entry = PluginPaths.AgentEntry;
            }
            catch (Exception ex)
            {
                Logger.Error("resolving sidecar paths failed", ex);
                SetState(SidecarState.Failed, "Không xác định được đường dẫn node/agent: " + ex.Message);
                return;
            }
            if (node == null)
            {
                SetState(SidecarState.Failed,
                    "Không tìm thấy node.exe. Cài Node.js >= 18 (https://nodejs.org) hoặc đặt biến môi trường CLAUDECAD_NODE.");
                return;
            }
            if (entry == null)
            {
                SetState(SidecarState.Failed,
                    "Không tìm thấy agent\\dist\\index.js. Chạy 'npm install' và 'npm run build' trong thư mục agent, hoặc đặt CLAUDECAD_AGENT_DIR.");
                return;
            }

            string skills = null;
            try { skills = PluginPaths.SkillsDirectory; } catch (Exception ex) { Logger.Warn("skills dir: " + ex.Message); }
            var args = new StringBuilder();
            args.Append(Quote(entry)).Append(" --pipe ").Append(Quote(_pipeName));
            if (skills != null) args.Append(" --skills ").Append(Quote(skills));
            args.Append(" --log-dir ").Append(Quote(Logger.LogDirectory));
            args.Append(" --parent-pid ").Append(Process.GetCurrentProcess().Id);

            var psi = new ProcessStartInfo(node, args.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(entry)) ?? PluginPaths.DllDirectory,
            };
            // "Dùng API key" in the palette: the saved key is given to the assistant (it wins over the Claude login)
            var savedKey = ApiKeyStore.Load();
            if (!string.IsNullOrEmpty(savedKey)) psi.EnvironmentVariables["ANTHROPIC_API_KEY"] = savedKey;

            try
            {
                SetState(state, state == SidecarState.Restarting
                    ? $"Đang khởi động lại trợ lý (lần {_restarts}/{MaxRestarts})..."
                    : "Đang khởi động trợ lý...");
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += (s, e) => { if (e.Data != null) Logger.Info("[sidecar] " + e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Warn("[sidecar:err] " + e.Data); };
                p.Exited += OnExited;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _job.Add(p);
                _proc = p;
                _startedAt = DateTime.UtcNow;
                Logger.Info($"Sidecar started pid={p.Id}: {node} {args}");
                SetState(SidecarState.Running, "Trợ lý đã chạy, đang kết nối...");
            }
            catch (Exception ex)
            {
                Logger.Error("Sidecar start failed", ex);
                SetState(SidecarState.Failed, "Không khởi động được trợ lý: " + ex.Message);
            }
        }

        private void OnExited(object sender, EventArgs e)
        {
            var p = sender as Process;
            int code = -1;
            try { code = p?.ExitCode ?? -1; } catch { }

            lock (_gate)
            {
                if (!ReferenceEquals(p, _proc)) return; // an old process we already replaced
                _proc = null;
                if (_stopping)
                {
                    SetState(SidecarState.Stopped, "Trợ lý đã dừng.");
                    return;
                }

                Logger.Warn($"Sidecar exited unexpectedly (code {code})");
                if (DateTime.UtcNow - _startedAt > StableAfter) _restarts = 0;
                if (_restarts >= MaxRestarts)
                {
                    SetState(SidecarState.Failed,
                        $"Trợ lý bị dừng {MaxRestarts + 1} lần liên tiếp (mã {code}). Xem log tại {Logger.LogDirectory}. Bấm New để thử lại.");
                    return;
                }
                _restarts++;
                SetState(SidecarState.Restarting, $"Mất kết nối với trợ lý (mã {code}). Đang tự khôi phục...");
            }

            // brief back-off, then start again outside the Exited callback
            Task.Delay(TimeSpan.FromSeconds(Math.Min(5, _restarts))).ContinueWith(_ =>
            {
                lock (_gate)
                {
                    if (_stopping || (_proc != null && !_proc.HasExited)) return;
                    StartLocked(SidecarState.Restarting);
                }
            });
        }

        private void KillLocked()
        {
            var p = _proc;
            _proc = null;
            if (p == null) return;
            try
            {
                if (!p.HasExited)
                {
                    // the whole tree: node AND the Claude Code process it started (Kill() alone left that one running
                    // until its pipe broke; the job object only cleans up when AutoCAD exits)
#if ACAD_R24
                    try
                    {
                        using (var tk = Process.Start(new ProcessStartInfo("taskkill", "/T /F /PID " + p.Id) { UseShellExecute = false, CreateNoWindow = true }))
                            tk?.WaitForExit(3000);
                    }
                    catch (Exception ex) { Logger.Warn("taskkill failed: " + ex.Message); }
                    if (!SafeHasExited(p)) p.Kill();
#else
                    p.Kill(true);
#endif
                    p.WaitForExit(2000);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Sidecar kill failed: " + ex.Message);
            }
            finally
            {
                p.Dispose();
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _stopping = true;
                KillLocked();
                SetState(SidecarState.Stopped, "Trợ lý đã dừng.");
            }
        }

        private void SetState(SidecarState s, string message)
        {
            State = s;
            try { StateChanged?.Invoke(s, message); }
            catch (Exception ex) { Logger.Error("StateChanged handler threw", ex); }
        }

        private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        public void Dispose()
        {
            Stop();
            _job.Dispose();
        }
    }
}
