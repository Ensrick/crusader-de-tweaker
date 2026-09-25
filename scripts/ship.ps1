# scripts/ship.ps1
#
# PURPOSE: The ONE end-to-end ship path for CrusaderDETweaker. Named stages, each idempotent,
#          each verifying its own result, each re-runnable on its own with -Stage.
#
#   Stage       Kind    What it does                                              Verified by
#   ---------   ------  --------------------------------------------------------  ------------------------------
#   preflight   local   clean tree; -Version == PluginInfo == info.json; CHANGELOG; README.txt generates; the checks themselves
#                       entry exists; tag v<ver> not on origin/github (or already
#                       at HEAD); tools present; Workshop description <= 8000
#   build       local   Release rebuild -> dist\stage\<ver>\build\                  whitelist + DLL/info.json versions
#   package     local   zip -> dist\stage\<ver>\Crusader DE Tweaker.zip; with       zip entry list == whitelist
#                       -Publish also copied (+ BBCode txt) to -ReleaseDir
#   tag         PUBLIC  annotated tag v<ver> at HEAD, pushed to origin + github     git ls-remote on both == HEAD
#   github      PUBLIC  gh release v<ver> + zip (Ensrick/crusader-de-tweaker)       gh release view: asset size
#   gitlab      PUBLIC  glab release v<ver> + zip (ensrick7/crusader-de-tweaker)    glab release view
#   workshop    PUBLIC  stage + .map (packager, local) -> upload item 3726034964    workshop_log "Uploaded new content"
#                       with -z description                                        + Web API file_size == .map size
#   nexus       PUBLIC  scripts\nexus\Publish-NexusMod.ps1 (mod 38)                 script completes; receipt written
#   deploy      local   scripts\deploy.ps1 from the packaged tree (LAST)            deploy.ps1 hash check
#
# DRY-RUN IS THE DEFAULT. Without -Publish, local stages run (build/package into dist\ only) and every
# PUBLIC stage, the release-dir copy and the game-folder deploy only print what they would do.
# -Publish requires -Version <x.y.z> equal to PluginInfo.PLUGIN_VERSION.
# Nothing here uploads, pushes or touches the game folder without -Publish.
#
# IDEMPOTENCE: a public stage that finds its result already in place (tag at HEAD on both remotes,
# release with the asset, receipt in dist\receipts\<ver>\) reports SKIP. Re-run a single stage with
# e.g.  -Stage workshop -Version 2.6.6 -Publish.  -Force redoes Workshop / Nexus despite a receipt.
#
# USAGE:
#   .\scripts\ship.ps1                                        # full dry run
#   .\scripts\ship.ps1 -Version 2.6.6 -Publish                # the real ship
#   .\scripts\ship.ps1 -Stage preflight,build,package         # local stages only
#   .\scripts\ship.ps1 -Stage github -Version 2.6.6 -Publish  # redo one stage
#
# AFTER A REAL SHIP: fully restart Steam before testing the Workshop copy (Steam re-downloads a
# self-authored item only on restart). The Nexus mod DESCRIPTION is not API-editable: refresh by hand.
#

