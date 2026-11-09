param([Parameter(Mandatory=$true)][string]$ImagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, System.Web.Extensions
Add-Type -Path "$PSScriptRoot\ClashPekkas.cs" -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
$image = [Drawing.Bitmap]::new($ImagePath)
try {
    # Game viewport in the user's 2559 x 1439 windowed screenshot; excludes chrome and taskbar.
    $gameRect = [Drawing.Rectangle]::new(56,32,2412,1357)
    $crop = $image.Clone($gameRect,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { $canonical = [ClashPekkas.Vision]::Resize($crop,1600,900) } finally { $crop.Dispose() }
    try {
        $canonical.Save("$PSScriptRoot\reference-game.png",[Drawing.Imaging.ImageFormat]::Png)
        $face = $canonical.Clone([ClashPekkas.Vision]::TroopFace,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $face.Save("$PSScriptRoot\pekka-face.png",[Drawing.Imaging.ImageFormat]::Png) } finally { $face.Dispose() }
        $spell = $canonical.Clone([ClashPekkas.Vision]::SpellFace,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $spell.Save("$PSScriptRoot\spell-face.png",[Drawing.Imaging.ImageFormat]::Png) } finally { $spell.Dispose() }
        [ClashPekkas.Vision]::DrawPlan($canonical,[ClashPekkas.Settings]::new())
        $canonical.Save("$PSScriptRoot\reference-preview.png",[Drawing.Imaging.ImageFormat]::Png)
    } finally { $canonical.Dispose() }
} finally { $image.Dispose() }

