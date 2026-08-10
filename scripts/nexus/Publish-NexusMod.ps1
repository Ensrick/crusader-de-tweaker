<#
.SYNOPSIS
    Local Nexus Mods publisher — uploads a file, adds a version, bumps the mod
    version, and posts a changelog through the official Nexus Upload API (v3).

.DESCRIPTION
    A faithful PowerShell port of the official Nexus-Mods/upload-action
    (https://github.com/Nexus-Mods/upload-action, src/index.ts). Uses the
    sanctioned v3 API with a single `apikey` header — no cookies, no browser
    scraping, no ToS grey area.

    What v3 CAN do (and this script does):
      - Upload a file (multipart, direct-to-S3 presigned PUTs)
      - Add a new version under an existing file slot   (POST /mod-files/{id}/versions)
      - Set the file's version string
      - Bump the mod page version                       (update_mod_version)
      - Archive the previous file version
      - Post a changelog entry                          (POST /mods/{id}/changelogs)

    What v3 CANNOT do (still manual on the website):
      - Edit the mod page DESCRIPTION. No endpoint exists. Refresh it by hand.

    API-KEY HANDLING (never hardcode, never commit):
      Resolved in order: -ApiKey param > $env:NEXUS_API_KEY > nexus.local.json
      (a gitignored file beside this script: { "ApiKey": "..." }).
      The key is never written to the console or logs.

.PARAMETER Action
    validate  : confirm the key works and print rate-limit headroom.
    list-files: list the mod's file slots (find the FileId to target).
    publish   : run the full upload -> version -> changelog flow (default).

.PARAMETER FileId
    The FILE SLOT id to add a new version to (NOT the mod id). Find it under the
    mod's Files tab -> Manage Files, or via -Action list-files.

.PARAMETER ModId
    The mod page id (e.g. 38 for Unit Stat Editor). Needed for list-files and for
    the changelog call.

.EXAMPLE
    # 1. Confirm the key is live and see remaining quota
    ./Publish-NexusMod.ps1 -Action validate

.EXAMPLE
    # 2. Discover the file slot id for the mod
    ./Publish-NexusMod.ps1 -Action list-files -ModId 38

.EXAMPLE
    # 3. Dry run the publish (no network writes) then do it for real
    ./Publish-NexusMod.ps1 -FileId 12345 -ModId 38 -FilePath "D:\...\Crusader DE Tweaker.zip" `
        -Version 2.6.0 -Changelog "Rebuilt against SHCDE-SE 1.40.0 (game 2.8)." -WhatIf
    # remove -WhatIf to publish

.NOTES
    Rate limits: 20,000/day, 500/hour (reset 00:00 GMT / top of hour). A publish
    is ~10 calls. `file_id` != `mod_id`. Changelog silently no-ops without ModId.
    Verified against api.nexusmods.com/v3 and the upload-action source, 2026-08-10.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidateSet('validate', 'list-files', 'publish')]
    [string]$Action = 'publish',

    [string]$GameDomain = 'strongholdcrusaderdefinitiveedition',

    [int]$ModId,
    [int]$FileId,
    [string]$FilePath,
    [string]$Version,

    [string]$DisplayName,
    [string]$Description,
    [ValidateSet('main', 'update', 'optional', 'old_version', 'miscellaneous')]
    [string]$Category = 'main',
    [string]$Changelog,

    # Defaults chosen for a standard "new release" of an existing single-file mod.
    [bool]$UpdateModVersion = $true,
    [bool]$ArchiveExisting  = $true,
    [Nullable[bool]]$PrimaryModManagerDownload,
    [Nullable[bool]]$AllowModManagerDownload,
    [Nullable[bool]]$ShowRequirementsPopup,

    [string]$ApiKey,

    [string]$ApiBase = 'https://api.nexusmods.com/v3'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Key resolution — param > env > gitignored settings file. Never printed.
# ---------------------------------------------------------------------------
function Resolve-ApiKey {
    param([string]$Explicit)
    if ($Explicit) { return $Explicit.Trim() }
    if ($env:NEXUS_API_KEY) { return $env:NEXUS_API_KEY.Trim() }
    $settings = Join-Path $PSScriptRoot 'nexus.local.json'
    if (Test-Path $settings) {
        $json = Get-Content $settings -Raw | ConvertFrom-Json
        if ($json.ApiKey) { return ([string]$json.ApiKey).Trim() }
    }
    throw "No API key found. Set `$env:NEXUS_API_KEY, pass -ApiKey, or create $settings with { `"ApiKey`": `"...`" }. Get the key at https://www.nexusmods.com/users/myaccount?tab=api"
}

$script:ApiKey = Resolve-ApiKey -Explicit $ApiKey

function Invoke-NexusApi {
    <# Calls an api.nexusmods.com/v3 path with the apikey header. Returns parsed JSON.
       On failure, surfaces the response body (v3 returns descriptive 422s). #>
    param(
        [string]$Method,
        [string]$Path,           # relative to $ApiBase, e.g. /uploads/multipart
        [object]$Body,           # hashtable -> JSON, or $null
        [switch]$ReturnResponse  # return the raw HttpResponseMessage-like object
    )
    $headers = @{
        'apikey'     = $script:ApiKey
        'User-Agent' = 'crusader-de-tweaker/Publish-NexusMod.ps1'
    }
    $uri = "$ApiBase$Path"
    $params = @{
        Method  = $Method
        Uri     = $uri
        Headers = $headers
    }
    if ($null -ne $Body) {
        $params.ContentType = 'application/json'
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    try {
        return Invoke-RestMethod @params
    }
    catch {
        $resp = $_.Exception.Response
        $detail = ''
        if ($resp) {
            try {
                $stream = $resp.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $detail = $reader.ReadToEnd()
            } catch { }
        }
        throw "Nexus API $Method $Path failed: $($_.Exception.Message)`n$detail"
    }
}

