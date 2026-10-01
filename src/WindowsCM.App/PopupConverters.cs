// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;

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

// Subtitle/Kind label: human-readable type description (e.g. "PNG Image", "C# Code", "PDF Document").
internal sealed class KindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = System.Windows.Application.Current as App;
        var categories = app?.Settings?.FileCategories;
        if (value is ClipboardItem item)
        {
            return ItemDisplayFormatter.GetTypeLabel(item, categories);
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
        var app = System.Windows.Application.Current as App;
        var categories = app?.Settings?.FileCategories;
        if (value is ClipboardItem item)
        {
            return ItemDisplayFormatter.GetKindIconGlyph(item.Kind, item.Content, categories);
        }
        if (value is ItemKind kind)
        {
            return ItemDisplayFormatter.GetKindIconGlyph(kind, null, categories);
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

// Image and file thumbnails: captured PNGs (the app's own image store) are
// decoded here; a File card's thumbnail comes from CardFileFacts, never
// from the disk on the UI thread.
internal sealed class ImageThumbConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? localPath = null;
        if (value is ClipboardItem item)
        {
            if (item.Kind is ItemKind.File or ItemKind.Files)
            {
                return CardFileFacts.ForItem(item)?.Thumbnail;
            }
            localPath = ItemDisplayFormatter.TryGetLocalImagePath(item);
        }
        else if (value is string content)
        {
            if (content.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var p = new Uri(content).LocalPath;
                    localPath = p;
                }
                catch
                {
                }
            }
            else if (Path.IsPathFullyQualified(content))
            {
                localPath = content;
            }
        }

        if (string.IsNullOrEmpty(localPath))
        {
            return null;
        }

        // Decoded once per file, off the UI thread (bounded cache, prewarmed
        // for new captures); the card refreshes when a queued decode lands.
        return ImageThumbnailCache.GetOrQueue(localPath);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for image preview box: captured images, and File
// cards once their probed thumbnail (Shell preview or decoded image) is in.
internal sealed class ImagePreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            if (item.Kind == ItemKind.Image)
            {
                return Visibility.Visible;
            }

            if (item.Kind is ItemKind.File or ItemKind.Files
                && !MediaItemClassifier.IsAudioItem(item, out _)
                && CardFileFacts.ForItem(item)?.Thumbnail != null)
            {
                return Visibility.Visible;
            }
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

// Visibility converter for general text preview box (when not code, character, image or file).
internal sealed class TextPreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            if (item.Kind == ItemKind.Code) return Visibility.Collapsed;
            if (item.Kind == ItemKind.Character) return Visibility.Collapsed;
            if (item.Kind == ItemKind.Image) return Visibility.Collapsed;
            if (item.Kind == ItemKind.File) return Visibility.Collapsed;
            if (item.Kind == ItemKind.Files) return Visibility.Collapsed;
            if (item.Kind == ItemKind.Link) return Visibility.Collapsed;
            if (ItemDisplayFormatter.TryGetLocalImagePath(item) != null) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Relative time string in the UI language (e.g. "just now", "5 min ago", "2h ago").
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
        var strings = LocalizationManager.Strings;
        if (diff.TotalSeconds < 60)
        {
            return strings.TimeJustNow;
        }
        if (diff.TotalMinutes < 60)
        {
            var m = (int)diff.TotalMinutes;
            return strings.TimeMinutesAgo(m);
        }
        if (diff.TotalHours < 24)
        {
            var h = (int)diff.TotalHours;
            return strings.TimeHoursAgo(h);
        }
        if (diff.TotalDays < 7)
        {
            var d = (int)diff.TotalDays;
            return strings.TimeDaysAgo(d);
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

// Unicode code point formatter (e.g. "U+1F680") or emoji count ("3 emojis").
internal sealed class CharacterCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? content = null;
        if (value is ClipboardItem item)
        {
            // Bound on every card: only character cards show it.
            content = item.Kind == ItemKind.Character ? item.Content : null;
        }
        else if (value is string text)
        {
            content = text;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return "";
        }

        var trimmed = content.Trim();
        if (EmojiDetector.IsAllEmojis(trimmed))
        {
            var count = EmojiDetector.CountEmojis(trimmed);
            if (count > 1)
            {
                return $"{count} emojis";
            }
            return ItemDisplayFormatter.GetUnicodeCodePoint(trimmed);
        }

        return ItemDisplayFormatter.GetUnicodeCodePoint(trimmed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Direct2D full-color emoji thumbnail converter.
internal sealed class EmojiThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? content = null;
        if (value is ClipboardItem item && item.Kind == ItemKind.Character)
        {
            content = item.Content;
        }
        else if (value is string text)
        {
            content = text;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return EmojiService.GetEmojiThumbnail(content);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for fallback text block when no emoji image is displayed.
internal sealed class CharacterFallbackVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? content = null;
        if (value is ClipboardItem item && item.Kind == ItemKind.Character)
        {
            content = item.Content;
        }
        else if (value is string text)
        {
            content = text;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Visibility.Collapsed;
        }

        return EmojiDetector.IsAllEmojis(content) ? Visibility.Collapsed : Visibility.Visible;
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

// Visibility converter for file / files preview box (when not an image):
// the icon + name view, also shown while the file is still being probed.
internal sealed class FilePreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item && item.Kind is ItemKind.File or ItemKind.Files)
        {
            if (MediaItemClassifier.IsAudioItem(item, out _))
            {
                return Visibility.Collapsed;
            }
            return CardFileFacts.ForItem(item)?.Thumbnail != null ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// System icon for File / Files items: the file's own icon once probed,
// the extension's generic icon (registry only) until then.
internal sealed class FileIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isLarge = parameter is not "small";
        if (value is ClipboardItem item)
        {
            if (item.Kind is not (ItemKind.File or ItemKind.Files))
            {
                return null;
            }
            var firstPath = TextPreview.FirstNonEmptySegment(item.Content);
            var facts = CardFileFacts.TryGet(firstPath);
            return (isLarge ? facts?.Icon : null) ?? FileIconService.GetExtensionIcon(firstPath, isLarge);
        }
        if (value is string path)
        {
            return FileIconService.GetExtensionIcon(path, isLarge);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Clean file name or multiple file count.
internal sealed class FileDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var details = CardFileFacts.DetailsFor(item);
            return details?.FileName ?? LocalizationManager.Strings.KindFile;
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Subtitle/details for the file preview: e.g. "Documento PDF • 2,4 MB"
internal sealed class FileDetailsSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var details = CardFileFacts.DetailsFor(item);
            if (details == null) return "";
            if (details.IsMultiple)
            {
                return LocalizationManager.Strings.LabelFilesSelected(details.FileCount);
            }
            if (!string.IsNullOrEmpty(details.FormattedSize))
            {
                return $"{details.TypeLabel} • {details.FormattedSize}";
            }
            return details.TypeLabel;
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Directory path indicator: e.g. "📁 C:\Users\gabri\Documents"
internal sealed class FileDirectorySummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var details = CardFileFacts.DetailsFor(item);
            if (!string.IsNullOrEmpty(details?.DirectoryPath))
            {
                return "📁 " + details.DirectoryPath;
            }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Multi-file list preview (up to 3 files + count remaining)
internal sealed class FileListSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var details = CardFileFacts.DetailsFor(item);
            if (details is { IsMultiple: true })
            {
                var take = details.Items.Take(3).Select(f => $"• {f.FileName}").ToList();
                if (details.FileCount > 3)
                {
                    var rem = details.FileCount - 3;
                    take.Add(LocalizationManager.Strings.LabelMoreFilesRemaining(rem));
                }
                return string.Join("\n", take);
            }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Syntax highlighting input: only Code cards show the code box. Bound to
// every card's Content, the tokenizer built hundreds of Runs for each text
// card that scrolled into view, all inside a collapsed TextBlock.
internal sealed class CodeContentConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ClipboardItem { Kind: ItemKind.Code } item ? item.Content : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Attached behavior to render highlighted code tokens in TextBlock Inlines.
public static class SyntaxHighlightHelper
{
    public static readonly DependencyProperty CodeContentProperty =
        DependencyProperty.RegisterAttached(
            "CodeContent",
            typeof(string),
            typeof(SyntaxHighlightHelper),
            new PropertyMetadata(null, OnCodeContentChanged));

    public static readonly DependencyProperty ShowLineNumbersProperty =
        DependencyProperty.RegisterAttached(
            "ShowLineNumbers",
            typeof(bool),
            typeof(SyntaxHighlightHelper),
            new PropertyMetadata(false, OnCodeContentChanged));

    public static string? GetCodeContent(DependencyObject obj) =>
        (string?)obj.GetValue(CodeContentProperty);

    public static void SetCodeContent(DependencyObject obj, string? value) =>
        obj.SetValue(CodeContentProperty, value);

    public static bool GetShowLineNumbers(DependencyObject obj) =>
        (bool)obj.GetValue(ShowLineNumbersProperty);

    public static void SetShowLineNumbers(DependencyObject obj, bool value) =>
        obj.SetValue(ShowLineNumbersProperty, value);

    private static void OnCodeContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb)
        {
            return;
        }

        tb.Inlines.Clear();
        var rawCode = GetCodeContent(tb);
        if (string.IsNullOrEmpty(rawCode))
        {
            return;
        }

        var showNumbers = GetShowLineNumbers(tb);
        if (showNumbers)
        {
            // Bounded: only the six shown lines are read from the content.
            var lines = TextPreview.FirstLines(rawCode, 6).Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var numRun = new Run($"{i}  ");
                numRun.SetResourceReference(TextElement.ForegroundProperty, "CodeCommentBrush");
                numRun.FontFamily = tb.FontFamily;
                numRun.FontSize = tb.FontSize;
                tb.Inlines.Add(numRun);

                var lineTokens = CodeSyntaxTokenizer.Tokenize(lines[i], 1);
                foreach (var token in lineTokens)
                {
                    var run = new Run(token.Text);
                    var brushKey = token.Kind switch
                    {
                        CodeSyntaxTokenKind.Keyword => "CodeKeywordBrush",
                        CodeSyntaxTokenKind.Type => "CodeTypeBrush",
                        CodeSyntaxTokenKind.String => "CodeStringBrush",
                        CodeSyntaxTokenKind.Comment => "CodeCommentBrush",
                        CodeSyntaxTokenKind.Number => "CodeNumberBrush",
                        CodeSyntaxTokenKind.Operator => "CodeOperatorBrush",
                        _ => "PreviewCodeForegroundBrush"
                    };

                    run.SetResourceReference(TextElement.ForegroundProperty, brushKey);

                    if (token.Kind == CodeSyntaxTokenKind.Keyword)
                    {
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    else if (token.Kind == CodeSyntaxTokenKind.Comment)
                    {
                        run.FontStyle = FontStyles.Italic;
                    }

                    tb.Inlines.Add(run);
                }

                if (i < lines.Count - 1)
                {
                    tb.Inlines.Add(new LineBreak());
                }
            }
            return;
        }

        var tokens = CodeSyntaxTokenizer.Tokenize(rawCode, 8);
        foreach (var token in tokens)
        {
            var run = new Run(token.Text);
            var brushKey = token.Kind switch
            {
                CodeSyntaxTokenKind.Keyword => "CodeKeywordBrush",
                CodeSyntaxTokenKind.Type => "CodeTypeBrush",
                CodeSyntaxTokenKind.String => "CodeStringBrush",
                CodeSyntaxTokenKind.Comment => "CodeCommentBrush",
                CodeSyntaxTokenKind.Number => "CodeNumberBrush",
                CodeSyntaxTokenKind.Operator => "CodeOperatorBrush",
                _ => "PreviewCodeForegroundBrush"
            };

            run.SetResourceReference(TextElement.ForegroundProperty, brushKey);

            if (token.Kind == CodeSyntaxTokenKind.Keyword)
            {
                run.FontWeight = FontWeights.SemiBold;
            }
            else if (token.Kind == CodeSyntaxTokenKind.Comment)
            {
                run.FontStyle = FontStyles.Italic;
            }

            tb.Inlines.Add(run);
        }
    }
}

// Semantic accent brush converter for item kinds (used in KindIcon and pills).
internal sealed class KindBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ItemKind kind = ItemKind.Text;
        string? content = null;
        if (value is ClipboardItem item)
        {
            kind = item.Kind;
            content = item.Content;
        }
        else if (value is ItemKind k)
        {
            kind = k;
        }

        // Color items with valid hex use their own color!
        if (kind == ItemKind.Color && !string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var parsed = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(content.Trim());
                var brush = new SolidColorBrush(parsed);
                brush.Freeze();
                return brush;
            }
            catch
            {
            }
        }

        var app = System.Windows.Application.Current as App;
        var categories = app?.Settings?.FileCategories;

        if ((kind == ItemKind.File || kind == ItemKind.Files) && categories != null && !string.IsNullOrWhiteSpace(content))
        {
            var first = TextPreview.FirstNonEmptySegment(content);
            var cat = categories.ResolveCategory(first);
            if (cat != null && !string.IsNullOrWhiteSpace(cat.ColorHex))
            {
                try
                {
                    var catColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(cat.ColorHex);
                    var catBrush = new SolidColorBrush(catColor);
                    catBrush.Freeze();
                    return catBrush;
                }
                catch
                {
                }
            }
        }

        var key = kind switch
        {
            ItemKind.Link => "KindLinkBrush",
            ItemKind.Code => "KindCodeBrush",
            ItemKind.File or ItemKind.Files => "KindFileBrush",
            ItemKind.Image => "KindImageBrush",
            ItemKind.Character => "KindCharBrush",
            ItemKind.Color => "KindColorBrush",
            _ => "KindTextBrush"
        };

        if (System.Windows.Application.Current?.Resources[key] is System.Windows.Media.Brush appBrush)
        {
            return appBrush;
        }

        var isLight = System.Windows.Application.Current?.Resources["CardBackgroundBrush"] is SolidColorBrush scb && scb.Color.R > 200;
        var hex = ItemTypeTheme.GetKindAccentHex(kind, isLight, content, fileCategories: categories);
        try
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return System.Windows.Media.Brushes.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Subtle translucent tinted background brush for item kind pills/badges.
internal sealed class KindBackgroundBrushConverter : IValueConverter
{
    private static readonly KindBrushConverter KindConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var accentBrush = KindConverter.Convert(value, targetType, parameter, culture) as SolidColorBrush;
        if (accentBrush != null)
        {
            var isLight = System.Windows.Application.Current?.Resources["CardBackgroundBrush"] is SolidColorBrush scb && scb.Color.R > 200;
            byte alpha = isLight ? (byte)0x1F : (byte)0x26;
            var c = accentBrush.Color;
            var bgBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, c.R, c.G, c.B));
            bgBrush.Freeze();
            return bgBrush;
        }

        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Card indicator stripe converter: pure semantic accent color (retired legacy tags override).
internal sealed class CardIndicatorBrushConverter : IValueConverter
{
    private static readonly KindBrushConverter KindConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return KindConverter.Convert(item, targetType, parameter, culture);
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for dedicated link / website preview cards.
internal sealed class LinkPreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return item.Kind == ItemKind.Link ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Website Favicon converter: returns frozen BitmapSource from FaviconService.
internal sealed class LinkFaviconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item && item.Kind == ItemKind.Link)
        {
            return FaviconService.GetFavicon(item.Content);
        }
        if (value is string url)
        {
            return FaviconService.GetFavicon(url);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Clean website domain converter (e.g. "github.com").
internal sealed class LinkDomainConverter : IValueConverter
{
    // Link bindings are evaluated for every card (the link branch is only
    // collapsed): other kinds answer at once instead of copying and parsing
    // their whole content.
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return item.Kind == ItemKind.Link ? LinkDisplayHelper.GetDomain(item.Content) : "";
        }
        if (value is string url)
        {
            return LinkDisplayHelper.GetDomain(url);
        }
        return "Link";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Page title or friendly subpath converter.
internal sealed class LinkTitleOrPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem { Kind: ItemKind.Link } item)
        {
            return LinkDisplayHelper.GetPathOrTitle(item);
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Clean formatted URL converter.
internal sealed class LinkDisplayUrlConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return item.Kind == ItemKind.Link ? LinkDisplayHelper.GetDisplayUrl(item.Content) : "";
        }
        if (value is string url)
        {
            return LinkDisplayHelper.GetDisplayUrl(url);
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for the card QR code action button.
// All items with content (Text, Files, Images, etc.) can now be shared to mobile via QR code!
internal sealed class QrButtonVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            return !string.IsNullOrWhiteSpace(item.Content) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Link preview thumbnail: the image the preview service cached on disk,
// decoded off the UI thread like screenshots. It used to be decoded on the
// UI thread on every card realization, and a remote URL (stored when the
// download failed, or with preview images turned off) started a throwaway
// download each time and never showed.
internal sealed class LinkPreviewImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ClipboardItem { Kind: ItemKind.Link } item && LinkPreviewImages.LocalPath(item) is { } path
            ? ImageThumbnailCache.GetOrQueue(path, banner: true)
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal static class LinkPreviewImages
{
    // The cached preview image of a link card, when it is a local file.
    public static string? LocalPath(ClipboardItem item)
    {
        var (_, _, image) = ItemMetadataJson.GetLink(item.MetadataJson);
        if (string.IsNullOrWhiteSpace(image) || !Path.IsPathFullyQualified(image))
        {
            return null;
        }
        return image;
    }
}

// Website description converter from metadata.
internal sealed class LinkDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem { Kind: ItemKind.Link } item)
        {
            return LinkDisplayHelper.GetDescription(item);
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter checking if a link card has a rich preview image.
internal sealed class LinkHasPreviewImageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Only a local cached image is shown (see LinkPreviewImageConverter).
        var hasImage = value is ClipboardItem { Kind: ItemKind.Link } item
            && LinkPreviewImages.LocalPath(item) is not null;

        bool invert = parameter is string p && (p.Equals("Invert", StringComparison.OrdinalIgnoreCase) || p.Equals("Inverse", StringComparison.OrdinalIgnoreCase));
        if (invert)
        {
            return hasImage ? Visibility.Collapsed : Visibility.Visible;
        }
        return hasImage ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter checking if a link card has a non-empty description.
internal sealed class LinkHasDescriptionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItem { Kind: ItemKind.Link } item)
        {
            var desc = LinkDisplayHelper.GetDescription(item);
            return !string.IsNullOrWhiteSpace(desc) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Media classifier for dedicated audio and video rich card presentations
internal static class MediaItemClassifier
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".opus", ".aiff", ".alac", ".mid", ".midi"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".3gp"
    };

    public static bool IsAudioItem(ClipboardItem item, out string? singlePath) =>
        IsMediaItem(item, IsAudioPath, out singlePath);

    public static bool IsVideoItem(ClipboardItem item, out string? singlePath) =>
        IsMediaItem(item, IsVideoPath, out singlePath);

    public static bool IsAudioPath(string path) => IsCategory(path, "audio", AudioExtensions);

    public static bool IsVideoPath(string path) => IsCategory(path, "video", VideoExtensions);

    private static bool IsMediaItem(ClipboardItem item, Func<string, bool> isMediaPath, out string? singlePath)
    {
        singlePath = null;
        if (item.Kind != ItemKind.File)
        {
            return false;
        }

        var first = TextPreview.FirstNonEmptySegment(item.Content);
        var path = FileDisplayHelper.NormalizePath(first);
        if (string.IsNullOrWhiteSpace(path) || !isMediaPath(path))
        {
            return false;
        }
        singlePath = path;
        return true;
    }

    private static bool IsCategory(string path, string categoryId, HashSet<string> extensions)
    {
        var ext = Path.GetExtension(path);
        var app = System.Windows.Application.Current as App;
        var category = app?.Settings?.FileCategories?.ResolveCategory(ext);
        if (category != null && category.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return extensions.Contains(ext);
    }
}

// Probed facts of an audio / video File card (CardFileFacts), or of a raw
// path; null until the probe lands or when the file is gone.
internal static class MediaFacts
{
    public static FileFacts? Audio(object value) => Of(value, MediaItemClassifier.IsAudioItem);

    public static FileFacts? Video(object value) => Of(value, MediaItemClassifier.IsVideoItem);

    private delegate bool MediaTest(ClipboardItem item, out string? path);

    private static FileFacts? Of(object value, MediaTest isMedia)
    {
        var path = value switch
        {
            ClipboardItem item when isMedia(item, out var itemPath) => itemPath,
            string rawPath => rawPath,
            _ => null,
        };
        return CardFileFacts.TryGet(path) is { Exists: true } facts ? facts : null;
    }
}

// Visibility converter for the dedicated Audio preview layout: shown while
// the file is probed (name + generic art), hidden once it is known gone.
internal sealed class AudioPreviewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ClipboardItem item
            && MediaItemClassifier.IsAudioItem(item, out var path)
            && CardFileFacts.TryGet(path) is not { Exists: false }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter for audio album cover art
internal sealed class AudioCoverImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MediaFacts.Audio(value)?.AudioCover;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter to check whether audio file has visual cover art
internal sealed class AudioHasCoverVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasCover = MediaFacts.Audio(value)?.AudioCover != null;

        bool invert = parameter is string p && (p.Equals("Invert", StringComparison.OrdinalIgnoreCase) || p.Equals("Inverse", StringComparison.OrdinalIgnoreCase));
        if (invert)
        {
            return hasCover ? Visibility.Collapsed : Visibility.Visible;
        }
        return hasCover ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter for song title (metadata title or clean file name)
internal sealed class AudioTrackTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value switch
        {
            ClipboardItem item when MediaItemClassifier.IsAudioItem(item, out var itemPath) => itemPath,
            string rawPath => rawPath,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        var title = MediaFacts.Audio(value)?.Audio?.Title;
        return !string.IsNullOrWhiteSpace(title) ? title : Path.GetFileNameWithoutExtension(path);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter for artist and album summary ("Artist • Album" or "Artist")
internal sealed class AudioArtistAlbumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MediaFacts.Audio(value)?.Audio?.ArtistAndAlbumSummary ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for whether artist/album line should be visible
internal sealed class AudioHasArtistOrAlbumVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MediaFacts.Audio(value)?.Audio?.HasArtistOrAlbum == true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter for audio duration and file size summary ("03:55 • 34,2 MB")
internal sealed class AudioDurationAndSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MediaFacts.Audio(value)?.Audio?.DurationAndSizeSummary ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Converter for overlay badge on video thumbnails ("02:15 • 45,2 MB")
internal sealed class VideoOverlayBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        MediaFacts.Video(value)?.VideoBadge ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Visibility converter for video overlay badge
internal sealed class VideoOverlayVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ClipboardItem && !string.IsNullOrWhiteSpace(MediaFacts.Video(value)?.VideoBadge)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
