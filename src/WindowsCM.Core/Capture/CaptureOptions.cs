// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Capture;

// Capture tunables. Incognito itself is runtime state on CaptureService
// (in-memory only, never persisted) — not a setting here.
public sealed class CaptureOptions
{
    // Windows process names mapped from Copyous wmclass-exclusions.
    // Matched case-insensitively, with or without a trailing ".exe".
    public HashSet<string> ExcludedProcesses { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Copyous `max-characters` for character items.
    public int MaxCharacters { get; set; } = Classifier.DefaultMaxCharacters;

    // Copyous `update-date-on-copy`: copying from history refreshes datetime.
    public bool UpdateDateOnCopy { get; set; } = true;
}
