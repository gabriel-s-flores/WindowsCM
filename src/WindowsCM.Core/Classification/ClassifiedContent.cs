// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;

namespace WindowsCM.Core.Classification;

// A classified clipboard payload. The monitor persists Text/File payloads
// directly; images persist under `<images-dir>/<FileName>` with the DB
// content set to that file:// URI (Copyous parity).
public abstract record ClassifiedContent(ItemKind Kind, string ContentHash);

// Text-like items (Text, Code, Link, Character, Color). MetadataJson is null
// in v1: code language ids arrive with the highlighter spike (issue 14).
public sealed record ClassifiedText(
    ItemKind Kind,
    string Content,
    string? MetadataJson,
    string ContentHash) : ClassifiedContent(Kind, ContentHash);

// One path (File) or \n-joined paths (Files) with `{"operation":…}` metadata.
public sealed record ClassifiedFile(
    ItemKind Kind,
    string Content,
    string MetadataJson,
    string ContentHash) : ClassifiedContent(Kind, ContentHash);

// An image payload: content bytes hash plus the file name to persist them as.
public sealed record ClassifiedImage(
    string FileName,
    string Extension,
    string ContentHash) : ClassifiedContent(ItemKind.Image, ContentHash);
