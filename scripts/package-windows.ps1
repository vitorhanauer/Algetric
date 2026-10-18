[CmdletBinding()]
param(
    [string]$PackageDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/win-x64'),
    [string]$ProjRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/windows-native/x64-algetric-windows'),
    [string]$OutputArchive = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/algetric-win-x64.zip')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$projLibraries = @(Get-ChildItem -LiteralPath (Join-Path $ProjRoot 'bin') -Filter 'proj*.dll' -File)
if ($projLibraries.Count -ne 1) { throw 'Expected exactly one PROJ release DLL in the native build.' }
foreach ($file in @('Algetric.Desktop.exe', 'Web/vendor/Cesium/Cesium.js')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageDirectory $file))) { throw "Incomplete Windows publish: $file" }
}
if (-not (Test-Path -LiteralPath (Join-Path $ProjRoot 'share/proj/proj.db'))) { throw 'The PROJ database is missing.' }
Get-ChildItem -LiteralPath (Join-Path $ProjRoot 'bin') -Filter '*.dll' -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $PackageDirectory -Force
}
New-Item -ItemType Directory -Path (Join-Path $PackageDirectory 'proj-data') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $ProjRoot 'share/proj/proj.db') -Destination (Join-Path $PackageDirectory 'proj-data') -Force
$licenses = Join-Path $PackageDirectory 'native-licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $ProjRoot 'share') -Directory | ForEach-Object {
    $copyright = Join-Path $_.FullName 'copyright'
    if (Test-Path -LiteralPath $copyright) { Copy-Item -LiteralPath $copyright -Destination (Join-Path $licenses ($_.Name + '.txt')) -Force }
}
foreach ($script in @('install', 'uninstall')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$script-windows.ps1") -Destination (Join-Path $PackageDirectory "$script.ps1") -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$script-windows.cmd") -Destination (Join-Path $PackageDirectory "$script.cmd") -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'windows-common.ps1') -Destination $PackageDirectory -Force
foreach ($file in @('README.md', 'THIRD_PARTY_NOTICES.md', 'docs')) {
    Copy-Item -LiteralPath (Join-Path $repository $file) -Destination $PackageDirectory -Recurse -Force
}
$zip = $OutputArchive
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
Compress-Archive -Path (Join-Path $PackageDirectory '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $([IO.Path]::GetFileName($zip))`n")
Write-Output "Windows package: $zip"
