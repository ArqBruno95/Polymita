param([string]$RhinoRoot = 'C:\Program Files\Rhino 8', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root 'dist' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @(
    (Join-Path $RhinoRoot 'Plug-ins\Grasshopper\Grasshopper.dll'),
    (Join-Path $RhinoRoot 'Plug-ins\Grasshopper\GH_IO.dll'),
    (Join-Path $RhinoRoot 'System\RhinoCommon.dll'),
    (Join-Path $RhinoRoot 'System\RhinoWindows.dll'),
    'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'System.Runtime.Serialization.dll', 'System.Xml.dll'
)
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework compiler was not found. Use dotnet build with the SDK and .NET 4.8 targeting pack.' }
$argsList = @('/nologo', '/target:library', '/platform:anycpu', '/optimize+', '/warn:4', '/utf8output', ('/out:' + (Join-Path $out 'Polymita.gha')))
foreach ($ref in $refs) { $argsList += '/reference:' + $ref }
# Carried inside the assembly so the plug-in installs as a single .gha. The copies below
# are still written next to it, and WireStyles prefers them when they are present.
foreach ($framework in @('net48', 'net7.0', 'net8.0')) {
    $argsList += '/resource:' + (Join-Path $root ('lib\harmony-2.3.3\lib\' + $framework + '\0Harmony.dll')) + ',Polymita.runtimes.' + $framework + '.0Harmony.dll'
}
$argsList += @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @argsList
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
foreach ($framework in @('net48', 'net7.0', 'net8.0')) {
    $destination = Join-Path $out ('runtimes\' + $framework)
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $dependency = Join-Path $root ('lib\harmony-2.3.3\lib\' + $framework + '\0Harmony.dll')
    $installedDependency = Join-Path $destination '0Harmony.dll'
    if ((Test-Path -LiteralPath $installedDependency) -and ((Get-FileHash -LiteralPath $dependency).Hash -eq (Get-FileHash -LiteralPath $installedDependency).Hash)) { continue }
    Copy-Item -LiteralPath $dependency -Destination $destination -Force
}
Get-Item -LiteralPath (Join-Path $out 'Polymita.gha') | Select-Object FullName,Length


