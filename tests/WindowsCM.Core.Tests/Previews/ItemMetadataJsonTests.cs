// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Tests.Previews;

// Metadata-column seam: code language ids (issue 14 spike) must merge with
// the CF_HTML envelope CaptureService already stores — never overwrite it.
// Corrupt JSON degrades to null (store parity), never drops the item.
public sealed class ItemMetadataJsonTests
{
    [Fact]
    public void Merge_LanguageIntoHtmlEnvelope_PreservesBoth()
    {
        const string html = """{"html":"<b>x</b>"}""";

        var merged = ItemMetadataJson.Merge(html, """{"language":{"id":"csharp","name":"C#"}}""");

        Assert.Equal("<b>x</b>", ItemMetadataJson.GetString(merged, "html"));
        var (id, name) = ItemMetadataJson.GetLanguage(merged);
        Assert.Equal("csharp", id);
        Assert.Equal("C#", name);
    }

    [Fact]
    public void Merge_CorruptBase_TreatedAsEmpty()
    {
        var merged = ItemMetadataJson.Merge("{oops", """{"html":"x"}""");

        Assert.Equal("x", ItemMetadataJson.GetString(merged, "html"));
    }

    [Fact]
    public void GetString_CorruptJson_ReturnsNull()
    {
        Assert.Null(ItemMetadataJson.GetString("{oops", "html"));
        Assert.Null(ItemMetadataJson.GetString(null, "html"));
    }

    [Fact]
    public void GetLink_RoundTripsTitleDescriptionImage()
    {
        var json = ItemMetadataJson.EncodeLink("T", "D", "https://cdn.example.com/i.png");

        var (title, desc, image) = ItemMetadataJson.GetLink(json);

        Assert.Equal("T", title);
        Assert.Equal("D", desc);
        Assert.Equal("https://cdn.example.com/i.png", image);
    }

    [Fact]
    public void GetLink_CorruptJson_ReturnsNulls()
    {
        var (title, desc, image) = ItemMetadataJson.GetLink("{oops");

        Assert.Null(title);
        Assert.Null(desc);
        Assert.Null(image);
    }

    [Fact]
    public void EncodeCode_RoundTripsIdAndName()
    {
        var json = ItemMetadataJson.EncodeCode("python", "Python");

        var (id, name) = ItemMetadataJson.GetLanguage(json);

        Assert.Equal("python", id);
        Assert.Equal("Python", name);
    }
}
