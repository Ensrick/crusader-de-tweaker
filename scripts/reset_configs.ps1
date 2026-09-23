# scripts/reset_configs.ps1
#
# PURPOSE: Delete all generated CrusaderDETweaker config files so they are
#          regenerated fresh on the next game launch.
#
# SAFETY: Destructive for personal settings, so it (1) asks for confirmation unless -Force, and
#         (2) ALWAYS runs backup_configs.ps1 first and aborts if the backup fails. Restore with
#         .\scripts\restore_configs.ps1 -Name <the backup name it prints>.
#
# USAGE:
#   .\scripts\reset_configs.ps1                    # Back up, confirm, delete TOML and CSV files
#   .\scripts\reset_configs.ps1 -Force             # Back up and delete without the prompt
#   .\scripts\reset_configs.ps1 -KeepCsv           # Delete only TOML files
#   .\scripts\reset_configs.ps1 -WhatIf            # Show what would be backed up / deleted
#   .\scripts\reset_configs.ps1 -GamePath "D:\Games\SHCDE"
#
# PARAMETERS:
#   -GamePath    Override Steam game install path
#   -KeepCsv     Skip deletion of CSV damage matrix files
#   -BackupName  Name for the safety backup (default: pre-reset_<timestamp>)
#   -Force       Do not ask for confirmation (the backup still runs)
#

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [switch]$KeepCsv = $false,
    [string]$BackupName = "",
    [switch]$Force
)

$configDir = Join-Path $GamePath "BepInEx\config\CrusaderDETweaker"
$matrixDir = Join-Path $configDir "DamageMatrices"

if (-not (Test-Path $configDir)) {
    Write-Host "Config directory not found: $configDir" -ForegroundColor Yellow
    exit 0
}

if ($Force -and -not $PSBoundParameters.ContainsKey('Confirm')) { $ConfirmPreference = 'None' }

Write-Host "=== Resetting CrusaderDETweaker Configs ===" -ForegroundColor Cyan
Write-Host "Config dir: $configDir" -ForegroundColor Gray
Write-Host ""

$what = if ($KeepCsv) { "all TOML/cfg config files" } else { "all TOML/cfg config files and damage-matrix CSVs" }
if (-not $PSCmdlet.ShouldProcess($configDir, "Back up, then DELETE $what")) {
    Write-Host "Nothing deleted." -ForegroundColor Yellow
    exit 0
}

# Safety net first: no backup, no reset.
if ($BackupName -eq "") { $BackupName = "pre-reset_" + (Get-Date -Format "yyyy-MM-dd_HHmmss") }
& (Join-Path $PSScriptRoot "backup_configs.ps1") -GamePath $GamePath -Name $BackupName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Backup FAILED - nothing deleted." -ForegroundColor Red
    exit 1
}
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
Write-Host "Launch the game to regenerate all configs. Undo: .\scripts\restore_configs.ps1 -Name `"$BackupName`"" -ForegroundColor Gray
