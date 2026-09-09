// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Settings;

// History screen (Copyous History parity, grilling 06 Q10): SQLite only in
// the UI combo (Memory is debug-only, JSON dropped per research 04 §5),
// a .db file picker, length/age ranges, and the three end-of-session modes.
// `database-backend` is therefore not persisted: the backend is always
// SQLite, and the location setting resolves via DatabasePaths.
public enum EndOfSessionMode
{
    // Copyous `clear`: wipe everything on restart/logout/shutdown.
    Clear = 0,
    // Copyous `keep-pinned-and-tagged` (default): protected items survive.
    KeepPinnedAndTagged = 1,
    // Copyous `keep-all`: session-end cleanup does nothing.
    KeepAll = 2,
}

public static class HistoryBackends
{
    public const string Sqlite = "sqlite";

    // Debug-only: never offered in the History combo, only honored by
    // automation/tests spinning an in-memory store.
    public const string DebugMemory = "memory";

    public static IReadOnlyList<string> AvailableForUi { get; } = [Sqlite];
}

public sealed class HistorySettings
{
    // Empty means the LocalAppData default (DatabasePaths.Default).
    // The file picker filters to *.db; Validate reports problems shown,
    // never thrown.
    public string DatabaseLocation { get; set; } = "";

    public int MaxItems { get; set; } = SettingLimits.HistoryLengthDefault;

    public int MaxAgeMinutes { get; set; } = SettingLimits.HistoryTimeDefault;

    public EndOfSessionMode EndOfSession { get; set; } = EndOfSessionMode.KeepPinnedAndTagged;

    public void Clamp()
    {
        MaxItems = SettingLimits.ClampInt(MaxItems, SettingLimits.HistoryLengthMin, SettingLimits.HistoryLengthMax);
        MaxAgeMinutes = SettingLimits.ClampInt(MaxAgeMinutes, SettingLimits.HistoryTimeMin, SettingLimits.HistoryTimeMax);
    }

    public string ResolveDatabasePath() =>
        DatabasePaths.Resolve(string.IsNullOrWhiteSpace(DatabaseLocation) ? null : DatabaseLocation.Trim());

    public string? ValidateDatabasePath(string path) => DatabasePaths.Validate(path);
}

// Session-end cleanup (<1s, never cancels logout per spec Janitor): maps
// the three modes onto the store. KeepAll is a no-op returning 0.
public static class SessionCleanup
{
    public static bool ShouldClear(EndOfSessionMode mode) => mode != EndOfSessionMode.KeepAll;

    public static int Apply(EndOfSessionMode mode, IHistoryStore store)
    {
        if (mode == EndOfSessionMode.KeepAll)
        {
            return 0;
        }
        return store.Clear(keepProtected: mode == EndOfSessionMode.KeepPinnedAndTagged);
    }
}
