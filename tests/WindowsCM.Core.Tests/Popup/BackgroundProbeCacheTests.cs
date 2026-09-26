// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Popup;

namespace WindowsCM.Core.Tests.Popup;

public sealed class BackgroundProbeCacheTests
{
    private readonly List<Action> _scheduled = [];
    private readonly List<string> _probed = [];
    private readonly List<string> _updated = [];
    private DateTime _now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private BackgroundProbeCache<string> Create(
        Func<string, string>? probe = null, int capacity = 16, Action<string, Exception>? onFailed = null)
    {
        var cache = new BackgroundProbeCache<string>(
            key =>
            {
                _probed.Add(key);
                return (probe ?? (k => "facts:" + k))(key);
            },
            _scheduled.Add,
            freshFor: TimeSpan.FromSeconds(30),
            capacity: capacity,
            utcNow: () => _now,
            comparer: StringComparer.OrdinalIgnoreCase,
            onProbeFailed: onFailed);
        cache.Updated += _updated.Add;
        return cache;
    }

    private void RunScheduled()
    {
        var work = _scheduled.ToList();
        _scheduled.Clear();
        work.ForEach(w => w());
    }

    // The freeze: a card asked the disk from the UI thread. A lookup must
    // only ever queue the probe, however slow the path is.
    [Fact]
    public void Miss_NeverProbesOnTheCallersThread()
    {
        var cache = Create();

        Assert.False(cache.TryGet(@"\\server\share\report.pdf", out _));

        Assert.Empty(_probed);
        Assert.Single(_scheduled);
    }

    [Fact]
    public void RepeatedMisses_ShareOneProbe()
    {
        var cache = Create();

        for (var i = 0; i < 20; i++)
        {
            cache.TryGet(@"\\server\share\report.pdf", out _);
        }
        cache.TryGet(@"\\SERVER\share\REPORT.pdf", out _);

        Assert.Single(_scheduled);
    }

    [Fact]
    public void ProbedValue_IsServed_AndAnnouncedOnce()
    {
        var cache = Create();
        cache.TryGet("a", out _);

        RunScheduled();

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal("facts:a", value);
        Assert.Equal(["a"], _updated);
        Assert.Empty(_scheduled);
    }

    [Fact]
    public void StaleValue_IsStillServed_WhileOneReprobeRuns()
    {
        var cache = Create();
        cache.TryGet("a", out _);
        RunScheduled();

        _now += TimeSpan.FromSeconds(31);
        Assert.True(cache.TryGet("a", out var stale));
        cache.TryGet("a", out _);

        Assert.Equal("facts:a", stale);
        Assert.Single(_scheduled);
    }

    [Fact]
    public void Reprobe_WithTheSameAnswer_DoesNotAnnounceAgain()
    {
        var cache = Create();
        cache.TryGet("a", out _);
        RunScheduled();
        _updated.Clear();

        _now += TimeSpan.FromSeconds(31);
        cache.TryGet("a", out _);
        RunScheduled();

        Assert.Empty(_updated);
        Assert.True(cache.TryGet("a", out _));
        Assert.Empty(_scheduled);
    }

    [Fact]
    public void Reprobe_WithANewAnswer_IsAnnounced()
    {
        var answer = "old";
        var cache = Create(_ => answer);
        cache.TryGet("a", out _);
        RunScheduled();
        _updated.Clear();

        answer = "new";
        _now += TimeSpan.FromSeconds(31);
        cache.TryGet("a", out _);
        RunScheduled();

        Assert.Equal(["a"], _updated);
        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal("new", value);
    }

    [Fact]
    public void FailedProbe_IsReported_AndRetriedOnTheNextLookup()
    {
        var failures = new List<string>();
        var cache = Create(_ => throw new IOException("share gone"), onFailed: (key, _) => failures.Add(key));
        cache.TryGet("a", out _);

        RunScheduled();

        Assert.Equal(["a"], failures);
        Assert.Empty(_updated);
        Assert.False(cache.TryGet("a", out _));
        Assert.Single(_scheduled);
    }

    // A language switch changes formatted answers ("34,2 MB" vs "34.2 MB").
    [Fact]
    public void Clear_ForgetsEveryAnswer()
    {
        var cache = Create();
        cache.TryGet("a", out _);
        RunScheduled();

        cache.Clear();

        Assert.False(cache.TryGet("a", out _));
        Assert.Single(_scheduled);
    }

    [Fact]
    public void Capacity_EvictsTheLeastRecentlyUsed()
    {
        var cache = Create(capacity: 2);
        foreach (var key in new[] { "a", "b", "c" })
        {
            cache.TryGet(key, out _);
            RunScheduled();
        }

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }
}
