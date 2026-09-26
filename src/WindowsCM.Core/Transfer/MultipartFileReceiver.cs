// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace WindowsCM.Core.Transfer;

// Streams a multipart/form-data upload to disk, one file per part that has
// a file name. The whole body used to be buffered in memory and copied
// again (a 1.5 GB video from the phone peaked near 3.5 GB in the tray
// process, and any body over 2 GB failed after buffering it); memory now
// stays at one buffer whatever the size.
public sealed class MultipartFileReceiver
{
    private const int BufferSize = 64 * 1024;
    private const int MaxPartHeaderBytes = 16 * 1024;
    private const int MaxFileNameLength = 200;

    private readonly string _folder;

    public MultipartFileReceiver(string folder) => _folder = folder;

    // onProgress runs after every read (the caller's idle timeout). Files of
    // a request that fails half-way — the phone left the network, a
    // malformed body — are deleted: the app never announces them.
    public async Task<IReadOnlyList<IncomingFile>> ReceiveAsync(
        Stream body, string boundary, Action onProgress, CancellationToken ct)
    {
        var saved = new List<IncomingFile>();
        try
        {
            var reader = new BodyReader(body, onProgress);
            // Preamble, then the first boundary (no CRLF before it).
            await reader.CopyUntilAsync(Encoding.ASCII.GetBytes("--" + boundary), Stream.Null, ct).ConfigureAwait(false);
            var delimiter = Encoding.ASCII.GetBytes("\r\n--" + boundary);
            while (true)
            {
                // After a boundary: "--" closes the body, CRLF opens a part.
                var next = await reader.ReadExactAsync(2, ct).ConfigureAwait(false);
                if (next == "--")
                {
                    return saved;
                }
                if (next != "\r\n")
                {
                    throw new InvalidDataException("Malformed multipart boundary line.");
                }
                var (fileName, contentType) = ParsePartHeaders(await reader.ReadPartHeadersAsync(ct).ConfigureAwait(false));
                if (string.IsNullOrEmpty(fileName))
                {
                    await reader.CopyUntilAsync(delimiter, Stream.Null, ct).ConfigureAwait(false);
                    continue;
                }
                var safeName = SanitizeFileName(fileName);
                var (path, file) = CreateUnique(_folder, safeName);
                long size;
                try
                {
                    await using (file.ConfigureAwait(false))
                    {
                        size = await reader.CopyUntilAsync(delimiter, file, ct).ConfigureAwait(false);
                    }
                }
                catch
                {
                    TryDelete(path);
                    throw;
                }
                saved.Add(new IncomingFile(safeName, contentType, path, size));
            }
        }
        catch
        {
            foreach (var file in saved)
            {
                TryDelete(file.SavedPath);
            }
            throw;
        }
    }

    // Browsers send the base name, but anything goes over the wire: path
    // parts are dropped, and characters Windows refuses in a name become
    // '_' — a ':' made "Meeting 10:30.pdf" an empty "Meeting 10" with the
    // data hidden in an NTFS stream, the others failed the whole upload.
    public static string SanitizeFileName(string? raw)
    {
        var name = raw ?? "";
        var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        name = name[(slash + 1)..];
        var chars = name.Select(c => c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray();
        name = new string(chars).Trim().TrimEnd('.', ' ');
        if (name.Length > MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            if (extension.Length > 20)
            {
                extension = "";
            }
            name = name[..(MaxFileNameLength - extension.Length)].TrimEnd('.', ' ') + extension;
        }
        if (name.Length == 0 || name.Trim('.').Length == 0)
        {
            return "unnamed_file_" + DateTime.UtcNow.Ticks;
        }
        var stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
        {
            name = "_" + name;
        }
        return name;
    }

    private static (string? FileName, string ContentType) ParsePartHeaders(string headersText)
    {
        string? fileName = null;
        var contentType = "application/octet-stream";
        foreach (var line in headersText.Split("\r\n"))
        {
            if (line.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase))
            {
                fileName = FileNameFromDisposition(line);
            }
            else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
            {
                contentType = line["Content-Type:".Length..].Trim();
            }
        }
        return (fileName, contentType);
    }

