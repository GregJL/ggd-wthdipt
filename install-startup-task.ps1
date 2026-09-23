$ErrorActionPreference = 'Stop'

$taskName = 'GGD-WTHDIPT'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcher = Join-Path $projectRoot 'run-ggd-background.ps1'
$publishedApp = Join-Path $projectRoot 'publish\Ggd.Api.exe'
$certificate = Join-Path $projectRoot 'certs\ggd-local.pem'
$certificateKey = Join-Path $projectRoot 'certs\ggd-local-key.pem'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principalCheck = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principalCheck.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator, then run this installer again.'
}

foreach ($requiredFile in @($launcher, $publishedApp, $certificate, $certificateKey)) {
    if (-not (Test-Path $requiredFile)) { throw "Required file not found: $requiredFile" }
}

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        if (-not (Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
}

$listener = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    throw 'Port 5001 is already in use. Stop the manually started GGD application with Ctrl+C, then run this installer again.'
}

$userId = $identity.Name
$arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}"' -f $launcher
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments -WorkingDirectory $projectRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $userId
$principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description 'Runs the published GGD-WTHDIPT home inventory application at Windows sign-in.' `
    -Force | Out-Null

Start-ScheduledTask -TaskName $taskName
Write-Host ''
Write-Host 'GGD-WTHDIPT is installed and starting in the background.' -ForegroundColor Green
Write-Host 'It will start automatically whenever this Windows account signs in.'
Write-Host "Log file: $(Join-Path $projectRoot 'logs\ggd.log')"
