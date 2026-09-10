// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// Opt-in autostart via HKCU\...\Run (research 02, spec story 41): the value
// is "<exe>" --hidden so login starts tray-only. The installer checkbox and
// the settings toggle both funnel through here; the WPF layer passes the
// entry-assembly path, tests use a fake path. Quoting is unconditional so
// Program Files paths survive intact.
public static class AutostartManager
{
    public const string RunValueName = "WindowsCM";
    public const string HiddenFlag = "--hidden";

    public static string BuildCommand(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("Executable path must not be empty.", nameof(exePath));
        }
        return "\"" + exePath.Trim() + "\" " + HiddenFlag;
    }

    public static bool IsEnabled(IRunKeyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.GetCommand() is not null;
    }

    public static void Enable(IRunKeyStore store, string exePath)
    {
        ArgumentNullException.ThrowIfNull(store);
        store.SetCommand(BuildCommand(exePath));
    }

    public static void Disable(IRunKeyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        store.Remove();
    }
}
