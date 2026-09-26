# SPDX-License-Identifier: GPL-3.0-or-later
# Builds and packages the portable version and the installer executable into dist/

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    # Release version (e.g. 1.0.3). Empty = the csproj/.iss version (local builds).
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# $ErrorActionPreference does not cover native commands: check the exit code.
function Assert-LastExit([string]$Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed (exit code $LASTEXITCODE)."
    }
}

$PublishVersionArgs = @()
$IsccVersionArgs = @()
if ($Version) {
    $PublishVersionArgs = @("-p:Version=$Version")
    $IsccVersionArgs = @("/DAppVersion=$Version")
}
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot
Write-Host "==> Building and packaging WindowsCM ($Configuration - $Runtime)..." -ForegroundColor Cyan

# Close any running WindowsCM process to release the binaries
Get-Process -Name WindowsCM -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 1. Locate the Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

$isccPath = $isccCandidates | Select-Object -First 1
if (-not $isccPath) {
    throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup 6 or add it to PATH."
}
Write-Host "==> Inno Setup Compiler found at: $isccPath" -ForegroundColor Gray

# 2. Ensure the dist directory exists
$DistDir = Join-Path $RepoRoot "dist"
$PortableDir = Join-Path $DistDir "WindowsCM-portable"
$InstallerPublishDir = Join-Path $RepoRoot "installer\publish"

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

# 3. Publish the portable version
Write-Host "==> Publishing the portable version to $PortableDir..." -ForegroundColor Cyan
if (Test-Path $PortableDir) {
    Remove-Item $PortableDir -Recurse -Force
}
dotnet publish src/WindowsCM.App/WindowsCM.App.csproj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $PortableDir @PublishVersionArgs
Assert-LastExit "dotnet publish (portable)"

# Copy LICENSE into the portable folder
Copy-Item (Join-Path $RepoRoot "LICENSE") (Join-Path $PortableDir "LICENSE") -Force

# Create the portable .zip
$ZipPath = Join-Path $DistDir "WindowsCM-portable.zip"
Write-Host "==> Zipping the portable package to $ZipPath..." -ForegroundColor Cyan
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

$zipped = $false
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        Start-Sleep -Milliseconds (600 * $attempt)
        Compress-Archive -Path "$PortableDir\*" -DestinationPath $ZipPath -Force
        $zipped = $true
        break
    } catch {
        Write-Warning "Attempt $attempt to zip failed ($($_.Exception.Message)). Waiting for the file to be released..."
    }
}
if (-not $zipped) {
    throw "Failed to zip $ZipPath after 5 attempts."
}

# 4. Publish the installer staging files and compile the Inno Setup installer
Write-Host "==> Publishing the installer staging files to $InstallerPublishDir..." -ForegroundColor Cyan
if (Test-Path $InstallerPublishDir) {
    Remove-Item $InstallerPublishDir -Recurse -Force
}
dotnet publish src/WindowsCM.App/WindowsCM.App.csproj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $InstallerPublishDir @PublishVersionArgs
Assert-LastExit "dotnet publish (installer)"

# The installer copies only WindowsCM.exe; a stray DLL in the staging folder = the installed app does not start
$strayDlls = Get-ChildItem -Path $InstallerPublishDir -Filter *.dll
if ($strayDlls) {
    throw "Native DLLs outside the single-file exe ($($strayDlls.Name -join ', ')). Check IncludeNativeLibrariesForSelfExtract in the csproj."
}

$IssFile = Join-Path $RepoRoot "installer\WindowsCM.iss"
Write-Host "==> Compiling the Inno Setup installer into $DistDir..." -ForegroundColor Cyan
& $isccPath /O"$DistDir" @IsccVersionArgs $IssFile
Assert-LastExit "Inno Setup (ISCC)"

# 5. Final summary
Write-Host "`n==> Build completed successfully! Artifacts available in dist/:" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
