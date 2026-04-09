using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KDM.Core;
using KDM.Models;
using KDM.Network;
using Serilog;

namespace KDM.UI
{
    /// <summary>
    /// ViewModel chính cho MainWindow.
    /// Quản lý danh sách download, các command, và state của UI.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DownloadScheduler _scheduler;
        private readonly AppSettings _settings;
        private readonly ExtensionServer _extensionServer;
        private static readonly ILogger _log = Log.ForContext<MainViewModel>();

        /// <summary>Danh sách download items hiển thị trên UI</summary>
        public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

        // --- Input fields ---
        private string _newUrl = string.Empty;
        public string NewUrl
        {
            get => _newUrl;
            set => SetProperty(ref _newUrl, value);
        }

        private string _saveDirectory = string.Empty;
        public string SaveDirectory
        {
            get => _saveDirectory;
            set => SetProperty(ref _saveDirectory, value);
        }

        private int _threadCount = 8;
        public int ThreadCount
        {
            get => _threadCount;
            set => SetProperty(ref _threadCount, value);
        }

        private DownloadItemViewModel? _selectedItem;
        public DownloadItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetProperty(ref _selectedItem, value);
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection => _selectedItem != null;

        // --- Statistics ---
        private string _totalSpeedText = "";
        public string TotalSpeedText
        {
            get => _totalSpeedText;
            set => SetProperty(ref _totalSpeedText, value);
        }

        private int _activeCount = 0;
        public int ActiveCount
        {
            get => _activeCount;
            set => SetProperty(ref _activeCount, value);
        }

        private string _statusBarText = "Sẵn sàng";
        public string StatusBarText
        {
            get => _statusBarText;
            set => SetProperty(ref _statusBarText, value);
        }

        // --- Commands ---
        public ICommand AddDownloadCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand PauseAllCommand { get; }
        public ICommand ResumeAllCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand OpenFileLocationCommand { get; }

        public MainViewModel()
        {
            _settings = new AppSettings();
            _saveDirectory = _settings.DefaultDownloadDirectory;
            _scheduler = new DownloadScheduler(_settings);

            // Đăng ký events
            _scheduler.ItemsChanged += OnItemsChanged;
            _scheduler.ProgressUpdated += OnProgressUpdated;

            // Khởi tạo commands
            AddDownloadCommand = new AsyncRelayCommand(AddDownloadAsync, () => !string.IsNullOrWhiteSpace(NewUrl));
            PauseCommand = new RelayCommand(_ => PauseSelected(), _ => SelectedItem?.CanPause == true);
            ResumeCommand = new AsyncRelayCommand(_ => ResumeSelectedAsync(), _ => SelectedItem?.CanResume == true);
            DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedItem != null);
            PauseAllCommand = new RelayCommand(_ => PauseAll());
            ResumeAllCommand = new AsyncRelayCommand(_ => ResumeAllAsync());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            OpenFileLocationCommand = new RelayCommand(_ => OpenFileLocation(), _ => SelectedItem != null);

