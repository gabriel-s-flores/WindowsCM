// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Security.Principal;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using WindowsCM.Core.Actions;
using WindowsCM.Core.Capture;
using WindowsCM.Core.Capture.Win32;
using WindowsCM.Core.Classification;
using WindowsCM.Core.Diagnostics;
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
using WindowsCM.App.Localization;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Settings;
using WindowsCM.Core.Transfer;
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
    private ResourceDictionary? _localizationDict;
    private LockedHistoryStore? _store;
    private CaptureService? _capture;
    private CaptureOptions? _captureOptions;
    private PasteOptions? _pasteOptions;
    private PopupViewModel? _popupModel;
    private PopupWindow? _popup;
    private CompactPopupWindow? _compactPopup;
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
    private WelcomeWindow? _welcomeWindow;
    private Win32WindowsThemeDetector? _themeDetector;
    private IncognitoSessionCoordinator? _coordinator;
    private LinkPreviewService? _linkPreviewService;
    private MiniTransferHttpServer? _transferServer;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, byte> _inFlightLinkFetches = new();
    private readonly ErrorLog _errorLog = new(ErrorLog.DefaultPath());
    private readonly UnhandledErrorPolicy _errorPolicy = new();
    private System.Windows.Threading.DispatcherTimer? _viewRefreshTimer;
    private System.Windows.Threading.DispatcherTimer? _liveSettingsTimer;
    private Win32ForegroundTracker? _foregroundTracker;
    private ColorScheme _lastSystemScheme = ColorScheme.Dark;
    private bool _servicesReady;

    internal AppSettings Settings => _settings;
    internal IHistoryStore Store => _coordinator ?? (IHistoryStore?)_store ?? throw new InvalidOperationException("Services not built.");
    internal ActionExecutor Executor => _executor ?? throw new InvalidOperationException("Services not built.");
    internal ActionConfig Actions => _actions;
    internal bool IsIncognito => _coordinator?.IsIncognito ?? false;
    internal MiniTransferHttpServer? TransferServer => _transferServer;

    internal void SetIncognito(bool on)
    {
        if (_coordinator is null || _popupModel is null || _popup is null)
        {
            return;
        }
        _coordinator.SetIncognito(on);
        _popupModel.SetIncognito(on);
        RefreshOpenPopups();
        _tray?.UpdateIncognitoState(on);
    }

    // The two popups share one PopupViewModel but may order it differently
    // (recent first or last, per popup). Refreshing both re-queried the
    // model in the compact popup's order while the large popup kept showing
    // its own, so clicking card i there pasted a different item. Only an
    // open popup is refreshed, in its own order; a hidden one reloads when
    // it is shown.
    internal void RefreshOpenPopups()
    {
        if (_popupModel is null)
        {
            return;
        }
        if (_popup?.IsVisible == true)
        {
            _popupModel.Refresh();
            _popup.RefreshView();
        }
        else if (_compactPopup?.IsVisible == true)
        {
            _popupModel.Refresh();
            _compactPopup.RefreshView();
        }
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        InstallCrashHandlers();
        AppFolders.EnsureCreated();
        _settingsPath = AppFolders.SettingsPath();
        _settings = SettingsStore.Load(_settingsPath);
        _actionsPath = ActionsPaths.Resolve(null);
        _actions = ActionsStore.Load(_actionsPath);

        LocalizationManager.CurrentLanguage = _settings.Language;
        UpdateLanguage(_settings.Language);

        var cli = CliOptions.Parse(e.Args);
        if (cli.ShowHelp)
        {
            MessageBox.Show(
                LocalizationManager.Strings.CliHelpText,
                "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            MessageBox.Show(LocalizationManager.Strings.ErrorSidDetermination,
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
                MessageBox.Show(LocalizationManager.Strings.ErrorForwardFailed,
                    "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            Shutdown(0);
            return;
        }

        BuildServices(pipeName);
        _servicesReady = true;
        // Also on a --hidden (autostart) first start: a tray-only app is
        // otherwise invisible to someone who never opened it themselves.
        ShowWelcomeOnFirstRun();
        if (!cli.StartHidden)
        {
            // Normal start stays tray-only; the popup opens via hotkey/tray.
        }
    }

    // Safety net: before this any exception in a UI handler closed the app
    // without a trace. Errors are logged (never clipboard content); isolated
    // UI errors are survived, a burst (a per-frame failure) is not. A
    // failure while services are still being built is never swallowed: a
    // half-built app would keep running without a tray icon, holding the
    // single-instance mutex.
    private void InstallCrashHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogError("ui", args.Exception);
            args.Handled = _servicesReady && _errorPolicy.ShouldContinue(DateTime.UtcNow);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogError(args.IsTerminating ? "fatal" : "background",
                args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError("task", args.Exception);
            args.SetObserved();
        };
        // The tray icon is WinForms: an exception in its click or menu
        // handlers never reaches DispatcherUnhandledException. WinForms
        // showed its own "unhandled exception" dialog instead, whose Quit
        // ended the process without cleanup and without a log line.
        try
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(
                System.Windows.Forms.UnhandledExceptionMode.CatchException);
        }
        catch (InvalidOperationException)
        {
            // A WinForms window already exists on this thread: the handler
            // below still receives the exceptions.
        }
        System.Windows.Forms.Application.ThreadException += (_, args) =>
        {
            LogError("tray", args.Exception);
            if (!_errorPolicy.ShouldContinue(DateTime.UtcNow))
            {
                Shutdown(1);
            }
        };
    }

    internal void LogError(string context, Exception exception) => _errorLog.Write(context, exception);

    private void BuildServices(string pipeName)
    {
        var dispatcher = Dispatcher;
        var clock = new SystemClock();
        // Coalesces background refresh requests (link previews finishing,
        // one per link) into a single model + view refresh.
        _viewRefreshTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _viewRefreshTimer.Tick += (_, _) =>
        {
            _viewRefreshTimer.Stop();
            RefreshOpenPopups();
        };

        // Never throws: a damaged or unreachable database used to end every
        // launch here, before the tray icon existed.
        var history = HistoryStoreOpener.Open(
            _settings.History.ResolveDatabasePath(), DatabasePaths.Default(), clock.UtcNow);
        if (history.Error is not null)
        {
            LogError("history-open", history.Error);
        }
        _store = new LockedHistoryStore(history.Store);
        _disposables.Add(_store);

        var persistentImages = new FileImageAssetStore(AppFolders.ImagesDir());
        _coordinator = new IncognitoSessionCoordinator(_store, persistentImages);
        _disposables.Add(_coordinator);

        var captureOptions = _settings.ToCaptureOptions();
        _captureOptions = captureOptions;
        _capture = new CaptureService(
            _coordinator, _coordinator, captureOptions, clock);

        // Startup rotation + orphan sweep (spec Janitor/history limits).
        // Housekeeping: a disk error here must not keep the app from
        // starting.
        try
        {
            _store.Evict(_settings.History.MaxItems, _settings.History.MaxAgeMinutes,
                clock.UtcNow, _settings.Behavior.ProtectPinned, _settings.Behavior.ProtectTagged);
            _capture.SweepOrphanImages();
            EphemeralImageAssetStore.SweepStaleSessions(TimeSpan.FromDays(1));
            // Only the first screenful: a full prewarm would hold every
            // thumbnail in memory even if the popup is never opened.
            PrewarmImageThumbnails(_store.Search("", kind: ItemKind.Image).Take(24));
        }
        catch (Exception ex)
        {
            LogError("startup-housekeeping", ex);
        }

        var listener = new MessageOnlyClipboardListener();
        _disposables.Add(listener);
        listener.HandlerFailed += (_, ex) => LogError("clipboard-listener", ex);
        var monitor = new ClipboardMonitor(listener, new Win32ClipboardReader(),
            _capture, new Win32SequenceProvider(), new Win32ForegroundProcess(), clock);
        _disposables.Add(monitor);
        monitor.CaptureFailed += (_, ex) => LogError("capture", ex);
        listener.ClipboardChanged += OnClipboardChangedFeedback;

        var foreground = new Win32ForegroundWindow();
        // Must be built on this (UI) thread: the foreground hook calls back
        // through its message loop.
        _foregroundTracker = new Win32ForegroundTracker();
        _disposables.Add(_foregroundTracker);
        var pasteOptions = _settings.ToPasteOptions();
        _pasteOptions = pasteOptions;
        _orchestrator = new PasteOrchestrator(_coordinator, _capture,
            new FileImageReader(AppFolders.ImagesDir()), new Win32ClipboardWriter(),
            foreground, new Win32ElevationProbe(), new Win32PasteInjector(),
            new SystemPasteDelay(), pasteOptions, clock);
        _pasteTarget = CapturePasteTarget();

        _executor = new ActionExecutor(new ProcessRunner(), new ShellLauncher());

        _linkPreviewService = new LinkPreviewService(new LinkPreviewHttpClient(),
            new LinkImageCache(Path.Combine(AppFolders.CacheDir(), "link-images")), _settings.ToLinkPreviewOptions());

        _transferServer = new MiniTransferHttpServer();
        _transferServer.PayloadReceived += OnTransferPayloadReceived;
        _transferServer.Start();
        _disposables.Add(_transferServer);

        _popupModel = new PopupViewModel(_coordinator);
        _popup = new PopupWindow(_popupModel, this);
        _compactPopup = new CompactPopupWindow(_popupModel, this);
        _hotkeyWindow = new HotkeyWindow(OnHotkey);
        var hwnd = _hotkeyWindow.EnsureHandle();
        // Before the first parse: gestures may use layout keys (Ç, Cyrillic, ...).
        KeyCodes.Layout = new Win32KeyboardLayout();
        _hotkeySettings = new SettingsHotkeySettings(_settings, _settingsPath);
        _hotkeys = new HotkeyService(new Win32HotkeyRegistrar(), _hotkeySettings);
        var registration = _hotkeys.RegisterAll(hwnd);
        var conflictGuidance = string.Join("\n",
            new[] { registration.Open.Diagnostics, registration.Incognito.Diagnostics }
                .Where(d => !string.IsNullOrWhiteSpace(d)));

        var incognito = new ShellIncognito(this);
        var shell = new ShellPopup(_popup, dispatcher, incognito);
        // Ticket 22: tray and pipe shows capture the paste target too —
        // otherwise the orchestrator pastes with the stale hotkey-time
        // handle and misreports focus loss.
        var popup = new TargetCapturingPopup(
            shell,
            captureTarget: CapturePasteTarget,
            onCaptured: hw => _pasteTarget = hw);
        var clearHistory = new ShellHistory(_coordinator, RefreshOpenPopups);
        var settings = new ShellSettingsOpener(dispatcher, () => OpenSettings());
        var exiter = new ShellExiter(() => Shutdown(0));
        var controller = new TrayController(popup, incognito, clearHistory, settings, exiter);

        _tray = new TrayManager(controller, incognito, dispatcher, () => ShowCompactPopup(),
            isAutoPaste: () => _settings.Behavior.AutoPaste,
            setAutoPaste: SetAutoPaste);
        _disposables.Add(_tray);
        if (!string.IsNullOrWhiteSpace(conflictGuidance))
        {
            _tray.ShowBalloon(LocalizationManager.Strings.TrayShortcutConflictTitle, conflictGuidance);
        }
        var historyNotice = history.Outcome switch
        {
            HistoryOpenOutcome.RecoveredDamaged =>
                LocalizationManager.Strings.HistoryDatabaseRecoveredBalloon(history.Detail ?? ""),
            HistoryOpenOutcome.FellBackToDefault =>
                LocalizationManager.Strings.HistoryDatabaseFallbackBalloon(history.Detail ?? ""),
            HistoryOpenOutcome.MemoryOnly => LocalizationManager.Strings.HistoryDatabaseMemoryOnlyBalloon,
            _ => null,
        };
        if (historyNotice is not null)
        {
            _tray.ShowBalloon("WindowsCM", historyNotice);
        }

        var dispatcherFacade = new IpcDispatcher(popup, _coordinator);
        var server = new NamedPipeServer(pipeName, dispatcherFacade);
        server.Start();
        _disposables.Add(server);

        var janitor = new SessionJanitor(new SystemEventsSessionEndingSource(),
            _store, () => _settings.History.EndOfSession);
        _disposables.Add(janitor);

        _themeDetector = new Win32WindowsThemeDetector();
        _disposables.Add(_themeDetector);
        _lastSystemScheme = _themeDetector.DetectSystemScheme();
        _themeDetector.ThemeChanged += (_, scheme) =>
        {
            try
            {
                dispatcher.Invoke(() =>
                {
                    // Windows raises this for many unrelated preference
                    // changes; each rebuilt every window's theme, hidden
                    // popups full of cards included.
                    if (scheme == _lastSystemScheme)
                    {
                        return;
                    }
                    _lastSystemScheme = scheme;
                    UpdateTheme(scheme);
                });
            }
            catch (Exception ex)
            {
                LogError("theme", ex);
            }
        };
        UpdateTheme(_lastSystemScheme);

        WatchActionsFile();

        // A frozen UI leaves a trace in the log (start, duration, resources)
        // even when the user ends up killing the app.
        var watchdog = new UiHangWatchdog(
            work => dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, work),
            detail => _errorLog.Note("ui-hang", detail),
            threshold: TimeSpan.FromSeconds(5),
            describeProcess: DescribeProcessResources);
        _disposables.Add(watchdog);
        watchdog.Start();
    }

    // Memory and handle counts: tells a hang from resource exhaustion
    // (the process dies at 10,000 GDI or USER objects).
    private static string DescribeProcessResources()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return $"private {process.PrivateMemorySize64 / (1024 * 1024)} MB, {process.HandleCount} handles, "
            + $"{process.Threads.Count} threads, GDI {GetGuiResources(process.Handle, 0)}, USER {GetGuiResources(process.Handle, 1)}";
    }

    private void UpdateTheme(ColorScheme systemScheme)
    {
        var effectiveScheme = _settings.Theme.Scheme == ColorScheme.System
            ? systemScheme
            : _settings.Theme.Scheme;
        var themeDict = PopupThemeBrushes.CreateThemeDictionary(effectiveScheme, _settings.ItemColors);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);
        if (_localizationDict is not null)
        {
            Resources.MergedDictionaries.Add(_localizationDict);
        }
        _popup?.ApplyTheme(effectiveScheme);
        _compactPopup?.ApplyTheme(effectiveScheme);
        _settingsWindow?.ApplyTheme(effectiveScheme);
    }

    internal void UpdateLanguage(AppLanguage language)
    {
        LocalizationManager.CurrentLanguage = language;
        var strings = LocalizationManager.Strings;
        if (_localizationDict is not null)
        {
            Resources.MergedDictionaries.Remove(_localizationDict);
        }
        _localizationDict = AppLocalizationResources.BuildResourceDictionary(strings);
        Resources.MergedDictionaries.Add(_localizationDict);
        MediaMetadataService.ClearCache();
        CardFileFacts.Clear();

        _tray?.UpdateLanguage();
        _popup?.UpdateLanguage();
        _compactPopup?.UpdateLanguage();
        _settingsWindow?.UpdateLanguage();
    }

    // The window a pick pastes into, resolved when the popup is asked for:
    // the app the user was in (even when opened from the tray, where the
    // taskbar holds the foreground), or Zero when the last place was the
    // desktop — then a pick only copies (PasteTargetPolicy).
    private IntPtr CapturePasteTarget()
    {
        try
        {
            return _foregroundTracker?.ResolveTarget() ?? IntPtr.Zero;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            return IntPtr.Zero;
        }
    }

    // Tray menu toggle: applies live and persists.
    internal void SetAutoPaste(bool on)
    {
        _settings.Behavior.AutoPaste = on;
        ApplyAutoPaste();
        SaveSettingsQuietly();
        _settingsWindow?.SyncAutoPaste(on);
    }

    // Free placement: the large window reports where the user dropped or
    // resized it (per orientation) and it reopens exactly there.
    internal void SaveLargeFreeBounds(DialogOrientation orientation, WindowBounds bounds)
    {
        _settings.Dialog.SetFreeBounds(orientation, bounds);
        SaveSettingsQuietly();
    }

    private void SaveSettingsQuietly()
    {
        try
        {
            SettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogError("settings-save", ex);
        }
    }

    private void ApplyAutoPaste()
    {
        if (_pasteOptions is not null)
        {
            _pasteOptions.AutoPaste = _settings.Behavior.AutoPaste;
        }
    }

    private void OnHotkey(HotkeySlot slot)
    {
        if (_popup is null || _compactPopup is null || _capture is null || _coordinator is null)
        {
            return;
        }
        _pasteTarget = CapturePasteTarget();
        if (_popup.IsVisible)
        {
            _popup.Hide();
            return;
        }
        if (_compactPopup.IsVisible)
        {
            _compactPopup.Hide();
            return;
        }

        if (slot == HotkeySlot.Incognito)
        {
            if (!_coordinator.IsIncognito)
            {
                SetIncognito(true);
            }
            _compactPopup.ShowAtCursor(incognito: true);
            return;
        }
        _compactPopup.ShowAtCursor(incognito: _coordinator.IsIncognito);
    }

    internal void ShowCompactPopup()
    {
        if (_compactPopup is null || _coordinator is null)
        {
            return;
        }
        _pasteTarget = CapturePasteTarget();
        if (_popup?.IsVisible == true)
        {
            _popup.Hide();
        }
        _compactPopup.ShowAtCursor(incognito: _coordinator.IsIncognito);
    }


    // Copy-feedback for captures the monitor stored: runs after the
    // monitor's handler (subscribed later), so the store head is current.
    // Head identity (id + datetime) dedups bumps vs. genuinely new items.
    // It runs on the clipboard listener thread, so the head is one indexed
    // query (not the whole history per copy) and nothing may escape.
    private void OnClipboardChangedFeedback(object? sender, EventArgs e)
    {
        if (_coordinator is null || _tray is null)
        {
            return;
        }
        try
        {
            var head = _coordinator.GetLatest();
            if (head is null || (head.Id == _lastFeedbackId && head.CapturedAt == _lastFeedbackAt))
            {
                return;
            }
            _lastFeedbackId = head.Id;
            _lastFeedbackAt = head.CapturedAt;
            CopyFeedbackService.NotifyCopied(_settings.ToCopyFeedbackOptions(), _tray, _tray);
            SoundFeedback.PlayIfEnabled(_settings.ToSoundOptions(),
                new MediaPlayerSoundPlayer(Dispatcher, AppContext.BaseDirectory));

            if (head.Kind == ItemKind.Link)
            {
                FetchPreviewInBackground(head);
            }
            else if (head.Kind == ItemKind.Image)
            {
                PrewarmImageThumbnails([head]);
            }
        }
        catch (Exception ex)
        {
            LogError("copy-feedback", ex);
        }
    }

    private static void PrewarmImageThumbnails(IEnumerable<ClipboardItem> items) =>
        ImageThumbnailCache.Prewarm(items
            .Where(i => i.Kind == ItemKind.Image)
            .Select(ItemDisplayFormatter.TryGetLocalImagePath)
            .OfType<string>());

    // Thread-safe: coalesces into one refresh on the dispatcher.
    private void RequestViewRefresh() =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewRefreshTimer is null)
            {
                return;
            }
            _viewRefreshTimer.Stop();
            _viewRefreshTimer.Start();
        });

    internal void EnsureLinkPreviewsForRecentItems()
    {
        if (_coordinator is null || _linkPreviewService is null) return;
        var links = _coordinator.Search("", kind: ItemKind.Link).Take(15);
        foreach (var link in links)
        {
            var (title, _, img) = ItemMetadataJson.GetLink(link.MetadataJson);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(img))
            {
                FetchPreviewInBackground(link);
            }
        }
    }

    private void FetchPreviewInBackground(ClipboardItem item)
    {
        if (_linkPreviewService is null || _coordinator is null)
        {
            return;
        }
        if (item.Kind != ItemKind.Link || string.IsNullOrWhiteSpace(item.Content))
        {
            return;
        }
        if (!_inFlightLinkFetches.TryAdd(item.Id, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _linkPreviewService.FetchAsync(item.Content).ConfigureAwait(false);
                if (result is null)
                {
                    return;
                }

                var title = result.Metadata.Title;
                var description = result.Metadata.Description;
                var image = result.CachedImagePath ?? result.Metadata.ImageUrl;

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(image))
                {
                    return;
                }

                var overlay = ItemMetadataJson.EncodeLink(title, description, image);
                var merged = ItemMetadataJson.Merge(item.MetadataJson, overlay);

                _coordinator.SetMetadataAndTitle(item.Id, merged, title);

                RequestViewRefresh();
            }
            catch (Exception ex)
            {
                // Best-effort background enrichment
                LogError("link-preview", ex);
            }
            finally
            {
                _inFlightLinkFetches.TryRemove(item.Id, out _);
            }
        });
    }

    // Popups fire and forget these (a click, Enter): nothing may escape, or
    // the failure only surfaced when the GC finalized the task and the user
    // saw nothing happen.
    internal async Task ActivateAsync(ActivationRequest request, bool shiftHeld)
    {
        try
        {
            await ActivateCoreAsync(request, shiftHeld).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogError("activate", ex);
            _tray?.ShowBalloon("WindowsCM", LocalizationManager.Strings.TrayActivationFailedBalloon(ex.Message));
        }
    }

    private async Task ActivateCoreAsync(ActivationRequest request, bool shiftHeld)
    {
        if (_coordinator is null || _orchestrator is null || _executor is null || _popup is null)
        {
            return;
        }
        var item = _coordinator.GetById(request.ItemId);
        if (item is null)
        {
            var missing = ActivationFeedbackPolicy.ForMissingItem(request.ItemId);
            // Never silent: the list went stale (e.g. cleared via tray/pipe
            // between show and Enter), so refresh and explain instead of
            // vanishing.
            RefreshOpenPopups();
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
            () =>
            {
                _popup.Hide();
                _compactPopup?.Hide();
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        // Ticket 22, never silent: success signals the copy; copy-with-a-
        // warning signals plus balloons the reason (focus lost / elevated);
        // hard failures balloon without a copy signal.
        var feedback = ActivationFeedbackPolicy.ForOutcome(outcome);
        if (feedback.SignalCopy)
        {
            ExplicitCopyFeedback();
        }
        if (shiftHeld || outcome.Status is PasteStatus.CopiedOnly or PasteStatus.CopiedOnlyForegroundLost or PasteStatus.CopiedOnlyElevated)
        {
            SubtleToastWindow.ShowToast(LocalizationManager.Strings.ToastAddedToClipboard);
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
        try
        {
            var result = await _executor.ExecuteAsync(action, item).ConfigureAwait(true);
            await HandleActionResultAsync(result, item).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogError("action", ex);
            _tray?.ShowBalloon("WindowsCM", LocalizationManager.Strings.TrayActionFailedBalloon(ex.Message));
        }
    }

    internal void ShowQr(string payload)
    {
        ShowInFront(new QrWindow(payload));
    }

    internal void ShowQr(ClipboardItem item)
    {
        if (_transferServer != null && (item.Kind == ItemKind.File || item.Kind == ItemKind.Files || item.Kind == ItemKind.Image || item.Content.Length > 250))
        {
            string? filePath = null;
            List<string>? filePaths = null;

            if (item.Kind == ItemKind.File)
            {
                filePath = item.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }
            else if (item.Kind == ItemKind.Files)
            {
                filePaths = item.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                filePath = filePaths.FirstOrDefault();
            }
            else if (item.Kind == ItemKind.Image)
            {
                filePath = ItemDisplayFormatter.TryGetLocalImagePath(item);
            }

            var session = _transferServer.RegisterShare(
                title: ItemDisplayFormatter.GetTitle(item),
                kindLabel: ItemDisplayFormatter.GetTypeLabel(item, _settings.FileCategories),
                filePath: filePath,
                filePaths: filePaths,
                textContent: (item.Kind != ItemKind.File && item.Kind != ItemKind.Files && item.Kind != ItemKind.Image) ? item.Content : null,
                itemId: item.Id);

            var ip = LocalNetworkResolver.GetPreferredLocalIp();
            var url = _transferServer.BuildUrl(ip, $"/d/{session.Token}");
            ShowInFront(new QrWindow(session, url, this));
            return;
        }

        var textPayload = WindowsCM.Core.Actions.QrActions.Payload(item.Kind, item.Content);
        if (textPayload != null)
        {
            ShowInFront(new QrWindow(textPayload));
        }
    }

    internal void ShowMobileTransfer()
    {
        if (_transferServer == null) return;
        var ip = LocalNetworkResolver.GetPreferredLocalIp();
        ShowInFront(new MobileTransferWindow(_transferServer, ip));
    }

    // The large popup stays up behind a QR opened from it and is Topmost, so
    // a plain Show() left the QR covered by the strip.
    private void ShowInFront(Window window)
    {
        _popup?.LeaveTopmostBand();
        window.Show();
        window.Activate();
    }

    private void OnTransferPayloadReceived(IncomingTransferPayload payload)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                try
                {
                    System.Windows.Clipboard.SetText(payload.Text);
                }
                catch
                {
                }

                _capture?.CaptureNow(
                    new ClipboardPayload(Image: null, Files: null, Text: payload.Text, Formats: ["CF_UNICODETEXT"], Html: null),
                    "Mobile Transfer");

                RefreshOpenPopups();

                SubtleToastWindow.ShowToast(LocalizationManager.Strings.MobileTextCopiedSuccess);
            }

            if (payload.Files != null && payload.Files.Count > 0)
            {
                var paths = payload.Files.Select(f => f.SavedPath).Where(File.Exists).ToList();
                if (paths.Count > 0)
                {
                    try
                    {
                        var col = new System.Collections.Specialized.StringCollection();
                        col.AddRange(paths.ToArray());
                        System.Windows.Clipboard.SetFileDropList(col);
                    }
                    catch
                    {
                    }

                    _capture?.CaptureNow(
                        new ClipboardPayload(Image: null, Files: new FileSnapshot(paths, FileOperation.Copy), Text: null, Formats: ["CF_HDROP"], Html: null),
                        "Mobile Transfer");

                    RefreshOpenPopups();

                    var first = payload.Files[0].FileName;
                    var count = payload.Files.Count;
                    var msg = count > 1 ? LocalizationManager.Strings.MobileFilesReceived(count, first) : LocalizationManager.Strings.MobileSingleFileReceived(first);
                    SubtleToastWindow.ShowToast(msg);
                }
            }
        });
    }

    private async Task HandleActionResultAsync(ActionResult result, ClipboardItem item)
    {
        if (_popup is null)
        {
            return;
        }
        _compactPopup?.Hide();
        switch (result.Status)
        {
            case ActionStatus.Done:
            case ActionStatus.NoDefault:
            case ActionStatus.NotApplicable:
                _popup.Hide();
                break;
            case ActionStatus.Copy when result.Output is not null:
                _popup.Hide();
                try
                {
                    // Another app holding the clipboard open throws here.
                    System.Windows.Clipboard.SetText(result.Output);
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    LogError("action-copy", ex);
                    _tray?.ShowBalloon("WindowsCM", LocalizationManager.Strings.TrayCopyFailedBalloon(ex.Message));
                    break;
                }
                ExplicitCopyFeedback();
                SubtleToastWindow.ShowToast(LocalizationManager.Strings.ToastAddedToClipboard);
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
                    _tray?.ShowBalloon("WindowsCM", LocalizationManager.Strings.TrayPasteFailedBalloon(ex.Message));
                    SubtleToastWindow.ShowToast(LocalizationManager.Strings.ToastAddedToClipboard);
                }
                break;
            case ActionStatus.ShowQr when result.Output is not null:
                _popup.Hide();
                ShowInFront(new QrWindow(result.Output));
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
        RefreshOpenPopups();
    }

    private void ShowWelcomeOnFirstRun()
    {
        if (_settings.Onboarding.WelcomeShown)
        {
            return;
        }
        // Persist before showing so a crash or kill never re-greets.
        _settings.Onboarding.WelcomeShown = true;
        SaveSettingsQuietly();
        ShowWelcome();
    }

    internal void ShowWelcome()
    {
        if (_welcomeWindow is not null)
        {
            _welcomeWindow.Activate();
            return;
        }
        _welcomeWindow = new WelcomeWindow(
            _hotkeys?.OpenChord.ToString() ?? HotkeyDefaults.OpenGesture,
            _hotkeys?.IncognitoChord.ToString() ?? HotkeyDefaults.IncognitoGesture,
            openSettings: () => OpenSettings());
        _welcomeWindow.Closed += (_, _) => _welcomeWindow = null;
        _welcomeWindow.Show();
        _welcomeWindow.Activate();
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            var handle = new System.Windows.Interop.WindowInteropHelper(_settingsWindow).Handle;
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
            return;
        }

        var effectiveScheme = _settings.Theme.Scheme == ColorScheme.System
            ? (_themeDetector?.DetectSystemScheme() ?? ColorScheme.Dark)
            : _settings.Theme.Scheme;

        _settingsWindow = new SettingsWindow(
            _settings,
            _settingsPath,
            _hotkeys,
            _hotkeyWindow?.Handle ?? IntPtr.Zero,
            effectiveScheme,
            onSettingsLiveUpdated: RequestLiveSettingsApply,
            onClosed: () => _settingsWindow = null,
            onShowWelcome: ShowWelcome);

        _settingsWindow.Closed += (_, _) => OnSettingsClosed();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // Settings apply while the window is open, but a slider drag or typing
    // raises this per tick or keystroke. Each call used to re-theme every
    // window, reload the history — and evict it: dragging the history limit
    // down and back up deleted everything past the lowest value for good.
    // One apply once the input settles; eviction waits for the window to
    // close (and captures meanwhile honor the settled limit).
    private void RequestLiveSettingsApply()
    {
        if (_liveSettingsTimer is null)
        {
            _liveSettingsTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200),
            };
            _liveSettingsTimer.Tick += (_, _) =>
            {
                _liveSettingsTimer.Stop();
                ApplyLiveSettings();
            };
        }
        _liveSettingsTimer.Stop();
        _liveSettingsTimer.Start();
    }

    private void ApplyLiveSettings()
    {
        UpdateTheme(_themeDetector?.DetectSystemScheme() ?? ColorScheme.Dark);
        ApplyHistoryLimitsToCapture();
        ApplyAutoPaste();
        RefreshOpenPopups();
    }

    // History limits are enforced on every capture, so slider changes must
    // reach the live options before the settings window closes.
    private void ApplyHistoryLimitsToCapture()
    {
        if (_captureOptions is null)
        {
            return;
        }
        var fresh = _settings.ToCaptureOptions();
        _captureOptions.HistoryMaxItems = fresh.HistoryMaxItems;
        _captureOptions.HistoryMaxAgeMinutes = fresh.HistoryMaxAgeMinutes;
        _captureOptions.ProtectPinned = fresh.ProtectPinned;
        _captureOptions.ProtectTagged = fresh.ProtectTagged;
    }

    private void OnSettingsClosed()
    {
        // First: when anything below threw (a locked settings file, a busy
        // database), the field kept the closed window and every later
        // OpenSettings threw on Show() until restart.
        _settingsWindow = null;
        _liveSettingsTimer?.Stop();
        SaveSettingsQuietly();
        try
        {
            ApplyClosedSettings();
        }
        catch (Exception ex)
        {
            LogError("settings-apply", ex);
        }
    }

    private void ApplyClosedSettings()
    {
        // Reapply live:Ctor-held option shapes are mutated in place because
        // the services keep the same references (no restart needed).
        if (_captureOptions is not null)
        {
            var fresh = _settings.ToCaptureOptions();
            _captureOptions.ExcludedProcesses = fresh.ExcludedProcesses;
            _captureOptions.MaxCharacters = fresh.MaxCharacters;
            _captureOptions.UpdateDateOnCopy = fresh.UpdateDateOnCopy;
        }
        ApplyHistoryLimitsToCapture();
        if (_pasteOptions is not null)
        {
            var fresh = _settings.ToPasteOptions();
            _pasteOptions.PasteSequence = fresh.PasteSequence;
            _pasteOptions.PasteDelayMs = fresh.PasteDelayMs;
            _pasteOptions.SwapCopyPaste = fresh.SwapCopyPaste;
            _pasteOptions.AutoPaste = fresh.AutoPaste;
        }
        _store?.Evict(_settings.History.MaxItems, _settings.History.MaxAgeMinutes,
            DateTime.UtcNow, _settings.Behavior.ProtectPinned, _settings.Behavior.ProtectTagged);
        RefreshOpenPopups();
        UpdateTheme(_themeDetector?.DetectSystemScheme() ?? ColorScheme.Dark);
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
            watcher.Error += (_, args) => LogError("actions-watcher", args.GetException());
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
        _compactPopup?.CloseForExit();
        _popup?.CloseForExit();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);
}
