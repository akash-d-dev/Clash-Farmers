. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
$callback=[ClashPekkas.OcrReader] {param([byte[]]$png)Read-GameText $png}
$logger=[Action[string]] {param([string]$message)Write-Host $message}
$reader=[ClashPekkas.ScreenReader]::new($callback,$null,$logger)
foreach($fixture in @('google-scout-fixture.png','reference-game.png')) {
    $source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory $fixture))
    try {$image=[ClashPekkas.Vision]::Resize($source,1600,900)}finally{$source.Dispose()}
    try {
        $card=$reader.FirstTroopCard($image)
        if($null -eq $card -or $card.X -lt .1 -or $card.X -gt .23){throw "First troop card not found safely: $fixture"}
        "PASS: generic first card detected in $fixture at x=$($card.X); no P.E.K.K.A identity required."
        if($fixture -eq 'google-scout-fixture.png') {
            $loot=$reader.ReadLoot($image,$true,$true,$true,$true)
            if($null -eq $loot -or $loot.DarkElixir -ne 13163){throw 'Google dark elixir does not match 13,163.'}
            'PASS: gold, elixir and dark elixir read from Google scouting screenshot.'
        }
    }finally{$image.Dispose()}
}
$cases=@(
    @('google-surrender-button.png','retreat'),
    @('google-end-dialog.png','surrenderTitle'))
foreach($case in $cases) {
    $image=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory $case[0]))
    try {
        if($null -eq $reader.Find($image,$case[1])){throw "Provided screenshot not recognized: $($case[0])"}
        if($case[1] -eq 'surrenderTitle' -and $null -eq $reader.Find($image,'confirmSurrender')){throw 'End Battle Okay button not found.'}
        "PASS: supplied $($case[0]) recognized."
    }finally{$image.Dispose()}
}
$source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'edge-filled-base.png'))
$field=[Drawing.Bitmap]::new(2404,1352);$g=[Drawing.Graphics]::FromImage($field)
try{$g.DrawImageUnscaled($source,0,-32);$image=[ClashPekkas.Vision]::Resize($field,1600,900)}finally{$source.Dispose();$g.Dispose();$field.Dispose()}
try{[ClashPekkas.Vision]::DrawPlan($image,[ClashPekkas.Settings]::new());$image.Save((Join-Path $TestOutputDirectory 'outer-strip-preview.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
