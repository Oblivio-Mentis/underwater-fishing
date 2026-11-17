param([switch]$Capture)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidResults = Join-Path $raidRoot '.studioigor/test-results/spectator'
$raidCaptures = Join-Path $raidRoot '.studioigor/captures/spectator'
New-Item -ItemType Directory -Force -Path $raidResults,$raidCaptures | Out-Null
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidFile in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidFile
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
foreach ($raidFile in @('spectator-dead.png','spectator-restored.png')) {
    $raidOld = Join-Path $raidCaptures $raidFile
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcesses = @()
try {
    $raidCommon = '--raid-fast-diving --raid-expedition-seconds 180 --raid-exit-after 165'
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-spectator-bot captain $raidCommon" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidDisplay = '-batchmode -nographics'
    if ($Capture) { $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-spectator-capture `"$raidCaptures`"" }
    $raidArguments = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-spectator-bot victim $raidCommon"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArguments -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArguments -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(150)
    $raidPassed = $false
    do {
        $raidHostPassed = (Test-Path -LiteralPath $raidHostLog) -and (Select-String -LiteralPath $raidHostLog -Encoding UTF8 -SimpleMatch 'RAID_SPECTATOR_PASS role=captain completed=1 phase=Base alive=True emergency=False' -Quiet)
        $raidClientPassed = (Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch 'RAID_SPECTATOR_PASS role=victim completed=1 phase=Base alive=True emergency=False' -Quiet)
        if ($raidHostPassed -and $raidClientPassed) { $raidPassed = $true; break }
        $raidInvalid = $false
        foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
            if ((Test-Path -LiteralPath $raidLog) -and (Select-String -LiteralPath $raidLog -Encoding UTF8 -Pattern 'RAID_SPECTATOR_INVALID|RAID_SPECTATOR_TIMEOUT|Exception:' -Quiet)) { $raidInvalid = $true; break }
        }
        if ($raidInvalid -or $raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidPassed) { throw "Real death/spectator/return scenario did not complete. Inspect $raidResults" }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Encoding UTF8 -Pattern 'Exception:|error CS|RAID_SPECTATOR_INVALID|RAID_SPECTATOR_TIMEOUT' -Quiet) { throw "Runtime failure in $raidLog" }
    }
    foreach ($raidMarker in @('RAID_SPECTATOR_CAPTAIN_SAW_INCAPACITATED','RAID_SPECTATOR_CAPTAIN_SAW_DEAD','RAID_SPECTATOR_RETURN_REQUESTED victimDead=True')) {
        if (!(Select-String -LiteralPath $raidHostLog -Encoding UTF8 -SimpleMatch $raidMarker -Quiet)) { throw "Missing authoritative observation: $raidMarker" }
    }
    foreach ($raidMarker in @('RAID_SPECTATOR_INCAPACITATED','RAID_SPECTATOR_DEAD_CONFIRMED cameraEnabled=True bodyStationary=True outside=True','RAID_SPECTATOR_REVIVED')) {
        if (!(Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch $raidMarker -Quiet)) { throw "Missing remote-owner observation: $raidMarker" }
    }
    foreach ($raidPhase in @('Return','Debrief','Base')) {
        if (!(Select-String -LiteralPath $raidHostLog -Encoding UTF8 -Pattern ("RAID_EXPEDITION phase=$raidPhase\b.*emergency=False") -Quiet)) { throw "Missing normal server phase: $raidPhase" }
    }
    if ($Capture) {
        foreach ($raidPhase in @('dead','restored')) {
            $raidImage = Join-Path $raidCaptures "spectator-$raidPhase.png"
            $raidCaptureDeadline = [DateTime]::UtcNow.AddSeconds(5)
            while (!(Test-Path -LiteralPath $raidImage) -and [DateTime]::UtcNow -lt $raidCaptureDeadline) { Start-Sleep -Milliseconds 100 }
            if (!(Test-Path -LiteralPath $raidImage) -or (Get-Item -LiteralPath $raidImage).Length -le 0) { throw "Native $raidPhase screenshot missing." }
        }
    }
    $raidDeadEvidence = (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch 'RAID_SPECTATOR_DEAD_CONFIRMED' | Select-Object -Last 1).Line
    $raidReviveEvidence = (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch 'RAID_SPECTATOR_REVIVED' | Select-Object -Last 1).Line
    @{completedDays=1;normalMinimumTwo=$true;oxygenDeath=$true;deadBodyOutside=$true;spectatorCamera=$true;revivedAtDebriefOrBase=$true;deadCrewDidNotBlockReturn=$true;hostObserved=$raidHostPassed;remoteOwnerObserved=$raidClientPassed;nativeCapture=[bool]$Capture;deadEvidence=$raidDeadEvidence;revivalEvidence=$raidReviveEvidence;steamVerified=$false;assemblySha256=(Get-FileHash -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing_Data/Managed/Assembly-CSharp.dll')).Hash;timestamp=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output 'PASS: real oxygen incapacity and death; remote camera followed surviving captain while body stayed outside; dead crew did not block normal return; revival and own camera restored at debrief/base. Both owners observed one completed day.'
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
