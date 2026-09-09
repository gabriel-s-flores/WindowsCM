// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text;

namespace WindowsCM.Core.Actions;

// A shell invocation: cmd.exe with prebuilt Arguments, content on stdin.
// Tests fake the runner, asserting argv/stdin/timeout mapping.
public sealed record ProcessRequest(
    string FileName,
    string Arguments,
    string StandardInput,
    int TimeoutMs);

public sealed record ProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct = default);
}

// Native open/reveal behind the four ported command ids. Tests fake it,
// asserting the rewritten local path or URL; production shells out.
public interface IShellLauncher
{
    void Open(string pathOrUrl);
    void Reveal(string localPath);
}

// Production process runner (research 05 §2.2): cmd.exe with redirected
// stdin/stdout/stderr, UTF-8, no window, 30s timeout with whole-tree kill.
// Stdout/stderr drain asynchronously from the start — WaitForExit before
// ReadToEnd deadlocks on verbose children (documented Learn trap).
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
            EnableRaisingEvents = true,
        };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), ct).ConfigureAwait(false);
        process.StandardInput.Close();

        using var timeout = new CancellationTokenSource(request.TimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Raced with a natural exit just under the timeout.
            }
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), true);
        }
        // Post-true drain so the async handlers finish before we read.
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
    }
}

// Production shell launcher (research 05 §2.1): UseShellExecute must be
// set explicitly — it defaults to false on .NET Core/5+, which refuses to
// open documents. Reveal spawns explorer /select (v1); the PIDL-based
// SHOpenFolderAndSelectItems P/Invoke is a post-v1 refinement.
public sealed class ShellLauncher : IShellLauncher
{
    public void Open(string pathOrUrl) =>
        Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });

    public void Reveal(string localPath) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{localPath}\"")
        {
            UseShellExecute = true,
        });
}
