param([string]$CropPath="$PSScriptRoot\diagnostics\last-loot-gold.png")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$PSScriptRoot\Ocr.ps1"
$crop=[Drawing.Bitmap]::new($CropPath)
try {
    foreach($scale in @(2,4,3)) {
        $large=[ClashPekkas.Vision]::Resize($crop,$crop.Width*$scale,$crop.Height*$scale)
        $mask=[ClashPekkas.Vision]::WhiteDigits($large)
        try {
            $stream=[IO.MemoryStream]::new()
            try {$mask.Save($stream,[Drawing.Imaging.ImageFormat]::Png);"Scale $scale : $(Read-GameText $stream.ToArray())"} finally {$stream.Dispose()}
        } finally {$large.Dispose();$mask.Dispose()}
    }
} finally {$crop.Dispose()}