[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$Publish,
    [ValidateSet('preflight', 'build', 'package', 'tag', 'github', 'gitlab', 'workshop', 'nexus', 'deploy')]
    [string[]]$Stage = @('preflight', 'build', 'package', 'tag', 'github', 'gitlab', 'workshop', 'nexus', 'deploy'),
    [switch]$AllowDirty,
    [switch]$Force,

    [string]$GamePath    = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [string]$ShcdeseDir  = "",
    [string]$ReleaseDir  = "D:\Game Mods\Stronghold\Crusader DE Tweaker",
    [string]$NotesFile   = "",

    [string]$GitHubRepo  = "Ensrick/crusader-de-tweaker",
    [string]$GitLabRepo  = "ensrick7/crusader-de-tweaker",

    [string]$WorkshopId  = "3726034964",
    [string]$SteamAppId  = "3024040",
    [string]$PackagerExe = "C:\Users\danjo\source\repos\_shcdese_v1.31.0\packager\SHCDESE.WorkshopPackager.exe",
    [string]$UploaderExe = "C:\Users\danjo\source\repos\_shcdese_v1.31.0\uploader\pdengine.steamugc.tool.exe",
    [string]$SteamDir    = "C:\Program Files (x86)\Steam",
    [string]$WorkshopDescription = "",
    [int]$WorkshopVerifyTimeoutSec = 300,

    [int]$NexusModId = 38
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_release_common.ps1')

$PublicStages = @('tag', 'github', 'gitlab', 'workshop', 'nexus')
$OrderedStages = @('preflight', 'build', 'package', 'tag', 'github', 'gitlab', 'workshop', 'nexus', 'deploy')
$Stage = @($OrderedStages | Where-Object { $Stage -contains $_ })

$pluginVersion = Get-PluginVersion
if (-not $Version) { $Version = $pluginVersion }
$tag         = "v$Version"
$stageRoot   = Get-StageRoot $Version
$buildDir    = Join-Path $stageRoot 'build'
$zipTree     = Join-Path $stageRoot 'zip'
$zipPath     = Join-Path $stageRoot $script:ZipName
$pluginTree  = Join-Path $zipTree "BepInEx\plugins\$script:PluginGuid"
$receiptDir  = Join-Path $script:RepoRoot "dist\receipts\$Version"
if (-not $WorkshopDescription) { $WorkshopDescription = Join-Path $script:RepoRoot 'workshop\workshop-description.txt' }

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$StageName, [string]$Status, [string]$Detail) {
    $results.Add([pscustomobject]@{ Stage = $StageName; Status = $Status; Detail = $Detail })
    $color = switch ($Status) { 'OK' { 'Green' } 'SKIP' { 'Gray' } 'DRY-RUN' { 'Yellow' } 'WARN' { 'Yellow' } default { 'Red' } }
    Write-Host ("  [{0}] {1}" -f $Status, $Detail) -ForegroundColor $color
}
function Write-Would([string]$Text) { Write-Host "    would: $Text" -ForegroundColor DarkYellow }
# Run a native command: Out = stdout lines, Err = stderr lines (kept apart: git prints warnings such
# as "redirecting to ..." on stderr). EAP is relaxed locally because Windows PowerShell 5.1 turns
# redirected native stderr into terminating errors under 'Stop'.
function Invoke-Exe([scriptblock]$Block) {
    $ErrorActionPreference = 'Continue'
    $all = @(& $Block 2>&1)
    $code = $LASTEXITCODE
    $err = @($all | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" } | Where-Object { $_ -ne '' })
    $out = @($all | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" } | Where-Object { $_ -ne '' })
    return [pscustomobject]@{ Code = $code; Out = $out; Err = $err }
}
function Invoke-Native([string]$What, [scriptblock]$Block) {
    $r = Invoke-Exe $Block
    if ($r.Code -ne 0) { throw "$What failed (exit $($r.Code)): $(($r.Err + $r.Out) -join ' ')" }
    return $r.Out
}
function Get-HeadSha { ((Invoke-Native 'git rev-parse' { git -C $script:RepoRoot rev-parse HEAD }) -join '').Trim() }
# Commit sha a remote's tag points at ($null if absent). Peeled (^{}) entry wins for annotated tags.
function Get-RemoteTagSha([string]$Remote) {
    $lines = @(Invoke-Native "git ls-remote $Remote" { git -C $script:RepoRoot ls-remote --tags $Remote "refs/tags/$tag" "refs/tags/$tag^{}" } | Where-Object { $_ })
    if (-not $lines.Count) { return $null }
    $peeled = @($lines | Where-Object { "$_" -match '\^\{\}$' })
    $line = if ($peeled.Count) { $peeled[0] } else { $lines[0] }
    return ("$line" -split '\s+')[0]
}
function Get-ReleaseNotes {
    if ($NotesFile) { return (Get-Content -LiteralPath $NotesFile -Raw -Encoding UTF8) }
    $entry = Get-ChangelogEntry $Version
    return $entry.Body
}
function Get-ReleaseTitle { (Get-ChangelogEntry $Version).Title }
# Steam Workshop "Change Notes" text for this version: the CHANGELOG entry converted from Markdown to Steam
# BBCode (bullets -> [list][*], **bold** -> [b], `code` -> plain). The Workshop DESCRIPTION holds no changelog;
# this is where Steam users read what changed (user request 2026-09-25).
function Get-SteamChangeNote { ConvertTo-SteamChangeNote -Version $Version -Title (Get-ReleaseTitle) -Body (Get-ReleaseNotes) -ChangelogUrl "https://gitlab.com/$GitLabRepo/-/blob/main/CHANGELOG.md" }
function Write-Receipt([string]$Name, [hashtable]$Data) {
    New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
    $Data.Version = $Version; $Data.Time = (Get-Date).ToString('s'); $Data.Head = Get-HeadSha
    ($Data | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath (Join-Path $receiptDir "$Name.json") -Encoding UTF8
}
function Test-Receipt([string]$Name) { Test-Path -LiteralPath (Join-Path $receiptDir "$Name.json") }

# ---------------------------------------------------------------------------------------------
# Stages
# ---------------------------------------------------------------------------------------------

function Stage-Preflight {
    $problems = @(); $warnings = @()
    if ($Version -ne $pluginVersion) { $problems += "-Version $Version != PluginInfo.PLUGIN_VERSION $pluginVersion" }
    $infoVer = Get-InfoJsonVersion (Join-Path $script:RepoRoot 'info.json')
    if ($infoVer -ne $pluginVersion) { $problems += "info.json Version $infoVer != PLUGIN_VERSION $pluginVersion" }

    $dirty = Get-GitDirtyLines
    if ($dirty.Count) {
        if ($AllowDirty -and -not $Publish) { $warnings += "dirty tree ($($dirty.Count) change(s)), allowed for a dry run" }
        else { $problems += "working tree not clean ($($dirty.Count) change(s)); commit first" }
    }

    $entry = Get-ChangelogEntry $Version
    if (-not $entry -or -not $entry.Body) { $problems += "CHANGELOG.md has no '$Version - ...' entry" }

    # The shipped README.txt is generated from the guide + this CHANGELOG entry; prove it builds and
    # names this version before anything is compiled (the build / package stages verify the file again).
    try {
        $readme = New-UserReadme $Version
        if (($readme -split "`r`n")[1] -ne "Version: $Version") { $problems += "generated README.txt does not name version $Version" }
    } catch { $problems += "README.txt generation failed: $($_.Exception.Message)" }

    $head = Get-HeadSha
    foreach ($remote in 'origin', 'github') {
        try {
            $sha = Get-RemoteTagSha $remote
            if ($sha -and $sha -ne $head) { $problems += "tag $tag already on $remote at $($sha.Substring(0,8)) (HEAD is $($head.Substring(0,8)))" }
            elseif ($sha) { $warnings += "tag $tag already on $remote at HEAD (tag stage will SKIP)" }
        } catch { $warnings += "could not query $remote ($($_.Exception.Message))" }
    }

    if (-not (Find-MSBuild)) { $warnings += 'MSBuild (VS 2022) not found; build falls back to dotnet' }
    $tools = @{ gh = 'github'; glab = 'gitlab' }
    foreach ($t in $tools.Keys) {
        if (($Stage -contains $tools[$t]) -and -not (Get-Command $t -ErrorAction SilentlyContinue)) { $problems += "$t CLI not on PATH" }
    }
    if ($Stage -contains 'workshop') {
        foreach ($exe in $PackagerExe, $UploaderExe) { if (-not (Test-Path -LiteralPath $exe)) { $problems += "missing tool: $exe" } }
        if (-not (Test-Path -LiteralPath $WorkshopDescription)) { $problems += "missing Workshop description: $WorkshopDescription" }
        else {
            $desc = [IO.File]::ReadAllText($WorkshopDescription)
            if ($desc.Length -gt 8000) { $problems += "Workshop description is $($desc.Length) chars (Steam cap 8000; -z fails with k_EResultInvalidParam)" }
            $first = ($desc -split "`r?`n")[0]
            if ($first -notmatch [regex]::Escape($Version)) {
                $msg = "Workshop description first line '$first' does not name $Version - update workshop\workshop-description.txt"
                if ($Publish) { $problems += $msg } else { $warnings += $msg }
            }
        }
    }
    if ($Stage -contains 'nexus' -and -not (Test-Path (Join-Path $PSScriptRoot 'nexus\Publish-NexusMod.ps1'))) { $problems += 'scripts\nexus\Publish-NexusMod.ps1 missing' }

    foreach ($w in $warnings) { Add-Result 'preflight' 'WARN' $w }
    if ($problems.Count) { throw ($problems -join '; ') }
    $cl = if ($entry) { $entry.Title } else { '' }
    Add-Result 'preflight' 'OK' "version $Version (PluginInfo = info.json), CHANGELOG '$cl', HEAD $($head.Substring(0,8))"
}

function Stage-Build {
    $old = Move-Aside $buildDir
    if ($old) { Write-Host "    (previous build moved aside: $old)" -ForegroundColor Gray }
    $buildArgs = @{ Configuration = 'Release'; GamePath = $GamePath; OutputPath = $buildDir; Rebuild = $true }
    if ($ShcdeseDir) { $buildArgs.ShcdeseDir = $ShcdeseDir }
    & (Join-Path $PSScriptRoot 'build.ps1') @buildArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'build.ps1 failed' }
    $check = Assert-PluginPayload $buildDir $Version
    Add-Result 'build' 'OK' "$buildDir : $($check.Files -join ', ') ; AssemblyVersion/FileVersion/info.json = $Version"
}

function Stage-Package {
    if (-not (Test-Path -LiteralPath (Join-Path $buildDir "$script:PluginGuid.dll"))) { throw "no build at $buildDir - run the build stage" }
    $pkgArgs = @{ BuildDir = $buildDir; StagingPath = $ReleaseDir; GamePath = $GamePath }
    if (-not $Publish) { $pkgArgs.NoReleaseDirCopy = $true }
    if ($AllowDirty -and -not $Publish) { $pkgArgs.AllowDirty = $true }
    & (Join-Path $PSScriptRoot 'package_release.ps1') @pkgArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'package_release.ps1 failed' }
    $kb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1KB, 1)
    if ($Publish) { Add-Result 'package' 'OK' "$zipPath ($kb KB) verified; copied to $ReleaseDir" }
    else {
        Write-Would "copy '$script:ZipName' + NEXUS_DESCRIPTION.txt + CONFIGURATION_GUIDE.txt to $ReleaseDir (old zip -> .bak.v<old>)"
        Add-Result 'package' 'OK' "$zipPath ($kb KB) verified (release-dir copy: dry run)"
    }
}

