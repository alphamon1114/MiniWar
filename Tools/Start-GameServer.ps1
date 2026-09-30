param([ValidateRange(1024,65535)][int]$Port = 7777, [string]$DataDirectory = '', [string]$ServerPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $ServerPath) { $ServerPath = Join-Path $projectRoot 'Builds/Server/MiniWar.Server.exe' }
try {
    if (-not (Test-Path -LiteralPath $ServerPath -PathType Leaf)) { throw 'Server files are missing. Build with Tools/Build-Server.ps1, or copy the complete Builds/Server folder.' }
    $busy = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
        Where-Object { $_.Port -eq $Port }
    if ($busy) { throw "TCP $Port is already in use. Close the existing server or choose another port. No process was stopped." }
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
