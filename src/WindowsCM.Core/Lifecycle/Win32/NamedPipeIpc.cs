// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Pipes;
using System.Text;

namespace WindowsCM.Core.Lifecycle.Win32;

// Pipe transport v1 (research 05 §10): one UTF-8 line in, one line out
// ("ok"/"unknown"), framing BOM-less. The server serves one connection at
// a time (single live instance; control commands are fast). The client
// connects with a 2s budget per the research and reports delivery as bool,
// never throwing on absence so second launches can exit with guidance.
// CurrentUserOnly applies on Windows (same user + elevation); other OSes
// fall back to None so the seam stays testable.
public sealed class NamedPipeForwarder : IIpcForwarder
{
    // BOM-less: Encoding.UTF8 emits a preamble on first write and the
    // preamble stalls line framing over pipes — always suppress it.
    internal static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // The primary answers once the command ran (a cold first popup can take
    // over a second): longer than the connect budget, but bounded — a
    // primary whose UI thread hangs never answers, and the launching
    // process used to wait for it forever.
    public static TimeSpan DefaultReplyTimeout { get; } = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _replyTimeout;

    public NamedPipeForwarder(TimeSpan? replyTimeout = null)
    {
        _replyTimeout = replyTimeout ?? DefaultReplyTimeout;
    }

    private static async Task<string?> ExchangeAsync(Stream pipe, string line, CancellationToken ct)
    {
        await pipe.WriteAsync(Utf8NoBom.GetBytes(line + "\n"), ct).ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(pipe, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        return await reader.ReadLineAsync(ct).ConfigureAwait(false);
    }

    public bool TryForward(string pipeName, string line, TimeSpan timeout, out string? response)
    {
        response = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            client.Connect(timeout);
            client.ReadMode = PipeTransmissionMode.Byte;
            // The whole exchange runs within the reply budget: on Windows the
            // write itself can block until the other end reads. The wait is
            // bounded even if a pipe operation ignores cancellation; leaving
            // closes the pipe, which ends the pending operation.
            using var replyBudget = new CancellationTokenSource(_replyTimeout);
            var exchange = ExchangeAsync(client, line, replyBudget.Token);
            if (!exchange.Wait(_replyTimeout + TimeSpan.FromSeconds(1)))
            {
                exchange.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                return false;
            }
            response = exchange.GetAwaiter().GetResult();
            return response is not null;
        }
        catch (AggregateException ex) when (ex.InnerException is TimeoutException or IOException
            or UnauthorizedAccessException or ObjectDisposedException or OperationCanceledException)
        {
            response = null;
            return false;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException
            or ObjectDisposedException or OperationCanceledException)
        {
            response = null;
            return false;
        }
    }
}

public sealed class NamedPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly IpcDispatcher _dispatcher;
    private readonly TimeSpan _clientTimeout;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private bool _disposed;

    // A connected client gets this long to send its line. The pipe serves
    // one connection at a time, so a client that never wrote used to hold
    // it for good and every later launch reported "not running".
    public static TimeSpan DefaultClientTimeout { get; } = TimeSpan.FromSeconds(5);

    public NamedPipeServer(string pipeName, IpcDispatcher dispatcher, TimeSpan? clientTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _pipeName = pipeName;
        _dispatcher = dispatcher;
        _clientTimeout = clientTimeout ?? DefaultClientTimeout;
    }

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NamedPipeServer));
        }
        _loop ??= Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or AggregateException)
            {
            }
        }
    }

    private static PipeOptions ServerOptions() =>
        OperatingSystem.IsWindows()
            ? PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
            : PipeOptions.Asynchronous;

    // Sequential handling: with a single server instance alive at a time,
    // minting the next instance while the previous still serves faults the
    // listener (max 1). One line per connection is fast enough that
    // concurrent handling buys nothing here.
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, ServerOptions());
            }
            catch (IOException)
            {
                try
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }
            try
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch (IOException)
            {
                server.Dispose();
                continue;
            }
            await HandleConnectionAsync(server).ConfigureAwait(false);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server)
    {
        using (server)
        {
            try
            {
                using var reader = new StreamReader(server, NamedPipeForwarder.Utf8NoBom, leaveOpen: true);
                using var writer = new StreamWriter(server, NamedPipeForwarder.Utf8NoBom, leaveOpen: true)
                {
                    AutoFlush = true,
                };
                string? line;
                using (var lineBudget = new CancellationTokenSource(_clientTimeout))
                {
                    line = await reader.ReadLineAsync(lineBudget.Token).ConfigureAwait(false);
                }
                string response;
                try
                {
                    response = _dispatcher.Handle(line);
                }
                catch
                {
                    // A store failure must never kill the listener, but it
                    // must not report success either: answer "error" and keep
                    // serving.
                    response = IpcProtocol.Error;
                }
                await writer.WriteLineAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _cts.Dispose();
    }
}
