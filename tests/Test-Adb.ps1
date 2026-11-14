. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$testDir=Join-Path ([IO.Path]::GetTempPath()) ('clash-adb-transport-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $testDir
$source=@'
using System;
using System.IO;
public static class FakeAdb {
    public static void Main(string[] args) {
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string command=String.Join(" ",args);
        File.AppendAllText(Path.Combine(root,"calls.txt"),command+Environment.NewLine);
        if(command.Contains("exec-out screencap -p")) {
            byte[] png=File.ReadAllBytes(Path.Combine(root,"frame.png"));
            using(Stream output=Console.OpenStandardOutput()) output.Write(png,0,png.Length);
        } else if(args.Length==5 && args[2]=="push") File.Copy(args[3],Path.Combine(root,"helper.dex"),true);
        else if(command.Contains("sha256sum")) {
            string file=Path.Combine(root,"helper.dex");
            if(File.Exists(file))using(var sha=System.Security.Cryptography.SHA256.Create())
                Console.WriteLine(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant()+"  helper.dex");
        }
        else if(command.Contains("app_process")) Console.WriteLine("ZOOM_OUT_SENT");
        else if(command.EndsWith("get-state")) Console.WriteLine("device");
        else if(command=="devices") Console.WriteLine("List of devices attached\r\n127.0.0.1:6520 device product:kiwi_x86_64_dev");
        else if(command.Contains("dumpsys window")) Console.WriteLine("mCurrentFocus=Window{test u0 com.supercell.clashofclans/com.supercell.titan.GameApp}");
    }
}
'@
try {
    $fakeExe=Join-Path $testDir 'FakeAdb.exe'
    Add-Type -TypeDefinition $source -OutputAssembly $fakeExe -OutputType ConsoleApplication
    Copy-Item -LiteralPath "$FixtureDirectory\attack-fixture.png" -Destination (Join-Path $testDir 'frame.png')
    $client=[ClashPekkas.AdbClient]::new($fakeExe,'127.0.0.1:5555')
    $image=$client.Screenshot()
    try { if($image.Width -ne 1600 -or $image.Height -ne 900){ throw 'Binary screenshot dimensions were corrupted.' } }
    finally { $image.Dispose() }
    $client.Tap(107,854)
    $calls=Get-Content -LiteralPath (Join-Path $testDir 'calls.txt')
    if($calls.Count -ne 2 -or $calls[0] -ne '-s 127.0.0.1:5555 exec-out screencap -p' -or $calls[1] -ne '-s 127.0.0.1:5555 shell input tap 107 854'){throw 'Android commands were incorrectly targeted.'}
    $settings=[ClashPekkas.Settings]::new();$settings.Platform='GooglePlayGames';$settings.UseAdbWhenAvailable=$false;$settings.GoogleAdbPath=$fakeExe
    $reader=[ClashPekkas.OcrReader]{param([byte[]]$png) '[]'}
    $game=[ClashPekkas.AndroidGame]::new([IntPtr]::Zero,$settings,$reader,(Join-Path $AssetDirectory 'pekka-face.png'),[Action[string]]{param($line)})
    try {
        $capture=$game.Capture();$capture.Dispose()
        $before=@(Get-Content -LiteralPath (Join-Path $testDir 'calls.txt') | Where-Object {$_ -match 'exec-out screencap'}).Count
        $observed=$game.ObserveNavigation();$observed.Dispose()
        $after=@(Get-Content -LiteralPath (Join-Path $testDir 'calls.txt') | Where-Object {$_ -match 'exec-out screencap'}).Count
        if($after -ne $before+1){throw 'Recovery observation must use exactly one Android capture.'}
        $game.Click([ClashPekkas.Vec]::new(.5,.5))
        $game.Swipe([ClashPekkas.Vec]::new(.4,.4),[ClashPekkas.Vec]::new(.6,.6),250)
        $game.Back()
        $game.ZoomOutStep();$game.ZoomOutStep()
        foreach($key in @('D1','A','Z','Q','W','E','R')){$game.PressKey([Windows.Forms.Keys]::$key)}
        if(-not $game.TrySelectSupport([Windows.Forms.Keys]::Z)){throw 'Android siege selection must use ADB.'}
        if(-not $game.TrySelectSupport([Windows.Forms.Keys]::Q)){throw 'Android hero selection must use ADB.'}
    }finally{$game.Dispose()}
    $calls=Get-Content -LiteralPath (Join-Path $testDir 'calls.txt')
    foreach($code in @(8,29,54,45,51,33,46)){if($calls -notcontains "-s 127.0.0.1:6520 shell input keyevent $code"){throw "Missing Android key event $code"}}
    if($calls -notcontains '-s 127.0.0.1:6520 shell input tap 800 450'){throw 'Google taps must scale to the Android screenshot, not Windows coordinates.'}
    if($calls -notcontains '-s 127.0.0.1:6520 shell input swipe 640 360 959 539 250'){throw 'Google swipe must scale to the Android screen and use the requested duration.'}
    if($calls -notcontains '-s 127.0.0.1:6520 shell input keyevent 4'){throw 'Recovery Back must be an Android key event, not desktop Escape.'}
    if($calls -match '^connect '){throw 'An online loopback alias must be reused without reconnecting.'}
    if(@($calls|Where-Object{$_ -match ' push .*android-zoom.dex'}).Count -ne 1){throw 'Zoom helper should be transferred once per connection.'}
    if(@($calls|Where-Object{$_ -match 'app_process / clashfarmer.input.ZoomInput 1600 900$'}).Count -ne 2){throw 'Zoom must target the Android screenshot size through ADB, not the desktop mouse.'}
    'PASS: Android screenshot bytes decode correctly and taps target the specified local emulator. Uses a fake ADB executable; no game input, network connection, desktop focus, or mouse input was used.'
} finally {
    foreach($name in @('FakeAdb.exe','FakeAdb.pdb','frame.png','calls.txt','helper.dex')) { Remove-Item -LiteralPath (Join-Path $testDir $name) -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $testDir -ErrorAction SilentlyContinue
}

