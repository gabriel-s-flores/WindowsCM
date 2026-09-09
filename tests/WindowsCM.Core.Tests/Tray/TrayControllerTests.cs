// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Tests.Tray;

public sealed class TrayControllerTests
{
    private sealed class FakePopup : ITrayPopup
    {
        public bool IsVisible { get; private set; }
        public List<bool> Shows = [];
        public int Hides;
        public int Toggles;
        public void Show(bool incognito)
        {
            Shows.Add(incognito);
            IsVisible = true;
        }
        public void Hide()
        {
            Hides++;
            IsVisible = false;
        }
        public void Toggle()
        {
            Toggles++;
            IsVisible = !IsVisible;
        }
    }

    private sealed class FakeIncognito : IIncognitoToggle
    {
        public bool IsIncognito { get; private set; }
        public List<bool> Sets = [];
        public void SetIncognito(bool on)
        {
            Sets.Add(on);
            IsIncognito = on;
        }
    }

    private sealed class FakeHistory : IClearHistory
    {
        public int Calls;
        public int ClearKeepProtected()
        {
            Calls++;
            return 3;
        }
    }

    private sealed class FakeSettings : ISettingsOpener
    {
        public int Opens;
        public void OpenSettings() => Opens++;
    }

    private sealed class FakeExit : IAppExiter
    {
        public int Exits;
        public void RequestExit() => Exits++;
    }

    private readonly FakePopup _popup = new();
    private readonly FakeIncognito _incognito = new();
    private readonly FakeHistory _history = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeExit _exit = new();

    private TrayController Subject() => new(_popup, _incognito, _history, _settings, _exit);

    [Fact]
    public void Menu_HasFiveItems_InOrder()
    {
        Assert.Equal(
            [TrayMenuItem.Open, TrayMenuItem.Incognito, TrayMenuItem.Clear, TrayMenuItem.Settings, TrayMenuItem.Exit],
            TrayMenu.All);
    }

    [Fact]
    public void Menu_Clear_KeepsPinsAndTagsByLabel()
    {
        Assert.Contains("keep pins", TrayMenu.LabelFor(TrayMenuItem.Clear), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeftClick_TogglesPopup()
    {
        Subject().OnLeftClick();
        Subject().OnLeftClick();

        Assert.Equal(2, _popup.Toggles);
        Assert.False(_popup.IsVisible);
    }

    [Fact]
    public void DoubleClick_AliasesSingleToggle()
    {
        Subject().OnDoubleClick();

        Assert.Equal(1, _popup.Toggles);
        Assert.True(_popup.IsVisible);
    }

    [Fact]
    public void MenuOpen_ShowsNonIncognito()
    {
        Subject().OnMenu(TrayMenuItem.Open);

        Assert.Equal([false], _popup.Shows);
        Assert.Empty(_incognito.Sets);
    }

    [Fact]
    public void MenuIncognito_SetsFlagThenShows()
    {
        Subject().OnMenu(TrayMenuItem.Incognito);

        Assert.Equal([true], _incognito.Sets);
        Assert.True(_incognito.IsIncognito);
        Assert.Equal([true], _popup.Shows);
    }

    [Fact]
    public void MenuClear_KeepsProtected()
    {
        Subject().OnMenu(TrayMenuItem.Clear);

        Assert.Equal(1, _history.Calls);
        Assert.Empty(_popup.Shows);
    }

    [Fact]
    public void MenuSettings_Opens()
    {
        Subject().OnMenu(TrayMenuItem.Settings);

        Assert.Equal(1, _settings.Opens);
    }

    [Fact]
    public void MenuExit_RequestsExit()
    {
        Subject().OnMenu(TrayMenuItem.Exit);

        Assert.Equal(1, _exit.Exits);
    }
}
