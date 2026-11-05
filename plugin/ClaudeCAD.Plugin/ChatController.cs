using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    /// dispatcher and the per-turn undo group together, plus the palette features:
    /// per-drawing history, attachments, context, activity log, result zoom,
    /// undo / edit / retry, confirmation cards, usage, notifications and theme.
    /// One instance per AutoCAD session.
    /// </summary>
    public sealed class ChatController : ITurnHost, IDisposable
    {
        private static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NotifyAfter = TimeSpan.FromSeconds(8);

        private readonly ChatPalette _palette = new ChatPalette();
        private readonly IpcServer _ipc;
        private readonly SidecarManager _sidecar;
        private readonly ToolDispatcher _dispatcher;
        private readonly object _gate = new object();
        private readonly ChatStore _store = new ChatStore(ChatStore.DefaultDirectory);
        private readonly PaletteSettings _settings = PaletteSettings.Load();
        private readonly HashSet<Document> _watchedDocs = new HashSet<Document>();

        private bool _started;
        private volatile bool _busy;
        private int _turnSeq;
        private Document _turnDoc;
        private string _turnInvalidReason;
        private DateTime _turnStarted;

        // per-drawing transcript (feature 17)
        private ChatTranscript _transcript;
        private Document _transcriptDoc;
        private bool _pendingSwitch;

        // current turn
        private ChatEntry _assistant;
        private StringBuilder _assistantText = new StringBuilder();
        private bool _textBreak;
        private readonly HashSet<string> _turnHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, object> _lastPayload;   // retry (feature 13)
        private ChatEntry _lastUserEntry;
        private string _lastError;

        // drawing interaction
        private PickedSelection _selection;                 // feature 1
        private bool _undoAvailable;                        // feature 5
        private bool _editing;                              // feature 20
        private ComposedMessage _afterUndo;
        private bool _foreignChangeDuringTurn;              // user ran commands while Claude worked
        private int _chatGen;                               // bumped by NewChat / drawing switch
        private (string id, bool yes)? _pendingConfirm;     // feature 11

        // usage (feature 14)
        private string _usageText = "";
        private string _rateText = "";

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
            _ipc.Connected += OnConnected;
            _ipc.Disconnected += OnDisconnected;
            _sidecar.StateChanged += OnSidecarState;

            var ui = _palette.Control;
            ui.SendRequested += m => Guard("send", () => SendComposed(m));
            ui.StopRequested += () => Guard("stop", Cancel);
            ui.NewChatRequested += () => Guard("new chat", NewChat);
            ui.PickSelectionRequested += () => Guard("pick", () => MainThread.Post(() => Interaction.Start(Interaction.PickCommand)));
            ui.PickPointRequested += () => Guard("point", () => MainThread.Post(() => Interaction.Start(Interaction.PointCommand)));
            ui.ZoomRequested += h => Guard("zoom", () => Zoom(h));
            ui.UndoTurnRequested += () => Guard("undo", UndoTurn);
            ui.EditLastRequested += () => Guard("edit", EditLast);
            ui.RetryRequested += () => Guard("retry", Retry);
            ui.ConfirmAnswered += (id, yes) => Guard("confirm", () => AnswerConfirm(id, yes));
            ui.ExportRequested += () => Guard("export", () => MainThread.Post(Export));
            ui.SettingsChanged += s => Guard("settings", () => OnSettingsChanged(s));

            Interaction.SelectionPicked += sel => { _selection = sel; Ui.SetSelectionChip(sel, true); _palette.FocusInput(); };
            Interaction.ImpliedSelectionChanged += sel => { if (!_busy) { _selection = sel; Ui.SetSelectionChip(sel, false); } };
            Interaction.PointsPicked += pts => { Ui.InsertAtCaret(Interaction.FormatPoints(pts)); _palette.FocusInput(); };

            var dm = AcApp.DocumentManager;
            dm.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            dm.DocumentActivated += OnDocumentActivated;
            dm.DocumentCreated += (s, e) => WatchDoc(e.Document);
            AcApp.SystemVariableChanged += OnSystemVariableChanged;
        }

        private ChatControl Ui => _palette.Control;

        // ================================================================= palette lifecycle

        /// <summary>CLAUDECHAT: show the palette, start IPC + sidecar the first time.</summary>
        public void ShowPalette()
        {
            _palette.Show();
            if (!_started)
            {
                _started = true;
                Ui.ApplySettings(_settings);
                ApplyTheme();
                Ui.SetTemplates(QuickTemplates.LoadAll(PluginPaths.TemplatesFile, QuickTemplates.UserFile));
                foreach (Document d in AcApp.DocumentManager) WatchDoc(d);
                LoadTranscriptFor(AcApp.DocumentManager.MdiActiveDocument);
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
            ShowPalette();
            _sidecar.Restart();
        }

        private void WatchDoc(Document d)
        {
            if (d == null || !_watchedDocs.Add(d)) return;
            Interaction.Watch(d);
            d.CommandEnded += OnCommandEnded;
            d.CommandCancelled += OnCommandEnded;
            d.CommandFailed += OnCommandEnded;
            try
            {
                d.Database.ObjectAppended += OnDbChanged;
                d.Database.ObjectErased += OnDbChanged;
                d.Database.ObjectModified += OnDbChanged;
            }
            catch (Exception ex) { Logger.Warn("database events: " + ex.Message); }
        }

        /// <summary>Any drawing change outside a chat turn (Properties palette, grips...) makes "Hoàn tác lượt này" unsafe.</summary>
        private void OnDbChanged(object sender, EventArgs e)
        {
            if (_busy || !_undoAvailable) return;
            var obj = (e as Autodesk.AutoCAD.DatabaseServices.ObjectEventArgs)?.DBObject ?? (e as Autodesk.AutoCAD.DatabaseServices.ObjectErasedEventArgs)?.DBObject;
            if (obj is Autodesk.AutoCAD.DatabaseServices.ViewportTableRecord || obj is Autodesk.AutoCAD.DatabaseServices.Viewport) return; // pan / zoom
            _undoAvailable = false;
            Ui.SetUndoAvailable(false);
        }

        // ================================================================= settings / theme (9, 14, 19)

        private void OnSettingsChanged(PaletteSettings s)
        {
            string oldModel = _settings.Model, oldEffort = _settings.Effort, oldTheme = _settings.Theme;
            _settings.Standard = s.Standard;
            _settings.Scale = s.Scale;
            _settings.Model = s.Model;
            _settings.Effort = s.Effort;
            _settings.Theme = s.Theme;
            _settings.Notify = s.Notify;
            _settings.FontSize = s.FontSize;
            _settings.Save();
            if (oldModel != s.Model || oldEffort != s.Effort) SendOptions();
            if (oldTheme != s.Theme) ApplyTheme();
        }

        private void SendOptions()
        {
            // "default" = back to the sidecar's model / the model's default effort
            _ipc.Send(new Dictionary<string, object>
            {
                ["type"] = "set_options",
                ["model"] = string.IsNullOrEmpty(_settings.Model) ? "default" : _settings.Model,
                ["effort"] = string.IsNullOrEmpty(_settings.Effort) ? "default" : _settings.Effort,
            });
        }

        private void ApplyTheme()
        {
            bool dark = _settings.Theme == "dark" || (_settings.Theme != "light" && Theme.AutoCadIsDark());
            Ui.SetDark(dark);
        }

        private void OnSystemVariableChanged(object sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs e)
        {
            if (e.Name.Equals("COLORTHEME", StringComparison.OrdinalIgnoreCase) && _settings.Theme == "auto") ApplyTheme();
        }

        // ================================================================= per-drawing history (17)

        private static string KeyFor(Document doc)
        {
            if (doc == null) return "(none)";
            try
            {
                // unsaved drawings ("Drawing1.dwg") must not share history across AutoCAD sessions
                return doc.IsNamedDrawing
                    ? ChatTranscript.KeyFor(doc.Name)
                    : "unsaved:" + Process.GetCurrentProcess().StartTime.Ticks.ToString(CultureInfo.InvariantCulture) + ":" + doc.Name.ToLowerInvariant();
            }
            catch
            {
                return "(none)";
            }
        }

        private void LoadTranscriptFor(Document doc)
        {
            SaveTranscript();
            _chatGen++;
            _pendingConfirm = null;
            _transcriptDoc = doc;
            _transcript = _store.Load(KeyFor(doc), SafeDocName(doc));
            _undoAvailable = false;
            _selection = null;
            Ui.SetSelectionChip(null, false);
            Ui.LoadTranscript(_transcript, false);
            SendSwitchSession();
        }

        private void SaveTranscript()
        {
            var t = _transcript;
            if (t != null) _store.Save(t);
        }

        private void SendSwitchSession()
        {
            var m = new Dictionary<string, object> { ["type"] = "switch_session" };
            if (!string.IsNullOrEmpty(_transcript?.SessionId)) m["session_id"] = _transcript.SessionId;
            _ipc.Send(m);
        }

        // ================================================================= sending

        private void SendComposed(ComposedMessage m)
        {
            if (_busy) return;
            if (!_ipc.IsConnected)
            {
                Ui.AddErrorCard("Chưa kết nối được với trợ lý. Đợi vài giây rồi bấm Thử lại, hoặc gõ lệnh CLAUDECHATRESTART.", false);
                Ui.SetInputText(m.Text);
                _sidecar.EnsureStarted();
                return;
            }
            if (_editing)
            {
                _editing = false;
                if (_undoAvailable)
                {
                    // feature 20: undo the previous turn first, send once AutoCAD finished the U
                    var refused = Interaction.UndoOnce();
                    if (refused != null)
                    {
                        _editing = true;
                        Ui.SetInputText(m.Text);
                        Ui.SetToolStatus("Chưa hoàn tác được lượt trước: " + refused);
                        return;
                    }
                    _afterUndo = m;
                    _undoAvailable = false;
                    Ui.SetUndoAvailable(false);
                    Ui.AddNote("Đang hoàn tác lượt trước để làm lại theo yêu cầu đã sửa...");
                    Task.Delay(8000).ContinueWith(_ => MainThread.Post(() =>
                    {
                        // U never ran (AutoCAD busy / focus elsewhere): do not send as if it had
                        var p = System.Threading.Interlocked.Exchange(ref _afterUndo, null);
                        if (p == null) return;
                        _editing = true;
                        Ui.SetInputText(p.Text);
                        Ui.AddErrorCard("AutoCAD chưa chạy lệnh U để hoàn tác lượt trước. Bấm vào bản vẽ rồi gửi lại, hoặc tự gõ U trước.", false);
                    }));
                    return;
                }
            }
            Send(m, false);
        }

        /// <summary>Builds and sends the user_message (main thread).</summary>
        private void Send(ComposedMessage m, bool edited, string confirm = null)
        {
            if (_busy) return;
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc != _transcriptDoc) LoadTranscriptFor(doc);

            var payload = new Dictionary<string, object> { ["type"] = "user_message", ["text"] = m.Text };
            if (doc != null) payload["drawing"] = SafeDocName(doc);
            var attachments = new Dictionary<string, object>();
            int selCount = 0;
            if (m.IncludeSelection && _selection != null && _selection.Count > 0)
            {
                selCount = _selection.Count;
                attachments["selection"] = new Dictionary<string, object>
                {
                    ["handles"] = _selection.Handles.Cast<object>().ToList(),
                    ["count"] = _selection.Count,
                    ["summary"] = _selection.Summary,
                };
            }
            if (m.Images.Count > 0)
                attachments["images"] = m.Images.Select(i => (object)new Dictionary<string, object> { ["media_type"] = i.MediaType, ["data"] = i.Base64 }).ToList();
            if (attachments.Count > 0) payload["attachments"] = attachments;
            var ctx = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(_settings.Standard)) ctx["standard"] = _settings.Standard;
            if (!string.IsNullOrEmpty(_settings.Scale)) ctx["scale"] = _settings.Scale;
            if (ctx.Count > 0) payload["context"] = ctx;
            if (edited) payload["edited"] = true;
            if (confirm != null) payload["confirm"] = confirm;

            var entry = new ChatEntry { Role = "user", Text = m.Text, SelectionCount = selCount, ImageCount = m.Images.Count };
            _lastPayload = payload;
            _lastUserEntry = entry;
            _settings.AddHistory(m.Text);
            _settings.Save();
            if (selCount > 0) { _selection = null; Ui.SetSelectionChip(null, false); }
            StartTurn(doc, payload, entry);
        }

        private void StartTurn(Document doc, Dictionary<string, object> payload, ChatEntry userEntry)
        {
            lock (_gate)
            {
                _busy = true;
                _turnSeq++;
                _turnDoc = doc;
                _turnInvalidReason = null;
                _turnStarted = DateTime.UtcNow;
                _assistant = new ChatEntry { Role = "assistant", Text = "" };
                _assistantText = new StringBuilder();
                _textBreak = false;
                _turnHandles.Clear();
                _lastError = null;
                _foreignChangeDuringTurn = false;
                _transcript?.Entries.Add(userEntry);
            }
            _undoAvailable = false;
            Ui.SetUndoAvailable(false);
            SaveTranscript();
            Ui.AddUserMessage(userEntry);
            Ui.SetBusy(true);
            Ui.SetToolStatus("Claude đang suy nghĩ...");
            Ui.BeginAssistant();
            _ipc.SendAsync(payload).ContinueWith(t =>
            {
                if (!t.Result) EndTurn("Gửi tin nhắn thất bại: mất kết nối với trợ lý.", true);
            });
        }

        private void Retry()
        {
            if (_busy || _lastPayload == null) return;
            var copy = new ChatEntry { Role = "user", Text = _lastUserEntry?.Text ?? "", SelectionCount = _lastUserEntry?.SelectionCount ?? 0, ImageCount = _lastUserEntry?.ImageCount ?? 0 };
            if (!_ipc.IsConnected)
            {
                Ui.AddErrorCard("Vẫn chưa kết nối được với trợ lý.", true);
                _sidecar.EnsureStarted();
                return;
            }
            MainThread.Post(() => StartTurn(AcApp.DocumentManager.MdiActiveDocument, _lastPayload, copy));
        }

        private void EditLast()
        {
            if (_busy) return;
            var last = _transcript?.Entries.LastOrDefault(e => e.Role == "user");
            if (last == null) return;
            _editing = true;
            Ui.SetInputText(last.Text);
            Ui.SetToolStatus(_undoAvailable
                ? "Đang sửa yêu cầu trước: khi gửi, lượt trước sẽ được hoàn tác rồi làm lại."
                : "Đang sửa yêu cầu trước (lượt trước không thay đổi bản vẽ hoặc không còn hoàn tác được).");
        }

        private void AnswerConfirm(string id, bool yes)
        {
            if (_busy) { _pendingConfirm = (id, yes); return; } // the card arrived before the turn ended
            MainThread.Post(() => Send(new ComposedMessage { Text = yes ? "Đồng ý" : "Không, hủy thao tác đó." }, false, yes ? "yes" : "no"));
        }

        private void Cancel()
        {
            if (!_busy) return;
            int seq = _turnSeq;
            Ui.SetToolStatus("Đang dừng...");
            _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
            Task.Delay(CancelGrace).ContinueWith(_ =>
            {
                if (_busy && seq == _turnSeq) EndTurn("Đã dừng lượt (trợ lý không phản hồi lệnh dừng).", false);
            });
        }

        private void NewChat()
        {
            if (_busy)
            {
                _ipc.Send(new Dictionary<string, object> { ["type"] = "cancel" });
                EndTurn(null, false);
            }
            _chatGen++;
            _pendingConfirm = null;
            if (_transcript != null)
            {
                _transcript.Entries.Clear();
                _transcript.SessionId = null;
                SaveTranscript();
                Ui.LoadTranscript(_transcript, false);
            }
            _undoAvailable = false;
            if (_sidecar.State == SidecarState.Failed) _sidecar.Restart();
            else _ipc.Send(new Dictionary<string, object> { ["type"] = "new_session" });
        }

        /// <summary>Ends the current turn: closes the undo group, saves the transcript, unlocks the UI. Thread-safe.</summary>
        private void EndTurn(string errorNote, bool canRetry, Dictionary<string, object> usage = null)
        {
            ChatEntry assistant;
            HashSet<string> handles;
            DateTime started;
            int gen;
            lock (_gate)
            {
                if (!_busy) return;
                _busy = false;
                assistant = _assistant;
                _assistant = null;
                handles = new HashSet<string>(_turnHandles, StringComparer.OrdinalIgnoreCase);
                started = _turnStarted;
                gen = _chatGen;
                if (assistant != null)
                {
                    assistant.Text = _assistantText.ToString().Trim();
                    assistant.Handles = handles.ToList();
                }
            }
            // same Dispatcher queue as the streamed text/tool rows → runs after them
            Ui.Enqueue(() => MainThread.RunInAppContext(() =>
            {
                bool wrote = Undo.IsOpen && !_foreignChangeDuringTurn;
                Undo.End();
                if (gen != _chatGen)
                {
                    // New chat / drawing switch happened meanwhile: the old reply belongs nowhere
                    lock (_gate) { _turnDoc = null; _turnInvalidReason = null; }
                    Ui.DiscardAssistant();
                    Ui.SetBusy(false);
                    return;
                }
                lock (_gate)
                {
                    _turnDoc = null;
                    _turnInvalidReason = null;
                }
                _undoAvailable = wrote;
                bool empty = assistant == null || (assistant.Text.Length == 0 && assistant.Tools.Count == 0);
                if (!empty)
                {
                    _transcript?.Entries.Add(assistant);
                    Ui.EndAssistant(assistant, wrote);
                }
                else Ui.DiscardAssistant();
                // an agent error without any reply (login, rate limit, crash) becomes a retry card
                if (string.IsNullOrEmpty(errorNote) && empty && !string.IsNullOrEmpty(_lastError)) errorNote = _lastError;
                if (!string.IsNullOrEmpty(errorNote))
                {
                    _transcript?.Entries.Add(new ChatEntry { Role = "error", Text = errorNote });
                    Ui.AddErrorCard(errorNote, canRetry && _lastPayload != null);
                }
                SaveTranscript();
                Ui.SetBusy(false);
                if (usage != null) ShowUsage(usage);
                MaybeNotify(assistant, errorNote, started);
                if (_pendingSwitch) { _pendingSwitch = false; LoadTranscriptFor(AcApp.DocumentManager.MdiActiveDocument); }
                if (_pendingConfirm.HasValue) { var c = _pendingConfirm.Value; _pendingConfirm = null; AnswerConfirm(c.id, c.yes); }
            }));
        }

        private void MaybeNotify(ChatEntry assistant, string error, DateTime started)
        {
            if (!_settings.Notify || DateTime.UtcNow - started < NotifyAfter || !Notifier.AutoCadInBackground()) return;
            string text = error ?? MarkdownLite.ToPlainText(assistant?.Text ?? "").Split('\n').FirstOrDefault() ?? "Đã xong.";
            Notifier.Show(error != null ? "ClaudeCAD: lượt bị lỗi" : "ClaudeCAD đã xong", string.IsNullOrWhiteSpace(text) ? "Đã xong." : text);
        }

        private void ShowUsage(Dictionary<string, object> u)
        {
            double inT = u.TryGetValue("input_tokens", out var a) && a is double x ? x : 0;
            double outT = u.TryGetValue("output_tokens", out var b) && b is double y ? y : 0;
            string cost = u.TryGetValue("session_cost_usd", out var c) && c is double z ? $" · phiên ≈ ${z.ToString("0.00", CultureInfo.InvariantCulture)}" : "";
            _usageText = $"Lượt vừa rồi: ↑{Kilo(inT)} ↓{Kilo(outT)} token{cost}";
            Ui.SetUsage(_usageText + _rateText);
        }

        private static string Kilo(double n) => n >= 1000 ? (n / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k" : n.ToString("0", CultureInfo.InvariantCulture);

        // ================================================================= drawing interaction (3, 4, 5)

        private void Zoom(List<string> handles)
        {
            if (handles == null || handles.Count == 0) return;
            MainThread.Post(() =>
            {
                int n = Interaction.ZoomAndHighlight(handles);
                if (n == 0) Ui.SetToolStatus("Các đối tượng này không còn trong bản vẽ đang mở.");
            });
        }

        private void UndoTurn()
        {
            if (_busy || !_undoAvailable) return;
            _undoAvailable = false;
            Ui.SetUndoAvailable(false);
            MainThread.Post(() =>
            {
                var refused = Interaction.UndoOnce();
                if (refused == null) Ui.AddNote("Đã hoàn tác lượt vừa rồi (một lệnh U).");
                else
                {
                    _undoAvailable = true;
                    Ui.SetUndoAvailable(true);
                    Ui.SetToolStatus(refused);
                }
            });
        }

        private void OnCommandEnded(object sender, CommandEventArgs e)
        {
            var name = e.GlobalCommandName ?? "";
            if (name.StartsWith("CLAUDECAD_", StringComparison.OrdinalIgnoreCase)) return;
            if (name.Equals("U", StringComparison.OrdinalIgnoreCase))
            {
                var p = System.Threading.Interlocked.Exchange(ref _afterUndo, null);
                if (p != null) { Send(p, true); return; }
            }
            if (_busy)
            {
                // a command run while Claude works lands inside the turn's undo group
                _foreignChangeDuringTurn = true;
                return;
            }
            if (_undoAvailable)
            {
                // any other command (or the user's own U) makes "Hoàn tác lượt này" unsafe
                _undoAvailable = false;
                Ui.SetUndoAvailable(false);
            }
        }

        // ================================================================= export (18)

        private void Export()
        {
            if (_transcript == null || _transcript.Entries.Count == 0) { Ui.SetToolStatus("Chưa có gì để xuất."); return; }
            var name = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(_transcript.DrawingName) ? "ClaudeCAD" : _transcript.DrawingName);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Xuất cuộc chat",
                FileName = name + "-chat.md",
                Filter = "Markdown (*.md)|*.md|Văn bản (*.txt)|*.txt",
                InitialDirectory = SafeDir(_transcriptDoc),
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, _transcript.ToMarkdown(), new UTF8Encoding(false));
            Ui.SetToolStatus("Đã xuất: " + dlg.FileName);
        }

        private static string SafeDir(Document d)
        {
            try
            {
                if (d != null && d.IsNamedDrawing) return Path.GetDirectoryName(d.Name);
            }
            catch { }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        // ================================================================= ITurnHost

        public Document ResolveDocument()
        {
            lock (_gate)
            {
                if (!_busy)
                    throw new FatalToolException("This chat turn has already ended (stopped, timed out or disconnected). Do not continue; wait for the user's next message.");
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

        // ================================================================= IPC events

        private void OnConnected()
        {
            Ui.SetConnection("Đã kết nối, đang khởi tạo phiên...", true);
            // a (re)started sidecar knows nothing: restore this drawing's conversation and the chosen model
            SendSwitchSession();
            SendOptions();
        }

        private void OnMessage(Dictionary<string, object> msg)
        {
            string type = msg.TryGetValue("type", out var t) ? t as string : null;
            switch (type)
            {
                case "tool_call":
                    HandleToolCall(msg);
                    break;
                case "agent_text":
                    if (_busy)
                    {
                        var d = msg.TryGetValue("delta", out var dv) ? dv as string : "";
                        if (string.IsNullOrEmpty(d)) break;
                        string toUi;
                        lock (_gate)
                        {
                            if (!_busy) break;
                            // a new text block after a tool call starts a new paragraph
                            toUi = _textBreak && _assistantText.Length > 0 ? "\n\n" + d : d;
                            _textBreak = false;
                            _assistantText.Append(toUi);
                        }
                        Ui.AppendAssistant(toUi);
                    }
                    break;
                case "agent_text_end":
                    _textBreak = true;
                    break;
                case "tool_status":
                    HandleToolStatus(msg);
                    break;
                case "turn_end":
                    {
                        string err = msg.TryGetValue("error", out var e) ? e as string : null;
                        var usage = msg.TryGetValue("usage", out var u) ? u as Dictionary<string, object> : null;
                        bool cancelled = msg.TryGetValue("cancelled", out var c) && c is bool cb && cb;
                        if (cancelled && string.IsNullOrEmpty(err)) Ui.AddNote("Đã dừng lượt.");
                        EndTurn(string.IsNullOrEmpty(err) ? null : "Lượt bị lỗi: " + err, true, usage);
                        break;
                    }
                case "agent_error":
                    {
                        var m = msg.TryGetValue("message", out var mv) ? mv as string : "?";
                        if (_busy)
                        {
                            _lastError = m; // becomes a "Thử lại" card if the turn ends without a reply
                            Ui.AddNote(m, true);
                        }
                        else Ui.AddErrorCard(m, _lastPayload != null);
                        break;
                    }
                case "confirm_request":
                    {
                        string id = msg.TryGetValue("id", out var iv) ? iv as string : "";
                        string text = msg.TryGetValue("text", out var tv) ? tv as string : "";
                        int? count = msg.TryGetValue("count", out var cv) && cv is double cd ? (int)cd : (int?)null;
                        var hs = msg.TryGetValue("handles", out var hv) && hv is List<object> hl ? hl.OfType<string>().ToList() : null;
                        Ui.AddConfirmCard(id, text, count, hs);
                        Ui.Enqueue(() => _transcript?.Entries.Add(new ChatEntry { Role = "note", Text = "Xác nhận: " + text }));
                        break;
                    }
                case "session_info":
                    {
                        var sid = msg.TryGetValue("session_id", out var sv) ? sv as string : null;
                        Ui.Enqueue(() =>
                        {
                            if (_transcript != null && !string.IsNullOrEmpty(sid) && sid != _transcript.SessionId)
                            {
                                _transcript.SessionId = sid;
                                SaveTranscript();
                            }
                        });
                        break;
                    }
                case "rate_limit":
                    {
                        string status = msg.TryGetValue("status", out var st) ? st as string : "";
                        double util = msg.TryGetValue("utilization", out var uv) && uv is double ud ? ud : -1;
                        string kind = msg.TryGetValue("limit_type", out var kv) ? kv as string : "";
                        string reset = msg.TryGetValue("resets_at", out var rv) && rv is double rd
                            ? DateTimeOffset.FromUnixTimeSeconds((long)rd).LocalDateTime.ToString("HH:mm dd/MM", CultureInfo.InvariantCulture) : "";
                        string label = kind == "five_hour" ? "hạn mức 5 giờ" : kind != null && kind.StartsWith("seven_day") ? "hạn mức tuần" : "hạn mức";
                        _rateText = status == "allowed" && util < 0.5 ? "" :
                            $" · {label}{(util >= 0 ? $" {util * 100:0}%" : "")}{(status == "rejected" ? " — đã hết" : "")}{(reset.Length > 0 ? $", làm mới {reset}" : "")}";
                        Ui.SetUsage(_usageText + _rateText);
                        break;
                    }
                case "options":
                    {
                        var model = msg.TryGetValue("model", out var mv) ? mv as string : null;
                        if (!string.IsNullOrEmpty(model)) Ui.SetConnection("Đã kết nối · " + PaletteSettings.ModelLabel(model), true);
                        break;
                    }
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
            if (!_busy) return; // late status after Stop / New chat
            string tool = msg.TryGetValue("tool", out var t) ? t as string : "";
            string state = msg.TryGetValue("state", out var s) ? s as string : "";
            var entry = new ToolLogEntry
            {
                Tool = tool,
                State = state,
                Ms = msg.TryGetValue("ms", out var ms) && ms is double d ? (long)d : 0,
                Summary = msg.TryGetValue("summary", out var sm) ? sm as string : null,
                Error = msg.TryGetValue("error", out var er) ? er as string : null,
            };
            if (msg.TryGetValue("handles", out var hv) && hv is List<object> hl && state == "done")
                lock (_gate) foreach (var h in hl.OfType<string>()) _turnHandles.Add(h);
            lock (_gate)
            {
                var a = _assistant;
                if (a != null)
                {
                    int idx = state == "running" ? -1 : a.Tools.FindIndex(x => x.State == "running" && x.Tool == tool);
                    if (idx >= 0) a.Tools[idx] = entry; else a.Tools.Add(entry);
                }
            }
            Ui.UpdateTool(entry);
            string label = ToolLabels.Describe(tool);
            switch (state)
            {
                case "running": Ui.SetToolStatus("⏳ " + label + "..."); break;
                case "done": Ui.SetToolStatus("✓ " + label); break;
                case "error": Ui.SetToolStatus("⚠ " + label + " lỗi"); break;
            }
        }

        private void OnDisconnected(string reason)
        {
            if (_busy) EndTurn("Mất kết nối với trợ lý giữa lượt; lượt này đã dừng. Lịch sử chat vẫn giữ nguyên.", true);
            Ui.SetConnection("Mất kết nối với trợ lý...", false);
        }

        private void OnSidecarState(SidecarState state, string message)
        {
            bool ok = state == SidecarState.Running || state == SidecarState.Starting;
            if (!_ipc.IsConnected || !ok) Ui.SetConnection(message, ok);
            if (state == SidecarState.Restarting || state == SidecarState.Failed) Ui.AddNote(message, state == SidecarState.Failed);
        }

        // ================================================================= document events

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
            Interaction.Unwatch(e.Document);
            _watchedDocs.Remove(e.Document);
            if (e.Document == _transcriptDoc) { SaveTranscript(); _transcriptDoc = null; }
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
                _pendingSwitch = true;
                return;
            }
            if (!_started || e.Document == _transcriptDoc) return;
            if (_busy) { _pendingSwitch = true; return; }
            WatchDoc(e.Document);
            LoadTranscriptFor(e.Document);
        }

        // ================================================================= helpers

        private void Guard(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Logger.Error("palette action failed: " + what, ex);
                try { Ui.AddNote($"Lỗi nội bộ ({what}): {ex.Message}. Xem log bằng lệnh CLAUDECHATLOG.", true); } catch { }
            }
        }

        private static string SafeDocName(Document d)
        {
            try { return d == null ? "" : Path.GetFileName(d.Name); } catch { return ""; }
        }

        public void Dispose()
        {
            try
            {
                SaveTranscript();
                _settings.Save();
                var dm = AcApp.DocumentManager;
                dm.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
                dm.DocumentActivated -= OnDocumentActivated;
                AcApp.SystemVariableChanged -= OnSystemVariableChanged;
                foreach (var d in _watchedDocs.ToList()) { try { d.CommandEnded -= OnCommandEnded; } catch { } Interaction.Unwatch(d); }
                Notifier.Dispose();
            }
            catch { }
            _sidecar.Dispose();
            _ipc.Dispose();
        }
    }
}
