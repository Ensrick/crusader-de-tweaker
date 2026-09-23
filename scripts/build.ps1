# scripts/build.ps1
#
# PURPOSE: Build the CrusaderDETweaker BepInEx plugin using MSBuild. Building never writes into the
#          game folder; -Deploy hands the result to deploy.ps1 afterwards.
#
# USAGE:
#   .\scripts\build.ps1                          # Release build -> bin\Release\
#   .\scripts\build.ps1 -Configuration Debug     # -> bin\Debug\
#   .\scripts\build.ps1 -Deploy                  # build, then .\scripts\deploy.ps1
#   .\scripts\build.ps1 -OutputPath dist\x -Rebuild -ShcdeseDir <extracted SE>\BepInEx\plugins\000shcdese
#
# PARAMETERS:
#   -Configuration  Build configuration (Release/Debug). Default: Release
#   -Deploy         After a successful build, run deploy.ps1 (copies the output into the game's
#                   BepInEx\plugins\CrusaderDETweaker\; refuses while the game runs; never touches config)
#   -GamePath       Game install used for the reference assemblies (read-only) and by -Deploy
#   -ShcdeseDir     Compile against this SHCDE-SE folder instead of the installed one
#   -OutputPath     Output folder (default bin\<Configuration>\). Relative paths are repo-relative.
#   -Rebuild        Full rebuild instead of an incremental build (used for release packaging)
#
# OUTPUT:
#   <OutputPath>\ = a complete plugin folder: CrusaderDETweaker.dll, Tomlyn.dll, info.json,
#   Override\Assets\GUI\Sprites\CrusaderDETweaker.png (+ .pdb / Tomlyn.xml dev artifacts).
#   info.json in the OUTPUT is stamped with PluginInfo.PLUGIN_VERSION; the tracked info.json is never
#   rewritten (a mismatch is reported here and fails package_release.ps1 / ship.ps1 preflight).
#
# EXIT CODES:
#   0 = Build (and deploy, if requested) succeeded
#   1 = Build or deploy failed
#
# IMPORTANT FOR AI AGENTS:
# - This is the PRIMARY build script - use for all builds
# - Requires Visual Studio 2022 MSBuild (Community/Pro/Enterprise); falls back to 'dotnet build'
# - After a build, deploy with deploy.ps1 (or -Deploy) and test with launch_game.ps1
#
# DEPENDENCIES:
# - Visual Studio 2022 with .NET Framework 4.8.1 targeting pack
# - NuGet packages (restored automatically)
# - Tomlyn 0.19.0, BepInEx 5.4.23.4
#

param(
    [string]$Configuration = "Release",
    [switch]$Deploy = $false,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$ShcdeseDir = "",
    [string]$OutputPath = "",
    [switch]$Rebuild = $false
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_release_common.ps1')

# Build THIS checkout, whatever the caller's current directory is. MSBuild is invoked with a
# relative "CrusaderDETweaker.csproj", so without this a second checkout (e.g. a git worktree)
# silently built the other tree (2026-09-07). Push/Pop so a calling script keeps its location.
Push-Location $script:RepoRoot
try {
    if (-not $OutputPath) { $OutputPath = "bin\$Configuration" }
    if (-not [IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $script:RepoRoot $OutputPath }
    # No trailing backslash: it would escape the closing quote PowerShell adds around a path with
    # spaces. MSBuild appends the slash itself (EnsureTrailingSlash).
    $OutputPath = $OutputPath.TrimEnd('\')

    Write-Host "=== Building CrusaderDETweaker ===" -ForegroundColor Cyan
    Write-Host "  Project: $script:RepoRoot" -ForegroundColor Gray
    Write-Host "  Output : $OutputPath" -ForegroundColor Gray
    if ($ShcdeseDir) { Write-Host "  SHCDE-SE: $ShcdeseDir" -ForegroundColor Gray }
    Write-Host ""

    $props = @("/p:Configuration=$Configuration", "/p:GameDir=$GamePath", "/p:OutputPath=$OutputPath")
    if ($ShcdeseDir) { $props += "/p:ShcdeseDir=$ShcdeseDir" }
    $target = if ($Rebuild) { 'Rebuild' } else { 'Build' }

    $msbuildPath = Find-MSBuild
    if ($null -eq $msbuildPath) {
        Write-Host "MSBuild not found. Trying dotnet build..." -ForegroundColor Yellow
        $dnProps = $props | ForEach-Object { $_ -replace '^/p:', '-p:' }
        dotnet build CrusaderDETweaker.csproj --configuration $Configuration @dnProps
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed! Please use Visual Studio 2022 or install MSBuild." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Gray
        Write-Host "Configuration: $Configuration ($target)" -ForegroundColor Gray
        Write-Host ""

        Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
        & $msbuildPath /t:Restore @props CrusaderDETweaker.csproj | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Package restore failed!" -ForegroundColor Red
            exit 1
        }

        Write-Host "Building project..." -ForegroundColor Yellow
        & $msbuildPath "/t:$target" /v:minimal /nologo @props CrusaderDETweaker.csproj
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed!" -ForegroundColor Red
            exit 1
        }
    }

    # Plugin GUID "CrusaderDETweaker" (PluginInfo.cs) = the BepInEx plugin folder name deploy.ps1 targets.
    $dllPath = Join-Path $OutputPath "CrusaderDETweaker.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Host "Build completed but DLL not found: $dllPath" -ForegroundColor Red
        exit 1
    }

    # Stamp the OUTPUT info.json from the single source of truth (PluginInfo.cs). The tracked
    # info.json is left byte-for-byte alone; if it disagrees, say so (packaging will refuse).
    $version = Get-PluginVersion
    $infoOut = Join-Path $OutputPath 'info.json'
    Set-InfoJsonVersion $infoOut $version
    $srcInfoVersion = Get-InfoJsonVersion (Join-Path $script:RepoRoot 'info.json')
    if ($srcInfoVersion -ne $version) {
        Write-Host "  WARNING: info.json says $srcInfoVersion but PluginInfo.cs says $version - update info.json (package/ship refuse a mismatch)." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "  DLL: $dllPath ($((Get-Item $dllPath).Length) bytes), version $version" -ForegroundColor Green
} finally {
    Pop-Location
}

if ($Deploy) {
    Write-Host ""
    & (Join-Path $PSScriptRoot 'deploy.ps1') -SourceDir $OutputPath -GamePath $GamePath
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
exit 0
