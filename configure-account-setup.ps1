$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$setupCode = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
& dotnet user-secrets set 'AccountSetup:Code' $setupCode --project (Join-Path $projectRoot 'server')
if ($LASTEXITCODE -ne 0) { throw 'Could not configure account setup.' }
Write-Host ''
Write-Host 'Your one-time account setup code:'
Write-Host $setupCode
Write-Host ''
Write-Host 'Enter this code in the app after publishing and restarting. Choose your password in the app.'
Write-Host 'The setup endpoint closes automatically after the first account is configured.'
