// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class ImageContentTests
{
    [Theory]
    [InlineData("image/png", "png")]
    [InlineData("image/jpeg", "jpeg")]
    [InlineData("image/webp", "webp")]
    [InlineData("image/avif", "avif")]
    [InlineData("image/jxl", "jxl")]
    [InlineData("image/svg+xml", "svg+xml")]
    public void ExtensionFor_FollowsSourceMimetype(string mimeType, string expected)
    {
        Assert.Equal(expected, ImageContent.ExtensionFor(mimeType));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("")]
    public void ExtensionFor_NoSlash_FallsBackToBin(string mimeType)
    {
        Assert.Equal("bin", ImageContent.ExtensionFor(mimeType));
    }
}
