param(
    [switch]$Failure,
    [switch]$Capture,
    [ValidateRange(60, 900)][int]$TimeoutSeconds = 900,
    [ValidateRange(180, 3600)][int]$ExpeditionSeconds = 180,
    [string]$BuildPath = 'Builds/Windows/UnderwaterFishing.exe'
)
$ErrorActionPreference = 'Stop'
$raidRoot = Split-Path -Parent $PSScriptRoot
$raidBinary = (Resolve-Path -LiteralPath (Join-Path $raidRoot $BuildPath)).Path
$raidScenario = if ($Failure) { 'core-failure' } else { 'core-success' }
$raidRun = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$raidResults = Join-Path $raidRoot ".studioigor/test-results/$raidScenario/$raidRun"
$raidCaptures = Join-Path $raidRoot ".studioigor/captures/$raidScenario/$raidRun"
New-Item -ItemType Directory -Force -Path $raidResults | Out-Null
if ($Capture) { New-Item -ItemType Directory -Force -Path $raidCaptures | Out-Null }
if ($Capture) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RaidCorePreviewWindow {
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
}
$raidHostLog = Join-Path $raidResults 'host.log'
$raidClientLog = Join-Path $raidResults 'client.log'
$raidProcesses = @()
$raidLifetime = [Math]::Min(900, $TimeoutSeconds + 15)
$raidStarted = [DateTime]::UtcNow
$raidResult = [ordered]@{
    passed = $false
    scenario = $raidScenario
    transport = 'LAN'
    steamVerified = $false
    remoteAccountsVerified = $false
    separateProcesses = 2
    expeditionSeconds = $ExpeditionSeconds
    startedUtc = $raidStarted.ToString('o')
    resultsDirectory = $raidResults
    capturesDirectory = if ($Capture) { $raidCaptures } else { $null }
}
function Read-RaidLog([string]$Path) {
    if (Test-Path -LiteralPath $Path) { return [string](Get-Content -LiteralPath $Path -Raw -Encoding UTF8) }
    return ''
}
function Require-RaidMatch([string]$Text, [string]$Pattern, [string]$Reason) {
    if ($Text -notmatch $Pattern) { throw $Reason }
}
try {
    Write-Output "Running $raidScenario through two LAN owner processes. Results: $raidResults"
    for ($raidIndex = 0; $raidIndex -lt 2; $raidIndex++) {
        $raidRole = if ($raidIndex -eq 0) { 'captain' } else { 'hunter' }
        $raidConnection = if ($raidIndex -eq 0) { '--raid-host' } else { '--raid-join 127.0.0.1' }
        $raidLog = if ($raidIndex -eq 0) { $raidHostLog } else { $raidClientLog }
        $raidDisplay = '-batchmode -nographics'
        if ($Capture) {
            $raidDisplay = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --raid-core-capture `"$raidCaptures`""
        }
        $raidFailureFlag = if ($Failure) { '--raid-core-failure' } else { '' }
        $raidArguments = "$raidDisplay -logFile `"$raidLog`" $raidConnection --raid-core-bot $raidRole $raidFailureFlag --raid-expedition-seconds $ExpeditionSeconds --raid-exit-after $raidLifetime"
        $raidWindowStyle = if ($Capture) { 'Normal' } else { 'Hidden' }
        $raidPlayer = Start-Process -FilePath $raidBinary -ArgumentList $raidArguments -WindowStyle $raidWindowStyle -PassThru
        $raidProcesses += $raidPlayer
        if ($Capture) {
            $raidWindowDeadline = [DateTime]::UtcNow.AddSeconds(10)
            do { $raidPlayer.Refresh(); if ($raidPlayer.MainWindowHandle -ne [IntPtr]::Zero) { break }; Start-Sleep -Milliseconds 100 } while (!$raidPlayer.HasExited -and [DateTime]::UtcNow -lt $raidWindowDeadline)
            if ($raidPlayer.MainWindowHandle -ne [IntPtr]::Zero) {
                $raidWidth = [Math]::Min(1280, [RaidCorePreviewWindow]::GetSystemMetrics(0) - 60)
                $raidHeight = [Math]::Min(720, [RaidCorePreviewWindow]::GetSystemMetrics(1) - 60)
                $raidX = if ($raidIndex -eq 0) { 0 } else { [Math]::Max(60, [RaidCorePreviewWindow]::GetSystemMetrics(0) - $raidWidth) }
                $raidY = if ($raidIndex -eq 0) { 0 } else { [Math]::Max(60, [RaidCorePreviewWindow]::GetSystemMetrics(1) - $raidHeight) }
                [void][RaidCorePreviewWindow]::MoveWindow($raidPlayer.MainWindowHandle, $raidX, $raidY, $raidWidth, $raidHeight, $true)
            }
        }
        if ($raidIndex -eq 0) { Start-Sleep -Seconds 3 }
    }
    $raidDeadline = $raidStarted.AddSeconds($TimeoutSeconds)
    $raidNextProgress = [DateTime]::UtcNow
    $raidCompleted = $false
    do {
        $raidHostText = Read-RaidLog $raidHostLog
        $raidClientText = Read-RaidLog $raidClientLog
        $raidErrors = 'RAID_CORE_LOOP_FAIL|RAID_CARGO_BOT_TIMEOUT|RAID_CARGO_DUPLICATE|RAID_BOT_TIMEOUT|Exception:|error CS\d+'
        if ($raidHostText -match $raidErrors -or $raidClientText -match $raidErrors) { throw 'A bot deadline, failed invariant, or runtime exception occurred.' }
        $raidMode = if ($Failure) { 'failure' } else { 'success' }
        $raidHostPassed = $raidHostText -match "RAID_CORE_LOOP_PASS role=Captain mode=$raidMode days=3\b"
        $raidClientPassed = $raidClientText -match "RAID_CORE_LOOP_PASS role=Hunter mode=$raidMode days=3\b"
        if ($raidHostPassed -and $raidClientPassed) { $raidCompleted = $true; break }
        foreach ($raidProcess in $raidProcesses) {
            if ($raidProcess.HasExited) { throw "Player $($raidProcess.Id) exited before both owners confirmed the complete loop (exit $($raidProcess.ExitCode))." }
        }
        if ([DateTime]::UtcNow -ge $raidNextProgress) {
            $raidNextProgress = [DateTime]::UtcNow.AddSeconds(20)
            $raidLatest = [regex]::Matches($raidHostText, '(?m)^RAID_CORE_(?:STAGE|DIAG|PHASE).*$')
            $raidProgress = if ($raidLatest.Count -gt 0) { $raidLatest[$raidLatest.Count - 1].Value } else { 'Waiting for connected owners and authorities.' }
            Write-Output ("[{0:0}s] {1}" -f ([DateTime]::UtcNow - $raidStarted).TotalSeconds, $raidProgress)
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $raidDeadline)
    if (!$raidCompleted) { throw "Complete three-day scenario exceeded $TimeoutSeconds seconds." }

    # Require server events as well as both client-side assertions. A lone PASS marker is insufficient.
    $raidDiveCount = [regex]::Matches($raidHostText, '(?m)^RAID_EXPEDITION phase=Expedition\b').Count
    $raidDebriefCount = [regex]::Matches($raidHostText, '(?m)^RAID_EXPEDITION phase=Debrief\b').Count
    if ($raidDiveCount -ne 3 -or $raidDebriefCount -ne 3) { throw "Expected exactly three server expeditions/debriefs, observed $raidDiveCount/$raidDebriefCount." }
    if ($raidHostText -match 'RAID_EXPEDITION .*emergency=True') { throw 'An emergency return invalidated the deliberate return scenario.' }
    Require-RaidMatch $raidHostText 'RAID_EXPEDITION phase=Offering\b' 'Missing server offering phase.'
    $raidLoadMatches = [regex]::Matches($raidHostText, '(?m)^RAID_CARGO_LOAD item=(\d+)\b')
    $raidLoadedIds = @($raidLoadMatches | ForEach-Object { $_.Groups[1].Value })
    $raidUniqueIds = @($raidLoadedIds | Select-Object -Unique)
    if ($raidUniqueIds.Count -ne $raidLoadedIds.Count) { throw 'Server accepted a duplicate cargo source identity.' }
    $raidProcessMatches = [regex]::Matches($raidHostText, '(?m)^RAID_CORE_PROCESS_CONFIRMED id=(\d+)\b')
    $raidProcessedIds = @($raidProcessMatches | ForEach-Object { $_.Groups[1].Value })
    if (@($raidProcessedIds | Select-Object -Unique).Count -ne $raidProcessedIds.Count) { throw 'Captain confirmed the same processed item twice.' }
    foreach ($raidId in $raidProcessedIds) { if ($raidLoadedIds -notcontains $raidId) { throw "Processed item $raidId has no matching server cargo delivery." } }

    if ($Failure) {
        Require-RaidMatch $raidHostText 'RAID_OFFERING success=False\b' 'Missing server failed offering.'
        Require-RaidMatch $raidHostText 'RAID_EXPEDITION phase=Failure\b' 'Missing server Failure phase.'
        Require-RaidMatch $raidHostText 'RAID_CORE_RESET_REQUEST\b' 'The owner did not issue the real reset RPC.'
        Require-RaidMatch $raidClientText 'RAID_CORE_FAILURE_CONFIRMED\b' 'Remote owner did not observe Failure before reset.'
        if ($raidLoadedIds.Count -ne 0) { throw 'Failure scenario unexpectedly loaded cargo.' }
        if ($raidHostText -match 'RAID_OFFERING success=True\b') { throw 'Failure scenario unexpectedly fulfilled an offering.' }
    }
    else {
        if ($raidLoadedIds.Count -lt 3 -or $raidProcessedIds.Count -lt 2) { throw 'Missing real fish/technical delivery and processing evidence.' }
        if ([regex]::Matches($raidHostText, '(?m)^RAID_CATCH diver=\d+ fish=\d+').Count -lt 2) { throw 'Two real fish captures were not confirmed by the server.' }
        Require-RaidMatch $raidHostText 'RAID_ASSIST .*helpers=[1-9]' 'No server-confirmed cooperative fishing assistance.'
        Require-RaidMatch $raidHostText 'RAID_CARRY_ASSIST .*helpers=[1-9]' 'No server-confirmed cooperative carrying.'
        Require-RaidMatch $raidHostText 'RAID_CORE_UPGRADE_CONFIRMED level=1\b' 'Oxygen purchase and material spending were not confirmed.'
        Require-RaidMatch $raidHostText 'RAID_CORE_SONAR_CRAFT_CONFIRMED charges=1\b' 'Material-funded personal sonar was not confirmed.'
        Require-RaidMatch $raidHostText 'RAID_CORE_SONAR_CONFIRMED contacts=[1-9]\d* charges=0\b' 'Sonar did not reveal server-returned contacts and consume its charge.'
        Require-RaidMatch $raidHostText 'RAID_GEAR_APPLIED\b' 'Missing server effective-gear application event.'
        Require-RaidMatch $raidClientText 'RAID_CORE_GEAR_CONFIRMED oxygen=\d+\.\d+ baseline=\d+\.\d+ level=1\b' 'Remote owner did not observe actual increased oxygen.'
        Require-RaidMatch $raidHostText 'RAID_CORE_OFFERING_SUBMIT\b' 'Captain did not submit the complete reserved offering through the owner RPC.'
        Require-RaidMatch $raidHostText 'RAID_OFFERING success=True\b' 'Missing server accepted offering.'
        Require-RaidMatch $raidHostText 'RAID_EXPEDITION phase=Success\b' 'Missing timed Success phase.'
        Require-RaidMatch $raidClientText 'RAID_CORE_SUCCESS_CONFIRMED\b' 'Remote owner did not observe Success before Base.'
    }
    if ($Capture) {
        $raidCaptureDeadline = [DateTime]::UtcNow.AddSeconds(5)
        do {
            $raidCapturesReady = (Test-Path -LiteralPath (Join-Path $raidCaptures 'captain-complete.png')) -and (Test-Path -LiteralPath (Join-Path $raidCaptures 'hunter-complete.png'))
            if ($raidCapturesReady) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $raidCaptureDeadline)
        if (!$raidCapturesReady) { throw 'Both final native game screenshots were not written.' }
    }
    $raidResult.passed = $true
    $raidResult.serverExpeditions = $raidDiveCount
    $raidResult.serverDebriefs = $raidDebriefCount
    $raidResult.loadedCargoIds = $raidLoadedIds
    $raidResult.processedCargoIds = $raidProcessedIds
    $raidResult.bothOwnersConfirmed = $true
    $raidAssembly = Join-Path (Split-Path -Parent $raidBinary) 'UnderwaterFishing_Data/Managed/Assembly-CSharp.dll'
    if (Test-Path -LiteralPath $raidAssembly) { $raidResult.assemblySha256 = (Get-FileHash -LiteralPath $raidAssembly -Algorithm SHA256).Hash }
    Write-Output "PASS: $raidScenario completed three server-confirmed days in two LAN processes. Steam and separate Steam accounts were not tested."
}
catch {
    $raidResult.failure = $_.Exception.Message
    Write-Output "FAIL: $($raidResult.failure) Logs: $raidResults"
    throw
}
finally {
    $raidResult.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $raidResult.elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $raidStarted).TotalSeconds, 1)
    try { $raidResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8 }
    finally {
        foreach ($raidProcess in $raidProcesses) {
            if (!$raidProcess.HasExited) { Stop-Process -InputObject $raidProcess -ErrorAction SilentlyContinue }
        }
    }
}
