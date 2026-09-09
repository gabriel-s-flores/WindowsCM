// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// What a copy-back writes to the clipboard. Mirrors ClipboardPayload (the
// read side): at most one family is set. Text carries the verbatim content
// plus the opaque CF_HTML envelope when the item has one; ImagePng is the
// persisted PNG with ImageDib its CF_DIB conversion (null when the PNG
// flavor is outside the v1 scope — the writer then offers PNG only);
// FileLocalPaths are absolute local paths always written forcing copy.
public sealed record ClipboardContents(
    string? Text = null,
    string? Html = null,
    byte[]? ImagePng = null,
    byte[]? ImageDib = null,
    IReadOnlyList<string>? FileLocalPaths = null);