# ---------------------------------------------------------------------------
# Action: validate — legacy v1 endpoint that reports quota headers cheaply.
# ---------------------------------------------------------------------------
function Invoke-Validate {
    $headers = @{ 'apikey' = $script:ApiKey; 'User-Agent' = 'crusader-de-tweaker/Publish-NexusMod.ps1' }
    try {
        $resp = Invoke-WebRequest -Method Get -Uri 'https://api.nexusmods.com/v1/users/validate.json' -Headers $headers
    }
    catch {
        throw "API key rejected by Nexus ($($_.Exception.Message)). Check the key at https://www.nexusmods.com/users/myaccount?tab=api"
    }
    $user = $resp.Content | ConvertFrom-Json
    Write-Host "API key OK." -ForegroundColor Green
    Write-Host "  Account : $($user.name) (user_id $($user.user_id), premium=$($user.is_premium))"
    Write-Host "  Hourly remaining : $($resp.Headers['x-rl-hourly-remaining']) / $($resp.Headers['x-rl-hourly-limit'])"
    Write-Host "  Daily  remaining : $($resp.Headers['x-rl-daily-remaining']) / $($resp.Headers['x-rl-daily-limit'])"
}

# ---------------------------------------------------------------------------
# Action: list-files — enumerate the mod's file slots to find a FileId.
# ---------------------------------------------------------------------------
function Invoke-ListFiles {
    if (-not $ModId) { throw "-ModId is required for list-files." }
    $data = Invoke-NexusApi -Method Get -Path "/games/$GameDomain/mods/$ModId/files"
    $files = if ($data.PSObject.Properties.Name -contains 'data') { $data.data } else { $data }
    Write-Host "Files for $GameDomain mod $ModId :" -ForegroundColor Cyan
    foreach ($f in $files) {
        $fid = if ($f.PSObject.Properties.Name -contains 'file_id') { $f.file_id } elseif ($f.PSObject.Properties.Name -contains 'id') { $f.id } else { '?' }
        $ver = if ($f.PSObject.Properties.Name -contains 'version') { $f.version } else { '' }
        $cat = if ($f.PSObject.Properties.Name -contains 'category_name') { $f.category_name } elseif ($f.PSObject.Properties.Name -contains 'file_category') { $f.file_category } else { '' }
        $nm  = if ($f.PSObject.Properties.Name -contains 'name') { $f.name } else { '' }
        Write-Host ("  FileId {0,-8} v{1,-12} [{2,-8}] {3}" -f $fid, $ver, $cat, $nm)
    }
    Write-Host "`nUse the FileId of the slot you want to add a new version to." -ForegroundColor Gray
}

