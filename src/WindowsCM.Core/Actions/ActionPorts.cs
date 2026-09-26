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
        process.Start();
        // Drained from the start (a verbose child blocks on a full pipe),
        // in raw chunks and capped: the line reader kept a whole
        // newline-free output in memory, however large.
        var stdout = DrainAsync(process.StandardOutput);
        var stderr = DrainAsync(process.StandardError);
        // The item is fed alongside, not before, the timed wait: the timeout
        // used to start only once the whole item was written, so a command
        // that never reads stdin (a pipe holds a few KB) blocked the write
        // forever and was never killed.
        var feeding = FeedAsync(process, request.StandardInput);

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
            await feeding.ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false), true);
        }
        process.WaitForExit();
        await feeding.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false), false);
    }

    // Output kept per stream: a command printing gigabytes used to grow the
    // tray process without bound. The rest is read and dropped, so the
    // command never blocks on a full pipe.
    internal const int MaxOutputChars = 8 * 1024 * 1024;

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var kept = new StringBuilder();
        var chunk = new char[16 * 1024];
        int read;
        while ((read = await reader.ReadAsync(chunk.AsMemory()).ConfigureAwait(false)) > 0)
        {
            var room = MaxOutputChars - kept.Length;
            if (room > 0)
            {
                kept.Append(chunk, 0, Math.Min(read, room));
            }
        }
        return kept.ToString();
    }

    // A command may exit (or close its input) without reading all of it:
    // the broken pipe used to escape and lose the command's result.
    private static async Task FeedAsync(Process process, string input)
    {
        try
        {
            await process.StandardInput.WriteAsync(input.AsMemory()).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
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
