// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class SensitiveHintsTests
{
    [Fact]
    public void ContainsSensitive_KdePasswordHint_True()
    {
        Assert.True(SensitiveHints.ContainsSensitive(["Text", "x-kde-passwordManagerHint"]));
    }

    [Fact]
    public void ContainsSensitive_WindowsExcludeFormat_True()
    {
        Assert.True(SensitiveHints.ContainsSensitive(["ExcludeClipboardContentFromMonitorProcessing"]));
    }

    [Fact]
    public void ContainsSensitive_PlainFormats_False()
    {
        Assert.False(SensitiveHints.ContainsSensitive(["Text", "UnicodeText", "CF_HDROP"]));
    }

    [Fact]
    public void ContainsSensitive_Empty_False()
    {
        Assert.False(SensitiveHints.ContainsSensitive([]));
    }
}
