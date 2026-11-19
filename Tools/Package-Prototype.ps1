param([string]$Build='FirstPlayable')
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $projectRoot "Builds/$Build"
$gameExecutable = Join-Path $buildDirectory 'UnderwaterFishing.exe'
if (-not (Test-Path -LiteralPath $gameExecutable)) { throw "Build is missing: $gameExecutable" }
$runningPrototype=Get-Process -Name UnderwaterFishing -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $gameExecutable }
if($runningPrototype) { throw 'Close instances of this build before packaging; Unity holds its data files open.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'steam_appid.txt') -Destination $buildDirectory
foreach ($guide in 'Docs/HOW_TO_PLAY.md', 'Docs/RAID-013/COOP-RU.md', 'Docs/THIRD_PARTY.md') {
    Copy-Item -LiteralPath (Join-Path $projectRoot $guide) -Destination $buildDirectory -Force
}
$licenses = Join-Path $buildDirectory 'Licenses'
[IO.Directory]::CreateDirectory($licenses) | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Docs/Licenses') |
    Copy-Item -Destination $licenses -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/_Game/UI/Overhaul/Fonts/OFL.txt') -Destination (Join-Path $licenses 'PT-Sans-OFL.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt') -Destination $licenses -Force

foreach ($name in 'Start-Menu', 'Start-Solo') {
    $mode = if ($name -eq 'Start-Solo') { ' -raid-host' } else { '' }
    $lines = @(
        '@echo off'
        'setlocal DisableDelayedExpansion'
        'cd /d "%~dp0" || exit /b 1'
        'if not exist "UserData\" mkdir "UserData" || exit /b 1'
        ('start "" "%~dp0UnderwaterFishing.exe"' + $mode + ' -raid-save "%~dp0UserData\team-progress.json"')
    )
    [IO.File]::WriteAllLines((Join-Path $buildDirectory "$name.cmd"), $lines, [Text.Encoding]::ASCII)
}

function Add-PlayerFiles($zip, $directory, [string]$prefix) {
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        $entry = "$prefix/$($item.Name)"
        if ($item.PSIsContainer) {
            if ($item.Name -in 'UserData','QA' -or $item.Name -match 'DoNotShip|DontShipItWithYourGame') { continue }
            Add-PlayerFiles $zip $item.FullName $entry
        } else {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $item.FullName, $entry) | Out-Null
        }
    }
}

$archive = Join-Path $projectRoot "Builds/UnderwaterFishing-$Build.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$pending = "$archive.$([guid]::NewGuid().ToString('N')).tmp"
try {
    $zip = [IO.Compression.ZipFile]::Open($pending, 'Create')
    try { Add-PlayerFiles $zip $buildDirectory (Split-Path -Leaf $buildDirectory) }
    finally { $zip.Dispose() }
    Move-Item -LiteralPath $pending -Destination $archive -Force
} finally {
    if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending }
}
Get-FileHash -LiteralPath $gameExecutable,$archive -Algorithm SHA256
