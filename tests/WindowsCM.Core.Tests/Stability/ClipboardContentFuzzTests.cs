// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Previews;
using Xunit.Abstractions;

namespace WindowsCM.Core.Tests.Stability;

[Collection(StabilityCollection.Name)]
public sealed class ClipboardContentFuzzTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("<meta ")]
    [InlineData("<title>")]
    [InlineData("<link rel='image_src' ")]
    public async Task LinkMetadata_RepeatedUnclosedTags_DoNotExhaustPreviewWorkers(string fragment)
    {
        var html = string.Concat(Enumerable.Repeat(fragment, 20000));
        await Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            var metadata = LinkMetadataParser.Parse(html, "https://example.invalid/");
            Assert.Null(metadata.Title);
            Assert.Null(metadata.ImageUrl);
            output.WriteLine($"20,000 unclosed {fragment} tags: {watch.Elapsed.TotalMilliseconds:F2} ms.");
            Assert.True(watch.ElapsedMilliseconds < 1000, "Malformed HTML exhausted a preview worker for over one second.");
        }).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Tokenizer_SeededInputs_AlwaysTerminateAndPreservePreview()
    {
        await Task.Run(() =>
        {
            var random = new Random(20260930);
            const string alphabet = "abcXYZ09_@$#/'\"`\\*-=!<>+&|^~?:;(){}[] \r\n\t\0é中😀";
            var watch = Stopwatch.StartNew();
            for (var sample = 0; sample < 20000; sample++)
            {
                var code = new string(Enumerable.Range(0, random.Next(1, 160))
                    .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
                var tokens = CodeSyntaxTokenizer.Tokenize(code);
                var (lines, more) = TextPreview.FirstLines(code, 8);
                var expected = string.Join("\n", lines) + (more ? "\n..." : "");
                if (string.IsNullOrWhiteSpace(code)) expected = "";
                Assert.Equal(expected, string.Concat(tokens.Select(token => token.Text)));
                Assert.All(tokens, token => Assert.NotEmpty(token.Text));
            }
            output.WriteLine($"20,000 tokenizer inputs: {watch.ElapsedMilliseconds} ms (seed 20260930).");
        }).WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task ColorParser_AlmostValidInputs_DoNotBlockClipboardCapture()
    {
        await Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            foreach (var name in new[] { "rgb", "rgba", "hsl", "hsla", "hwb", "lab", "lch", "oklab", "oklch", "color" })
            {
                foreach (var padding in new[] { ' ', '0', '.', ',', 'e', '\t' })
                {
                    var text = name + "(" + new string(padding, 480) + "!";
                    Assert.False(ColorParser.IsColor(text));
                }
            }
            output.WriteLine($"60 adversarial color inputs: {watch.ElapsedMilliseconds} ms.");
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "Invalid colors blocked capture for over two seconds.");
        }).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClassifyAndFormat_SeededClipboardAndMetadata_NeverThrow()
    {
        await Task.Run(() =>
        {
            var random = new Random(42);
            string[] fragments = ["plain note", "C#", "var x = 1;", "#", "/*", "*/", "\\", "\0", "\uD800", "\uDC00", "😀", "é", "中", "\r\n", "file://", "https://", "rgb(", "{}", "[]"];
            string?[] metadata = [null, "", "null", "[]", "{", "{}", "{\"language\":false}", "{\"title\":3}", "{\"language\":{\"name\":[]}}", "{\"image\":\"\\u0000\"}"];
            var watch = Stopwatch.StartNew();
            for (var sample = 0; sample < 5000; sample++)
            {
                var text = new StringBuilder();
                for (var i = 0; i < random.Next(1, 15); i++) text.Append(fragments[random.Next(fragments.Length)]);
                var content = text.ToString();
                _ = Classifier.ClassifyText(content);
                foreach (var kind in Enum.GetValues<ItemKind>())
                {
                    var item = new ClipboardItem(kind, content, false, null, DateTime.UtcNow, metadata[sample % metadata.Length], null);
                    _ = ItemDisplayFormatter.GetTitle(item);
                    _ = ItemDisplayFormatter.GetPreviewText(item);
                    _ = ItemDisplayFormatter.GetTypeLabel(item);
                    _ = ItemMetadataJson.GetLanguage(item.MetadataJson);
                    _ = ItemMetadataJson.GetString(item.MetadataJson, "image");
                }
            }
            output.WriteLine($"5,000 classifier inputs / 40,000 card-format cases: {watch.ElapsedMilliseconds} ms (seed 42).");
        }).WaitAsync(TimeSpan.FromSeconds(20));
    }
}
