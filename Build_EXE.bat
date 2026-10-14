@echo off
setlocal
cd /d "%~dp0"
title Build Native Standalone GreenLumaManager.exe

echo ================================================================
echo    Building Standalone GreenLumaManager.exe (Single Executable)
echo ================================================================
echo [*] Embedding StealthMode payloads directly into the EXE...
echo [*] Compiling GreenLumaApp.cs with C# compiler (csc.exe)...

"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize+ ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
  /r:"System.Xaml.dll" ^
  /r:"System.dll" ^
  /r:"System.Core.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll" ^
  /resource:"StealthMode\user32.dll",user32.dll ^
  /resource:"StealthMode\user32SF.dll",user32SF.dll ^
  /resource:"StealthMode\DeleteSteamAppCache.exe",DeleteSteamAppCache.exe ^
  /resource:"assets\logo.png",logo.png ^
  /out:"%~dp0GreenLumaManager.exe" "%~dp0GreenLumaApp.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ================================================================
    echo  [OK] Build SUCCESS! GreenLumaManager.exe is ready!
    echo  - 100%% Native Windows Desktop Application - No browser
    echo  - 100%% Portable Single File with Embedded Payloads
    echo  - Friends can run it anywhere without extra folders!
    echo  - Drop new DLLs in the folder anytime to override/update!
    echo ================================================================
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%
)

pause
