using System;
using Serilog;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace KDM.UI
{
    /// <summary>
    /// System Tray Icon wrapper.
    /// Cho phép minimize ứng dụng xuống Notification Area (system tray).
    /// Dùng WinForms NotifyIcon vì WPF không có built-in tray icon.
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private readonly WinForms.NotifyIcon _notifyIcon;
        private readonly WinForms.ContextMenuStrip _contextMenu;
        private static readonly ILogger _log = Log.ForContext<TrayIconManager>();

        /// <summary>Event khi user double-click vào tray icon (mở app)</summary>
        public event Action? ShowRequested;

        /// <summary>Event khi user chọn Exit từ menu</summary>
        public event Action? ExitRequested;

        public TrayIconManager()
        {
            // Tạo context menu cho tray icon
            _contextMenu = new WinForms.ContextMenuStrip();

            var showItem = new WinForms.ToolStripMenuItem("Mở KDM");
            showItem.Font = new Drawing.Font(showItem.Font, Drawing.FontStyle.Bold);
            showItem.Click += (s, e) => ShowRequested?.Invoke();

            var separator = new WinForms.ToolStripSeparator();

            var exitItem = new WinForms.ToolStripMenuItem("Thoát");
            exitItem.Click += (s, e) => ExitRequested?.Invoke();

            _contextMenu.Items.AddRange(new WinForms.ToolStripItem[] { showItem, separator, exitItem });

            // Tạo NotifyIcon
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "KDM Download Manager",
                Visible = false,
                ContextMenuStrip = _contextMenu
            };

            // Load icon từ app resources
            try
            {
                var iconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.ico");

                if (System.IO.File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Drawing.Icon(iconPath);
                }
                else
                {
                    // Fallback: dùng icon từ executable
                    _notifyIcon.Icon = Drawing.Icon.ExtractAssociatedIcon(
                        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Không thể load tray icon");
                // Fallback: system default icon
                _notifyIcon.Icon = Drawing.SystemIcons.Application;
            }

            // Double-click tray icon → mở app
            _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke();

            _log.Information("Tray icon initialized");
        }

        /// <summary>
        /// Hiện tray icon (khi minimize)
        /// </summary>
        public void Show()
        {
            _notifyIcon.Visible = true;
        }

        /// <summary>
        /// Ẩn tray icon (khi restore window)
        /// </summary>
        public void Hide()
        {
            _notifyIcon.Visible = false;
        }

        /// <summary>
        /// Hiện balloon notification trên tray
        /// </summary>
        public void ShowBalloon(string title, string text, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, icon);
        }

        /// <summary>
        /// Cập nhật tooltip text (ví dụ: hiện tốc độ download)
        /// </summary>
        public void UpdateTooltip(string text)
        {
            // NotifyIcon tooltip max 63 chars
            if (text.Length > 63) text = text.Substring(0, 63);
            _notifyIcon.Text = text;
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}
