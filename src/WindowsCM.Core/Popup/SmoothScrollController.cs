// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Popup;

// Frame-rate independent smooth scrolling controller for horizontal cards.
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
