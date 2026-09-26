// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Popup;

// Cursor-first placement with per-monitor DPI clamping (research 03
// §4.2–§4.3, spec: cursor-first v1, caret-UIA deferred to v2). All math is
// unit-agnostic DIPs: the UI converts GetCursorPos pixels and the
// Screen.WorkingArea via TransformFromDevice (or VisualTreeHelper.GetDpi)
// before calling in, then assigns Window.Left/Top directly.
public sealed record WorkArea(double Left, double Top, double Right, double Bottom);

public static class PopupPlacement
{
    // Cursor offset so the popup never opens directly under the pointer
    // (prototype Window_Loaded parity: +12/+12 DIPs).
    public const double CursorOffset = 12;

    public static (double Left, double Top) PlaceAtCursor(
        double cursorX,
        double cursorY,
        double popupWidth,
        double popupHeight,
        WorkArea area)
    {
        var left = cursorX + CursorOffset;
        var top = cursorY + CursorOffset;
        if (left + popupWidth > area.Right)
        {
            left = area.Right - popupWidth;
        }
        if (top + popupHeight > area.Bottom)
        {
            top = area.Bottom - popupHeight;
        }
        if (left < area.Left)
        {
            left = area.Left;
        }
        if (top < area.Top)
        {
            top = area.Top;
        }
        return (left, top);
    }

    public static (double Left, double Top, double Width) PlaceHorizontalFill(
        double cursorY,
        double popupHeight,
        WorkArea area,
        double margin = PopupSizing.DefaultHorizontalMargin)
    {
        var width = CalculateHorizontalFillWidth(area, margin);
        var left = area.Left + margin;
        var top = cursorY + CursorOffset;
        if (top + popupHeight > area.Bottom)
        {
            top = area.Bottom - popupHeight;
        }
        if (top < area.Top)
        {
            top = area.Top;
        }
        return (left, top, width);
    }

    public static double CalculateHorizontalFillWidth(
        WorkArea area,
        double margin = PopupSizing.DefaultHorizontalMargin)
    {
        var available = (area.Right - area.Left) - (2 * margin);
        return Math.Max(PopupSizing.MinWidth, available);
    }

    public static (double Left, double Top, double Width, double Height) PlaceLargePopup(
        DialogOrientation orientation,
        LargeHorizontalPosition hPos,
        LargeVerticalPosition vPos,
        WorkArea area,
        double verticalWidth = PopupSizing.DefaultVerticalWidth,
        double horizontalHeight = PopupSizing.MaxHeight,
        double margin = PopupSizing.DefaultHorizontalMargin)
    {
        if (orientation == DialogOrientation.Vertical)
        {
            var width = Math.Min(verticalWidth, Math.Max(0, (area.Right - area.Left) - (2 * margin)));
            var height = Math.Max(0, (area.Bottom - area.Top) - (2 * margin));
            var top = area.Top + margin;
            var left = vPos == LargeVerticalPosition.Right
                ? area.Right - width - margin
                : area.Left + margin;
            return (left, top, width, height);
        }
        else
        {
            var width = CalculateHorizontalFillWidth(area, margin);
            var height = horizontalHeight;
            var left = area.Left + margin;
            var top = hPos == LargeHorizontalPosition.Top
                ? area.Top + margin
                : area.Bottom - height - margin;
            return (left, top, width, height);
        }
    }

    // Smallest free-mode size that still fits the header: horizontally the
    // action buttons, the 420 DIP search capsule and the clear pill share
    // one row; vertically the capsule gets its own row.
    public static (double Width, double Height) FreeMinimumSize(DialogOrientation orientation) =>
        orientation == DialogOrientation.Vertical ? (320, 360) : (720, 260);

    // First free-mode placement: a floating window centered on the given
    // area, visibly detached from the edges so it reads as movable.
    public static (double Left, double Top, double Width, double Height) DefaultFreeBounds(
        DialogOrientation orientation, WorkArea area)
    {
        var areaWidth = area.Right - area.Left;
        var areaHeight = area.Bottom - area.Top;
        var (minWidth, minHeight) = FreeMinimumSize(orientation);
        double width;
        double height;
        if (orientation == DialogOrientation.Vertical)
        {
            width = PopupSizing.DefaultVerticalWidth;
            height = Math.Max(minHeight, Math.Min(820, areaHeight * 0.8));
        }
        else
        {
            width = Math.Max(minWidth, Math.Min(1100, areaWidth * 0.8));
            height = PopupSizing.MaxHeight;
        }
        width = Math.Min(width, areaWidth);
        height = Math.Min(height, areaHeight);
        return (area.Left + ((areaWidth - width) / 2), area.Top + ((areaHeight - height) / 2), width, height);
    }

