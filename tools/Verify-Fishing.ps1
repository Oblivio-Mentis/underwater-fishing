param(
    [ValidateRange(1,600)][int]$TimeoutSeconds = 115,
    [switch]$WaitForShutdown,
    [switch]$Capture,
    [switch]$Large
)
$ErrorActionPreference = 'Stop'
if ($Large -and !$PSBoundParameters.ContainsKey('TimeoutSeconds')) { $TimeoutSeconds = 195 }
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidSize = if ($Large) { 'large' } else { 'small' }
$raidNamespace = if ($Large) { 'fishing-large' } else { 'fishing' }
$raidResults = Join-Path $raidRoot ".studioigor/test-results/$raidNamespace"
New-Item -ItemType Directory -Force -Path $raidResults | Out-Null
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidName in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidName
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcesses = @()
$raidLifetime = if ($Large) { [Math]::Max(210, $TimeoutSeconds + 15) } elseif ($WaitForShutdown) { 55 } else { [Math]::Max(135, $TimeoutSeconds + 15) }
$raidDisplay = '-batchmode -nographics'
$raidCapturePrefix = if ($Large) { 'fishing-large-' } else { 'fishing-' }
if ($Capture) {
    $raidCapture = Join-Path $raidRoot ".studioigor/captures/native-$raidNamespace"
    New-Item -ItemType Directory -Force -Path $raidCapture | Out-Null
    foreach ($raidPhase in @('fight','caught')) {
        $raidOld = Join-Path $raidCapture "$raidCapturePrefix$raidPhase.png"
        if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
    }
    $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-capture-directory `"$raidCapture`""
}
try {
    $raidStarted = [DateTime]::UtcNow
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-fishing-bot helper --raid-fish-size $raidSize --raid-exit-after $raidLifetime" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidClientArguments = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-fishing-bot hunter --raid-fish-size $raidSize --raid-exit-after $raidLifetime"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidClientArguments -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidClientArguments -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $raidCaught = $false
    do {
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch 'RAID_BOT_CAUGHT' -Quiet)) { $raidCaught = $true; break }
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -SimpleMatch 'RAID_BOT_TIMEOUT' -Quiet)) { break }
        if ($raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidCaught) { throw "Client did not physically catch a $raidSize fish. Inspect $raidResults" }
    $raidCaughtLine = (Select-String -LiteralPath $raidClientLog -Encoding UTF8 -Pattern 'RAID_BOT_CAUGHT fish=\d+' | Select-Object -Last 1).Line
    $raidCaughtId = [regex]::Match($raidCaughtLine, 'RAID_BOT_CAUGHT fish=(\d+)')
    if (!$raidCaughtId.Success) { throw 'Client capture marker did not identify its fish.' }
    $raidFishId = [int]$raidCaughtId.Groups[1].Value
    $raidReportedSize = [regex]::Match($raidCaughtLine, '\bisLarge=(True|False)\b.*\bspecies=(.+)$')
    if ($Large -and (!$raidReportedSize.Success -or $raidReportedSize.Groups[1].Value -ne 'True')) {
        throw 'Large-fish scenario did not confirm IsLarge on the actual captured object.'
    }
    if (!$Large -and $raidReportedSize.Success -and $raidReportedSize.Groups[1].Value -ne 'False') {
        throw 'Small-fish scenario captured the wrong species size.'
    }
    $raidHookLine = Select-String -LiteralPath $raidHostLog -Encoding UTF8 -Pattern ("RAID_HOOK diver=\d+ fish=$raidFishId\s+species=(.+)$") | Select-Object -Last 1
    if (!$raidHookLine) { throw 'The captured fish ID has no matching server-confirmed hook.' }
    $raidSpecies = $raidHookLine.Matches[0].Groups[1].Value.Trim()
    if ($raidReportedSize.Success -and $raidReportedSize.Groups[2].Value.Trim() -ne $raidSpecies) {
        throw 'Client captured-object species differs from its server-confirmed hook.'
    }
    $raidServerCatch = Select-String -LiteralPath $raidHostLog -Encoding UTF8 -Pattern ("RAID_CATCH diver=\d+ fish=$raidFishId\b") -Quiet
    $raidAssisted = Select-String -LiteralPath $raidHostLog -Encoding UTF8 -Pattern ("RAID_ASSIST diver=\d+ fish=$raidFishId\s+helpers=[1-9]\d*\b") -Quiet
    if (!$raidServerCatch) { throw 'Client catch was not confirmed by the server for the same fish ID.' }
    if (!$raidAssisted) { throw 'Catch occurred but no server-confirmed teammate assistance was exercised for that fish.' }
    if ($Capture) {
        $raidExpectedCaptures = if ($Large) { @('fight','caught') } else { @('caught') }
        foreach ($raidPhase in $raidExpectedCaptures) {
            $raidCapturePath = Join-Path $raidCapture "$raidCapturePrefix$raidPhase.png"
            $raidCaptureDeadline = [DateTime]::UtcNow.AddSeconds(5)
            while (!(Test-Path -LiteralPath $raidCapturePath) -and [DateTime]::UtcNow -lt $raidCaptureDeadline) { Start-Sleep -Milliseconds 100 }
            if (!(Test-Path -LiteralPath $raidCapturePath)) { throw "Native $raidPhase game capture was not written." }
        }
    }
    if ($WaitForShutdown) {
        $raidShutdownDeadline = $raidStarted.AddSeconds($raidLifetime + 20)
        foreach ($raidProcess in $raidProcesses) {
            while (!$raidProcess.HasExited -and [DateTime]::UtcNow -lt $raidShutdownDeadline) { Start-Sleep -Milliseconds 500 }
            if (!$raidProcess.HasExited) { throw 'Expected graceful shutdown did not finish.' }
            if ($raidProcess.ExitCode -ne 0) { throw "Player exited with code $($raidProcess.ExitCode)" }
        }
    }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Encoding UTF8 -Pattern 'Exception:|error CS' -Quiet) { throw "Runtime exception in $raidLog" }
    }
    @{clientCaught=$raidCaught;serverConfirmed=$raidServerCatch;assistance=$raidAssisted;serverHookConfirmed=$true;fishId=$raidFishId;species=$raidSpecies;fishSize=$raidSize;isLarge=[bool]$Large;gracefulShutdown=[bool]$WaitForShutdown;steamVerified=$false;assemblySha256=(Get-FileHash -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing_Data/Managed/Assembly-CSharp.dll')).Hash;timestamp=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output "PASS: remote client swam through the hatch, hooked, fought and carried a $raidSize fish ($raidSpecies, ID $raidFishId); server confirmed assistance and catch. No runtime exceptions."
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
