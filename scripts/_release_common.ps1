# scripts/_release_common.ps1
#
# PURPOSE: Shared helpers for build.ps1 / package_release.ps1 / deploy.ps1 / ship.ps1.
#          Dot-source it:  . (Join-Path $PSScriptRoot '_release_common.ps1')
#
# IMPORTANT FOR AI AGENTS:
# - $PayloadWhitelist is the exact set of files a release may contain (relative to
#   BepInEx\plugins\CrusaderDETweaker\). Derived from the shipped 2.6.5 zip, plus the lobby
#   tab XAML since 2.7.0. Adding a shipped file means adding it here, or packaging fails on purpose.
# - Nothing here deletes recursively; superseded folders are renamed to <name>.bak.<timestamp>.
#

$script:RepoRoot  = Split-Path $PSScriptRoot -Parent
$script:PluginGuid = 'CrusaderDETweaker'
$script:ZipName    = 'Crusader DE Tweaker.zip'
$script:DefaultGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition'

$script:PayloadWhitelist = @(
    'CrusaderDETweaker.dll',
    'Tomlyn.dll',
    'info.json',
    'Override\Assets\GUI\Sprites\CrusaderDETweaker.png',
    'Override\ScriptExtenderUI\CDTLobbySettings.xaml'
)
# Build by-products that are never shipped (debug symbols, library XML docs).
$script:DevArtifactPatterns = @('*.pdb', '*.xml')

function Get-PluginVersion {
    $pi = Join-Path $script:RepoRoot 'PluginInfo.cs'
    $m = Select-String -Path $pi -Pattern 'PLUGIN_VERSION\s*=\s*"([^"]+)"'
    if (-not $m) { throw "PLUGIN_VERSION not found in $pi" }
    return $m.Matches[0].Groups[1].Value
}

function Get-InfoJsonVersion([string]$Path) {
    $m = Select-String -Path $Path -Pattern '"Version"\s*:\s*"([^"]+)"'
    if (-not $m) { throw ('"Version" not found in ' + $Path) }
    return $m.Matches[0].Groups[1].Value
}

# Rewrite ONLY the "Version" value of an info.json, byte-preserving everything else (no BOM, no
# reformatting). Used on the build OUTPUT copy; the tracked source info.json is never rewritten.
function Set-InfoJsonVersion([string]$Path, [string]$Version) {
    $text = [IO.File]::ReadAllText($Path)
    $new = [regex]::Replace($text, '("Version"\s*:\s*")[^"]+(")', "`${1}$Version`${2}", 1)
    if ($new -ne $text) { [IO.File]::WriteAllText($Path, $new, (New-Object Text.UTF8Encoding($false))) }
}

function Get-GitDirtyLines {
    $lines = & git -C $script:RepoRoot status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'git status failed' }
    return @($lines | Where-Object { $_ })
}

function Get-StageRoot([string]$Version) {
    return (Join-Path $script:RepoRoot "dist\stage\$Version")
}

# Rename an existing file/folder aside (never delete). Returns the new path, or $null if absent.
function Move-Aside([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $ts = Get-Date -Format 'yyyyMMdd-HHmmss'
    $dest = "$Path.bak.$ts"
    Move-Item -LiteralPath $Path -Destination $dest
    return $dest
}

# The shippable files of a build output folder, relative paths. Dev artifacts are excluded.
function Get-PayloadFiles([string]$Dir) {
    $root = (Resolve-Path -LiteralPath $Dir).Path.TrimEnd('\')
    Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $name = $_.Name
        -not ($script:DevArtifactPatterns | Where-Object { $name -like $_ })
    } | ForEach-Object { $_.FullName.Substring($root.Length + 1) }
}

function ConvertTo-Version3([string]$v) {
    $p = ([version]$v)
    return '{0}.{1}.{2}' -f $p.Major, $p.Minor, [math]::Max($p.Build, 0)
}

# Throws unless $Dir holds exactly the whitelisted payload and every version stamp equals $Version.
function Assert-PluginPayload([string]$Dir, [string]$Version) {
    $files = @(Get-PayloadFiles $Dir)
    $unexpected = @($files | Where-Object { $script:PayloadWhitelist -notcontains $_ })
    $missing    = @($script:PayloadWhitelist | Where-Object { $files -notcontains $_ })
    if ($unexpected.Count) { throw "Unexpected file(s) in ${Dir}: $($unexpected -join ', ') (whitelist: scripts/_release_common.ps1)" }
    if ($missing.Count)    { throw "Missing file(s) in ${Dir}: $($missing -join ', ')" }

    $dll = Join-Path $Dir "$script:PluginGuid.dll"
    $asmVer  = ConvertTo-Version3 ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString())
    $fileVer = ConvertTo-Version3 ((Get-Item -LiteralPath $dll).VersionInfo.FileVersion)
    $infoVer = Get-InfoJsonVersion (Join-Path $Dir 'info.json')
    $bad = @()
    if ($asmVer  -ne $Version) { $bad += "AssemblyVersion=$asmVer" }
    if ($fileVer -ne $Version) { $bad += "FileVersion=$fileVer" }
    if ($infoVer -ne $Version) { $bad += "info.json Version=$infoVer" }
    if ($bad.Count) { throw "Version mismatch in ${Dir} (PLUGIN_VERSION=$Version): $($bad -join ', ')" }
    return [pscustomobject]@{ Files = $files; AssemblyVersion = $asmVer; FileVersion = $fileVer; InfoVersion = $infoVer }
}

