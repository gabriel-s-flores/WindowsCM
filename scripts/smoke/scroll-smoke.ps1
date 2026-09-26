# SPDX-License-Identifier: GPL-3.0-or-later
#
# Scroll stress for the large popup on a real Windows desktop.
#
# Fills the history to its limit with the kinds a real user collects (long
# prompts, code, screenshots, links with previews, files, emoji, colors),
# opens the popup and spins the mouse wheel over the cards the way a user
# flicks it, back and forth across the whole list. It fails when the popup
# stops answering window messages (a hung UI thread), when the process
# dies, or when the app logs an error.
#
# One history item is a file on a share that does not answer (like a file
# copied from a network drive, WSL or a USB stick that is gone now), deep
# enough that only scrolling reaches it: cards used to probe the disk on the
# UI thread, and that card froze the popup for ~20 s.
#
# On a hang it saves the managed stacks of every thread (dotnet-stack); on a
# crash the runtime writes a dump that is summarised with dotnet-dump. With
# -Trace each phase is CPU-profiled (dotnet-trace, speedscope output).
#
# Windows PowerShell 5.1 (STA, needed for the clipboard). It DELETES the
# WindowsCM data and settings of the current user, so it refuses to run
# without -ResetUserData (CI passes it on a throwaway runner).

param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutDir = "scroll-smoke-results",
    [int]$Copies = 130,
    [int]$ScrollSeconds = 30,
    # Display mode to switch to first (e.g. 1920x1080); empty = leave as is.
    [string]$Resolution = "",
    [switch]$Trace,
    [switch]$ResetUserData
)

$ErrorActionPreference = 'Stop'
if (-not $ResetUserData) { throw "Refusing to run: this smoke deletes WindowsCM user data. Pass -ResetUserData." }
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ScrollSmoke
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process, int flags);
    public static string GuiObjects(Process p) { return "GDI " + GetGuiResources(p.Handle, 0) + ", USER " + GetGuiResources(p.Handle, 1); }
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr w, IntPtr l, uint flags, uint timeoutMs, out IntPtr result);

    public static int ProcessOf(IntPtr hwnd)
    {
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        return (int)pid;
    }

    // The largest visible top-level window of a process: the open popup.
    public static IntPtr LargestVisibleWindow(int pid)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (ProcessOf(h) == pid && IsWindowVisible(h))
            {
                RECT r;
                GetWindowRect(h, out r);
                long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
                if (area > bestArea) { bestArea = area; best = h; }
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    // One wheel notch per call; negative = toward the user (scrolls right).
    public static void Wheel(int notches)
    {
        mouse_event(0x0800, 0, 0, notches * 120, UIntPtr.Zero);            // MOUSEEVENTF_WHEEL
    }

    // Precision touchpads send many small deltas instead of 120-unit notches.
    public static void WheelDelta(int delta)
    {
        mouse_event(0x0800, 0, 0, delta, UIntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion; public short dmDriverVersion; public short dmSize; public short dmDriverExtra;
        public int dmFields; public int dmPositionX; public int dmPositionY; public int dmDisplayOrientation; public int dmDisplayFixedOutput;
        public short dmColor; public short dmDuplex; public short dmYResolution; public short dmTTOption; public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels; public int dmBitsPerPel; public int dmPelsWidth; public int dmPelsHeight;
        public int dmDisplayFlags; public int dmDisplayFrequency;
        public int dmICMMethod; public int dmICMIntent; public int dmMediaType; public int dmDitherType;
        public int dmReserved1; public int dmReserved2; public int dmPanningWidth; public int dmPanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);

    // The largest mode up to width x height: a full-HD popup is ~7 cards
    // wide, the runner's default 1024 px only ~4.
    public static string SetResolution(int width, int height)
    {
        var best = new DEVMODE(); best.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        var dm = new DEVMODE(); dm.dmSize = best.dmSize;
        bool found = false;
        for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
        {
            if (dm.dmPelsWidth <= width && dm.dmPelsHeight <= height
                && (!found || (long)dm.dmPelsWidth * dm.dmPelsHeight > (long)best.dmPelsWidth * best.dmPelsHeight))
            {
                best = dm; found = true;
            }
            dm = new DEVMODE(); dm.dmSize = best.dmSize;
        }
        if (!found) return "no display modes";
        best.dmFields = 0x00080000 | 0x00100000;                          // DM_PELSWIDTH | DM_PELSHEIGHT
        int result = ChangeDisplaySettings(ref best, 0);
        return best.dmPelsWidth + "x" + best.dmPelsHeight + " (ChangeDisplaySettings " + result + ")";
    }

    // Round trip of WM_NULL through the window's thread: how long the UI
    // thread takes to get back to its message loop. -1 = no answer in time.
    public static int ProbeMs(IntPtr hwnd, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        IntPtr result;
        var ok = SendMessageTimeout(hwnd, 0x0000, IntPtr.Zero, IntPtr.Zero, 0x0000, (uint)timeoutMs, out result);
        return ok == IntPtr.Zero ? -1 : (int)watch.ElapsedMilliseconds;
    }
}
"@

$failures = New-Object System.Collections.Generic.List[string]
$metrics = [ordered]@{}
$report = New-Object System.Collections.Generic.List[string]

function Note([string]$text) { Write-Host $text; $report.Add($text) }
function Check([bool]$ok, [string]$what) {
    if ($ok) { Note "  PASS  $what" } else { Note "  FAIL  $what"; $failures.Add($what) }
}

$dataDir = Join-Path $env:LOCALAPPDATA 'WindowsCM'
$configDir = Join-Path $env:APPDATA 'WindowsCM'
$settingsPath = Join-Path $configDir 'settings.json'
$dbPath = Join-Path $dataDir 'clipboard.db'
$logPath = Join-Path $dataDir 'logs\windowscm.log'
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$pipeName = "WindowsCM.$sid"
$dotnetTools = Join-Path $env:USERPROFILE '.dotnet\tools'

function Pipe([string]$command, [int]$timeoutMs = 5000) {
    $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $client.Connect($timeoutMs)
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        $writer = New-Object System.IO.StreamWriter($client, $utf8)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($client, $utf8)
        $writer.WriteLine($command)
        return $reader.ReadLine()
    } finally { $client.Dispose() }
}

