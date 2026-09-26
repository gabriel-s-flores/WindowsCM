// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using WindowsCM.Core.Actions;

namespace WindowsCM.Core.Tests.Actions;

// The production runner against real processes (cmd.exe on Windows, sh
// elsewhere). A command action gets the item on stdin; the item can be
// megabytes, and a command may never read it.
public sealed class ProcessRunnerTests
{
    private static readonly string BigInput = new('x', 2 * 1024 * 1024);

    private static ProcessRequest Shell(string windows, string unix, string input, int timeoutMs) =>
        OperatingSystem.IsWindows()
            ? new ProcessRequest("cmd.exe", "/c " + windows, input, timeoutMs)
            : new ProcessRequest("/bin/sh", $"-c \"{unix}\"", input, timeoutMs);

    // The timeout used to be armed only after the whole item was written:
    // a command that never reads stdin blocked the write (a pipe holds a
    // few KB) forever, and was never killed.
    [Fact]
    public async Task CommandThatNeverReadsItsInput_IsKilledAtTheTimeout()
    {
        var request = Shell("ping -n 30 127.0.0.1 >nul", "sleep 30", BigInput, timeoutMs: 1000);

        var watch = Stopwatch.StartNew();
        var run = new ProcessRunner().RunAsync(request);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(run, finished);
        Assert.True((await run).TimedOut);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15));
    }

    // A command that exits without reading its input made the write throw
    // (broken pipe) out of the action: its result was lost.
    [Fact]
    public async Task CommandThatExitsWithoutReadingItsInput_StillReportsItsResult()
    {
        var request = Shell("echo done", "echo done", BigInput, timeoutMs: 20_000);

        var result = await new ProcessRunner().RunAsync(request);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("done", result.Stdout.Trim());
    }

    [Fact]
    public async Task InputIsDelivered()
    {
        var request = Shell("more", "cat", "hello from the clipboard", timeoutMs: 20_000);

        var result = await new ProcessRunner().RunAsync(request);

        Assert.Contains("hello from the clipboard", result.Stdout);
    }
}
