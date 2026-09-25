# scripts/make_readme.ps1
#
# PURPOSE: Generates the user README.txt that ships inside every package (Nexus / GitHub / GitLab zip,
#          Steam Workshop .map) at BepInEx\plugins\CrusaderDETweaker\README.txt. NEVER hand-edit a
#          README.txt: it is rebuilt from CONFIGURATION_GUIDE.md + CHANGELOG.md + PluginInfo.cs on every
#          build (build.ps1 writes it into the build output).
#
# USAGE:
#   .\scripts\make_readme.ps1 -ReadmeOutPath dist\README.txt      # write one (version from PluginInfo.cs)
#   . (Join-Path $PSScriptRoot 'make_readme.ps1')             # dot-source: defines the functions only
#
# OUTPUT: plain text, wrapped at 100 columns, CRLF line endings, UTF-8 without BOM. The second line is
#         "Version: <x.y.z>"; Test-UserReadme (package / ship verification) checks exactly that line.
#
# CONVERSION (CONFIGURATION_GUIDE.md is Nexus BBCode):
#   [size=6]/[center] title -> "=" underline, [size=5] heading -> "-" underline, [list] -> "- ",
#   [list=1] -> "1. ", [code] blocks indented 4 spaces (not wrapped), [url=x]text[/url] -> "text (x)",
#   [b] [i] [u] [size] [center] stripped. CHANGELOG markdown: ** and ` stripped.
#
# Parameter names are unique on purpose: _release_common.ps1 dot-sources this file into scripts that
# have their own $Version / $OutputPath, and a dot-sourced param block would overwrite those.
param(
    [string]$ReadmeOutPath = '',
    [string]$ReadmeVersion = ''
)

$script:ReadmeWidth = 100

function Get-ReadmeRepoRoot { Split-Path $PSScriptRoot -Parent }

function ConvertFrom-BBCodeInline([string]$Text) {
    $t = [regex]::Replace($Text, '\[url=([^\]]+)\](.*?)\[/url\]', {
        param($m)
        $url = $m.Groups[1].Value; $label = $m.Groups[2].Value
        if ($label -eq $url -or $label -eq '') { $url } else { "$label ($url)" }
    })
    $t = [regex]::Replace($t, '\[url\](.*?)\[/url\]', '$1')
    $t = [regex]::Replace($t, '\[/?(b|i|u|center)\]', '')
    $t = [regex]::Replace($t, '\[size=[^\]]*\]|\[/size\]', '')
    return $t
}

# Word-wrap one logical line; $first / $rest are the prefixes of the first and following lines.
function Format-Wrapped([string]$Text, [string]$First = '', [string]$Rest = '') {
    $out = New-Object System.Collections.Generic.List[string]
    $line = $First
    $empty = $true
    foreach ($word in ($Text -split '\s+' | Where-Object { $_ -ne '' })) {
        if (-not $empty -and ($line.Length + 1 + $word.Length) -gt $script:ReadmeWidth) {
            $out.Add($line.TrimEnd())
            $line = $Rest + $word
        } elseif ($empty) {
            $line = $line + $word
        } else {
            $line = $line + ' ' + $word
        }
        $empty = $false
    }
    if (-not $empty) { $out.Add($line.TrimEnd()) }
    return $out
}

function Add-Heading($Out, [string]$Title, [char]$Underline) {
    if ($Out.Count -and $Out[$Out.Count - 1] -ne '') { $Out.Add('') }
    $Out.Add($Title)
    $Out.Add(([string]$Underline) * $Title.Length)
    $Out.Add('')
}

