@echo off
title MiniWar LAN Server
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-Server.ps1" -ServerPath "%~dp0MiniWar.Server.exe"
pause
