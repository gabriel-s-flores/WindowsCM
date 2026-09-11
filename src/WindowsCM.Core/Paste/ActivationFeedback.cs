// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// Never-silent activation policy (ticket 22): choosing an item always ends
// in a visible outcome. Success (Pasted / CopiedOnly) signals the copy
// (tray flash + sound, no balloon); every other status left the item on the
// clipboard or failed, so it carries a human-readable balloon on top of the
// copy signal when a copy happened. Failed never signals a copy.
public sealed record ActivationFeedback(bool SignalCopy, string? BalloonText);

public static class ActivationFeedbackPolicy
{
    public static ActivationFeedback ForOutcome(PasteOutcome outcome) =>
        outcome.Status switch
        {
            PasteStatus.Pasted => new ActivationFeedback(SignalCopy: true, BalloonText: null),
            PasteStatus.CopiedOnly => new ActivationFeedback(SignalCopy: true, BalloonText: null),
            PasteStatus.CopiedOnlyForegroundLost => new ActivationFeedback(
                SignalCopy: true, BalloonText: outcome.Diagnostics),
            PasteStatus.CopiedOnlyElevated => new ActivationFeedback(
                SignalCopy: true, BalloonText: outcome.Diagnostics),
            _ => new ActivationFeedback(SignalCopy: false, BalloonText: outcome.Diagnostics),
        };

    public static ActivationFeedback ForMissingItem(long itemId) => new(
        SignalCopy: false,
        BalloonText: $"Item {itemId} is no longer in history, so nothing was copied.");
}
