param([string]$LibrariesDirectory)
$ErrorActionPreference = 'Stop'
$defaultLibraries = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Grasshopper\Libraries'))
$base = if ($LibrariesDirectory) { [IO.Path]::GetFullPath($LibrariesDirectory) } else { $defaultLibraries }
if ($base.TrimEnd('\') -ieq $defaultLibraries.TrimEnd('\')) {
    if (Get-Process Rhino -ErrorAction SilentlyContinue) { throw 'Close all Rhino windows before installing Polymita.' }
}
$source = Join-Path $PSScriptRoot 'release\Polymita.gha'
if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot 'dist\Polymita.gha' }
if (!(Test-Path -LiteralPath $source)) { throw 'Extract the complete ZIP, including release\Polymita.gha, before installing.' }
$folder = Join-Path $base 'Polymita'
$destination = Join-Path $folder 'Polymita.gha'
$stamp = (Get-Date -Format yyyyMMddHHmmssfff) + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$staged = Join-Path $folder ('Polymita.gha.new-' + $stamp)
Copy-Item -LiteralPath $source -Destination $staged
$backups = @()
try {
    # Old spellings are only recognized here to prevent two active plugin builds.
    $names = @('Polymita','WireShelf','Zunzun','Zunzún','Nitido','Nítido')
    $candidates = @()
    foreach ($name in $names) {
        $candidates += Join-Path $base ($name + '.gha')
        $candidates += Join-Path (Join-Path $base $name) ($name + '.gha')
    }
    foreach ($old in ($candidates | Select-Object -Unique)) {
        $checked = [IO.Path]::GetFullPath($old)
        if (!$checked.StartsWith($base.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation path.' }
        if (Test-Path -LiteralPath $checked) {
            $backup = $checked + '.previous-' + $stamp
            Move-Item -LiteralPath $checked -Destination $backup
            $backups += [pscustomobject]@{Original=$checked; Backup=$backup}
        }
    }
    Move-Item -LiteralPath $staged -Destination $destination
}
catch {
    foreach ($pair in $backups) {
        if ((Test-Path -LiteralPath $pair.Backup) -and !(Test-Path -LiteralPath $pair.Original)) {
            Move-Item -LiteralPath $pair.Backup -Destination $pair.Original
        }
    }
    throw
}
finally { if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged } }
Write-Output ('Polymita installed: ' + $destination)
Write-Output 'Earlier assemblies were archived. Existing library and settings will migrate on the next plugin start.'
