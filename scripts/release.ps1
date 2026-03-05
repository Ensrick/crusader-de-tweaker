# scripts/release.ps1
#
# PURPOSE: Full release pipeline for CrusaderDETweaker.
#          Runs all steps in order: build → backup → reset → launch → package → restore.
#
# USAGE:
#   .\scripts\release.ps1                          # Auto-named backup (timestamp)
#   .\scripts\release.ps1 -BackupName "pre-v2.2"   # Named backup
#   .\scripts\release.ps1 -GamePath "D:\Games\SHCDE"
#
# STEPS:
#   1. Build        — compiles the plugin DLL
#   2. Backup       — saves current configs to Backups\<name>
#   3. Reset        — deletes live config files
#   4. Launch game  — starts game via Steam, waits for init, kills it (regenerates configs)
#   5. Package      — copies plugin + fresh configs to staging dir and zips
#   6. Restore      — puts your personal configs back
#
# PARAMETERS:
#   -BackupName     Name for the config backup. Defaults to timestamp.
#   -GamePath       Override Steam game install path.
#

param(
    [string]$BackupName = "",
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot

# Resolve backup name
if ($BackupName -eq "") {
    $BackupName = Get-Date -Format "yyyy-MM-dd_HHmmss"
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "  CrusaderDETweaker Release Pipeline   " -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "  Backup name : $BackupName"            -ForegroundColor White
Write-Host ""

# --- Step 1: Build ---
Write-Host "[1/6] Building..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "build.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 2: Backup configs ---
Write-Host "[2/6] Backing up configs as '$BackupName'..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "backup_configs.ps1") -Name $BackupName -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "BACKUP FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 3: Reset configs ---
Write-Host "[3/6] Resetting configs..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "reset_configs.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "RESET FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 4: Launch game to regenerate configs ---
Write-Host "[4/6] Launching game to regenerate configs..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "launch_game.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "LAUNCH FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 5: Package release ---
Write-Host "[5/6] Packaging release..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "package_release.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "PACKAGE FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 6: Restore configs ---
Write-Host "[6/6] Restoring configs from '$BackupName'..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "restore_configs.ps1") -Name $BackupName -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "RESTORE FAILED (configs are in backup '$BackupName')." -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green
Write-Host "  Release pipeline complete!           " -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
