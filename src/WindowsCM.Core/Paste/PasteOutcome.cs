// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// How a copy-back + paste attempt ended. CopyFailed covers every
// nothing-happened case (missing item, missing image, write/inject error);
// the CopiedOnly* cases all left the item on the clipboard. Diagnostics is
// always set except on success, so refusals surface instead of vanishing.
public enum PasteStatus
{
    Pasted,
    CopiedOnly,
    CopiedOnlyForegroundLost,
    CopiedOnlyElevated,
    Failed,
}

public sealed record PasteOutcome(
    PasteStatus Status,
    PasteSequence? InjectedChord,
    string? Diagnostics);
