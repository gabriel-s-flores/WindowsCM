// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using WindowsCM.Core.Diagnostics;

namespace WindowsCM.Core.Tests.Diagnostics;

public sealed class UiActivityTests
{
    // A clock the test moves by whole seconds.
    private sealed class FakeClock
    {
        public long Now;
        public void Advance(double seconds) => Now += (long)(seconds * Stopwatch.Frequency);
    }

    [Fact]
    public void NestedStages_AreDescribedOutermostFirst_WithTheirDurations()
    {
        var clock = new FakeClock();
        var activity = new UiActivity(() => clock.Now);

        using (activity.Enter("hotkey"))
        {
            clock.Advance(1);
            using (activity.Enter("compact.show"))
            {
                clock.Advance(0.5);
                Assert.Equal("hotkey 1.5 s > compact.show 0.5 s", activity.Describe());
            }
            Assert.Equal("hotkey 1.5 s", activity.Describe());
        }
    }

    [Fact]
    public void Idle_NamesTheLastFinishedStage()
    {
        var clock = new FakeClock();
        var activity = new UiActivity(() => clock.Now);
        Assert.StartsWith("no tracked stage", activity.Describe());

        using (activity.Enter("theme"))
        {
            clock.Advance(0.2);
        }
        clock.Advance(3);

        Assert.Equal("no tracked stage (message loop or a window procedure); last finished: theme, 3.0 s ago", activity.Describe());
    }

    [Fact]
    public void DispatcherWork_IsDescribedLazily_AndExitedByIdentity()
    {
        var clock = new FakeClock();
        var activity = new UiActivity(() => clock.Now);
        var described = 0;
        activity.DescribeWork = work => { described++; return "Render " + work; };
        var outer = new object();
        var inner = "Layout";

        activity.EnterWork(outer);
        activity.EnterWork(inner);
        Assert.Equal(0, described);
        Assert.Contains("Render Layout 0.0 s", activity.Describe());

        // An outer operation finishing first (nested loop unwound) removes
        // the inner one too.
        activity.ExitWork(outer);
        Assert.StartsWith("no tracked stage", activity.Describe());
    }

    [Fact]
    public void FailingWorkDescription_FallsBackToAGenericName()
    {
        var activity = new UiActivity();
        activity.DescribeWork = _ => throw new InvalidOperationException();
        activity.EnterWork(new object());

        Assert.StartsWith("dispatcher operation", activity.Describe());
    }

    [Fact]
    public void Timeline_ListsEachStepAndTheTotal_AndMarksTheStage()
    {
        var clock = new FakeClock();
        var activity = new UiActivity(() => clock.Now);
        var timeline = new PhaseTimeline(() => clock.Now);

        using (timeline.Measure("settings", activity))
        {
            Assert.StartsWith("settings", activity.Describe());
            clock.Advance(0.012);
        }
        using (timeline.Measure("history-open"))
        {
            clock.Advance(0.3);
        }
        clock.Advance(0.1);

        Assert.Equal("settings 12 ms, history-open 300 ms, total 412 ms", timeline.Format());
        Assert.StartsWith("no tracked stage", activity.Describe());
    }
}
