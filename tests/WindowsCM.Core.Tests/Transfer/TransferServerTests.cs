// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WindowsCM.Core.Transfer;
using Xunit;

namespace WindowsCM.Core.Tests.Transfer;

public sealed class TransferServerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MiniTransferHttpServer _server;
    private readonly HttpClient _http;

    public TransferServerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WindowsCM_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _server = new MiniTransferHttpServer(preferredPort: 0, incomingFolder: _tempDir);
        _server.Start();
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_server.Port}") };
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void LocalNetworkResolver_ResolvesPreferredIp()
    {
        var ip = LocalNetworkResolver.GetPreferredLocalIp();
        Assert.NotNull(ip);
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, ip.AddressFamily);
    }

    [Fact]
    public void LocalNetworkResolver_GetAllLocalIps_ReturnsList()
    {
        var list = LocalNetworkResolver.GetAllLocalIps();
        Assert.NotNull(list);
    }

    [Fact]
    public void MobileWebTemplate_DownloadPage_RendersAudioPlayer()
    {
        var session = new SharedItemSession(
            Token: "testaudio",
            ItemId: 1,
            Title: "Podcast Track.mp3",
            KindLabel: "Áudio MP3",
            FilePath: @"C:\music\track.mp3",
            FilePaths: null,
            TextContent: null,
            RawBytes: null,
            FileName: "track.mp3",
            ContentType: "audio/mpeg",
            FileSize: 1048576,
            CreatedAt: DateTime.UtcNow);

        var html = MobileWebTemplate.RenderDownloadPage(session, "192.168.1.8:58921");
        Assert.Contains("<audio controls", html);
        Assert.Contains("Podcast Track.mp3", html);
        Assert.Contains("/file/testaudio", html);
    }

    [Fact]
    public void MobileWebTemplate_DownloadPage_RendersImagePreview()
    {
        var session = new SharedItemSession(
            Token: "testimg",
            ItemId: 2,
            Title: "Foto de férias",
            KindLabel: "Imagem PNG",
            FilePath: @"C:\photos\vacation.png",
            FilePaths: null,
            TextContent: null,
            RawBytes: null,
            FileName: "vacation.png",
            ContentType: "image/png",
            FileSize: 2048576,
            CreatedAt: DateTime.UtcNow);

        var html = MobileWebTemplate.RenderDownloadPage(session, "192.168.1.8:58921");
        Assert.Contains("<img src=\"/file/testimg\"", html);
        Assert.Contains("Foto de férias", html);
    }

    [Fact]
    public void MobileWebTemplate_DownloadPage_RendersTextPreAndCopy()
    {
        var session = new SharedItemSession(
            Token: "testtext",
            ItemId: 3,
            Title: "Texto copiado",
            KindLabel: "Texto",
            FilePath: null,
            FilePaths: null,
            TextContent: "Olá do WindowsCM!\nLinha 2",
            RawBytes: null,
            FileName: "item.txt",
            ContentType: "text/plain; charset=utf-8",
            FileSize: 30,
            CreatedAt: DateTime.UtcNow);

        var html = MobileWebTemplate.RenderDownloadPage(session, "192.168.1.8:58921");
        Assert.Contains("Olá do WindowsCM!", html);
        Assert.Contains("copyTextToClipboard()", html);
    }

    [Fact]
    public async Task Server_GetRoot_ReturnsUploadPage()
    {
        var resp = await _http.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("WindowsCM Transfer", content);
        Assert.Contains("Enviar para o PC", content);
    }

    [Fact]
    public async Task Server_GetPing_ReturnsOnline()
    {
        var resp = await _http.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("online", content);
    }

    [Fact]
    public async Task Server_ShareFile_DownloadPageAndStreamWork()
    {
        var sampleFile = Path.Combine(_tempDir, "sample_doc.pdf");
        var sampleBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Test PDF Content In WindowsCM");
        await File.WriteAllBytesAsync(sampleFile, sampleBytes);

        var session = _server.RegisterShare(
            title: "Relatorio.pdf",
            kindLabel: "Documento PDF",
            filePath: sampleFile);

        // 1. Download UI page
        var pageResp = await _http.GetAsync($"/d/{session.Token}");
        Assert.Equal(HttpStatusCode.OK, pageResp.StatusCode);
        var pageHtml = await pageResp.Content.ReadAsStringAsync();
        Assert.Contains("Relatorio.pdf", pageHtml);

        // 2. Binary file download
        var fileResp = await _http.GetAsync($"/file/{session.Token}?download=1");
        Assert.Equal(HttpStatusCode.OK, fileResp.StatusCode);
        Assert.Equal("application/pdf", fileResp.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await fileResp.Content.ReadAsByteArrayAsync();
        Assert.Equal(sampleBytes, downloadedBytes);

        // 3. Unregistered returns 404
        _server.UnregisterShare(session.Token);
        var notFoundResp = await _http.GetAsync($"/d/{session.Token}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResp.StatusCode);
    }

    [Fact]
    public async Task Server_PostUpload_TextReceived_FiresEvent()
    {
        IncomingTransferPayload? received = null;
        _server.PayloadReceived += p => received = p;

        var json = "{\"text\":\"Texto enviado do celular 123!\"}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync("/api/upload", content);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.NotNull(received);
        Assert.Equal("Texto enviado do celular 123!", received!.Text);
        Assert.Empty(received.Files);
    }

    [Fact]
    public async Task Server_PostUpload_MultipartFiles_SavedAndFiresEvent()
    {
        IncomingTransferPayload? received = null;
        _server.PayloadReceived += p => received = p;

        using var multiContent = new MultipartFormDataContent("----WebKitFormBoundaryXYZ123");
        var fileBytes = Encoding.UTF8.GetBytes("Audio or image test payload from mobile device");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multiContent.Add(fileContent, "files", "minha_foto.jpg");

        var resp = await _http.PostAsync("/api/upload", multiContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.NotNull(received);
        Assert.Single(received!.Files);
        var incoming = received.Files[0];
        Assert.Equal("minha_foto.jpg", incoming.FileName);
        Assert.True(File.Exists(incoming.SavedPath));
        var savedContent = await File.ReadAllBytesAsync(incoming.SavedPath);
        Assert.Equal(fileBytes, savedContent);
    }

    [Fact]
    public async Task Server_PostUpload_PathTraversal_SanitizesFileName()
    {
        IncomingTransferPayload? received = null;
        _server.PayloadReceived += p => received = p;

        using var multiContent = new MultipartFormDataContent("----WebKitFormBoundaryXYZ123");
        var fileBytes = Encoding.UTF8.GetBytes("Malicious payload content");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        // Malicious relative path attempt
        multiContent.Add(fileContent, "files", "../../../etc/passwd.txt");

        var resp = await _http.PostAsync("/api/upload", multiContent);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.NotNull(received);
        Assert.Single(received!.Files);
        var incoming = received.Files[0];
        // Path.GetFileName strips leading directory traversal sequences
        Assert.Equal("passwd.txt", incoming.FileName);
        Assert.StartsWith(_tempDir, incoming.SavedPath);
        Assert.True(File.Exists(incoming.SavedPath));
    }

    [Fact]
    public async Task Server_PostUpload_FileCollision_SavesWithUniqueNumber()
    {
        using var multi1 = new MultipartFormDataContent("----WebKitFormBoundaryXYZ123");
        multi1.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("First")), "files", "document.pdf");
        await _http.PostAsync("/api/upload", multi1);

        IncomingTransferPayload? received2 = null;
        _server.PayloadReceived += p => received2 = p;

        using var multi2 = new MultipartFormDataContent("----WebKitFormBoundaryXYZ123");
        multi2.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Second")), "files", "document.pdf");
        await _http.PostAsync("/api/upload", multi2);

        Assert.NotNull(received2);
        Assert.Single(received2!.Files);
        var file2 = received2.Files[0];
        Assert.Contains("document (1).pdf", file2.SavedPath);
        Assert.True(File.Exists(file2.SavedPath));
    }
}
