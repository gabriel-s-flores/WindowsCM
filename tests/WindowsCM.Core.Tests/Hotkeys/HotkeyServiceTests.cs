// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Tests.Hotkeys;

// Registrar fake: scriptable per-id errors, records mods+vk so tests prove
// NOREPEAT is always ORed and remaps unregister before reregistering.
internal sealed class FakeRegistrar : IHotkeyRegistrar
{
    public readonly record struct Call(int Id, uint Modifiers, uint Vk);
    public List<Call> Registers = [];
    public List<int> Unregisters = [];
    public Dictionary<int, int> FailWith = [];

    public bool TryRegister(IntPtr hwnd, int id, uint modifiers, uint vk, out int error)
    {
        Registers.Add(new Call(id, modifiers, vk));
        if (FailWith.TryGetValue(id, out error))
        {
            return false;
        }
        error = 0;
        return true;
    }

    public void Unregister(IntPtr hwnd, int id) => Unregisters.Add(id);
}

public sealed class HotkeyServiceTests
{
    private static readonly IntPtr Hwnd = new(4242);

    [Fact]
    public void RegisterAll_RegistersDefaultsWithNoRepeat()
    {
        var registrar = new FakeRegistrar();
        var service = new HotkeyService(registrar, new MemoryHotkeySettings());

        var result = service.RegisterAll(Hwnd);

        Assert.True(result.Open.Registered);
        Assert.True(result.Incognito.Registered);
        Assert.Equal(2, registrar.Registers.Count);
        foreach (var call in registrar.Registers)
        {
            Assert.NotEqual(0u, call.Modifiers & HotkeyDefaults.NoRepeat);
        }
        var open = registrar.Registers.Single(c => c.Id == HotkeyDefaults.IdOpen);
        Assert.Equal((uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift) | HotkeyDefaults.NoRepeat, open.Modifiers);
        Assert.Equal((uint)'V', open.Vk);
        // Unregister-before-reregister: each id is freed before it is taken.
        Assert.Equal(
            [HotkeyDefaults.IdOpen, HotkeyDefaults.IdIncognito],
            registrar.Unregisters);
    }

    [Fact]
    public void Ids_LiveInAppRange_AwayFromControlIds()
    {
        Assert.True(HotkeyDefaults.IdOpen >= 0x8000);
        Assert.True(HotkeyDefaults.IdIncognito >= 0x8000);
        Assert.NotEqual(HotkeyDefaults.IdOpen, HotkeyDefaults.IdIncognito);
    }

    [Fact]
    public void RegisterAll_Occupied_ReportsGuidance()
    {
        var registrar = new FakeRegistrar
        {
            FailWith = { [HotkeyDefaults.IdOpen] = HotkeyDefaults.AlreadyRegistered },
        };
        var service = new HotkeyService(registrar, new MemoryHotkeySettings());

        var result = service.RegisterAll(Hwnd);

        Assert.False(result.Open.Registered);
        Assert.True(result.Open.Occupied);
        Assert.Contains("another app", result.Open.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shortcuts", result.Open.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Incognito.Registered);
    }

    [Fact]
    public void RegisterAll_OtherError_SurfacesWin32Code()
    {
        var registrar = new FakeRegistrar { FailWith = { [HotkeyDefaults.IdIncognito] = 5 } };
        var service = new HotkeyService(registrar, new MemoryHotkeySettings());

        var result = service.RegisterAll(Hwnd);

        Assert.False(result.Incognito.Registered);
        Assert.False(result.Incognito.Occupied);
        Assert.Contains("5", result.Incognito.Diagnostics);
    }

    [Fact]
    public void Remap_Live_PersistsGestureAndReregisters()
    {
        var settings = new MemoryHotkeySettings();
        var registrar = new FakeRegistrar();
        var service = new HotkeyService(registrar, settings);
        service.RegisterAll(Hwnd);
        registrar.Registers.Clear();
        registrar.Unregisters.Clear();

        var outcome = service.Remap(Hwnd, HotkeySlot.Open, HotkeyChord.Parse("Ctrl+Alt+O"));

        Assert.True(outcome.Registered);
        Assert.Equal("Ctrl+Alt+O", settings.OpenGesture);
        Assert.Equal(1, settings.Saves);
        Assert.Equal([HotkeyDefaults.IdOpen], registrar.Unregisters);
        var call = Assert.Single(registrar.Registers);
        Assert.Equal((uint)'O', call.Vk);
    }

    [Fact]
    public void Remap_Occupied_RestoresOldAndKeepsSettings()
    {
        var settings = new MemoryHotkeySettings();
        var registrar = new FakeRegistrar();
        var service = new HotkeyService(registrar, settings);
        service.RegisterAll(Hwnd);
        registrar.FailWith[HotkeyDefaults.IdOpen] = HotkeyDefaults.AlreadyRegistered;
        registrar.Registers.Clear();
        registrar.Unregisters.Clear();

        var outcome = service.Remap(Hwnd, HotkeySlot.Open, HotkeyChord.Parse("Ctrl+Alt+O"));

        Assert.False(outcome.Registered);
        Assert.True(outcome.Occupied);
        Assert.Equal(HotkeyDefaults.OpenGesture, settings.OpenGesture);
        Assert.Equal(0, settings.Saves);
        // New attempt failed, then the old chord was restored best-effort.
        Assert.Equal(2, registrar.Registers.Count);
        Assert.Equal((uint)'O', registrar.Registers[0].Vk);
        Assert.Equal((uint)'V', registrar.Registers[1].Vk);
    }

    [Fact]
    public void Remap_Reserved_NeverTouchesRegistrar()
    {
        var settings = new MemoryHotkeySettings();
        var registrar = new FakeRegistrar();
        var service = new HotkeyService(registrar, settings);

        var outcome = service.Remap(Hwnd, HotkeySlot.Open, HotkeyChord.Parse("Win+V"));

        Assert.False(outcome.Registered);
        Assert.False(outcome.Occupied);
        Assert.Contains("Windows key", outcome.Diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(registrar.Registers);
        Assert.Empty(registrar.Unregisters);
        Assert.Equal(HotkeyDefaults.OpenGesture, settings.OpenGesture);
    }

    [Fact]
    public void CorruptSettings_FallBackToDefaults()
    {
        var settings = new MemoryHotkeySettings { OpenGesture = "nonsense" };
        var service = new HotkeyService(new FakeRegistrar(), settings);

        Assert.Equal(HotkeyDefaults.Open, service.OpenChord);
    }
}