$script:app = $null
function Start-App {
    # A crash leaves a dump the runtime writes itself (createdump).
    $env:DOTNET_DbgEnableMiniDump = '1'
    $env:DOTNET_DbgMiniDumpType = '2'
    $env:DOTNET_DbgMiniDumpName = (Join-Path $OutDir 'crash-%p.dmp')
    $script:app = Start-Process -FilePath $Exe -ArgumentList '--hidden' -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        try { if ((Pipe 'ping' 1000) -eq 'ok') { Start-Sleep -Milliseconds 500; return } } catch { }
        if ($script:app.HasExited) { throw "WindowsCM exited during startup (code $($script:app.ExitCode))" }
        Start-Sleep -Milliseconds 250
    }
    throw 'WindowsCM did not answer the pipe within 30 s'
}

function Stop-App {
    if ($script:app -and -not $script:app.HasExited) { Stop-Process -Id $script:app.Id -Force; $script:app.WaitForExit(10000) | Out-Null }
    Get-Process WindowsCM -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Alive { $script:app.Refresh(); return -not $script:app.HasExited }

function Set-Clip($data) {
    [System.Windows.Forms.Clipboard]::SetDataObject($data, $true, 20, 50)
}

function Item-Counts {
    $py = "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print(';'.join(t+'='+str(n) for t,n in c.execute('select type, count(*) from clipboard group by type')))"
    $pairs = @{}
    foreach ($pair in ((& python -c $py $dbPath) -split ';')) { if ($pair) { $kv = $pair -split '='; $pairs[$kv[0]] = [int]$kv[1] } }
    return $pairs
}

function Save-Stacks([string]$label) {
    $stackExe = Join-Path $dotnetTools 'dotnet-stack.exe'
    if (-not (Test-Path $stackExe)) { Note "    (dotnet-stack unavailable: no stacks for $label)"; return }
    $file = Join-Path $OutDir "stacks-$label.txt"
    & $stackExe report -p $script:app.Id 2>&1 | Set-Content -Path $file -Encoding UTF8
    Note "    managed stacks saved to $(Split-Path $file -Leaf)"
    # The UI thread is the one running the dispatcher.
    $text = Get-Content $file -Raw
    $ui = ($text -split "(?m)^(?=\s*Thread \()") | Where-Object { $_ -match 'Dispatcher|PresentationFramework|PresentationCore' } | Select-Object -First 1
    if ($ui) {
        Note '```'
        Note (($ui -split "`n" | Select-Object -First 60) -join "`n")
        Note '```'
    }
}

function Summarise-Dumps {
    $dumpExe = Join-Path $dotnetTools 'dotnet-dump.exe'
    foreach ($dump in (Get-ChildItem $OutDir -Filter 'crash-*.dmp' -ErrorAction SilentlyContinue)) {
        Note "  crash dump: $($dump.Name) ($([math]::Round($dump.Length / 1MB, 1)) MB)"
        if (-not (Test-Path $dumpExe)) { continue }
        $file = Join-Path $OutDir "$($dump.BaseName)-analysis.txt"
        & $dumpExe analyze $dump.FullName -c 'pe -nested' -c 'clrstack -all' -c 'exit' 2>&1 | Set-Content -Path $file -Encoding UTF8
        Note '```'
        Note ((Get-Content $file | Select-Object -First 80) -join "`n")
        Note '```'
    }
}

function Save-Screenshot([string]$label) {
    try {
        $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $shot = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
        $sg = [System.Drawing.Graphics]::FromImage($shot)
        $sg.CopyFromScreen($screen.Left, $screen.Top, 0, 0, $shot.Size)
        $sg.Dispose()
        $shot.Save((Join-Path $OutDir "screen-$label.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $shot.Dispose()
    } catch { Note "    (screenshot $label failed: $($_.Exception.Message))" }
}

# CPU sampling of the app while a phase runs (managed stacks per thread).
function Start-Trace([string]$label, [int]$seconds) {
    $traceExe = Join-Path $dotnetTools 'dotnet-trace.exe'
    if (-not $Trace -or -not (Test-Path $traceExe)) { return $null }
    $file = Join-Path $OutDir "trace-$label.nettrace"
    return Start-Process -FilePath $traceExe -PassThru -NoNewWindow `
        -RedirectStandardOutput (Join-Path $OutDir "trace-$label.out.txt") -RedirectStandardError (Join-Path $OutDir "trace-$label.err.txt") `
        -ArgumentList @('collect', '-p', $script:app.Id, '--duration', ('00:00:00:{0:D2}' -f $seconds),
        '--format', 'speedscope', '-o', $file)
}

# ------------------------------------------------------------- payloads

$random = New-Object System.Random 11
$words = 'the implementation issue branch worktree review merge build test stability popup scroll card history clipboard window thread layout render preview image link file code text'.Split(' ')
function Words([int]$chars) {
    $sb = New-Object System.Text.StringBuilder
    while ($sb.Length -lt $chars) {
        [void]$sb.Append($words[$random.Next($words.Length)])
        [void]$sb.Append($(if ($random.Next(12) -eq 0) { "`n" } else { ' ' }))
    }
    return $sb.ToString()
}

$assets = Join-Path $env:TEMP 'wcm-scroll-assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$tempFiles = @()
foreach ($ext in 'txt', 'pdf', 'docx', 'zip', 'mp3', 'mp4') {
    $path = Join-Path $assets "sample.$ext"
    Set-Content $path "sample $ext"
    $tempFiles += $path
}
$photo = Join-Path $assets 'photo.png'
$bmp = New-Object System.Drawing.Bitmap 2400, 1350
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::SteelBlue)
$g.DrawString('photo', (New-Object System.Drawing.Font 'Arial', 96), [System.Drawing.Brushes]::White, 60, 60)
$g.Dispose()
$bmp.Save($photo, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$tempFiles += $photo

# Real sites: favicons and link previews (og:image) are fetched for these.
$links = @(
    'https://github.com/dotnet/wpf/issues/{0}',
    'https://learn.microsoft.com/en-us/dotnet/desktop/wpf/?v={0}',
    'https://en.wikipedia.org/wiki/Clipboard_(computing)?v={0}',
    'https://www.youtube.com/watch?v=dQw4w9WgXcQ&t={0}',
    'https://stackoverflow.com/questions/{0}'
)

# A file copied from a share (or WSL, or a drive) that is gone now, and a
# text starting with such a path: deep in the history, reached only by
# scrolling. 10.255.255.1 is unroutable, so SMB waits for its timeout.
$UnreachableShare = '\\10.255.255.1\share'
$UnreachableItems = @{ 60 = 'file'; 61 = 'text' }

function New-Payload([int]$i) {
    $data = New-Object System.Windows.Forms.DataObject
    if ($UnreachableItems[$i] -eq 'file') {
        $list = New-Object System.Collections.Specialized.StringCollection
        $list.Add("$UnreachableShare\report.pdf") | Out-Null
        $data.SetFileDropList($list)
        return ,$data
    }
    if ($UnreachableItems[$i] -eq 'text') {
        $data.SetText("$UnreachableShare\notes`nsee the notes on the share")
        return ,$data
    }
    switch ($i % 10) {
        0 { $data.SetText((Words ($random.Next(300, 7000)))) }
        1 { $data.SetText("public static int Compute$i(int x)`n{`n    var total = 0;`n    for (var k = 0; k < x; k++) { total += k * $i; }`n    return total;`n}") }
        2 { $data.SetText(($links[$i % $links.Count] -f (1000 + $i))) }
        3 {
            $shot = New-Object System.Drawing.Bitmap 1920, 1080
            $sg = [System.Drawing.Graphics]::FromImage($shot)
            $sg.Clear([System.Drawing.Color]::FromArgb($random.Next(256), $random.Next(256), $random.Next(256)))
            $sg.DrawString("screenshot $i", (New-Object System.Drawing.Font 'Arial', 48), [System.Drawing.Brushes]::White, 40, 40)
            $sg.Dispose()
            $data.SetImage($shot)
        }
        4 {
            $list = New-Object System.Collections.Specialized.StringCollection
            $list.Add($tempFiles[$i % $tempFiles.Count]) | Out-Null
            if ($i % 3 -eq 0) { $list.AddRange([string[]]$tempFiles) }
            $data.SetFileDropList($list)
        }
        5 { $data.SetText([char]::ConvertFromUtf32(0x1F600 + ($i % 50)) + $(if ($i % 4 -eq 0) { [char]::ConvertFromUtf32(0x1F680) } else { '' })) }
        6 { $data.SetText("#{0:X6}" -f $random.Next(0x1000000)) }
        7 { $data.SetText("[2026-09-21 18:34:$('{0:D2}' -f ($i % 60))] run $i`n" + (Words ($random.Next(200, 5000)))) }
        8 { $data.SetText((Words ($random.Next(40, 400)))) }
        default { $data.SetText("{`"id`": $i, `"items`": [" + ((1..($random.Next(50, 600))) -join ',') + ']}') }
    }
    return ,$data
}

if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'python is required to read the history database' }

try {

Stop-App
Remove-Item -Recurse -Force $dataDir, $configDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
(@{ history = @{ maxItems = 100 }; onboarding = @{ welcomeShown = $true } } | ConvertTo-Json -Depth 4) |
    Set-Content -Path $settingsPath -Encoding UTF8

Note "# WindowsCM scroll smoke"
Note ""
if ($Resolution) {
    $parts = $Resolution -split 'x'
    Note "Display mode: $([ScrollSmoke]::SetResolution([int]$parts[0], [int]$parts[1]))"
    Start-Sleep -Seconds 2
}
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
Note "Screen: $($work.Width)x$($work.Height) working area."

Note ""
Note "## 1. Fill the history ($Copies copies)"
Start-App
for ($i = 1; $i -le $Copies; $i++) {
    try { Set-Clip (New-Payload $i) } catch { Note "    clipboard busy at copy $i" }
    Start-Sleep -Milliseconds $(if ($i % 10 -eq 3) { 400 } else { 120 })
}
Start-Sleep -Seconds 2
Check (Alive) "app alive after filling the history"
$byType = Item-Counts
Note "  by type: $(($byType.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name) $($_.Value)" }) -join ', ')"
$metrics.itemsByType = $byType

$reply = Pipe 'show'
Start-Sleep -Milliseconds 800
$popup = [ScrollSmoke]::LargestVisibleWindow($script:app.Id)
Check (($reply -eq 'ok') -and ($popup -ne [IntPtr]::Zero)) "popup open (pipe answered '$reply')"
$rect = New-Object ScrollSmoke+RECT
[ScrollSmoke]::GetWindowRect($popup, [ref]$rect) | Out-Null
# Over the cards: below the top bar, clear of the scrollbar.
$x = [int](($rect.Left + $rect.Right) / 2)
$y = [int]($rect.Top + ($rect.Bottom - $rect.Top) * 0.55)
[ScrollSmoke]::SetCursorPos($x, $y) | Out-Null
Note "  popup $($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top) at ($($rect.Left),$($rect.Top)); wheel at ($x,$y)"
Save-Screenshot 'open'

# One scroll gesture repeated for $seconds while the UI thread is probed.
# $step sends a few wheel events and returns; $state carries its counters.
function Scroll-Phase([string]$name, [int]$seconds, [scriptblock]$step) {
    Note ""
    Note "## $name ($seconds s)"
    if (-not (Alive)) { Note "  (skipped: the app is gone)"; return }
    $trace = Start-Trace ($name -replace '\W', '') ([math]::Min($seconds, 20))
    $cpuBefore = (Get-Process -Id $script:app.Id).TotalProcessorTime
    $latencies = New-Object System.Collections.Generic.List[int]
    $state = @{ events = 0; direction = -1 }
    $hungAt = $null
    $reshows = 0
    $shot = $false
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $seconds) {
        if (-not (Alive)) { break }
        & $step $state
        $ms = [ScrollSmoke]::ProbeMs($popup, 3000)
        if ($ms -lt 0) { $hungAt = $watch.Elapsed.TotalSeconds; break }
        $latencies.Add($ms)
        if (-not $shot -and $watch.Elapsed.TotalSeconds -gt $seconds / 2) { Save-Screenshot ($name -replace '\W', ''); $shot = $true }
        if (-not [ScrollSmoke]::IsWindowVisible($popup)) {
            # Lost to a focus change: reopen and keep going.
            $reshows++
            Pipe 'show' | Out-Null
            Start-Sleep -Milliseconds 500
            [ScrollSmoke]::SetCursorPos($x, $y) | Out-Null
        }
    }
    $watch.Stop()

    if ($hungAt -ne $null) {
        Note ("  the popup stopped answering after {0:N1} s and {1} wheel events" -f $hungAt, $state.events)
        Save-Screenshot 'hang'
        Save-Stacks 'hang'
        $recovered = $false
        $hangWatch = [System.Diagnostics.Stopwatch]::StartNew()
        while ($hangWatch.Elapsed.TotalSeconds -lt 30 -and (Alive)) {
            if ([ScrollSmoke]::ProbeMs($popup, 2000) -ge 0) { $recovered = $true; break }
        }
        if (Alive) {
            $sample = Get-Process -Id $script:app.Id
            Note ("  after the hang: CPU {0:N1} s, private {1:N0} MB, working set {2:N0} MB, {3} handles" -f `
                ($sample.TotalProcessorTime - $cpuBefore).TotalSeconds, ($sample.PrivateMemorySize64 / 1MB), ($sample.WorkingSet64 / 1MB), $sample.HandleCount)
        }
        if ($recovered) { Note ("  recovered after {0:N1} s more" -f $hangWatch.Elapsed.TotalSeconds) }
        elseif (Alive) { Save-Stacks 'hang-30s' }
    }
    $alive = Alive
    Check ($hungAt -eq $null) "${name}: the popup keeps answering ($($state.events) wheel events in $([math]::Round($watch.Elapsed.TotalSeconds,1)) s)"
    Check $alive "${name}: app still running"
    $phase = [ordered]@{ events = $state.events; reshows = $reshows }
    if ($alive) {
        $p = Get-Process -Id $script:app.Id
        $cpu = ($p.TotalProcessorTime - $cpuBefore).TotalSeconds
        Note ("  CPU {0:N1} s over {1:N1} s ({2:N1} cores); private {3:N0} MB, working set {4:N0} MB, {5} handles, {6}" -f `
            $cpu, $watch.Elapsed.TotalSeconds, ($cpu / [math]::Max(0.1, $watch.Elapsed.TotalSeconds)), ($p.PrivateMemorySize64 / 1MB), ($p.WorkingSet64 / 1MB), $p.HandleCount, [ScrollSmoke]::GuiObjects($p))
        $phase.cpuCores = [math]::Round($cpu / [math]::Max(0.1, $watch.Elapsed.TotalSeconds), 2)
        $phase.privateMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
    } else {
        Note "  exit code: $($script:app.ExitCode)"
        Summarise-Dumps
    }
    if ($latencies.Count -gt 0) {
        $sorted = $latencies | Sort-Object
        $p50 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.5)]
        $p95 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.95)]
        $max = $sorted[$sorted.Count - 1]
        Note "  UI thread round trip: p50 $p50 ms, p95 $p95 ms, max $max ms ($($sorted.Count) probes)"
        $phase.probeMs = @{ p50 = $p50; p95 = $p95; max = $max; samples = $sorted.Count }
        Check ($max -lt 1000) "${name}: no stall of a second or more (max $max ms)"
    }
    if ($reshows -gt 0) { Note "  (the popup closed $reshows time(s) and was reopened)" }
    if ($trace) { $trace.WaitForExit(60000) | Out-Null }
    $metrics[$name] = $phase
}

