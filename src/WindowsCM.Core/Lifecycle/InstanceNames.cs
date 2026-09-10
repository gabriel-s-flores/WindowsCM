// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// Per-user instance names (research 02 single-instance, 05 §10). The mutex
// lives in the session namespace (Local\) qualified by the user SID so two
// logged-in users never share one; the pipe name carries the same suffix
// without the namespace (pipe names cannot contain backslashes). .NET 8 has
// no MutexSecurity, so isolation is Local\ + SID for the mutex and
// PipeOptions.CurrentUserOnly for the pipe (same user and elevation).
public static class InstanceNames
{
    public const string MutexPrefix = @"Local\WindowsCM.";
    public const string PipePrefix = "WindowsCM.";

    public static string BuildMutexName(string userSid)
    {
        ValidateSid(userSid);
        return MutexPrefix + userSid;
    }

    public static string BuildPipeName(string userSid)
    {
        ValidateSid(userSid);
        return PipePrefix + userSid;
    }

    // Unique pipe suffix for tests: real servers run with one instance per
    // name, so every pipe test mints its own name and never collides.
    public static string BuildTestPipeName(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            throw new ArgumentException("Test pipe suffix must not be empty.", nameof(suffix));
        }
        return PipePrefix + "test-" + suffix;
    }

    private static void ValidateSid(string userSid)
    {
        if (string.IsNullOrWhiteSpace(userSid))
        {
            throw new ArgumentException("User SID must not be empty.", nameof(userSid));
        }
        if (userSid.IndexOfAny(['\\', '/', ' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("User SID must not contain separators or whitespace.", nameof(userSid));
        }
    }
}
