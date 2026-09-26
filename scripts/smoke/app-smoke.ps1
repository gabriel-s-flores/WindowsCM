# SPDX-License-Identifier: GPL-3.0-or-later
#
# Real-app stability and performance smoke for WindowsCM on Windows.
#
# Drives the built WindowsCM.exe the way a user does — global hotkey,
# clipboard, Enter — plus the pipe for popup timing, and fails on any crash,
# logged error, unbounded history, memory growth or wrong behavior:
#
#   1. Stress: hundreds of copies (text, code, links, multi-MB text, images,
#      file lists, colors) with bursts, popup open/close cycles measured
#      through the pipe, process memory sampled along the way.
#   2. Placement: docked, fixed monitor (disconnected -> primary) and free
#      (saved rectangle restored exactly, off-screen rectangle pulled back,
#      edge resize hit-testing, bounds saved after a move).
#   3. Auto-paste end to end in Notepad: paste into the focused field, via
#      the taskbar fallback, copy-only from the desktop, copy-only when the
#      option is off.
#
# Windows PowerShell 5.1 (STA, needed for the clipboard). It DELETES the
# WindowsCM data and settings of the current user, so it refuses to run
# without -ResetUserData (CI passes it on a throwaway runner).

param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutDir = "smoke-results",
    [int]$Copies = 300,
    # Diagnostics: GC conserve level for the app process (0 = default) and
    # stopping after the stress phase.
    [int]$GcConserveMemory = 0,
    [switch]$StressOnly,
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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Smoke
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, StringBuilder l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, string l);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach, uint to, bool fAttach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    public static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

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

    public static IntPtr FindTextControl(IntPtr top)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(top, delegate (IntPtr h, IntPtr l)
        {
            var cls = ClassOf(h);
            if (cls == "Edit" || cls.StartsWith("RichEdit")) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static string GetText(IntPtr hwnd)
    {
        int length = SendMessage(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero).ToInt32(); // WM_GETTEXTLENGTH
        var sb = new StringBuilder(length + 1);
        SendMessage(hwnd, 0x000D, new IntPtr(sb.Capacity), sb);                    // WM_GETTEXT
        return sb.ToString();
    }

    public static void SetText(IntPtr hwnd, string text)
    {
        SendMessage(hwnd, 0x000C, IntPtr.Zero, text);                                // WM_SETTEXT
    }

    // Foreground changes from a background process are restricted: try the
    // plain call, then sharing the current foreground thread's input state,
    // then an ALT tap (synthesized input lifts the foreground lock). The tap
    // goes out before the switch so it never leaves the target in menu mode.
    public static bool Focus(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) { ShowWindow(hwnd, 9); }                        // SW_RESTORE
        for (int attempt = 0; attempt < 5 && GetForegroundWindow() != hwnd; attempt++)
        {
            SetForegroundWindow(hwnd);
            if (GetForegroundWindow() == hwnd) break;
            uint pid;
            uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            uint me = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(me, fgThread, false);
            if (GetForegroundWindow() == hwnd) break;
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            SetForegroundWindow(hwnd);
            System.Threading.Thread.Sleep(150);
        }
        return GetForegroundWindow() == hwnd;
    }

    public static void Chord(params byte[] keys)
    {
        foreach (var k in keys) keybd_event(k, 0, 0, UIntPtr.Zero);
        for (int i = keys.Length - 1; i >= 0; i--) keybd_event(keys[i], 0, 2, UIntPtr.Zero);
    }

    public static int HitTest(IntPtr hwnd, int x, int y)
    {
        uint packed = ((uint)(y & 0xFFFF) << 16) | (uint)(x & 0xFFFF);           // MAKELPARAM(x, y)
        return SendMessage(hwnd, 0x0084, IntPtr.Zero, new IntPtr((long)packed)).ToInt32(); // WM_NCHITTEST
    }

    [DllImport("user32.dll")] static extern IntPtr GetOpenClipboardWindow();

    // Who holds the clipboard open right now (for failed writes).
    public static string ClipboardHolder()
    {
        IntPtr h = GetOpenClipboardWindow();
        if (h == IntPtr.Zero) return "no window (OpenClipboard(NULL) or already closed)";
        int pid = ProcessOf(h);
        string name = "?";
        try { name = System.Diagnostics.Process.GetProcessById(pid).ProcessName; } catch { }
        return name + " (pid " + pid + ", window class " + ClassOf(h) + ")";
    }

    public static void ExitSizeMove(IntPtr hwnd)
    {
        SendMessage(hwnd, 0x0232, IntPtr.Zero, IntPtr.Zero);                         // WM_EXITSIZEMOVE
    }
}
"@

