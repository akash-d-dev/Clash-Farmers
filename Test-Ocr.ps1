param([string]$ImagePath = 'C:\Users\Amd\AppData\Local\Temp\codex-clipboard-70f35265-bca1-456c-8c3f-5912169bb5c3.png')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Ocr.ps1"
Add-Type -AssemblyName System.Drawing
$img = [Drawing.Bitmap]::new($ImagePath)
try {
    foreach ($region in @(@(0.80,0.65,0.18,0.15), @(0.18,0.82,0.055,0.055))) {
        $rect = [Drawing.Rectangle]::new([int]($img.Width*$region[0]),[int]($img.Height*$region[1]),[int]($img.Width*$region[2]),[int]($img.Height*$region[3]))
        $crop = $img.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $ms = [IO.MemoryStream]::new()
        try { $crop.Save($ms,[Drawing.Imaging.ImageFormat]::Png); Read-GameText $ms.ToArray() }
        finally { $ms.Dispose(); $crop.Dispose() }
    }
} finally { $img.Dispose() }
