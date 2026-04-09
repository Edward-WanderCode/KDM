using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KDM.Models;
using KDM.UI;

namespace KDM
{
    /// <summary>
    /// MainWindow code-behind.
    /// Xử lý minimize to system tray, window lifecycle, và context menu.
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

        // ============================================================
        // TRAY ICON
        // ============================================================

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

        // ============================================================
        // LANGUAGE TOGGLE
        // ============================================================

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

        // ============================================================
        // CONTEXT MENU HANDLERS
        // ============================================================

        /// <summary>Lấy danh sách các item đang được chọn (multi-select)</summary>
        private List<DownloadItemViewModel> GetSelectedItems()
        {
            return DownloadListBox.SelectedItems
                .Cast<DownloadItemViewModel>()
                .ToList();
        }

        /// <summary>Lấy ViewModel chính</summary>
        private MainViewModel? GetViewModel() => DataContext as MainViewModel;

        /// <summary>Mở file đã tải (chỉ cho item đầu tiên đã chọn)</summary>
        private void CtxOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var item = selected[0];
            if (item.Status != DownloadStatus.Completed) return;

            var path = item.Item.FullPath;
            if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Không thể mở file");
                }
            }
        }

        /// <summary>Mở thư mục chứa file (highlight file trong Explorer)</summary>
        private void CtxOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var item = selected[0];
            try
            {
                var path = item.Item.FullPath;
                if (File.Exists(path))
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    Process.Start("explorer.exe", item.Item.SaveDirectory);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Không thể mở thư mục");
            }
        }

        /// <summary>Copy URL của (các) item đã chọn vào clipboard</summary>
        private void CtxCopyUrl_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var urls = string.Join(Environment.NewLine, selected.Select(s => s.Url));
            Clipboard.SetText(urls);

            var vm = GetViewModel();
            if (vm != null)
                vm.StatusBarText = $"Đã sao chép {selected.Count} URL";
        }

        /// <summary>Tạm dừng (các) item đã chọn</summary>
        private void CtxPause_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var vm = GetViewModel();
            if (vm == null) return;

            int count = 0;
            foreach (var item in selected.Where(s => s.CanPause))
            {
                vm.PauseByIdCommand?.Execute(item.Id);
                count++;
            }
            vm.StatusBarText = $"Đã tạm dừng {count} downloads";
        }

        /// <summary>Tiếp tục (các) item đã chọn</summary>
        private void CtxResume_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var vm = GetViewModel();
            if (vm == null) return;

            int count = 0;
            foreach (var item in selected.Where(s => s.CanResume))
            {
                vm.ResumeByIdCommand?.Execute(item.Id);
                count++;
            }
            vm.StatusBarText = $"Đã tiếp tục {count} downloads";
        }

        /// <summary>Xóa (các) item đã chọn</summary>
        private void CtxDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedItems();
            if (selected.Count == 0) return;

            var msg = selected.Count == 1
                ? $"Bạn có muốn xóa \"{selected[0].FileName}\"?"
                : $"Bạn có muốn xóa {selected.Count} downloads đã chọn?";

            var result = MessageBox.Show(
                msg + "\n\nYes = xóa cả file đã tải.\nNo = chỉ xóa khỏi danh sách.",
                "Xác nhận xóa",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            var deleteFile = result == MessageBoxResult.Yes;
            var vm = GetViewModel();
            if (vm == null) return;

            foreach (var item in selected.ToList())
            {
                vm.DeleteByIdCommand?.Execute(new DeleteRequest(item.Id, deleteFile));
            }

            vm.StatusBarText = $"Đã xóa {selected.Count} downloads";
        }
    }
}