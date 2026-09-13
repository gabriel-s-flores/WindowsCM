// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// UI-agnostic key codes for the popup map. The WPF layer translates
// System.Windows.Input.Key (plus Keyboard.Modifiers) into these; bare Alt
// never arrives here — WPF reports it as Key.System and keeps menu-focus
// behavior (research 03 §5 deviation: toggle-pinned moved to Alt+P).
public enum PopupKey
{
    None,
    Up,
    Down,
    Left,
    Right,
    Tab,
    Home,
    End,
    Enter,
    Space,
    Delete,
    A,
    E,
    F,
    P,
    S,
    T,
    D0,
    D1,
    D2,
    D3,
    D4,
    D5,
    D6,
    D7,
    D8,
    D9,
    NumPad0,
    NumPad1,
    NumPad2,
    NumPad3,
    NumPad4,
    NumPad5,
    NumPad6,
    NumPad7,
    NumPad8,
    NumPad9,
    Oem3,
    Escape,
}

[Flags]
public enum PopupModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

public enum PopupAction
{
    MoveNext,
    MovePrevious,
    MoveFirst,
    MoveLast,
    JumpToSlot,
    TogglePinsFilter,
    CycleTypeNext,
    CycleTypePrevious,
    CycleTagNext,
    CycleTagPrevious,
    Delete,
    DeleteForce,
    ActivateSelected,
    ActivateDefault,
    TogglePin,
    EditItem,
    EditTitle,
    ShowActionsMenu,
    FocusSearch,
    ApplyTagSlot,
    Close,
}

public sealed record PopupKeyEvent(PopupKey Key, PopupModifiers Modifiers, bool InSearchBox);

public sealed record PopupKeyResult(PopupAction Action, int Slot = 0, bool ShiftHeld = false);

// Full internal keyboard map (research 03 §5, spec story 34) with the single
// deviation toggle-pinned Alt → Alt+P. Window.InputBindings parity (global
// in the popup, safe inside the single-line search box) vs PreviewKeyDown
// parity (focus-sensitive: Enter/Space/Delete/Ctrl+A stay with the search
// box when it has focus). Returns null when the keystroke belongs to text
// editing. Scroll behavior lives in PopupScrollMap below.
public static class PopupKeyboardMap
{
    public static PopupKeyResult? Resolve(PopupKeyEvent ev)
    {
        var ctrl = ev.Modifiers.HasFlag(PopupModifiers.Ctrl);
        var shift = ev.Modifiers.HasFlag(PopupModifiers.Shift);
        var alt = ev.Modifiers.HasFlag(PopupModifiers.Alt);

        if (ev.Key == PopupKey.Escape && !ctrl && !shift && !alt)
        {
            return new PopupKeyResult(PopupAction.Close);
        }

        if (ev.InSearchBox)
        {
            return ResolveInSearch(ev, ctrl, shift, alt);
        }
        return ResolveInList(ev, ctrl, shift, alt);
    }

