// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

// Clipboard ownership rules (research 02 § "Regras de ownership") as testable
// helpers. The Win32 reader calls these; the rules themselves are pure:
// - STA thread (WPF is STA; Forms/WPF Clipboard demand it)
// - OpenClipboard retry with backoff (another window may hold it)
// - Handles copied immediately, never freed/locked, Close in finally
//   (enforced in Win32ClipboardReader, reviewed not unit-tested: there is no
//   real clipboard in CI).
public static class ClipboardOwnership
{
    public static void AssertSta()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "Clipboard access requires an STA thread.");
        }
    }

    // Tries tryOpen up to maxAttempts with exponential backoff. The delay is
    // injectable so tests pass 0 and never sleep; production passes ~10ms.
    public static bool OpenRetry(Func<bool> tryOpen, int maxAttempts = 5, int initialDelayMs = 10)
    {
        var delay = initialDelayMs;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (tryOpen())
            {
                return true;
            }
            if (attempt < maxAttempts && delay > 0)
            {
                Thread.Sleep(delay);
                delay *= 2;
            }
        }
        return false;
    }
}
