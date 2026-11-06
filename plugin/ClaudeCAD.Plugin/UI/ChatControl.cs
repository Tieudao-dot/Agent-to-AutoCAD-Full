using System;
using System.Collections.Generic;
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
        public event Action<string, bool> SkillToggled;
        public event Action<string> SkillEditRequested;
        public event Action<string> SkillDeleteRequested;
        public event Action OpenSkillsFolderRequested;
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
        public event Action CreateSkillFromChatRequested;
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
        private readonly ComboBox _standard, _scale;
        private readonly WrapPanel _chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        private readonly Popup _slashPopup;
        private readonly ListBox _slashList;
        private readonly Popup _settingsPopup;
        private readonly Popup _skillsPopup;
        private readonly StackPanel _skillRows = new StackPanel();
        private readonly TextBlock _skillsHeader;
        private Button _addSkill, _skillsBtn;
        private List<SkillInfo> _skills = new List<SkillInfo>();
        private List<string> _standardNames = new List<string>(PaletteSettings.Standards);
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
        private Border _apiKeyPanel;
        private PasswordBox _apiKeyBox;
        private TextBlock _apiKeyInfo;
        // layer scope
        private Button _layersBtn;
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
            _settingsBtn = MakeButton("⚙", "Cài đặt: model, mức suy nghĩ, giao diện, cỡ chữ, thông báo");
            _settingsBtn.MinWidth = 28;
            _export = MakeButton("Xuất", "Xuất cuộc chat ra file Markdown");
            _new = MakeButton("New", "Bắt đầu cuộc chat mới cho bản vẽ này");
            _stop = MakeButton("Stop", "Dừng lượt đang chạy");
            _stop.IsEnabled = false;
            _settingsBtn.Click += (s, e) =>
            {
                _settingsPopup.IsOpen = !_settingsPopup.IsOpen;
                if (_settingsPopup.IsOpen) SettingsOpened?.Invoke(); // refresh the account line
            };
            _export.Click += (s, e) => ExportRequested?.Invoke();
            _new.Click += (s, e) => NewChatRequested?.Invoke();
            _stop.Click += (s, e) => StopRequested?.Invoke();
            foreach (var b in new[] { _new, _stop, _settingsBtn, _export }) { DockPanel.SetDock(b, Dock.Right); top.Children.Add(b); }
            var statusStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _connection = MutedText("Đang khởi động...");
            _usage = MutedText("");
            _usage.FontSize = 11;
            statusStack.Children.Add(_connection);
            statusStack.Children.Add(_usage);
            top.Children.Add(statusStack);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            // ---- context bar (feature 9)
            var ctxBar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 4) };
            ctxBar.Children.Add(MutedText("Chuẩn "));
            _standard = MakeCombo(PaletteSettings.Standards.Select(PaletteSettings.StandardLabel).ToArray(), 104);
            _standard.ToolTip = "Chuẩn vẽ (skill) áp dụng cho mọi yêu cầu. Tự động = Claude tự chọn skill phù hợp.";
            ctxBar.Children.Add(_standard);
            ctxBar.Children.Add(MutedText("  Tỷ lệ "));
            _scale = MakeCombo(PaletteSettings.Scales.Select(s => s == "" ? "Tự động" : s).ToArray(), 76);
            _scale.ToolTip = "Tỷ lệ in: quyết định chiều cao chữ và kích thước.";
            ctxBar.Children.Add(_scale);
            _addSkill = MakeButton("+ Add skill", "Thêm skill từ file .md trong máy (mặc định mở thư mục skill của plugin)");
            _addSkill.Margin = new Thickness(6, 0, 0, 0);
            _addSkill.Click += (s, e) => AddSkillRequested?.Invoke();
            ctxBar.Children.Add(_addSkill);
            _skillsBtn = MakeButton("Skill", "Danh sách skill: bật/tắt, sửa, xóa");
            _skillsBtn.Click += (s, e) => { RefreshSkillsPanel(); _skillsPopup.IsOpen = !_skillsPopup.IsOpen; };
            ctxBar.Children.Add(_skillsBtn);
            _layersBtn = MakeButton("Layer", "Chọn layer để chat chỉnh sửa (đổi màu, nét, khóa... hoặc sửa các đối tượng trên layer)");
            _layersBtn.Click += (s, e) => ToggleLayerPanel();
            ctxBar.Children.Insert(ctxBar.Children.IndexOf(_addSkill), _layersBtn);
            _macrosBtn = MakeButton("Macro", "Macro đã lưu: chạy lại các lượt vẽ ưng ý mà không cần Claude (không tốn token)");
            _macrosBtn.Click += (s, e) => { RefreshMacrosPanel(); _macrosPopup.IsOpen = !_macrosPopup.IsOpen; };
            ctxBar.Children.Add(_macrosBtn);
            _notesBtn = MakeButton("Ghi chú", "Ghi chú dự án gửi kèm mọi tin nhắn (cao độ, chiều dày tường, khung tên...)");
            _notesBtn.Click += (s, e) => NotesRequested?.Invoke();
            ctxBar.Children.Add(_notesBtn);
            _versionsBtn = MakeButton("Phiên bản", "Bản sao bản vẽ trước khi chat sửa (30 phút một lần, giữ 10 bản): mở ra xem, Đóng để về bản hiện tại");
            _versionsBtn.Click += (s, e) => { RefreshVersionsPanel(); _versionsPopup.IsOpen = !_versionsPopup.IsOpen; };
            ctxBar.Children.Add(_versionsBtn);
            _readOnlyBtn = new ToggleButton { Content = "🔓 Sửa được", ToolTip = "Chỉ xem: Claude chỉ được đọc, đo, hỏi đáp — không sửa bản vẽ này", MinWidth = 40, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
            _readOnlyBtn.Checked += (s, e) => { PaintReadOnly(); if (!_suppressReadOnlyEvent) ReadOnlyToggled?.Invoke(true); };
            _readOnlyBtn.Unchecked += (s, e) => { PaintReadOnly(); if (!_suppressReadOnlyEvent) ReadOnlyToggled?.Invoke(false); };
            ctxBar.Children.Add(_readOnlyBtn);
            _standard.SelectionChanged += (s, e) => OnSettingsUi();
            _scale.SelectionChanged += (s, e) => OnSettingsUi();
            DockPanel.SetDock(ctxBar, Dock.Top);
            root.Children.Add(ctxBar);

            // ---- banner while an old version is open (read-only tab)
            var bannerRow = new DockPanel { Margin = new Thickness(8, 5, 8, 5) };
            var closeVersion = new Button { Content = "Đóng — về bản hiện tại", Padding = new Thickness(10, 2, 10, 2), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            closeVersion.Click += (s, e) => VersionCloseRequested?.Invoke();
            DockPanel.SetDock(closeVersion, Dock.Right);
            bannerRow.Children.Add(closeVersion);
            _versionBannerText = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Theme.Ref(_versionBannerText, TextBlock.ForegroundProperty, Theme.Fg);
            bannerRow.Children.Add(_versionBannerText);
            _versionBanner = new Border { Child = bannerRow, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
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
            _toolStatus.FontStyle = FontStyles.Italic;
            _toolStatus.Margin = new Thickness(0, 0, 0, 4);
            DockPanel.SetDock(_toolStatus, Dock.Top);
            bottom.Children.Add(_toolStatus);
            DockPanel.SetDock(_suggestions, Dock.Top);
            bottom.Children.Add(_suggestions);
            DockPanel.SetDock(_chips, Dock.Top);
            bottom.Children.Add(_chips);

            var actions = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            _send = MakeButton("Gửi", "Gửi (Enter). Shift+Enter xuống dòng, / để chọn mẫu lệnh, ↑↓ gọi lại tin đã gửi");
            DockPanel.SetDock(_send, Dock.Right);
            actions.Children.Add(_send);
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            _pickSel = MakeButton("Chọn đối tượng", "Quét chọn đối tượng trên bản vẽ để gửi kèm tin nhắn");
            _pickPt = MakeButton("Lấy điểm", "Bấm điểm trên bản vẽ để chèn tọa độ vào tin nhắn (Enter để xong)");
            _addImg = MakeButton("Ảnh", "Đính kèm ảnh phác thảo / ảnh scan (hoặc dán Ctrl+V, kéo thả vào khung chat)");
            _capture = MakeButton("Chụp", "Chụp vùng bản vẽ đang xem gửi kèm tin nhắn, để chỉ cho Claude \"chỗ này sai\"");
            _capture.Click += (s, e) => CaptureScreenRequested?.Invoke();
            foreach (var b in new[] { _pickSel, _pickPt, _addImg, _capture }) { b.Margin = new Thickness(0, 0, 4, 0); left.Children.Add(b); }
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
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

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
            var openFolder = SmallButton("Mở thư mục", "Mở thư mục skill của plugin trong Explorer");
            openFolder.Click += (s, e) => OpenSkillsFolderRequested?.Invoke();
            var addInPanel = SmallButton("+ Add skill", "Thêm skill từ file .md");
            addInPanel.Click += (s, e) => AddSkillRequested?.Invoke();
            DockPanel.SetDock(openFolder, Dock.Right);
            DockPanel.SetDock(addInPanel, Dock.Right);
            skillsHead.Children.Add(openFolder);
            skillsHead.Children.Add(addInPanel);
            skillsHead.Children.Add(_skillsHeader);
            var skillsStack = new StackPanel { Margin = new Thickness(10), Width = 340 };
            skillsStack.Children.Add(skillsHead);
            var fromChat = SmallButton("✨ Tạo skill từ cuộc chat này", "Claude soạn SKILL.md từ các chuẩn cậu đã chỉnh trong cuộc chat; cậu xem lại rồi thêm vào plugin");
            fromChat.HorizontalAlignment = HorizontalAlignment.Left;
            fromChat.Margin = new Thickness(0, 0, 0, 8);
            fromChat.Click += (s, e) => { _skillsPopup.IsOpen = false; CreateSkillFromChatRequested?.Invoke(); };
            skillsStack.Children.Add(fromChat);
            skillsStack.Children.Add(new ScrollViewer { Content = _skillRows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var skillsBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = skillsStack };
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
            var macrosBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = macrosStack };
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
            var versionsBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = versionsStack };
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
            root.Children.Add(_scroll);
            Content = root;
            RefreshChips();
        }

        // ================================================================= building blocks

        private Button MakeButton(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, MinWidth = 40, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
            return b;
        }

        private static Button SmallButton(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 0, 6, 1), FontSize = 11, Cursor = Cursors.Hand };
            return b;
        }

        private static TextBlock MutedText(string text)
        {
            var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.MutedFg);
            return t;
        }

        private static ComboBox MakeCombo(string[] items, double width)
        {
            var c = new ComboBox { Width = width, Margin = new Thickness(2, 0, 0, 0), FontSize = 12 };
            foreach (var i in items) c.Items.Add(i);
            c.SelectedIndex = 0;
            return c;
        }

        private FrameworkElement BuildSettingsPanel()
        {
            var p = new StackPanel { Margin = new Thickness(10), MinWidth = 220 };
            var border = new Border { BorderThickness = new Thickness(1), Child = p, CornerRadius = new CornerRadius(4) };
            Theme.Ref(border, Border.BackgroundProperty, Theme.InputBg);
            Theme.Ref(border, Border.BorderBrushProperty, Theme.Border);
            TextBlock Label(string t) { var l = new TextBlock { Text = t, Margin = new Thickness(0, 6, 0, 2) }; Theme.Ref(l, TextBlock.ForegroundProperty, Theme.Fg); return l; }

            p.Children.Add(Label("Model"));
            _model = MakeCombo(PaletteSettings.Models.Select(PaletteSettings.ModelLabel).ToArray(), 200);
            p.Children.Add(_model);
            p.Children.Add(Label("Mức suy nghĩ (effort)"));
            _effort = MakeCombo(new[] { "Mặc định", "low — nhanh", "medium", "high", "xhigh — kỹ nhất" }, 200);
            p.Children.Add(_effort);
            p.Children.Add(Label("Giao diện"));
            _theme = MakeCombo(new[] { "Theo AutoCAD", "Tối", "Sáng" }, 200);
            p.Children.Add(_theme);
            p.Children.Add(Label("Cỡ chữ (Ctrl + lăn chuột)"));
            var fonts = new StackPanel { Orientation = Orientation.Horizontal };
            var minus = SmallButton("A−", "Chữ nhỏ hơn");
            var plus = SmallButton("A+", "Chữ to hơn");
            minus.Click += (s, e) => ChangeFont(-1);
            plus.Click += (s, e) => ChangeFont(+1);
            fonts.Children.Add(minus);
            fonts.Children.Add(plus);
            p.Children.Add(fonts);
            _notify = new CheckBox { Content = "Thông báo Windows khi lượt xong lúc tớ đang ở cửa sổ khác", Margin = new Thickness(0, 8, 0, 0), IsChecked = true };
            Theme.Ref(_notify, ForegroundProperty, Theme.Fg);
            p.Children.Add(_notify);
            CheckBox Check(string text, string tip)
            {
                var c = new CheckBox { Content = text, ToolTip = tip, Margin = new Thickness(0, 6, 0, 0), IsChecked = true };
                Theme.Ref(c, ForegroundProperty, Theme.Fg);
                c.Checked += (s, e) => OnSettingsUi();
                c.Unchecked += (s, e) => OnSettingsUi();
                p.Children.Add(c);
                return c;
            }
            _visualCheck = Check("Claude tự nhìn bản vẽ để kiểm tra sau khi vẽ việc lớn", "Claude chụp vùng vừa vẽ (capture_view) để tự soát lỗi; tốn thêm ít token cho mỗi ảnh");
            _planFirst = Check("Trình bày kế hoạch trước khi làm việc nhiều bước", "Thẻ kế hoạch có nút Làm / Sửa kế hoạch trước khi Claude vẽ việc lớn");
            _suggestCheck = Check("Gợi ý bước tiếp theo sau mỗi câu trả lời", "2–3 nút gợi ý dưới câu trả lời; bấm là gửi");
            var setupRow = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var accTitle = new TextBlock { Text = "Tài khoản AI", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
            Theme.Ref(accTitle, TextBlock.ForegroundProperty, Theme.Fg);
            setupRow.Children.Add(accTitle);
            _accountStatus = MutedText("Đang kiểm tra...");
            _accountStatus.TextWrapping = TextWrapping.Wrap;
            _accountStatus.FontSize = 11;
            setupRow.Children.Add(_accountStatus);
            var accButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            Button Act(string text, string tip, Action a)
            {
                var b = SmallButton(text, tip);
                b.Margin = new Thickness(0, 0, 6, 4);
                b.Click += (s, e) => { _settingsPopup.IsOpen = false; a(); };
                accButtons.Children.Add(b);
                return b;
            }
            Act("Đăng nhập Claude", "Đăng nhập tài khoản Claude Pro/Max trên trình duyệt (dùng Claude Code có sẵn trong plugin)", () => LoginRequested?.Invoke());
            Act("Đổi tài khoản", "Đăng xuất tài khoản hiện tại rồi đăng nhập tài khoản Claude khác", () => SwitchAccountRequested?.Invoke());
            Act("Đăng xuất", "Đăng xuất tài khoản Claude trên máy này (cả Claude Code nếu có)", () => LogoutRequested?.Invoke());
            Act("Dùng API key", "Dán API key Anthropic (trả theo lượng dùng) thay cho tài khoản Pro/Max", () => ShowApiKeyPanel(ApiKeyMask?.Invoke()));
            setupRow.Children.Add(accButtons);
            var helpButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var report = SmallButton("Gửi báo cáo lỗi", "Gom thông tin máy, log, kết quả tự kiểm tra thành một file .zip trên Desktop để gửi cho người hỗ trợ");
            report.Margin = new Thickness(0, 0, 6, 4);
            report.Click += (s, e) => { _settingsPopup.IsOpen = false; ReportRequested?.Invoke(); };
            var welcome = SmallButton("Hướng dẫn bắt đầu", "Kiểm tra cài đặt, đăng nhập, tự kiểm tra");
            welcome.Margin = new Thickness(0, 0, 6, 4);
            welcome.Click += (s, e) => { _settingsPopup.IsOpen = false; WelcomeRequested?.Invoke(); };
            helpButtons.Children.Add(report);
            helpButtons.Children.Add(welcome);
            setupRow.Children.Add(helpButtons);
            _hotkey = Check("Phím tắt Ctrl+Shift+Space mở khung chat", "Trong AutoCAD: Ctrl+Shift+Space mở palette và đặt con trỏ vào ô nhập (lệnh tắt: CC)");
            _versionsCheck = Check("Tự lưu phiên bản trước khi chat sửa (30 phút/lần, giữ 10 bản)", "Bản sao DWG trong %LocalAppData%\\ClaudeCAD\\versions; mở xem bằng nút Phiên bản");
            _sideSnapshot = Check("Lưu biên bản cạnh bản vẽ (nhóm dùng chung thư mục dự án)", "Biên bản thay đổi lưu thêm trong thư mục ẩn .claudecad cạnh file DWG, để máy khác có cài plugin cũng biết bản vẽ đã được sửa tay");
            _sideSnapshot.IsChecked = false;
            p.Children.Add(setupRow);
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

        private void ScrollToEnd() => _scroll.ScrollToEnd();

        // ================================================================= settings / theme

        public void ApplySettings(PaletteSettings s) => Ui(() =>
        {
            _settings = s.Clone();
            _suppressSettingsEvents = true;
            try
            {
                _standard.SelectedIndex = Math.Max(0, _standardNames.IndexOf(s.Standard));
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
            _settings.Standard = _standardNames[Math.Max(0, Math.Min(_standardNames.Count - 1, _standard.SelectedIndex))];
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

        public void SetDark(bool dark) => Ui(() => Theme.Apply(Resources, dark));

        private void ChangeFont(int delta)
        {
            _settings.FontSize = _settings.ClampFont(_settings.FontSize + delta);
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
                try
                {
                    // Excel/Word also put a bitmap of the cells on the clipboard: text wins
                    if (Clipboard.ContainsText() || !Clipboard.ContainsImage()) return;
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
            if (_busy) return;
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
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var close = SmallButton("Đóng", "Đóng danh sách layer");
            close.Margin = new Thickness(0);
            var apply = SmallButton("Dùng layer đã chọn", "Gửi kèm các layer đã tick với tin nhắn tiếp theo");
            var none = SmallButton("Bỏ chọn", "Bỏ tick tất cả");
            foreach (var b in new[] { close, apply, none }) { DockPanel.SetDock(b, Dock.Right); head.Children.Add(b); }
            var title = new TextBlock { Text = "Chọn layer để chat chỉnh sửa", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            head.Children.Add(title);
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
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
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
                var check = new CheckBox { Content = content, IsChecked = _layerPicked.Contains(name), VerticalAlignment = VerticalAlignment.Center };
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
            var b = new Border { Child = sp, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 4, 2), Margin = new Thickness(0, 0, 4, 4) };
            Theme.Ref(b, Border.BackgroundProperty, Theme.ChipBg);
            return b;
        }

        // ================================================================= skills

        /// <summary>Skill list from the plugin's skill folder (skills.json): panel rows + the Chuẩn selector.</summary>
        public void SetSkills(List<SkillInfo> skills) => Ui(() =>
        {
            _skills = skills ?? new List<SkillInfo>();
            var usable = _skills.Where(k => k.Enabled && k.Error == null).ToList();
            _skillsBtn.Content = $"Skill ({usable.Count})";
            _suppressSettingsEvents = true;
            try
            {
                _standardNames = new List<string> { "" };
                _standardNames.AddRange(usable.Select(k => k.Name));
                _standard.Items.Clear();
                foreach (var n in _standardNames) _standard.Items.Add(StandardLabelFor(n));
                _standard.SelectedIndex = Math.Max(0, _standardNames.IndexOf(_settings.Standard));
            }
            finally
            {
                _suppressSettingsEvents = false;
            }
            RefreshSkillsPanel();
        });

        private string StandardLabelFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Tự động";
            if (SkillStore.BuiltinNames.Contains(name)) return PaletteSettings.StandardLabel(name);
            var k = _skills.FirstOrDefault(s => s.Name == name);
            var t = k?.Title ?? name;
            return t.Length > 22 ? t.Substring(0, 21) + "…" : t;
        }

        private void RefreshSkillsPanel()
        {
            int on = _skills.Count(k => k.Enabled && k.Error == null);
            _skillsHeader.Text = $"Skill của plugin — {on}/{_skills.Count} đang bật";
            _skillRows.Children.Clear();
            if (_skills.Count == 0)
            {
                _skillRows.Children.Add(MutedText("Chưa có skill nào. Bấm + Add skill để thêm file .md."));
                return;
            }
            foreach (var k in _skills)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var check = new CheckBox { IsChecked = k.Enabled, IsEnabled = k.Error == null, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 6, 0), ToolTip = k.Enabled ? "Đang bật — bỏ chọn để tắt" : "Đang tắt — chọn để bật" };
                var name = k.Name;
                check.Checked += (s, e) => SkillToggled?.Invoke(name, true);
                check.Unchecked += (s, e) => SkillToggled?.Invoke(name, false);
                DockPanel.SetDock(check, Dock.Left);
                row.Children.Add(check);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                var edit = SmallButton("Sửa", "Mở SKILL.md để sửa; lưu file là Claude dùng bản mới từ tin nhắn tiếp theo");
                edit.Click += (s, e) => SkillEditRequested?.Invoke(name);
                var del = SmallButton("Xóa", "Xóa skill khỏi plugin");
                del.Margin = new Thickness(0);
                del.Click += (s, e) => SkillDeleteRequested?.Invoke(name);
                buttons.Children.Add(edit);
                buttons.Children.Add(del);
                DockPanel.SetDock(buttons, Dock.Right);
                row.Children.Add(buttons);

                var info = new StackPanel();
                var title = new TextBlock { Text = k.Name + (k.Title != null && k.Title != k.Name ? "  ·  " + k.Title : ""), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
                Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
                info.Children.Add(title);
                string status = k.Error != null ? "⚠ " + k.Error
                    : (k.Builtin ? "Skill mẫu" : "Đã thêm" + (k.Added.HasValue ? " " + k.Added.Value.ToString("dd/MM/yyyy") : ""))
                      + (k.Enabled ? " · đang bật" : " · đã tắt");
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
                _skillRows.Children.Add(row);
            }
        }

        // ================================================================= history rendering

        public void ClearHistory() => Ui(() =>
        {
            _messages.Children.Clear();
            _current = null;
            _lastAssistant = null;
            _lastEditButton = null;
            _toolStatus.Text = "";
        });

        public void LoadTranscript(ChatTranscript t, bool canUndoLast) => Ui(() =>
        {
            ClearHistory();
            if (t.Entries.Count == 0)
            {
                AddNote("Chào cậu! Gõ yêu cầu bằng tiếng Việt, ví dụ \"vẽ lưới trục 5x4 bước 4000\". Gõ / để chọn mẫu lệnh, Enter để gửi.");
                return;
            }
            AddNote($"Lịch sử chat của {(string.IsNullOrEmpty(t.DrawingName) ? "bản vẽ này" : t.DrawingName)} — {t.Entries.Count} mục. Claude vẫn nhớ ngữ cảnh cuộc chat.");
            foreach (var e in t.Entries)
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
            if (_lastAssistant != null) _lastAssistant.SetUndo(canUndoLast);
            ScrollToEnd();
        });

        public void AddUserMessage(ChatEntry e) => Ui(() => { AddUserCore(e); ScrollToEnd(); });

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
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 5, 8, 4),
                Margin = new Thickness(40, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = stack,
            };
            Theme.Ref(border, Border.BackgroundProperty, Theme.UserBg);
            _messages.Children.Add(border);
        }

        public void BeginAssistant() => Ui(() =>
        {
            if (_current == null) { _current = NewAssistant(); ScrollToEnd(); }
        });

        private AssistantView NewAssistant()
        {
            var v = new AssistantView(this);
            _messages.Children.Add(v.Root);
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
            if (_current != null) { _current.Flush(); _current = null; }
        }

        public void SetUndoAvailable(bool available) => Ui(() => _lastAssistant?.SetUndo(available));

        public void AddNote(string text, bool isError = false) => Ui(() => { AddNoteCore(text, isError); ScrollToEnd(); });

        private void AddNoteCore(string text, bool isError)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic, Margin = new Thickness(4) };
            Theme.Ref(t, TextBlock.ForegroundProperty, isError ? Theme.ErrorFg : Theme.MutedFg);
            _messages.Children.Add(t);
        }

        /// <summary>Error card with "Thử lại" (feature 13).</summary>
        public void AddErrorCard(string text, bool canRetry) => Ui(() =>
        {
            var stack = new StackPanel();
            var t = new TextBlock { Text = "⚠ " + text, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.ErrorFg);
            stack.Children.Add(t);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
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
            var b = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 24, 4) };
            Theme.Ref(b, Border.BorderBrushProperty, Theme.ErrorFg);
            _messages.Children.Add(b);
            ScrollToEnd();
        });

        /// <summary>Confirmation card with Đồng ý / Hủy / Xem trước (feature 11).</summary>
        public void AddConfirmCard(string id, string text, int? count, List<string> handles) => Ui(() =>
        {
            var stack = new StackPanel();
            var t = new TextBlock { Text = "Xác nhận: " + text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(t);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var yes = new Button { Content = "Đồng ý", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold };
            var no = new Button { Content = "Hủy", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 6, 0) };
            row.Children.Add(yes);
            row.Children.Add(no);
            if (handles != null && handles.Count > 0)
            {
                var preview = new Button { Content = $"Xem trước ({handles.Count})", Padding = new Thickness(8, 2, 8, 2) };
                preview.Click += (s, e) => ZoomRequested?.Invoke(handles);
                row.Children.Add(preview);
            }
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
            stack.Children.Add(row);
            var b = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 0, 4), Tag = "confirm" };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Accent);
            if (_current != null) _current.AddCard(b);       // inside the turn's bubble: text after it continues there
            else { b.Margin = new Thickness(0, 4, 24, 4); _messages.Children.Add(b); }
            ScrollToEnd();
        });

        // ================================================================= status

        public void SetToolStatus(string text) => Ui(() => _toolStatus.Text = text ?? "");

        public void SetConnection(string text, bool ok) => Ui(() =>
        {
            _connection.Text = text;
            Theme.Ref(_connection, TextBlock.ForegroundProperty, ok ? Theme.MutedFg : Theme.ErrorFg);
        });

        public void SetUsage(string text) => Ui(() => _usage.Text = text ?? "");

        public void SetBusy(bool busy) => Ui(() =>
        {
            _busy = busy;
            _send.IsEnabled = !busy && !_viewingVersion;
            _stop.IsEnabled = busy;
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
            if (on) Theme.Ref(_readOnlyBtn, ForegroundProperty, Theme.ErrorFg);
            else _readOnlyBtn.ClearValue(ForegroundProperty);
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
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
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
            _notesBtn.Content = any ? "Ghi chú •" : "Ghi chú";
            _notesBtn.FontWeight = any ? FontWeights.SemiBold : FontWeights.Normal;
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
            _macrosBtn.Content = _macros.Count > 0 ? $"Macro ({_macros.Count})" : "Macro";
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
            _versionsBtn.Content = _versions.Count > 0 ? $"Phiên bản ({_versions.Count})" : "Phiên bản";
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
            _send.IsEnabled = !_busy;
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

        // ---------------------------------------------------------------- account

        public void SetAccountStatus(string text) => Ui(() => { if (_accountStatus != null) _accountStatus.Text = text ?? ""; });

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
            var border = new Border { Child = p, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(6, 0, 6, 4), Visibility = Visibility.Collapsed };
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
            var t = new TextBlock { Text = "⚠ " + text, TextWrapping = TextWrapping.Wrap };
            Theme.Ref(t, TextBlock.ForegroundProperty, Theme.ErrorFg);
            stack.Children.Add(t);
            var b = new Button { Content = "Đăng nhập Claude", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (s, e) => LoginRequested?.Invoke();
            stack.Children.Add(b);
            var card = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 6), Margin = new Thickness(0, 4, 24, 4) };
            Theme.Ref(card, Border.BorderBrushProperty, Theme.ErrorFg);
            _messages.Children.Add(card);
            ScrollToEnd();
        });

        /// <summary>First start after installing: what is ready, sign-in, self-test.</summary>
        public void AddWelcomeCard(List<(string name, bool? ok, string detail)> checks, bool needLogin) => Ui(() =>
        {
            var stack = new StackPanel();
            var title = new TextBlock { Text = "Chào mừng đến với ClaudeCAD", FontWeight = FontWeights.SemiBold, FontSize = FontSize + 1 };
            Theme.Ref(title, TextBlock.ForegroundProperty, Theme.Fg);
            stack.Children.Add(title);
            foreach (var (name, ok, detail) in checks)
            {
                var row = new TextBlock { Text = $"{(ok == true ? "✓" : ok == false ? "✗" : "…")} {name}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 2, 0, 0) };
                Theme.Ref(row, TextBlock.ForegroundProperty, ok == false ? Theme.ErrorFg : Theme.Fg);
                stack.Children.Add(row);
            }
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            if (needLogin)
            {
                var login = new Button { Content = "Đăng nhập Claude", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 6, 4), FontWeight = FontWeights.SemiBold };
                login.Click += (s, e) => LoginRequested?.Invoke();
                buttons.Children.Add(login);
            }
            var test = new Button { Content = "Chạy tự kiểm tra", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4), ToolTip = "CLAUDECHATSELFTEST: thử trên một bản vẽ tạm, mở báo cáo ✓/✗" };
            test.Click += (s, e) => SelfTestRequested?.Invoke();
            buttons.Children.Add(test);
            stack.Children.Add(buttons);
            var hint = MutedText("Gõ yêu cầu bằng tiếng Việt rồi Enter, ví dụ \"vẽ lưới trục 5x4 bước 4000\". Mở nhanh khung chat: Ctrl+Shift+Space hoặc lệnh CC. Xem lại hướng dẫn này: ⚙ ▸ Hướng dẫn bắt đầu.");
            hint.TextWrapping = TextWrapping.Wrap;
            hint.FontSize = 11;
            hint.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(hint);
            var card = Card(stack, "welcome");
            card.Margin = new Thickness(0, 4, 24, 4);
            _messages.Children.Add(card);
            ScrollToEnd();
        });

        // ---------------------------------------------------------------- cards: skill draft, plan

        private Border Card(UIElement content, string tag)
        {
            var b = new Border { Child = content, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 0, 4), Tag = tag };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Accent);
            return b;
        }

        private void PlaceCard(Border b)
        {
            if (_current != null) _current.AddCard(b);
            else { b.Margin = new Thickness(0, 4, 24, 4); _messages.Children.Add(b); }
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
            private readonly Expander _toolsExpander;
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
                _toolsExpander = new Expander { Header = MutedText("Thao tác"), Content = _toolRows, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 0) };
                var stack = new StackPanel();
                stack.Children.Add(_toolsExpander);
                stack.Children.Add(_md);
                stack.Children.Add(_cards);
                stack.Children.Add(_changes);
                stack.Children.Add(_actions);
                stack.Children.Add(_prompts);
                Root = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 24, 4), HorizontalAlignment = HorizontalAlignment.Stretch, Child = stack, Tag = this };
                Theme.Ref(Root, Border.BackgroundProperty, Theme.AssistantBg);
                _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _renderTimer.Tick += (s, e) => { _renderTimer.Stop(); if (_dirty) { _dirty = false; _md.Markdown = _text.ToString(); } };
            }

            public void AddCard(UIElement card) => _cards.Children.Add(card);

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

            /// <summary>Tool activity row (feature 12): running rows are updated when the same tool finishes.</summary>
            public void AddTool(ToolLogEntry e)
            {
                _toolsExpander.Visibility = Visibility.Visible;
                if (e.State != "running")
                {
                    var idx = _tools.FindIndex(t => t.entry.State == "running" && t.entry.Tool == e.Tool);
                    if (idx >= 0)
                    {
                        _tools[idx] = (e, _tools[idx].row);
                        Paint(_tools[idx].row, e);
                        UpdateHeader();
                        return;
                    }
                }
                var row = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(14, 0, 0, 1) };
                Paint(row, e);
                _tools.Add((e, row));
                _toolRows.Children.Add(row);
                UpdateHeader();
            }

            private static void Paint(TextBlock row, ToolLogEntry e)
            {
                string label = ToolLabels.Describe(e.Tool);
                switch (e.State)
                {
                    case "running": row.Text = "⏳ " + label; Theme.Ref(row, TextBlock.ForegroundProperty, Theme.MutedFg); break;
                    case "error": row.Text = $"⚠ {label} — {e.Error} ({e.Ms} ms)"; Theme.Ref(row, TextBlock.ForegroundProperty, Theme.ErrorFg); break;
                    default: row.Text = $"✓ {label}{(string.IsNullOrEmpty(e.Summary) ? "" : " — " + e.Summary)} ({e.Ms} ms)"; Theme.Ref(row, TextBlock.ForegroundProperty, Theme.MutedFg); break;
                }
            }

            private void UpdateHeader()
            {
                int errors = _tools.Count(t => t.entry.State == "error");
                long ms = _tools.Sum(t => t.entry.Ms);
                var h = MutedText($"Thao tác ({_tools.Count}{(errors > 0 ? $", {errors} lỗi" : "")}) · {ms / 1000.0:0.0} s");
                h.FontSize = 11;
                _toolsExpander.Header = h;
            }

            public void Finish(ChatEntry e, bool canUndo, bool canSaveMacro = false)
            {
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
                var b = new Border { Child = box, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 4, 0, 0) };
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
