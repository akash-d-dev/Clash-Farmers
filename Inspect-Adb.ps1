param([string]$OutputPath = "$PSScriptRoot\adb-current.png")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$config=[Web.Script.Serialization.JavaScriptSerializer]::new().Deserialize([IO.File]::ReadAllText("$PSScriptRoot\settings.json"),[ClashPekkas.Settings])
$client=[ClashPekkas.AdbClient]::Connect($config)
$focus=$client.Text('shell dumpsys window windows')
($focus -split '\r?\n' | Where-Object { $_ -match 'mCurrentFocus|mFocusedApp' }) -join [Environment]::NewLine
$image=$client.Screenshot()
try {
    $image.Save($OutputPath,[Drawing.Imaging.ImageFormat]::Png)
    "PASS: live local ADB screenshot is $($image.Width) x $($image.Height). Saved to $OutputPath. No taps sent."
} finally { $image.Dispose() }