            // Khởi tạo Extension Server (nhận link từ browser)
            _extensionServer = new ExtensionServer(52888);
            _extensionServer.DownloadRequested += OnExtensionDownloadRequested;
            _extensionServer.GetDownloadsList = () => Downloads.Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                progress = d.Progress,
                status = d.StatusText,
                speed = d.SpeedText,
                size = d.SizeText
            }).ToList();

            try
            {
                _extensionServer.Start();
                _log.Information("Extension server started on port 52888");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Không thể khởi động Extension Server (cần chạy với quyền Admin hoặc mở port)");
            }

            // Load existing items từ scheduler
            OnItemsChanged();
        }

        /// <summary>
        /// Xử lý link download nhận từ browser extension
        /// </summary>
        private void OnExtensionDownloadRequested(string url, string filename)
        {
            Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                _log.Information("Nhận link từ extension: {Url}", url);
                StatusBarText = $"📥 Nhận từ extension: {url}";

                try
                {
                    await _scheduler.AddDownloadAsync(url, SaveDirectory, ThreadCount);
                    StatusBarText = $"✓ Đã thêm từ extension: {filename}";
                }
                catch (Exception ex)
                {
                    StatusBarText = $"Lỗi từ extension: {ex.Message}";
                    _log.Error(ex, "Lỗi thêm download từ extension");
                }
            });
        }

        /// <summary>
        /// Thêm download mới
        /// </summary>
        private async Task AddDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(NewUrl)) return;

            var url = NewUrl.Trim();

            // Validate URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                StatusBarText = "URL không hợp lệ. Vui lòng nhập URL HTTP/HTTPS.";
                return;
            }

            StatusBarText = $"Đang thêm download: {url}";
            _log.Information("Thêm download: {Url}", url);

            try
            {
                await _scheduler.AddDownloadAsync(url, SaveDirectory, ThreadCount);
                NewUrl = string.Empty;
                StatusBarText = "Đã thêm download thành công";
            }
            catch (Exception ex)
            {
                StatusBarText = $"Lỗi: {ex.Message}";
                _log.Error(ex, "Lỗi thêm download");
            }
        }

        /// <summary>
        /// Tạm dừng item đang chọn
        /// </summary>
        private void PauseSelected()
        {
            if (SelectedItem == null) return;
            _scheduler.PauseDownload(SelectedItem.Id);
            StatusBarText = $"Đã tạm dừng: {SelectedItem.FileName}";
        }

        /// <summary>
        /// Resume item đang chọn
        /// </summary>
        private async Task ResumeSelectedAsync()
        {
            if (SelectedItem == null) return;
            StatusBarText = $"Đang resume: {SelectedItem.FileName}";
            await _scheduler.ResumeDownloadAsync(SelectedItem.Id);
        }

        /// <summary>
        /// Xóa item đang chọn
        /// </summary>
        private void DeleteSelected()
        {
            if (SelectedItem == null) return;

            var result = MessageBox.Show(
                $"Bạn có muốn xóa download \"{SelectedItem.FileName}\"?\n\nChọn Yes để xóa cả file đã tải.\nChọn No để chỉ xóa khỏi danh sách.",
                "Xác nhận xóa",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            var deleteFile = result == MessageBoxResult.Yes;
            var fileName = SelectedItem.FileName;
            _scheduler.RemoveDownload(SelectedItem.Id, deleteFile);
            StatusBarText = $"Đã xóa: {fileName}";
        }

        /// <summary>
        /// Tạm dừng tất cả download
        /// </summary>
        private void PauseAll()
        {
            foreach (var item in Downloads.Where(d => d.CanPause))
            {
                _scheduler.PauseDownload(item.Id);
            }
            StatusBarText = "Đã tạm dừng tất cả";
        }

        /// <summary>
        /// Resume tất cả download
        /// </summary>
        private async Task ResumeAllAsync()
        {
            foreach (var item in Downloads.Where(d => d.CanResume))
            {
                await _scheduler.ResumeDownloadAsync(item.Id);
            }
            StatusBarText = "Đã resume tất cả";
        }

        /// <summary>
        /// Mở dialog chọn thư mục lưu
        /// </summary>
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Chọn thư mục lưu file",
                InitialDirectory = SaveDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                SaveDirectory = dialog.FolderName;
            }
        }

        /// <summary>
        /// Mở thư mục chứa file đã tải
        /// </summary>
        private void OpenFileLocation()
        {
            if (SelectedItem == null) return;
            try
            {
                var path = SelectedItem.Item.FullPath;
                if (System.IO.File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    System.Diagnostics.Process.Start("explorer.exe", SelectedItem.Item.SaveDirectory);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Không thể mở file location");
            }
        }

        // --- Event handlers ---

        /// <summary>
        /// Khi danh sách items thay đổi → refresh ObservableCollection
        /// </summary>
        private void OnItemsChanged()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var items = _scheduler.GetAllItems();
                Downloads.Clear();
                foreach (var item in items)
                {
                    Downloads.Add(new DownloadItemViewModel(item));
                }
            });
        }

        /// <summary>
        /// Khi progress cập nhật → refresh ViewModel tương ứng
        /// </summary>
        private void OnProgressUpdated(DownloadItem item)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var vm = Downloads.FirstOrDefault(d => d.Id == item.Id);
                if (vm != null)
                {
                    vm.Refresh();
                }
                else
                {
                    // Item mới, thêm vào
                    Downloads.Add(new DownloadItemViewModel(item));
                }

                // Cập nhật statistics
                var activeDownloads = Downloads.Where(d => d.Status == DownloadStatus.Downloading).ToList();
                ActiveCount = activeDownloads.Count;

                var totalSpeed = activeDownloads.Sum(d => d.Item.Speed);
                TotalSpeedText = totalSpeed > 0 ? FormatSpeed(totalSpeed) : "";

                RelayCommand.RaiseCanExecuteChanged();
            });
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
            int idx = 0;
            double speed = bytesPerSecond;
            while (speed >= 1024 && idx < units.Length - 1) { speed /= 1024; idx++; }
            return $"{speed:F2} {units[idx]}";
        }
    }
}
