// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Tests.Classification;

public sealed class CodeDetectorTests
{
    [Theory]
    [InlineData("public void Foo()\n{\n    return 42;\n}")]
    [InlineData("def foo():\n    return 42")]
    [InlineData("SELECT * FROM users WHERE id = 1;")]
    [InlineData("function foo() { return 1; }")]
    [InlineData("if (x) { y(); }")]
    public void IsCode_CodeSnippets_True(string text)
    {
        Assert.True(CodeDetector.IsCode(text));
    }

    [Theory]
    [InlineData("Hello world, this is a note.")]
    [InlineData("call me (tomorrow)")]
    [InlineData("Shopping:\n  milk\n  eggs")]
    [InlineData("TODO: buy milk, eggs, bread")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsCode_ProseAndBlanks_False(string? text)
    {
        Assert.False(CodeDetector.IsCode(text));
    }

    [Fact]
    public void IsCode_LongProse_False()
    {
        var prose = string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 45));

        Assert.False(CodeDetector.IsCode(prose));
    }

    [Fact]
    public void IsCode_LongCode_True()
    {
        var code = string.Concat(Enumerable.Repeat("public void Foo() { return 42; }\n", 60));

        Assert.True(CodeDetector.IsCode(code));
    }

    [Fact]
    public void IsCode_CodePastSliceLimit_Ignored()
    {
        // Only the first 10k chars feed detection (Copyous slices there too).
        var text = new string('x', 10000) + "public void Foo() { return 1; }";

        Assert.False(CodeDetector.IsCode(text));
    }

    [Fact]
    public void IsCode_CodeInsideSliceWithNoise_True()
    {
        var text = "public void Foo() { return 1; }" + new string('x', 150);

        Assert.True(CodeDetector.IsCode(text));
    }
}
