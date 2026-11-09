param([switch]$SelfTest,[int]$ResumeBattles=0,[int]$CompletedBattles=0,[switch]$ResumeSearch)
. "$PSScriptRoot\Paths.ps1"
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Web.Extensions
    $sources = @($SourcePath)
    if ($SelfTest) { $sources += "$ProjectRoot\tests\SelfTest.cs" }
    Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
    if ($SelfTest) { [ClashPekkas.Tests]::Run(); exit }
    . "$ProjectRoot\scripts\Ocr.ps1"
    $reader = [ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
    [ClashPekkas.App]::Run($ProjectRoot,$reader,$ResumeBattles,$CompletedBattles,[bool]$ResumeSearch)
} catch {
    $message = $_ | Out-String
    $message | Set-Content -LiteralPath "$DataDirectory\last-error.txt"
    if ($SelfTest) { Write-Error $message; exit 1 }
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show($message, 'Clash Farmer - startup error') | Out-Null
    exit 1
}
