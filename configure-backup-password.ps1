param(
    [switch]$FromClipboard
)

$ErrorActionPreference = 'Stop'

$passwordDirectory = Join-Path $env:APPDATA 'postgresql'
$passwordFile = Join-Path $passwordDirectory 'pgpass.conf'
$password = $null
$credential = $null
$plainPassword = $null

if ($FromClipboard) {
    $plainPassword = Get-Clipboard -Raw
    if ([string]::IsNullOrWhiteSpace($plainPassword)) {
        throw 'The Windows clipboard is empty. Copy the PostgreSQL password, then run this command again.'
    }
    $plainPassword = $plainPassword.TrimEnd("`r", "`n")
}
else {
    $password = Read-Host 'Enter the PostgreSQL password for user postgres' -AsSecureString
    $credential = New-Object System.Management.Automation.PSCredential('postgres', $password)
    $plainPassword = $credential.GetNetworkCredential().Password
}

try {
    $escapedPassword = $plainPassword.Replace('\', '\\').Replace(':', '\:')
    New-Item -ItemType Directory -Path $passwordDirectory -Force | Out-Null
    Set-Content -LiteralPath $passwordFile -Value "localhost:5432:ggd_inventory:postgres:$escapedPassword" -Encoding ASCII
    Write-Host "PostgreSQL backup credentials saved for this Windows account: $passwordFile" -ForegroundColor Green
}
finally {
    if ($FromClipboard) { Set-Clipboard -Value ' ' }
    $plainPassword = $null
    $credential = $null
    $password = $null
}
