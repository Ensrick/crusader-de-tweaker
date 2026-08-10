# scripts/build.ps1
#
# PURPOSE: Build the CrusaderDETweaker BepInEx plugin using MSBuild.
#
# USAGE:
#   .\scripts\build.ps1                    # Standard release build
#   .\scripts\build.ps1 -Configuration Debug
#   .\scripts\build.ps1 -GamePath "D:\Games\SHCDE"
#
# PARAMETERS:
#   -Configuration  Build configuration (Release/Debug). Default: Release
#   -Deploy         [Unused - kept for compatibility]
#   -GamePath       Override Steam game install path
#
# OUTPUT:
#   DLL built directly to: {GamePath}\BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll
#   This matches VS 2022 behavior - no separate copy step needed.
#
# EXIT CODES:
#   0 = Build succeeded
#   1 = Build failed (check MSBuild output for errors)
#
# IMPORTANT FOR AI AGENTS:
# - This is the PRIMARY build script - use for all builds
# - Output goes directly to game folder (defined in .csproj OutputPath)
# - Cleans up old DLLs from deprecated folder locations
# - Requires Visual Studio 2022 MSBuild (checks Community/Pro/Enterprise paths)
# - Falls back to 'dotnet build' if MSBuild not found
# - After successful build, run launch_game.ps1 to test
#
# DEPENDENCIES:
# - Visual Studio 2022 with .NET Framework 4.8.1 targeting pack
# - NuGet packages (restored automatically)
# - Tomlyn 0.19.0, BepInEx 5.4.23.4
#