function Stage-Tag {
    $head = Get-HeadSha
    $pending = @()
    foreach ($remote in 'origin', 'github') {
        $sha = Get-RemoteTagSha $remote
        if ($sha -and $sha -ne $head) { throw "$tag on $remote points at $sha, not HEAD $head" }
        if (-not $sha) { $pending += $remote }
    }
    if (-not $pending.Count) { Add-Result 'tag' 'SKIP' "$tag already at HEAD on origin + github"; return }

    $lr = Invoke-Exe { git -C $script:RepoRoot rev-parse -q --verify "refs/tags/$tag^{commit}" }
    $local = if ($lr.Code -eq 0) { ($lr.Out -join '').Trim() } else { $null }
    if (-not $Publish) {
        if (-not $local) { Write-Would "git tag -a $tag -m '$(Get-ReleaseTitle)' (at $($head.Substring(0,8)))" }
        foreach ($rn in $pending) { Write-Would "git push $rn $tag" }
        Add-Result 'tag' 'DRY-RUN' "$tag -> $($pending -join ', ')"
        return
    }
    if ($local -and $local.Trim() -ne $head) { throw "local tag $tag points at $local, not HEAD" }
    if (-not $local) { Invoke-Native 'git tag' { git -C $script:RepoRoot tag -a $tag -m (Get-ReleaseTitle) } | Out-Null }
    foreach ($rn in $pending) { Invoke-Native "git push $rn" { git -C $script:RepoRoot push $rn "refs/tags/$tag" } | Out-Host }
    foreach ($rn in 'origin', 'github') {
        if ((Get-RemoteTagSha $rn) -ne $head) { throw "verification: $tag on $rn is not HEAD after push" }
    }
    Add-Result 'tag' 'OK' "$tag at $($head.Substring(0,8)) on origin + github (ls-remote verified)"
}

