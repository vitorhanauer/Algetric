$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script:AlgetricRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Algetric'
$script:AlgetricRuntimeKey = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'

function Test-AlgetricWebView2 {
    foreach ($path in @("HKCU:\Software\$script:AlgetricRuntimeKey", "HKLM:\SOFTWARE\WOW6432Node\$script:AlgetricRuntimeKey")) {
        $value = Get-ItemProperty -LiteralPath $path -Name pv -ErrorAction SilentlyContinue
        if ($null -ne $value) {
            $version = $null
            if ([Version]::TryParse([string]$value.pv, [ref]$version) -and $version -gt [Version]'0.0.0.0') { return $true }
        }
    }
    return $false
}

function Test-AlgetricManaged([string]$Directory) {
    if (-not (Test-Path -LiteralPath $Directory)) { return $false }
    if ((Get-Item -LiteralPath $Directory).Attributes -band [IO.FileAttributes]::ReparsePoint) { return $false }
    $marker = Join-Path $Directory '.algetric-install'
    return ((Test-Path -LiteralPath $marker) -and (Get-Content -LiteralPath $marker -Raw).Trim() -eq 'algetric-user-install-v1')
}

function Get-AlgetricShortcut {
    return Join-Path ([Environment]::GetFolderPath('Programs', 'DoNotVerify')) 'Algetric.lnk'
}

function Get-AlgetricInstallDirectory([string]$RequestedDirectory) {
    if ($RequestedDirectory) { return $RequestedDirectory }
    $registration = Get-ItemProperty -LiteralPath $script:AlgetricRegistry -Name InstallLocation -ErrorAction SilentlyContinue
    if ($null -ne $registration -and $registration.InstallLocation) { return $registration.InstallLocation }
    return Join-Path $env:LOCALAPPDATA 'Programs/Algetric'
}

function Get-AlgetricPackageDirectory([string]$ScriptDirectory) {
    if (Test-Path -LiteralPath (Join-Path $ScriptDirectory 'Algetric.Desktop.exe')) { return $ScriptDirectory }
    Write-Host 'Building Algetric from the current source before installation.'
    & (Join-Path $ScriptDirectory 'publish-windows.ps1') | Out-Host
    return Join-Path (Split-Path $ScriptDirectory -Parent) 'artifacts/win-x64'
}
