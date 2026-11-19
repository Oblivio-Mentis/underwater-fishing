param([ValidateSet('Menu','Host','Client')][string]$Mode='Menu', [string]$Build='FirstPlayable')
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $projectRoot "Builds/$Build"
$gameExecutable = Join-Path $buildDirectory 'UnderwaterFishing.exe'
if (-not (Test-Path -LiteralPath $gameExecutable)) { throw "Build is missing: $gameExecutable" }
$arguments = @('-screen-fullscreen','1')
if ($Mode -eq 'Host') { $arguments += '-raid-host' }
if ($Mode -eq 'Client') { $arguments += '-raid-client' }
Start-Process -FilePath $gameExecutable -WorkingDirectory $buildDirectory -ArgumentList $arguments