# CONFIGURATION_GUIDE.md (BBCode) -> list of plain-text lines.
function ConvertFrom-BBCodeDocument([string]$Source) {
    $out = New-Object System.Collections.Generic.List[string]
    $lists = New-Object System.Collections.Generic.List[object]   # stack of @{ Ordered; N }
    $inCode = $false
    foreach ($raw in ($Source -replace "`r`n", "`n" -split "`n")) {
        $line = $raw.TrimEnd()

        if ($inCode) {
            if ($line.Trim() -eq '[/code]') { $inCode = $false; $out.Add(''); continue }
            $out.Add(('    ' + $line).TrimEnd())
            continue
        }
        if ($line.Trim() -eq '[code]') {
            $inCode = $true
            if ($out.Count -and $out[$out.Count - 1] -ne '') { $out.Add('') }
            continue
        }

        $m = [regex]::Match($line, '^\s*(\[center\])?\[size=(\d)\]\[b\](.*?)\[/b\]\[/size\](\[/center\])?\s*$')
        if ($m.Success) {
            $title = ConvertFrom-BBCodeInline $m.Groups[3].Value
            Add-Heading $out $title ($(if ([int]$m.Groups[2].Value -ge 6) { '=' } else { '-' }))
            continue
        }

        $trim = $line.Trim()
        if ($trim -eq '[list]' -or $trim -match '^\[list=1\]$') {
            if ($lists.Count -eq 0 -and $out.Count -and $out[$out.Count - 1] -ne '') { $out.Add('') }
            $lists.Add(@{ Ordered = ($trim -ne '[list]'); N = 0 })
            continue
        }
        if ($trim -eq '[/list]') {
            if ($lists.Count) { $lists.RemoveAt($lists.Count - 1) }
            $out.Add('')
            continue
        }

        if ($trim.StartsWith('[*]') -and $lists.Count) {
            $top = $lists[$lists.Count - 1]
            $indent = '  ' * ($lists.Count - 1)
            if ($top.Ordered) { $top.N++; $marker = "$($top.N). " } else { $marker = '- ' }
            $text = ConvertFrom-BBCodeInline $trim.Substring(3).Trim()
            foreach ($l in (Format-Wrapped $text ($indent + $marker) ($indent + (' ' * $marker.Length)))) { $out.Add($l) }
            continue
        }

        if ($trim -eq '') {
            if ($out.Count -and $out[$out.Count - 1] -ne '') { $out.Add('') }
            continue
        }

        foreach ($l in (Format-Wrapped (ConvertFrom-BBCodeInline $trim))) { $out.Add($l) }
    }
    return $out
}

# The CHANGELOG body of one version (markdown bullets) -> wrapped plain-text lines.
function ConvertFrom-ChangelogBody([string]$Body) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($raw in ($Body -replace "`r`n", "`n" -split "`n")) {
        $t = $raw.Trim()
        if ($t -eq '') { continue }
        $t = $t -replace '\*\*', '' -replace '`', ''
        $m = [regex]::Match($raw, '^(\s*)- (.*)$')
        if ($m.Success) {
            $depth = [math]::Floor($m.Groups[1].Value.Length / 2)
            $indent = '  ' * $depth
            $text = ($m.Groups[2].Value -replace '\*\*', '' -replace '`', '').Trim()
            foreach ($l in (Format-Wrapped $text ($indent + '- ') ($indent + '  '))) { $out.Add($l) }
        } else {
            foreach ($l in (Format-Wrapped $t)) { $out.Add($l) }
        }
    }
    return $out
}

