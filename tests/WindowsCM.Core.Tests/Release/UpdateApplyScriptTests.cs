// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text;
using WindowsCM.Core.Release;
using Xunit;

namespace WindowsCM.Core.Tests.Release;

public class UpdateApplyScriptTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PortableReplacementSwapsVerifiedFileOrPreservesOriginalOnFailure(bool stagedExists)
    {
        if (!OperatingSystem.IsWindows()) return;
        // Apostrophes, spaces and dollar signs must remain literal PowerShell paths.
        var dir = Path.Combine(Path.GetTempPath(), "WindowsCM update '$-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var exe = Path.Combine(dir, "WindowsCM.exe");
            var staged = Path.Combine(dir, "new.exe");
            await File.WriteAllTextAsync(exe, "previous executable");
            if (stagedExists) await File.WriteAllTextAsync(staged, "verified update");
            var script = Path.Combine(dir, "apply.ps1");
            await File.WriteAllTextAsync(script, UpdateApplyScript.PortableOperation(exe, staged, "fixture"), Encoding.UTF8);
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
            Assert.Equal(stagedExists ? 0 : 1, process.ExitCode);
            Assert.Equal(stagedExists ? "verified update" : "previous executable", await File.ReadAllTextAsync(exe));
            Assert.False(File.Exists(exe + ".update-fixture"));
            Assert.False(File.Exists(exe + ".backup-fixture"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