function Stage-GitHub {
    $zipLen = (Get-Item -LiteralPath $zipPath).Length
    $existing = $null
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        $r = Invoke-Exe { gh release view $tag -R $GitHubRepo --json assets,url }
        if ($r.Code -eq 0 -and $r.Out.Count) { $existing = ($r.Out -join "`n") | ConvertFrom-Json }
    }
    if ($existing -and @($existing.assets | Where-Object { $_.size -eq $zipLen }).Count) {
        Add-Result 'github' 'SKIP' "release $tag already has a $zipLen-byte asset ($($existing.url))"; return
    }
    if (-not $Publish) {
        $verb = if ($existing) { "gh release upload $tag '<zip>' -R $GitHubRepo --clobber" } else { "gh release create $tag '<zip>' -R $GitHubRepo --title '$(Get-ReleaseTitle)' --notes-file <CHANGELOG $Version entry>" }
        Write-Would $verb
        Add-Result 'github' 'DRY-RUN' "$GitHubRepo $tag ($zipLen-byte zip)"
        return
    }
    $notes = Join-Path $stageRoot 'release-notes.md'
    [IO.File]::WriteAllText($notes, (Get-ReleaseNotes), (New-Object Text.UTF8Encoding($false)))
    if ($existing) { Invoke-Native 'gh release upload' { gh release upload $tag $zipPath -R $GitHubRepo --clobber } | Out-Host }
    else { Invoke-Native 'gh release create' { gh release create $tag $zipPath -R $GitHubRepo --verify-tag --title (Get-ReleaseTitle) --notes-file $notes } | Out-Host }
    $v = (Invoke-Native 'gh release view' { gh release view $tag -R $GitHubRepo --json assets,url }) -join "`n" | ConvertFrom-Json
    if (-not @($v.assets | Where-Object { $_.size -eq $zipLen -and $_.state -eq 'uploaded' }).Count) { throw "verification: no uploaded $zipLen-byte asset on $tag" }
    Add-Result 'github' 'OK' "$($v.url) asset $zipLen bytes verified"
}

