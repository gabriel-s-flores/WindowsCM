// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class SingleInstanceCoordinatorTests
{
    private sealed class FakeLock : ISingleInstanceLock
    {
        private readonly bool _acquirable;
        public FakeLock(bool acquirable) => _acquirable = acquirable;
        public bool IsAcquired { get; private set; }
        public bool TryAcquire()
        {
            IsAcquired = _acquirable;
            return _acquirable;
        }
        public void Release() => IsAcquired = false;
        public void Dispose() { }
    }

    private sealed class FakeForwarder : IIpcForwarder
    {
        private readonly bool _result;
        public string? SeenPipe;
        public string? SeenLine;
        public TimeSpan SeenTimeout;
        public FakeForwarder(bool result) => _result = result;
        public bool TryForward(string pipeName, string line, TimeSpan timeout, out string? response)
        {
            SeenPipe = pipeName;
            SeenLine = line;
            SeenTimeout = timeout;
            response = _result ? "ok" : null;
            return _result;
        }
    }

    [Fact]
    public void FirstInstance_IsPrimaryWithoutForwarding()
    {
        var forwarder = new FakeForwarder(true);

        var outcome = SingleInstanceCoordinator.Decide(
            new FakeLock(true), forwarder,
            CliOptions.Parse(["--toggle"]), "WindowsCM.test", TimeSpan.FromSeconds(1));

        Assert.Equal(SingleInstanceOutcome.IsPrimary, outcome);
        Assert.Null(forwarder.SeenPipe);
    }

    [Fact]
    public void SecondLaunch_ForwardsCommandAndExits()
    {
        var forwarder = new FakeForwarder(true);

        var outcome = SingleInstanceCoordinator.Decide(
            new FakeLock(false), forwarder,
            CliOptions.Parse(["--show"]), "WindowsCM.test", TimeSpan.FromSeconds(1));

        Assert.Equal(SingleInstanceOutcome.Forwarded, outcome);
        Assert.Equal("WindowsCM.test", forwarder.SeenPipe);
        Assert.Equal("show", forwarder.SeenLine);
    }

    [Fact]
    public void SecondLaunch_NoArgs_ForwardsToggle()
    {
        var forwarder = new FakeForwarder(true);

        var outcome = SingleInstanceCoordinator.Decide(
            new FakeLock(false), forwarder,
            CliOptions.Parse(["app.exe"]), "WindowsCM.test");

        Assert.Equal(SingleInstanceOutcome.Forwarded, outcome);
        Assert.Equal("toggle", forwarder.SeenLine);
    }

    [Fact]
    public void ForwardFailure_NeverStartsSecondUi()
    {
        var forwarder = new FakeForwarder(false);

        var outcome = SingleInstanceCoordinator.Decide(
            new FakeLock(false), forwarder,
            CliOptions.Parse(["--toggle"]), "WindowsCM.test");

        Assert.Equal(SingleInstanceOutcome.ForwardFailed, outcome);
    }

    [Fact]
    public void DefaultTimeout_IsTwoSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), SingleInstanceCoordinator.DefaultForwardTimeout);
    }
}
