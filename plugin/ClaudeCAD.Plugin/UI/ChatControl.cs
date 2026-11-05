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
        private ComboBox _model, _effort, _theme;
        private CheckBox _notify;

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
            _settingsBtn.Click += (s, e) => _settingsPopup.IsOpen = !_settingsPopup.IsOpen;
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
            var ctxBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 4) };
            ctxBar.Children.Add(MutedText("Chuẩn "));
            _standard = MakeCombo(PaletteSettings.Standards.Select(PaletteSettings.StandardLabel).ToArray(), 92);
            _standard.ToolTip = "Chuẩn vẽ (skill) áp dụng cho mọi yêu cầu. Tự động = Claude tự chọn.";
            ctxBar.Children.Add(_standard);
            ctxBar.Children.Add(MutedText("  Tỷ lệ "));
            _scale = MakeCombo(PaletteSettings.Scales.Select(s => s == "" ? "Tự động" : s).ToArray(), 76);
            _scale.ToolTip = "Tỷ lệ in: quyết định chiều cao chữ và kích thước.";
            ctxBar.Children.Add(_scale);
            _standard.SelectionChanged += (s, e) => OnSettingsUi();
            _scale.SelectionChanged += (s, e) => OnSettingsUi();
            DockPanel.SetDock(ctxBar, Dock.Top);
            root.Children.Add(ctxBar);

            // ---- bottom: tool status, chips, input, action buttons
            var bottom = new DockPanel { Margin = new Thickness(6, 2, 6, 6) };
            _toolStatus = MutedText("");
            _toolStatus.FontStyle = FontStyles.Italic;
            _toolStatus.Margin = new Thickness(0, 0, 0, 4);
            DockPanel.SetDock(_toolStatus, Dock.Top);
            bottom.Children.Add(_toolStatus);
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
            foreach (var b in new[] { _pickSel, _pickPt, _addImg }) { b.Margin = new Thickness(0, 0, 4, 0); left.Children.Add(b); }
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
            _input.TextChanged += (s, e) => UpdateSlashPopup();
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
            _model.SelectionChanged += (s, e) => OnSettingsUi();
            _effort.SelectionChanged += (s, e) => OnSettingsUi();
            _theme.SelectionChanged += (s, e) => OnSettingsUi();
            _notify.Checked += (s, e) => OnSettingsUi();
            _notify.Unchecked += (s, e) => OnSettingsUi();
            return border;
        }

        private void Ui(Action a)
        {
            if (Dispatcher.CheckAccess()) a();
            else Dispatcher.BeginInvoke(a);
        }

        private void ScrollToEnd() => _scroll.ScrollToEnd();

        // ================================================================= settings / theme

        public void ApplySettings(PaletteSettings s) => Ui(() =>
        {
            _settings = s;
            _suppressSettingsEvents = true;
            try
            {
                _standard.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Standards, s.Standard));
                _scale.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Scales, s.Scale));
                _model.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Models, s.Model));
                _effort.SelectedIndex = Math.Max(0, Array.IndexOf(PaletteSettings.Efforts, s.Effort));
                _theme.SelectedIndex = s.Theme == "dark" ? 1 : s.Theme == "light" ? 2 : 0;
                _notify.IsChecked = s.Notify;
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
            _settings.Standard = PaletteSettings.Standards[Math.Max(0, _standard.SelectedIndex)];
            _settings.Scale = PaletteSettings.Scales[Math.Max(0, _scale.SelectedIndex)];
            _settings.Model = PaletteSettings.Models[Math.Max(0, _model.SelectedIndex)];
            _settings.Effort = PaletteSettings.Efforts[Math.Max(0, _effort.SelectedIndex)];
            _settings.Theme = _theme.SelectedIndex == 1 ? "dark" : _theme.SelectedIndex == 2 ? "light" : "auto";
            _settings.Notify = _notify.IsChecked == true;
            SettingsChanged?.Invoke(_settings);
        }

        public void SetDark(bool dark) => Ui(() => Theme.Apply(Resources, dark));

        private void ChangeFont(int delta)
        {
            _settings.FontSize = _settings.ClampFont(_settings.FontSize + delta);
            FontSize = _settings.FontSize;
            foreach (var v in _messages.Children.OfType<FrameworkElement>().Select(e => e.Tag).OfType<AssistantView>()) v.Rerender();
            SettingsChanged?.Invoke(_settings);
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
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && Clipboard.ContainsImage())
            {
                e.Handled = true;
                try { AddImage(ImageAttachment.FromBitmap(Clipboard.GetImage())); }
                catch (Exception ex) { AddNote("Không dán được ảnh: " + ex.Message, true); }
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
            _slashPopup.IsOpen = false;
            var text = _input.Text.Trim();
            if (text.Length == 0 && _images.Count == 0) return;
            if (text.Length == 0) text = "Vẽ theo ảnh đính kèm.";
            var msg = new ComposedMessage
            {
                Text = text,
                Images = _images.ToList(),
                IncludeSelection = _selection != null && _selection.Count > 0 && _selectionIncluded,
            };
            _input.Clear();
            _images.Clear();
            _settings.AddHistory(text);
            _history.Reset();
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
                var label = _selectionIncluded
                    ? $"Kèm {_selection.Count} đối tượng đang chọn ({_selection.Summary})"
                    : $"Không kèm {_selection.Count} đối tượng đang chọn";
                _chips.Children.Add(Chip(label, () => { _selectionIncluded = !_selectionIncluded; RefreshChips(); },
                    _selectionIncluded ? "✕" : "+", () => ZoomRequested?.Invoke(_selection.Handles)));
            }
            foreach (var img in _images.ToList())
            {
                var chip = Chip($"Ảnh {img.Width}×{img.Height}", () => { _images.Remove(img); RefreshChips(); }, "✕", null, img.Thumbnail);
                _chips.Children.Add(chip);
            }
            _chips.Visibility = _chips.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

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
            var tb = new TextBox { Text = e.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0) };
            Theme.Ref(tb, ForegroundProperty, Theme.Fg);
            var stack = new StackPanel();
            stack.Children.Add(tb);
            var extras = new List<string>();
            if (e.SelectionCount > 0) extras.Add($"kèm {e.SelectionCount} đối tượng");
            if (e.ImageCount > 0) extras.Add($"kèm {e.ImageCount} ảnh");
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 3, 0, 0) };
            if (extras.Count > 0) { var x = MutedText(string.Join(" · ", extras) + "  "); x.FontSize = 11; footer.Children.Add(x); }
            var copy = SmallButton("Copy", "Copy tin nhắn");
            copy.Click += (s, a) => SafeCopy(e.Text);
            footer.Children.Add(copy);
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
        public void EndAssistant(ChatEntry e, bool canUndo) => Ui(() =>
        {
            if (_current == null) _current = NewAssistant();
            _current.Finish(e, canUndo);
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
            if (canRetry)
            {
                var retry = SmallButton("Thử lại", "Gửi lại yêu cầu vừa rồi");
                retry.Margin = new Thickness(0, 4, 0, 0);
                retry.HorizontalAlignment = HorizontalAlignment.Left;
                retry.Click += (s, e) => { retry.IsEnabled = false; RetryRequested?.Invoke(); };
                stack.Children.Add(retry);
            }
            var b = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 24, 4) };
            Theme.Ref(b, Border.BorderBrushProperty, Theme.ErrorFg);
            _messages.Children.Add(b);
            ScrollToEnd();
        });

        /// <summary>Confirmation card with Đồng ý / Hủy / Xem trước (feature 11).</summary>
        public void AddConfirmCard(string id, string text, int? count, List<string> handles) => Ui(() =>
        {
            EndAssistantCore();
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
                answer.Text = ok ? "  → đã đồng ý" : "  → đã hủy";
                ConfirmAnswered?.Invoke(id, ok);
            }
            yes.Click += (s, e) => Done(true);
            no.Click += (s, e) => Done(false);
            row.Children.Add(answer);
            stack.Children.Add(row);
            var b = new Border { Child = stack, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 24, 4), Tag = "confirm" };
            Theme.Ref(b, Border.BackgroundProperty, Theme.CardBg);
            Theme.Ref(b, Border.BorderBrushProperty, Theme.Accent);
            _messages.Children.Add(b);
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
            _send.IsEnabled = !busy;
            _stop.IsEnabled = busy;
            _pickSel.IsEnabled = _pickPt.IsEnabled = !busy;
            if (_lastEditButton != null) _lastEditButton.IsEnabled = !busy;
            if (!busy) _toolStatus.Text = "";
        });

        private static void SafeCopy(string text)
        {
            try { Clipboard.SetText(text ?? ""); } catch { }
        }

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
            private readonly DispatcherTimer _renderTimer;
            private Button _undo;
            private bool _dirty;

            public Border Root { get; }

            public AssistantView(ChatControl owner)
            {
                _owner = owner;
                _md.FontSize = owner.FontSize;
                _md.HandleLinkClicked += h => owner.ZoomRequested?.Invoke(h);
                _toolsExpander = new Expander { Header = MutedText("Thao tác"), Content = _toolRows, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 0) };
                var stack = new StackPanel();
                stack.Children.Add(_toolsExpander);
                stack.Children.Add(_md);
                stack.Children.Add(_actions);
                Root = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 24, 4), HorizontalAlignment = HorizontalAlignment.Stretch, Child = stack, Tag = this };
                Theme.Ref(Root, Border.BackgroundProperty, Theme.AssistantBg);
                _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _renderTimer.Tick += (s, e) => { _renderTimer.Stop(); if (_dirty) { _dirty = false; _md.Markdown = _text.ToString(); } };
            }

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

            public void Finish(ChatEntry e, bool canUndo)
            {
                _text.Clear();
                _text.Append(e.Text ?? "");
                Flush();
                _actions.Children.Clear();
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
