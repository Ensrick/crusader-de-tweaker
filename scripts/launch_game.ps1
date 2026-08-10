# scripts/launch_game.ps1
# Launches game through Steam, FORCES WINDOWED MODE, keeps minimized, auto-closes after init

param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [int]$SteamAppId = 3024040,
    [int]$ProcessTimeoutSeconds = 90,
    [int]$InitializationTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"

Write-Host "=== CrusaderDETweaker Test Launch ===" -ForegroundColor Cyan

# Force windowed mode because fullscreen cannot be minimized reliably.
$settingsPath = "$env:APPDATA\..\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\settings.cfg"
$originalFullscreen = $null
$fullscreenChanged = $false
$process = $null
$testPassed = $false
$exitCode = 1

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

try {
    if (Test-Path $settingsPath) {
        $content = Get-Content $settingsPath -Raw
        if ($content -match "FullscreenType:(\d+)") {
            $originalFullscreen = $matches[1]
            if ($originalFullscreen -ne "0") {
                $content = $content -replace "FullscreenType:\d+", "FullscreenType:0"
                Set-Content $settingsPath $content -NoNewline
                $fullscreenChanged = $true
                Write-Host "Forced windowed mode (was fullscreen type $originalFullscreen)" -ForegroundColor Yellow
            }
        }
    }

    $markerPath = Join-Path $env:APPDATA "BepInEx\config\CrusaderDETweaker\plugin_initialized.ready"
    Remove-Item $markerPath -Force -ErrorAction SilentlyContinue

    Write-Host "Launching game through Steam..." -ForegroundColor Yellow
    $steamExe = Get-Process -Name "steam" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Path
    if (-not $steamExe) {
        $steamPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction SilentlyContinue).SteamPath
        if ($steamPath) {
            $steamExe = Join-Path $steamPath "steam.exe"
        }
    }

    if ($steamExe -and (Test-Path -LiteralPath $steamExe)) {
        Start-Process -FilePath $steamExe -ArgumentList "-applaunch", $SteamAppId
    }
    else {
        Start-Process "steam://run/$SteamAppId"
    }

    Write-Host "Waiting for game process..." -ForegroundColor Yellow
    $gameProcessName = "Stronghold Crusader Definitive Edition"
    $processDeadline = (Get-Date).AddSeconds($ProcessTimeoutSeconds)
    while ((Get-Date) -lt $processDeadline) {
        $process = Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($process) {
            Write-Host "Game started (PID: $($process.Id))" -ForegroundColor Green
            Minimize-GameWindow -ProcessId $process.Id
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $process) {
        throw "Game did not start within $ProcessTimeoutSeconds seconds"
    }

    Write-Host "Waiting for plugin init..." -ForegroundColor Yellow
    $logPath = Join-Path $GamePath "BepInEx\LogOutput.log"
    $initializationDeadline = (Get-Date).AddSeconds($InitializationTimeoutSeconds)

    while ((Get-Date) -lt $initializationDeadline) {
        $process = Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $process) {
            throw "Game exited before the plugin initialized"
        }

        Minimize-GameWindow -ProcessId $process.Id

        if (Test-Path $markerPath) {
            Write-Host "Plugin initialized!" -ForegroundColor Green
            $testPassed = $true
            $exitCode = 0
            break
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $testPassed) {
        Write-Host "Plugin did not initialize within $InitializationTimeoutSeconds seconds" -ForegroundColor Red
    }

    Write-Host "Terminating game..." -ForegroundColor Yellow
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "=== Results ===" -ForegroundColor Cyan

    if (Test-Path $logPath) {
        $summary = Select-String -Path $logPath -Pattern "Total mismatches:" | Select-Object -Last 1
        if ($summary) { Write-Host $summary.Line -ForegroundColor Yellow }

        $ranged = Select-String -Path $logPath -Pattern "No ranged formula mismatches" | Select-Object -Last 1
        if ($ranged) { Write-Host "Ranged: 0 mismatches" -ForegroundColor Green }
    }
}
finally {
    if ($process -and -not $process.HasExited -and -not $testPassed) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    if ($fullscreenChanged -and (Test-Path $settingsPath)) {
        $content = Get-Content $settingsPath -Raw
        $content = $content -replace "FullscreenType:0", "FullscreenType:$originalFullscreen"
        Set-Content $settingsPath $content -NoNewline
        Write-Host "Restored fullscreen setting to $originalFullscreen" -ForegroundColor Cyan
    }
}

if ($testPassed) {
    Write-Host "Test passed." -ForegroundColor Green
}
exit $exitCode

