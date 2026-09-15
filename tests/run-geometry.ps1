$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
& (Join-Path $PSScriptRoot 'prepare-runtime.ps1') -Name 'Polymita.Regression085'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /utf8output ('/out:' + (Join-Path $out 'GeometryRunner.exe')) (Join-Path $PSScriptRoot 'GeometryRunner.cs')
if ($LASTEXITCODE -ne 0) { throw 'Geometry runner build failed.' }
& (Join-Path $out 'GeometryRunner.exe') (Join-Path $out 'Polymita.Regression085.dll') | Tee-Object -FilePath (Join-Path $out 'geometry.txt')
if ($LASTEXITCODE -ne 0) { throw 'Geometry tests failed.' }
