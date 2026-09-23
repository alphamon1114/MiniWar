$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$serverExe = Join-Path $projectRoot 'Builds/Server/MiniWar.Server.exe'
$pidFile = Join-Path $projectRoot 'Builds/LocalPreviewData/server-process.json'
if (-not (Test-Path -LiteralPath $pidFile)) { Write-Output 'Local preview server is not running.'; return }
$saved = Get-Content -LiteralPath $pidFile -Raw | ConvertFrom-Json
$candidate = Get-Process -Id $saved.pid -ErrorAction SilentlyContinue
if ($candidate -and $candidate.Path -eq $serverExe -and $candidate.StartTime.ToUniversalTime().Ticks.ToString() -eq $saved.startTicks) {
    Stop-Process -Id $candidate.Id
    Write-Output 'Local preview server stopped. Saved files were retained.'
} else { Write-Output 'Local preview server is already stopped.' }
