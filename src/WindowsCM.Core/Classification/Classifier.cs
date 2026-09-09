// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Classification;

// Turns raw clipboard payloads into typed items (Copyous `getContent` +
// `convertContent` parity). Pure logic: no clipboard, disk, or settings
// access — the monitor (issue 11) observes, this decides.
public static class Classifier
{
    // Copyous `max-characters` default for character items.
    public const int DefaultMaxCharacters = 1;

    // Text sub-pipeline: Link → Character → Color → Code → Text fallback.
    // Detection runs on the trimmed text; the stored content stays verbatim.
    public static ClassifiedText? ClassifyText(string? text, int maxCharacters = DefaultMaxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var trimmed = text.Trim();
        ItemKind kind = LinkDetector.IsLink(trimmed) ? ItemKind.Link
            : !GraphemeCounter.HasMoreThan(trimmed, maxCharacters) ? ItemKind.Character
            : ColorParser.IsColor(trimmed) ? ItemKind.Color
            : CodeDetector.IsCode(trimmed) ? ItemKind.Code
            : ItemKind.Text;
        return new ClassifiedText(kind, text, null, ClipboardHash.Md5Hex(text));
    }

    // One path becomes File, many become Files with \n-joined content.
    public static ClassifiedFile? ClassifyFiles(FileSnapshot? files)
    {
        if (files is null || files.Paths.Count == 0)
        {
            return null;
        }
        var kind = files.Paths.Count == 1 ? ItemKind.File : ItemKind.Files;
        var content = files.Paths.Count == 1 ? files.Paths[0] : string.Join("\n", files.Paths);
        var metadata = $"{{\"operation\":\"{files.Operation.ToString().ToLowerInvariant()}\"}}";
        return new ClassifiedFile(kind, content, metadata, ClipboardHash.FileHash(files.Paths));
    }

    // Zero-byte images and blank mimetypes are discarded (Copyous parity).
    public static ClassifiedImage? ClassifyImage(ImageSnapshot? image)
    {
        if (image is null || image.Data.Length == 0 || string.IsNullOrWhiteSpace(image.MimeType))
        {
            return null;
        }
        var hash = ClipboardHash.Md5Hex(image.Data);
        var extension = ImageContent.ExtensionFor(image.MimeType);
        return new ClassifiedImage($"{hash}.{extension}", extension, hash);
    }

    // Probe order Image > File > Text: the richest representation wins.
    // A sensitive format rejects the whole payload before anything else.
    public static ClassifiedContent? Probe(
        ImageSnapshot? image,
        FileSnapshot? files,
        string? text,
        int maxCharacters = DefaultMaxCharacters,
        IEnumerable<string>? formats = null)
    {
        if (formats is not null && SensitiveHints.ContainsSensitive(formats))
        {
            return null;
        }
        if (ClassifyImage(image) is { } img)
        {
            return img;
        }
        if (ClassifyFiles(files) is { } file)
        {
            return file;
        }
        return ClassifyText(text, maxCharacters);
    }
}
