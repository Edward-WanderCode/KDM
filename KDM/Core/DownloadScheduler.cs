using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KDM.Models;
using KDM.Storage;
using Serilog;

namespace KDM.Core
{
    /// <summary>
    /// Download Scheduler - quản lý hàng đợi download.
    /// Giới hạn số download đồng thời, tự động bắt đầu download tiếp theo khi có slot trống.
    /// </summary>
    public class DownloadScheduler
    {
        private readonly AppSettings _settings;
        private readonly DownloadEngine _engine;
        private readonly FileManager _fileManager;
        private static readonly ILogger _log = Log.ForContext<DownloadScheduler>();

        /// <summary>Danh sách tất cả download items</summary>
        private readonly List<DownloadItem> _items = new();

        /// <summary>Map từ ID -> CancellationTokenSource (để có thể cancel/pause)</summary>
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

        /// <summary>Map từ ID -> Task đang chạy</summary>
        private readonly ConcurrentDictionary<string, Task> _runningTasks = new();

        /// <summary>Semaphore giới hạn số download đồng thời</summary>
        private SemaphoreSlim _concurrencySemaphore;

        /// <summary>Lock cho danh sách items</summary>
        private readonly object _lock = new();

        /// <summary>Event khi danh sách thay đổi (add/remove)</summary>
        public event Action? ItemsChanged;

        /// <summary>Event khi progress thay đổi (để UI update)</summary>
        public event Action<DownloadItem>? ProgressUpdated;

        public DownloadScheduler(AppSettings settings)
        {
            _settings = settings;
            _engine = new DownloadEngine(settings);
            _fileManager = _engine.GetFileManager();
            _concurrencySemaphore = new SemaphoreSlim(settings.MaxConcurrentDownloads);

            // Đăng ký events từ engine
            _engine.ProgressChanged += OnProgressChanged;
            _engine.DownloadCompleted += OnDownloadCompleted;
            _engine.DownloadFailed += OnDownloadFailed;

            // Restore các download cũ
            RestorePreviousDownloads();
        }

