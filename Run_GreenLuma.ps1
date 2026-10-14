#Requires -Version 5.1
<#
.SYNOPSIS
    GreenLuma Auto-Launcher & Cleaner (Stealth Mode)
.DESCRIPTION
    Injects GreenLuma stealth files into the Steam directory, launches Steam,
    monitors the session, and automatically wipes all injected files upon exit.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "GreenLuma Stealth Session Manager"

function Get-SteamPath {
    $candidates = @()
    
    # 1. Check HKCU
    $hkcu = Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue
    if ($hkcu -and $hkcu.SteamPath) {
        $candidates += ($hkcu.SteamPath -replace '/', '\').TrimEnd('\')
    }

    # 2. Check HKLM 64-bit / WOW64
    $hklm = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -ErrorAction SilentlyContinue
    if ($hklm -and $hklm.InstallPath) {
        $candidates += ($hklm.InstallPath -replace '/', '\').TrimEnd('\')
    }

    # 3. Common fallback directories
    $candidates += "E:\steam"
    $candidates += "C:\Program Files (x86)\Steam"
    $candidates += "D:\Steam"
    $candidates += "C:\Steam"

    foreach ($path in ($candidates | Select-Object -Unique)) {
        if ($path -and (Test-Path (Join-Path $path "steam.exe"))) {
            return $path
        }
    }
    return $null
}

function Stop-SteamProcess {
    param([string]$SteamDir)
    
    $procs = Get-Process -Name steam -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "`n[i] Steam is running. Shutting down gracefully..." -ForegroundColor Yellow
        $steamExe = Join-Path $SteamDir "steam.exe"
        if (Test-Path $steamExe) {
            Start-Process -FilePath $steamExe -ArgumentList "-shutdown" -WindowStyle Hidden
            Start-Sleep -Seconds 3
        }

        # Force terminate if still alive after 3 seconds
        $remaining = Get-Process -Name steam -ErrorAction SilentlyContinue
        if ($remaining) {
            Write-Host "[i] Force terminating remaining Steam process..." -ForegroundColor Yellow
            $remaining | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }
    }
}

function Remove-InjectedFiles {
    param([string]$SteamDir)
    
    $user32Path = Join-Path $SteamDir "user32.dll"
    $appListPath = Join-Path $SteamDir "AppList"
    
    Write-Host "`n=======================================================" -ForegroundColor Cyan
    Write-Host "                CLEANUP PHASE" -ForegroundColor Cyan
    Write-Host "=======================================================" -ForegroundColor Cyan

    # Ensure Steam is stopped so files aren't locked
    Stop-SteamProcess -SteamDir $SteamDir

    # Clean user32.dll
    if (Test-Path -LiteralPath $user32Path) {
        $removed = $false
        for ($i = 1; $i -le 5; $i++) {
            try {
                Remove-Item -LiteralPath $user32Path -Force -ErrorAction Stop
                $removed = $true
                Write-Host "[OK] Successfully deleted: user32.dll" -ForegroundColor Green
                break
            } catch {
                Write-Host "[!] Waiting for user32.dll release (attempt $i/5)..." -ForegroundColor Yellow
                Start-Sleep -Seconds 1
            }
        }
        if (-not $removed) {
            Write-Host "[ERROR] Could not delete $user32Path (file locked)." -ForegroundColor Red
        }
    } else {
        Write-Host "[-] user32.dll not present in Steam directory." -ForegroundColor Gray
    }

    # Clean AppList folder
    if (Test-Path -LiteralPath $appListPath) {
        $removed = $false
        for ($i = 1; $i -le 5; $i++) {
            try {
                Remove-Item -LiteralPath $appListPath -Recurse -Force -ErrorAction Stop
                $removed = $true
                Write-Host "[OK] Successfully deleted: AppList folder" -ForegroundColor Green
                break
            } catch {
                Write-Host "[!] Waiting for AppList release (attempt $i/5)..." -ForegroundColor Yellow
                Start-Sleep -Seconds 1
            }
        }
        if (-not $removed) {
            Write-Host "[ERROR] Could not delete $appListPath." -ForegroundColor Red
        }
    } else {
        Write-Host "[-] AppList folder not present in Steam directory." -ForegroundColor Gray
    }

    Write-Host "`n[✓] Steam directory cleanup finished." -ForegroundColor Green
}

function Clean-AllGreenLumaArtifacts {
    param([string]$SteamDir)

    Stop-SteamProcess -SteamDir $SteamDir

    $artifacts = @(
        "user32.dll",
        "AppList",
        "DLLInjector.exe",
        "DLLInjector.ini",
        "GreenLumaSettings_2026.exe",
        "GreenLuma_2026_x64.dll",
        "GreenLuma_2026_x86.dll",
        "GreenLuma_2026.log",
        "GreenLuma2026_Files",
        "NoQuestion.bin"
    )

    Write-Host "`n[i] Scanning for all GreenLuma artifacts in: $SteamDir" -ForegroundColor Cyan
    $count = 0
    foreach ($item in $artifacts) {
        $target = Join-Path $SteamDir $item
        if (Test-Path -LiteralPath $target) {
            try {
                Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
                Write-Host "[OK] Removed: $item" -ForegroundColor Green
                $count++
            } catch {
                Write-Host "[ERROR] Could not remove $item" -ForegroundColor Red
            }
        }
    }
    Write-Host "[✓] Total items purged: $count" -ForegroundColor Green
}

# --- Main Script ---

Clear-Host
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "       GreenLuma 2026 Stealth Session Manager          " -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

# 1. Locate Steam
$steamDir = Get-SteamPath
if (-not $steamDir) {
    Write-Host "[!] Could not automatically detect Steam directory." -ForegroundColor Red
    $steamDir = Read-Host "Please enter your Steam directory path (e.g. E:\steam)"
    if (-not (Test-Path (Join-Path $steamDir "steam.exe"))) {
        Write-Host "[ERROR] Invalid Steam directory. steam.exe not found." -ForegroundColor Red
        pause
        exit 1
    }
}
Write-Host "[+] Steam detected at: $steamDir" -ForegroundColor Green

# 2. Check source folders
$stealthDir = Join-Path $PSScriptRoot "StealthMode"
if (-not (Test-Path $stealthDir)) {
    Write-Host "[ERROR] StealthMode folder not found at: $stealthDir" -ForegroundColor Red
    pause
    exit 1
}

# Source AppList check
$srcAppList = Join-Path $PSScriptRoot "AppList"
if (-not (Test-Path $srcAppList)) {
    $srcAppList = Join-Path $stealthDir "AppList"
}

# 3. Interactive Menu
Write-Host "`nSelect an option:" -ForegroundColor Yellow
Write-Host " [1] Launch Stealth Mode (Standard user32.dll) [Default]"
Write-Host " [2] Launch Stealth Mode (Steam Families user32SF.dll)"
Write-Host " [3] Clean all GreenLuma files from Steam now (Reset)"
Write-Host " [4] Exit"
$choice = Read-Host "`nEnter selection (1-4) [Default: 1]"
if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }

