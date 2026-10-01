// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace WindowsCM.Core.Release;

public sealed record AvailableUpdate(Version Version, string AssetName, Uri DownloadUrl, string Sha256);

public sealed class GitHubUpdateService(HttpClient client)
{
    public const string Repository = "gabriel-s-flores/WindowsCM";

    public async Task<AvailableUpdate?> CheckAsync(Version current, bool installed, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("WindowsCM/" + current.ToString());
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString();
        if (!Version.TryParse(tag?.TrimStart('v'), out var version) || version.Build < 0 || version <= current) return null;
        var name = installed ? $"WindowsCM-Setup-{version}.exe" : $"WindowsCM-portable-{version}.zip";
        Uri? assetUrl = null;
        Uri? sumsUrl = null;
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var assetName = asset.GetProperty("name").GetString();
            if (assetName != name && assetName != "SHA256SUMS.txt") continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (!IsReleaseAsset(url, tag!)) throw new InvalidDataException("Unexpected release asset URL.");
            if (assetName == name) assetUrl = url; else sumsUrl = url;
        }
        if (assetUrl is null || sumsUrl is null) return null; // Release upload may still be in progress.
        var sums = await client.GetStringAsync(sumsUrl, cancellationToken).ConfigureAwait(false);
        var hash = ParseChecksum(sums, name);
        return hash is null ? null : new AvailableUpdate(version, name, assetUrl, hash);
    }

    public static bool IsReleaseAsset(Uri url, string tag) =>
        url.Scheme == "https" && url.Host == "github.com" && url.IsDefaultPort &&
        url.AbsolutePath.StartsWith($"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/", StringComparison.Ordinal);

    public static string? ParseChecksum(string sums, string name)
    {
        foreach (var line in sums.Split('\n'))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1] == name && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit))
                return parts[0];
        }
        return null;
    }

    public async Task DownloadAsync(AvailableUpdate update, string destination, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(destination))
                await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await using var input = File.OpenRead(destination);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
            if (!hash.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }
}
