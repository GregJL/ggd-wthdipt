$ErrorActionPreference = 'Stop'

$taskName = 'GGD-WTHDIPT Backup'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backupScript = Join-Path $projectRoot 'backup-ggd.ps1'
$passwordFile = Join-Path $env:APPDATA 'postgresql\pgpass.conf'
$cloudBackupRoot = 'H:\My Drive\GGD-Backups'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principalCheck = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principalCheck.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator, then run this installer again.'
}

foreach ($requiredPath in @($backupScript, $passwordFile, $cloudBackupRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required path not found: $requiredPath" }
}

$arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}"' -f $backupScript
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments -WorkingDirectory $projectRoot
$trigger = New-ScheduledTaskTrigger -Daily -At '3:00 AM'
$principal = New-ScheduledTaskPrincipal -UserId $identity.Name -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 5) `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -MultipleInstances IgnoreNew

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description 'Creates nightly PostgreSQL and photo backups for GGD-WTHDIPT.' `
    -Force | Out-Null

Write-Host ''
Write-Host 'The GGD-WTHDIPT nightly backup task is installed.' -ForegroundColor Green
Write-Host 'Schedule: every day at 3:00 AM while this Windows account is signed in.'
Write-Host "Cloud destination: $cloudBackupRoot"
Write-Host "Backup log: $(Join-Path $projectRoot 'logs\backup.log')"
