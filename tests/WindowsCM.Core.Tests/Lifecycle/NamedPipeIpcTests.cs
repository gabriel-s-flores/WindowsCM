// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class NamedPipeIpcTests
{
    private sealed class FakePopup : ITrayPopup
    {
        public bool IsVisible { get; private set; }
        public int Toggles;
        public List<bool> Shows = [];
        public int Hides;
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

    private static string UniquePipe() =>
        InstanceNames.BuildTestPipeName(Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ToggleRoundTrip_ReachesPopup()
    {
        var pipe = UniquePipe();
        var popup = new FakePopup();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        using var server = new NamedPipeServer(pipe, new IpcDispatcher(popup, store));
        server.Start();
        await Task.Delay(300);

        var ok = new NamedPipeForwarder().TryForward(
            pipe, "toggle", TimeSpan.FromSeconds(5), out var response);

        Assert.True(ok);
        Assert.Equal("ok", response);
        Assert.Equal(1, popup.Toggles);
    }

    [Fact]
    public async Task ClearRoundTrip_KeepsProtected()
    {
        var pipe = UniquePipe();
        var popup = new FakePopup();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var now = DateTime.UtcNow;
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "plain", false, null, now, null, null));
        store.AddOrUpdate(new ClipboardItem(ItemKind.Text, "pinned", true, null, now, null, null));
        using var server = new NamedPipeServer(pipe, new IpcDispatcher(popup, store));
        server.Start();
        await Task.Delay(300);

        var ok = new NamedPipeForwarder().TryForward(
            pipe, "CLEAR", TimeSpan.FromSeconds(5), out var response);

        Assert.True(ok);
        Assert.Equal("ok", response);
        Assert.Single(store.List());
    }

    [Fact]
    public async Task Unknown_AnswersUnknownAndKeepsServing()
    {
        var pipe = UniquePipe();
        var popup = new FakePopup();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        using var server = new NamedPipeServer(pipe, new IpcDispatcher(popup, store));
        server.Start();
        await Task.Delay(300);
        var forwarder = new NamedPipeForwarder();

        Assert.True(forwarder.TryForward(pipe, "frobnicate", TimeSpan.FromSeconds(5), out var first));
        Assert.Equal("unknown", first);
        Assert.Equal(0, popup.Toggles);

        Assert.True(forwarder.TryForward(pipe, "ping", TimeSpan.FromSeconds(5), out var second));
        Assert.Equal("ok", second);
    }

    [Fact]
    public void Forward_AbsentServer_ReturnsFalse()
    {
        var ok = new NamedPipeForwarder().TryForward(
            UniquePipe(), "ping", TimeSpan.FromMilliseconds(300), out var response);

        Assert.False(ok);
        Assert.Null(response);
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var popup = new FakePopup();
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var server = new NamedPipeServer(UniquePipe(), new IpcDispatcher(popup, store));
        server.Dispose();

        Assert.Throws<ObjectDisposedException>(() => server.Start());
    }

    [Fact]
    public void Mutex_TwoLocks_SingleOwner()
    {
        var name = @"Local\WindowsCM.test-" + Guid.NewGuid().ToString("N");
        using var first = new MutexSingleInstanceLock(name);
        using var second = new MutexSingleInstanceLock(name);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void Mutex_ReleaseLetsNextAcquire()
    {
        var name = @"Local\WindowsCM.test-" + Guid.NewGuid().ToString("N");
        var first = new MutexSingleInstanceLock(name);
        Assert.True(first.TryAcquire());
        first.Release();
        first.Dispose();

        using var second = new MutexSingleInstanceLock(name);

        Assert.True(second.TryAcquire());
    }
}
