[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs/Algetric'),
    [switch]$DesktopShortcut
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitOperatingSystem) { throw 'Algetric requires Windows x64.' }
. (Join-Path $PSScriptRoot 'windows-common.ps1')
$package = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $package 'Algetric.Desktop.exe'))) {
    $package = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/win-x64'
    if (-not (Test-Path -LiteralPath (Join-Path $package 'install.ps1'))) {
        & (Join-Path $PSScriptRoot 'publish-windows.ps1')
    }
}
foreach ($file in @('Algetric.Desktop.exe', 'Web/vendor/Cesium/Cesium.js', 'proj-data/proj.db', 'uninstall.ps1', 'windows-common.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $file))) { throw "Incomplete application package: $file" }
}
if (@(Get-ChildItem -LiteralPath $package -Filter 'proj*.dll' -File).Count -ne 1) { throw 'The package must contain one PROJ DLL.' }
if (-not (Test-AlgetricWebView2)) {
    throw 'Install Microsoft Edge WebView2 Runtime first: https://developer.microsoft.com/microsoft-edge/webview2/#download-section . Then rerun install.cmd.'
}
if (Get-Process -Name 'Algetric.Desktop' -ErrorAction SilentlyContinue) { throw 'Close Algetric before installing or updating.' }
$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
if ((Test-Path -LiteralPath $InstallDirectory) -and -not (Test-AlgetricManaged $InstallDirectory)) {
    throw "Refusing to replace an unmanaged directory: $InstallDirectory"
}
$parent = Split-Path $InstallDirectory -Parent
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$stage = Join-Path $parent ('.algetric-stage-' + [Guid]::NewGuid().ToString('N'))
$backup = $InstallDirectory + '.previous-' + [Guid]::NewGuid().ToString('N')
try {
    New-Item -ItemType Directory -Path $stage | Out-Null
    Get-ChildItem -LiteralPath $package -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse -Force }
    Set-Content -LiteralPath (Join-Path $stage '.algetric-install') -Value 'algetric-user-install-v1' -Encoding ASCII
    if (Test-Path -LiteralPath $InstallDirectory) { Move-Item -LiteralPath $InstallDirectory -Destination $backup }
    Move-Item -LiteralPath $stage -Destination $InstallDirectory
    $executable = Join-Path $InstallDirectory 'Algetric.Desktop.exe'
    $shell = New-Object -ComObject WScript.Shell
    $links = @((Get-AlgetricShortcut))
    if ($DesktopShortcut) { $links += Join-Path ([Environment]::GetFolderPath('Desktop', 'DoNotVerify')) 'Algetric.lnk' }
    foreach ($link in $links) {
        New-Item -ItemType Directory -Path (Split-Path $link -Parent) -Force | Out-Null
        $shortcut = $shell.CreateShortcut($link)
        $shortcut.TargetPath = $executable
        $shortcut.WorkingDirectory = $InstallDirectory
        $shortcut.IconLocation = "$executable,0"
        $shortcut.Description = 'Edit geometries on a Cartesian plane and Earth globe'
        $shortcut.Save()
    }
    $uninstaller = Join-Path $InstallDirectory 'uninstall.ps1'
    New-Item -Path $script:AlgetricRegistry -Force | Out-Null
    $properties = @{
        DisplayName = 'Algetric'; DisplayVersion = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion
        Publisher = 'Algetric'; InstallLocation = $InstallDirectory; DisplayIcon = "$executable,0"
        UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstaller`""
    }
    foreach ($key in $properties.Keys) { New-ItemProperty -LiteralPath $script:AlgetricRegistry -Name $key -Value $properties[$key] -PropertyType String -Force | Out-Null }
    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
} catch {
    if (Test-Path -LiteralPath $backup) {
        if (Test-Path -LiteralPath $InstallDirectory) { Remove-Item -LiteralPath $InstallDirectory -Recurse -Force }
        Move-Item -LiteralPath $backup -Destination $InstallDirectory
    }
    throw
} finally { if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force } }
Write-Host 'Algetric installed. Open Algetric from the Start menu. No administrator privileges are required.'
