// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Windows.Threading;
using WindowsCM.Core.Release;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Settings;

namespace WindowsCM.App;

internal sealed class AutoUpdater(App app) : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly CancellationTokenSource _stop = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(6) };
    private Version? _offered;
    private bool _checking;
    private UpdateWindow? _window;

    public void Start()
    {
        var failure = Path.Combine(AppFolders.DataDir(), "update-failure.log");
        if (File.Exists(failure))
        {
            System.Windows.MessageBox.Show(LocalizationManager.Strings.UpdateApplyFailed,
                LocalizationManager.Strings.UpdateTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            File.Delete(failure);
        }
        // Framework-dependent developer builds are not distribution packages.
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "WindowsCM.dll"))) return;
        _timer.Tick += Tick;
        _timer.Start();
        _ = CheckAsync();
    }

    private void Tick(object? sender, EventArgs e) => _ = CheckAsync();

    private static bool IsInstalled(string exe) => File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "unins000.exe"));

    private async Task CheckAsync()
    {
        if (_checking || _window is not null || _stop.IsCancellationRequested) return;
        _checking = true;
        try
        {
            // Assembly versions have a fourth component; release tags have three.
            var assembly = typeof(App).Assembly.GetName().Version!;
            var version = new Version(assembly.Major, assembly.Minor, assembly.Build);
            var service = new GitHubUpdateService(_client);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var update = await service.CheckAsync(version, IsInstalled(Environment.ProcessPath!), timeout.Token);
            if (update is null || update.Version == _offered || _stop.IsCancellationRequested) return;
            _offered = update.Version;
            _window = new UpdateWindow(update, () => InstallAsync(service, update));
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { app.LogError("update-check", ex); }
        finally { _checking = false; }
    }

    private async Task InstallAsync(GitHubUpdateService service, AvailableUpdate update)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WindowsCM-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var exe = Environment.ProcessPath!;
        var installed = IsInstalled(exe);
        try
        {
            var download = Path.Combine(dir, update.AssetName);
            await service.DownloadAsync(update, download, _stop.Token);
            string operation;
            if (installed)
            {
                operation = $"$p = Start-Process -WindowStyle Hidden -FilePath {Quote(download)} -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER', {Quote("/DIR=\"" + Path.GetDirectoryName(exe) + "\"")}) -Wait -PassThru\nif ($p.ExitCode -ne 0) {{ throw 'Installer failed.' }}";
            }
            else
            {
                var probe = Path.Combine(Path.GetDirectoryName(exe)!, ".WindowsCM-update-" + Guid.NewGuid().ToString("N"));
                await File.WriteAllTextAsync(probe, "", _stop.Token);
                File.Delete(probe);
                // Extract only the executable; never trust archive paths.
                using var zip = ZipFile.OpenRead(download);
                var entry = zip.GetEntry("WindowsCM.exe") ?? throw new InvalidDataException("Update executable missing.");
                var staged = Path.Combine(dir, "WindowsCM.exe");
                await Task.Run(() => entry.ExtractToFile(staged), _stop.Token);
                operation = UpdateApplyScript.PortableOperation(exe, staged, Guid.NewGuid().ToString("N"));
            }
            // A hidden helper waits for graceful shutdown before touching binaries.
            var script = Path.Combine(dir, "apply.ps1");
            var body = $"$ErrorActionPreference = 'Stop'\n$success = $false\ntry {{\nWait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue\n{operation}\n$success = $true\n}} catch {{ $_ | Out-File -LiteralPath {Quote(Path.Combine(AppFolders.DataDir(), "update-failure.log"))} }}\nStart-Process -WindowStyle Hidden -FilePath {Quote(exe)} -ArgumentList '--hidden'\nif ($success) {{ Remove-Item -LiteralPath {Quote(dir)} -Recurse -Force }}\n";
            await File.WriteAllTextAsync(script, body, Encoding.UTF8, _stop.Token);
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(arg);
            using var helper = Process.Start(start) ?? throw new InvalidOperationException("Update helper did not start.");
            app.Shutdown();
        }
        catch
        {
            Directory.Delete(dir, recursive: true);
            throw;
        }
    }

    private static string Quote(string value) => UpdateApplyScript.Quote(value);

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Tick;
        _stop.Cancel();
        _client.Dispose();
        _stop.Dispose();
    }
}
