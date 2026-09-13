using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

// Fluent theme dictionary generator for PopupWindow (Dark & Light modes).
// Provides frozen brushes for instantaneous switching without visual tree teardown.
public static class PopupThemeBrushes
{
    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush BrushFromHex(string? hex, SolidColorBrush fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            var parsed = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(parsed);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return fallback;
        }
    }

    public static ResourceDictionary CreateThemeDictionary(bool isLight, ItemColorSettings? customColors = null) =>
        CreateThemeDictionary(isLight ? ColorScheme.Light : ColorScheme.Dark, customColors);

    public static ResourceDictionary CreateThemeDictionary(ColorScheme scheme, ItemColorSettings? customColors = null)
    {
        var dict = new ResourceDictionary();

        if (scheme == ColorScheme.HighContrast)
        {
            // Windows High Contrast Accessibility Palette (Pure Black, Pure White, Cyan and Yellow accents)
            dict["PopupBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PopupBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PopupShadowColor"] = Color.FromRgb(0x00, 0x00, 0x00);
            dict["PopupShadowOpacity"] = 0.0;

            dict["SearchBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["SearchBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchCaretBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchIconBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchPlaceholderBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["SearchDropdownHoverBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["SearchCapsulePinHoverBrush"] = Brush(0x1A, 0x1A, 0x1A);

            dict["IconButtonForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["IconButtonHoverBackgroundBrush"] = Brush(0x22, 0x22, 0x22);
            dict["IconButtonHoverBorderBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["IconButtonPressedBackgroundBrush"] = Brush(0x33, 0x33, 0x33);

            dict["PillButtonBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PillButtonBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PillButtonForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PillButtonHoverBackgroundBrush"] = Brush(0x22, 0x22, 0x22);
            dict["PillButtonHoverBorderBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["PillButtonPressedBackgroundBrush"] = Brush(0x33, 0x33, 0x33);

            dict["CardBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["CardBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardHoverBackgroundBrush"] = Brush(0x14, 0x14, 0x14);
            dict["CardHoverBorderBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["CardSelectedBackgroundBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["CardSelectedBorderBrush"] = Brush(0x00, 0xFF, 0xFF);

            dict["CardPinnedBorderBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["CardPinnedBackgroundBrush"] = Brush(0x1C, 0x1A, 0x00);
            dict["CardPinnedBadgeBrush"] = Brush(0xFF, 0xFF, 0x00);

            dict["CardTitleBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardSubtitleBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["CardTimeBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["CardIconBrush"] = Brush(0xFF, 0xFF, 0xFF);

            // Dialog text & accent (QrWindow, MobileTransferWindow)
            dict["TextBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SecondaryTextBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["CardAccentBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["CardAccentForegroundBrush"] = Brush(0x00, 0x00, 0x00);

            dict["CardButtonForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardButtonHoverBackgroundBrush"] = Brush(0x22, 0x22, 0x22);
            dict["CardButtonHoverForegroundBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["CardButtonPressedBackgroundBrush"] = Brush(0x33, 0x33, 0x33);

            dict["PreviewImageBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewCodeBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewCodeBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PreviewCodeForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["CodeKeywordBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["CodeTypeBrush"] = Brush(0x55, 0xFF, 0x55);
            dict["CodeStringBrush"] = Brush(0xFF, 0xFF, 0x55);
            dict["CodeCommentBrush"] = Brush(0xAA, 0xAA, 0xAA);
            dict["CodeNumberBrush"] = Brush(0xFF, 0x55, 0xFF);
            dict["CodeOperatorBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["PreviewFileBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewFileBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PreviewFileSecondaryTextBrush"] = Brush(0x00, 0xFF, 0xFF);

            dict["PreviewCharBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewCharBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PreviewCharTextBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["PreviewTextBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewTextBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PreviewTextForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["PreviewLinkBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["PreviewLinkBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["PreviewLinkSecondaryTextBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["FaviconContainerBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["FaviconContainerBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);

            // Semantic Item Kind Accent Brushes (High Contrast)
            dict["KindLinkBrush"] = Brush(0x00, 0xFF, 0xFF);
            dict["KindCodeBrush"] = Brush(0xFF, 0x55, 0xFF);
            dict["KindFileBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["KindImageBrush"] = Brush(0x55, 0xFF, 0x55);
            dict["KindCharBrush"] = Brush(0xFF, 0x88, 0xFF);
            dict["KindColorBrush"] = Brush(0xFF, 0x55, 0x55);
            dict["KindTextBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["ScrollBarTrackBrush"] = Brush(0x00, 0x00, 0x00);
            dict["ScrollBarThumbBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["ScrollBarThumbHoverBrush"] = Brush(0x00, 0xFF, 0xFF);

            dict["MenuBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["MenuBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["MenuItemForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["MenuItemHoverBackgroundBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["MenuSeparatorBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["IncognitoBannerBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["IncognitoBannerBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["IncognitoBadgeBackgroundBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["IncognitoAccentBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["IncognitoTextBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["IncognitoExitButtonBackgroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["IncognitoExitButtonBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["IncognitoBorderGlowBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["IncognitoActiveButtonBrush"] = Brush(0xFF, 0xFF, 0x00);
            dict["IncognitoActiveButtonBorderBrush"] = Brush(0xFF, 0xFF, 0xFF);
        }
        else if (scheme == ColorScheme.Light)
        {
            // Windows 11 Fluent Light Palette

            dict["PopupBackgroundBrush"] = Brush(0xF3, 0xF3, 0xF3);
            dict["PopupBorderBrush"] = Brush(0xD1, 0xD1, 0xD1);
            dict["PopupShadowColor"] = Color.FromRgb(0x00, 0x00, 0x00);
            dict["PopupShadowOpacity"] = 0.18;

            dict["SearchBackgroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchBorderBrush"] = Brush(0xCE, 0xCE, 0xCE);
            dict["SearchForegroundBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["SearchCaretBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["SearchIconBrush"] = Brush(0x5C, 0x5C, 0x5C);
            dict["SearchPlaceholderBrush"] = Brush(0x76, 0x76, 0x76);
            dict["SearchDropdownHoverBrush"] = Brush(0xEE, 0xEE, 0xEE);
            dict["SearchCapsulePinHoverBrush"] = Brush(0xEE, 0xEE, 0xEE);

            dict["IconButtonForegroundBrush"] = Brush(0x33, 0x33, 0x33);
            dict["IconButtonHoverBackgroundBrush"] = Brush(0xE2, 0xE2, 0xE2);
            dict["IconButtonHoverBorderBrush"] = Brush(0xC8, 0xC8, 0xC8);
            dict["IconButtonPressedBackgroundBrush"] = Brush(0xD0, 0xD0, 0xD0);

            dict["PillButtonBackgroundBrush"] = Brush(0xE8, 0xE8, 0xE8);
            dict["PillButtonBorderBrush"] = Brush(0xD0, 0xD0, 0xD0);
            dict["PillButtonForegroundBrush"] = Brush(0x20, 0x20, 0x20);
            dict["PillButtonHoverBackgroundBrush"] = Brush(0xDC, 0xDC, 0xDC);
            dict["PillButtonHoverBorderBrush"] = Brush(0xBA, 0xBA, 0xBA);
            dict["PillButtonPressedBackgroundBrush"] = Brush(0xD0, 0xD0, 0xD0);

            dict["CardBackgroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardBorderBrush"] = Brush(0xE2, 0xE2, 0xE2);
            dict["CardHoverBackgroundBrush"] = Brush(0xF8, 0xF8, 0xF8);
            dict["CardHoverBorderBrush"] = Brush(0xCC, 0xCC, 0xCC);
            dict["CardSelectedBackgroundBrush"] = Brush(0xED, 0xF5, 0xFD);
            dict["CardSelectedBorderBrush"] = Brush(0x00, 0x78, 0xD4);

            dict["CardPinnedBorderBrush"] = Brush(0xE5, 0xA0, 0x00);
            dict["CardPinnedBackgroundBrush"] = Brush(0xFF, 0xFA, 0xEB);
            dict["CardPinnedBadgeBrush"] = Brush(0xD4, 0x88, 0x00);

            dict["CardTitleBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["CardSubtitleBrush"] = Brush(0x5F, 0x5F, 0x64);
            dict["CardTimeBrush"] = Brush(0x70, 0x70, 0x70);
            dict["CardIconBrush"] = Brush(0x50, 0x50, 0x50);

            // Dialog text & accent (QrWindow, MobileTransferWindow)
            dict["TextBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["SecondaryTextBrush"] = Brush(0x5F, 0x5F, 0x64);
            dict["CardAccentBrush"] = Brush(0x00, 0x78, 0xD4);
            dict["CardAccentForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["CardButtonForegroundBrush"] = Brush(0x5F, 0x5F, 0x64);
            dict["CardButtonHoverBackgroundBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["CardButtonHoverForegroundBrush"] = Brush(0x00, 0x00, 0x00);
            dict["CardButtonPressedBackgroundBrush"] = Brush(0xD0, 0xD0, 0xD0);

            dict["PreviewImageBackgroundBrush"] = Brush(0xEB, 0xEB, 0xEB);
            dict["PreviewCodeBackgroundBrush"] = Brush(0xF6, 0xF8, 0xFA);
            dict["PreviewCodeBorderBrush"] = Brush(0xE1, 0xE4, 0xE8);
            dict["PreviewCodeForegroundBrush"] = Brush(0x24, 0x29, 0x2E);

            dict["CodeKeywordBrush"] = Brush(0xAF, 0x00, 0xDB);
            dict["CodeTypeBrush"] = Brush(0x05, 0x50, 0xAE);
            dict["CodeStringBrush"] = Brush(0xA3, 0x15, 0x15);
            dict["CodeCommentBrush"] = Brush(0x00, 0x80, 0x00);
            dict["CodeNumberBrush"] = Brush(0x09, 0x86, 0x58);
            dict["CodeOperatorBrush"] = Brush(0x24, 0x29, 0x2E);

            dict["PreviewFileBackgroundBrush"] = Brush(0xF8, 0xF9, 0xFA);
            dict["PreviewFileBorderBrush"] = Brush(0xE3, 0xE5, 0xE8);
            dict["PreviewFileSecondaryTextBrush"] = Brush(0x60, 0x60, 0x60);

            dict["PreviewCharBackgroundBrush"] = Brush(0xF5, 0xF5, 0xF5);
            dict["PreviewCharBorderBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["PreviewCharTextBrush"] = Brush(0x66, 0x66, 0x66);

            dict["PreviewTextBackgroundBrush"] = Brush(0xF9, 0xF9, 0xF9);
            dict["PreviewTextBorderBrush"] = Brush(0xE8, 0xE8, 0xE8);
            dict["PreviewTextForegroundBrush"] = Brush(0x1F, 0x1F, 0x1F);

            // Preview Link (Light)
            dict["PreviewLinkBackgroundBrush"] = Brush(0xF8, 0xF9, 0xFA);
            dict["PreviewLinkBorderBrush"] = Brush(0xE3, 0xE5, 0xE8);
            dict["PreviewLinkSecondaryTextBrush"] = Brush(0x60, 0x60, 0x60);
            dict["FaviconContainerBackgroundBrush"] = Brush(0xEB, 0xF3, 0xFC);
            dict["FaviconContainerBorderBrush"] = Brush(0xCE, 0xE1, 0xF8);

            // Semantic Item Kind Accent Brushes (Light)
            dict["KindLinkBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Link), Brush(0x00, 0x67, 0xB8));
            dict["KindCodeBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Code), Brush(0x63, 0x66, 0xF1));
            dict["KindFileBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.File), Brush(0xD9, 0x77, 0x06));
            dict["KindImageBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Image), Brush(0x16, 0xA3, 0x4A));
            dict["KindCharBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Character), Brush(0xE1, 0x1D, 0x48));
            dict["KindColorBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Color), Brush(0xC0, 0x26, 0xD3));
            dict["KindTextBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Text), Brush(0x47, 0x55, 0x69));

            dict["ScrollBarTrackBrush"] = Brush(0xF0, 0xF0, 0xF0);
            dict["ScrollBarThumbBrush"] = Brush(0xCC, 0xCC, 0xCC);
            dict["ScrollBarThumbHoverBrush"] = Brush(0x9E, 0x9E, 0x9E);

            dict["MenuBackgroundBrush"] = Brush(0xFA, 0xFA, 0xFA);
            dict["MenuBorderBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["MenuItemForegroundBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["MenuItemHoverBackgroundBrush"] = Brush(0xEB, 0xEB, 0xEB);
            dict["MenuSeparatorBrush"] = Brush(0xE5, 0xE5, 0xE5);

            // Incognito / Private Session Palette (Light)
            dict["IncognitoBannerBackgroundBrush"] = Brush(0xF5, 0xEE, 0xFD);
            dict["IncognitoBannerBorderBrush"] = Brush(0xD8, 0xB4, 0xFE);
            dict["IncognitoBadgeBackgroundBrush"] = Brush(0xED, 0xDC, 0xFC);
            dict["IncognitoAccentBrush"] = Brush(0x7C, 0x3A, 0xED);
            dict["IncognitoTextBrush"] = Brush(0x4C, 0x1D, 0x95);
            dict["IncognitoExitButtonBackgroundBrush"] = Brush(0xFA, 0xF5, 0xFF);
            dict["IncognitoExitButtonBorderBrush"] = Brush(0xC0, 0x84, 0xFC);
            dict["IncognitoBorderGlowBrush"] = Brush(0x87, 0x64, 0xB8);
            dict["IncognitoActiveButtonBrush"] = Brush(0x7C, 0x3A, 0xED);
            dict["IncognitoActiveButtonBorderBrush"] = Brush(0x93, 0x33, 0xEA);
        }
        else
        {
            // Windows 11 Fluent Dark Palette
            dict["PopupBackgroundBrush"] = Brush(0x20, 0x20, 0x20);
            dict["PopupBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["PopupShadowColor"] = Color.FromRgb(0x00, 0x00, 0x00);
            dict["PopupShadowOpacity"] = 0.45;

            dict["SearchBackgroundBrush"] = Brush(0x2B, 0x2B, 0x2B);
            dict["SearchBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["SearchForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchCaretBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SearchIconBrush"] = Brush(0x88, 0x88, 0x88);
            dict["SearchPlaceholderBrush"] = Brush(0x88, 0x88, 0x88);
            dict["SearchDropdownHoverBrush"] = Brush(0x38, 0x38, 0x38);
            dict["SearchCapsulePinHoverBrush"] = Brush(0x38, 0x38, 0x38);

            dict["IconButtonForegroundBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["IconButtonHoverBackgroundBrush"] = Brush(0x38, 0x38, 0x38);
            dict["IconButtonHoverBorderBrush"] = Brush(0x4F, 0x4F, 0x4F);
            dict["IconButtonPressedBackgroundBrush"] = Brush(0x25, 0x25, 0x25);

            dict["PillButtonBackgroundBrush"] = Brush(0x2D, 0x2D, 0x2D);
            dict["PillButtonBorderBrush"] = Brush(0x40, 0x40, 0x40);
            dict["PillButtonForegroundBrush"] = Brush(0xE8, 0xE8, 0xE8);
            dict["PillButtonHoverBackgroundBrush"] = Brush(0x38, 0x38, 0x38);
            dict["PillButtonHoverBorderBrush"] = Brush(0x4F, 0x4F, 0x4F);
            dict["PillButtonPressedBackgroundBrush"] = Brush(0x22, 0x22, 0x22);

            dict["CardBackgroundBrush"] = Brush(0x2B, 0x2B, 0x2B);
            dict["CardBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["CardHoverBackgroundBrush"] = Brush(0x33, 0x33, 0x33);
            dict["CardHoverBorderBrush"] = Brush(0x4A, 0x4A, 0x4A);
            dict["CardSelectedBackgroundBrush"] = Brush(0x30, 0x30, 0x30);
            dict["CardSelectedBorderBrush"] = Brush(0x00, 0x78, 0xD4);

            dict["CardPinnedBorderBrush"] = Brush(0xD4, 0x9B, 0x00);
            dict["CardPinnedBackgroundBrush"] = Brush(0x32, 0x2C, 0x22);
            dict["CardPinnedBadgeBrush"] = Brush(0xFF, 0xC1, 0x07);

            dict["CardTitleBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardSubtitleBrush"] = Brush(0x8E, 0x8E, 0x93);
            dict["CardTimeBrush"] = Brush(0x77, 0x77, 0x77);
            dict["CardIconBrush"] = Brush(0xA0, 0xA0, 0xA0);

            // Dialog text & accent (QrWindow, MobileTransferWindow)
            dict["TextBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["SecondaryTextBrush"] = Brush(0xC5, 0xC5, 0xC5);
            dict["CardAccentBrush"] = Brush(0x00, 0x78, 0xD4);
            dict["CardAccentForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);

            dict["CardButtonForegroundBrush"] = Brush(0x8E, 0x8E, 0x93);
            dict["CardButtonHoverBackgroundBrush"] = Brush(0x44, 0x44, 0x44);
            dict["CardButtonHoverForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardButtonPressedBackgroundBrush"] = Brush(0x25, 0x25, 0x25);

            dict["PreviewImageBackgroundBrush"] = Brush(0x1A, 0x1A, 0x1A);
            dict["PreviewCodeBackgroundBrush"] = Brush(0x18, 0x18, 0x18);
            dict["PreviewCodeBorderBrush"] = Brush(0x28, 0x28, 0x28);
            dict["PreviewCodeForegroundBrush"] = Brush(0xDC, 0xDC, 0xDC);

            dict["CodeKeywordBrush"] = Brush(0xC5, 0x86, 0xC0);
            dict["CodeTypeBrush"] = Brush(0x4E, 0xC9, 0xB0);
            dict["CodeStringBrush"] = Brush(0xCE, 0x91, 0x78);
            dict["CodeCommentBrush"] = Brush(0x6A, 0x99, 0x55);
            dict["CodeNumberBrush"] = Brush(0xB5, 0xCE, 0xA8);
            dict["CodeOperatorBrush"] = Brush(0xD4, 0xD4, 0xD4);

            dict["PreviewFileBackgroundBrush"] = Brush(0x22, 0x22, 0x22);
            dict["PreviewFileBorderBrush"] = Brush(0x33, 0x33, 0x33);
            dict["PreviewFileSecondaryTextBrush"] = Brush(0x9E, 0x9E, 0x9E);

            dict["PreviewCharBackgroundBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["PreviewCharBorderBrush"] = Brush(0x2A, 0x2A, 0x2A);
            dict["PreviewCharTextBrush"] = Brush(0x77, 0x77, 0x77);

            dict["PreviewTextBackgroundBrush"] = Brush(0x23, 0x23, 0x23);
            dict["PreviewTextBorderBrush"] = Brush(0x2E, 0x2E, 0x2E);
            dict["PreviewTextForegroundBrush"] = Brush(0xE0, 0xE0, 0xE0);

            // Preview Link (Dark)
            dict["PreviewLinkBackgroundBrush"] = Brush(0x22, 0x22, 0x22);
            dict["PreviewLinkBorderBrush"] = Brush(0x33, 0x33, 0x33);
            dict["PreviewLinkSecondaryTextBrush"] = Brush(0x9E, 0x9E, 0x9E);
            dict["FaviconContainerBackgroundBrush"] = Brush(0x18, 0x2A, 0x3A);
            dict["FaviconContainerBorderBrush"] = Brush(0x25, 0x40, 0x59);

            // Semantic Item Kind Accent Brushes (Dark)
            dict["KindLinkBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Link), Brush(0x4C, 0xC2, 0xFF));
            dict["KindCodeBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Code), Brush(0xA7, 0x8B, 0xFA));
            dict["KindFileBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.File), Brush(0xFB, 0xBF, 0x24));
            dict["KindImageBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Image), Brush(0x4A, 0xDE, 0x80));
            dict["KindCharBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Character), Brush(0xFB, 0x71, 0x85));
            dict["KindColorBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Color), Brush(0xE8, 0x79, 0xF9));
            dict["KindTextBrush"] = BrushFromHex(customColors?.GetCustomColor(ItemKind.Text), Brush(0x94, 0xA3, 0xB8));

            dict["ScrollBarTrackBrush"] = Brush(0x24, 0x24, 0x24);
            dict["ScrollBarThumbBrush"] = Brush(0x4A, 0x4A, 0x4A);
            dict["ScrollBarThumbHoverBrush"] = Brush(0x66, 0x66, 0x66);

            dict["MenuBackgroundBrush"] = Brush(0x2B, 0x2B, 0x2B);
            dict["MenuBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["MenuItemForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["MenuItemHoverBackgroundBrush"] = Brush(0x38, 0x38, 0x38);
            dict["MenuSeparatorBrush"] = Brush(0x38, 0x38, 0x38);

            // Incognito / Private Session Palette (Dark)
            dict["IncognitoBannerBackgroundBrush"] = Brush(0x28, 0x18, 0x38);
            dict["IncognitoBannerBorderBrush"] = Brush(0x58, 0x2A, 0x7E);
            dict["IncognitoBadgeBackgroundBrush"] = Brush(0x3B, 0x1E, 0x54);
            dict["IncognitoAccentBrush"] = Brush(0xC0, 0x84, 0xFC);
            dict["IncognitoTextBrush"] = Brush(0xE9, 0xD5, 0xFF);
            dict["IncognitoExitButtonBackgroundBrush"] = Brush(0x3B, 0x1E, 0x54);
            dict["IncognitoExitButtonBorderBrush"] = Brush(0x7E, 0x22, 0xCE);
            dict["IncognitoBorderGlowBrush"] = Brush(0x93, 0x33, 0xEA);
            dict["IncognitoActiveButtonBrush"] = Brush(0x87, 0x64, 0xB8);
            dict["IncognitoActiveButtonBorderBrush"] = Brush(0xA8, 0x55, 0xF7);
        }

        return dict;
    }
}
