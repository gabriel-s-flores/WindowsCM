// SPDX-License-Identifier: GPL-3.0-or-later
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

    // Physical pixels to DIPs at the point of positioning
    // (CompositionTarget.TransformFromDevice parity).
    public static double ToDips(double pixels, double fromDeviceScale) => pixels * fromDeviceScale;
}