# A flicked mouse wheel: ~60 notches a second, reversing every 110 notches
# (one notch scrolls one card: the whole list and back).
Scroll-Phase 'wheel flick' $ScrollSeconds {
    param($state)
    for ($k = 0; $k -lt 6; $k++) {
        [ScrollSmoke]::Wheel($state.direction)
        $state.events++
        if ($state.events % 110 -eq 0) { $state.direction = -$state.direction }
        Start-Sleep -Milliseconds 16
    }
}

# A precision touchpad: a stream of small deltas at ~120 Hz.
Scroll-Phase 'touchpad' $ScrollSeconds {
    param($state)
    for ($k = 0; $k -lt 12; $k++) {
        [ScrollSmoke]::WheelDelta($state.direction * (10 + ($state.events % 30)))
        $state.events++
        if ($state.events % 1400 -eq 0) { $state.direction = -$state.direction }
        Start-Sleep -Milliseconds 8
    }
}

# Wheel bursts with no pause: 20 notches at once, as fast as input goes.
Scroll-Phase 'wheel burst' $ScrollSeconds {
    param($state)
    for ($k = 0; $k -lt 20; $k++) {
        [ScrollSmoke]::Wheel($state.direction)
        $state.events++
        if ($state.events % 100 -eq 0) { $state.direction = -$state.direction }
    }
    Start-Sleep -Milliseconds 40
}

Note ""
Note "## Error log"
if (Test-Path $logPath) {
    Copy-Item $logPath (Join-Path $OutDir 'windowscm.log')
    Note '```'
    Note ((Get-Content $logPath | Select-Object -First 120) -join "`n")
    Note '```'
    Check $false "no errors logged by the app"
} else {
    Check $true "no errors logged by the app (log file never created)"
}

} catch {
    Note ""
    Note "ABORTED: $($_.Exception.Message)"
    Note "$($_.ScriptStackTrace)"
    $failures.Add("smoke aborted: $($_.Exception.Message)")
} finally {
    Stop-App
}

Note ""
if ($failures.Count -eq 0) { Note "RESULT: PASS" } else { Note "RESULT: FAIL ($($failures.Count))"; $failures | ForEach-Object { Note "  - $_" } }
$report | Set-Content (Join-Path $OutDir 'report.md') -Encoding UTF8
($metrics | ConvertTo-Json -Depth 6) | Set-Content (Join-Path $OutDir 'metrics.json') -Encoding UTF8
Remove-Item $assets -Recurse -ErrorAction SilentlyContinue
if ($failures.Count -gt 0) { exit 1 }
exit 0
