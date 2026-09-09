// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Feedback;

namespace WindowsCM.Core.Tests.Feedback;

// Copy-feedback seam (research 05 §9): balloon per send-notification plus
// the 3x65ms icon flash when wiggle-indicator is on. No OS toast API is
// referenced anywhere in this path — by construction, not by flag.
public sealed class CopyFeedbackTests
{
    [Fact]
    public void Defaults_NotificationsOff_WiggleOn()
    {
        var options = new CopyFeedbackOptions();

        Assert.False(options.SendNotification);
        Assert.True(options.WiggleIndicator);
    }

    [Fact]
    public void Notify_BalloonEnabled_ShowsBalloonAndFlashes()
    {
        var notifier = new FakeNotifier();
        var flasher = new FakeFlasher();
        var options = new CopyFeedbackOptions { SendNotification = true, WiggleIndicator = true };

        CopyFeedbackService.NotifyCopied(options, notifier, flasher);

        Assert.Single(notifier.Balloons);
        var flash = Assert.Single(flasher.Flashes);
        Assert.Equal(CopyFeedbackService.FlashCount, flash.Times);
        Assert.Equal(CopyFeedbackService.FlashIntervalMs, flash.IntervalMs);
    }

    [Fact]
    public void Notify_BalloonDisabled_SkipsBalloonStillFlashes()
    {
        var notifier = new FakeNotifier();
        var flasher = new FakeFlasher();

        CopyFeedbackService.NotifyCopied(new CopyFeedbackOptions(), notifier, flasher);

        Assert.Empty(notifier.Balloons);
        Assert.Single(flasher.Flashes);
    }

    [Fact]
    public void Notify_WiggleDisabled_SkipsFlash()
    {
        var notifier = new FakeNotifier();
        var flasher = new FakeFlasher();
        var options = new CopyFeedbackOptions { SendNotification = true, WiggleIndicator = false };

        CopyFeedbackService.NotifyCopied(options, notifier, flasher);

        Assert.Single(notifier.Balloons);
        Assert.Empty(flasher.Flashes);
    }

    [Fact]
    public void Notify_BothDisabled_DoesNothing()
    {
        var notifier = new FakeNotifier();
        var flasher = new FakeFlasher();
        var options = new CopyFeedbackOptions { SendNotification = false, WiggleIndicator = false };

        CopyFeedbackService.NotifyCopied(options, notifier, flasher);

        Assert.Empty(notifier.Balloons);
        Assert.Empty(flasher.Flashes);
    }

    [Fact]
    public void FlashConstants_MatchWiggleParity()
    {
        Assert.Equal(3, CopyFeedbackService.FlashCount);
        Assert.Equal(65, CopyFeedbackService.FlashIntervalMs);
    }

    private sealed class FakeNotifier : ICopyNotifier
    {
        public List<(string Title, string Text)> Balloons = [];
        public void ShowBalloon(string title, string text) => Balloons.Add((title, text));
    }

    private sealed class FakeFlasher : IIconFlasher
    {
        public List<(int Times, int IntervalMs)> Flashes = [];
        public void Flash(int times, int intervalMs) => Flashes.Add((times, intervalMs));
    }
}
