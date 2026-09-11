// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WindowsCM.App;

// Runtime tray icons (no binary asset to ship): a dark rounded square with
// a paper-clipboard glyph, plus an accent variant for the copy-feedback
// flash (Copyous wiggle parity: alternate base/overlay 3×65ms).
internal static class AppIcons
{
    private static readonly Color Bg = Color.FromArgb(0x36, 0x36, 0x3A);
    private static readonly Color Paper = Color.FromArgb(0xFA, 0xFA, 0xFB);
    private static readonly Color Accent = Color.FromArgb(0x35, 0x84, 0xE4);

    public static Icon Base { get; } = Build(true);
    public static Icon Overlay { get; } = Build(false);

    private static Icon Build(bool isBase)
    {
        using var bitmap = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var bg = new SolidBrush(isBase ? Bg : Accent))
            using (var path = RoundedRect(2, 2, 60, 60, 12))
            {
                g.FillPath(bg, path);
            }
            // Paper sheet.
            using (var paper = new SolidBrush(isBase ? Paper : Bg))
            {
                g.FillRectangle(paper, 20, 14, 24, 36);
            }
            // Clip.
            using (var clip = new SolidBrush(isBase ? Accent : Paper))
            {
                g.FillRectangle(clip, 27, 10, 10, 8);
            }
            // Text lines.
            using (var lines = new SolidBrush(isBase ? Bg : Paper))
            {
                g.FillRectangle(lines, 24, 24, 16, 3);
                g.FillRectangle(lines, 24, 31, 16, 3);
                g.FillRectangle(lines, 24, 38, 10, 3);
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
