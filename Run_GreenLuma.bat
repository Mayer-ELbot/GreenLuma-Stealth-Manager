@echo off
setlocal
cd /d "%~dp0"
title GreenLuma Stealth Session Manager

:: Run PowerShell script with ExecutionPolicy Bypass
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run_GreenLuma.ps1"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [Script exited with code %ERRORLEVEL%]
    pause
)
