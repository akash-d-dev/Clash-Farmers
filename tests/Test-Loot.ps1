. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
$callback=[ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
$logger=[System.Action[string]] { param([string]$message) Write-Host $message }
$reader=[ClashPekkas.ScreenReader]::new($callback,$TestOutputDirectory,$logger)
foreach($fixture in @(@('reference-game.png',567460,1437714),@('live-deployed25.png',306913,356556))) {
    $source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory $fixture[0]))
    try { $image=[ClashPekkas.Vision]::Resize($source,1600,900) } finally { $source.Dispose() }
    try {
        $loot=$reader.ReadLoot($image)
        if($null -eq $loot) { throw "Loot could not be read from $($fixture[0])." }
        "Read $($fixture[0]): gold=$($loot.Gold), elixir=$($loot.Elixir)"
        if($loot.Gold -ne $fixture[1] -or $loot.Elixir -ne $fixture[2]) { throw 'Loot OCR does not match the known screenshot values.' }
    } finally { $image.Dispose() }
}
'PASS: exact gold/elixir values match the supplied scouting screenshot and live battle screenshot.'

