using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KDM.Models;
using KDM.Network;
using KDM.Storage;
using Serilog;

namespace KDM.Core
{
    /// <summary>
    /// Download Engine - module trung tâm điều khiển quá trình tải file.
    /// Hỗ trợ multi-thread download với Range request, resume, speed limit.
    /// </summary>
    public class DownloadEngine
    {
        private readonly HttpDownloadClient _httpClient;
        private readonly FileManager _fileManager;
        private readonly AppSettings _settings;
        private static readonly ILogger _log = Log.ForContext<DownloadEngine>();

        /// <summary>Lock để ghi file an toàn (tránh corrupt)</summary>
        private readonly object _fileLock = new();

        /// <summary>Event báo cáo tiến độ download</summary>
        public event Action<DownloadItem>? ProgressChanged;

        /// <summary>Event báo download hoàn thành</summary>
        public event Action<DownloadItem>? DownloadCompleted;

        /// <summary>Event báo download lỗi</summary>
        public event Action<DownloadItem, string>? DownloadFailed;

        public DownloadEngine(AppSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpDownloadClient(settings);
            _fileManager = new FileManager();
        }

        /// <summary>
        /// Chuẩn bị download: lấy thông tin file, tạo segments.
        /// Gọi trước khi bắt đầu tải.
        /// </summary>
        public async Task PrepareDownloadAsync(DownloadItem item, CancellationToken ct = default)
        {
            _log.Information("Chuẩn bị download: {Url}", item.Url);

            // Lấy thông tin file từ server
            var fileInfo = await _httpClient.GetFileInfoAsync(item.Url, ct);

            // Cập nhật thông tin
            if (string.IsNullOrEmpty(item.FileName))
            {
                item.FileName = fileInfo.FileName;
            }
            item.TotalSize = fileInfo.ContentLength;
            item.SupportsRange = fileInfo.SupportsRange;

            // Đảm bảo thư mục lưu file tồn tại
            _fileManager.EnsureDirectory(item.SaveDirectory);

            // Tạo segments nếu server hỗ trợ Range và file đủ lớn
            if (item.SupportsRange && item.TotalSize > 0)
            {
                CreateSegments(item);
            }
            else
            {
                // Fallback: single thread, 1 segment duy nhất
                _log.Warning("Server không hỗ trợ Range hoặc không biết dung lượng. Fallback single-thread.");
                item.ThreadCount = 1;
                item.Segments = new List<DownloadSegment>
                {
                    new DownloadSegment
                    {
                        Index = 0,
                        StartByte = 0,
                        EndByte = item.TotalSize > 0 ? item.TotalSize - 1 : 0,
                        TempFilePath = _fileManager.GetSegmentTempPath(item, 0)
                    }
                };
            }

            // Lưu state ban đầu
            _fileManager.SaveState(item);
        }

        /// <summary>
        /// Chia file thành nhiều segment dựa trên số thread
        /// </summary>
        private void CreateSegments(DownloadItem item)
        {
            item.Segments.Clear();
            var segmentSize = item.TotalSize / item.ThreadCount;
            var remainder = item.TotalSize % item.ThreadCount;

            long currentByte = 0;
            for (int i = 0; i < item.ThreadCount; i++)
            {
                var size = segmentSize + (i < remainder ? 1 : 0);
                var segment = new DownloadSegment
                {
                    Index = i,
                    StartByte = currentByte,
                    EndByte = currentByte + size - 1,
                    TempFilePath = _fileManager.GetSegmentTempPath(item, i)
                };
                item.Segments.Add(segment);
                currentByte += size;
            }

            _log.Information("Tạo {Count} segments, mỗi segment ~{Size} bytes",
                item.Segments.Count, segmentSize);
        }

