// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Classification;

namespace WindowsCM.Core.Capture;

// Reads the current clipboard, copying handles out immediately. The Win32
// implementation honors ownership rules (STA, open-retry, copy-out, close in
// finally); tests fake it with canned payloads.
public interface IClipboardReader
{
    ClipboardPayload? Read();
}

// Event-driven change notifications (WM_CLIPBOARDUPDATE). The monitor
// subscribes and never polls; sequence numbers are only a punctual
// activation-time check (spec: never a polling loop).
public interface IClipboardChangeSource
{
    event EventHandler? ClipboardChanged;
}

// Punctual sequence check for activation (GetClipboardSequenceNumber).
public interface ISequenceProvider
{
    uint GetSequenceNumber();
}

// Foreground process for per-process exclusions (Windows map of
// Copyous wmclass-exclusions). Null when unknown (fail open: capture).
public interface IForegroundProcess
{
    string? CurrentProcessName { get; }
}
