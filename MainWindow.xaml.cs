using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clipboard = System.Windows.Clipboard;
using Button = System.Windows.Controls.Button;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

namespace ClipVault
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private NotifyIcon _notifyIcon;
        private string _lastCopiedContent = string.Empty;
        private string _mediaFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clips_media");
        
        public ObservableCollection<ClipModel> Clips { get; set; } = new ObservableCollection<ClipModel>();

        public MainWindow()
        {
            try {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault_log.txt"), $"{DateTime.Now}: Constructor Started\n");
                InitializeComponent();
                DatabaseHelper.InitializeDatabase();
                SetupNotifyIcon();
                
                ClipsList.ItemsSource = Clips;

                if (!Directory.Exists(_mediaFolder))
                    Directory.CreateDirectory(_mediaFolder);

                // Force native handle creation
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();

                InitClipboard();
                RefreshClips();
                
                // Hide initially
                this.Visibility = Visibility.Hidden;
            } catch (Exception ex) {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault_log.txt"), $"{DateTime.Now}: FATAL CONSTRUCTOR ERROR: {ex.Message}\n");
            }
        }

        private void InitClipboard()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault_log.txt");
            try {
                var helper = new WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;

                // Register Hotkey: Ctrl+Alt+V
                bool hotkeyRegistered = RegisterHotKey(hwnd, HOTKEY_ID, 0x0001 | 0x0002, 0x56); 
                File.AppendAllText(logPath, $"{DateTime.Now}: Hotkey Registration: {hotkeyRegistered}\n");

                // Initialize Clipboard Listener
                var hwndSource = HwndSource.FromHwnd(hwnd);
                hwndSource.AddHook(HwndProc);
                bool listenerAdded = AddClipboardFormatListener(hwnd);
                
                File.AppendAllText(logPath, $"{DateTime.Now}: Clipboard Listener Result: {listenerAdded}\n");
            } catch (Exception ex) {
                File.AppendAllText(logPath, $"{DateTime.Now}: InitClipboard ERROR: {ex.Message}\n");
            }
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Text = "ClipVault - Left Click to Open, Right Click for Menu"
            };

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clipvault.png");
                if (File.Exists(iconPath))
                {
                    using (var bitmap = new System.Drawing.Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                    }
                }
                else
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Information;
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Information;
            }

            // Use MouseClick to distinguish between Left and Right click
            _notifyIcon.MouseClick += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleWindow();
                }
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open ClipVault", null, (s, e) => { ToggleWindow(); });
            contextMenu.Items.Add("-"); // Separator
            contextMenu.Items.Add("Exit", null, (s, e) => { System.Windows.Application.Current.Shutdown(); });
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ToggleWindow()
        {
            if (this.IsVisible)
            {
                this.Hide();
            }
            else
            {
                PositionWindowNearTray();
                RefreshClips();
                this.Show();
                this.Activate();
                this.Focus();
            }
        }

        private void PositionWindowNearTray()
        {
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 10;
            this.Top = desktopWorkingArea.Bottom - this.Height - 10;
        }

        private void RefreshClips(string filter = "ALL")
        {
            var data = DatabaseHelper.GetClips(filter);
            Clips.Clear();
            foreach (var item in data)
                Clips.Add(item);
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TabControl.SelectedItem is ListBoxItem item && item.Tag is string filter)
            {
                RefreshClips(filter);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            ClipModel clip = null;
            if (sender is FrameworkElement fe && fe.DataContext is ClipModel c)
                clip = c;
            else if (ClipsList.SelectedItem is ClipModel sc)
                clip = sc;

            if (clip != null)
            {
                if (clip.Type == "IMAGE" && File.Exists(clip.Content))
                {
                    var bitmap = new BitmapImage(new Uri(clip.Content));
                    Clipboard.SetImage(bitmap);
                }
                else
                {
                    Clipboard.SetText(clip.Content);
                }
                NotificationWindow.Show("Copied", "Content copied to clipboard");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            ClipModel clip = null;
            if (sender is FrameworkElement fe && fe.DataContext is ClipModel c)
                clip = c;
            else if (ClipsList.SelectedItem is ClipModel sc)
                clip = sc;

            if (clip != null)
            {
                DatabaseHelper.DeleteClip(clip.Id);
                
                // Refresh with current filter
                string filter = "ALL";
                if (TabControl.SelectedItem is ListBoxItem item && item.Tag is string t)
                    filter = t;
                
                RefreshClips(filter);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
        }

        private void Window_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private bool _isDark = false;
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            if (_isDark)
            {
                Resources["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 15, 15));
                Resources["ItemBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26));
                Resources["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
                Resources["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153));
                Resources["AccentColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 162, 255));
                Resources["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 34));
                Resources["CodeBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 10, 10));
                Resources["CodeText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 212, 212));
            }
            else
            {
                Resources["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
                Resources["ItemBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                Resources["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 17, 17));
                Resources["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
                Resources["AccentColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                Resources["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238));
                Resources["CodeBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                Resources["CodeText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
            }
        }

        private void ClipsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && !(element is ListBoxItem))
            {
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }

            if (element is ListBoxItem item)
            {
                ClipsList.SelectedItem = item.DataContext; // Explicitly set selection
                item.IsSelected = true;
                
                if (item.ContextMenu != null)
                {
                    item.ContextMenu.PlacementTarget = item;
                    item.ContextMenu.IsOpen = true;
                    e.Handled = true;
                }
            }
        }

        private double _scrollTarget = 0;
        private void ClipsList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scrollViewer = GetScrollViewer(ClipsList);
            if (scrollViewer == null) return;

            e.Handled = true;
            _scrollTarget = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, _scrollTarget - e.Delta));
            
            // Smooth animate to target
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = _scrollTarget,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            scrollViewer.BeginAnimation(SmoothScrollViewerHelper.VerticalOffsetProperty, animation);
        }

        private System.Windows.Controls.ScrollViewer GetScrollViewer(DependencyObject obj)
        {
            if (obj is System.Windows.Controls.ScrollViewer viewer) return viewer;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private IntPtr HwndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault_log.txt"), $"{DateTime.Now}: Clipboard Change Detected in WndProc\n");
                ProcessClipboardChange();
            }
            else if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindow();
            }

            return IntPtr.Zero;
        }

        private async void ProcessClipboardChange()
        {
            await System.Threading.Tasks.Task.Delay(150);

            try
            {
                if (Clipboard.ContainsImage())
                {
                    HandleImageClip();
                }
                else if (Clipboard.ContainsText())
                {
                    HandleTextClip();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"{DateTime.Now}: Error reading clipboard: {ex.Message}\n");
            }
        }

        private void HandleImageClip()
        {
            var image = Clipboard.GetImage();
            if (image == null) return;

            string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(_mediaFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fileStream);
            }

            DatabaseHelper.InsertOrUpdateClip(filePath, "IMAGE", "Image copied ");
            NotificationWindow.Show("Image Saved", "An image has been added to ClipVault 🖼️");
            RefreshClips();
        }

        private void HandleTextClip()
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text) || text == _lastCopiedContent) return;

            _lastCopiedContent = text;
            string category = AnalyzeText(text);
            string preview = text.Length > 80 ? text.Substring(0, 77) + "..." : text;

            DatabaseHelper.InsertOrUpdateClip(text, category, preview);
            NotificationWindow.Show($"{category} Copied", preview);
            RefreshClips();
        }

        private string AnalyzeText(string text)
        {
            if (text.StartsWith("http://") || text.StartsWith("https://"))
                return "LINK";

            if (Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return "EMAIL";

            if (text.Contains("{") || text.Contains("}") || text.Contains("(") || text.Contains(")") || text.Contains(";"))
                return "CODE";

            return "TEXT";
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            this.Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            RemoveClipboardFormatListener(helper.Handle);

            base.OnClosed(e);
        }
    }

    public static class SmoothScrollViewerHelper
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static void SetVerticalOffset(DependencyObject target, double value) => target.SetValue(VerticalOffsetProperty, value);
        public static double GetVerticalOffset(DependencyObject target) => (double)target.GetValue(VerticalOffsetProperty);

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.ScrollViewer viewer && e.NewValue is double offset)
            {
                viewer.ScrollToVerticalOffset(offset);
            }
        }
    }
}
