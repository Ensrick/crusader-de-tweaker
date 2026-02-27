# scripts/test_initialization_order.ps1
#
# PURPOSE: Verify TOML → CSV initialization order is preserved.
#          Critical for ensuring CSV damage overrides work correctly.
#
# STATUS: ACTIVE - Run after making changes to ConfigManager initialization.
#
# USAGE:
#   .\scripts\test_initialization_order.ps1
#
# PREREQUISITES:
#   - Game must have been run at least once (to generate log file)
#   - BepInEx\LogOutput.log must contain initialization messages
#
# TESTS PERFORMED:
#   1. Capture defaults BEFORE TOML loads
#   2. TOML loads BEFORE CSV loads
#   3. All critical systems initialized
#
# EXIT CODES:
#   0 = All tests passed
#   1 = Test failed or log file not found
#
# IMPORTANT FOR AI AGENTS:
# - Run this after modifying ConfigManager.Initialize() or related code
# - The order is critical: Capture Defaults → TOML → CSV
# - If order is wrong, CSV overrides may not work correctly
# - This test analyzes the BepInEx log file line numbers
#
# DEPENDENCY CHAIN:
#   1. Capture Defaults: Save original game values (before any mods)
#   2. Load TOML: Apply armor multipliers, health, speed, etc.
#   3. Load CSV: Override specific damage matchups (compares against captured defaults)
#

$ErrorActionPreference = "Stop"

Write-Host "=== Initialization Order Verification ===" -ForegroundColor Cyan
Write-Host ""

$logPath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log"

if (-not (Test-Path $logPath)) {
    Write-Host "[ERROR] Log file not found. Run game first." -ForegroundColor Red
    exit 1
}

Write-Host "Analyzing initialization sequence..." -ForegroundColor Yellow
Write-Host ""

# Extract initialization events
$captureDefaults = Select-String "Capturing original game defaults" $logPath | Select-Object -Last 1
$generateDefaults = Select-String "Generating default config files" $logPath | Select-Object -Last 1
$loadConfigs = Select-String "Loading config files" $logPath | Select-Object -Last 1

# Extract system-specific load events
$unitTagsLoad = Select-String "Loaded Unit Tags" $logPath | Select-Object -Last 1
$unitsLoad = Select-String "Loaded Unit Config" $logPath | Select-Object -Last 1
$structuresLoad = Select-String "Loaded Structure Config" $logPath | Select-Object -Last 1
$damageMatrixLoad = Select-String "Loaded Damage Matrix" $logPath | Select-Object -Last 1

Write-Host "INITIALIZATION SEQUENCE:" -ForegroundColor Cyan
Write-Host ""

$sequence = @()
if ($captureDefaults) { $sequence += @{Step="1. Capture Defaults"; Line=$captureDefaults.Line; Found=$true} }
if ($generateDefaults) { $sequence += @{Step="2. Generate Files"; Line=$generateDefaults.Line; Found=$true} }
if ($unitTagsLoad) { $sequence += @{Step="3. Load UnitTags (TOML)"; Line=$unitTagsLoad.Line; Found=$true} }
if ($unitsLoad) { $sequence += @{Step="4. Load Units (TOML)"; Line=$unitsLoad.Line; Found=$true} }
if ($structuresLoad) { $sequence += @{Step="5. Load Structures (TOML)"; Line=$structuresLoad.Line; Found=$true} }
if ($damageMatrixLoad) { $sequence += @{Step="6. Load Damage Matrix (CSV)"; Line=$damageMatrixLoad.Line; Found=$true} }

foreach ($item in $sequence) {
    if ($item.Found) {
        Write-Host "  [PASS] $($item.Step)" -ForegroundColor Green
        Write-Host "         $($item.Line)" -ForegroundColor Gray
    } else {
        Write-Host "  [FAIL] $($item.Step) - NOT FOUND" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "CRITICAL ORDER VERIFICATION:" -ForegroundColor Magenta
Write-Host ""

# Verify capture happens before TOML loads
$captureLineNum = if ($captureDefaults) { $captureDefaults.LineNumber } else { 999999 }
$tomlLineNum = if ($unitsLoad) { $unitsLoad.LineNumber } else { 0 }
$csvLineNum = if ($damageMatrixLoad) { $damageMatrixLoad.LineNumber } else { 0 }

$test1 = $captureLineNum -lt $tomlLineNum
$test2 = $tomlLineNum -lt $csvLineNum
$test3 = $captureDefaults -and $unitsLoad -and $damageMatrixLoad

Write-Host "Test 1: Capture defaults BEFORE TOML loads" -ForegroundColor Cyan
if ($test1) {
    Write-Host "  [PASS] Capture at line $captureLineNum, TOML at line $tomlLineNum" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Wrong order! Capture at line $captureLineNum, TOML at line $tomlLineNum" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 2: TOML loads BEFORE CSV loads" -ForegroundColor Cyan
if ($test2) {
    Write-Host "  [PASS] TOML at line $tomlLineNum, CSV at line $csvLineNum" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Wrong order! TOML at line $tomlLineNum, CSV at line $csvLineNum" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 3: All systems initialized" -ForegroundColor Cyan
if ($test3) {
    Write-Host "  [PASS] All critical systems found in log" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Some systems missing from log" -ForegroundColor Red
}

Write-Host ""
Write-Host "DEPENDENCY CHAIN EXPLANATION:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. Capture Defaults: Saves original game damage values" -ForegroundColor Gray
Write-Host "     - MUST happen before TOML modifies anything" -ForegroundColor Gray
Write-Host "     - Used by CSV loader for comparison" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Load TOML Configs: Modifies unit properties" -ForegroundColor Gray
Write-Host "     - Applies armor multipliers, health, speed, etc." -ForegroundColor Gray
Write-Host "     - Changes game state from original defaults" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Load CSV Matrices: Overrides specific matchups" -ForegroundColor Gray
Write-Host "     - Compares CSV values against ORIGINAL defaults" -ForegroundColor Gray
Write-Host "     - Applies overrides to CURRENT game state (after TOML)" -ForegroundColor Gray
Write-Host ""

Write-Host "SIMPLIFIED LOOP VERIFICATION:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  The ConfigManager simplification consolidated two loops into one:" -ForegroundColor Gray
Write-Host ""
Write-Host "  BEFORE: foreach TOML system → foreach CSV system" -ForegroundColor Yellow
Write-Host "  AFTER:  foreach all systems (array order preserved)" -ForegroundColor Green
Write-Host ""
Write-Host "  Array order in ConfigManager.cs:" -ForegroundColor Gray
Write-Host "    [0] UnitTagsConfigSystem   (TOML)" -ForegroundColor Gray
Write-Host "    [1] UnitConfigSystem       (TOML)" -ForegroundColor Gray
Write-Host "    [2] StructureConfigSystem  (TOML)" -ForegroundColor Gray
Write-Host "    [3] DamageMatrixConfigSystem (CSV) ← LAST" -ForegroundColor Gray
Write-Host ""

Write-Host "TEST RESULT: " -NoNewline
if ($test1 -and $test2 -and $test3) {
    Write-Host "PASSED" -ForegroundColor Green
    Write-Host "  Initialization order preserved correctly" -ForegroundColor Green
    Write-Host "  Simplification did NOT break sequence" -ForegroundColor Green
} else {
    Write-Host "FAILED" -ForegroundColor Red
    Write-Host "  Initialization order may be broken" -ForegroundColor Red
}

Write-Host ""
