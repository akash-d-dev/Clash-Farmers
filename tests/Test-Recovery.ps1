. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path (@($SourcePaths)+@("$PSScriptRoot\SelfTest.cs","$PSScriptRoot\RecoveryTests.cs")) -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
[ClashPekkas.RecoveryTests]::Run()
. "$ProjectRoot\scripts\Ocr.ps1"
$reader=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png) Read-GameText $png},$null,$null,$AssetDirectory)
$emptyReader=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png) '[]'},$null,$null,$AssetDirectory)
foreach($name in @('adb-home.png','developer-home.png','developer-home-no-stars.png','adb-menu.png','adb-army.png','adb-results.png','developer-battle.png','surrender-dialog.png','google-scout-fixture.png','developer-confirm-exit.png','developer-inactivity.png')) {
    $source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory $name))
    try {$frame=[ClashPekkas.Vision]::Resize($source,1600,900)}finally{$source.Dispose()}
    try {
        $observation=$reader.ObserveNavigation($frame)
        try {
            $expected=switch($name){'adb-home.png'{'Home'} 'developer-home.png'{'Home'} 'developer-home-no-stars.png'{'Home'} 'adb-menu.png'{'Menu'} 'adb-army.png'{'Menu'} 'adb-results.png'{'Results'} 'developer-battle.png'{'Battle'} 'surrender-dialog.png'{'Battle exit'} 'google-scout-fixture.png'{'Scouting'} 'developer-confirm-exit.png'{'Exit dialog'} 'developer-inactivity.png'{'Inactivity dialog'}}
            if($observation.Kind -ne $expected){throw "$name expected $expected, observed $($observation.Kind)"}
            if($expected -eq 'Exit dialog' -and ($null -eq $observation.Cancel -or $observation.Cancel.X -ge .5)){throw 'Exit dialog must select the left Cancel button.'}
            if($expected -eq 'Inactivity dialog' -and ($null -eq $observation.Reload -or $observation.Reload.X -ge .5)){throw 'Inactivity dialog must select its Reload Game button.'}
        }finally{$observation.Dispose()}
        $silent=$emptyReader.ObserveNavigation($frame)
        try {
            if(($expected -eq 'Home') -ne ($silent.Kind -eq 'Home')){throw "Visual-only home fallback misclassified $name as $($silent.Kind)"}
        }finally{$silent.Dispose()}
    }finally{$frame.Dispose()}
    "PASS: one-frame navigation and visual fallback: $name"
}
# Reproduce the reported startup through the real reader, not a fake "Home" response.
# Remove Battle templates entirely to prove that OCR is an independent route.
$anchorDirectory=Join-Path $TestOutputDirectory ('home-anchors-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $anchorDirectory
foreach($asset in @('home-build.png','home-shop.png','developer-home-build.png','developer-home-shop.png')){Copy-Item -LiteralPath (Join-Path $AssetDirectory $asset) -Destination (Join-Path $anchorDirectory $asset)}
$ocrOnlyBattle=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png) Read-GameText $png},$null,$null,$anchorDirectory)
$reported=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'developer-home-no-stars.png'))
try {
    foreach($recognizer in @($reader,$ocrOnlyBattle,$emptyReader)) {
        $game=[ClashPekkas.ScreenshotHomeGame]::new($recognizer,$reported)
        $settings=[ClashPekkas.Settings]::new();$settings.Platform='GooglePlayGames';$settings.RandomSwipes=$false
        $runner=[ClashPekkas.Runner]::new($game,$settings,1,$false,[Action[string]]{param($line)},[Random]::new(1))
        $began=[DateTime]::new(2026,1,1);$runner.Start($began)
        for($tick=0;$tick -lt 300 -and $runner.Active -and $runner.State -ne 'Scouting';$tick++){
            $game.Now=$began.AddMilliseconds($tick*50);$runner.Tick($game.Now)
        }
        if($runner.State -ne 'Scouting' -or $game.HomeClicks -ne 1 -or $game.FindClicks -ne 1 -or $game.ArmyClicks -ne 1 -or $game.Backs -ne 0 -or $game.Cancels -ne 0){throw 'Reported home screenshot must advance startup without a recovery Back/Cancel loop.'}
        $runner.Stop('Offline navigation complete')
    }
}finally{$reported.Dispose()}
'PASS: reported startup reaches scouting with OCR, missing Battle templates, and empty OCR; no recovery inputs.'
$loadingCanvas=[Drawing.Bitmap]::new(1600,900)
try {
    $loadingReader=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png)'[{"Text":"Loadiug","X":700,"Y":800,"Width":100,"Height":30}]'},$null,$null,$AssetDirectory)
    $loadingObservation=$loadingReader.ObserveNavigation($loadingCanvas)
    try{if($loadingObservation.Kind -ne 'Loading'){throw 'Observed stylized Loading text must suppress Back during reload.'}}finally{$loadingObservation.Dispose()}
}finally{$loadingCanvas.Dispose()}
$village=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'developer-home.png'))
try {
    $script:homeReads=0
    $alternating=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png)$script:homeReads++;if($script:homeReads%2){'[{"Text":"Battle","X":40,"Y":845,"Width":90,"Height":30}]'}else{'[]'}},$null,$null,$AssetDirectory)
    $lastPoint=$null
    for($i=0;$i -lt 4;$i++){
        $homeObservation=$alternating.ObserveNavigation($village)
        try {
            if($homeObservation.Kind -ne 'Home' -or ($null -ne $lastPoint -and [ClashPekkas.Plan]::PixelDistance($homeObservation.Home,$lastPoint) -gt .01)){throw 'Alternating OCR success/failure must preserve the same stable Battle target.'}
            $lastPoint=$homeObservation.Home
        }finally{$homeObservation.Dispose()}
    }
    $selected=[ClashPekkas.ScreenReader]::new([ClashPekkas.OcrReader]{param([byte[]]$png)'[{"Text":"Upgrade","X":600,"Y":790,"Width":100,"Height":30}]'},$null,$null,$AssetDirectory)
    $selection=$selected.ObserveNavigation($village)
    try{if($selection.Kind -ne 'Menu'){throw 'Selected-building controls must block home clicks.'}}finally{$selection.Dispose()}
    $graphics=[Drawing.Graphics]::FromImage($village)
    try{$graphics.FillRectangle([Drawing.Brushes]::Black,1350,300,250,600)}finally{$graphics.Dispose()}
    $noAnchor=$emptyReader.ObserveNavigation($village)
    try{if($noAnchor.Kind -eq 'Home'){throw 'Battle tile alone must not prove home without a second HUD indicator.'}}finally{$noAnchor.Dispose()}
}finally{$village.Dispose()}
$directory=Join-Path $TestOutputDirectory ('recovery-retention-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $directory
Set-Content -LiteralPath (Join-Path $directory 'unrelated.json') -Value '{}'
$sample=[ClashPekkas.NavigationObservation]::new();$sample.Kind='Unknown';$sample.Frame=[Drawing.Bitmap]::new(16,16)
try {
    for($i=0;$i -lt 23;$i++) {
        $record=[ClashPekkas.RecoveryRecord]::new();$record.Completed=$i;$record.Total=30;$record.Reason='offline test'
        [ClashPekkas.RecoveryDiagnostics]::Save($directory,$sample,$record)
        Start-Sleep -Milliseconds 2
    }
    $records=@(Get-ChildItem -LiteralPath $directory -Filter '*.json' | Where-Object {$_.Name -ne 'unrelated.json'})
    if($records.Count -ne 20 -or @(Get-ChildItem -LiteralPath $directory -Filter '*.png').Count -ne 20){throw 'Recovery must retain exactly the latest 20 screenshot/JSON pairs.'}
    $latest=Get-Content -LiteralPath (($records | Sort-Object Name -Descending | Select-Object -First 1).FullName) -Raw | ConvertFrom-Json
    if($latest.Context.Completed -ne 22 -or $latest.Observation -ne 'Unknown' -or -not (Test-Path -LiteralPath (Join-Path $directory 'unrelated.json'))){throw 'Recovery diagnostics lost context or deleted an unrelated file.'}
}finally{$sample.Dispose()}
'PASS: selected buildings, independent home anchors, and timestamped diagnostic retention.'
