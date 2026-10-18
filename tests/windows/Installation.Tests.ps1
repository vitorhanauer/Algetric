[CmdletBinding()]
param([string]$PackageDirectory = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'artifacts/win-x64'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'Run the real installer tests on Windows.' }
function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
. (Join-Path $PackageDirectory 'windows-common.ps1')
if (Test-Path -LiteralPath $script:AlgetricRegistry) { throw 'Installer tests require a user profile without an existing Algetric installation.' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('algetric-install-test-' + [Guid]::NewGuid().ToString('N'))
$installed = Join-Path $temporary 'application with spaces'
$userData = Join-Path $env:LOCALAPPDATA 'algetric'
New-Item -ItemType Directory -Path $userData -Force | Out-Null
$sentinel = Join-Path $userData ('installer-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
[IO.File]::WriteAllText($sentinel, 'keep user data')
try {
    & (Join-Path $PackageDirectory 'install.ps1') -InstallDirectory $installed -DesktopShortcut
    Assert-True (Test-Path -LiteralPath (Join-Path $installed 'Algetric.Desktop.exe')) 'Missing installed executable.'
    Assert-True (Test-Path -LiteralPath (Get-AlgetricShortcut)) 'Missing Start menu shortcut.'
    $shell = New-Object -ComObject WScript.Shell
    Assert-True ($shell.CreateShortcut((Get-AlgetricShortcut)).TargetPath -eq (Join-Path $installed 'Algetric.Desktop.exe')) 'Incorrect shortcut target.'
    Assert-True ((Get-ItemProperty -LiteralPath $script:AlgetricRegistry).InstallLocation -eq $installed) 'Missing Apps registration.'
    $process = Start-Process -FilePath (Join-Path $installed 'Algetric.Desktop.exe') -ArgumentList '--diagnostics' -Wait -PassThru
    Assert-True ($process.ExitCode -eq 0) 'Installed application diagnostics failed.'
    & (Join-Path $PackageDirectory 'install.ps1') -InstallDirectory $installed
    & (Join-Path $installed 'uninstall.ps1')
    Assert-True (-not (Test-Path -LiteralPath $installed)) 'Application files remain after uninstall.'
    Assert-True (-not (Test-Path -LiteralPath (Get-AlgetricShortcut))) 'Start menu shortcut remains after uninstall.'
    Assert-True (-not (Test-Path -LiteralPath $script:AlgetricRegistry)) 'Apps registration remains after uninstall.'
    Assert-True ((Get-Content -LiteralPath $sentinel -Raw) -eq 'keep user data') 'Uninstall removed user data.'
    New-Item -ItemType Directory -Path $installed -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $installed 'unrelated.txt'), 'keep')
    $failed = $false
    try { & (Join-Path $PackageDirectory 'install.ps1') -InstallDirectory $installed }
    catch { $failed = $true }
    Assert-True $failed 'Installer must refuse an unmanaged directory.'
    Write-Host 'PASS: install, Start menu and desktop shortcuts, Apps registration, diagnostics, upgrade, uninstall, preserved user data, and unmanaged-directory rejection.'
} finally {
    if (Test-AlgetricManaged $installed) { & (Join-Path $installed 'uninstall.ps1') }
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
    if (Test-Path -LiteralPath $sentinel) { Remove-Item -LiteralPath $sentinel }
}
