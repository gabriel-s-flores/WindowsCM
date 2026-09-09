// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Settings;

public enum MiddleClickAction
{
    None = 0,
    Pin = 1,
    Delete = 2,
}

public enum OpenBehavior
{
    Toggle = 0,
    OpenOrSelectNext = 1,
}

// Shortcuts screens (Copyous Shortcuts parity, 01 §5): the persisted map
// plus the hardcoded popup map. Global chords (open/incognito) validate
// like HotkeyService (no Win/F12/bare keys); in-popup chords only need to
// parse (bare Delete is the default). Middle-click pin and both swaps are
// kept (grilling 06 Q15).
public sealed class ShortcutSettings
{
    public string OpenGesture { get; set; } = HotkeyDefaults.OpenGesture;
    public string IncognitoGesture { get; set; } = HotkeyDefaults.IncognitoGesture;
    public string PinGesture { get; set; } = "Ctrl+S";
    public string DeleteGesture { get; set; } = "Delete";
    public string EditGesture { get; set; } = "Ctrl+E";
    public string EditTitleGesture { get; set; } = "Ctrl+T";
    public string OpenMenuGesture { get; set; } = "Ctrl+A";
    public MiddleClickAction MiddleClick { get; set; } = MiddleClickAction.Pin;
    public bool SwapCopy { get; set; } = false;
    public bool SwapScroll { get; set; } = false;
    public OpenBehavior OpenBehavior { get; set; } = OpenBehavior.Toggle;

    public void ResetToDefaults()
    {
        var fresh = new ShortcutSettings();
        OpenGesture = fresh.OpenGesture;
        IncognitoGesture = fresh.IncognitoGesture;
        PinGesture = fresh.PinGesture;
        DeleteGesture = fresh.DeleteGesture;
        EditGesture = fresh.EditGesture;
        EditTitleGesture = fresh.EditTitleGesture;
        OpenMenuGesture = fresh.OpenMenuGesture;
        MiddleClick = fresh.MiddleClick;
        SwapCopy = fresh.SwapCopy;
        SwapScroll = fresh.SwapScroll;
        OpenBehavior = fresh.OpenBehavior;
    }

    // Null when the gesture can be a global hotkey.
    public static string? ValidateGlobalGesture(string? gesture)
    {
        if (!HotkeyChord.TryParse(gesture, out var chord) || chord is null)
        {
            return $"Cannot parse hotkey gesture '{gesture}'. Use like 'Ctrl+Shift+V'.";
        }
        return chord.Validate();
    }

    // Null when the gesture can be an in-popup shortcut (bare keys allowed).
    public static string? ValidateLocalGesture(string? gesture) =>
        HotkeyChord.TryParse(gesture, out _) ? null : $"Cannot parse shortcut '{gesture}'.";
}

public sealed record ShortcutEntry(string Group, string Action, string Gesture, bool Customizable);

// The full shortcut map the settings window shows: persisted rows bound to
// ShortcutSettings plus the hardcoded popup rows (PopupKeyboardMap,
// CopyPasteChords and the scroll map parity). Labels are English in v1.
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutEntry> Persisted(ShortcutSettings settings) =>
    [
        new("Global", "Open history", settings.OpenGesture, true),
        new("Global", "Open incognito history", settings.IncognitoGesture, true),
        new("Global", "Open behavior (toggle / open-or-select-next)", settings.OpenBehavior.ToString(), true),
        new("Item", "Pin item", settings.PinGesture, true),
        new("Item", "Delete item", settings.DeleteGesture, true),
        new("Item", "Edit item", settings.EditGesture, true),
        new("Item", "Edit title", settings.EditTitleGesture, true),
        new("Item", "Open actions menu", settings.OpenMenuGesture, true),
        new("Item", "Middle-click action (none / pin / delete)", settings.MiddleClick.ToString(), true),
        new("Item", "Swap copy/paste shortcuts", settings.SwapCopy ? "On" : "Off", true),
        new("Search", "Swap scroll shortcuts", settings.SwapScroll ? "On" : "Off", true),
    ];

    public static IReadOnlyList<ShortcutEntry> Hardcoded() =>
    [
        new("Activation", "Copy and paste (or copy only with Shift)", "Enter / Space", false),
        new("Activation", "Run default action", "Ctrl+Enter", false),
        new("Navigation", "Move selection", "Arrows / Tab / Home / End", false),
        new("Navigation", "Jump to item 1..9, 0 is 10th", "Ctrl+0..9", false),
        new("Search", "Filter pinned items only", "Alt+P", false),
        new("Search", "Cycle item type", "Ctrl+Tab", false),
        new("Search", "Cycle tag", "Ctrl+`", false),
        new("Search", "Apply tag slot", "Ctrl+Shift+0..9", false),
        new("Item", "Delete (Shift forces pinned)", "Delete", false),
        new("Popup", "Close", "Esc", false),
        new("Search", "Cycle type (scroll) / tag (Ctrl+scroll)", "Mouse wheel", false),
    ];
}
