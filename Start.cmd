@echo off
if exist "%~dp0ClashFarmer.exe" (
  start "" "%~dp0ClashFarmer.exe"
) else if exist "%~dp0ClashPekkas.exe" (
  start "" "%~dp0ClashPekkas.exe"
) else (
  start "" /b powershell.exe -NoLogo -NoProfile -STA -WindowStyle Hidden -File "%~dp0Start.ps1"
)
