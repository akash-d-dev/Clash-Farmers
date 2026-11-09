$ErrorActionPreference='Stop'
# Windows PowerShell 5.1 supplies the same OCR host used by Start.ps1.
if($PSVersionTable.PSEdition -eq 'Core') { & powershell.exe -NoProfile -File $PSCommandPath;exit $LASTEXITCODE }
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Web.Extensions,UIAutomationClient,UIAutomationTypes,WindowsBase
$refs=@([System.Windows.Forms.Form].Assembly.Location,[Drawing.Bitmap].Assembly.Location,[Web.Script.Serialization.JavaScriptSerializer].Assembly.Location,[System.Linq.Enumerable].Assembly.Location,[System.Management.Automation.PowerShell].Assembly.Location,[System.Windows.Automation.AutomationElement].Assembly.Location,[System.Windows.Automation.ControlType].Assembly.Location,[System.Windows.Rect].Assembly.Location)
$compiler=Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$arguments=@('/nologo','/noconfig','/reference:System.dll','/target:winexe','/platform:x64',('/out:'+(Join-Path $PSScriptRoot 'ClashFarmer.exe')))
$arguments+=@($refs|Select-Object -Unique|ForEach-Object {'/reference:'+$_})
$arguments+=@((Join-Path $PSScriptRoot 'ClashPekkas.cs'),(Join-Path $PSScriptRoot 'Program.cs'))
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Runner compilation failed.'}
# Preserve existing shortcuts that still launch the old filename.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ClashFarmer.exe') -Destination (Join-Path $PSScriptRoot 'ClashPekkas.exe') -Force
'Built ClashFarmer.exe (legacy ClashPekkas.exe launcher retained)'
