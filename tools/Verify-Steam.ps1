param([uint32]$AppId = 480)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot 'Builds/Windows/UnderwaterFishing.exe')).Path
$raidResult = Join-Path $raidRoot '.studioigor/test-results/steam'
$raidCapture = Join-Path $raidRoot '.studioigor/captures/steam'
New-Item -ItemType Directory -Force -Path $raidResult,$raidCapture | Out-Null
$raidLog = Join-Path $raidResult 'host.log'
$raidImage = Join-Path $raidCapture 'steam-host.png'
foreach ($raidOld in @($raidLog,$raidImage,(Join-Path $raidResult 'result.json'))) {
    if (Test-Path -LiteralPath $raidOld) { Remove-Item -LiteralPath $raidOld }
}
$raidProcess = Start-Process -FilePath $raidBinary -ArgumentList "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile `"$raidLog`" --steam-app-id $AppId --raid-steam-smoke --raid-steam-capture `"$raidImage`"" -PassThru
try {
    $raidDeadline = [DateTime]::UtcNow.AddSeconds(115)
    while (!$raidProcess.WaitForExit(500)) {
        if ([DateTime]::UtcNow -gt $raidDeadline) { throw 'Steam smoke check timed out. Inspect host.log.' }
    }
    if ($raidProcess.ExitCode -ne 0) { throw "Steam host check exited $($raidProcess.ExitCode). Inspect $raidLog" }
    if (!(Select-String -LiteralPath $raidLog -SimpleMatch 'RAID_STEAM_SMOKE_PASS' -Quiet)) { throw 'Steam check did not complete all three cycles.' }
    if (Select-String -LiteralPath $raidLog -Pattern 'Exception:|RAID_STEAM_SMOKE_FAIL' -Quiet) { throw 'Steam check reported a runtime error.' }
    @{localSteamHost=$true;rehostCycles=3;remoteAccountsVerified=$false;invitesVerified=$false;appId=$AppId;timestamp=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $raidResult 'result.json') -Encoding UTF8
    Write-Output 'PASS: real Steam lobby/host started and stopped three times; Steam transport confirmed. Separate-account connections and invitations still require external testing.'
}
finally {
    if (Get-Process -Id $raidProcess.Id -ErrorAction SilentlyContinue) { Stop-Process -Id $raidProcess.Id }
}
