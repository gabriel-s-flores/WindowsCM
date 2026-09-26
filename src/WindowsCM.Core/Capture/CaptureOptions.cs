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

    // History limits (Copyous `history-length` / `history-time`), enforced
    // after every stored capture so a long session never outgrows them.
    // 0 disables a rule; production copies HistorySettings here.
    public int HistoryMaxItems { get; set; }

    public int HistoryMaxAgeMinutes { get; set; }

    // Protection flags for the limits (Behavior settings parity).
    public bool ProtectPinned { get; set; } = true;

    public bool ProtectTagged { get; set; } = true;

    // How often, at most, image files no stored item references are
    // deleted while the app runs (checked after a stored capture). The
    // startup sweep alone left evicted screenshots on disk for weeks.
    public TimeSpan OrphanImageSweepInterval { get; set; } = TimeSpan.FromMinutes(15);

    // Off when the history in use is not the user's real one (the default
    // location standing in for an unmounted drive, a memory-only session,
    // a freshly recreated file): the images folder is shared, so a sweep
    // against that history would delete the real one's images.
    public bool SweepOrphanImages { get; set; } = true;
}
