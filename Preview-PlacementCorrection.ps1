$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$source=[Drawing.Bitmap]::new('C:\Users\Amd\AppData\Local\Temp\codex-clipboard-6e933be2-2413-49da-93b4-ac8285a4bb97.png')
try {
    $crop=$source.Clone([Drawing.Rectangle]::new(4,26,1585,896),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {$image=[ClashPekkas.Vision]::Resize($crop,1600,900)}finally{$crop.Dispose()}
}finally{$source.Dispose()}
try {
    [ClashPekkas.Vision]::DrawPlan($image,[ClashPekkas.Settings]::new())
    $image.Save((Join-Path $PSScriptRoot 'placement-correction-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$image.Dispose()}
