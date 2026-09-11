// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// Paste-target capture for every popup show path (ticket 22). The hotkey
// path captures in App.OnHotkey, but tray left-click, the Open/Incognito
// menu entries and the pipe show/toggle commands all flow through
// ITrayPopup — without a capture there the orchestrator would paste with a
// stale hotkey-time handle and misreport focus loss. This decorator captures
// the foreground handle immediately before delegating to the inner popup's
// show, so the orchestrator always compares against the window that was
// focused when the user asked for the popup. Hide never captures.
public sealed class TargetCapturingPopup : ITrayPopup
{
    private readonly ITrayPopup _inner;
    private readonly Func<IntPtr> _captureTarget;
    private readonly Action<IntPtr> _onCaptured;

    public TargetCapturingPopup(
        ITrayPopup inner,
        Func<IntPtr> captureTarget,
        Action<IntPtr> onCaptured)
    {
        _inner = inner;
        _captureTarget = captureTarget;
        _onCaptured = onCaptured;
    }

    public bool IsVisible => _inner.IsVisible;

    public void Show(bool incognito)
    {
        _onCaptured(_captureTarget());
        _inner.Show(incognito);
    }

    public void Hide() => _inner.Hide();

    public void Toggle()
    {
        if (_inner.IsVisible)
        {
            _inner.Hide();
            return;
        }
        _onCaptured(_captureTarget());
        _inner.Toggle();
    }
}
