using System;
using KDM.Models;

namespace KDM.UI
{
    /// <summary>
    /// ViewModel wrapper cho mỗi DownloadItem - để binding vào UI.
    /// Chứa các property formatted cho hiển thị.
    /// </summary>
    public class DownloadItemViewModel : ViewModelBase
    {
        private readonly DownloadItem _item;

        public DownloadItemViewModel(DownloadItem item)
        {
            _item = item;
        }

        /// <summary>Model bên dưới</summary>
        public DownloadItem Item => _item;

        /// <summary>ID duy nhất</summary>
        public string Id => _item.Id;

        /// <summary>Tên file</summary>
        public string FileName => _item.FileName;

        /// <summary>URL nguồn</summary>
        public string Url => _item.Url;

        /// <summary>Tiến độ (0-100)</summary>
        public double Progress => _item.Progress;

        /// <summary>Trạng thái hiển thị</summary>
        public string StatusText
        {
            get
            {
                return _item.Status switch
                {
                    DownloadStatus.Queued => "Đang chờ",
                    DownloadStatus.Downloading => "Đang tải",
                    DownloadStatus.Paused => "Tạm dừng",
                    DownloadStatus.Completed => "Hoàn tất",
                    DownloadStatus.Failed => $"Lỗi: {_item.ErrorMessage}",
                    DownloadStatus.Merging => "Đang ghép file...",
                    _ => "N/A"
                };
            }
        }

        /// <summary>Trạng thái (enum)</summary>
        public DownloadStatus Status => _item.Status;

        /// <summary>Tốc độ tải hiển thị</summary>
        public string SpeedText
        {
            get
            {
                if (_item.Status != DownloadStatus.Downloading) return "";
                return FormatSpeed(_item.Speed);
            }
        }

        /// <summary>Dung lượng đã tải / tổng dung lượng</summary>
        public string SizeText
        {
            get
            {
                var downloaded = FormatSize(_item.DownloadedSize);
                if (_item.TotalSize > 0)
                {
                    var total = FormatSize(_item.TotalSize);
                    return $"{downloaded} / {total}";
                }
                return downloaded;
            }
        }

        /// <summary>Thời gian còn lại ước tính</summary>
        public string EtaText
        {
            get
            {
                if (_item.Status != DownloadStatus.Downloading || _item.EtaSeconds <= 0) return "";
                var ts = TimeSpan.FromSeconds(_item.EtaSeconds);
                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m";
                if (ts.TotalMinutes >= 1)
                    return $"{ts.Minutes}m {ts.Seconds}s";
                return $"{ts.Seconds}s";
            }
        }

        /// <summary>Số thread đang sử dụng</summary>
        public string ThreadsText => $"{_item.ThreadCount} threads";

        /// <summary>Có thể pause không</summary>
        public bool CanPause => _item.Status == DownloadStatus.Downloading;

        /// <summary>Có thể resume không</summary>
        public bool CanResume => _item.Status == DownloadStatus.Paused || _item.Status == DownloadStatus.Failed;

        /// <summary>
        /// Cập nhật tất cả bindings (gọi khi data thay đổi)
        /// </summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(SpeedText));
            OnPropertyChanged(nameof(SizeText));
            OnPropertyChanged(nameof(EtaText));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(FileName));
        }

        // --- Format helpers ---

        /// <summary>Format byte count thành đơn vị dễ đọc</summary>
        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int idx = 0;
            double size = bytes;
            while (size >= 1024 && idx < units.Length - 1)
            {
                size /= 1024;
                idx++;
            }
            return $"{size:F2} {units[idx]}";
        }

        /// <summary>Format speed (bytes/s) thành đơn vị dễ đọc</summary>
        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
            int idx = 0;
            double speed = bytesPerSecond;
            while (speed >= 1024 && idx < units.Length - 1)
            {
                speed /= 1024;
                idx++;
            }
            return $"{speed:F2} {units[idx]}";
        }
    }
}
