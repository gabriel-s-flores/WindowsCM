// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// Popup visibility behind tray gestures. Production drives the WPF popup
// window (H.NotifyIcon TaskbarIcon with LeftClickCommand = toggle,
// MenuActivation = RightClick); tests fake it.
public interface ITrayPopup
{
    bool IsVisible { get; }
    void Show(bool incognito);
    void Hide();
    void Toggle();
}

// Incognito state behind the tray toggle. Production flips the in-memory
// flag on CaptureService (never persisted) and mirrors it to the popup
// header; tests fake it.
public interface IIncognitoToggle
{
    bool IsIncognito { get; }
    void SetIncognito(bool on);
}

// Tray Clear: protected items survive (pins+tags kept, spec story 17).
// Production calls store.Clear(keepProtected: true); tests record it.
public interface IClearHistory
{
    int ClearKeepProtected();
}

public interface ISettingsOpener
{
    void OpenSettings();
}

public interface IAppExiter
{
    void RequestExit();
}
