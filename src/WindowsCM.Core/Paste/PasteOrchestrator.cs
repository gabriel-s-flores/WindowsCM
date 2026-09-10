// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Paste;

// Choosing an item, end to end: plan the clipboard contents, write them,
// record the copy in history (date policy + echo suppression), then —
// unless the chord says copy-only — hide the popup, wait for the target to
// regain focus, assert it is still foreground, and inject the paste chord.
// The target handle is captured by the UI at hotkey time and passed in
// (research 02: never re-resolve it here). Every refusal carries
// diagnostics; nothing fails silently.
public sealed class PasteOrchestrator
{
    private readonly IHistoryStore _store;
    private readonly CaptureService _capture;
    private readonly IImageFileReader _images;
    private readonly IClipboardWriter _writer;
    private readonly IForegroundWindow _foreground;
    private readonly IElevationProbe _elevation;
    private readonly IPasteInjector _injector;
    private readonly IPasteDelay _delay;
    private readonly PasteOptions _options;
    private readonly IClock _clock;

    public PasteOrchestrator(
        IHistoryStore store,
        CaptureService capture,
        IImageFileReader images,
        IClipboardWriter writer,
        IForegroundWindow foreground,
        IElevationProbe elevation,
        IPasteInjector injector,
        IPasteDelay delay,
        PasteOptions options,
        IClock clock)
    {
        _store = store;
        _capture = capture;
        _images = images;
        _writer = writer;
        _foreground = foreground;
        _elevation = elevation;
        _injector = injector;
        _delay = delay;
        _options = options;
        _clock = clock;
    }

    public async Task<PasteOutcome> ExecuteAsync(
        long itemId,
        IntPtr capturedTarget,
        bool shiftHeld,
        Func<Task> hideUi,
        CancellationToken ct = default)
    {
        var intent = CopyPasteChords.Resolve(shiftHeld, _options.SwapCopyPaste);
        var item = _store.List().FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return new PasteOutcome(
                PasteStatus.Failed, null,
                $"Item {itemId} is no longer in history; nothing was copied.");
        }
        var contents = CopyBackPlanner.Plan(item, _images.LoadPng);
        if (contents is null)
        {
            return new PasteOutcome(
                PasteStatus.Failed, null,
                "No writable clipboard content for this item (the image file is missing); nothing was copied.");
        }
        try
        {
            _writer.Write(contents);
        }
        catch (ClipboardWriteException ex)
        {
            return new PasteOutcome(
                PasteStatus.Failed, null, $"Copy-back failed: {ex.Message}");
        }
        _capture.CopiedFromHistory(itemId, _clock.UtcNow);
        if (intent == ActivationIntent.CopyOnly)
        {
            return new PasteOutcome(PasteStatus.CopiedOnly, null, null);
        }
        await hideUi().ConfigureAwait(false);
        await _delay.Delay(_options.PasteDelayMs, ct).ConfigureAwait(false);
        // Foreground first: the captured handle may be stale (target closed
        // or focus moved on), and a stale handle must report focus loss, not
        // a wrong elevation verdict. Only the confirmed handle is probed.
        if (_foreground.GetCurrent() != capturedTarget)
        {
            return new PasteOutcome(
                PasteStatus.CopiedOnlyForegroundLost, null,
                "The target window lost focus before pasting, so the item was only copied. " +
                "If the target runs as administrator, relaunch WindowsCM elevated and try again.");
        }
        // UIPI second: an elevated target swallows SendInput silently, so a
        // predicted refusal beats an injected-into-the-void paste.
        if (!_elevation.IsCurrentProcessElevated() && _elevation.IsTargetElevated(capturedTarget))
        {
            return new PasteOutcome(
                PasteStatus.CopiedOnlyElevated, null,
                "Paste was blocked: the target window runs elevated (administrator). " +
                "The item is on the clipboard; relaunch WindowsCM elevated to paste into it.");
        }
        try
        {
            _injector.Inject(_options.PasteSequence);
        }
        catch (PasteInjectionException ex)
        {
            return new PasteOutcome(
                PasteStatus.Failed, null, $"Paste injection failed after copying: {ex.Message}");
        }
        return new PasteOutcome(PasteStatus.Pasted, _options.PasteSequence, null);
    }
}
