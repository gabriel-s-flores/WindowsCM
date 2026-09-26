// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Tests.History;

// Dates were written with the current culture: a '.' time separator
// (fi-FI, da-DK, or a custom Windows setting) made every row unreadable,
// and another calendar (th-TH, fa-IR) stored years centuries off. The
// reader always parsed them invariantly.
public sealed class HistoryDateCultureTests : IDisposable
{
    private static readonly DateTime When = new(2026, 9, 26, 10, 30, 0, DateTimeKind.Utc);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-dates-" + Guid.NewGuid().ToString("N"));
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public HistoryDateCultureTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static ClipboardItem Item(string content, DateTime at) =>
        new(ItemKind.Text, content, false, null, at, null, null);

    [Theory]
    [InlineData("fi-FI")]
    [InlineData("da-DK")]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void AnyCulture_WritesDatesItCanReadBack(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        using var store = new SqliteHistoryStore("Data Source=:memory:");

        store.AddOrUpdate(Item("first", When));
        store.AddOrUpdate(Item("second", When.AddMinutes(5)));

        Assert.Equal(["second", "first"], store.List().Select(i => i.Content));
        Assert.Equal(When, store.List()[1].CapturedAt);
        Assert.Equal(1, store.Evict(1, 0, When.AddHours(1)));
    }

    // Rows written by an earlier version under such a culture come back,
    // read the way the culture that wrote them meant them.
    [Theory]
    [InlineData("fi-FI")]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    public void LegacyRowsWrittenUnderTheCulture_AreRestoredOnOpen(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        var path = Path.Combine(_dir, "clipboard.db");
        var legacy = When.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.CurrentCulture);
        using (var store = new SqliteHistoryStore(HistoryStoreOpener.ConnectionStringFor(path)))
        {
            store.AddOrUpdate(Item("old copy", When));
            store.ExecuteForTests($"UPDATE clipboard SET datetime = '{legacy}'");
        }

        using var reopened = new SqliteHistoryStore(HistoryStoreOpener.ConnectionStringFor(path));

        var item = Assert.Single(reopened.List());
        Assert.Equal(When, item.CapturedAt);
    }
}
