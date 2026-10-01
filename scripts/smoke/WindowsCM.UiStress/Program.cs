// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

// Runs the production WPF windows without App.OnStartup: no clipboard
// capture, tray, hotkeys, user database, settings writes, or paste injection.
internal static class Program
{
    private static long _heartbeat = Environment.TickCount64;
    private static string _phase = "startup";

    [STAThread]
    private static int Main(string[] args)
    {
        using var watchdog = new Timer(_ =>
        {
            if (Environment.TickCount64 - Interlocked.Read(ref _heartbeat) > 5000)
            {
                Console.Error.WriteLine($"FAIL: UI blocked for over 5 seconds during {_phase}.");
                Environment.Exit(1);
            }
        }, null, 1000, 1000);
        try
        {
            var scenario = args.FirstOrDefault() ?? "cards";
            if (scenario.StartsWith("remote-", StringComparison.Ordinal)) return CheckRemote(scenario);
            if (scenario == "image-shapes") return CheckImageShapes();
            if (scenario == "workflow") return RunWorkflow();
            return RunCards(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 500);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL during {_phase}: {ex}");
            return 1;
        }
    }

    private static IValueConverter Converter(string name) =>
        (IValueConverter)Activator.CreateInstance(typeof(App).Assembly.GetType("WindowsCM.App." + name, throwOnError: true)!, nonPublic: true)!;

