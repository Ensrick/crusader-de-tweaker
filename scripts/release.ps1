# scripts/release.ps1
#
# PURPOSE: Full release pipeline for CrusaderDETweaker.
#          Runs all steps in order: build → backup → package.
#
# USAGE:
#   .\scripts\release.ps1                          # Auto-named backup (timestamp)
#   .\scripts\release.ps1 -BackupName "pre-v2.2"   # Named backup
#   .\scripts\release.ps1 -GamePath "D:\Games\SHCDE"
#
# STEPS:
#   1. Build        — compiles the plugin DLL
#   2. Backup       — saves current configs to Backups\<name> (safety net)
#   3. Package      — stages the plugin folder and zips it
#
# HISTORY: Until v2.4.0 this pipeline had three more steps (reset configs →
# launch game to regenerate pristine defaults → restore personal configs)
# because the zip shipped config files. As of v2.4.1 the zip is plugin-only —
# extracting an upgrade over an install used to overwrite users' personalized
# configs with the shipped defaults. Configs now come from first-launch
# generation + on-update migration, so the reset/launch/restore dance is gone.
# reset_configs.ps1 / restore_configs.ps1 remain available as standalone tools.
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
Write-Host "[1/3] Building..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "build.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 2: Backup configs (safety net; nothing in the pipeline touches them anymore) ---
Write-Host "[2/3] Backing up configs as '$BackupName'..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "backup_configs.ps1") -Name $BackupName -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "BACKUP FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

# --- Step 3: Package release ---
Write-Host "[3/3] Packaging release..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "package_release.ps1") -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "PACKAGE FAILED. Aborting." -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green
Write-Host "  Release pipeline complete!           " -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
