. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$source=[Drawing.Bitmap]::new((Join-Path $FixtureDirectory 'placement-annotated.png'))
try {
    $crop=$source.Clone([Drawing.Rectangle]::new(4,26,1585,896),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {$image=[ClashPekkas.Vision]::Resize($crop,1600,900)}finally{$crop.Dispose()}
}finally{$source.Dispose()}
try {
    [ClashPekkas.Vision]::DrawPlan($image,[ClashPekkas.Settings]::new())
    $image.Save((Join-Path $TestOutputDirectory 'placement-correction-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$image.Dispose()}
