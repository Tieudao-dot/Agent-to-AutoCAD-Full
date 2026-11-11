using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>What the user is about to send.</summary>
    public sealed class ComposedMessage
    {
        public string Text;
        public List<ImageAttachment> Images = new List<ImageAttachment>();
        public bool IncludeSelection;
        /// <summary>Layers unticked under the selection chip: their objects are not sent.</summary>
        public List<string> ExcludedLayers = new List<string>();
        /// <summary>Layers chosen in the layer picker ("Layer: A-WALL, A-DOOR").</summary>
        public List<string> Layers = new List<string>();
        public Dictionary<string, int> LayerCounts = new Dictionary<string, int>();
    }

    /// <summary>
    /// WPF chat surface hosted in the ClaudeCAD PaletteSet (code-only, no XAML).
    /// All public methods are thread-safe (they marshal to the Dispatcher).
    /// </summary>
    public sealed class ChatControl : UserControl
    {
        // ------------------------------------------------------------ events (UI → controller)
        public event Action<ComposedMessage> SendRequested;
        /// <summary>"Bỏ" on the queued-message row: the message typed while Claude was working is dropped.</summary>
        public event Action QueueCancelRequested;
        /// <summary>The palette went compact (true) or back to full (false): the host resizes the palette set.</summary>
        public event Action<bool> CompactChanged;
        public event Action StopRequested;
        public event Action NewChatRequested;
        public event Action PickSelectionRequested;
        public event Action PickPointRequested;
        public event Action ExportRequested;
        public event Action UndoTurnRequested;
        public event Action EditLastRequested;
        public event Action RetryRequested;
        public event Action<List<string>> ZoomRequested;
        public event Action<string, bool> ConfirmAnswered;
        public event Action<PaletteSettings> SettingsChanged;
        // skills (+ Add skill / Skill list)
        public event Action AddSkillRequested;
        /// <summary>"+" (true) / "−" (false): use the skill in the current chat or not; the library keeps it.</summary>
        public event Action<string, bool> SkillToggled;
        public event Action<string> SkillEditRequested;
        // v0.6: look at the drawing, read-only, project notes, macros, templates, skill drafts, plans
        public event Action CaptureScreenRequested;
        public event Action<bool> ReadOnlyToggled;
        public event Action NotesRequested;
        public event Action<string, string> NotesSaved;            // project-folder notes, drawing notes
        public event Action<ChatEntry, string> MacroSaveRequested;  // turn, name
        public event Action<string, bool> MacroRunRequested;        // name, at a picked point
        public event Action<string> MacroDeleteRequested;
        public event Action OpenMacrosFolderRequested;
        public event Action<string, string> TemplateSaveRequested;  // title, text
        public event Action<string> SkillDraftOpenRequested;        // SKILL.md of the draft
        public event Action<string> SkillDraftAddRequested;
        public event Action<string, string> PlanAnswered;           // id, go | edit | cancel
        // v0.7: version history
        public event Action<string> VersionOpenRequested;           // version id
        public event Action VersionCloseRequested;
        public event Action OpenVersionsFolderRequested;
        // v0.9.1: setup
        public event Action LoginRequested;
        public event Action SelfTestRequested;
        public event Action WelcomeRequested;
        // v0.9.2: account + bug report
        public event Action SettingsOpened;
        public event Action SwitchAccountRequested;
        public event Action LogoutRequested;
        public event Action<string> ApiKeySaveRequested;
        public event Action ApiKeyClearRequested;
        public event Action ReportRequested;
        /// <summary>Usage limit card: true = send the request again automatically when the limit resets.</summary>
        public event Action<bool> LimitAutoRetryToggled;
        // v0.9.4: activation
        public event Action ActivationRequestRequested;
        public event Action<string> ActivationKeySubmitted;
        /// <summary>"sk-ant-…AbCd" of the saved key, or null (shown in the API key editor).</summary>
        public Func<string> ApiKeyMask;
        /// <summary>Layer / block / style names of the active drawing for "@" (called on the UI thread).</summary>
        public Func<List<MentionItem>> MentionSource;
        /// <summary>Layers of the active drawing with object counts for the layer picker (UI thread).</summary>
        public Func<List<LayerRow>> LayerSource;

        // ------------------------------------------------------------ layout
        private readonly StackPanel _messages = new StackPanel { Margin = new Thickness(6) };
        private readonly ScrollViewer _scroll;
        private readonly TextBox _input;
        private readonly Button _send, _stop, _new, _export, _settingsBtn, _pickSel, _pickPt, _addImg;
        private readonly TextBlock _connection, _usage, _toolStatus;
        private readonly ComboBox _scale;
        private readonly WrapPanel _chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        private readonly Popup _slashPopup;
        private readonly ListBox _slashList;
        private readonly Popup _settingsPopup;
        private readonly Popup _skillsPopup;
        private readonly StackPanel _skillRows = new StackPanel();
        private readonly TextBlock _skillsHeader;
        private Button _addSkill, _skillsBtn;
        private List<SkillInfo> _skills = new List<SkillInfo>();
        private HashSet<string> _chatSkills = new HashSet<string>(StringComparer.Ordinal);
        private ComboBox _model, _effort, _theme;
        private CheckBox _notify, _visualCheck, _planFirst, _suggestCheck, _hotkey, _versionsCheck, _sideSnapshot;
        private Button _versionsBtn;
        private Popup _versionsPopup;
        private readonly StackPanel _versionRows = new StackPanel();
        private Border _versionBanner;
        private TextBlock _versionBannerText;
        private List<VersionInfo> _versions = new List<VersionInfo>();
        private bool _viewingVersion;
        private TextBlock _accountStatus;
        private Category _accountCat;
        private Border _apiKeyPanel;
        private PasswordBox _apiKeyBox;
        private TextBlock _apiKeyInfo;
        private readonly StackPanel _toasts = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(12, 6, 12, 0), IsHitTestVisible = true };
        // layer scope
        private Button _layersBtn;
        // usage meter (hạn mức) row in the toolbar
        private DockPanel _usageMeter;
        // message typed while a turn was running, waiting to be sent
        // compact mode: only the toolbar row, a status line and the input stay visible
        private bool _compact;
        private Button _compactBtn;
        private Button _compactLine;
        private TextBlock _compactLineText;
        private WrapPanel _ctxBar;
        private StackPanel _leftActions;
        private Grid _middle;
        private Border _bottomBorder;
        // "● Đang làm · 1 phút 12 giây · 5 thao tác" while a turn runs
        private TextBlock _busyLine;
        private DispatcherTimer _busyTimer;
        private DateTime _busySince;
        private DockPanel _queuedRow;
        private TextBlock _queuedText;
        private TextBlock _usageLabel, _usageReset;
        private ColumnDefinition _usageFill, _usageRest;
        private Border _layerPanel;
        private TextBox _layerSearch;
        private StackPanel _layerList;
        private TextBlock _layerPanelInfo;
        private List<LayerRow> _layerRows = new List<LayerRow>();
        private readonly HashSet<string> _layerPicked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _layerScope = new List<string>();
        private readonly Dictionary<string, int> _layerScopeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _excludedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // v0.6
        private readonly Button _capture, _macrosBtn, _notesBtn;
        private readonly ToggleButton _readOnlyBtn;
        private readonly WrapPanel _suggestions = new WrapPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
        private readonly Popup _macrosPopup, _mentionPopup;
        private readonly Border _notesPanel;
        private readonly StackPanel _macroRows = new StackPanel();
        private readonly ListBox _mentionList;
        private TextBox _notesFolder, _notesDrawing;
        private TextBlock _notesFolderLabel;
        private List<Macro> _macros = new List<Macro>();
        private List<MentionItem> _mentionCache;
        private int _mentionStart = -1;
        private bool _suppressReadOnlyEvent;
        private string _lastUserText = "";

        // ------------------------------------------------------------ state
        private PaletteSettings _settings = new PaletteSettings();
        private List<QuickTemplate> _templates = new List<QuickTemplate>();
        private InputHistoryCursor _history = new InputHistoryCursor(new List<string>());
        private readonly List<ImageAttachment> _images = new List<ImageAttachment>();
        private PickedSelection _selection;
        private bool _selectionIncluded = true;
        private bool _busy;
        private bool _suppressSettingsEvents;
        private AssistantView _current;
        private AssistantView _lastAssistant;
        private Button _lastEditButton;
        private readonly List<Button> _openConfirmButtons = new List<Button>();
        private readonly List<DispatcherTimer> _cardTimers = new List<DispatcherTimer>();

        public ChatControl()
        {
            Theme.Apply(Resources, true);
            Theme.Ref(this, BackgroundProperty, Theme.PanelBg);
            Theme.Ref(this, ForegroundProperty, Theme.Fg);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            AllowDrop = true;
            Drop += OnDrop;
            PreviewMouseWheel += OnZoomWheel;

            var root = new DockPanel { LastChildFill = true };

            // ---- top bar: status + usage | Xuất, ⚙, New, Stop
            var top = new DockPanel { Margin = new Thickness(6, 6, 6, 2) };
            _settingsBtn = IconButton("⚙", "Cài đặt — model, mức suy nghĩ, giao diện, cỡ chữ, thông báo");
            _export = IconButton("⤓", "Xuất — lưu cuộc chat này ra file Markdown");
            _new = IconButton("⊞", "Chat mới — mở đoạn chat mới cho bản vẽ này, đoạn cũ vẫn xem lại được");
            _stop = IconButton("■", "Dừng — ngắt lượt đang chạy; phần đã vẽ xong vẫn giữ (hoàn tác bằng 1 lần U)");
            _stop.IsEnabled = false;
            _compactBtn = IconButton("⌄", CompactTip(false));
            _compactBtn.Click += (s, e) => SetCompact(!_compact);
            _settingsBtn.Click += (s, e) =>
            {
                _settingsPopup.IsOpen = !_settingsPopup.IsOpen;
                if (_settingsPopup.IsOpen) SettingsOpened?.Invoke(); // refresh the account line
            };
            _export.Click += (s, e) => ExportRequested?.Invoke();
            _new.Click += (s, e) => NewChatRequested?.Invoke();
            _stop.Click += (s, e) => StopRequested?.Invoke();
            foreach (var b in new[] { _settingsBtn, _compactBtn, _new, _export, _stop }) { DockPanel.SetDock(b, Dock.Right); top.Children.Add(b); }
            var statusStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _connection = MutedText("Đang khởi động...");
            _usage = MutedText("");
            _usage.FontSize = 11;
            statusStack.Children.Add(_connection);
            statusStack.Children.Add(_usage);
            top.Children.Add(statusStack);

            // ---- usage meter: "Hạn mức 5 giờ · 41%" + a 3 px bar + "làm mới 14:30"
            _usageMeter = new DockPanel { Margin = new Thickness(6, 0, 6, 3), Visibility = Visibility.Collapsed };
            _usageLabel = MutedText("");
            _usageLabel.FontSize = 11;
            _usageLabel.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(_usageLabel, Dock.Left);
            _usageMeter.Children.Add(_usageLabel);
            _usageReset = MutedText("");
            _usageReset.FontSize = 11;
            _usageReset.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(_usageReset, Dock.Right);
            _usageMeter.Children.Add(_usageReset);
            _usageFill = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
            _usageRest = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            var bar = new Grid { Height = 3, VerticalAlignment = VerticalAlignment.Center };
            bar.ColumnDefinitions.Add(_usageFill);
            bar.ColumnDefinitions.Add(_usageRest);
            var fill = new Border();
            Theme.Ref(fill, Border.BackgroundProperty, Theme.Accent);
            bar.Children.Add(fill);
            var track = new Border { Child = bar, CornerRadius = Theme.Corner };
            Theme.Ref(track, Border.BackgroundProperty, Theme.InputBg);
            _usageMeter.Children.Add(track);

            // ---- context bar (feature 9)
            var ctxBar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 4) };
            _ctxBar = ctxBar;
            ctxBar.Children.Add(MutedText("Tỷ lệ "));
            _scale = MakeCombo(PaletteSettings.Scales.Select(s => s == "" ? "Tự động" : s).ToArray(), 76);
            _scale.ToolTip = "Tỷ lệ in: quyết định chiều cao chữ và kích thước.";
            ctxBar.Children.Add(_scale);
            _addSkill = IconButton("＋", "Add skill — thêm file .md từ máy (chọn nhiều file được): vào thư viện và dùng luôn ở đoạn chat này");
            _addSkill.Margin = new Thickness(6, 0, 1, 0);
            _addSkill.Click += (s, e) => AddSkillRequested?.Invoke();
            ctxBar.Children.Add(_addSkill);
            _skillsBtn = IconButton("✦", "Skill — skill đang dùng ở đoạn chat này (− bỏ, + dùng) và thư viện skill đã thêm");
            _skillsBtn.Click += (s, e) => { RefreshSkillsPanel(); _skillsPopup.IsOpen = !_skillsPopup.IsOpen; };
            ctxBar.Children.Add(_skillsBtn);
            _layersBtn = IconButton("≡", "Layer — chọn layer để chat chỉnh sửa (đổi màu, nét, khóa… hoặc sửa đối tượng trên layer)");
            _layersBtn.Click += (s, e) => ToggleLayerPanel();
            ctxBar.Children.Insert(ctxBar.Children.IndexOf(_addSkill), _layersBtn);
            _macrosBtn = IconButton("▷", "Macro — chạy lại các lượt vẽ đã lưu, không cần Claude (không tốn token)");
            _macrosBtn.Click += (s, e) => { RefreshMacrosPanel(); _macrosPopup.IsOpen = !_macrosPopup.IsOpen; };
            ctxBar.Children.Add(_macrosBtn);
            _notesBtn = IconButton("✎", "Ghi chú dự án — gửi kèm mọi tin nhắn (cao độ, chiều dày tường, khung tên…)");
            _notesBtn.Click += (s, e) => NotesRequested?.Invoke();
            ctxBar.Children.Add(_notesBtn);
            _versionsBtn = IconButton("⟲", "Phiên bản — bản sao bản vẽ trước khi chat sửa (30 phút/lần, giữ 10 bản): mở ra xem, Đóng để về bản hiện tại");
            _versionsBtn.Click += (s, e) => { RefreshVersionsPanel(); _versionsPopup.IsOpen = !_versionsPopup.IsOpen; };
            ctxBar.Children.Add(_versionsBtn);
            _readOnlyBtn = new ToggleButton { Content = "🔓 Sửa được", ToolTip = "Chỉ xem: Claude chỉ được đọc, đo, hỏi đáp — không sửa bản vẽ này", MinWidth = 40, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
            _readOnlyBtn.Checked += (s, e) => { PaintReadOnly(); if (!_suppressReadOnlyEvent) ReadOnlyToggled?.Invoke(true); };
            _readOnlyBtn.Unchecked += (s, e) => { PaintReadOnly(); if (!_suppressReadOnlyEvent) ReadOnlyToggled?.Invoke(false); };
            ctxBar.Children.Add(_readOnlyBtn);
            _scale.SelectionChanged += (s, e) => OnSettingsUi();

            // The two rows above are the palette's toolbar: one strip, like a Properties category header,
            // with a thin shadow so it reads as sitting on top of the drawing.
            top.Margin = new Thickness(6, 4, 6, 2);
            ctxBar.Margin = new Thickness(6, 0, 6, 4);
            _compactLineText = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11 };
            Theme.Ref(_compactLineText, TextBlock.ForegroundProperty, Theme.Fg);
            _compactLine = new Button
            {
                Content = _compactLineText,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 2, 8, 3),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed,
                ToolTip = "Bấm để mở rộng và xem đoạn chat",
                Focusable = false,
            };
            Theme.Ref(_compactLine, BackgroundProperty, Theme.PanelBg);
            Theme.Ref(_compactLine, BorderBrushProperty, Theme.Border);
            _compactLine.Click += (s, e) => SetCompact(false);

            var toolbar = new StackPanel();
            toolbar.Children.Add(top);
            toolbar.Children.Add(_usageMeter);
            toolbar.Children.Add(ctxBar);
            toolbar.Children.Add(_compactLine);
            var toolbarBorder = new Border
            {
                Child = toolbar,
                CornerRadius = Theme.Corner,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Effect = Theme.Shadow(),
            };
            Theme.Ref(toolbarBorder, Border.BackgroundProperty, Theme.HeaderBg);
            Theme.Ref(toolbarBorder, Border.BorderBrushProperty, Theme.Border);
            DockPanel.SetDock(toolbarBorder, Dock.Top);
            root.Children.Add(toolbarBorder);

            // ---- banner while an old version is open (read-only tab)
            var bannerRow = new DockPanel { Margin = new Thickness(8, 5, 8, 5) };
            var closeVersion = new Button { Content = "Đóng — về bản hiện tại", ToolTip = "Đóng tab chỉ xem của bản sao và quay lại bản vẽ đang làm", Padding = new Thickness(10, 2, 10, 2), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            closeVersion.Click += (s, e) => VersionCloseRequested?.Invoke();
            DockPanel.SetDock(closeVersion, Dock.Right);
            bannerRow.Children.Add(closeVersion);
            _versionBannerText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Theme.Ref(_versionBannerText, TextBlock.ForegroundProperty, Theme.Fg);
            bannerRow.Children.Add(_versionBannerText);
            _versionBanner = new Border { Child = bannerRow, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
            Theme.Ref(_versionBanner, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(_versionBanner, Border.BorderBrushProperty, Theme.Accent);
            DockPanel.SetDock(_versionBanner, Dock.Top);
            root.Children.Add(_versionBanner);

            // ---- API key editor (inline: a password box in a popup gets no keys inside a PaletteSet)
            _apiKeyPanel = BuildApiKeyPanel();
            DockPanel.SetDock(_apiKeyPanel, Dock.Top);
            root.Children.Add(_apiKeyPanel);

            // ---- layer picker (inline: it has a search box)
            _layerPanel = BuildLayerPanel();
            DockPanel.SetDock(_layerPanel, Dock.Top);
            root.Children.Add(_layerPanel);

            // ---- project notes editor (inline: text boxes in WPF popups get no keys inside a PaletteSet)
            _notesPanel = BuildNotesPanel();
            DockPanel.SetDock(_notesPanel, Dock.Top);
            root.Children.Add(_notesPanel);

            // ---- bottom: tool status, chips, input, action buttons
            var bottom = new DockPanel { Margin = new Thickness(6, 2, 6, 6) };
            _toolStatus = MutedText("");
            _toolStatus.TextWrapping = TextWrapping.Wrap; // long hints ("Cần kích hoạt…", errors) are not cut at 300–400 px
            _toolStatus.TextTrimming = TextTrimming.None;
            _toolStatus.FontStyle = FontStyles.Italic;
            _toolStatus.Margin = new Thickness(0, 0, 0, 4);
            DockPanel.SetDock(_toolStatus, Dock.Top);
            bottom.Children.Add(_toolStatus);

            // ---- what the running turn is doing: elapsed time + number of actions (real figures only)
            _busyLine = MutedText("");
            _busyLine.FontSize = 11;
            _busyLine.Margin = new Thickness(0, 0, 0, 3);
            _busyLine.Visibility = Visibility.Collapsed;
            _busyLine.ToolTip = "Lượt đang chạy: thời gian đã trôi qua và số thao tác Claude đã làm trên bản vẽ. Esc trong ô nhập hoặc ■ để dừng";
            DockPanel.SetDock(_busyLine, Dock.Top);
            bottom.Children.Add(_busyLine);
            _busyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _busyTimer.Tick += (s, e) => UpdateBusyLine();

            // ---- queued message ("gửi thêm ý" while Claude works)
            _queuedRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
            var dropQueued = SmallButton("Bỏ", "Không gửi tin đang chờ này nữa");
            dropQueued.Click += (s, e) => QueueCancelRequested?.Invoke();
            DockPanel.SetDock(dropQueued, Dock.Right);
            _queuedRow.Children.Add(dropQueued);
            _queuedText = MutedText("");
            _queuedText.FontSize = 11;
            _queuedRow.Children.Add(_queuedText);
            DockPanel.SetDock(_queuedRow, Dock.Top);
            bottom.Children.Add(_queuedRow);
            DockPanel.SetDock(_suggestions, Dock.Top);
            bottom.Children.Add(_suggestions);
            DockPanel.SetDock(_chips, Dock.Top);
            bottom.Children.Add(_chips);

            var actions = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            _send = MakeButton("Gửi", "Gửi (Enter). Shift+Enter xuống dòng, / để chọn mẫu lệnh, ↑↓ gọi lại tin đã gửi");
            DockPanel.SetDock(_send, Dock.Right);
            actions.Children.Add(_send);
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            _leftActions = left;
            // Same icon row as the toolbar (screen A1): glyph + "Tên — giải thích ngắn" tooltip.
            _pickSel = IconButton("⬚", "Chọn đối tượng — quét chọn trên bản vẽ để gửi kèm tin nhắn");
            _pickPt = IconButton("✛", "Lấy điểm — bấm điểm trên bản vẽ để chèn tọa độ vào tin nhắn (Enter để xong)");
            _addImg = IconButton("▨", "Ảnh — đính kèm ảnh phác thảo / ảnh scan (hoặc dán Ctrl+V, kéo thả vào khung chat)");
            _capture = IconButton("⛶", "Chụp — chụp vùng bản vẽ đang xem gửi kèm, để chỉ cho Claude \"chỗ này sai\"");
            _capture.Click += (s, e) => CaptureScreenRequested?.Invoke();
            foreach (var b in new[] { _pickSel, _pickPt, _addImg, _capture }) { b.Margin = new Thickness(0, 0, 2, 0); left.Children.Add(b); }
            actions.Children.Add(left);
            _pickSel.Click += (s, e) => PickSelectionRequested?.Invoke();
            _pickPt.Click += (s, e) => PickPointRequested?.Invoke();
            _addImg.Click += (s, e) => BrowseImage();
            _send.Click += (s, e) => SubmitInput();
            DockPanel.SetDock(actions, Dock.Bottom);
            bottom.Children.Add(actions);

            _input = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 52,
                MaxHeight = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(4),
            };
            Theme.Ref(_input, BackgroundProperty, Theme.InputBg);
            Theme.Ref(_input, ForegroundProperty, Theme.Fg);
            Theme.Ref(_input, TextBoxBase.CaretBrushProperty, Theme.Fg);
            Theme.Ref(_input, BorderBrushProperty, Theme.Border);
            _input.PreviewKeyDown += OnInputKeyDown;
            _input.TextChanged += (s, e) => { UpdateSlashPopup(); UpdateMentionPopup(); };
            DataObject.AddPastingHandler(_input, OnPasting);
            bottom.Children.Add(_input);
            // Input strip: same treatment as the toolbar, shadow pointing up.
            var bottomBorder = new Border
            {
                Child = bottom,
                CornerRadius = Theme.Corner,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Effect = Theme.Shadow(-2),
            };
            Theme.Ref(bottomBorder, Border.BackgroundProperty, Theme.HeaderBg);
            Theme.Ref(bottomBorder, Border.BorderBrushProperty, Theme.Border);
            DockPanel.SetDock(bottomBorder, Dock.Bottom);
            root.Children.Add(bottomBorder);
            _bottomBorder = bottomBorder;

            // ---- slash-template popup (feature 8)
            _slashList = new ListBox { MaxHeight = 260, MinWidth = 260, BorderThickness = new Thickness(1) };
            Theme.Ref(_slashList, BackgroundProperty, Theme.InputBg);
            Theme.Ref(_slashList, ForegroundProperty, Theme.Fg);
            Theme.Ref(_slashList, BorderBrushProperty, Theme.Border);
            _slashList.MouseDoubleClick += (s, e) => AcceptTemplate();
            _slashPopup = new Popup { PlacementTarget = _input, Placement = PlacementMode.Top, StaysOpen = false, Child = _slashList, AllowsTransparency = false };

            // ---- settings popup (features 14, 15, 19)
            _settingsPopup = new Popup { PlacementTarget = _settingsBtn, Placement = PlacementMode.Bottom, StaysOpen = false, Child = BuildSettingsPanel() };

            // ---- skills popup (+ Add skill / list)
            _skillsHeader = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(_skillsHeader, TextBlock.ForegroundProperty, Theme.Fg);
            var skillsHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var addInPanel = SmallButton("+ Add skill", "Thêm skill từ file .md (chọn được nhiều file): vào thư viện và dùng luôn trong đoạn chat này");
            addInPanel.Margin = new Thickness(0);
            addInPanel.Click += (s, e) => AddSkillRequested?.Invoke();
            DockPanel.SetDock(addInPanel, Dock.Right);
            skillsHead.Children.Add(addInPanel);
            skillsHead.Children.Add(_skillsHeader);
            var skillsStack = new StackPanel { Margin = new Thickness(10), Width = 340 };
            skillsStack.Children.Add(skillsHead);
            skillsStack.Children.Add(new ScrollViewer { Content = _skillRows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var skillsBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Child = skillsStack };
            Theme.Ref(skillsBorder, Border.BackgroundProperty, Theme.InputBg);
            Theme.Ref(skillsBorder, Border.BorderBrushProperty, Theme.Border);
            _skillsPopup = new Popup { PlacementTarget = _skillsBtn, Placement = PlacementMode.Bottom, StaysOpen = false, Child = skillsBorder };

            // ---- macros popup
            var macrosHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var macrosFolder = SmallButton("Mở thư mục", "Mở thư mục macro trong Explorer");
            macrosFolder.Margin = new Thickness(0);
            macrosFolder.Click += (s, e) => OpenMacrosFolderRequested?.Invoke();
            DockPanel.SetDock(macrosFolder, Dock.Right);
            macrosHead.Children.Add(macrosFolder);
            var macrosTitle = new TextBlock { Text = "Macro — chạy lại không cần Claude", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(macrosTitle, TextBlock.ForegroundProperty, Theme.Fg);
            macrosHead.Children.Add(macrosTitle);
            var macrosStack = new StackPanel { Margin = new Thickness(10), Width = 340 };
            macrosStack.Children.Add(macrosHead);
            macrosStack.Children.Add(new ScrollViewer { Content = _macroRows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var macrosBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Child = macrosStack };
            Theme.Ref(macrosBorder, Border.BackgroundProperty, Theme.InputBg);
            Theme.Ref(macrosBorder, Border.BorderBrushProperty, Theme.Border);
            _macrosPopup = new Popup { PlacementTarget = _macrosBtn, Placement = PlacementMode.Bottom, StaysOpen = false, Child = macrosBorder };


            // ---- versions popup
            var versionsHead = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var versionsFolder = SmallButton("Mở thư mục", "Mở thư mục chứa các bản sao trong Explorer");
            versionsFolder.Margin = new Thickness(0);
            versionsFolder.Click += (s, e) => OpenVersionsFolderRequested?.Invoke();
            DockPanel.SetDock(versionsFolder, Dock.Right);
            versionsHead.Children.Add(versionsFolder);
            var versionsTitle = new TextBlock { Text = "Phiên bản của bản vẽ này", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(versionsTitle, TextBlock.ForegroundProperty, Theme.Fg);
            versionsHead.Children.Add(versionsTitle);
            var versionsHint = MutedText("Tự lưu bản sao ngay trước khi chat sửa bản vẽ, tối đa 30 phút một lần, giữ 10 bản gần nhất. Mở ra là một tab chỉ xem.");
            versionsHint.FontSize = 11;
            versionsHint.TextWrapping = TextWrapping.Wrap;
            versionsHint.Margin = new Thickness(0, 0, 0, 6);
            var versionsStack = new StackPanel { Margin = new Thickness(10), Width = 340 };
            versionsStack.Children.Add(versionsHead);
            versionsStack.Children.Add(versionsHint);
            versionsStack.Children.Add(new ScrollViewer { Content = _versionRows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var versionsBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Child = versionsStack };
            Theme.Ref(versionsBorder, Border.BackgroundProperty, Theme.InputBg);
            Theme.Ref(versionsBorder, Border.BorderBrushProperty, Theme.Border);
            _versionsPopup = new Popup { PlacementTarget = _versionsBtn, Placement = PlacementMode.Bottom, StaysOpen = false, Child = versionsBorder };

            // ---- @ names popup
            _mentionList = new ListBox { MaxHeight = 240, MinWidth = 240, BorderThickness = new Thickness(1) };
            Theme.Ref(_mentionList, BackgroundProperty, Theme.InputBg);
            Theme.Ref(_mentionList, ForegroundProperty, Theme.Fg);
            Theme.Ref(_mentionList, BorderBrushProperty, Theme.Border);
            _mentionList.MouseDoubleClick += (s, e) => AcceptMention();
            _mentionPopup = new Popup { PlacementTarget = _input, Placement = PlacementMode.Top, StaysOpen = false, Child = _mentionList };

            // popups must be in the logical tree for the theme resources to resolve
            foreach (var pop in new[] { _slashPopup, _settingsPopup, _skillsPopup, _macrosPopup, _versionsPopup, _mentionPopup })
            {
                DockPanel.SetDock(pop, Dock.Top);
                root.Children.Add(pop);
            }

            // ---- middle: history
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _messages,
            };
            // toasts float over the conversation only (not over the context bar's buttons at narrow widths),
            // they never push the messages around
            var middle = new Grid();
            _middle = middle;
            middle.Children.Add(_scroll);
            middle.Children.Add(_toasts);
            root.Children.Add(middle);
            Content = root;
            RefreshChips();
        }

        // ================================================================= building blocks

        private Button MakeButton(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, MinWidth = 40, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
            return b;
        }

        /// <summary>
        /// Toolbar button the way AutoCAD palettes have them: a 16 px glyph, no text, and a tooltip that
        /// says in one short line what it does. <paramref name="tip"/> is "Tên — giải thích ngắn".
        /// </summary>
        private Button IconButton(string glyph, string tip)
        {
            var b = new Button
            {
                Content = new TextBlock { Text = glyph, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                ToolTip = tip,
                Width = 24,
                Height = 22,
                MinWidth = 24,
                Margin = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Focusable = false,
            };
            Theme.Ref(b, ForegroundProperty, Theme.Fg);
            return b;
        }

        private static Button SmallButton(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 0, 6, 1), FontSize = 11, Cursor = Cursors.Hand };
            return b;
        }

        /// <summary>Check box label that wraps instead of being cut off in narrow panels.</summary>
        private static TextBlock Wrapped(string text) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };

        private static TextBlock MutedText(string text)
        {
            var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.MutedFg);
            return t;
        }

        /// <summary>
        /// A collapsible group the way AutoCAD palettes have them (the General / Geometry rows in
        /// Properties): a flat header strip with a ▾ / ▸ glyph, no rounded chevron box.
        /// </summary>
        private sealed class Category
        {
            public readonly Border Root;
            private readonly TextBlock _glyph;
            private readonly TextBlock _title;
            private readonly FrameworkElement _content;

            public Category(string title, FrameworkElement content, bool expanded = true)
            {
                _content = content;
                _glyph = new TextBlock { Text = expanded ? "▾" : "▸", FontSize = 9, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
                Theme.Ref(_glyph, TextBlock.ForegroundProperty, Theme.MutedFg);
                _title = new TextBlock { Text = title, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Theme.Ref(_title, TextBlock.ForegroundProperty, Theme.MutedFg);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(_glyph);
                row.Children.Add(_title);

                var header = new Button
                {
                    Content = row,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(6, 2, 6, 2),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Focusable = false,
                    ToolTip = "Bấm để mở / thu gọn",
                };
                Theme.Ref(header, Control.BackgroundProperty, Theme.HeaderBg);
                Theme.Ref(header, Control.BorderBrushProperty, Theme.Border);
                header.Click += (s, e) => Toggle();

                _content.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                var stack = new StackPanel();
                stack.Children.Add(header);
                stack.Children.Add(_content);
                Root = new Border { Child = stack, CornerRadius = Theme.Corner, Margin = new Thickness(0, 2, 0, 2) };
            }

            public Visibility Visibility
            {
                get => Root.Visibility;
                set => Root.Visibility = value;
            }

            public void SetTitle(string text) => _title.Text = text;

            /// <summary>Opens the group if it is collapsed (e.g. the account group while not signed in).</summary>
            public void Expand()
            {
                if (_content.Visibility != Visibility.Visible) Toggle();
            }

            public void Collapse()
            {
                if (_content.Visibility == Visibility.Visible) Toggle();
            }

            public void Toggle()
            {
                bool open = _content.Visibility == Visibility.Visible;
                _content.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                _glyph.Text = open ? "▸" : "▾";
            }
        }

        /// <summary>
        /// An icon button has no room for a count, so "there is something here" shows as the accent color
        /// (like an active toggle in AutoCAD) and the count goes in the tooltip.
        /// </summary>
        private static void MarkIcon(Button b, string state, string tip)
        {
            if (b == null) return;
            b.ToolTip = string.IsNullOrEmpty(state) ? tip : tip + " · " + state;
            Theme.Ref(b, ForegroundProperty, string.IsNullOrEmpty(state) ? Theme.Fg : Theme.Accent);
        }

        /// <summary>Small "CẬU" / "CLAUDE" label above a message block, instead of a chat bubble.</summary>
        private static TextBlock SpeakerLabel(string who)
        {
            var t = MutedText(who);
            t.FontSize = 10;
            t.FontWeight = FontWeights.SemiBold;
            t.Margin = new Thickness(0, 0, 0, 1);
            return t;
        }

        /// <summary><paramref name="width"/> = NaN lets the combo fill its cell (the settings grid).</summary>
        private static ComboBox MakeCombo(string[] items, double width)
        {
            var c = new ComboBox { Width = width, Margin = new Thickness(2, 0, 0, 0), FontSize = 12 };
            if (double.IsNaN(width)) { c.Width = double.NaN; c.HorizontalAlignment = HorizontalAlignment.Stretch; c.Margin = new Thickness(0); }
            foreach (var i in items) c.Items.Add(i);
            c.SelectedIndex = 0;
            return c;
        }

        private FrameworkElement BuildSettingsPanel()
        {
            // Laid out like the Properties palette: collapsible groups of two-column rows
            // (name on the left, value on the right).
            var p = new StackPanel { Width = 320 };
            var border = new Border { BorderThickness = new Thickness(1), Child = p, CornerRadius = Theme.Corner, Effect = Theme.Shadow() };
            Theme.Ref(border, Border.BackgroundProperty, Theme.PanelBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);

            Grid NewGrid()
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                return g;
            }

            // One row of a group: the name, then the control. Both carry the explanation.
            void Row(Grid g, string name, FrameworkElement value, string tip)
            {
                int r = g.RowDefinitions.Count;
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = name,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 3, 6, 3),
                    ToolTip = tip,
                };
                Theme.Ref(label, TextBlock.ForegroundProperty, Theme.Fg);
                var cell = new Border { Child = value, BorderThickness = new Thickness(1, 0, 0, 1), Padding = new Thickness(4, 2, 4, 2) };
                Theme.Ref(cell, Border.BorderBrushProperty, Theme.Border);
                var labelCell = new Border { Child = label, BorderThickness = new Thickness(0, 0, 0, 1) };
                Theme.Ref(labelCell, Border.BorderBrushProperty, Theme.Border);
                value.ToolTip = tip;
                value.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(labelCell, r);
                Grid.SetColumn(labelCell, 0);
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, 1);
                g.Children.Add(labelCell);
                g.Children.Add(cell);
            }

            // A checkbox row: a bare box in the value cell, like the yes/no properties in AutoCAD.
            CheckBox CheckRow(Grid g, string name, string tip, bool on = true)
            {
                var c = new CheckBox { IsChecked = on, HorizontalAlignment = HorizontalAlignment.Left };
                Theme.Ref(c, ForegroundProperty, Theme.Fg);
                c.Checked += (s, e) => OnSettingsUi();
                c.Unchecked += (s, e) => OnSettingsUi();
                Row(g, name, c, tip);
                return c;
            }

            // ---- Chung: giao diện
            var look = NewGrid();
            _theme = MakeCombo(new[] { "Theo AutoCAD", "Tối", "Sáng" }, double.NaN);
            Row(look, "Giao diện", _theme, "Màu khung chat: theo theme AutoCAD (khuyên dùng), hoặc ép tối / sáng");
            var fonts = new StackPanel { Orientation = Orientation.Horizontal };
            var minus = SmallButton("A−", "Chữ nhỏ hơn (Ctrl + lăn chuột xuống)");
            var plus = SmallButton("A+", "Chữ to hơn (Ctrl + lăn chuột lên)");
            minus.Click += (s, e) => ChangeFont(-1);
            plus.Click += (s, e) => ChangeFont(+1);
            fonts.Children.Add(minus);
            fonts.Children.Add(plus);
            Row(look, "Cỡ chữ", fonts, "Mặc định theo cỡ chữ palette của AutoCAD; chọn cỡ ở đây thì giữ cỡ của cậu");
            _notify = CheckRow(look, "Báo khi lượt xong", "Bong bóng ở icon ClaudeCAD trên thanh trạng thái khi khung chat đang đóng / tự ẩn; và thông báo Windows khi AutoCAD không phải cửa sổ trước mặt");
            _hotkey = CheckRow(look, "Phím tắt Ctrl+Shift+Space", "Mở palette và đặt con trỏ vào ô nhập (lệnh tắt: CC)");
            p.Children.Add(new Category("CHUNG — GIAO DIỆN", look).Root);

            // ---- Cách Claude làm việc
            var work = NewGrid();
            _model = MakeCombo(PaletteSettings.Models.Select(PaletteSettings.ModelLabel).ToArray(), double.NaN);
            Row(work, "Model", _model, "Model Claude dùng cho các lượt sau; mặc định là model của trợ lý");
            _effort = MakeCombo(new[] { "Mặc định", "low — nhanh", "medium", "high", "xhigh — kỹ nhất" }, double.NaN);
            Row(work, "Mức suy nghĩ", _effort, "Càng cao Claude càng cân nhắc kỹ nhưng chậm và tốn token hơn");
            _visualCheck = CheckRow(work, "Tự kiểm tra bằng ảnh", "Claude chụp vùng vừa vẽ (capture_view) để tự soát lỗi; tốn thêm ít token cho mỗi ảnh");
            _planFirst = CheckRow(work, "Kế hoạch trước việc lớn", "Thẻ kế hoạch có nút Làm / Sửa kế hoạch trước khi Claude vẽ việc nhiều bước");
            _suggestCheck = CheckRow(work, "Gợi ý bước tiếp theo", "2–3 nút gợi ý dưới câu trả lời; bấm là gửi");
            p.Children.Add(new Category("CÁCH CLAUDE LÀM VIỆC", work).Root);

            // ---- An toàn bản vẽ
            var safety = NewGrid();
            _versionsCheck = CheckRow(safety, "Lưu phiên bản trước khi sửa", "Bản sao DWG trong %LocalAppData%\\ClaudeCAD\\versions (30 phút/lần, giữ 10 bản); mở xem bằng nút ⟲");
            _sideSnapshot = CheckRow(safety, "Biên bản cạnh bản vẽ", "Biên bản thay đổi lưu thêm trong thư mục ẩn .claudecad cạnh file DWG, để máy khác có cài plugin cũng biết bản vẽ đã được sửa tay", on: false);
            p.Children.Add(new Category("AN TOÀN BẢN VẼ", safety).Root);

            // ---- Tài khoản AI (thu gọn)
            var account = new StackPanel { Margin = new Thickness(6, 4, 6, 6) };
            _accountStatus = MutedText("Đang kiểm tra...");
            _accountStatus.TextWrapping = TextWrapping.Wrap;
            _accountStatus.TextTrimming = TextTrimming.None;
            _accountStatus.FontSize = 11;
            account.Children.Add(_accountStatus);
            var accButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            Button Act(WrapPanel into, string text, string tip, Action a)
            {
                var b = SmallButton(text, tip);
                b.Margin = new Thickness(0, 0, 6, 4);
                b.Click += (s, e) => { _settingsPopup.IsOpen = false; a(); };
                into.Children.Add(b);
                return b;
            }
            Act(accButtons, "Đăng nhập Claude", "Đăng nhập tài khoản Claude Pro/Max trên trình duyệt (dùng Claude Code có sẵn trong plugin)", () => LoginRequested?.Invoke());
            Act(accButtons, "Đổi tài khoản", "Đăng xuất tài khoản hiện tại rồi đăng nhập tài khoản Claude khác", () => SwitchAccountRequested?.Invoke());
            Act(accButtons, "Đăng xuất", "Đăng xuất tài khoản Claude trên máy này (cả Claude Code nếu có)", () => LogoutRequested?.Invoke());
            Act(accButtons, "Dùng API key", "Dán API key Anthropic (trả theo lượng dùng) thay cho tài khoản Pro/Max", () => ShowApiKeyPanel(ApiKeyMask?.Invoke()));
            account.Children.Add(accButtons);
            _accountCat = new Category("TÀI KHOẢN AI", account, expanded: false);
            p.Children.Add(_accountCat.Root);

            // ---- Trợ giúp (thu gọn)
            var help = new StackPanel { Margin = new Thickness(6, 4, 6, 6) };
            var helpButtons = new WrapPanel();
            Act(helpButtons, "Gửi báo cáo lỗi", "Gom thông tin máy, log, kết quả tự kiểm tra thành một file .zip trên Desktop để gửi cho người hỗ trợ", () => ReportRequested?.Invoke());
            Act(helpButtons, "Hướng dẫn bắt đầu", "Kiểm tra cài đặt, đăng nhập, tự kiểm tra", () => WelcomeRequested?.Invoke());
            help.Children.Add(helpButtons);
            p.Children.Add(new Category("TRỢ GIÚP", help, expanded: false).Root);

            _model.SelectionChanged += (s, e) => OnSettingsUi();
            _effort.SelectionChanged += (s, e) => OnSettingsUi();
            _theme.SelectionChanged += (s, e) => OnSettingsUi();
            _notify.Checked += (s, e) => OnSettingsUi();
            _notify.Unchecked += (s, e) => OnSettingsUi();
            return border;
        }

        private void Ui(Action a)
        {
            void Safe()
            {
                try { a(); }
                catch (Exception ex) { Logger.Error("palette UI update failed", ex); }
            }
            if (Dispatcher.CheckAccess()) Safe();
            else Dispatcher.BeginInvoke((Action)Safe);
        }

        /// <summary>
        /// Queues work behind all UI updates already posted (same Dispatcher queue), so the
        /// controller's end-of-turn work never overtakes the last streamed text or tool rows.
        /// </summary>
        public void Enqueue(Action a) => Dispatcher.BeginInvoke((Action)(() =>
        {
            try { a(); }
            catch (Exception ex) { Logger.Error("palette queued action failed", ex); }
        }));

        internal static void ForwardWheel(UIElement element)
        {
            element.PreviewMouseWheel += (s, e) =>
            {
                if (e.Handled || (Keyboard.Modifiers & ModifierKeys.Control) != 0) return;
                e.Handled = true;
                var parent = (element as FrameworkElement)?.Parent as UIElement;
                parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = element });
            };
        }

        /// <summary>
        /// Follows new content only when the user is at (or near) the bottom: scrolling up to read an earlier answer
        /// while Claude is still writing no longer jumps back down on every word. force: the user's own message, a
        /// conversation opening.
        /// </summary>
        // ≥ 0 while older messages are inserted above the live ones ("Xem thêm"), else -1 (append)
        private int _insertAt = -1;

        private void AppendMessage(UIElement element)
        {
            if (_insertAt >= 0) _messages.Children.Insert(_insertAt++, element);
            else _messages.Children.Add(element);
        }

        private void ScrollToEnd(bool force = false)
        {
            if (force || _scroll.VerticalOffset >= _scroll.ScrollableHeight - 80) _scroll.ScrollToEnd();
        }

        // ================================================================= settings / theme

        public void ApplySettings(PaletteSettings s) => Ui(() =>
        {
            _settings = s.Clone();
            _suppressSettingsEvents = true;
            try
            {
                _scale.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Scales, s.Scale));
                _model.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Models, s.Model));
                _effort.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Efforts, s.Effort));
                _theme.SelectedIndex = s.Theme == "dark" ? 1 : s.Theme == "light" ? 2 : 0;
                _notify.IsChecked = s.Notify;
                _visualCheck.IsChecked = s.VisualCheck;
                _planFirst.IsChecked = s.PlanFirst;
                _suggestCheck.IsChecked = s.Suggestions;
                _hotkey.IsChecked = s.Hotkey;
                _versionsCheck.IsChecked = s.Versions;
                _sideSnapshot.IsChecked = s.SnapshotNextToDrawing;
                FontSize = s.FontSize;
                _history = new InputHistoryCursor(s.History);
            }
            finally
            {
                _suppressSettingsEvents = false;
            }
        });

        private void OnSettingsUi()
        {
            if (_suppressSettingsEvents) return;
            _settings.Scale = PaletteSettings.Scales[Math.Max(0, _scale.SelectedIndex)];
            _settings.Model = PaletteSettings.Models[Math.Max(0, _model.SelectedIndex)];
            _settings.Effort = PaletteSettings.Efforts[Math.Max(0, _effort.SelectedIndex)];
            _settings.Theme = _theme.SelectedIndex == 1 ? "dark" : _theme.SelectedIndex == 2 ? "light" : "auto";
            _settings.Notify = _notify.IsChecked == true;
            _settings.VisualCheck = _visualCheck.IsChecked == true;
            _settings.PlanFirst = _planFirst.IsChecked == true;
            _settings.Suggestions = _suggestCheck.IsChecked == true;
            _settings.Hotkey = _hotkey.IsChecked == true;
            _settings.Versions = _versionsCheck.IsChecked == true;
            _settings.SnapshotNextToDrawing = _sideSnapshot.IsChecked == true;
            if (!_settings.Suggestions) ClearSuggestionsCore();
            SettingsChanged?.Invoke(_settings.Clone());
        }

        public void SetDark(bool dark) => SetDark(dark, null);

        /// <summary>
        /// Repaints the palette. <paramref name="acad"/> carries the colors and font AutoCAD uses for its
        /// own palettes (null = keep the built-in set, e.g. in the UI tests).
        /// </summary>
        public void SetDark(bool dark, AcadTheme.Info acad) => Ui(() =>
        {
            Theme.Apply(Resources, dark, acad);
            ApplyAcadFont(acad);
        });

        /// <summary>
        /// Takes the palette font from AutoCAD. The user's own font size (⚙ ▸ Cỡ chữ) wins, so this only
        /// sets the size while the setting is still on "theo AutoCAD" (FontSize = 0).
        /// </summary>
        private void ApplyAcadFont(AcadTheme.Info acad)
        {
            if (acad == null) return;
            if (acad.Font != null) FontFamily = acad.Font;
            if (acad.FontSize > 0 && (_settings == null || _settings.FontAuto)) FontSize = acad.FontSize;
        }

        private void ChangeFont(int delta)
        {
            // The user picked a size: stop following AutoCAD's palette font size from here on.
            _settings.FontSize = _settings.ClampFont((FontSize > 0 ? FontSize : _settings.FontSize) + delta);
            _settings.FontAuto = false;
            FontSize = _settings.FontSize;
            foreach (var v in _messages.Children.OfType<FrameworkElement>().Select(e => e.Tag).OfType<AssistantView>()) v.Rerender();
            SettingsChanged?.Invoke(_settings.Clone());
        }

        private void OnZoomWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;
            ChangeFont(e.Delta > 0 ? 1 : -1);
        }

        public void SetTemplates(List<QuickTemplate> templates) => Ui(() => _templates = templates ?? new List<QuickTemplate>());

        // ================================================================= input

        public void FocusInput() => Ui(() => _input.Focus());

        public void InsertAtCaret(string text) => Ui(() =>
        {
            int i = _input.CaretIndex;
            string before = i > 0 && !char.IsWhiteSpace(_input.Text[i - 1]) ? " " : "";
            _input.Text = _input.Text.Insert(i, before + text);
            _input.CaretIndex = i + before.Length + text.Length;
            _input.Focus();
        });

        public void SetInputText(string text) => Ui(() =>
        {
            _input.Text = text ?? "";
            _input.CaretIndex = _input.Text.Length;
            _input.Focus();
        });

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (_mentionPopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.Down: _mentionList.SelectedIndex = Math.Min(_mentionList.Items.Count - 1, _mentionList.SelectedIndex + 1); _mentionList.ScrollIntoView(_mentionList.SelectedItem); e.Handled = true; return;
                    case Key.Up: _mentionList.SelectedIndex = Math.Max(0, _mentionList.SelectedIndex - 1); _mentionList.ScrollIntoView(_mentionList.SelectedItem); e.Handled = true; return;
                    case Key.Enter:
                    case Key.Tab: AcceptMention(); e.Handled = true; return;
                    case Key.Escape: _mentionPopup.IsOpen = false; e.Handled = true; return;
                }
            }
            if (_slashPopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.Down: _slashList.SelectedIndex = Math.Min(_slashList.Items.Count - 1, _slashList.SelectedIndex + 1); e.Handled = true; return;
                    case Key.Up: _slashList.SelectedIndex = Math.Max(0, _slashList.SelectedIndex - 1); e.Handled = true; return;
                    case Key.Enter:
                    case Key.Tab: AcceptTemplate(); e.Handled = true; return;
                    case Key.Escape: _slashPopup.IsOpen = false; e.Handled = true; return;
                }
            }
            if (e.Key == Key.Escape && _busy)
            {
                // AutoCAD habit: Esc cancels what is running. Only while the caret is in the palette —
                // Esc in the drawing stays AutoCAD's own.
                e.Handled = true;
                StopRequested?.Invoke();
                return;
            }
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                SubmitInput();
                return;
            }
            if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None && _input.GetLineIndexFromCharacterIndex(_input.CaretIndex) <= 0)
            {
                var prev = _history.Previous(_input.Text);
                if (prev != null) { _input.Text = prev; _input.CaretIndex = prev.Length; e.Handled = true; }
                return;
            }
            if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None && _input.GetLineIndexFromCharacterIndex(_input.CaretIndex) >= _input.LineCount - 1)
            {
                var next = _history.Next();
                if (next != null) { _input.Text = next; _input.CaretIndex = next.Length; e.Handled = true; }
                return;
            }
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                bool image;
                try
                {
                    // Excel/Word also put a bitmap of the cells on the clipboard: text wins
                    image = !Clipboard.ContainsText() && Clipboard.ContainsImage();
                }
                catch { return; } // clipboard busy (clipboard manager, remote desktop): let the normal text paste happen
                if (!image) return;
                try
                {
                    e.Handled = true;
                    var img = Clipboard.GetImage();
                    // screenshots often come as Bgra32 with alpha = 0: drop the alpha channel
                    AddImage(ImageAttachment.FromBitmap(new System.Windows.Media.Imaging.FormatConvertedBitmap(img, PixelFormats.Bgr32, null, 0)));
                }
                catch (Exception ex)
                {
                    e.Handled = true;
                    AddNote("Không dán được ảnh: " + ex.Message, true);
                }
            }
        }

        /// <summary>Excel paste → Markdown table (feature 7).</summary>
        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)) return;
            var text = e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string;
            var table = ClipboardText.TsvToMarkdownTable(text, out var rows, out var cols);
            if (table == null) return;
            e.CancelCommand();
            InsertAtCaret("\n" + table + "\n");
            _toolStatus.Text = $"Đã dán bảng {rows} hàng × {cols} cột (dạng bảng Markdown).";
        }

        private void SubmitInput()
        {
            // While Claude is working the message is not lost and not blocked: the controller queues it
            // and sends it when the turn ends (the row above the input shows it, with "Bỏ").
            if (_viewingVersion) { _toolStatus.Text = "Đang xem phiên bản cũ (chỉ xem) — bấm \"Đóng — về bản hiện tại\" rồi chat."; return; }
            _slashPopup.IsOpen = false;
            var text = _input.Text.Trim();
            if (text.Length == 0 && _images.Count == 0) return;
            if (text.Length == 0) text = "Vẽ theo ảnh đính kèm.";
            var msg = new ComposedMessage
            {
                Text = text,
                Images = _images.ToList(),
                IncludeSelection = _selection != null && _selection.Count > 0 && _selectionIncluded,
                ExcludedLayers = _excludedLayers.ToList(),
                Layers = _layerScope.ToList(),
                LayerCounts = new Dictionary<string, int>(_layerScopeCounts),
            };
            _input.Clear();
            _images.Clear();
            ClearLayerScope();
            _layerPanel.Visibility = Visibility.Collapsed;
            ClearSuggestionsCore();
            _mentionPopup.IsOpen = false;
            _settings.AddHistory(text);
            _history = new InputHistoryCursor(_settings.History);
            RefreshChips();
            SendRequested?.Invoke(msg);
        }

        // ================================================================= slash templates

        private void UpdateSlashPopup()
        {
            var t = _input.Text;
            if (!t.StartsWith("/") || t.Contains(" ") || t.Contains("\n") || _templates.Count == 0)
            {
                _slashPopup.IsOpen = false;
                return;
            }
            var matches = QuickTemplates.Filter(_templates, t);
            _slashList.Items.Clear();
            foreach (var m in matches.Take(30))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Tag = m };
                row.Children.Add(new TextBlock { Text = "/" + m.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
                var title = MutedText(m.Title);
                row.Children.Add(title);
                _slashList.Items.Add(new ListBoxItem { Content = row, Tag = m });
            }
            if (_slashList.Items.Count == 0) { _slashPopup.IsOpen = false; return; }
            _slashList.SelectedIndex = 0;
            _slashPopup.IsOpen = true;
        }

        private void AcceptTemplate()
        {
            if (!(_slashList.SelectedItem is ListBoxItem item) || !(item.Tag is QuickTemplate t)) return;
            _slashPopup.IsOpen = false;
            var text = QuickTemplates.Expand(t.Text, out var start, out var len);
            _input.Text = text;
            _input.Focus();
            if (start >= 0) _input.Select(start, len);
            else _input.CaretIndex = text.Length;
        }

        // ================================================================= attachments (features 1, 6)

        public void SetSelectionChip(PickedSelection sel, bool explicitPick) => Ui(() =>
        {
            _selection = sel != null && sel.Count > 0 ? sel : null;
            if (explicitPick || _selection == null) _selectionIncluded = true;
            _excludedLayers.Clear();
            RefreshChips();
        });

        private void BrowseImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn ảnh phác thảo / ảnh scan",
                Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
                Multiselect = true,
            };
            if (dlg.ShowDialog() != true) return;
            foreach (var f in dlg.FileNames)
            {
                try { AddImage(ImageAttachment.FromFile(f)); }
                catch (Exception ex) { AddNote($"Không đọc được ảnh {System.IO.Path.GetFileName(f)}: {ex.Message}", true); }
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                if (!ImageAttachment.IsImageFile(f)) continue;
                try { AddImage(ImageAttachment.FromFile(f)); }
                catch (Exception ex) { AddNote($"Không đọc được ảnh {System.IO.Path.GetFileName(f)}: {ex.Message}", true); }
            }
        }

        private void AddImage(ImageAttachment img)
        {
            if (_images.Count >= 5) { AddNote("Tối đa 5 ảnh mỗi tin nhắn.", true); return; }
            _images.Add(img);
            RefreshChips();
        }

        private void RefreshChips()
        {
            _chips.Children.Clear();
            if (_selection != null)
            {
                var layers = _selection.Layers;
                int kept = LayerSelection.Filter(_selection.Handles, _selection.HandleLayers, _excludedLayers).Count;
                string onLayers = layers.Count > 0 ? $" trên {layers.Count} layer" : "";
                var label = _selectionIncluded
                    ? (kept == _selection.Count
                        ? $"Kèm {_selection.Count} đối tượng đang chọn{onLayers} ({_selection.Summary})"
                        : $"Kèm {kept}/{_selection.Count} đối tượng đang chọn (bỏ {_excludedLayers.Count} layer)")
                    : $"Không kèm {_selection.Count} đối tượng đang chọn";
                _chips.Children.Add(Chip(label, () => { _selectionIncluded = !_selectionIncluded; RefreshChips(); },
                    _selectionIncluded ? "✕" : "+", () => ZoomRequested?.Invoke(_selection.Handles)));
                if (_selectionIncluded && layers.Count > 0) _chips.Children.Add(SelectionLayerRow(layers));
            }
            if (_layerScope.Count > 0)
            {
                int total = _layerScope.Sum(l => _layerScopeCounts.TryGetValue(l, out var n) ? n : 0);
                var label = $"Layer: {LayerSelection.Label(_layerScope)} · {total} đối tượng";
                _chips.Children.Add(Chip(label, () => { ClearLayerScope(); RefreshChips(); }, "✕", () => OpenLayerPanel()));
            }
            foreach (var img in _images.ToList())
            {
                var chip = Chip($"Ảnh {img.Width}×{img.Height}", () => { _images.Remove(img); RefreshChips(); }, "✕", null, img.Thumbnail);
                _chips.Children.Add(chip);
            }
            _chips.Visibility = _chips.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>One small toggle per layer of the selection (tick = its objects are sent) + "Cả layer".</summary>
        private UIElement SelectionLayerRow(Dictionary<string, int> layers)
        {
            var row = new WrapPanel { Margin = new Thickness(6, 0, 0, 4) };
            var head = MutedText("Layer: ");
            head.FontSize = 11;
            row.Children.Add(head);
            foreach (var kv in layers.Take(12))
            {
                var name = kv.Key;
                bool on = !_excludedLayers.Contains(name);
                var b = new ToggleButton
                {
                    Content = $"{(on ? "☑" : "☐")} {name} {kv.Value}",
                    IsChecked = on,
                    FontSize = 11,
                    Padding = new Thickness(6, 0, 6, 1),
                    Margin = new Thickness(0, 0, 4, 2),
                    ToolTip = on ? $"Bỏ tick: không gửi {kv.Value} đối tượng trên {name}" : $"Tick: gửi kèm {kv.Value} đối tượng trên {name}",
                    Cursor = Cursors.Hand,
                };
                b.Click += (s, e) =>
                {
                    if (_excludedLayers.Contains(name)) _excludedLayers.Remove(name); else _excludedLayers.Add(name);
                    if (_excludedLayers.Count >= layers.Count) _excludedLayers.Remove(name); // keep at least one layer
                    RefreshChips();
                };
                row.Children.Add(b);
            }
            if (layers.Count > 12)
            {
                var more = MutedText($"… +{layers.Count - 12} layer");
                more.FontSize = 11;
                row.Children.Add(more);
            }
            var whole = SmallButton("→ Cả layer", "Dùng toàn bộ các layer đã tick (mọi đối tượng trên chúng), không chỉ phần đang chọn");
            whole.Click += (s, e) =>
            {
                SetLayerScope(layers.Keys.Where(l => !_excludedLayers.Contains(l)).ToList(), null);
                _selectionIncluded = false;
                RefreshChips();
            };
            row.Children.Add(whole);
            return row;
        }

        // ---------------------------------------------------------------- layer picker

        private Border BuildLayerPanel()
        {
            var p = new StackPanel { Margin = new Thickness(8) };
            // title on its own line (it was cut off when it shared the line with the buttons)
            var title = new TextBlock { Text = "Chọn layer để chat chỉnh sửa", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            p.Children.Add(title);
            var head = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            var apply = SmallButton("Dùng layer đã chọn", "Gửi kèm các layer đã tick với tin nhắn tiếp theo");
            var none = SmallButton("Bỏ chọn", "Bỏ tick tất cả");
            var close = SmallButton("Đóng", "Đóng danh sách layer");
            foreach (var b in new[] { apply, none, close }) head.Children.Add(b);
            p.Children.Add(head);
            _layerSearch = InputBox();
            _layerSearch.Margin = new Thickness(0, 2, 0, 4);
            _layerSearch.ToolTip = "Lọc theo tên layer (gõ vài chữ, ví dụ: wall, A-D)";
            _layerSearch.TextChanged += (s, e) => RenderLayerRows();
            p.Children.Add(_layerSearch);
            _layerPanelInfo = MutedText("");
            _layerPanelInfo.FontSize = 11;
            _layerPanelInfo.TextWrapping = TextWrapping.Wrap;
            p.Children.Add(_layerPanelInfo);
            _layerList = new StackPanel();
            p.Children.Add(new ScrollViewer { Content = _layerList, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 4, 0, 0) });
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
            Theme.Ref(border, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);
            close.Click += (s, e) => border.Visibility = Visibility.Collapsed;
            none.Click += (s, e) => { _layerPicked.Clear(); RenderLayerRows(); };
            apply.Click += (s, e) =>
            {
                border.Visibility = Visibility.Collapsed;
                SetLayerScope(_layerRows.Where(r => _layerPicked.Contains(r.Name)).Select(r => r.Name).ToList(), _layerRows);
                RefreshChips();
                _input.Focus();
                if (_layerScope.Count > 0) _toolStatus.Text = $"Đã chọn {_layerScope.Count} layer — gõ yêu cầu, ví dụ \"đổi màu sang đỏ\" hoặc \"tăng chiều cao chữ lên 300\".";
            };
            return border;
        }

        private void ToggleLayerPanel()
        {
            if (_layerPanel.Visibility == Visibility.Visible) { _layerPanel.Visibility = Visibility.Collapsed; return; }
            OpenLayerPanel();
        }

        /// <summary>Opens the layer picker (also used by the UI tests).</summary>
        public void ShowLayerPicker() => Ui(OpenLayerPanel);

        private void OpenLayerPanel()
        {
            try { _layerRows = LayerSource?.Invoke() ?? new List<LayerRow>(); }
            catch (Exception ex) { Logger.Warn("layer list unavailable: " + ex.Message); _layerRows = new List<LayerRow>(); }
            _layerPicked.Clear();
            foreach (var l in _layerScope) _layerPicked.Add(l);
            _layerSearch.Text = "";
            RenderLayerRows();
            _layerPanel.Visibility = Visibility.Visible;
            _layerSearch.Focus();
        }

        private void RenderLayerRows()
        {
            _layerList.Children.Clear();
            var rows = LayerSelection.Search(_layerRows, _layerSearch.Text);
            int pickedObjects = _layerRows.Where(r => _layerPicked.Contains(r.Name)).Sum(r => r.Count);
            _layerPanelInfo.Text = _layerRows.Count == 0
                ? "Không đọc được layer của bản vẽ đang mở."
                : $"{_layerRows.Count} layer · đã tick {_layerPicked.Count} layer ({pickedObjects} đối tượng). Đang hiện {rows.Count}.";
            foreach (var r in rows.Take(300))
            {
                var name = r.Name;
                var line = new DockPanel { Margin = new Thickness(0, 0, 0, 1) };
                var count = MutedText(r.Count + (string.IsNullOrEmpty(r.State) ? "" : " · " + r.State));
                count.FontSize = 11;
                DockPanel.SetDock(count, Dock.Right);
                line.Children.Add(count);
                var swatch = new Border
                {
                    Width = 10, Height = 10, Margin = new Thickness(0, 0, 6, 0), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromRgb((byte)(r.Rgb >> 16), (byte)(r.Rgb >> 8), (byte)r.Rgb)),
                };
                Theme.Ref(swatch, Border.BorderBrushProperty, Theme.Border);
                var label = new TextBlock { Text = name, FontWeight = r.Current ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };
                Theme.Ref(label, TextBlock.ForegroundProperty, r.Count == 0 ? Theme.MutedFg : Theme.Fg);
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(swatch);
                content.Children.Add(label);
                var check = new CheckBox { Content = content, ToolTip = "Chọn layer này để gửi kèm tin nhắn (Claude chỉ sửa trong phạm vi layer đã chọn)", IsChecked = _layerPicked.Contains(name), VerticalAlignment = VerticalAlignment.Center };
                check.Checked += (s, e) => { _layerPicked.Add(name); UpdateLayerInfo(); };
                check.Unchecked += (s, e) => { _layerPicked.Remove(name); UpdateLayerInfo(); };
                line.Children.Add(check);
                _layerList.Children.Add(line);
            }
            if (rows.Count > 300)
            {
                var more = MutedText($"… còn {rows.Count - 300} layer — gõ để lọc.");
                more.FontSize = 11;
                _layerList.Children.Add(more);
            }
        }

        private void UpdateLayerInfo()
        {
            int pickedObjects = _layerRows.Where(r => _layerPicked.Contains(r.Name)).Sum(r => r.Count);
            _layerPanelInfo.Text = $"{_layerRows.Count} layer · đã tick {_layerPicked.Count} layer ({pickedObjects} đối tượng).";
        }

        /// <summary>Layers sent with the next message; counts from the picker rows (or the selection when converting).</summary>
        private void SetLayerScope(List<string> names, List<LayerRow> rows)
        {
            _layerScope.Clear();
            _layerScopeCounts.Clear();
            if (rows == null)
            {
                try { rows = LayerSource?.Invoke(); } catch { rows = null; }
            }
            foreach (var n in names)
            {
                _layerScope.Add(n);
                var r = rows?.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
                if (r != null) _layerScopeCounts[n] = r.Count;
            }
        }

        private void ClearLayerScope()
        {
            _layerScope.Clear();
            _layerScopeCounts.Clear();
        }

        /// <summary>Test / controller hook: choose layers as if ticked in the picker.</summary>
        public void SetLayerScopeForTest(Dictionary<string, int> layers) => Ui(() =>
        {
            ClearLayerScope();
            foreach (var kv in layers) { _layerScope.Add(kv.Key); _layerScopeCounts[kv.Key] = kv.Value; }
            RefreshChips();
        });

        private UIElement Chip(string text, Action toggle, string toggleText, Action click, ImageSource thumb = null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (thumb != null) sp.Children.Add(new Image { Source = thumb, Height = 28, Margin = new Thickness(0, 0, 4, 0) });
            var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, FontSize = 11, MaxWidth = 260, TextTrimming = TextTrimming.CharacterEllipsis };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            if (click != null) { t.Cursor = Cursors.Hand; t.ToolTip = "Bấm để xem trên bản vẽ"; t.MouseLeftButtonUp += (s, e) => click(); }
            sp.Children.Add(t);
            var x = new Button { Content = toggleText, Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(4, 0, 0, 0), FontSize = 10, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            Theme.Ref(x, ForegroundProperty, Theme.MutedFg);
            x.Click += (s, e) => toggle();
            sp.Children.Add(x);
            var b = new Border { Child = sp, CornerRadius = Theme.Corner, Padding = new Thickness(8, 2, 4, 2), Margin = new Thickness(0, 0, 4, 4) };
            Theme.Ref(b, Border.BackgroundProperty, Theme.ChipBg);
            return b;
        }

        // ================================================================= skills

        /// <summary>The library (every skill added so far) and the skills the current chat uses.</summary>
        public void SetSkills(List<SkillInfo> library, IEnumerable<string> chatSkills) => Ui(() =>
        {
            _skills = library ?? new List<SkillInfo>();
            _chatSkills = new HashSet<string>(chatSkills ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            int used = _skills.Count(k => k.Error == null && _chatSkills.Contains(k.Name));
            MarkIcon(_skillsBtn, used > 0 ? $"đang dùng {used}" : null,
                "Skill — skill đang dùng ở đoạn chat này (− bỏ, + dùng) và thư viện skill đã thêm");
            RefreshSkillsPanel();
        });

        private void RefreshSkillsPanel()
        {
            var used = _skills.Where(k => k.Error == null && _chatSkills.Contains(k.Name)).ToList();
            var rest = _skills.Where(k => !used.Contains(k)).ToList();
            _skillsHeader.Text = $"Skill — {used.Count} đang dùng · thư viện {_skills.Count}";
            _skillRows.Children.Clear();
            if (_skills.Count == 0)
            {
                _skillRows.Children.Add(Wrapped("Thư viện chưa có skill nào. Bấm + Add skill để thêm file .md (chọn được nhiều file)."));
                return;
            }
            _skillRows.Children.Add(SectionTitle("ĐANG DÙNG TRONG ĐOẠN CHAT NÀY"));
            if (used.Count == 0)
            {
                var none = MutedText("Chưa dùng skill nào — bấm + ở skill bên dưới, hoặc + Add skill.");
                none.FontSize = 11;
                none.TextWrapping = TextWrapping.Wrap;
                none.Margin = new Thickness(0, 0, 0, 8);
                _skillRows.Children.Add(none);
            }
            foreach (var k in used) _skillRows.Children.Add(SkillRow(k, true));
            if (rest.Count > 0)
            {
                _skillRows.Children.Add(SectionTitle("THƯ VIỆN (chưa dùng trong đoạn chat này)"));
                foreach (var k in rest) _skillRows.Children.Add(SkillRow(k, false));
            }
        }

        private static TextBlock SectionTitle(string text)
        {
            var t = MutedText(text);
            t.FontSize = 10;
            t.FontWeight = FontWeights.SemiBold;
            t.Margin = new Thickness(0, 2, 0, 4);
            return t;
        }

        /// <summary>One skill: name · title, description, [Sửa] and "−" (in use) or "+" (library only).</summary>
        private UIElement SkillRow(SkillInfo k, bool inUse)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var name = k.Name;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            var edit = SmallButton("Sửa", "Mở SKILL.md để sửa; lưu file là Claude dùng bản mới từ tin nhắn tiếp theo");
            edit.Click += (s, e) => SkillEditRequested?.Invoke(name);
            buttons.Children.Add(edit);
            var toggle = SmallButton(inUse ? "−" : "+", inUse
                ? "Không dùng skill này trong đoạn chat hiện tại (vẫn giữ trong thư viện)"
                : "Dùng skill này trong đoạn chat hiện tại");
            toggle.MinWidth = 24;
            toggle.FontWeight = FontWeights.Bold;
            toggle.Margin = new Thickness(0);
            toggle.IsEnabled = k.Error == null;
            toggle.Click += (s, e) => SkillToggled?.Invoke(name, !inUse);
            buttons.Children.Add(toggle);
            DockPanel.SetDock(buttons, Dock.Right);
            row.Children.Add(buttons);

            var info = new StackPanel();
            var title = new TextBlock { Text = (inUse ? "✓ " : "") + k.Name + (k.Title != null && k.Title != k.Name ? "  ·  " + k.Title : ""), FontWeight = inUse ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            info.Children.Add(title);
            string status = k.Error != null ? "⚠ " + k.Error
                : k.Builtin ? "Skill mẫu đi kèm plugin" : "Đã thêm" + (k.Added.HasValue ? " " + k.Added.Value.ToString("dd/MM/yyyy") : "");
            var st = new TextBlock { Text = status, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(st, TextBlock.ForegroundProperty, k.Error != null ? Theme.ErrorFg : Theme.MutedFg);
            info.Children.Add(st);
            if (!string.IsNullOrEmpty(k.Description))
            {
                var d = MutedText(k.Description);
                d.FontSize = 11;
                d.TextWrapping = TextWrapping.Wrap;
                d.MaxHeight = 30;
                d.ToolTip = k.Description + (string.IsNullOrEmpty(k.Source) ? "" : "\n\nNguồn: " + k.Source);
                info.Children.Add(d);
            }
            row.Children.Add(info);
            return row;
        }

        // ================================================================= history rendering

        public void ClearHistory() => Ui(() =>
        {
            _messages.Children.Clear();
            _current = null;
            _lastAssistant = null;
            _lastEditButton = null;
            _openConfirmButtons.Clear();
            foreach (var t in _cardTimers) t.Stop(); // limit-card countdowns of removed cards (up to a week otherwise)
            _cardTimers.Clear();
            _toolStatus.Text = "";
        });

        /// <summary>Messages built when a conversation opens; older ones on "Xem thêm" (each reply is a rich-text block: 400 are slow).</summary>
        public const int TranscriptWindow = 60;

        public void LoadTranscript(ChatTranscript t, bool canUndoLast) => Ui(() => RenderTranscript(t, canUndoLast, Math.Max(0, t.Entries.Count - TranscriptWindow)));

        private void RenderTranscript(ChatTranscript t, bool canUndoLast, int from)
        {
            ClearHistory();
            if (t.Entries.Count == 0)
            {
                AddNote("Chào cậu! Gõ yêu cầu bằng tiếng Việt, ví dụ \"vẽ lưới trục 5x4 bước 4000\". Gõ / để chọn mẫu lệnh, Enter để gửi.");
                return;
            }
            AddNote($"Lịch sử chat của {(string.IsNullOrEmpty(t.DrawingName) ? "bản vẽ này" : t.DrawingName)} — {t.Entries.Count} mục. Claude vẫn nhớ ngữ cảnh cuộc chat.");
            if (from > 0) AppendMessage(MoreButton(t, from));
            foreach (var e in t.Entries.Skip(from)) RenderEntry(e);
            if (_lastAssistant != null) _lastAssistant.SetUndo(canUndoLast);
            ScrollToEnd(true);
        }

        private void RenderEntry(ChatEntry e)
        {
            switch (e.Role)
            {
                case "user": AddUserCore(e); break;
                case "assistant":
                    var v = NewAssistant();
                    foreach (var tl in e.Tools) v.AddTool(tl);
                    v.Finish(e, false);
                    break;
                case "error": AddNoteCore(e.Text, true); break;
                default: AddNoteCore(e.Text, false); break;
            }
        }

        private Button MoreButton(ChatTranscript t, int from)
        {
            var more = new Button { Content = $"Xem thêm {Math.Min(from, TranscriptWindow)} tin cũ hơn ({from} tin chưa hiện)", ToolTip = "Hiện thêm tin cũ của đoạn chat này; các thẻ và nút đang mở vẫn giữ nguyên", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 2, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
            more.Click += (s, e) => ShowOlder(t, more, from);
            return more;
        }

        /// <summary>
        /// "Xem thêm": the older messages are inserted where the button was. Nothing below is rebuilt, so open cards
        /// (confirmation, plan, usage limit), "Hoàn tác lượt này" / "Lưu macro" and a reply being written stay.
        /// </summary>
        private void ShowOlder(ChatTranscript t, Button more, int from)
        {
            int at = _messages.Children.IndexOf(more);
            if (at < 0) return;
            _messages.Children.RemoveAt(at);
            int newFrom = Math.Max(0, from - TranscriptWindow);
            var current = _current;
            var last = _lastAssistant;
            var lastUser = _lastUserText;
            var edit = _lastEditButton;
            var confirms = _openConfirmButtons.ToList();
            _openConfirmButtons.Clear();
            _current = null;
            _lastAssistant = null;
            _lastEditButton = null; // only the newest request keeps its "Sửa" button
            _insertAt = at;
            try
            {
                if (newFrom > 0) AppendMessage(MoreButton(t, newFrom));
                foreach (var e in t.Entries.Skip(newFrom).Take(Math.Max(0, from - newFrom))) RenderEntry(e);
                EndAssistantCore();
                _lastAssistant?.SetUndo(false);
                if (_lastEditButton != null) _lastEditButton.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _insertAt = -1;
                _current = current;
                _lastAssistant = last;
                _lastUserText = lastUser;
                _lastEditButton = edit;
                _openConfirmButtons.Clear();
                _openConfirmButtons.AddRange(confirms);
            }
        }

        public void AddUserMessage(ChatEntry e) => Ui(() => { SetCompactNote(null); AddUserCore(e); ScrollToEnd(true); });

        private void AddUserCore(ChatEntry e)
        {
            EndAssistantCore();
            _lastUserText = e.Text ?? "";
            foreach (var b in _openConfirmButtons) b.IsEnabled = false; // answered by typing instead
            _openConfirmButtons.Clear();
            var tb = new TextBox { Text = e.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0) };
            Theme.Ref(tb, ForegroundProperty, Theme.Fg);
            ForwardWheel(tb);
            var stack = new StackPanel();
            stack.Children.Add(SpeakerLabel("CẬU"));
            stack.Children.Add(tb);
            var extras = new List<string>();
            if (e.SelectionCount > 0) extras.Add($"kèm {e.SelectionCount} đối tượng");
            if (e.ImageCount > 0) extras.Add($"kèm {e.ImageCount} ảnh");
            if (e.Layers.Count > 0) extras.Add($"layer {LayerSelection.Label(e.Layers, 2)}");
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 3, 0, 0) };
            if (extras.Count > 0) { var x = MutedText(string.Join(" · ", extras) + "  "); x.FontSize = 11; footer.Children.Add(x); }
            var copy = SmallButton("Copy", "Copy tin nhắn");
            copy.Click += (s, a) => SafeCopy(e.Text);
            footer.Children.Add(copy);
            var saveTpl = SmallButton("☆ Mẫu", "Lưu tin nhắn này thành mẫu lệnh (gõ / để dùng lại)");
            saveTpl.Click += (s, a) =>
            {
                saveTpl.IsEnabled = false;
                var first = (e.Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                var editor = InlinePrompt("Tên mẫu:", first.Length > 40 ? first.Substring(0, 40).TrimEnd() : first, "Lưu",
                    title => TemplateSaveRequested?.Invoke(title, e.Text), () => saveTpl.IsEnabled = true);
                stack.Children.Add(editor);
            };
            footer.Children.Add(saveTpl);
            var edit = SmallButton("Sửa", "Sửa và gửi lại (hoàn tác lượt trước nếu nó đã thay đổi bản vẽ)");
            edit.Click += (s, a) => EditLastRequested?.Invoke();
            footer.Children.Add(edit);
            if (_lastEditButton != null) _lastEditButton.Visibility = Visibility.Collapsed; // only the last request can be edited
            _lastEditButton = edit;
            edit.IsEnabled = !_busy;
            stack.Children.Add(footer);
            // Flat full-width block with a small speaker label, the way AutoCAD palettes list things —
            // no chat bubble, no right alignment.
            var border = new Border
            {
                CornerRadius = Theme.Corner,
                Padding = new Thickness(8, 4, 8, 5),
                Margin = new Thickness(0, 0, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = stack,
            };
            Theme.Ref(border, Border.BackgroundProperty, Theme.UserBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);
            AppendMessage(border);
        }

        public void BeginAssistant() => Ui(() =>
        {
            if (_current == null) { _current = NewAssistant(); ScrollToEnd(); }
        });

        private AssistantView NewAssistant()
        {
            var v = new AssistantView(this);
            AppendMessage(v.Root);
            _lastAssistant?.SetUndo(false);
            _lastAssistant = v;
            return v;
        }

        public void AppendAssistant(string delta) => Ui(() =>
        {
            if (string.IsNullOrEmpty(delta)) return;
            if (_current == null) _current = NewAssistant();
            _current.Append(delta);
            ScrollToEnd();
        });

        /// <summary>
        /// Step progress of the running turn (screen D): <paramref name="steps"/> are the steps of the plan the user
        /// approved (or that Claude declared), <paramref name="step"/> the 1-based step Claude is starting now.
        /// </summary>
        public void SetProgress(List<string> steps, int step, string note) => Ui(() =>
        {
            if (steps == null || steps.Count == 0) return;
            step = Math.Max(1, Math.Min(step, steps.Count));
            if (_current == null) _current = NewAssistant();
            _current.SetProgress(steps, step, note);
            _progressLabel = $"Bước {step}/{steps.Count}: {FirstLine(steps[step - 1], 40)}";
            if (_busy) UpdateBusyLine();
        });

        /// <summary>The turn is over: <paramref name="clean"/> = Claude finished it (no error, not stopped).</summary>
        public void EndProgress(bool clean) => Ui(() => _current?.SettleProgress(clean));

        public void UpdateTool(ToolLogEntry e) => Ui(() =>
        {
            if (_current == null) _current = NewAssistant();
            _current.AddTool(e);
        });

        /// <summary>Turn finished: final markdown, buttons "Xem kết quả" / "Hoàn tác lượt này".</summary>
        public void EndAssistant(ChatEntry e, bool canUndo, bool canSaveMacro = false) => Ui(() =>
        {
            if (_current == null) _current = NewAssistant();
            _current.Finish(e, canUndo, canSaveMacro);
            _current = null;
            if (_compact)
            {
                var line = FirstLine(e?.Text);
                SetCompactNote(line.Length > 0 ? "✓ Đã xong: " + line : "✓ Đã xong");
            }
            ScrollToEnd();
        });

        /// <summary>Removes the (empty) bubble of a turn that produced nothing.</summary>
        public void DiscardAssistant() => Ui(() =>
        {
            if (_current == null) return;
            _messages.Children.Remove(_current.Root);
            if (_lastAssistant == _current) _lastAssistant = null;
            _current = null;
        });

        private void EndAssistantCore()
        {
            if (_current != null) { _current.Flush(); _current.SettleProgress(false); _current.Settle(); _current = null; }
        }

        public void SetUndoAvailable(bool available) => Ui(() => _lastAssistant?.SetUndo(available));

        public void AddNote(string text, bool isError = false) => Ui(() => { AddNoteCore(text, isError); ScrollToEnd(); });

        private void AddNoteCore(string text, bool isError)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic, Margin = new Thickness(4) };
            Theme.Ref(t, TextBlock.ForegroundProperty, isError ? Theme.ErrorFg : Theme.MutedFg);
            AppendMessage(t);
        }

        /// <summary>
        /// A message line the way AutoCAD reports things: a colored glyph (⊗ error, ⚠ warning) and plain
        /// text. The color sits on the glyph only — cards are not tinted red.
        /// </summary>
        private static DockPanel IconLine(string glyph, string glyphKey, string text, bool bold = false)
        {
            var line = new DockPanel();
            var g = new TextBlock { Text = glyph, FontSize = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Top };
            Theme.Ref(g, TextBlock.ForegroundProperty, glyphKey);
            DockPanel.SetDock(g, Dock.Left);
            line.Children.Add(g);
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            line.Children.Add(t);
            return line;
        }

        /// <summary>Card frame shared by the error / sign-in / limit cards: neutral border, thin shadow.</summary>
        private static Border NoticeCard(UIElement child, object tag = null)
        {
            var b = new Border { Child = child, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 4, 0, 4), Tag = tag, Effect = Theme.Shadow() };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Border);
            return b;
        }

        /// <summary>Error card with "Thử lại" (feature 13).</summary>
        public void AddErrorCard(string text, bool canRetry) => Ui(() =>
        {
            var stack = new StackPanel();
            stack.Children.Add(IconLine("⊗", Theme.ErrorFg, text));
            SetCompactNote("⊗ Lượt chưa xong: " + FirstLine(text, 40));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (canRetry)
            {
                var retry = SmallButton("Thử lại", "Gửi lại yêu cầu vừa rồi");
                retry.Click += (s, e) => { retry.IsEnabled = false; RetryRequested?.Invoke(); };
                row.Children.Add(retry);
            }
            var report = SmallButton("Gửi báo cáo lỗi", "Gom thông tin máy và log thành một file .zip trên Desktop để gửi cho người hỗ trợ");
            report.Click += (s, e) => ReportRequested?.Invoke();
            row.Children.Add(report);
            stack.Children.Add(row);
            AppendMessage(NoticeCard(stack));
            ScrollToEnd();
        });

        /// <summary>Confirmation card with Đồng ý / Hủy / Xem trước (feature 11).</summary>
        public void AddConfirmCard(string id, string text, int? count, List<string> handles) => Ui(() =>
        {
            ExpandForDecision();
            // Header strip with a warning glyph, the count as a large figure, one accent button.
            // The warning lives in the glyph: the card itself is not tinted.
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var warn = new TextBlock { Text = "⚠", FontSize = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(warn, TextBlock.ForegroundProperty, Theme.WarnFg);
            head.Children.Add(warn);
            var title = new TextBlock { Text = "Xác nhận thay đổi", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            head.Children.Add(title);
            var headBorder = new Border { Child = head, Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0, 0, 0, 1) };
            Theme.Ref(headBorder, Border.BackgroundProperty, Theme.HeaderBg);
            Theme.Ref(headBorder, Border.BorderBrushProperty, Theme.Border);

            var body = new StackPanel { Margin = new Thickness(8, 6, 8, 6) };
            int affected = count ?? handles?.Count ?? 0;
            if (affected > 0)
            {
                var figure = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                var n = new TextBlock { Text = affected.ToString(CultureInfo.InvariantCulture), FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 6, 0) };
                Theme.Ref(n, TextBlock.ForegroundProperty, Theme.Fg);
                figure.Children.Add(n);
                var unit = MutedText("đối tượng bị ảnh hưởng");
                unit.FontSize = 11;
                unit.VerticalAlignment = VerticalAlignment.Bottom;
                unit.Margin = new Thickness(0, 0, 0, 3);
                figure.Children.Add(unit);
                body.Children.Add(figure);
            }
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            body.Children.Add(t);

            if (handles != null && handles.Count > 0)
            {
                var preview = new Button
                {
                    Content = $"⌖ Xem trên bản vẽ — tô sáng {handles.Count} đối tượng",
                    ToolTip = "Zoom tới các đối tượng này và chọn sẵn chúng bằng đúng kiểu chọn của AutoCAD (Esc trong bản vẽ để bỏ chọn)",
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                };
                preview.Click += (s, e) => ZoomRequested?.Invoke(handles);
                body.Children.Add(preview);
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var yes = new Button { Content = "Đồng ý", ToolTip = "Cho Claude làm việc vừa nêu; trước khi sửa plugin lưu một phiên bản (hoàn tác được bằng 1 lần U)", Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold };
            Theme.Ref(yes, BackgroundProperty, Theme.PressBg);
            Theme.Ref(yes, ForegroundProperty, Theme.AccentFg);
            Theme.Ref(yes, BorderBrushProperty, Theme.Accent);
            var no = new Button { Content = "Không", ToolTip = "Không làm việc này; bản vẽ không bị đụng tới", Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 6, 0) };
            row.Children.Add(yes);
            row.Children.Add(no);
            var answer = MutedText("");
            void Done(bool ok)
            {
                yes.IsEnabled = no.IsEnabled = false;
                _openConfirmButtons.Remove(yes);
                _openConfirmButtons.Remove(no);
                answer.Text = ok ? "  → đã đồng ý" : "  → đã hủy";
                ConfirmAnswered?.Invoke(id, ok);
            }
            yes.Click += (s, e) => Done(true);
            no.Click += (s, e) => Done(false);
            _openConfirmButtons.Add(yes);
            _openConfirmButtons.Add(no);
            row.Children.Add(answer);
            body.Children.Add(row);

            var note = MutedText("Trước khi sửa, plugin lưu một phiên bản của bản vẽ. Sau đó vẫn hoàn tác được — tương đương 1 lần U.");
            note.FontSize = 11;
            note.TextWrapping = TextWrapping.Wrap;
            note.TextTrimming = TextTrimming.None;
            note.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(note);

            var stack = new StackPanel();
            stack.Children.Add(headBorder);
            stack.Children.Add(body);
            var b = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(0, 4, 0, 4), Tag = "confirm", Effect = Theme.Shadow() };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Border);
            if (_current != null) _current.AddCard(b);       // inside the turn's block: text after it continues there
            else AppendMessage(b);
            ScrollToEnd();
        });

        // ================================================================= status

        public void SetToolStatus(string text) => Ui(() => _toolStatus.Text = text ?? "");

        public void SetConnection(string text, bool ok) => Ui(() =>
        {
            _connection.Text = text;
            _connection.ToolTip = string.IsNullOrEmpty(text) ? null : text; // usually cut next to the buttons
            Theme.Ref(_connection, TextBlock.ForegroundProperty, ok ? Theme.MutedFg : Theme.ErrorFg);
        });

        // ================================================================= compact mode

        private static string CompactTip(bool compact) => compact
            ? "Mở rộng — hiện lại đoạn chat của phiên này"
            : "Thu gọn — palette co lại chỉ còn ô nhập, Enter gửi nhanh; bấm lại để mở rộng";

        public bool IsCompact => _compact;

        /// <summary>
        /// Compact = toolbar row + a one-line status + the input. Nothing is unloaded: expanding shows the
        /// conversation exactly as it was.
        /// </summary>
        public void SetCompact(bool compact) => Ui(() =>
        {
            if (_compact == compact) return;
            _compact = compact;
            var rest = compact ? Visibility.Collapsed : Visibility.Visible;
            _middle.Visibility = rest;
            _ctxBar.Visibility = rest;
            _leftActions.Visibility = rest;
            _suggestions.Visibility = rest;
            if (compact)
            {
                // the inline editors need the full palette
                _layerPanel.Visibility = Visibility.Collapsed;
                _notesPanel.Visibility = Visibility.Collapsed;
                _apiKeyPanel.Visibility = Visibility.Collapsed;
                _settingsPopup.IsOpen = _skillsPopup.IsOpen = _macrosPopup.IsOpen = _versionsPopup.IsOpen = false;
            }
            else
            {
                SetCompactNote(null);
                ScrollToEnd(true);
            }
            _input.MinHeight = compact ? 26 : 52;
            // compact: the input sits right under the toolbar (a docked palette keeps its full height)
            DockPanel.SetDock(_bottomBorder, compact ? Dock.Top : Dock.Bottom);
            ((TextBlock)_compactBtn.Content).Text = compact ? "⌃" : "⌄";
            _compactBtn.ToolTip = CompactTip(compact);
            CompactChanged?.Invoke(compact);
            _input.Focus();
        });

        /// <summary>One line of news while compact ("✓ Đã xong: …"); null clears it. Ignored when expanded.</summary>
        private void SetCompactNote(string text)
        {
            if (string.IsNullOrEmpty(text) || !_compact)
            {
                _compactLine.Visibility = Visibility.Collapsed;
                _compactLineText.Text = "";
                return;
            }
            _compactLineText.Text = text + " · bấm để xem";
            _compactLine.Visibility = Visibility.Visible;
        }

        /// <summary>A card that needs a decision (confirmation, plan, sign-in, activation) opens the palette.</summary>
        private void ExpandForDecision()
        {
            if (_compact) SetCompact(false);
        }

        /// <summary>First line of a reply, cut for the compact status line.</summary>
        internal static string FirstLine(string markdown, int max = 60)
        {
            var plain = MarkdownLite.ToPlainText(markdown ?? "");
            var line = plain.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
            return line.Length <= max ? line : line.Substring(0, max).TrimEnd() + "…";
        }

        /// <summary>"Bước 3/7: Vẽ tường" while a turn reports its plan steps (report_progress); null otherwise.</summary>
        private string _progressLabel;

        private void UpdateBusyLine()
        {
            int tools = _current?.ToolCount ?? 0;
            _busyLine.Text = "● " + (_progressLabel ?? "Đang làm") + " · " + Elapsed(DateTime.UtcNow - _busySince)
                + (tools > 0 ? $" · {tools} thao tác" : "") + " — Esc để dừng";
        }

        /// <summary>"12 giây", "1 phút 12 giây".</summary>
        internal static string Elapsed(TimeSpan t)
        {
            int total = Math.Max(0, (int)t.TotalSeconds);
            return total < 60 ? $"{total} giây" : $"{total / 60} phút {total % 60} giây";
        }

        public void SetUsage(string text) => Ui(() => _usage.Text = text ?? "");

        /// <summary>
        /// Shows the message typed while Claude was working: it is sent when the turn ends. null hides the row.
        /// </summary>
        public void SetQueued(string text) => Ui(() =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _queuedRow.Visibility = Visibility.Collapsed;
                _queuedText.Text = "";
                return;
            }
            var oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            _queuedText.Text = "⏳ Chờ gửi khi Claude xong lượt này: " + oneLine;
            _queuedRow.ToolTip = "Tin này tự gửi ngay khi lượt đang chạy kết thúc:\n" + text;
            _queuedRow.Visibility = Visibility.Visible;
        });

        /// <summary>
        /// The 5-hour / weekly allowance as AutoCAD would show it: a label, a 3 px bar and the reset time.
        /// <paramref name="fraction"/> below 0 hides the row (nothing known yet, or plenty left).
        /// </summary>
        public void SetUsageMeter(string label, double fraction, string resetAt) => Ui(() =>
        {
            if (fraction < 0 || string.IsNullOrEmpty(label))
            {
                _usageMeter.Visibility = Visibility.Collapsed;
                return;
            }
            double f = Math.Max(0, Math.Min(1, fraction));
            _usageFill.Width = new GridLength(f, GridUnitType.Star);
            _usageRest.Width = new GridLength(1 - f, GridUnitType.Star);
            _usageLabel.Text = $"{label} · đã dùng {f * 100:0}%";
            _usageReset.Text = string.IsNullOrEmpty(resetAt) ? "" : "làm mới " + resetAt;
            _usageMeter.ToolTip = $"Hạn mức tài khoản Claude: đã dùng {f * 100:0}%"
                + (string.IsNullOrEmpty(resetAt) ? "" : $", làm mới lúc {resetAt}");
            _usageMeter.Visibility = Visibility.Visible;
        });

        public void SetBusy(bool busy) => Ui(() =>
        {
            _busy = busy;
            _send.IsEnabled = !_viewingVersion;
            _send.Content = busy ? "Gửi thêm" : "Gửi";
            _send.ToolTip = busy
                ? "Gửi thêm ý — xếp hàng, tự gửi ngay khi Claude xong lượt này (Enter). Bấm ■ để dừng lượt đang chạy"
                : "Gửi (Enter). Shift+Enter xuống dòng, / để chọn mẫu lệnh, ↑↓ gọi lại tin đã gửi";
            _stop.IsEnabled = busy;
            if (busy)
            {
                if (!_busyTimer.IsEnabled) { _busySince = DateTime.UtcNow; _busyTimer.Start(); _progressLabel = null; }
                _busyLine.Visibility = Visibility.Visible;
                UpdateBusyLine();
            }
            else
            {
                _busyTimer.Stop();
                _busyLine.Visibility = Visibility.Collapsed;
                _progressLabel = null;
            }
            _pickSel.IsEnabled = _pickPt.IsEnabled = !busy;
            foreach (var b in _macroRows.Children.OfType<FrameworkElement>().SelectMany(r => FindButtons(r)).Where(b => (b.Tag as string) == "run")) b.IsEnabled = !busy;
            if (_lastEditButton != null) _lastEditButton.IsEnabled = !busy;
            if (!busy) _toolStatus.Text = "";
        });

        private static void SafeCopy(string text)
        {
            try { Clipboard.SetText(text ?? ""); } catch { }
        }


        // ================================================================= v0.6: suggestions, read-only, notes, @, macros, cards

        private static IEnumerable<Button> FindButtons(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (child is Button b) yield return b;
                foreach (var x in FindButtons(child)) yield return x;
            }
        }

        private TextBox InputBox(double minWidth = 120)
        {
            var tb = new TextBox { MinWidth = minWidth, FontSize = 12, Padding = new Thickness(2) };
            Theme.Ref(tb, BackgroundProperty, Theme.InputBg);
            Theme.Ref(tb, ForegroundProperty, Theme.Fg);
            Theme.Ref(tb, TextBoxBase.CaretBrushProperty, Theme.Fg);
            Theme.Ref(tb, BorderBrushProperty, Theme.Border);
            return tb;
        }

        /// <summary>Small inline editor (label, text box, OK / Hủy) that removes itself when done.</summary>
        private FrameworkElement InlinePrompt(string label, string initial, string okText, Action<string> ok, Action cancelled = null)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var l = MutedText(label + " ");
            l.FontSize = 11;
            DockPanel.SetDock(l, Dock.Left);
            row.Children.Add(l);
            var okB = SmallButton(okText, okText);
            var cancel = SmallButton("Hủy", "Hủy");
            cancel.Margin = new Thickness(0);
            DockPanel.SetDock(cancel, Dock.Right);
            DockPanel.SetDock(okB, Dock.Right);
            row.Children.Add(cancel);
            row.Children.Add(okB);
            var tb = InputBox();
            tb.Text = initial ?? "";
            ForwardWheel(tb);
            tb.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(tb);
            void Close() { if (row.Parent is Panel p) p.Children.Remove(row); }
            void Ok()
            {
                var v = tb.Text.Trim();
                if (v.Length == 0) return;
                Close();
                ok(v);
            }
            okB.Click += (s, e) => Ok();
            cancel.Click += (s, e) => { Close(); cancelled?.Invoke(); };
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Ok(); }
                else if (e.Key == Key.Escape) { e.Handled = true; Close(); cancelled?.Invoke(); }
            };
            tb.Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
            return row;
        }

        // ---------------------------------------------------------------- next-step suggestions

        /// <summary>Adds next-step buttons under the last reply (at most 4, duplicates ignored).</summary>
        public void AddSuggestions(IEnumerable<string> items) => Ui(() =>
        {
            if (!_settings.Suggestions || items == null) return; // the controller drops stale ones
            var existing = _suggestions.Children.OfType<Button>().Select(b => b.Tag as string).ToList();
            foreach (var raw in items)
            {
                var text = (raw ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (text.Length == 0 || text.Length > 200) continue;
                if (existing.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase))) continue;
                if (existing.Count >= 4) break;
                existing.Add(text);
                var label = new TextBlock { Text = "→ " + text, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 300, FontSize = 12 };
                Theme.Ref(label, TextBlock.ForegroundProperty, Theme.Fg);
                var b = new Button { Content = label, Tag = text, ToolTip = "Bấm để gửi: " + text + "\nChuột phải: đưa vào ô nhập để sửa trước khi gửi", Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(0, 0, 4, 4), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                Theme.Ref(b, BackgroundProperty, Theme.ChipBg);
                Theme.Ref(b, BorderBrushProperty, Theme.Border);
                b.Click += (s, e) =>
                {
                    if (_busy) return;
                    _input.Text = text;
                    SubmitInput();
                };
                b.MouseRightButtonUp += (s, e) => { e.Handled = true; ClearSuggestionsCore(); SetInputText(text); };
                _suggestions.Children.Add(b);
            }
            _suggestions.Visibility = _suggestions.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        });

        public void ClearSuggestions() => Ui(ClearSuggestionsCore);

        private void ClearSuggestionsCore()
        {
            _suggestions.Children.Clear();
            _suggestions.Visibility = Visibility.Collapsed;
        }

        // ---------------------------------------------------------------- read-only ("Chỉ xem")

        public void SetReadOnly(bool on) => Ui(() =>
        {
            _suppressReadOnlyEvent = true;
            try { _readOnlyBtn.IsChecked = on; }
            finally { _suppressReadOnlyEvent = false; }
            PaintReadOnly();
        });

        private void PaintReadOnly()
        {
            bool on = _readOnlyBtn.IsChecked == true;
            _readOnlyBtn.Content = on ? "🔒 Chỉ xem" : "🔓 Sửa được";
            _readOnlyBtn.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            _readOnlyBtn.ToolTip = on
                ? "Đang bật Chỉ xem: Claude chỉ đọc, đo, hỏi đáp — không sửa bản vẽ này. Bấm để cho phép sửa."
                : "Bấm để bật Chỉ xem: Claude chỉ được đọc, đo, hỏi đáp — không sửa bản vẽ này";
            if (on)
            {
                Theme.Ref(_readOnlyBtn, ForegroundProperty, Theme.ErrorFg);
                Theme.Ref(_readOnlyBtn, BackgroundProperty, Theme.InputBg); // the default light chrome made the dark-theme red unreadable
                Theme.Ref(_readOnlyBtn, BorderBrushProperty, Theme.ErrorFg);
            }
            else
            {
                _readOnlyBtn.ClearValue(ForegroundProperty);
                _readOnlyBtn.ClearValue(BackgroundProperty);
                _readOnlyBtn.ClearValue(BorderBrushProperty);
            }
        }

        // ---------------------------------------------------------------- project notes

        private Border BuildNotesPanel()
        {
            var p = new StackPanel { Margin = new Thickness(8) };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var close = SmallButton("Đóng", "Đóng (không lưu)");
            close.Margin = new Thickness(0);
            var save = SmallButton("Lưu", "Lưu ghi chú — gửi kèm từ tin nhắn tiếp theo");
            DockPanel.SetDock(close, Dock.Right);
            DockPanel.SetDock(save, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(save);
            var title = new TextBlock { Text = "Ghi chú gửi kèm mọi tin nhắn", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            head.Children.Add(title);
            p.Children.Add(head);
            var hint = MutedText("Ví dụ: cao độ ±0.000 = +3.200; tường bao 220, tường ngăn 110; khung tên công ty X ở layout A3.");
            hint.FontSize = 11;
            hint.TextWrapping = TextWrapping.Wrap;
            p.Children.Add(hint);
            TextBox Box()
            {
                var tb = InputBox();
                tb.AcceptsReturn = true;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.Height = 72;
                tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                tb.MaxLength = ProjectNotes.MaxChars;
                tb.Margin = new Thickness(0, 2, 0, 4);
                return tb;
            }
            _notesFolderLabel = MutedText("");
            _notesFolderLabel.FontSize = 11;
            _notesFolderLabel.TextWrapping = TextWrapping.Wrap;
            _notesFolderLabel.Margin = new Thickness(0, 6, 0, 0);
            p.Children.Add(_notesFolderLabel);
            _notesFolder = Box();
            p.Children.Add(_notesFolder);
            var dl = MutedText("Riêng bản vẽ này");
            dl.FontSize = 11;
            p.Children.Add(dl);
            _notesDrawing = Box();
            p.Children.Add(_notesDrawing);
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
            Theme.Ref(border, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);
            close.Click += (s, e) => border.Visibility = Visibility.Collapsed;
            save.Click += (s, e) =>
            {
                border.Visibility = Visibility.Collapsed;
                NotesSaved?.Invoke(_notesFolder.IsEnabled ? _notesFolder.Text : null, _notesDrawing.Text);
            };
            return border;
        }

        /// <summary>Drawing switched: the notes editor and the layer picker / chosen layers belong to the old one.</summary>
        public void CloseDrawingPanels() => Ui(() =>
        {
            _notesPanel.Visibility = Visibility.Collapsed;
            _layerPanel.Visibility = Visibility.Collapsed;
            _layerRows = new List<LayerRow>();
            ClearLayerScope();
            RefreshChips();
        });

        /// <summary>Opens the notes editor. folderFile = null when the drawing was never saved.</summary>
        public void ShowNotes(string folderText, string drawingText, string folderFile) => Ui(() =>
        {
            if (_notesPanel.Visibility == Visibility.Visible) { _notesPanel.Visibility = Visibility.Collapsed; return; }
            _notesFolder.Text = folderText ?? "";
            _notesDrawing.Text = drawingText ?? "";
            _notesFolder.IsEnabled = folderFile != null;
            _notesFolderLabel.Text = folderFile != null
                ? "Dự án — dùng chung cho mọi bản vẽ trong thư mục (file " + System.IO.Path.GetFileName(folderFile) + " cạnh bản vẽ)"
                : "Dự án — lưu bản vẽ trước để có ghi chú chung cho cả thư mục";
            _notesPanel.Visibility = Visibility.Visible;
            (folderFile != null ? _notesFolder : _notesDrawing).Focus();
        });

        public void SetNotesIndicator(bool any) => Ui(() =>
        {
            MarkIcon(_notesBtn, any ? "đang có ghi chú" : null,
                "Ghi chú dự án — gửi kèm mọi tin nhắn (cao độ, chiều dày tường, khung tên…)");
        });

        // ---------------------------------------------------------------- @ names

        private void UpdateMentionPopup()
        {
            if (!Mentions.TryGetToken(_input.Text, _input.CaretIndex, out var start, out var q))
            {
                _mentionPopup.IsOpen = false;
                _mentionCache = null;
                return;
            }
            if (_mentionCache == null || start != _mentionStart)
            {
                try { _mentionCache = MentionSource?.Invoke() ?? new List<MentionItem>(); }
                catch (Exception ex) { Logger.Warn("@ names unavailable: " + ex.Message); _mentionCache = new List<MentionItem>(); }
            }
            _mentionStart = start;
            _mentionList.Items.Clear();
            foreach (var m in Mentions.Filter(_mentionCache, q))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = m.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
                row.Children.Add(MutedText(m.KindLabel));
                _mentionList.Items.Add(new ListBoxItem { Content = row, Tag = m });
            }
            if (_mentionList.Items.Count == 0) { _mentionPopup.IsOpen = false; return; }
            _mentionList.SelectedIndex = 0;
            _mentionPopup.IsOpen = true;
        }

        private void AcceptMention()
        {
            if (!(_mentionList.SelectedItem is ListBoxItem item) || !(item.Tag is MentionItem m)) return;
            _mentionPopup.IsOpen = false;
            if (!Mentions.TryGetToken(_input.Text, _input.CaretIndex, out var start, out _)) return;
            var text = Mentions.Insert(_input.Text, start, _input.CaretIndex, m.Name, out var caret);
            _mentionCache = null;
            _input.Text = text;
            _input.CaretIndex = Math.Min(caret, text.Length);
            _input.Focus();
        }

        // ---------------------------------------------------------------- macros

        public void SetMacros(List<Macro> macros) => Ui(() =>
        {
            _macros = macros ?? new List<Macro>();
            MarkIcon(_macrosBtn, _macros.Count > 0 ? $"đã lưu {_macros.Count}" : null,
                "Macro — chạy lại các lượt vẽ đã lưu, không cần Claude (không tốn token)");
            RefreshMacrosPanel();
        });

        private void RefreshMacrosPanel()
        {
            _macroRows.Children.Clear();
            if (_macros.Count == 0)
            {
                var t = MutedText("Chưa có macro. Sau một lượt vẽ ưng ý, bấm \"Lưu macro\" dưới câu trả lời của Claude.");
                t.TextWrapping = TextWrapping.Wrap;
                _macroRows.Children.Add(t);
                return;
            }
            foreach (var m in _macros)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                var name = m.Name;
                var run = SmallButton("▶ Chạy", "Vẽ lại đúng như lúc lưu (cùng tọa độ)");
                run.Tag = "run";
                run.IsEnabled = !_busy;
                run.Click += (s, e) => { _macrosPopup.IsOpen = false; MacroRunRequested?.Invoke(name, false); };
                var at = SmallButton("📍 Tại điểm", m.BasePoint != null ? "Chọn điểm chèn trên bản vẽ: macro được dời tới đó" : "Macro này không có điểm gốc (không tạo đối tượng mới)");
                at.Tag = m.BasePoint != null ? "run" : null; // SetBusy must not enable it for macros without a base point
                at.IsEnabled = !_busy && m.BasePoint != null;
                at.Click += (s, e) => { _macrosPopup.IsOpen = false; MacroRunRequested?.Invoke(name, true); };
                var del = SmallButton("Xóa", "Xóa macro");
                del.Margin = new Thickness(0);
                del.Click += (s, e) => MacroDeleteRequested?.Invoke(name);
                buttons.Children.Add(run);
                buttons.Children.Add(at);
                buttons.Children.Add(del);
                DockPanel.SetDock(buttons, Dock.Right);
                row.Children.Add(buttons);
                var info = new StackPanel();
                var title = new TextBlock { Text = m.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
                Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
                info.Children.Add(title);
                var d = MutedText(MacroLogic.Describe(m) + " · " + m.Created.ToString("dd/MM/yyyy"));
                d.FontSize = 11;
                d.ToolTip = string.IsNullOrEmpty(m.Description) ? null : m.Description;
                info.Children.Add(d);
                row.Children.Add(info);
                _macroRows.Children.Add(row);
            }
        }

        // ---------------------------------------------------------------- version history

        public void SetVersions(List<VersionInfo> versions) => Ui(() =>
        {
            _versions = versions ?? new List<VersionInfo>();
            MarkIcon(_versionsBtn, _versions.Count > 0 ? $"có {_versions.Count} bản" : null,
                "Phiên bản — bản sao bản vẽ trước khi chat sửa (30 phút/lần, giữ 10 bản): mở ra xem, Đóng để về bản hiện tại");
            RefreshVersionsPanel();
        });

        private void RefreshVersionsPanel()
        {
            _versionRows.Children.Clear();
            if (_versions.Count == 0)
            {
                var t = MutedText("Chưa có phiên bản nào. Lần đầu chat sửa bản vẽ này, plugin sẽ lưu một bản sao trước khi sửa.");
                t.TextWrapping = TextWrapping.Wrap;
                _versionRows.Children.Add(t);
                return;
            }
            foreach (var v in _versions)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var id = v.Id;
                var open = SmallButton("Xem", "Mở bản sao này trong một tab chỉ xem; bản vẽ hiện tại không bị đụng tới");
                open.Margin = new Thickness(0);
                open.VerticalAlignment = VerticalAlignment.Top;
                open.Click += (s, e) => { _versionsPopup.IsOpen = false; VersionOpenRequested?.Invoke(id); };
                DockPanel.SetDock(open, Dock.Right);
                row.Children.Add(open);
                var info = new StackPanel();
                var title = new TextBlock { Text = v.Time.ToString("HH:mm  dd/MM/yyyy"), FontWeight = FontWeights.SemiBold };
                Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
                info.Children.Add(title);
                var label = MutedText("Trước lượt: " + (string.IsNullOrEmpty(v.Label) ? "(không rõ)" : v.Label) + (v.Bytes > 0 ? $" · {v.Bytes / 1048576.0:0.0} MB" : ""));
                label.FontSize = 11;
                label.TextWrapping = TextWrapping.Wrap;
                info.Children.Add(label);
                row.Children.Add(info);
                _versionRows.Children.Add(row);
            }
        }

        /// <summary>An old version is the active drawing: show the banner and block sending.</summary>
        public void ShowVersionBanner(string text) => Ui(() =>
        {
            _viewingVersion = true;
            _versionBannerText.Text = "👁 " + text;
            _versionBanner.Visibility = Visibility.Visible;
            _send.IsEnabled = false;
            foreach (var b in _openConfirmButtons) b.IsEnabled = false; // plan / confirm cards wait for the real drawing
        });

        public void HideVersionBanner() => Ui(() =>
        {
            _viewingVersion = false;
            _versionBanner.Visibility = Visibility.Collapsed;
            _send.IsEnabled = true;
            foreach (var b in _openConfirmButtons) b.IsEnabled = true;
        });

        // ---------------------------------------------------------------- screenshot of the view

        public void AddImagePng(byte[] png) => Ui(() =>
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            using (var ms = new System.IO.MemoryStream(png))
            {
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            AddImage(ImageAttachment.FromBitmap(bmp));
            _toolStatus.Text = "Đã chụp vùng đang xem — gõ yêu cầu (ví dụ \"chỗ này sai, sửa lại\") rồi gửi.";
            _input.Focus();
        });

        // ---------------------------------------------------------------- toasts

        public enum ToastKind { Info, Success, Warning, Error }

        /// <summary>
        /// Small notice floating over the chat that disappears by itself (click to close). At most 3 at once;
        /// the same text is not stacked twice. action/actionText: optional button (e.g. "Xem").
        /// </summary>
        public void ShowToast(string text, ToastKind kind = ToastKind.Info, double seconds = 4, string actionText = null, Action action = null) => Ui(() =>
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var old in _toasts.Children.OfType<Border>().ToList())
                if ((old.Tag as string) == text) _toasts.Children.Remove(old);
            while (_toasts.Children.Count >= 3) _toasts.Children.RemoveAt(0);
            string icon = kind == ToastKind.Success ? "✓" : kind == ToastKind.Warning ? "⚠" : kind == ToastKind.Error ? "✕" : "ℹ";
            var row = new DockPanel();
            if (action != null && !string.IsNullOrEmpty(actionText))
            {
                var b = SmallButton(actionText, actionText);
                b.Margin = new Thickness(8, 0, 0, 0);
                b.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
                b.Click += (s, e) => action();
            }
            var t = new TextBlock { Text = icon + "  " + text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            Theme.Ref(t, TextBlock.ForegroundProperty, kind == ToastKind.Error ? Theme.ErrorFg : Theme.Fg);
            row.Children.Add(t);
            var toast = new Border
            {
                Child = row, Tag = text, BorderThickness = new Thickness(1, 1, 1, 1), CornerRadius = Theme.Corner,
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6), Opacity = 0, Cursor = Cursors.Hand,
                ToolTip = "Bấm để đóng",
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.35 },
            };
            Theme.Ref(toast, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(toast, Border.BorderBrushProperty, kind == ToastKind.Error || kind == ToastKind.Warning ? Theme.ErrorFg : Theme.Accent);
            void Remove()
            {
                var fade = new System.Windows.Media.Animation.DoubleAnimation(toast.Opacity, 0, TimeSpan.FromMilliseconds(250));
                fade.Completed += (s, e) => _toasts.Children.Remove(toast);
                toast.BeginAnimation(OpacityProperty, fade);
            }
            toast.MouseLeftButtonUp += (s, e) => Remove();
            _toasts.Children.Add(toast);
            toast.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1.5, seconds)) };
            timer.Tick += (s, e) =>
            {
                if (toast.IsMouseOver) return; // keep it while the user reads it
                timer.Stop();
                Remove();
            };
            timer.Start();
        });

        // ---------------------------------------------------------------- usage limit card

        /// <summary>
        /// The Claude plan's usage limit was reached: what limit, when it resets, buttons "Tự gửi lại lúc …"
        /// (sends the last request again when the limit resets) and "Dùng API key".
        /// </summary>
        public void AddLimitCard(string title, string detail, DateTime? resetsAt, bool canAutoRetry, bool autoRetryOn) => Ui(() =>
        {
            var stack = new StackPanel();
            stack.Children.Add(IconLine("⊗", Theme.ErrorFg, title, bold: true));
            SetCompactNote("⊗ " + FirstLine(title, 50));
            if (!string.IsNullOrEmpty(detail))
            {
                var d = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
                Theme.Ref(d, TextBlock.ForegroundProperty, Theme.Fg);
                stack.Children.Add(d);
            }
            var countdown = MutedText("");
            countdown.FontSize = 11;
            stack.Children.Add(countdown);
            if (resetsAt.HasValue)
            {
                void Tick()
                {
                    var left = resetsAt.Value - DateTime.Now;
                    countdown.Text = left <= TimeSpan.Zero ? "Đã có thể dùng lại." : $"Còn khoảng {(left.TotalHours >= 1 ? $"{(int)left.TotalHours} giờ {left.Minutes} phút" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))} phút")}.";
                }
                Tick();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                timer.Tick += (s, e) =>
                {
                    Tick();
                    if (resetsAt.Value <= DateTime.Now) { timer.Stop(); _cardTimers.Remove(timer); }
                };
                timer.Start();
                _cardTimers.Add(timer);
            }
            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            if (canAutoRetry && resetsAt.HasValue)
            {
                var auto = new ToggleButton
                {
                    Content = autoRetryOn ? $"✓ Sẽ tự gửi lại lúc {resetsAt.Value:HH:mm}" : $"Tự gửi lại lúc {resetsAt.Value:HH:mm}",
                    IsChecked = autoRetryOn, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4),
                    ToolTip = "Khi hạn mức được làm mới, plugin tự gửi lại yêu cầu vừa rồi (cứ để AutoCAD mở)",
                };
                auto.Checked += (s, e) => { auto.Content = $"✓ Sẽ tự gửi lại lúc {resetsAt.Value:HH:mm}"; LimitAutoRetryToggled?.Invoke(true); };
                auto.Unchecked += (s, e) => { auto.Content = $"Tự gửi lại lúc {resetsAt.Value:HH:mm}"; LimitAutoRetryToggled?.Invoke(false); };
                row.Children.Add(auto);
            }
            var key = new Button { Content = "Dùng API key", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4), ToolTip = "API key Anthropic trả theo lượng dùng, không phụ thuộc hạn mức gói" };
            key.Click += (s, e) => ShowApiKeyPanel(ApiKeyMask?.Invoke());
            row.Children.Add(key);
            stack.Children.Add(row);
            var card = NoticeCard(stack, "limit");
            AppendMessage(card);
            ScrollToEnd();
        });

        // ---------------------------------------------------------------- account

        public void SetAccountStatus(string text) => Ui(() =>
        {
            if (_accountStatus == null) return;
            _accountStatus.Text = text ?? "";
            // Not signed in is the one time the account group matters: open it so the buttons are in view.
            if ((text ?? "").IndexOf("chưa đăng nhập", StringComparison.OrdinalIgnoreCase) >= 0) _accountCat?.Expand();
        });

        private Border BuildApiKeyPanel()
        {
            var p = new StackPanel { Margin = new Thickness(8) };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var close = SmallButton("Đóng", "Đóng");
            close.Margin = new Thickness(0);
            var clear = SmallButton("Xóa key đã lưu", "Bỏ API key đã lưu: quay lại dùng tài khoản Claude đã đăng nhập");
            var save = SmallButton("Lưu", "Lưu key (mã hóa theo tài khoản Windows) và khởi động lại trợ lý");
            foreach (var b in new[] { close, clear, save }) { DockPanel.SetDock(b, Dock.Right); head.Children.Add(b); }
            var title = new TextBlock { Text = "Dùng API key Anthropic", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            head.Children.Add(title);
            p.Children.Add(head);
            var hint = MutedText("Tạo key ở console.anthropic.com (trả theo lượng dùng, không dùng hạn mức gói Pro/Max). Key được lưu mã hóa cho riêng tài khoản Windows này và được ưu tiên hơn tài khoản Claude đã đăng nhập.");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.FontSize = 11;
            p.Children.Add(hint);
            _apiKeyBox = new PasswordBox { Margin = new Thickness(0, 6, 0, 4), Padding = new Thickness(2), FontSize = 12 };
            Theme.Ref(_apiKeyBox, BackgroundProperty, Theme.InputBg);
            Theme.Ref(_apiKeyBox, ForegroundProperty, Theme.Fg);
            Theme.Ref(_apiKeyBox, BorderBrushProperty, Theme.Border);
            p.Children.Add(_apiKeyBox);
            _apiKeyInfo = MutedText("");
            _apiKeyInfo.FontSize = 11;
            p.Children.Add(_apiKeyInfo);
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
            Theme.Ref(border, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);
            void Close() { _apiKeyBox.Clear(); border.Visibility = Visibility.Collapsed; }
            close.Click += (s, e) => Close();
            clear.Click += (s, e) => { Close(); ApiKeyClearRequested?.Invoke(); };
            save.Click += (s, e) =>
            {
                var key = _apiKeyBox.Password.Trim();
                if (key.Length < 20) { _apiKeyInfo.Text = "Key chưa đúng (thường bắt đầu bằng sk-ant-)."; return; }
                Close();
                ApiKeySaveRequested?.Invoke(key);
            };
            _apiKeyBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); } };
            return border;
        }

        /// <summary>Opens the API key editor; savedMask = "sk-ant-…AbCd" when a key is already saved.</summary>
        public void ShowApiKeyPanel(string savedMask = null) => Ui(() =>
        {
            _apiKeyInfo.Text = string.IsNullOrEmpty(savedMask) ? "Chưa có key nào được lưu." : "Đang dùng key đã lưu: " + savedMask;
            _apiKeyPanel.Visibility = Visibility.Visible;
            _apiKeyBox.Focus();
        });

        // ---------------------------------------------------------------- cards: login, welcome

        /// <summary>Not signed in to Claude: one button opens the sign-in (browser).</summary>
        public void AddLoginCard(string text) => Ui(() =>
        {
            var stack = new StackPanel();
            ExpandForDecision();
            stack.Children.Add(IconLine("⚠", Theme.WarnFg, text));
            var b = new Button { Content = "Đăng nhập Claude", ToolTip = "Mở trình duyệt để đăng nhập tài khoản Claude Pro / Max; xong là gửi tin nhắn được ngay", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (s, e) => LoginRequested?.Invoke();
            stack.Children.Add(b);
            AppendMessage(NoticeCard(stack));
            ScrollToEnd();
        });

        /// <summary>Example request of step 3 of the "Bắt đầu" card: drawn on a scratch drawing by the self-test too.</summary>
        public const string WelcomeExample = "Vẽ lưới trục 5x4 bước 4000";

        /// <summary>
        /// First start after installing (screen C): "Bắt đầu với ClaudeCAD — 3 bước" with numbered steps
        /// Kích hoạt → Đăng nhập Claude → Thử một yêu cầu mẫu. A finished step shows ✓, the step to do now
        /// has its number in the accent color and its buttons, a later step is muted. The other checks
        /// (Node.js, sidecar, skill) stay out of the way: one muted line, or a ⊗ line when one is missing.
        /// </summary>
        public void AddWelcomeCard(List<(string name, bool? ok, string detail)> checks, bool needLogin) => Ui(() =>
        {
            (string name, bool? ok, string detail) Find(string name) => checks.FirstOrDefault(c => c.name == name);
            var act = Find("Kích hoạt");
            var acc = Find("Tài khoản Claude");
            bool activated = act.name == null || act.ok != false;
            bool signedIn = !needLogin;

            var stack = new StackPanel();
            var head = new TextBlock { TextWrapping = TextWrapping.Wrap };
            head.Inlines.Add(new System.Windows.Documents.Run("Bắt đầu với ClaudeCAD") { FontWeight = FontWeights.SemiBold });
            var sub = new System.Windows.Documents.Run(" — 3 bước, khoảng 2 phút") { FontSize = 11 };
            sub.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, Theme.MutedFg);
            head.Inlines.Add(sub);
            Theme.Ref(head, TextBlock.ForegroundProperty, Theme.Fg);
            var headBar = new Border { Child = head, Padding = new Thickness(8, 5, 8, 5), BorderThickness = new Thickness(0, 0, 0, 1) };
            Theme.Ref(headBar, Border.BackgroundProperty, Theme.HeaderBg);
            Theme.Ref(headBar, Border.BorderBrushProperty, Theme.Border);
            stack.Children.Add(headBar);

            // a missing part of the plugin comes first: nothing below works without it
            var others = checks.Where(c => c.name != "Kích hoạt" && c.name != "Tài khoản Claude").ToList();
            foreach (var (name, ok, detail) in others.Where(c => c.ok == false))
            {
                var bad = IconLine("⊗", Theme.ErrorFg, name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
                bad.Margin = new Thickness(8, 6, 8, 0);
                stack.Children.Add(bad);
            }

            // 1. activation
            var s1 = WelcomeStep(1, activated ? "done" : "now", "Kích hoạt máy này",
                activated ? (string.IsNullOrEmpty(act.detail) ? "Đã kích hoạt" : "Đã kích hoạt · " + act.detail) : "Máy này chưa có key kích hoạt.", true);
            stack.Children.Add(s1.root);

            // 2. sign-in
            var s2 = WelcomeStep(2, signedIn ? "done" : activated ? "now" : "later", "Đăng nhập Claude",
                signedIn ? (string.IsNullOrEmpty(acc.detail) ? "Đã đăng nhập" : acc.detail)
                         : "Trình duyệt mở trang cấp quyền, bấm đồng ý bằng tài khoản Pro / Max.", true);
            if (!signedIn && activated)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                var login = AccentButton("Đăng nhập Claude", "Mở trình duyệt để đăng nhập tài khoản Claude Pro / Max; xong là gửi tin nhắn được ngay");
                login.Click += (s, e) => LoginRequested?.Invoke();
                row.Children.Add(login);
                var key = SmallButton("Dùng API key", "Dán API key Anthropic (trả theo lượng dùng) thay cho tài khoản Pro/Max");
                key.Click += (s, e) => ShowApiKeyPanel(ApiKeyMask?.Invoke());
                row.Children.Add(key);
                s2.body.Children.Add(row);
            }
            stack.Children.Add(s2.root);

            // 3. first request
            bool ready = activated && signedIn;
            var s3 = WelcomeStep(3, ready ? "now" : "later", "Thử một yêu cầu mẫu",
                $"\"{WelcomeExample}\" — Claude vẽ ngay trên bản vẽ đang mở; hoàn tác bằng 1 lần U.", false);
            var row3 = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            if (ready)
            {
                var tryIt = AccentButton("Điền vào ô nhập", "Đưa câu mẫu vào ô nhập để cậu xem lại rồi bấm Enter");
                tryIt.Click += (s, e) => SetInputText(WelcomeExample);
                row3.Children.Add(tryIt);
            }
            var test = SmallButton("Chạy tự kiểm tra", "CLAUDECHATSELFTEST: thử trên một bản vẽ tạm, mở báo cáo ✓/✗");
            test.Click += (s, e) => SelfTestRequested?.Invoke();
            row3.Children.Add(test);
            s3.body.Children.Add(row3);
            stack.Children.Add(s3.root);

            var okParts = others.Where(c => c.ok != false).Select(c => string.IsNullOrEmpty(c.detail) ? c.name : c.name + " " + c.detail);
            var hint = MutedText((okParts.Any() ? "Plugin: " + string.Join(" · ", okParts) + ". " : "")
                + "Mở nhanh khung chat: Ctrl+Shift+Space hoặc lệnh CC. Xem lại: ⚙ ▸ Hướng dẫn bắt đầu.");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.TextTrimming = TextTrimming.None;
            hint.FontSize = 10;
            hint.Margin = new Thickness(8, 4, 8, 6);
            stack.Children.Add(hint);

            var card = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Margin = new Thickness(0, 4, 0, 4), Tag = "welcome", Effect = Theme.Shadow() };
            Theme.Ref(card, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(card, Border.BorderBrushProperty, Theme.Border);
            AppendMessage(card);
            ScrollToEnd();
        });

        /// <summary>
        /// One numbered row of the "Bắt đầu" card. <paramref name="state"/>: "done" (✓ green), "now" (number in
        /// the accent color, title in full color), "later" (all muted). Buttons go into the returned body.
        /// </summary>
        private (Border root, StackPanel body) WelcomeStep(int n, string state, string title, string detail, bool divider)
        {
            var mark = new TextBlock
            {
                Text = state == "done" ? "✓" : n.ToString(),
                Width = 18,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = "step-" + state,
            };
            Theme.Ref(mark, TextBlock.ForegroundProperty, state == "done" ? Theme.OkFg : state == "now" ? Theme.Accent : Theme.MutedFg);
            var body = new StackPanel();
            var t = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, state == "later" ? Theme.MutedFg : Theme.Fg);
            body.Children.Add(t);
            var d = MutedText(detail);
            d.TextWrapping = TextWrapping.Wrap;
            d.TextTrimming = TextTrimming.None;
            d.FontSize = 11;
            d.Margin = new Thickness(0, 1, 0, 0);
            body.Children.Add(d);
            var line = new DockPanel();
            DockPanel.SetDock(mark, Dock.Left);
            line.Children.Add(mark);
            line.Children.Add(body);
            var root = new Border { Child = line, Padding = new Thickness(8, 7, 8, 7), BorderThickness = new Thickness(0, 0, 0, divider ? 1 : 0) };
            Theme.Ref(root, Border.BorderBrushProperty, Theme.Border);
            return (root, body);
        }

        /// <summary>The one accent button of a card (Đồng ý, Đăng nhập Claude…), same look as the confirmation card.</summary>
        private static Button AccentButton(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4), FontWeight = FontWeights.SemiBold, FontSize = 11, Cursor = Cursors.Hand };
            Theme.Ref(b, BackgroundProperty, Theme.PressBg);
            Theme.Ref(b, ForegroundProperty, Theme.AccentFg);
            Theme.Ref(b, BorderBrushProperty, Theme.Accent);
            return b;
        }

        // ---------------------------------------------------------------- card: activation (v0.9.4)

        private TextBlock _activationStatus;
        private TextBox _activationKey;
        private Button _activationGo, _activationAsk;
        private Border _activationCard;
        private bool _activationDone;

        /// <summary>
        /// This machine has no licence: machine code (copy), the owner's contact, "Gửi yêu cầu kích hoạt" (emails the
        /// owner a key) and a box for the key the owner sends back. Only the newest card is live.
        /// </summary>
        public void AddActivationCard(string machine, string device, string contact, string status) => Ui(() =>
        {
            ExpandForDecision();
            // one live card: pressing Enter again while not activated brings the existing card back into view
            if (_activationCard != null && _activationCard.Parent != null && !_activationDone)
            {
                _activationCard.BringIntoView();
                _activationKey?.Focus();
                return;
            }
            _activationDone = false;
            var stack = new StackPanel();
            var title = new TextBlock { Text = "🔒 Kích hoạt ClaudeCAD", FontWeight = FontWeights.SemiBold, FontSize = FontSize + 1 };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(title);
            var intro = new TextBlock { Text = "Máy này cần key kích hoạt để dùng plugin. " + contact + " để nhận key, rồi dán key vào ô dưới.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            Theme.Ref(intro, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(intro);

            var codeRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var code = new TextBox { Text = machine, IsReadOnly = true, Focusable = true, FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.SemiBold, Padding = new Thickness(4, 1, 4, 1), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Mã máy: gửi mã này cho chủ plugin" };
            Theme.Ref(code, BackgroundProperty, Theme.InputBg);
            Theme.Ref(code, ForegroundProperty, Theme.Fg);
            Theme.Ref(code, BorderBrushProperty, Theme.Border);
            var copy = SmallButton("Sao chép", "Sao chép mã máy");
            copy.Margin = new Thickness(6, 0, 0, 0);
            copy.VerticalAlignment = VerticalAlignment.Center;
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText($"Mã máy ClaudeCAD: {machine} ({device})"); copy.Content = "✓ Đã chép"; }
                catch { code.SelectAll(); code.Focus(); }
            };
            var label = MutedText("Mã máy: ");
            codeRow.Children.Add(label);
            codeRow.Children.Add(code);
            codeRow.Children.Add(copy);
            stack.Children.Add(codeRow);
            var dev = MutedText("Thiết bị: " + device);
            dev.FontSize = 11;
            stack.Children.Add(dev);

            var keyRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = true };
            var go = new Button { Content = "Kích hoạt", ToolTip = "Kiểm tra key cho máy này rồi mở khung chat (key vĩnh viễn, cập nhật không hỏi lại)", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold };
            DockPanel.SetDock(go, Dock.Right);
            keyRow.Children.Add(go);
            var key = new TextBox { Padding = new Thickness(3, 2, 3, 2), FontFamily = new FontFamily("Consolas"), ToolTip = "Key dạng XXXX-XXXX-XXXX-XXXX (hoặc mã kích hoạt offline bắt đầu bằng CCL1|)" };
            Theme.Ref(key, BackgroundProperty, Theme.InputBg);
            Theme.Ref(key, ForegroundProperty, Theme.Fg);
            Theme.Ref(key, BorderBrushProperty, Theme.Border);
            keyRow.Children.Add(key);
            stack.Children.Add(keyRow);

            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var ask = new Button { Content = "Gửi yêu cầu kích hoạt", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4), ToolTip = "Gửi mã máy và tên thiết bị tới email của chủ plugin" };
            buttons.Children.Add(ask);
            stack.Children.Add(buttons);
            var st = MutedText(status ?? "");
            st.TextWrapping = TextWrapping.Wrap;
            st.TextTrimming = TextTrimming.None;
            st.FontSize = 11;
            stack.Children.Add(st);

            void Submit()
            {
                var k = key.Text.Trim();
                if (k.Length == 0) { SetActivationStatus("Dán key chủ plugin gửi cho cậu vào ô trên.", true); return; }
                go.IsEnabled = false;
                SetActivationStatus("Đang kích hoạt...", false);
                ActivationKeySubmitted?.Invoke(k);
            }
            go.Click += (s, e) => Submit();
            key.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit(); } };
            ask.Click += (s, e) => { ask.IsEnabled = false; SetActivationStatus("Đang gửi yêu cầu...", false); ActivationRequestRequested?.Invoke(); };

            ForwardWheel(code);
            ForwardWheel(key);
            _activationStatus = st;
            _activationKey = key;
            _activationGo = go;
            _activationAsk = ask;
            var card = Card(stack, "activation");
            _activationCard = card;
            card.Margin = new Thickness(0, 4, 0, 4);
            card.Effect = Theme.Shadow();
            AppendMessage(card);
            ScrollToEnd();
            key.Focus();
        });

        /// <summary>Status line of the activation card; done = activated (inputs locked).</summary>
        public void SetActivationStatus(string text, bool error, bool done = false) => Ui(() =>
        {
            if (_activationStatus == null) return;
            _activationStatus.Text = text ?? "";
            Theme.Ref(_activationStatus, TextBlock.ForegroundProperty, error ? Theme.ErrorFg : done ? Theme.Accent : Theme.MutedFg);
            if (_activationGo != null) _activationGo.IsEnabled = !done;
            if (_activationAsk != null) _activationAsk.IsEnabled = !done;
            if (_activationKey != null) _activationKey.IsReadOnly = done;
            if (done) _activationDone = true;
        });

        // ---------------------------------------------------------------- cards: skill draft, plan

        private Border Card(UIElement content, string tag)
        {
            var b = new Border { Child = content, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 0, 4), Tag = tag };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Accent);
            return b;
        }

        private void PlaceCard(Border b)
        {
            if (_current != null) _current.AddCard(b);
            else AppendMessage(b);
            ScrollToEnd();
        }

        /// <summary>Claude drafted a SKILL.md from the conversation: review it, then add it to the plugin.</summary>
        public void AddSkillDraftCard(string name, string description, string file) => Ui(() =>
        {
            var stack = new StackPanel();
            var t = new TextBlock { Text = "Bản nháp skill: " + name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(t);
            if (!string.IsNullOrEmpty(description))
            {
                var d = MutedText(description);
                d.TextWrapping = TextWrapping.Wrap;
                d.FontSize = 11;
                stack.Children.Add(d);
            }
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var open = new Button { Content = "Xem / sửa", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Mở SKILL.md bản nháp để xem và sửa trước khi thêm" };
            var add = new Button { Content = "Thêm vào plugin", Padding = new Thickness(10, 2, 10, 2), FontWeight = FontWeights.SemiBold, ToolTip = "Thêm skill này (như + Add skill)" };
            var status = MutedText("");
            open.Click += (s, e) => SkillDraftOpenRequested?.Invoke(file);
            add.Click += (s, e) => { add.IsEnabled = false; status.Text = "  → đã gửi yêu cầu thêm"; SkillDraftAddRequested?.Invoke(file); };
            row.Children.Add(open);
            row.Children.Add(add);
            row.Children.Add(status);
            stack.Children.Add(row);
            PlaceCard(Card(stack, "skill-draft"));
        });

        /// <summary>Plan card ("Làm" / "Sửa kế hoạch" / "Bỏ") before a big multi-step task.</summary>
        public void AddPlanCard(string id, string summary, List<string> steps) => Ui(() =>
        {
            ExpandForDecision();
            var stack = new StackPanel();
            var t = new TextBlock { Text = "Kế hoạch: " + summary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(t);
            int i = 0;
            foreach (var step in steps ?? new List<string>())
            {
                var st = new TextBlock { Text = $"{++i}. {step}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 2, 0, 0) };
                Theme.Ref(st, TextBlock.ForegroundProperty, Theme.Fg);
                stack.Children.Add(st);
            }
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var go = new Button { Content = "Làm", Padding = new Thickness(14, 2, 14, 2), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold, ToolTip = "Claude thực hiện đúng kế hoạch này" };
            var edit = new Button { Content = "Sửa kế hoạch", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Gõ điều cần đổi, Claude lập lại kế hoạch" };
            var drop = new Button { Content = "Bỏ", Padding = new Thickness(10, 2, 10, 2), ToolTip = "Không làm" };
            var answer = MutedText("");
            void Done(string kind, string label)
            {
                go.IsEnabled = edit.IsEnabled = drop.IsEnabled = false;
                _openConfirmButtons.Remove(go);
                _openConfirmButtons.Remove(edit);
                _openConfirmButtons.Remove(drop);
                answer.Text = "  → " + label;
                PlanAnswered?.Invoke(id, kind);
            }
            go.Click += (s, e) => Done("go", "làm theo kế hoạch");
            edit.Click += (s, e) => Done("edit", "sửa kế hoạch");
            drop.Click += (s, e) => Done("cancel", "đã bỏ");
            _openConfirmButtons.Add(go);
            _openConfirmButtons.Add(edit);
            _openConfirmButtons.Add(drop);
            row.Children.Add(go);
            row.Children.Add(edit);
            row.Children.Add(drop);
            row.Children.Add(answer);
            stack.Children.Add(row);
            PlaceCard(Card(stack, "plan"));
        });

        // ================================================================= one assistant turn

        private sealed class AssistantView
        {
            private readonly ChatControl _owner;
            private readonly StringBuilder _text = new StringBuilder();
            private readonly MarkdownView _md = new MarkdownView();
            private readonly Category _toolsCat;
            private readonly StackPanel _toolRows = new StackPanel();
            private readonly List<(ToolLogEntry entry, TextBlock row)> _tools = new List<(ToolLogEntry, TextBlock)>();
            private readonly WrapPanel _actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            private readonly StackPanel _cards = new StackPanel();
            private readonly StackPanel _changes = new StackPanel();
            private readonly StackPanel _prompts = new StackPanel();
            private readonly string _userText;
            private readonly DispatcherTimer _renderTimer;
            private Button _undo;
            private bool _dirty;

            public Border Root { get; }

            public AssistantView(ChatControl owner)
            {
                _owner = owner;
                _userText = owner._lastUserText ?? "";
                _md.FontSize = owner.FontSize;
                _md.HandleLinkClicked += h => owner.ZoomRequested?.Invoke(h);
                _toolsCat = new Category("Thao tác", _toolRows, expanded: false) { Visibility = Visibility.Collapsed };
                var stack = new StackPanel();
                stack.Children.Add(SpeakerLabel("CLAUDE"));
                _planRows = new StackPanel();
                _planBar = new Grid { Height = 3, Margin = new Thickness(0, 0, 0, 3) };
                _planBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
                _planBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var fill = new Border();
                Theme.Ref(fill, Border.BackgroundProperty, Theme.Accent);
                var rest = new Border();
                Theme.Ref(rest, Border.BackgroundProperty, Theme.Border);
                Grid.SetColumn(rest, 1);
                _planBar.Children.Add(fill);
                _planBar.Children.Add(rest);
                var planBody = new StackPanel();
                planBody.Children.Add(_planBar);
                planBody.Children.Add(_planRows);
                _planCat = new Category("Bước", planBody, expanded: true) { Visibility = Visibility.Collapsed };
                stack.Children.Add(_planCat.Root);
                stack.Children.Add(_toolsCat.Root);
                stack.Children.Add(_md);
                stack.Children.Add(_cards);
                stack.Children.Add(_changes);
                stack.Children.Add(_actions);
                stack.Children.Add(_prompts);
                // Flat, full width, a thin rule under it: a palette list entry, not a card.
                Root = new Border
                {
                    CornerRadius = Theme.Corner,
                    Padding = new Thickness(8, 4, 8, 5),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = stack,
                    Tag = this,
                };
                Theme.Ref(Root, Border.BackgroundProperty, Theme.AssistantBg);
                Theme.Ref(Root, Border.BorderBrushProperty, Theme.Border);
                _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _renderTimer.Tick += (s, e) => { _renderTimer.Stop(); if (_dirty) { _dirty = false; _md.Markdown = _text.ToString(); } };
            }

            public void AddCard(UIElement card) => _cards.Children.Add(card);

            /// <summary>Number of tool calls of this turn so far (the busy line shows it).</summary>
            public int ToolCount => _tools.Count;

            public void Append(string delta)
            {
                _text.Append(delta);
                _dirty = true;
                if (!_renderTimer.IsEnabled) _renderTimer.Start(); // throttled re-render while streaming
            }

            public void Flush()
            {
                _renderTimer.Stop();
                _md.Markdown = _text.ToString();
                _dirty = false;
            }

            public void Rerender()
            {
                _md.FontSize = _owner.FontSize;
                _md.Render();
            }

            private bool _live, _settled;
            private readonly Category _planCat;
            private readonly StackPanel _planRows;
            private readonly Grid _planBar;
            private List<string> _planSteps;
            private int _planStep;
            private string _planNote;
            private bool? _planClean;

            /// <summary>Current step (1-based) and number of steps, 0 when the turn reports no steps.</summary>
            internal (int step, int total) Progress => (_planStep, _planSteps?.Count ?? 0);

            /// <summary>
            /// The approved plan as ✓ done / ● now / ○ not yet, with a 3 px bar of the finished steps. Only what
            /// Claude reported: the step it is starting, never a guessed percentage.
            /// </summary>
            public void SetProgress(List<string> steps, int step, string note)
            {
                if (_planClean != null) return; // the turn is over
                _planSteps = steps.ToList();
                _planStep = step;
                _planNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                if (_planCat.Visibility != Visibility.Visible) _toolsCat.Collapse(); // first report: the steps take over
                _planCat.Visibility = Visibility.Visible;
                _planCat.Expand();
                PaintPlan();
            }

            /// <summary>End of the turn: the step in progress is done if Claude finished cleanly, else marked unfinished.</summary>
            public void SettleProgress(bool clean)
            {
                if (_planSteps == null || _planClean != null) return;
                _planClean = clean;
                PaintPlan();
                _planCat.Collapse();
            }

            private void PaintPlan()
            {
                int n = _planSteps.Count;
                _planRows.Children.Clear();
                for (int i = 1; i <= n; i++)
                {
                    string name = _planSteps[i - 1];
                    string state, text, color;
                    if (i < _planStep || (i == _planStep && _planClean == true)) { state = "done"; text = "✓ " + name; color = Theme.OkFg; }
                    else if (i == _planStep && _planClean == false) { state = "stopped"; text = "○ " + name + " — dừng giữa chừng"; color = Theme.WarnFg; }
                    else if (i == _planStep) { state = "now"; text = "● " + name + (_planNote != null ? " — " + _planNote : ""); color = Theme.Fg; }
                    else { state = "later"; text = "○ " + name; color = Theme.MutedFg; }
                    var row = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(14, 0, 0, 1), Tag = "plan-" + state };
                    if (state == "now") row.FontWeight = FontWeights.SemiBold;
                    Theme.Ref(row, TextBlock.ForegroundProperty, color);
                    _planRows.Children.Add(row);
                }
                int done = _planStep - 1 + (_planClean == true ? 1 : 0);
                _planBar.ColumnDefinitions[0].Width = new GridLength(done, GridUnitType.Star);
                _planBar.ColumnDefinitions[1].Width = new GridLength(n - done, GridUnitType.Star);
                _planBar.ToolTip = $"{done}/{n} bước đã xong";
                string cur = n > 0 ? _planSteps[_planStep - 1] : "";
                _planCat.SetTitle(_planClean == null ? $"Bước {_planStep}/{n}: {cur}"
                    : _planClean == true ? $"Đã làm {done}/{n} bước" + (done < n ? $" · dừng sau bước {_planStep}" : "")
                    : $"Dừng ở bước {_planStep}/{n}: {cur}");
            }

            /// <summary>
            /// Tool activity (feature 12) as a step list (screen D): while the turn runs the group is open and
            /// reads "Đang làm · bước 3: Vẽ tường"; each step is one row, ✓ done, ● running, ⚠ error. The same
            /// tool called several times in a row is one step "×46", so a wall of LINE calls stays one line.
            /// Steps that have not started are not shown: the plugin does not know them, and no % is made up.
            /// </summary>
            public void AddTool(ToolLogEntry e)
            {
                _toolsCat.Visibility = Visibility.Visible;
                // with plan steps on screen the commands stay folded under them (screen D: "Xem chi tiết lệnh")
                if (!_live && !_settled) { _live = true; if (_planSteps == null) _toolsCat.Expand(); }
                TextBlock row = null;
                if (e.State != "running")
                {
                    var idx = _tools.FindIndex(t => t.entry.State == "running" && t.entry.Tool == e.Tool);
                    if (idx >= 0)
                    {
                        row = _tools[idx].row;
                        _tools[idx] = (e, row);
                    }
                }
                if (row == null)
                {
                    var last = _tools.Count > 0 ? _tools[_tools.Count - 1] : default;
                    if (last.row != null && last.entry.Tool == e.Tool && last.entry.State != "error") row = last.row;
                    else
                    {
                        row = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(14, 0, 0, 1) };
                        _toolRows.Children.Add(row);
                    }
                    _tools.Add((e, row));
                }
                Paint(row);
                UpdateHeader();
            }

            /// <summary>Number of steps (rows) so far.</summary>
            internal int StepCount => _toolRows.Children.Count;

            private void Paint(TextBlock row)
            {
                var group = _tools.Where(t => t.row == row).Select(t => t.entry).ToList();
                var e = group[group.Count - 1];
                string label = ToolLabels.Describe(e.Tool);
                string times = group.Count > 1 ? " ×" + group.Count : "";
                long ms = group.Sum(g => g.Ms);
                if (group.Any(g => g.State == "running"))
                {
                    row.Text = _settled ? $"○ {label}{times} — chưa xong" : $"● {label}{times} — đang chạy";
                    row.Tag = "running";
                    Theme.Ref(row, TextBlock.ForegroundProperty, _settled ? Theme.MutedFg : Theme.Fg);
                }
                else if (e.State == "error")
                {
                    row.Text = $"⚠ {label}{times} — {e.Error} ({ms} ms)";
                    row.Tag = "error";
                    Theme.Ref(row, TextBlock.ForegroundProperty, Theme.ErrorFg);
                }
                else
                {
                    row.Text = $"✓ {label}{times}{(string.IsNullOrEmpty(e.Summary) ? "" : " — " + e.Summary)} ({ms} ms)";
                    row.Tag = "done";
                    Theme.Ref(row, TextBlock.ForegroundProperty, Theme.OkFg);
                }
            }

            private void UpdateHeader()
            {
                if (_live)
                {
                    var cur = _tools.Count > 0 ? ToolLabels.Describe(_tools[_tools.Count - 1].entry.Tool) : "";
                    _toolsCat.SetTitle($"Đang làm · bước {StepCount}: {cur}");
                    return;
                }
                int errors = _tools.Count(t => t.entry.State == "error");
                long ms = _tools.Sum(t => t.entry.Ms);
                _toolsCat.SetTitle($"Thao tác ({_tools.Count}{(errors > 0 ? $", {errors} lỗi" : "")}) · {ms / 1000.0:0.0} s");
            }

            /// <summary>The turn is over (finished, stopped or failed): back to the folded "Thao tác (N)" summary.</summary>
            public void Settle()
            {
                if (_settled) return;
                _settled = true;
                bool wasLive = _live;
                _live = false;
                foreach (TextBlock row in _toolRows.Children) Paint(row);
                UpdateHeader();
                if (wasLive) _toolsCat.Collapse();
            }

            public void Finish(ChatEntry e, bool canUndo, bool canSaveMacro = false)
            {
                SettleProgress(true); // no-op when the controller already said how the turn ended
                Settle();
                _text.Clear();
                _text.Append(e.Text ?? "");
                Flush();
                _actions.Children.Clear();
                ShowChanges(e.Changes);
                if (e.Handles.Count > 0)
                {
                    var view = SmallButton($"Xem kết quả ({e.Handles.Count})", "Zoom tới và tô sáng các đối tượng vừa vẽ / sửa");
                    view.Click += (s, a) => _owner.ZoomRequested?.Invoke(e.Handles);
                    _actions.Children.Add(view);
                }
                _undo = SmallButton("Hoàn tác lượt này", "Hoàn tác toàn bộ thay đổi của lượt này (một lệnh U)");
                _undo.Click += (s, a) => { _undo.IsEnabled = false; _owner.UndoTurnRequested?.Invoke(); };
                _actions.Children.Add(_undo);
                SetUndo(canUndo);
                var copy = SmallButton("Copy", "Copy câu trả lời");
                copy.Click += (s, a) => SafeCopy(MarkdownLite.ToPlainText(e.Text ?? ""));
                _actions.Children.Add(copy);
                if (canSaveMacro)
                {
                    var save = SmallButton("💾 Lưu macro", "Lưu các thao tác vẽ của lượt này để chạy lại sau mà không cần Claude (không tốn token)");
                    save.Click += (s, a) =>
                    {
                        save.IsEnabled = false;
                        var first = _userText.Replace("\r", " ").Replace("\n", " ").Trim();
                        _prompts.Children.Add(_owner.InlinePrompt("Tên macro:", first.Length > 40 ? first.Substring(0, 40).TrimEnd() : first, "Lưu",
                            name => { save.Content = "✓ Đã lưu macro"; _owner.MacroSaveRequested?.Invoke(e, name); },
                            () => save.IsEnabled = true));
                    };
                    _actions.Children.Add(save);
                }
            }

            /// <summary>"Thêm 15 · Sửa 3 · Xóa 0" with one clickable row per kind and layer.</summary>
            private void ShowChanges(ChangeSummary c)
            {
                _changes.Children.Clear();
                if (c == null || c.IsEmpty) return;
                var box = new StackPanel();
                var head = new TextBlock { Text = "Thay đổi: " + c.Headline, FontWeight = FontWeights.SemiBold, FontSize = 11 };
                Theme.Ref(head, TextBlock.ForegroundProperty, Theme.Fg);
                box.Children.Add(head);
                foreach (var r in c.Rows.Take(8))
                {
                    var row = new TextBlock { Text = $"• {ChangeSummary.KindLabel(r.Kind)} {r.Count} trên {r.Layer}", FontSize = 11, Margin = new Thickness(6, 1, 0, 0) };
                    if (r.Kind != "erased" && r.Handles.Count > 0)
                    {
                        Theme.Ref(row, TextBlock.ForegroundProperty, Theme.LinkFg);
                        row.Cursor = Cursors.Hand;
                        row.ToolTip = "Bấm để xem trên bản vẽ";
                        var hs = r.Handles;
                        row.MouseLeftButtonUp += (s, a) => _owner.ZoomRequested?.Invoke(hs);
                    }
                    else Theme.Ref(row, TextBlock.ForegroundProperty, Theme.MutedFg);
                    box.Children.Add(row);
                }
                if (c.Rows.Count > 8)
                {
                    var more = MutedText($"… và {c.Rows.Count - 8} dòng nữa");
                    more.FontSize = 11;
                    more.Margin = new Thickness(6, 1, 0, 0);
                    box.Children.Add(more);
                }
                var b = new Border { Child = box, BorderThickness = new Thickness(1), CornerRadius = Theme.Corner, Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 4, 0, 0) };
                Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
                Theme.Ref(b, Border.BorderBrushProperty, Theme.Border);
                _changes.Children.Add(b);
            }

            public void SetUndo(bool available)
            {
                if (_undo == null) return;
                _undo.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                _undo.IsEnabled = available;
            }
        }
    }
}
