// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// A parsed global chord: modifiers plus one Win32 virtual-key code.
// NOREPEAT is not part of the chord — the service always ORs it at
// registration time (research 03 §1.4, Win7+).
public sealed record HotkeyChord(HotkeyModifiers Modifiers, uint VirtualKey, string KeyName)
{
    // Win-reserved combos and the debugger-reserved F12 can never be
    // registered (research 03 §1.5–§1.6). Null when the chord is allowed.
    public string? ReservedError()
    {
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            return "Combinations with the Windows key are reserved for the OS " +
                "(Win+V opens the Windows clipboard history). Pick a combination without Win.";
        }
        if (VirtualKey == KeyCodes.F12)
        {
            return "F12 is reserved for the debugger. Pick another key.";
        }
        return null;
    }

    // A bare key with no modifier would swallow normal typing globally.
    public string? Validate()
    {
        var reserved = ReservedError();
        if (reserved is not null)
        {
            return reserved;
        }
        if (Modifiers == HotkeyModifiers.None)
        {
            return "Add at least one modifier (Ctrl, Shift or Alt); a bare key cannot be a global hotkey.";
        }
        return null;
    }

    public bool IsReserved => ReservedError() is not null;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }
        parts.Add(KeyName);
        return string.Join("+", parts);
    }

    public static bool TryParse(string? gesture, out HotkeyChord? chord)
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
        if (!KeyCodes.TryResolve(keyToken, out var vk, out var name))
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
public static class KeyCodes
{
    public const uint F12 = 0x7B;

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
        };

    public static bool TryResolve(string token, out uint vk, out string name)
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
        return false;
    }
}
