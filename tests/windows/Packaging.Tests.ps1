$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$scripts = @(Get-ChildItem -LiteralPath (Join-Path $repository 'scripts') -Filter '*.ps1') + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1')
$scripts | ForEach-Object {
    $tokens = $null; $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    Assert-True ($errors.Count -eq 0) "Invalid PowerShell syntax: $($_.Name): $errors"
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('algetric-windows-package-' + [Guid]::NewGuid().ToString('N'))
try {
    $package = Join-Path $temporary 'package with spaces'
    $native = Join-Path $temporary 'native'
    foreach ($path in @("$package/Web/vendor/Cesium", "$native/bin", "$native/share/proj", "$native/share/sqlite3")) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
    foreach ($file in @("$package/Algetric.Desktop.exe", "$package/Web/vendor/Cesium/Cesium.js", "$native/bin/proj_9_8.dll", "$native/bin/sqlite3.dll", "$native/share/proj/proj.db", "$native/share/proj/copyright", "$native/share/sqlite3/copyright")) {
        [IO.File]::WriteAllText($file, 'test fixture')
    }
    $zip = Join-Path $temporary 'test.zip'
    & (Join-Path $repository 'scripts/package-windows.ps1') -PackageDirectory $package -ProjRoot $native -OutputArchive $zip
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($file in @('Algetric.Desktop.exe', 'proj_9_8.dll', 'sqlite3.dll', 'proj-data/proj.db', 'native-licenses/proj.txt', 'install.cmd', 'install.ps1', 'uninstall.cmd', 'uninstall.ps1', 'windows-common.ps1', 'README.md', 'docs/verification.md')) {
            Assert-True ($null -ne $archive.GetEntry($file)) "Missing packaged file: $file"
        }
    } finally { $archive.Dispose() }
    $expected = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ((Get-Content -LiteralPath "$zip.sha256" -Raw).StartsWith($expected)) 'Invalid ZIP checksum.'
    Remove-Item -LiteralPath (Join-Path $native 'share/proj/proj.db')
    $failed = $false
    try { & (Join-Path $repository 'scripts/package-windows.ps1') -PackageDirectory $package -ProjRoot $native -OutputArchive $zip }
    catch { $failed = $true }
    Assert-True $failed 'Packaging must reject a missing PROJ database.'
    Assert-True ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected) 'A failed package must preserve the existing archive.'
    Write-Host 'PASS: PowerShell syntax, Windows package contents, native licenses, checksum, and missing-database rejection.'
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
