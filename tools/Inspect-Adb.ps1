param([string]$OutputPath = "$PSScriptRoot\..\data\captures\adb-current.png")
. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePaths -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$config=[ClashPekkas.Settings]::FromJson([IO.File]::ReadAllText("$DataDirectory\settings.json"))
$client=[ClashPekkas.AdbClient]::Connect($config)
$focus=$client.Text('shell dumpsys window windows')
($focus -split '\r?\n' | Where-Object { $_ -match 'mCurrentFocus|mFocusedApp' }) -join [Environment]::NewLine
$image=$client.Screenshot()
try {
    $image.Save($OutputPath,[Drawing.Imaging.ImageFormat]::Png)
    "PASS: live local ADB screenshot is $($image.Width) x $($image.Height). Saved to $OutputPath. No taps sent."
} finally { $image.Dispose() }

