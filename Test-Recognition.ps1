$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$PSScriptRoot\Ocr.ps1"
$image = [Drawing.Bitmap]::new("$PSScriptRoot\reference-game.png")
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
    $reference = [Drawing.Bitmap]::new("$PSScriptRoot\pekka-face.png")
    try {
        if (-not [ClashPekkas.Vision]::Similar($face,$reference)) { throw 'Troop reference mismatch.' }
        'PASS: P.E.K.K.A reference matches the actual first troop card.'
    } finally { $face.Dispose(); $reference.Dispose() }
} finally { $image.Dispose() }
$spellSource=[Drawing.Bitmap]::new("$PSScriptRoot\live-deployed25.png")
try { $spellFrame=[ClashPekkas.Vision]::Resize($spellSource,1600,900) } finally { $spellSource.Dispose() }
try {
    $spellFace=$spellFrame.Clone([ClashPekkas.Vision]::SpellFace,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $spellReference=[Drawing.Bitmap]::new("$PSScriptRoot\spell-face.png")
    try {
        if(-not [ClashPekkas.Vision]::Similar($spellFace,$spellReference)) { throw 'Haste spell reference does not match the live spell card.' }
        'PASS: spell-card position and appearance match the live battle screenshot.'
    } finally { $spellFace.Dispose();$spellReference.Dispose() }
} finally { $spellFrame.Dispose() }
$reader = [ClashPekkas.OcrReader] { param([byte[]]$png) Read-GameText $png }
if ([string]::IsNullOrWhiteSpace($reader.Invoke([IO.File]::ReadAllBytes("$PSScriptRoot\reference-game.png")))) { throw 'OCR callback returned no result.' }
[Windows.Forms.Application]::EnableVisualStyles()
$testFolder = Join-Path ([IO.Path]::GetTempPath()) ('clash-pekkas-ui-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testFolder
Copy-Item -LiteralPath "$PSScriptRoot\settings.json" -Destination (Join-Path $testFolder 'settings.json')
$testConfig=Get-Content -LiteralPath (Join-Path $testFolder 'settings.json') -Raw | ConvertFrom-Json
$testConfig | Add-Member -NotePropertyName UseAdbWhenAvailable -NotePropertyValue $false -Force
$testConfig | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testFolder 'settings.json')
$script:activations = 0
$picker = [ClashPekkas.PlayerPicker] { return [IntPtr]::new(1) }
$activator = [ClashPekkas.PlayerActivator] { param([IntPtr]$window) $script:activations++ }
$form = [ClashPekkas.MainForm]::new($testFolder,$reader,$picker,$activator)
$render = [Drawing.Bitmap]::new($form.Width,$form.Height)
try {
    if ($form.Controls.Count -lt 10) { throw 'App controls are missing.' }
    if ($form.Text -ne 'Clash Farmer - Ready') { throw 'Idle app title is incorrect.' }
    $stop = $form.Controls.Find('stopButton',$true)[0]
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
    $duration=$form.Controls.Find('durationSeconds',$true)[0]
    $gold=$form.Controls.Find('goldMinimum',$true)[0]
    $elixir=$form.Controls.Find('elixirMinimum',$true)[0]
    $dark=$form.Controls.Find('darkMinimum',$true)[0]
    $troops.Value=500;$duration.Value=3;$gold.Value=0;$elixir.Value=0;$dark.Value=100
    $saved=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText((Join-Path $testFolder 'settings.json')))
    if($saved.Troops -ne 500 -or $saved.AttackDurationSeconds -ne 3 -or $saved.MinGold -ne 0 -or $saved.MinElixir -ne 0 -or $saved.MinDarkElixir -ne 100){throw 'Configurable inputs were not saved.'}
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $start.PerformClick()
    if (-not $form.IsBusy -or -not $stop.Enabled -or $start.Enabled -or $script:activations -ne 1) { throw 'Countdown must enable Stop, disable Start, and activate the selected player once.' }
    if ($form.Text -ne 'Starting | 30 left (0/30) - Clash Farmer') { throw 'Countdown title is incorrect.' }
    if($troops.Enabled -or $duration.Enabled -or $gold.Enabled -or $elixir.Enabled -or $dark.Enabled){throw 'Run settings must be locked during countdown.'}
    $stop.PerformClick()
    if ($form.IsBusy -or $stop.Enabled -or -not $start.Enabled) { throw 'Stopping must immediately restore idle controls.' }
    if ($form.Text -ne 'Stopped | 30 left (0/30) - Clash Farmer') { throw 'Cancelled title must retain the remaining count.' }
    $activity = $form.Controls.Find('activityLog',$true)[0]
    $length = $activity.TextLength
    $stop.PerformClick()
    if ($activity.TextLength -ne $length) { throw 'Disabled Stop must not log repeated stop messages.' }
    $start.PerformClick()
    $stop.PerformClick()
    if ($script:activations -ne 2 -or $form.IsBusy -or $stop.Enabled) { throw 'Restart after cancellation failed.' }
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
    $form.GetType().GetMethod('Finish',$private).Invoke($form,@('Finished 30 battles.',$false))
    if($form.Text -ne 'Finished | 0 left (30/30) - Clash Farmer'){throw 'Finished title must show zero remaining.'}
    $form.DrawToBitmap($render,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
    $render.Save("$PSScriptRoot\app-preview.png",[Drawing.Imaging.ImageFormat]::Png)
    $form.Hide()
    'PASS: 1-30 battles, configurable inputs, Stop/restart, Clash Farmer taskbar activities and remaining counts through completion, and UI rendering. Player activation mocked; no live game input.'
} finally {
    $render.Dispose(); $form.Dispose()
    Remove-Item -LiteralPath (Join-Path $testFolder 'run.log'),(Join-Path $testFolder 'settings.json') -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testFolder -ErrorAction SilentlyContinue
}