# ---------------------------------------------------------------- helpers

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
$scale = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero).DpiX / 96.0
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea

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

function Write-Settings([hashtable]$dialog = @{}, [bool]$autoPaste = $true) {
    New-Item -ItemType Directory -Force -Path $configDir | Out-Null
    $settings = [ordered]@{
        history    = @{ maxItems = 100 }
        behavior   = @{ autoPaste = $autoPaste }
        onboarding = @{ welcomeShown = $true }
        dialog     = $dialog
    }
    ($settings | ConvertTo-Json -Depth 6) | Set-Content -Path $settingsPath -Encoding UTF8
}

$script:app = $null
function Start-App {
    if ($GcConserveMemory -gt 0) { $env:DOTNET_GCConserveMemory = "$GcConserveMemory" } else { Remove-Item Env:DOTNET_GCConserveMemory -ErrorAction SilentlyContinue }
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
    # Retries: another process (WindowsCM reading the last copy) may hold
    # the clipboard open for a moment.
    [System.Windows.Forms.Clipboard]::SetDataObject($data, $true, 20, 50)
}

function Item-Counts {
    $py = "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print(';'.join(t+'='+str(n) for t,n in c.execute('select type, count(*) from clipboard group by type')))"
    $pairs = @{}
    foreach ($pair in ((& python -c $py $dbPath) -split ';')) { if ($pair) { $kv = $pair -split '='; $pairs[$kv[0]] = [int]$kv[1] } }
    return $pairs
}

function Item-Count {
    $py = "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print(c.execute('select count(*) from clipboard').fetchone()[0])"
    return [int](& python -c $py $dbPath)
}

function Popup-Rect {
    $h = [Smoke]::LargestVisibleWindow($script:app.Id)
    if ($h -eq [IntPtr]::Zero) { return $null }
    $r = New-Object Smoke+RECT
    [Smoke]::GetWindowRect($h, [ref]$r) | Out-Null
    return @{ Handle = $h; Left = $r.Left; Top = $r.Top; Right = $r.Right; Bottom = $r.Bottom; Width = $r.Right - $r.Left; Height = $r.Bottom - $r.Top }
}

function Near([double]$a, [double]$b, [double]$tolerance = 4) { return [math]::Abs($a - $b) -le $tolerance }

function Sample-Process([string]$label) {
    $p = Get-Process -Id $script:app.Id
    $sample = [ordered]@{
        at = $label
        workingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
        privateMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        handles = $p.HandleCount
        threads = $p.Threads.Count
    }
    return $sample
}

# Managed heap after a full, induced GC (dotnet-gcdump collects one): the
# leak signal. Private bytes alone only show that the GC has not run yet.
$gcdumpExe = Join-Path $env:USERPROFILE '.dotnet\tools\dotnet-gcdump.exe'
function Heap-After-GC([string]$label) {
    if (-not (Test-Path $gcdumpExe)) { return $null }
    $dump = Join-Path $OutDir "heap-$label.gcdump"
    & $gcdumpExe collect -p $script:app.Id -o $dump 2>&1 | Out-Null
    if (-not (Test-Path $dump)) { return $null }
    $text = (& $gcdumpExe report $dump 2>&1 | Out-String)
    Set-Content -Path (Join-Path $OutDir "heap-$label.txt") -Value $text
    $match = [regex]::Match($text, '([\d,\.]+)\s+GC Heap bytes')
    $bytes = if ($match.Success) { [double]($match.Groups[1].Value -replace '[,\.]', '') } else { $null }
    $top = ($text -split "`n" | Where-Object { $_ -match '^\s+[\d,\.]+\s+[\d,\.]+\s+\S' } | Select-Object -First 6) -join "`n"
    return @{ MB = if ($bytes) { [math]::Round($bytes / 1MB, 1) } else { $null }; Top = $top }
}

