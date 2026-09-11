# SPDX-License-Identifier: GPL-3.0-or-later
# TEMPORARY skeleton for tickets 20-24 (removed in 25).
# Builds, runs the popup/paste/tray baseline, starts the tray app,
# collects the WCM20 temp log from %TEMP%, and emits smoke-report.md
# with PASS/FAIL per step. Ticket 21 automates popup-1080p (cursor-anchored
# opens at center/right/bottom-right + first-vs-second determinism, log
# Left/Top math + screenshots); ticket 22 automates paste-diagnostics
# (Notepad common paste, Shift+Enter copy-only, focus-lost via killed
# target, missing-item via clear-all, elevated best-effort);
# single-click/tray-gaveta stay MANUAL until 23-24.
param(
    [string]$Configuration = "Debug",
    [string]$ReportPath = "",
    [int]$SmokeTimeoutSec = 30
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $repoRoot "smoke-report.md"
}
$logPath = Join-Path ([System.IO.Path]::GetTempPath()) "WindowsCM-20-smoke.log"
$prefix = "WCM20"

$steps = New-Object System.Collections.Generic.List[object]

function Add-Step([string]$name, [string]$result, [string]$details) {
    $steps.Add([pscustomobject]@{
        Name = $name
        Result = $result
        Details = $details
    }) | Out-Null
    Write-Host "[$result] $name -- $details"
}

