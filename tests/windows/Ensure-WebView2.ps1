$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'scripts/windows-common.ps1')
if (-not (Test-AlgetricWebView2)) {
    $setup = Join-Path ([IO.Path]::GetTempPath()) ('algetric-webview2-' + [Guid]::NewGuid().ToString('N') + '.exe')
    try {
        Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $setup
        $signature = Get-AuthenticodeSignature -LiteralPath $setup
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
            throw 'The WebView2 bootstrapper does not have a valid Microsoft signature.'
        }
        $process = Start-Process -FilePath $setup -ArgumentList '/silent', '/install' -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-AlgetricWebView2)) { throw 'WebView2 Runtime setup failed.' }
    } finally { if (Test-Path -LiteralPath $setup) { Remove-Item -LiteralPath $setup } }
}
