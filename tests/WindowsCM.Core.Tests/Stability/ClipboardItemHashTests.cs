// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using WindowsCM.Core.History;
using Xunit.Abstractions;

namespace WindowsCM.Core.Tests.Stability;

[Collection(StabilityCollection.Name)]
public sealed class ClipboardItemHashTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GetHashCode_LargeContent_HasBoundedCostForWpfItemStorage()
    {
        // Pixel virtualization uses items as dictionary keys while calculating
        // offsets. Hashing their full clipboard payload on every lookup made
        // scrolling cost megabytes of work per item in the history.
        var item = new ClipboardItem(ItemKind.Text, new string('x', 4_000_000), false, null,
            DateTime.UtcNow, new string('y', 4_000_000), null, 42);
        await Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            var hash = 0;
            for (var i = 0; i < 1000; i++) hash ^= item.GetHashCode();
            output.WriteLine($"1,000 WPF item-key hashes with 8M-character payload: {watch.Elapsed.TotalMilliseconds:F2} ms; checksum {hash}.");
            Assert.True(watch.ElapsedMilliseconds < 250, "Item-key hashing scanned the clipboard payload during scrolling.");
        }).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EqualItems_HaveEqualHashes_AndContentStillParticipatesInEquality()
    {
        var item = new ClipboardItem(ItemKind.Text, "first", false, null, DateTime.UtcNow, null, null, 42);
        Assert.Equal(item, item with { });
        Assert.Equal(item.GetHashCode(), (item with { }).GetHashCode());
        Assert.NotEqual(item, item with { Content = "second" });
        Assert.NotEqual(item, item with { MetadataJson = "{}" });
    }
}
