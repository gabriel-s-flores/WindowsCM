// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Lifecycle;
using WindowsCM.Core.Lifecycle.Win32;

namespace WindowsCM.Core.Tests.Lifecycle;

public sealed class AutostartManagerTests
{
    [Fact]
    public void DisabledByDefault()
    {
        Assert.False(AutostartManager.IsEnabled(new MemoryRunKeyStore()));
    }

    [Fact]
    public void Enable_WritesQuotedHiddenCommand()
    {
        var store = new MemoryRunKeyStore();

        AutostartManager.Enable(store, @"C:\Program Files\WindowsCM\WindowsCM.exe");

        Assert.True(AutostartManager.IsEnabled(store));
        Assert.Equal("\"C:\\Program Files\\WindowsCM\\WindowsCM.exe\" --hidden", store.GetCommand());
    }

    [Fact]
    public void Disable_RemovesAndIsIdempotent()
    {
        var store = new MemoryRunKeyStore();
        AutostartManager.Enable(store, @"C:\apps\WindowsCM.exe");

        AutostartManager.Disable(store);
        AutostartManager.Disable(store);

        Assert.False(AutostartManager.IsEnabled(store));
        Assert.Null(store.GetCommand());
    }

    [Fact]
    public void BuildCommand_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => AutostartManager.BuildCommand("  "));
    }

    [Fact]
    public void RunValueName_IsPinned()
    {
        Assert.Equal("WindowsCM", AutostartManager.RunValueName);
    }

    [Fact]
    public void RegistryStore_RejectsEmptyValueName()
    {
        Assert.Throws<ArgumentException>(() => new RegistryRunKeyStore(" "));
    }

    [Fact]
    public void RegistryStore_AbsentTestValueReadsNull()
    {
        // Read-only against the real hive on Windows; throws before touching
        // anything elsewhere. Never writes: real writes are manual-smoke.
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(
                () => new RegistryRunKeyStore("WindowsCM-test-" + Guid.NewGuid().ToString("N")).GetCommand());
            return;
        }
        var probe = new RegistryRunKeyStore("WindowsCM-test-" + Guid.NewGuid().ToString("N"));

        Assert.Null(probe.GetCommand());
    }
}
