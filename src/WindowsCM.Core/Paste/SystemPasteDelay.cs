// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Paste;

// Production delay for the post-focus wait (PasteOptions.PasteDelayMs).
public sealed class SystemPasteDelay : IPasteDelay
{
    public Task Delay(int milliseconds, CancellationToken ct = default) =>
        Task.Delay(Math.Max(0, milliseconds), ct);
}
