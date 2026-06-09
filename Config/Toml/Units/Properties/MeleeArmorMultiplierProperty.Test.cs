// Config/Toml/Units/Properties/MeleeArmorMultiplierProperty.Test.cs
//
// PURPOSE: Manual test verification for mismatch tracking reset functionality.
//
// HOW TO TEST:
// 1. Build the project
// 2. Launch the game (plugin will initialize and load configs)
// 3. Check BepInEx logs for "Applied melee armor" messages with mismatch counts
// 4. Manually trigger ConfigManager.ReloadAll() via console/script if available
// 5. Verify that mismatch counts reset to 0 (not accumulating from previous load)
//
// EXPECTED BEHAVIOR:
// - First load: Reports N mismatches (e.g., "5 mismatches")
// - After ReloadAll(): Reports same N mismatches (NOT 2N)
// - Mismatch summary shows correct count, not doubled
//
// AUTOMATED TEST STRATEGY (Future):
// If automated testing is added, this should verify:
// 1. AllMismatches.Count == 0 after ResetMismatchTracking()
// 2. TotalMismatchCount == 0 after ResetMismatchTracking()
// 3. Subsequent SetToAPI calls increment from 0, not previous values
// 4. LogMismatchSummary() uses correct counts after reset
//
// Note: This is a documentation file for manual testing procedures.
// C# doesn't have built-in unit test framework in this project structure.
// Consider adding NUnit or xUnit if automated testing is desired.

/*
MANUAL TEST PROCEDURE:

Test Case 1: Single Load
-------------------------
1. Delete all config files from BepInEx\config\
2. Launch game
3. Check log output for "Applied melee armor to {unit}: ×{multiplier} ({X} damage values, {Y} tag-modified, {Z} mismatches)"
4. Note the mismatch count Z for reference

Expected: Z should be a consistent number (e.g., 5-10 mismatches for default values)

Test Case 2: Config Reload
---------------------------
1. With game running (if hot-reload supported) or restart game
2. Observe "Applied melee armor" log messages again
3. Compare mismatch count Z with Test Case 1

Expected: Z should be SAME as Test Case 1 (not doubled)
Expected: "MELEE DAMAGE MISMATCH SUMMARY" should show same count

Test Case 3: Multiple Reloads
------------------------------
1. If ReloadAll() can be triggered multiple times, do so
2. Each time, verify mismatch count remains consistent

Expected: Count never increases beyond the actual formula mismatches

VERIFICATION CHECKLIST:
[ ] AllMismatches list clears before each config load
[ ] TotalMismatchCount resets to 0 before each config load
[ ] Mismatch counts are consistent across reloads
[ ] LogMismatchSummary() shows correct totals
[ ] No accumulation of old mismatch data

LOGGING TO CHECK:
- Look for: "Melee formula mismatch: {attacker}→{defender}: calc={X}, orig={Y}, diff={Z}"
- Count occurrences per load cycle
- Verify count matches "Total mismatches: X" in summary

KNOWN ISSUES BEFORE FIX:
- Without ResetMismatchTracking(), mismatches doubled on each reload
- Static lists accumulated data across multiple initializations
- LogMismatchSummary() showed incorrect totals

VERIFICATION AFTER FIX:
✓ ResetMismatchTracking() called at start of ApplyAllUnitConfigs()
✓ Static lists clear before processing begins
✓ Mismatch counts consistent across reloads
*/
