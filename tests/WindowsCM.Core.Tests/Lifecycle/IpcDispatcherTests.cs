// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class IpcDispatcherTests
{
    private sealed class FakePopup : ITrayPopup
    {
        public bool IsVisible { get; private set; }
        public List<bool> Shows = [];
        public int Hides;
        public int Toggles;
        public void Show(bool incognito)
        {
            Shows.Add(incognito);
            IsVisible = true;
        }
        public void Hide()
        {
            Hides++;
            IsVisible = false;
        }
        public void Toggle()
        {
            Toggles++;
            IsVisible = !IsVisible;
        }
    }

    private readonly FakePopup _popup = new();

    private static void Seed(SqliteHistoryStore store)
    {
        var now = DateTime.UtcNow;
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "plain", false, null, now, null, null));
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "pinned", true, null, now, null, null));
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "tagged", false, "#3584e4", now, null, null));
    }

    [Fact]
    public void Toggle_TogglesPopup()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("ok", dispatcher.Handle("toggle"));

        Assert.Equal(1, _popup.Toggles);
    }

    [Fact]
    public void Show_ShowsNonIncognito()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("ok", dispatcher.Handle("SHOW"));

        Assert.Equal([false], _popup.Shows);
    }

    [Fact]
    public void Hide_Hides()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var dispatcher = new IpcDispatcher(_popup, store);
        dispatcher.Handle("show");

        Assert.Equal("ok", dispatcher.Handle("hide"));

        Assert.Equal(1, _popup.Hides);
        Assert.False(_popup.IsVisible);
    }

    [Fact]
    public void Clear_KeepsProtected()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("ok", dispatcher.Handle("clear"));

        Assert.Equal(2, store.List().Count);
    }

    [Fact]
    public void ClearAll_WipesEverything()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("ok", dispatcher.Handle("clear-all"));

        Assert.Empty(store.List());
    }

    [Fact]
    public void Ping_AnswersOkWithoutSideEffects()
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("ok", dispatcher.Handle("ping"));

        Assert.Empty(_popup.Shows);
        Assert.Equal(0, _popup.Toggles);
        Assert.Equal(3, store.List().Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("copy 12")]
    public void Unknown_AnswersUnknownWithoutSideEffects(string? line)
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        Seed(store);
        var dispatcher = new IpcDispatcher(_popup, store);

        Assert.Equal("unknown", dispatcher.Handle(line));

        Assert.Empty(_popup.Shows);
        Assert.Equal(0, _popup.Toggles);
        Assert.Equal(0, _popup.Hides);
        Assert.Equal(3, store.List().Count);
    }
}
