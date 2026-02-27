# scripts/test_reload_tracking.ps1
#
# PURPOSE: Verify mismatch tracking reset works correctly during config reload.
#          Ensures mismatch counts don't accumulate across reload cycles.
#
# STATUS: ACTIVE - Run after making changes to mismatch tracking logic.
#
# USAGE:
#   .\scripts\test_reload_tracking.ps1
#
# PREREQUISITES:
#   - Game must have been run at least once (to generate log file)
#   - BepInEx\LogOutput.log must contain mismatch messages
#
# TESTS PERFORMED:
#   1. Parse mismatch counts from "Applied melee armor" messages
#   2. Check for consistent counts across reload cycles
#   3. Verify no accumulation of old mismatch data
#
# EXIT CODES:
#   0 = Test passed or single load detected
#   1 = Log file not found
#
# IMPORTANT FOR AI AGENTS:
# - Tests the ResetMismatchTracking() functionality
# - If counts increase across reloads, the reset is not working
# - Expected: Same mismatch count on every reload cycle
# - Related code: MeleeArmorMultiplierProperty.cs, ConfigLoader.cs
#
# WHAT WAS FIXED:
# - Before fix: Mismatch counts doubled on each reload (static lists accumulated)
# - After fix: ResetMismatchTracking() clears lists before each load cycle
#

$ErrorActionPreference = "Stop"

Write-Host "=== Mismatch Tracking Reset Test ===" -ForegroundColor Cyan
Write-Host ""

# Path to BepInEx log file
$logPath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log"

# Check if log exists
if (-not (Test-Path $logPath)) {
    Write-Host "Log file not found. Game needs to run at least once." -ForegroundColor Yellow
    Write-Host "Expected path: $logPath" -ForegroundColor Gray
    exit 1
}

Write-Host "Analyzing BepInEx log for mismatch patterns..." -ForegroundColor Yellow
Write-Host ""

# Get all log lines related to mismatch tracking
$mismatchLines = Get-Content $logPath | Select-String "Melee formula mismatch:|Applied melee armor to|MELEE DAMAGE MISMATCH SUMMARY|Total mismatches:"

if ($mismatchLines.Count -eq 0) {
    Write-Host "No mismatch data found in log. This could mean:" -ForegroundColor Yellow
    Write-Host "  1. No mismatches exist (all formulas match game defaults)" -ForegroundColor Gray
    Write-Host "  2. Game hasn't been run since rebuild" -ForegroundColor Gray
    Write-Host "  3. MeleeArmorMultiplier property wasn't processed" -ForegroundColor Gray
    exit 0
}

Write-Host "Found $($mismatchLines.Count) mismatch-related log entries" -ForegroundColor Green
Write-Host ""

# Parse mismatch counts from "Applied melee armor" messages
$armorApplications = $mismatchLines | Select-String "Applied melee armor to .* \(.*\s(\d+) mismatches\)"

if ($armorApplications.Count -gt 0) {
    Write-Host "Mismatch counts per unit:" -ForegroundColor Cyan
    
    $totalMismatchesPerLoad = @()
    $currentLoadTotal = 0
    
    foreach ($line in $armorApplications) {
        if ($line -match "Applied melee armor to (\w+):.*?(\d+) mismatches") {
            $unit = $matches[1]
            $count = [int]$matches[2]
            $currentLoadTotal += $count
            Write-Host "  $unit : $count mismatches" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "Total mismatches in this session: $currentLoadTotal" -ForegroundColor Yellow
}

# Check for summary messages
$summaryLines = $mismatchLines | Select-String "Total mismatches: (\d+)"

if ($summaryLines.Count -gt 0) {
    Write-Host ""
    Write-Host "Mismatch Summary Reports:" -ForegroundColor Cyan
    
    $counts = @()
    foreach ($line in $summaryLines) {
        if ($line -match "Total mismatches: (\d+)") {
            $count = [int]$matches[1]
            $counts += $count
            Write-Host "  Total: $count" -ForegroundColor Gray
        }
    }
    
    if ($counts.Count -gt 1) {
        Write-Host ""
        Write-Host "RELOAD TEST ANALYSIS:" -ForegroundColor Magenta
        
        $allSame = ($counts | Select-Object -Unique).Count -eq 1
        
        if ($allSame) {
            Write-Host "  ✓ PASSED: All reload cycles show same mismatch count ($($counts[0]))" -ForegroundColor Green
            Write-Host "  ✓ Reset is working correctly - no accumulation detected" -ForegroundColor Green
        } else {
            Write-Host "  ✗ FAILED: Mismatch counts vary across reloads" -ForegroundColor Red
            Write-Host "  Counts: $($counts -join ', ')" -ForegroundColor Red
            Write-Host "  This suggests reset may not be working correctly" -ForegroundColor Red
        }
    } else {
        Write-Host ""
        Write-Host "Single load detected - cannot verify reload behavior" -ForegroundColor Yellow
        Write-Host "To test reload:" -ForegroundColor Gray
        Write-Host "  1. Keep game running" -ForegroundColor Gray
        Write-Host "  2. Modify a config file" -ForegroundColor Gray
        Write-Host "  3. Trigger reload (if hot-reload supported)" -ForegroundColor Gray
        Write-Host "  4. Re-run this test script" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "MANUAL VERIFICATION STEPS:" -ForegroundColor Cyan
Write-Host "1. Check the counts above are consistent" -ForegroundColor Gray
Write-Host "2. If testing reload, restart game and compare counts" -ForegroundColor Gray
Write-Host "3. Counts should NOT double with each load" -ForegroundColor Gray
Write-Host ""

# Check for the reset call in log (if debug logging enabled)
$resetCalls = Get-Content $logPath | Select-String "Reset|mismatch.*track" -Context 0,1

if ($resetCalls.Count -gt 0) {
    Write-Host "Reset tracking evidence found:" -ForegroundColor Cyan
    Write-Host "  $($resetCalls.Count) potential reset-related log entries" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Test complete. Review results above." -ForegroundColor Green