    // Inside the search box only popup-global chords apply; everything else
    // is TextBox editing (Enter/Space/Delete/Ctrl+A/Home/End/Tab).
    private static PopupKeyResult? ResolveInSearch(PopupKeyEvent ev, bool ctrl, bool shift, bool alt)
    {
        switch (ev.Key)
        {
            case PopupKey.Up when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MovePrevious);
            case PopupKey.Down when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MoveNext);
            case PopupKey.Left when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MovePrevious);
            case PopupKey.Right when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MoveNext);
            case PopupKey.Enter when ctrl && !alt:
                return new PopupKeyResult(PopupAction.ActivateDefault);
            case PopupKey.S when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.TogglePin);
            case PopupKey.E when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.EditItem);
            case PopupKey.T when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.EditTitle);
            case PopupKey.F when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.FocusSearch);
            case PopupKey.P when alt && !ctrl && !shift:
                return new PopupKeyResult(PopupAction.TogglePinsFilter);
            case PopupKey.Tab when ctrl && !alt:
                return shift
                    ? new PopupKeyResult(PopupAction.CycleTypePrevious)
                    : new PopupKeyResult(PopupAction.CycleTypeNext);
            default:
                break;
        }
        if (ctrl && !alt && !shift && TryDigitSlot(ev.Key, out var slot))
        {
            return new PopupKeyResult(PopupAction.JumpToSlot, slot);
        }
        return null;
    }

    private static PopupKeyResult? ResolveInList(PopupKeyEvent ev, bool ctrl, bool shift, bool alt)
    {
        switch (ev.Key)
        {
            case PopupKey.Up when !ctrl && !alt:
            case PopupKey.Left when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MovePrevious);
            case PopupKey.Down when !ctrl && !alt:
            case PopupKey.Right when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MoveNext);
            case PopupKey.Tab when !ctrl && !alt:
                return shift
                    ? new PopupKeyResult(PopupAction.MovePrevious)
                    : new PopupKeyResult(PopupAction.MoveNext);
            case PopupKey.Home when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MoveFirst);
            case PopupKey.End when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.MoveLast);
            case PopupKey.Enter when !ctrl && !alt:
            case PopupKey.Space when !ctrl && !alt:
                return new PopupKeyResult(PopupAction.ActivateSelected, ShiftHeld: shift);
            case PopupKey.Enter when ctrl && !alt:
                return new PopupKeyResult(PopupAction.ActivateDefault);
            case PopupKey.Delete when !ctrl && !alt:
                return shift
                    ? new PopupKeyResult(PopupAction.DeleteForce)
                    : new PopupKeyResult(PopupAction.Delete);
            case PopupKey.S when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.TogglePin);
            case PopupKey.E when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.EditItem);
            case PopupKey.T when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.EditTitle);
            case PopupKey.A when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.ShowActionsMenu);
            case PopupKey.F when ctrl && !alt && !shift:
                return new PopupKeyResult(PopupAction.FocusSearch);
            case PopupKey.P when alt && !ctrl && !shift:
                return new PopupKeyResult(PopupAction.TogglePinsFilter);
            case PopupKey.Tab when ctrl && !alt:
                return shift
                    ? new PopupKeyResult(PopupAction.CycleTypePrevious)
                    : new PopupKeyResult(PopupAction.CycleTypeNext);
            default:
                break;
        }
        if (ctrl && !alt && !shift && TryDigitSlot(ev.Key, out var slot))
        {
            return new PopupKeyResult(PopupAction.JumpToSlot, slot);
        }
        return null;
    }

    public static bool TryDigitSlot(PopupKey key, out int slot)
    {
        slot = key switch
        {
            PopupKey.D0 or PopupKey.NumPad0 => 0,
            PopupKey.D1 or PopupKey.NumPad1 => 1,
            PopupKey.D2 or PopupKey.NumPad2 => 2,
            PopupKey.D3 or PopupKey.NumPad3 => 3,
            PopupKey.D4 or PopupKey.NumPad4 => 4,
            PopupKey.D5 or PopupKey.NumPad5 => 5,
            PopupKey.D6 or PopupKey.NumPad6 => 6,
            PopupKey.D7 or PopupKey.NumPad7 => 7,
            PopupKey.D8 or PopupKey.NumPad8 => 8,
            PopupKey.D9 or PopupKey.NumPad9 => 9,
            _ => -1,
        };
        return slot >= 0;
    }
}

public enum ScrollDimension
{
    Type,
    Tag,
}

// Wheel parity (research 03 §5): plain scroll cycles the type filter,
// Ctrl+scroll cycles tags; the swap setting inverts both.
public static class PopupScrollMap
{
    public static ScrollDimension ResolveTarget(bool ctrlHeld, bool swap) =>
        (ctrlHeld != swap) ? ScrollDimension.Tag : ScrollDimension.Type;
}
