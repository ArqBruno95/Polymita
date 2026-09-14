param([string]$RhinoRoot = 'C:\Program Files\Rhino 8')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework compiler was not found.' }
# The gesture suite reaches internal interaction types, so it is compiled into the
# same assembly as the sources. run-gestures.py loads this exact file name inside Rhino.
$refs = @(
    (Join-Path $RhinoRoot 'Plug-ins\Grasshopper\Grasshopper.dll'),
    (Join-Path $RhinoRoot 'Plug-ins\Grasshopper\GH_IO.dll'),
    (Join-Path $RhinoRoot 'System\RhinoCommon.dll'),
    (Join-Path $RhinoRoot 'System\RhinoWindows.dll'),
    'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'System.Runtime.Serialization.dll', 'System.Xml.dll'
)
$argsList = @('/nologo', '/target:library', '/platform:anycpu', '/utf8output',
    ('/out:' + (Join-Path $out 'Polymita.GestureTest06.dll')))
foreach ($ref in $refs) { $argsList += '/reference:' + $ref }
$argsList += @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$argsList += (Join-Path $PSScriptRoot 'GestureTests.cs')
& $compiler @argsList
if ($LASTEXITCODE -ne 0) { throw "Gesture test build failed: $LASTEXITCODE" }
foreach ($framework in @('net48', 'net7.0', 'net8.0')) {
    $destination = Join-Path $out ('runtimes\' + $framework)
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $root ('lib\harmony-2.3.3\lib\' + $framework + '\0Harmony.dll')) -Destination $destination -Force
}
Write-Output 'Built test-output\Polymita.GestureTest06.dll'
Write-Output 'Now run tests\run-gestures.py from Rhino with RunPythonScript; results go to test-output\gestures.txt.'