function New-Payload([int]$i) {
    $data = New-Object System.Windows.Forms.DataObject
    switch ($i % 9) {
        0 { $data.SetText("public static int Compute$i(int x) { return x * $i; } // code") }
        1 { $data.SetText("https://example.com/articles/${i}?ref=smoke") }   # braces: "$i?" would read a variable named "i?"
        2 { $data.SetText("#{0:X6}" -f $random.Next(0x1000000)) }
        3 { if ($i % 27 -eq 3) { $data.SetText($bigText + $i) } else { $data.SetText("note $i " + ('x' * $random.Next(10, 4000))) } }
        4 {
            if ($i % 18 -eq 4) {
                $bmp = New-Object System.Drawing.Bitmap 1600, 900
                $g = [System.Drawing.Graphics]::FromImage($bmp)
                $g.Clear([System.Drawing.Color]::FromArgb($random.Next(256), $random.Next(256), $random.Next(256)))
                $g.DrawString("smoke $i", (New-Object System.Drawing.Font 'Arial', 48), [System.Drawing.Brushes]::White, 40, 40)
                $g.Dispose()
                $data.SetImage($bmp)
            } else { $data.SetText("plain text item number $i") }
        }
        5 {
            $list = New-Object System.Collections.Specialized.StringCollection
            $list.AddRange([string[]]$tempFiles[0..($i % 5)])
            $data.SetFileDropList($list)
        }
        6 { $data.SetText([char]::ConvertFromUtf32(0x1F600 + ($i % 50))) }
        default { $data.SetText("multi`nline`ntext $i`n" + ('line ' * ($i % 40))) }
    }
    return ,$data
}

function Copy-Burst([int]$from, [int]$to, $failuresList) {
    for ($i = $from; $i -le $to; $i++) {
        $data = New-Payload $i
        try { Set-Clip $data } catch { $failuresList.Add("copy $i ($($data.GetFormats() -join ', ')) held by $([Smoke]::ClipboardHolder())") }
        if ($i % 20 -lt 3) { Start-Sleep -Milliseconds 5 } else { Start-Sleep -Milliseconds $gapMs }
    }
}

if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'python is required to read the history database' }

# ------------------------------------------------------------ fresh state

