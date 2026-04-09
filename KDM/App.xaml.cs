using System;
using System.IO;
using System.Windows;
using Serilog;

namespace KDM
{
    /// <summary>
    /// Application startup - cấu hình logging và initialization
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Cấu hình Serilog
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KDM", "Logs");

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(logDirectory, "kdm-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== KDM Download Manager khởi động ===");
            Log.Information("Log directory: {Dir}", logDirectory);

            // Xử lý unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
                Log.CloseAndFlush();
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Log.Error(args.Exception, "UI thread exception");
                MessageBox.Show($"Đã xảy ra lỗi: {args.Exception.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== KDM Download Manager đóng ===");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
