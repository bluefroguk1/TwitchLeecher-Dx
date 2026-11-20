# Test script for the "feature-chat-html" branch.
# Builds the app, then walks you through a short cropped download with chat saved as HTML.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-chat-html.ps1
# Linux:    pwsh test-scripts/test-feature-chat-html.ps1
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

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru

Ask 'dialog' @'
Search a channel with active chat (for example a big streamer's past broadcast) and open Download on one VOD.
Is there a format box next to "Save chat", greyed out until "Save chat" is ticked, offering Text / HTML / HTML (offline)?
'@

Ask 'text unchanged' @'
Tick "Save chat", keep "Text", crop Start 01:00:00 to End 01:02:00 and download.
Is <video>.chat.txt next to the video, with [00:00:xx] name: text lines starting near 00:00:00?
'@

Ask 'html download' @'
Download the same VOD again under another filename with "HTML" and the same crop.
Did it finish, with <video>.chat.html next to the video and no warning?
'@

Ask 'html look' @'
Open the .chat.html in a browser. Dark background, readable font; each line is [HH:MM:SS], a coloured bold name, then the message.
Do timestamps start near [00:00:00], and do names show in their chat colours (dark colours lightened so they are readable)?
'@

Ask 'html emotes' @'
Find a message with a Twitch emote (Kappa, LUL, ...). Is it shown as an image about 28px high, with its code on hover?
'@

Ask 'html encoding' @'
Search the page for a message containing < or & (or view source). Is it shown as text, never as markup?
'@

Ask 'offline download' @'
Download once more with "HTML (offline)" and the same crop. Did it finish with a .chat.html and no warning?
The log (Downloads > Show log) should mention "Embedding N emotes".
'@

Ask 'offline view' @'
Turn the network off (Wi-Fi off or unplug), then open the offline .chat.html in a browser.
Do emotes still show as images? Then open the plain "HTML" file: its emotes should be broken/missing offline (expected).
'@

Ask 'chat failure keeps video' @'
Network back on. Start an HTML chat download, and while it says "Saving Chat" turn the network off.
Does the video stay, with a "Chat could not be saved" warning instead of a failed download?
'@

if (-not $p.HasExited) { $p.Kill() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-chat-html-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
