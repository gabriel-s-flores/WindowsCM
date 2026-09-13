# SPDX-License-Identifier: GPL-3.0-or-later
# Compila e empacota a versao portatil e o instalador executavel na pasta dist/

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot
Write-Host "==> Iniciando compilacao e empacotamento do WindowsCM ($Configuration - $Runtime)..." -ForegroundColor Cyan

# Fechar qualquer processo WindowsCM em execucao para liberar os binarios
Get-Process -Name WindowsCM -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 1. Localizar Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

$isccPath = $isccCandidates | Select-Object -First 1
if (-not $isccPath) {
    throw "Inno Setup Compiler (ISCC.exe) nao foi encontrado. Instale o Inno Setup 6 ou adicione ao PATH."
}
Write-Host "==> Inno Setup Compiler encontrado em: $isccPath" -ForegroundColor Gray

# 2. Garantir diretorio dist
$DistDir = Join-Path $RepoRoot "dist"
$PortableDir = Join-Path $DistDir "WindowsCM-portable"
$InstallerPublishDir = Join-Path $RepoRoot "installer\publish"

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

# 3. Publicar versao portatil
Write-Host "==> Publicando versao portatil em $PortableDir..." -ForegroundColor Cyan
if (Test-Path $PortableDir) {
    Remove-Item $PortableDir -Recurse -Force
}
dotnet publish src/WindowsCM.App/WindowsCM.App.csproj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $PortableDir

# Copiar LICENSE para a pasta portatil
Copy-Item (Join-Path $RepoRoot "LICENSE") (Join-Path $PortableDir "LICENSE") -Force

# Criar arquivo .zip do portatil
$ZipPath = Join-Path $DistDir "WindowsCM-portable.zip"
Write-Host "==> Compactando pacote portatil em $ZipPath..." -ForegroundColor Cyan
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
        Write-Warning "Tentativa $attempt falhou ao compactar ($($_.Exception.Message)). Aguardando liberacao do arquivo..."
    }
}
if (-not $zipped) {
    throw "Falha ao compactar $ZipPath apos 5 tentativas."
}

# 4. Publicar staging para o instalador e compilar Inno Setup
Write-Host "==> Publicando arquivos para staging do instalador em $InstallerPublishDir..." -ForegroundColor Cyan
if (Test-Path $InstallerPublishDir) {
    Remove-Item $InstallerPublishDir -Recurse -Force
}
dotnet publish src/WindowsCM.App/WindowsCM.App.csproj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true -o $InstallerPublishDir

$IssFile = Join-Path $RepoRoot "installer\WindowsCM.iss"
Write-Host "==> Compilando instalador Inno Setup para $DistDir..." -ForegroundColor Cyan
& $isccPath /O"$DistDir" $IssFile

# 5. Resumo final
Write-Host "`n==> Compilacao concluida com sucesso! Artefatos disponiveis em dist/:" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