# ---------------------------------------------------------------------------
# Action: publish — the 7-step upload flow (faithful to upload-action).
# ---------------------------------------------------------------------------
function Invoke-Publish {
    if (-not $FileId)   { throw "-FileId is required for publish (the file slot to add a version to; use -Action list-files to find it)." }
    if (-not $FilePath) { throw "-FilePath is required for publish." }
    if (-not $Version)  { throw "-Version is required for publish." }
    if (-not (Test-Path $FilePath)) { throw "File not found: $FilePath" }
    if ($Changelog -and -not $ModId) { throw "-ModId is required when -Changelog is set (the changelog call is POST /mods/{ModId}/changelogs)." }

    $file = Get-Item -LiteralPath $FilePath
    $fileName = $file.Name
    $sizeBytes = $file.Length
    $name = if ($DisplayName) { $DisplayName } else { $fileName }

    Write-Host "=== Nexus publish plan ===" -ForegroundColor Cyan
    Write-Host "  Game/Mod/File : $GameDomain / mod $ModId / file slot $FileId"
    Write-Host "  Upload        : $fileName ($([math]::Round($sizeBytes/1KB,1)) KB)"
    Write-Host "  Version       : $Version   (bump mod version: $UpdateModVersion)"
    Write-Host "  Category      : $Category   (archive existing: $ArchiveExisting)"
    Write-Host "  Changelog     : $([bool]$Changelog)"
    Write-Host "  NOTE: mod DESCRIPTION is not editable via API — refresh it on the website." -ForegroundColor Yellow

    if (-not $PSCmdlet.ShouldProcess("$GameDomain mod $ModId file $FileId", "Upload $fileName as v$Version")) {
        Write-Host "`n[dry run] No network writes performed. Remove -WhatIf to publish." -ForegroundColor Yellow
        return
    }

    # --- Step 1: create multipart upload ---
    Write-Host "`n[1/7] Creating multipart upload..." -ForegroundColor White
    $create = Invoke-NexusApi -Method Post -Path '/uploads/multipart' -Body @{
        filename   = $fileName
        size_bytes = [string]$sizeBytes
    }
    $u = $create.data
    $uploadId      = $u.id
    $partUrls      = @($u.part_presigned_urls)
    $partSize      = [int64]$u.part_size_bytes
    $completeUrl   = $u.complete_presigned_url
    Write-Host "  uploadId=$uploadId, $($partUrls.Count) part(s) x $partSize bytes"

    # --- Step 2: PUT each part to its presigned S3 URL, capture ETags ---
    Write-Host "[2/7] Uploading parts..." -ForegroundColor White
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $parts = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $partUrls.Count; $i++) {
        $partNumber = $i + 1
        $offset = [int64]$i * $partSize
        $len = [Math]::Min($partSize, $bytes.Length - $offset)
        $chunk = New-Object byte[] $len
        [Array]::Copy($bytes, $offset, $chunk, 0, $len)

        $putResp = Invoke-WebRequest -Method Put -Uri $partUrls[$i] -Body $chunk `
            -ContentType 'application/octet-stream' -Headers @{ 'Content-Length' = [string]$len }
        $etag = ([string]$putResp.Headers['ETag']).Trim('"')
        if (-not $etag) { throw "No ETag returned for part $partNumber." }
        $parts.Add([pscustomobject]@{ PartNumber = $partNumber; ETag = $etag })
        Write-Host "  part $partNumber/$($partUrls.Count) ($len bytes) etag=$etag"
    }

    # --- Step 3: complete multipart (XML manifest to the presigned complete URL) ---
    Write-Host "[3/7] Completing multipart upload..." -ForegroundColor White
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('<CompleteMultipartUpload>')
    foreach ($p in $parts) {
        [void]$sb.Append("<Part><PartNumber>$($p.PartNumber)</PartNumber><ETag>$($p.ETag)</ETag></Part>")
    }
    [void]$sb.Append('</CompleteMultipartUpload>')
    Invoke-WebRequest -Method Post -Uri $completeUrl -Body $sb.ToString() -ContentType 'application/xml' | Out-Null

    # --- Step 4: finalise ---
    Write-Host "[4/7] Finalising upload..." -ForegroundColor White
    $fin = Invoke-NexusApi -Method Post -Path "/uploads/$uploadId/finalise"
    Write-Host "  state=$($fin.data.state)"

    # --- Step 5: poll until available (2s * 1.5^n, cap 30s, 60 attempts) ---
    Write-Host "[5/7] Waiting for processing..." -ForegroundColor White
    $available = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $state = Invoke-NexusApi -Method Get -Path "/uploads/$uploadId"
        $s = $state.data.state
        Write-Host "  state=$s"
        if ($s -eq 'available') { $available = $true; break }
        $delayMs = [Math]::Min(2000 * [Math]::Pow(1.5, $attempt), 30000)
        Start-Sleep -Milliseconds $delayMs
    }
    if (-not $available) { throw "Upload $uploadId did not reach 'available' within 60 attempts." }

    # --- Step 6: create the new file version ---
    Write-Host "[6/7] Creating mod file version..." -ForegroundColor White
    $body = @{
        upload_id             = $uploadId
        name                  = $name
        version               = $Version
        file_category         = $Category
        archive_existing_file = $ArchiveExisting
        update_mod_version    = $UpdateModVersion
    }
    if ($Description) { $body.description = $Description }
    if ($null -ne $PrimaryModManagerDownload) { $body.primary_mod_manager_download = [bool]$PrimaryModManagerDownload }
    if ($null -ne $AllowModManagerDownload)   { $body.allow_mod_manager_download   = [bool]$AllowModManagerDownload }
    if ($null -ne $ShowRequirementsPopup)     { $body.show_requirements_pop_up      = [bool]$ShowRequirementsPopup }

    $ver = Invoke-NexusApi -Method Post -Path "/mod-files/$FileId/versions" -Body $body
    $versionId = $ver.data.version.id
    Write-Host "  version_id=$versionId" -ForegroundColor Green

    # --- Step 7: changelog (optional) ---
    if ($Changelog -and $ModId) {
        Write-Host "[7/7] Posting changelog..." -ForegroundColor White
        Invoke-NexusApi -Method Post -Path "/mods/$ModId/changelogs" -Body @{
            version   = $Version
            changelog = $Changelog
        } | Out-Null
        Write-Host "  changelog posted for v$Version" -ForegroundColor Green
    } else {
        Write-Host "[7/7] No changelog (skipped)." -ForegroundColor Gray
    }

    Write-Host "`nPublished v$Version to $GameDomain mod $ModId (file slot $FileId, version_id $versionId)." -ForegroundColor Green
    Write-Host "Remember: refresh the mod DESCRIPTION by hand on the website if it changed." -ForegroundColor Yellow
}

switch ($Action) {
    'validate'   { Invoke-Validate }
    'list-files' { Invoke-ListFiles }
    'publish'    { Invoke-Publish }
}
