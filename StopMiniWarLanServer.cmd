@echo off
title MiniWar LAN Server
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Stop-GameServer.ps1"
pause
