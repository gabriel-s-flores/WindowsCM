// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.History;

// A single clipboard record as the user sees it (an "item" in CONTEXT.md
// vocabulary; the stored row behind it is an "entry"). The caller owns the
// clock: CapturedAt must already be UTC.
public sealed record ClipboardItem(
    ItemKind Kind,
    string Content,
    bool Pinned,
    string? Tag,
    DateTime CapturedAt,
    string? MetadataJson,
    string? Title,
    long Id = 0);
