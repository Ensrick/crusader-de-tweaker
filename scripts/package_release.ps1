# scripts/package_release.ps1
#
# PURPOSE: Stage the latest plugin + config files and pack them into the release zip.
#
# USAGE:
#   .\scripts\package_release.ps1
#   .\scripts\package_release.ps1 -GamePath "D:\Games\SHCDE"
#   .\scripts\package_release.ps1 -StagingPath "E:\Mods\Crusader DE Tweaker"
#
# PARAMETERS:
#   -GamePath     Override Steam game install path
#   -StagingPath  Override staging/release folder path
#

param(
    [string]$GamePath    = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$StagingPath = "D:\Game Mods\Stronghold\Crusader DE Tweaker"
)

$gamePlugin = Join-Path $GamePath    "BepInEx\plugins\CrusaderDETweaker"
$gameConfig = Join-Path $GamePath    "BepInEx\config\CrusaderDETweaker"

$stagingBepInEx      = Join-Path $StagingPath "BepInEx"
$stagingPlugin       = Join-Path $StagingPath "BepInEx\plugins\CrusaderDETweaker"
$stagingConfig       = Join-Path $StagingPath "BepInEx\config\CrusaderDETweaker"
$zipPath             = Join-Path $StagingPath "Crusader DE Tweaker.zip"

Write-Host "=== CrusaderDETweaker Package Release ===" -ForegroundColor Cyan
Write-Host ""

# --- Validate sources ---
if (-not (Test-Path $gamePlugin)) {
    Write-Host "Plugin folder not found: $gamePlugin" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $gameConfig)) {
    Write-Host "Config folder not found: $gameConfig" -ForegroundColor Red
    exit 1
}

# --- Copy plugin folder ---
Write-Host "Copying plugin..." -ForegroundColor White
if (Test-Path $stagingPlugin) {
    Remove-Item $stagingPlugin -Recurse -Force
}
Copy-Item $gamePlugin $stagingPlugin -Recurse -Force
Write-Host "  plugins\CrusaderDETweaker\" -ForegroundColor Green

# --- Copy config folder (excluding Backups subfolder) ---
Write-Host "Copying config..." -ForegroundColor White
if (Test-Path $stagingConfig) {
    Remove-Item $stagingConfig -Recurse -Force
}
Copy-Item $gameConfig $stagingConfig -Recurse -Force

# Remove Backups folder if it got copied (user-specific, not for distribution)
$stagingBackups = Join-Path $stagingConfig "Backups"
if (Test-Path $stagingBackups) {
    Remove-Item $stagingBackups -Recurse -Force
    Write-Host "  (excluded Backups subfolder)" -ForegroundColor Gray
}
Write-Host "  config\CrusaderDETweaker\" -ForegroundColor Green

# --- Pack BepInEx folder into zip ---
Write-Host ""
Write-Host "Packing zip..." -ForegroundColor White

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Compress-Archive needs the contents to sit under BepInEx\ in the zip.
# We compress the BepInEx folder itself so the zip root contains BepInEx\.
Compress-Archive -Path $stagingBepInEx -DestinationPath $zipPath -CompressionLevel Optimal

$zipSize = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "  $zipPath ($zipSize KB)" -ForegroundColor Green

# --- Copy Nexus/Workshop BBCode docs next to the zip for copy/paste during upload ---
Write-Host ""
Write-Host "Copying upload docs..." -ForegroundColor White
$repoRoot = Split-Path $PSScriptRoot -Parent
Copy-Item (Join-Path $repoRoot "NEXUS_DESCRIPTION.md")   (Join-Path $StagingPath "NEXUS_DESCRIPTION.txt")   -Force
Copy-Item (Join-Path $repoRoot "CONFIGURATION_GUIDE.md") (Join-Path $StagingPath "CONFIGURATION_GUIDE.txt") -Force
Write-Host "  NEXUS_DESCRIPTION.txt + CONFIGURATION_GUIDE.txt (BBCode, paste into the Nexus pages)" -ForegroundColor Green

Write-Host ""
Write-Host "Done. Users can extract '$([System.IO.Path]::GetFileName($zipPath))' directly into their game folder." -ForegroundColor Cyan
