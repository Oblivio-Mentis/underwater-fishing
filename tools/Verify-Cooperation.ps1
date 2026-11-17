param([switch]$Capture)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidResults = Join-Path $raidRoot '.studioigor/test-results/cooperation'
$raidCaptures = Join-Path $raidRoot '.studioigor/captures/cooperation'
New-Item -ItemType Directory -Force -Path $raidResults,$raidCaptures | Out-Null
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidName in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidName
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcesses = @()
try {
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-cooperation-bot helper --raid-fast-diving --raid-exit-after 105" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidDisplay = '-batchmode -nographics'
    if ($Capture) { $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-cooperation-capture `"$raidCaptures`"" }
    $raidArgs = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-cooperation-bot victim --raid-exit-after 105"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(100)
    $raidPassed = $false
    do {
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -SimpleMatch 'RAID_COOP_BOT_PASS' -Quiet)) { $raidPassed = $true; break }
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Pattern 'RAID_COOP_BOT_TIMEOUT|Exception:' -Quiet)) { break }
        if ($raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidPassed) { throw "Cooperation scenario did not complete. Inspect $raidResults" }
    foreach ($raidEvent in @('RAID_TRAP_PIN','RAID_TRAP_CLEAR','RAID_DIVER_RESCUE')) {
        if (!(Select-String -LiteralPath $raidHostLog -SimpleMatch $raidEvent -Quiet)) { throw "Missing server event $raidEvent" }
    }
    if ($Capture) { Start-Sleep -Seconds 1 }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Pattern 'Exception:|RAID_COOP_BOT_TIMEOUT' -Quiet) { throw "Runtime error in $raidLog" }
    }
    @{trapRescued=$true;oxygenRescued=$true;remoteOwner=$true;steamVerified=$false;timestamp=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output 'PASS: remote diver entered a real trap, teammate held rescue, oxygen depleted naturally, teammate restored consciousness with an emergency charge.'
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
