@echo off
title MiniWar LAN Server
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Start-GameServer.ps1"
if errorlevel 1 pause
