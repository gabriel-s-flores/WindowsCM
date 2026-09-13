// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Tests.Hotkeys;

public sealed class KeyboardLayoutChordTests
{
    [Theory]
    [InlineData("Ctrl+Shift+ç")]
    [InlineData("Ctrl+Shift+Ç")]
    [InlineData("ctrl + shift + Ç")]
    public void Cedilla_ResolvesThroughLayout(string gesture)
    {
        Assert.True(HotkeyChord.TryParse(gesture, FakeKeyboardLayout.Abnt2(), out var chord));
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, chord!.Modifiers);
        Assert.Equal(0xBAu, chord.VirtualKey);
        Assert.Equal("Ctrl+Shift+Ç", chord.ToString());
    }

    [Theory]
    [InlineData('а', (uint)'F')]
    [InlineData('к', (uint)'R')]
    [InlineData('л', (uint)'K')]
    [InlineData('с', (uint)'C')]
    [InlineData('й', (uint)'Q')]
    [InlineData('д', (uint)'L')]
    [InlineData('и', (uint)'B')]
    [InlineData('о', (uint)'J')]
    [InlineData('х', 0xDBu)]
    [InlineData('ш', (uint)'I')]
    public void CyrillicLetters_ResolveToThePhysicalKey(char letter, uint expectedVk)
    {
        Assert.True(HotkeyChord.TryParse($"Ctrl+Alt+{letter}", FakeKeyboardLayout.Russian(), out var chord));
        Assert.Equal(expectedVk, chord!.VirtualKey);
        Assert.Equal($"Ctrl+Alt+{char.ToUpperInvariant(letter)}", chord.ToString());
    }

    [Fact]
    public void NonLatinKey_WithoutLayout_FailsParse()
    {
        Assert.False(HotkeyChord.TryParse("Ctrl+Ç", null, out _));
        Assert.False(HotkeyChord.TryParse("Ctrl+ш", null, out _));
    }

    [Fact]
    public void CharMissingFromLayout_FailsParse()
    {
        Assert.False(HotkeyChord.TryParse("Ctrl+ш", FakeKeyboardLayout.Abnt2(), out _));
    }

    [Fact]
    public void LatinLettersAndDigits_IgnoreLayout()
    {
        // Parsing "Ctrl+F" on a Russian layout still means the F key.
        Assert.True(HotkeyChord.TryParse("Ctrl+F", FakeKeyboardLayout.Russian(), out var f));
        Assert.Equal((uint)'F', f!.VirtualKey);
        Assert.Equal("Ctrl+F", f.ToString());
    }

    [Theory]
    [InlineData("Ctrl+Oem1", 0xBAu)]
    [InlineData("Ctrl+OemPlus", 0xBBu)]
    [InlineData("Ctrl+AbntC1", 0xC1u)]
    [InlineData("Ctrl+Oem102", 0xE2u)]
    public void OemNames_ParseWithoutLayout(string gesture, uint expectedVk)
    {
        Assert.True(HotkeyChord.TryParse(gesture, null, out var chord));
        Assert.Equal(expectedVk, chord!.VirtualKey);
    }

    [Fact]
    public void FromVirtualKey_NamesKeyAfterWhatTheLayoutTypes()
    {
        var abnt2 = FakeKeyboardLayout.Abnt2();
        Assert.Equal("Ctrl+Shift+Ç", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0xBA, abnt2)!.ToString());
        Assert.Equal("Ctrl+´", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xDB, abnt2)!.ToString());
        Assert.Equal("Ctrl+/", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xC1, abnt2)!.ToString());

        var russian = FakeKeyboardLayout.Russian();
        Assert.Equal("Alt+А", HotkeyChord.FromVirtualKey(HotkeyModifiers.Alt, 'F', russian)!.ToString());
        Assert.Equal("Alt+Ж", HotkeyChord.FromVirtualKey(HotkeyModifiers.Alt, 0xBA, russian)!.ToString());
    }

    [Fact]
    public void FromVirtualKey_FallsBackToFixedNames()
    {
        // Latin letters stay Latin; digits and F-keys never use the layout.
        Assert.Equal("Ctrl+V", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 'V', FakeKeyboardLayout.Abnt2())!.ToString());
        Assert.Equal("Ctrl+2", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, '2', null)!.ToString());
        Assert.Equal("Ctrl+F6", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0x75, null)!.ToString());
        Assert.Equal("Ctrl+Space", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0x20, null)!.ToString());
        Assert.Equal("Ctrl+Oem1", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xBA, null)!.ToString());
        Assert.Equal("Ctrl+`", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xC0, null)!.ToString());
    }

    [Fact]
    public void FromVirtualKey_PlusKeyUsesOemName()
    {
        // "Ctrl++" could not be parsed back, so the + key keeps its OEM name.
        var chord = HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xBB, FakeKeyboardLayout.German())!;
        Assert.Equal("Ctrl+OemPlus", chord.ToString());
    }

    [Fact]
    public void FromVirtualKey_RejectsNamesThatParseToAnotherKey()
    {
        // "`" always parses to OEM_3, so a layout typing ` on another key
        // (UK: OEM_8) must fall back to that key's OEM name.
        var layout = new FakeKeyboardLayout(new() { [0xDF] = '`' });
        Assert.Equal("Ctrl+Oem8", HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, 0xDF, layout)!.ToString());
    }

    [Theory]
    [InlineData(0x10u)]
    [InlineData(0xA2u)]
    [InlineData(0x5Bu)]
    [InlineData(0xADu)]
    [InlineData(0x60u)]
    public void FromVirtualKey_UnsupportedKeys_ReturnNull(uint vk)
    {
        Assert.Null(HotkeyChord.FromVirtualKey(HotkeyModifiers.Control, vk, FakeKeyboardLayout.Abnt2()));
    }

    [Theory]
    [InlineData(0xBAu)]
    [InlineData(0xDBu)]
    [InlineData(0xC1u)]
    [InlineData(0xC0u)]
    [InlineData((uint)'F')]
    [InlineData((uint)'2')]
    public void RecordedChords_RoundTripThroughTheirGesture(uint vk)
    {
        foreach (var layout in new IKeyboardLayout?[] { null, FakeKeyboardLayout.Abnt2(), FakeKeyboardLayout.Russian(), FakeKeyboardLayout.German() })
        {
            var recorded = HotkeyChord.FromVirtualKey(HotkeyModifiers.Control | HotkeyModifiers.Alt, vk, layout)!;
            Assert.True(HotkeyChord.TryParse(recorded.ToString(), layout, out var parsed), recorded.ToString());
            Assert.Equal(recorded, parsed);
        }
    }

    [Theory]
    [InlineData("Win+Ç", HotkeyProblem.WinKeyReserved)]
    [InlineData("Ctrl+F12", HotkeyProblem.F12Reserved)]
    [InlineData("Ç", HotkeyProblem.NeedsModifier)]
    public void FindProblem_ClassifiesInvalidChords(string gesture, HotkeyProblem expected)
    {
        Assert.True(HotkeyChord.TryParse(gesture, FakeKeyboardLayout.Abnt2(), out var chord));
        Assert.Equal(expected, chord!.FindProblem());
    }

    [Fact]
    public void FindProblem_NullForValidChord()
    {
        Assert.True(HotkeyChord.TryParse("Ctrl+Alt+Ç", FakeKeyboardLayout.Abnt2(), out var chord));
        Assert.Null(chord!.FindProblem());
        Assert.Null(chord.Validate());
    }
}
