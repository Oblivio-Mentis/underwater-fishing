param([switch]$Capture, [switch]$Fish, [switch]$Large)
$ErrorActionPreference = 'Stop'
if ($Large) { $Fish = $true }
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidName = if ($Large) { 'cargo-large' } elseif ($Fish) { 'cargo-fish' } else { 'cargo' }
$raidResults = Join-Path $raidRoot ".studioigor/test-results/$raidName"
$raidCaptures = Join-Path $raidRoot ".studioigor/captures/$raidName"
New-Item -ItemType Directory -Force -Path $raidResults,$raidCaptures | Out-Null
if ($Capture) {
    foreach ($raidImage in @('cargo-delivered.png','cargo-ascending.png','cargo-loaded.png')) {
        $raidOldImage = Join-Path $raidCaptures $raidImage
        if (Test-Path -LiteralPath $raidOldImage) { Remove-Item -LiteralPath $raidOldImage }
    }
}
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
foreach ($raidFile in @('host.log','client.log','result.json')) {
    $raidOld = Join-Path $raidResults $raidFile
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcesses = @()
try {
    $raidHostRole = if ($Fish) { '--raid-fishing-bot helper --raid-follow-cargo' } else { '--raid-cargo-bot hauler' }
    $raidClientRole = if ($Fish) { '--raid-fishing-bot hunter --raid-cargo-bot fish' } else { '--raid-cargo-bot hauler' }
    if ($Large) { $raidHostRole += ' --raid-fish-size large'; $raidClientRole += ' --raid-fish-size large' }
    $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList "-batchmode -nographics -logFile `"$raidHostLog`" --raid-host --raid-gym $raidHostRole --raid-exit-after 190" -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $raidDisplay = '-batchmode -nographics'
    if ($Capture) { $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-cargo-capture `"$raidCaptures`"" }
    $raidArgs = "$raidDisplay -logFile `"$raidClientLog`" --raid-join 127.0.0.1 --raid-gym $raidClientRole --raid-exit-after 190"
    if ($Capture) { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -PassThru }
    else { $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -WindowStyle Hidden -PassThru }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(175)
    $raidPassed = $false
    do {
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -SimpleMatch 'RAID_CARGO_BOT_PASS' -Quiet)) { $raidPassed = $true; break }
        if ((Test-Path -LiteralPath $raidClientLog) -and (Select-String -LiteralPath $raidClientLog -Pattern 'RAID_CARGO_BOT_TIMEOUT|RAID_CARGO_DUPLICATE|Exception:' -Quiet)) { break }
        if ($raidProcesses[0].HasExited -or $raidProcesses[1].HasExited) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidPassed) { throw "Cargo scenario did not complete. Inspect $raidResults" }
    $raidLoads = @(Select-String -LiteralPath $raidHostLog -Pattern '^RAID_CARGO_LOAD item=(\d+)')
    $raidIds = @($raidLoads | ForEach-Object { $_.Matches[0].Groups[1].Value })
    if (($raidIds | Select-Object -Unique).Count -ne $raidIds.Count) { throw 'Server accepted duplicate cargo identities.' }
    $raidCycles = @(Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_CARGO_UNLOADED').Count
    $raidRequired = if ($Fish) { 1 } else { 2 }
    if ($raidCycles -lt $raidRequired -or $raidIds.Count -lt $raidRequired) { throw 'Missing server-confirmed cargo cycles.' }
    if ($Fish -and !(Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_CATCH' -Quiet)) { throw 'Fish was not actually caught.' }
    if ($Large -and !(Select-String -LiteralPath $raidHostLog -SimpleMatch 'RAID_CARRY_ASSIST' -Quiet)) { throw 'Large fish delivery did not exercise cooperative carrying.' }
    if ($Capture) { Start-Sleep -Seconds 1 }
    foreach ($raidLog in @($raidHostLog,$raidClientLog)) {
        if (Select-String -LiteralPath $raidLog -Pattern 'Exception:|RAID_CARGO_DUPLICATE|RAID_CARGO_BOT_TIMEOUT' -Quiet) { throw "Runtime error in $raidLog" }
    }
    @{remoteClient=$true;cycles=$raidCycles;uniqueLoadedItems=$raidIds.Count;fishScenario=[bool]$Fish;largeFish=[bool]$Large;steamVerified=$false;timestamp=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output "PASS: remote owner delivered real cargo through $raidCycles winch cycle(s), with $($raidIds.Count) unique server-confirmed items."
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
