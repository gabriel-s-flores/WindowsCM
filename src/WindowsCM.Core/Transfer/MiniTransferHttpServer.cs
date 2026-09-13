// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WindowsCM.Core.Transfer;

// Ultra-lightweight, zero-admin-dependency HTTP/1.1 server running on TcpListener.
// Handles local Wi-Fi / LAN transfers: serves mobile web interface, handles file/media downloads,
// and receives uploaded files and text from mobile devices directly into the PC clipboard.
public sealed class MiniTransferHttpServer : IDisposable
{
    private readonly ConcurrentDictionary<string, SharedItemSession> _sharedSessions = new(StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private int _port;

    public int Port => _port;
    public bool IsRunning => _listener != null;
    public string IncomingFolder { get; set; }

    public event Action<IncomingTransferPayload>? PayloadReceived;

    public MiniTransferHttpServer(int preferredPort = 58921, string? incomingFolder = null)
    {
        _port = preferredPort;
        IncomingFolder = incomingFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "WindowsCM Transfers");
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();

        // Try preferred port first; if busy, let OS assign an ephemeral free port (0)
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
        }
        catch (SocketException)
        {
            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
        }

        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
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
        var token = Guid.NewGuid().ToString("N")[..8];
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
            CreatedAt: DateTime.UtcNow);

        _sharedSessions[token] = session;
        return session;
    }

    public void UnregisterShare(string token)
    {
        _sharedSessions.TryRemove(token, out _);
    }

    public SharedItemSession? GetShare(string token)
    {
        _sharedSessions.TryGetValue(token, out var session);
        return session;
    }

    public string BuildUrl(IPAddress localIp, string path)
    {
        var cleanPath = path.StartsWith('/') ? path : "/" + path;
        return $"http://{localIp}:{_port}{cleanPath}";
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                // Read HTTP Request Headers
                var headerBuffer = new byte[8192];
                var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), ct).ConfigureAwait(false);
                if (headerBytesRead <= 0) return;

                var rawHeaders = Encoding.UTF8.GetString(headerBuffer, 0, headerBytesRead);
                var headerEndIdx = rawHeaders.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEndIdx < 0) return;

                var requestLines = rawHeaders[..headerEndIdx].Split("\r\n");
                var requestLineParts = requestLines[0].Split(' ');
                if (requestLineParts.Length < 2) return;

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
                    await HandleGetAsync(stream, path, rawUrl, host, ct).ConfigureAwait(false);
                }
                else if (method == "POST" && path.Equals("/api/upload", StringComparison.OrdinalIgnoreCase))
                {
                    var bodyStart = headerEndIdx + 4;
                    var bodyBytesInHeader = headerBytesRead - bodyStart;
                    var initialBody = new byte[bodyBytesInHeader];
                    if (bodyBytesInHeader > 0)
                    {
                        Array.Copy(headerBuffer, bodyStart, initialBody, 0, bodyBytesInHeader);
                    }

                    await HandlePostUploadAsync(stream, headers, initialBody, ct).ConfigureAwait(false);
                }
                else
                {
                    await SendResponseAsync(stream, 405, "Method Not Allowed", "text/plain", Encoding.UTF8.GetBytes("Method Not Allowed")).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var err = Encoding.UTF8.GetBytes("Error: " + ex.Message);
                    await SendResponseAsync(stream, 500, "Internal Server Error", "text/plain", err).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }

    private async Task HandleGetAsync(NetworkStream stream, string path, string rawUrl, string host, CancellationToken ct)
    {
        if (path == "/" || path.Equals("/upload", StringComparison.OrdinalIgnoreCase))
        {
            var html = MobileWebTemplate.RenderUploadPage(host);
            await SendResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html)).ConfigureAwait(false);
            return;
        }

        if (path.Equals("/api/ping", StringComparison.OrdinalIgnoreCase))
        {
            var json = "{\"status\":\"online\"}";
            await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
            return;
        }

        // Shared item page: /d/{token}
        if (path.StartsWith("/d/", StringComparison.OrdinalIgnoreCase))
        {
            var token = path[3..].Trim('/');
            if (_sharedSessions.TryGetValue(token, out var session))
            {
                var html = MobileWebTemplate.RenderDownloadPage(session, host);
                await SendResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html)).ConfigureAwait(false);
                return;
            }

            var notFoundMsg = WindowsCM.Core.Localization.LocalizationManager.IsPortuguese
                ? "<h1>Item não encontrado ou expirado.</h1>"
                : "<h1>Item not found or expired.</h1>";
            await SendResponseAsync(stream, 404, "Not Found", "text/html; charset=utf-8",
                Encoding.UTF8.GetBytes(notFoundMsg)).ConfigureAwait(false);
            return;
        }

        // Shared file stream: /file/{token}
        if (path.StartsWith("/file/", StringComparison.OrdinalIgnoreCase))
        {
            var token = path[6..].Trim('/');
            if (_sharedSessions.TryGetValue(token, out var session))
            {
                var isDownload = rawUrl.Contains("download=1", StringComparison.OrdinalIgnoreCase);
                await ServeSessionFileAsync(stream, session, isDownload, ct).ConfigureAwait(false);
                return;
            }

            await SendResponseAsync(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("File Not Found")).ConfigureAwait(false);
            return;
        }

        await SendResponseAsync(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("Not Found")).ConfigureAwait(false);
    }

    private async Task ServeSessionFileAsync(NetworkStream stream, SharedItemSession session, bool isDownload, CancellationToken ct)
    {
        var disposition = isDownload ? "attachment" : "inline";
        var safeFileName = Uri.EscapeDataString(session.FileName);

        if (!string.IsNullOrEmpty(session.FilePath) && File.Exists(session.FilePath))
        {
            var fileInfo = new FileInfo(session.FilePath);
            var header = $"HTTP/1.1 200 OK\r\n" +
                         $"Content-Type: {session.ContentType}\r\n" +
                         $"Content-Length: {fileInfo.Length}\r\n" +
                         $"Content-Disposition: {disposition}; filename=\"{safeFileName}\"\r\n" +
                         $"Connection: close\r\n\r\n";

            var headerBytes = Encoding.UTF8.GetBytes(header);
            await stream.WriteAsync(headerBytes, ct).ConfigureAwait(false);

            using var fs = new FileStream(session.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            await fs.CopyToAsync(stream, ct).ConfigureAwait(false);
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
            await SendResponseAsync(stream, 200, "OK", "text/plain; charset=utf-8", textBytes).ConfigureAwait(false);
            return;
        }

        await SendResponseAsync(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("File content missing")).ConfigureAwait(false);
    }

    private async Task HandlePostUploadAsync(NetworkStream stream, Dictionary<string, string> headers, byte[] initialBody, CancellationToken ct)
    {
        headers.TryGetValue("Content-Type", out var contentType);
        headers.TryGetValue("Content-Length", out var contentLengthStr);
        long.TryParse(contentLengthStr, out var contentLength);

        if (string.IsNullOrEmpty(contentType))
        {
            await SendResponseAsync(stream, 400, "Bad Request", "text/plain", Encoding.UTF8.GetBytes("Missing Content-Type")).ConfigureAwait(false);
            return;
        }

        // 1. JSON Text Upload
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            ms.Write(initialBody, 0, initialBody.Length);
            var remaining = contentLength - initialBody.Length;
            if (remaining > 0)
            {
                var buf = new byte[8192];
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, remaining)), ct).ConfigureAwait(false);
                    if (read <= 0) break;
                    ms.Write(buf, 0, read);
                    remaining -= read;
                }
            }

            var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    text = textProp.GetString();
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var payload = new IncomingTransferPayload(text, Array.Empty<IncomingFile>(), DateTime.UtcNow);
                PayloadReceived?.Invoke(payload);
                await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"status\":\"ok\"}")).ConfigureAwait(false);
                return;
            }

            await SendResponseAsync(stream, 400, "Bad Request", "text/plain", Encoding.UTF8.GetBytes("No text provided")).ConfigureAwait(false);
            return;
        }

        // 2. Multipart File Upload
        if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var boundaryIdx = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (boundaryIdx < 0)
            {
                await SendResponseAsync(stream, 400, "Bad Request", "text/plain", Encoding.UTF8.GetBytes("Missing boundary")).ConfigureAwait(false);
                return;
            }

            var boundary = contentType[(boundaryIdx + 9)..].Split(';')[0].Trim('"', ' ');
            Directory.CreateDirectory(IncomingFolder);

            var savedFiles = await ParseAndSaveMultipartFilesAsync(stream, boundary, contentLength, initialBody, ct).ConfigureAwait(false);

            if (savedFiles.Count > 0)
            {
                var payload = new IncomingTransferPayload(null, savedFiles, DateTime.UtcNow);
                PayloadReceived?.Invoke(payload);
                await SendResponseAsync(stream, 200, "OK", "application/json", Encoding.UTF8.GetBytes("{\"status\":\"ok\"}")).ConfigureAwait(false);
                return;
            }

            await SendResponseAsync(stream, 400, "Bad Request", "text/plain", Encoding.UTF8.GetBytes("No valid files uploaded")).ConfigureAwait(false);
            return;
        }

        await SendResponseAsync(stream, 415, "Unsupported Media Type", "text/plain", Encoding.UTF8.GetBytes("Unsupported Content-Type")).ConfigureAwait(false);
    }

    private async Task<List<IncomingFile>> ParseAndSaveMultipartFilesAsync(
        NetworkStream stream,
        string boundary,
        long contentLength,
        byte[] initialBody,
        CancellationToken ct)
    {
        var result = new List<IncomingFile>();
        using var bodyStream = new MemoryStream();
        bodyStream.Write(initialBody, 0, initialBody.Length);
        var remaining = contentLength - initialBody.Length;
        var readBuf = new byte[65536];

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(readBuf.Length, remaining);
            var read = await stream.ReadAsync(readBuf.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read <= 0) break;
            bodyStream.Write(readBuf, 0, read);
            remaining -= read;
        }

        var fullBytes = bodyStream.ToArray();
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
        var endBoundaryBytes = Encoding.UTF8.GetBytes("--" + boundary + "--");

        int pos = 0;
        while (pos < fullBytes.Length)
        {
            var partStart = IndexOf(fullBytes, boundaryBytes, pos);
            if (partStart < 0) break;

            pos = partStart + boundaryBytes.Length;
            if (pos >= fullBytes.Length) break;

            // Check if final boundary
            if (pos + 2 <= fullBytes.Length && fullBytes[pos] == '-' && fullBytes[pos + 1] == '-')
            {
                break;
            }

            // Skip CRLF after boundary
            if (pos + 2 <= fullBytes.Length && fullBytes[pos] == '\r' && fullBytes[pos + 1] == '\n')
            {
                pos += 2;
            }

            // Next boundary is where this part ends
            var nextPart = IndexOf(fullBytes, boundaryBytes, pos);
            var partEnd = nextPart >= 0 ? nextPart : fullBytes.Length;

            // Part headers end with \r\n\r\n
            var headerDelim = Encoding.UTF8.GetBytes("\r\n\r\n");
            var headerEnd = IndexOf(fullBytes, headerDelim, pos, partEnd - pos);
            if (headerEnd < 0)
            {
                pos = partEnd;
                continue;
            }

            var partHeaders = Encoding.UTF8.GetString(fullBytes, pos, headerEnd - pos);
            var dataStart = headerEnd + 4;
            var dataEnd = partEnd;

            // Strip trailing \r\n before next boundary
            if (dataEnd >= dataStart + 2 && fullBytes[dataEnd - 2] == '\r' && fullBytes[dataEnd - 1] == '\n')
            {
                dataEnd -= 2;
            }

            var dataLen = dataEnd - dataStart;
            if (dataLen > 0)
            {
                var (fileName, partContentType) = ParsePartHeaders(partHeaders);
                if (!string.IsNullOrEmpty(fileName))
                {
                    // Clean and sanitize filename to prevent directory traversal
                    var safeName = Path.GetFileName(fileName).Trim();
                    if (string.IsNullOrWhiteSpace(safeName))
                    {
                        safeName = "unnamed_file_" + DateTime.UtcNow.Ticks;
                    }

                    var destPath = GetUniqueFilePath(IncomingFolder, safeName);
                    using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.Write(fullBytes, dataStart, dataLen);
                    }

                    result.Add(new IncomingFile(safeName, partContentType, destPath, dataLen));
                }
            }

            pos = partEnd;
        }

        return result;
    }

    private static (string? FileName, string ContentType) ParsePartHeaders(string headersText)
    {
        string? fileName = null;
        string contentType = "application/octet-stream";

        var lines = headersText.Split("\r\n");
        foreach (var line in lines)
        {
            if (line.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase))
            {
                fileName = ExtractFileNameFromDisposition(line);
            }
            else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
            {
                contentType = line[13..].Trim();
            }
        }

        return (fileName, contentType);
    }

    private static string? ExtractFileNameFromDisposition(string dispositionLine)
    {
        // Check filename*=utf-8''... (RFC 5987)
        var fnStar = dispositionLine.IndexOf("filename*=", StringComparison.OrdinalIgnoreCase);
        if (fnStar >= 0)
        {
            var val = dispositionLine[(fnStar + 10)..].Split(';')[0].Trim();
            var tick = val.LastIndexOf('\'');
            if (tick >= 0 && tick < val.Length - 1)
            {
                try
                {
                    return Uri.UnescapeDataString(val[(tick + 1)..]);
                }
                catch
                {
                }
            }
        }

        // Check filename="xyz" or filename=xyz
        var fnIdx = dispositionLine.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
        if (fnIdx >= 0)
        {
            var raw = dispositionLine[(fnIdx + 9)..].Split(';')[0].Trim();
            if (raw.StartsWith('"') && raw.Length > 1)
            {
                var end = raw.IndexOf('"', 1);
                if (end > 0) return raw[1..end];
            }
            return raw.Trim('"', ' ');
        }

        return null;
    }

    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var basePath = Path.Combine(folder, fileName);
        if (!File.Exists(basePath)) return basePath;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(folder, $"{nameWithoutExt} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(folder, $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}");
    }

    private static int IndexOf(byte[] source, byte[] pattern, int startIndex = 0, int count = -1)
    {
        if (count < 0) count = source.Length - startIndex;
        var end = Math.Min(source.Length, startIndex + count) - pattern.Length;

        for (int i = startIndex; i <= end; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string statusReason, string contentType, byte[] body)
    {
        var header = $"HTTP/1.1 {statusCode} {statusReason}\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     $"Connection: close\r\n\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body).ConfigureAwait(false);
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
