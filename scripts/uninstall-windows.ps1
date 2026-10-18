[CmdletBinding()]
param([string]$InstallDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'Run this uninstaller on Windows.' }
. (Join-Path $PSScriptRoot 'windows-common.ps1')
if (-not $InstallDirectory) {
    $registration = Get-ItemProperty -LiteralPath $script:AlgetricRegistry -ErrorAction SilentlyContinue
    if ($null -ne $registration) { $InstallDirectory = $registration.InstallLocation }
    else { $InstallDirectory = Join-Path $env:LOCALAPPDATA 'Programs/Algetric' }
}
if (-not (Test-Path -LiteralPath $InstallDirectory)) { Write-Host 'Algetric is not installed.'; return }
if (-not (Test-AlgetricManaged $InstallDirectory)) { throw 'Refusing to remove an unmanaged directory.' }
if (Get-Process -Name 'Algetric.Desktop' -ErrorAction SilentlyContinue) { throw 'Close Algetric before uninstalling.' }
$shell = New-Object -ComObject WScript.Shell
$executable = Join-Path $InstallDirectory 'Algetric.Desktop.exe'
foreach ($link in @((Get-AlgetricShortcut), (Join-Path ([Environment]::GetFolderPath('Desktop', 'DoNotVerify')) 'Algetric.lnk'))) {
    if ((Test-Path -LiteralPath $link) -and $shell.CreateShortcut($link).TargetPath -eq $executable) { Remove-Item -LiteralPath $link -Force }
}
$registration = Get-ItemProperty -LiteralPath $script:AlgetricRegistry -ErrorAction SilentlyContinue
if ($null -ne $registration -and $registration.InstallLocation -eq $InstallDirectory) {
    Remove-Item -LiteralPath $script:AlgetricRegistry -Recurse -Force
}
Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
Write-Host 'Algetric uninstalled. Projects, settings, credentials, and downloaded maps were preserved.'
