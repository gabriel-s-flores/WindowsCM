// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Capture;

// A raw clipboard observation with handles already copied out (ownership rule:
// copy immediately, never free/lock). The Win32 reader builds this; tests fake it.
// Html is the CF_HTML payload stored opaque and rewritten verbatim (spec v1).
public sealed record ClipboardPayload(
    ImageSnapshot? Image,
    FileSnapshot? Files,
    string? Text,
    IReadOnlyList<string> Formats,
    string? Html = null);