try {

Stop-App
Remove-Item -Recurse -Force $dataDir, $configDir -ErrorAction SilentlyContinue
Note "# WindowsCM app smoke"
Note ""
Note "Screen: $($work.Width)x$($work.Height) working area, DPI scale $scale, $([System.Windows.Forms.Screen]::AllScreens.Count) monitor(s)."

# ---------------------------------------------------------------- 1. stress

Note ""
Note "## 0. Baseline: the same copies with WindowsCM NOT running"
$tempFiles = @()
for ($f = 0; $f -lt 5; $f++) { $path = Join-Path $env:TEMP "wcm-smoke-$f.txt"; Set-Content $path "file $f"; $tempFiles += $path }
$bigText = ('lorem ipsum dolor sit amet, consectetur adipiscing elit ' * 40000)   # ~2.2 MB
$random = New-Object System.Random 7
$gapMs = 150
$baselineFailures = New-Object System.Collections.Generic.List[string]
Copy-Burst 1 60 $baselineFailures
foreach ($failure in ($baselineFailures | Select-Object -First 10)) { Note "    clipboard busy at $failure" }
Note "  environment alone: $($baselineFailures.Count) of 60 clipboard writes failed"
$metrics.baselineClipboardWriteFailures = $baselineFailures.Count

Note ""
Note "## 1. Stress: $Copies copies + popup cycles"
Write-Settings
Start-App
$startup = Sample-Process 'startup'
$samples = New-Object System.Collections.Generic.List[object]
$samples.Add($startup)
$openTimes = New-Object System.Collections.Generic.List[double]

$clipboardFailures = New-Object System.Collections.Generic.List[string]
$heapPoints = New-Object System.Collections.Generic.List[object]
$copyWatch = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le $Copies; $i++) {
    $data = New-Payload $i
    # Another app copying while WindowsCM reads the previous copy: it must
    # never find the clipboard held for longer than its ~1 s of retries.
    try { Set-Clip $data } catch { $clipboardFailures.Add("copy $i ($($data.GetFormats() -join ', ')) held by $([Smoke]::ClipboardHolder())") }
    # A fast human pace, plus bursts (every 20th copy starts three in a
    # row, 5 ms apart) that WindowsCM coalesces into one read.
    if ($i % 20 -lt 3) { Start-Sleep -Milliseconds 5 } else { Start-Sleep -Milliseconds $gapMs }

    if ($i % 50 -eq 0) {
        if (-not (Alive)) { break }
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $reply = Pipe 'show'
        $watch.Stop()
        $openTimes.Add($watch.Elapsed.TotalMilliseconds)
        Start-Sleep -Milliseconds 400
        Pipe 'hide' | Out-Null
        if ($reply -ne 'ok') { $failures.Add("pipe show answered '$reply' at copy $i") }
        $samples.Add((Sample-Process "copy $i"))
        if ($i % 150 -eq 0) {
            $point = Heap-After-GC "copy$i"
            if ($point) {
                $after = Sample-Process "after GC at copy $i"
                $heapPoints.Add(@{ copy = $i; heapMB = $point.MB; privateMB = $after.privateMB; top = $point.Top })
            }
        }
    }
}
$copyWatch.Stop()
Start-Sleep -Seconds 2

