@echo off
setlocal
title Jiangshan Atlas - Debug Launcher
cd /d "%~dp0"

echo ============================================
echo   Jiangshan Atlas - Windows Chrome Launcher
echo ============================================
echo.
echo Working folder:
echo %CD%
echo.

if not exist "%~dp0Start-Jiangshan-Map.ps1" (
    echo ERROR: Start-Jiangshan-Map.ps1 was not found.
    echo Make sure the ZIP was fully extracted and both launcher files are together.
    echo.
    pause
    exit /b 1
)

set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PS%" (
    echo ERROR: Windows PowerShell was not found:
    echo %PS%
    echo.
    pause
    exit /b 1
)

echo Starting PowerShell. This window will remain open for diagnostics.
echo.
"%PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -NoExit -File "%~dp0Start-Jiangshan-Map.ps1"

echo.
echo PowerShell has exited with code %ERRORLEVEL%.
echo The launcher window will remain open.
pause
endlocal
