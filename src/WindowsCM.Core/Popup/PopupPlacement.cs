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

// Ticket 21: stable popup size on 1080p. The card strip keeps one fixed
// width every open for the same history (XAML Width parity) so right-edge
// clamping never drifts with measured content width; height is the measured
// content clamped to the window max. The shell freezes SizeToContent after
// Show so search filtering never resizes the window.
public static class PopupSizing
{
    // PopupWindow.xaml Width="380" parity (enforced by the shell each open).
    public const double FixedWidth = 380;

    // PopupWindow.xaml MaxHeight="520" parity.
    public const double MaxHeight = 520;

    public static double ClampHeight(double measuredHeight) => Math.Min(measuredHeight, MaxHeight);
}
