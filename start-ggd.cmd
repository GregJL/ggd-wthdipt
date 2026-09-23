@echo off
setlocal
cd /d "%~dp0"
if not exist "certs\ggd-local.pem" (
  echo Missing certs\ggd-local.pem
  pause
  exit /b 1
)
if not exist "certs\ggd-local-key.pem" (
  echo Missing certs\ggd-local-key.pem
  pause
  exit /b 1
)
if not exist "publish\Ggd.Api.exe" (
  echo The published application is missing. Run publish-ggd.cmd first.
  pause
  exit /b 1
)
set "ASPNETCORE_Kestrel__Certificates__Default__Path=%~dp0certs\ggd-local.pem"
set "ASPNETCORE_Kestrel__Certificates__Default__KeyPath=%~dp0certs\ggd-local-key.pem"
set "ASPNETCORE_URLS=https://0.0.0.0:5001"
echo Starting GGD-WTHDIPT at https://192.168.68.108:5001
echo Close this window to stop the application.
cd /d "%~dp0publish"
Ggd.Api.exe --urls "https://0.0.0.0:5001"
