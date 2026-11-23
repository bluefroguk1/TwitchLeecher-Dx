# Test script for branch "perf-parallel-chat": chat is fetched in parallel segments.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-perf-parallel-chat.ps1 -SkipBuild
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net11.0\TwitchLeecher.exe'
if (-not $SkipBuild) { dotnet build $proj -c Release; if ($LASTEXITCODE -ne 0) { throw 'Build failed' } }
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process $exe -PassThru
$folder = Read-Host 'Your download folder'
$start = Get-Date
Read-Host 'Download a busy VOD cropped to 1 hour with "Save chat" (as text). Note how long "Saving Chat" takes in the log, then press Enter'
$chat = Get-ChildItem $folder -Recurse -Filter *.chat.txt -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$res = [ordered]@{}
if ($chat) {
  $prev = -1; $ok = $true; $n = 0
  foreach ($l in Get-Content $chat.FullName -Encoding UTF8) { $m = [regex]::Match($l, '^\[(\d+):(\d\d):(\d\d)\]'); if ($m.Success) { $n++; $t = [int]$m.Groups[1].Value*3600 + [int]$m.Groups[2].Value*60 + [int]$m.Groups[3].Value; if ($t -lt $prev) { $ok = $false }; $prev = $t } }
  $res['chat in order'] = $(if ($ok -and $n -gt 0) { "PASS ($n messages, last at ${prev}s)" } else { "FAIL (in order: $ok, messages: $n)" })
} else { $res['chat file'] = 'FAIL (no new .chat.txt)' }
foreach ($q in @('Saving Chat was clearly faster than the same range on the previous build', 'Cancel during "Saving Chat" stops within a few seconds')) { do { $a = Read-Host "$q  [y/n/s]" } while ($a -notin 'y','n','s'); $res[$q] = $a }
if (-not $p.HasExited) { $p.Kill() }
$out = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-perf-parallel-chat-$(Get-Date -Format yyyyMMdd-HHmm).txt"
($res.GetEnumerator() | ForEach-Object { "$($_.Key): $($_.Value)" }) | Set-Content $out; Get-Content $out; Write-Host "Saved to $out"
