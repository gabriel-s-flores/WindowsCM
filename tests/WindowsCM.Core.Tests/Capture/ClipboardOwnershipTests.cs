// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;

namespace WindowsCM.Core.Tests.Capture;

public sealed class ClipboardOwnershipTests
{
    [Fact]
    public void OpenRetry_SucceedsAfterTransientFailures()
    {
        var attempts = 0;
        var result = ClipboardOwnership.OpenRetry(() =>
        {
            attempts++;
            return attempts < 3 ? false : true;
        }, maxAttempts: 5, initialDelayMs: 0);

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void OpenRetry_GivesUpAfterMaxAttempts()
    {
        var attempts = 0;
        var result = ClipboardOwnership.OpenRetry(() =>
        {
            attempts++;
            return false;
        }, maxAttempts: 3, initialDelayMs: 0);

        Assert.False(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void AssertSta_OnMta_Throws()
    {
        // xUnit runs tests on a thread-pool (MTA) thread by default, so the
        // guard must refuse here — proving it actually checks the apartment.
        Assert.Throws<InvalidOperationException>(ClipboardOwnership.AssertSta);
    }
}
