param([int]$Port = 7777, [string]$DataDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$server = Join-Path $projectRoot 'Builds/Server/MiniWar.Server.exe'
if (-not (Test-Path -LiteralPath $server)) { throw 'Build the server with Tools/Build-Server.ps1 first.' }
$serverArgs = @('--port', $Port)
if ($DataDirectory) { $serverArgs += @('--data', $DataDirectory) }
& $server @serverArgs
