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
    -Only engines runs only the siege engine range stage (first-shot distance of the five engines, crewed by Engineers,
    with the game range and a changed AttackRange) and the Crossbowman EngageRange < AttackRange rungs (GitHub #5).
    -Quick (development) runs only the unit limit / gameplay options stages (editor map, then a real custom skirmish).

    Config handling: the whole BepInEx\config\CrusaderDETweaker folder is copied into the run folder
    first, the test values below are written, and afterwards every file is put back byte for byte;
    files the run created are moved into the run folder (never deleted). The previous LogOutput.log is
    kept as LogOutput.before.log in the run folder.

    Refuses when the game is running or Steam is not; stops only the process it started.
    The game runs on a private desktop inside a kill-on-close job (scripts\HiddenDesktopProcess.cs, from the Handicap's
    runner): no window can reach the user's screen, also when this script runs from a detached shell, and the whole
    process tree ends with the script. Every 250 ms a visible window of the job's processes on the user's desktop, or a
    MainWindowHandle of the game, stops the run at once (runner check in overall.txt). One minute after the plugin's
    final result the runner ends the game if it has not quit by itself.
    Evidence: logs\selftest-<timestamp>\ (overall.txt = runner checks + result.txt, unity.log, LogOutput.log,
    config-before\, windows.txt when a window appeared).

    Pattern: shcde-naval-mod\runtime\test-headless.ps1 (SteamAppId environment); CrusaderDEHandicap\scripts\test_headless.ps1
    (private desktop, job, window checks).

.EXAMPLE
    .\scripts\build.ps1 -Deploy; .\scripts\test_headless.ps1
#>
[CmdletBinding()]
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    [int]$TimeoutSeconds = 5400,
    [ValidateSet('', 'economy', 'pools', 'engines')][string]$Only = '',
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

$owned = $null; $windowHits = 0; $privateWindows = @{}; $finalAt = $null; $endedAt = $null; $same = $false
$startedAt = Get-Date
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

    # Private desktop + kill-on-close job (scripts/HiddenDesktopProcess.cs, from the Handicap's runner): no window of the
    # game or of a child it starts can reach the user's screen, and the whole tree ends with this script. Start-Process
    # -WindowStyle Hidden was not enough: a run started from a detached shell showed the window for about 25 s (2026-10-11).
    if (-not ('HiddenDesktopProcess' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'HiddenDesktopProcess.cs') }
    $arguments = '-batchmode -nosound -silent-crashes -cdt-selftest "' + $result + '" -logFile "' + (Join-Path $run 'unity.log') + '"'
    if ($Only) { $arguments += " -cdt-selftest-only $Only" }
    if ($Quick) { $arguments += ' -cdt-selftest-quick' }
    $env:SteamAppId = '3024040'; $env:SteamGameId = '3024040'
    $launchAt = Get-Date
    $owned = [HiddenDesktopProcess]::Start($exe, $arguments, $GamePath)
    $env:SteamAppId = $oldApp; $env:SteamGameId = $oldGame
    Write-Host "Self-test PID $($owned.Pid) on private desktop $($owned.DesktopName); evidence $run"

    $deadline = $launchAt.AddSeconds($TimeoutSeconds)
    $lastNote = Get-Date
    while (-not $owned.WaitForExit(250)) {
        # Every 250 ms: a visible window of any process in the job on the user's desktop stops the run at once.
        $visible = $owned.WindowsOnUserDesktop()
        if ($visible.Count -gt 0) {
            $windowHits++
            Set-Content -LiteralPath (Join-Path $run 'windows.txt') -Value ("WINDOW ON THE USER'S DESKTOP at $(Get-Date -Format o):`n" + ($visible -join "`n")) -Encoding utf8
            $owned.Kill()
            throw "A window of the game appeared on the user's desktop; the game was stopped at once: $($visible -join '; ')"
        }
        # The old check as well (MainWindowHandle looks at the caller's desktop, the user's when run in the foreground).
        $main = Get-Process -Id $owned.Pid -ErrorAction SilentlyContinue
        if ($main -and $main.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowHits++
            Set-Content -LiteralPath (Join-Path $run 'windows.txt') -Value ("MAIN WINDOW ON THE USER'S DESKTOP at $(Get-Date -Format o): handle $($main.MainWindowHandle) '$($main.MainWindowTitle)'") -Encoding utf8
            $owned.Kill()
            throw "The game's main window appeared on the user's desktop; the game was stopped at once."
        }
        foreach ($w in $owned.WindowsOnPrivateDesktop()) { $privateWindows[$w] = $true }
        if ((Get-Date) -gt $deadline) { $owned.Kill(); throw "Self-test timed out after $TimeoutSeconds s; see $run" }
        # The plugin writes its final result (with its END line; the skirmish stages write a provisional FAIL first), then
        # asks the game to quit; a batch-mode quit can take minutes (Handicap, 2026-10-12: 9 min). One minute after the
        # final result the runner ends the job.
        if (-not $finalAt -and (Test-Path -LiteralPath $result) -and (Select-String -LiteralPath $result -Pattern '^END (PASS|FAIL) ' -Quiet)) { $finalAt = Get-Date }
        if ($finalAt -and ((Get-Date) - $finalAt).TotalSeconds -ge 60) { Write-Host 'Result written 60 s ago and the game has not quit; ending its process tree.'; $owned.Kill(); break }
        if (((Get-Date) - $lastNote).TotalSeconds -ge 60) {
            $lastNote = Get-Date
            Write-Host ("[{0:HH:mm:ss}] running {1:N0} s" -f (Get-Date), ((Get-Date) - $launchAt).TotalSeconds)
        }
    }
    $endedAt = Get-Date
    Write-Host "Game exited with code $($owned.ExitCode) after $([int]($endedAt - $launchAt).TotalSeconds) s"
    if (-not (Test-Path -LiteralPath $result)) { throw "The game exited (code $($owned.ExitCode)) without a result; see $run" }
    Get-Content -LiteralPath $result
}
finally {
    $env:SteamAppId = $oldApp; $env:SteamGameId = $oldGame
    # Closing the job ends the process this script started and anything it started (KILL_ON_JOB_CLOSE).
    if ($owned) { $owned.Dispose() }
    if (-not $endedAt) { $endedAt = Get-Date }
    Start-Sleep -Milliseconds 500
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

    # Runner checks + the plugin's result in overall.txt (also when the run threw: the window check is the point).
    $runnerLines = @(
        "$(if ($windowHits -eq 0) { 'PASS' } else { 'FAIL' }) [runner] No game window on the user's desktop: window checks every 250 ms over the job's processes (EnumWindows) and the game's MainWindowHandle: $windowHits hits; windows the game created on its private desktop: $(if ($privateWindows.Count) { ($privateWindows.Keys | Sort-Object) -join '; ' } else { 'none' })",
        "$(if ($same) { 'PASS' } else { 'FAIL' }) [runner] CrusaderDETweaker configs back byte for byte: $($before.Count) files, SHA256 compared ($restored restored, $moved created files moved to the run folder)",
        "NOTE [runner] run $($startedAt.ToString('yyyy-MM-dd HH:mm:ss')) to $($endedAt.ToString('yyyy-MM-dd HH:mm:ss')); $seLine")
    $driver = if (Test-Path -LiteralPath $result) { @(Get-Content -LiteralPath $result) } else { @('FAIL', 'FAIL the plugin wrote no result file') }
    $overallOk = ($driver[0] -eq 'PASS') -and -not ($runnerLines | Where-Object { $_.StartsWith('FAIL') })
    Set-Content -LiteralPath (Join-Path $run 'overall.txt') -Encoding utf8 -Value (@("OVERALL $(if ($overallOk) { 'PASS' } else { 'FAIL' })") + $runnerLines + @('', '--- plugin (result.txt) ---') + $driver)
    $runnerLines | ForEach-Object { Write-Host $_ }
}
if (-not $overallOk) { throw "Self-test FAILED; see $run\overall.txt" }
Write-Host "OVERALL PASS; evidence $run"
