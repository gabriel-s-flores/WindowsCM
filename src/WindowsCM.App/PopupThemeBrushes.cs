using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

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

    public static ResourceDictionary CreateThemeDictionary(bool isLight)
    {
        var dict = new ResourceDictionary();

        if (isLight)
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

            dict["IconButtonForegroundBrush"] = Brush(0x33, 0x33, 0x33);
            dict["IconButtonHoverBackgroundBrush"] = Brush(0xE2, 0xE2, 0xE2);
            dict["IconButtonHoverBorderBrush"] = Brush(0xC8, 0xC8, 0xC8);
            dict["IconButtonPressedBackgroundBrush"] = Brush(0xD0, 0xD0, 0xD0);

            dict["CardBackgroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardBorderBrush"] = Brush(0xE2, 0xE2, 0xE2);
            dict["CardHoverBackgroundBrush"] = Brush(0xF8, 0xF8, 0xF8);
            dict["CardHoverBorderBrush"] = Brush(0xCC, 0xCC, 0xCC);
            dict["CardSelectedBackgroundBrush"] = Brush(0xED, 0xF5, 0xFD);
            dict["CardSelectedBorderBrush"] = Brush(0x00, 0x78, 0xD4);

            dict["CardTitleBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["CardSubtitleBrush"] = Brush(0x5F, 0x5F, 0x64);
            dict["CardTimeBrush"] = Brush(0x70, 0x70, 0x70);
            dict["CardIconBrush"] = Brush(0x50, 0x50, 0x50);

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

            dict["ScrollBarTrackBrush"] = Brush(0xF0, 0xF0, 0xF0);
            dict["ScrollBarThumbBrush"] = Brush(0xCC, 0xCC, 0xCC);
            dict["ScrollBarThumbHoverBrush"] = Brush(0x9E, 0x9E, 0x9E);

            dict["MenuBackgroundBrush"] = Brush(0xFA, 0xFA, 0xFA);
            dict["MenuBorderBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["MenuItemForegroundBrush"] = Brush(0x1C, 0x1C, 0x1C);
            dict["MenuItemHoverBackgroundBrush"] = Brush(0xEB, 0xEB, 0xEB);
            dict["MenuSeparatorBrush"] = Brush(0xE5, 0xE5, 0xE5);
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

            dict["IconButtonForegroundBrush"] = Brush(0xE0, 0xE0, 0xE0);
            dict["IconButtonHoverBackgroundBrush"] = Brush(0x38, 0x38, 0x38);
            dict["IconButtonHoverBorderBrush"] = Brush(0x4F, 0x4F, 0x4F);
            dict["IconButtonPressedBackgroundBrush"] = Brush(0x25, 0x25, 0x25);

            dict["CardBackgroundBrush"] = Brush(0x2B, 0x2B, 0x2B);
            dict["CardBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["CardHoverBackgroundBrush"] = Brush(0x33, 0x33, 0x33);
            dict["CardHoverBorderBrush"] = Brush(0x4A, 0x4A, 0x4A);
            dict["CardSelectedBackgroundBrush"] = Brush(0x30, 0x30, 0x30);
            dict["CardSelectedBorderBrush"] = Brush(0x00, 0x78, 0xD4);

            dict["CardTitleBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["CardSubtitleBrush"] = Brush(0x8E, 0x8E, 0x93);
            dict["CardTimeBrush"] = Brush(0x77, 0x77, 0x77);
            dict["CardIconBrush"] = Brush(0xA0, 0xA0, 0xA0);

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

            dict["ScrollBarTrackBrush"] = Brush(0x24, 0x24, 0x24);
            dict["ScrollBarThumbBrush"] = Brush(0x4A, 0x4A, 0x4A);
            dict["ScrollBarThumbHoverBrush"] = Brush(0x66, 0x66, 0x66);

            dict["MenuBackgroundBrush"] = Brush(0x2B, 0x2B, 0x2B);
            dict["MenuBorderBrush"] = Brush(0x38, 0x38, 0x38);
            dict["MenuItemForegroundBrush"] = Brush(0xFF, 0xFF, 0xFF);
            dict["MenuItemHoverBackgroundBrush"] = Brush(0x38, 0x38, 0x38);
            dict["MenuSeparatorBrush"] = Brush(0x38, 0x38, 0x38);
        }

        return dict;
    }
}
