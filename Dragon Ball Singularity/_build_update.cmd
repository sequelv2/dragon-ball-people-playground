@echo off
rem Rebuilds dragon ball.dll from the sources in this folder and writes _build.log
cd /d "%~dp0"
echo Building Dragon Ball Singularity... > "%~dp0_build.log"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" >> "%~dp0_build.log" 2>&1
echo EXITCODE=%ERRORLEVEL% >> "%~dp0_build.log"
