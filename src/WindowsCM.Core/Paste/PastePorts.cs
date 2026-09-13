// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// Writes planned contents to the clipboard. The Win32 implementation honors
// the ownership rules (STA, open-retry, handles copied out, close in
// finally); tests fake it with recorded contents.
public interface IClipboardWriter
{
    void Write(ClipboardContents contents);
}

public sealed class ClipboardWriteException : Exception
{
    public ClipboardWriteException(string message)
        : base(message)
    {
    }
}

// Injects the paste chord into the foreground app via SendInput (research
// 02: serial delivery, subject to UIPI). Tests fake it, asserting the chord.
public interface IPasteInjector
{
    void Inject(PasteSequence sequence);
}

public sealed class PasteInjectionException : Exception
{
    public PasteInjectionException(string message)
        : base(message)
    {
    }
}

// Foreground target for the paste flow. The UI captures the handle at hotkey
// time and passes it to the orchestrator, which asserts it before injecting
// (research 02 § "Colar no app focado").
public interface IForegroundWindow
{
    IntPtr GetCurrent();
    bool RestoreForeground(IntPtr hwnd);
}

// Integrity probe behind the elevated-target diagnostics. SendInput fails
// silently against higher-integrity apps (UIPI, research 02), so the
// orchestrator predicts the refusal and reports it instead of vanishing.
public interface IElevationProbe
{
    bool IsCurrentProcessElevated();
    bool IsTargetElevated(IntPtr hwnd);
}

// Loads persisted image PNG bytes for a stored file:// content. Null when
// the file is gone (crash leftover or user deletion).
public interface IImageFileReader
{
    byte[]? LoadPng(string content);
}

// Injectable post-focus delay (Testing Decisions: tunables, never sleeps).
public interface IPasteDelay
{
    Task Delay(int milliseconds, CancellationToken ct = default);
}
