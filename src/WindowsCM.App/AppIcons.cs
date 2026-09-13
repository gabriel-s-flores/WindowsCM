// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace WindowsCM.App;

// Production tray icons: loads high-resolution Fluent Design assets (app.ico / app-flash.ico)
// matching the current system DPI / SmallIconSize, with fallback to vector rendering.
// Overlay is the copy-feedback flash (Copyous wiggle parity: alternate base/overlay 3×65ms).
internal static class AppIcons
{
    private static readonly Color BgBase = Color.FromArgb(0x00, 0x78, 0xD4);
    private static readonly Color BgFlash = Color.FromArgb(0x00, 0xE5, 0xFF);
    private static readonly Color Paper = Color.FromArgb(0xFA, 0xFA, 0xFB);
    private static readonly Color Clip = Color.FromArgb(0x00, 0x5A, 0x9E);

    public static Icon Base { get; } = LoadIcon("app.ico", isBase: true);
    public static Icon Overlay { get; } = LoadIcon("app-flash.ico", isBase: false);

    private static Icon LoadIcon(string assetName, bool isBase)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{assetName}", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                var smallSize = System.Windows.Forms.SystemInformation.SmallIconSize;
                return new Icon(stream, smallSize);
            }
        }
        catch
        {
            // Fall through to file path or vector fallback
        }

        try
        {
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", assetName);
            if (File.Exists(localPath))
            {
                var smallSize = System.Windows.Forms.SystemInformation.SmallIconSize;
                return new Icon(localPath, smallSize);
            }
        }
        catch
        {
            // Fall through to vector fallback
        }

        return Build(isBase);
    }

    private static Icon Build(bool isBase)
    {
        using var bitmap = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var bg = new SolidBrush(isBase ? BgBase : BgFlash))
            using (var path = RoundedRect(2, 2, 60, 60, 14))
            {
                g.FillPath(bg, path);
            }
            // Frosted / paper sheet.
            using (var paper = new SolidBrush(Paper))
            {
                g.FillRectangle(paper, 18, 14, 28, 38);
            }
            // Clip.
            using (var clip = new SolidBrush(isBase ? Clip : BgBase))
            {
                g.FillRectangle(clip, 26, 10, 12, 8);
            }
            // Content lines.
            using (var lines = new SolidBrush(isBase ? BgBase : Clip))
            {
                g.FillRectangle(lines, 22, 24, 20, 3);
                g.FillRectangle(lines, 22, 31, 20, 3);
                g.FillRectangle(lines, 22, 38, 14, 3);
            }
        }
        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
