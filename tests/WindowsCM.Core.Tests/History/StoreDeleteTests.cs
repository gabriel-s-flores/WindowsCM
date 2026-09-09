// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.History;

public sealed class StoreDeleteTests : IDisposable
{
    private readonly SqliteHistoryStore _store = new("Data Source=:memory:");
    private static readonly DateTime T0 = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _store.Dispose();

    [Fact]
    public void Delete_RemovesOneRow()
    {
        var saved = _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Text, "gone", false, null, T0, null, null));

        Assert.True(_store.Delete(saved.Id));
        Assert.Empty(_store.List());
    }

    [Fact]
    public void Delete_MissingId_ReturnsFalse()
    {
        Assert.False(_store.Delete(9999));
    }

    [Fact]
    public void Delete_KeepsSiblings_DateOrderIntact()
    {
        var a = _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Text, "a", false, null, T0, null, null));
        var b = _store.AddOrUpdate(new ClipboardItem(
            ItemKind.Text, "b", false, null, T0.AddMinutes(1), null, null));

        Assert.True(_store.Delete(a.Id));

        Assert.Equal([b.Id], _store.List().Select(i => i.Id));
    }
}