# Copy the whitelisted payload from a build output into a plugin folder (created fresh).
function Copy-PluginPayload([string]$FromDir, [string]$ToPluginDir) {
    foreach ($rel in $script:PayloadWhitelist) {
        $dest = Join-Path $ToPluginDir $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
        Copy-Item -LiteralPath (Join-Path $FromDir $rel) -Destination $dest -Force
    }
}

# Zip a folder with forward-slash entry names (Windows PowerShell 5.1's Compress-Archive writes
# backslashes, which non-Windows extractors and some mod managers mangle).
function New-ForwardSlashZip([string]$SourceDir, [string]$ZipPath) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $root = (Resolve-Path -LiteralPath $SourceDir).Path.TrimEnd('\')
    $zip = [IO.Compression.ZipFile]::Open($ZipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($f in Get-ChildItem -LiteralPath $root -Recurse -File) {
            $entry = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose() }
}

function Get-ZipEntries([string]$ZipPath) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try { return @($zip.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName }) } finally { $zip.Dispose() }
}

# The CHANGELOG block for $Version: header line "<ver> - <title> (<date>):" plus its bullets.
function Get-ChangelogEntry([string]$Version) {
    $lines = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'CHANGELOG.md') -Encoding UTF8
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match ('^' + [regex]::Escape($Version) + ' - ')) { $start = $i; break }
    }
    if ($start -lt 0) { return $null }
    $end = $lines.Count
    for ($j = $start + 1; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -match '^(\d+\.\d+\.\d+|Unreleased)\b') { $end = $j; break }
    }
    $title = ($lines[$start] -replace '\s*\(\d{4}-\d{2}-\d{2}\):?\s*$', '' -replace ':\s*$', '')
    $body = ($lines[($start + 1)..($end - 1)] | ForEach-Object { $_ -replace '^\s{7}', '' }) -join "`n"
    return [pscustomobject]@{ Title = $title; Body = $body.Trim() }
}

function Find-MSBuild {
    foreach ($ed in 'Community', 'Professional', 'Enterprise') {
        $p = "C:\Program Files\Microsoft Visual Studio\2022\$ed\MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $p) { return $p }
    }
    return $null
}

# Steam Workshop "Change Notes" text: a CHANGELOG entry converted from Markdown to Steam BBCode
# (bullets -> [list][*], **bold** -> [b], `code` -> plain), capped under Steam's 8000-char limit.
# Used by ship.ps1 (workshop stage) and workshop_description.ps1.
function ConvertTo-SteamChangeNote([string]$Version, [string]$Title, [string]$Body, [string]$ChangelogUrl) {
    $out = New-Object System.Collections.Generic.List[string]
    $head = if ($Title.StartsWith($Version)) { $Title } else { "$Version - $Title" }
    $out.Add("[b]$head[/b]")
    $inList = $false
    foreach ($raw in (($Body -replace "`r", '') -split "`n")) {
        $line = $raw.Trim()
        if (-not $line) { continue }
        $line = [regex]::Replace($line, '\*\*(.+?)\*\*', '[b]$1[/b]')
        $line = $line -replace '`', ''
        if ($line -match '^[-*]\s+(.*)$') {
            if (-not $inList) { $out.Add('[list]'); $inList = $true }
            $out.Add("[*]$($Matches[1])")
        } else {
            if ($inList) { $out.Add('[/list]'); $inList = $false }
            $out.Add($line)
        }
    }
    if ($inList) { $out.Add('[/list]') }
    $note = ($out -join "`n")
    if ($note.Length -gt 7900) { $note = $note.Substring(0, 7900) + "`n... full notes: $ChangelogUrl" }
    return $note
}