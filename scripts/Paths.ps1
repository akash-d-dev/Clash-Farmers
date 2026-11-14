# Shared, absolute paths so scripts work from any current directory.
$ProjectRoot=Split-Path -Parent $PSScriptRoot
# Both executable builds and Add-Type callers compile this same source set.
# Program.cs is the executable-only PowerShell host and is added by Build.ps1.
$SourcePaths=@(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Filter '*.cs' -File -Recurse |
    Where-Object { $_.Name -ne 'Program.cs' } | Sort-Object FullName | ForEach-Object { $_.FullName })
$FixtureDirectory=Join-Path $ProjectRoot 'tests\fixtures'
$AssetDirectory=Join-Path $ProjectRoot 'assets'
$DataDirectory=Join-Path $ProjectRoot 'data'
$TestOutputDirectory=Join-Path $DataDirectory 'test-results'
$null=New-Item -ItemType Directory -Path $DataDirectory,$TestOutputDirectory -Force
