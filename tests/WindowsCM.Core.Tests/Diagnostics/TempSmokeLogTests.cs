// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Diagnostics;

namespace WindowsCM.Core.Tests.Diagnostics;

// Temporary instrumentation (ticket 20, removed in 25): pins the unique
// prefix, TEMP-only path, and never-throw contract the smoke script greps.
public sealed class TempSmokeLogTests : IDisposable
{
    private readonly string _logPath;
    private readonly string? _previousOverride;

    public TempSmokeLogTests()
    {
        _previousOverride = Environment.GetEnvironmentVariable("WINDOWS_CM_SMOKE_LOG");
        _logPath = Path.Combine(Path.GetTempPath(), $"wcm20-test-{Guid.NewGuid():N}.log");
        Environment.SetEnvironmentVariable("WINDOWS_CM_SMOKE_LOG", _logPath);
        TempSmokeLog.Clear();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
        }
        catch
        {
        }
        Environment.SetEnvironmentVariable("WINDOWS_CM_SMOKE_LOG", _previousOverride);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Prefix_IsUniqueWcm20()
    {
        Assert.Equal("WCM20", TempSmokeLog.Prefix);
    }

    [Fact]
    public void LogPath_DefaultsToTempFile()
    {
        Environment.SetEnvironmentVariable("WINDOWS_CM_SMOKE_LOG", null);
        var path = TempSmokeLog.LogPath;
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), TempSmokeLog.FileName),
            path);
    }

    [Fact]
    public void Write_AppendsPrefixedCategoryLine()
    {
        TempSmokeLog.Write("popup-open", "cursorPx=1,2 visible=3");

        var text = File.ReadAllText(_logPath);
        Assert.Contains("[WCM20:popup-open]", text);
        Assert.Contains("cursorPx=1,2 visible=3", text);
    }

    [Fact]
    public void Write_NeverThrows_AndClear_RemovesFile()
    {
        TempSmokeLog.Write("tray", "visible=True");
        Assert.True(File.Exists(_logPath));

        TempSmokeLog.Clear();
        Assert.False(File.Exists(_logPath));

        // Clearing a missing file is a no-op, never a throw.
        TempSmokeLog.Clear();
    }

    [Theory]
    [InlineData(null, "<empty>")]
    [InlineData("", "<empty>")]
    [InlineData("abc", "abc")]
    public void Preview_HandlesEmpty(string? content, string expected)
    {
        Assert.Equal(expected, TempSmokeLog.Preview(content));
    }

    [Fact]
    public void Preview_EscapesNewlinesAndTruncates()
    {
        Assert.Equal(@"a\rb\nc", TempSmokeLog.Preview("a\rb\nc"));

        var longText = new string('x', 200);
        var preview = TempSmokeLog.Preview(longText, maxLength: 80);
        Assert.Equal(81, preview.Length);
        Assert.EndsWith("…", preview);
    }
}