    private static string? FileNameFromDisposition(string line)
    {
        // filename*=utf-8''... (RFC 5987) wins over filename=.
        var star = line.IndexOf("filename*=", StringComparison.OrdinalIgnoreCase);
        if (star >= 0)
        {
            var value = line[(star + 10)..].Split(';')[0].Trim();
            var tick = value.LastIndexOf('\'');
            if (tick >= 0 && tick < value.Length - 1)
            {
                try
                {
                    return Uri.UnescapeDataString(value[(tick + 1)..]);
                }
                catch (UriFormatException)
                {
                }
            }
        }
        var plain = line.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
        if (plain < 0)
        {
            return null;
        }
        var raw = line[(plain + 9)..].Trim();
        if (raw.StartsWith('"'))
        {
            var end = raw.IndexOf('"', 1);
            return end > 0 ? raw[1..end] : raw.Trim('"');
        }
        return raw.Split(';')[0].Trim();
    }

    // CreateNew, not Exists-then-Create: two uploads with the same name at
    // once used to overwrite each other.
    private static (string Path, FileStream File) CreateUnique(string folder, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var i = 0; ; i++)
        {
            var candidate = Path.Combine(folder,
                i == 0 ? fileName
                : i < 1000 ? $"{stem} ({i}){extension}"
                : $"{stem}_{Guid.NewGuid():N}{extension}");
            try
            {
                return (candidate, new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true));
            }
            catch (IOException) when (File.Exists(candidate))
            {
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // A buffered view of the body that finds delimiters across reads.
    private sealed class BodyReader(Stream stream, Action onProgress)
    {
        private static readonly byte[] CrLf = "\r\n"u8.ToArray();
        private static readonly byte[] HeadersEnd = "\r\n\r\n"u8.ToArray();

        private readonly byte[] _buffer = new byte[BufferSize * 2];
        private int _start;
        private int _end;
        private bool _eof;

        private int Buffered => _end - _start;

        // Span work stays out of the async methods (no ref locals there).
        private int IndexOf(byte[] needle) => _buffer.AsSpan(_start, Buffered).IndexOf(needle);

        private bool StartsWith(byte[] prefix) => _buffer.AsSpan(_start, Buffered).StartsWith(prefix);

        private async Task<bool> FillAsync(CancellationToken ct)
        {
            if (_eof)
            {
                return false;
            }
            if (_start > 0)
            {
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, Buffered);
                _end -= _start;
                _start = 0;
            }
            var read = await stream.ReadAsync(_buffer.AsMemory(_end), ct).ConfigureAwait(false);
            onProgress();
            if (read == 0)
            {
                _eof = true;
                return false;
            }
            _end += read;
            return true;
        }

        public async Task<string> ReadExactAsync(int count, CancellationToken ct)
        {
            while (Buffered < count)
            {
                if (!await FillAsync(ct).ConfigureAwait(false))
                {
                    throw new InvalidDataException("Multipart body ended early.");
                }
            }
            var text = Encoding.ASCII.GetString(_buffer, _start, count);
            _start += count;
            return text;
        }

        public async Task<string> ReadPartHeadersAsync(CancellationToken ct)
        {
            while (true)
            {
                if (StartsWith(CrLf))
                {
                    // A part without headers.
                    _start += 2;
                    return "";
                }
                var at = IndexOf(HeadersEnd);
                if (at >= 0)
                {
                    var headers = Encoding.UTF8.GetString(_buffer, _start, at);
                    _start += at + HeadersEnd.Length;
                    return headers;
                }
                if (Buffered > MaxPartHeaderBytes)
                {
                    throw new InvalidDataException("Multipart part headers too large.");
                }
                if (!await FillAsync(ct).ConfigureAwait(false))
                {
                    throw new InvalidDataException("Multipart body ended inside part headers.");
                }
            }
        }

        // Copies up to the delimiter into target and consumes the delimiter.
        public async Task<long> CopyUntilAsync(byte[] delimiter, Stream target, CancellationToken ct)
        {
            long copied = 0;
            while (true)
            {
                var at = IndexOf(delimiter);
                if (at >= 0)
                {
                    await target.WriteAsync(_buffer.AsMemory(_start, at), ct).ConfigureAwait(false);
                    _start += at + delimiter.Length;
                    return copied + at;
                }
                // Everything but a possible start of the delimiter.
                var safe = Math.Max(0, Buffered - (delimiter.Length - 1));
                if (safe > 0)
                {
                    await target.WriteAsync(_buffer.AsMemory(_start, safe), ct).ConfigureAwait(false);
                    copied += safe;
                    _start += safe;
                }
                if (!await FillAsync(ct).ConfigureAwait(false))
                {
                    throw new InvalidDataException("Multipart body ended inside a part.");
                }
            }
        }
    }
}
