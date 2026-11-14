// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;
using Xunit;

namespace WindowsCM.Core.Tests.Popup;

public sealed class SmoothScrollControllerTests
{
    [Fact]
    public void InitialState_IsAtOriginAndNotAnimating()
    {
        var controller = new SmoothScrollController();

        Assert.Equal(0, controller.CurrentOffset);
        Assert.Equal(0, controller.TargetOffset);
        Assert.False(controller.IsAnimating);
    }

    [Fact]
    public void SetImmediate_SetsBothOffsetsAndStopsAnimation()
    {
        var controller = new SmoothScrollController();
        controller.AddDelta(300, scrollableWidth: 1000);
        Assert.True(controller.IsAnimating);

        controller.SetImmediate(150, scrollableWidth: 1000);

        Assert.Equal(150, controller.CurrentOffset);
        Assert.Equal(150, controller.TargetOffset);
        Assert.False(controller.IsAnimating);
    }

    [Fact]
    public void AddDelta_ClampsToScrollableBounds()
    {
        var controller = new SmoothScrollController();

        // Below zero clamps to 0
        controller.AddDelta(-100, scrollableWidth: 500);
        Assert.Equal(0, controller.TargetOffset);

        // Above max clamps to max
        controller.AddDelta(800, scrollableWidth: 500);
        Assert.Equal(500, controller.TargetOffset);
        Assert.True(controller.IsAnimating);
    }

    [Fact]
    public void Tick_Simulating144Hz_SmoothlyConvergesToTarget()
    {
        var controller = new SmoothScrollController(initialOffset: 0);
        controller.AddDelta(260, scrollableWidth: 1000);

        const double dt144Hz = 1.0 / 144.0;
        int frames = 0;
        double previousOffset = 0;

        while (controller.IsAnimating && frames < 200)
        {
            var keepAnimating = controller.Tick(dt144Hz);
            frames++;

            // Offset must advance monotonically toward target
            Assert.True(controller.CurrentOffset >= previousOffset);
            previousOffset = controller.CurrentOffset;

            if (!keepAnimating)
            {
                break;
            }
        }

        Assert.False(controller.IsAnimating);
        Assert.Equal(260, controller.CurrentOffset);
        // At 144Hz with smoothness 22, should converge in ~30-45 frames (~200-300ms)
        Assert.True(frames is > 10 and < 100, $"Converged in {frames} frames");
    }

    [Fact]
    public void Tick_RapidMultiNotchScroll_AccumulatesWithoutDiscontinuity()
    {
        var controller = new SmoothScrollController(initialOffset: 0);

        // Notch 1
        controller.AddDelta(260, scrollableWidth: 2000);
        Assert.Equal(260, controller.TargetOffset);

        // Advance 3 frames at 144Hz
        const double dt = 1.0 / 144.0;
        controller.Tick(dt);
        controller.Tick(dt);
        controller.Tick(dt);
        var intermediateOffset = controller.CurrentOffset;
        Assert.True(intermediateOffset > 0 && intermediateOffset < 260);

        // Notch 2 arrives while in motion
        controller.AddDelta(260, scrollableWidth: 2000);
        Assert.Equal(520, controller.TargetOffset);

        // Next tick must continue moving forward seamlessly without resetting to 0
        controller.Tick(dt);
        Assert.True(controller.CurrentOffset > intermediateOffset);
    }

    [Fact]
    public void Rebase_ContinuesFromTheActualOffset_TowardTheClampedTarget()
    {
        var controller = new SmoothScrollController();
        controller.AddDelta(500, scrollableWidth: 1000);

        // The list's extent shrank to 400 while scrolling.
        controller.Rebase(150, scrollableWidth: 400);

        Assert.Equal(150, controller.CurrentOffset);
        Assert.Equal(400, controller.TargetOffset);
        Assert.True(controller.IsAnimating);
    }

    [Fact]
    public void Rebase_AtTheTarget_StopsAnimating()
    {
        var controller = new SmoothScrollController();
        controller.AddDelta(300, scrollableWidth: 1000);

        controller.Rebase(300, scrollableWidth: 1000);

        Assert.False(controller.IsAnimating);
    }

    [Theory]
    [InlineData(-120, 96, 96)]
    [InlineData(-30, 96, 24)]
    [InlineData(-360, 96, 288)]
    [InlineData(120, 260, -260)]
    public void WheelDistance_IsProportionalToTheDelta(int delta, double perNotch, double expected)
    {
        Assert.Equal(expected, WheelScrollStep.Distance(delta, perNotch), 6);
    }

    [Theory]
    [InlineData(3, 400, 96)]
    [InlineData(1, 400, 32)]
    [InlineData(0, 400, 0)]
    [InlineData(-1, 400, 360)]
    public void VerticalStep_FollowsTheWindowsLinesSetting(int lines, double viewport, double expected)
    {
        Assert.Equal(expected, WheelScrollStep.VerticalPerNotch(lines, viewport), 6);
    }
}
