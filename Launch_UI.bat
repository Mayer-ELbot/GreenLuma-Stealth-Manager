@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
title GreenLuma 2026 Stealth Dashboard

echo ===================================================
echo     GreenLuma 2026 Stealth Session & UI Manager
echo ===================================================
echo [i] Starting local server at http://127.0.0.1:58421 ...
echo [i] Opening modern dashboard window...
echo.

python "%~dp0app.py"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [!] Server exited with code %ERRORLEVEL%
    pause
)
