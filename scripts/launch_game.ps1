# scripts/launch_game.ps1
# Launches game through Steam, FORCES WINDOWED MODE, keeps minimized, auto-closes after init

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [int]$SteamAppId = 3024040
)

Write-Host "=== CrusaderDETweaker Test Launch ===" -ForegroundColor Cyan

# FORCE WINDOWED MODE - fullscreen can't be minimized
$settingsPath = "$env:APPDATA\..\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\settings.cfg"
$originalFullscreen = $null
if (Test-Path $settingsPath) {
    $content = Get-Content $settingsPath -Raw
    if ($content -match "FullscreenType:(\d+)") {
        $originalFullscreen = $matches[1]
        if ($originalFullscreen -ne "0") {
            $content = $content -replace "FullscreenType:\d+", "FullscreenType:0"
            Set-Content $settingsPath $content -NoNewline
            Write-Host "Forced windowed mode (was fullscreen type $originalFullscreen)" -ForegroundColor Yellow
        }
    }
}

# Win32 API for aggressive window minimization
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    
    public const int SW_MINIMIZE = 6;
    public const int SW_HIDE = 0;
    public const int SW_FORCEMINIMIZE = 11;
    public const int SW_SHOWMINNOACTIVE = 7;
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_HIDEWINDOW = 0x0080;
}
"@

# Function to minimize game window
function Minimize-GameWindow {
    param([int]$ProcessId)
    $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) {
        [Win32]::ShowWindow($proc.MainWindowHandle, [Win32]::SW_FORCEMINIMIZE) | Out-Null
        [Win32]::SetWindowPos($proc.MainWindowHandle, [Win32]::HWND_BOTTOM, 0, 0, 0, 0, 
            [Win32]::SWP_NOMOVE -bor [Win32]::SWP_NOSIZE -bor [Win32]::SWP_NOACTIVATE) | Out-Null
    }
}

# Delete marker file
$markerPath = Join-Path $env:APPDATA "BepInEx\config\CrusaderDETweaker\plugin_initialized.ready"
Remove-Item $markerPath -Force -ErrorAction SilentlyContinue

# 2. Launch game through Steam
Write-Host "Launching game through Steam..." -ForegroundColor Yellow
Start-Process "steam://run/$SteamAppId"

trap {
    # Cleanup any background jobs if script is interrupted (Ctrl+C, error, etc)
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
    Write-Host "[Cleanup] Removed all background jobs." -ForegroundColor Yellow
    break
}

# 3. Wait for game process with aggressive minimization during wait
Write-Host "Waiting for game process..." -ForegroundColor Yellow
$process = $null
$gameProcessName = "Stronghold Crusader Definitive Edition"

# 3. Wait for game process
Write-Host "Waiting for game process..." -ForegroundColor Yellow
$process = $null
$gameProcessName = "Stronghold Crusader Definitive Edition"

for ($i = 0; $i -lt 90; $i++) {
    $process = Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($process) {
        Write-Host "Game started (PID: $($process.Id))" -ForegroundColor Green
        Minimize-GameWindow -ProcessId $process.Id
        break
    }
    Start-Sleep -Milliseconds 500
}

if (-not $process) {
    Write-Host "Game did not start within 90 seconds" -ForegroundColor Red
    exit 1
}

# 4. Wait for plugin initialization
Write-Host "Waiting for plugin init..." -ForegroundColor Yellow
$logPath = Join-Path $GamePath "BepInEx\LogOutput.log"
$startTime = Get-Date

while ($true) {
    # Refresh process
    $process = Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    
    # Check if process exited
    if (-not $process) {
        Write-Host "Game exited unexpectedly" -ForegroundColor Yellow
        break
    }
    
    # Minimize occasionally to ensure it stays down
    Minimize-GameWindow -ProcessId $process.Id
    
    # Check if plugin initialized
    if (Test-Path $markerPath) {
        Write-Host "Plugin initialized!" -ForegroundColor Green
        
        # Kill the game immediately
        Write-Host "Terminating game..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        break
    }
    
    # Timeout after 5 minutes
    if (((Get-Date) - $startTime).TotalSeconds -gt 300) {
        Write-Host "Timeout - killing game" -ForegroundColor Red
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        break
    }
    
    Start-Sleep -Milliseconds 500
}

# 5. Show results
Write-Host ""
Write-Host "=== Results ===" -ForegroundColor Cyan

if (Test-Path $logPath) {
    # Show mismatch summary
    $summary = Select-String -Path $logPath -Pattern "Total mismatches:" | Select-Object -Last 1
    if ($summary) { Write-Host $summary.Line -ForegroundColor Yellow }
    
    $ranged = Select-String -Path $logPath -Pattern "No ranged formula mismatches" | Select-Object -Last 1
    if ($ranged) { Write-Host "Ranged: 0 mismatches" -ForegroundColor Green }
}

# Restore original fullscreen setting
if ($originalFullscreen -and $originalFullscreen -ne "0") {
    $content = Get-Content $settingsPath -Raw
    $content = $content -replace "FullscreenType:0", "FullscreenType:$originalFullscreen"
    Set-Content $settingsPath $content -NoNewline
    Write-Host "Restored fullscreen setting to $originalFullscreen" -ForegroundColor Cyan
}

Write-Host "Done." -ForegroundColor Green

