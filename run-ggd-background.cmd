@echo off
setlocal
cd /d "%~dp0"
if not exist "logs" mkdir "logs"
call "%~dp0start-ggd.cmd" >> "%~dp0logs\ggd.log" 2>&1
exit /b %errorlevel%
