param([switch]$Capture, [switch]$Timeout)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidName = if ($Timeout) { 'expedition-timeout' } else { 'expedition-consent' }
$raidResults = Join-Path $raidRoot ".studioigor/test-results/$raidName"
$raidCaptures = Join-Path $raidRoot ".studioigor/captures/$raidName"
New-Item -ItemType Directory -Force -Path $raidResults,$raidCaptures | Out-Null
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidFile in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidFile
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidMode = if ($Timeout) { '--raid-expedition-timeout-test' } else { '' }
$raidProcesses = @()
try {
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-fast-expeditions --raid-expedition-bot captain $raidMode --raid-exit-after 125" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidDisplay = '-batchmode -nographics'
    if ($Capture) { $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-expedition-capture `"$raidCaptures`"" }
    $raidArgs = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-expedition-bot diver $raidMode --raid-exit-after 125"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(115)
    $raidPassed = $false
    do {
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -SimpleMatch 'RAID_EXP_BOT_PASS' -Quiet) -and (Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_EXP_BOT_PASS' -Quiet)) { $raidPassed = $true; break }
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Pattern 'RAID_EXP_BOT_TIMEOUT|RAID_EXP_BOT_INVALID|Exception:' -Quiet)) { break }
        if ($raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidPassed) { throw "Expedition scenario did not complete. Inspect $raidResults" }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Pattern 'Exception:|RAID_EXP_BOT_TIMEOUT|RAID_EXP_BOT_INVALID' -Quiet) { throw "Runtime error in $raidLog" }
    }
    if ($Capture) { Start-Sleep -Seconds 1 }
    @{completedDays=1;returnConsentTest=(!$Timeout);emergencyTimeout=[bool]$Timeout;remoteClient=$true;steamVerified=$false;timestamp=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output "PASS: both owners observed a real expedition and one completed day; timeout=$Timeout."
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
