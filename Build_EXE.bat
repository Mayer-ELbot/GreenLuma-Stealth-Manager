@echo off
setlocal
cd /d "%~dp0"
title Build Native Standalone GreenLumaManager.exe

echo ================================================================
echo    Building Standalone GreenLumaManager.exe (Single Executable)
echo ================================================================

set "RESOURCES="

if exist "%~dp0assets\logo.png" (
    set RESOURCES=%RESOURCES% /resource:"assets\logo.png",logo.png
)

if exist "%~dp0StealthMode\user32.dll" (
    echo [*] Detected StealthMode payloads - embedding directly into EXE...
    set RESOURCES=%RESOURCES% /resource:"StealthMode\user32.dll",user32.dll
    set RESOURCES=%RESOURCES% /resource:"StealthMode\user32SF.dll",user32SF.dll
    set RESOURCES=%RESOURCES% /resource:"StealthMode\DeleteSteamAppCache.exe",DeleteSteamAppCache.exe
) else (
    echo [*] StealthMode payloads not found locally. Building lightweight executable.
    echo [*] (You can drop user32.dll / user32SF.dll beside the EXE at runtime)
)

echo [*] Compiling GreenLumaApp.cs with C# compiler (csc.exe)...

"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize+ ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
  /r:"System.Xaml.dll" ^
  /r:"System.dll" ^
  /r:"System.Core.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll" ^
  %RESOURCES% ^
  /out:"%~dp0GreenLumaManager.exe" "%~dp0GreenLumaApp.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ================================================================
    echo  [OK] Build SUCCESS! GreenLumaManager.exe is ready!
    echo  - 100%% Native Windows Desktop Application - No browser
    echo  - 100%% Portable Single File
    echo  - Friends can run it anywhere!
    echo  - Drop new DLLs in the folder anytime to override/update!
    echo ================================================================
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%
)

pause
