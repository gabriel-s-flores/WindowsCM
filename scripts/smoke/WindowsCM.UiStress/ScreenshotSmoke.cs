// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsCM.App;
using WindowsCM.Core.History;
using WindowsCM.Core.Capture;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Release;
using WindowsCM.Core.Settings;

internal static class ScreenshotSmoke
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        using var store = new SqliteHistoryStore("Data Source=:memory:");
        using var images = new EphemeralImageAssetStore();
        using var coordinator = new IncognitoSessionCoordinator(store, images);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        typeof(App).GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, coordinator);
        var language = typeof(App).GetMethod("UpdateLanguage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        language.Invoke(app, [AppLanguage.English]);
        var settings = (AppSettings)typeof(App).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        settings.Language = AppLanguage.English;
        var now = DateTime.UtcNow;
        var samples = new (ItemKind Kind, string Content, bool Pin)[]
        {
            (ItemKind.Character, "🚀", false),
            (ItemKind.Text, "Release checklist\nReview changes\nRun smoke tests\nPublish the release", true),
            (ItemKind.Color, "#0078D4", false),
            (ItemKind.Code, "public static string Greet(string name)\n{\n    return $\"Hello, {name}!\";\n}", true),
            (ItemKind.Text, "Meeting notes\nKeep useful snippets within reach.\nSearch history, pin items and paste into your previous app.", false),
        };
        for (var i = 1; i < samples.Length; i++)
            store.AddOrUpdate(new ClipboardItem(samples[i].Kind, samples[i].Content, samples[i].Pin, null, now.AddSeconds(i), null, samples[i].Kind == ItemKind.Character ? "Rocket" : null));
        var model = new PopupViewModel(coordinator);
        var compact = new CompactPopupWindow(model, app);
        compact.ApplyTheme(ColorScheme.Dark);
        compact.ShowAtCursor(false);
        Save(compact, output, "compact-popup.png");
        var search = (TextBox)compact.FindName("SearchBox");
        search.Text = "Release checklist";
        Drain();
        model.SetSearch(search.Text);
        compact.RefreshView();
        if (model.VisibleItems.Count != 1 || !model.VisibleItems[0].Pinned) throw new InvalidOperationException("Search/pin smoke failed.");
        Save(compact, output, "search-pinned.png");
        search.Text = "";
        model.SetSearch("");
        compact.Hide();
        store.AddOrUpdate(new ClipboardItem(ItemKind.Character, samples[0].Content, false, null, now, null, null));
        var large = new PopupWindow(model, app);
        settings.Dialog.LargePlacement = LargePlacementMode.Free;
        large.ShowAtCursor(false);
        large.Width = 1360;
        large.Height = 348;
        Save(large, output, "rich-preview-cards.png");
        large.Hide();
        model.SetIncognito(true);
        coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Text, "Temporary note\nOnly available during this incognito session.", false, null, now, null, null));
        coordinator.AddOrUpdate(new ClipboardItem(ItemKind.Code, "var session = new TemporarySession();", false, null, now.AddSeconds(1), null, null));
        compact.ShowAtCursor(true);
        Save(compact, output, "incognito-popup.png");
        model.SetIncognito(false);
        if (coordinator.IsIncognito || coordinator.EphemeralStore is not null || store.List().Count != samples.Length)
            throw new InvalidOperationException("Incognito exit did not clear temporary items and preserve normal history.");
        compact.Hide();
        var configPath = Path.Combine(Path.GetTempPath(), "WindowsCM-screenshot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var config = new SettingsWindow(settings, configPath, null, IntPtr.Zero, ColorScheme.Dark, null, () => { });
            config.Show();
            ((Button)config.FindName("NavBtnLayout")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Save(config, output, "settings-layout.png");
            ((Button)config.FindName("NavBtnColors")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Save(config, output, "settings-colors.png");
            config.Close();
        }
        finally { File.Delete(configPath); }
        var update = new UpdateWindow(new AvailableUpdate(new Version(1, 2, 3), "fixture.zip", new Uri("https://example.invalid"), ""), () => Task.CompletedTask);
        update.Show();
        Save(update, output, "update-prompt.png");
        language.Invoke(app, [AppLanguage.Portuguese]);
        if (update.Title != PortugueseAppStrings.Instance.UpdateTitle) throw new InvalidOperationException("Open update window did not switch language.");
        language.Invoke(app, [AppLanguage.English]);
        update.Close();
        app.Shutdown();
        Console.WriteLine("PASS: English production windows, mixed item types, pinned search, incognito indicator, settings navigation and live update localization; seven PNG captures saved.");
        return 0;
    }

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Save(Window window, string output, string name)
    {
        Drain();
        window.UpdateLayout();
        // Render the actual production visual tree, independent of other desktop windows.
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name));
        encoder.Save(file);
        Console.WriteLine($"Captured {name}: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
    }
}
