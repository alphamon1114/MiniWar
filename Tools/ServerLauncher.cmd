@echo off
title MiniWar LAN Server
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Server.ps1" -ServerPath "%~dp0MiniWar.Server.exe" -PauseWhenRunning
if errorlevel 1 pause