if ($choice -eq "3") {
    Clean-AllGreenLumaArtifacts -SteamDir $steamDir
    Write-Host "`nPress any key to exit..."
    $null = [Console]::ReadKey($true)
    exit 0
}

if ($choice -eq "4") {
    exit 0
}

$dllSource = if ($choice -eq "2") {
    Join-Path $stealthDir "user32SF.dll"
} else {
    Join-Path $stealthDir "user32.dll"
}

if (-not (Test-Path $dllSource)) {
    Write-Host "[ERROR] Source DLL not found: $dllSource" -ForegroundColor Red
    pause
    exit 1
}

# Optional AppCache deletion
$delAppCache = Read-Host "`nClear Steam AppCache before starting? (y/N) [Default: N]"
if ($delAppCache -eq "y" -or $delAppCache -eq "Y") {
    $delExe = Join-Path $stealthDir "DeleteSteamAppCache.exe"
    if (Test-Path $delExe) {
        Write-Host "[i] Running DeleteSteamAppCache.exe..." -ForegroundColor Yellow
        Start-Process -FilePath $delExe -Wait -NoNewWindow
    }
}

# 4. Stop running Steam if any
Stop-SteamProcess -SteamDir $steamDir

# 5. Copy files
Write-Host "`n[i] Copying Stealth files to Steam..." -ForegroundColor Cyan

$destDll = Join-Path $steamDir "user32.dll"
$destAppList = Join-Path $steamDir "AppList"

try {
    Copy-Item -LiteralPath $dllSource -Destination $destDll -Force -ErrorAction Stop
    Write-Host "[+] Copied: $(Split-Path $dllSource -Leaf) -> user32.dll" -ForegroundColor Green

    if (Test-Path $srcAppList) {
        if (-not (Test-Path $destAppList)) {
            New-Item -ItemType Directory -Path $destAppList -Force | Out-Null
        }
        Copy-Item -Path "$srcAppList\*" -Destination $destAppList -Recurse -Force -ErrorAction Stop
        Write-Host "[+] Copied: AppList directory" -ForegroundColor Green
    } else {
        Write-Host "[!] Warning: AppList folder not found in source." -ForegroundColor Yellow
    }
} catch {
    Write-Host "[ERROR] Failed copying files: $_" -ForegroundColor Red
    pause
    exit 1
}

# 6. Launch Steam
Write-Host "`n[i] Launching Steam..." -ForegroundColor Cyan
$steamExe = Join-Path $steamDir "steam.exe"
Start-Process -FilePath $steamExe

# Wait for steam process to start
$proc = $null
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 1
    $proc = Get-Process -Name steam -ErrorAction SilentlyContinue
    if ($proc) { break }
}

if (-not $proc) {
    Write-Host "[!] Note: Steam process was not detected yet. It may still be loading." -ForegroundColor Yellow
}

# 7. Monitoring Session
Write-Host "`n=======================================================" -ForegroundColor Green
Write-Host " >> GreenLuma session is ACTIVE!" -ForegroundColor Green
Write-Host " >> Play your games now." -ForegroundColor White
Write-Host " >> WHEN YOU ARE DONE:" -ForegroundColor Yellow
Write-Host "    - Close Steam normally, OR" -ForegroundColor Yellow
Write-Host "    - Press [ENTER] in this window to stop and clean up." -ForegroundColor Yellow
Write-Host "=======================================================" -ForegroundColor Green

try {
    while ($true) {
        Start-Sleep -Milliseconds 800
        
        # Check if Steam process closed
        $steamRunning = Get-Process -Name steam -ErrorAction SilentlyContinue
        if (-not $steamRunning) {
            Write-Host "`n[!] Steam process has terminated." -ForegroundColor Yellow
            break
        }

        # Check if user pressed Enter in console
        try {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -eq [ConsoleKey]::Enter) {
                    Write-Host "`n[!] User initiated session finish." -ForegroundColor Yellow
                    break
                }
            }
        } catch {
            # Fallback if console reading is restricted
        }
    }
} finally {
    # 8. Clean up
    Remove-InjectedFiles -SteamDir $steamDir
}

Write-Host "`n=======================================================" -ForegroundColor Cyan
Write-Host " Session finished! All injected files removed." -ForegroundColor Green
Write-Host " Your Steam folder is back to its original state." -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "Press any key to exit..."
try { $null = [Console]::ReadKey($true) } catch {}
