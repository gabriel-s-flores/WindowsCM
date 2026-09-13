// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Hotkeys;

public sealed class CompactHotkeyConfigurationTests
{
    [Fact]
    public void HotkeySlots_ContainsOnlyOpenAndIncognito()
    {
        var slots = Enum.GetValues<HotkeySlot>();
        Assert.Equal(2, slots.Length);
        Assert.Contains(HotkeySlot.Open, slots);
        Assert.Contains(HotkeySlot.Incognito, slots);
    }

    [Fact]
    public void HotkeyDefaults_OpenGestureIsCtrlShiftV()
    {
        Assert.Equal("Ctrl+Shift+V", HotkeyDefaults.OpenGesture);
        Assert.Equal(0x8000, HotkeyDefaults.IdOpen);
        Assert.Equal(HotkeyChord.Parse("Ctrl+Shift+V"), HotkeyDefaults.Open);
    }

    [Fact]
    public void HotkeyDefaults_IncognitoGestureIsCtrlShiftAltV()
    {
        Assert.Equal("Ctrl+Shift+Alt+V", HotkeyDefaults.IncognitoGesture);
        Assert.Equal(0x8001, HotkeyDefaults.IdIncognito);
        Assert.Equal(HotkeyChord.Parse("Ctrl+Shift+Alt+V"), HotkeyDefaults.Incognito);
    }

    [Fact]
    public void ShortcutSettings_DefaultsToOpenAndIncognito()
    {
        var settings = new ShortcutSettings();
        Assert.Equal("Ctrl+Shift+V", settings.OpenGesture);
        Assert.Equal("Ctrl+Shift+Alt+V", settings.IncognitoGesture);

        settings.OpenGesture = "Ctrl+Alt+O";
        settings.IncognitoGesture = "Ctrl+Alt+I";
        settings.ResetToDefaults();

        Assert.Equal("Ctrl+Shift+V", settings.OpenGesture);
        Assert.Equal("Ctrl+Shift+Alt+V", settings.IncognitoGesture);
    }

    [Fact]
    public void HotkeyService_RegistersExactlyTwoSlots()
    {
        var registrar = new FakeRegistrar();
        var settings = new MemoryHotkeySettings();
        var service = new HotkeyService(registrar, settings);
        var hwnd = new IntPtr(9999);

        var registration = service.RegisterAll(hwnd);

        Assert.True(registration.Open.Registered);
        Assert.True(registration.Incognito.Registered);
        Assert.Equal(2, registrar.Registers.Count);
        Assert.Equal(2, registrar.Unregisters.Count);
    }
}
