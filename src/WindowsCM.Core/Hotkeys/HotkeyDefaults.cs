// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Hotkeys;

// Global-chord defaults (research 03 §2): open under the cursor, incognito
// on the four-modifier chord. Win+V-style OS combos stay excluded by rule
// (HotkeyChord rejects Win). IDs live in the per-HWND app range 0x8000+:
// low IDs collide with common control/command IDs on the same window.
public static class HotkeyDefaults
{
    public const int IdOpen = 0x8000;
    public const int IdIncognito = 0x8001;

    // Always ORed at registration so holding the chord never fires N times
    // (research 03 §1.4, Win7+).
    public const uint NoRepeat = 0x4000;

    // winerror.h ERROR_HOTKEY_ALREADY_REGISTERED (research 03 §6.1 to
    // confirm at implementation time — confirmed: 1409).
    public const int AlreadyRegistered = 1409;

    public const string OpenGesture = "Ctrl+Shift+V";
    public const string IncognitoGesture = "Ctrl+Shift+Alt+V";

    public static HotkeyChord Open => HotkeyChord.Parse(OpenGesture);
    public static HotkeyChord Incognito => HotkeyChord.Parse(IncognitoGesture);
}

public enum HotkeySlot
{
    Open,
    Incognito,
}
