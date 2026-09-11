// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Security.Principal;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using WindowsCM.Core.Actions;
using WindowsCM.Core.Capture;
using WindowsCM.Core.Capture.Win32;
using WindowsCM.Core.Feedback;
using WindowsCM.Core.History;
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Hotkeys.Win32;
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Paste.Win32;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;
using WindowsCM.Core.Settings;
using WindowsCM.Core.Tray;

namespace WindowsCM.App;

// Composition root: builds every Core service with its production Win32
// adapter and owns the shutdown order. No domain logic lives here — only
// wiring (hotkey HWND, UI-thread marshaling, option bridges, file paths).
public partial class App : System.Windows.Application
{
    private readonly List<IDisposable> _disposables = [];
    private AppSettings _settings = AppSettings.Default();
    private string _settingsPath = AppFolders.SettingsPath();
    private LockedHistoryStore? _store;
    private CaptureService? _capture;
    private CaptureOptions? _captureOptions;
    private PasteOptions? _pasteOptions;
    private PopupViewModel? _popupModel;
    private PopupWindow? _popup;
    private TrayManager? _tray;
    private HotkeyWindow? _hotkeyWindow;
    private HotkeyService? _hotkeys;
    private SettingsHotkeySettings? _hotkeySettings;
    private PasteOrchestrator? _orchestrator;
    private ActionExecutor? _executor;
    private ActionConfig _actions = BuiltinActions.Default();
    private string _actionsPath = ActionsPaths.Default();
    private IntPtr _pasteTarget;
    private long _lastFeedbackId = -1;
    private DateTime _lastFeedbackAt;
    private SettingsWindow? _settingsWindow;

    internal AppSettings Settings => _settings;
    internal IHistoryStore Store => _store ?? throw new InvalidOperationException("Services not built.");
    internal ActionExecutor Executor => _executor ?? throw new InvalidOperationException("Services not built.");
    internal ActionConfig Actions => _actions;

