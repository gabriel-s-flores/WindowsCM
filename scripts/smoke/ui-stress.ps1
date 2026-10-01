# SPDX-License-Identifier: GPL-3.0-or-later
param(
    [int]$Cards = 2000,
    [string]$OutDir = "TestResults/stress/ui"
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $PSScriptRoot 'WindowsCM.UiStress/WindowsCM.UiStress.csproj'
Set-Location $repoRoot
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
dotnet build $project -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'UI stress harness build failed.' }
$exe = Join-Path $PSScriptRoot 'WindowsCM.UiStress/bin/Release/net8.0-windows/WindowsCM.UiStress.exe'
foreach ($scenario in @('remote-image', 'remote-link', 'image-shapes', 'cards', 'workflow')) {
    Write-Host "==> UI stress: $scenario"
    & $exe $scenario $Cards | Tee-Object -FilePath (Join-Path $OutDir "$scenario.txt")
    if ($LASTEXITCODE -ne 0) { throw "UI stress failed: $scenario (exit code $LASTEXITCODE)." }
}
Write-Host "All isolated UI stress scenarios passed. Reports: $OutDir"
