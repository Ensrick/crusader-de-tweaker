# scripts/reset_configs.ps1
#
# PURPOSE: Delete all generated CrusaderDETweaker config files so they are
#          regenerated fresh on the next game launch.
#
# USAGE:
#   .\scripts\reset_configs.ps1                    # Delete TOML and CSV files
#   .\scripts\reset_configs.ps1 -KeepCsv           # Delete only TOML files
#   .\scripts\reset_configs.ps1 -GamePath "D:\Games\SHCDE"
#
# PARAMETERS:
#   -GamePath   Override Steam game install path
#   -KeepCsv    Skip deletion of CSV damage matrix files
#

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [switch]$KeepCsv = $false
)

$configDir = Join-Path $GamePath "BepInEx\config\CrusaderDETweaker"
$matrixDir = Join-Path $configDir "DamageMatrices"

if (-not (Test-Path $configDir)) {
    Write-Host "Config directory not found: $configDir" -ForegroundColor Yellow
    exit 0
}

Write-Host "=== Resetting CrusaderDETweaker Configs ===" -ForegroundColor Cyan
Write-Host "Config dir: $configDir" -ForegroundColor Gray
Write-Host ""

$tomlFiles = @(
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

$deleted = 0
$skipped = 0

foreach ($file in $tomlFiles) {
    $path = Join-Path $configDir $file
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "  Deleted: $file" -ForegroundColor Green
        $deleted++
    } else {
        Write-Host "  Missing: $file" -ForegroundColor Gray
        $skipped++
    }
}

if (-not $KeepCsv) {
    foreach ($file in $csvFiles) {
        $path = Join-Path $matrixDir $file
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "  Deleted: DamageMatrices\$file" -ForegroundColor Green
            $deleted++
        } else {
            Write-Host "  Missing: DamageMatrices\$file" -ForegroundColor Gray
            $skipped++
        }
    }
} else {
    Write-Host ""
    Write-Host "  Skipped CSV files (-KeepCsv)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. Deleted=$deleted, Already missing=$skipped" -ForegroundColor Cyan
Write-Host "Launch the game to regenerate all configs." -ForegroundColor Gray
