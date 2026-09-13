// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

public enum HotkeyRecordStatus
{
    // Still listening; Preview shows what is held right now.
    Listening,
    // Every key was released after a regular key: Chord is the combination.
    Captured,
    // Only modifiers were pressed and released; still listening.
    ModifiersOnly,
    // The regular key cannot be part of a chord (media keys, numpad, ...).
    Unsupported,
    // Bare Esc: recording stopped without a chord.
    Canceled,
}

public sealed record HotkeyRecordStep(HotkeyRecordStatus Status, string Preview, HotkeyChord? Chord = null);

// "Press and release" capture for the settings recorder: the combination is
// the modifiers held while the regular key was down (including ones added
// after it), committed once every key is released. The UI feeds raw
// virtual keys from KeyDown/KeyUp and suspends the global hotkeys meanwhile
// so the current chord reaches the window instead of opening the popup.
public sealed class HotkeyRecorder
{
    private readonly IKeyboardLayout? _layout;
    private readonly HashSet<uint> _down = [];
    private uint? _key;
    private HotkeyModifiers _keyModifiers;
    private HotkeyModifiers _peakModifiers;

    public HotkeyRecorder(IKeyboardLayout? layout)
    {
        _layout = layout;
    }

    public bool IsRecording { get; private set; }

    public void Start()
    {
        Reset();
        IsRecording = true;
    }

    public void Stop()
    {
        Reset();
        IsRecording = false;
    }

    public HotkeyRecordStep KeyDown(uint vk)
    {
        if (!IsRecording)
        {
            return new HotkeyRecordStep(HotkeyRecordStatus.Canceled, string.Empty);
        }
        var modifier = KeyCodes.ModifierFor(vk);
        if (vk == KeyCodes.Escape && _key is null && HeldModifiers() == HotkeyModifiers.None)
        {
            Stop();
            return new HotkeyRecordStep(HotkeyRecordStatus.Canceled, string.Empty);
        }
        if (modifier != HotkeyModifiers.None)
        {
            _down.Add(vk);
            _peakModifiers |= modifier;
            if (_key is uint key && _down.Contains(key))
            {
                _keyModifiers |= modifier;
            }
        }
        else if (!(_key == vk && _down.Contains(vk)))
        {
            // A new regular key (auto-repeat of the held one changes nothing).
            _down.Add(vk);
            _key = vk;
            _keyModifiers = HeldModifiers();
        }
        return new HotkeyRecordStep(HotkeyRecordStatus.Listening, Preview());
    }

    public HotkeyRecordStep KeyUp(uint vk)
    {
        if (!IsRecording)
        {
            return new HotkeyRecordStep(HotkeyRecordStatus.Canceled, string.Empty);
        }
        _down.Remove(vk);
        if (_down.Count > 0)
        {
            return new HotkeyRecordStep(HotkeyRecordStatus.Listening, Preview());
        }
        if (_key is not uint key)
        {
            if (_peakModifiers == HotkeyModifiers.None)
            {
                // Stray release (e.g. the Enter that clicked Record).
                return new HotkeyRecordStep(HotkeyRecordStatus.Listening, string.Empty);
            }
            var held = FormatModifiers(_peakModifiers);
            Reset();
            return new HotkeyRecordStep(HotkeyRecordStatus.ModifiersOnly, held);
        }
        var preview = Preview();
        var chord = HotkeyChord.FromVirtualKey(_keyModifiers, key, _layout);
        Stop();
        return chord is null
            ? new HotkeyRecordStep(HotkeyRecordStatus.Unsupported, preview)
            : new HotkeyRecordStep(HotkeyRecordStatus.Captured, chord.ToString(), chord);
    }

    // "Ctrl+Shift+…" while only modifiers are held, "Ctrl+Shift+Ç" once a key is.
    public string Preview()
    {
        if (_key is uint key)
        {
            var name = KeyCodes.TryName(key, _layout, out var keyName) ? keyName : "?";
            return string.Join("+", HotkeyChord.ModifierNames(_keyModifiers).Append(name));
        }
        var held = HeldModifiers();
        return held == HotkeyModifiers.None ? string.Empty : FormatModifiers(held) + "+…";
    }

    private static string FormatModifiers(HotkeyModifiers modifiers) =>
        string.Join("+", HotkeyChord.ModifierNames(modifiers));

    private HotkeyModifiers HeldModifiers()
    {
        var held = HotkeyModifiers.None;
        foreach (var vk in _down)
        {
            held |= KeyCodes.ModifierFor(vk);
        }
        return held;
    }

    private void Reset()
    {
        _down.Clear();
        _key = null;
        _keyModifiers = HotkeyModifiers.None;
        _peakModifiers = HotkeyModifiers.None;
    }
}
