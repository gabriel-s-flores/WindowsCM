// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// Tray gestures (research 03 §3.3, spec story 36): left-click toggles the
// popup, double-click aliases single (touch/discovery penalize double as
// the only path), right-click opens the five-item menu (the UI binds
// MenuActivation=RightClick; this controller dispatches the choice).
public sealed class TrayController
{
    private readonly ITrayPopup _popup;
    private readonly IIncognitoToggle _incognito;
    private readonly IClearHistory _history;
    private readonly ISettingsOpener _settings;
    private readonly IAppExiter _exit;

    public TrayController(
        ITrayPopup popup,
        IIncognitoToggle incognito,
        IClearHistory history,
        ISettingsOpener settings,
        IAppExiter exit)
    {
        _popup = popup;
        _incognito = incognito;
        _history = history;
        _settings = settings;
        _exit = exit;
    }

    public void OnLeftClick() => _popup.Toggle();

    public void OnDoubleClick() => _popup.Toggle();

    public void OnMenu(TrayMenuItem item)
    {
        switch (item)
        {
            case TrayMenuItem.Open:
                _popup.Show(incognito: false);
                break;
            case TrayMenuItem.Incognito:
                _incognito.SetIncognito(true);
                _popup.Show(incognito: true);
                break;
            case TrayMenuItem.Clear:
                _history.ClearKeepProtected();
                break;
            case TrayMenuItem.Settings:
                _settings.OpenSettings();
                break;
            case TrayMenuItem.Exit:
                _exit.RequestExit();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }
}
