<#
.SYNOPSIS
    Windowless in-game self-test of the installed Crusader DE Tweaker (Tests/HeadlessSelfTest.cs).

.DESCRIPTION
    Launches the game in -batchmode (no window, no sound) with -cdt-selftest. The plugin creates a
    disposable editor map, checks the unit limit (UnitLimit) and the advanced gameplay options' master
    switches (a Spearman with ImprovedSpearman, in a real custom skirmish run last), team colours on the real palettes /
    UI tables and Speed values above the Script Extender's 6 on spawned units, apothecary healing (["Apothecary Healing"])
    and BedouinHealMultiplier 0 on real units, lobby-tab MaxCount values, measures how far an Archer engages with the
    game's ranges, with AttackRange only, EngageRange only and both (and a Crossbowman), checks the economy settings
    (Stockpile build cost, worker GoodYieldMultiplier, skirmish starting troops), writes PASS / FAIL and quits.
    -Only economy runs the menu, colour and speed-table checks and the economy stages only (a few minutes).
    -Only pools runs only the building limit and sprite pool stages (editor map, thousands of units).
    -Quick (development) runs only the unit limit / gameplay options stages (editor map, then a real custom skirmish).

    Config handling: the whole BepInEx\config\CrusaderDETweaker folder is copied into the run folder
    first, the test values below are written, and afterwards every file is put back byte for byte;
    files the run created are moved into the run folder (never deleted). The previous LogOutput.log is
    kept as LogOutput.before.log in the run folder.

    Refuses when the game is running or Steam is not; stops only the process it started.
    Evidence: logs\selftest-<timestamp>\ (result.txt, unity.log, LogOutput.log, config-before\).

    Pattern: shcde-naval-mod\runtime\test-headless.ps1 (SteamAppId environment, hidden window check).

.EXAMPLE
    .\scripts\build.ps1 -Deploy; .\scripts\test_headless.ps1
#>
[CmdletBinding()]
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [int]$TimeoutSeconds = 5400,
    [ValidateSet('', 'economy', 'pools')][string]$Only = '',
    [switch]$Quick
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$processName = 'Stronghold Crusader Definitive Edition'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Keep in sync with Tests/HeadlessSelfTest.cs.
$unitSpeeds = [ordered]@{ 'CHIMP_TYPE_CATAPULT' = 8; 'CHIMP_TYPE_SIEGE_TOWER' = 1; 'CHIMP_TYPE_ARCHER' = 8 }
$teamColors = [ordered]@{ 'Red' = '[0, 255, 0]'; 'Blue' = '"#FF00FF"' }
$xbowRanges = [ordered]@{ 'AttackRange' = 80; 'EngageRange' = 80 }   # Archer, tiles; the game's AttackRange is 54, engage distance 50
$apothecaryHealing = [ordered]@{ 'HealPercent' = '10'; 'HealHitPoints' = '-1'; 'RadiusTiles' = '8'; 'IntervalTicks' = '50'; 'OutOfCombatTicks' = '200'; 'NeedsWorker' = 'false' }
$bedouinHealMultiplier = '0'   # HeadlessSelfTest.BedouinHealTestMultiplier
$macemanMaxCount = '1'         # HeadlessSelfTest.FileMacemanCap (the lobby MaxCount stage overrides it with -1, then clears the box)
$stockpileCost = [ordered]@{ 'GoldCost' = 20; 'WoodCost' = 5 }       # [STRUCT_GOODS_YARD]; the game's Stockpile is free
$goodYield = [ordered]@{ 'CHIMP_TYPE_WOODCUTTER' = '2.0'; 'CHIMP_TYPE_HUNTER' = '1.25' }   # GoodYieldMultiplier
$startTroops = [ordered]@{ 'Archer' = 3; 'Spearman' = 0; 'Knight' = 2 }   # ["Skirmish Starting Troops".Normal]; the real skirmish stage expects exactly these
$unitLimit = 4000   # ["Army Size"] UnitLimit (HeadlessSelfTest.LimitConfigured); the game's value is 3000
$buildingLimit = 32000   # ["Building Limit"] BuildingLimit (HeadlessSelfTest.BuildingLimitConfigured); the game's value is 4000
# ImprovedSpearman on with both master switches off: the switches must be raised by the mod itself.
$gameplayOptions = [ordered]@{ 'ImprovedSpearman' = 'true'; 'AdvancedOptionsEnabled' = 'false'; 'AdvancedSkirmishOptionsEnabled' = 'false' }

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) { throw 'The game is running. Close it first: this script never touches a running game.' }
if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) { throw 'Steam must already be running. This script will not open it.' }
$exe = Join-Path $GamePath "$processName.exe"
$plugin = Join-Path $GamePath 'BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll'
$built = Join-Path $repo 'bin\Release\CrusaderDETweaker.dll'
if (-not (Test-Path -LiteralPath $plugin)) { throw 'Crusader DE Tweaker is not installed. Run .\scripts\deploy.ps1 first.' }
if ((Test-Path -LiteralPath $built) -and (Get-FileHash -LiteralPath $built).Hash -ne (Get-FileHash -LiteralPath $plugin).Hash) {
    throw 'The installed DLL differs from bin\Release. Run .\scripts\deploy.ps1 first.'
}
$bepCfg = Get-Content -LiteralPath (Join-Path $GamePath 'BepInEx\config\BepInEx.cfg') -Raw
$console = [regex]::Match($bepCfg, '(?ms)^\[Logging\.Console\]\s*\r?\n(?<body>.*?)(?=^\[|\z)').Groups['body'].Value
if ($console -match '(?m)^Enabled\s*=\s*true\s*$') { throw 'BepInEx console is enabled; a windowless run needs it off.' }

