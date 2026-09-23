param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$setup = Join-Path $PSScriptRoot 'McpSetup'
New-Item -ItemType Directory -Force $setup | Out-Null
$archive = Join-Path $setup 'unity-mcp-v10.0.0.zip'
$source = Join-Path $setup 'unity-mcp-10.0.0'
if (-not (Test-Path -LiteralPath $source)) {
    Invoke-WebRequest -Uri 'https://codeload.github.com/CoplayDev/unity-mcp/zip/refs/tags/v10.0.0' -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $setup -Force
}
$venv = Join-Path $setup 'venv'
& $Python -m venv --without-pip $venv
if ($LASTEXITCODE -ne 0) { throw 'Python 3.10+ venv creation failed.' }
$venvPython = Join-Path $venv 'Scripts/python.exe'
& $Python -m pip --python $venvPython install (Join-Path $source 'Server')
if ($LASTEXITCODE -ne 0) { throw 'Unity MCP dependency installation failed.' }
& (Join-Path $PSScriptRoot 'Start-UnityMcp.ps1')
