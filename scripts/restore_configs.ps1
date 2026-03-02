# scripts/restore_configs.ps1
#
# PURPOSE: Restore CrusaderDETweaker config files from a previously created backup.
#
# USAGE:
#   .\scripts\restore_configs.ps1                       # List available backups
#   .\scripts\restore_configs.ps1 -Name "pre-v2.2"      # Restore named backup
#   .\scripts\restore_configs.ps1 -GamePath "D:\Games\SHCDE"
#
# PARAMETERS:
#   -Name       Name of the backup to restore (omit to list available backups)
#   -GamePath   Override Steam game install path
#

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$Name = ""
)

$configDir  = Join-Path $GamePath "BepInEx\config\CrusaderDETweaker"
$matrixDir  = Join-Path $configDir "DamageMatrices"
$backupRoot = Join-Path $configDir "Backups"

if (-not (Test-Path $backupRoot)) {
    Write-Host "No backups found. Run backup_configs.ps1 first." -ForegroundColor Yellow
    exit 0
}

# List mode
if ($Name -eq "") {
    $backups = Get-ChildItem -Path $backupRoot -Directory | Sort-Object Name
    if ($backups.Count -eq 0) {
        Write-Host "No backups found in: $backupRoot" -ForegroundColor Yellow
    } else {
        Write-Host "=== Available Backups ===" -ForegroundColor Cyan
        foreach ($b in $backups) {
            $fileCount = (Get-ChildItem $b.FullName -Recurse -File).Count
            Write-Host ("  {0,-30} ({1} files)" -f $b.Name, $fileCount) -ForegroundColor White
        }
        Write-Host ""
        Write-Host "Restore with: .\scripts\restore_configs.ps1 -Name `"<name>`"" -ForegroundColor Gray
    }
    exit 0
}

$src = Join-Path $backupRoot $Name

if (-not (Test-Path $src)) {
    Write-Host "Backup not found: $src" -ForegroundColor Red
    Write-Host "Run without -Name to list available backups." -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Restoring CrusaderDETweaker Configs ===" -ForegroundColor Cyan
Write-Host "Backup name: $Name" -ForegroundColor White
Write-Host "Source:      $src" -ForegroundColor Gray
Write-Host ""

New-Item -ItemType Directory -Path $matrixDir -Force | Out-Null

$copied  = 0
$missing = 0

# Restore root config files
foreach ($file in (Get-ChildItem -Path $src -File)) {
    $dest = Join-Path $configDir $file.Name
    Copy-Item $file.FullName $dest -Force
    Write-Host "  Restored: $($file.Name)" -ForegroundColor Green
    $copied++
}

# Restore DamageMatrices subfolder
$srcMatrix = Join-Path $src "DamageMatrices"
if (Test-Path $srcMatrix) {
    foreach ($file in (Get-ChildItem -Path $srcMatrix -File)) {
        $dest = Join-Path $matrixDir $file.Name
        Copy-Item $file.FullName $dest -Force
        Write-Host "  Restored: DamageMatrices\$($file.Name)" -ForegroundColor Green
        $copied++
    }
} else {
    Write-Host "  No DamageMatrices in this backup." -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done. Restored=$copied files from '$Name'." -ForegroundColor Cyan
Write-Host "Launch the game for changes to take effect." -ForegroundColor Gray