$run = Join-Path $repo ('logs\selftest-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null

# Results only count for the current Script Extender: report the installed version against the latest release.
$seInfo = Join-Path $GamePath 'BepInEx\plugins\000shcdese\info.json'
$seInstalled = if (Test-Path -LiteralPath $seInfo) { (Get-Content -LiteralPath $seInfo -Raw | ConvertFrom-Json).Version } else { 'not installed' }
try {
    $seLatest = (Invoke-RestMethod -Uri 'https://gitlab.com/api/v4/projects/74440776/releases?per_page=1' -TimeoutSec 15)[0].tag_name.TrimStart('v')
} catch { $seLatest = 'unknown (GitLab not reachable)' }
$seLine = "Script Extender installed $seInstalled, latest release $seLatest"
if ($seLatest -match '^\d' -and $seInstalled -ne $seLatest) { Write-Warning "$seLine. Results are for the installed version; update the Script Extender (with the owner's go) before trusting them." }
else { Write-Host $seLine }
Set-Content -LiteralPath (Join-Path $run 'script-extender.txt') -Value $seLine -Encoding utf8
$result = Join-Path $run 'result.txt'
$configDir = Join-Path $GamePath 'BepInEx\config\CrusaderDETweaker'
$snapshot = Join-Path $run 'config-before'
$bepLog = Join-Path $GamePath 'BepInEx\LogOutput.log'

function Get-ConfigHashes([string]$dir) {
    $map = @{}
    if (Test-Path -LiteralPath $dir) {
        Get-ChildItem -LiteralPath $dir -Recurse -File | ForEach-Object {
            $map[$_.FullName.Substring($dir.Length).TrimStart('\')] = (Get-FileHash -LiteralPath $_.FullName).Hash
        }
    }
    return $map
}

function Set-TomlKey([string]$text, [string]$header, [string]$key, [string]$value) {
    $h = [regex]::Escape($header)
    $section = [regex]::Match($text, "(?ms)^$h[ \t]*\r?\n(?<body>.*?)(?=^\[|\z)")
    if (-not $section.Success) { throw "Section $header not found" }
    $body = $section.Groups['body']
    $line = [regex]::Match($body.Value, "(?m)^$([regex]::Escape($key))[ \t]*=.*?(?=\r?$)")
    if ($line.Success) {
        $start = $body.Index + $line.Index
        return $text.Substring(0, $start) + "$key = $value" + $text.Substring($start + $line.Length)
    }
    return $text.Substring(0, $body.Index) + "$key = $value`r`n" + $text.Substring($body.Index)
}

function Add-TomlSection([string]$text, [string]$header) {
    if ($text -match ('(?m)^' + [regex]::Escape($header) + '[ \t]*\r?$')) { return $text }
    return $text.TrimEnd() + "`r`n`r`n$header`r`n"
}

$before = Get-ConfigHashes $configDir
if (Test-Path -LiteralPath $configDir) { Copy-Item -LiteralPath $configDir -Destination $snapshot -Recurse }
if (Test-Path -LiteralPath $bepLog) { Copy-Item -LiteralPath $bepLog -Destination (Join-Path $run 'LogOutput.before.log') }

$owned = $null
$oldApp = $env:SteamAppId; $oldGame = $env:SteamGameId
try {
    # Test values. A missing Units file is generated at launch with -1 everywhere, which this test cannot use.
    $unitsPath = Join-Path $configDir 'CrusaderDETweaker_Units.toml'
    $globalsPath = Join-Path $configDir 'CrusaderDETweaker_GameplaySettings.toml'
    $multipliersPath = Join-Path $configDir 'CrusaderDETweaker_GlobalMultipliers.cfg'
    if (-not (Test-Path -LiteralPath $unitsPath) -or -not (Test-Path -LiteralPath $globalsPath) -or -not (Test-Path -LiteralPath $multipliersPath)) {
        throw 'Config files missing; launch the game once with the mod installed to generate them.'
    }
    $units = [IO.File]::ReadAllText($unitsPath)
    foreach ($k in $unitSpeeds.Keys) { $units = Set-TomlKey $units "[$k]" 'Speed' ([string]$unitSpeeds[$k]) }
    foreach ($k in $xbowRanges.Keys) { $units = Set-TomlKey $units '[CHIMP_TYPE_ARCHER]' $k ([string]$xbowRanges[$k]) }
    $units = Set-TomlKey $units '[CHIMP_TYPE_XBOWMAN]' 'EngageRange' '80'   # applied through the game constants; crashed with the Script Extender's hook (HeadlessSelfTest.GuardEngageRange)
    $units = Set-TomlKey $units '[CHIMP_TYPE_MACEMAN]' 'MaxCount' $macemanMaxCount
    foreach ($k in $goodYield.Keys) { $units = Set-TomlKey $units "[$k]" 'GoodYieldMultiplier' $goodYield[$k] }
    [IO.File]::WriteAllText($unitsPath, $units, $utf8)
    $globals = [IO.File]::ReadAllText($globalsPath)
    if ($globals -notmatch '(?m)^\["Team Colors"\]') { $globals = $globals.TrimEnd() + "`r`n`r`n[`"Team Colors`"]`r`n" }
    foreach ($k in $teamColors.Keys) { $globals = Set-TomlKey $globals '["Team Colors"]' $k $teamColors[$k] }
    if ($globals -notmatch '(?m)^\["Apothecary Healing"\]') { $globals = $globals.TrimEnd() + "`r`n`r`n[`"Apothecary Healing`"]`r`n" }
    foreach ($k in $apothecaryHealing.Keys) { $globals = Set-TomlKey $globals '["Apothecary Healing"]' $k $apothecaryHealing[$k] }
    $multipliers = Set-TomlKey ([IO.File]::ReadAllText($multipliersPath)) '[Multipliers]' 'BedouinHealMultiplier' $bedouinHealMultiplier
    [IO.File]::WriteAllText($multipliersPath, $multipliers, $utf8)
    $globals = Add-TomlSection $globals '["Skirmish Starting Troops".Normal]'
    foreach ($k in $startTroops.Keys) { $globals = Set-TomlKey $globals '["Skirmish Starting Troops".Normal]' $k ([string]$startTroops[$k]) }
    if ($globals -notmatch '(?m)^\["Army Size"\]') { $globals = $globals.TrimEnd() + "`r`n`r`n[`"Army Size`"]`r`n" }
    $globals = Set-TomlKey $globals '["Army Size"]' 'UnitLimit' ([string]$unitLimit)
    $globals = Add-TomlSection $globals '["Building Limit"]'
    $globals = Set-TomlKey $globals '["Building Limit"]' 'BuildingLimit' ([string]$buildingLimit)
    foreach ($k in $gameplayOptions.Keys) { $globals = Set-TomlKey $globals '["Gameplay Options"]' $k $gameplayOptions[$k] }
    [IO.File]::WriteAllText($globalsPath, $globals, $utf8)
    # Older Structures files have no Stockpile section; the launch keeps these values when it adds it.
    $structuresPath = Join-Path $configDir 'CrusaderDETweaker_Structures.toml'
    $structures = Add-TomlSection ([IO.File]::ReadAllText($structuresPath)) '[STRUCT_GOODS_YARD]'
    foreach ($k in $stockpileCost.Keys) { $structures = Set-TomlKey $structures '[STRUCT_GOODS_YARD]' $k ([string]$stockpileCost[$k]) }
    [IO.File]::WriteAllText($structuresPath, $structures, $utf8)
    Write-Host "Test values written: $($unitSpeeds.Keys | ForEach-Object { "$_ Speed=$($unitSpeeds[$_])" }); Team Colors Red=$($teamColors.Red) Blue=$($teamColors.Blue); Archer AttackRange=$($xbowRanges.AttackRange) EngageRange=$($xbowRanges.EngageRange); Apothecary Healing $(($apothecaryHealing.Keys | ForEach-Object { "$_=$($apothecaryHealing[$_])" }) -join ' '); BedouinHealMultiplier=$bedouinHealMultiplier; Maceman MaxCount=$macemanMaxCount; Stockpile GoldCost=$($stockpileCost.GoldCost) WoodCost=$($stockpileCost.WoodCost); GoodYieldMultiplier $($goodYield.Keys | ForEach-Object { "$_=$($goodYield[$_])" }); Skirmish Starting Troops Normal Archer=$($startTroops.Archer) Spearman=$($startTroops.Spearman) Knight=$($startTroops.Knight); UnitLimit=$unitLimit; BuildingLimit=$buildingLimit; $($gameplayOptions.Keys | ForEach-Object { "$_=$($gameplayOptions[$_])" })"

    $env:SteamAppId = '3024040'; $env:SteamGameId = '3024040'
    $arguments = '-batchmode -nosound -silent-crashes -cdt-selftest "' + $result + '" -logFile "' + (Join-Path $run 'unity.log') + '"'
    if ($Only) { $arguments += " -cdt-selftest-only $Only" }
    if ($Quick) { $arguments += ' -cdt-selftest-quick' }
    $owned = Start-Process -FilePath $exe -WorkingDirectory $GamePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $env:SteamAppId = $oldApp; $env:SteamGameId = $oldGame
    try { $owned.PriorityClass = 'BelowNormal' } catch { }
    Write-Host "Self-test PID $($owned.Id); evidence $run"

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not $owned.HasExited) {
        if ((Get-Date) -gt $deadline) { throw "Self-test timed out after $TimeoutSeconds s; see $run" }
        $owned.Refresh()
        if ($owned.MainWindowHandle -ne 0) { throw 'A game window appeared; stopping the test process.' }
        Start-Sleep -Milliseconds 1000
    }
    if (-not (Test-Path -LiteralPath $result)) { throw "The game exited (code $($owned.ExitCode)) without a result; see $run" }
    Get-Content -LiteralPath $result
}
finally {
    $env:SteamAppId = $oldApp; $env:SteamGameId = $oldGame
    # Stop only the process this script started.
    if ($owned -and -not $owned.HasExited) { Stop-Process -InputObject $owned -Force; $owned.WaitForExit(15000) | Out-Null }
    if (Test-Path -LiteralPath $bepLog) { Copy-Item -LiteralPath $bepLog -Destination (Join-Path $run 'LogOutput.log') }

    # Put every config file back exactly; move files the run created into the run folder.
    $after = Get-ConfigHashes $configDir
    $restored = 0; $moved = 0
    foreach ($rel in $before.Keys) {
        if ($after[$rel] -ne $before[$rel]) {
            $target = Join-Path $configDir $rel
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $snapshot $rel) -Destination $target -Force
            $restored++
        }
    }
    foreach ($rel in $after.Keys) {
        if (-not $before.ContainsKey($rel)) {
            $dest = Join-Path (Join-Path $run 'config-created') $rel
            New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
            Move-Item -LiteralPath (Join-Path $configDir $rel) -Destination $dest
            $moved++
        }
    }
    $check = Get-ConfigHashes $configDir
    $same = ($check.Count -eq $before.Count) -and -not ($before.Keys | Where-Object { $check[$_] -ne $before[$_] })
    Write-Host "Configs: $restored file(s) restored, $moved created file(s) moved to the run folder; identical to before: $same"
}
if ((Get-Content -LiteralPath $result -First 1) -ne 'PASS') { throw "Self-test FAILED; see $run" }
