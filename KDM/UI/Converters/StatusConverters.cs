using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KDM.Models;

namespace KDM.UI.Converters
{
    /// <summary>
    /// Chuyển DownloadStatus thành màu cho progress bar
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DownloadStatus status)
            {
                return status switch
                {
                    DownloadStatus.Downloading => new SolidColorBrush(Color.FromRgb(0, 180, 120)),   // Xanh lá
                    DownloadStatus.Paused => new SolidColorBrush(Color.FromRgb(255, 180, 0)),         // Vàng cam
                    DownloadStatus.Completed => new SolidColorBrush(Color.FromRgb(50, 150, 255)),     // Xanh dương
                    DownloadStatus.Failed => new SolidColorBrush(Color.FromRgb(240, 70, 70)),         // Đỏ
                    DownloadStatus.Merging => new SolidColorBrush(Color.FromRgb(160, 100, 255)),      // Tím
                    _ => new SolidColorBrush(Color.FromRgb(120, 120, 140))                            // Xám
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Chuyển DownloadStatus thành icon text
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DownloadStatus status)
            {
                return status switch
                {
                    DownloadStatus.Downloading => "⬇",
                    DownloadStatus.Paused => "⏸",
                    DownloadStatus.Completed => "✓",
                    DownloadStatus.Failed => "✕",
                    DownloadStatus.Merging => "⟳",
                    DownloadStatus.Queued => "⏳",
                    _ => "?"
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Bool to Visibility converter
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Inverse bool converter
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}
