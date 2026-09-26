// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace WindowsCM.Core.Transfer;

// Ultra-lightweight, zero-admin-dependency HTTP/1.1 server running on TcpListener.
// Handles local Wi-Fi / LAN transfers: serves mobile web interface, handles file/media downloads,
// and receives uploaded files and text from mobile devices directly into the PC clipboard.
public sealed class MiniTransferHttpServer : IDisposable
{
    // Shared items stay downloadable this long; they used to stay forever
    // (text shares in memory too), behind 32-bit tokens.
    public static readonly TimeSpan ShareLifetime = TimeSpan.FromHours(24);

    // A connection that sends or accepts nothing for this long is dropped: a
    // phone that left the network mid-upload used to hold its connection
    // (and, before streaming, its buffered body) until the app exited.
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);

    // Text sent from the phone; files are streamed to disk instead.
    public const int MaxTextBodyBytes = 16 * 1024 * 1024;

    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxConnections = 16;

    private readonly ConcurrentDictionary<string, SharedItemSession> _sharedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _connections = new(MaxConnections);
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _idleTimeout;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private int _port;

    public int Port => _port;
    public bool IsRunning => _listener != null;
    public string IncomingFolder { get; set; }

    // Required to send anything to this PC: it travels in the QR code shown
    // by "Receive from phone". /api/upload used to accept text and files
    // from anyone on the network (a café Wi-Fi) and put them straight on
    // the clipboard.
    public string UploadKey { get; } = NewToken();

    public string UploadPagePath => "/u/" + UploadKey;

    public event Action<IncomingTransferPayload>? PayloadReceived;

    public MiniTransferHttpServer(
        int preferredPort = 58921,
        string? incomingFolder = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? idleTimeout = null)
    {
        _port = preferredPort;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
        IncomingFolder = incomingFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "WindowsCM Transfers");
    }

    // Throws when no port can be bound (SocketException): the caller shows
    // why, and IsRunning stays false.
    public void Start()
    {
        if (IsRunning) return;

        TcpListener listener;
        // Try preferred port first; if busy, let OS assign an ephemeral free port (0)
        try
        {
            listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
        }
        catch (SocketException)
        {
            listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
        }

        _cts = new CancellationTokenSource();
        _listener = listener;
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var token = _cts.Token;
        _listenTask = Task.Run(() => AcceptLoopAsync(listener, token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }
        _listener = null;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    public SharedItemSession RegisterShare(
        string title,
        string kindLabel,
        string? filePath = null,
        IReadOnlyList<string>? filePaths = null,
        string? textContent = null,
        byte[]? rawBytes = null,
        long? itemId = null)
    {
        PruneExpiredShares();
        var token = NewToken();
        var fileName = !string.IsNullOrEmpty(filePath)
            ? Path.GetFileName(filePath)
            : (filePaths != null && filePaths.Count > 0 ? Path.GetFileName(filePaths[0]) : "item.txt");

        var contentType = ResolveContentType(fileName, textContent != null);
        long fileSize = 0;

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            fileSize = new FileInfo(filePath).Length;
        }
        else if (rawBytes != null)
        {
            fileSize = rawBytes.Length;
        }
        else if (textContent != null)
        {
            fileSize = Encoding.UTF8.GetByteCount(textContent);
        }

        var session = new SharedItemSession(
            Token: token,
            ItemId: itemId,
            Title: title,
            KindLabel: kindLabel,
            FilePath: filePath,
            FilePaths: filePaths,
            TextContent: textContent,
            RawBytes: rawBytes,
            FileName: fileName,
            ContentType: contentType,
            FileSize: fileSize,
            CreatedAt: _utcNow());

        _sharedSessions[token] = session;
        return session;
    }

    public void UnregisterShare(string token)
    {
        _sharedSessions.TryRemove(token, out _);
    }

    public SharedItemSession? GetShare(string token)
    {
        if (!_sharedSessions.TryGetValue(token, out var session))
        {
            return null;
        }
        if (_utcNow() - session.CreatedAt > ShareLifetime)
        {
            _sharedSessions.TryRemove(token, out _);
            return null;
        }
        return session;
    }

    public string BuildUrl(IPAddress localIp, string path)
    {
        var cleanPath = path.StartsWith('/') ? path : "/" + path;
        return $"http://{localIp}:{_port}{cleanPath}";
    }

    public string BuildUploadUrl(IPAddress localIp) => BuildUrl(localIp, UploadPagePath);

    private void PruneExpiredShares()
    {
        var now = _utcNow();
        foreach (var (token, session) in _sharedSessions)
        {
            if (now - session.CreatedAt > ShareLifetime)
            {
                _sharedSessions.TryRemove(token, out _);
            }
        }
    }

    // 128 random bits.
    private static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private bool IsUploadKey(string candidate) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(UploadKey));

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
                continue;
            }
            // Bounded: a flood of connections cannot pile up handlers.
            if (!_connections.Wait(0))
            {
                client.Dispose();
                continue;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClientAsync(client, ct).ConfigureAwait(false);
                }
                finally
                {
                    _connections.Release();
                }
            });
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCt)
    {
        using (client)
        using (var idle = CancellationTokenSource.CreateLinkedTokenSource(serverCt))
        {
            // Re-armed on every read and write: only a silent peer times out,
            // never a long transfer.
            void Touch() => idle.CancelAfter(_idleTimeout);
            Touch();
            var ct = idle.Token;
            var stream = client.GetStream();
            try
            {
                var (head, initialBody) = await ReadHeadAsync(stream, Touch, ct).ConfigureAwait(false);
                if (head is null)
                {
                    return;
                }

                var requestLines = head.Split("\r\n");
                var requestLineParts = requestLines[0].Split(' ');
                if (requestLineParts.Length < 2)
                {
                    await SendTextAsync(stream, 400, "Bad Request", "Bad Request", ct).ConfigureAwait(false);
                    return;
                }

                var method = requestLineParts[0].ToUpperInvariant();
                var rawUrl = requestLineParts[1];
                var path = rawUrl.Split('?')[0];

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < requestLines.Length; i++)
                {
                    var sep = requestLines[i].IndexOf(':');
                    if (sep > 0)
                    {
                        var key = requestLines[i][..sep].Trim();
                        var val = requestLines[i][(sep + 1)..].Trim();
                        headers[key] = val;
                    }
                }

                var host = headers.GetValueOrDefault("Host", "localhost");

                // Route Dispatcher
                if (method == "GET")
                {
                    await HandleGetAsync(stream, path, rawUrl, host, Touch, ct).ConfigureAwait(false);
                }
                else if (method == "POST" && path.StartsWith("/api/upload", StringComparison.OrdinalIgnoreCase))
                {
                    var key = path["/api/upload".Length..].Trim('/');
                    if (!IsUploadKey(key))
                    {
                        await SendTextAsync(stream, 403, "Forbidden", "Scan the QR code shown by WindowsCM again.", ct).ConfigureAwait(false);
                        return;
                    }
                    await HandlePostUploadAsync(stream, headers, initialBody, Touch, ct).ConfigureAwait(false);
                }
                else
                {
                    await SendTextAsync(stream, 405, "Method Not Allowed", "Method Not Allowed", ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // The peer went away or went silent: nothing to answer.
            }
            catch (Exception ex)
            {
                try
                {
                    using var answer = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await SendTextAsync(stream, 500, "Internal Server Error", "Error: " + ex.Message, answer.Token).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }

    // Reads up to the end of the headers, however the request arrives in
    // TCP segments (a single read used to drop a request whose headers came
    // in two). Returns the bytes of the body read along with them.
    private static async Task<(string? Head, byte[] InitialBody)> ReadHeadAsync(
        NetworkStream stream, Action touch, CancellationToken ct)
    {
        var buffer = new byte[MaxHeaderBytes];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), ct).ConfigureAwait(false);
            touch();
            if (read <= 0)
            {
                return (null, []);
            }
            var searchFrom = Math.Max(0, filled - 3);
            filled += read;
            var end = IndexOfHeadEnd(buffer, searchFrom, filled);
            if (end >= 0)
            {
                return (Encoding.UTF8.GetString(buffer, 0, end), buffer[(end + 4)..filled]);
            }
        }
        return (null, []);
    }

    private static int IndexOfHeadEnd(byte[] buffer, int from, int to)
    {
        var at = buffer.AsSpan(from, to - from).IndexOf("\r\n\r\n"u8);
        return at < 0 ? -1 : from + at;
    }

    private async Task HandleGetAsync(NetworkStream stream, string path, string rawUrl, string host, Action touch, CancellationToken ct)
    {
        if (path.StartsWith("/u/", StringComparison.OrdinalIgnoreCase) && IsUploadKey(path[3..].Trim('/')))
        {
            var html = MobileWebTemplate.RenderUploadPage(host, uploadEndpoint: "/api/upload/" + UploadKey);
            await SendResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html), ct).ConfigureAwait(false);
            return;
        }

        if (path == "/" || path.Equals("/upload", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/u/", StringComparison.OrdinalIgnoreCase))
        {
            // No (or an old) key: send the user back to the QR code.
            var html = MobileWebTemplate.RenderUploadLinkInvalidPage();
            await SendResponseAsync(stream, 403, "Forbidden", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html), ct).ConfigureAwait(false);
            return;
        }

        if (path.Equals("/api/ping", StringComparison.OrdinalIgnoreCase))
        {
            var json = "{\"status\":\"online\"}";
            await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes(json), ct).ConfigureAwait(false);
            return;
        }

        // Shared item page: /d/{token}
        if (path.StartsWith("/d/", StringComparison.OrdinalIgnoreCase))
        {
            var token = path[3..].Trim('/');
            if (GetShare(token) is { } session)
            {
                var html = MobileWebTemplate.RenderDownloadPage(session, host);
                await SendResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html), ct).ConfigureAwait(false);
                return;
            }

            var notFoundMsg = WindowsCM.Core.Localization.LocalizationManager.IsPortuguese
                ? "<h1>Item não encontrado ou expirado.</h1>"
                : "<h1>Item not found or expired.</h1>";
            await SendResponseAsync(stream, 404, "Not Found", "text/html; charset=utf-8",
                Encoding.UTF8.GetBytes(notFoundMsg), ct).ConfigureAwait(false);
            return;
        }

        // Shared file stream: /file/{token}
        if (path.StartsWith("/file/", StringComparison.OrdinalIgnoreCase))
        {
            var token = path[6..].Trim('/');
            if (GetShare(token) is { } session)
            {
                var isDownload = rawUrl.Contains("download=1", StringComparison.OrdinalIgnoreCase);
                await ServeSessionFileAsync(stream, session, isDownload, touch, ct).ConfigureAwait(false);
                return;
            }

            await SendTextAsync(stream, 404, "Not Found", "File Not Found", ct).ConfigureAwait(false);
            return;
        }

        await SendTextAsync(stream, 404, "Not Found", "Not Found", ct).ConfigureAwait(false);
    }

    private static async Task ServeSessionFileAsync(
        NetworkStream stream, SharedItemSession session, bool isDownload, Action touch, CancellationToken ct)
    {
        var disposition = isDownload ? "attachment" : "inline";
        var safeFileName = Uri.EscapeDataString(session.FileName);

        if (!string.IsNullOrEmpty(session.FilePath) && File.Exists(session.FilePath))
        {
            using var fs = new FileStream(session.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, true);
            var header = $"HTTP/1.1 200 OK\r\n" +
                         $"Content-Type: {session.ContentType}\r\n" +
                         $"Content-Length: {fs.Length}\r\n" +
                         $"Content-Disposition: {disposition}; filename=\"{safeFileName}\"\r\n" +
                         $"Connection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.UTF8.GetBytes(header), ct).ConfigureAwait(false);
            var chunk = new byte[65536];
            int read;
            while ((read = await fs.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                await stream.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
                touch();
            }
            return;
        }

        if (session.RawBytes != null)
        {
            var header = $"HTTP/1.1 200 OK\r\n" +
                         $"Content-Type: {session.ContentType}\r\n" +
                         $"Content-Length: {session.RawBytes.Length}\r\n" +
                         $"Content-Disposition: {disposition}; filename=\"{safeFileName}\"\r\n" +
                         $"Connection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.UTF8.GetBytes(header), ct).ConfigureAwait(false);
            await stream.WriteAsync(session.RawBytes, ct).ConfigureAwait(false);
            return;
        }

        if (session.TextContent != null)
        {
            var textBytes = Encoding.UTF8.GetBytes(session.TextContent);
            await SendResponseAsync(stream, 200, "OK", "text/plain; charset=utf-8", textBytes, ct).ConfigureAwait(false);
            return;
        }

        await SendTextAsync(stream, 404, "Not Found", "File content missing", ct).ConfigureAwait(false);
    }

    private async Task HandlePostUploadAsync(
        NetworkStream stream, Dictionary<string, string> headers, byte[] initialBody, Action touch, CancellationToken ct)
    {
        headers.TryGetValue("Content-Type", out var contentType);
        if (!headers.TryGetValue("Content-Length", out var contentLengthStr)
            || !long.TryParse(contentLengthStr, out var contentLength) || contentLength < 0)
        {
            await SendTextAsync(stream, 411, "Length Required", "Length Required", ct).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(contentType))
        {
            await SendTextAsync(stream, 400, "Bad Request", "Missing Content-Type", ct).ConfigureAwait(false);
            return;
        }

        var body = new RequestBodyStream(initialBody, stream, contentLength);

        // 1. JSON Text Upload
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            if (contentLength > MaxTextBodyBytes)
            {
                await SendTextAsync(stream, 413, "Payload Too Large", "Text too large", ct).ConfigureAwait(false);
                return;
            }
            using var ms = new MemoryStream((int)contentLength);
            var buf = new byte[8192];
            int read;
            while ((read = await body.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                ms.Write(buf, 0, read);
                touch();
            }

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(ms.ToArray());
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("text", out var textProp)
                    && textProp.ValueKind == JsonValueKind.String)
                {
                    text = textProp.GetString();
                }
            }
            catch (JsonException)
            {
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var payload = new IncomingTransferPayload(text, Array.Empty<IncomingFile>(), DateTime.UtcNow);
                PayloadReceived?.Invoke(payload);
                await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"), ct).ConfigureAwait(false);
                return;
            }

            await SendTextAsync(stream, 400, "Bad Request", "No text provided", ct).ConfigureAwait(false);
            return;
        }

        // 2. Multipart File Upload, streamed to disk
        if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var boundaryIdx = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (boundaryIdx < 0)
            {
                await SendTextAsync(stream, 400, "Bad Request", "Missing boundary", ct).ConfigureAwait(false);
                return;
            }

            var boundary = contentType[(boundaryIdx + 9)..].Split(';')[0].Trim('"', ' ');
            Directory.CreateDirectory(IncomingFolder);

            IReadOnlyList<IncomingFile> savedFiles;
            try
            {
                savedFiles = await new MultipartFileReceiver(IncomingFolder)
                    .ReceiveAsync(body, boundary, touch, ct).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                await SendTextAsync(stream, 400, "Bad Request", "Malformed upload", ct).ConfigureAwait(false);
                return;
            }

            if (savedFiles.Count > 0)
            {
                var payload = new IncomingTransferPayload(null, savedFiles, DateTime.UtcNow);
                PayloadReceived?.Invoke(payload);
                await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"), ct).ConfigureAwait(false);
                return;
            }

            await SendTextAsync(stream, 400, "Bad Request", "No valid files uploaded", ct).ConfigureAwait(false);
            return;
        }

        await SendTextAsync(stream, 415, "Unsupported Media Type", "Unsupported Content-Type", ct).ConfigureAwait(false);
    }

    private static Task SendTextAsync(NetworkStream stream, int statusCode, string statusReason, string text, CancellationToken ct) =>
        SendResponseAsync(stream, statusCode, statusReason, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), ct);

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string statusReason, string contentType, byte[] body, CancellationToken ct)
    {
        var header = $"HTTP/1.1 {statusCode} {statusReason}\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     $"Connection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), ct).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, ct).ConfigureAwait(false);
        }
    }

    // The request body: bytes that arrived with the headers, then the rest
    // of the socket, never past Content-Length.
    private sealed class RequestBodyStream(byte[] initial, Stream network, long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var remaining = length - _position;
            if (remaining <= 0 || buffer.Length == 0)
            {
                return 0;
            }
            var wanted = (int)Math.Min(buffer.Length, remaining);
            int read;
            if (_position < initial.Length)
            {
                read = (int)Math.Min(wanted, initial.Length - _position);
                initial.AsMemory((int)_position, read).CopyTo(buffer);
            }
            else
            {
                read = await network.ReadAsync(buffer[..wanted], ct).ConfigureAwait(false);
            }
            _position += read;
            return read;
        }
    }

    private static string ResolveContentType(string fileName, bool isText)
    {
        if (isText) return "text/plain; charset=utf-8";
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".pdf" => "application/pdf",
            ".zip" or ".rar" or ".7z" => "application/zip",
            ".txt" => "text/plain; charset=utf-8",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
