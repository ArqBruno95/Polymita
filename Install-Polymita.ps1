$ErrorActionPreference = 'Stop'
if (Get-Process Rhino -ErrorAction SilentlyContinue) { throw 'Close Rhino before installing Polymita, then run this script again.' }
$source=Join-Path $PSScriptRoot 'dist\Polymita.gha'
if (!(Test-Path -LiteralPath $source)) { throw 'Extract the entire ZIP before installing.' }
$base=Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Grasshopper\Libraries'
$folder=Join-Path $base 'Polymita'
$stamp=Get-Date -Format yyyyMMddHHmmss
New-Item -ItemType Directory -Path $folder -Force | Out-Null
foreach($relative in @('WireShelf\WireShelf.gha','WireShelf.gha','Nitido\Nitido.gha','Zunzun\Zunzun.gha','Polymita\Polymita.gha')) {
 $old=Join-Path $base $relative
 if(Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination ($old+'.previous-'+$stamp) }
}
Copy-Item -LiteralPath $source -Destination (Join-Path $folder 'Polymita.gha')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\runtimes') -Destination $folder -Recurse -Force
Write-Output ('Polymita installed: '+$folder)
Write-Output 'Your existing library and settings are preserved. Start Rhino and Grasshopper.'
