// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.History;

// Opening the history must never keep the app from starting: a damaged or
// unreachable database used to throw out of startup on every launch, with
// no tray icon and no message.
public sealed class HistoryStoreOpenerTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 26, 14, 30, 5, DateTimeKind.Utc);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-opener-" + Guid.NewGuid().ToString("N"));

    public HistoryStoreOpenerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static ClipboardItem Item(string content) =>
        new(ItemKind.Text, content, false, null, Now, null, null);

    [Fact]
    public void HealthyDatabase_OpensInPlaceAndKeepsItsItems()
    {
        var path = Path.Combine(_dir, "clipboard.db");
        using (var first = HistoryStoreOpener.Open(path, path, Now).Store)
        {
            first.AddOrUpdate(Item("kept"));
        }

        var result = HistoryStoreOpener.Open(path, path, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.Opened, result.Outcome);
        Assert.Null(result.Error);
        Assert.Equal(["kept"], store.List().Select(i => i.Content));
    }

    [Fact]
    public void DamagedFile_IsMovedAsideWithItsSidecarsAndReplaced()
    {
        var path = Path.Combine(_dir, "clipboard.db");
        File.WriteAllText(path, "this is not an SQLite database, just some bytes long enough to have a header");
        File.WriteAllText(path + "-wal", "stale wal");
        File.WriteAllText(path + "-shm", "stale shm");

        var result = HistoryStoreOpener.Open(path, path, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.RecoveredDamaged, result.Outcome);
        Assert.NotNull(result.Error);
        var backup = Path.Combine(_dir, result.Detail!);
        Assert.StartsWith("clipboard.db.corrupt-", result.Detail);
        Assert.StartsWith("this is not an SQLite database", File.ReadAllText(backup));
        // SQLite drops the invalid sidecars itself when the failed connection
        // closes; any it leaves move with the file. None may be replayed
        // into the fresh database.
        foreach (var sidecar in new[] { path + "-wal", path + "-shm" })
        {
            Assert.False(File.Exists(sidecar) && File.ReadAllText(sidecar).StartsWith("stale"));
        }
        store.AddOrUpdate(Item("fresh"));
        Assert.Equal(["fresh"], store.List().Select(i => i.Content));
    }

    [Fact]
    public void UnreachableCustomLocation_FallsBackToTheDefault()
    {
        // The parent "directory" is a file: like a drive that is not mounted,
        // the folder can never be created.
        var blocker = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocker, "");
        var custom = Path.Combine(blocker, "history.db");
        var fallback = Path.Combine(_dir, "default", "clipboard.db");

        var result = HistoryStoreOpener.Open(custom, fallback, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.FellBackToDefault, result.Outcome);
        Assert.Equal(custom, result.Detail);
        Assert.True(File.Exists(fallback));
        store.AddOrUpdate(Item("x"));
        Assert.Single(store.List());
    }

    [Fact]
    public void NothingOpens_RunsInMemoryForTheSession()
    {
        var blocker = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocker, "");
        var custom = Path.Combine(blocker, "a", "history.db");
        var fallback = Path.Combine(blocker, "b", "clipboard.db");

        var result = HistoryStoreOpener.Open(custom, fallback, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.MemoryOnly, result.Outcome);
        Assert.NotNull(result.Error);
        store.AddOrUpdate(Item("x"));
        Assert.Single(store.List());
    }

    [Fact]
    public void SemicolonInPath_IsNotReadAsAConnectionStringSeparator()
    {
        var path = Path.Combine(_dir, "my;history", "clipboard.db");

        var result = HistoryStoreOpener.Open(path, path, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.Opened, result.Outcome);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DamagedDefaultWhileCustomIsUnreachable_RecoversTheDefault()
    {
        var blocker = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocker, "");
        var custom = Path.Combine(blocker, "history.db");
        var fallback = Path.Combine(_dir, "clipboard.db");
        File.WriteAllText(fallback, "garbage garbage garbage garbage garbage garbage garbage garbage");

        var result = HistoryStoreOpener.Open(custom, fallback, Now);
        using var store = result.Store;

        Assert.Equal(HistoryOpenOutcome.FellBackToDefault, result.Outcome);
        store.AddOrUpdate(Item("x"));
        Assert.Single(store.List());
        Assert.Single(Directory.GetFiles(_dir, "clipboard.db.corrupt-*"));
    }
}
