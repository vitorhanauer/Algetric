[CmdletBinding()]
param([string]$ProjRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitOperatingSystem) { throw 'Publish on Windows x64.' }
$repository = Split-Path $PSScriptRoot -Parent
Push-Location $repository
try {
    if (-not $ProjRoot) {
        & (Join-Path $PSScriptRoot 'prepare-windows-native.ps1') | Out-Host
        $ProjRoot = Join-Path $repository 'artifacts/windows-native/x64-algetric-windows'
    }
    $env:AlgetricProjRoot = (Resolve-Path -LiteralPath $ProjRoot).Path
    & npm.cmd ci --ignore-scripts --prefix src/Algetric.Desktop/Web
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    & npm.cmd run build --prefix src/Algetric.Desktop/Web
    if ($LASTEXITCODE -ne 0) { throw 'Cesium asset preparation failed.' }
    & npm.cmd test --prefix src/Algetric.Desktop/Web
    if ($LASTEXITCODE -ne 0) { throw 'JavaScript tests failed.' }
    & dotnet restore Algetric.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    & dotnet test tests/Algetric.Tests --no-restore -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw '.NET tests failed.' }
    & dotnet publish src/Algetric.Desktop -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    & (Join-Path $PSScriptRoot 'package-windows.ps1') -ProjRoot $env:AlgetricProjRoot
} finally { Pop-Location }
