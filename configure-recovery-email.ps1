param([switch]$FromClipboard)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$senderAddress = Read-Host 'Gmail/Google Workspace address to SEND recovery mail from'
if ($senderAddress -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Enter a complete email address.' }
$appPassword = $null
try {
    if ($FromClipboard) {
        $appPassword = (Get-Clipboard -Raw).Trim() -replace '\s', ''
        Set-Clipboard -Value ' '
    } else {
        $securePassword = Read-Host 'Google app password (not your Google account password)' -AsSecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        try { $appPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) -replace '\s', '' }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    }
    if ($appPassword.Length -ne 16) { throw 'A Google app password has 16 characters. If paste was truncated, retry with -FromClipboard.' }
    & dotnet user-secrets set 'RecoveryEmail:SenderAddress' $senderAddress --project (Join-Path $projectRoot 'server')
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the sender address.' }
    & dotnet user-secrets set 'RecoveryEmail:AppPassword' $appPassword --project (Join-Path $projectRoot 'server')
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the Google app password.' }
    Write-Host 'Recovery email configured. Restart the app to load these settings.'
} finally { $appPassword = $null }
