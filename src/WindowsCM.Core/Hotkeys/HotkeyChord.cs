// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// Why a chord cannot be a global hotkey; the UI maps it to localized text.
public enum HotkeyProblem
{
    WinKeyReserved,
    F12Reserved,
    NeedsModifier,
}

// A parsed global chord: modifiers plus one Win32 virtual-key code.
// NOREPEAT is not part of the chord — the service always ORs it at
// registration time (research 03 §1.4, Win7+).
public sealed record HotkeyChord(HotkeyModifiers Modifiers, uint VirtualKey, string KeyName)
{
    // Null when the chord is allowed as a global hotkey.
    public HotkeyProblem? FindProblem()
    {
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            return HotkeyProblem.WinKeyReserved;
        }
        if (VirtualKey == KeyCodes.F12)
        {
            return HotkeyProblem.F12Reserved;
        }
        if (Modifiers == HotkeyModifiers.None)
        {
            return HotkeyProblem.NeedsModifier;
        }
        return null;
    }

    // Win-reserved combos and the debugger-reserved F12 can never be
    // registered (research 03 §1.5–§1.6). Null when the chord is allowed.
    public string? ReservedError() => FindProblem() switch
    {
        HotkeyProblem.WinKeyReserved =>
            "Combinations with the Windows key are reserved for the OS " +
            "(Win+V opens the Windows clipboard history). Pick a combination without Win.",
        HotkeyProblem.F12Reserved => "F12 is reserved for the debugger. Pick another key.",
        _ => null,
    };

    // A bare key with no modifier would swallow normal typing globally.
    public string? Validate() => FindProblem() switch
    {
        HotkeyProblem.NeedsModifier =>
            "Add at least one modifier (Ctrl, Shift or Alt); a bare key cannot be a global hotkey.",
        _ => ReservedError(),
    };

    public bool IsReserved => ReservedError() is not null;

    public override string ToString() =>
        string.Join("+", ModifierNames(Modifiers).Append(KeyName));

    // Canonical order used by ToString and the shortcut recorder preview.
    public static IEnumerable<string> ModifierNames(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            yield return "Ctrl";
        }
        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            yield return "Shift";
        }
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            yield return "Alt";
        }
        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            yield return "Win";
        }
    }

    // A chord for a key the user pressed, named so it parses back to the
    // same virtual key. Null when the key cannot be part of a chord.
    public static HotkeyChord? FromVirtualKey(HotkeyModifiers modifiers, uint virtualKey, IKeyboardLayout? layout) =>
        KeyCodes.TryName(virtualKey, layout, out var name)
            ? new HotkeyChord(modifiers, virtualKey, name)
            : null;

    public static bool TryParse(string? gesture, out HotkeyChord? chord) =>
        TryParse(gesture, KeyCodes.Layout, out chord);

    public static bool TryParse(string? gesture, IKeyboardLayout? layout, out HotkeyChord? chord)
    {
        chord = null;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }
        var tokens = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }
        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            switch (tokens[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "win":
                case "windows":
                case "super":
                case "meta":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    return false;
            }
        }
        var keyToken = tokens[^1];
        if (!KeyCodes.TryResolve(keyToken, layout, out var vk, out var name))
        {
            return false;
        }
        chord = new HotkeyChord(modifiers, vk, name);
        return true;
    }

    public static HotkeyChord Parse(string gesture)
    {
        if (!TryParse(gesture, out var chord) || chord is null)
        {
            throw new FormatException($"Cannot parse hotkey gesture '{gesture}'. Use like 'Ctrl+Shift+V'.");
        }
        return chord;
    }
}

// Virtual-key codes for the keys a global chord may use. Letters and
// digits match their ASCII codes; the named values are the Win32 VKs.
// Any other single character (Ç, Cyrillic, ...) resolves through the
// keyboard layout.
public static class KeyCodes
{
    public const uint F12 = 0x7B;
    public const uint Escape = 0x1B;

    // Set once at startup (Win32KeyboardLayout); null keeps lookups to the
    // fixed tables below.
    public static IKeyboardLayout? Layout { get; set; }

