@echo off
setlocal
cd /d "%~dp0"
echo Publishing GGD-WTHDIPT as one ASP.NET application...
dotnet publish server\Ggd.Api.csproj -c Release -o publish
if errorlevel 1 (
  echo.
  echo Publish failed. Nothing in the existing publish folder was started.
  pause
  exit /b 1
)
echo.
echo Published successfully to %~dp0publish
pause