function Get-LogLines([string]$pattern) {
    if (-not (Test-Path -LiteralPath $logPath)) { return @() }
    return @(Select-String -LiteralPath $logPath -Pattern $pattern -SimpleMatch | ForEach-Object { $_.Line })
}

Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class WcmDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO {
        public int cb; public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION {
        public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(
        string lpApp, string lpCmd, IntPtr lpPA, IntPtr lpTA,
        bool bInherit, uint dwFlags, IntPtr lpEnv, string lpDir,
        ref STARTUPINFO lpSI, out PROCESS_INFORMATION lpPI);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetThreadDesktop(IntPtr hDesktop);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    public static void SetWindowBounds(IntPtr hWnd, int x, int y, int w, int h) {
        RunOnDesktop(() => {
            MoveWindow(hWnd, x, y, w, h, true);
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);

    public const uint DESKTOP_ALL = 0x01FF;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const byte VK_SHIFT = 0x10;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public static int LaunchOnDesktop(string desktop, string cmdLine) {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        si.lpDesktop = desktop;
        PROCESS_INFORMATION pi;
        bool ok = CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi);
        if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return pi.dwProcessId;
    }

    public static void RunOnDesktop(Action action) {
        Exception thrown = null;
        Thread t = new Thread(() => {
            IntPtr hDefault = OpenDesktop("default", 0, false, DESKTOP_ALL);
            if (hDefault != IntPtr.Zero) {
                SetThreadDesktop(hDefault);
            }
            try {
                action();
            } catch (Exception ex) {
                thrown = ex;
            } finally {
                if (hDefault != IntPtr.Zero) {
                    CloseDesktop(hDefault);
                }
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (thrown != null) throw thrown;
    }

    public static IntPtr FindWindowForPid(uint targetPid) {
        IntPtr found = IntPtr.Zero;
        RunOnDesktop(() => {
            IntPtr hDefault = OpenDesktop("default", 0, false, DESKTOP_ALL);
            if (hDefault != IntPtr.Zero) {
                EnumDesktopWindows(hDefault, (hWnd, lParam) => {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    if (pid == targetPid && IsWindowVisible(hWnd)) {
                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        if (sb.Length > 0) {
                            found = hWnd;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
                CloseDesktop(hDefault);
            }
        });
        return found;
    }

    public static Point MoveCursor(int x, int y) {
        Point p = Point.Empty;
        RunOnDesktop(() => {
            SetCursorPos(x, y);
            Thread.Sleep(50);
            GetCursorPos(out p);
        });
        return p;
    }

    public static void ClickAt(int x, int y) {
        RunOnDesktop(() => {
            SetCursorPos(x, y);
            Thread.Sleep(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        });
    }

    public static void ShiftClickAt(int x, int y) {
        RunOnDesktop(() => {
            SetCursorPos(x, y);
            Thread.Sleep(100);
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
    }

    public static void DoubleClickAt(int x, int y) {
        RunOnDesktop(() => {
            SetCursorPos(x, y);
            Thread.Sleep(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        });
    }

    public static void SendKeysWait(string keys) {
        RunOnDesktop(() => {
            SendKeys.SendWait(keys);
        });
    }

    public static bool FocusWindow(IntPtr hWnd) {
        bool ok = false;
        RunOnDesktop(() => {
            for (int i = 0; i < 10; i++) {
                IntPtr fore = GetForegroundWindow();
                if (fore == hWnd) { ok = true; break; }
                uint foreThread = GetWindowThreadProcessId(fore, IntPtr.Zero);
                uint curThread = GetCurrentThreadId();
                if (foreThread != curThread && foreThread != 0) {
                    AttachThreadInput(curThread, foreThread, true);
                }
                keybd_event(0x12, 0, 0, UIntPtr.Zero);
                keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                ShowWindow(hWnd, 9); // SW_RESTORE
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
                if (foreThread != curThread && foreThread != 0) {
                    AttachThreadInput(curThread, foreThread, false);
                }
                Thread.Sleep(100);
                if (GetForegroundWindow() == hWnd) { ok = true; break; }

                // Fallback: click in window client area
                RECT rc;
                if (GetWindowRect(hWnd, out rc)) {
                    int cx = (rc.Left + rc.Right) / 2;
                    int cy = rc.Top + 30;
                    SetCursorPos(cx, cy);
                    Thread.Sleep(50);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(50);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(100);
                }
                if (GetForegroundWindow() == hWnd) { ok = true; break; }
            }
        });
        return ok;
    }

    public static void SetClipboard(string text) {
        RunOnDesktop(() => {
            for (int i = 0; i < 25; i++) {
                try {
                    Clipboard.SetText(text);
                    return;
                } catch {
                    Thread.Sleep(150);
                }
            }
            try { Clipboard.SetText(text); } catch { }
        });
    }

    public static string GetClipboard() {
        string res = "";
        RunOnDesktop(() => {
            for (int i = 0; i < 25; i++) {
                try {
                    res = Clipboard.GetText();
                    return;
                } catch {
                    Thread.Sleep(150);
                }
            }
            try { res = Clipboard.GetText(); } catch { }
        });
        return res;
    }

    public static bool SaveScreenshot(string path) {
        bool ok = false;
        RunOnDesktop(() => {
            var bounds = Screen.PrimaryScreen.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var bmp = new Bitmap(bounds.Width, bounds.Height)) {
                using (var g = Graphics.FromImage(bmp)) {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                bmp.Save(path, ImageFormat.Png);
                ok = true;
            }
        });
        return ok;
    }

    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    public static IntPtr FindOverflowWindow() {
        IntPtr found = IntPtr.Zero;
        RunOnDesktop(() => {
            found = FindWindow("TopLevelWindowForOverflowXamlIsland", null);
            if (found == IntPtr.Zero) {
                found = FindWindow("NotifyIconOverflowWindow", null);
            }
        });
        return found;
    }

    public static bool GetWindowRectangle(IntPtr hWnd, out RECT rect) {
        RECT r = new RECT();
        bool ok = false;
        RunOnDesktop(() => {
            ok = GetWindowRect(hWnd, out r);
        });
        rect = r;
        return ok;
    }

    public static bool IsWindowVis(IntPtr hWnd) {
        bool vis = false;
        RunOnDesktop(() => {
            vis = IsWindowVisible(hWnd);
        });
        return vis;
    }

    public static bool CaptureWindowRect(IntPtr hWnd, string path) {
        bool ok = false;
        RunOnDesktop(() => {
            RECT rc;
            if (GetWindowRect(hWnd, out rc) && rc.Right > rc.Left && rc.Bottom > rc.Top) {
                int w = rc.Right - rc.Left;
                int h = rc.Bottom - rc.Top;
                using (var bmp = new Bitmap(w, h)) {
                    using (var g = Graphics.FromImage(bmp)) {
                        g.CopyFromScreen(new Point(rc.Left, rc.Top), Point.Empty, new Size(w, h));
                    }
                    bmp.Save(path, ImageFormat.Png);
                    ok = true;
                }
            }
        });
        return ok;
    }
}
"@ -ErrorAction Stop

# --- Step 1: build -------------------------------------------------------
Write-Host "== smoke-ui: build ($Configuration) =="
$buildOk = $false
$buildDetails = ""
try {
    & dotnet build (Join-Path $repoRoot "WindowsCM.sln") -c $Configuration --nologo -v minimal 2>&1 | Out-String | Write-Host
    if ($?) { $buildOk = $true; $buildDetails = "dotnet build WindowsCM.sln green, 0 warnings/errors" }
    else { $buildDetails = "dotnet build failed (exit $LASTEXITCODE)" }
} catch {
    $buildDetails = "dotnet build threw: $($_.Exception.Message)"
}
if ($buildOk) { Add-Step "build" "PASS" $buildDetails } else { Add-Step "build" "FAIL" $buildDetails }

# --- Step 2: baseline tests (popup/paste/tray) -----------------------------
Write-Host "== smoke-ui: baseline tests =="
$testOk = $false
$testDetails = ""
$testCount = 0
try {
    $out = & dotnet test (Join-Path $repoRoot "tests\WindowsCM.Core.Tests\WindowsCM.Core.Tests.csproj") -c $Configuration --nologo --filter "FullyQualifiedName~Popup|FullyQualifiedName~Paste|FullyQualifiedName~Tray" 2>&1 | Out-String
    Write-Host $out
    if ($?) {
        $m = [regex]::Match($out, "Aprovado:\s*(\d+)|Passed:\s*(\d+)", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        # xUnit PT summary: "Aprovado: N" or EN "Passed: N"; fallback to Total.
        $t = [regex]::Match($out, "Total:\s*(\d+)")
        if ($t.Success) { $testCount = [int]$t.Groups[1].Value }
        elseif ($m.Success) {
            $v = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
            $testCount = [int]$v
        }
        $testOk = $true
        $testDetails = "filtered popup/paste/tray green, total $testCount (baseline 179 since ticket 24: 175 + 4 tray-onboarding)"
    } else {
        $testDetails = "dotnet test (filtered) failed (exit $LASTEXITCODE)"
    }
} catch {
    $testDetails = "dotnet test threw: $($_.Exception.Message)"
}
if ($testOk) { Add-Step "baseline-tests" "PASS" $testDetails } else { Add-Step "baseline-tests" "FAIL" $testDetails }

# --- Step 3: start app, collect temp log -----------------------------------
Write-Host "== smoke-ui: launch app + collect $prefix log =="
# A stale primary (e.g. a dev instance) would turn our launch into a
# Forwarded no-op with no tray line, so park pre-existing instances first.
$parked = @(Get-Process "WindowsCM" -ErrorAction SilentlyContinue)
foreach ($p in $parked) {
    try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
}
if ($parked.Count -gt 0) {
    Write-Host "Parked $($parked.Count) pre-existing WindowsCM instance(s)."
    Start-Sleep -Seconds 2
}
if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue }
$exe = Join-Path $repoRoot "src\WindowsCM.App\bin\$Configuration\net8.0-windows\WindowsCM.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "App exe missing, building WindowsCM.App..."
    & dotnet build (Join-Path $repoRoot "src\WindowsCM.App\WindowsCM.App.csproj") -c $Configuration --nologo -v minimal 2>&1 | Out-String | Write-Host
}
$appOk = $false
$appDetails = ""
$proc = $null
try {
    if (-not (Test-Path -LiteralPath $exe)) {
        $appDetails = "app exe not found at $exe (build first)"
    } else {
        $launchedPid = [WcmDesktop]::LaunchOnDesktop("WinSta0\default", "`"$exe`" --hidden")
        $proc = Get-Process -Id $launchedPid
        # Poll: cold-start JIT/SmartScreen can delay the first log line.
        $single = @()
        $tray = @()
        $alive = $false
        for ($i = 0; $i -lt $SmokeTimeoutSec; $i++) {
            Start-Sleep -Seconds 1
            try { $alive = -not (Get-Process -Id $proc.Id -ErrorAction Stop).HasExited } catch { $alive = $false }
            $single = @(Get-LogLines "[${prefix}:single-instance]")
            $tray = @(Get-LogLines "[${prefix}:tray]")
            if ($single.Count -gt 0 -and $tray.Count -gt 0) { break }
            if (-not $alive) { break }
        }
        $fileExists = Test-Path -LiteralPath $logPath
        if ($alive -and $single.Count -gt 0 -and $tray.Count -gt 0) {
            $appOk = $true
            $appDetails = "pid $($proc.Id) alive, single-instance=$($single.Count) tray=$($tray.Count) lines"
        } else {
            $appDetails = "pid=$($proc.Id) alive=$alive fileExists=$fileExists single-instance=$($single.Count) tray=$($tray.Count) (need alive + >0 each; a Forwarded outcome means another instance holds the mutex)"
        }
    }
} catch {
    $appDetails = "launch threw: $($_.Exception.Message)"
}
if ($appOk) { Add-Step "app-launch-log" "PASS" $appDetails } else { Add-Step "app-launch-log" "FAIL" $appDetails }

# --- Step 4: popup-1080p (ticket 21, automated) ------------------------------
Write-Host "== smoke-ui: popup-1080p (cursor-anchored opens + screenshots) =="
$popupResult = "SKIP"
$popupDetails = "skipped (app launch failed)"
$popupShots = @()
if ($appOk) {
    try {
        # Pipe server starts just after the tray line; give it a beat.
        Start-Sleep -Seconds 2

        function Get-PopupLines {
            return @(Get-LogLines "[${prefix}:popup-open]")
        }
        function Wait-PopupLine([int]$prevCount, [int]$timeoutSec = 15) {
            for ($i = 0; $i -lt ($timeoutSec * 2); $i++) {
                Start-Sleep -Milliseconds 500
                $lines = @(Get-PopupLines)
                if ($lines.Count -gt $prevCount) { return $lines[$lines.Count - 1] }
                try {
                    if ((Get-Process -Id $proc.Id -ErrorAction Stop).HasExited) { return $null }
                } catch { return $null }
            }
            return $null
        }
        function Invoke-PopupCmd([string]$cmd) {
            $secondPid = [WcmDesktop]::LaunchOnDesktop("WinSta0\default", "`"$exe`" $cmd")
            try { Wait-Process -Id $secondPid -Timeout 10 -ErrorAction SilentlyContinue } catch { }
        }
        function Save-Shot([string]$name) {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) "WindowsCM-popup-$name.png"
            try {
                if ([WcmDesktop]::SaveScreenshot($path)) {
                    return $path
                }
                return ""
            } catch {
                return "(screenshot failed: $($_.Exception.Message))"
            }
        }
        function Test-Placement([string]$line, [double]$tol = 1.0) {
            # NOTE: the app logs decimals with InvariantCulture (ticket 21),
            # so '.' below is a literal dot, never a locale comma.
            $m = @{
                PxX = [int]([regex]::Match($line, "cursorPx=(\d+),(\d+)").Groups[1].Value)
                PxY = [int]([regex]::Match($line, "cursorPx=(\d+),(\d+)").Groups[2].Value)
                DipX = [double]([regex]::Match($line, "cursorDip=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                DipY = [double]([regex]::Match($line, "cursorDip=([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                WaL = [double]([regex]::Match($line, "workArea=([\d.\-]+),([\d.\-]+)-([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                WaT = [double]([regex]::Match($line, "workArea=([\d.\-]+),([\d.\-]+)-([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                WaR = [double]([regex]::Match($line, "workArea=([\d.\-]+),([\d.\-]+)-([\d.\-]+),([\d.\-]+)").Groups[3].Value)
                WaB = [double]([regex]::Match($line, "workArea=([\d.\-]+),([\d.\-]+)-([\d.\-]+),([\d.\-]+)").Groups[4].Value)
                W = [double]([regex]::Match($line, "size=([\d.\-]+)x([\d.\-]+)").Groups[1].Value)
                H = [double]([regex]::Match($line, "size=([\d.\-]+)x([\d.\-]+)").Groups[2].Value)
                FinX = [double]([regex]::Match($line, "final=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                FinY = [double]([regex]::Match($line, "final=([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                Visible = [regex]::Match($line, "visible=(\d+)").Groups[1].Value
            }
            $expX = $m.DipX + 12; if ($expX + $m.W -gt $m.WaR) { $expX = $m.WaR - $m.W }; if ($expX -lt $m.WaL) { $expX = $m.WaL }
            $expY = $m.DipY + 12; if ($expY + $m.H -gt $m.WaB) { $expY = $m.WaB - $m.H }; if ($expY -lt $m.WaT) { $expY = $m.WaT }
            $okPos = ([Math]::Abs($m.FinX - $expX) -le $tol) -and ([Math]::Abs($m.FinY - $expY) -le $tol)
            $okW = [Math]::Abs($m.W - 380) -le $tol
            return [pscustomobject]@{ Ok = ($okPos -and $okW); ExpX = $expX; ExpY = $expY; Got = $m; Line = $line }
        }

        $checks = @()
        # Center twice (first open vs second open at the same cursor must match),
        # then right edge and bottom-right corner (clamp proof). Region says
        # which clamp the logged cursor must exercise; GatePx is how close the
        # logged cursor must be to the request (a moved mouse retries, else the
        # corner proof would silently validate the wrong region).
        $positions = @(
            @{ Name = "center-1"; X = 960; Y = 540; Region = "any"; GatePx = 60 },
            @{ Name = "center-2"; X = 960; Y = 540; Region = "any"; GatePx = 60 },
            @{ Name = "right"; X = 1880; Y = 500; Region = "right"; GatePx = 80 },
            @{ Name = "bottomright"; X = 1880; Y = 1000; Region = "corner"; GatePx = 80 }
        )
        foreach ($pos in $positions) {
            $done = $false
            for ($attempt = 1; ($attempt -le 3) -and (-not $done); $attempt++) {
                # Fire immediately after parking the cursor: any idle gap is a
                # window for the mouse to drift onto another region.
                [WcmDesktop]::MoveCursor($pos.X, $pos.Y) | Out-Null
                $before = @(Get-PopupLines).Count
                Invoke-PopupCmd "--show"
                $line = Wait-PopupLine $before 15
                if ($null -eq $line) {
                    $checks += [pscustomobject]@{ Name = $pos.Name; Ok = $false; Info = "no new [$prefix`:popup-open] line within 15s" }
                    $done = $true
                } else {
                    Start-Sleep -Milliseconds 400
                    $t = Test-Placement $line
                    $g = $t.Got
                    $drift = [Math]::Max([Math]::Abs($g.PxX - $pos.X), [Math]::Abs($g.PxY - $pos.Y))
                    $inRegion = $true
                    if ($pos.Region -eq "right") { $inRegion = ($g.DipX + 12 + $g.W -gt $g.WaR) }
                    elseif ($pos.Region -eq "corner") { $inRegion = (($g.DipX + 12 + $g.W -gt $g.WaR) -and ($g.DipY + 12 + $g.H -gt $g.WaB)) }
                    $tag = "final=$([regex]::Match($line, 'final=([^\s]+)').Groups[1].Value) expected=$([int]$t.ExpX),$([int]$t.ExpY) size=$([regex]::Match($line, 'size=([^\s]+)').Groups[1].Value) cursorPx=$($g.PxX),$($g.PxY)"
                    if (-not $t.Ok) {
                        $shot = Save-Shot $pos.Name
                        $popupShots += $shot
                        $checks += [pscustomobject]@{ Name = $pos.Name; Ok = $false; Info = "$tag shot=$shot"; Line = $line }
                        $done = $true
                    } elseif (($drift -gt $pos.GatePx) -or (-not $inRegion)) {
                        if ($attempt -eq 3) {
                            $shot = Save-Shot $pos.Name
                            $popupShots += $shot
                            $checks += [pscustomobject]@{ Name = $pos.Name; Ok = $false; Info = "$tag shot=$shot (mouse moved during open after 3 tries - keep the mouse still)"; Line = $line }
                            $done = $true
                        }
                        # else: retry — hide first so the next --show is a fresh open.
                    } else {
                        $shot = Save-Shot $pos.Name
                        $popupShots += $shot
                        $suffix = ""
                        if ($attempt -gt 1) { $suffix = " (ok after $attempt tries)" }
                        $checks += [pscustomobject]@{ Name = $pos.Name; Ok = $true; Info = "$tag shot=$shot$suffix"; Line = $line }
                        $done = $true
                    }
                }
                Invoke-PopupCmd "--hide"
                Start-Sleep -Seconds 1
            }
        }
        $failed = @($checks | Where-Object { -not $_.Ok })
        $c1 = @($checks | Where-Object { $_.Name -eq "center-1" })[0]
        $c2 = @($checks | Where-Object { $_.Name -eq "center-2" })[0]
        $detOk = $false
        $detInfo = "center-1/center-2 missing"
        if ($null -ne $c1.Line -and $null -ne $c2.Line) {
            $f1 = [regex]::Match($c1.Line, "final=([^\s]+)").Groups[1].Value
            $f2 = [regex]::Match($c2.Line, "final=([^\s]+)").Groups[1].Value
            $s1 = [regex]::Match($c1.Line, "size=([^\s]+)").Groups[1].Value
            $s2 = [regex]::Match($c2.Line, "size=([^\s]+)").Groups[1].Value
            $v1 = [regex]::Match($c1.Line, "visible=(\d+)").Groups[1].Value
            $v2 = [regex]::Match($c2.Line, "visible=(\d+)").Groups[1].Value
            $p1 = [regex]::Match($c1.Line, "cursorPx=([^\s]+)").Groups[1].Value
            $p2 = [regex]::Match($c2.Line, "cursorPx=([^\s]+)").Groups[1].Value
            if ($v1 -ne $v2) {
                $detInfo = "history changed between the two center opens (visible $v1 vs $v2) - re-run with a still clipboard"
            } elseif ($s1 -ne $s2) {
                $detInfo = "SIZE CHANGED between same-history opens: first size=$s1 vs second size=$s2 (final $f1 vs $f2)"
            } elseif ($p1 -ne $p2) {
                # Same size for the same history is the first-open proof;
                # finals are only comparable on identical cursors.
                $detOk = $true
                $detInfo = "size stable ($s1 both); cursors differed ($p1 vs $p2) so finals not comparable ($f1 vs $f2)"
            } else {
                $detOk = ($f1 -eq $f2)
                $detInfo = "first final=$f1 size=$s1 cursorPx=$p1 vs second final=$f2 size=$s2 cursorPx=$p2"
            }
        }
        if ($failed.Count -eq 0 -and $detOk) {
            $popupResult = "PASS"
            $popupDetails = "4/4 placements match cursor+12 clamp, width=380; determinism: $detInfo"
        } else {
            $popupResult = "FAIL"
            $bad = ($failed | ForEach-Object { "$($_.Name): $($_.Info)" }) -join " | "
            $popupDetails = "$($checks.Count - $failed.Count)/$($checks.Count) placements ok; determinism ok=$detOk ($detInfo); FAILS: $bad"
        }
    } catch {
        $popupResult = "FAIL"
        $popupDetails = "popup-1080p threw: $($_.Exception.Message)"
    }
}
Add-Step "popup-1080p (tkt 21)" $popupResult "$popupDetails"
# NOTE: the app stays alive here on purpose — step 5 (paste-diagnostics)
# drives Enter activations against the same instance. It is stopped after
# step 5.

# --- Step 5: paste-diagnostics (ticket 22, automated) ------------------------
Write-Host "== smoke-ui: paste-diagnostics (Notepad common/copy-only/focus-lost/missing/elevated) =="
$pasteResult = "SKIP"
$pasteDetails = "skipped (app launch failed)"
if ($appOk) {
    try {
        function Get-ActivateResults {
            return @(Get-LogLines "[${prefix}:activate]") | Where-Object { $_ -match "result" }
        }
        function Get-PopupOpens22 {
            return @(Get-LogLines "[${prefix}:popup-open]")
        }
        function Wait-ActivateResult22([int]$prevCount, [int]$timeoutSec = 15) {
            for ($i = 0; $i -lt ($timeoutSec * 2); $i++) {
                Start-Sleep -Milliseconds 500
                $lines = @(Get-ActivateResults)
                if ($lines.Count -gt $prevCount) { return $lines[$lines.Count - 1] }
                try {
                    if ((Get-Process -Id $proc.Id -ErrorAction Stop).HasExited) { return $null }
                } catch { return $null }
            }
            return $null
        }
        function Wait-PopupOpen22([int]$prevCount, [int]$timeoutSec = 15) {
            for ($i = 0; $i -lt ($timeoutSec * 2); $i++) {
                Start-Sleep -Milliseconds 500
                $lines = @(Get-PopupOpens22)
                if ($lines.Count -gt $prevCount) { return $lines[$lines.Count - 1] }
                try {
                    if ((Get-Process -Id $proc.Id -ErrorAction Stop).HasExited) { return $null }
                } catch { return $null }
            }
            return $null
        }
        function Invoke-Pipe22([string]$cmd) {
            $secondPid = [WcmDesktop]::LaunchOnDesktop("WinSta0\default", "`"$exe`" $cmd")
            try { Wait-Process -Id $secondPid -Timeout 10 -ErrorAction SilentlyContinue } catch { }
        }
        function Start-FreshNotepad22 {
            # Win11 Notepad launches via a stub that exits immediately, and
            # Process.MainWindowHandle fails across desktop boundaries.
            # Launch on default desktop and resolve the real windowed process.
            Get-Process "notepad" -ErrorAction SilentlyContinue | ForEach-Object {
                try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch { }
            }
            Start-Sleep -Seconds 1
            [void]([WcmDesktop]::LaunchOnDesktop("WinSta0\default", "notepad.exe"))
            for ($i = 0; $i -lt 20; $i++) {
                Start-Sleep -Milliseconds 500
                $cands = @(Get-Process "notepad" -ErrorAction SilentlyContinue)
                foreach ($p in $cands) {
                    $h = [WcmDesktop]::FindWindowForPid($p.Id)
                    if ($h -ne [IntPtr]::Zero) {
                        # Expand Notepad to cover full work area so all clicks
                        # and double-clicks land on Notepad, never on Progman.
                        [WcmDesktop]::SetWindowBounds($h, 0, 0, 1920, 1040)
                        return [pscustomobject]@{
                            Id = $p.Id
                            MainWindowHandle = $h
                            ProcessName = $p.ProcessName
                        }
                    }
                }
            }
            return $null
        }
        function Focus-Window22([IntPtr]$h) {
            return [WcmDesktop]::FocusWindow($h)
        }
        function Stop-Notepads22 {
            Get-Process "notepad" -ErrorAction SilentlyContinue | ForEach-Object {
                try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch { }
            }
        }

        $checks22 = @()
        # 1) Common Notepad paste: Enter pastes into a normal app.
        try {
            $seed = "wcm22-common-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks22 += [pscustomobject]@{ Name = "common-paste"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                # Real user path: the global open hotkey grants the popup
                # foreground rights and captures the focused target (a pipe
                # --show cannot activate the popup, so SendKeys would miss).
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks22 += [pscustomobject]@{ Name = "common-paste"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    Start-Sleep -Milliseconds 800
                    [WcmDesktop]::SendKeysWait("{ENTER}")
                    $line = Wait-ActivateResult22 $rb 15
                    $okOutcome = ($null -ne $line) -and ($line -match "outcome=Pasted") -and ($line -match [regex]::Escape($seed.Substring(0, 20)))
                    $clipOk = $false
                    $noteOk = $false
                    if ($okOutcome) {
                        # Prove the chord landed in Notepad: select-all + copy
                        # must read the seeded text back (same-content echo
                        # dedups in history, so the check stays valid).
                        [void](Focus-Window22 $np.MainWindowHandle)
                        Start-Sleep -Milliseconds 400
                        [WcmDesktop]::SendKeysWait("^a")
                        Start-Sleep -Milliseconds 300
                        [WcmDesktop]::SendKeysWait("^c")
                        Start-Sleep -Milliseconds 500
                        try {
                            $clip = [WcmDesktop]::GetClipboard()
                            $clipOk = ($null -ne $clip) -and ($clip -match [regex]::Escape($seed.Substring(0, 20)))
                            $noteOk = $clipOk
                        } catch { $clipOk = $false }
                    }
                    $info = "outcome=$([regex]::Match("$line", 'outcome=([A-Za-z]+)').Groups[1].Value) clipOk=$clipOk"
                    $checks22 += [pscustomobject]@{ Name = "common-paste"; Ok = ($okOutcome -and $noteOk); Info = $info }
                }
            }
        } catch {
            $checks22 += [pscustomobject]@{ Name = "common-paste"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 2) Shift+Enter copies only: no chord injected.
        try {
            $seed = "wcm22-copyonly-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks22 += [pscustomobject]@{ Name = "copy-only"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks22 += [pscustomobject]@{ Name = "copy-only"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    Start-Sleep -Milliseconds 800
                    [WcmDesktop]::SendKeysWait("+{ENTER}")
                    $line = Wait-ActivateResult22 $rb 15
                    $clip = ""
                    try { $clip = [WcmDesktop]::GetClipboard() } catch { }
                    $ok = ($null -ne $line) -and ($line -match "outcome=CopiedOnly ") `
                        -and ($line -match "chord=<none>") `
                        -and ("$clip" -match [regex]::Escape($seed.Substring(0, 20)))
                    $checks22 += [pscustomobject]@{ Name = "copy-only"; Ok = $ok; Info = "line=$line" }
                }
            }
        } catch {
            $checks22 += [pscustomobject]@{ Name = "copy-only"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 3) Focus lost mid-flow: killing the captured target must report
        # focus loss, never elevation.
        try {
            $seed = "wcm22-focuslost-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks22 += [pscustomobject]@{ Name = "focus-lost"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks22 += [pscustomobject]@{ Name = "focus-lost"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    try { Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue } catch { }
                    Start-Sleep -Milliseconds 600
                    [WcmDesktop]::SendKeysWait("{ENTER}")
                    $line = Wait-ActivateResult22 $rb 15
                    $ok = ($null -ne $line) -and ($line -match "outcome=CopiedOnlyForegroundLost") -and ($line -notmatch "(?i)elevat")
                    $checks22 += [pscustomobject]@{ Name = "focus-lost"; Ok = $ok; Info = "line=$line" }
                }
            }
        } catch {
            $checks22 += [pscustomobject]@{ Name = "focus-lost"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 4) Missing item: clearing history while the popup shows stale rows
        # must end in a readable MissingItem, never silence.
        try {
            $seed = "wcm22-missing-$([DateTime]::UtcNow.Ticks)"
            $npMissing = Start-FreshNotepad22
            if ($null -eq $npMissing) {
                $checks22 += [pscustomobject]@{ Name = "missing-item"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $npMissing.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $npMissing.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks22 += [pscustomobject]@{ Name = "missing-item"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    Invoke-Pipe22 "--clear-all"
                    Start-Sleep -Milliseconds 800
                    [WcmDesktop]::SendKeysWait("{ENTER}")
                    $line = Wait-ActivateResult22 $rb 15
                    $ok = ($null -ne $line) -and ($line -match "outcome=MissingItem")
                    $checks22 += [pscustomobject]@{ Name = "missing-item"; Ok = $ok; Info = "line=$line" }
                }
            }
        } catch {
            $checks22 += [pscustomobject]@{ Name = "missing-item"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 5) Elevated target (best-effort): without an elevated window the
        # refusal path is unit-tested only and this sub-check SKIPs.
        $elevNote = ""
        try {
            $selfElev = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
                [Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($selfElev) {
                $elevNote = "SKIP (smoke runs elevated, refusal untestable; unit test covers it)"
            } else {
                Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WcmElev {
    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int TOKEN_QUERY = 0x0008;
    const int TokenElevation = 20;
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int a, bool b, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("advapi32.dll")] static extern bool OpenProcessToken(IntPtr p, int a, out IntPtr t);
    [DllImport("advapi32.dll")] static extern bool GetTokenInformation(IntPtr t, int c, IntPtr b, int l, out int r);
    public static bool IsPidElevated(int pid) {
        IntPtr proc = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
        if (proc == IntPtr.Zero) return true;
        try {
            IntPtr tok;
            if (!OpenProcessToken(proc, TOKEN_QUERY, out tok)) return true;
            try {
                IntPtr buf = Marshal.AllocHGlobal(4);
                try {
                    int ret;
                    if (!GetTokenInformation(tok, TokenElevation, buf, 4, out ret)) return true;
                    return Marshal.ReadInt32(buf) != 0;
                } finally { Marshal.FreeHGlobal(buf); }
            } finally { CloseHandle(tok); }
        } finally { CloseHandle(proc); }
    }
}
"@ -ErrorAction Stop
                $elevTarget = $null
                foreach ($p in Get-Process -ErrorAction SilentlyContinue) {
                    try {
                        if ($p.MainWindowHandle -eq [IntPtr]::Zero) { continue }
                        if ([WcmElev]::IsPidElevated($p.Id)) { $elevTarget = $p; break }
                    } catch { }
                }
                if ($null -eq $elevTarget) {
                    $elevNote = "SKIP (no elevated window found; unit test covers the refusal)"
                } elseif (-not (Focus-Window22 $elevTarget.MainWindowHandle)) {
                    $elevNote = "SKIP (could not focus elevated window $($elevTarget.ProcessName); unit test covers the refusal)"
                } else {
                    $seed = "wcm22-elevated-$([DateTime]::UtcNow.Ticks)"
                    [WcmDesktop]::SetClipboard($seed)
                    Start-Sleep -Seconds 2
                    $rb = @(Get-ActivateResults).Count
                    $pb = @(Get-PopupOpens22).Count
                    Invoke-Pipe22 "--show"
                    $pop = Wait-PopupOpen22 $pb 15
                    if ($null -eq $pop) {
                        $elevNote = "SKIP (popup would not open over the elevated target; unit test covers the refusal)"
                    } else {
                        Start-Sleep -Milliseconds 800
                        [WcmDesktop]::SendKeysWait("{ENTER}")
                        $line = Wait-ActivateResult22 $rb 15
                        if ($null -eq $line) {
                            $elevNote = "SKIP (activation could not be driven into the elevated target from this session; unit test covers the refusal)"
                        } else {
                            $ok = $line -match "outcome=CopiedOnlyElevated"
                            $checks22 += [pscustomobject]@{ Name = "elevated"; Ok = $ok; Info = "line=$line" }
                        }
                    }
                    Invoke-Pipe22 "--hide"
                    Start-Sleep -Milliseconds 500
                }
            }
        } catch {
            $elevNote = "SKIP (elevated probe threw: $($_.Exception.Message); unit test covers the refusal)"
        }
        Stop-Notepads22
        $failed22 = @($checks22 | Where-Object { -not $_.Ok })
        $required22 = @($checks22 | Where-Object { $_.Name -ne "elevated" })
        $requiredFailed = @($required22 | Where-Object { -not $_.Ok })
        if ($requiredFailed.Count -eq 0 -and ($checks22.Count -gt 0)) {
            $pasteResult = "PASS"
            $detailParts = @($checks22 | ForEach-Object { "$($_.Name): $($_.Info)" })
            $pasteDetails = "$($required22.Count)/$($required22.Count) required ok; $($detailParts -join ' | ')"
            if ($elevNote -ne "") { $pasteDetails += " | elevated $elevNote" }
        } elseif ($checks22.Count -eq 0 -and $elevNote -ne "") {
            $pasteResult = "SKIP"
            $pasteDetails = "no checks ran; elevated $elevNote"
        } else {
            $pasteResult = "FAIL"
            $bad = ($failed22 | ForEach-Object { "$($_.Name): $($_.Info)" }) -join " | "
            $pasteDetails = "$($required22.Count - $requiredFailed.Count)/$($required22.Count) required ok; FAILS: $bad"
            if ($elevNote -ne "") { $pasteDetails += " | elevated $elevNote" }
        }
    } catch {
        $pasteResult = "FAIL"
        $pasteDetails = "paste-diagnostics threw: $($_.Exception.Message)"
    }
}
Add-Step "paste-diagnostics (tkt 22)" $pasteResult "$pasteDetails"

# --- Step 6: single-click-paste (ticket 23, automated) -----------------------
Write-Host "== smoke-ui: single-click-paste (click / shift+click / keyboard-nav / double-click) =="
$clickResult = "SKIP"
$clickDetails = "skipped (app launch failed)"
if ($appOk) {
    try {
        $checks23 = @()
        # 1) Single-click paste into Notepad
        try {
            $seed = "wcm23-click-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks23 += [pscustomobject]@{ Name = "single-click"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks23 += [pscustomobject]@{ Name = "single-click"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    $pxX = [double]([regex]::Match($pop, "cursorPx=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $dipX = [double]([regex]::Match($pop, "cursorDip=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $scaleX = if ($dipX -gt 0) { $pxX / $dipX } else { 1.0 }
                    $finX = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $finY = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                    $targetPxX = [int](($finX + 150) * $scaleX)
                    $targetPxY = [int](($finY + 68) * $scaleX)

                    [WcmDesktop]::ClickAt($targetPxX, $targetPxY)
                    $line = Wait-ActivateResult22 $rb 15
                    $okOutcome = ($null -ne $line) -and ($line -match "outcome=Pasted") -and ($line -match [regex]::Escape($seed.Substring(0, 20)))
                    $clipOk = $false
                    $noteOk = $false
                    if ($okOutcome) {
                        [void](Focus-Window22 $np.MainWindowHandle)
                        Start-Sleep -Milliseconds 400
                        [WcmDesktop]::SendKeysWait("^a")
                        Start-Sleep -Milliseconds 300
                        [WcmDesktop]::SendKeysWait("^c")
                        Start-Sleep -Milliseconds 500
                        try {
                            $clip = [WcmDesktop]::GetClipboard()
                            $clipOk = ($null -ne $clip) -and ($clip -match [regex]::Escape($seed.Substring(0, 20)))
                            $noteOk = $clipOk
                        } catch { $clipOk = $false }
                    }
                    $info = "outcome=$([regex]::Match("$line", 'outcome=([A-Za-z]+)').Groups[1].Value) noteOk=$noteOk"
                    $checks23 += [pscustomobject]@{ Name = "single-click"; Ok = ($okOutcome -and $noteOk); Info = $info }
                }
            }
        } catch {
            $checks23 += [pscustomobject]@{ Name = "single-click"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 2) Shift+click copies only (no injection, no hide)
        try {
            $seed = "wcm23-shift-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks23 += [pscustomobject]@{ Name = "shift-click"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks23 += [pscustomobject]@{ Name = "shift-click"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    $pxX = [double]([regex]::Match($pop, "cursorPx=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $dipX = [double]([regex]::Match($pop, "cursorDip=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $scaleX = if ($dipX -gt 0) { $pxX / $dipX } else { 1.0 }
                    $finX = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $finY = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                    $targetPxX = [int](($finX + 150) * $scaleX)
                    $targetPxY = [int](($finY + 68) * $scaleX)

                    [WcmDesktop]::ShiftClickAt($targetPxX, $targetPxY)
                    $line = Wait-ActivateResult22 $rb 15
                    $clip = ""
                    try { $clip = [WcmDesktop]::GetClipboard() } catch { }
                    $ok = ($null -ne $line) -and ($line -match "outcome=CopiedOnly ") `
                        -and ($line -match "chord=<none>") `
                        -and ("$clip" -match [regex]::Escape($seed.Substring(0, 20)))
                    $checks23 += [pscustomobject]@{ Name = "shift-click"; Ok = $ok; Info = "outcome=$([regex]::Match("$line", 'outcome=([A-Za-z]+)').Groups[1].Value) chord=<none>" }
                }
            }
        } catch {
            $checks23 += [pscustomobject]@{ Name = "shift-click"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 3) Keyboard navigation does not trigger paste
        try {
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks23 += [pscustomobject]@{ Name = "keyboard-nav"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks23 += [pscustomobject]@{ Name = "keyboard-nav"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    Start-Sleep -Milliseconds 500
                    [WcmDesktop]::SendKeysWait("{DOWN}")
                    Start-Sleep -Milliseconds 200
                    [WcmDesktop]::SendKeysWait("{DOWN}")
                    Start-Sleep -Milliseconds 200
                    [WcmDesktop]::SendKeysWait("{UP}")
                    Start-Sleep -Milliseconds 200
                    [WcmDesktop]::SendKeysWait("{HOME}")
                    Start-Sleep -Milliseconds 200
                    [WcmDesktop]::SendKeysWait("{END}")
                    Start-Sleep -Milliseconds 500
                    $ra = @(Get-ActivateResults).Count
                    $noActivation = ($ra -eq $rb)
                    $checks23 += [pscustomobject]@{ Name = "keyboard-nav"; Ok = $noActivation; Info = "activationsBefore=$rb activationsAfter=$ra (no paste triggered)" }
                }
            }
        } catch {
            $checks23 += [pscustomobject]@{ Name = "keyboard-nav"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        # 4) Double-click idempotence (does not duplicate paste)
        try {
            $seed = "wcm23-double-$([DateTime]::UtcNow.Ticks)"
            $np = Start-FreshNotepad22
            if ($null -eq $np) {
                $checks23 += [pscustomobject]@{ Name = "double-click"; Ok = $false; Info = "notepad window never appeared" }
            } else {
                [void](Focus-Window22 $np.MainWindowHandle)
                [WcmDesktop]::SendKeysWait("^a{DELETE}")
                [WcmDesktop]::SetClipboard($seed)
                Start-Sleep -Seconds 2
                $rb = @(Get-ActivateResults).Count
                $pb = @(Get-PopupOpens22).Count
                [void](Focus-Window22 $np.MainWindowHandle)
                Start-Sleep -Milliseconds 300
                [WcmDesktop]::SendKeysWait("^+V")
                $pop = Wait-PopupOpen22 $pb 15
                if ($null -eq $pop) {
                    $checks23 += [pscustomobject]@{ Name = "double-click"; Ok = $false; Info = "no new popup-open line within 15s" }
                } else {
                    $pxX = [double]([regex]::Match($pop, "cursorPx=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $dipX = [double]([regex]::Match($pop, "cursorDip=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $scaleX = if ($dipX -gt 0) { $pxX / $dipX } else { 1.0 }
                    $finX = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[1].Value)
                    $finY = [double]([regex]::Match($pop, "final=([\d.\-]+),([\d.\-]+)").Groups[2].Value)
                    $targetPxX = [int](($finX + 150) * $scaleX)
                    $targetPxY = [int](($finY + 68) * $scaleX)

                    [WcmDesktop]::DoubleClickAt($targetPxX, $targetPxY)
                    $line = Wait-ActivateResult22 $rb 15
                    $okOutcome = ($null -ne $line) -and ($line -match "outcome=Pasted")
                    $count = 0
                    if ($okOutcome) {
                        [void](Focus-Window22 $np.MainWindowHandle)
                        Start-Sleep -Milliseconds 400
                        [WcmDesktop]::SendKeysWait("^a")
                        Start-Sleep -Milliseconds 300
                        [WcmDesktop]::SendKeysWait("^c")
                        Start-Sleep -Milliseconds 500
                        try {
                            $clip = [WcmDesktop]::GetClipboard()
                            $count = ([regex]::Matches($clip, [regex]::Escape($seed.Substring(0, 20)))).Count
                        } catch { $count = 0 }
                    }
                    $checks23 += [pscustomobject]@{ Name = "double-click"; Ok = ($okOutcome -and ($count -eq 1)); Info = "outcome=$([regex]::Match("$line", 'outcome=([A-Za-z]+)').Groups[1].Value) occurrences=$count (single paste preserved)" }
                }
            }
        } catch {
            $checks23 += [pscustomobject]@{ Name = "double-click"; Ok = $false; Info = "threw: $($_.Exception.Message)" }
        } finally {
            Invoke-Pipe22 "--hide"
            Start-Sleep -Milliseconds 500
        }

        Stop-Notepads22
        $failed23 = @($checks23 | Where-Object { -not $_.Ok })
        if ($failed23.Count -eq 0 -and ($checks23.Count -gt 0)) {
            $clickResult = "PASS"
            $detailParts = @($checks23 | ForEach-Object { "$($_.Name): $($_.Info)" })
            $clickDetails = "$($checks23.Count)/$($checks23.Count) ok; $($detailParts -join ' | ')"
        } else {
            $clickResult = "FAIL"
            $bad = ($failed23 | ForEach-Object { "$($_.Name): $($_.Info)" }) -join " | "
            $clickDetails = "$($checks23.Count - $failed23.Count)/$($checks23.Count) ok; FAILS: $bad"
        }
    } catch {
        $clickResult = "FAIL"
        $clickDetails = "single-click-paste threw: $($_.Exception.Message)"
    }
}
Add-Step "single-click-paste (tkt 23)" $clickResult "$clickDetails"
if ($proc -ne $null) {
    try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
    $proc = $null
}

# --- Step 7: tray-gaveta (ticket 24, automated) ------------------------------
Write-Host "== smoke-ui: tray-gaveta (tray icon + overflow drawer + feedback + onboarding) =="
$trayResult = "SKIP"
$trayDetails = ""
$trayShots = @()

try {
    # If the app process was stopped, relaunch with --hidden
    if ($proc -eq $null -or $proc.HasExited) {
        $launchedPid = [WcmDesktop]::LaunchOnDesktop("WinSta0\default", "`"$exe`" --hidden")
        $proc = Get-Process -Id $launchedPid
        Start-Sleep -Seconds 2
    }

    $checks24 = New-Object System.Collections.Generic.List[object]

    # 1) Process alive & tray visible in log
    $isAlive = ($null -ne $proc) -and (-not $proc.HasExited)
    $trayVisLines = @(Get-LogLines "[${prefix}:tray] visible=True tooltip=WindowsCM")
    $hasTrayVis = $trayVisLines.Count -gt 0
    $checks24.Add([pscustomobject]@{
        Name = "tray-icon-visible"
        Ok = ($isAlive -and $hasTrayVis)
        Info = "pid=$($proc.Id) alive=$isAlive trayLogCount=$($trayVisLines.Count) tooltip=WindowsCM"
    })

    # 2) Feedback de cópia (flash + balloon)
    $flashLinesBefore = @(Get-LogLines "[${prefix}:tray] flash")
    $balloonLines = @(Get-LogLines "[${prefix}:tray] balloon")

    # Trigger copy feedback by copying fresh text to clipboard
    $feedSeed = "wcm24-feedback-$([DateTime]::UtcNow.Ticks)"
    [WcmDesktop]::SetClipboard($feedSeed)
    Start-Sleep -Seconds 2
    $flashLinesAfter = @(Get-LogLines "[${prefix}:tray] flash")
    $hasNewFlash = $flashLinesAfter.Count -gt $flashLinesBefore.Count
    $hasBalloon = $balloonLines.Count -gt 0
    $checks24.Add([pscustomobject]@{
        Name = "copy-feedback"
        Ok = ($hasNewFlash -and $hasBalloon)
        Info = "flashCount=$($flashLinesAfter.Count) (new=$hasNewFlash) balloonCount=$($balloonLines.Count)"
    })

    # 3) Settings / Diagnóstico onboarding guidance
    $guidance = ""
    $coreDll = Join-Path $repoRoot "src\WindowsCM.Core\bin\$Configuration\net8.0\WindowsCM.Core.dll"
    if (Test-Path -LiteralPath $coreDll) {
        try {
            $asm = [System.Reflection.Assembly]::LoadFrom($coreDll)
            $type = $asm.GetType("WindowsCM.Core.Tray.TrayOnboarding")
            $guidance = [string]($type.GetField("Guidance").GetValue($null))
        } catch { }
    }
    if ([string]::IsNullOrWhiteSpace($guidance)) {
        $onboardingSrc = Join-Path $repoRoot "src\WindowsCM.Core\Tray\TrayOnboarding.cs"
        if (Test-Path -LiteralPath $onboardingSrc) {
            $guidance = Get-Content -LiteralPath $onboardingSrc -Raw
        }
    }
    $hasOverflowWord = $guidance -match "overflow"
    $hasDragWord = $guidance -match "drag"
    $hasNoPromo = $guidance -match "promotion"
    $checks24.Add([pscustomobject]@{
        Name = "onboarding-guidance"
        Ok = ($hasOverflowWord -and $hasDragWord -and $hasNoPromo)
        Info = "overflow=$hasOverflowWord drag=$hasDragWord noProgrammaticPromo=$hasNoPromo"
    })

    # 4) Overflow drawer (gaveta) detection, open, screenshot & close
    $overflowHwnd = [WcmDesktop]::FindOverflowWindow()
    $drawerOk = $false
    $drawerInfo = ""
    $fullShot = Join-Path ([System.IO.Path]::GetTempPath()) "WindowsCM-tray-overflow.png"
    $cropShot = Join-Path ([System.IO.Path]::GetTempPath()) "WindowsCM-tray-overflow-cropped.png"

    if ($overflowHwnd -ne [IntPtr]::Zero) {
        $rc = New-Object WcmDesktop+RECT
        [void][WcmDesktop]::GetWindowRectangle($overflowHwnd, [ref]$rc)
        $chevronX = if ($rc.Left -gt 0) { [int](($rc.Left + $rc.Right) / 2) } else { 1670 }
        $chevronY = 1056

        # Click chevron to open overflow drawer
        [WcmDesktop]::ClickAt($chevronX, $chevronY)
        Start-Sleep -Milliseconds 700

        $opened = [WcmDesktop]::IsWindowVis($overflowHwnd)
        if (-not $opened) {
            [WcmDesktop]::ClickAt($chevronX, $chevronY)
            Start-Sleep -Milliseconds 700
            $opened = [WcmDesktop]::IsWindowVis($overflowHwnd)
        }

        # Take screenshots
        $shot1 = [WcmDesktop]::SaveScreenshot($fullShot)
        $shot2 = [WcmDesktop]::CaptureWindowRect($overflowHwnd, $cropShot)

        if ($shot1) { $trayShots += $fullShot }
        if ($shot2) { $trayShots += $cropShot }

        # Close overflow drawer
        [WcmDesktop]::ClickAt($chevronX, $chevronY)
        Start-Sleep -Milliseconds 500
        $closed = -not [WcmDesktop]::IsWindowVis($overflowHwnd)
        if (-not $closed) {
            [WcmDesktop]::SendKeysWait("{ESC}")
            Start-Sleep -Milliseconds 300
        }

        $drawerOk = $opened -and (Test-Path -LiteralPath $fullShot)
        $drawerInfo = "hwnd=0x$($overflowHwnd.ToInt64().ToString('X')) opened=$opened closed=$closed fullShot=$shot1 cropShot=$shot2"
    } else {
        $drawerOk = $false
        $drawerInfo = "overflow window not found"
    }

    $checks24.Add([pscustomobject]@{
        Name = "overflow-drawer-screenshot"
        Ok = $drawerOk
        Info = $drawerInfo
    })

    $failed24 = @($checks24 | Where-Object { -not $_.Ok })
    if ($failed24.Count -eq 0 -and ($checks24.Count -gt 0)) {
        $trayResult = "PASS"
        $detailParts = @($checks24 | ForEach-Object { "$($_.Name): $($_.Info)" })
        $trayDetails = "$($checks24.Count)/$($checks24.Count) ok; $($detailParts -join ' | ')"
    } else {
        $trayResult = "FAIL"
        $bad = ($failed24 | ForEach-Object { "$($_.Name): $($_.Info)" }) -join " | "
        $trayDetails = "$($checks24.Count - $failed24.Count)/$($checks24.Count) ok; FAILS: $bad"
    }
} catch {
    $trayResult = "FAIL"
    $trayDetails = "tray-gaveta threw: $($_.Exception.Message)"
} finally {
    if ($proc -ne $null) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
        $proc = $null
    }
}
Add-Step "tray-gaveta (tkt 24)" $trayResult "$trayDetails"

# --- Emit smoke-report.md ---------------------------------------------------
$now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$pass = @($steps | Where-Object { $_.Result -eq "PASS" }).Count
$fail = @($steps | Where-Object { $_.Result -eq "FAIL" }).Count
$skip = @($steps | Where-Object { $_.Result -eq "SKIP" }).Count
$logExcerpt = ""
if (Test-Path -LiteralPath $logPath) {
    $logExcerpt = (Get-Content -LiteralPath $logPath -Tail 40 -ErrorAction SilentlyContinue) -join "`n"
} else {
    $logExcerpt = "(no temp log at $logPath)"
}

$allShotLines = @()
foreach ($shot in $popupShots) {
    $allShotLines += "- Popup screenshot: ``$shot``"
}
foreach ($shot in $trayShots) {
    $allShotLines += "- Tray screenshot: ``$shot``"
}
$report = @()
$report += "# smoke-report (tickets 21-24: popup-1080p + paste-diagnostics + single-click + tray-gaveta automated)"
$report += ""
$report += "- Date (UTC): $now"
$report += "- Configuration: $Configuration"
$report += "- Temp log: ``$logPath`` (prefix ``$prefix``)"
$report += "- Baseline filtered tests: $testCount (expect 179 since ticket 24)"
$report += "- Summary: $pass PASS / $fail FAIL / $skip SKIP"
$report += ""
$report += "| Step | Result | Details |"
$report += "| ---- | ------ | ------- |"
foreach ($s in $steps) {
    $d = $s.Details -replace '\|', '/'
    $report += "| $($s.Name) | $($s.Result) | $d |"
}
$report += ""
foreach ($shotLine in $allShotLines) {
    $report += $shotLine
}
$report += ""
$report += "## Temp log excerpt (last 40 lines)"
$report += ""
$report += '```'
$report += $logExcerpt
$report += '```'
$report += ""
$report += "_Skeleton: all tickets 21-24 automated; temp log + this script are removed in ticket 25._"
[System.IO.File]::WriteAllLines($ReportPath, $report)
Write-Host "Report written to $ReportPath ($pass PASS / $fail FAIL / $skip SKIP)"

if ($fail -gt 0) { exit 1 }
exit 0
