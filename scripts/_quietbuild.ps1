# Quiet build wrapper for overhaul iteration — runs build.ps1, prints only the
# result summary and any real compiler diagnostics (not the giant csc line).
# TEMPORARY dev aid; safe to delete after the overhaul.
param([string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition")
$log = Join-Path $env:TEMP "cdt_build.log"
& "$PSScriptRoot\build.ps1" -GamePath $GamePath *>&1 | Out-File -FilePath $log -Encoding utf8
Get-Content $log | Where-Object {
    $_ -match '(: error |: warning |error CS|warning CS|Build succeeded|Build FAILED|Build successful|Build failed|\d+ Warning\(s\)|\d+ Error\(s\)|not found at expected)'
} | ForEach-Object { $_.Trim() }
