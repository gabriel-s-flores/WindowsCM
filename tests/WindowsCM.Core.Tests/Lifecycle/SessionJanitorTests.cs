// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class SessionJanitorTests
{
    private sealed class FakeSessionSource : ISessionEndingSource
    {
        public event EventHandler<SessionEndingEventArgs>? SessionEnding;
        public SessionEndingEventArgs Raise()
        {
            var args = new SessionEndingEventArgs();
            SessionEnding?.Invoke(this, args);
            return args;
        }
    }

    private static void Seed(SqliteHistoryStore store)
    {
        var now = DateTime.UtcNow;
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "plain", false, null, now, null, null));
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "pinned", true, null, now, null, null));
    }

    [Fact]
    public void SessionEnding_Clear_RemovesEverything()
    {
        var source = new FakeSessionSource();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        using var janitor = new SessionJanitor(source, store, EndOfSessionMode.Clear);

        var args = source.Raise();

        Assert.Empty(store.List());
        Assert.False(args.Cancel);
    }

    [Fact]
    public void SessionEnding_KeepProtected_SparesPins()
    {
        var source = new FakeSessionSource();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        using var janitor = new SessionJanitor(source, store, EndOfSessionMode.KeepPinnedAndTagged);

        source.Raise();

        Assert.Single(store.List());
    }

    [Fact]
    public void SessionEnding_KeepAll_IsNoop()
    {
        var source = new FakeSessionSource();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        using var janitor = new SessionJanitor(source, store, EndOfSessionMode.KeepAll);

        var args = source.Raise();

        Assert.Equal(2, store.List().Count);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void SessionEnding_NeverCancelsLogout()
    {
        var source = new FakeSessionSource();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        using var janitor = new SessionJanitor(source, store, () => EndOfSessionMode.Clear);

        var args = source.Raise();

        Assert.False(args.Cancel);
    }

    [Fact]
    public void Dispose_UnsubscribesCleanly()
    {
        var source = new FakeSessionSource();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        var janitor = new SessionJanitor(source, store, EndOfSessionMode.Clear);
        janitor.Dispose();

        source.Raise();

        Assert.Equal(2, store.List().Count);
    }

    [Fact]
    public void StoreFailure_IsSwallowedNeverBlocksLogoff()
    {
        var source = new FakeSessionSource();
        var store = new SqliteHistoryStore("Data Source=:memory:");
        store.Dispose();
        using var janitor = new SessionJanitor(source, store, EndOfSessionMode.Clear);

        var args = source.Raise();

        Assert.False(args.Cancel);
    }
}
