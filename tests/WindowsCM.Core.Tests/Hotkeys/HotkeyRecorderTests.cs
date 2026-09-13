// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Hotkeys;

namespace WindowsCM.Core.Tests.Hotkeys;

public sealed class HotkeyRecorderTests
{
    private const uint LCtrl = 0xA2;
    private const uint RCtrl = 0xA3;
    private const uint LShift = 0xA0;
    private const uint LAlt = 0xA4;
    private const uint LWin = 0x5B;
    private const uint Esc = 0x1B;
    private const uint OemCedilla = 0xBA;
    private const uint VolumeMute = 0xAD;

    private static HotkeyRecorder Started(IKeyboardLayout? layout = null)
    {
        var recorder = new HotkeyRecorder(layout ?? FakeKeyboardLayout.Abnt2());
        recorder.Start();
        return recorder;
    }

    [Fact]
    public void PressAndRelease_CapturesChordOnLastRelease()
    {
        var recorder = Started();

        Assert.Equal("Ctrl+…", recorder.KeyDown(LCtrl).Preview);
        Assert.Equal("Ctrl+Shift+…", recorder.KeyDown(LShift).Preview);
        var held = recorder.KeyDown(OemCedilla);
        Assert.Equal(HotkeyRecordStatus.Listening, held.Status);
        Assert.Equal("Ctrl+Shift+Ç", held.Preview);

        Assert.Equal(HotkeyRecordStatus.Listening, recorder.KeyUp(OemCedilla).Status);
        Assert.Equal(HotkeyRecordStatus.Listening, recorder.KeyUp(LShift).Status);
        var done = recorder.KeyUp(LCtrl);

        Assert.Equal(HotkeyRecordStatus.Captured, done.Status);
        Assert.Equal("Ctrl+Shift+Ç", done.Preview);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, done.Chord!.Modifiers);
        Assert.Equal(OemCedilla, done.Chord.VirtualKey);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void ReleasingModifiersFirst_KeepsThemInTheChord()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown(LAlt);
        recorder.KeyDown('V');
        recorder.KeyUp(LCtrl);
        recorder.KeyUp(LAlt);

        var done = recorder.KeyUp('V');

        Assert.Equal("Ctrl+Alt+V", done.Chord!.ToString());
    }

    [Fact]
    public void ModifierAddedWhileKeyHeld_JoinsTheChord()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown('V');
        Assert.Equal("Ctrl+Shift+V", recorder.KeyDown(LShift).Preview);
        recorder.KeyUp('V');
        recorder.KeyUp(LShift);

        Assert.Equal("Ctrl+Shift+V", recorder.KeyUp(LCtrl).Chord!.ToString());
    }

    [Fact]
    public void AutoRepeat_DoesNotDropReleasedModifiers()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown('V');
        recorder.KeyUp(LCtrl);
        Assert.Equal("Ctrl+V", recorder.KeyDown('V').Preview);

        Assert.Equal("Ctrl+V", recorder.KeyUp('V').Chord!.ToString());
    }

    [Fact]
    public void LastRegularKeyWins()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown('V');
        recorder.KeyDown('A');
        recorder.KeyUp('V');
        recorder.KeyUp('A');

        Assert.Equal("Ctrl+A", recorder.KeyUp(LCtrl).Chord!.ToString());
    }

    [Fact]
    public void LeftAndRightModifiers_MapToTheSameFlag()
    {
        var recorder = Started();
        recorder.KeyDown(RCtrl);
        recorder.KeyDown('V');
        recorder.KeyUp('V');

        Assert.Equal(HotkeyModifiers.Control, recorder.KeyUp(RCtrl).Chord!.Modifiers);
    }

    [Fact]
    public void CyrillicLayout_PreviewShowsTypedLetter()
    {
        var recorder = Started(FakeKeyboardLayout.Russian());
        recorder.KeyDown(LCtrl);
        Assert.Equal("Ctrl+Ш", recorder.KeyDown('I').Preview);
        recorder.KeyUp('I');

        var done = recorder.KeyUp(LCtrl);
        Assert.Equal("Ctrl+Ш", done.Chord!.ToString());
        Assert.Equal((uint)'I', done.Chord.VirtualKey);
    }

    [Fact]
    public void ModifiersOnly_ReportsAndKeepsListening()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown(LShift);
        recorder.KeyUp(LShift);

        var step = recorder.KeyUp(LCtrl);

        Assert.Equal(HotkeyRecordStatus.ModifiersOnly, step.Status);
        Assert.Equal("Ctrl+Shift", step.Preview);
        Assert.True(recorder.IsRecording);

        // The next attempt starts clean.
        recorder.KeyDown(LAlt);
        recorder.KeyDown('V');
        recorder.KeyUp('V');
        Assert.Equal("Alt+V", recorder.KeyUp(LAlt).Chord!.ToString());
    }

    [Fact]
    public void BareEscape_Cancels()
    {
        var recorder = Started();

        var step = recorder.KeyDown(Esc);

        Assert.Equal(HotkeyRecordStatus.Canceled, step.Status);
        Assert.Null(step.Chord);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void EscapeWithModifier_IsCaptured()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown(Esc);
        recorder.KeyUp(Esc);

        Assert.Equal("Ctrl+Esc", recorder.KeyUp(LCtrl).Chord!.ToString());
    }

    [Fact]
    public void BareKey_IsCapturedSoValidationCanExplainIt()
    {
        var recorder = Started();
        recorder.KeyDown('V');

        var done = recorder.KeyUp('V');

        Assert.Equal(HotkeyRecordStatus.Captured, done.Status);
        Assert.Equal(HotkeyProblem.NeedsModifier, done.Chord!.FindProblem());
    }

    [Fact]
    public void WinKey_IsCapturedSoValidationCanExplainIt()
    {
        var recorder = Started();
        recorder.KeyDown(LWin);
        recorder.KeyDown('V');
        recorder.KeyUp('V');

        Assert.Equal(HotkeyProblem.WinKeyReserved, recorder.KeyUp(LWin).Chord!.FindProblem());
    }

    [Fact]
    public void UnsupportedKey_StopsWithoutChord()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        Assert.Equal("Ctrl+?", recorder.KeyDown(VolumeMute).Preview);
        recorder.KeyUp(VolumeMute);

        var done = recorder.KeyUp(LCtrl);

        Assert.Equal(HotkeyRecordStatus.Unsupported, done.Status);
        Assert.Null(done.Chord);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void StrayRelease_BeforeAnyPress_IsIgnored()
    {
        var recorder = Started();

        var step = recorder.KeyUp(0x0D);

        Assert.Equal(HotkeyRecordStatus.Listening, step.Status);
        Assert.Equal(string.Empty, step.Preview);
        Assert.True(recorder.IsRecording);
    }

    [Fact]
    public void Stop_DiscardsPartialInput()
    {
        var recorder = Started();
        recorder.KeyDown(LCtrl);
        recorder.KeyDown('V');
        recorder.Stop();
        recorder.Start();

        Assert.Equal(string.Empty, recorder.Preview());
        Assert.Equal(HotkeyRecordStatus.Listening, recorder.KeyUp('V').Status);
    }
}