        /// <summary>
        /// Bắt đầu download. Tải tất cả segments song song bằng thread pool.
        /// </summary>
        public async Task StartDownloadAsync(DownloadItem item, CancellationToken ct)
        {
            item.Status = DownloadStatus.Downloading;
            item.StartTime = DateTime.Now;

            _log.Information("Bắt đầu download: {FileName} ({Threads} threads)", item.FileName, item.ThreadCount);

            try
            {
                if (!item.SupportsRange || item.TotalSize <= 0)
                {
                    // Single-thread download (fallback)
                    await DownloadSingleThreadAsync(item, ct);
                }
                else
                {
                    // Multi-thread download
                    await DownloadMultiThreadAsync(item, ct);
                }

                // Kiểm tra lại nếu bị cancel
                ct.ThrowIfCancellationRequested();

                // Merge segments thành file hoàn chỉnh
                item.Status = DownloadStatus.Merging;
                ProgressChanged?.Invoke(item);

                _fileManager.MergeSegments(item);
                _fileManager.CleanupTempFiles(item);

                item.Status = DownloadStatus.Completed;
                item.EndTime = DateTime.Now;
                item.Speed = 0;
                ProgressChanged?.Invoke(item);
                DownloadCompleted?.Invoke(item);

                _log.Information("Download hoàn tất: {FileName}", item.FileName);
            }
            catch (OperationCanceledException)
            {
                // Pause hoặc cancel - lưu state để resume sau
                if (item.Status != DownloadStatus.Paused)
                {
                    item.Status = DownloadStatus.Paused;
                }
                _fileManager.SaveState(item);
                ProgressChanged?.Invoke(item);
                _log.Information("Download tạm dừng: {FileName}", item.FileName);
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = ex.Message;
                _fileManager.SaveState(item);
                ProgressChanged?.Invoke(item);
                DownloadFailed?.Invoke(item, ex.Message);

                _log.Error(ex, "Download lỗi: {FileName}", item.FileName);
            }
        }

        /// <summary>
        /// Download multi-thread: tải tất cả segments song song.
        /// Sử dụng SemaphoreSlim để giới hạn số thread chạy đồng thời.
        /// </summary>
        private async Task DownloadMultiThreadAsync(DownloadItem item, CancellationToken ct)
        {
            // Timer để cập nhật speed và progress định kỳ
            var speedTimer = new Stopwatch();
            speedTimer.Start();
            long lastDownloaded = item.DownloadedSize;

            // Tạo timer để cập nhật UI mỗi 500ms
            var progressTimer = new System.Timers.Timer(500);
            progressTimer.Elapsed += (s, e) =>
            {
                // Tính tổng bytes đã tải
                var totalDownloaded = item.Segments.Sum(seg => seg.DownloadedBytes);
                item.DownloadedSize = totalDownloaded;

                // Tính speed
                var elapsed = speedTimer.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    item.Speed = (totalDownloaded - lastDownloaded) / elapsed;
                    lastDownloaded = totalDownloaded;
                    speedTimer.Restart();
                }

                // Tính ETA
                if (item.Speed > 0 && item.TotalSize > 0)
                {
                    item.EtaSeconds = (item.TotalSize - totalDownloaded) / item.Speed;
                }

                // Lưu state định kỳ (mỗi lần update)
                _fileManager.SaveState(item);

                ProgressChanged?.Invoke(item);
            };
            progressTimer.Start();

            try
            {
                // Lọc ra các segment chưa hoàn thành (để hỗ trợ resume)
                var pendingSegments = item.Segments.Where(s => !s.IsCompleted).ToList();

                // Tải các segments song song
                var tasks = pendingSegments.Select(segment =>
                    Task.Run(() => DownloadSegmentAsync(item, segment, ct), ct)
                ).ToArray();

                await Task.WhenAll(tasks);
            }
            finally
            {
                progressTimer.Stop();
                progressTimer.Dispose();
                speedTimer.Stop();

                // Cập nhật lần cuối
                item.DownloadedSize = item.Segments.Sum(seg => seg.DownloadedBytes);
                ProgressChanged?.Invoke(item);
            }
        }

