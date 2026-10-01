// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Release;

public static class UpdateApplyScript
{
    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    public static string PortableOperation(string executable, string staged, string suffix)
    {
        // Windows File.Replace atomically swaps files on the same volume.
        // A failed copy into the sibling staging file cannot damage the running version.
        var sibling = executable + ".update-" + suffix;
        var backup = executable + ".backup-" + suffix;
        return $"$ErrorActionPreference = 'Stop'\ntry {{\nCopy-Item -LiteralPath {Quote(staged)} -Destination {Quote(sibling)}\n[System.IO.File]::Replace({Quote(sibling)}, {Quote(executable)}, {Quote(backup)})\n}} finally {{\nRemove-Item -LiteralPath {Quote(sibling)} -Force -ErrorAction SilentlyContinue\n}}\nRemove-Item -LiteralPath {Quote(backup)} -Force -ErrorAction SilentlyContinue";
    }
}
