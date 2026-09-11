// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.History;

namespace WindowsCM.App;

// Tag hex (ItemTags.All parity) to brush; unknown/empty tags are invisible.
internal sealed class TagBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            try
            {
                return new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tag.Trim()));
            }
            catch (FormatException)
            {
            }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Card headline: the edited title, else the first content line (120 chars).
internal sealed class TitleLineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClipboardItem item)
        {
            return "";
        }
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title.Trim();
        }
        var line = (item.Content ?? "").Split('\n').FirstOrDefault()?.Trim() ?? "";
        return line.Length > 120 ? line[..120] : line;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal sealed class KindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ItemKind kind ? kind.ToString() : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal sealed class PinnedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Image-kind thumbnails from the persisted PNG (content is the file://
// URI). Anything unresolvable yields null and the template collapses.
internal sealed class ImageThumbConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string content || !content.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        try
        {
            var path = new Uri(content).LocalPath;
            if (!File.Exists(path))
            {
                return null;
            }
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.DecodePixelHeight = 128;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is UriFormatException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
