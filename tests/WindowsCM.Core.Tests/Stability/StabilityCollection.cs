// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tests.Stability;

// Soak, scale and fuzz tests saturate the CPU on purpose. Run in parallel
// with the rest of the suite they starved the timing-sensitive pipe tests
// (flaky failures) and got noisy timings themselves, so xUnit runs this
// collection on its own after the parallel ones.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StabilityCollection
{
    public const string Name = "Stability (isolated)";
}
