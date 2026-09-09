// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

public sealed class CopyPasteChordsTests
{
    [Theory]
    // Copyous parity: plain Enter/Space pastes, Shift copies; swap inverts.
    [InlineData(false, false, ActivationIntent.CopyAndPaste)]
    [InlineData(true, false, ActivationIntent.CopyOnly)]
    [InlineData(false, true, ActivationIntent.CopyOnly)]
    [InlineData(true, true, ActivationIntent.CopyAndPaste)]
    public void Resolve_Matrix(bool shiftHeld, bool swap, ActivationIntent expected)
    {
        Assert.Equal(expected, CopyPasteChords.Resolve(shiftHeld, swap));
    }

    [Fact]
    public void PasteOptions_Defaults()
    {
        var options = new PasteOptions();

        Assert.Equal(PasteSequence.CtrlV, options.PasteSequence);
        Assert.Equal(200, options.PasteDelayMs);
        Assert.False(options.SwapCopyPaste);
    }
}
