# scripts/workshop_description.ps1
#
# PURPOSE: Update ONLY the Steam Workshop description (and optionally post a Change Note) of item
#          3726034964, without re-uploading mod content. Use it when the page text changes between
#          releases; a release itself goes through ship.ps1 (its workshop stage sends both).
#
# DRY-RUN IS THE DEFAULT: without -Publish it prints what it would send.
#
# USAGE:
#   .\scripts\workshop_description.ps1                                   # dry run
#   .\scripts\workshop_description.ps1 -Publish                          # description only
#   .\scripts\workshop_description.ps1 -Publish -ChangeNoteVersion 2.7.0 # + that CHANGELOG entry as Change Note
#
# VERIFIED BY: Steam Web API GetPublishedFileDetails description == the file (after Steam's own trimming).

[CmdletBinding()]
param(
    [switch]$Publish,
    [string]$ChangeNoteVersion = "",
    [string]$WorkshopId  = "3726034964",
    [string]$SteamAppId  = "3024040",
    [string]$UploaderExe = "C:\Users\danjo\source\repos\_shcdese_v1.31.0\uploader\pdengine.steamugc.tool.exe",
    [string]$Description = "",
    [string]$GitLabRepo  = "ensrick7/crusader-de-tweaker"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_release_common.ps1')
if (-not $Description) { $Description = Join-Path $script:RepoRoot 'workshop\workshop-description.txt' }

$desc = [IO.File]::ReadAllText($Description)
if ($desc.Length -gt 8000) { throw "Workshop description is $($desc.Length) chars; Steam's cap is 8000 (-z fails with k_EResultInvalidParam)." }

$args2 = @('-v', '-a', $SteamAppId, '-i', $WorkshopId, '-u', '-z', $Description)
$note = $null
if ($ChangeNoteVersion) {
    $entry = Get-ChangelogEntry $ChangeNoteVersion
    $note = ConvertTo-SteamChangeNote -Version $ChangeNoteVersion -Title $entry.Title -Body $entry.Body `
        -ChangelogUrl "https://gitlab.com/$GitLabRepo/-/blob/main/CHANGELOG.md"
    $args2 += @('-c', $note)
}

Write-Host "=== Workshop description update: item $WorkshopId ($($desc.Length) chars) ===" -ForegroundColor Cyan
if ($note) { Write-Host "Change Note ($($note.Length) chars):`n$note" -ForegroundColor Gray }
if (-not $Publish) { Write-Host "DRY RUN: nothing sent. Re-run with -Publish." -ForegroundColor Yellow; exit 0 }

& $UploaderExe @args2 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "uploader exit $LASTEXITCODE" }

# Verify from Steam, not from the tool's own output.
$norm = { param($s) (($s -replace "`r", '') -replace '\s+', ' ').Trim() }
for ($i = 0; $i -lt 6; $i++) {
    Start-Sleep -Seconds (5 * $i)
    $resp = Invoke-RestMethod -Method Post -Uri 'https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/' `
        -Body @{ itemcount = 1; 'publishedfileids[0]' = $WorkshopId }
    $live = $resp.response.publishedfiledetails[0].description
    if ((& $norm $live) -eq (& $norm $desc)) { Write-Host "[OK] live description matches ($($live.Length) chars)" -ForegroundColor Green; exit 0 }
}
Write-Host "[FAIL] live description does not match the file after ~75 s (Steam may still be caching; re-check before re-running)." -ForegroundColor Red
exit 4
