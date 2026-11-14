. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
$image = [Drawing.Bitmap]::new("$FixtureDirectory\reference-game.png")
try {
    $region = [ClashPekkas.Vision]::Region('next')
    $crop = $image.Clone($region,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stream = [IO.MemoryStream]::new()
    $enlarged = [ClashPekkas.Vision]::Resize($crop,$crop.Width*2,$crop.Height*2)
    try {
        $enlarged.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        $recognized = Read-GameText $stream.ToArray()
        $serializer = [Web.Script.Serialization.JavaScriptSerializer]::new()
        $lines = $serializer.Deserialize($recognized,[ClashPekkas.OcrLine[]])
        foreach ($line in $lines) { $line.X/=2; $line.Y/=2; $line.Width/=2; $line.Height/=2 }
        $point = [ClashPekkas.Vision]::Locate('next',$lines,$region)
        if ($null -eq $point -or $point.X -lt 0.8) { throw "Scouting button not recognized: $recognized" }
        "PASS: Windows OCR detects the actual scouting button at ($([math]::Round($point.X,3)), $([math]::Round($point.Y,3)))."
    } finally { $crop.Dispose(); $stream.Dispose(); $enlarged.Dispose() }
    $face = $image.Clone([ClashPekkas.Vision]::TroopFace,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $reference = [Drawing.Bitmap]::new("$AssetDirectory\pekka-face.png")
    try {
        if (-not [ClashPekkas.Vision]::Similar($face,$reference)) { throw 'Troop reference mismatch.' }
        'PASS: P.E.K.K.A reference matches the actual first troop card.'
    } finally { $face.Dispose(); $reference.Dispose() }
} finally { $image.Dispose() }
$spellSource=[Drawing.Bitmap]::new("$FixtureDirectory\live-deployed25.png")
try { $spellFrame=[ClashPekkas.Vision]::Resize($spellSource,1600,900) } finally { $spellSource.Dispose() }
try {
    $spellFace=$spellFrame.Clone([ClashPekkas.Vision]::SpellFace,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $spellReference=[Drawing.Bitmap]::new("$AssetDirectory\spell-face.png")
    try {
        if(-not [ClashPekkas.Vision]::Similar($spellFace,$spellReference)) { throw 'Haste spell reference does not match the live spell card.' }
        'PASS: spell-card position and appearance match the live battle screenshot.'
    } finally { $spellFace.Dispose();$spellReference.Dispose() }
} finally { $spellFrame.Dispose() }
$reader = [ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
if ([string]::IsNullOrWhiteSpace($reader.Invoke([IO.File]::ReadAllBytes("$FixtureDirectory\reference-game.png")))) { throw 'OCR callback returned no result.' }
[Windows.Forms.Application]::EnableVisualStyles()
$testFolder = Join-Path ([IO.Path]::GetTempPath()) ('clash-pekkas-ui-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testFolder
$null = New-Item -ItemType Directory -Path (Join-Path $testFolder 'data')
Copy-Item -LiteralPath "$ProjectRoot\config\settings.example.json" -Destination (Join-Path $testFolder 'data\settings.json')
$testConfig=Get-Content -LiteralPath (Join-Path $testFolder 'data\settings.json') -Raw | ConvertFrom-Json
$testConfig | Add-Member -NotePropertyName UseAdbWhenAvailable -NotePropertyValue $false -Force
$testConfig | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testFolder 'data\settings.json')
$script:activations = 0
$picker = [ClashPekkas.PlayerPicker] { return [IntPtr]::new(1) }
$activator = [ClashPekkas.PlayerActivator] { param([IntPtr]$window) $script:activations++ }
$form = [ClashPekkas.MainForm]::new($testFolder,$reader,$picker,$activator)
$render = [Drawing.Bitmap]::new($form.Width,$form.Height)
try {
    if ($form.Controls.Count -lt 10) { throw 'App controls are missing.' }
    if ($form.Text -ne 'Clash Farmer - Ready') { throw 'Idle app title is incorrect.' }
    $orderButton=$form.Controls.Find('actionOrderButton',$true)[0]
    if($null -eq $orderButton -or -not $orderButton.Enabled){throw 'Action order button must be enabled while idle.'}
    $orderDialog=[ClashPekkas.ActionOrderForm]::new([ClashPekkas.Settings]::new().ActionOrder)
    try {
        $orderDialog.Show();[Windows.Forms.Application]::DoEvents()
        $orderList=$orderDialog.Controls.Find('actionOrderList',$true)[0]
        $moveUp=$orderDialog.Controls.Find('moveActionUp',$true)[0];$moveDown=$orderDialog.Controls.Find('moveActionDown',$true)[0];$saveOrder=$orderDialog.Controls.Find('saveActionOrder',$true)[0]
        $orderList.SelectedIndex=3;$moveUp.PerformClick();$moveUp.PerformClick();$moveUp.PerformClick()
        if($saveOrder.Enabled){throw 'Abilities before heroes must disable Save.'}
        $moveDown.PerformClick()
        if(-not $saveOrder.Enabled){throw 'Moving abilities after heroes must enable Save.'}
        $chosenOrder=$orderDialog.SelectedOrder
        $dialogImage=[Drawing.Bitmap]::new($orderDialog.Width,$orderDialog.Height)
        try{$orderDialog.DrawToBitmap($dialogImage,[Drawing.Rectangle]::new(0,0,$orderDialog.Width,$orderDialog.Height));$dialogImage.Save("$TestOutputDirectory\action-order-preview.png",[Drawing.Imaging.ImageFormat]::Png)}finally{$dialogImage.Dispose()}
        $saveOrder.PerformClick()
        if($orderDialog.DialogResult -ne [Windows.Forms.DialogResult]::OK){throw 'Action order Save failed.'}
    }finally{$orderDialog.Dispose()}
    $privateOrder=[Reflection.BindingFlags]'Instance,NonPublic'
    $form.GetType().GetMethod('ApplyActionOrder',$privateOrder).Invoke($form,(,[object]$chosenOrder))
    $stop = $form.Controls.Find('stopButton',$true)[0]
    if($stop.Text -ne 'Stop (F9)'){throw 'Stop button must advertise only the F9 keyboard shortcut.'}
    if ($null -eq $stop -or $stop.Enabled -or $form.IsBusy) { throw 'Stop must be disabled while idle.' }
    $start = $form.Controls.Find('startButton',$true)[0]
    if ($null -eq $start -or -not $start.Enabled) { throw 'Start must be enabled while idle.' }
    $count = $form.Controls.Find('battleCount',$true)[0]
    $count.Value=1
    if ($start.Text -ne 'Start 1 battle') { throw 'Single-battle Start label is incorrect.' }
    $count.Value=10
    if ($start.Text -ne 'Start 10 battles') { throw 'Ten-battle Start label is incorrect.' }
    $count.Value=30
    if ($start.Text -ne 'Start 30 battles') { throw 'Thirty-battle Start label is incorrect.' }
    $troops=$form.Controls.Find('troopCount',$true)[0]
    $spells=$form.Controls.Find('spellCount',$true)[0]
    $deployment=$form.Controls.Find('deploymentSelector',$true)[0]
    if($spells.Minimum -ne 0 -or $spells.Maximum -ne 100 -or $deployment.Items.Count -ne 2){throw 'New deployment controls have incorrect options.'}
    $spells.Value=15;$deployment.SelectedIndex=1
    $supportToggle=$form.Controls.Find('supportUnits',$true)[0]
    if($supportToggle.Enabled){throw 'Support toggle should be disabled for BlueStacks.'}
    $form.Controls.Find('platformSelector',$true)[0].SelectedIndex=1
    if(-not $supportToggle.Enabled){throw 'Support toggle should enable for Google.'}
    $swipeToggle=$form.Controls.Find('randomSwipes',$true)[0]
    if($null -eq $swipeToggle -or -not $swipeToggle.Enabled -or -not $swipeToggle.Checked){throw 'Random swipe checkbox should default on for Google ADB.'}
    $swipeToggle.Checked=$false
    $swipeSaved=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText((Join-Path $testFolder 'data\settings.json')))
    if($swipeSaved.RandomSwipes){throw 'Random swipe preference must save.'}
    $swipeToggle.Checked=$true
    $supportToggle.Checked=$false;$supportToggle.Checked=$true
    $openingDepth=$form.Controls.Find('openingDepth',$true)[0]
    $laterDepth=$form.Controls.Find('laterDepth',$true)[0]
    $fourDepth=$form.Controls.Find('fourSidesDepth',$true)[0]
    foreach($input in @($openingDepth,$laterDepth,$fourDepth)){if($input.Minimum -ne 0 -or $input.Maximum -ne 100 -or $input.Increment -ne 5){throw 'Incorrect depth bounds or increment.'}}
    $openingDepth.Value=10;$laterDepth.Value=70;$fourDepth.Value=20
    $deployment.SelectedIndex=0;$deployment.SelectedIndex=1
    $duration=$form.Controls.Find('durationSeconds',$true)[0]
    $gold=$form.Controls.Find('goldMinimum',$true)[0]
    $elixir=$form.Controls.Find('elixirMinimum',$true)[0]
    $dark=$form.Controls.Find('darkMinimum',$true)[0]
    $troops.Value=500;$duration.Value=3;$gold.Value=0;$elixir.Value=0;$dark.Value=100
    $saved=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText((Join-Path $testFolder 'data\settings.json')))
    if($saved.Spells -ne 15 -or $saved.DeploymentMode -ne 'FourSides'){throw 'Deployment mode and spell count were not saved.'}
    if(($saved.ActionOrder -join ',') -ne ($chosenOrder -join ',')){throw 'Chosen action order must persist through other input changes.'}
    if(-not $saved.DeploySiegeAndFirstHero){throw 'Support preference must save.'}
    if($saved.LShapeOpeningDepthPercent -ne 10 -or $saved.LShapeLaterDepthPercent -ne 70 -or $saved.FourSidesSpellDepthPercent -ne 20){throw 'Independent depth values were not preserved.'}
    if($saved.Troops -ne 500 -or $saved.AttackDurationSeconds -ne 3 -or $saved.MinGold -ne 0 -or $saved.MinElixir -ne 0 -or $saved.MinDarkElixir -ne 100){throw 'Configurable inputs were not saved.'}
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $logToggle=$form.Controls.Find('showLogs',$true)[0]
    $activity=$form.Controls.Find('activityLog',$true)[0]
    $private=[Reflection.BindingFlags]'Instance,NonPublic'
    $writeLog=$form.GetType().GetMethod('Write',$private)
    if($null -eq $logToggle -or $logToggle.Checked -or $activity.Visible){throw 'Log display should default hidden.'}
    if([ClashPekkas.Settings]::FromJson('{}').ShowLogs){throw 'Older settings should default to hidden logs.'}
    $beforeHidden=$activity.Text
    $writeLog.Invoke($form,@('hidden-render-check'))
    if($activity.Text -ne $beforeHidden){throw 'Hidden logs must not update the textbox.'}
    if(-not [IO.File]::ReadAllText((Join-Path $testFolder 'data\run.log')).Contains('hidden-render-check')){throw 'Hidden entries must still be written to the file log.'}
    $logToggle.Checked=$true
    if(-not $activity.Visible -or -not $activity.Text.Contains('hidden-render-check')){throw 'Showing logs must restore recent entries.'}
    $writeLog.Invoke($form,@('visible-render-check'))
    if(-not $activity.Text.Contains('visible-render-check')){throw 'Visible logs must follow new entries.'}
    $reopened=[ClashPekkas.MainForm]::new($testFolder,$reader,$picker,$activator)
    try{if(-not $reopened.Controls.Find('showLogs',$true)[0].Checked){throw 'Log display preference must survive reopening.'}}finally{$reopened.Dispose()}
    if(-not $fourDepth.Visible -or $openingDepth.Visible -or $laterDepth.Visible){throw 'Four-side depth visibility is incorrect.'}
    $deployment.SelectedIndex=0
    if($fourDepth.Visible -or -not $openingDepth.Visible -or -not $laterDepth.Visible){throw 'L-shaped depth visibility is incorrect.'}
    $spells.Value=0
    if($openingDepth.Enabled -or $laterDepth.Enabled -or $fourDepth.Enabled){throw 'Zero spells must disable depth controls.'}
    $spells.Value=1
    if(-not $openingDepth.Enabled -or $laterDepth.Enabled){throw 'One spell must disable later depth only.'}
    $spells.Value=6
    if(-not $openingDepth.Enabled -or $laterDepth.Enabled){throw 'Six spells must use opening depth only.'}
    $spells.Value=7
    if(-not $laterDepth.Enabled){throw 'Seven spells must enable later depth.'}
    $spells.Value=15
    if(-not $laterDepth.Enabled){throw 'Later depth must enable when a later batch exists.'}
    $disabled=Get-Content -LiteralPath (Join-Path $testFolder 'data\settings.json') -Raw | ConvertFrom-Json
    $disabled.UseSpells=$false
    $disabled | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testFolder 'data\settings.json')
    $spells.Value=14
    if($openingDepth.Enabled -or $laterDepth.Enabled -or $fourDepth.Enabled){throw 'UseSpells=false must disable all depth controls.'}
    $disabled.UseSpells=$true
    $disabled | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testFolder 'data\settings.json')
    $spells.Value=15
    $form.DrawToBitmap($render,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
    $render.Save("$TestOutputDirectory\app-depth-lshape.png",[Drawing.Imaging.ImageFormat]::Png)
    $deployment.SelectedIndex=1
    $start.PerformClick()
    if (-not $form.IsBusy -or -not $stop.Enabled -or $start.Enabled -or $script:activations -ne 0) { throw 'Google ADB countdown must enable Stop without activating a desktop window.' }
    if ($form.Text -ne 'Starting | 30 left (0/30) - Clash Farmer') { throw 'Countdown title is incorrect.' }
    if($troops.Enabled -or $duration.Enabled -or $gold.Enabled -or $elixir.Enabled -or $dark.Enabled){throw 'Run settings must be locked during countdown.'}
    if($spells.Enabled -or $deployment.Enabled){throw 'Spell and deployment settings must be locked during countdown.'}
    if($supportToggle.Enabled){throw 'Support toggle must lock while running.'}
    if($swipeToggle.Enabled){throw 'Random swipe setting must lock while running.'}
    if($orderButton.Enabled){throw 'Action order must lock during a run.'}
    if($openingDepth.Enabled -or $laterDepth.Enabled -or $fourDepth.Enabled){throw 'Depth controls must lock during countdown.'}
    if(-not $logToggle.Enabled){throw 'Log visibility must remain available during a run.'}
    $logToggle.Checked=$false
    $writeLog.Invoke($form,@('busy-hidden-render-check'))
    if(-not $form.IsBusy -or $activity.Visible -or $activity.Text.Contains('busy-hidden-render-check')){throw 'Hiding logs must skip textbox updates without stopping the run.'}
    $visibilitySaved=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText((Join-Path $testFolder 'data\settings.json')))
    if($visibilitySaved.ShowLogs -or $visibilitySaved.Troops -ne $saved.Troops -or ($visibilitySaved.ActionOrder -join ',') -ne ($saved.ActionOrder -join ',')){throw 'Saving log visibility during a run must preserve other settings.'}
    $stop.PerformClick()
    if ($form.IsBusy -or $stop.Enabled -or -not $start.Enabled) { throw 'Stopping must immediately restore idle controls.' }
    if(-not $spells.Enabled -or -not $deployment.Enabled){throw 'New settings must unlock after Stop.'}
    if(-not $fourDepth.Enabled){throw 'Visible depth control must unlock after Stop.'}
    if(-not $swipeToggle.Enabled){throw 'Random swipe setting must unlock after Stop.'}
    if ($form.Text -ne 'Stopped | 30 left (0/30) - Clash Farmer') { throw 'Cancelled title must retain the remaining count.' }
    $activity = $form.Controls.Find('activityLog',$true)[0]
    $length = $activity.TextLength
    $stop.PerformClick()
    if ($activity.TextLength -ne $length) { throw 'Disabled Stop must not log repeated stop messages.' }
    $start.PerformClick()
    $stop.PerformClick()
    if ($script:activations -ne 0 -or $form.IsBusy -or $stop.Enabled) { throw 'Background restart must not activate a desktop window.' }
    $private=[Reflection.BindingFlags]'Instance,NonPublic'
    $fake=[ClashPekkas.Runner]::new($null,$saved,30,$false,[Action[string]]{param($line)},[Random]::new(1))
    $fake.Start([DateTime]::Now)
    $fake.GetType().GetProperty('Completed').GetSetMethod($true).Invoke($fake,@(10))
    $form.GetType().GetField('runner',$private).SetValue($form,$fake)
    foreach($case in @(@('Home','On home'),@('Scouting','Searching'),@('Deploying','Battle running'),@('Spells','Battle running'),@('Result','Battle running'),@('Exit battle','Ending battle'),@('Return home','Returning home'),@('Navigation recovery','Recovering search'))) {
        $fake.GetType().GetProperty('State').GetSetMethod($true).Invoke($fake,@($case[0]))
        $form.GetType().GetMethod('UpdateWindowTitle',$private).Invoke($form,@())
        if($form.Text -ne ($case[1]+' | 20 left (10/30) - Clash Farmer')){throw "Incorrect taskbar activity/count: $($form.Text)"}
    }
    $fake.GetType().GetProperty('Completed').GetSetMethod($true).Invoke($fake,@(30))
    $fake.GetType().GetProperty('RecoveryAttempt').GetSetMethod($true).Invoke($fake,@(2))
    $fake.GetType().GetProperty('State').GetSetMethod($true).Invoke($fake,@('Home recovery'))
    $form.GetType().GetMethod('UpdateWindowTitle',$private).Invoke($form,@())
    if($form.Text -ne 'Recovering home - attempt 2/3 | 0 left (30/30) - Clash Farmer'){throw 'Recovery taskbar title must include its attempt and unchanged counter.'}
    $form.GetType().GetMethod('Finish',$private).Invoke($form,@('Finished 30 battles.',$false))
    if($form.Text -ne 'Finished | 0 left (30/30) - Clash Farmer'){throw 'Finished title must show zero remaining.'}
    $form.DrawToBitmap($render,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
    $render.Save("$TestOutputDirectory\app-preview.png",[Drawing.Imaging.ImageFormat]::Png)
    $form.Hide()
    'PASS: 1-30 battles, configurable inputs, Stop/restart, Clash Farmer taskbar activities and remaining counts through completion, and UI rendering. Player activation mocked; no live game input.'
} finally {
    $render.Dispose(); $form.Dispose()
    Remove-Item -LiteralPath (Join-Path $testFolder 'data\run.log'),(Join-Path $testFolder 'data\settings.json') -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $testFolder 'data') -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testFolder -ErrorAction SilentlyContinue
}

