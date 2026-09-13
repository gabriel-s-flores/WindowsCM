// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Classification;

public sealed class WindowsFileTypeResolverTests
{
    private sealed class FakeRegistryAssociations : IWindowsRegistryAssociations
    {
        private readonly Dictionary<string, string> _perceived = new(StringComparer.OrdinalIgnoreCase);

        public void SetPerceived(string ext, string perceivedType) => _perceived[ext] = perceivedType;

        public string? GetPerceivedType(string extension) =>
            _perceived.TryGetValue(extension, out var pt) ? pt : null;
    }

    [Fact]
    public void Resolve_UserCategoryTakesPrecedence()
    {
        var settings = new FileCategorySettings();
        // User maps .custom to "presentations"
        settings.Categories.First(c => c.Id == "presentations").Extensions.Add(".custom");

        var fakeRegistry = new FakeRegistryAssociations();
        fakeRegistry.SetPerceived(".custom", "video"); // Registry says video, but user chose presentations

        var resolver = new WindowsFileTypeResolver(settings, fakeRegistry);
        var info = resolver.ResolveFileInfo("demo.custom");

        Assert.Equal("presentations", info.CategoryId);
        Assert.Equal("Apresentações", info.CategoryName);
        Assert.Equal("#EA580C", info.ColorHex);
    }

    [Fact]
    public void Resolve_FallsBackToRegistryPerceivedTypeWhenNotMappedInSettings()
    {
        var settings = new FileCategorySettings();
        var fakeRegistry = new FakeRegistryAssociations();
        fakeRegistry.SetPerceived(".oggvid", "video");
        fakeRegistry.SetPerceived(".flacstream", "audio");

        var resolver = new WindowsFileTypeResolver(settings, fakeRegistry);

        var videoInfo = resolver.ResolveFileInfo("stream.oggvid");
        Assert.Equal("video", videoInfo.CategoryId);
        Assert.Equal("Vídeos", videoInfo.CategoryName);
        Assert.Equal("#DC2626", videoInfo.ColorHex);

        var audioInfo = resolver.ResolveFileInfo("track.flacstream");
        Assert.Equal("audio", audioInfo.CategoryId);
        Assert.Equal("Áudio", audioInfo.CategoryName);
        Assert.Equal("#8B5CF6", audioInfo.ColorHex);
    }

    [Fact]
    public void Resolve_FallsBackToRegistryPerceivedType_Image_UsesUnifiedColor()
    {
        var settings = new FileCategorySettings();
        var fakeRegistry = new FakeRegistryAssociations();
        fakeRegistry.SetPerceived(".unmappedpic", "image");

        var resolver = new WindowsFileTypeResolver(settings, fakeRegistry);
        var info = resolver.ResolveFileInfo("picture.unmappedpic");

        Assert.Equal("images", info.CategoryId);
        Assert.Equal("Imagens", info.CategoryName);
        Assert.Equal("#16A34A", info.ColorHex);
    }

    [Fact]
    public void Resolve_UnknownExtension_FallsBackToGenericFile()
    {
        var settings = new FileCategorySettings();
        var fakeRegistry = new FakeRegistryAssociations();

        var resolver = new WindowsFileTypeResolver(settings, fakeRegistry);
        var info = resolver.ResolveFileInfo("data.xyz999");

        Assert.Equal("file", info.CategoryId);
        Assert.Equal("Arquivo", info.CategoryName);
        Assert.Equal("#D97706", info.ColorHex);
        Assert.Equal("Arquivo XYZ999", info.DisplayLabel);
    }

    [Theory]
    [InlineData("doc.pdf", "Documento PDF")]
    [InlineData("movie.mp4", "Vídeo MP4")]
    [InlineData("photo.png", "Imagem PNG")]
    [InlineData("slides.pptx", "Apresentação PowerPoint")]
    [InlineData("budget.xlsx", "Planilha Excel")]
    public void Resolve_DisplayLabelProducesFriendlyNames(string path, string expectedLabel)
    {
        var settings = new FileCategorySettings();
        var resolver = new WindowsFileTypeResolver(settings);

        var info = resolver.ResolveFileInfo(path);
        Assert.Equal(expectedLabel, info.DisplayLabel);
    }
}