function Stage-GitLab {
    # GitLab rejects asset link paths containing spaces ("Filepath is in an invalid format"), so the
    # GitLab asset is a space-free copy of the release zip. Match on THAT name: the release title also
    # contains "Crusader DE Tweaker", so a title match would pass for a release with no asset.
    $glName = "CrusaderDETweaker-$Version.zip"
    $glZip  = Join-Path $stageRoot $glName
    $glRx   = [regex]::Escape($glName)
    $existing = $false; $view = ''
    if (Get-Command glab -ErrorAction SilentlyContinue) {
        $r = Invoke-Exe { glab release view $tag -R $GitLabRepo }
        $existing = ($r.Code -eq 0)
        $view = $r.Out -join "`n"
    }
    if ($existing) {
        if ($view -match $glRx) {
            Add-Result 'gitlab' 'SKIP' "release $tag already exists with the zip asset"; return
        }
    }
    if (-not $Publish) {
        Write-Would "glab release create $tag '<zip>' -R $GitLabRepo --name '$(Get-ReleaseTitle)' --notes-file <CHANGELOG $Version entry>"
        Add-Result 'gitlab' 'DRY-RUN' "$GitLabRepo $tag"
        return
    }
    $notes = Join-Path $stageRoot 'release-notes.md'
    [IO.File]::WriteAllText($notes, (Get-ReleaseNotes), (New-Object Text.UTF8Encoding($false)))
    Copy-Item -LiteralPath $zipPath -Destination $glZip -Force
    if ($existing) { Invoke-Native 'glab release upload' { glab release upload $tag $glZip -R $GitLabRepo } | Out-Host }
    else { Invoke-Native 'glab release create' { glab release create $tag $glZip -R $GitLabRepo --name (Get-ReleaseTitle) --notes-file $notes } | Out-Host }
    $view = (Invoke-Native 'glab release view' { glab release view $tag -R $GitLabRepo }) -join "`n"
    if ($view -notmatch $glRx) { throw "verification: glab release view $tag shows no $glName asset" }
    Add-Result 'gitlab' 'OK' "$GitLabRepo $tag with zip (glab release view verified)"
}

