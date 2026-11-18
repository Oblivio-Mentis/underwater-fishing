param([string]$Build='FirstPlayable')
$ErrorActionPreference='Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $projectRoot "Builds/$Build"
$gameExecutable = Join-Path $buildDirectory 'UnderwaterFishing.exe'
if (-not (Test-Path -LiteralPath $gameExecutable)) { throw "Build is missing: $gameExecutable" }
$runningPrototype=Get-Process -Name UnderwaterFishing -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $gameExecutable }
if($runningPrototype) { throw 'Close instances of this build before packaging; Unity holds its data files open.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'steam_appid.txt') -Destination $buildDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs/HOW_TO_PLAY.md') -Destination (Join-Path $buildDirectory 'HOW_TO_PLAY.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs/THIRD_PARTY.md') -Destination $buildDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs/Licenses') -Destination $buildDirectory -Recurse -Force
$archive = Join-Path $projectRoot "Builds/UnderwaterFishing-$Build.zip"
Compress-Archive -LiteralPath $buildDirectory -DestinationPath $archive -Force
Get-FileHash -LiteralPath $gameExecutable,$archive -Algorithm SHA256