function New-UserReadme([string]$Version) {
    $root = Get-ReadmeRepoRoot
    if (-not $Version) { $Version = Get-PluginVersion }
    $entry = Get-ChangelogEntry $Version
    if (-not $entry -or -not $entry.Body) { throw "CHANGELOG.md has no '$Version - ...' entry for the README 'What's new' section" }
    $guide = [IO.File]::ReadAllText((Join-Path $root 'CONFIGURATION_GUIDE.md'))

    $doc = New-Object System.Collections.Generic.List[string]
    $title = 'Crusader DE Tweaker'
    $doc.Add($title)
    $doc.Add("Version: $Version")
    $doc.Add('A BepInEx mod for Stronghold Crusader: Definitive Edition: edit unit, structure, damage and')
    $doc.Add('gameplay values with plain config files.')
    $doc.Add('')
    $doc.Add('This file is generated from the mod''s CONFIGURATION_GUIDE and CHANGELOG for this version.')

    Add-Heading $doc 'Requirements' '-'
    foreach ($x in @(
        'Stronghold Crusader: Definitive Edition (Steam).',
        'BepInEx 5 Bootstrapper (https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/36).',
        'SHCDE Script Extender 2.8.0 or newer (https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/35).')) {
        foreach ($l in (Format-Wrapped $x '- ' '  ')) { $doc.Add($l) }
    }

    Add-Heading $doc 'Install' '-'
    $n = 0
    foreach ($x in @(
        'Install the BepInEx 5 Bootstrapper and the Script Extender (2.8.0 or newer) first.',
        'Nexus / GitHub / GitLab zip: extract it into the game folder (it contains a BepInEx folder; merge it with the existing one). Steam Workshop: subscribe to the item.',
        'Start the game once and go to the main menu. This creates the config files in BepInEx\config\CrusaderDETweaker\ in the game folder.',
        ('Check BepInEx\LogOutput.log: it contains "Loading [Crusader DE Tweaker {0}]" and "TEST SUITES PASSED".' -f $Version))) {
        $n++; foreach ($l in (Format-Wrapped $x "$n. " '   ')) { $doc.Add($l) }
    }

    Add-Heading $doc 'Update' '-'
    foreach ($l in (Format-Wrapped 'Replace the BepInEx\plugins\CrusaderDETweaker folder with the new one (or let Steam update the Workshop item). Your config files in BepInEx\config\CrusaderDETweaker\ are kept: the mod never resets your values, it only adds new settings at launch.')) { $doc.Add($l) }

    Add-Heading $doc 'Uninstall' '-'
    foreach ($l in (Format-Wrapped 'Delete the folder BepInEx\plugins\CrusaderDETweaker (Workshop: unsubscribe). Your config files stay in BepInEx\config\CrusaderDETweaker\; delete that folder too to remove everything.')) { $doc.Add($l) }

    Add-Heading $doc 'Where the configs live' '-'
    foreach ($x in @(
        '<game folder>\BepInEx\config\CrusaderDETweaker\ : CrusaderDETweaker_Units.toml, CrusaderDETweaker_Structures.toml, CrusaderDETweaker_GameplaySettings.toml, CrusaderDETweaker_GlobalMultipliers.cfg',
        '<game folder>\BepInEx\config\CrusaderDETweaker\DamageMatrices\ : the 7 damage matrix CSV files',
        'Log file: <game folder>\BepInEx\LogOutput.log (overwritten at every launch)')) {
        foreach ($l in (Format-Wrapped $x '- ' '  ')) { $doc.Add($l) }
    }

    Add-Heading $doc 'Report a bug / request a feature' '-'
    foreach ($x in @(
        'Bug report: https://gitlab.com/ensrick7/crusader-de-tweaker/-/issues/new?issuable_template=Bug%20report (or https://github.com/Ensrick/crusader-de-tweaker/issues/new?template=bug_report.yml)',
        'Feature request: https://gitlab.com/ensrick7/crusader-de-tweaker/-/issues/new?issuable_template=Feature%20request (or https://github.com/Ensrick/crusader-de-tweaker/issues/new?template=feature_request.yml)',
        'Attach BepInEx\LogOutput.log from the session that shows the problem and the config file you edited. The section "Reporting a Problem or Requesting a Feature" below has the full checklist.')) {
        foreach ($l in (Format-Wrapped $x '- ' '  ')) { $doc.Add($l) }
    }

    Add-Heading $doc 'License and source' '-'
    foreach ($x in @(
        'MIT License. Source: https://gitlab.com/ensrick7/crusader-de-tweaker (mirror: https://github.com/Ensrick/crusader-de-tweaker).',
        'Nexus: https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/38  Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3726034964',
        'Script Extender by Rawra.')) {
        foreach ($l in (Format-Wrapped $x '- ' '  ')) { $doc.Add($l) }
    }

    Add-Heading $doc "What's new in $Version" '-'
    foreach ($l in (ConvertFrom-ChangelogBody $entry.Body)) { $doc.Add($l) }
    foreach ($l in (Format-Wrapped 'Full history: CHANGELOG.md in the source repository.')) { $doc.Add(''); $doc.Add($l) }

    $doc.Add('')
    $doc.Add('')
    Add-Heading $doc 'CONFIGURATION GUIDE' '='
    foreach ($l in (ConvertFrom-BBCodeDocument $guide)) { $doc.Add($l) }

    # Collapse runs of blank lines, trim the end.
    $final = New-Object System.Collections.Generic.List[string]
    foreach ($l in $doc) {
        if ($l -eq '' -and $final.Count -and $final[$final.Count - 1] -eq '' -and ($final.Count -lt 2 -or $final[$final.Count - 2] -eq '')) { continue }
        $final.Add($l)
    }
    while ($final.Count -and $final[$final.Count - 1] -eq '') { $final.RemoveAt($final.Count - 1) }
    return ($final -join "`r`n") + "`r`n"
}

function Write-UserReadme([string]$Path, [string]$Version) {
    $text = New-UserReadme $Version
    $Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding($false)))
}

# Throws unless $Path is a non-empty README.txt whose second line is "Version: $Version".
function Test-UserReadme([string]$Path, [string]$Version) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "README.txt missing: $Path" }
    $Path = (Resolve-Path -LiteralPath $Path).Path   # .NET resolves relative paths against the process dir
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 200) { throw "README.txt is empty or truncated ($($bytes.Length) bytes): $Path" }
    if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "README.txt has a UTF-8 BOM: $Path" }
    $lines = [Text.Encoding]::UTF8.GetString($bytes) -split "`r`n"
    if ($lines.Count -lt 2 -or $lines[1] -ne "Version: $Version") { throw "README.txt version line is '$($lines[1])', expected 'Version: $Version': $Path" }
}

if ($ReadmeOutPath) {
    # Capture the arguments first: _release_common.ps1 dot-sources this file again (functions only),
    # which resets $ReadmeOutPath / $ReadmeVersion in this scope.
    $target = $ReadmeOutPath; $ver = $ReadmeVersion
    . (Join-Path $PSScriptRoot '_release_common.ps1')
    if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path (Get-ReadmeRepoRoot) $target }
    if (-not $ver) { $ver = Get-PluginVersion }
    Write-UserReadme $target $ver
    Test-UserReadme $target $ver
    Write-Host "README.txt written: $target (version $ver)" -ForegroundColor Green
}
