// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture.Win32;

// Punctual sequence check (never a polling loop).
public sealed class Win32SequenceProvider : ISequenceProvider
{
    public uint GetSequenceNumber() => NativeClipboard.GetClipboardSequenceNumber();
}
