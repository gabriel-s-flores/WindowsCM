// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Feedback;

// Copy-feedback tunables (Copyous Feedback prefs parity, 01 §5):
// send-notification defaults false, wiggle-indicator defaults true.
public sealed class CopyFeedbackOptions
{
    public bool SendNotification { get; set; } = false;
    public bool WiggleIndicator { get; set; } = true;
}

// Tray balloon sink (NotifyIcon.ShowBalloonTip parity, research 05 §9):
// one call, no AUMID/installer/COM. Duration is OS-controlled (the timeout
// parameter is deprecated); one balloon at a time. The production adapter
// lives in the UI layer (needs NotifyIcon); tests fake it.
public interface ICopyNotifier
{
    void ShowBalloon(string title, string text);
}

// Tray-icon flash sink (Copyous wiggle 2px/65ms x3 parity, research 05
// §9): the production adapter alternates the base/overlay icon. Timing
// stays behind the call (times + interval asserted, never slept in tests).
public interface IIconFlasher
{
    void Flash(int times, int intervalMs);
}

// Copy-feedback policy: balloon per send-notification plus the icon flash
// when wiggle-indicator is on. Deliberately no OS toast reference anywhere
// in this path (toast blocked until an AUMID-holding installer exists;
// balloon is the v1, per spec Out of Scope).
public static class CopyFeedbackService
{
    // Wiggle parity: 3 flashes at 65ms (indicator.ts:175-179).
    public const int FlashCount = 3;
    public const int FlashIntervalMs = 65;

    public const string BalloonTitle = "WindowsCM";
    public const string BalloonText = "Copied to history";

    public static void NotifyCopied(
        CopyFeedbackOptions options,
        ICopyNotifier notifier,
        IIconFlasher flasher)
    {
        if (options.SendNotification)
        {
            notifier.ShowBalloon(BalloonTitle, BalloonText);
        }
        if (options.WiggleIndicator)
        {
            flasher.Flash(FlashCount, FlashIntervalMs);
        }
    }
}