        /// <summary>
        /// Tải một segment cụ thể. Chạy trên thread pool.
        /// Hỗ trợ resume từ vị trí đã tải dở.
        /// </summary>
        private async Task DownloadSegmentAsync(DownloadItem item, DownloadSegment segment, CancellationToken ct)
        {
            int retryCount = 0;

            while (retryCount <= _settings.MaxRetryCount)
            {
                try
                {
                    segment.Status = SegmentStatus.Downloading;

                    // Tính vị trí bắt đầu (có tính resume)
                    var startByte = segment.StartByte + segment.DownloadedBytes;
                    var endByte = segment.EndByte;

                    if (startByte > endByte)
                    {
                        // Segment đã tải xong
                        segment.Status = SegmentStatus.Completed;
                        return;
                    }

                    _log.Debug("Tải segment {Index}: bytes {Start}-{End}",
                        segment.Index, startByte, endByte);

                    using var stream = await _httpClient.GetRangeStreamAsync(item.Url, startByte, endByte, ct);

                    // Mở file để ghi (append nếu resume)
                    var fileMode = segment.DownloadedBytes > 0 ? FileMode.Append : FileMode.Create;
                    using var fileStream = new FileStream(
                        segment.TempFilePath, fileMode, FileAccess.Write, FileShare.None);

                    var buffer = new byte[_settings.BufferSize];
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();

                        fileStream.Write(buffer, 0, bytesRead);
                        segment.DownloadedBytes += bytesRead;

                        // Giới hạn băng thông nếu có cấu hình
                        if (item.SpeedLimit > 0)
                        {
                            var delayMs = (int)(bytesRead * 1000.0 / (item.SpeedLimit / item.ThreadCount));
                            if (delayMs > 0)
                            {
                                await Task.Delay(delayMs, ct);
                            }
                        }
                    }

                    segment.Status = SegmentStatus.Completed;
                    _log.Debug("Segment {Index} hoàn tất", segment.Index);
                    return; // Thoát vòng retry

                }
                catch (OperationCanceledException)
                {
                    throw; // Không retry khi user cancel/pause
                }
                catch (Exception ex)
                {
                    retryCount++;
                    segment.Status = SegmentStatus.Failed;

                    if (retryCount > _settings.MaxRetryCount)
                    {
                        _log.Error(ex, "Segment {Index} thất bại sau {Retries} lần retry",
                            segment.Index, _settings.MaxRetryCount);
                        throw;
                    }

                    // Exponential backoff: 1s, 2s, 4s, 8s, 16s...
                    var delay = (int)Math.Pow(2, retryCount) * 1000;
                    _log.Warning("Segment {Index} lỗi, retry {Count}/{Max} sau {Delay}ms: {Error}",
                        segment.Index, retryCount, _settings.MaxRetryCount, delay, ex.Message);

                    await Task.Delay(delay, ct);
                }
            }
        }

        /// <summary>
        /// Download single-thread (fallback khi server không hỗ trợ Range)
        /// </summary>
        private async Task DownloadSingleThreadAsync(DownloadItem item, CancellationToken ct)
        {
            _log.Information("Single-thread download: {FileName}", item.FileName);

            var segment = item.Segments.First();
            segment.Status = SegmentStatus.Downloading;

            var speedTimer = new Stopwatch();
            speedTimer.Start();
            long lastDownloaded = 0;

            int retryCount = 0;

            while (retryCount <= _settings.MaxRetryCount)
            {
                try
                {
                    using var stream = await _httpClient.GetFullStreamAsync(item.Url, ct);
                    using var fileStream = new FileStream(
                        segment.TempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[_settings.BufferSize];
                    int bytesRead;
                    long totalRead = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();

                        fileStream.Write(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        segment.DownloadedBytes = totalRead;
                        item.DownloadedSize = totalRead;

                        // Cập nhật tổng dung lượng nếu chưa biết
                        if (item.TotalSize <= 0)
                        {
                            item.TotalSize = totalRead;
                        }

                        // Tính speed mỗi 500ms
                        if (speedTimer.ElapsedMilliseconds >= 500)
                        {
                            item.Speed = (totalRead - lastDownloaded) / speedTimer.Elapsed.TotalSeconds;
                            lastDownloaded = totalRead;
                            speedTimer.Restart();

                            if (item.Speed > 0 && item.TotalSize > 0)
                            {
                                item.EtaSeconds = (item.TotalSize - totalRead) / item.Speed;
                            }

                            ProgressChanged?.Invoke(item);
                        }

                        // Speed limit
                        if (item.SpeedLimit > 0)
                        {
                            var delayMs = (int)(bytesRead * 1000.0 / item.SpeedLimit);
                            if (delayMs > 0) await Task.Delay(delayMs, ct);
                        }
                    }

                    segment.Status = SegmentStatus.Completed;
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > _settings.MaxRetryCount) throw;

                    var delay = (int)Math.Pow(2, retryCount) * 1000;
                    _log.Warning("Single-thread lỗi, retry {Count}/{Max}: {Error}",
                        retryCount, _settings.MaxRetryCount, ex.Message);
                    await Task.Delay(delay, ct);
                }
            }
        }

        /// <summary>
        /// Lấy FileManager instance (để UI có thể truy cập)
        /// </summary>
        public FileManager GetFileManager() => _fileManager;
    }
}
