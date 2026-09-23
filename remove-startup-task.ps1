$ErrorActionPreference = 'Stop'
$taskName = 'GGD-WTHDIPT'
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if (-not $task) {
    Write-Host 'The GGD-WTHDIPT startup task is not installed.'
    exit 0
}
Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
Write-Host 'The GGD-WTHDIPT startup task was removed. The application files and data were not changed.' -ForegroundColor Green
