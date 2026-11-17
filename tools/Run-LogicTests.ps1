param([string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidResult = Join-Path $raidRoot '.studioigor/test-results/logic-results.xml'
$raidLog = Join-Path $raidRoot 'Logs/Logic-Tests.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $raidResult),(Split-Path -Parent $raidLog) | Out-Null
if (Test-Path -LiteralPath $raidResult) { Remove-Item -LiteralPath $raidResult }
$raidTests = Start-Process -FilePath $EditorPath -ArgumentList "-batchmode -nographics -projectPath `"$raidRoot`" -runTests -testPlatform EditMode -testFilter UnderwaterFishing -testResults `"$raidResult`" -logFile `"$raidLog`"" -WindowStyle Hidden -PassThru
Write-Output "Unity test process: $($raidTests.Id)"
$raidTests.WaitForExit()
if ($raidTests.ExitCode -ne 0) { throw "Unity tests exited $($raidTests.ExitCode). Inspect $raidLog." }
[xml]$raidXml = Get-Content -LiteralPath $raidResult -Raw
$raidRun = $raidXml.'test-run'
if ($raidRun.result -ne 'Passed' -or [int]$raidRun.total -lt 1 -or [int]$raidRun.failed -gt 0) { throw 'Unity did not report a nonempty successful test run.' }
Write-Output "PASS: $($raidRun.passed)/$($raidRun.total) Unity EditMode tests."