    internal void SetIncognito(bool on)
    {
        if (_capture is null || _popupModel is null || _popup is null)
        {
            return;
        }
        _capture.IsIncognito = on;
        _popupModel.SetIncognito(on);
        _popup.RefreshView();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        AppFolders.EnsureCreated();
        _settingsPath = AppFolders.SettingsPath();
        _settings = SettingsStore.Load(_settingsPath);
        _actionsPath = ActionsPaths.Resolve(null);
        _actions = ActionsStore.Load(_actionsPath);

        var cli = CliOptions.Parse(e.Args);
        if (cli.ShowHelp)
        {
            MessageBox.Show(
                "WindowsCM clipboard manager\n\n" +
                "--toggle | --show | --hide | --clear | --clear-all\n" +
                "--hidden   start tray-only (autostart)\n" +
                "--help     this text",
                "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            MessageBox.Show("Could not determine the current user SID; exiting.",
                "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mutex = new MutexSingleInstanceLock(InstanceNames.BuildMutexName(sid));
        _disposables.Add(mutex);
        var pipeName = InstanceNames.BuildPipeName(sid);
        var outcome = SingleInstanceCoordinator.Decide(
            mutex, new NamedPipeForwarder(), cli, pipeName);
        if (outcome != SingleInstanceOutcome.IsPrimary)
        {
            if (outcome == SingleInstanceOutcome.ForwardFailed)
            {
                MessageBox.Show("WindowsCM is not running and the command could not be delivered.",
                    "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            Shutdown(0);
            return;
        }

        BuildServices(pipeName);
        if (!cli.StartHidden)
        {
            // Normal start stays tray-only; the popup opens via hotkey/tray.
        }
    }

    private void BuildServices(string pipeName)
    {
        var dispatcher = Dispatcher;
        var clock = new SystemClock();

        _store = new LockedHistoryStore(
            new SqliteHistoryStore($"Data Source={_settings.History.ResolveDatabasePath()};Pooling=false"));
        _disposables.Add(_store);

        var captureOptions = _settings.ToCaptureOptions();
        _captureOptions = captureOptions;
        _capture = new CaptureService(
            _store, new FileImageAssetStore(AppFolders.ImagesDir()), captureOptions, clock);

        // Startup rotation + orphan sweep (spec Janitor/history limits).
        _store.Evict(_settings.History.MaxItems, _settings.History.MaxAgeMinutes,
            clock.UtcNow, _settings.Behavior.ProtectPinned, _settings.Behavior.ProtectTagged);
        _capture.SweepOrphanImages();

        var listener = new MessageOnlyClipboardListener();
        _disposables.Add(listener);
        var monitor = new ClipboardMonitor(listener, new Win32ClipboardReader(),
            _capture, new Win32SequenceProvider(), new Win32ForegroundProcess(), clock);
        _disposables.Add(monitor);
        listener.ClipboardChanged += OnClipboardChangedFeedback;

        var foreground = new Win32ForegroundWindow();
        var pasteOptions = _settings.ToPasteOptions();
        _pasteOptions = pasteOptions;
        _orchestrator = new PasteOrchestrator(_store, _capture,
            new FileImageReader(AppFolders.ImagesDir()), new Win32ClipboardWriter(),
            foreground, new Win32ElevationProbe(), new Win32PasteInjector(),
            new SystemPasteDelay(), pasteOptions, clock);
        _pasteTarget = foreground.GetCurrent();

        _executor = new ActionExecutor(new ProcessRunner(), new ShellLauncher());

        var previews = new LinkPreviewService(new LinkPreviewHttpClient(),
            new LinkImageCache(AppFolders.CacheDir()), _settings.ToLinkPreviewOptions());

        _popupModel = new PopupViewModel(_store);
        _popup = new PopupWindow(_popupModel, this);
        _hotkeyWindow = new HotkeyWindow(OnHotkey);
        var hwnd = _hotkeyWindow.EnsureHandle();
        _hotkeySettings = new SettingsHotkeySettings(_settings, _settingsPath);
        _hotkeys = new HotkeyService(new Win32HotkeyRegistrar(), _hotkeySettings);
        var registration = _hotkeys.RegisterAll(hwnd);
        var conflictGuidance = string.Join("\n",
            new[] { registration.Open.Diagnostics, registration.Incognito.Diagnostics }
                .Where(d => !string.IsNullOrWhiteSpace(d)));

        var shell = new ShellPopup(_popup, dispatcher);
        // Ticket 22: tray and pipe shows capture the paste target too —
        // otherwise the orchestrator pastes with the stale hotkey-time
        // handle and misreports focus loss.
        var popup = new TargetCapturingPopup(
            shell,
            captureTarget: () =>
            {
                try
                {
                    return foreground.GetCurrent();
                }
                catch (PlatformNotSupportedException)
                {
                    return IntPtr.Zero;
                }
            },
            onCaptured: hw => _pasteTarget = hw);
        var incognito = new ShellIncognito(_capture, _popupModel, _popup);
        var history = new ShellHistory(_store, _popupModel, _popup);
        var settings = new ShellSettingsOpener(dispatcher, () => OpenSettings());
        var exiter = new ShellExiter(() => Shutdown(0));
        var controller = new TrayController(popup, incognito, history, settings, exiter);
        _tray = new TrayManager(controller, incognito, dispatcher);
        _disposables.Add(_tray);
        if (!string.IsNullOrWhiteSpace(conflictGuidance))
        {
            _tray.ShowBalloon("Hotkey conflict", conflictGuidance);
        }

        var dispatcherFacade = new IpcDispatcher(popup, _store);
        var server = new NamedPipeServer(pipeName, dispatcherFacade);
        server.Start();
        _disposables.Add(server);

        var janitor = new SessionJanitor(new SystemEventsSessionEndingSource(),
            _store, () => _settings.History.EndOfSession);
        _disposables.Add(janitor);

        WatchActionsFile();
    }

    private void OnHotkey(HotkeySlot slot)
    {
        if (_popup is null || _capture is null)
        {
            return;
        }
        _pasteTarget = new Win32ForegroundWindow().GetCurrent();
        if (slot == HotkeySlot.Incognito)
        {
            _capture.IsIncognito = true;
            _popup.ShowAtCursor(incognito: true);
            return;
        }
        _popup.ShowAtCursor(incognito: false);
    }

    // Copy-feedback for captures the monitor stored: runs after the
    // monitor's handler (subscribed later), so the store head is current.
    // Head identity (id + datetime) dedups bumps vs. genuinely new items.
    private void OnClipboardChangedFeedback(object? sender, EventArgs e)
    {
        if (_store is null || _tray is null)
        {
            return;
        }
        var head = _store.List().FirstOrDefault();
        if (head is null || (head.Id == _lastFeedbackId && head.CapturedAt == _lastFeedbackAt))
        {
            return;
        }
        _lastFeedbackId = head.Id;
        _lastFeedbackAt = head.CapturedAt;
        CopyFeedbackService.NotifyCopied(_settings.ToCopyFeedbackOptions(), _tray, _tray);
        SoundFeedback.PlayIfEnabled(_settings.ToSoundOptions(),
            new MediaPlayerSoundPlayer(Dispatcher, AppContext.BaseDirectory));
    }

    internal async Task ActivateAsync(ActivationRequest request, bool shiftHeld)
    {
        if (_store is null || _orchestrator is null || _executor is null || _popup is null)
        {
            return;
        }
        var item = _store.List().FirstOrDefault(i => i.Id == request.ItemId);
        if (item is null)
        {
            var missing = ActivationFeedbackPolicy.ForMissingItem(request.ItemId);
            // Never silent: the list went stale (e.g. cleared via tray/pipe
            // between show and Enter), so refresh and explain instead of
            // vanishing.
            _popupModel?.Refresh();
            _popup.RefreshView();
            if (!string.IsNullOrWhiteSpace(missing.BalloonText))
            {
                _tray?.ShowBalloon("WindowsCM", missing.BalloonText);
            }
            return;
        }
        if (request.RunDefaultAction)
        {
            var defaultResult = await _executor.ExecuteDefaultAsync(_actions, item).ConfigureAwait(true);
            await HandleActionResultAsync(defaultResult, item).ConfigureAwait(true);
            return;
        }
        var captured = _pasteTarget;
        var outcome = await _orchestrator.ExecuteAsync(
            item.Id, captured, shiftHeld,
            () => { _popup.Hide(); return Task.CompletedTask; }).ConfigureAwait(true);
        // Ticket 22, never silent: success signals the copy; copy-with-a-
        // warning signals plus balloons the reason (focus lost / elevated);
        // hard failures balloon without a copy signal.
        var feedback = ActivationFeedbackPolicy.ForOutcome(outcome);
        if (feedback.SignalCopy)
        {
            ExplicitCopyFeedback();
        }
        if (!string.IsNullOrWhiteSpace(feedback.BalloonText))
        {
            _tray?.ShowBalloon("WindowsCM", feedback.BalloonText);
        }
    }

    internal async Task RunActionAsync(ClipboardAction action, ClipboardItem item)
    {
        if (_executor is null || _popup is null)
        {
            return;
        }
        var result = await _executor.ExecuteAsync(action, item).ConfigureAwait(true);
        await HandleActionResultAsync(result, item).ConfigureAwait(true);
    }

    internal void ShowQr(string payload)
    {
        new QrWindow(payload).Show();
    }

    private async Task HandleActionResultAsync(ActionResult result, ClipboardItem item)
    {
        if (_popup is null)
        {
            return;
        }
        switch (result.Status)
        {
            case ActionStatus.Done:
            case ActionStatus.NoDefault:
            case ActionStatus.NotApplicable:
                _popup.Hide();
                break;
            case ActionStatus.Copy when result.Output is not null:
                System.Windows.Clipboard.SetText(result.Output);
                _popup.Hide();
                ExplicitCopyFeedback();
                break;
            case ActionStatus.Paste when result.Output is not null:
                // Best-effort direct paste (no orchestrator item involved):
                // write, hide, inject the configured chord.
                try
                {
                    System.Windows.Clipboard.SetText(result.Output);
                    _popup.Hide();
                    await Task.Delay(_settings.Paste.DelayMs).ConfigureAwait(true);
                    new Win32PasteInjector().Inject(_settings.ToPasteOptions().PasteSequence);
                    ExplicitCopyFeedback();
                }
                catch (Exception ex) when (ex is PasteInjectionException or System.Runtime.InteropServices.ExternalException)
                {
                    _tray?.ShowBalloon("WindowsCM", $"Paste failed after copying: {ex.Message}");
                }
                break;
            case ActionStatus.ShowQr when result.Output is not null:
                _popup.Hide();
                new QrWindow(result.Output).Show();
                break;
            case ActionStatus.Failed:
                _popup.Hide();
                if (!string.IsNullOrWhiteSpace(result.Diagnostics))
                {
                    _tray?.ShowBalloon("WindowsCM", result.Diagnostics);
                }
                break;
            default:
                _popup.Hide();
                break;
        }
    }

    // Feedback for copies the shell performed itself: the monitor's echo is
    // suppressed by design (CopiedFromHistory), so no head change fires —
    // the shell reports its own copy explicitly instead.
    private void ExplicitCopyFeedback()
    {
        if (_tray is null)
        {
            return;
        }
        CopyFeedbackService.NotifyCopied(_settings.ToCopyFeedbackOptions(), _tray, _tray);
        SoundFeedback.PlayIfEnabled(_settings.ToSoundOptions(),
            new MediaPlayerSoundPlayer(Dispatcher, AppContext.BaseDirectory));
    }

    internal void ApplyTagSlot(ClipboardItem item, int slot)
    {
        if (_store is null || _popupModel is null || _popup is null)
        {
            return;
        }
        // Slots 1..9 address the nine tag colors; slot 0 clears the tag.
        _store.SetTag(item.Id, slot == 0 ? null : ItemTags.All[(slot - 1) % ItemTags.All.Count]);
        _popupModel.Refresh();
        _popup.RefreshView();
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, _settingsPath, _hotkeys,
            _hotkeyWindow?.Handle ?? IntPtr.Zero, () => _settingsWindow = null);
        _settingsWindow.Closed += (_, _) => OnSettingsClosed();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsClosed()
    {
        SettingsStore.Save(_settingsPath, _settings);
        // Reapply live:Ctor-held option shapes are mutated in place because
        // the services keep the same references (no restart needed).
        if (_captureOptions is not null)
        {
            var fresh = _settings.ToCaptureOptions();
            _captureOptions.ExcludedProcesses = fresh.ExcludedProcesses;
            _captureOptions.MaxCharacters = fresh.MaxCharacters;
            _captureOptions.UpdateDateOnCopy = fresh.UpdateDateOnCopy;
        }
        if (_pasteOptions is not null)
        {
            var fresh = _settings.ToPasteOptions();
            _pasteOptions.PasteSequence = fresh.PasteSequence;
            _pasteOptions.PasteDelayMs = fresh.PasteDelayMs;
            _pasteOptions.SwapCopyPaste = fresh.SwapCopyPaste;
        }
        _settingsWindow = null;
    }

    private void WatchActionsFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(_actionsPath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }
            Directory.CreateDirectory(directory);
            var watcher = new FileSystemWatcher(directory, Path.GetFileName(_actionsPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            watcher.Changed += (_, _) => ReloadActions();
            watcher.Created += (_, _) => ReloadActions();
            _disposables.Add(watcher);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Live reload is convenience: a missing/unwatchable config dir
            // never blocks startup (Load already degraded to built-ins).
        }
    }

    private void ReloadActions()
    {
        try
        {
            _actions = ActionsStore.Load(_actionsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the last good config; corrupt files degrade on next load.
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            try
            {
                _disposables[i].Dispose();
            }
            catch
            {
            }
        }
        _hotkeyWindow?.Close();
    }
}
