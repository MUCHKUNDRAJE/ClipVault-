using System;
using System.Windows;
using System.Windows.Threading;

namespace ClipVault
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public NotificationWindow(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;

            // Position near tray
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Right - this.Width - 10;
            this.Top = workingArea.Bottom - this.Height - 10;

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _closeTimer.Tick += (s, e) => { this.Close(); _closeTimer.Stop(); };
            _closeTimer.Start();
        }

        public static void Show(string title, string message)
        {
            var win = new NotificationWindow(title, message);
            win.Show();
        }
    }
}