function Stage-Workshop {
    if (-not (Test-Path -LiteralPath $pluginTree)) { throw "no packaged plugin tree at $pluginTree - run the package stage" }
    if ((Test-Receipt 'workshop') -and -not $Force) { Add-Result 'workshop' 'SKIP' "receipt $receiptDir\workshop.json exists (-Force to re-upload)"; return }

    # 1. Stage (local): <stage>\info.json + steam-preview.png + BepInEx\plugins\CrusaderDETweaker\...
    $wsRoot  = Join-Path $stageRoot 'workshop'
    $old = Move-Aside $wsRoot
    if ($old) { Write-Host "    (previous workshop stage moved aside: $old)" -ForegroundColor Gray }
    $wsStage  = Join-Path $wsRoot "$script:PluginGuid-Workshop"
    $wsUpload = Join-Path $wsRoot 'upload'     # contains ONLY the .map (the uploader ships the whole dir)
    New-Item -ItemType Directory -Force -Path $wsStage, $wsUpload | Out-Null
    Copy-PluginPayload $pluginTree (Join-Path $wsStage "BepInEx\plugins\$script:PluginGuid")
    Copy-Item -LiteralPath (Join-Path $pluginTree 'info.json') -Destination (Join-Path $wsStage 'info.json')
    $preview = Join-Path $script:RepoRoot 'workshop\steam-preview.png'
    Copy-Item -LiteralPath $preview -Destination (Join-Path $wsStage 'steam-preview.png')
    $descCopy = Join-Path $wsRoot 'workshop-description.txt'
    Copy-Item -LiteralPath $WorkshopDescription -Destination $descCopy

    # 2. Package the .map (local).
    $map = Join-Path $wsUpload "$script:PluginGuid.map"
    $pk = Invoke-Exe { & $PackagerExe -s $wsStage -o $map }
    if ($pk.Code -ne 0 -or -not (Test-Path -LiteralPath $map)) { throw "WorkshopPackager failed (exit $($pk.Code)): $(($pk.Err + $pk.Out) -join ' ')" }
    $extra = @(Get-ChildItem -LiteralPath $wsUpload -Force | Where-Object { $_.FullName -ne $map })
    if ($extra.Count) { throw "upload dir must contain ONLY the .map; also found: $($extra.Name -join ', ')" }
    $mapLen = (Get-Item -LiteralPath $map).Length
    $descLen = [IO.File]::ReadAllText($descCopy).Length
    if ($descLen -gt 8000) { throw "Workshop description $descLen chars > 8000" }

    $upArgs = @('-v', '-a', $SteamAppId, '-i', $WorkshopId, '-u', '-s', $wsUpload, '-z', $descCopy, '-c', (Get-SteamChangeNote))
    if (-not $Publish) {
        $shown = ($upArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
        Write-Would ('"' + $UploaderExe + '" ' + $shown)
        Write-Would "verify: $SteamDir\logs\workshop_log.txt 'Uploaded new content ... for item $WorkshopId' + GetPublishedFileDetails file_size == $mapLen"
        Add-Result 'workshop' 'DRY-RUN' "staged + packaged $map ($mapLen bytes, description $descLen chars); upload to item $WorkshopId not performed"
        return
    }

    # 3. Upload (public) + verify from ground truth, not the tool's own "Upload finished".
    $start = Get-Date
    $up = Invoke-Exe { & $UploaderExe @upArgs }
    ($up.Out + $up.Err) | Out-Host
    if ($up.Code -ne 0) { throw "uploader exit $($up.Code)" }

    $log = Join-Path $SteamDir 'logs\workshop_log.txt'
    $logOk = $false; $apiSize = $null
    $deadline = (Get-Date).AddSeconds($WorkshopVerifyTimeoutSec)
    do {
        if (-not $logOk -and (Test-Path -LiteralPath $log)) {
            foreach ($l in (Get-Content -LiteralPath $log -Tail 400)) {
                if ($l -match '^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\].*Uploaded new content.*for item (\d+)' -and $Matches[2] -eq $WorkshopId) {
                    if ([datetime]::ParseExact($Matches[1], 'yyyy-MM-dd HH:mm:ss', $null) -ge $start.AddSeconds(-5)) { $logOk = $true }
                }
            }
        }
        try {
            $r = Invoke-RestMethod -Method Post -Uri 'https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/' `
                -Body @{ itemcount = 1; 'publishedfileids[0]' = $WorkshopId }
            $apiSize = [int64]$r.response.publishedfiledetails[0].file_size
        } catch { $apiSize = $null }
        if ($logOk -and $apiSize -eq $mapLen) { break }
        Start-Sleep -Seconds 15
    } while ((Get-Date) -lt $deadline)
    if (-not $logOk)          { throw "verification: no 'Uploaded new content' for item $WorkshopId in $log since $start" }
    if ($apiSize -ne $mapLen) { throw "verification: Web API file_size $apiSize != .map size $mapLen" }
    Write-Receipt 'workshop' @{ Item = $WorkshopId; MapBytes = $mapLen; ApiFileSize = $apiSize }
    Add-Result 'workshop' 'OK' "item $WorkshopId updated: workshop_log 'Uploaded new content' + API file_size $apiSize == .map"
}

function Stage-Nexus {
    if ((Test-Receipt 'nexus') -and -not $Force) { Add-Result 'nexus' 'SKIP' "receipt $receiptDir\nexus.json exists (-Force to re-publish)"; return }
    if (-not (Test-Path -LiteralPath $zipPath)) { throw "no zip at $zipPath - run the package stage" }
    $nexus = Join-Path $PSScriptRoot 'nexus\Publish-NexusMod.ps1'
    if (-not $Publish) {
        # No -WhatIf call either: even the script's dry run makes Nexus API reads with the key.
        Write-Would "& '$nexus' -Action publish -ModId $NexusModId -FilePath '<zip>' -Version $Version -Changelog <CHANGELOG $Version entry> -Confirm:`$false"
        Add-Result 'nexus' 'DRY-RUN' "mod $NexusModId v$Version"
        return
    }
    & $nexus -Action publish -ModId $NexusModId -FilePath $zipPath -Version $Version -Changelog (Get-ReleaseNotes) -Confirm:$false | Out-Host
    Write-Receipt 'nexus' @{ ModId = $NexusModId; Zip = $zipPath }
    Add-Result 'nexus' 'OK' "mod $NexusModId v$Version published (description is manual)"
}

function Stage-Deploy {
    if (-not (Test-Path -LiteralPath $pluginTree)) { throw "no packaged plugin tree at $pluginTree - run the package stage" }
    $deploy = Join-Path $PSScriptRoot 'deploy.ps1'
    if (-not $Publish) {
        & $deploy -SourceDir $pluginTree -GamePath $GamePath -WhatIf | Out-Host
        Add-Result 'deploy' 'DRY-RUN' "deploy.ps1 -WhatIf from $pluginTree (nothing written to the game folder)"
        return
    }
    & $deploy -SourceDir $pluginTree -GamePath $GamePath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'deploy.ps1 failed (game running?)' }
    Add-Result 'deploy' 'OK' "deployed + hash-verified into $GamePath\BepInEx\plugins\$script:PluginGuid"
}

# ---------------------------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------------------------

$mode = if ($Publish) { 'PUBLISH' } else { 'DRY RUN (no uploads, pushes, release-dir copy or game-folder writes)' }
Write-Host "=== CrusaderDETweaker ship $Version - $mode ===" -ForegroundColor Cyan
Write-Host "    stages: $($Stage -join ', ')" -ForegroundColor Gray

$failed = $false
if ($Publish) {
    if (-not $PSBoundParameters.ContainsKey('Version')) {
        Write-Host "-Publish requires an explicit -Version <x.y.z> naming the release." -ForegroundColor Red; exit 1
    }
    if ($Version -ne $pluginVersion) {
        Write-Host "-Version $Version does not match PluginInfo.PLUGIN_VERSION $pluginVersion." -ForegroundColor Red; exit 1
    }
    if ($AllowDirty) { Write-Host "-AllowDirty is ignored with -Publish: a release ships from a clean commit." -ForegroundColor Yellow }
}

foreach ($s in $Stage) {
    Write-Host ""
    $kind = if ($PublicStages -contains $s) { 'public' } else { 'local' }
    Write-Host "--- $s ($kind) ---" -ForegroundColor Cyan
    try {
        switch ($s) {
            'preflight' { Stage-Preflight }
            'build'     { Stage-Build }
            'package'   { Stage-Package }
            'tag'       { Stage-Tag }
            'github'    { Stage-GitHub }
            'gitlab'    { Stage-GitLab }
            'workshop'  { Stage-Workshop }
            'nexus'     { Stage-Nexus }
            'deploy'    { Stage-Deploy }
        }
    } catch {
        Add-Result $s 'FAIL' $_.Exception.Message
        $failed = $true
        break
    }
}

Write-Host ""
Write-Host "=== Stage summary ($Version, $(if ($Publish) { 'publish' } else { 'dry run' })) ===" -ForegroundColor Cyan
foreach ($r in $results) { Write-Host ("  {0,-10} {1,-8} {2}" -f $r.Stage, $r.Status, $r.Detail) }
$ran = @($results | ForEach-Object { $_.Stage } | Select-Object -Unique)
foreach ($s in $Stage) { if ($ran -notcontains $s) { Write-Host ("  {0,-10} {1,-8} {2}" -f $s, 'NOT RUN', 'stopped after an earlier failure') -ForegroundColor DarkGray } }
if ($failed) { exit 1 }
if ($Publish -and ($Stage -contains 'workshop')) { Write-Host "Restart Steam fully before testing the Workshop copy." -ForegroundColor Yellow }
exit 0
