// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Hotkeys;

// Outcome of one registration attempt. Occupied means another thread
// (usually another app) already owns the chord — only its owner can free
// it, so we never retry Unregister on it (research 03 §1.5).
public sealed record HotkeyOutcome(bool Registered, bool Occupied, string? Diagnostics);

public sealed record HotkeyRegistration(HotkeyOutcome Open, HotkeyOutcome Incognito);

// Per-HWND global chords with app-range IDs. Registration always ORs
// NOREPEAT; re-registration explicitly unregisters first because Win32
// keeps the old chord otherwise (research 03 §1.2). Remaps persist the
// gesture strings only after the new chord registers.
public sealed class HotkeyService
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly IHotkeySettings _settings;

    public HotkeyService(IHotkeyRegistrar registrar, IHotkeySettings settings)
    {
        _registrar = registrar;
        _settings = settings;
    }

    public HotkeyChord OpenChord => ChordOrDefault(_settings.OpenGesture, HotkeyDefaults.OpenGesture);
    public HotkeyChord IncognitoChord => ChordOrDefault(_settings.IncognitoGesture, HotkeyDefaults.IncognitoGesture);

    public HotkeyRegistration RegisterAll(IntPtr hwnd)
    {
        var open = RegisterSlot(hwnd, HotkeySlot.Open, OpenChord);
        var incognito = RegisterSlot(hwnd, HotkeySlot.Incognito, IncognitoChord);
        return new HotkeyRegistration(open, incognito);
    }

    public void UnregisterAll(IntPtr hwnd)
    {
        _registrar.Unregister(hwnd, HotkeyDefaults.IdOpen);
        _registrar.Unregister(hwnd, HotkeyDefaults.IdIncognito);
    }

    // Live remap: validate first (no Win/F12/bare keys), then
    // unregister-before-reregister. When the new chord is occupied the old
    // one is restored best-effort and the settings stay untouched.
    public HotkeyOutcome Remap(IntPtr hwnd, HotkeySlot slot, HotkeyChord chord)
    {
        var invalid = chord.Validate();
        if (invalid is not null)
        {
            return new HotkeyOutcome(false, false, invalid);
        }
        var id = IdFor(slot);
        var current = slot switch
        {
            HotkeySlot.Open => OpenChord,
            HotkeySlot.Incognito => IncognitoChord,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
        if (current.Equals(chord))
        {
            return RegisterSlot(hwnd, slot, chord);
        }
        _registrar.Unregister(hwnd, id);
        var outcome = TryRegister(hwnd, id, chord, slot);
        if (outcome.Registered)
        {
            SetGesture(slot, chord.ToString());
            _settings.Save();
            return outcome;
        }
        if (outcome.Occupied)
        {
            // Best-effort restore so a failed remap never leaves the slot dead.
            TryRegister(hwnd, id, current, slot);
        }
        return outcome;
    }

    // WM_HOTKEY dispatch for the UI WndProc: wParam carries the id.
    public static HotkeySlot? SlotForId(int id) => id switch
    {
        HotkeyDefaults.IdOpen => HotkeySlot.Open,
        HotkeyDefaults.IdIncognito => HotkeySlot.Incognito,
        _ => null,
    };

    private HotkeyOutcome RegisterSlot(IntPtr hwnd, HotkeySlot slot, HotkeyChord chord)
    {
        var invalid = chord.Validate();
        if (invalid is not null)
        {
            return new HotkeyOutcome(false, false, invalid);
        }
        _registrar.Unregister(hwnd, IdFor(slot));
        return TryRegister(hwnd, IdFor(slot), chord, slot);
    }

    private HotkeyOutcome TryRegister(IntPtr hwnd, int id, HotkeyChord chord, HotkeySlot slot)
    {
        var mods = (uint)chord.Modifiers | HotkeyDefaults.NoRepeat;
        if (_registrar.TryRegister(hwnd, id, mods, chord.VirtualKey, out var error))
        {
            return new HotkeyOutcome(true, false, null);
        }
        if (error == HotkeyDefaults.AlreadyRegistered)
        {
            return new HotkeyOutcome(false, true, OccupiedGuidance(slot, chord));
        }
        return new HotkeyOutcome(false, false,
            $"Could not register {SlotName(slot)} ({chord}): Win32 error {error}. " +
            "Pick another combination in Settings > Shortcuts.");
    }

    private static int IdFor(HotkeySlot slot) => slot switch
    {
        HotkeySlot.Open => HotkeyDefaults.IdOpen,
        HotkeySlot.Incognito => HotkeyDefaults.IdIncognito,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static string SlotName(HotkeySlot slot) => slot switch
    {
        HotkeySlot.Open => "Open popup",
        HotkeySlot.Incognito => "Incognito popup",
        _ => "Popup"
    };

    private static string OccupiedGuidance(HotkeySlot slot, HotkeyChord chord) =>
        $"{SlotName(slot)} ({chord}) is already registered by another app — " +
        "only its owner can release it. Pick another combination in Settings > Shortcuts " +
        $"or free it in the other app first (Ctrl+Shift+V also pastes plain text in editors).";

    private void SetGesture(HotkeySlot slot, string gesture)
    {
        switch (slot)
        {
            case HotkeySlot.Open:
                _settings.OpenGesture = gesture;
                break;
            case HotkeySlot.Incognito:
                _settings.IncognitoGesture = gesture;
                break;
        }
    }

    private static HotkeyChord ChordOrDefault(string gesture, string fallback) =>
        HotkeyChord.TryParse(gesture, out var chord) && chord is not null
            ? chord
            : HotkeyChord.Parse(fallback);
}