Check (Alive) "app still running after $Copies copies and $($openTimes.Count) popup cycles"
foreach ($failure in ($clipboardFailures | Select-Object -First 25)) { Note "    clipboard busy at $failure" }
Check ($clipboardFailures.Count -eq 0) "other apps can always copy: $($clipboardFailures.Count) of $Copies clipboard writes failed"
$metrics.clipboardWriteFailures = $clipboardFailures.Count
if (Alive) {
    $count = Item-Count
    Note "  items in history: $count"
    Check ($count -le 100) "history stays within the 100-item limit (Evict on every capture)"
    Check ($count -ge 50) "copies were captured (>= 50 items)"
    $byType = Item-Counts
    Note "  by type: $(($byType.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name) $($_.Value)" }) -join ', ')"
    # The last 100 copies hold ~11 of each generated kind (the five file
    # lists repeat, so they dedupe to at most 5; images every 18th copy).
    Check (($byType['Image'] -ge 2) -and (($byType['File'] + $byType['Files']) -ge 3) -and ($byType['Text'] -ge 20) -and ($byType['Link'] -ge 8) -and ($byType['Code'] -ge 8) -and ($byType['Color'] -ge 8) -and ($byType['Character'] -ge 8)) "every kind was captured in the expected proportion"
    $metrics.itemsByType = $byType
    $final = Sample-Process 'end'
    $samples.Add($final)
    $early = $samples | Where-Object { $_.at -eq 'copy 100' } | Select-Object -First 1
    if ($early) {
        $growth = $final.privateMB - $early.privateMB
        Note "  private memory: $($early.privateMB) MB at copy 100 -> $($final.privateMB) MB at the end ($([math]::Round($growth,1)) MB)"
        # Informative: private bytes grow until the GC decides to collect;
        # the leak check is the managed heap after a forced full GC below.
    }
    Check ($final.workingSetMB -lt 600) "working set under 600 MB ($($final.workingSetMB) MB)"
    $sorted = $openTimes | Sort-Object
    if ($sorted.Count -gt 0) {
        $p50 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.5)]
        $max = $sorted[$sorted.Count - 1]
        Note ("  popup open via pipe: p50 {0:N0} ms, max {1:N0} ms over {2} opens" -f $p50, $max, $sorted.Count)
        Check ($max -lt 3000) "popup opens in under 3 s even with a full history"
        $metrics.popupOpenMs = @{ p50 = [math]::Round($p50, 1); max = [math]::Round($max, 1); samples = $sorted.Count }
    }
    $metrics.copiesPerSecond = [math]::Round($Copies / $copyWatch.Elapsed.TotalSeconds, 1)
    foreach ($sample in $samples) { Note ("  memory at {0}: private {1} MB, working set {2} MB, {3} handles, {4} threads" -f $sample.at, $sample.privateMB, $sample.workingSetMB, $sample.handles, $sample.threads) }
    foreach ($point in $heapPoints) { Note "  after a full GC at copy $($point.copy): managed heap $($point.heapMB) MB, private bytes $($point.privateMB) MB" }
    if ($heapPoints.Count -ge 2) {
        $first = $heapPoints[0]
        $last = $heapPoints[$heapPoints.Count - 1]
        Note '  largest types at the end:'
        Note '```'
        Note $last.top
        Note '```'
        Check (($last.heapMB - $first.heapMB) -lt 20) "no managed memory leak: the heap after a full GC stays flat once the history is full (+$([math]::Round($last.heapMB - $first.heapMB, 1)) MB from copy $($first.copy) to $($last.copy))"
        $metrics.afterFullGc = $heapPoints | ForEach-Object { @{ copy = $_.copy; heapMB = $_.heapMB; privateMB = $_.privateMB } }
    } else {
        Note "  (dotnet-gcdump unavailable: managed heap not measured)"
    }
    # The history itself, for diagnosis (type, time, title, content start).
    $dump = "import sqlite3,sys; c=sqlite3.connect(sys.argv[1])`nfor r in c.execute('select id, type, datetime, title, substr(replace(content, char(10), \' \'), 1, 70) from clipboard order by datetime desc'): print(*r, sep=' | ')"
    $env:PYTHONIOENCODING = 'utf-8'
    & python -c $dump $dbPath | Set-Content (Join-Path $OutDir 'history.txt') -Encoding UTF8
    # The same popup, app idle: separates rendering cost from load.
    $idleTimes = New-Object System.Collections.Generic.List[double]
    for ($k = 0; $k -lt 6; $k++) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        Pipe 'show' | Out-Null
        $watch.Stop()
        $idleTimes.Add($watch.Elapsed.TotalMilliseconds)
        Start-Sleep -Milliseconds 400
        Pipe 'hide' | Out-Null
        Start-Sleep -Milliseconds 300
    }
    $idleSorted = $idleTimes | Sort-Object
    $idleP50 = $idleSorted[[int][math]::Floor(($idleSorted.Count - 1) * 0.5)]
    Note ("  popup open, app idle: p50 {0:N0} ms, first {1:N0} ms, max {2:N0} ms" -f $idleP50, $idleTimes[0], $idleSorted[$idleSorted.Count - 1])
    $metrics.popupOpenIdleMs = @{ p50 = [math]::Round($idleP50, 1); max = [math]::Round($idleSorted[$idleSorted.Count - 1], 1) }
    $metrics.itemsAfterStress = $count
}
$metrics.processSamples = $samples
Stop-App

if ($StressOnly) { throw 'StressOnly: stopping after the stress phase' }

# ------------------------------------------------------------- 2. placement

Note ""
Note "## 2. Large window placement"

function Show-And-Measure([hashtable]$dialog) {
    Write-Settings -dialog $dialog
    Start-App
    Pipe 'show' | Out-Null
    Start-Sleep -Milliseconds 600
    return Popup-Rect
}

# Docked (default): full-width strip at the bottom of the primary work area.
$rect = Show-And-Measure @{ largePlacement = 'FollowMouse' }
$margin = 20 * $scale
Check ($rect -ne $null) "docked: popup visible"
if ($rect) {
    Check ((Near $rect.Left ($work.Left + $margin)) -and (Near $rect.Right ($work.Right - $margin)) -and (Near $rect.Bottom ($work.Bottom - $margin))) "docked: bottom strip with 20 DIP margins ($($rect.Left),$($rect.Top) $($rect.Width)x$($rect.Height))"
    $edge = [Smoke]::HitTest($rect.Handle, $rect.Right - 2, $rect.Top + [int]($rect.Height / 2))
    Check ($edge -ne 11) "docked: edges do not resize (hit test $edge)"
}
Stop-App

