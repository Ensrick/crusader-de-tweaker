# scripts/deploy.ps1
#
# PURPOSE: Install a built plugin folder into the game: <GamePath>\BepInEx\plugins\CrusaderDETweaker\.
#          The ONLY script that writes plugin files into the game folder.
#
# USAGE:
#   .\scripts\deploy.ps1                                  # deploys bin\Release\
#   .\scripts\deploy.ps1 -SourceDir dist\stage\2.6.6\zip\BepInEx\plugins\CrusaderDETweaker
#   .\scripts\deploy.ps1 -WhatIf                          # list what would be copied / backed up
#
# SAFETY:
#   - Refuses while the game is running (check_game_running.ps1): the DLL is locked and a half-
#     replaced plugin folder is worse than none.
#   - Every plugin file it is about to replace is first copied to
#     <repo>\dist\deploy-backups\<timestamp>\ (outside BepInEx\plugins, so BepInEx never loads a
#     backup copy of the plugin as a duplicate GUID).
#   - NEVER touches BepInEx\config (user configs): the target is asserted to be the plugin folder.
#   - A stale DLL under the old GUID folder (plugins\ensrick.crusaderdetweaker\) is renamed to
#     .bak so it cannot load alongside the real plugin.
#
# EXIT CODES: 0 = deployed (or -WhatIf listed), 1 = refused / failed
#

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceDir = "",
    [string]$GamePath  = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_release_common.ps1')

if (-not $SourceDir) { $SourceDir = Join-Path $script:RepoRoot 'bin\Release' }
if (-not [IO.Path]::IsPathRooted($SourceDir)) { $SourceDir = Join-Path $script:RepoRoot $SourceDir }
$SourceDir = $SourceDir.TrimEnd('\')

$pluginsDir = Join-Path $GamePath 'BepInEx\plugins'
$targetDir  = Join-Path $pluginsDir $script:PluginGuid

Write-Host "=== Deploying CrusaderDETweaker ===" -ForegroundColor Cyan
Write-Host "  From: $SourceDir" -ForegroundColor Gray
Write-Host "  To  : $targetDir" -ForegroundColor Gray

if (-not (Test-Path (Join-Path $SourceDir "$script:PluginGuid.dll"))) {
    Write-Host "No $script:PluginGuid.dll in $SourceDir - build first (.\scripts\build.ps1)." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $pluginsDir)) {
    Write-Host "BepInEx plugins folder not found: $pluginsDir" -ForegroundColor Red
    exit 1
}
# Hard guard: the plugin folder, and nothing under BepInEx\config, is ever a target.
$fullTarget = [IO.Path]::GetFullPath($targetDir)
if ($fullTarget -match '\\BepInEx\\config(\\|$)' -or (Split-Path $fullTarget -Leaf) -ne $script:PluginGuid) {
    Write-Host "Refusing: target $fullTarget is not BepInEx\plugins\$script:PluginGuid." -ForegroundColor Red
    exit 1
}

& (Join-Path $PSScriptRoot 'check_game_running.ps1') | Out-Null
if ($LASTEXITCODE -eq 0) {
    if ($WhatIfPreference) {
        Write-Host "  (the game is running: a real deploy would refuse right here)" -ForegroundColor Yellow
    } else {
        Write-Host "Refusing: Stronghold Crusader DE is running. Close the game, then deploy again." -ForegroundColor Red
        exit 1
    }
}

$srcFiles = @(Get-ChildItem -LiteralPath $SourceDir -Recurse -File)
$backupDir = Join-Path $script:RepoRoot ("dist\deploy-backups\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$backedUp = 0; $copied = 0

foreach ($f in $srcFiles) {
    $rel  = $f.FullName.Substring($SourceDir.Length + 1)
    $dest = Join-Path $targetDir $rel
    if (Test-Path -LiteralPath $dest) {
        $bak = Join-Path $backupDir $rel
        if ($PSCmdlet.ShouldProcess($dest, "Back up to $bak")) {
            New-Item -ItemType Directory -Force -Path (Split-Path $bak -Parent) | Out-Null
            Copy-Item -LiteralPath $dest -Destination $bak -Force
            $backedUp++
        }
    }
    if ($PSCmdlet.ShouldProcess($dest, "Copy $rel")) {
        New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
        Copy-Item -LiteralPath $f.FullName -Destination $dest -Force
        $copied++
    }
}

# A DLL left under the pre-GUID-fix folder would load as a second copy of the plugin.
$oldDll = Join-Path $pluginsDir 'ensrick.crusaderdetweaker\CrusaderDETweaker.dll'
if ((Test-Path -LiteralPath $oldDll) -and $PSCmdlet.ShouldProcess($oldDll, 'Rename stale old-GUID DLL to .bak')) {
    Rename-Item -LiteralPath $oldDll -NewName ("CrusaderDETweaker.dll.bak." + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

if ($WhatIfPreference) {
    Write-Host "  [WhatIf] $($srcFiles.Count) file(s) would be deployed; nothing written." -ForegroundColor Yellow
    exit 0
}

# Verify: every deployed file hashes equal to its source.
$bad = @($srcFiles | Where-Object {
    $rel = $_.FullName.Substring($SourceDir.Length + 1)
    (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $targetDir $rel)).Hash
})
if ($bad.Count) {
    Write-Host "Deploy verification FAILED for: $($bad.Name -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "Deployed $copied file(s), hashes verified. Backed up $backedUp replaced file(s)$(if ($backedUp) { " to $backupDir" })." -ForegroundColor Green
exit 0
