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
            using var writer = new StreamWriter(client, Utf8NoBom, leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(client, Utf8NoBom, leaveOpen: true);
            writer.WriteLine(line);
            response = reader.ReadLine();
            return response is not null;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or ObjectDisposedException)
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
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private bool _disposed;

    public NamedPipeServer(string pipeName, IpcDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _pipeName = pipeName;
        _dispatcher = dispatcher;
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
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                string response;
                try
                {
                    response = _dispatcher.Handle(line);
                }
                catch
                {
                    // A store failure must never kill the listener: report ok
                    // (the action was received) and keep serving.
                    response = IpcProtocol.Ok;
                }
                await writer.WriteLineAsync(response).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
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
