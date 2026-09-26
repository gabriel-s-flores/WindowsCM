// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// One connected display as the placement code sees it: the OS device name
// (stable identity for the "fixed monitor" setting), its full bounds and
// its working area (bounds minus the taskbar), all in the same units.
public sealed record MonitorArea(string DeviceName, WorkArea Bounds, WorkArea WorkingArea, bool IsPrimary);

public static class MonitorLayout
{
    // The order users see on their desk — left to right, then top to
    // bottom — so "Monitor 1" in Settings is the leftmost screen and the
    // Identify overlay numbers match.
    public static IReadOnlyList<MonitorArea> Ordered(IEnumerable<MonitorArea> monitors) =>
        monitors
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();

    // The monitor for the "fixed monitor" mode: the saved device when it is
    // connected, otherwise the primary (a disconnected choice is kept in
    // settings and used again when it comes back), otherwise any monitor.
    public static MonitorArea? Resolve(string? deviceName, IReadOnlyList<MonitorArea> monitors)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var saved = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (saved is not null)
            {
                return saved;
            }
        }
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }
}
