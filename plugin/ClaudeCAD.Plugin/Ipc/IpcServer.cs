using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Ipc
{
    /// <summary>
    /// Named-pipe server, one client (the sidecar) at a time.
    /// Protocol: JSON Lines, UTF-8 without BOM (spec §6). All I/O is async and runs
    /// on thread-pool threads; events are raised on those threads, so consumers must
    /// marshal to the UI / AutoCAD main thread themselves.
    /// When the client disconnects the server immediately waits for a new client
    /// (that is how a restarted sidecar reconnects).
    /// </summary>
    public sealed class IpcServer : IDisposable
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private NamedPipeServerStream _pipe;
        private StreamWriter _writer;
        private volatile bool _connected;
        private Task _loop;

        public string PipeName { get; }
        public bool IsConnected => _connected;

        /// <summary>Raised for every well-formed JSON object line.</summary>
        public event Action<Dictionary<string, object>> MessageReceived;
        public event Action Connected;
        public event Action<string> Disconnected;

        public IpcServer(string pipeName)
        {
            PipeName = pipeName;
        }

        public static string DefaultPipeName() =>
            "claudecad-" + System.Diagnostics.Process.GetCurrentProcess().Id;

        public void Start()
        {
            if (_loop != null) return;
            _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                string reason = "pipe closed";
                try
                {
                    _pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Logger.Info("IPC waiting for sidecar on \\\\.\\pipe\\" + PipeName);
                    await _pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    _writer = new StreamWriter(_pipe, Utf8NoBom) { AutoFlush = true, NewLine = "\n" };
                    _connected = true;
                    Logger.Info("IPC sidecar connected");
                    SafeRaise(() => Connected?.Invoke());

                    using (var reader = new StreamReader(_pipe, Utf8NoBom, false, 64 * 1024, true))
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line == null) break; // EOF: client went away
                            if (line.Length == 0) continue;
                            Logger.Ipc("<-", line);
                            Dictionary<string, object> msg;
                            try
                            {
                                msg = JsonCodec.ParseObject(line);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("IPC dropped malformed line: " + ex.Message);
                                continue;
                            }
                            SafeRaise(() => MessageReceived?.Invoke(msg));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    reason = "server stopped";
                }
                catch (IOException ex)
                {
                    reason = "pipe broken: " + ex.Message;
                }
                catch (Exception ex)
                {
                    reason = "pipe error: " + ex.Message;
                    Logger.Error("IPC loop error", ex);
                }
                finally
                {
                    bool was = _connected;
                    _connected = false;
                    try { _pipe?.Dispose(); } catch { }
                    _pipe = null;
                    _writer = null;
                    if (was)
                    {
                        Logger.Warn("IPC sidecar disconnected: " + reason);
                        SafeRaise(() => Disconnected?.Invoke(reason));
                    }
                }
                if (!ct.IsCancellationRequested) await Task.Delay(100).ConfigureAwait(false);
            }
        }

        /// <summary>Serializes and writes one JSON line. Returns false if not connected.</summary>
        public async Task<bool> SendAsync(object message)
        {
            string line = JsonCodec.Serialize(message);
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var w = _writer;
                if (!_connected || w == null) return false;
                await w.WriteAsync(line + "\n").ConfigureAwait(false);
                Logger.Ipc("->", line);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn("IPC send failed: " + ex.Message);
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>Fire-and-forget send that never throws.</summary>
        public void Send(object message)
        {
            _ = SendAsync(message);
        }

        private static void SafeRaise(Action a)
        {
            try { a(); }
            catch (Exception ex) { Logger.Error("IPC event handler threw", ex); }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }
        }
    }
}
