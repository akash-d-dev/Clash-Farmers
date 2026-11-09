@echo off
if exist "%~dp0bin\ClashFarmer.exe" (
  start "" "%~dp0bin\ClashFarmer.exe"
) else (
  start "" /b powershell.exe -NoLogo -NoProfile -STA -WindowStyle Hidden -File "%~dp0scripts\Start.ps1"
)
