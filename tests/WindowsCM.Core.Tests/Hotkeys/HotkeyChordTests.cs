// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Tests.Hotkeys;

public sealed class HotkeyChordTests
{
    [Fact]
    public void Defaults_ParseToExpectedModsAndVk()
    {
        var open = HotkeyChord.Parse("Ctrl+Shift+V");
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, open.Modifiers);
        Assert.Equal((uint)'V', open.VirtualKey);

        var incognito = HotkeyChord.Parse("Ctrl+Shift+Alt+V");
        Assert.Equal(
            HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt,
            incognito.Modifiers);
        Assert.Equal((uint)'V', incognito.VirtualKey);
    }

    [Fact]
    public void Defaults_ClassMatchesParsedGestures()
    {
        Assert.Equal(HotkeyChord.Parse(HotkeyDefaults.OpenGesture), HotkeyDefaults.Open);
        Assert.Equal(HotkeyChord.Parse(HotkeyDefaults.IncognitoGesture), HotkeyDefaults.Incognito);
    }

    [Fact]
    public void ToString_CanonicalOrder_RoundTrips()
    {
        Assert.Equal("Ctrl+Shift+V", HotkeyDefaults.Open.ToString());
        Assert.Equal("Ctrl+Shift+Alt+V", HotkeyDefaults.Incognito.ToString());
        Assert.Equal(
            "Ctrl+Shift+V",
            HotkeyChord.Parse("Shift+Ctrl+V").ToString());
    }

    [Theory]
    [InlineData("ctrl+shift+v")]
    [InlineData("CTRL+SHIFT+V")]
    [InlineData(" Control + Shift + V ")]
    public void Parse_IsCaseInsensitiveAndTrims(string gesture)
    {
        Assert.Equal(HotkeyDefaults.Open, HotkeyChord.Parse(gesture));
    }

    [Theory]
    [InlineData("Win+V")]
    [InlineData("Ctrl+Shift+Win+V")]
    [InlineData("Meta+V")]
    public void WinCombos_AreReserved(string gesture)
    {
        var chord = HotkeyChord.Parse(gesture);
        Assert.True(chord.IsReserved);
        Assert.NotNull(chord.Validate());
        Assert.Contains("Windows key", chord.Validate(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void F12_IsReserved()
    {
        var chord = HotkeyChord.Parse("Ctrl+F12");
        Assert.True(chord.IsReserved);
        Assert.Contains("F12", chord.Validate());
    }

    [Fact]
    public void BareKey_RequiresModifier()
    {
        var chord = HotkeyChord.Parse("V");
        Assert.False(chord.IsReserved);
        Assert.Contains("modifier", chord.Validate(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Nope")]
    [InlineData("Ctrl+Shift")]
    public void Garbage_FailsParse(string gesture)
    {
        Assert.False(HotkeyChord.TryParse(gesture, out _));
    }

    [Fact]
    public void FunctionAndNamedKeys_Resolve()
    {
        Assert.True(HotkeyChord.TryParse("Ctrl+F6", out var f6));
        Assert.Equal(0x75u, f6!.VirtualKey);
        Assert.True(HotkeyChord.TryParse("Ctrl+`", out var tick));
        Assert.Equal(0xC0u, tick!.VirtualKey);
        Assert.True(HotkeyChord.TryParse("Ctrl+Oem3", out var oem));
        Assert.Equal(0xC0u, oem!.VirtualKey);
    }

    [Fact]
    public void SlotForId_MapsAppRangeIds()
    {
        Assert.Equal(HotkeySlot.Open, HotkeyService.SlotForId(HotkeyDefaults.IdOpen));
        Assert.Equal(HotkeySlot.Incognito, HotkeyService.SlotForId(HotkeyDefaults.IdIncognito));
        Assert.Null(HotkeyService.SlotForId(999));
    }
}
