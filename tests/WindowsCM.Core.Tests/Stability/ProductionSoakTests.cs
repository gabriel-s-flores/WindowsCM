// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using WindowsCM.Core.Capture;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Tests.Paste;
using Xunit.Abstractions;

namespace WindowsCM.Core.Tests.Stability;

// The production composition from App.BuildServices — a file-backed SQLite
// store behind LockedHistoryStore, the incognito coordinator, capture with
// history limits, the popup model and the paste orchestrator (only the
// Win32 edges faked) — driven from five threads at once for a few seconds,
// the way the running app is: clipboard listener, UI, link previews, pipe
// commands and incognito toggles. Nothing may throw, the history must stay
// bounded and the database must come out intact.
[Collection(StabilityCollection.Name)]
public sealed class ProductionSoakTests : IDisposable
{
    private const int MaxItems = 100;
    // Keep the default suite fast; opt into a sustained local/CI stress run.
    private static readonly TimeSpan SoakDuration = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("WINDOWSCM_SOAK_SECONDS"), out var seconds)
            ? Math.Clamp(seconds, 3, 300)
            : 3);

    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-soak-" + Guid.NewGuid().ToString("N"));

    public ProductionSoakTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
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
    public async Task FullPipeline_UnderConcurrentLoad_StaysConsistentBoundedAndIntact()
    {
        var dbPath = Path.Combine(_dir, "history.db");
        var errors = new ConcurrentQueue<(string Thread, Exception Error)>();
        var counts = new ConcurrentDictionary<string, int>();
        long captures = 0;

        using (var store = new LockedHistoryStore(new SqliteHistoryStore($"Data Source={dbPath};Pooling=false")))
        using (var coordinator = new IncognitoSessionCoordinator(store, new FileImageAssetStore(Path.Combine(_dir, "images"))))
        {
            var clock = new SystemClock();
            var capture = new CaptureService(coordinator, coordinator,
                new CaptureOptions { HistoryMaxItems = MaxItems }, clock);
            var model = new PopupViewModel(coordinator);
            var pasteOptions = new PasteOptions();
            var orchestrator = new PasteOrchestrator(coordinator, capture, new FakeImages(),
                new FakeWriter(), new FakeForeground { ShouldRestore = true }, new FakeElevation(),
                new FakeInjector(), new FakeDelay(), pasteOptions, new FakePasteClock());
            using var stop = new CancellationTokenSource(SoakDuration);

            async Task Run(string name, Func<Random, Task> step)
            {
                var random = new Random(name.GetHashCode());
                await Task.Yield();
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        await step(random);
                        counts.AddOrUpdate(name, 1, (_, n) => n + 1);
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue((name, ex));
                        return;
                    }
                }
            }

            var watch = Stopwatch.StartNew();
            await Task.WhenAll(
                // Clipboard listener: every kind of payload, some huge.
                Task.Run(() => Run("listener", random =>
                {
                    capture.Capture(RandomPayload(random), "notepad.exe", DateTime.UtcNow);
                    Interlocked.Increment(ref captures);
                    return Task.CompletedTask;
                })),
                // UI: refresh/search the popup, pin, delete, and pick items
                // (auto-paste on and off, with and without a target).
                Task.Run(() => Run("ui", async random =>
                {
                    switch (random.Next(6))
                    {
                        case 0:
                            model.SetSearch(random.Next(3) == 0 ? "note" : "");
                            break;
                        case 1:
                            model.Show(incognito: random.Next(2) == 0);
                            break;
                        case 2:
                            model.SetSelectedIndex(random.Next(Math.Max(1, model.VisibleItems.Count)));
                            model.TogglePinSelected();
                            break;
                        case 3:
                            model.SetSelectedIndex(random.Next(Math.Max(1, model.VisibleItems.Count)));
                            model.DeleteSelected(force: random.Next(2) == 0);
                            break;
                        default:
                            model.Refresh();
                            if (model.VisibleItems.Count > 0)
                            {
                                var item = model.VisibleItems[random.Next(model.VisibleItems.Count)];
                                pasteOptions.AutoPaste = random.Next(2) == 0;
                                await orchestrator.ExecuteAsync(item.Id,
                                    random.Next(3) == 0 ? IntPtr.Zero : new IntPtr(123),
                                    shiftHeld: random.Next(4) == 0, () => Task.CompletedTask);
                            }
                            break;
                    }
                })),
                // Link previews finishing in the background.
                Task.Run(() => Run("link-preview", random =>
                {
                    if (coordinator.GetLatest() is { } head)
                    {
                        coordinator.SetMetadataAndTitle(head.Id, """{"link":{"title":"t"}}""", "t");
                    }
                    if (random.Next(10) == 0)
                    {
                        coordinator.GetById(random.Next(1, 500));
                    }
                    return Task.CompletedTask;
                })),
                // Pipe commands (clear keeps pins).
                Task.Run(() => Run("pipe", async random =>
                {
                    if (random.Next(50) == 0)
                    {
                        coordinator.Clear(keepProtected: true);
                    }
                    coordinator.Search("", kind: ItemKind.Link);
                    await Task.Delay(1);
                })),
                // Incognito on/off: swaps and disposes the in-memory session.
                Task.Run(() => Run("incognito", async random =>
                {
                    coordinator.SetIncognito(random.Next(2) == 0);
                    await Task.Delay(random.Next(1, 15));
                })));
            watch.Stop();

            foreach (var (thread, error) in errors)
            {
                _output.WriteLine($"[{thread}] {error}");
            }
            Assert.Empty(errors);

            _output.WriteLine($"Soak {watch.Elapsed.TotalSeconds:F1}s: " + string.Join(", ",
                counts.OrderBy(c => c.Key).Select(c => $"{c.Key}={c.Value} ops ({c.Value / watch.Elapsed.TotalSeconds:F0}/s)")));

            // Bounded: one more capture in the persistent session trims
            // everything the concurrent run left above the limit.
            coordinator.SetIncognito(false);
            capture.Capture(new ClipboardPayload(null, null, "final note " + Guid.NewGuid(), []), null, DateTime.UtcNow);
            var unpinned = store.List().Count(i => !i.Pinned && i.Tag is null);
            _output.WriteLine($"Captures: {captures}; unpinned left: {unpinned}; pinned: {store.List().Count(i => i.Pinned)}");
            Assert.True(captures > 100, $"only {captures} captures ran");
            Assert.InRange(unpinned, 1, MaxItems);
        }

        // Intact: SQLite's own consistency check on the file the app uses.
        using var check = new SqliteConnection($"Data Source={dbPath};Pooling=false");
        check.Open();
        using var command = check.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", command.ExecuteScalar() as string);
    }

    private static readonly string LargeText = string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 20_000));

    private static ClipboardPayload RandomPayload(Random random)
    {
        var n = random.Next(400);
        return random.Next(8) switch
        {
            0 => new ClipboardPayload(null, null, $"public class C{n} {{ void M() {{ return; }} }}", []),
            1 => new ClipboardPayload(null, null, $"https://example.com/page/{n}", []),
            2 => new ClipboardPayload(null, null, LargeText + n, []),
            3 => new ClipboardPayload(new ImageSnapshot("image/png", BitConverter.GetBytes(n).Concat(new byte[256]).ToArray()), null, null, []),
            4 => new ClipboardPayload(null, new FileSnapshot([$@"C:\docs\file{n}.pdf", $@"C:\docs\other{n}.txt"], FileOperation.Copy), null, []),
            5 => new ClipboardPayload(null, null, "#" + n.ToString("X6"), []),
            _ => new ClipboardPayload(null, null, $"just a note number {n}", []),
        };
    }
}
