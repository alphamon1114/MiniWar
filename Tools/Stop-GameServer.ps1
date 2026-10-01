param([ValidateRange(1024,65535)][int]$Port = 7777, [string]$ServerPath = '')
$ErrorActionPreference = 'Stop'
if (-not $ServerPath) { $ServerPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Builds/Server/MiniWar.Server.exe' }
$expectedPath = [System.IO.Path]::GetFullPath($ServerPath)
try {
    $owners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
    if ($owners.Count -eq 0) { Write-Host '서버가 이미 꺼져 있습니다.'; exit 0 }
    $stopped = $false
    foreach ($ownerId in $owners) {
        $candidate = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        # Never terminate an unrelated port owner or a different copy of the server.
        if ($candidate -and $candidate.Path -eq $expectedPath) {
            $candidate.Kill()
            if (-not $candidate.WaitForExit(5000)) { throw 'Server did not stop in time.' }
            $stopped = $true
        }
    }
    if (-not $stopped) { throw "TCP $Port belongs to another program. No process was stopped." }
    Write-Host '서버를 종료했습니다. 저장된 계정과 장비는 유지됩니다.' -ForegroundColor Green
} catch {
    Write-Host ('서버 종료 실패: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
