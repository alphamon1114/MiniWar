@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Start-LocalGame.ps1"
if errorlevel 1 pause
