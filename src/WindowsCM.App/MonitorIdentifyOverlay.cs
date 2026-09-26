// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace WindowsCM.App;

// Settings "Identify": flashes each monitor's number (the Settings list
// numbering) in the middle of that monitor for two seconds, like Windows
// display settings, so "Monitor 2" is unambiguous on a multi-monitor desk.
internal static class MonitorIdentifyOverlay
{
    private const double BadgeSize = 180;

    public static void ShowAll(Window owner)
    {
        var fromDevice = PresentationSource.FromVisual(owner)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var screens = WinForms.Screen.AllScreens;
        var overlays = new List<Window>();
        foreach (var display in DisplayMonitors.Numbered())
        {
            var screen = screens.FirstOrDefault(s => s.DeviceName == display.DeviceName);
            if (screen is null)
            {
                continue;
            }
            var area = DisplayMonitors.ToDips(screen.WorkingArea, fromDevice);
            var overlay = CreateBadge(display.Number);
            overlay.Left = area.Left + ((area.Right - area.Left - BadgeSize) / 2);
            overlay.Top = area.Top + ((area.Bottom - area.Top - BadgeSize) / 2);
            overlay.Show();
            overlays.Add(overlay);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var overlay in overlays)
            {
                overlay.Close();
            }
        };
        timer.Start();
    }

    private static Window CreateBadge(int number)
    {
        var badge = new System.Windows.Controls.Border
        {
            Width = BadgeSize,
            Height = BadgeSize,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xE6, 0x20, 0x20, 0x20)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)),
            BorderThickness = new Thickness(3),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 110,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            IsHitTestVisible = false,
            Width = BadgeSize,
            Height = BadgeSize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = badge,
        };
        // Click-through: the overlay must never steal a click meant for
        // the window underneath while it is up.
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
        return window;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
