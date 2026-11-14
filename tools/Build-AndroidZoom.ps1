param([Parameter(Mandatory=$true)][string]$JdkDirectory,[Parameter(Mandatory=$true)][string]$D8Jar)
. "$PSScriptRoot\..\scripts\Paths.ps1"
$ErrorActionPreference='Stop'
$output=Join-Path $TestOutputDirectory ('android-zoom-build-'+[Guid]::NewGuid().ToString('N'))
$classes=Join-Path $output 'classes'
$null=New-Item -ItemType Directory -Path $classes -Force
& (Join-Path $JdkDirectory 'bin\javac.exe') -source 8 -target 8 -Xlint:-options -encoding UTF-8 -d $classes (Join-Path $ProjectRoot 'android\ZoomInput.java')
if($LASTEXITCODE -ne 0){throw 'Zoom helper Java compilation failed.'}
& (Join-Path $JdkDirectory 'bin\java.exe') -cp $D8Jar com.android.tools.r8.D8 --release --min-api 26 --output $output (Join-Path $classes 'clashfarmer\input\ZoomInput.class')
if($LASTEXITCODE -ne 0){throw 'Zoom helper DEX compilation failed.'}
Copy-Item -LiteralPath (Join-Path $output 'classes.dex') -Destination (Join-Path $AssetDirectory 'android-zoom.dex') -Force
Get-FileHash -LiteralPath (Join-Path $AssetDirectory 'android-zoom.dex') -Algorithm SHA256
