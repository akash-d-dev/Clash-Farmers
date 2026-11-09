param([switch]$RunOneBattle)
. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
if (-not $RunOneBattle) { throw 'Pass -RunOneBattle to run exactly one live attack through local ADB.' }
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
[ClashPekkas.AppFiles]::Initialize($ProjectRoot)
$config=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText("$DataDirectory\settings.json"))
$config.Battles=1
$reader=[ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
$logger=[System.Action[string]] {
    param([string]$message)
    $line=(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')+'  '+$message
    Write-Host $line
    Add-Content -LiteralPath "$DataDirectory\live-test.log" -Value $line
}
$game=[ClashPekkas.AndroidGame]::new([IntPtr]::Zero,$config,$reader,"$AssetDirectory\pekka-face.png",$logger)
try {
    if($null -eq $game.Find('attack')) {
        if($null -eq $game.Find('launch')) { throw 'Open the main village or the verified army screen before this check.' }
        # Close the army dialog using its observed upper-right X; no battle is launched here.
        $game.Click([ClashPekkas.Vec]::new(.967,.092))
        Start-Sleep -Milliseconds 800
        if($null -eq $game.Find('attack')) { throw 'Main village was not restored after closing the army screen.' }
    }
    $runner=[ClashPekkas.Runner]::new($game,$config,1,$false,$logger,[Random]::new())
    $runner.Start([DateTime]::Now)
    while($runner.Active) { $runner.Tick([DateTime]::Now); Start-Sleep -Milliseconds 40 }
    $frame=$game.Capture()
    try { $frame.Save("$DataDirectory\last-live-test.png",[Drawing.Imaging.ImageFormat]::Png) } finally { $frame.Dispose() }
    if($runner.Completed -ne 1) { throw ('Live check stopped: '+$runner.Message) }
    'PASS: one complete live battle through local ADB, including army Attack confirmation, troop deployment, the configured spell sequence, results, and return home. No second attack started.'
} finally { $game.Dispose() }

