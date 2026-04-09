using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace KDM.Network
{
    /// <summary>
    /// HTTP Server nhỏ chạy trên localhost để nhận link từ browser extension.
    /// Port mặc định: 52888
    /// 
    /// API Endpoints:
    ///   GET  /api/status     → Kiểm tra KDM đang chạy
    ///   POST /api/download   → Thêm download mới
    ///   GET  /api/downloads  → Lấy danh sách download
    /// </summary>
    public class ExtensionServer
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private static readonly ILogger _log = Log.ForContext<ExtensionServer>();

        /// <summary>Port server lắng nghe</summary>
        public int Port { get; }

        /// <summary>Event khi nhận được URL download từ extension</summary>
        public event Action<string, string>? DownloadRequested;

        /// <summary>Delegate lấy danh sách downloads hiện tại</summary>
        public Func<object>? GetDownloadsList { get; set; }

        public ExtensionServer(int port = 52888)
        {
            Port = port;
            _listener = new HttpListener();
            // Dùng 127.0.0.1 thay vì localhost để tránh cần quyền Admin
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Khởi động server
        /// </summary>
        public void Start()
        {
            try
            {
                _listener.Start();
                _log.Information("Extension server đang chạy tại http://localhost:{Port}/", Port);

                // Chạy listen loop trên background thread
                Task.Run(() => ListenLoop());
            }
            catch (HttpListenerException ex)
            {
                _log.Error(ex, "Không thể khởi động Extension Server trên port {Port}", Port);
            }
        }

        /// <summary>
        /// Dừng server
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            _log.Information("Extension server đã dừng");
        }

        /// <summary>
        /// Vòng lặp lắng nghe request
        /// </summary>
        private async Task ListenLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    break; // Server đã stop
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Lỗi khi xử lý request");
                }
            }
        }

        /// <summary>
        /// Xử lý một HTTP request
        /// </summary>
        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // CORS headers cho extension
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                // Handle CORS preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var path = request.Url?.AbsolutePath ?? "";

                switch (path.ToLower())
                {
                    case "/api/status":
                        await HandleStatus(response);
                        break;

                    case "/api/download":
                        if (request.HttpMethod == "POST")
                            await HandleAddDownload(request, response);
                        else
                            await SendJson(response, 405, new { error = "Method not allowed" });
                        break;

                    case "/api/downloads":
                        await HandleGetDownloads(response);
                        break;

                    default:
                        await SendJson(response, 404, new { error = "Not found" });
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Lỗi xử lý request: {Path}", request.Url?.AbsolutePath);
                try
                {
                    await SendJson(response, 500, new { error = ex.Message });
                }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// GET /api/status - Kiểm tra KDM đang chạy
        /// </summary>
        private async Task HandleStatus(HttpListenerResponse response)
        {
            await SendJson(response, 200, new
            {
                status = "running",
                app = "KDM Download Manager",
                version = "1.0"
            });
        }

        /// <summary>
        /// POST /api/download - Thêm download mới
        /// Body: { "url": "...", "filename": "..." }
        /// </summary>
        private async Task HandleAddDownload(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Đọc body
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var data = JsonConvert.DeserializeAnonymousType(body, new { url = "", filename = "" });

            if (string.IsNullOrWhiteSpace(data?.url))
            {
                await SendJson(response, 400, new { error = "URL is required" });
                return;
            }

            _log.Information("Nhận URL từ extension: {Url}", data.url);

            // Gọi event để MainViewModel xử lý
            DownloadRequested?.Invoke(data.url, data.filename ?? "");

            await SendJson(response, 200, new
            {
                success = true,
                message = $"Download added: {data.url}"
            });
        }

        /// <summary>
        /// GET /api/downloads - Lấy danh sách downloads
        /// </summary>
        private async Task HandleGetDownloads(HttpListenerResponse response)
        {
            var downloads = GetDownloadsList?.Invoke() ?? new object[] { };
            await SendJson(response, 200, new { downloads });
        }

        /// <summary>
        /// Gửi JSON response
        /// </summary>
        private static async Task SendJson(HttpListenerResponse response, int statusCode, object data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}
