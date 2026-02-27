# scripts/check_game_running.ps1
#
# PURPOSE: Check if Stronghold Crusader Definitive Edition is currently running.
#
# USAGE:
#   .\check_game_running.ps1
#
# EXIT CODES:
#   0 = Game is running (outputs PID)
#   1 = Game is NOT running
#
# IMPORTANT FOR AI AGENTS:
# - Use this before launch_game.ps1 to avoid duplicate instances
# - Useful for build scripts that need to know if hot-reload is possible
# - Simple utility - no side effects, just checks process state
#
# Check if Stronghold Crusader Definitive Edition is running
$processName = "Stronghold Crusader Definitive Edition"
$process = Get-Process -Name $processName -ErrorAction SilentlyContinue

if ($process) {
    Write-Host "Game is RUNNING (PID: $($process.Id))" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Game is NOT running" -ForegroundColor Red
    exit 1
}

