// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Tests.Capture;

public sealed class ClipboardUpdateCoalescerTests
{
    private readonly ClipboardUpdateCoalescer _coalescer = new(
        quiet: TimeSpan.FromMilliseconds(100), maxWait: TimeSpan.FromMilliseconds(500));

    [Fact]
    public void SingleUpdate_WaitsTheQuietPeriod()
    {
        Assert.Equal(100, _coalescer.OnUpdate(1_000));
        Assert.True(_coalescer.IsPending);
    }

    [Fact]
    public void Burst_KeepsPushingTheReadBack_ThenReadsOnce()
    {
        _coalescer.OnUpdate(1_000);
        Assert.Equal(100, _coalescer.OnUpdate(1_030));
        Assert.Equal(100, _coalescer.OnUpdate(1_060));

        _coalescer.OnRead();

        Assert.False(_coalescer.IsPending);
        Assert.Equal(100, _coalescer.OnUpdate(2_000));
    }

    [Fact]
    public void ChattySource_CannotPostponeTheReadPastMaxWait()
    {
        _coalescer.OnUpdate(1_000);
        Assert.Equal(100, _coalescer.OnUpdate(1_380));
        Assert.Equal(60, _coalescer.OnUpdate(1_440));
        Assert.Equal(0, _coalescer.OnUpdate(1_600));
    }

    [Fact]
    public void Defaults_AreShortEnoughToFeelInstant()
    {
        var coalescer = new ClipboardUpdateCoalescer();

        Assert.InRange(coalescer.OnUpdate(0), 50, 150);
        Assert.True(ClipboardUpdateCoalescer.DefaultMaxWait <= TimeSpan.FromSeconds(1));
    }
}
