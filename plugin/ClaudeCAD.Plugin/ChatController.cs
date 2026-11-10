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
        private SkillStore _skillStore;
        private FileSystemWatcher _skillWatcher;
        private System.Threading.Timer _skillDebounce;

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

        // v0.6: macros, change summary, suggestions, plans
        private readonly MacroRecorder _recorder = new MacroRecorder();
        private readonly ChangeTracker _changes = new ChangeTracker();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ChatEntry, List<MacroCall>> _turnMacros =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ChatEntry, List<MacroCall>>();
        private MacroStore _macroStore;
        private Autodesk.AutoCAD.DatabaseServices.Database _turnDb;
        private bool _macroRunning;
        private volatile bool _macroCancel;
        private readonly List<string> _turnSuggestions = new List<string>();
        private (string id, string kind)? _pendingPlan;

        // v0.9.2: usage limit + toasts
        private string _rateStatus = "";
        private double _rateUtil = -1;
        private string _rateKind = "";
        private DateTime? _rateResetsAt;
        private string _rateWarnedKey;
        private int _limitCardTurn = -1;
        private System.Threading.Timer _autoRetryTimer;
        private bool _autoRetryOn;
        private bool _wasDisconnected;

        // v0.7: manual edits, snapshot, version history
        private DrawingHistory History => DrawingHistory.Instance;
        private Document _turnStartDoc;
        private string _turnLabel = "";
        private bool _versionChecked;
        private readonly Dictionary<Document, (Document original, VersionInfo info)> _versionDocs = new Dictionary<Document, (Document, VersionInfo)>();
        private (Document original, VersionInfo info)? _openingVersion;

        // usage (feature 14)
        private string _usageText = "";
        private string _rateText = "";

        public UndoScope Undo { get; } = new UndoScope();
        private string _readyInfo = "";

        /// <summary>CLAUDECHATSELFTEST: state of the palette, the assistant process and the paths.</summary>
        public List<(string name, bool ok, string detail)> Diagnostics()
        {
            var list = new List<(string, bool, string)>
            {
                ("Kích hoạt (key theo máy)", Activation.IsActivated, Activation.StatusText),
                ("Khung chat đã mở trong phiên này", _started, _started ? "có" : "chưa (gõ CLAUDECHAT để khởi động trợ lý)"),
                ("Tiến trình trợ lý (Node)", _sidecar.State == SidecarState.Running, _sidecar.State.ToString()),
                ("Kết nối plugin ↔ trợ lý (named pipe)", _ipc.IsConnected, _ipc.IsConnected ? _ipc.PipeName : "chưa kết nối"),
                ("Phiên Claude (model · đăng nhập)", _readyInfo.Contains("·"), string.IsNullOrEmpty(_readyInfo) ? "chưa có — gửi một tin nhắn thử" : _readyInfo),
                ("node.exe", PluginPaths.NodeExecutable != null, PluginPaths.NodeExecutable ?? "không tìm thấy Node.js 18+"),
                ("Thư mục trợ lý (agent)", PluginPaths.AgentEntry != null, PluginPaths.AgentEntry ?? "không thấy agent\\dist\\index.js"),
                ("Thư mục skill", _skillStore != null || PluginPaths.SkillsDirectory != null, _skillStore?.Root ?? PluginPaths.SkillsDirectory ?? "không thấy"),
            };
            return list;
        }
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
            ui.AddSkillRequested += () => Guard("add skill", () => MainThread.Post(AddSkill));
            ui.SkillToggled += (n, on) => Guard("toggle skill", () => ToggleSkill(n, on));
            ui.SkillEditRequested += n => Guard("edit skill", () => EditSkill(n));
            ui.SkillDeleteRequested += n => Guard("delete skill", () => MainThread.Post(() => DeleteSkill(n)));
            ui.OpenSkillsFolderRequested += () => Guard("skills folder", OpenSkillsFolder);
            ui.CaptureScreenRequested += () => Guard("capture", () => MainThread.Post(CaptureScreen));
            ui.ReadOnlyToggled += on => Guard("read-only", () => SetReadOnly(on));
            ui.NotesRequested += () => Guard("notes", OpenNotes);
            ui.NotesSaved += (f, d) => Guard("notes", () => SaveNotes(f, d));
            ui.MacroSaveRequested += (e, n) => Guard("save macro", () => MainThread.Post(() => SaveMacro(e, n)));
            ui.MacroRunRequested += (n, at) => Guard("run macro", () => MainThread.Post(() => RunMacro(n, at)));
            ui.MacroDeleteRequested += n => Guard("delete macro", () => MainThread.Post(() => DeleteMacro(n)));
            ui.OpenMacrosFolderRequested += () => Guard("macros folder", OpenMacrosFolder);
            ui.TemplateSaveRequested += (t, x) => Guard("save template", () => SaveTemplate(t, x));
            ui.CreateSkillFromChatRequested += () => Guard("skill from chat", CreateSkillFromChat);
            ui.SkillDraftOpenRequested += f => Guard("open draft", () => OpenFile(f));
            ui.SkillDraftAddRequested += f => Guard("add draft", () => MainThread.Post(() => ImportSkillFiles(new[] { f })));
            ui.PlanAnswered += (id, kind) => Guard("plan", () => AnswerPlan(id, kind));
            ui.MentionSource = () => Interaction.DrawingNames(AcApp.DocumentManager.MdiActiveDocument);
            ui.LayerSource = () => Interaction.LayerRows(AcApp.DocumentManager.MdiActiveDocument);
            ui.VersionOpenRequested += id => Guard("open version", () => MainThread.Post(() => OpenVersion(id)));
            ui.VersionCloseRequested += () => Guard("close version", () => MainThread.Post(CloseVersions));
            ui.OpenVersionsFolderRequested += () => Guard("versions folder", OpenVersionsFolder);
            ui.LoginRequested += () => Guard("login", Login);
            ui.SelfTestRequested += () => Guard("self-test", () => MainThread.Post(() => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_CLAUDECHATSELFTEST ", true, false, false)));
            ui.WelcomeRequested += () => Guard("welcome", ShowWelcome);
            ui.SettingsOpened += () => Guard("account status", RefreshAccountStatus);
            ui.SwitchAccountRequested += () => Guard("switch account", SwitchAccount);
            ui.LogoutRequested += () => Guard("logout", Logout);
            ui.ApiKeySaveRequested += k => Guard("api key", () => SaveApiKey(k));
            ui.ApiKeyClearRequested += () => Guard("api key", ClearApiKey);
            ui.ReportRequested += () => Guard("report", ReportBug);
            ui.LimitAutoRetryToggled += on => Guard("auto retry", () => SetAutoRetry(on));
            ui.ActivationRequestRequested += () => Guard("activation request", RequestActivation);
            ui.ActivationKeySubmitted += k => Guard("activation", () => SubmitActivation(k));
            ui.ApiKeyMask = () => ApiKeyStore.HasKey ? ApiKeyStore.Mask(ApiKeyStore.Load()) : null;

            Interaction.SelectionPicked += sel => { _selection = sel; Ui.SetSelectionChip(sel, true); _palette.FocusInput(); };
            Interaction.ImpliedSelectionChanged += sel => { if (!_busy) { _selection = sel; Ui.SetSelectionChip(sel, false); } };
            Interaction.PointsPicked += pts => { Ui.InsertAtCaret(Interaction.FormatPoints(pts)); _palette.FocusInput(); };

            var dm = AcApp.DocumentManager;
            dm.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            dm.DocumentActivated += OnDocumentActivated;
            dm.DocumentCreated += (s, e) => WatchDoc(e.Document);
            DrawingHistory.Instance.ObjectChanged += OnDbChanged; // one subscription to the database events for the whole plugin
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
                InitSkills();
                _macroStore = new MacroStore(MacrosRoot);
                Ui.SetMacros(_macroStore.List());
                foreach (Document d in AcApp.DocumentManager) WatchDoc(d);
                LoadTranscriptFor(AcApp.DocumentManager.MdiActiveDocument);
                _ipc.Start();
                UpdateService.CheckInBackground(msg => { Ui.AddNote(msg); Ui.ShowToast(msg, ChatControl.ToastKind.Info, 10); }); // phase 3: daily update check
            }
            // no Node / Claude Code process on a machine without a licence: it would run for nothing
            if (!Activation.IsActivated) ShowActivationCard();
            else _sidecar.EnsureStarted();
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

        /// <summary>CC / Ctrl+Shift+Space: show the palette and put the caret in the input box.</summary>
        public void FocusInput()
        {
            ShowPalette();
            _palette.FocusInput();
        }

        public void RestartSidecar()
        {
            ShowPalette();
            RestartSidecarQuietly();
        }

        private void WatchDoc(Document d)
        {
            if (d == null || !_watchedDocs.Add(d)) return;
            Interaction.Watch(d);
            d.CommandEnded += OnCommandEnded;
            d.CommandCancelled += OnCommandEnded;
            d.CommandFailed += OnCommandEnded;
            DrawingHistory.Instance.Watch(d); // database events come from DrawingHistory.ObjectChanged
        }

        /// <summary>Any drawing change outside a chat turn (Properties palette, grips...) makes "Hoàn tác lượt này" unsafe.</summary>
        private void OnDbChanged(Autodesk.AutoCAD.DatabaseServices.Database db, Autodesk.AutoCAD.DatabaseServices.DBObject obj, string kind, bool erased)
        {
            if (_busy)
            {
                // "Thêm · Sửa · Xóa" card of the turn: entities of the turn's drawing only
                if (!(obj is Autodesk.AutoCAD.DatabaseServices.Entity) || !ReferenceEquals(db, _turnDb)) return;
                string h;
                try { h = obj.Handle.ToString(); } catch { return; }
                if (kind == "added") _changes.Appended(h);
                else if (kind == "modified") _changes.Modified(h);
                else _changes.Erased(h, erased);
                return;
            }
            if (!_undoAvailable) return;
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
            _settings.VisualCheck = s.VisualCheck;
            _settings.PlanFirst = s.PlanFirst;
            _settings.Suggestions = s.Suggestions;
            _settings.Hotkey = s.Hotkey;
            PluginApp.HotkeyEnabled = s.Hotkey;
            _settings.Versions = s.Versions;
            _settings.SnapshotNextToDrawing = s.SnapshotNextToDrawing;
            History.NextToDrawing = s.SnapshotNextToDrawing;
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

        // ================================================================= skills (+ Add skill)

        /// <summary>Skill folder of the plugin (Contents\skills in the bundle, repo\skills in dev).</summary>
        private string SkillsRoot => PluginPaths.SkillsDirectory ?? Path.Combine(PluginPaths.DllDirectory, "skills");

        private void InitSkills()
        {
            _skillStore = new SkillStore(SkillsRoot);
            RefreshSkills();
            try
            {
                Directory.CreateDirectory(_skillStore.Root);
                // edits made with "Sửa" (or in Explorer) are picked up automatically
                _skillWatcher = new FileSystemWatcher(_skillStore.Root, "*.md") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName };
                FileSystemEventHandler h = (s, e) => ScheduleSkillReload();
                _skillWatcher.Changed += h;
                _skillWatcher.Created += h;
                _skillWatcher.Deleted += h;
                _skillWatcher.Renamed += (s, e) => ScheduleSkillReload();
                _skillWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.Warn("skills watcher unavailable: " + ex.Message);
            }
        }

        private void ScheduleSkillReload()
        {
            _skillDebounce?.Dispose();
            _skillDebounce = new System.Threading.Timer(_ => Ui.Enqueue(() =>
            {
                RefreshSkills();
                ReloadSkillsInAgent();
                Ui.ShowToast("Skill đã thay đổi — Claude dùng bản mới từ tin nhắn tiếp theo.", ChatControl.ToastKind.Info);
            }), null, 1500, System.Threading.Timeout.Infinite);
        }

        private List<SkillInfo> RefreshSkills()
        {
            var list = _skillStore?.Scan() ?? new List<SkillInfo>();
            Ui.SetSkills(list);
            return list;
        }

        private void ReloadSkillsInAgent() => _ipc.Send(new Dictionary<string, object> { ["type"] = "reload_skills" });

        /// <summary>"+ Add skill": pick .md files (default: the plugin's skill folder), copy, register, report.</summary>
        private void AddSkill()
        {
            if (_skillStore == null) InitSkills();
            if (!_skillStore.IsWritable())
            {
                Ui.AddErrorCard($"Không ghi được vào thư mục skill của plugin ({_skillStore.Root}). Nếu cậu cài cho mọi người dùng (ProgramData), hãy cài lại cho riêng tài khoản của cậu bằng Setup.exe.", false);
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file skill (.md)",
                Filter = "Skill Markdown (*.md)|*.md",
                Multiselect = true,
                InitialDirectory = _skillStore.Root,
            };
            if (dlg.ShowDialog() != true) return;
            ImportSkillFiles(dlg.FileNames);
        }

        /// <summary>Imports skill files (+ Add skill, or a draft Claude wrote): conflict prompt, status notes, reload.</summary>
        private void ImportSkillFiles(IEnumerable<string> files)
        {
            if (_skillStore == null) InitSkills();
            if (!_skillStore.IsWritable())
            {
                Ui.AddErrorCard($"Không ghi được vào thư mục skill của plugin ({_skillStore.Root}).", false);
                return;
            }
            if (_skillWatcher != null) _skillWatcher.EnableRaisingEvents = false; // our own copies are not "edits"
            var added = new List<string>();
            try
            {
                foreach (var file in files)
                {
                    var r = _skillStore.Import(file, false);
                    if (r.Status == SkillImportStatus.Conflict)
                    {
                        var answer = System.Windows.MessageBox.Show(
                            $"Plugin đã có skill \"{r.Name}\".\n\nThay bằng nội dung của file:\n{file} ?",
                            "ClaudeCAD — skill đã tồn tại", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                        if (answer != System.Windows.MessageBoxResult.Yes) { Ui.AddNote($"Giữ nguyên skill \"{r.Name}\" (không thay)."); continue; }
                        r = _skillStore.Import(file, true);
                    }
                    switch (r.Status)
                    {
                        case SkillImportStatus.Invalid:
                            Ui.AddErrorCard(r.Message + "\n\nFile skill cần bắt đầu bằng:\n---\nname: ten-skill\ndescription: Khi nào dùng skill, các từ khóa...\n---", false);
                            break;
                        default:
                            added.Add(r.Name);
                            Ui.AddNote("✓ " + r.Message + " Skill đang bật; Claude dùng từ tin nhắn tiếp theo (vẫn nhớ cuộc chat).");
                            break;
                    }
                }
            }
            finally
            {
                if (_skillWatcher != null) _skillWatcher.EnableRaisingEvents = true;
            }
            if (added.Count == 0) return;
            RefreshSkills();
            ReloadSkillsInAgent();
        }

        private void ToggleSkill(string name, bool on)
        {
            _skillStore?.SetEnabled(name, on);
            RefreshSkills();
            ReloadSkillsInAgent();
            Ui.SetToolStatus(on ? $"Đã bật skill {name}." : $"Đã tắt skill {name}.");
            if (!on && _settings.Standard == name)
            {
                _settings.Standard = "";
                _settings.Save();
                Ui.ApplySettings(_settings);
            }
        }

        private void EditSkill(string name)
        {
            var file = _skillStore?.SkillFile(name);
            if (file == null || !File.Exists(file)) return;
            try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); }
            catch { Process.Start("notepad.exe", "\"" + file + "\""); } // no .md editor associated
            Ui.SetToolStatus($"Đang mở {name}\\SKILL.md — lưu file là xong, plugin tự nạp lại.");
        }

        private void DeleteSkill(string name)
        {
            var answer = System.Windows.MessageBox.Show(
                $"Xóa skill \"{name}\" khỏi plugin?\n\nThư mục {Path.Combine(_skillStore.Root, name)} sẽ bị xóa.",
                "ClaudeCAD — xóa skill", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (answer != System.Windows.MessageBoxResult.Yes) return;
            if (_skillWatcher != null) _skillWatcher.EnableRaisingEvents = false;
            try { _skillStore.Delete(name); }
            finally { if (_skillWatcher != null) _skillWatcher.EnableRaisingEvents = true; }
            if (_settings.Standard == name) { _settings.Standard = ""; _settings.Save(); Ui.ApplySettings(_settings); }
            RefreshSkills();
            ReloadSkillsInAgent();
            Ui.AddNote($"Đã xóa skill \"{name}\".");
        }

        private void OpenSkillsFolder()
        {
            var root = _skillStore?.Root ?? SkillsRoot;
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", "\"" + root + "\"");
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
            // the request waiting for the usage limit belongs to the drawing we leave: never resend it into another one
            if (doc != _transcriptDoc)
            {
                if (CancelAutoRetry()) Ui.ShowToast("Đã hủy tự gửi lại vì cậu chuyển sang bản vẽ khác.", ChatControl.ToastKind.Info, 6);
                _lastPayload = null;
                _lastUserEntry = null;
            }
            _activationCardShown = false; // the card (if any) went away with the old conversation
            _chatGen++;
            _pendingConfirm = null;
            _pendingPlan = null;
            _transcriptDoc = doc;
            _transcript = _store.Load(KeyFor(doc), SafeDocName(doc));
            _undoAvailable = false;
            _selection = null;
            Ui.SetSelectionChip(null, false);
            Ui.LoadTranscript(_transcript, false);
            Ui.SetReadOnly(_transcript.ReadOnly);
            Ui.ClearSuggestions();
            Ui.CloseDrawingPanels(); // notes / layer picker belong to the previous drawing
            UpdateNotesIndicator();
            RefreshVersions();
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
            if (!RequireActivation()) { Ui.SetInputText(m.Text); return; }
            if (IsVersionDoc(AcApp.DocumentManager.MdiActiveDocument))
            {
                Ui.SetInputText(m.Text);
                Ui.SetToolStatus("Đang xem phiên bản cũ (chỉ xem) — bấm \"Đóng — về bản hiện tại\" rồi chat.");
                return;
            }
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
        private void Send(ComposedMessage m, bool edited, string confirm = null, string plan = null)
        {
            if (_busy) return;
            if (!RequireActivation()) return; // retry / edit / plan answers come here without SendComposed
            CancelAutoRetry(); // a new request replaces the one waiting for the limit
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (RefuseOnVersionTab(doc, m.Text)) return;
            if (doc != _transcriptDoc) LoadTranscriptFor(doc);

            var payload = new Dictionary<string, object> { ["type"] = "user_message", ["text"] = m.Text };
            if (doc != null) payload["drawing"] = SafeDocName(doc);
            var attachments = new Dictionary<string, object>();
            int selCount = 0;
            if (m.IncludeSelection && _selection != null && _selection.Count > 0)
            {
                // layers unticked under the selection chip are left out
                var kept = LayerSelection.Filter(_selection.Handles, _selection.HandleLayers, m.ExcludedLayers);
                var keptSet = new HashSet<string>(kept, StringComparer.OrdinalIgnoreCase);
                var keptLayers = _selection.HandleLayers.Count == _selection.Handles.Count
                    ? LayerSelection.CountByLayer(_selection.Handles.Select((h, i) => keptSet.Contains(h) ? _selection.HandleLayers[i] : null).Where(l => l != null))
                    : new Dictionary<string, int>();
                selCount = kept.Count;
                var selDict = new Dictionary<string, object>
                {
                    ["handles"] = kept.Cast<object>().ToList(),
                    ["count"] = kept.Count,
                    ["summary"] = kept.Count == _selection.Count ? _selection.Summary : $"{kept.Count} đối tượng",
                };
                if (keptLayers.Count > 0) selDict["layers"] = keptLayers.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                if (kept.Count > 0) attachments["selection"] = selDict;
            }
            if (m.Layers.Count > 0)
            {
                attachments["layers"] = new Dictionary<string, object>
                {
                    ["names"] = m.Layers.Cast<object>().ToList(),
                    ["counts"] = m.Layers.ToDictionary(l => l, l => (object)(m.LayerCounts.TryGetValue(l, out var n) ? n : 0)),
                    ["total"] = m.Layers.Sum(l => m.LayerCounts.TryGetValue(l, out var n) ? n : 0),
                };
            }
            if (m.Images.Count > 0)
                attachments["images"] = m.Images.Select(i => (object)new Dictionary<string, object> { ["media_type"] = i.MediaType, ["data"] = i.Base64 }).ToList();
            if (attachments.Count > 0) payload["attachments"] = attachments;
            var ctx = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(_settings.Standard)) ctx["standard"] = _settings.Standard;
            if (!string.IsNullOrEmpty(_settings.Scale)) ctx["scale"] = _settings.Scale;
            if (_transcript?.ReadOnly == true) ctx["read_only"] = true;
            var notes = NotesFor(doc);
            if (notes != null) ctx["notes"] = notes;
            if (!_settings.VisualCheck) ctx["visual_check"] = false;
            if (!_settings.PlanFirst) ctx["plan_first"] = false;
            if (!_settings.Suggestions) ctx["suggestions"] = false;
            Dictionary<string, object> manual = null;
            string manualShort = null;
            try
            {
                bool hasConversation = _transcript != null && _transcript.Entries.Any(x => x.Role == "assistant");
                manual = History.TakeManualChanges(doc, KeyFor(doc), DrawingPathOf(doc), hasConversation, out manualShort);
            }
            catch (Exception ex) { Logger.Warn("manual changes not collected: " + ex.Message); }
            if (manual != null) ctx["manual_changes"] = manual;
            if (ctx.Count > 0) payload["context"] = ctx;
            if (edited) payload["edited"] = true;
            if (confirm != null) payload["confirm"] = confirm;
            if (plan != null) payload["plan"] = plan;

            var entry = new ChatEntry { Role = "user", Text = m.Text, SelectionCount = selCount, ImageCount = m.Images.Count, Layers = m.Layers.ToList() };
            _lastPayload = payload;
            _lastUserEntry = entry;
            _settings.AddHistory(m.Text);
            _settings.Save();
            if (selCount > 0) { _selection = null; Ui.SetSelectionChip(null, false); }
            if (manualShort != null)
            {
                var since = (manual.TryGetValue("since", out var sv) ? sv as string : "") == "previous" ? "từ lần chat trước" : "từ lượt chat trước";
                var note = $"✎ Bản vẽ đã được sửa ngoài khung chat {since}: {manualShort} — Claude sẽ đọc lại các đối tượng này trước khi làm.";
                Ui.AddNote(note);
                _transcript?.Entries.Add(new ChatEntry { Role = "note", Text = note });
            }
            StartTurn(doc, payload, entry);
        }

        /// <summary>An old version (read-only copy) is the active drawing: chatting there would lose the conversation.</summary>
        private bool RefuseOnVersionTab(Document doc, string text)
        {
            if (!IsVersionDoc(doc)) return false;
            if (!string.IsNullOrEmpty(text)) Ui.SetInputText(text);
            Ui.SetToolStatus("Đang xem phiên bản cũ (chỉ xem) — bấm \"Đóng — về bản hiện tại\" rồi làm tiếp.");
            return true;
        }

        private void StartTurn(Document doc, Dictionary<string, object> payload, ChatEntry userEntry, bool toAgent = true)
        {
            if (RefuseOnVersionTab(doc, null)) return;
            lock (_gate)
            {
                // two Retry() posts in one pump cycle (auto-retry timer + "Thử lại") would start the same turn twice
                if (_busy) return;
                _busy = true;
                _turnSeq++;
                if (payload != null) payload["turn_id"] = "t-" + _turnSeq.ToString(CultureInfo.InvariantCulture);
                _turnDoc = doc;
                _turnInvalidReason = null;
                _turnStarted = DateTime.UtcNow;
                _assistant = new ChatEntry { Role = "assistant", Text = "" };
                _assistantText = new StringBuilder();
                _textBreak = false;
                _turnHandles.Clear();
                _lastError = null;
                _foreignChangeDuringTurn = false;
                _turnDb = doc?.Database;
                _turnStartDoc = doc;
                _turnLabel = userEntry?.Text ?? "";
                _versionChecked = false;
                _turnSuggestions.Clear();
                _transcript?.Entries.Add(userEntry);
            }
            _recorder.Clear();
            _changes.Clear();
            // a slow (Wblock) version copy is prepared now, before any tool call can time out waiting for it
            if (_settings.Versions && doc != null)
            {
                try { History.PrepareVersion(doc, KeyFor(doc)); }
                catch (Exception ex) { Logger.Warn("version copy not prepared: " + ex.Message); }
            }
            History.TurnDb = doc?.Database; // changes from now on are Claude's, not manual
            Ui.ClearSuggestions();
            _undoAvailable = false;
            Ui.SetUndoAvailable(false);
            SaveTranscript();
            Ui.AddUserMessage(userEntry);
            Ui.SetBusy(true);
            Ui.SetToolStatus(toAgent ? "Claude đang suy nghĩ..." : "Đang chạy macro...");
            Ui.BeginAssistant();
            if (!toAgent) return;
            _ipc.SendAsync(payload).ContinueWith(t =>
            {
                if (t.IsFaulted) Logger.Error("send user_message failed", t.Exception?.GetBaseException());
                if (t.IsFaulted || !t.Result) EndTurn("Gửi tin nhắn thất bại: mất kết nối với trợ lý.", true);
            });
        }

        private void Retry()
        {
            if (_busy || _lastPayload == null) return;
            if (AcApp.DocumentManager.MdiActiveDocument != _transcriptDoc)
            {
                // the last request was made in another drawing (or that drawing was closed)
                Ui.SetToolStatus("Yêu cầu trước thuộc bản vẽ khác — gõ lại yêu cầu cho bản vẽ này.");
                return;
            }
            var copy = new ChatEntry { Role = "user", Text = _lastUserEntry?.Text ?? "", SelectionCount = _lastUserEntry?.SelectionCount ?? 0, ImageCount = _lastUserEntry?.ImageCount ?? 0, Layers = _lastUserEntry?.Layers?.ToList() ?? new List<string>() };
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
            if (_macroRunning) { _macroCancel = true; Ui.SetToolStatus("Đang dừng macro..."); return; }
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
            _pendingPlan = null;
            if (_macroRunning) { _macroCancel = true; _macroRunning = false; }
            CancelAutoRetry();
            _lastPayload = null; // "Thử lại" must not resend the old request into the new chat
            _lastUserEntry = null;
            _activationCardShown = false;
            try { History.Reset(_transcriptDoc, KeyFor(_transcriptDoc), DrawingPathOf(_transcriptDoc)); }
            catch (Exception ex) { Logger.Warn("snapshot reset failed: " + ex.Message); }
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
            Autodesk.AutoCAD.DatabaseServices.Database turnDb;
            Document turnStartDoc;
            List<string> suggestions;
            lock (_gate)
            {
                if (!_busy) return;
                _busy = false;
                turnDb = _turnDb;
                _turnDb = null;
                turnStartDoc = _turnStartDoc;
                _turnStartDoc = null;
                suggestions = _turnSuggestions.ToList();
                _turnSuggestions.Clear();
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
                var macroCalls = _recorder.Take();
                var changes = BuildChangeSummary(turnDb);
                History.TurnDb = null;
                History.DiscardPendingVersion(); // prepared for a turn that did not change the drawing
                try
                {
                    // the "biên bản": what the drawing looks like after this turn
                    if (turnStartDoc != null && !turnStartDoc.IsDisposed && gen == _chatGen)
                        History.AfterTurn(turnStartDoc, KeyFor(turnStartDoc), DrawingPathOf(turnStartDoc));
                }
                catch (Exception ex) { Logger.Warn("snapshot after turn failed: " + ex.Message); }
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
                    assistant.Changes = changes;
                    bool canMacro = macroCalls != null;
                    if (canMacro) { _turnMacros.Remove(assistant); _turnMacros.Add(assistant, macroCalls); }
                    _transcript?.Entries.Add(assistant);
                    Ui.EndAssistant(assistant, wrote, canMacro);
                }
                else Ui.DiscardAssistant();
                // an agent error without any reply (login, rate limit, crash) becomes a retry card
                if (string.IsNullOrEmpty(errorNote) && empty && !string.IsNullOrEmpty(_lastError)) errorNote = _lastError;
                // the usage limit already has its own card for this turn: no second, generic "Lượt bị lỗi" card
                if (_limitHitTurn == _turnSeq) errorNote = null;
                if (!string.IsNullOrEmpty(errorNote))
                {
                    _transcript?.Entries.Add(new ChatEntry { Role = "error", Text = errorNote });
                    Ui.AddErrorCard(errorNote, canRetry && _lastPayload != null);
                }
                SaveTranscript();
                Ui.SetBusy(false);
                if (_settings.Suggestions && suggestions.Count > 0 && string.IsNullOrEmpty(errorNote)) Ui.AddSuggestions(suggestions);
                if (usage != null) ShowUsage(usage);
                MaybeNotify(assistant, errorNote, started);
                if (_pendingSwitch)
                {
                    _pendingSwitch = false;
                    var active = AcApp.DocumentManager.MdiActiveDocument;
                    if (!IsVersionDoc(active) && active != _transcriptDoc) LoadTranscriptFor(active);
                }
                if (_pendingConfirm.HasValue) { var c = _pendingConfirm.Value; _pendingConfirm = null; AnswerConfirm(c.id, c.yes); }
                if (_pendingPlan.HasValue) { var pp = _pendingPlan.Value; _pendingPlan = null; AnswerPlan(pp.id, pp.kind); }
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

        // ================================================================= v0.6: look at the drawing, read-only, notes

        /// <summary>"Chụp": the current view as an image attachment of the next message.</summary>
        private void CaptureScreen()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { Ui.SetToolStatus("Không có bản vẽ nào đang mở."); return; }
            try
            {
                var r = Capture.Render(doc, null, null, 1280, null);
                Ui.AddImagePng(r.Png);
            }
            catch (Exception ex)
            {
                Logger.Error("capture failed", ex);
                Ui.AddNote("Không chụp được bản vẽ: " + ex.Message, true);
            }
        }

        private void SetReadOnly(bool on)
        {
            if (_transcript == null) return;
            if (_transcript.ReadOnly == on) return;
            _transcript.ReadOnly = on;
            SaveTranscript();
            var name = string.IsNullOrEmpty(_transcript.DrawingName) ? "bản vẽ này" : _transcript.DrawingName;
            Ui.AddNote(on
                ? $"🔒 Đã bật Chỉ xem cho {name}: Claude chỉ đọc, đo, hỏi đáp — mọi lệnh sửa bản vẽ đều bị chặn."
                : $"🔓 Đã tắt Chỉ xem: Claude được sửa {name}.");
        }

        private string DrawingPathOf(Document d)
        {
            try { return d != null && d.IsNamedDrawing ? d.Name : null; } catch { return null; }
        }

        private Document _notesDoc;

        private void OpenNotes()
        {
            _notesDoc = _transcriptDoc;
            var path = DrawingPathOf(_transcriptDoc);
            Ui.ShowNotes(ProjectNotes.LoadFolder(path), _transcript?.Notes ?? "", ProjectNotes.FolderFile(path));
        }

        private void SaveNotes(string folderText, string drawingText)
        {
            if (_notesDoc != _transcriptDoc)
            {
                Ui.AddNote("Ghi chú này được mở cho bản vẽ khác — không lưu. Mở lại Ghi chú cho bản vẽ đang mở.", true);
                return;
            }
            var path = DrawingPathOf(_transcriptDoc);
            if (folderText != null && path != null)
            {
                try { ProjectNotes.SaveFolder(path, folderText); }
                catch (Exception ex)
                {
                    Ui.AddErrorCard($"Không ghi được ghi chú dự án cạnh bản vẽ ({ProjectNotes.FolderFile(path)}): {ex.Message}. Ghi chú riêng bản vẽ vẫn được lưu.", false);
                }
            }
            if (_transcript != null)
            {
                _transcript.Notes = ProjectNotes.Clip(drawingText);
                SaveTranscript();
            }
            UpdateNotesIndicator();
            Ui.ShowToast("Đã lưu ghi chú — Claude nhận kèm từ tin nhắn tiếp theo.", ChatControl.ToastKind.Success);
        }

        private string NotesFor(Document doc) =>
            ProjectNotes.Combine(ProjectNotes.LoadFolder(DrawingPathOf(doc)), doc == _transcriptDoc ? _transcript?.Notes : null);

        private void UpdateNotesIndicator() => Ui.SetNotesIndicator(NotesFor(_transcriptDoc) != null);

        // ================================================================= v0.6: change summary ("Thêm · Sửa · Xóa")

        private ChangeSummary BuildChangeSummary(Autodesk.AutoCAD.DatabaseServices.Database db)
        {
            var snap = _changes.Snapshot();
            _changes.Clear();
            if (snap.Count == 0 || db == null || db.IsDisposed) return null;
            var items = new List<(string, string, string)>();
            var layoutOwner = new Dictionary<Autodesk.AutoCAD.DatabaseServices.ObjectId, bool>();
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (var (kind, h) in snap)
                    {
                        try
                        {
                            var id = Acad.IdFromHandle(db, h, true);
                            if (!(tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, true) is Autodesk.AutoCAD.DatabaseServices.Entity ent)) continue;
                            if (!layoutOwner.TryGetValue(ent.OwnerId, out var isLayout))
                            {
                                isLayout = tr.GetObject(ent.OwnerId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, true) is Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr && btr.IsLayout;
                                layoutOwner[ent.OwnerId] = isLayout;
                            }
                            if (isLayout) items.Add((kind, ent.Layer, h)); // not entities inside block definitions
                        }
                        catch { /* purged or never committed */ }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("change summary failed: " + ex.Message);
                return null;
            }
            var s = ChangeSummary.Build(items);
            return s.IsEmpty ? null : s;
        }

        // ================================================================= v0.6: macros

        /// <summary>%AppData%\ClaudeCAD\macros: per user, survives plugin updates.</summary>
        private static string MacrosRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeCAD", "macros");

        private void SaveMacro(ChatEntry entry, string name)
        {
            if (_macroStore == null) _macroStore = new MacroStore(MacrosRoot);
            if (entry == null || !_turnMacros.TryGetValue(entry, out var calls))
            {
                Ui.AddNote("Không còn dữ liệu thao tác của lượt này để lưu macro (chỉ lưu được các lượt từ khi mở khung chat).", true);
                return;
            }
            var m = new Macro
            {
                Name = name.Trim(),
                Description = UserTextBefore(entry),
                Drawing = _transcript?.DrawingName ?? "",
                Calls = calls.Select(c => new MacroCall { Tool = c.Tool, Params = (Dictionary<string, object>)MacroLogic.DeepCopy(c.Params), ResultHandles = c.ResultHandles.ToList() }).ToList(),
            };
            m.BasePoint = BasePointOf(AcApp.DocumentManager.MdiActiveDocument, MacroLogic.CreatedHandles(m));
            if (_macroStore.Exists(m.Name))
            {
                var answer = System.Windows.MessageBox.Show($"Đã có macro \"{m.Name}\". Thay bằng lượt này?", "ClaudeCAD — macro", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (answer != System.Windows.MessageBoxResult.Yes) { Ui.SetToolStatus("Giữ nguyên macro cũ."); return; }
            }
            try { _macroStore.Save(m); }
            catch (Exception ex) { Ui.AddErrorCard("Không lưu được macro: " + ex.Message, false); return; }
            var external = MacroLogic.ExternalHandles(m);
            Ui.AddNote($"✓ Đã lưu macro \"{m.Name}\" ({MacroLogic.Describe(m)}). Bấm Macro ▸ ▶ Chạy để vẽ lại mà không cần Claude"
                + (m.BasePoint != null ? ", hoặc 📍 Tại điểm để đặt ở chỗ khác." : ".")
                + (external.Count > 0 ? $" Lưu ý: macro dùng {external.Count} đối tượng có sẵn trong bản vẽ này; ở bản vẽ khác các lệnh đó sẽ báo lỗi." : ""));
            Ui.SetMacros(_macroStore.List());
            Ui.ShowToast($"Đã lưu macro \"{m.Name}\".", ChatControl.ToastKind.Success);
        }

        private string UserTextBefore(ChatEntry entry)
        {
            var list = _transcript?.Entries;
            if (list == null) return "";
            int i = list.IndexOf(entry);
            for (int k = (i < 0 ? list.Count : i) - 1; k >= 0; k--)
                if (list[k].Role == "user") return list[k].Text ?? "";
            return "";
        }

        /// <summary>Lower-left corner of the entities among the handles (null if none is a visible entity).</summary>
        private static double[] BasePointOf(Document doc, IEnumerable<string> handles)
        {
            if (doc == null) return null;
            Autodesk.AutoCAD.DatabaseServices.Extents3d? ext = null;
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles)
                {
                    try
                    {
                        var id = Acad.IdFromHandle(doc.Database, h);
                        if (!(tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) is Autodesk.AutoCAD.DatabaseServices.Entity e)) continue;
                        var x = EntityInfo.Extents(e);
                        if (!x.HasValue) continue;
                        if (ext.HasValue) { var a = ext.Value; a.AddExtents(x.Value); ext = a; } else ext = x;
                    }
                    catch { }
                }
                tr.Commit();
            }
            return ext.HasValue ? new[] { Math.Round(ext.Value.MinPoint.X, 3), Math.Round(ext.Value.MinPoint.Y, 3) } : null;
        }

        private static List<string> EntityHandlesOnly(Document doc, IEnumerable<string> handles)
        {
            var list = new List<string>();
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var h in handles)
                {
                    try
                    {
                        var id = Acad.IdFromHandle(doc.Database, h);
                        if (tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) is Autodesk.AutoCAD.DatabaseServices.Entity) list.Add(h);
                    }
                    catch { }
                }
                tr.Commit();
            }
            return list;
        }

        private void RunMacro(string name, bool atPoint)
        {
            if (!RequireActivation()) return;
            if (_busy) { Ui.SetToolStatus("Đợi lượt hiện tại xong rồi chạy macro."); return; }
            if (_macroStore == null) _macroStore = new MacroStore(MacrosRoot);
            Macro m;
            try { m = _macroStore.Load(name); }
            catch (Exception ex) { Ui.AddErrorCard($"Không đọc được macro \"{name}\": {ex.Message}", false); return; }
            if (m == null || m.Calls.Count == 0) { Ui.AddNote($"Macro \"{name}\" không còn hoặc trống.", true); Ui.SetMacros(_macroStore.List()); return; }
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { Ui.SetToolStatus("Không có bản vẽ nào đang mở."); return; }
            if (doc != _transcriptDoc) LoadTranscriptFor(doc);
            if (_transcript?.ReadOnly == true)
            {
                Ui.AddErrorCard("Bản vẽ này đang bật 🔒 Chỉ xem — tắt Chỉ xem rồi chạy macro.", false);
                return;
            }
            if (atPoint)
            {
                if (m.BasePoint == null) return;
                Interaction.PickOnePoint(pt => MainThread.Post(() => StartMacro(m, pt)));
                return;
            }
            StartMacro(m, null);
        }

        /// <summary>Replays a macro as one turn (one undo group) without Claude: handles created by earlier calls are remapped.</summary>
        private async void StartMacro(Macro m, Autodesk.AutoCAD.Geometry.Point3d? at)
        {
            try
            {
                if (_busy) return;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                if (doc != _transcriptDoc) LoadTranscriptFor(doc);
                var entry = new ChatEntry { Role = "user", Text = $"▶ Chạy macro \"{m.Name}\"" + (at.HasValue ? " tại " + Interaction.FormatPoints(new[] { at.Value }) : "") };
                if (RefuseOnVersionTab(doc, null)) return;
                _macroRunning = true;
                _macroCancel = false;
                StartTurn(doc, null, entry, toAgent: false);
                int seq = _turnSeq;
                bool Alive() => _busy && _turnSeq == seq && !_macroCancel;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int ok = 0, failed = 0, n = 0;
                string firstError = null;
                // independent calls run together as one batch_execute (one transaction, one screen update)
                var groups = MacroLogic.PlanBatches(m.Calls, t => _dispatcher.TryGet(t, out var def) && !def.BatchForbidden, ToolDispatcher.MaxBatchCalls);
                var createdByMacro = new HashSet<string>(MacroLogic.CreatedHandles(m), StringComparer.OrdinalIgnoreCase);
                foreach (var group in groups)
                {
                    if (!Alive()) break;
                    // 1: a call that uses an object an earlier (failed) call should have created is skipped,
                    // never sent with the old handle (it could hit the user's own object)
                    var runnable = new List<int>();
                    foreach (var i in group)
                    {
                        var missing = MacroLogic.Unresolved(m.Calls[i].Params, createdByMacro, map);
                        if (missing.Count == 0) { runnable.Add(i); continue; }
                        failed++;
                        if (firstError == null) firstError = $"{m.Calls[i].Tool}: bỏ qua vì đối tượng {string.Join(", ", missing)} của bước trước không vẽ được";
                        AddToolLog(new ToolLogEntry { Tool = m.Calls[i].Tool, State = "error", Error = "bỏ qua: bước tạo đối tượng trước đó bị lỗi" });
                    }
                    var g = runnable;
                    if (g.Count == 0) continue;
                    if (g.Count == 1)
                    {
                        var c = m.Calls[g[0]];
                        var resp = await RunMacroCall(c.Tool, MacroLogic.Remap(c.Params, map) as Dictionary<string, object>, ++n);
                        if (resp.ok) { ok++; MacroLogic.Learn(map, c.ResultHandles, resp.handles); }
                        else { failed++; if (firstError == null) firstError = $"{c.Tool}: {resp.error}"; }
                        continue;
                    }
                    var calls = g.Select(i => (object)new Dictionary<string, object>
                    {
                        ["tool"] = m.Calls[i].Tool,
                        ["params"] = MacroLogic.Remap(m.Calls[i].Params, map),
                    }).ToList();
                    var batch = await RunMacroBatch(calls, ++n);
                    if (batch.error != null)
                    {
                        failed += g.Count;
                        if (firstError == null) firstError = "batch_execute: " + batch.error;
                        continue;
                    }
                    for (int k = 0; k < g.Count && k < batch.results.Count; k++)
                    {
                        var c = m.Calls[g[k]];
                        var r = batch.results[k];
                        if (r.ok) { ok++; MacroLogic.Learn(map, c.ResultHandles, r.handles); }
                        else { failed++; if (firstError == null) firstError = $"{c.Tool}: {r.error}"; }
                    }
                }
                string moved = "";
                if (at.HasValue && m.BasePoint != null && ok > 0 && Alive())
                {
                    var created = MacroLogic.CreatedHandles(m).Select(h => map.TryGetValue(h, out var nh) ? nh : null).Where(h => h != null).ToList();
                    var ents = EntityHandlesOnly(doc, created);
                    if (ents.Count > 0)
                    {
                        var disp = new List<object> { at.Value.X - m.BasePoint[0], at.Value.Y - m.BasePoint[1] };
                        var mv = await RunMacroCall("move_entities", new Dictionary<string, object> { ["handles"] = ents.Cast<object>().ToList(), ["displacement"] = disp }, ++n);
                        moved = mv.ok ? ", đã dời tới " + Interaction.FormatPoints(new[] { at.Value }) : ", không dời được: " + mv.error;
                    }
                }
                var text = $"Đã chạy macro **{m.Name}**: {ok}/{m.Calls.Count} lệnh OK"
                    + (failed > 0 ? $", {failed} lỗi (lỗi đầu: {firstError})" : "")
                    + moved
                    + (_macroCancel ? " — đã dừng giữa chừng" : "")
                    + ". Không dùng Claude, không tốn token. Gõ U một lần nếu cậu muốn hoàn tác cả macro.";
                if (_busy && _turnSeq == seq)
                {
                    lock (_gate) { _assistantText.Append(text); }
                    Ui.AppendAssistant(text);
                    EndTurn(null, false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("macro replay failed", ex);
                EndTurn("Macro bị lỗi: " + ex.Message, false);
            }
            finally
            {
                _macroRunning = false;
                _macroCancel = false;
            }
        }

        /// <summary>Runs several macro calls as one batch_execute; per-call results in order (error = the whole batch failed).</summary>
        private async Task<(List<(bool ok, List<string> handles, string error)> results, string error)> RunMacroBatch(List<object> calls, int n)
        {
            AddToolLog(new ToolLogEntry { Tool = "batch_execute", State = "running" });
            var sw = Stopwatch.StartNew();
            var resp = await _dispatcher.HandleAsync(new Dictionary<string, object> { ["id"] = "macro-" + n, ["tool"] = "batch_execute", ["params"] = new Dictionary<string, object> { ["calls"] = calls } });
            var log = new ToolLogEntry { Tool = "batch_execute", Ms = sw.ElapsedMilliseconds };
            var list = new List<(bool, List<string>, string)>();
            if (!(resp.TryGetValue("ok", out var okv) && okv is bool b && b))
            {
                var err = resp.TryGetValue("error", out var ev) ? ev as Dictionary<string, object> : null;
                var msg = err != null && err.TryGetValue("message", out var mv) ? mv as string : "lỗi";
                log.State = "error";
                log.Error = msg?.Length > 300 ? msg.Substring(0, 300) : msg;
                AddToolLog(log);
                return (list, msg ?? "lỗi");
            }
            var result = resp.TryGetValue("result", out var rv) ? rv as Dictionary<string, object> : null;
            var items = result != null && result.TryGetValue("results", out var rl) ? rl as List<object> : null;
            int okCount = 0, all = 0;
            foreach (var o in items ?? new List<object>())
            {
                var d = o as Dictionary<string, object>;
                bool callOk = d != null && d.TryGetValue("ok", out var co) && co is bool cb && cb;
                var hs = callOk ? MacroLogic.ResultHandles(d.TryGetValue("result", out var cr) ? cr : null) : new List<string>();
                string error = null;
                if (!callOk && d != null && d.TryGetValue("error", out var ce) && ce is Dictionary<string, object> ced && ced.TryGetValue("message", out var cm)) error = cm as string;
                if (callOk) { okCount++; lock (_gate) foreach (var h in hs) _turnHandles.Add(h); }
                all++;
                list.Add((callOk, hs, error ?? (callOk ? null : "lỗi")));
            }
            log.State = okCount == all ? "done" : "error";
            log.Summary = $"{okCount}/{all} lệnh OK";
            if (okCount != all) log.Error = $"{all - okCount} lệnh lỗi";
            AddToolLog(log);
            return (list, null);
        }

        private async Task<(bool ok, List<string> handles, string error)> RunMacroCall(string tool, Dictionary<string, object> p, int n)
        {
            var running = new ToolLogEntry { Tool = tool, State = "running" };
            AddToolLog(running);
            var sw = Stopwatch.StartNew();
            var resp = await _dispatcher.HandleAsync(new Dictionary<string, object> { ["id"] = "macro-" + n, ["tool"] = tool, ["params"] = p ?? new Dictionary<string, object>() });
            bool ok = resp.TryGetValue("ok", out var okv) && okv is bool b && b;
            var log = new ToolLogEntry { Tool = tool, State = ok ? "done" : "error", Ms = sw.ElapsedMilliseconds };
            var handles = new List<string>();
            string error = null;
            if (ok)
            {
                handles = MacroLogic.ResultHandles(resp.TryGetValue("result", out var r) ? r : null);
                lock (_gate) foreach (var h in handles) _turnHandles.Add(h);
                if (handles.Count > 0) log.Summary = handles.Count + " đối tượng";
            }
            else
            {
                var err = resp.TryGetValue("error", out var ev) ? ev as Dictionary<string, object> : null;
                error = err != null && err.TryGetValue("message", out var mv) ? mv as string : "lỗi";
                log.Error = error?.Length > 300 ? error.Substring(0, 300) : error;
            }
            AddToolLog(log);
            return (ok, handles, error);
        }

        private void AddToolLog(ToolLogEntry entry)
        {
            lock (_gate)
            {
                var a = _assistant;
                if (a != null)
                {
                    int idx = entry.State == "running" ? -1 : a.Tools.FindIndex(x => x.State == "running" && x.Tool == entry.Tool);
                    if (idx >= 0) a.Tools[idx] = entry; else a.Tools.Add(entry);
                }
            }
            Ui.UpdateTool(entry);
        }

        private void DeleteMacro(string name)
        {
            var answer = System.Windows.MessageBox.Show($"Xóa macro \"{name}\"?", "ClaudeCAD — macro", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (answer != System.Windows.MessageBoxResult.Yes) return;
            if (_macroStore == null) _macroStore = new MacroStore(MacrosRoot);
            _macroStore.Delete(name);
            Ui.SetMacros(_macroStore.List());
            Ui.SetToolStatus($"Đã xóa macro \"{name}\".");
        }

        private void OpenMacrosFolder()
        {
            Directory.CreateDirectory(MacrosRoot);
            Process.Start("explorer.exe", "\"" + MacrosRoot + "\"");
        }

        // ================================================================= v0.6: templates, skill drafts, plans

        private void SaveTemplate(string title, string text)
        {
            try
            {
                var shipped = QuickTemplates.LoadAll(PluginPaths.TemplatesFile, null).Select(x => x.Name);
                var t = QuickTemplates.SaveUser(QuickTemplates.UserFile, title, text, false, shipped);
                Ui.SetTemplates(QuickTemplates.LoadAll(PluginPaths.TemplatesFile, QuickTemplates.UserFile));
                Ui.ShowToast($"Đã lưu mẫu lệnh /{t.Name} — gõ / để dùng lại.", ChatControl.ToastKind.Success);
            }
            catch (Exception ex)
            {
                Ui.AddErrorCard("Không lưu được mẫu lệnh: " + ex.Message, false);
            }
        }

        private void CreateSkillFromChat()
        {
            if (_busy) { Ui.SetToolStatus("Đợi lượt hiện tại xong đã."); return; }
            if (_transcript == null || _transcript.Entries.Count(e => e.Role == "user") == 0)
            {
                Ui.AddNote("Chưa có cuộc chat nào để tạo skill. Làm vài yêu cầu, chỉnh chuẩn theo ý cậu, rồi bấm lại.", true);
                return;
            }
            MainThread.Post(() => Send(new ComposedMessage
            {
                Text = "Tạo skill từ cuộc chat này: soạn một SKILL.md ghi lại các chuẩn, quy ước và thông số tớ đã dùng hoặc đã chỉnh trong cuộc chat " +
                       "(layer, màu, nét, chiều cao chữ, kiểu kích thước, ký hiệu, trình tự làm). Gọi propose_skill để tớ xem lại; không vẽ gì.",
            }, false));
        }

        private static string SkillDraftsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "skill-drafts");

        private void ShowSkillDraft(string name, string description, string content)
        {
            var slug = SkillStore.Slugify(name);
            if (slug.Length == 0) slug = "skill-moi";
            if (string.IsNullOrWhiteSpace(content)) { Ui.AddNote("Claude gửi bản nháp skill trống.", true); return; }
            var dir = Path.Combine(SkillDraftsRoot, slug);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "SKILL.md");
            File.WriteAllText(file, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
            Ui.AddSkillDraftCard(slug, description, file);
            _transcript?.Entries.Add(new ChatEntry { Role = "note", Text = $"Bản nháp skill {slug}: {file}" });
            SaveTranscript();
        }

        private static void OpenFile(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); }
            catch { Process.Start("notepad.exe", "\"" + file + "\""); }
        }

        private void AnswerPlan(string id, string kind)
        {
            if (_busy) { _pendingPlan = (id, kind); return; } // the card arrived before the turn ended
            switch (kind)
            {
                case "go":
                    MainThread.Post(() => Send(new ComposedMessage { Text = "Làm theo kế hoạch." }, false, null, "approved"));
                    break;
                case "edit":
                    Ui.SetInputText("Sửa kế hoạch: ");
                    Ui.SetToolStatus("Gõ điều cần đổi trong kế hoạch rồi gửi — Claude lập lại kế hoạch.");
                    break;
                default:
                    Ui.AddNote("Đã bỏ kế hoạch — Claude không làm gì thêm.");
                    _transcript?.Entries.Add(new ChatEntry { Role = "note", Text = "Người dùng bỏ kế hoạch." });
                    SaveTranscript();
                    break;
            }
        }

        // ================================================================= v0.9.1: sign-in and first start

        private bool _loginRunning;

        /// <summary>"Đăng nhập Claude": the bundled Claude Code opens the browser; afterwards the assistant restarts with the new credentials.</summary>
        private void Login()
        {
            if (_loginRunning) { Ui.SetToolStatus("Cửa sổ đăng nhập Claude đang mở — làm theo hướng dẫn trên trình duyệt."); return; }
            bool started;
            try
            {
                started = ClaudeAuth.StartLogin(status => Ui.Enqueue(() =>
                {
                    _loginRunning = false;
                    if (status.LoggedIn == true)
                    {
                        if (ClaudeAuth.ApiKeyInUse)
                            Ui.AddNote("✓ Đã đăng nhập Claude, nhưng plugin vẫn dùng API key (được ưu tiên hơn tài khoản). Muốn dùng tài khoản Claude: ⚙ ▸ Dùng API key ▸ Xóa key đã lưu.", true);
                        else
                            Ui.AddNote("✓ Đã đăng nhập Claude. Trợ lý khởi động lại với tài khoản này — cậu gõ yêu cầu được rồi.");
                        Ui.ShowToast("Đã đăng nhập Claude.", ChatControl.ToastKind.Success);
                        RestartSidecarQuietly();
                    }
                    else Ui.AddLoginCard("Chưa đăng nhập xong (" + status.Label + "). Bấm lại để thử lần nữa.");
                }));
            }
            catch (Exception ex)
            {
                Logger.Error("login failed to start", ex);
                started = false;
            }
            if (!started)
            {
                Ui.AddErrorCard("Không tìm thấy Claude Code đi kèm plugin (thư mục agent\\node_modules). Cài lại ClaudeCAD bằng Setup.exe, hoặc đặt biến môi trường ANTHROPIC_API_KEY.", false);
                return;
            }
            _loginRunning = true;
            Ui.AddNote("Đang mở cửa sổ đăng nhập Claude: trình duyệt sẽ mở trang đăng nhập — chọn tài khoản Pro/Max, đồng ý, rồi quay lại AutoCAD. Cửa sổ đen tự đóng khi xong.");
        }

        // ================================================================= v0.9.2: usage limit

        private static string LimitLabel(string kind) =>
            kind == "five_hour" ? "hạn mức 5 giờ" : kind != null && kind.StartsWith("seven_day") ? "hạn mức tuần" : "hạn mức sử dụng";

        /// <summary>Limit reached: one card per turn in the conversation, plus a toast.</summary>
        private int _limitHitTurn = -1;

        private void ShowLimitCard(string code, int turn)
        {
            if (_limitCardTurn == turn) return; // one card per turn (agent_error + rate_limit event)
            _limitCardTurn = turn;
            if (code == "billing_error")
            {
                Ui.AddLimitCard("Tài khoản Claude hết hạn mức thanh toán", "Gói đã hết hạn hoặc thanh toán bị lỗi. Kiểm tra trên claude.ai (Settings ▸ Billing), hoặc dùng API key.", null, false, false);
                Ui.ShowToast("Tài khoản Claude gặp lỗi thanh toán / hết hạn gói.", ChatControl.ToastKind.Error, 8);
                return;
            }
            var label = LimitLabel(_rateKind);
            var when = _rateResetsAt.HasValue ? $" Được dùng lại lúc {_rateResetsAt.Value:HH:mm dd/MM}." : " Đợi một lúc rồi thử lại.";
            var detail = $"Gói Claude đã dùng hết {label}.{when} Yêu cầu vừa rồi chưa được thực hiện" +
                         (_lastPayload != null ? " — bấm \"Tự gửi lại\" để plugin gửi lại khi có hạn mức." : ".") +
                         " Muốn dùng ngay: dùng API key Anthropic (trả theo lượng dùng).";
            Ui.AddLimitCard("Đã chạm giới hạn sử dụng Claude", detail, _rateResetsAt, _lastPayload != null && _rateResetsAt.HasValue, _autoRetryOn);
            Ui.ShowToast($"Hết {label}" + (_rateResetsAt.HasValue ? $" — dùng lại lúc {_rateResetsAt.Value:HH:mm}" : ""), ChatControl.ToastKind.Error, 8);
            _transcript?.Entries.Add(new ChatEntry { Role = "error", Text = $"Hết {label}.{when}" });
        }

        /// <summary>"Tự gửi lại lúc …": resend the last request about a minute after the limit resets.</summary>
        private void SetAutoRetry(bool on)
        {
            _autoRetryTimer?.Dispose();
            _autoRetryTimer = null;
            _autoRetryOn = on && _rateResetsAt.HasValue && _lastPayload != null;
            if (!_autoRetryOn)
            {
                Ui.ShowToast(on ? "Chưa biết giờ làm mới hạn mức (hoặc không còn yêu cầu để gửi lại) — gửi lại tay khi có hạn mức." : "Đã hủy tự gửi lại.", ChatControl.ToastKind.Info, 6);
                return;
            }
            var due = _rateResetsAt.Value.AddSeconds(60) - DateTime.Now;
            if (due < TimeSpan.FromSeconds(5)) due = TimeSpan.FromSeconds(5);
            _autoRetryTimer = new System.Threading.Timer(_ => Ui.Enqueue(() =>
            {
                if (!_autoRetryOn) return;
                _autoRetryOn = false;
                if (_busy || _lastPayload == null) { Ui.ShowToast("Hạn mức đã làm mới — gửi lại yêu cầu khi cậu sẵn sàng.", ChatControl.ToastKind.Info, 8); return; }
                Ui.ShowToast("Hạn mức đã được làm mới — đang gửi lại yêu cầu vừa rồi.", ChatControl.ToastKind.Success, 6);
                Retry();
            }), null, due, System.Threading.Timeout.InfiniteTimeSpan);
            Ui.ShowToast($"Sẽ tự gửi lại lúc {_rateResetsAt.Value.AddSeconds(60):HH:mm} (cứ để AutoCAD mở).", ChatControl.ToastKind.Success, 5);
        }

        /// <summary>Turns "Tự gửi lại" off; true when it was on.</summary>
        private bool CancelAutoRetry()
        {
            _autoRetryTimer?.Dispose();
            _autoRetryTimer = null;
            if (!_autoRetryOn) return false;
            _autoRetryOn = false;
            return true;
        }

        // ================================================================= v0.9.2: account, bug report

        private void RefreshAccountStatus()
        {
            Ui.SetAccountStatus("Đang kiểm tra...");
            ClaudeAuth.StatusAsync().ContinueWith(t =>
            {
                var a = t.IsFaulted ? new AuthStatus { Error = t.Exception?.GetBaseException().Message } : t.Result;
                var extra = a.Method == "api_key_saved" ? " (" + ApiKeyStore.Mask(ApiKeyStore.Load()) + ")" : "";
                Ui.SetAccountStatus("Đang dùng: " + a.Label + extra + (string.IsNullOrEmpty(_readyInfo) ? "" : " · " + _readyInfo));
            });
        }

        /// <summary>"Đổi tài khoản": sign out, then the normal sign-in (browser) for another account.</summary>
        private void SwitchAccount()
        {
            if (ApiKeyStore.HasKey)
            {
                Ui.AddNote("Đang dùng API key đã lưu — bấm ⚙ ▸ Dùng API key ▸ Xóa key đã lưu nếu cậu muốn dùng tài khoản Claude.", true);
                return;
            }
            Ui.AddNote("Đang đăng xuất tài khoản hiện tại...");
            ClaudeAuth.LogoutAsync().ContinueWith(t => Ui.Enqueue(() =>
            {
                if (!t.Result) Ui.AddNote("Không đăng xuất được bằng Claude Code đi kèm; vẫn mở đăng nhập — chọn tài khoản khác trên trình duyệt.", true);
                Login();
            }));
        }

        private void Logout()
        {
            ClaudeAuth.LogoutAsync().ContinueWith(t => Ui.Enqueue(() =>
            {
                if (t.Result)
                {
                    Ui.AddNote(ClaudeAuth.ApiKeyInUse
                        ? "Đã đăng xuất tài khoản Claude trên máy này. Trợ lý vẫn chạy bằng API key đã lưu (⚙ ▸ Dùng API key ▸ Xóa key đã lưu để ngừng)."
                        : "Đã đăng xuất tài khoản Claude trên máy này. Bấm ⚙ ▸ Đăng nhập Claude để đăng nhập lại.");
                    RestartSidecarQuietly();
                }
                else Ui.AddErrorCard("Không đăng xuất được (không thấy Claude Code đi kèm plugin hoặc lệnh bị lỗi). Xóa tay file %USERPROFILE%\\.claude\\.credentials.json nếu cần.", false);
            }));
        }

        private void SaveApiKey(string key)
        {
            try { ApiKeyStore.Save(key); }
            catch (Exception ex) { Ui.AddErrorCard("Không lưu được API key: " + ex.Message, false); return; }
            Ui.AddNote($"✓ Đã lưu API key {ApiKeyStore.Mask(key)} (mã hóa cho tài khoản Windows này). Trợ lý khởi động lại và dùng key này thay cho tài khoản Claude.");
            Ui.ShowToast("Đã lưu API key — trợ lý đang khởi động lại.", ChatControl.ToastKind.Success);
            RestartSidecarQuietly();
        }

        private void ClearApiKey()
        {
            if (!ApiKeyStore.HasKey) { Ui.SetToolStatus("Chưa có API key nào được lưu."); return; }
            ApiKeyStore.Clear();
            Ui.AddNote("Đã xóa API key đã lưu. Trợ lý khởi động lại và dùng tài khoản Claude đã đăng nhập" +
                       (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) ? "." : " — lưu ý: biến môi trường ANTHROPIC_API_KEY vẫn đang được đặt và vẫn được ưu tiên."));
            RestartSidecarQuietly();
        }

        /// <summary>"Gửi báo cáo lỗi": zip on the Desktop, then Explorer shows it.</summary>
        private void ReportBug()
        {
            Ui.SetToolStatus("Đang gom thông tin báo cáo lỗi...");
            ClaudeAuth.StatusAsync().ContinueWith(t =>
            {
                var auth = t.IsFaulted ? new AuthStatus { Error = t.Exception?.GetBaseException().Message } : t.Result;
                MainThread.Post(() =>
                {
                    string info;
                    try { info = DiagnosticsBundle.Info(this, auth); }
                    catch (Exception ex) { info = "Không lấy được thông tin: " + ex.Message; }
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var zip = DiagnosticsBundle.Create(info);
                            Ui.AddNote($"✓ Đã tạo báo cáo lỗi: {zip}\nGửi file này cho người hỗ trợ (Zalo, email...). File gồm thông tin máy, log 3 ngày gần đây, kết quả tự kiểm tra và log cài đặt — không có lịch sử chat, bản vẽ hay API key. Muốn kèm kết quả tự kiểm tra mới nhất: gõ CLAUDECHATSELFTEST trước rồi bấm lại.");
                            Ui.ShowToast("Đã tạo báo cáo lỗi trên Desktop.", ChatControl.ToastKind.Success, 6);
                            try { Process.Start("explorer.exe", "/select,\"" + zip + "\""); } catch { }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("bug report failed", ex);
                            Ui.AddErrorCard("Không tạo được báo cáo lỗi: " + ex.Message + ". Gõ CLAUDECHATLOG để mở thư mục log và gửi tay.", false);
                        }
                    });
                });
            });
        }

        /// <summary>
        /// The "Bắt đầu" card: node, assistant, Claude sign-in, skills — with Login / Self-test buttons.
        /// autoLogin (first AutoCAD start after installing): when this Windows user is not signed in yet, the
        /// browser sign-in opens by itself, so the user only confirms there.
        /// </summary>
        public void ShowWelcome() => ShowWelcome(false);

        public void ShowWelcome(bool autoLogin)
        {
            ShowPalette();
            if (!Activation.IsActivated) { ShowActivationCard(); return; } // sign-in comes after the key
            ClaudeAuth.StatusAsync().ContinueWith(t =>
            {
                var auth = t.IsFaulted ? new AuthStatus { Error = t.Exception?.GetBaseException().Message } : t.Result;
                var node = PluginPaths.NodeExecutable;
                var checks = new List<(string, bool?, string)>
                {
                    ("Node.js", node != null, node == null ? "không thấy — cài lại bằng Setup.exe" : (node.IndexOf("ClaudeCAD.bundle", StringComparison.OrdinalIgnoreCase) >= 0 ? "đi kèm plugin" : node)),
                    ("Trợ lý (sidecar)", PluginPaths.AgentEntry != null, PluginPaths.AgentEntry == null ? "không thấy thư mục agent" : "sẵn sàng"),
                    ("Kích hoạt", true, "mã máy " + Activation.MachineCode),
                    ("Tài khoản Claude", auth.LoggedIn, auth.Label),
                    ("Skill", _skillStore != null, _skillStore == null ? "" : $"{_skillStore.Scan().Count} skill trong plugin"),
                };
                Ui.AddWelcomeCard(checks, auth.LoggedIn != true);
                if (autoLogin && auth.LoggedIn == false) Ui.Enqueue(Login); // not when the state is unknown
            });
        }

        // ================================================================= v0.9.4: activation

        private bool _activationCardShown;
        private bool _activating;
        private bool _autoRequested; // the automatic request is sent once per AutoCAD session at most

        /// <summary>False (and the activation card is shown) while this machine has no valid licence.</summary>
        private bool RequireActivation()
        {
            if (Activation.IsActivated) return true;
            ShowActivationCard(force: true); // the palette reuses the live card (brings it into view)
            Ui.SetToolStatus("Cần kích hoạt ClaudeCAD trên máy này trước. " + Activation.Contact + ".");
            return false;
        }

        /// <summary>
        /// The activation card. The first time per day on a machine without a licence the request (machine code +
        /// device name) is emailed to the owner by itself — like the installer does — so a copied or .zip install
        /// still reaches the owner.
        /// </summary>
        private void ShowActivationCard(bool force = false)
        {
            if (_activationCardShown && !force) return;
            _activationCardShown = true;
            bool auto = !_autoRequested && Activation.ShouldAutoRequest && Activation.ServiceUrl != null;
            if (auto) _autoRequested = true;
            Ui.AddActivationCard(Activation.MachineCode, Activation.DeviceName, Activation.Contact,
                auto ? "Đang gửi yêu cầu kích hoạt cho chủ plugin..." : "Bấm \"Gửi yêu cầu kích hoạt\" để chủ plugin nhận mã máy qua email, hoặc nhắn mã máy qua Zalo.");
            if (auto) RequestActivation();
        }

        private void RequestActivation()
        {
            Activation.RequestAsync().ContinueWith(t =>
            {
                var (ok, msg) = t.IsFaulted ? (false, "Không gửi được yêu cầu: " + t.Exception?.GetBaseException().Message) : t.Result;
                Ui.SetActivationStatus(msg, !ok);
                if (ok) Ui.ShowToast("Đã gửi yêu cầu kích hoạt", ChatControl.ToastKind.Info, 5);
            });
        }

        private void SubmitActivation(string input)
        {
            if (_activating) return;
            _activating = true;
            Activation.ActivateAsync(input).ContinueWith(t =>
            {
                _activating = false;
                var (ok, msg) = t.IsFaulted ? (false, "Kích hoạt lỗi: " + t.Exception?.GetBaseException().Message) : t.Result;
                Ui.SetActivationStatus(msg, !ok, ok);
                if (!ok) return;
                Ui.ShowToast("Đã kích hoạt ClaudeCAD", ChatControl.ToastKind.Success, 6);
                Ui.Enqueue(() =>
                {
                    _sidecar.EnsureStarted(); // not started while the machine had no licence
                    ShowWelcome(autoLogin: true); // next step: Claude sign-in (opens by itself when needed)
                });
            });
        }

        // ================================================================= v0.7: version history

        private bool IsVersionDoc(Document d) => d != null && _versionDocs.ContainsKey(d);

        private void RefreshVersions()
        {
            try
            {
                var doc = _transcriptDoc;
                Ui.SetVersions(doc == null ? new List<VersionInfo>() : History.Versions(KeyFor(doc)).List());
            }
            catch (Exception ex) { Logger.Warn("versions list failed: " + ex.Message); }
        }

        private static string VersionBannerText(Document original, VersionInfo v)
        {
            string name = "";
            try { name = Path.GetFileName(original?.Name ?? ""); } catch { }
            return $"Đang xem phiên bản lúc {v.Time:HH:mm dd/MM} của {name} — trước lượt \"{v.Label}\". Chỉ xem: sửa ở đây không ảnh hưởng bản vẽ thật.";
        }

        /// <summary>Opens a version copy read-only in its own tab; the real drawing stays as it is.</summary>
        private void OpenVersion(string id)
        {
            var original = _transcriptDoc ?? AcApp.DocumentManager.MdiActiveDocument;
            if (original == null) return;
            if (_busy) { Ui.SetToolStatus("Đợi lượt hiện tại xong rồi mở phiên bản."); return; }
            var store = History.Versions(KeyFor(original));
            var v = store.List().FirstOrDefault(x => x.Id == id);
            if (v == null) { Ui.AddNote("Phiên bản này không còn (đã bị xóa khi vượt quá 10 bản).", true); RefreshVersions(); return; }
            var file = store.FileOf(v);
            // already open → just switch to it
            foreach (var kv in _versionDocs)
                if (kv.Value.info.Id == id && !kv.Key.IsDisposed) { AcApp.DocumentManager.MdiActiveDocument = kv.Key; return; }
            _openingVersion = (original, v);
            try
            {
                var vdoc = DocumentCollectionExtension.Open(AcApp.DocumentManager, file, true);
                if (vdoc != null && !_versionDocs.ContainsKey(vdoc)) _versionDocs[vdoc] = (original, v);
                if (vdoc != null) Ui.ShowVersionBanner(VersionBannerText(original, v));
            }
            catch (Exception ex)
            {
                Logger.Error("open version failed", ex);
                Ui.AddErrorCard("Không mở được phiên bản: " + ex.Message, false);
            }
            finally
            {
                _openingVersion = null;
            }
        }

        /// <summary>"Đóng — về bản hiện tại": closes every version tab (discarding) and returns to the drawing.</summary>
        private void CloseVersions()
        {
            Document back = null;
            foreach (var kv in _versionDocs.ToList())
            {
                back = back ?? kv.Value.original;
                try { if (!kv.Key.IsDisposed) kv.Key.CloseAndDiscard(); }
                catch (Exception ex) { Logger.Warn("closing version tab failed: " + ex.Message); }
            }
            _versionDocs.Clear();
            Ui.HideVersionBanner();
            try { if (back != null && !back.IsDisposed) AcApp.DocumentManager.MdiActiveDocument = back; } catch { }
        }

        private void OpenVersionsFolder()
        {
            var doc = _transcriptDoc;
            var dir = doc == null ? DrawingHistory.VersionsRoot : History.Versions(KeyFor(doc)).Dir;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", "\"" + dir + "\"");
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

        /// <summary>"Chỉ xem" of the drawing whose chat is shown (the turn's drawing).</summary>
        public bool ReadOnly => _transcript?.ReadOnly == true;

        public void RecordCall(string tool, Dictionary<string, object> parameters, object result) => _recorder.Record(tool, parameters, result);

        /// <summary>First change of a chat turn: keep a copy of the drawing as it was (every 30 min max).</summary>
        public void BeforeWrite(Document doc)
        {
            if (_versionChecked || !_settings.Versions || doc == null) return;
            _versionChecked = true;
            try
            {
                var v = History.MaybeSaveVersion(doc, KeyFor(doc), _turnLabel);
                if (v != null)
                {
                    Ui.ShowToast($"Đã lưu phiên bản {v.Time:HH:mm} trước khi sửa — xem bằng nút Phiên bản.", ChatControl.ToastKind.Success);
                    if (doc == _transcriptDoc) RefreshVersions();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("version copy failed", ex);
                Ui.ShowToast("Không lưu được phiên bản trước khi sửa: " + ex.Message, ChatControl.ToastKind.Warning, 8); // the status line is cleared when the turn ends
            }
        }

        // ================================================================= IPC events

        private void OnConnected()
        {
            if (_wasDisconnected) Ui.ShowToast("Đã kết nối lại với trợ lý.", ChatControl.ToastKind.Success);
            _wasDisconnected = false;
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
                        string tid = msg.TryGetValue("turn_id", out var tv0) ? tv0 as string : null;
                        if (_macroRunning) { Logger.Info("turn_end ignored: a macro is running"); break; }
                        if (tid != null && tid != "t-" + _turnSeq.ToString(CultureInfo.InvariantCulture)) { Logger.Info($"late turn_end {tid} ignored"); break; }
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
                        var code = msg.TryGetValue("code", out var cv0) ? cv0 as string : null;
                        if (code == "rate_limit" || code == "billing_error")
                        {
                            _limitHitTurn = _turnSeq;
                            int turn = _turnSeq;
                            // the SDK's rate_limit event (with the reset time) often comes right after this error:
                            // wait a moment so the one card shows the time and offers "Tự gửi lại"
                            if (code == "rate_limit") Task.Delay(700).ContinueWith(_ => Ui.Enqueue(() => ShowLimitCard(code, turn)));
                            else Ui.Enqueue(() => ShowLimitCard(code, turn));
                            if (_busy) _lastError = null; // the limit card replaces the generic error card
                            break;
                        }
                        if (code == "authentication_failed" || code == "oauth_org_not_allowed")
                        {
                            Ui.AddLoginCard(m); // one button instead of terminal instructions
                            if (_busy) _lastError = null;
                            break;
                        }
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
                        string label = LimitLabel(kind);
                        _rateText = status == "allowed" && util < 0.5 ? "" :
                            $" · {label}{(util >= 0 ? $" {util * 100:0}%" : "")}{(status == "rejected" ? " — đã hết" : "")}{(reset.Length > 0 ? $", làm mới {reset}" : "")}";
                        Ui.SetUsage(_usageText + _rateText);
                        _rateStatus = status ?? "";
                        _rateUtil = util;
                        _rateKind = kind ?? "";
                        _rateResetsAt = msg.TryGetValue("resets_at", out var rv2) && rv2 is double rd2 ? DateTimeOffset.FromUnixTimeSeconds((long)rd2).LocalDateTime : (DateTime?)null;
                        if (status == "rejected")
                        {
                            if (_busy) _limitHitTurn = _turnSeq;
                            int turn = _turnSeq;
                            Ui.Enqueue(() => ShowLimitCard(null, turn));
                        }
                        else if (status == "allowed_warning" || util >= 0.8)
                        {
                            // once per limit window: "sắp hết hạn mức"
                            var key = (kind ?? "") + "|" + reset;
                            if (key != _rateWarnedKey)
                            {
                                _rateWarnedKey = key;
                                Ui.ShowToast($"Sắp hết {label}: đã dùng {(util >= 0 ? $"{util * 100:0}%" : "phần lớn")}{(reset.Length > 0 ? $", làm mới lúc {reset}" : "")}. Ưu tiên việc quan trọng, hoặc chọn model nhẹ hơn trong ⚙.", ChatControl.ToastKind.Warning, 9);
                            }
                        }
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
                        if (!string.IsNullOrEmpty(info)) _readyInfo = info;
                        Ui.SetConnection("Đã kết nối" + (string.IsNullOrEmpty(info) ? "" : " · " + info), true);
                        break;
                    }
                case "skills_reloaded":
                    {
                        var names = msg.TryGetValue("skills", out var sk) && sk is List<object> sl ? sl.OfType<string>().ToList() : new List<string>();
                        Logger.Info("sidecar skills: " + string.Join(", ", names));
                        Ui.SetToolStatus($"Claude sẽ dùng {names.Count} skill: {string.Join(", ", names)}");
                        break;
                    }
                case "session_reset":
                    Ui.SetConnection("Đã kết nối · phiên mới", true);
                    break;
                case "suggestions":
                    {
                        var items = msg.TryGetValue("items", out var iv) && iv is List<object> il ? il.OfType<string>().ToList() : new List<string>();
                        string source = msg.TryGetValue("source", out var sv) ? sv as string : "tool";
                        if (!_settings.Suggestions || items.Count == 0) break;
                        lock (_gate)
                        {
                            if (_busy)
                            {
                                // shown when the turn ends; a late SDK prediction of an older turn is dropped
                                if (source != "sdk") _turnSuggestions.AddRange(items);
                                break;
                            }
                        }
                        Ui.AddSuggestions(items);
                        break;
                    }
                case "plan_request":
                    {
                        string id = msg.TryGetValue("id", out var iv) ? iv as string : "";
                        string summary = msg.TryGetValue("summary", out var sv) ? sv as string : "";
                        var steps = msg.TryGetValue("steps", out var stv) && stv is List<object> sl ? sl.OfType<string>().ToList() : new List<string>();
                        Ui.AddPlanCard(id, summary, steps);
                        Ui.Enqueue(() => _transcript?.Entries.Add(new ChatEntry { Role = "note", Text = "Kế hoạch: " + summary + (steps.Count > 0 ? "\n" + string.Join("\n", steps.Select((x, i) => $"{i + 1}. {x}")) : "") }));
                        break;
                    }
                case "skill_draft":
                    {
                        string name = msg.TryGetValue("name", out var nv) ? nv as string : "";
                        string description = msg.TryGetValue("description", out var dv) ? dv as string : "";
                        string content = msg.TryGetValue("content", out var cv) ? cv as string : "";
                        Ui.Enqueue(() => Guard("skill draft", () => ShowSkillDraft(name, description, content)));
                        break;
                    }
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

        private volatile bool _restartExpected;

        /// <summary>Restart asked by the user (sign-in, API key, CLAUDECHATRESTART): no "Mất kết nối" warning for it.</summary>
        private void RestartSidecarQuietly()
        {
            _restartExpected = true;
            _sidecar.Restart();
        }

        private void OnDisconnected(string reason)
        {
            bool expected = _restartExpected;
            _restartExpected = false;
            if (!_wasDisconnected && !expected) Ui.ShowToast("Mất kết nối với trợ lý — đang thử kết nối lại...", ChatControl.ToastKind.Warning, 6);
            _wasDisconnected = true;
            if (_busy && !_macroRunning) EndTurn("Mất kết nối với trợ lý giữa lượt; lượt này đã dừng. Lịch sử chat vẫn giữ nguyên.", true);
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
            if (_versionDocs.Remove(e.Document) && _versionDocs.Count == 0) Ui.HideVersionBanner();
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
            // an old version opened from the palette: no chat of its own
            if (_openingVersion.HasValue && e.Document != null && !_versionDocs.ContainsKey(e.Document) && e.Document != _transcriptDoc)
            {
                _versionDocs[e.Document] = _openingVersion.Value;
                _openingVersion = null;
            }
            bool isVersion = e.Document != null && _versionDocs.ContainsKey(e.Document);
            if (isVersion) { var vd = _versionDocs[e.Document]; Ui.ShowVersionBanner(VersionBannerText(vd.original, vd.info)); }
            else if (_versionDocs.Count > 0) Ui.HideVersionBanner();
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
            if (isVersion || !_started || e.Document == _transcriptDoc) return; // a version tab has no chat of its own
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
                DrawingHistory.Instance.ObjectChanged -= OnDbChanged;
                Notifier.Dispose();
                _skillWatcher?.Dispose();
                _skillDebounce?.Dispose();
                _autoRetryTimer?.Dispose();
            }
            catch { }
            _sidecar.Dispose();
            _ipc.Dispose();
        }
    }
}
