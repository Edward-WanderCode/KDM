using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using KDM.Models;
using Serilog;

namespace KDM.Network
{
    /// <summary>
    /// HTTP Client wrapper - xử lý tất cả các request mạng.
    /// Hỗ trợ Range request, proxy, timeout, và retry.
    /// </summary>
    public class HttpDownloadClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private static readonly ILogger _log = Log.ForContext<HttpDownloadClient>();

        public HttpDownloadClient(AppSettings settings)
        {
            _settings = settings;

            var handler = new HttpClientHandler
            {
                // Cho phép auto redirect
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                // KHÔNG dùng AutomaticDecompression vì download manager
                // cần tải raw bytes - nếu bật sẽ decompress khiến
                // bytes tải được > Content-Length → progress > 100%
                AutomaticDecompression = DecompressionMethods.None
            };

            // Cấu hình proxy nếu có
            if (!string.IsNullOrEmpty(settings.ProxyUrl))
            {
                var proxy = new WebProxy(settings.ProxyUrl);
                if (!string.IsNullOrEmpty(settings.ProxyUsername))
                {
                    proxy.Credentials = new NetworkCredential(settings.ProxyUsername, settings.ProxyPassword);
                }
                handler.Proxy = proxy;
                handler.UseProxy = true;
                _log.Information("Sử dụng proxy: {ProxyUrl}", settings.ProxyUrl);
            }

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds)
            };

            // User-Agent giống trình duyệt để tránh bị block
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Lấy thông tin file từ server (HEAD request, fallback GET).
        /// Trả về: tổng dung lượng, tên file, có hỗ trợ Range không.
        /// </summary>
        public async Task<FileInfo> GetFileInfoAsync(string url, CancellationToken ct = default)
        {
            _log.Information("Lấy thông tin file từ: {Url}", url);

            HttpResponseMessage response;

            // Thử HEAD trước
            try
            {
                var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                response = await _httpClient.SendAsync(headRequest, ct);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // Một số server không hỗ trợ HEAD → thử GET với ResponseHeadersRead
                _log.Warning("HEAD request thất bại, thử GET: {Url}", url);
                var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                response = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
            }

            var info = new FileInfo
            {
                ContentLength = response.Content.Headers.ContentLength ?? -1,
                SupportsRange = response.Headers.AcceptRanges?.Contains("bytes") == true,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? ""
            };

            // === LẤY TÊN FILE ===
            info.FileName = ExtractFileName(response, url);

            // === ĐẢM BẢO CÓ EXTENSION ===
            info.FileName = EnsureFileExtension(info.FileName, info.ContentType);

            // Kiểm tra Range support bằng cách thử request Range nếu HEAD không trả về AcceptRanges
            if (!info.SupportsRange && info.ContentLength > 0)
            {
                info.SupportsRange = await TestRangeSupportAsync(url, ct);
            }

            _log.Information("File info: Name={FileName}, Size={Size}, ContentType={ContentType}, Range={Range}",
                info.FileName, info.ContentLength, info.ContentType, info.SupportsRange);

            return info;
        }

        /// <summary>
        /// Trích xuất tên file từ HTTP response.
        /// Ưu tiên: Content-Disposition → URL path → fallback "download"
        /// </summary>
        private static string ExtractFileName(HttpResponseMessage response, string originalUrl)
        {
            // 1. Thử lấy từ Content-Disposition header
            var disposition = response.Content.Headers.ContentDisposition;
            if (disposition != null)
            {
                // Ưu tiên FileNameStar (filename*= với encoding UTF-8)
                if (!string.IsNullOrWhiteSpace(disposition.FileNameStar))
                {
                    var name = disposition.FileNameStar.Trim('"', '\'', ' ');
                    if (!string.IsNullOrEmpty(name)) return SanitizeFileName(name);
                }

                // Sau đó thử FileName (filename=)
                if (!string.IsNullOrWhiteSpace(disposition.FileName))
                {
                    var name = disposition.FileName.Trim('"', '\'', ' ');
                    if (!string.IsNullOrEmpty(name)) return SanitizeFileName(name);
                }
            }

            // 2. Thử parse Content-Disposition header thủ công (các server trả format lạ)
            if (response.Content.Headers.TryGetValues("Content-Disposition", out var cdValues))
            {
                foreach (var cdValue in cdValues)
                {
                    var parsed = ParseContentDispositionManual(cdValue);
                    if (!string.IsNullOrEmpty(parsed)) return SanitizeFileName(parsed);
                }
            }

            // 3. Lấy từ URL (có thể là redirect URL)
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? originalUrl;
            var nameFromUrl = ExtractFileNameFromUrl(finalUrl);
            if (!string.IsNullOrEmpty(nameFromUrl)) return SanitizeFileName(nameFromUrl);

            // 4. Fallback
            return "download";
        }

        /// <summary>
        /// Parse Content-Disposition header thủ công cho các format không chuẩn.
        /// Ví dụ: attachment; filename="test file.zip"
        ///        attachment; filename=test.zip
        ///        attachment; filename*=UTF-8''encoded%20name.zip
        /// </summary>
        private static string? ParseContentDispositionManual(string headerValue)
        {
            if (string.IsNullOrEmpty(headerValue)) return null;

            // Tìm filename*= (ưu tiên cao nhất)
            var starIdx = headerValue.IndexOf("filename*=", StringComparison.OrdinalIgnoreCase);
            if (starIdx >= 0)
            {
                var value = headerValue.Substring(starIdx + 10).Trim();
                // Format: UTF-8''encoded_name hoặc utf-8'en'encoded_name
                var quoteIdx = value.IndexOf("''");
                if (quoteIdx >= 0)
                {
                    value = value.Substring(quoteIdx + 2);
                }
                // Lấy đến dấu ; hoặc hết string
                var endIdx = value.IndexOf(';');
                if (endIdx >= 0) value = value.Substring(0, endIdx);
                value = Uri.UnescapeDataString(value.Trim('"', '\'', ' '));
                if (!string.IsNullOrEmpty(value)) return value;
            }

            // Tìm filename= (thường)
            var fnIdx = headerValue.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
            if (fnIdx >= 0)
            {
                var value = headerValue.Substring(fnIdx + 9).Trim();
                // Bỏ dấu ngoặc kép nếu có
                if (value.StartsWith("\""))
                {
                    var closeQuote = value.IndexOf('"', 1);
                    if (closeQuote > 0)
                    {
                        value = value.Substring(1, closeQuote - 1);
                    }
                }
                else
                {
                    // Không có ngoặc kép → lấy đến dấu ; hoặc hết string
                    var endIdx = value.IndexOf(';');
                    if (endIdx >= 0) value = value.Substring(0, endIdx);
                }
                value = value.Trim();
                if (!string.IsNullOrEmpty(value)) return value;
            }

            return null;
        }

        /// <summary>
        /// Trích xuất tên file từ URL.
        /// Xử lý: query string, fragment, URL encoding, redirect paths.
        /// </summary>
        private static string ExtractFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // Bỏ trailing slash
                path = path.TrimEnd('/');

                // Lấy segment cuối cùng
                var lastSlash = path.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    path = path.Substring(lastSlash + 1);
                }

                // URL decode
                path = Uri.UnescapeDataString(path);

                // Kiểm tra path có chứa extension hợp lệ không
                if (!string.IsNullOrEmpty(path) && path.Contains('.'))
                {
                    return path;
                }

                // Nếu URL path không có file name, thử tìm trong query string
                var query = uri.Query;
                if (!string.IsNullOrEmpty(query))
                {
                    // Tìm các parameter phổ biến chứa filename
                    var queryParams = System.Web.HttpUtility.ParseQueryString(query);
                    foreach (var key in new[] { "filename", "file", "name", "f", "fn", "title" })
                    {
                        var val = queryParams[key];
                        if (!string.IsNullOrEmpty(val) && val.Contains('.'))
                        {
                            return Uri.UnescapeDataString(val);
                        }
                    }

                    // Tìm bất kỳ value nào có chứa extension
                    foreach (string? key in queryParams)
                    {
                        if (key == null) continue;
                        var val = queryParams[key];
                        if (val != null && HasKnownExtension(val))
                        {
                            return Uri.UnescapeDataString(val);
                        }
                    }
                }

                return path;
            }
            catch
            {
                // Fallback: parse URL đơn giản
                var clean = url.Split('?')[0].Split('#')[0].TrimEnd('/');
                var lastSlash = clean.LastIndexOf('/');
                return lastSlash >= 0 ? clean.Substring(lastSlash + 1) : "";
            }
        }

        /// <summary>
        /// Đảm bảo file name có extension.
        /// Nếu không có → suy ra từ Content-Type.
        /// </summary>
        private static string EnsureFileExtension(string fileName, string contentType)
        {
            // Kiểm tra đã có extension chưa
            var ext = System.IO.Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && ext.Length > 1)
            {
                return fileName; // Đã có extension
            }

            // Suy ra extension từ Content-Type
            var guessedExt = ContentTypeToExtension(contentType);
            if (!string.IsNullOrEmpty(guessedExt))
            {
                return fileName + guessedExt;
            }

            return fileName;
        }

        /// <summary>
        /// Chuyển Content-Type thành file extension.
        /// Bảng mapping phổ biến nhất.
        /// </summary>
        private static string ContentTypeToExtension(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return "";

            // Bỏ parameters (charset, boundary,...)
            var type = contentType.Split(';')[0].Trim().ToLower();

            return type switch
            {
                // ===== Archives =====
                "application/zip" => ".zip",
                "application/x-zip-compressed" => ".zip",
                "application/x-rar-compressed" => ".rar",
                "application/vnd.rar" => ".rar",
                "application/x-7z-compressed" => ".7z",
                "application/x-tar" => ".tar",
                "application/gzip" => ".gz",
                "application/x-gzip" => ".gz",
                "application/x-bzip2" => ".bz2",
                "application/x-xz" => ".xz",

                // ===== Executables =====
                "application/x-msdownload" => ".exe",
                "application/x-msdos-program" => ".exe",
                "application/x-dosexec" => ".exe",
                "application/vnd.microsoft.portable-executable" => ".exe",
                "application/x-msi" => ".msi",

                // ===== Documents =====
                "application/pdf" => ".pdf",
                "application/msword" => ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "application/vnd.ms-excel" => ".xls",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
                "application/vnd.ms-powerpoint" => ".ppt",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
                "application/rtf" => ".rtf",
                "text/plain" => ".txt",
                "text/csv" => ".csv",
                "text/html" => ".html",
                "text/xml" => ".xml",
                "application/json" => ".json",

                // ===== Video =====
                "video/mp4" => ".mp4",
                "video/x-matroska" => ".mkv",
                "video/x-msvideo" => ".avi",
                "video/quicktime" => ".mov",
                "video/x-ms-wmv" => ".wmv",
                "video/x-flv" => ".flv",
                "video/webm" => ".webm",
                "video/3gpp" => ".3gp",
                "video/mpeg" => ".mpeg",

                // ===== Audio =====
                "audio/mpeg" => ".mp3",
                "audio/mp3" => ".mp3",
                "audio/flac" => ".flac",
                "audio/wav" => ".wav",
                "audio/x-wav" => ".wav",
                "audio/aac" => ".aac",
                "audio/ogg" => ".ogg",
                "audio/x-ms-wma" => ".wma",
                "audio/webm" => ".weba",
                "audio/mp4" => ".m4a",

                // ===== Images =====
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tiff",
                "image/x-icon" => ".ico",

                // ===== Disk images =====
                "application/x-iso9660-image" => ".iso",
                "application/x-raw-disk-image" => ".img",
                "application/x-apple-diskimage" => ".dmg",

                // ===== Fonts =====
                "font/woff" => ".woff",
                "font/woff2" => ".woff2",
                "font/ttf" => ".ttf",
                "font/otf" => ".otf",

                // ===== Android/iOS =====
                "application/vnd.android.package-archive" => ".apk",

                // ===== Misc =====
                "application/x-bittorrent" => ".torrent",
                "application/octet-stream" => "", // Không thể suy ra
                _ => ""
            };
        }

        /// <summary>
        /// Kiểm tra string có chứa extension file phổ biến không
        /// </summary>
        private static bool HasKnownExtension(string value)
        {
            var knownExts = new[] {
                ".zip", ".rar", ".7z", ".tar", ".gz", ".iso",
                ".exe", ".msi", ".dmg", ".deb", ".rpm",
                ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm",
                ".mp3", ".flac", ".wav", ".aac", ".ogg",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx",
                ".apk", ".torrent", ".bin", ".img"
            };
            var lower = value.ToLower();
            foreach (var ext in knownExts)
            {
                if (lower.EndsWith(ext)) return true;
            }
            return false;
        }

        /// <summary>
        /// Loại bỏ ký tự không hợp lệ khỏi tên file
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            // Loại bỏ ký tự không hợp lệ cho tên file Windows
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            // Giới hạn độ dài
            if (fileName.Length > 200)
            {
                var ext = System.IO.Path.GetExtension(fileName);
                fileName = fileName.Substring(0, 200 - ext.Length) + ext;
            }
            return fileName.Trim('.', ' ', '_');
        }

        /// <summary>
        /// Kiểm tra server có hỗ trợ Range request không bằng cách thử request 1 byte
        /// </summary>
        private async Task<bool> TestRangeSupportAsync(string url, CancellationToken ct)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                return response.StatusCode == HttpStatusCode.PartialContent;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tải một segment (phần) của file.
        /// Sử dụng Range header để tải từ startByte đến endByte.
        /// </summary>
        public async Task<System.IO.Stream> GetRangeStreamAsync(string url, long startByte, long endByte, CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(startByte, endByte);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync(ct);
        }

        /// <summary>
        /// Tải toàn bộ file (không dùng Range - single thread fallback)
        /// </summary>
        public async Task<System.IO.Stream> GetFullStreamAsync(string url, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync(ct);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Thông tin file lấy từ server
    /// </summary>
    public class FileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public long ContentLength { get; set; } = -1;
        public bool SupportsRange { get; set; } = false;
        public string ContentType { get; set; } = string.Empty;
    }
}
