// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// History screen (ticket 16): SQLite only, .db picker, ranges, three
// end-of-session modes.
public sealed class HistorySettingsTests
{
    [Fact]
    public void Ui_OffersSqliteOnly_MemoryIsDebugOnly()
    {
        Assert.Equal(["sqlite"], HistoryBackends.AvailableForUi);
        Assert.Equal("sqlite", HistoryBackends.Sqlite);
        Assert.Equal("memory", HistoryBackends.DebugMemory);
        Assert.DoesNotContain(HistoryBackends.DebugMemory, HistoryBackends.AvailableForUi);
    }

    [Fact]
    public void Defaults_Length100_AgeUnlimited_KeepProtected()
    {
        var history = new HistorySettings();

        Assert.Equal("", history.DatabaseLocation);
        Assert.Equal(100, history.MaxItems);
        Assert.Equal(0, history.MaxAgeMinutes);
        Assert.Equal(EndOfSessionMode.KeepPinnedAndTagged, history.EndOfSession);
    }

    [Fact]
    public void Ranges_HistoryLimitCapsAt100()
    {
        Assert.Equal(10, SettingLimits.HistoryLengthMin);
        Assert.Equal(100, SettingLimits.HistoryLengthMax);
        Assert.Equal(100, SettingLimits.HistoryLengthDefault);
        Assert.Equal(0, SettingLimits.HistoryTimeMin);
        Assert.Equal(1440, SettingLimits.HistoryTimeMax);
    }

    [Fact]
    public void Clamp_PinsOutOfRangeToEdges()
    {
        var history = new HistorySettings { MaxItems = 5000, MaxAgeMinutes = -5 };
        history.Clamp();

        Assert.Equal(100, history.MaxItems);
        Assert.Equal(0, history.MaxAgeMinutes);
    }

    [Fact]
    public void ThreeEndOfSessionModes_Exist()
    {
        var modes = Enum.GetValues<EndOfSessionMode>();

        Assert.Equal(3, modes.Length);
        Assert.Contains(EndOfSessionMode.Clear, modes);
        Assert.Contains(EndOfSessionMode.KeepPinnedAndTagged, modes);
        Assert.Contains(EndOfSessionMode.KeepAll, modes);
    }

    [Fact]
    public void ResolveDatabasePath_EmptyUsesLocalAppDataDefault()
    {
        var history = new HistorySettings();

        Assert.Equal(DatabasePaths.Default(), history.ResolveDatabasePath());
    }

    [Fact]
    public void ValidateDatabasePath_RejectsNonDb()
    {
        var history = new HistorySettings();

        Assert.NotNull(history.ValidateDatabasePath("C:\\tmp\\clipboard.json"));
    }

    [Fact]
    public void SessionCleanup_KeepAll_IsNoop()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        store.AddOrUpdate(new ClipboardItem(
            WindowsCM.Core.History.ItemKind.Text, "hello", false, null, DateTime.UtcNow, null, null));

        Assert.False(SessionCleanup.ShouldClear(EndOfSessionMode.KeepAll));
        Assert.Equal(0, SessionCleanup.Apply(EndOfSessionMode.KeepAll, store));
        Assert.Single(store.List());
    }

    [Fact]
    public void SessionCleanup_Clear_RemovesEverything()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        store.AddOrUpdate(new ClipboardItem(
            WindowsCM.Core.History.ItemKind.Text, "pinned", true, null, DateTime.UtcNow, null, null));

        Assert.True(SessionCleanup.ShouldClear(EndOfSessionMode.Clear));
        Assert.Equal(1, SessionCleanup.Apply(EndOfSessionMode.Clear, store));
        Assert.Empty(store.List());
    }

    [Fact]
    public void SessionCleanup_KeepProtected_SparesPinsAndTags()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        store.AddOrUpdate(new ClipboardItem(
            WindowsCM.Core.History.ItemKind.Text, "plain", false, null, DateTime.UtcNow, null, null));
        store.AddOrUpdate(new ClipboardItem(
            WindowsCM.Core.History.ItemKind.Text, "pinned", true, null, DateTime.UtcNow, null, null));
        store.AddOrUpdate(new ClipboardItem(
            WindowsCM.Core.History.ItemKind.Text, "tagged", false, "#3584e4", DateTime.UtcNow, null, null));

        var removed = SessionCleanup.Apply(EndOfSessionMode.KeepPinnedAndTagged, store);

        Assert.Equal(1, removed);
        Assert.Equal(2, store.List().Count);
    }
}
