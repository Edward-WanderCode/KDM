using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KDM.Models
{
    /// <summary>
    /// Trạng thái của một download item
    /// </summary>
    public enum DownloadStatus
    {
        Queued,       // Đang chờ trong hàng đợi
        Downloading,  // Đang tải
        Paused,       // Tạm dừng
        Completed,    // Hoàn thành
        Failed,       // Thất bại
        Merging       // Đang merge các segment
    }

    /// <summary>
    /// Model chính đại diện cho một download task
    /// </summary>
    public class DownloadItem
    {
        /// <summary>ID duy nhất cho mỗi download</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>URL nguồn để tải</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Tên file lưu</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Đường dẫn thư mục lưu file</summary>
        public string SaveDirectory { get; set; } = string.Empty;

        /// <summary>Đường dẫn đầy đủ đến file cuối cùng</summary>
        [JsonIgnore]
        public string FullPath => System.IO.Path.Combine(SaveDirectory, FileName);

        /// <summary>Tổng dung lượng file (bytes), -1 nếu chưa biết</summary>
        public long TotalSize { get; set; } = -1;

        /// <summary>Số bytes đã tải được</summary>
        public long DownloadedSize { get; set; } = 0;

        /// <summary>Trạng thái hiện tại</summary>
        public DownloadStatus Status { get; set; } = DownloadStatus.Queued;

        /// <summary>Số thread (segment) sử dụng</summary>
        public int ThreadCount { get; set; } = 8;

        /// <summary>Server có hỗ trợ Range request không</summary>
        public bool SupportsRange { get; set; } = false;

        /// <summary>Danh sách các segment đã/đang tải</summary>
        public List<DownloadSegment> Segments { get; set; } = new();

        /// <summary>Thời điểm bắt đầu tải</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>Thời điểm hoàn thành</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>Tốc độ tải hiện tại (bytes/s)</summary>
        [JsonIgnore]
        public double Speed { get; set; } = 0;

        /// <summary>Thời gian còn lại ước tính (giây)</summary>
        [JsonIgnore]
        public double EtaSeconds { get; set; } = 0;

        /// <summary>Phần trăm hoàn thành (0-100)</summary>
        [JsonIgnore]
        public double Progress
        {
            get
            {
                if (TotalSize <= 0) return 0;
                var pct = (double)DownloadedSize / TotalSize * 100.0;
                return Math.Min(pct, 100.0); // Cap tại 100%
            }
        }

        /// <summary>Số lần retry đã thực hiện</summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>Thông báo lỗi nếu có</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Giới hạn băng thông (bytes/s), 0 = không giới hạn</summary>
        public long SpeedLimit { get; set; } = 0;
    }
}
