// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using WindowsCM.Core.Diagnostics;

namespace WindowsCM.Core.Tests.Diagnostics;

public sealed class UiHangWatchdogTests
{
    private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);

    // A UI thread the test controls: posted work runs only when allowed.
    private sealed class FakeUi
    {
        private readonly ConcurrentQueue<Action> _queue = new();
        public volatile bool Blocked;

        public void Post(Action work)
        {
            _queue.Enqueue(work);
            if (!Blocked)
            {
                Drain();
            }
        }

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work();
            }
        }
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }
            Thread.Sleep(10);
        }
        return condition();
    }

    [Fact]
    public void AnsweringUi_IsNeverReported()
    {
        var ui = new FakeUi();
        var reports = new ConcurrentQueue<string>();
        using var watchdog = new UiHangWatchdog(ui.Post, reports.Enqueue, Threshold, Interval);

        watchdog.Start();
        Thread.Sleep(600);

        Assert.Empty(reports);
    }

    [Fact]
    public void BlockedUi_IsReportedOnce_ThenItsRecoveryWithTheDuration()
    {
        var ui = new FakeUi { Blocked = true };
        var reports = new ConcurrentQueue<string>();
        using var watchdog = new UiHangWatchdog(ui.Post, reports.Enqueue, Threshold, Interval,
            describeProcess: () => "private 200 MB");

        watchdog.Start();
        Assert.True(WaitFor(() => reports.Count == 1));
        Thread.Sleep(400);
        Assert.Single(reports);
        Assert.Contains("not answered", reports.First());
        Assert.Contains("private 200 MB", reports.First());

        ui.Blocked = false;
        ui.Drain();

        Assert.True(WaitFor(() => reports.Count == 2));
        Assert.Matches(@"answered again after \d+[.,]\d s", reports.Last());
    }

    [Fact]
    public void BlockedUi_NamesTheStage_AndEveryStageSeenUntilItAnswers()
    {
        var ui = new FakeUi { Blocked = true };
        var reports = new ConcurrentQueue<string>();
        var stage = "startup 0.2 s > history-open 0.1 s";
        using var watchdog = new UiHangWatchdog(ui.Post, reports.Enqueue, Threshold, Interval,
            describeActivity: () => Volatile.Read(ref stage), samplePeriod: TimeSpan.FromMilliseconds(20));

        watchdog.Start();
        Assert.True(WaitFor(() => reports.Count == 1));
        Assert.Contains("stage: startup 0.2 s > history-open 0.1 s", reports.First());

        // Only the durations change: the same stage, not reported twice.
        Volatile.Write(ref stage, "startup 0.4 s > history-open 0.3 s");
        Thread.Sleep(100);
        Volatile.Write(ref stage, "startup 0.5 s > theme 0.1 s");
        Thread.Sleep(100);
        ui.Blocked = false;
        ui.Drain();

        Assert.True(WaitFor(() => reports.Count == 2));
        var recovery = reports.Last();
        Assert.Contains("stages seen:", recovery);
        Assert.Contains("history-open", recovery);
        Assert.Contains("theme 0.1 s", recovery);
        Assert.Equal(2, recovery.Split(" | ").Length);
    }

    [Fact]
    public void Dispose_StopsWatching()
    {
        var ui = new FakeUi { Blocked = true };
        var reports = new ConcurrentQueue<string>();
        var watchdog = new UiHangWatchdog(ui.Post, reports.Enqueue, Threshold, Interval);

        watchdog.Start();
        watchdog.Dispose();
        Thread.Sleep(400);

        Assert.Empty(reports);
    }

    [Fact]
    public void FailingPost_EndsTheWatchdogQuietly()
    {
        var reports = new ConcurrentQueue<string>();
        using var watchdog = new UiHangWatchdog(
            _ => throw new InvalidOperationException("dispatcher shut down"), reports.Enqueue, Threshold, Interval);

        watchdog.Start();
        Thread.Sleep(300);

        Assert.Empty(reports);
    }
}
