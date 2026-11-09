. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
Add-Type -Path $SourcePath -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions,System.Core,UIAutomationClient,UIAutomationTypes,WindowsBase
. "$ProjectRoot\scripts\Ocr.ps1"
$source=[Drawing.Bitmap]::new("$FixtureDirectory\reference-game.png")
try {
    foreach($label in @('GoldLoot','ElixirLoot')) {
        $region=if($label -eq 'GoldLoot'){[ClashPekkas.Vision]::GoldLoot}else{[ClashPekkas.Vision]::ElixirLoot}
        $crop=$source.Clone($region,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            foreach($scale in @(2,3,4)) {
                $large=[ClashPekkas.Vision]::Resize($crop,$crop.Width*$scale,$crop.Height*$scale)
                $mask=[ClashPekkas.Vision]::WhiteDigits($large)
                try {
                    foreach($mode in @('color','mask')) {
                        $stream=[IO.MemoryStream]::new()
                        try {
                            if($mode -eq 'color'){$large.Save($stream,[Drawing.Imaging.ImageFormat]::Png)}else{$mask.Save($stream,[Drawing.Imaging.ImageFormat]::Png)}
                            "$label $scale $mode : $(Read-GameText $stream.ToArray())"
                        } finally {$stream.Dispose()}
                    }
                } finally {$large.Dispose();$mask.Dispose()}
            }
        } finally {$crop.Dispose()}
    }
} finally {$source.Dispose()}

