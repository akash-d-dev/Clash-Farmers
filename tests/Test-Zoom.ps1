. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path (@($SourcePaths)+@("$PSScriptRoot\SelfTest.cs","$PSScriptRoot\RecoveryTests.cs","$PSScriptRoot\ZoomTests.cs")) -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
[ClashPekkas.ZoomTests]::Run()
$helper=Join-Path $AssetDirectory 'android-zoom.dex'
$header=[IO.File]::ReadAllBytes($helper)
if($header.Length -lt 100 -or [Text.Encoding]::ASCII.GetString($header,0,4) -ne "dex`n"){throw 'Bundled zoom helper is not a DEX file.'}
