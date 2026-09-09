// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// Per-process exclusions (Copyous `wmclass-exclusions` → Windows process
// names, grilling 06 Q8). Matched case-insensitively with or without a
// trailing ".exe" (CaptureOptions parity). The settings window binds a
// list box plus an add box; validation messages are shown, never thrown.
public sealed class ProcessExclusions
{
    private HashSet<string> _processes = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Processes
    {
        get => _processes;
        // Hand-edited JSON may carry null: coerce to empty, never null.
        set => _processes = value ?? new(StringComparer.OrdinalIgnoreCase);
    }

    public bool Add(string processName)
    {
        if (ValidateProcessName(processName) is not null)
        {
            return false;
        }
        return Processes.Add(processName.Trim());
    }

    public bool Remove(string processName)
    {
        var match = Processes.FirstOrDefault(p =>
            StripExe(p.Trim()).Equals(StripExe(processName.Trim()), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }
        return Processes.Remove(match);
    }

    public bool IsExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }
        var normalized = StripExe(processName.Trim());
        return Processes.Any(e => StripExe(e.Trim()).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    // Null when the name is usable: a bare process name, never a path.
    public static string? ValidateProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return "Type a process name, for example notepad.exe.";
        }
        var trimmed = processName.Trim();
        if (trimmed.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
        {
            return "Use just the process name (notepad.exe), not a path.";
        }
        if (trimmed.Length > 260)
        {
            return "That process name is too long.";
        }
        return null;
    }

    internal static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
}
