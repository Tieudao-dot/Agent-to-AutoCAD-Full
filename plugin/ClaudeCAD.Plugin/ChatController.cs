using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using ClaudeCAD.Plugin.Ipc;
using ClaudeCAD.Plugin.Sidecar;
using ClaudeCAD.Plugin.UI;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin
{
    /// <summary>
    /// Wires the palette, the IPC server, the sidecar supervisor, the tool
    /// dispatcher and the per-turn undo group together. One instance per AutoCAD
    /// session (one chat window).
    /// </summary>
    public sealed class ChatController : ITurnHost, IDisposable
    {
        private static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(10);

        private readonly ChatPalette _palette = new ChatPalette();
        private readonly IpcServer _ipc;
        private readonly SidecarManager _sidecar;
        private readonly ToolDispatcher _dispatcher;
        private readonly object _gate = new object();

        private bool _started;
        private volatile bool _busy;
        private int _turnSeq;
        private Document _turnDoc;
        private string _turnInvalidReason;

        public UndoScope Undo { get; } = new UndoScope();
        public ToolDispatcher Dispatcher => _dispatcher;
        public bool Busy => _busy;

        public ChatController()
        {
            _ipc = new IpcServer(IpcServer.DefaultPipeName());
            _sidecar = new SidecarManager(_ipc.PipeName);
            _dispatcher = new ToolDispatcher(this);
            DrawingService.RegisterAll(_dispatcher, this);

            _ipc.MessageReceived += OnMessage;
            _ipc.Connected += () => Ui.SetConnection("Đã kết nối, đang khởi tạo phiên...", true);
            _ipc.Disconnected += OnDisconnected;
            _sidecar.StateChanged += OnSidecarState;

            var ui = _palette.Control;
            ui.SendRequested += SendUserMessage;
            ui.StopRequested += Cancel;
            ui.NewChatRequested += NewChat;

            var dm = AcApp.DocumentManager;
            dm.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            dm.DocumentActivated += OnDocumentActivated;
        }

        private ChatControl Ui => _palette.Control;

        /// <summary>CLAUDECHAT: show the palette, start IPC + sidecar the first time.</summary>
        public void ShowPalette()
        {
            _palette.Show();
            if (!_started)
            {
                _started = true;
                _ipc.Start();
                UpdateService.CheckInBackground(msg => Ui.AddNote(msg)); // phase 3: daily update check
            }
            _sidecar.EnsureStarted();
        }

        /// <summary>CLAUDECHATUPDATE: check now and stage the update if there is one.</summary>
        public void RunUpdate()
        {
            _palette.Show();
            Ui.AddNote("Đang kiểm tra cập nhật...");
            UpdateService.DownloadAndStageAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Logger.Error("update failed", t.Exception.GetBaseException());
                    Ui.AddNote("Cập nhật thất bại: " + t.Exception.GetBaseException().Message, true);
                }
                else Ui.AddNote(t.Result);
            });
        }

        public void RestartSidecar()
        {
            _palette.Show();
            if (!_started) { _started = true; _ipc.Start(); }
            _sidecar.Restart();
        }

        // ------------------------------------------------------------------ turns

        private void SendUserMessage(string text)
        {
            if (_busy) return;
            if (!_ipc.IsConnected)
            {
                Ui.AddNote("Chưa kết nối được với trợ lý. Đợi vài giây hoặc gõ lệnh CLAUDECHATRESTART.", true);
                _sidecar.EnsureStarted();
                return;
            }

            Document doc = AcApp.DocumentManager.MdiActiveDocument; // UI events arrive on the main thread
            lock (_gate)
            {
                _busy = true;
                _turnSeq++;
                _turnDoc = doc;
                _turnInvalidReason = null;
            }
            Ui.AddUserMessage(text);
            Ui.SetBusy(true);
            Ui.SetToolStatus("Claude đang suy nghĩ...");

            var msg = new Dictionary<string, object> { ["type"] = "user_message", ["text"] = text };
            if (doc != null)
            {
                msg["drawing"] = SafeDocName(doc);
            }
            _ipc.SendAsync(msg).ContinueWith(t =>
            {
                if (!t.Result) EndTurn("Gửi tin nhắn thất bại: mất kết nối với trợ lý.");
            });
        }

        private void Cancel()
        {
            if (!_busy) return;
            int seq = _turnSeq;
            Ui.SetToolStatus("Đang dừng...");
            _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
            // if the sidecar does not answer with turn_end, end the turn locally
            Task.Delay(CancelGrace).ContinueWith(_ =>
            {
                if (_busy && seq == _turnSeq) EndTurn("Đã dừng lượt (trợ lý không phản hồi lệnh dừng).");
            });
        }

        private void NewChat()
        {
            if (_busy)
            {
                _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
                EndTurn(null);
            }
            Ui.ClearHistory();
            Ui.AddNote("Cuộc chat mới.");
            if (_sidecar.State == SidecarState.Failed) _sidecar.Restart();
            else _ipc.Send(new Dictionary<string, object> { ["type"] = "new_session" });
        }

        /// <summary>Ends the current turn: closes the undo group and unlocks the UI. Thread-safe.</summary>
        private void EndTurn(string errorNote)
        {
            lock (_gate)
            {
                if (!_busy) return;
                _busy = false;
            }
            MainThread.Post(() =>
            {
                Undo.End();
                lock (_gate)
                {
                    _turnDoc = null;
                    _turnInvalidReason = null;
                }
            });
            Ui.EndAssistant();
            if (!string.IsNullOrEmpty(errorNote)) Ui.AddNote(errorNote, true);
            Ui.SetBusy(false);
        }

        // ---------------------------------------------------------------- ITurnHost

        public Document ResolveDocument()
        {
            lock (_gate)
            {
                if (_turnInvalidReason != null) throw new FatalToolException(_turnInvalidReason);
                var active = AcApp.DocumentManager.MdiActiveDocument;
                if (_turnDoc == null) _turnDoc = active;
                if (_turnDoc == null || _turnDoc.IsDisposed)
                    throw new FatalToolException("No drawing is open in AutoCAD. Stop and ask the user to open or create a drawing.");
                if (active != _turnDoc)
                    throw new FatalToolException("The user switched to another drawing during this turn. Stop now and tell the user; do not retry.");
                return _turnDoc;
            }
        }

        // -------------------------------------------------------------- IPC events

        private void OnMessage(Dictionary<string, object> msg)
        {
            string type = msg.TryGetValue("type", out var t) ? t as string : null;
            switch (type)
            {
                case "tool_call":
                    HandleToolCall(msg);
                    break;
                case "agent_text":
                    if (_busy) Ui.AppendAssistant(msg.TryGetValue("delta", out var d) ? d as string : "");
                    break;
                case "agent_text_end":
                    Ui.EndAssistant();
                    break;
                case "tool_status":
                    HandleToolStatus(msg);
                    break;
                case "turn_end":
                    {
                        string err = msg.TryGetValue("error", out var e) ? e as string : null;
                        EndTurn(string.IsNullOrEmpty(err) ? null : "Lượt bị lỗi: " + err);
                        break;
                    }
                case "agent_error":
                    Ui.AddNote("Lỗi trợ lý: " + (msg.TryGetValue("message", out var m) ? m as string : "?"), true);
                    break;
                case "ready":
                    {
                        string info = msg.TryGetValue("info", out var i) ? i as string : null;
                        Ui.SetConnection("Đã kết nối" + (string.IsNullOrEmpty(info) ? "" : " · " + info), true);
                        break;
                    }
                case "session_reset":
                    Ui.SetConnection("Đã kết nối · phiên mới", true);
                    break;
                default:
                    Logger.Warn("IPC unknown message type: " + (type ?? "(none)"));
                    break;
            }
        }

        private async void HandleToolCall(Dictionary<string, object> msg)
        {
            try
            {
                var response = await _dispatcher.HandleAsync(msg).ConfigureAwait(false);
                await _ipc.SendAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error("HandleToolCall crashed", ex); // HandleAsync never throws; belt and braces
            }
        }

        private void HandleToolStatus(Dictionary<string, object> msg)
        {
            string tool = msg.TryGetValue("tool", out var t) ? t as string : "";
            string state = msg.TryGetValue("state", out var s) ? s as string : "";
            string label = ToolLabels.Describe(tool);
            switch (state)
            {
                case "running": Ui.SetToolStatus("⏳ " + label + "..."); break;
                case "done": Ui.SetToolStatus("✓ " + label); break;
                case "error": Ui.SetToolStatus("⚠ " + label + " lỗi"); break;
                default: Ui.SetToolStatus(label); break;
            }
        }

        private void OnDisconnected(string reason)
        {
            if (_busy) EndTurn("Mất kết nối với trợ lý giữa lượt; lượt này đã dừng. Lịch sử chat vẫn giữ nguyên.");
            Ui.SetConnection("Mất kết nối với trợ lý...", false);
        }

        private void OnSidecarState(SidecarState state, string message)
        {
            bool ok = state == SidecarState.Running || state == SidecarState.Starting;
            if (!_ipc.IsConnected || !ok) Ui.SetConnection(message, ok);
            if (state == SidecarState.Restarting || state == SidecarState.Failed) Ui.AddNote(message, state == SidecarState.Failed);
        }

        // --------------------------------------------------------- document events

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            bool cancel = false;
            lock (_gate)
            {
                if (_busy && e.Document == _turnDoc)
                {
                    _turnInvalidReason = "The drawing was closed during this turn. Stop now and tell the user.";
                    cancel = true;
                }
            }
            Undo.Abandon(e.Document);
            if (cancel)
            {
                _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
                Ui.AddNote("Bản vẽ bị đóng giữa lượt — đã dừng.", true);
            }
        }

        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            bool cancel = false;
            lock (_gate)
            {
                if (_busy && _turnDoc != null && e.Document != _turnDoc && _turnInvalidReason == null)
                {
                    _turnInvalidReason = "The user switched to another drawing during this turn. Stop now and tell the user; do not retry.";
                    cancel = true;
                }
            }
            if (cancel)
            {
                _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
                Ui.AddNote("Cậu đã chuyển sang bản vẽ khác giữa lượt — đã dừng lượt để an toàn.", true);
            }
        }

        private static string SafeDocName(Document d)
        {
            try { return System.IO.Path.GetFileName(d.Name); } catch { return ""; }
        }

        public void Dispose()
        {
            try
            {
                var dm = AcApp.DocumentManager;
                dm.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
                dm.DocumentActivated -= OnDocumentActivated;
            }
            catch { }
            _sidecar.Dispose();
            _ipc.Dispose();
        }
    }
}
