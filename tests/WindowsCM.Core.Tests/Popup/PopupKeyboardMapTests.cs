// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class PopupKeyboardMapTests
{
    private static PopupKeyResult? Resolve(PopupKey key, PopupModifiers mods = PopupModifiers.None, bool inSearch = false) =>
        PopupKeyboardMap.Resolve(new PopupKeyEvent(key, mods, inSearch));

    [Theory]
    [InlineData(PopupKey.Up)]
    [InlineData(PopupKey.Left)]
    public void Arrows_MoveSelection(PopupKey key)
    {
        Assert.Equal(PopupAction.MovePrevious, Resolve(key)!.Action);
        Assert.Equal(PopupAction.MovePrevious, Resolve(PopupKey.Up, inSearch: true)!.Action);
    }

    [Theory]
    [InlineData(PopupKey.Down)]
    [InlineData(PopupKey.Right)]
    public void ArrowsDown_MoveNext(PopupKey key)
    {
        Assert.Equal(PopupAction.MoveNext, Resolve(key)!.Action);
    }

    [Fact]
    public void Tab_Moves_ShiftTab_Reverses()
    {
        Assert.Equal(PopupAction.MoveNext, Resolve(PopupKey.Tab)!.Action);
        Assert.Equal(
            PopupAction.MovePrevious,
            Resolve(PopupKey.Tab, PopupModifiers.Shift)!.Action);
    }

    [Fact]
    public void HomeEnd_FirstLast()
    {
        Assert.Equal(PopupAction.MoveFirst, Resolve(PopupKey.Home)!.Action);
        Assert.Equal(PopupAction.MoveLast, Resolve(PopupKey.End)!.Action);
    }

    [Fact]
    public void HomeEnd_InSearch_BelongToTextbox()
    {
        Assert.Null(Resolve(PopupKey.Home, inSearch: true));
        Assert.Null(Resolve(PopupKey.End, inSearch: true));
    }

    [Theory]
    [InlineData(PopupKey.D1, 1)]
    [InlineData(PopupKey.D9, 9)]
    [InlineData(PopupKey.D0, 0)]
    [InlineData(PopupKey.NumPad3, 3)]
    public void CtrlDigits_JumpToSlot(PopupKey key, int slot)
    {
        var result = Resolve(key, PopupModifiers.Ctrl);

        Assert.Equal(PopupAction.JumpToSlot, result!.Action);
        Assert.Equal(slot, result.Slot);
    }

    [Fact]
    public void AltP_TogglesPinsFilter_TheDeviation()
    {
        var result = Resolve(PopupKey.P, PopupModifiers.Alt);

        Assert.Equal(PopupAction.TogglePinsFilter, result!.Action);
    }

    [Fact]
    public void BareAlt_NeverResolves()
    {
        Assert.Null(Resolve(PopupKey.None, PopupModifiers.Alt));
        Assert.Null(Resolve(PopupKey.P));
    }

    [Fact]
    public void CtrlTab_CyclesType_ShiftReverses()
    {
        Assert.Equal(
            PopupAction.CycleTypeNext,
            Resolve(PopupKey.Tab, PopupModifiers.Ctrl)!.Action);
        Assert.Equal(
            PopupAction.CycleTypePrevious,
            Resolve(PopupKey.Tab, PopupModifiers.Ctrl | PopupModifiers.Shift)!.Action);
    }

    [Fact]
    public void CtrlBackquote_Retired_ReturnsNull()
    {
        Assert.Null(Resolve(PopupKey.Oem3, PopupModifiers.Ctrl));
        Assert.Null(Resolve(PopupKey.Oem3, PopupModifiers.Ctrl | PopupModifiers.Shift));
    }

    [Fact]
    public void Delete_ForceOnShift()
    {
        Assert.Equal(PopupAction.Delete, Resolve(PopupKey.Delete)!.Action);
        Assert.Equal(
            PopupAction.DeleteForce,
            Resolve(PopupKey.Delete, PopupModifiers.Shift)!.Action);
    }

    [Fact]
    public void Enter_Space_ActivateWithShiftFlag()
    {
        Assert.Equal(PopupAction.ActivateSelected, Resolve(PopupKey.Enter)!.Action);
        Assert.False(Resolve(PopupKey.Enter)!.ShiftHeld);
        Assert.True(Resolve(PopupKey.Enter, PopupModifiers.Shift)!.ShiftHeld);
        Assert.Equal(PopupAction.ActivateSelected, Resolve(PopupKey.Space)!.Action);
    }

    [Fact]
    public void CtrlEnter_RunsDefaultAction()
    {
        Assert.Equal(
            PopupAction.ActivateDefault,
            Resolve(PopupKey.Enter, PopupModifiers.Ctrl)!.Action);
    }

    [Theory]
    [InlineData(PopupKey.S, PopupAction.TogglePin)]
    [InlineData(PopupKey.E, PopupAction.EditItem)]
    [InlineData(PopupKey.T, PopupAction.EditTitle)]
    [InlineData(PopupKey.A, PopupAction.ShowActionsMenu)]
    [InlineData(PopupKey.F, PopupAction.FocusSearch)]
    public void CtrlLetters_MirrorCopyous(PopupKey key, PopupAction action)
    {
        Assert.Equal(action, Resolve(key, PopupModifiers.Ctrl)!.Action);
    }

    [Fact]
    public void CtrlShiftDigits_Retired_ReturnsNull()
    {
        Assert.Null(Resolve(PopupKey.D3, PopupModifiers.Ctrl | PopupModifiers.Shift));
    }

    [Fact]
    public void Escape_Closes_EvenInSearch()
    {
        Assert.Equal(PopupAction.Close, Resolve(PopupKey.Escape)!.Action);
        Assert.Equal(PopupAction.Close, Resolve(PopupKey.Escape, inSearch: true)!.Action);
    }

    [Theory]
    [InlineData(PopupKey.Enter)]
    [InlineData(PopupKey.Space)]
    [InlineData(PopupKey.Delete)]
    [InlineData(PopupKey.A)]
    public void TextEntryKeys_InSearch_StayWithTextbox(PopupKey key)
    {
        var mods = key == PopupKey.A ? PopupModifiers.Ctrl : PopupModifiers.None;
        Assert.Null(Resolve(key, mods, inSearch: true));
    }

    [Fact]
    public void GlobalChords_WorkInSearch()
    {
        Assert.Equal(
            PopupAction.TogglePin,
            Resolve(PopupKey.S, PopupModifiers.Ctrl, inSearch: true)!.Action);
        Assert.Equal(
            PopupAction.ActivateDefault,
            Resolve(PopupKey.Enter, PopupModifiers.Ctrl, inSearch: true)!.Action);
        Assert.Equal(
            PopupAction.TogglePinsFilter,
            Resolve(PopupKey.P, PopupModifiers.Alt, inSearch: true)!.Action);
        Assert.Equal(
            PopupAction.JumpToSlot,
            Resolve(PopupKey.D2, PopupModifiers.Ctrl, inSearch: true)!.Action);
    }

    [Theory]
    [InlineData(false, false, ScrollDimension.Type)]
    [InlineData(true, false, ScrollDimension.Tag)]
    [InlineData(false, true, ScrollDimension.Tag)]
    [InlineData(true, true, ScrollDimension.Type)]
    public void Scroll_TargetWithSwap(bool ctrl, bool swap, ScrollDimension expected)
    {
        Assert.Equal(expected, PopupScrollMap.ResolveTarget(ctrl, swap));
    }
}
