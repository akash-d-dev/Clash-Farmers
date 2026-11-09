$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$PSScriptRoot\Ocr.ps1"
$readerCallback = [ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
$readLog = [System.Action[string]] { param([string]$message) Write-Host $message }
$reader = [ClashPekkas.ScreenReader]::new($readerCallback,$null,$readLog)
foreach ($case in @(@('home-frame.png','attack'),@('menu-frame.png','find'))) {
    $source = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot $case[0]))
    try {
        $availableW = $source.Width-32; $availableH = $source.Height-32
        $width=$availableW; $height=[int][Math]::Round($width/(16.0/9.0))
        if ($height -gt $availableH) { $height=$availableH; $width=[int][Math]::Round($height*(16.0/9.0)) }
        $cropRect=[Drawing.Rectangle]::new([int](($availableW-$width)/2),[int](32+($availableH-$height)/2),$width,$height)
        $crop=$source.Clone($cropRect,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $game=[ClashPekkas.Vision]::Resize($crop,1600,900) } finally { $crop.Dispose() }
        try {
            $game.Save((Join-Path $PSScriptRoot ($case[1]+'-fixture.png')),[Drawing.Imaging.ImageFormat]::Png)
            $point=$reader.Find($game,$case[1])
            if ($null -eq $point) { throw "Actual $($case[0]) did not match $($case[1])." }
            if ($case[1] -eq 'attack' -and ($point.X -gt 0.15 -or $point.Y -lt 0.85)) { throw 'Home click lies outside the Battle button.' }
            if ($case[1] -eq 'find' -and ($point.X -gt 0.30 -or $point.Y -lt 0.60 -or $point.Y -gt 0.82)) { throw 'Matchmaking click lies outside the left Find a Match button.' }
            "PASS: actual $($case[0]) recognized at ($([Math]::Round($point.X,3)), $([Math]::Round($point.Y,3)))."
        } finally { $game.Dispose() }
    } finally { $source.Dispose() }
}
$armyFixture=Join-Path $PSScriptRoot 'adb-army.png'
if(Test-Path -LiteralPath $armyFixture) {
    $armyImage=[Drawing.Bitmap]::new($armyFixture)
    try {
        $armyPoint=$reader.Find($armyImage,'launch')
        if($null -eq $armyPoint -or $armyPoint.X -lt .80 -or $armyPoint.Y -lt .82) { throw 'The actual army Attack button was not recognized.' }
        'PASS: actual local-ADB army screen recognizes the lower-right Attack confirmation.'
    } finally { $armyImage.Dispose() }
}
$resultFixture=Join-Path $PSScriptRoot 'adb-results.png'
if(Test-Path -LiteralPath $resultFixture) {
    $resultSource=[Drawing.Bitmap]::new($resultFixture)
    try { $resultImage=[ClashPekkas.Vision]::Resize($resultSource,1600,900) } finally { $resultSource.Dispose() }
    try {
        if($null -eq $reader.Find($resultImage,'return')) { throw 'Return Home was not recognized over the dense wall background.' }
        'PASS: actual Return Home button recognized on the dense-wall results screen.'
    } finally { $resultImage.Dispose() }
}
$surrenderFixture=Join-Path $PSScriptRoot 'surrender-dialog.png'
if(Test-Path -LiteralPath $surrenderFixture) {
    $dialog=[Drawing.Bitmap]::new($surrenderFixture)
    try {
        $title=$reader.Find($dialog,'surrenderTitle')
        $okay=$reader.Find($dialog,'confirmSurrender')
        if($null -eq $title -or $null -eq $okay -or $okay.X -lt .50 -or $okay.X -gt .72 -or $okay.Y -lt .55 -or $okay.Y -gt .73) { throw 'Actual surrender dialog title / Okay button not recognized safely.' }
        'PASS: actual Surrender title and right-hand Okay button recognized; Cancel is not selected.'
    } finally { $dialog.Dispose() }
}
$defaults=[ClashPekkas.Settings]::new()
$homeCropPath=Join-Path $PSScriptRoot 'google-home-crop.png'
if(Test-Path -LiteralPath $homeCropPath) {
    $homeCrop=[Drawing.Bitmap]::new($homeCropPath)
    $homeCanvas=[Drawing.Bitmap]::new(1600,900)
    $drawing=[Drawing.Graphics]::FromImage($homeCanvas)
    try {
        $drawing.DrawImageUnscaled($homeCrop,0,590)
        $homePoint=$reader.Find($homeCanvas,'attack')
        if($null -eq $homePoint -or $homePoint.X -gt .12 -or $homePoint.Y -lt .90) { throw 'Actual Baetie home-button crop did not match safely.' }
        'PASS: actual Google home button previously read as Baetie is recognized at its button position.'
    } finally {$drawing.Dispose();$homeCanvas.Dispose();$homeCrop.Dispose()}
}
foreach($fixedCase in @(@('adb-home.png','attack',$defaults.HomeBattleButton),@('adb-menu.png','find',$defaults.FindMatchButton),@('adb-army.png','launch',$defaults.ArmyAttackButton))) {
    $fixture=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot $fixedCase[0]))
    try {
        $detected=$reader.Find($fixture,$fixedCase[1])
        if($null -eq $detected -or [Math]::Abs($detected.X-$fixedCase[2].X) -gt .05 -or [Math]::Abs($detected.Y-$fixedCase[2].Y) -gt .06) { throw "Default button differs from actual screenshot: $($fixedCase[1])" }
        "PASS: fixed $($fixedCase[1]) position agrees with the actual ADB button."
    } finally { $fixture.Dispose() }
}

