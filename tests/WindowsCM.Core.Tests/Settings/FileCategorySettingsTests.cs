// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

public sealed class FileCategorySettingsTests
{
    [Fact]
    public void DefaultCategories_ContainsStandardCategories()
    {
        var settings = new FileCategorySettings();

        Assert.NotEmpty(settings.Categories);
        Assert.Contains(settings.Categories, c => c.Id == "images" && c.Name == "Imagens");
        Assert.Contains(settings.Categories, c => c.Id == "audio" && c.Name == "Áudio");
        Assert.Contains(settings.Categories, c => c.Id == "video" && c.Name == "Vídeos");
        Assert.Contains(settings.Categories, c => c.Id == "documents" && c.Name == "Documentos");
        Assert.Contains(settings.Categories, c => c.Id == "spreadsheets" && c.Name == "Planilhas");
        Assert.Contains(settings.Categories, c => c.Id == "presentations" && c.Name == "Apresentações");
        Assert.Contains(settings.Categories, c => c.Id == "code" && c.Name == "Código");
        Assert.Contains(settings.Categories, c => c.Id == "archives" && c.Name == "Compactados");
    }

    [Theory]
    [InlineData(".png", "images", "#16A34A")]
    [InlineData("photo.JPG", "images", "#16A34A")]
    [InlineData(".mp3", "audio", "#8B5CF6")]
    [InlineData("song.WAV", "audio", "#8B5CF6")]
    [InlineData("video.mp4", "video", "#DC2626")]
    [InlineData(".MKV", "video", "#DC2626")]
    [InlineData("notes.docx", "documents", "#0078D4")]
    [InlineData("sheet.xlsx", "spreadsheets", "#0D9488")]
    [InlineData("slides.pptx", "presentations", "#EA580C")]
    [InlineData("Program.cs", "code", "#6366F1")]
    [InlineData("archive.ZIP", "archives", "#B45309")]
    public void ResolveCategory_ResolvesBuiltInExtensions(string input, string expectedId, string expectedColor)
    {
        var settings = new FileCategorySettings();

        var resolved = settings.ResolveCategory(input);

        Assert.NotNull(resolved);
        Assert.Equal(expectedId, resolved.Id);
        Assert.Equal(expectedColor, resolved.ColorHex);
    }

    [Fact]
    public void ResolveCategory_ReturnsNullForUnknownExtension()
    {
        var settings = new FileCategorySettings();

        Assert.Null(settings.ResolveCategory(".unknownxyz"));
        Assert.Null(settings.ResolveCategory(""));
        Assert.Null(settings.ResolveCategory(null));
    }

    [Fact]
    public void AddCategory_CreatesAndResolvesNewExtension()
    {
        var settings = new FileCategorySettings();

        var cat = settings.AddCategory("Modelos 3D", "#9B59B6", new[] { ".obj", ".fbx", "blend" });

        Assert.NotNull(cat);
        Assert.False(cat.IsBuiltIn);
        Assert.Equal("Modelos 3D", cat.Name);
        Assert.Equal("#9B59B6", cat.ColorHex);

        var resolved = settings.ResolveCategory("character.blend");
        Assert.NotNull(resolved);
        Assert.Equal(cat.Id, resolved.Id);
        Assert.Equal("#9B59B6", resolved.ColorHex);
    }

    [Fact]
    public void RemoveCategory_CannotRemoveBuiltIn_CanRemoveCustom()
    {
        var settings = new FileCategorySettings();
        var custom = settings.AddCategory("Custom", "#112233", new[] { ".custom" });

        var removedBuiltIn = settings.RemoveCategory("images");
        Assert.False(removedBuiltIn);
        Assert.NotNull(settings.ResolveCategory(".png"));

        var removedCustom = settings.RemoveCategory(custom.Id);
        Assert.True(removedCustom);
        Assert.Null(settings.ResolveCategory(".custom"));
    }

    [Fact]
    public void ResetToDefaults_RestoresInitialState()
    {
        var settings = new FileCategorySettings();
        settings.AddCategory("Extra", "#333333", new[] { ".extra" });
        settings.Categories.First(c => c.Id == "images").ColorHex = "#FFFFFF";

        settings.ResetToDefaults();

        Assert.Null(settings.ResolveCategory(".extra"));
        Assert.Equal("#16A34A", settings.Categories.First(c => c.Id == "images").ColorHex);
    }

    [Fact]
    public void Clamp_MigratesLegacyColorsToUnifiedDefaults()
    {
        var settings = new FileCategorySettings();
        var img = settings.Categories.First(c => c.Id == "images");
        img.ColorHex = "#B146C2"; // Old legacy purple

        var code = settings.Categories.First(c => c.Id == "code");
        code.ColorHex = "#5A62D6"; // Old legacy indigo

        settings.Clamp();

        Assert.Equal("#16A34A", img.ColorHex);
        Assert.Equal("#6366F1", code.ColorHex);
    }

    [Fact]
    public void JsonSerialization_RoundTripsThroughAppSettings()
    {
        var original = new AppSettings();
        original.FileCategories.AddCategory("3D", "#FF00FF", new[] { ".stl" });

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        var resolved = restored.FileCategories.ResolveCategory("model.stl");
        Assert.NotNull(resolved);
        Assert.Equal("3D", resolved.Name);
        Assert.Equal("#FF00FF", resolved.ColorHex);
    }
}