    private static int CheckRemote(string scenario)
    {
        _phase = scenario;
        const string path = @"\\192.0.2.1\WindowsCM-unavailable\image.png";
        var item = new ClipboardItem(scenario == "remote-link" ? ItemKind.Link : ItemKind.Image,
            scenario == "remote-link" ? "https://example.invalid/" : path, false, null, DateTime.UtcNow,
            ItemMetadataJson.EncodeLink(null, null, path), null);
        var name = scenario == "remote-link" ? "LinkPreviewImageConverter" : "ImageThumbConverter";
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++) _ = Converter(name).Convert(item, typeof(ImageSource), null!, CultureInfo.InvariantCulture);
        Console.WriteLine($"{scenario}: 20 production converter calls in {watch.Elapsed.TotalMilliseconds:F2} ms.");
        if (watch.ElapsedMilliseconds > 250) throw new InvalidOperationException("Image lookup performed blocking work on the UI thread.");
        Console.WriteLine("PASS: unavailable image paths never block card realization.");
        return 0;
    }

    private static int RunCards(int count)
    {
        if (count is < 100 or > 10000) throw new ArgumentOutOfRangeException(nameof(count));
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsCM-ui-stress-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            return RunCardsInDirectory(count, dir);
        }
        finally
        {
            // Only the uniquely named fixture directory created above.
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    private static int CheckImageShapes()
    {
        _phase = "extreme image aspect ratios";
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsCM-image-stress-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var decode = typeof(App).Assembly.GetType("WindowsCM.App.ImageThumbnailCache")!
                .GetMethod("TryDecode", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var (width, height) in new[] { (1000, 10), (10, 1000), (1, 1), (3840, 2160) })
            {
                var path = System.IO.Path.Combine(dir, $"{width}x{height}.png");
                var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, new byte[width * height * 3], width * 3);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using (var stream = System.IO.File.Create(path)) encoder.Save(stream);
                foreach (var banner in new[] { false, true })
                {
                    var image = (BitmapSource?)decode.Invoke(null, [path, banner]);
                    if (image is null) throw new InvalidOperationException("Valid image failed to decode.");
                    Console.WriteLine($"{width}x{height}, banner={banner}: {image.PixelWidth}x{image.PixelHeight}, frozen={image.IsFrozen}");
                    if (image.PixelWidth > 320 || image.PixelHeight > 180 || !image.IsFrozen)
                        throw new InvalidOperationException("Thumbnail exceeded its pixel/memory budget.");
                    if (image.PixelWidth > width || image.PixelHeight > height)
                        throw new InvalidOperationException("Thumbnail unnecessarily upscaled its source.");
                }
                // Decoded thumbnails must not hold the source file open.
                System.IO.File.Delete(path);
            }
            Console.WriteLine("PASS: extreme aspect ratios stay bounded and source files remain unlocked.");
            return 0;
        }
        finally { System.IO.Directory.Delete(dir, recursive: true); }
    }

    private static int RunCardsInDirectory(int count, string dir)
    {
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var updateLanguage = typeof(App).GetMethod("UpdateLanguage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        updateLanguage.Invoke(app, [AppLanguage.English]);
        var settings = (AppSettings)typeof(App).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var imagePath = System.IO.Path.Combine(dir, "4k.png");
        var bitmap = BitmapSource.Create(3840, 2160, 96, 96, PixelFormats.Bgr24, null, new byte[3840 * 2160 * 3], 3840 * 3);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = System.IO.File.Create(imagePath)) encoder.Save(file);
        // Seed the favicon in memory so this isolated harness never downloads
        // or writes into the user's persistent favicon cache.
        bitmap.Freeze();
        var faviconCache = typeof(FaviconService).GetField("MemoryCache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        faviconCache.GetType().GetMethod("Set")!.Invoke(faviconCache, ["example.invalid", bitmap]);
        var corruptPath = System.IO.Path.Combine(dir, "corrupt.png");
        System.IO.File.WriteAllText(corruptPath, "not an image");
        var missingPath = System.IO.Path.Combine(dir, "missing.png");
        var huge = new string('x', 2_000_000);
        var corpus = new (ItemKind Kind, string Content, string? Metadata)[]
        {
            (ItemKind.Code, "public class C {\nvar x = (#ff00aa);\necho ${value##prefix}\nC#\n/* unfinished", null),
            (ItemKind.Text, huge, null),
            (ItemKind.Text, "plain note\r\nemoji 😀 / RTL العربية / CJK 中文 / e\u0301 / \0 / \uD800", null),
            (ItemKind.Image, imagePath, null),
            (ItemKind.Image, corruptPath, null),
            (ItemKind.Image, missingPath, null),
            (ItemKind.File, missingPath, null),
            (ItemKind.Files, missingPath + "\n" + corruptPath, "{}"),
            (ItemKind.Link, "https://example.invalid/page#anchor", ItemMetadataJson.EncodeLink("title", "description", imagePath)),
            (ItemKind.Link, "https://example.invalid/", "{broken"),
            (ItemKind.Character, "👩‍👩‍👧‍👦", null),
            (ItemKind.Color, "#ff00aa", null),
        };
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            var sample = corpus[i % corpus.Length];
            store.AddOrUpdate(new ClipboardItem(sample.Kind, sample.Content, i % 7 == 0, null,
                now.AddSeconds(i), sample.Metadata, $"stress item {i}"));
        }
        // Real history deduplicates equal content; a distinct title alone is
        // not enough. Use an independent list to stress container count too.
        var items = Enumerable.Range(0, count).Select(i =>
        {
            var sample = corpus[i % corpus.Length];
            return new ClipboardItem(sample.Kind, sample.Content, i % 7 == 0, null, now.AddSeconds(i), sample.Metadata, $"stress item {i}", i + 1);
        }).ToArray();
        var model = new PopupViewModel(store);
        var large = new PopupWindow(model, app) { Width = 1200, Height = 348 };
        var compact = new CompactPopupWindow(model, app);
        var windows = new Window[] { large, compact };
        var lists = windows.Select(window => (ListBox)window.FindName("ItemsList")).ToArray();
        var samples = new List<double>();
        var opens = new List<double>();
        var phases = new List<object>();
        var process = Process.GetCurrentProcess();
        var maxPrivate = process.PrivateMemorySize64;
        var maxHandles = process.HandleCount;
        var step = 0;
        var totalSteps = count * 8;
        var currentMode = -1;
        var phaseWatch = new Stopwatch();
        Action? advance = null;
        advance = () =>
        {
            Interlocked.Exchange(ref _heartbeat, Environment.TickCount64);
            if (step == totalSteps)
            {
                phases.Add(new { mode = currentMode, milliseconds = phaseWatch.Elapsed.TotalMilliseconds });
                large.Hide();
                compact.Hide();
                if (samples.Max() > 1000) throw new InvalidOperationException("A single scroll/layout step exceeded one second.");
                Console.WriteLine(JsonSerializer.Serialize(new { result = "PASS", cards = count, steps = step,
                    scrollLayoutP50Ms = Percentile(samples, .5), scrollLayoutP95Ms = Percentile(samples, .95), scrollLayoutMaxMs = samples.Max(),
                    showLayoutP95Ms = Percentile(opens, .95), maxPrivateMB = maxPrivate / 1048576.0, maxHandles, phases }, new JsonSerializerOptions { WriteIndented = true }));
                app.Shutdown();
                return;
            }
            var mode = step / (count * 2);
            var windowIndex = mode / 2;
            var orientation = mode % 2 == 0 ? DialogOrientation.Horizontal : DialogOrientation.Vertical;
            var window = windows[windowIndex];
            var list = lists[windowIndex];
            if (mode != currentMode)
            {
                if (currentMode >= 0) phases.Add(new { mode = currentMode, milliseconds = phaseWatch.Elapsed.TotalMilliseconds });
                phaseWatch.Restart();
                currentMode = mode;
                _phase = $"{(windowIndex == 0 ? "large" : "compact")} {orientation}";
                Console.WriteLine($"Starting {_phase}: {count} cards, forward and backward scroll.");
                foreach (var other in windows) other.Hide();
                settings.Dialog.Orientation = orientation;
                settings.Dialog.CompactOrientation = orientation;
                if (windowIndex == 0)
                {
                    large.ApplyLayoutOrientation(orientation);
                    large.Width = orientation == DialogOrientation.Horizontal ? 1200 : 420;
                    large.Height = orientation == DialogOrientation.Horizontal ? 348 : 700;
                }
                else compact.ApplyLayoutOrientation(orientation);
                list.ItemsSource = items;
                var openWatch = Stopwatch.StartNew();
                window.Show();
                window.UpdateLayout();
                opens.Add(openWatch.Elapsed.TotalMilliseconds);
            }
            var index = step % (count * 2);
            if (index >= count) index = count * 2 - index - 1;
            var tick = Stopwatch.StartNew();
            list.ScrollIntoView(items[index]);
            window.UpdateLayout();
            samples.Add(tick.Elapsed.TotalMilliseconds);
            if (step % 100 == 0)
            {
                // The actual wheel handler, refresh bursts, resource replacement,
                // theme switch and repeated hide/show after a scroll.
                list.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, step % 200 == 0 ? -120 : 15) { RoutedEvent = UIElement.PreviewMouseWheelEvent });
                updateLanguage.Invoke(app, [step % 200 == 0 ? AppLanguage.Portuguese : AppLanguage.English]);
                large.ApplyTheme(step % 200 == 0 ? ColorScheme.Light : ColorScheme.Dark);
                list.Items.Refresh();
                window.Hide();
                var openWatch = Stopwatch.StartNew();
                window.Show();
                window.UpdateLayout();
                opens.Add(openWatch.Elapsed.TotalMilliseconds);
                process.Refresh();
                maxPrivate = Math.Max(maxPrivate, process.PrivateMemorySize64);
                maxHandles = Math.Max(maxHandles, process.HandleCount);
            }
            step++;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background, advance!);
        };
        app.Dispatcher.BeginInvoke(DispatcherPriority.Background, advance);
        app.Run();
        return 0;
    }

    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling((sorted.Length - 1) * percentile)];
    }

    private static int RunWorkflow()
    {
        _phase = "normal workflow and window lifecycle";
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsCM-workflow-stress-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            using var store = new SqliteHistoryStore($"Data Source={System.IO.Path.Combine(dir, "history.db")};Pooling=false");
            var now = DateTime.UtcNow;
            for (var i = 0; i < 1000; i++)
                store.AddOrUpdate(new ClipboardItem(i % 3 == 0 ? ItemKind.Code : ItemKind.Text,
                    $"var item{i} = value;\nC#\n" + new string('x', 1000), i % 7 == 0, null, now.AddSeconds(i), null, null));
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var settings = (AppSettings)typeof(App).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
            var language = typeof(App).GetMethod("UpdateLanguage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            language.Invoke(app, [AppLanguage.English]);
            var model = new PopupViewModel(store);
            var large = new PopupWindow(model, app);
            var compact = new CompactPopupWindow(model, app);
            var openTimes = new List<double>();
            long baselineMemory = 0;
            long baselineManaged = 0;
            int baselineHandles = 0;
            int cycle = 0;
            var process = Process.GetCurrentProcess();
            Action? advance = null;
            advance = () =>
            {
                Interlocked.Exchange(ref _heartbeat, Environment.TickCount64);
                if (cycle == 350)
                {
                    large.Hide();
                    compact.Hide();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    process.Refresh();
                    var growthMB = (process.PrivateMemorySize64 - baselineMemory) / 1048576.0;
                    var handleGrowth = process.HandleCount - baselineHandles;
                    Console.WriteLine(JsonSerializer.Serialize(new { cycles = cycle, showAtCursorP50Ms = Percentile(openTimes, .5),
                        showAtCursorP95Ms = Percentile(openTimes, .95), showAtCursorMaxMs = openTimes.Max(),
                        privateMemoryGrowthAfterWarmupMB = growthMB, handleGrowthAfterWarmup = handleGrowth,
                        managedMemoryMB = GC.GetTotalMemory(false) / 1048576.0,
                        managedMemoryGrowthMB = (GC.GetTotalMemory(false) - baselineManaged) / 1048576.0,
                        windows = app.Windows.Count,
                        remainingItems = store.List().Count }, new JsonSerializerOptions { WriteIndented = true }));
                    if (growthMB > 64 || handleGrowth > 100) throw new InvalidOperationException("Window lifecycle exceeded its memory/handle growth budget.");
                    if (openTimes.Max() > 1000) throw new InvalidOperationException("Opening a popup exceeded one second.");
                    if (store.List().Count >= 1000) throw new InvalidOperationException("Workflow did not exercise item deletion.");
                    Console.WriteLine("PASS: search, selection, pin/delete, empty results, reverse ordering, language/theme changes, settings and popup lifecycle.");
                    app.Shutdown();
                    return;
                }
                if (cycle == 50)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    process.Refresh();
                    baselineMemory = process.PrivateMemorySize64;
                    baselineHandles = process.HandleCount;
                    baselineManaged = GC.GetTotalMemory(false);
                }
                if (cycle % 50 == 0)
                {
                    process.Refresh();
                    Console.WriteLine($"Workflow cycle {cycle}/350; private {process.PrivateMemorySize64 / 1048576.0:F1} MB, managed {GC.GetTotalMemory(false) / 1048576.0:F1} MB.");
                }
                large.Hide();
                compact.Hide();
                settings.Dialog.Orientation = cycle % 4 < 2 ? DialogOrientation.Horizontal : DialogOrientation.Vertical;
                settings.Dialog.CompactOrientation = settings.Dialog.Orientation;
                settings.Dialog.LargeHorizontalOrder = cycle % 2 == 0 ? HorizontalItemOrder.RecentOnLeft : HorizontalItemOrder.RecentOnRight;
                settings.Dialog.LargeVerticalOrder = cycle % 2 == 0 ? VerticalItemOrder.RecentOnTop : VerticalItemOrder.RecentOnBottom;
                var window = cycle % 2 == 0 ? (Window)large : compact;
                var watch = Stopwatch.StartNew();
                if (window == large) large.ShowAtCursor(false);
                else compact.ShowAtCursor(false);
                window.UpdateLayout();
                openTimes.Add(watch.Elapsed.TotalMilliseconds);
                var list = (ListBox)window.FindName("ItemsList");
                var search = (TextBox)window.FindName("SearchBox");
                // Navigation must flush the debounced search before selection.
                search.Text = cycle % 5 == 0 ? "no matching item" : "item" + cycle % 10;
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Right)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                window.UpdateLayout();
                if (model.SearchText != search.Text) throw new InvalidOperationException("Navigation acted on stale search results.");
                if (model.VisibleItems.Count > 0)
                {
                    list.SelectedIndex = model.VisibleItems.Count - 1;
                    // Compact selection stays local until activation; the large
                    // popup synchronizes it immediately. Assert the picked ID.
                    var request = model.ActivateAt(list.SelectedIndex);
                    if (request?.ItemId != ((ClipboardItem)list.SelectedItem).Id)
                        throw new InvalidOperationException("View/model activation diverged.");
                    model.TogglePinSelected();
                    if (cycle % 11 == 0) model.DeleteSelected(force: true);
                }
                language.Invoke(app, [cycle % 2 == 0 ? AppLanguage.Portuguese : AppLanguage.English]);
                large.ApplyTheme(cycle % 3 == 0 ? ColorScheme.HighContrast : ColorScheme.Dark);
                model.SetSearch("");
                if (cycle % 25 == 0)
                {
                    var settingsWindow = new SettingsWindow(settings, System.IO.Path.Combine(dir, "settings.json"), null,
                        IntPtr.Zero, ColorScheme.Dark, null, () => { });
                    settingsWindow.Show();
                    settingsWindow.UpdateLayout();
                    settingsWindow.Close();
                }
                cycle++;
                // WPF releases disconnected visual trees at ContextIdle.
                // Drain that work between user gestures; a continuously queued
                // Background callback would starve cleanup in the harness.
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, advance!);
            };
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background, advance);
            app.Run();
            return 0;
        }
        finally { System.IO.Directory.Delete(dir, recursive: true); }
    }
}
