// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Classification;

// Copy vs cut for file items (Copyous `FileOperation`).
public enum FileOperation
{
    Copy,
    Cut,
}

// A file payload: paths plus the operation that produced them.
public sealed record FileSnapshot(IReadOnlyList<string> Paths, FileOperation Operation);

// Parses file payloads. Two sources: the GNOME text payload (first line
// `copy|cut`, rest paths, missing operation means copy) and the Win32
// `Preferred DropEffect` DWORD read by the monitor.
public static class FileDrop
{
    private const int DropEffectMove = 2;

    public static FileSnapshot? Parse(IReadOnlyList<string>? lines)
    {
        if (lines is null)
        {
            return null;
        }
        var cleaned = lines.Select(l => l.Trim()).Where(l => l.Length != 0).ToList();
        if (cleaned.Count == 0)
        {
            return null;
        }
        if (cleaned[0].Equals("copy", StringComparison.OrdinalIgnoreCase))
        {
            return cleaned.Count == 1 ? null : new FileSnapshot(cleaned.Skip(1).ToList(), FileOperation.Copy);
        }
        if (cleaned[0].Equals("cut", StringComparison.OrdinalIgnoreCase))
        {
            return cleaned.Count == 1 ? null : new FileSnapshot(cleaned.Skip(1).ToList(), FileOperation.Cut);
        }
        return new FileSnapshot(cleaned, FileOperation.Copy);
    }

    public static FileOperation FromDropEffect(int effect) =>
        (effect & DropEffectMove) != 0 ? FileOperation.Cut : FileOperation.Copy;
}
