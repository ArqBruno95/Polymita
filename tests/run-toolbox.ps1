$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
& (Join-Path $root 'build.ps1') -OutputDirectory $out
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('System.Drawing.dll','System.Windows.Forms.dll', (Join-Path $out 'Polymita.gha'), 'C:\Program Files\Rhino 8\Plug-ins\Grasshopper\Grasshopper.dll')
$arguments = @('/nologo','/target:exe','/utf8output',('/out:' + (Join-Path $out 'ToolboxTests.exe')))
$arguments += $refs | ForEach-Object { '/r:' + $_ }
$arguments += '/r:C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll'
$arguments += Join-Path $PSScriptRoot 'ToolboxTests.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas de herramientas.' }
Copy-Item -LiteralPath (Join-Path $out 'Polymita.gha') -Destination (Join-Path $out 'Polymita.dll') -Force
& (Join-Path $out 'ToolboxTests.exe') | Tee-Object -FilePath (Join-Path $out 'toolbox.txt')
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de herramientas.' }

