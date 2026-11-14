param([switch]$Navigate)
. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
if (-not $Navigate) { throw 'This check navigates Battle and Find a Match, then stops before Attack. Pass -Navigate to run it.' }
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
[ClashPekkas.AppFiles]::Initialize($ProjectRoot)
$config=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText("$DataDirectory\settings.json"))
$reader=[ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
$logger=[System.Action[string]] { param([string]$message) Write-Host $message }
$game=[ClashPekkas.AndroidGame]::new([IntPtr]::Zero,$config,$reader,"$AssetDirectory\pekka-face.png",$logger)
function Find-StableButton([string]$Name) {
    $previous=$null
    for($attempt=0;$attempt -lt 12;$attempt++) {
        $point=$game.Find($Name)
        if($null -ne $point -and $null -ne $previous -and [Math]::Abs($point.X-$previous.X) -lt .015 -and [Math]::Abs($point.Y-$previous.Y) -lt .015) { return $point }
        $previous=$point
        Start-Sleep -Milliseconds 400
    }
    throw "Could not read the $Name button. No further taps sent."
}
function Save-GameFrame([string]$Name) {
    $frame=$game.Capture()
    try { $frame.Save((Join-Path $DataDirectory $Name),[Drawing.Imaging.ImageFormat]::Png) } finally { $frame.Dispose() }
}
try {
    $homeButton=Find-StableButton 'attack'
    Save-GameFrame 'adb-home.png'
    $game.Click($homeButton)
    $find=Find-StableButton 'find'
    Save-GameFrame 'adb-menu.png'
    $game.Click($find)
    $attack=Find-StableButton 'launch'
    Save-GameFrame 'adb-army.png'
    "PASS: live background navigation reached the army screen and recognized Attack at ($([Math]::Round($attack.X,3)), $([Math]::Round($attack.Y,3))). Stopped before pressing Attack; no battle launched and no troops deployed."
} finally { $game.Dispose() }

