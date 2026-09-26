// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Media;
using WindowsCM.Core.Popup;
using WinForms = System.Windows.Forms;

namespace WindowsCM.App;

// Connected displays for placement and for the Settings monitor picker.
// Screen reports device pixels; placement works in WPF DIPs, so callers
// pass the TransformFromDevice of the window being placed.
internal static class DisplayMonitors
{
    // A display as listed in Settings: 1-based number in desk order
    // (MonitorLayout.Ordered), identity and the pixel resolution users know
    // from Windows display settings.
    public sealed record Display(int Number, string DeviceName, bool IsPrimary, int PixelWidth, int PixelHeight);

    public static IReadOnlyList<MonitorArea> InDips(Matrix fromDevice) =>
        MonitorLayout.Ordered(WinForms.Screen.AllScreens.Select(screen => new MonitorArea(
            screen.DeviceName,
            ToDips(screen.Bounds, fromDevice),
            ToDips(screen.WorkingArea, fromDevice),
            screen.Primary)));

    public static IReadOnlyList<Display> Numbered()
    {
        var screens = WinForms.Screen.AllScreens;
        var ordered = MonitorLayout.Ordered(screens.Select(screen => new MonitorArea(
            screen.DeviceName,
            ToDips(screen.Bounds, Matrix.Identity),
            ToDips(screen.WorkingArea, Matrix.Identity),
            screen.Primary)));
        return ordered
            .Select((monitor, index) =>
            {
                var screen = screens.First(s => s.DeviceName == monitor.DeviceName);
                return new Display(index + 1, screen.DeviceName, screen.Primary, screen.Bounds.Width, screen.Bounds.Height);
            })
            .ToList();
    }

    public static WorkArea ToDips(System.Drawing.Rectangle pixels, Matrix fromDevice)
    {
        var topLeft = fromDevice.Transform(new System.Windows.Point(pixels.Left, pixels.Top));
        var bottomRight = fromDevice.Transform(new System.Windows.Point(pixels.Right, pixels.Bottom));
        return new WorkArea(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }
}
