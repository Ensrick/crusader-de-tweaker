# scripts/package_release.ps1
#
# PURPOSE: Build a release from a CLEAN tree and pack it into the release zip.
#
#   1. Refuse a dirty git tree (unless -AllowDirty): the zip must be reproducible from a commit.
#   2. Release rebuild into dist\stage\<version>\build\ (or use -BuildDir from ship.ps1's build stage).
#   3. Verify the build: exactly the whitelisted files (scripts/_release_common.ps1), and the DLL's
#      AssemblyVersion + FileVersion and the stamped info.json Version all equal PLUGIN_VERSION.
#   4. Stage dist\stage\<version>\zip\BepInEx\plugins\CrusaderDETweaker\ and zip it
#      (-> dist\stage\<version>\Crusader DE Tweaker.zip), then re-verify the zip's entry list.
#   5. Copy the zip + Nexus BBCode texts to -StagingPath (skipped with -NoReleaseDirCopy). An existing
#      zip there is renamed to "<zip>.bak.v<its version>", never deleted.
#
# NOTE: Config files are deliberately NOT shipped (since v2.4.1): extracting an upgrade over an
# install would overwrite users' personalized configs. The plugin generates defaults on first launch
# and migrates existing files on every update.
#
# NOTE: Before 2.6.6 this script zipped the LIVE game plugin folder, so whatever was last deployed
# (a dev build, a stray file) shipped. It never reads the game folder now.
#
# USAGE:
#   .\scripts\package_release.ps1
#   .\scripts\package_release.ps1 -ShcdeseDir <extracted SE>\BepInEx\plugins\000shcdese
#   .\scripts\package_release.ps1 -NoReleaseDirCopy        # build + zip under dist\ only
#
# PARAMETERS:
#   -GamePath          Game install, for reference assemblies only (read-only)
#   -ShcdeseDir        Compile against this SHCDE-SE folder instead of the installed one
#   -StagingPath       Release folder that receives the zip + BBCode docs
#   -BuildDir          Use an existing verified build output instead of building
#   -AllowDirty        Package even with uncommitted changes (never for a real release)
#   -NoReleaseDirCopy  Do not copy anything to -StagingPath
#
# EXIT CODES: 0 = packaged and verified, 1 = refused / failed
#

param(
    [string]$GamePath    = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$ShcdeseDir  = "",
    [string]$StagingPath = "D:\Game Mods\Stronghold\Crusader DE Tweaker",
    [string]$BuildDir    = "",
    [switch]$AllowDirty,
    [switch]$NoReleaseDirCopy
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_release_common.ps1')

Write-Host "=== CrusaderDETweaker Package Release ===" -ForegroundColor Cyan

try {
    $version = Get-PluginVersion
    $srcInfo = Get-InfoJsonVersion (Join-Path $script:RepoRoot 'info.json')
    if ($srcInfo -ne $version) { throw "info.json Version $srcInfo != PluginInfo.PLUGIN_VERSION $version. Update info.json." }

    $dirty = Get-GitDirtyLines
    if ($dirty.Count -and -not $AllowDirty) {
        throw "Working tree is not clean ($($dirty.Count) change(s)); commit first or pass -AllowDirty:`n  $($dirty -join "`n  ")"
    }
    if ($dirty.Count) { Write-Host "  WARNING: packaging a DIRTY tree (-AllowDirty) - not a reproducible release." -ForegroundColor Yellow }

    $stageRoot = Get-StageRoot $version
    if (-not $BuildDir) {
        $BuildDir = Join-Path $stageRoot 'build'
        $old = Move-Aside $BuildDir
        if ($old) { Write-Host "  (previous build moved aside: $old)" -ForegroundColor Gray }
        $buildArgs = @{ Configuration = 'Release'; GamePath = $GamePath; OutputPath = $BuildDir; Rebuild = $true }
        if ($ShcdeseDir) { $buildArgs.ShcdeseDir = $ShcdeseDir }
        & (Join-Path $PSScriptRoot 'build.ps1') @buildArgs
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    }

    $check = Assert-PluginPayload $BuildDir $version
    Write-Host "  Build verified: $($check.Files.Count) files, AssemblyVersion/FileVersion/info.json = $version" -ForegroundColor Green

    # Zip tree: BepInEx\plugins\CrusaderDETweaker\ with the whitelisted payload only.
    $zipTree = Join-Path $stageRoot 'zip'
    $old = Move-Aside $zipTree
    if ($old) { Write-Host "  (previous zip tree moved aside: $old)" -ForegroundColor Gray }
    Copy-PluginPayload $BuildDir (Join-Path $zipTree "BepInEx\plugins\$script:PluginGuid")

    $zipPath = Join-Path $stageRoot $script:ZipName
    $old = Move-Aside $zipPath
    if ($old) { Write-Host "  (previous zip moved aside: $old)" -ForegroundColor Gray }
    New-ForwardSlashZip $zipTree $zipPath

    $expected = @($script:PayloadWhitelist | ForEach-Object { "BepInEx/plugins/$script:PluginGuid/" + $_.Replace('\', '/') })
    $entries = @(Get-ZipEntries $zipPath)
    $diff = @(Compare-Object $expected $entries)
    if ($diff.Count) { throw "Zip content mismatch: $($diff | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })" }
    $zipKB = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1KB, 1)
    Write-Host "  Zip verified: $zipPath ($zipKB KB, $($entries.Count) entries)" -ForegroundColor Green

    if ($NoReleaseDirCopy) {
        Write-Host "  (-NoReleaseDirCopy: nothing copied to $StagingPath)" -ForegroundColor Gray
    } else {
        New-Item -ItemType Directory -Force -Path $StagingPath | Out-Null
        $destZip = Join-Path $StagingPath $script:ZipName
        if (Test-Path -LiteralPath $destZip) {
            # Name the aside copy after the version it holds, e.g. "Crusader DE Tweaker.zip.bak.v2.6.5".
            $oldVer = 'unknown'
            try {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $z = [IO.Compression.ZipFile]::OpenRead($destZip)
                try {
                    $e = @($z.Entries | Where-Object { $_.FullName -like '*/info.json' })[0]
                    if ($e) {
                        $r = New-Object IO.StreamReader($e.Open())
                        try { if ($r.ReadToEnd() -match '"Version"\s*:\s*"([^"]+)"') { $oldVer = $Matches[1] } } finally { $r.Dispose() }
                    }
                } finally { $z.Dispose() }
            } catch { }
            $aside = "$destZip.bak.v$oldVer"
            if (Test-Path -LiteralPath $aside) { $aside = "$aside." + (Get-Date -Format 'yyyyMMdd-HHmmss') }
            Move-Item -LiteralPath $destZip -Destination $aside
            Write-Host "  (previous release zip moved aside: $aside)" -ForegroundColor Gray
        }
        Copy-Item -LiteralPath $zipPath -Destination $destZip
        Copy-Item (Join-Path $script:RepoRoot "NEXUS_DESCRIPTION.md")   (Join-Path $StagingPath "NEXUS_DESCRIPTION.txt")   -Force
        Copy-Item (Join-Path $script:RepoRoot "CONFIGURATION_GUIDE.md") (Join-Path $StagingPath "CONFIGURATION_GUIDE.txt") -Force
        Write-Host "  Copied zip + NEXUS_DESCRIPTION.txt + CONFIGURATION_GUIDE.txt to $StagingPath" -ForegroundColor Green
    }
} catch {
    Write-Host "PACKAGE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Done. Users extract '$script:ZipName' directly into their game folder." -ForegroundColor Cyan
exit 0
