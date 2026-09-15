$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'test-output'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /utf8output ('/out:' + (Join-Path $out 'StorageTests.exe')) /r:System.Runtime.Serialization.dll /r:System.Drawing.dll (Join-Path $root 'src\Library.cs') (Join-Path $root 'src\SettingsStorage.cs') (Join-Path $root 'src\GridLayout.cs') (Join-Path $root 'src\ToolboxSettings.cs') (Join-Path $PSScriptRoot 'StorageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Storage test build failed.' }
& (Join-Path $out 'StorageTests.exe') | Tee-Object -FilePath (Join-Path $out 'storage.txt')
if ($LASTEXITCODE -ne 0) { throw 'Storage tests failed.' }
