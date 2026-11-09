. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
$callback=[ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
$logger=[System.Action[string]] { param([string]$message) Write-Host $message }
$reader=[ClashPekkas.ScreenReader]::new($callback,$TestOutputDirectory,$logger)
$source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'google-scout-window.jpg'))
try {
    # Known game bounds from the live renderer; the saved image is window-relative.
    $crop=$source.Clone([Drawing.Rectangle]::new(65,36,2404,1352),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { $image=[ClashPekkas.Vision]::Resize($crop,1600,900) } finally { $crop.Dispose() }
} finally { $source.Dispose() }
try {
    $image.Save((Join-Path $TestOutputDirectory 'google-scout-fixture.png'),[Drawing.Imaging.ImageFormat]::Png)
    foreach($repeat in 1..3) {
        $loot=$reader.ReadLoot($image,$true)
        if($null -eq $loot) {throw 'Google loot could not be read.'}
        "Read Google fixture: gold=$($loot.Gold), elixir=$($loot.Elixir)"
        if($loot.Gold -ne 1966173 -or $loot.Elixir -ne 2046173) {throw 'Google loot does not match the actual skipped base.'}
    }
    'PASS: the actual qualifying Google base reads exactly 1,966,173 gold and 2,046,173 elixir on three repeated reads. No game input.'
    foreach($faceCase in @(@('P.E.K.K.A',[Drawing.Rectangle]::new(242,803,66,48),'pekka-face.png'),@('Haste',[ClashPekkas.Vision]::SpellFace,'spell-face.png'))) {
        $face=$image.Clone($faceCase[1],[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $reference=[Drawing.Bitmap]::new((Join-Path $AssetDirectory $faceCase[2]))
        try {
            $matches=[ClashPekkas.Vision]::Similar($face,$reference)
            $face.Save((Join-Path $TestOutputDirectory ('google-'+$faceCase[2])),[Drawing.Imaging.ImageFormat]::Png)
            if(-not $matches){throw "Google $($faceCase[0]) card differs from the reference."}
            "PASS: Google $($faceCase[0]) card matches its selection reference."
        } finally {$face.Dispose();$reference.Dispose()}
    }
} finally { $image.Dispose() }
