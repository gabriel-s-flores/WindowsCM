// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tests.Release;

// Locates repo-level artifacts (installer script, LICENSE, csproj) by
// walking up from the test assembly to the solution root. Fails loudly
// when the layout changes instead of silently passing.
internal static class RepoFiles
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WindowsCM.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate the repo root (no WindowsCM.sln found above "
            + AppContext.BaseDirectory + ").");
    }

    public static string Read(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException("Expected repo artifact is missing: " + relativePath);
        }
        return File.ReadAllText(full);
    }
}
