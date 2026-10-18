[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'Build Windows native dependencies on Windows x64.' }
$repository = Split-Path $PSScriptRoot -Parent
$manifest = Join-Path $PSScriptRoot 'windows-native'
$baseline = (Get-Content -LiteralPath (Join-Path $manifest 'vcpkg.json') -Raw | ConvertFrom-Json).'builtin-baseline'
$checkout = Join-Path $repository 'artifacts/tools/vcpkg'
if (-not (Test-Path -LiteralPath (Join-Path $checkout '.git'))) {
    & git clone --filter=blob:none https://github.com/microsoft/vcpkg.git $checkout
    if ($LASTEXITCODE -ne 0) { throw 'Unable to clone vcpkg.' }
}
& git -C $checkout checkout --detach $baseline
if ($LASTEXITCODE -ne 0) { throw 'Unable to select the pinned vcpkg revision.' }
& (Join-Path $checkout 'bootstrap-vcpkg.bat') -disableMetrics
if ($LASTEXITCODE -ne 0) { throw 'Unable to bootstrap vcpkg.' }
$installed = Join-Path $repository 'artifacts/windows-native'
& (Join-Path $checkout 'vcpkg.exe') install "--x-manifest-root=$manifest" "--x-install-root=$installed" "--overlay-triplets=$(Join-Path $manifest 'triplets')" '--triplet=x64-algetric-windows'
if ($LASTEXITCODE -ne 0) { throw 'Unable to build PROJ. Install Visual Studio C++ build tools and CMake first.' }
Write-Output (Join-Path $installed 'x64-algetric-windows')