        /// <summary>
        /// Khôi phục trạng thái download từ lần chạy trước
        /// </summary>
        private void RestorePreviousDownloads()
        {
            var savedItems = _fileManager.LoadAllStates();
            foreach (var item in savedItems)
            {
                // Chỉ restore các download chưa hoàn thành
                if (item.Status != DownloadStatus.Completed)
                {
                    item.Status = DownloadStatus.Paused;
                    item.Speed = 0;
                    lock (_lock) { _items.Add(item); }
                    _log.Information("Restored download: {FileName} ({Progress:F1}%)",
                        item.FileName, item.Progress);
                }
            }

            if (savedItems.Any())
            {
                ItemsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Thêm một download mới vào hàng đợi và bắt đầu tải
        /// </summary>
        public async Task<DownloadItem> AddDownloadAsync(string url, string saveDirectory, int? threadCount = null)
        {
            var item = new DownloadItem
            {
                Url = url,
                SaveDirectory = saveDirectory,
                ThreadCount = threadCount ?? _settings.DefaultThreadCount,
                SpeedLimit = _settings.DefaultSpeedLimit
            };

            lock (_lock) { _items.Add(item); }
            ItemsChanged?.Invoke();

            // Chuẩn bị download (lấy thông tin file, tạo segments)
            try
            {
                await _engine.PrepareDownloadAsync(item);
                ItemsChanged?.Invoke();

                // Bắt đầu download
                _ = StartItemAsync(item);
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = ex.Message;
                ProgressUpdated?.Invoke(item);
                _log.Error(ex, "Không thể chuẩn bị download: {Url}", url);
            }

            return item;
        }

        /// <summary>
        /// Bắt đầu tải một item (có kiểm tra semaphore)
        /// </summary>
        private async Task StartItemAsync(DownloadItem item)
        {
            // Chờ slot trống
            await _concurrencySemaphore.WaitAsync();

            var cts = new CancellationTokenSource();
            _cancellationTokens[item.Id] = cts;

            var task = Task.Run(async () =>
            {
                try
                {
                    await _engine.StartDownloadAsync(item, cts.Token);
                }
                finally
                {
                    _concurrencySemaphore.Release();
                    _cancellationTokens.TryRemove(item.Id, out _);
                    _runningTasks.TryRemove(item.Id, out _);
                }
            });

            _runningTasks[item.Id] = task;
        }

        /// <summary>
        /// Tạm dừng download
        /// </summary>
        public void PauseDownload(string itemId)
        {
            var item = GetItem(itemId);
            if (item == null || item.Status != DownloadStatus.Downloading) return;

            item.Status = DownloadStatus.Paused;

            if (_cancellationTokens.TryGetValue(itemId, out var cts))
            {
                cts.Cancel();
            }

            _log.Information("Paused: {FileName}", item.FileName);
        }

        /// <summary>
        /// Resume download đã tạm dừng
        /// </summary>
        public async Task ResumeDownloadAsync(string itemId)
        {
            var item = GetItem(itemId);
            if (item == null || item.Status != DownloadStatus.Paused) return;

            _log.Information("Resuming: {FileName}", item.FileName);

            // Kiểm tra lại segments chưa xong
            if (item.Segments.All(s => s.IsCompleted))
            {
                // Tất cả segments đã xong, chỉ cần merge
                item.Status = DownloadStatus.Merging;
                ProgressUpdated?.Invoke(item);

                try
                {
                    _fileManager.MergeSegments(item);
                    _fileManager.CleanupTempFiles(item);
                    item.Status = DownloadStatus.Completed;
                    item.EndTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = ex.Message;
                }

                ProgressUpdated?.Invoke(item);
                return;
            }

            _ = StartItemAsync(item);
        }

        /// <summary>
        /// Xóa download khỏi danh sách
        /// </summary>
        public void RemoveDownload(string itemId, bool deleteFile = false)
        {
            var item = GetItem(itemId);
            if (item == null) return;

            // Cancel nếu đang chạy
            if (_cancellationTokens.TryGetValue(itemId, out var cts))
            {
                cts.Cancel();
            }

            // Xóa temp files
            _fileManager.CleanupTempFiles(item);

            // Xóa file đã tải nếu yêu cầu
            if (deleteFile && System.IO.File.Exists(item.FullPath))
            {
                try { System.IO.File.Delete(item.FullPath); }
                catch (Exception ex) { _log.Warning(ex, "Không thể xóa file: {Path}", item.FullPath); }
            }

            lock (_lock) { _items.Remove(item); }
            ItemsChanged?.Invoke();

            _log.Information("Removed: {FileName}", item.FileName);
        }

        /// <summary>
        /// Lấy danh sách tất cả items (copy)
        /// </summary>
        public List<DownloadItem> GetAllItems()
        {
            lock (_lock) { return new List<DownloadItem>(_items); }
        }

        /// <summary>
        /// Lấy item theo ID
        /// </summary>
        public DownloadItem? GetItem(string itemId)
        {
            lock (_lock) { return _items.FirstOrDefault(i => i.Id == itemId); }
        }

        /// <summary>
        /// Cập nhật số download đồng thời tối đa
        /// </summary>
        public void UpdateMaxConcurrent(int maxConcurrent)
        {
            _settings.MaxConcurrentDownloads = maxConcurrent;
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrent);
        }

        // --- Event handlers ---

        private void OnProgressChanged(DownloadItem item)
        {
            ProgressUpdated?.Invoke(item);
        }

        private void OnDownloadCompleted(DownloadItem item)
        {
            _log.Information("Completed: {FileName} ({Size} bytes)", item.FileName, item.TotalSize);
            ProgressUpdated?.Invoke(item);

            // Tìm download tiếp theo trong queue
            TryStartNextQueued();
        }

        private void OnDownloadFailed(DownloadItem item, string error)
        {
            _log.Error("Failed: {FileName} - {Error}", item.FileName, error);
            ProgressUpdated?.Invoke(item);
        }

        /// <summary>
        /// Tìm và bắt đầu download tiếp theo trong queue (nếu có slot)
        /// </summary>
        private void TryStartNextQueued()
        {
            DownloadItem? nextItem;
            lock (_lock)
            {
                nextItem = _items.FirstOrDefault(i => i.Status == DownloadStatus.Queued);
            }

            if (nextItem != null)
            {
                _ = StartItemAsync(nextItem);
            }
        }
    }
}
