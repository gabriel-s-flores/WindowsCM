// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using WindowsCM.Core.Transfer;

namespace WindowsCM.Core.Tests.Transfer;

// Uploads from the phone are streamed to disk part by part. The whole body
// used to be buffered in memory (then copied again): a 1.5 GB video peaked
// near 3.5 GB in the tray process.
public sealed class MultipartFileReceiverTests : IDisposable
{
    private const string Boundary = "----WebKitFormBoundaryAbC123";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wcm-multipart-" + Guid.NewGuid().ToString("N"));

    public MultipartFileReceiverTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static byte[] Body(params (string? FileName, string ContentType, byte[] Data)[] parts)
    {
        using var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.UTF8.GetBytes(s));
        foreach (var (fileName, contentType, data) in parts)
        {
            W($"--{Boundary}\r\n");
            W(fileName is null
                ? "Content-Disposition: form-data; name=\"note\"\r\n"
                : $"Content-Disposition: form-data; name=\"files\"; filename=\"{fileName}\"\r\n");
            W($"Content-Type: {contentType}\r\n\r\n");
            ms.Write(data);
            W("\r\n");
        }
        W($"--{Boundary}--\r\n");
        return ms.ToArray();
    }

    // Serves the body a few bytes at a time, like a slow network.
    private sealed class TrickleStream(byte[] data, int chunk) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(Math.Min(count, chunk), data.Length - _position);
            Array.Copy(data, _position, buffer, offset, n);
            _position += n;
            return n;
        }
    }

    private Task<IReadOnlyList<IncomingFile>> Receive(Stream body) =>
        new MultipartFileReceiver(_dir).ReceiveAsync(body, Boundary, () => { }, CancellationToken.None);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    public async Task Files_AreWrittenExactly_WhateverTheChunking(int chunk)
    {
        var photo = Enumerable.Range(0, 300_000).Select(i => (byte)(i * 31)).ToArray();
        // Data that looks like a boundary but is not one.
        var tricky = Encoding.UTF8.GetBytes($"line\r\n--{Boundary[..10]}not the end\r\n-");
        var body = Body(("foto.jpg", "image/jpeg", photo), (null, "text/plain", "ignored"u8.ToArray()), ("notes.txt", "text/plain", tricky));

        var files = await Receive(new TrickleStream(body, chunk));

        Assert.Equal(["foto.jpg", "notes.txt"], files.Select(f => f.FileName));
        Assert.Equal(photo, File.ReadAllBytes(files[0].SavedPath));
        Assert.Equal(tricky, File.ReadAllBytes(files[1].SavedPath));
        Assert.Equal(photo.Length, files[0].FileSize);
        Assert.Equal("image/jpeg", files[0].ContentType);
    }

    [Fact]
    public async Task EmptyFile_IsKept()
    {
        var files = await Receive(new MemoryStream(Body(("empty.txt", "text/plain", []))));

        Assert.Single(files);
        Assert.Empty(File.ReadAllBytes(files[0].SavedPath));
    }

    // A body cut short (the phone left Wi-Fi) must not leave half files, or
    // files the app never announced, in the Downloads folder.
    [Fact]
    public async Task TruncatedBody_Throws_AndLeavesNoFiles()
    {
        var body = Body(("a.bin", "application/octet-stream", new byte[50_000]), ("b.bin", "application/octet-stream", new byte[50_000]));
        var cut = body[..(body.Length - 30_000)];

        await Assert.ThrowsAsync<InvalidDataException>(() => Receive(new MemoryStream(cut)));

        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task SameNameTwice_GetsANumberedCopy()
    {
        var body = Body(("doc.pdf", "application/pdf", "one"u8.ToArray()), ("doc.pdf", "application/pdf", "two"u8.ToArray()));

        var files = await Receive(new MemoryStream(body));

        Assert.Equal("one", File.ReadAllText(files[0].SavedPath));
        Assert.Equal("two", File.ReadAllText(files[1].SavedPath));
        Assert.EndsWith("doc (1).pdf", files[1].SavedPath);
    }

    [Fact]
    public async Task ProgressCallback_RunsWhileReading()
    {
        var calls = 0;
        var body = Body(("big.bin", "application/octet-stream", new byte[1_000_000]));

        await new MultipartFileReceiver(_dir).ReceiveAsync(new TrickleStream(body, 8192), Boundary, () => calls++, CancellationToken.None);

        Assert.True(calls > 10);
    }

    [Theory]
    [InlineData("Reunião 10:30.pdf", "Reunião 10_30.pdf")]
    [InlineData("a?b*c<d>e|f\"g.txt", "a_b_c_d_e_f_g.txt")]
    [InlineData("../../../etc/passwd.txt", "passwd.txt")]
    [InlineData(@"..\..\Windows\win.ini", "win.ini")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("lpt1", "_lpt1")]
    [InlineData("trailing dots... ", "trailing dots")]
    [InlineData("tab\there.txt", "tab_here.txt")]
    public void FileNames_AreSafeOnWindows(string raw, string expected)
    {
        Assert.Equal(expected, MultipartFileReceiver.SanitizeFileName(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("/")]
    public void EmptyNames_GetAGeneratedOne(string raw)
    {
        Assert.StartsWith("unnamed_file_", MultipartFileReceiver.SanitizeFileName(raw));
    }

    [Fact]
    public void VeryLongNames_KeepTheirExtension()
    {
        var name = MultipartFileReceiver.SanitizeFileName(new string('x', 400) + ".jpeg");

        Assert.True(name.Length <= 200);
        Assert.EndsWith(".jpeg", name);
    }
}
