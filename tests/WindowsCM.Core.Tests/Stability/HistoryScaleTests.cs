// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using WindowsCM.Core.Capture;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using Xunit.Abstractions;

namespace WindowsCM.Core.Tests.Stability;

// Hot paths against a heavy history: pinned items are never evicted, so a
// long-time user can hold thousands of them (some multi-megabyte). Budgets
// are ~10x what a CI machine needs — they catch complexity regressions
// (a full-history scan sneaking back into a per-copy path), not jitter.
// Measured numbers are written to the test output.
[Collection(StabilityCollection.Name)]
public sealed class HistoryScaleTests : IDisposable
{
    private const int Pinned = 2000;
    private const int MaxItems = 100;

    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-scale-" + Guid.NewGuid().ToString("N"));
    private readonly LockedHistoryStore _store;
    private readonly List<long> _ids = [];

    public HistoryScaleTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
        _store = new LockedHistoryStore(new SqliteHistoryStore(
            $"Data Source={Path.Combine(_dir, "history.db")};Pooling=false"));
        var huge = new string('x', 1_000_000);
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < Pinned; i++)
        {
            var content = i % 100 == 0 ? huge + i : $"pinned snippet {i} " + new string('y', 1500);
            var saved = _store.AddOrUpdate(new ClipboardItem(ItemKind.Text, content, true, null,
                t0.AddSeconds(i), """{"language":{"id":"csharp","name":"C#"}}""", null));
            _ids.Add(saved.Id);
        }
        for (var i = 0; i < MaxItems; i++)
        {
            var saved = _store.AddOrUpdate(new ClipboardItem(ItemKind.Text, $"recent note {i}", false, null,
                t0.AddDays(1).AddSeconds(i), null, null));
            _ids.Add(saved.Id);
        }
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void PerCopyAndPerPastePaths_DoNotScaleWithHistorySize()
    {
        var random = new Random(7);

        var latest = Average(200, () => _store.GetLatest());
        var byId = Average(200, () => _store.GetById(_ids[random.Next(_ids.Count)]));

        _output.WriteLine($"{Pinned + MaxItems} items (20 x 1 MB): GetLatest {latest:F3} ms, GetById {byId:F3} ms");
        Assert.True(latest < 25, $"GetLatest took {latest:F3} ms");
        Assert.True(byId < 25, $"GetById took {byId:F3} ms");
    }

    [Fact]
    public void CaptureWithEviction_StaysFastAndBounded()
    {
        var capture = new CaptureService(_store, new FileImageAssetStore(Path.Combine(_dir, "images")),
            new CaptureOptions { HistoryMaxItems = MaxItems }, new SystemClock());
        var n = 0;

        var perCapture = Average(500, () => capture.Capture(
            new ClipboardPayload(null, null, $"fresh copy number {n++}", []), null, DateTime.UtcNow));

        var unpinned = _store.List().Count(i => !i.Pinned);
        _output.WriteLine($"Capture + evict over {Pinned} pinned: {perCapture:F3} ms per copy; unpinned kept {unpinned}");
        Assert.True(perCapture < 50, $"capture took {perCapture:F3} ms");
        Assert.Equal(MaxItems, unpinned);
    }

    [Fact]
    public void PopupOpenAndSearch_OverAHeavyHistory_StayWithinBudget()
    {
        var model = new PopupViewModel(_store);

        var open = Average(10, () => model.Show(incognito: false));
        var rows = model.VisibleItems.Count;
        var search = Average(10, () => model.SetSearch("snippet 19"));
        var previews = Average(3, () =>
        {
            foreach (var item in model.VisibleItems.Take(40))
            {
                ItemDisplayFormatter.GetTitle(item);
                ItemDisplayFormatter.GetPreviewText(item, 8);
                ItemDisplayFormatter.GetTypeLabel(item);
            }
        });

        _output.WriteLine($"Popup open ({rows} rows): {open:F1} ms; search ({model.VisibleItems.Count} hits): {search:F1} ms; 40 card previews: {previews:F2} ms");
        Assert.Equal(Pinned + MaxItems, rows);
        Assert.True(open < 3000, $"open took {open:F1} ms");
        Assert.True(search < 3000, $"search took {search:F1} ms");
        Assert.True(previews < 200, $"previews took {previews:F2} ms");
    }

    private static double Average(int runs, Action action)
    {
        action(); // warm-up (JIT, SQLite page cache)
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < runs; i++)
        {
            action();
        }
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / runs;
    }
}
