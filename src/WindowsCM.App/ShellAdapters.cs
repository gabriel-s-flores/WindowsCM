// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsCM.Core.Feedback;
using WindowsCM.Core.History;
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Settings;
using WindowsCM.Core.Tray;

namespace WindowsCM.App;

// Thread-safe store: the single SqliteConnection is touched from the UI
// thread (popup, orchestrator) and the pipe-server thread (remote
// clear/toggle). One lock around every seam keeps the stories 36/41
// cross-thread use honest.
public sealed class LockedHistoryStore : IHistoryStore
{
    private readonly IHistoryStore _inner;
    private readonly object _gate = new();
    private bool _disposed;

    public LockedHistoryStore(IHistoryStore inner) => _inner = inner;

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        lock (_gate)
        {
            return _inner.AddOrUpdate(item);
        }
    }

    public IReadOnlyList<ClipboardItem> List()
    {
        lock (_gate)
        {
            return _inner.List();
        }
    }

    public long TryUpdateContent(long id, ItemKind kind, string content)
    {
        lock (_gate)
        {
            return _inner.TryUpdateContent(id, kind, content);
        }
    }

    public int Clear(bool keepProtected, bool protectPinned = true, bool protectTagged = true)
    {
        lock (_gate)
        {
            return _inner.Clear(keepProtected, protectPinned, protectTagged);
        }
    }

    public int Evict(int maxCount, int maxAgeMinutes, DateTime utcNow,
        bool protectPinned = true, bool protectTagged = true)
    {
        lock (_gate)
        {
            return _inner.Evict(maxCount, maxAgeMinutes, utcNow, protectPinned, protectTagged);
        }
    }

    public IReadOnlyList<ClipboardItem> Search(string query, bool? pinned = null, string? tag = null,
        ItemKind? kind = null, bool excludePinned = false, bool excludeTagged = false)
    {
        lock (_gate)
        {
            return _inner.Search(query, pinned, tag, kind, excludePinned, excludeTagged);
        }
    }

    public void RefreshDate(long id, DateTime utcNow)
    {
        lock (_gate)
        {
            _inner.RefreshDate(id, utcNow);
        }
    }

    public bool Delete(long id)
    {
        lock (_gate)
        {
            return _inner.Delete(id);
        }
    }

    public void SetPinned(long id, bool pinned)
    {
        lock (_gate)
        {
            _inner.SetPinned(id, pinned);
        }
    }

    public void SetTag(long id, string? tag)
    {
        lock (_gate)
        {
            _inner.SetTag(id, tag);
        }
    }

    public void SetTitle(long id, string? title)
    {
        lock (_gate)
        {
            _inner.SetTitle(id, title);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        lock (_gate)
        {
            _inner.Dispose();
        }
    }
}

// UI-thread popup behind TrayController and the pipe dispatcher: the tray
// gestures already run on the UI thread, but pipe commands arrive on pool
// threads, so every call marshals to the window's dispatcher.
public sealed class ShellPopup(PopupWindow window, Dispatcher dispatcher) : ITrayPopup
{
    public bool IsVisible => dispatcher.Invoke(() => window.IsVisible);

    public void Show(bool incognito) =>
        dispatcher.Invoke(() => window.ShowAtCursor(incognito));

    public void Hide() => dispatcher.Invoke(window.Hide);

    public void Toggle() => dispatcher.Invoke(() =>
    {
        if (window.IsVisible)
        {
            window.Hide();
        }
        else
        {
            window.ShowAtCursor(incognito: false);
        }
    });
}

public sealed class ShellIncognito(
    Core.Capture.CaptureService capture, PopupViewModel model, PopupWindow window) : IIncognitoToggle
{
    public bool IsIncognito => capture.IsIncognito;

    public void SetIncognito(bool on)
    {
        capture.IsIncognito = on;
        model.SetIncognito(on);
        window.RefreshView();
    }
}

public sealed class ShellHistory(
    IHistoryStore store, PopupViewModel model, PopupWindow window) : IClearHistory
{
    public int ClearKeepProtected()
    {
        var removed = store.Clear(keepProtected: true);
        model.Refresh();
        window.RefreshView();
        return removed;
    }
}

public sealed class ShellSettingsOpener(Dispatcher dispatcher, Action open) : ISettingsOpener
{
    public void OpenSettings() => dispatcher.Invoke(open);
}

public sealed class ShellExiter(Action exit) : IAppExiter
{
    public void RequestExit() => exit();
}

// Gesture persistence behind HotkeyService remaps: the two global chords
// live on ShortcutSettings and persist with the whole settings file.
public sealed class SettingsHotkeySettings(AppSettings settings, string path) : IHotkeySettings
{
    public string OpenGesture
    {
        get => settings.Shortcuts.OpenGesture;
        set => settings.Shortcuts.OpenGesture = value;
    }

    public string IncognitoGesture
    {
        get => settings.Shortcuts.IncognitoGesture;
        set => settings.Shortcuts.IncognitoGesture = value;
    }

    public void Save() => SettingsStore.Save(path, settings);
}

// Production session-ending source over SystemEvents (needs the WPF message
// pump, which the tray app has). Cancel is never set: cleanup never blocks
// logout (SessionJanitor owns that invariant too).
public sealed class SystemEventsSessionEndingSource : ISessionEndingSource, IDisposable
{
    private bool _disposed;

    public event EventHandler<SessionEndingEventArgs>? SessionEnding;

    public SystemEventsSessionEndingSource() =>
        Microsoft.Win32.SystemEvents.SessionEnding += OnSystemSessionEnding;

    private void OnSystemSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e) =>
        SessionEnding?.Invoke(this, new SessionEndingEventArgs());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Microsoft.Win32.SystemEvents.SessionEnding -= OnSystemSessionEnding;
    }
}

// Production sound player over MediaPlayer (SoundPlayer has no volume).
// Assets are resolved under the app directory; a missing wav is a silent
// no-op (assets ship post-v1; the default sound is None anyway).
public sealed class MediaPlayerSoundPlayer(Dispatcher dispatcher, string appDir) : ISoundPlayer
{
    public void Play(SoundName name, double gain)
    {
        var file = SoundAssets.FileNameFor(name);
        if (file is null)
        {
            return;
        }
        var path = Path.Combine(appDir, "sounds", file);
        if (!File.Exists(path))
        {
            return;
        }
        dispatcher.Invoke(() =>
        {
            try
            {
                var player = new MediaPlayer { Volume = Math.Clamp(gain, 0.0, 1.0) };
                player.MediaEnded += (_, _) => player.Close();
                player.Open(new Uri(path));
                player.Play();
            }
            catch
            {
                // Feedback must never crash the capture flow.
            }
        });
    }
}
