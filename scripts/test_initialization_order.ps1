# scripts/test_initialization_order.ps1
#
# Verifies the startup-safe config sequence from the latest BepInEx log:
#   1. Generate every config file (file I/O only).
#   2. Load Global, Unit, Structure, then Damage Matrix configs.
#   3. Complete without per-system generation or load failures.

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
)

$ErrorActionPreference = "Stop"
$logPath = Join-Path $GamePath "BepInEx\LogOutput.log"

Write-Host "=== Initialization Order Verification ===" -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $logPath)) {
    Write-Host "[FAIL] Log file not found: $logPath" -ForegroundColor Red
    exit 1
}

function Find-LastMarker {
    param([string]$Text)
    Select-String -LiteralPath $logPath -SimpleMatch $Text | Select-Object -Last 1
}

$markers = [ordered]@{
    "Generate Global"    = Find-LastMarker "[DIAG] GenerateDefaults: Global Config..."
    "Generate Unit"      = Find-LastMarker "[DIAG] GenerateDefaults: Unit Config..."
    "Generate Structure" = Find-LastMarker "[DIAG] GenerateDefaults: Structure Config..."
    "Generate Damage"    = Find-LastMarker "[DIAG] GenerateDefaults: Damage Matrix..."
    "Load Global"        = Find-LastMarker "[DIAG] Load: Global Config..."
    "Load Unit"          = Find-LastMarker "[DIAG] Load: Unit Config..."
    "Load Structure"     = Find-LastMarker "[DIAG] Load: Structure Config..."
    "Load Damage"        = Find-LastMarker "[DIAG] Load: Damage Matrix..."
    "Initialized"        = Find-LastMarker "Initialized 4 config systems (4 loaded, 4 validated)"
}

$missing = @($markers.GetEnumerator() | Where-Object { $null -eq $_.Value })
foreach ($entry in $markers.GetEnumerator()) {
    if ($entry.Value) {
        Write-Host ("[PASS] {0} (line {1})" -f $entry.Key, $entry.Value.LineNumber) -ForegroundColor Green
    } else {
        Write-Host ("[FAIL] {0} marker missing" -f $entry.Key) -ForegroundColor Red
    }
}

$orderedNames = @(
    "Generate Global",
    "Generate Unit",
    "Generate Structure",
    "Generate Damage",
    "Load Global",
    "Load Unit",
    "Load Structure",
    "Load Damage",
    "Initialized"
)

$orderIsValid = $missing.Count -eq 0
if ($orderIsValid) {
    for ($i = 1; $i -lt $orderedNames.Count; $i++) {
        $previous = $markers[$orderedNames[$i - 1]].LineNumber
        $current = $markers[$orderedNames[$i]].LineNumber
        if ($current -le $previous) {
            $orderIsValid = $false
            break
        }
    }
}

$failures = @(
    Select-String -LiteralPath $logPath -Pattern "Failed to generate defaults for|Failed to load .* Config|Validation failed for"
)

if ($orderIsValid -and $failures.Count -eq 0) {
    Write-Host "[PASS] Config generation and load order is correct." -ForegroundColor Green
    exit 0
}

if (-not $orderIsValid) {
    Write-Host "[FAIL] Config systems did not initialize in the required order." -ForegroundColor Red
}
foreach ($failure in $failures) {
    Write-Host "[FAIL] $($failure.Line)" -ForegroundColor Red
}
exit 1