    private static readonly Dictionary<string, (uint Vk, string Name)> Named =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = (0x20, "Space"),
            ["tab"] = (0x09, "Tab"),
            ["enter"] = (0x0D, "Enter"),
            ["return"] = (0x0D, "Enter"),
            ["esc"] = (0x1B, "Esc"),
            ["escape"] = (0x1B, "Esc"),
            ["backspace"] = (0x08, "Backspace"),
            ["ins"] = (0x2D, "Insert"),
            ["insert"] = (0x2D, "Insert"),
            ["del"] = (0x2E, "Delete"),
            ["delete"] = (0x2E, "Delete"),
            ["home"] = (0x24, "Home"),
            ["end"] = (0x23, "End"),
            ["pgup"] = (0x21, "PageUp"),
            ["pageup"] = (0x21, "PageUp"),
            ["pgdn"] = (0x22, "PageDown"),
            ["pagedown"] = (0x22, "PageDown"),
            ["left"] = (0x25, "Left"),
            ["up"] = (0x26, "Up"),
            ["right"] = (0x27, "Right"),
            ["down"] = (0x28, "Down"),
            ["`"] = (0xC0, "`"),
            ["backquote"] = (0xC0, "`"),
            ["oem3"] = (0xC0, "`"),
            // Layout-independent names for the punctuation/OEM keys.
            ["oem1"] = (0xBA, "Oem1"),
            ["oemplus"] = (0xBB, "OemPlus"),
            ["oemcomma"] = (0xBC, "OemComma"),
            ["oemminus"] = (0xBD, "OemMinus"),
            ["oemperiod"] = (0xBE, "OemPeriod"),
            ["oem2"] = (0xBF, "Oem2"),
            ["abntc1"] = (0xC1, "AbntC1"),
            ["abntc2"] = (0xC2, "AbntC2"),
            ["oem4"] = (0xDB, "Oem4"),
            ["oem5"] = (0xDC, "Oem5"),
            ["oem6"] = (0xDD, "Oem6"),
            ["oem7"] = (0xDE, "Oem7"),
            ["oem8"] = (0xDF, "Oem8"),
            ["oem102"] = (0xE2, "Oem102"),
        };

    private static readonly Dictionary<uint, string> NameByVk = BuildNameByVk();

    // OEM keys type different characters per layout (Ç, ~, ´, Ж, ...).
    private static readonly HashSet<uint> OemKeys =
        [0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xC1, 0xC2, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE2];

    private static Dictionary<uint, string> BuildNameByVk()
    {
        var byVk = new Dictionary<uint, string>();
        foreach (var (vk, name) in Named.Values)
        {
            byVk.TryAdd(vk, name);
        }
        return byVk;
    }

    public static bool TryResolve(string token, out uint vk, out string name) =>
        TryResolve(token, Layout, out vk, out name);

    public static bool TryResolve(string token, IKeyboardLayout? layout, out uint vk, out string name)
    {
        vk = 0;
        name = token;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }
        var text = token.Trim();
        if (text.Length == 1)
        {
            var upper = char.ToUpperInvariant(text[0]);
            if (upper is >= 'A' and <= 'Z')
            {
                vk = upper;
                name = upper.ToString();
                return true;
            }
            if (upper is >= '0' and <= '9')
            {
                vk = upper;
                name = upper.ToString();
                return true;
            }
        }
        if (Named.TryGetValue(text, out var named))
        {
            vk = named.Vk;
            name = named.Name;
            return true;
        }
        if (text.StartsWith("f", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text[1..], out var f) && f is >= 1 and <= 24)
        {
            vk = (uint)(0x6F + f);
            name = "F" + f;
            return true;
        }
        if (text.Length == 1 && layout?.VirtualKeyForChar(text[0]) is uint layoutVk)
        {
            vk = layoutVk;
            name = char.ToUpperInvariant(text[0]).ToString();
            return true;
        }
        return false;
    }

    // Display name for a pressed key: the character it types on the active
    // layout when that parses back to the same key (Ç, Ж), else the fixed
    // name (V, 5, F6, Space, Oem1). False for keys a chord cannot use
    // (modifiers, media keys, ...).
    public static bool TryName(uint vk, IKeyboardLayout? layout, out string name)
    {
        name = string.Empty;
        if (vk is >= 'A' and <= 'Z')
        {
            // Latin layouts keep the plain letter; Cyrillic/Greek/... show theirs.
            name = LayoutName(vk, layout, nonAsciiLetterOnly: true) ?? ((char)vk).ToString();
            return true;
        }
        if (vk is >= '0' and <= '9')
        {
            name = ((char)vk).ToString();
            return true;
        }
        if (vk is >= 0x70 and <= 0x87)
        {
            name = "F" + (vk - 0x6F);
            return true;
        }
        if (OemKeys.Contains(vk))
        {
            name = LayoutName(vk, layout, nonAsciiLetterOnly: false) ?? NameByVk[vk];
            return true;
        }
        return NameByVk.TryGetValue(vk, out name!);
    }

    public static bool IsModifierKey(uint vk) => ModifierFor(vk) != HotkeyModifiers.None;

    // Generic, left and right variants of Shift/Ctrl/Alt plus both Win keys.
    public static HotkeyModifiers ModifierFor(uint vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,
        0x5B or 0x5C => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };

    private static string? LayoutName(uint vk, IKeyboardLayout? layout, bool nonAsciiLetterOnly)
    {
        if (layout?.CharForVirtualKey(vk) is not char c)
        {
            return null;
        }
        // '+' is the gesture separator and blanks trim away: neither can be a token.
        if (c == '+' || char.IsWhiteSpace(c) || char.IsControl(c))
        {
            return null;
        }
        if (nonAsciiLetterOnly && (c < 0x80 || !char.IsLetter(c)))
        {
            return null;
        }
        var name = char.ToUpperInvariant(c).ToString();
        return TryResolve(name, layout, out var back, out _) && back == vk ? name : null;
    }
}
