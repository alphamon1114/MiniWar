param([ValidateRange(1024,65535)][int]$Port = 7777, [string]$DataDirectory = '', [string]$ServerPath = '', [switch]$PauseWhenRunning)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $ServerPath) { $ServerPath = Join-Path $projectRoot 'Builds/Server/MiniWar.Server.exe' }
try {
    if (-not (Test-Path -LiteralPath $ServerPath -PathType Leaf)) { throw 'Server files are missing. Build with Tools/Build-Server.ps1, or copy the complete Builds/Server folder.' }
    $busy = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
        Where-Object { $_.Port -eq $Port }
    if ($busy) {
        # A second click must not stop/restart a live game or report it as a startup failure.
        $expectedPath = [System.IO.Path]::GetFullPath($ServerPath)
        $owners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        $existingServer = $null
        foreach ($ownerId in $owners) {
            $candidate = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
            if ($candidate -and $candidate.Path -eq $expectedPath) { $existingServer = $candidate; break }
        }
        if ($existingServer) {
            Write-Host ''
            Write-Host '서버가 이미 실행 중입니다.' -ForegroundColor Green
            Write-Host '게임에서 새로고침 후 접속하면 됩니다.'
            Write-Host '이 안내 창을 닫아도 실행 중인 서버는 유지됩니다.'
            Write-Host '서버 종료: StopMiniWarLanServer.cmd 또는 배포 폴더의 StopServer.cmd'
            if ($PauseWhenRunning) { [void](Read-Host 'Enter를 누르면 안내 창을 닫습니다') }
            exit 0
        }
        throw "TCP $Port is in use by another program. MiniWar did not stop or replace it."
    }
    $discoveryBusy = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveUdpListeners() | Where-Object { $_.Port -eq 7778 }
    if ($discoveryBusy) { throw 'UDP 7778 is already in use. Close the other server before starting this one.' }
    if (-not $DataDirectory) { $DataDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MiniWar/Server' }
    $DataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)
    Write-Host ''
    Write-Host '========== MiniWar LAN Server ==========' -ForegroundColor Cyan
    Write-Host "This PC: 127.0.0.1   Port: $Port"
    Write-Host 'Other academy PCs: use one of this PC''s LAN IPv4 addresses:'
    foreach ($adapter in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
        if ($adapter.OperationalStatus -ne 'Up') { continue }
        foreach ($address in $adapter.GetIPProperties().UnicastAddresses) {
            $ip = $address.Address
            if ($ip.AddressFamily -eq 'InterNetwork' -and -not [System.Net.IPAddress]::IsLoopback($ip) -and -not $ip.ToString().StartsWith('169.254.')) {
                Write-Host ('  ' + $ip + '  (' + $adapter.Name + ')') -ForegroundColor Green
            }
        }
    }
    Write-Host "Account saves: $DataDirectory"
    Write-Host 'Automatic server discovery: UDP 7778. No address or authentication code entry needed.'
    Write-Host 'Players open the game, then enter their nickname and password.'
    Write-Host 'Keep this window open while playing. Press Ctrl+C here to stop.'
    Write-Host 'This launcher starts only the server, not the game.'
    Write-Host '======================================='
    Write-Host ''
    & $ServerPath '--port' $Port '--data' $DataDirectory
    if ($LASTEXITCODE -ne 0) { throw "Server exited with code $LASTEXITCODE. Check the message above." }
} catch {
    Write-Host ('Server could not start: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
