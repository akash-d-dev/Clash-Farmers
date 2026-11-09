. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
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
        } else if(command.EndsWith("get-state")) Console.WriteLine("device");
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
    'PASS: Android screenshot bytes decode correctly and taps target the specified local emulator. Uses a fake ADB executable; no game input, network connection, desktop focus, or mouse input was used.'
} finally {
    foreach($name in @('FakeAdb.exe','FakeAdb.pdb','frame.png','calls.txt')) { Remove-Item -LiteralPath (Join-Path $testDir $name) -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $testDir -ErrorAction SilentlyContinue
}

