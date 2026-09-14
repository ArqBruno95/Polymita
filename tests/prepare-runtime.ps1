param([string]$Name = 'WireShelf.TestBuild')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('C:\Program Files\Rhino 8\Plug-ins\Grasshopper\Grasshopper.dll','C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll','C:\Program Files\Rhino 8\System\RhinoCommon.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll')
$arguments = @('/nologo','/target:library','/utf8output',('/out:' + (Join-Path $out ($Name + '.dll'))))
$arguments += $refs | ForEach-Object { '/r:' + $_ }
$arguments += '/r:C:\Program Files\Rhino 8\System\RhinoWindows.dll'
$arguments += Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName
$arguments += Join-Path $PSScriptRoot 'IntegrationTests.cs'
$arguments += Join-Path $PSScriptRoot 'ToolboxNativeTests.cs'
$arguments += Join-Path $PSScriptRoot 'OperationsTests.cs'
$arguments += Join-Path $PSScriptRoot 'VisualRegressionTests.cs'
$arguments += Join-Path $PSScriptRoot 'GestureTests.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Error de compilación de pruebas de integración.' }
foreach ($framework in @('net48', 'net7.0', 'net8.0')) {
    $destination = Join-Path $out ('runtimes\' + $framework)
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    if (Test-Path -LiteralPath (Join-Path $destination '0Harmony.dll')) { continue }
    Copy-Item -LiteralPath (Join-Path $root ('lib\harmony-2.3.3\lib\' + $framework + '\0Harmony.dll')) -Destination $destination -Force
}

