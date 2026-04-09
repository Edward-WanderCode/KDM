namespace KDM.Models
{
    /// <summary>
    /// Cấu hình ứng dụng
    /// </summary>
    public class AppSettings
    {
        /// <summary>Thư mục lưu file mặc định</summary>
        public string DefaultDownloadDirectory { get; set; } = 
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        /// <summary>Số thread mặc định cho mỗi download</summary>
        public int DefaultThreadCount { get; set; } = 8;

        /// <summary>Số download đồng thời tối đa</summary>
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>Số lần retry tối đa khi lỗi</summary>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>Timeout cho mỗi request (giây)</summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>Kích thước buffer khi đọc stream (bytes)</summary>
        public int BufferSize { get; set; } = 8192;

        /// <summary>Giới hạn băng thông mặc định (bytes/s), 0 = không giới hạn</summary>
        public long DefaultSpeedLimit { get; set; } = 0;

        /// <summary>Proxy URL (nếu có)</summary>
        public string? ProxyUrl { get; set; }

        /// <summary>Proxy username</summary>
        public string? ProxyUsername { get; set; }

        /// <summary>Proxy password</summary>
        public string? ProxyPassword { get; set; }
    }
}