# Fixed monitor that is not connected: the primary stands in.
$rect = Show-And-Measure @{ largePlacement = 'FixedMonitor'; largeMonitor = '\\.\DISPLAY99' }
Check ($rect -ne $null -and (Near $rect.Bottom ($work.Bottom - $margin)) -and $rect.Left -ge $work.Left -and $rect.Right -le $work.Right) "fixed monitor (disconnected): falls back to the primary"
Stop-App

# Free, saved rectangle: reopens exactly there, edges resize, moves are saved.
$saved = @{ left = 40; top = 60; width = 760; height = 300 }
$rect = Show-And-Measure @{ largePlacement = 'Free'; largeFreeBoundsHorizontal = $saved }
Check ($rect -ne $null) "free: popup visible"
if ($rect) {
    Check ((Near $rect.Left (40 * $scale)) -and (Near $rect.Top (60 * $scale)) -and (Near $rect.Width (760 * $scale)) -and (Near $rect.Height (300 * $scale))) "free: reopens at the saved rectangle ($($rect.Left),$($rect.Top) $($rect.Width)x$($rect.Height))"
    $midY = $rect.Top + [int]($rect.Height / 2)
    $midX = $rect.Left + [int]($rect.Width / 2)
    Check ([Smoke]::HitTest($rect.Handle, $rect.Right - 2, $midY) -eq 11) "free: right edge resizes (HTRIGHT)"
    Check ([Smoke]::HitTest($rect.Handle, $rect.Left + 1, $rect.Top + 1) -eq 13) "free: top-left corner resizes (HTTOPLEFT)"
    Check ([Smoke]::HitTest($rect.Handle, $midX, $midY) -eq 1) "free: the content is not a resize handle (HTCLIENT)"
    # Simulate the end of a user move: Windows sends WM_EXITSIZEMOVE.
    [Smoke]::SetWindowPos($rect.Handle, [IntPtr]::Zero, [int](120 * $scale), [int](200 * $scale), $rect.Width, $rect.Height, 0x0014) | Out-Null
    Start-Sleep -Milliseconds 300
    [Smoke]::ExitSizeMove($rect.Handle)
    Start-Sleep -Milliseconds 500
    $persisted = (Get-Content $settingsPath -Raw | ConvertFrom-Json).dialog.largeFreeBoundsHorizontal
    Check ($persisted -and (Near $persisted.left 120 1) -and (Near $persisted.top 200 1)) "free: new position saved to settings after the move ($($persisted.left),$($persisted.top))"
}
Stop-App

# Free, rectangle from a monitor that is gone: pulled back on screen.
$rect = Show-And-Measure @{ largePlacement = 'Free'; largeFreeBoundsHorizontal = @{ left = -6000; top = -6000; width = 900; height = 400 } }
Check ($rect -ne $null -and $rect.Left -ge $work.Left - 1 -and $rect.Top -ge $work.Top - 1 -and $rect.Right -le $work.Right + 1 -and $rect.Bottom -le $work.Bottom + 1) "free: off-screen rectangle is pulled back into the work area"
Stop-App

# ------------------------------------------------------------ 3. auto-paste

Note ""
Note "## 3. Auto-paste end to end (Notepad)"

$notepad = Start-Process notepad -PassThru
$notepad.WaitForInputIdle(10000) | Out-Null
Start-Sleep -Milliseconds 800
$notepad.Refresh()
$np = $notepad.MainWindowHandle
$edit = [Smoke]::FindTextControl($np)
Check ($edit -ne [IntPtr]::Zero) "notepad text control found ($([Smoke]::ClassOf($edit)))"

