using System;
using System.ComponentModel;
using System.Windows;
using KDM.UI;

namespace KDM
{
    /// <summary>
    /// MainWindow code-behind.
    /// Xử lý minimize to system tray và window lifecycle.
    /// </summary>
    public partial class MainWindow : Window
    {
        private TrayIconManager? _trayIcon;
        private bool _isReallyClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();

            // Đăng ký events
            StateChanged += MainWindow_StateChanged;
        }

        /// <summary>
        /// Khởi tạo tray icon
        /// </summary>
        private void InitializeTrayIcon()
        {
            _trayIcon = new TrayIconManager();

            // Khi user double-click tray icon → restore window
            _trayIcon.ShowRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    _trayIcon.Hide();
                });
            };

            // Khi user chọn Exit từ tray menu → đóng app thật sự
            _trayIcon.ExitRequested += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    _isReallyClosing = true;
                    Close();
                });
            };
        }

        /// <summary>
        /// Khi window state thay đổi → minimize to tray
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Ẩn khỏi taskbar, hiện tray icon
                Hide();
                _trayIcon?.Show();
                _trayIcon?.ShowBalloon(
                    "KDM Download Manager",
                    "Ứng dụng đã thu nhỏ xuống khay hệ thống.\nDouble-click icon để mở lại.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        /// <summary>
        /// Khi đóng window → minimize to tray thay vì đóng app
        /// (trừ khi user chọn Exit từ tray menu)
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isReallyClosing)
            {
                // Không đóng, chỉ minimize to tray
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                return;
            }

            // Đóng thật sự - cleanup tray icon
            _trayIcon?.Dispose();
            base.OnClosing(e);
        }

        private bool _isEnglish = false;
        private void ToggleLanguage_Click(object sender, RoutedEventArgs e)
        {
            _isEnglish = !_isEnglish;
            string dictPath = _isEnglish ? "Locales/Lang.en.xaml" : "Locales/Lang.vi.xaml";
            
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri(dictPath, UriKind.Relative);
            
            // Thay thế dictionary đầu tiên (thường là Locale dictionary đã thêm trong App.xaml)
            Application.Current.Resources.MergedDictionaries[0] = dictionary;
        }
    }
}