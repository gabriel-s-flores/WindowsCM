// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Diagnostics;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Diagnostics;

public sealed class ErrorLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Write_CreatesTheFolderAndAppendsContextAndException()
    {
        var path = Path.Combine(_dir, "nested", "windowscm.log");
        var log = new ErrorLog(path, utcNow: () => new DateTime(2026, 9, 25, 12, 30, 0, DateTimeKind.Utc));

        log.Write("capture", new InvalidOperationException("clipboard busy"));
        log.Write("startup");

        var text = File.ReadAllText(path);
        Assert.Contains("2026-09-25 12:30:00.000Z [capture] System.InvalidOperationException: clipboard busy", text);
        Assert.Contains("2026-09-25 12:30:00.000Z [startup]", text);
    }

    [Fact]
    public void Note_AppendsAnEventWithItsDetail()
    {
        var path = Path.Combine(_dir, "windowscm.log");
        var log = new ErrorLog(path, utcNow: () => new DateTime(2026, 9, 25, 12, 30, 0, DateTimeKind.Utc));

        log.Note("ui-hang", "the UI thread has not answered for 5 s");

        Assert.Equal("2026-09-25 12:30:00.000Z [ui-hang] the UI thread has not answered for 5 s" + Environment.NewLine,
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_RotatesPastMaxBytes()
    {
        var path = Path.Combine(_dir, "windowscm.log");
        var log = new ErrorLog(path, maxBytes: 200);

        for (var i = 0; i < 10; i++)
        {
            log.Write("ui", new Exception(new string('x', 50)));
        }

        Assert.True(File.Exists(path + ".1"));
        Assert.True(new FileInfo(path).Length < 200 + 2000);
    }

    [Fact]
    public void Write_NeverThrows_WhenThePathIsUnusable()
    {
        // A directory where the file should be: every append fails.
        var path = Path.Combine(_dir, "taken");
        Directory.CreateDirectory(path);
        var log = new ErrorLog(path);

        var thrown = Record.Exception(() => log.Write("ui", new Exception("boom")));

        Assert.Null(thrown);
    }

    [Fact]
    public void DefaultPath_LivesUnderTheLogsFolder()
    {
        Assert.StartsWith(AppFolders.LogsDir(), ErrorLog.DefaultPath());
        Assert.StartsWith(AppFolders.DataDir(), AppFolders.LogsDir());
    }
}

public sealed class UnhandledErrorPolicyTests
{
    private static readonly DateTime T0 = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsolatedErrors_KeepTheAppRunning()
    {
        var policy = new UnhandledErrorPolicy(maxErrors: 3, window: TimeSpan.FromSeconds(10));

        for (var i = 0; i < 20; i++)
        {
            Assert.True(policy.ShouldContinue(T0.AddMinutes(i)));
        }
    }

    [Fact]
    public void Burst_LetsTheAppEnd()
    {
        var policy = new UnhandledErrorPolicy(maxErrors: 3, window: TimeSpan.FromSeconds(10));

        Assert.True(policy.ShouldContinue(T0));
        Assert.True(policy.ShouldContinue(T0.AddSeconds(1)));
        Assert.True(policy.ShouldContinue(T0.AddSeconds(2)));
        Assert.False(policy.ShouldContinue(T0.AddSeconds(3)));
    }

    [Fact]
    public void Burst_OlderThanTheWindow_IsForgotten()
    {
        var policy = new UnhandledErrorPolicy(maxErrors: 2, window: TimeSpan.FromSeconds(10));
        policy.ShouldContinue(T0);
        policy.ShouldContinue(T0.AddSeconds(1));

        Assert.True(policy.ShouldContinue(T0.AddSeconds(30)));
    }
}

public sealed class LruCacheTests
{
    [Fact]
    public void Set_PastCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = new LruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        Assert.True(cache.TryGet("a", out _)); // "a" is now the most recent

        cache.Set("c", 3);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet("a", out var a));
        Assert.Equal(1, a);
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Set_ExistingKey_Replaces()
    {
        var cache = new LruCache<string, string?>(2, StringComparer.OrdinalIgnoreCase);
        cache.Set("Key", "old");
        cache.Set("KEY", null);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("key", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void GetOrAdd_RunsTheFactoryOncePerKey()
    {
        var cache = new LruCache<int, string>(4);
        var calls = 0;

        var first = cache.GetOrAdd(7, k => { calls++; return $"v{k}"; });
        var second = cache.GetOrAdd(7, k => { calls++; return "other"; });

        Assert.Equal("v7", first);
        Assert.Equal("v7", second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ConcurrentUse_StaysWithinCapacity()
    {
        var cache = new LruCache<int, int>(64);

        Parallel.For(0, 10_000, i =>
        {
            cache.Set(i % 500, i);
            cache.TryGet((i * 7) % 500, out _);
        });

        Assert.True(cache.Count <= 64);
    }
}
