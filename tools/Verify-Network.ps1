param(
    [ValidateRange(2,5)][int]$Players = 5,
    [string]$BuildPath = 'Builds/Windows/UnderwaterFishing.exe'
)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot $BuildPath)).Path
$raidResults = Join-Path $raidRoot ".studioigor/test-results/network-$Players"
New-Item -ItemType Directory -Force -Path $raidResults | Out-Null
$raidProcesses = @()
try {
    for ($raidIndex = 0; $raidIndex -lt $Players; $raidIndex++) {
        $raidLog = Join-Path $raidResults "player-$raidIndex.log"
        $raidMode = if ($raidIndex -eq 0) { '--raid-host' } else { '--raid-join 127.0.0.1' }
        $raidArgs = "-batchmode -nographics -logFile `"$raidLog`" $raidMode --raid-exit-after 50"
        $raidProcesses += Start-Process -FilePath $raidBinary -ArgumentList $raidArgs -WindowStyle Hidden -PassThru
        if ($raidIndex -eq 0) { Start-Sleep -Seconds 3 }
    }
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(35)
    do {
        $raidReady = 0
        for ($raidIndex = 0; $raidIndex -lt $Players; $raidIndex++) {
            $raidLog = Join-Path $raidResults "player-$raidIndex.log"
            if ((Test-Path -LiteralPath $raidLog) -and (Select-String -LiteralPath $raidLog -SimpleMatch "RAID_SESSION players=$Players " -Quiet)) { $raidReady++ }
        }
        if ($raidReady -eq $Players) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if ($raidReady -ne $Players) { throw "Only $raidReady/$Players processes observed all $Players divers. Inspect $raidResults" }
    Write-Output "PASS: $Players separate Windows processes each observed $Players divers."
    $raidHostLog = Join-Path $raidResults 'player-0.log'
    $raidPreviousLines = @(Get-Content -LiteralPath $raidHostLog).Count
    Stop-Process -Id $raidProcesses[-1].Id
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(16)
    $raidAfterDisconnect = $false
    do {
        $raidNewLines = Get-Content -LiteralPath $raidHostLog | Select-Object -Skip $raidPreviousLines
        $raidAfterDisconnect = [bool]($raidNewLines | Select-String -SimpleMatch "RAID_SESSION players=$($Players-1) " -Quiet)
        if ($raidAfterDisconnect) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidAfterDisconnect) { throw 'Server did not remove the disconnected diver.' }
    $raidHostLoss = $null
    if ($Players -gt 2) {
        $raidClientLog = Join-Path $raidResults 'player-1.log'
        $raidBeforeHostLoss = @(Get-Content -LiteralPath $raidClientLog).Count
        Stop-Process -Id $raidProcesses[0].Id
        $raidHostLoss = $false
        $raidDeadline = [DateTime]::UtcNow.AddSeconds(16)
        do {
            $raidHostLoss = [bool](Get-Content -LiteralPath $raidClientLog | Select-Object -Skip $raidBeforeHostLoss | Select-String -SimpleMatch 'RAID_CLIENT state=Stopped' -Quiet)
            if ($raidHostLoss) { break }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $raidDeadline)
        if (!$raidHostLoss) { throw 'Client did not stop after losing the host.' }
        Write-Output 'PASS: client terminated its connection after host process loss.'
    }
    foreach ($raidFile in Get-ChildItem -LiteralPath $raidResults -Filter '*.log') {
        if (Select-String -LiteralPath $raidFile.FullName -Pattern 'Exception:|error CS|Spawned NetworkObject was null' -Quiet) { throw "Runtime error in $($raidFile.Name)" }
    }
    @{ players=$Players; observedByAll=$raidReady; disconnectCleanup=$raidAfterDisconnect; hostLoss=$raidHostLoss; steamVerified=$false; timestamp=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8
    Write-Output 'PASS: disconnect removed the diver; no runtime exceptions in process logs. Steam remote acceptance remains separate.'
}
finally {
    foreach ($raidProcess in $raidProcesses) {
        if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
    }
}
