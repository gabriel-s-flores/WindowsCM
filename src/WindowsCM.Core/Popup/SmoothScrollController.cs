// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Frame-rate independent smooth scrolling for the popup card lists (both
// popups, both orientations; one axis per list).
// Uses continuous exponential decay (V-Sync synchronized) to achieve native monitor refresh rate
// (60Hz, 120Hz, 144Hz, 240Hz) with zero stutter and smooth momentum accumulation.
public sealed class SmoothScrollController
{
    public const double DefaultSmoothness = 22.0;
    public const double ConvergenceThreshold = 0.25;

    public double CurrentOffset { get; private set; }
    public double TargetOffset { get; private set; }
    public bool IsAnimating { get; private set; }
    public double Smoothness { get; set; } = DefaultSmoothness;

    public SmoothScrollController(double initialOffset = 0)
    {
        CurrentOffset = initialOffset;
        TargetOffset = initialOffset;
        IsAnimating = false;
    }

    public void SetImmediate(double offset, double scrollableWidth)
    {
        var clamped = Math.Clamp(offset, 0, Math.Max(0, scrollableWidth));
        CurrentOffset = clamped;
        TargetOffset = clamped;
        IsAnimating = false;
    }

    public void AddDelta(double deltaDips, double scrollableWidth)
    {
        var max = Math.Max(0, scrollableWidth);
        TargetOffset = Math.Clamp(TargetOffset + deltaDips, 0, max);
        if (Math.Abs(TargetOffset - CurrentOffset) > ConvergenceThreshold)
        {
            IsAnimating = true;
        }
    }

    // The view moved the offset itself (the virtualizing panel corrected an
    // estimated extent, or clamped past the end): continue from where the
    // list really is, toward the same target, kept within the new bounds.
    public void Rebase(double actualOffset, double scrollableWidth)
    {
        var max = Math.Max(0, scrollableWidth);
        CurrentOffset = Math.Clamp(actualOffset, 0, max);
        TargetOffset = Math.Clamp(TargetOffset, 0, max);
        if (Math.Abs(TargetOffset - CurrentOffset) <= ConvergenceThreshold)
        {
            CurrentOffset = TargetOffset;
            IsAnimating = false;
        }
    }

    public bool Tick(double dtSeconds)
    {
        if (!IsAnimating)
        {
            return false;
        }

        if (dtSeconds <= 0)
        {
            return true;
        }

        // Frame-rate independent exponential interpolation:
        // factor = 1 - e^(-lambda * dt)
        var factor = 1.0 - Math.Exp(-Smoothness * dtSeconds);
        CurrentOffset += (TargetOffset - CurrentOffset) * factor;

        if (Math.Abs(TargetOffset - CurrentOffset) <= ConvergenceThreshold)
        {
            CurrentOffset = TargetOffset;
            IsAnimating = false;
            return false;
        }

        return true;
    }
}

// How far one wheel event scrolls a card list, in DIPs: proportional to the
// delta (a notch is 120; precision touchpads send small deltas often), so a
// fast spin goes further and a touchpad glide stays fine-grained. Positive
// distances move toward the end of the list.
public static class WheelScrollStep
{
    public const int NotchDelta = 120;

    // Per line of the Windows "lines to scroll" setting: 3 lines (the
    // default) move ~100 DIPs a notch, about a quarter of the compact
    // popup's list. The old fixed 32 DIPs per event ignored the delta.
    public const double LineDips = 32;

    public static double Distance(int wheelDelta, double dipsPerNotch) =>
        -wheelDelta / (double)NotchDelta * dipsPerNotch;

    // wheelScrollLines is -1 when Windows is set to scroll a page a notch.
    public static double VerticalPerNotch(int wheelScrollLines, double viewport) =>
        wheelScrollLines < 0
            ? Math.Max(LineDips, viewport * 0.9)
            : wheelScrollLines * LineDips;
}
