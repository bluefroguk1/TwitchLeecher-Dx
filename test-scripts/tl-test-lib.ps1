# Shared helpers for the automated PC checks (test-*.ps1 that say "uses tl-test-lib.ps1").
# Dot-source it:  . (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
# A script sets $AppDir (optional) before dot-sourcing. Provides: $tl, $ffmpeg, $work, Gql, Find-VodWithChapters,
# Invoke-Tl, Check, Finish.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$results = New-Object System.Collections.Generic.List[object]

function Find-Exe([string]$name) {
    $dirs = @()
    if ($AppDir) { $dirs += $AppDir }
    $dirs += Join-Path $root 'TwitchLeecher\TwitchLeecher\bin\Release'
    $dirs += Join-Path $root 'TwitchLeecher\TwitchLeecher.Cli\bin\Release'
    foreach ($d in $dirs) {
        if (Test-Path $d) {
            $f = Get-ChildItem -Path $d -Recurse -Filter $name -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    Write-Host "CANNOT RUN: $name not found. Build first (dotnet build TwitchLeecher\TwitchLeecher.sln -c Release) or pass -AppDir."
    exit 2
}

# A fresh scratch folder per script, under %TEMP%\tl-test.
function New-Work([string]$name) {
    $dir = Join-Path $env:TEMP "tl-test\$name"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Get-ChildItem $dir -File -ErrorAction SilentlyContinue | Remove-Item -Force
    return $dir
}

# the web client id the app itself uses for public Twitch requests
$constants = Get-Content -Raw (Join-Path $root 'TwitchLeecher\TwitchLeecher.Core\Constants\Constants.cs')
if ($constants -notmatch 'ClientIdWeb\s*=\s*"([^"]+)"') { Write-Host 'CANNOT RUN: ClientIdWeb not found in Constants.cs'; exit 2 }
$clientId = $Matches[1]

function Gql($body) {
    Invoke-RestMethod -Method Post -Uri 'https://gql.twitch.tv/gql' -Headers @{ 'Client-ID' = $clientId } `
        -ContentType 'text/plain' -Body ($body | ConvertTo-Json -Depth 10 -Compress)
}

# A recent public VOD of $channel with at least two game chapters of 2+ minutes, or $null.
# Returns @{ Video = (id, title, lengthSeconds, owner.displayName); Edges = chapter edges }.
function Find-VodWithChapters([string]$channel) {
    $query = 'query{user(login:"' + $channel + '"){videos(first:20,type:ARCHIVE){edges{node{id title lengthSeconds owner{displayName}}}}}}'
    foreach ($v in (Gql @{ query = $query }).data.user.videos.edges.node) {
        $r = Gql @{ operationName = 'VideoPlayer_ChapterSelectButtonVideo'; variables = @{ videoID = $v.id };
                    extensions = @{ persistedQuery = @{ version = 1; sha256Hash = '71835d5ef425e154bf282453a926d99b328cdc5e32f36d3a209d0f4778b41203' } } }
        $edges = @($r.data.video.moments.edges)
        if ($edges.Count -ge 2 -and $edges[0].node.durationMilliseconds -ge 120000 -and $edges[1].node.durationMilliseconds -ge 120000) {
            return @{ Video = $v; Edges = $edges }
        }
    }
    return $null
}

# The newest public VOD of $channel that is at least $minSeconds long, or $null.
function Find-Vod([string]$channel, [int]$minSeconds = 600) {
    $query = 'query{user(login:"' + $channel + '"){videos(first:20,type:ARCHIVE){edges{node{id title lengthSeconds owner{displayName}}}}}}'
    (Gql @{ query = $query }).data.user.videos.edges.node | Where-Object { $_.lengthSeconds -ge $minSeconds } | Select-Object -First 1
}

# Runs tl with the given arguments; returns @{ Code; Out } (stdout as one string). stderr goes to the screen.
function Invoke-Tl([string[]]$arguments) {
    $out = & $script:tl @arguments
    return @{ Code = $LASTEXITCODE; Out = ($out -join "`n") }
}

function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail })
}

# Prints the table and exits: 0 all passed (scratch files deleted), 1 a check failed (files kept).
function Finish([string]$workDir) {
    $results | Format-Table -AutoSize | Out-Host
    $failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
    if ($failed -eq 0) {
        Write-Host "ALL PASSED ($($results.Count) checks). Test files deleted."
        if ($workDir) { Get-ChildItem $workDir -File | Remove-Item -Force }
        exit 0
    }
    Write-Host "$failed of $($results.Count) checks FAILED. Files kept in $workDir for a look."
    exit 1
}

$tl = Find-Exe 'tl.exe'
$ffmpeg = Find-Exe 'ffmpeg.exe'
Write-Host "tl:     $tl"
Write-Host "ffmpeg: $ffmpeg"
