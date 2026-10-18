$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $repository 'scripts/windows-common.ps1')
function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('algetric-update-test-' + [Guid]::NewGuid().ToString('N'))
$previousLocalAppData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $temporary 'user'
    $script:Registration = $null
    function Get-ItemProperty { param($LiteralPath, $Name, $ErrorAction) return $script:Registration }
    $default = Join-Path $env:LOCALAPPDATA 'Programs/Algetric'
    Assert-True ((Get-AlgetricInstallDirectory '') -eq $default) 'First installation must use the default directory.'
    $registered = Join-Path $temporary 'custom application'
    $script:Registration = [pscustomobject]@{ InstallLocation = $registered }
    Assert-True ((Get-AlgetricInstallDirectory '') -eq $registered) 'Updates must reuse the registered custom directory.'
    $explicit = Join-Path $temporary 'explicit application'
    Assert-True ((Get-AlgetricInstallDirectory $explicit) -eq $explicit) 'An explicit directory must take precedence.'
    $scripts = Join-Path $temporary 'source/scripts'
    $package = Join-Path $temporary 'source/artifacts/win-x64'
    New-Item -ItemType Directory -Path $scripts, $package -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $package 'Algetric.Desktop.exe'), 'old')
    [IO.File]::WriteAllText((Join-Path $package 'install.ps1'), '# old package')
    [IO.File]::WriteAllText((Join-Path $scripts 'publish-windows.ps1'), @'
$package = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/win-x64'
[IO.File]::WriteAllText((Join-Path $package 'Algetric.Desktop.exe'), 'latest')
Add-Content -LiteralPath (Join-Path $package 'publish.log') -Value 'published'
'@)
    foreach ($iteration in 1..2) {
        Assert-True ((Get-AlgetricPackageDirectory $scripts) -eq $package) 'Incorrect source package directory.'
        Assert-True ((Get-Content -LiteralPath (Join-Path $package 'Algetric.Desktop.exe') -Raw) -eq 'latest') 'An existing source package was reused without publishing.'
    }
    Assert-True (@(Get-Content -LiteralPath (Join-Path $package 'publish.log')).Count -eq 2) 'Every source installation must publish.'
    Assert-True ((Get-AlgetricPackageDirectory $package) -eq $package) 'Extracted packages must not require a build.'
    [IO.File]::WriteAllText((Join-Path $scripts 'publish-windows.ps1'), "throw 'Publish failed'")
    $failed = $false
    try { Get-AlgetricPackageDirectory $scripts | Out-Null } catch { $failed = $true }
    Assert-True $failed 'A failed publish must stop installation.'
    Write-Host 'PASS: source republishing, package installation, failed build, default/custom installation detection, and explicit directory override.'
} finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
