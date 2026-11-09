# Shared, absolute paths so scripts work from any current directory.
$ProjectRoot=Split-Path -Parent $PSScriptRoot
$SourcePath=Join-Path $ProjectRoot 'src\ClashFarmer.cs'
$FixtureDirectory=Join-Path $ProjectRoot 'tests\fixtures'
$AssetDirectory=Join-Path $ProjectRoot 'assets'
$DataDirectory=Join-Path $ProjectRoot 'data'
$TestOutputDirectory=Join-Path $DataDirectory 'test-results'
$null=New-Item -ItemType Directory -Path $DataDirectory,$TestOutputDirectory -Force