    // Free mode: reopen exactly where the user left the window, as long as
    // it is still on a connected monitor. The rectangle is pulled fully
    // into the working area it overlaps most (a smaller or rearranged
    // monitor can never strand it off-screen); when it overlaps none — the
    // monitor it lived on is gone — it starts over centered on fallbackArea.
    public static (double Left, double Top, double Width, double Height) PlaceFree(
        WindowBounds? saved,
        IReadOnlyList<WorkArea> workAreas,
        WorkArea fallbackArea,
        DialogOrientation orientation)
    {
        if (saved is not { IsUsable: true })
        {
            return DefaultFreeBounds(orientation, fallbackArea);
        }
        WorkArea? best = null;
        var bestOverlap = 0.0;
        foreach (var area in workAreas)
        {
            var overlap = Overlap(saved, area);
            if (overlap > bestOverlap)
            {
                best = area;
                bestOverlap = overlap;
            }
        }
        if (best is null)
        {
            return DefaultFreeBounds(orientation, fallbackArea);
        }
        var areaWidth = best.Right - best.Left;
        var areaHeight = best.Bottom - best.Top;
        var (minWidth, minHeight) = FreeMinimumSize(orientation);
        var width = Math.Min(Math.Max(saved.Width, Math.Min(minWidth, areaWidth)), areaWidth);
        var height = Math.Min(Math.Max(saved.Height, Math.Min(minHeight, areaHeight)), areaHeight);
        // Max() guards Clamp against min > max from floating-point rounding
        // when the window is exactly as large as the area.
        var left = Math.Clamp(saved.Left, best.Left, Math.Max(best.Left, best.Right - width));
        var top = Math.Clamp(saved.Top, best.Top, Math.Max(best.Top, best.Bottom - height));
        return (left, top, width, height);
    }

    private static double Overlap(WindowBounds rect, WorkArea area)
    {
        var width = Math.Min(rect.Left + rect.Width, area.Right) - Math.Max(rect.Left, area.Left);
        var height = Math.Min(rect.Top + rect.Height, area.Bottom) - Math.Max(rect.Top, area.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }

    public static (double Left, double Top, double Width, double Height) PlaceCompactPopup(
        DialogOrientation orientation,
        double cursorX,
        double cursorY,
        WorkArea area,
        double verticalWidth = 320,
        double verticalHeight = 480,
        double horizontalWidth = 540,
        double horizontalHeight = 240)
    {
        var width = orientation == DialogOrientation.Horizontal ? horizontalWidth : verticalWidth;
        var height = orientation == DialogOrientation.Horizontal ? horizontalHeight : verticalHeight;
        var (left, top) = PlaceAtCursor(cursorX, cursorY, width, height, area);
        return (left, top, width, height);
    }

    // Physical pixels to DIPs at the point of positioning
    // (CompositionTarget.TransformFromDevice parity).
    public static double ToDips(double pixels, double fromDeviceScale) => pixels * fromDeviceScale;
}

// Ticket 21, 26, 27 & 29: popup size on Windows 11. The horizontal card strip
// fills the width of the display working area (with comfortable side margins)
// in parity with Copyous horizontal layout; height is clamped to the window max (348px)
// ensuring ample clearance for cards and horizontal scrollbar without clipping.
public static class PopupSizing
{
    public const double DefaultHorizontalMargin = 20;

    public const double MinWidth = 600;

    // Baseline fallback width
    public const double FixedWidth = 880;

    // Default width for vertical large popup
    public const double DefaultVerticalWidth = 380;

    // PopupWindow.xaml MaxHeight="348" parity (240 card + 44 header + 24 padding + 40 scrollbar/breathing room).
    public const double MaxHeight = 348;

    public static double ClampHeight(double measuredHeight) => Math.Min(measuredHeight, MaxHeight);
}

public enum ResizeEdge
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

// Free-mode resizing of the borderless large window: which edge a point
// (in the same units as the window rectangle) grabs. The window answers
// WM_NCHITTEST with the matching HT code so Windows runs its native resize
// loop. Corners get a zone twice the border so diagonal resizing is easy
// to hit.
public static class ResizeHitTest
{
    public static ResizeEdge EdgeAt(double x, double y, WorkArea window, double border)
    {
        if (x < window.Left || x >= window.Right || y < window.Top || y >= window.Bottom || border <= 0)
        {
            return ResizeEdge.None;
        }
        var corner = border * 2;
        var left = x < window.Left + border;
        var right = x >= window.Right - border;
        var top = y < window.Top + border;
        var bottom = y >= window.Bottom - border;
        var nearLeft = x < window.Left + corner;
        var nearRight = x >= window.Right - corner;
        var nearTop = y < window.Top + corner;
        var nearBottom = y >= window.Bottom - corner;

        if ((top && nearLeft) || (left && nearTop))
        {
            return ResizeEdge.TopLeft;
        }
        if ((top && nearRight) || (right && nearTop))
        {
            return ResizeEdge.TopRight;
        }
        if ((bottom && nearLeft) || (left && nearBottom))
        {
            return ResizeEdge.BottomLeft;
        }
        if ((bottom && nearRight) || (right && nearBottom))
        {
            return ResizeEdge.BottomRight;
        }
        if (left)
        {
            return ResizeEdge.Left;
        }
        if (right)
        {
            return ResizeEdge.Right;
        }
        if (top)
        {
            return ResizeEdge.Top;
        }
        return bottom ? ResizeEdge.Bottom : ResizeEdge.None;
    }
}
