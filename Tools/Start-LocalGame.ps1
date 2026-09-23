param([ValidateRange(1024,65535)][int]$Port = 17778, [ValidateSet(0,1)][int]$Body = 0, [switch]$NoClient)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$serverExe = Join-Path $projectRoot 'Builds/Server/MiniWar.Server.exe'
$clientExe = Join-Path $projectRoot 'Builds/OnlineClient/MiniWar.exe'
if (-not (Test-Path -LiteralPath $serverExe) -or -not (Test-Path -LiteralPath $clientExe)) {
    throw 'Builds/Server and Builds/OnlineClient are required. Keep their complete folders.'
}
$previewData = Join-Path $projectRoot 'Builds/LocalPreviewData'
New-Item -ItemType Directory -Path $previewData -Force | Out-Null
$pidFile = Join-Path $previewData 'server-process.json'

function Test-PreviewPort {
    $socket = [System.Net.Sockets.TcpClient]::new()
    try {
        $connection = $socket.ConnectAsync('127.0.0.1', $Port)
        return ($connection.Wait(300) -and $socket.Connected)
    } catch { return $false } finally { $socket.Dispose() }
}

$serverProcess = $null
if (Test-Path -LiteralPath $pidFile) {
    $saved = Get-Content -LiteralPath $pidFile -Raw | ConvertFrom-Json
    $candidate = Get-Process -Id $saved.pid -ErrorAction SilentlyContinue
    if ($candidate -and $candidate.Path -eq $serverExe -and $candidate.StartTime.ToUniversalTime().Ticks.ToString() -eq $saved.startTicks) {
        if ($saved.port -ne $Port) { throw "Local preview already uses port $($saved.port). StopMiniWarServer.cmd can stop it before changing ports." }
        $serverProcess = $candidate
    }
}
if (-not $serverProcess) {
    if (Test-PreviewPort) { throw "Port $Port is already used by another process. No existing process was stopped." }
    $serverArgs = @('--local', '--port', $Port, '--data', ('"' + $previewData + '"'))
    $serverProcess = Start-Process -FilePath $serverExe -ArgumentList $serverArgs -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $previewData 'server.log') -RedirectStandardError (Join-Path $previewData 'server-errors.log')
    @{pid=$serverProcess.Id;startTicks=$serverProcess.StartTime.ToUniversalTime().Ticks.ToString();port=$Port} |
        ConvertTo-Json | Set-Content -LiteralPath $pidFile -Encoding UTF8
}
$deadline = [DateTime]::UtcNow.AddSeconds(12)
while (-not (Test-PreviewPort)) {
    if ($serverProcess.HasExited) { throw ('Local server failed. ' + (Get-Content (Join-Path $previewData 'server-errors.log') -Raw)) }
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Local server startup timed out. See Builds/LocalPreviewData/server-errors.log.' }
    Start-Sleep -Milliseconds 200
}
$fingerprint = (Get-Content -LiteralPath (Join-Path $previewData 'server-fingerprint.txt') -Raw).Trim()
if ($fingerprint -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid local server fingerprint.' }
$configPath = Join-Path $previewData 'connection.json'
@{port=$Port;fingerprint=$fingerprint;body=$Body} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
if (-not $NoClient) {
    $logPath = Join-Path $previewData ('client-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.log')
    $clientArgs = @('--miniwar-local-preview', ('"' + $configPath + '"'), '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '-logFile', ('"' + $logPath + '"'))
    # This is the interactive game window explicitly requested by the user.
    $clientProcess = Start-Process -FilePath $clientExe -ArgumentList $clientArgs -WorkingDirectory $projectRoot -WindowStyle Normal -PassThru
    @{pid=$clientProcess.Id;log=$logPath} | ConvertTo-Json | Set-Content (Join-Path $previewData 'last-client.json') -Encoding UTF8
    Write-Output "MiniWar opened. Client PID $($clientProcess.Id)."
}
Write-Output "Local preview server ready on 127.0.0.1:$Port. Stop it with StopMiniWarServer.cmd."
