// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Settings;

public enum TextCountMode
{
    Characters = 0,
    Words = 1,
    Lines = 2,
}

public enum BackgroundSize
{
    Cover = 0,
    Contain = 1,
}

public enum FilePreviewVisibility
{
    PreviewOnly = 0,
    InfoOnly = 1,
    PreviewOrInfo = 2,
    PreviewAndInfo = 3,
    Hidden = 4,
}

[Flags]
public enum FilePreviewTypes
{
    None = 0,
    Text = 1,
    Image = 2,
    Thumbnail = 4,
    All = Text | Image | Thumbnail,
}

// Six per-type screens (Copyous child schemas parity, 01 §5). File
// thumbnails resolve through Windows Shell providers, not GdkPixbuf — the
// visibility/types/exclusion knobs keep their meaning regardless.
public sealed class TextItemSettings
{
    public bool ShowInfo { get; set; } = false;
    public TextCountMode CountMode { get; set; } = TextCountMode.Characters;
}

public sealed class CodeItemSettings
{
    public bool SyntaxHighlighting { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowInfo { get; set; } = false;
    public TextCountMode CountMode { get; set; } = TextCountMode.Characters;
}

public sealed class ImageItemSettings
{
    public bool ShowInfo { get; set; } = false;
    public BackgroundSize Background { get; set; } = BackgroundSize.Cover;
}

public sealed class FileItemSettings
{
    public FilePreviewVisibility Visibility { get; set; } = FilePreviewVisibility.PreviewOrInfo;
    public FilePreviewTypes Types { get; set; } = FilePreviewTypes.All;
    public List<string> ExclusionGlobs { get; set; } = [];
    public BackgroundSize Background { get; set; } = BackgroundSize.Cover;
    public bool SyntaxHighlighting { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
}

public sealed class LinkItemSettings
{
    public bool ShowPreview { get; set; } = true;
    public bool ShowImage { get; set; } = true;
    public BackgroundSize ImageBackground { get; set; } = BackgroundSize.Contain;
    public DialogOrientation Orientation { get; set; } = DialogOrientation.Vertical;
    public List<string> ExclusionPatterns { get; set; } = [];

    public bool IsExcluded(string url) =>
        LinkExclusions.IsExcluded(url, ExclusionPatterns);

    public LinkPreviewOptions ToPreviewOptions() => new()
    {
        ShowPreview = ShowPreview,
        ShowImage = ShowImage,
        ExclusionPatterns = [.. ExclusionPatterns],
    };
}

public sealed class CharacterItemSettings
{
    public int MaxCharacters { get; set; } = SettingLimits.MaxCharactersDefault;
    public bool ShowUnicode { get; set; } = false;

    public void Clamp()
    {
        MaxCharacters = SettingLimits.ClampInt(
            MaxCharacters, SettingLimits.MaxCharactersMin, SettingLimits.MaxCharactersMax);
    }
}

public sealed class PerTypeSettings
{
    public TextItemSettings Text { get; set; } = new();
    public CodeItemSettings Code { get; set; } = new();
    public ImageItemSettings Image { get; set; } = new();
    public FileItemSettings File { get; set; } = new();
    public LinkItemSettings Link { get; set; } = new();
    public CharacterItemSettings Character { get; set; } = new();

    public void Clamp() => Character.Clamp();
}
