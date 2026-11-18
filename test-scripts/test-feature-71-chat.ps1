# Test script for the "feature-71-chat" branch (#71: save VOD chat).
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-71-chat.ps1
# Linux:    pwsh test-scripts/test-feature-71-chat.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

# Checks a .chat.txt: every line is "[HH:MM:SS] name: text", times never go backwards and stay within the length.
function Test-ChatFile($file, [int]$maxSeconds) {
    $lines = @(Get-Content -Path $file -Encoding UTF8)
    if ($lines.Count -eq 0) { return 'FAIL (file is empty)' }
    $prev = -1
    foreach ($line in $lines) {
        $m = [regex]::Match($line, '^\[(\d+):(\d\d):(\d\d)\] [^:]+: ')
        if (-not $m.Success) { return "FAIL (bad line: $line)" }
        $t = [int]$m.Groups[1].Value * 3600 + [int]$m.Groups[2].Value * 60 + [int]$m.Groups[3].Value
        if ($t -lt $prev) { return "FAIL (time goes backwards at: $line)" }
        if ($t -gt $maxSeconds) { return "FAIL (time beyond ${maxSeconds}s: $line)" }
        $prev = $t
    }
    return "PASS ($($lines.Count) messages, last at ${prev}s)"
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$folder = Read-Host 'Your Twitch Leecher download folder (Settings > Download folder)'
if (-not (Test-Path $folder)) { throw "Folder not found: $folder" }

$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru

# --- 1. Cropped download with chat (automatic file check) ------------------
Write-Host ''
Write-Host '[#71 cropped] Pick a VOD from a channel with an active chat. In the download dialog:' -ForegroundColor Cyan
Write-Host '  tick "Save chat (.chat.txt)", Crop Start 01:00:00, End 01:05:00 (5 minutes), then Download.'
Write-Host '  Wait until it shows Done.'
$start = Get-Date
Read-Host 'Press Enter when the download is Done'
$chat = @(Get-ChildItem $folder -Recurse -Filter *.chat.txt -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start })
if ($chat.Count -eq 0) {
    $results['#71 chat file created'] = 'FAIL (no new .chat.txt)'
} else {
    $results['#71 chat file created'] = "PASS ($($chat[0].Name))"
    # 5 minute crop -> timestamps relative to the cropped file, so all within 300s
    $results['#71 chat lines valid + within crop'] = Test-ChatFile $chat[0].FullName 300
    Write-Host 'First lines:'; Get-Content $chat[0].FullName -TotalCount 5 -Encoding UTF8 | ForEach-Object { "  $_" }
}
Ask '#71 chat matches video' 'Open the video and the .chat.txt. Pick a few messages: does the chat match what was happening at that time in the video (within a few seconds)?'
Ask '#71 log' 'Open the download log. Does it say "Downloading chat to ... done! (N messages)"?'

# --- 2. Without the checkbox --------------------------------------------------
$start = Get-Date
Read-Host 'Download another short cropped VOD WITHOUT ticking "Save chat". Press Enter when Done'
$extra = @(Get-ChildItem $folder -Recurse -Filter *.chat.txt -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start })
$results['No chat file when unticked'] = $(if ($extra.Count -eq 0) { 'PASS' } else { "FAIL ($($extra.Name -join ', '))" })

# --- 3. Cancel while saving chat ------------------------------------------------
Ask '#71 cancel during chat (optional)' @'
Download a long VOD uncropped with "Save chat" ticked. When the status shows "Saving Chat", click Cancel.
Expected: it stops within a few seconds and shows Canceled. Answer s to skip.
'@

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-chat-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
