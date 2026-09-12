// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;

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

// Card headline: delegates to pure ItemDisplayFormatter.
// Never exposes internal file:// URIs or absolute disk paths for media/files.
internal sealed class TitleLineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClipboardItem item)
        {
            return "";
        }
        return ItemDisplayFormatter.GetTitle(item);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Subtitle/Kind label: human-readable type description (e.g. "Imagem PNG", "Código C#", "Documento PDF").
internal sealed class KindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return ItemDisplayFormatter.GetTypeLabel(item);
        }
        if (value is ItemKind kind)
        {
            return kind.ToString();
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Segoe Fluent Icon glyph for each item kind.
internal sealed class KindIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return ItemDisplayFormatter.GetKindIconGlyph(item.Kind, item.Content);
        }
        if (value is ItemKind kind)
        {
            return ItemDisplayFormatter.GetKindIconGlyph(kind);
        }
        return "\uE8A5";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Multiline preview text for Code and Text cards (up to 8 lines).
internal sealed class PreviewTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClipboardItem item)
        {
            return "";
        }
        return ItemDisplayFormatter.GetPreviewText(item, 8);
    }

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

// Image thumbnails from persisted PNGs (file:// URI) OR local file paths from Windows Explorer.
internal sealed class ImageThumbConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? localPath = null;
        if (value is ClipboardItem item)
        {
            localPath = ItemDisplayFormatter.TryGetLocalImagePath(item);
        }
        else if (value is string content)
        {
            if (content.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var p = new Uri(content).LocalPath;
                    if (File.Exists(p)) localPath = p;
                }
                catch
                {
                }
            }
            else if (File.Exists(content))
            {
                localPath = content;
            }
        }

        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(localPath);
            image.DecodePixelHeight = 180;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for image preview box.
internal sealed class ImagePreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var path = ItemDisplayFormatter.TryGetLocalImagePath(item);
            return !string.IsNullOrEmpty(path) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for code preview box.
internal sealed class CodePreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return item.Kind == ItemKind.Code ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for general text preview box (when not code, character or image).
internal sealed class TextPreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            if (item.Kind == ItemKind.Code) return Visibility.Collapsed;
            if (item.Kind == ItemKind.Character) return Visibility.Collapsed;
            if (ItemDisplayFormatter.TryGetLocalImagePath(item) != null) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Relative time string in Portuguese (e.g. "agora", "há 5 min", "há 2 h").
internal sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime dt;
        if (value is DateTime directDt)
        {
            dt = directDt;
        }
        else if (value is ClipboardItem item)
        {
            dt = item.CapturedAt;
        }
        else
        {
            return "";
        }

        var diff = DateTime.UtcNow - dt;
        if (diff.TotalSeconds < 60)
        {
            return "agora";
        }
        if (diff.TotalMinutes < 60)
        {
            return $"há {(int)diff.TotalMinutes} min";
        }
        if (diff.TotalHours < 24)
        {
            return $"há {(int)diff.TotalHours} h";
        }
        if (diff.TotalDays < 7)
        {
            return $"há {(int)diff.TotalDays} d";
        }
        return dt.ToLocalTime().ToString("dd/MM", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for character / emoji preview box.
internal sealed class CharacterPreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return item.Kind == ItemKind.Character ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Unicode code point formatter (e.g. "U+1F680").
internal sealed class CharacterCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return ItemDisplayFormatter.GetUnicodeCodePoint(item.Content);
        }
        if (value is string text)
        {
            return ItemDisplayFormatter.GetUnicodeCodePoint(text);
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Pin icon for card header button: filled pin if pinned, outline pin if not.
internal sealed class CardPinIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "\uE840" : "\uE718";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Pin brush for card header button: gold if pinned, muted gray if not.
internal sealed class CardPinBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush GoldBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB9, 0x00));
    private static readonly System.Windows.Media.Brush MutedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E, 0x8E, 0x93));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? GoldBrush : MutedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
