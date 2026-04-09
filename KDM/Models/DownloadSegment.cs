using Newtonsoft.Json;

namespace KDM.Models
{
    /// <summary>
    /// Trạng thái của một segment
    /// </summary>
    public enum SegmentStatus
    {
        Pending,      // Chưa bắt đầu
        Downloading,  // Đang tải
        Completed,    // Hoàn thành
        Failed        // Lỗi
    }

    /// <summary>
    /// Đại diện cho một phần (segment) của file cần tải.
    /// Multi-thread download chia file thành nhiều segment, mỗi segment tải riêng biệt.
    /// </summary>
    public class DownloadSegment
    {
        /// <summary>Index của segment (0, 1, 2, ...)</summary>
        public int Index { get; set; }

        /// <summary>Byte bắt đầu trong file gốc</summary>
        public long StartByte { get; set; }

        /// <summary>Byte kết thúc trong file gốc</summary>
        public long EndByte { get; set; }

        /// <summary>Số bytes đã tải được cho segment này</summary>
        public long DownloadedBytes { get; set; } = 0;

        /// <summary>Trạng thái segment</summary>
        public SegmentStatus Status { get; set; } = SegmentStatus.Pending;

        /// <summary>Đường dẫn file tạm cho segment này (.part0, .part1, ...)</summary>
        public string TempFilePath { get; set; } = string.Empty;

        /// <summary>Tổng dung lượng segment cần tải</summary>
        [JsonIgnore]
        public long TotalBytes => EndByte - StartByte + 1;

        /// <summary>Vị trí byte hiện tại đang tải (để resume)</summary>
        [JsonIgnore]
        public long CurrentPosition => StartByte + DownloadedBytes;

        /// <summary>Segment đã tải xong chưa</summary>
        [JsonIgnore]
        public bool IsCompleted => DownloadedBytes >= TotalBytes;
    }
}
