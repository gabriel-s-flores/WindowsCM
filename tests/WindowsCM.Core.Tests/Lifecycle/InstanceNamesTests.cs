// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class InstanceNamesTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";

    [Fact]
    public void MutexName_IsSessionQualified()
    {
        Assert.Equal(@"Local\WindowsCM." + Sid, InstanceNames.BuildMutexName(Sid));
    }

    [Fact]
    public void PipeName_CarriesSameSuffixWithoutNamespace()
    {
        var pipe = InstanceNames.BuildPipeName(Sid);

        Assert.Equal("WindowsCM." + Sid, pipe);
        Assert.DoesNotContain("\\", pipe);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sid with space")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void Build_RejectsBadSids(string? sid)
    {
        Assert.Throws<ArgumentException>(() => InstanceNames.BuildMutexName(sid!));
        Assert.Throws<ArgumentException>(() => InstanceNames.BuildPipeName(sid!));
    }

    [Fact]
    public void TestPipeName_IsUniquePerSuffix()
    {
        var first = InstanceNames.BuildTestPipeName(Guid.NewGuid().ToString("N"));
        var second = InstanceNames.BuildTestPipeName(Guid.NewGuid().ToString("N"));

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("\\", first);
    }

    [Fact]
    public void TestPipeName_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => InstanceNames.BuildTestPipeName(""));
    }
}
