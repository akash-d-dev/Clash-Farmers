$ErrorActionPreference='Stop'
# Separate processes keep Add-Type and WinForms state isolated between checks.
$checks=@('SelfTest.ps1','Test-Recognition.ps1','Test-Navigation.ps1','Test-Loot.ps1','Test-GoogleLoot.ps1','Test-Configurable.ps1','Test-Adb.ps1','Test-Recovery.ps1')
foreach($check in $checks) {
    Write-Host "Running $check"
    & powershell.exe -NoProfile -STA -File (Join-Path $PSScriptRoot $check)
    if($LASTEXITCODE -ne 0){throw "$check failed (exit $LASTEXITCODE)."}
}
Write-Host 'All offline checks passed. No live game input sent.'
