// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Tests.Paste;

// Ticket 22: choosing an item never fails silently. Success signals the
// copy with no balloon; copy-with-warning signals plus balloons the reason;
// hard failures balloon without a copy signal.
public sealed class ActivationFeedbackTests
{
    [Fact]
    public void Pasted_SignalsCopyWithoutBalloon()
    {
        var feedback = ActivationFeedbackPolicy.ForOutcome(
            new PasteOutcome(PasteStatus.Pasted, PasteSequence.CtrlV, null));

        Assert.True(feedback.SignalCopy);
        Assert.Null(feedback.BalloonText);
    }

    [Fact]
    public void ShiftEnterCopyOnly_SignalsCopyWithoutBalloon()
    {
        var feedback = ActivationFeedbackPolicy.ForOutcome(
            new PasteOutcome(PasteStatus.CopiedOnly, null, null));

        Assert.True(feedback.SignalCopy);
        Assert.Null(feedback.BalloonText);
    }

    [Fact]
    public void ForegroundLost_SignalsCopyAndExplainsFocus()
    {
        var outcome = new PasteOutcome(
            PasteStatus.CopiedOnlyForegroundLost, null, "lost focus before pasting");

        var feedback = ActivationFeedbackPolicy.ForOutcome(outcome);

        Assert.True(feedback.SignalCopy);
        Assert.Equal("lost focus before pasting", feedback.BalloonText);
    }

    [Fact]
    public void ElevatedTarget_SignalsCopyAndExplainsElevation()
    {
        var outcome = new PasteOutcome(
            PasteStatus.CopiedOnlyElevated, null, "target runs elevated");

        var feedback = ActivationFeedbackPolicy.ForOutcome(outcome);

        Assert.True(feedback.SignalCopy);
        Assert.Equal("target runs elevated", feedback.BalloonText);
    }

    [Theory]
    [InlineData("Item 7 is no longer in history")]
    [InlineData("image file is missing")]
    [InlineData("another window owns it")]
    [InlineData("not accepted for injection")]
    public void Failed_NeverSilent(string diagnostics)
    {
        var feedback = ActivationFeedbackPolicy.ForOutcome(
            new PasteOutcome(PasteStatus.Failed, null, diagnostics));

        Assert.False(feedback.SignalCopy);
        Assert.False(string.IsNullOrWhiteSpace(feedback.BalloonText));
    }

    [Fact]
    public void MissingItem_NeverSilent()
    {
        var feedback = ActivationFeedbackPolicy.ForMissingItem(42);

        Assert.False(feedback.SignalCopy);
        Assert.Contains("42", feedback.BalloonText);
        Assert.False(string.IsNullOrWhiteSpace(feedback.BalloonText));
    }
}
