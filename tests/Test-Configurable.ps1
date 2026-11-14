. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
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
$developer=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'developer-battle.png'))
try {
    if($null -eq $reader.Find($developer,'retreat')){throw 'Developer Edition End Battle button must be found in its lower position.'}
    $loot=$reader.ReadLoot($developer,$true,$true,$true,$true,$true)
    if($null -eq $loot -or $loot.Gold -ne 701370 -or $loot.Elixir -ne 695900 -or $loot.DarkElixir -ne 6758){throw 'Developer Edition loot must use its Android screenshot coordinates.'}
    'PASS: Developer Android End Battle and all three loot values recognized.'
}finally{$developer.Dispose()}
$sevenPanel=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'developer-seven-digit-panel.png'))
$sevenFrame=[Drawing.Bitmap]::new(1600,900);$sevenGraphics=[Drawing.Graphics]::FromImage($sevenFrame)
try {
    foreach($offset in @(-1,0,1)) {
        $sevenGraphics.Clear([Drawing.Color]::Black);$sevenGraphics.DrawImageUnscaled($sevenPanel,$offset,0)
        $loot=$reader.ReadLoot($sevenFrame,$true,$true,$true,$true,$true)
        if($null -eq $loot -or $loot.Gold -ne 2015434 -or $loot.Elixir -ne 1956218 -or $loot.DarkElixir -ne 23775){throw "Seven-digit loot lost a digit with offset $offset."}
    }
    $sevenGraphics.Clear([Drawing.Color]::Black);$sevenGraphics.DrawImageUnscaled($sevenPanel,0,0)
    $missingOne=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png)'[{"Text":"956 218","X":50,"Y":10,"Width":150,"Height":35}]'},$null,$null)
    if($null -ne $missingOne.ReadLoot($sevenFrame,$true,$false,$true,$false,$true)){throw 'Repeated truncated OCR must not pass when seven digit shapes are visible.'}
    $script:elixirReadCalls=0
    $recovering=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png)$script:elixirReadCalls++;if($script:elixirReadCalls -le 2){'[{"Text":"956 218"}]'}else{'[{"Text":"1 956 218"}]'}},$null,$null)
    $loot=$recovering.ReadLoot($sevenFrame,$true,$false,$true,$false,$true)
    if($null -eq $loot -or $loot.Elixir -ne 1956218 -or $script:elixirReadCalls -lt 4){throw 'Reader must discard repeated missing-leading-digit results before accepting full amount.'}
    'PASS: seven-digit screenshot reads 2,015,434 / 1,956,218; repeated missing-leading-1 OCR is rejected and recoverable.'
}finally{$sevenGraphics.Dispose();$sevenFrame.Dispose();$sevenPanel.Dispose()}
$field=[Drawing.Bitmap]::new(2404,1352);$g=[Drawing.Graphics]::FromImage($field)
try{$g.DrawImageUnscaled($source,0,-32);$image=[ClashPekkas.Vision]::Resize($field,1600,900)}finally{$source.Dispose();$g.Dispose();$field.Dispose()}
try{
    $fitDefaults=[ClashPekkas.Settings]::new()
    $fit=[ClashPekkas.BattlefieldRim]::Adapt($fitDefaults,$image)
    if([object]::ReferenceEquals($fitDefaults,$fit)){throw 'The clear full-edge reference must fit its shifted battlefield boundary.'}
    foreach($mode in @('LShape','FourSides')) {
        $fit.DeploymentMode=$mode
        foreach($reverse in @($false,$true)) {
            foreach($total in @(1,9,25,500)) {
                $fit.Troops=$total;$layout=[ClashPekkas.Plan]::Troops($fit,$reverse,[Random]::new(4))
                for($i=0;$i -lt $layout.Points.Count;$i++) {
                    $point=$layout.Points[$i];$side=$layout.Sides[$layout.PointSides[$i]].Name
                    $outside=switch($side){'upper-left'{$point.X+.78*$point.Y -lt .54} 'upper-right'{$point.X-.78*$point.Y -gt .45} 'lower-left'{$point.X-.78*$point.Y -lt -.32} 'lower-right'{$point.X+.78*$point.Y -gt 1.31}}
                    if(-not $outside){throw "Adaptive $side deployment crossed the full-edge reference's restricted boundary."}
                }
            }
            foreach($depth in @(0,10,70,100)) {
                $fit.LShapeOpeningDepthPercent=$depth;$fit.LShapeLaterDepthPercent=$depth;$fit.FourSidesSpellDepthPercent=$depth
                $null=[ClashPekkas.Plan]::Opening($fit,$reverse,[Random]::new(4))
            }
        }
    }
    if($fitDefaults.Normal[0].X -ne .0946){throw 'Per-base fitting must not modify saved preset settings.'}
    $custom=[ClashPekkas.Settings]::new();$custom.Normal[0].X-=.01;$customFit=[ClashPekkas.BattlefieldRim]::Adapt($custom,$image)
    if([ClashPekkas.Plan]::PixelDistance($custom.Normal[0],$customFit.Normal[0]) -gt .001 -or [ClashPekkas.Plan]::PixelDistance($custom.Normal[1],$customFit.Normal[1]) -gt .001){throw 'Automatic boundary fitting must preserve both custom endpoints.'}
    foreach($mode in @('LShape','FourSides')) {
        $preview=$image.Clone()
        try {
            $settings=[ClashPekkas.Settings]::new();$settings.DeploymentMode=$mode
            [ClashPekkas.PlacementPreview]::Draw($preview,$settings)
            $name=if($mode -eq 'LShape'){'outer-strip-preview.png'}else{'four-sides-preview.png'}
            $preview.Save((Join-Path $TestOutputDirectory $name),[Drawing.Imaging.ImageFormat]::Png)
        }finally{$preview.Dispose()}
    }
}finally{$image.Dispose()}
$rimSource=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'outer-boundary-base.png'))
try{$rimFrame=[ClashPekkas.Vision]::Resize($rimSource,1600,900)}finally{$rimSource.Dispose()}
try {
    $ambiguous=[Drawing.Bitmap]::new(1600,900)
    try {
        $defaults=[ClashPekkas.Settings]::new()
        if(-not [object]::ReferenceEquals($defaults,[ClashPekkas.BattlefieldRim]::Adapt($defaults,$ambiguous))){throw 'An unreadable boundary must retain the calibrated preset.'}
    }finally{$ambiguous.Dispose()}
    foreach($mode in @('LShape','FourSides')) {
        $preview=$rimFrame.Clone()
        try {
            $settings=[ClashPekkas.Settings]::new();$settings.DeploymentMode=$mode;$settings.Troops=25;$settings.UseSpells=$false
            [ClashPekkas.PlacementPreview]::Draw($preview,$settings)
            $name=if($mode -eq 'LShape'){'outer-grass-lshape.png'}else{'outer-grass-four-sides.png'}
            $preview.Save((Join-Path $TestOutputDirectory $name),[Drawing.Imaging.ImageFormat]::Png)
        }finally{$preview.Dispose()}
    }
    $example=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText((Join-Path $ProjectRoot 'config/settings.example.json')))
    foreach($reverse in @($false,$true)) {
        $actual=if($reverse){$example.Reverse}else{$example.Normal}
        $expected=[ClashPekkas.Settings]::OuterLines($reverse)
        for($i=0;$i -lt 4;$i++){if([ClashPekkas.Plan]::PixelDistance($actual[$i],$expected[$i]) -gt .001){throw 'Example config must use the same corrected rim as code defaults.'}}
    }
    'PASS: corrected outer-grass previews on the supplied full-edge base and matching example defaults.'
}finally{$rimFrame.Dispose()}
