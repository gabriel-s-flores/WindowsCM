// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class EmojiDetectorTests
{
    [Theory]
    [InlineData("🚀")]
    [InlineData("🎉")]
    [InlineData("😀")]
    [InlineData("❤️")]
    [InlineData("🔥")]
    [InlineData("✨")]
    [InlineData("⭐")]
    [InlineData("⚡")]
    [InlineData("☕")]
    [InlineData("1️⃣")]
    public void IsAllEmojis_SingleEmoji_ReturnsTrue(string text)
    {
        Assert.True(EmojiDetector.IsAllEmojis(text));
    }

    [Theory]
    [InlineData("🚀🎉")]
    [InlineData("😀😁😂🤣")]
    [InlineData("❤️🔥✨")]
    [InlineData("🚀 🎉")]
    [InlineData("  🚀   🎉  ")]
    [InlineData("😀\n😁\n😂")]
    public void IsAllEmojis_MultipleEmojis_ReturnsTrue(string text)
    {
        Assert.True(EmojiDetector.IsAllEmojis(text));
    }

    [Theory]
    [InlineData("👨‍👩‍👧‍👦")] // Family ZWJ sequence
    [InlineData("👩‍💻")]      // Technologist ZWJ sequence
    [InlineData("👍🏽")]      // Skin tone modifier
    [InlineData("👋🏿")]      // Dark skin tone modifier
    [InlineData("🇧🇷")]      // Brazil flag (Regional Indicators)
    [InlineData("🇺🇸")]      // US flag
    [InlineData("🇧🇷 🇺🇸")]   // Multiple flags
    public void IsAllEmojis_ComplexEmojis_ReturnsTrue(string text)
    {
        Assert.True(EmojiDetector.IsAllEmojis(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hello 🚀")]
    [InlineData("🚀 123")]
    [InlineData("A")]
    [InlineData("123")]
    [InlineData("🚀.")]
    [InlineData("Texto com emoji 😀")]
    [InlineData("function foo() { return '🚀'; }")]
    public void IsAllEmojis_MixedTextOrNonEmoji_ReturnsFalse(string? text)
    {
        Assert.False(EmojiDetector.IsAllEmojis(text));
    }

    [Fact]
    public void CountEmojis_ReturnsAccurateCount()
    {
        Assert.Equal(0, EmojiDetector.CountEmojis(null));
        Assert.Equal(0, EmojiDetector.CountEmojis(""));
        Assert.Equal(0, EmojiDetector.CountEmojis("abc"));
        Assert.Equal(1, EmojiDetector.CountEmojis("🚀"));
        Assert.Equal(2, EmojiDetector.CountEmojis("🚀🎉"));
        Assert.Equal(3, EmojiDetector.CountEmojis("🚀 🎉 ❤️"));
        Assert.Equal(4, EmojiDetector.CountEmojis("😀 😁 😂 🤣"));
        Assert.Equal(1, EmojiDetector.CountEmojis("👨‍👩‍👧‍👦")); // Compound counts as 1 emoji grapheme
        Assert.Equal(2, EmojiDetector.CountEmojis("🇧🇷 🇺🇸"));
    }
}