function Paste-Scenario([string]$name, [scriptblock]$focus, [bool]$expectPasted) {
    $token = "wcm-smoke-$name-" + [guid]::NewGuid().ToString('N').Substring(0, 8)
    [Smoke]::SetText($edit, '')
    [Smoke]::Focus($np) | Out-Null
    [Smoke]::Chord(0x1B)                 # Esc: Notepad never left in menu mode
    try { Set-Clip $token } catch { $failures.Add("${name}: clipboard write failed, held by $([Smoke]::ClipboardHolder())"); Note "  FAIL  ${name}: clipboard write failed"; return }
    Start-Sleep -Milliseconds 700        # captured as the newest item
    $focused = & $focus
    if (-not $focused) { $failures.Add("${name}: could not set up the foreground window"); Note "  FAIL  ${name}: foreground setup"; return }
    [Smoke]::Chord(0x11, 0x10, 0x56)     # Ctrl+Shift+V: the real hotkey
    Start-Sleep -Milliseconds 900
    $popup = [Smoke]::GetForegroundWindow()
    $popupIsOurs = [Smoke]::ProcessOf($popup) -eq $script:app.Id
    [Smoke]::Chord(0x0D)                 # Enter: pick the newest item
    Start-Sleep -Milliseconds 1500
    $text = [Smoke]::GetText($edit)
    $clip = [System.Windows.Forms.Clipboard]::GetText()
    if (-not $popupIsOurs) { Note "    (the hotkey did not bring the popup to the foreground)" }
    if ($expectPasted) {
        Check ($popupIsOurs -and $text.Contains($token)) "${name}: pasted into Notepad"
    } else {
        Check ($popupIsOurs -and -not $text.Contains($token) -and $clip -eq $token) "${name}: only copied (Notepad untouched, item on the clipboard)"
    }
    [Smoke]::Chord(0x1B)                 # Esc, in case the popup stayed open
    Start-Sleep -Milliseconds 300
}

Write-Settings -autoPaste $true
Start-App
Paste-Scenario 'focused-field' { [Smoke]::Focus($np) } $true
Paste-Scenario 'via-taskbar' {
    [Smoke]::Focus($np) | Out-Null
    $taskbar = [Smoke]::FindWindow('Shell_TrayWnd', $null)
    ($taskbar -ne [IntPtr]::Zero) -and ([Smoke]::Focus($taskbar))
} $true
Paste-Scenario 'from-desktop' {
    $desktop = [Smoke]::FindWindow('Progman', $null)
    if (($desktop -ne [IntPtr]::Zero) -and ([Smoke]::Focus($desktop))) { return $true }
    [Smoke]::Chord(0x5B, 0x44)           # Win+D
    Start-Sleep -Milliseconds 600
    @('Progman', 'WorkerW') -contains [Smoke]::ClassOf([Smoke]::GetForegroundWindow())
} $false
Stop-App

Write-Settings -autoPaste $false
Start-App
Paste-Scenario 'auto-paste-off' { [Smoke]::Focus($np) } $false
Stop-App
Stop-Process -Id $notepad.Id -Force -ErrorAction SilentlyContinue

# ------------------------------------------------------------ error log

Note ""
Note "## Error log"
if (Test-Path $logPath) {
    $log = Get-Content $logPath -Raw
    Copy-Item $logPath (Join-Path $OutDir 'windowscm.log')
    Note '```'
    Note $log
    Note '```'
    Check $false "no errors logged by the app"
} else {
    Check $true "no errors logged by the app (log file never created)"
}

} catch {
    if ($_.Exception.Message -like 'StressOnly*') { Note ""; Note "(stopped after the stress phase)" } else {
    Note ""
    Note "ABORTED: $($_.Exception.Message)"
    Note "$($_.ScriptStackTrace)"
    $failures.Add("smoke aborted: $($_.Exception.Message)")
    }
} finally {
    Stop-App
}

# ----------------------------------------------------------------- report

Note ""
if ($failures.Count -eq 0) { Note "RESULT: PASS" } else { Note "RESULT: FAIL ($($failures.Count))"; $failures | ForEach-Object { Note "  - $_" } }
$report | Set-Content (Join-Path $OutDir 'report.md') -Encoding UTF8
($metrics | ConvertTo-Json -Depth 6) | Set-Content (Join-Path $OutDir 'metrics.json') -Encoding UTF8
Remove-Item $tempFiles -ErrorAction SilentlyContinue
if ($failures.Count -gt 0) { exit 1 }
exit 0
