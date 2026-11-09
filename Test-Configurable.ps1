$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$PSScriptRoot\Ocr.ps1"
$callback=[ClashPekkas.OcrReader] {param([byte[]]$png)Read-GameText $png}
$logger=[Action[string]] {param([string]$message)Write-Host $message}
$reader=[ClashPekkas.ScreenReader]::new($callback,$null,$logger)
foreach($fixture in @('google-scout-fixture.png','reference-game.png')) {
    $source=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot $fixture))
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
    @('C:\Users\Amd\AppData\Local\Temp\codex-clipboard-4c16efeb-008f-46ff-b51b-c6e0ab6a2c42.png',42,32,'retreat','google-surrender-button.png'),
    @('C:\Users\Amd\AppData\Local\Temp\codex-clipboard-43bf0c31-3d11-4699-ac83-ab3710b7a146.png',104,36,'surrenderTitle','google-end-dialog.png'))
foreach($case in $cases) {
    $source=[Drawing.Bitmap]::new($case[0]);$field=[Drawing.Bitmap]::new(2404,1352);$g=[Drawing.Graphics]::FromImage($field)
    try{$g.DrawImageUnscaled($source,-$case[1],-$case[2]);$image=[ClashPekkas.Vision]::Resize($field,1600,900)}finally{$g.Dispose();$source.Dispose();$field.Dispose()}
    try {
        $image.Save((Join-Path $PSScriptRoot $case[4]),[Drawing.Imaging.ImageFormat]::Png)
        if($null -eq $reader.Find($image,$case[3])){throw "Provided screenshot not recognized: $($case[4])"}
        if($case[3] -eq 'surrenderTitle' -and $null -eq $reader.Find($image,'confirmSurrender')){throw 'End Battle Okay button not found.'}
        "PASS: supplied $($case[4]) recognized."
    }finally{$image.Dispose()}
}
$source=[Drawing.Bitmap]::new('C:\Users\Amd\AppData\Local\Temp\codex-clipboard-ec6d3156-1cf2-4f0a-bb68-8deedb4f4051.png')
$field=[Drawing.Bitmap]::new(2404,1352);$g=[Drawing.Graphics]::FromImage($field)
try{$g.DrawImageUnscaled($source,0,-32);$image=[ClashPekkas.Vision]::Resize($field,1600,900)}finally{$source.Dispose();$g.Dispose();$field.Dispose()}
try{[ClashPekkas.Vision]::DrawPlan($image,[ClashPekkas.Settings]::new());$image.Save((Join-Path $PSScriptRoot 'outer-strip-preview.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
