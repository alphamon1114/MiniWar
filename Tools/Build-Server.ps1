param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'Server/MiniWar.Server.csproj'
$output = Join-Path $projectRoot 'Builds/Server'
if ($FrameworkDependent) {
    & dotnet publish $project -c Release -o $output --no-self-contained
} else {
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $output
}
if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-GameServer.ps1') -Destination (Join-Path $output 'Start-Server.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ServerLauncher.cmd') -Destination (Join-Path $output 'StartServer.cmd') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stop-GameServer.ps1') -Destination (Join-Path $output 'Stop-Server.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ServerStopper.cmd') -Destination (Join-Path $output 'StopServer.cmd') -Force
Write-Output "Server: $output/MiniWar.Server.exe"
Write-Output "Launcher: $output/StartServer.cmd"
