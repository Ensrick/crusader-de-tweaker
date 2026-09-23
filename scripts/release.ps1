# scripts/release.ps1
#
# PURPOSE: Local release rehearsal: back up your configs, then run ship.ps1's LOCAL stages
#          (preflight -> build -> package). Nothing is uploaded, pushed or deployed.
#
# The full ship (tag, GitHub/GitLab releases, Steam Workshop, Nexus, deploy) is scripts/ship.ps1:
#   .\scripts\ship.ps1                           # dry run of every stage
#   .\scripts\ship.ps1 -Version 2.6.6 -Publish   # the real thing
#
# USAGE:
#   .\scripts\release.ps1                          # Auto-named backup (timestamp)
#   .\scripts\release.ps1 -BackupName "pre-v2.2"   # Named backup
#   .\scripts\release.ps1 -AllowDirty              # rehearse with uncommitted changes
#
# HISTORY: Until v2.4.0 this pipeline reset configs, launched the game to regenerate pristine
# defaults and restored personal configs, because the zip shipped config files. Since v2.4.1 the zip
# is plugin-only. Since 2.6.6 packaging builds from a clean tree into dist\ instead of zipping the
# live game plugin folder.
#
# PARAMETERS:
#   -BackupName     Name for the config backup. Defaults to timestamp.
#   -GamePath       Override Steam game install path.
#   -ShcdeseDir     Compile against this SHCDE-SE folder instead of the installed one.
#   -AllowDirty     Pass through to ship.ps1 (dry run only).
#

param(
    [string]$BackupName = "",
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$ShcdeseDir = "",
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot

if ($BackupName -eq "") {
    $BackupName = Get-Date -Format "yyyy-MM-dd_HHmmss"
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "  CrusaderDETweaker Release Rehearsal  " -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

Write-Host "[1/2] Backing up configs as '$BackupName'..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "backup_configs.ps1") -Name $BackupName -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Host "BACKUP FAILED. Aborting." -ForegroundColor Red; exit 1 }
Write-Host ""

Write-Host "[2/2] ship.ps1 local stages (preflight, build, package)..." -ForegroundColor Yellow
$shipArgs = @{ Stage = @('preflight', 'build', 'package'); GamePath = $GamePath }
if ($ShcdeseDir) { $shipArgs.ShcdeseDir = $ShcdeseDir }
if ($AllowDirty) { $shipArgs.AllowDirty = $true }
& (Join-Path $ScriptsDir "ship.ps1") @shipArgs
if ($LASTEXITCODE -ne 0) { Write-Host "RELEASE REHEARSAL FAILED." -ForegroundColor Red; exit 1 }
exit 0
