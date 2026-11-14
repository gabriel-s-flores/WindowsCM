// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsCM.App;
using WindowsCM.Core.History;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;
using WindowsCM.Core.Settings;

// Timing scenarios for the popups (first open, wheel distance, frame
// pacing, card refreshes). Same isolation as the stress scenarios: App is
// built without its startup, the history is an in-memory database seeded
// with 60 synthetic items, and favicons are seeded in memory. The windows
// appear on screen while a scenario runs. Numbers are printed, not asserted:
// they depend on the machine.
//
//   perf firstshow-compact [warm] [no-large]
//   perf firstshow-large
//   perf card-updates
//   perf wheel
//   perf frames
internal static class Perf
{
    public static int Run(string[] args, Action heartbeat)
    {
        var scenario = args.Length > 1 ? args[1] : "firstshow-compact";
        var options = args.Skip(2).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsCM-perf-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var t0 = Stopwatch.GetTimestamp();
            using var store = new SqliteHistoryStore("Data Source=:memory:");
            var app = Program.IsolatedApp();
            typeof(App).GetMethod("UpdateLanguage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [AppLanguage.English]);
            typeof(App).GetMethod("UpdateTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [ColorScheme.Dark]);
            var settings = (AppSettings)typeof(App).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
            var tApp = Ms(t0);
            Seed(store, dir);
            var model = new PopupViewModel(store);
            var t1 = Stopwatch.GetTimestamp();
            PopupWindow? large = null;
            var tLarge = 0.0;
            if (!options.Contains("no-large") || scenario != "firstshow-compact")
            {
                large = new PopupWindow(model, app);
                tLarge = Ms(t1);
            }
            t1 = Stopwatch.GetTimestamp();
            var compact = new CompactPopupWindow(model, app);
            compact.ApplyTheme(ColorScheme.Dark);
            var tCompact = Ms(t1);
            if (typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic) is null)
                Console.WriteLine("WARNING: DispatcherOperation._method not found: the hang log cannot name dispatcher operations.");
            Console.WriteLine($"[{scenario}] App object + language + theme dictionaries: {tApp:F0} ms; PopupWindow ctor: {(large is null ? "not built" : $"{tLarge:F0} ms")}; CompactPopupWindow ctor: {tCompact:F0} ms; items: {store.List().Count}");

            var beat = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
            beat.Tick += (_, _) => heartbeat();
            beat.Start();
            app.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    switch (scenario)
                    {
                        case "firstshow-compact":
                            if (options.Contains("warm"))
                            {
                                await WarmUp(app, compact);
                            }
                            await FirstShow(app, () => compact.ShowAtCursor(false), compact.Hide, compact);
                            break;
                        case "firstshow-large": await FirstShow(app, () => large!.ShowAtCursor(false), large!.Hide, large!); break;
                        case "card-updates": await CardUpdates(app, settings, large!, compact); break;
                        case "wheel": await Wheel(app, settings, large!, compact); break;
                        case "frames": await Frames(app, settings, large!, compact); break;
                        default: throw new ArgumentException("Unknown perf scenario: " + scenario);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("FAIL: " + ex);
                    Environment.ExitCode = 1;
                }
                finally
                {
                    large?.Hide();
                    compact.Hide();
                    app.Shutdown();
                }
            });
            app.Run();
            return Environment.ExitCode;
        }
        finally
        {
            // Only the uniquely named fixture directory created above.
            try { System.IO.Directory.Delete(dir, recursive: true); } catch (System.IO.IOException) { }
        }
    }

    private static double Ms(long from) => Stopwatch.GetElapsedTime(from).TotalMilliseconds;

    private static Task Idle(Dispatcher d, DispatcherPriority p) => d.InvokeAsync(() => { }, p).Task;

    // The production warm-up, as App runs it once startup is idle.
    private static async Task WarmUp(App app, CompactPopupWindow compact)
    {
        var method = typeof(CompactPopupWindow).GetMethod("WarmUpAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("This build has no compact popup warm-up.");
        var t = Stopwatch.GetTimestamp();
        await (Task)method.Invoke(compact, null)!;
        Console.WriteLine($"  warm-up: {Ms(t):F0} ms wall clock (spread over idle dispatcher frames)");
        await Task.Delay(1500);
    }

    private static T? Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            if (Find<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static int CountRealized(ListBox list)
    {
        var n = 0;
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not null) n++;
        }
        return n;
    }

    private static string Stats(List<double> v)
    {
        if (v.Count == 0) return "no samples";
        var s = v.OrderBy(x => x).ToList();
        double P(double q) => s[(int)Math.Min(s.Count - 1, Math.Floor(q * s.Count))];
        return $"n={s.Count} p50={P(.5):F1} p95={P(.95):F1} p99={P(.99):F1} max={s[^1]:F1} ms";
    }

    private static async Task FirstShow(App app, Action show, Action hide, Window window)
    {
        var d = app.Dispatcher;
        for (var i = 1; i <= 4; i++)
        {
            var gc0 = GC.CollectionCount(0);
            var t = Stopwatch.GetTimestamp();
            show();
            var sync = Ms(t);
            await Idle(d, DispatcherPriority.Loaded);
            var rendered = Ms(t);
            await Idle(d, DispatcherPriority.ApplicationIdle);
            var idle = Ms(t);
            var list = (ListBox)window.FindName("ItemsList");
            Console.WriteLine($"  show #{i}: ShowAtCursor blocked {sync:F0} ms; first render pass done at {rendered:F0} ms; dispatcher idle at {idle:F0} ms; realized cards {CountRealized(list)}; gen0 GCs {GC.CollectionCount(0) - gc0}; process age {(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:F0} ms");
            await Task.Delay(1500);
            hide();
            await Task.Delay(500);
        }
    }

    private static void RaiseWheel(ListBox list, ScrollViewer viewer, int delta)
    {
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        list.RaiseEvent(preview);
        if (!preview.Handled)
        {
            viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.MouseWheelEvent });
        }
    }

    private static (string Name, bool Large, DialogOrientation Orientation)[] Layouts =>
    [
        ("compact vertical", false, DialogOrientation.Vertical),
        ("compact horizontal", false, DialogOrientation.Horizontal),
        ("large horizontal", true, DialogOrientation.Horizontal),
        ("large vertical", true, DialogOrientation.Vertical),
    ];

    private static async Task<(Window Window, ListBox List, ScrollViewer Viewer)> Open(App app, AppSettings settings,
        PopupWindow large, CompactPopupWindow compact, bool isLarge, DialogOrientation orientation)
    {
        if (isLarge) settings.Dialog.Orientation = orientation; else settings.Dialog.CompactOrientation = orientation;
        Window window = isLarge ? large : compact;
        if (isLarge) large.ShowAtCursor(false); else compact.ShowAtCursor(false);
        await Idle(app.Dispatcher, DispatcherPriority.ApplicationIdle);
        await Task.Delay(1500);
        var list = (ListBox)window.FindName("ItemsList");
        return (window, list, Find<ScrollViewer>(list)!);
    }

    private static async Task Wheel(App app, AppSettings settings, PopupWindow large, CompactPopupWindow compact)
    {
        foreach (var c in Layouts)
        {
            var (window, list, viewer) = await Open(app, settings, large, compact, c.Large, c.Orientation);
            double Offset() => c.Orientation == DialogOrientation.Vertical ? viewer.VerticalOffset : viewer.HorizontalOffset;
            double Extent() => c.Orientation == DialogOrientation.Vertical ? viewer.ExtentHeight : viewer.ExtentWidth;
            double Viewport() => c.Orientation == DialogOrientation.Vertical ? viewer.ViewportHeight : viewer.ViewportWidth;
            var results = new List<string>();
            foreach (var delta in new[] { -120, -120, -30, -30, -360, 120 })
            {
                var before = Offset();
                var t = Stopwatch.GetTimestamp();
                RaiseWheel(list, viewer, delta);
                list.UpdateLayout();
                var cost = Ms(t);
                var immediate = Offset() - before;
                await Task.Delay(700);
                results.Add($"delta {delta}: moved {Offset() - before:F0} px (immediately {immediate:F0} px, handler+layout {cost:F1} ms)");
            }
            Console.WriteLine($"  {c.Name}: window {window.ActualWidth:F0}x{window.ActualHeight:F0}, viewport {Viewport():F0} px, extent {Extent():F0} px, realized {CountRealized(list)}/{list.Items.Count}");
            foreach (var r in results) Console.WriteLine("    " + r);
            if (c.Large) large.Hide(); else compact.Hide();
            await Task.Delay(400);
        }
    }

    // Thumbnails decode after the cards are realized: each image card
    // scrolled into view must show its picture once the decode lands,
    // without the popup being reopened or the list rebuilt.
    private static async Task CardUpdates(App app, AppSettings settings, PopupWindow large, CompactPopupWindow compact)
    {
        var clear = typeof(App).Assembly.GetType("WindowsCM.App.ImageThumbnailCache", throwOnError: true)!.GetMethod("Clear")!;
        var total = 0;
        foreach (var c in Layouts)
        {
            var (_, list, _) = await Open(app, settings, large, compact, c.Large, c.Orientation);
            var images = list.Items.OfType<ClipboardItem>().Where(item => item.Kind == ItemKind.Image).ToList();
            var cards = 0;
            var shown = 0;
            foreach (var image in images.Take(3))
            {
                // Not decoded yet when its card is realized.
                clear.Invoke(null, null);
                list.ScrollIntoView(image);
                list.UpdateLayout();
                await Task.Delay(1500);
                if (list.ItemContainerGenerator.ContainerFromItem(image) is not ListBoxItem container) continue;
                cards++;
                if (Descendants(container).OfType<Image>().Any(i => i.IsVisible && i.Source is not null)) shown++;
            }
            Console.WriteLine($"  {c.Name}: {shown}/{cards} image cards realized before their decode show their thumbnail");
            if (cards == 0 || shown != cards) throw new InvalidOperationException("A landed thumbnail did not reach its card.");
            total += cards;
            if (c.Large) large.Hide(); else compact.Hide();
            await Task.Delay(400);
        }
        Console.WriteLine($"PASS: background thumbnails reach their cards in all four layouts ({total} cards).");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

    private static double UiThreadCpuMs()
    {
        GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user);
        return (kernel + user) / 10000.0;
    }

    // What the popup does when a thumbnail, favicon or file probe lands for
    // a card: the production handler when this build has one, else the old
    // whole-list refresh.
    private static Action CardRefresher(Window window, ListBox list)
    {
        var handler = window.GetType().GetMethod("RefreshRealizedCards", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (handler is null)
        {
            return () => list.Items.Refresh();
        }
        // One landed result per tick, for a different realized card each time.
        var index = 0;
        return () =>
        {
            var realized = Enumerable.Range(0, list.Items.Count)
                .Where(i => list.ItemContainerGenerator.ContainerFromIndex(i) is not null)
                .Select(i => (ClipboardItem)list.Items[i])
                .ToList();
            if (realized.Count == 0) return;
            var item = realized[index++ % realized.Count];
            handler.Invoke(window, [new Func<ClipboardItem, bool>(candidate => candidate.Id == item.Id)]);
        };
    }

    private static async Task Frames(App app, AppSettings settings, PopupWindow large, CompactPopupWindow compact)
    {
        {
            var calls = 0; var unique = new HashSet<TimeSpan>();
            EventHandler h = (_, e) => { calls++; unique.Add(((RenderingEventArgs)e).RenderingTime); };
            CompositionTarget.Rendering += h;
            var cpu = UiThreadCpuMs();
            await Task.Delay(2000);
            CompositionTarget.Rendering -= h;
            Console.WriteLine($"  idle 2 s with a Rendering subscriber: {calls} callbacks, {unique.Count} distinct frames (~{unique.Count / 2.0:F0} Hz), UI thread CPU {UiThreadCpuMs() - cpu:F0} ms; render tier {RenderCapability.Tier >> 16}");
        }

        foreach (var c in Layouts)
        {
            var (window, list, viewer) = await Open(app, settings, large, compact, c.Large, c.Orientation);
            var vertical = c.Orientation == DialogOrientation.Vertical;
            var refresh = CardRefresher(window, list);
            var refreshCosts = new List<double>();

            // 1. Offset changes, layout passes and UI CPU during the app's own
            // wheel handling (no Rendering subscriber of ours).
            foreach (var withRefresh in new[] { false, true })
            {
                if (vertical) viewer.ScrollToVerticalOffset(0); else viewer.ScrollToHorizontalOffset(0);
                await Task.Delay(700);
                var scrollChanges = 0; var layouts = 0;
                ScrollChangedEventHandler onScroll = (_, e) => { if ((vertical ? e.VerticalChange : e.HorizontalChange) != 0) scrollChanges++; };
                EventHandler onLayout = (_, _) => layouts++;
                viewer.ScrollChanged += onScroll;
                list.LayoutUpdated += onLayout;
                var direction = -120;
                var wheel = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(80) };
                wheel.Tick += (_, _) => RaiseWheel(list, viewer, direction);
                var refresher = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
                refresher.Tick += (_, _) =>
                {
                    var t = Stopwatch.GetTimestamp();
                    refresh();
                    list.UpdateLayout();
                    refreshCosts.Add(Ms(t));
                };
                var cpu = UiThreadCpuMs();
                var wall = Stopwatch.GetTimestamp();
                wheel.Start();
                if (withRefresh) refresher.Start();
                await Task.Delay(3000);
                direction = 120;
                await Task.Delay(3000);
                wheel.Stop(); refresher.Stop();
                var elapsed = Ms(wall);
                var cpuUsed = UiThreadCpuMs() - cpu;
                viewer.ScrollChanged -= onScroll;
                list.LayoutUpdated -= onLayout;
                Console.WriteLine($"  {c.Name}, wheel every 80 ms for 6 s ({(withRefresh ? "card refresh every 250 ms" : "no refreshes")}): {scrollChanges * 1000.0 / elapsed:F0} offset changes/s, {layouts * 1000.0 / elapsed:F0} layout passes/s, UI thread CPU {100 * cpuUsed / elapsed:F0}%");
            }

            // 2. Presented-frame pacing (distinct RenderingTime values).
            foreach (var withRefresh in new[] { false, true })
            {
                if (vertical) viewer.ScrollToVerticalOffset(0); else viewer.ScrollToHorizontalOffset(0);
                await Task.Delay(700);
                var last = TimeSpan.MinValue; var stamps = new List<long>();
                EventHandler h = (_, e) =>
                {
                    var time = ((RenderingEventArgs)e).RenderingTime;
                    if (time != last) { last = time; stamps.Add(Stopwatch.GetTimestamp()); }
                };
                CompositionTarget.Rendering += h;
                var direction = -120;
                var wheel = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(80) };
                wheel.Tick += (_, _) => RaiseWheel(list, viewer, direction);
                var refresher = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
                refresher.Tick += (_, _) => refresh();
                wheel.Start();
                if (withRefresh) refresher.Start();
                await Task.Delay(3000);
                direction = 120;
                await Task.Delay(3000);
                wheel.Stop(); refresher.Stop();
                CompositionTarget.Rendering -= h;
                var intervals = new List<double>();
                for (var i = 1; i < stamps.Count; i++) intervals.Add((stamps[i] - stamps[i - 1]) * 1000.0 / Stopwatch.Frequency);
                var median = intervals.OrderBy(x => x).ElementAt(intervals.Count / 2);
                Console.WriteLine($"  {c.Name}, frame pacing ({(withRefresh ? "card refresh every 250 ms" : "no refreshes")}): {stamps.Count} frames; interval {Stats(intervals)}; over 1.5x median: {intervals.Count(x => x > median * 1.5)}");
            }
            Console.WriteLine($"  {c.Name}, one card refresh + layout: {Stats(refreshCosts)}");
            if (c.Large) large.Hide(); else compact.Hide();
            await Task.Delay(400);
        }
    }

    private static void Seed(SqliteHistoryStore store, string dir)
    {
        var random = new Random(7);
        var images = new List<string>();
        for (var n = 0; n < 6; n++)
        {
            const int w = 1920, h = 1080;
            var pixels = new byte[w * h * 3];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var o = (y * w + x) * 3;
                    pixels[o] = (byte)((x + n * 40) & 0xFF); pixels[o + 1] = (byte)((y * 2) & 0xFF); pixels[o + 2] = (byte)((x ^ y) & 0xFF);
                }
            }
            var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, pixels, w * 3);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = System.IO.Path.Combine(dir, $"shot{n}.png");
            using (var file = System.IO.File.Create(path)) encoder.Save(file);
            images.Add(path);
        }
        // Seeded in memory: the harness never downloads favicons.
        var icon = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgr24, null, new byte[64 * 64 * 3], 64 * 3);
        icon.Freeze();
        var favicons = typeof(FaviconService).GetField("MemoryCache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        favicons.GetType().GetMethod("Set")!.Invoke(favicons, ["example.invalid", icon]);

        var files = new List<string>();
        foreach (var name in new[] { "notes.txt", "report.pdf", "archive.zip", "letter.docx", "song.mp3", "data.csv" })
        {
            var path = System.IO.Path.Combine(dir, name);
            System.IO.File.WriteAllBytes(path, new byte[4096]);
            files.Add(path);
        }
        string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(_ => "lorem ipsum dolor sit amet consectetur"[..random.Next(4, 30)]));
        string Html(int kb) => "{\"html\":\"" + new string('h', kb * 1024) + "\"}";
        var code = "using System;\nnamespace Demo;\npublic sealed class Sample\n{\n    private readonly int _value = 42; // answer\n    public string Describe(string name) => $\"{name}: {_value}\";\n    public static void Main()\n    {\n        Console.WriteLine(new Sample().Describe(\"x\"));\n    }\n}\n";
        var now = DateTime.UtcNow.AddHours(-2);
        var id = 0;
        void Add(ItemKind kind, string content, string? metadata = null, bool pinned = false) =>
            store.AddOrUpdate(new ClipboardItem(kind, content, pinned, null, now.AddSeconds(id++ * 37), metadata, null));
        for (var i = 0; i < 60; i++)
        {
            switch (i % 10)
            {
                case 0: Add(ItemKind.Text, $"short note {i}"); break;
                case 1: Add(ItemKind.Text, $"{i} " + string.Join("\n", Enumerable.Range(0, 12).Select(_ => Words(14))), Html(200)); break;
                case 2: Add(ItemKind.Code, $"// snippet {i}\n" + code, "{\"html\":\"" + new string('c', 300 * 1024) + "\",\"language\":{\"id\":\"csharp\",\"name\":\"C#\"}}"); break;
                case 3: Add(ItemKind.Image, images[(i / 10) % images.Count]); break;
                case 4: Add(ItemKind.Link, $"https://example.invalid/page/{i}", ItemMetadataJson.EncodeLink($"Example page {i}", "A description of the page that wraps over two lines of the card.", i % 20 == 4 ? images[0] : null)); break;
                case 5: Add(ItemKind.File, files[(i / 10) % files.Count]); break;
                case 6: Add(ItemKind.Text, $"{i} " + Words(60), null, pinned: i == 6); break;
                case 7: Add(ItemKind.Character, new[] { "😀", "🚀", "👩‍👩‍👧‍👦", "🎉🎉🎉", "é", "→" }[(i / 10) % 6]); break;
                case 8: Add(ItemKind.Color, $"#ff{i:x2}aa"); break;
                default: Add(ItemKind.Text, $"{i} " + new string('x', 200_000)); break;
            }
        }
    }
}
