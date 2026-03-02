# scripts/backup_configs.ps1
#
# PURPOSE: Back up all CrusaderDETweaker config files to a named or timestamped folder.
#
# USAGE:
#   .\scripts\backup_configs.ps1                        # Timestamp backup (e.g. 2026-03-02_143022)
#   .\scripts\backup_configs.ps1 -Name "pre-v2.2"       # Named backup
#   .\scripts\backup_configs.ps1 -GamePath "D:\Games\SHCDE"
#
# PARAMETERS:
#   -Name       Optional name for the backup folder (defaults to timestamp)
#   -GamePath   Override Steam game install path
#

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$Name = ""
)

$configDir  = Join-Path $GamePath "BepInEx\config\CrusaderDETweaker"
$matrixDir  = Join-Path $configDir "DamageMatrices"
$backupRoot = Join-Path $configDir "Backups"

if (-not (Test-Path $configDir)) {
    Write-Host "Config directory not found: $configDir" -ForegroundColor Red
    exit 1
}

if ($Name -eq "") {
    $Name = Get-Date -Format "yyyy-MM-dd_HHmmss"
}

$dest = Join-Path $backupRoot $Name

if (Test-Path $dest) {
    Write-Host "Backup '$Name' already exists: $dest" -ForegroundColor Red
    Write-Host "Choose a different name or delete the existing backup first." -ForegroundColor Yellow
    exit 1
}

New-Item -ItemType Directory -Path $dest -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dest "DamageMatrices") -Force | Out-Null

Write-Host "=== Backing Up CrusaderDETweaker Configs ===" -ForegroundColor Cyan
Write-Host "Backup name: $Name" -ForegroundColor White
Write-Host "Destination: $dest" -ForegroundColor Gray
Write-Host ""

$files = @(
    "CrusaderDETweaker_GlobalMultipliers.cfg",
    "CrusaderDETweaker_GameplaySettings.toml",
    "CrusaderDETweaker_Units.toml",
    "CrusaderDETweaker_Structures.toml"
)

$csvFiles = @(
    "CrusaderDETweaker_MeleeDamage.csv",
    "CrusaderDETweaker_RangedDamage.csv",
    "CrusaderDETweaker_EunuchAoeDamage.csv",
    "CrusaderDETweaker_BallistaDamage.csv",
    "CrusaderDETweaker_UnitFireDamage.csv",
    "CrusaderDETweaker_BedouinHeal.csv",
    "CrusaderDETweaker_BuildingFireDamage.csv"
)

$copied = 0
$missing = 0

foreach ($file in $files) {
    $src = Join-Path $configDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $dest $file)
        Write-Host "  Backed up: $file" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "  Missing:   $file" -ForegroundColor Gray
        $missing++
    }
}

foreach ($file in $csvFiles) {
    $src = Join-Path $matrixDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $dest "DamageMatrices\$file")
        Write-Host "  Backed up: DamageMatrices\$file" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "  Missing:   DamageMatrices\$file" -ForegroundColor Gray
        $missing++
    }
}

Write-Host ""
Write-Host "Done. Copied=$copied, Missing=$missing" -ForegroundColor Cyan
Write-Host "Restore with: .\scripts\restore_configs.ps1 -Name `"$Name`"" -ForegroundColor Gray
