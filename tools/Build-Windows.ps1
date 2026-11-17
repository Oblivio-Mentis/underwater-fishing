param([string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidLog = Join-Path $raidRoot 'Logs/Build-Windows.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $raidLog) | Out-Null
$raidBuild = Start-Process -FilePath $EditorPath -ArgumentList "-batchmode -nographics -quit -projectPath `"$raidRoot`" -executeMethod UnderwaterFishing.Editor.FoundationBuilder.BuildFromSource -logFile `"$raidLog`"" -WindowStyle Hidden -PassThru
Write-Output "Windows build process: $($raidBuild.Id)"
$raidBuild.WaitForExit()
if ($raidBuild.ExitCode -ne 0) { throw "Unity build exited $($raidBuild.ExitCode). Inspect $raidLog. Close the interactive Editor before using batch mode." }
if (!(Select-String -LiteralPath $raidLog -SimpleMatch 'RAID_BUILD_SUCCEEDED' -Quiet)) { throw 'Build success marker is missing.' }
Write-Output (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')
