# scripts/package_release.ps1
#
# PURPOSE: Stage the latest plugin files and pack them into the release zip.
#
# NOTE: Config files are deliberately NOT shipped (changed in v2.4.1). The old
# pipeline zipped freshly-regenerated pristine configs, but a user upgrading by
# extracting the zip over their install would overwrite their personalized
# configs with those defaults — losing their settings and contradicting the
# Nexus description's "your existing config values are kept automatically".
# The plugin generates defaults on first launch and migrates existing files on
# every update, so shipping configs buys nothing and risks data loss. This also
# retires the old reset -> launch -> restore steps in release.ps1.
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

$stagingBepInEx      = Join-Path $StagingPath "BepInEx"
$stagingPlugin       = Join-Path $StagingPath "BepInEx\plugins\CrusaderDETweaker"
$stagingConfig       = Join-Path $StagingPath "BepInEx\config"
$zipPath             = Join-Path $StagingPath "Crusader DE Tweaker.zip"

Write-Host "=== CrusaderDETweaker Package Release ===" -ForegroundColor Cyan
Write-Host ""

# --- Validate sources ---
if (-not (Test-Path $gamePlugin)) {
    Write-Host "Plugin folder not found: $gamePlugin" -ForegroundColor Red
    exit 1
}

# --- Refresh plugin staging (rename previous copy aside; never recursive-delete) ---
Write-Host "Copying plugin..." -ForegroundColor White
if (Test-Path $stagingPlugin) {
    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    Rename-Item $stagingPlugin "CrusaderDETweaker.bak.$ts"
    Write-Host "  (previous staging renamed to CrusaderDETweaker.bak.$ts - clear old .bak dirs manually when convenient)" -ForegroundColor Gray
}
Copy-Item $gamePlugin $stagingPlugin -Recurse -Force
Write-Host "  plugins\CrusaderDETweaker\" -ForegroundColor Green

# --- Strip dev-only artifacts: debug symbols (.pdb) and library XML docs are never
#     needed at runtime and only bloat the end-user download (the .pdb alone is ~0.5 MB). ---
$stripped = Get-ChildItem $stagingPlugin -Recurse -Include '*.pdb', '*.xml' -File -ErrorAction SilentlyContinue
foreach ($f in $stripped) {
    Remove-Item $f.FullName -Force
    Write-Host "  (stripped $($f.Name) - dev artifact, not shipped)" -ForegroundColor Gray
}

# --- Guard: configs must not ship ---
if (Test-Path $stagingConfig) {
    Write-Host "Staging still contains a BepInEx\config folder - it would ship personal settings to users." -ForegroundColor Red
    Write-Host "Move it out of BepInEx\ (e.g. rename to ..\config.bak.<date>) and re-run." -ForegroundColor Red
    exit 1
}

# --- Guard: renamed .bak plugin dirs must not ship either ---
$bakDirs = Get-ChildItem (Join-Path $stagingBepInEx 'plugins') -Directory -Filter '*.bak.*' -ErrorAction SilentlyContinue
if ($bakDirs) {
    foreach ($d in $bakDirs) {
        Move-Item $d.FullName (Join-Path $StagingPath $d.Name)
        Write-Host "  (moved $($d.Name) out of the zip tree)" -ForegroundColor Gray
    }
}

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
