$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDirectory = Join-Path $projectRoot 'logs'
$logFile = Join-Path $logDirectory 'ggd.log'
$publishedApp = Join-Path $projectRoot 'publish\Ggd.Api.exe'
$certificate = Join-Path $projectRoot 'certs\ggd-local.pem'
$certificateKey = Join-Path $projectRoot 'certs\ggd-local-key.pem'

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$env:ASPNETCORE_Kestrel__Certificates__Default__Path = $certificate
$env:ASPNETCORE_Kestrel__Certificates__Default__KeyPath = $certificateKey

Set-Location (Join-Path $projectRoot 'publish')
& $publishedApp --urls 'https://0.0.0.0:5001' *>> $logFile
exit $LASTEXITCODE
