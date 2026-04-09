using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KDM.Models;
using Newtonsoft.Json;
using Serilog;

namespace KDM.Storage
{
    /// <summary>
    /// Quản lý file: merge segments, lưu/đọc trạng thái download, xóa file tạm.
    /// </summary>
    public class FileManager
    {
        private readonly string _stateDirectory;
        private static readonly ILogger _log = Log.ForContext<FileManager>();

        /// <summary>Kích thước buffer cho merge operation (1MB)</summary>
        private const int MERGE_BUFFER_SIZE = 1024 * 1024;

        public FileManager()
        {
            // Thư mục lưu trạng thái download
            _stateDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KDM", "State");

            if (!Directory.Exists(_stateDirectory))
            {
                Directory.CreateDirectory(_stateDirectory);
                _log.Information("Tạo thư mục state: {Dir}", _stateDirectory);
            }
        }

        /// <summary>
        /// Tạo đường dẫn file tạm cho một segment.
        /// Ví dụ: video.mp4.part0, video.mp4.part1, ...
        /// </summary>
        public string GetSegmentTempPath(DownloadItem item, int segmentIndex)
        {
            return Path.Combine(item.SaveDirectory, $"{item.FileName}.part{segmentIndex}");
        }

        /// <summary>
        /// Merge tất cả các segment thành file hoàn chỉnh.
        /// Ghi tuần tự từ part0 -> partN vào file cuối cùng.
        /// </summary>
        public void MergeSegments(DownloadItem item)
        {
            var outputPath = item.FullPath;
            _log.Information("Bắt đầu merge {Count} segments vào {Output}", item.Segments.Count, outputPath);

            // Sắp xếp segments theo index
            var sortedSegments = item.Segments.OrderBy(s => s.Index).ToList();

            using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[MERGE_BUFFER_SIZE];

                foreach (var segment in sortedSegments)
                {
                    if (!File.Exists(segment.TempFilePath))
                    {
                        throw new IOException($"Segment file không tồn tại: {segment.TempFilePath}");
                    }

                    using (var inputStream = new FileStream(segment.TempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        int bytesRead;
                        while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outputStream.Write(buffer, 0, bytesRead);
                        }
                    }

                    _log.Debug("Merged segment {Index}", segment.Index);
                }
            }

            _log.Information("Merge hoàn tất: {Output}", outputPath);
        }

        /// <summary>
        /// Xóa tất cả file tạm (segment files) của một download item
        /// </summary>
        public void CleanupTempFiles(DownloadItem item)
        {
            foreach (var segment in item.Segments)
            {
                try
                {
                    if (File.Exists(segment.TempFilePath))
                    {
                        File.Delete(segment.TempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Không thể xóa file tạm: {Path}", segment.TempFilePath);
                }
            }

            // Xóa file state
            DeleteState(item.Id);
        }

        /// <summary>
        /// Lưu trạng thái download vào file JSON (để resume sau này)
        /// </summary>
        public void SaveState(DownloadItem item)
        {
            var statePath = Path.Combine(_stateDirectory, $"{item.Id}.json");
            var json = JsonConvert.SerializeObject(item, Formatting.Indented);
            File.WriteAllText(statePath, json);
        }

        /// <summary>
        /// Đọc trạng thái download từ file JSON
        /// </summary>
        public DownloadItem? LoadState(string itemId)
        {
            var statePath = Path.Combine(_stateDirectory, $"{itemId}.json");
            if (!File.Exists(statePath)) return null;

            var json = File.ReadAllText(statePath);
            return JsonConvert.DeserializeObject<DownloadItem>(json);
        }

        /// <summary>
        /// Lấy tất cả trạng thái download đã lưu (để restore khi mở app)
        /// </summary>
        public List<DownloadItem> LoadAllStates()
        {
            var items = new List<DownloadItem>();

            if (!Directory.Exists(_stateDirectory)) return items;

            foreach (var file in Directory.GetFiles(_stateDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var item = JsonConvert.DeserializeObject<DownloadItem>(json);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Không thể đọc state file: {File}", file);
                }
            }

            return items;
        }

        /// <summary>
        /// Xóa file state
        /// </summary>
        public void DeleteState(string itemId)
        {
            var statePath = Path.Combine(_stateDirectory, $"{itemId}.json");
            try
            {
                if (File.Exists(statePath))
                {
                    File.Delete(statePath);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Không thể xóa state file: {Path}", statePath);
            }
        }

        /// <summary>
        /// Đảm bảo thư mục lưu file tồn tại
        /// </summary>
        public void EnsureDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
