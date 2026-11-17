param([int]$TimeoutSeconds = 115, [switch]$WaitForShutdown, [switch]$Capture)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidResults = Join-Path $raidRoot '.studioigor/test-results/fishing'
New-Item -ItemType Directory -Force -Path $raidResults | Out-Null
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidName in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidName
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcesses = @()
$raidLifetime = if ($WaitForShutdown) { 55 } else { 135 }
$raidDisplay = '-batchmode -nographics'
if ($Capture) {
    $raidCapture = Join-Path $raidRoot '.studioigor/captures/native-fishing'
    New-Item -ItemType Directory -Force -Path $raidCapture | Out-Null
    foreach ($raidName in @('fishing-fight.png','fishing-caught.png')) {
        $raidOld = Join-Path $raidCapture $raidName
        if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
    }
    $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-capture-directory `"$raidCapture`""
}
try {
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-fishing-bot helper --raid-exit-after $raidLifetime" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidClientArguments = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-fishing-bot hunter --raid-exit-after $raidLifetime"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidClientArguments -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidClientArguments -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $raidCaught = $false
    do {
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -SimpleMatch 'RAID_BOT_CAUGHT' -Quiet)) { $raidCaught = $true; break }
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -SimpleMatch 'RAID_BOT_TIMEOUT' -Quiet)) { break }
        if ($raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidCaught) { throw "Client did not physically catch a fish. Inspect $raidResults" }
    $raidServerCatch = Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_CATCH' -Quiet
    $raidAssisted = Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_ASSIST' -Quiet
    if (!$raidServerCatch) { throw 'Client catch was not confirmed by the server.' }
    if (!$raidAssisted) { throw 'Catch occurred but no server-confirmed teammate assistance was exercised.' }
    if ($Capture) {
        $raidCaptureDeadline = [DateTime]::UtcNow.AddSeconds(5)
        while (!(Test-Path -LiteralPath (Join-Path $raidCapture 'fishing-caught.png')) -and [DateTime]::UtcNow -lt $raidCaptureDeadline) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path -LiteralPath (Join-Path $raidCapture 'fishing-caught.png'))) { throw 'Native game capture was not written.' }
    }
    if ($WaitForShutdown) {
        foreach ($raidProcess in $raidProcesses) {
            if (!$raidProcess.WaitForExit(45000)) { throw 'Expected graceful shutdown did not finish.' }
            if ($raidProcess.ExitCode -ne 0) { throw "Player exited with code $($raidProcess.ExitCode)" }
        }
    }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Pattern 'Exception:|error CS' -Quiet) { throw "Runtime exception in $raidLog" }
    }
    @{clientCaught=$raidCaught;serverConfirmed=$raidServerCatch;assistance=$raidAssisted;gracefulShutdown=[bool]$WaitForShutdown;steamVerified=$false;assemblySha256=(Get-FileHash -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing_Data/Managed/Assembly-CSharp.dll')).Hash;timestamp=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output 'PASS: remote client swam through the hatch, hooked, fought and carried a fish; server confirmed assistance and catch. No runtime exceptions.'
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
