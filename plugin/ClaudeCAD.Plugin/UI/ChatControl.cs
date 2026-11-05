using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// WPF chat surface hosted in the ClaudeCAD PaletteSet.
    /// Built in code (no XAML) so the project compiles without the WPF markup
    /// compiler. All public methods are thread-safe: they marshal to the
    /// control's Dispatcher.
    /// </summary>
    public sealed class ChatControl : UserControl
    {
        private readonly StackPanel _messages = new StackPanel { Margin = new Thickness(6) };
        private readonly ScrollViewer _scroll;
        private readonly TextBox _input;
        private readonly Button _send;
        private readonly Button _stop;
        private readonly Button _new;
        private readonly TextBlock _connection;
        private readonly TextBlock _toolStatus;
        private TextBox _currentAssistant;
        private bool _busy;

        public event Action<string> SendRequested;
        public event Action StopRequested;
        public event Action NewChatRequested;

        private static readonly Brush UserBg = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x97));
        private static readonly Brush AssistantBg = new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x47));
        private static readonly Brush NoteFg = new SolidColorBrush(Color.FromRgb(0xA0, 0xA8, 0xB0));
        private static readonly Brush ErrorFg = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80));
        private static readonly Brush PanelBg = new SolidColorBrush(Color.FromRgb(0x25, 0x29, 0x2E));
        private static readonly Brush Fg = Brushes.WhiteSmoke;

        public ChatControl()
        {
            Background = PanelBg;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var root = new DockPanel { LastChildFill = true };

            // ---- top bar: connection status + New / Stop
            var top = new DockPanel { Margin = new Thickness(6, 6, 6, 2) };
            _new = MakeButton("New", "Bắt đầu cuộc chat mới (xóa ngữ cảnh)");
            _new.Click += (s, e) => NewChatRequested?.Invoke();
            _stop = MakeButton("Stop", "Dừng lượt đang chạy");
            _stop.IsEnabled = false;
            _stop.Click += (s, e) => StopRequested?.Invoke();
            DockPanel.SetDock(_new, Dock.Right);
            DockPanel.SetDock(_stop, Dock.Right);
            top.Children.Add(_new);
            top.Children.Add(_stop);
            _connection = new TextBlock
            {
                Text = "Đang khởi động...",
                Foreground = NoteFg,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            top.Children.Add(_connection);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            // ---- bottom: tool status + input
            var bottom = new DockPanel { Margin = new Thickness(6, 2, 6, 6) };
            _toolStatus = new TextBlock
            {
                Foreground = NoteFg,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            DockPanel.SetDock(_toolStatus, Dock.Top);
            bottom.Children.Add(_toolStatus);

            _send = MakeButton("Gửi", "Gửi (Enter). Shift+Enter để xuống dòng");
            _send.VerticalAlignment = VerticalAlignment.Bottom;
            _send.Click += (s, e) => SubmitInput();
            DockPanel.SetDock(_send, Dock.Right);
            bottom.Children.Add(_send);

            _input = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 48,
                MaxHeight = 160,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x25)),
                Foreground = Fg,
                CaretBrush = Fg,
                BorderBrush = AssistantBg,
                Padding = new Thickness(4),
            };
            _input.PreviewKeyDown += OnInputKeyDown;
            bottom.Children.Add(_input);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

            // ---- middle: history
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _messages,
            };
            root.Children.Add(_scroll);

            Content = root;
            AddNote("Chào cậu! Gõ yêu cầu bằng tiếng Việt, ví dụ: \"vẽ lưới trục 5x4 bước 4000\". Enter để gửi, Shift+Enter xuống dòng.");
        }

        private static Button MakeButton(string text, string tip) => new Button
        {
            Content = text,
            ToolTip = tip,
            MinWidth = 52,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
        };

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                SubmitInput();
            }
        }

        private void SubmitInput()
        {
            if (_busy) return;
            var text = _input.Text.Trim();
            if (text.Length == 0) return;
            _input.Clear();
            SendRequested?.Invoke(text);
        }

        public void FocusInput() => Ui(() => _input.Focus());

        // ------------------------------------------------------------ thread-safe API

        private void Ui(Action a)
        {
            if (Dispatcher.CheckAccess()) a();
            else Dispatcher.BeginInvoke(a);
        }

        public void AddUserMessage(string text) => Ui(() =>
        {
            EndAssistantCore();
            _messages.Children.Add(Bubble(text, UserBg, HorizontalAlignment.Right));
            ScrollToEnd();
        });

        public void AppendAssistant(string delta) => Ui(() =>
        {
            if (string.IsNullOrEmpty(delta)) return;
            if (_currentAssistant == null)
            {
                var border = Bubble("", AssistantBg, HorizontalAlignment.Left);
                _currentAssistant = (TextBox)border.Child;
                _messages.Children.Add(border);
            }
            _currentAssistant.AppendText(delta);
            ScrollToEnd();
        });

        /// <summary>Closes the current assistant bubble so the next text starts a new one.</summary>
        public void EndAssistant() => Ui(EndAssistantCore);

        private void EndAssistantCore()
        {
            if (_currentAssistant != null)
            {
                var t = _currentAssistant.Text.TrimEnd();
                if (t.Length == 0) _messages.Children.Remove((UIElement)_currentAssistant.Parent);
                else _currentAssistant.Text = t;
            }
            _currentAssistant = null;
        }

        public void AddNote(string text, bool isError = false) => Ui(() =>
        {
            EndAssistantCore();
            _messages.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isError ? ErrorFg : NoteFg,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(4, 4, 4, 4),
            });
            ScrollToEnd();
        });

        public void SetToolStatus(string text) => Ui(() => _toolStatus.Text = text ?? "");

        public void SetConnection(string text, bool ok) => Ui(() =>
        {
            _connection.Text = text;
            _connection.Foreground = ok ? NoteFg : ErrorFg;
        });

        public void SetBusy(bool busy) => Ui(() =>
        {
            _busy = busy;
            _send.IsEnabled = !busy;
            _stop.IsEnabled = busy;
            if (!busy) _toolStatus.Text = "";
        });

        public void ClearHistory() => Ui(() =>
        {
            _messages.Children.Clear();
            _currentAssistant = null;
            _toolStatus.Text = "";
        });

        private Border Bubble(string text, Brush bg, HorizontalAlignment align)
        {
            var tb = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Fg,
                Padding = new Thickness(0),
            };
            return new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = align == HorizontalAlignment.Right ? new Thickness(40, 4, 0, 4) : new Thickness(0, 4, 24, 4),
                HorizontalAlignment = align,
                Child = tb,
            };
        }

        private void ScrollToEnd() => _scroll.ScrollToEnd();
    }
}
