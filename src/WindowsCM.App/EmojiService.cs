// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.Direct2D.Common;
using Windows.Win32.Graphics.DirectWrite;
using Windows.Win32.Graphics.Gdi;
using WindowsCM.Core.Classification;

namespace WindowsCM.App;

// High-performance Direct2D + DirectWrite color emoji renderer for Windows 11 Fluent Design.
// Renders native Segoe UI Emoji with OpenType COLR/CPAL color glyphs enabled (D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT).
// Caches rendered bitmaps in memory (0ms overhead during popup scrolling) and scales dynamically for multi-emoji cards.
public static class EmojiService
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.Ordinal);
    private static readonly object RenderLock = new();

    public static ImageSource? GetEmojiThumbnail(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var text = rawText.Trim();
        if (!EmojiDetector.IsAllEmojis(text))
        {
            return null;
        }

        if (Cache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        lock (RenderLock)
        {
            if (Cache.TryGetValue(text, out cached))
            {
                return cached;
            }

            var rendered = RenderColorEmoji(text);
            if (rendered != null)
            {
                Cache[text] = rendered;
            }
            return rendered;
        }
    }

    private static unsafe BitmapSource? RenderColorEmoji(string text)
    {
        try
        {
            var count = EmojiDetector.CountEmojis(text);

            // Compute ideal container dimensions and font size based on emoji count
            int width;
            int height;
            float fontSize;

            if (count <= 1)
            {
                width = 120;
                height = 110;
                fontSize = 56f;
            }
            else if (count == 2)
            {
                width = 170;
                height = 90;
                fontSize = 42f;
            }
            else if (count == 3)
            {
                width = 180;
                height = 85;
                fontSize = 36f;
            }
            else if (count <= 5)
            {
                width = 190;
                height = 80;
                fontSize = 28f;
            }
            else
            {
                width = 200;
                height = 80;
                fontSize = 22f;
            }

            HDC hdc = PInvoke.CreateCompatibleDC(HDC.Null);
            if (hdc.IsNull)
            {
                return null;
            }

            BITMAPINFO bmi = default;
            bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            void* pBits = null;
            HBITMAP hBmp = PInvoke.CreateDIBSection(hdc, &bmi, DIB_USAGE.DIB_RGB_COLORS, &pBits, HANDLE.Null, 0);
            if (hBmp.IsNull || pBits == null)
            {
                PInvoke.DeleteDC(hdc);
                return null;
            }

            HGDIOBJ oldBmp = PInvoke.SelectObject(hdc, hBmp);

            try
            {
                var guidFactory = typeof(ID2D1Factory).GUID;
                PInvoke.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, &guidFactory, null, out var factoryObj);
                var d2dFactory = (ID2D1Factory)factoryObj;

                var rtProps = new D2D1_RENDER_TARGET_PROPERTIES
                {
                    type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
                    pixelFormat = new D2D1_PIXEL_FORMAT
                    {
                        format = Windows.Win32.Graphics.Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                        alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED
                    },
                    dpiX = 96,
                    dpiY = 96,
                    usage = D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE,
                    minLevel = D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT
                };

                d2dFactory.CreateDCRenderTarget(&rtProps, out var dcRenderTarget);

                var rect = new RECT { left = 0, top = 0, right = width, bottom = height };
                dcRenderTarget.BindDC(hdc, &rect);

                var guidDW = typeof(IDWriteFactory).GUID;
                PInvoke.DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, &guidDW, out var dwFactoryObj);
                var dwFactory = (IDWriteFactory)dwFactoryObj;

                dwFactory.CreateTextFormat(
                    "Segoe UI Emoji",
                    null,
                    DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
                    DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
                    fontSize,
                    "en-US",
                    out var textFormat);

                textFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
                textFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

                var black = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 1 };
                dcRenderTarget.CreateSolidColorBrush(&black, null, out var brush);

                var d2dRect = new D2D_RECT_F { left = 0, top = 0, right = width, bottom = height };

                dcRenderTarget.BeginDraw();
                var clear = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 0 };
                dcRenderTarget.Clear(&clear);

                fixed (char* pText = text)
                {
                    dcRenderTarget.DrawText(
                        pText,
                        (uint)text.Length,
                        textFormat,
                        &d2dRect,
                        brush,
                        D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT,
                        DWRITE_MEASURING_MODE.DWRITE_MEASURING_MODE_NATURAL);
                }

                dcRenderTarget.EndDraw(null, null);

                int stride = width * 4;
                byte[] pixels = new byte[stride * height];
                Marshal.Copy((IntPtr)pBits, pixels, 0, pixels.Length);

                var bs = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
                bs.Freeze();
                return bs;
            }
            finally
            {
                PInvoke.SelectObject(hdc, oldBmp);
                PInvoke.DeleteObject(hBmp);
                PInvoke.DeleteDC(hdc);
            }
        }
        catch
        {
            return null;
        }
    }
}