param(
    [string]$Configuration = "Release",
    [switch]$Deploy = $false,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building CrusaderDETweaker ===" -ForegroundColor Cyan
Write-Host ""

# Find MSBuild (VS 2022 uses MSBuild, not dotnet build for .NET Framework projects)
$msbuildPath = $null
$vsPaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

foreach ($path in $vsPaths) {
    if (Test-Path $path) {
        $msbuildPath = $path
        break
    }
}

if ($null -eq $msbuildPath) {
    Write-Host "MSBuild not found. Trying dotnet build..." -ForegroundColor Yellow
    dotnet build CrusaderDETweaker.csproj --configuration $Configuration -p:GameDir="$GamePath"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed! Please use Visual Studio 2022 or install MSBuild." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Gray
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray
    Write-Host ""
    
    # Restore packages
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    & $msbuildPath /t:Restore /p:Configuration=$Configuration "/p:GameDir=$GamePath" CrusaderDETweaker.csproj | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Package restore failed!" -ForegroundColor Red
        exit 1
    }
    
    # Build
    Write-Host "Building project..." -ForegroundColor Yellow
    & $msbuildPath /t:Build /p:Configuration=$Configuration "/p:GameDir=$GamePath" CrusaderDETweaker.csproj
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# ============================================================================================
# CRITICAL: Plugin GUID and Build Path Configuration
# ============================================================================================
# 
# Plugin GUID: "CrusaderDETweaker" (defined in Plugin.cs)
# Plugin folder: BepInEx/plugins/CrusaderDETweaker/
# DLL location: BepInEx/plugins/CrusaderDETweaker/CrusaderDETweaker.dll
# 
# BOTH Debug AND Release builds output to the SAME folder (this is intentional).
# BepInEx loads plugins from folders matching their GUID, so the folder name MUST match
# the plugin GUID exactly.
# 
# DO NOT:
# - Use different folders for Debug vs Release
# - Change the GUID without updating ALL build paths
# - Use "ensrick.crusaderdetweaker" (this was an old incorrect GUID)
# ============================================================================================

# Remove old DLL from wrong location (ensrick.crusaderdetweaker folder) to avoid conflicts
$oldDllPath = Join-Path $GamePath "BepInEx\plugins\ensrick.crusaderdetweaker\CrusaderDETweaker.dll"
if (Test-Path $oldDllPath) {
    Write-Host ""
    Write-Host "Removing old DLL from incorrect location..." -ForegroundColor Yellow
    Write-Host "  Old location: $oldDllPath" -ForegroundColor Gray
    Write-Host "  Reason: Plugin GUID changed from 'ensrick.crusaderdetweaker' to 'CrusaderDETweaker'" -ForegroundColor Gray
    Remove-Item $oldDllPath -Force -ErrorAction SilentlyContinue
    Write-Host "  Old DLL removed (was from old GUID structure)" -ForegroundColor Green
}

# DLL is built directly to game folder (matching VS 2022 behavior)
# Both Debug and Release builds output to the same location: plugins\CrusaderDETweaker\
# This MUST match the plugin GUID "CrusaderDETweaker" defined in Plugin.cs
$dllPath = Join-Path $GamePath "BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll"

if (Test-Path $dllPath) {
    $dllSize = (Get-Item $dllPath).Length
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "  DLL: $dllPath ($dllSize bytes)" -ForegroundColor Green
    Write-Host "  (Built directly to game folder, matching VS 2022 behavior)" -ForegroundColor Gray

    # Stamp info.json's Version from the single source of truth (PluginInfo.cs), then copy
    # it to the plugin folder. This keeps info.json, the BepInPlugin attribute, and the
    # assembly version in lockstep — the version is edited in exactly one place.
    $infoJsonSource = Join-Path $PSScriptRoot "..\info.json"
    $infoJsonDest = Join-Path $GamePath "BepInEx\plugins\CrusaderDETweaker\info.json"
    $pluginInfoPath = Join-Path $PSScriptRoot "..\PluginInfo.cs"
    if (Test-Path $infoJsonSource) {
        if (Test-Path $pluginInfoPath) {
            $verMatch = Select-String -Path $pluginInfoPath -Pattern 'PLUGIN_VERSION\s*=\s*"([^"]+)"'
            if ($verMatch) {
                $version = $verMatch.Matches[0].Groups[1].Value
                $info = Get-Content $infoJsonSource -Raw | ConvertFrom-Json
                if ($info.Version -ne $version) {
                    $info.Version = $version
                    ($info | ConvertTo-Json -Depth 10) | Set-Content $infoJsonSource -Encoding UTF8
                    Write-Host "  Stamped info.json Version = $version (from PluginInfo.cs)" -ForegroundColor Green
                }
            }
        }
        Copy-Item $infoJsonSource $infoJsonDest -Force
        Write-Host "  Copied info.json to plugin folder." -ForegroundColor Green
    }
} else {
    # Fallback: check bin\Release\ in case OutputPath wasn't applied
    $fallbackPath = "bin\$Configuration\CrusaderDETweaker.dll"
    if (Test-Path $fallbackPath) {
        Write-Host ""
        Write-Host "Build successful (fallback location)!" -ForegroundColor Green
        Write-Host "  DLL: $fallbackPath" -ForegroundColor Green
        Write-Host "  Note: DLL was built to bin\Release\ instead of game folder." -ForegroundColor Yellow
        Write-Host "  This suggests OutputPath in .csproj wasn't applied correctly." -ForegroundColor Yellow
        
        # Deploy to game folder if requested
        if ($Deploy) {
            Write-Host ""
            Write-Host "Deploying DLL to game folder..." -ForegroundColor Yellow
            $pluginPath = Join-Path $GamePath "BepInEx\plugins\CrusaderDETweaker"
            
            if (-not (Test-Path $pluginPath)) {
                New-Item -ItemType Directory -Path $pluginPath -Force | Out-Null
                Write-Host "  Created plugin directory: $pluginPath" -ForegroundColor Gray
            }
            
            Copy-Item $fallbackPath $dllPath -Force
            Write-Host "  Deployed to: $dllPath" -ForegroundColor Green
            Write-Host "  Deployment complete!" -ForegroundColor Green
        }
    } else {
        Write-Host "Build completed but DLL not found at expected path!" -ForegroundColor Red
        Write-Host "  Expected: $dllPath" -ForegroundColor Red
        Write-Host "  Fallback: $fallbackPath" -ForegroundColor Red
        exit 1
    }
}

