param([int]$Port = 8765)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$server = Join-Path $PSScriptRoot 'McpSetup/venv/Scripts/mcp-for-unity.exe'
if (-not (Test-Path -LiteralPath $server)) { throw 'Run Tools/Install-UnityMcp.ps1 first.' }
$health = "http://127.0.0.1:$Port/health"
try {
    $reply = Invoke-WebRequest -Uri $health -TimeoutSec 2 -NoProxy
    if ($reply.StatusCode -eq 200) { Write-Output "Unity MCP is already running: http://127.0.0.1:$Port/mcp"; return }
} catch { }
$env:UNITY_MCP_DISABLE_TELEMETRY = '1'
$logDir = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$serverArgs = @('--transport', 'http', '--http-url', "http://127.0.0.1:$Port", '--default-instance', 'MiniWar')
$proc = Start-Process -FilePath $server -ArgumentList $serverArgs -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDir 'unity-mcp.stdout.log') -RedirectStandardError (Join-Path $logDir 'unity-mcp.stderr.log')
Write-Output "Unity MCP PID $($proc.Id): http://127.0.0.1:$Port/mcp"
