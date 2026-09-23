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
Write-Output "Server: $output/MiniWar.Server.exe"
