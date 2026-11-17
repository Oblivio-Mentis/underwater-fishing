param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[1-9][0-9]{0,19}$')]
    [string]$LobbyId,
    [string]$BuildPath = '',
    [ValidateRange(120, 1800)][int]$TimeoutSeconds = 120,
    [ValidateRange(5, 60)][int]$StableSeconds = 10
)
$ErrorActionPreference = 'Stop'
$raidLobbyNumber = [uint64]0
if (![uint64]::TryParse($LobbyId, [ref]$raidLobbyNumber) -or $raidLobbyNumber -eq 0) { throw 'LobbyId must be a nonzero Steam lobby ID.' }
$raidLobby = $raidLobbyNumber.ToString([Globalization.CultureInfo]::InvariantCulture)
if ([string]::IsNullOrWhiteSpace($BuildPath)) {
    $raidParent = Split-Path -Parent $PSScriptRoot
    $raidCandidates = @(
        (Join-Path $PSScriptRoot 'UnderwaterFishing.exe'),
        (Join-Path $raidParent 'UnderwaterFishing.exe'),
        (Join-Path $raidParent 'Builds/Windows/UnderwaterFishing.exe')
    )
    $BuildPath = $raidCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$BuildPath) { throw 'UnderwaterFishing.exe was not found. Specify -BuildPath with the portable executable path.' }
}
$raidBinary = (Resolve-Path -LiteralPath $BuildPath).Path
$raidDirectory = Split-Path -Parent $raidBinary
$raidRun = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$raidResults = Join-Path $raidDirectory "SteamRemoteResults/$raidRun"
New-Item -ItemType Directory -Force -Path $raidResults | Out-Null
$raidLog = Join-Path $raidResults 'client.log'
$raidProcess = $null
$raidStarted = [DateTime]::UtcNow
$raidResult = [ordered]@{
    passed = $false
    appId = 480
    requestedLobby = $raidLobby
    transport = 'FishySteamworks'
    nativeSteamConnected = $false
    distinctSteamAccounts = $false
    twoPlayersObserved = $false
    automaticScope = 'Steam client connection and two replicated players only'
    manualGameplayChecked = $false
    manualDiscoveryChecked = $false
    manualOverlayInviteChecked = $false
    manualHostLossChecked = $false
    startedUtc = $raidStarted.ToString('o')
    clientLog = $raidLog
}
try {
    $raidAssembly = Join-Path $raidDirectory 'UnderwaterFishing_Data/Managed/Assembly-CSharp.dll'
    if (!(Test-Path -LiteralPath $raidAssembly -PathType Leaf)) { throw 'Managed development build is missing Assembly-CSharp.dll.' }
    $raidResult.assemblySha256 = (Get-FileHash -LiteralPath $raidAssembly -Algorithm SHA256).Hash
    $raidResult.executableSha256 = (Get-FileHash -LiteralPath $raidBinary -Algorithm SHA256).Hash
    # One visible client for the tester. The partner runs the host on another device/account.
    # There are deliberately no LAN, local-host, smoke-test, or automation-bot flags.
    $raidArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile `"$raidLog`" --steam-app-id 480 +connect_lobby $raidLobby"
    $raidProcess = Start-Process -FilePath $raidBinary -WorkingDirectory $raidDirectory -ArgumentList $raidArguments -WindowStyle Normal -PassThru
    Write-Output 'Opened one Steam client. Keep the partner host online; this check requires another signed-in Steam account.'
    Write-Output "Results: $raidResults"
    $raidDeadline = $raidStarted.AddSeconds($TimeoutSeconds)
    $raidStableSince = $null
    $raidConnectionIndex = -1
    $raidTelemetryIndex = -1
    $raidTelemetrySeen = [DateTime]::MinValue
    $raidNextProgress = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $raidDeadline) {
        if ($raidProcess.HasExited) { throw "The client exited before acceptance (exit $($raidProcess.ExitCode))." }
        $raidText = if (Test-Path -LiteralPath $raidLog) { [string](Get-Content -LiteralPath $raidLog -Raw -Encoding UTF8) } else { '' }
        if ($raidText -match 'Exception:|error CS\d+') { throw 'The client logged a runtime exception or compile error.' }
        $raidConnections = [regex]::Matches($raidText, '(?m)^RAID_STEAM_CONNECTED lobby=(\d+) local=(\d+) host=(\d+) transport=FishySteamworks\s*$')
        if ($raidConnections.Count -gt 0) {
            $raidConnected = $raidConnections[$raidConnections.Count - 1]
            if ($raidConnected.Groups[1].Value -ne $raidLobby) { throw 'The client connected to a different Steam lobby.' }
            $raidLocalId = $raidConnected.Groups[2].Value
            $raidHostId = $raidConnected.Groups[3].Value
            if ($raidLocalId -eq '0' -or $raidHostId -eq '0' -or $raidLocalId -eq $raidHostId) {
                throw 'The connection does not prove two distinct signed-in Steam accounts.'
            }
            $raidResult.nativeSteamConnected = $true
            $raidResult.distinctSteamAccounts = $true
            # IDs are stored as strings to preserve all 64 bits. They are account identifiers, not authentication tokens.
            $raidResult.localSteamId = $raidLocalId
            $raidResult.hostSteamId = $raidHostId
            $raidTail = $raidText.Substring($raidConnected.Index + $raidConnected.Length)
            if ($raidTail -match '(?m)^RAID_CLIENT state=(Stopped|Stopping)\b') { throw 'The Steam client disconnected before acceptance completed.' }
            $raidSessions = [regex]::Matches($raidTail, '(?m)^RAID_SESSION players=(\d+) server=(True|False) client=(True|False)\s*$')
            $raidHasCrew = $false
            if ($raidSessions.Count -gt 0) {
                $raidSession = $raidSessions[$raidSessions.Count - 1]
                $raidPlayers = [int]$raidSession.Groups[1].Value
                if ($raidSession.Groups[2].Value -ne 'False') { throw 'The verifier unexpectedly started a local server.' }
                $raidHasCrew = $raidPlayers -ge 2 -and $raidPlayers -le 5 -and $raidSession.Groups[3].Value -eq 'True'
                $raidSampleIndex = $raidConnected.Index + $raidConnected.Length + $raidSession.Index
                if ($raidSampleIndex -ne $raidTelemetryIndex) { $raidTelemetryIndex = $raidSampleIndex; $raidTelemetrySeen = [DateTime]::UtcNow }
                if (([DateTime]::UtcNow - $raidTelemetrySeen).TotalSeconds -gt 5) { $raidHasCrew = $false }
                $raidResult.lastObservedPlayers = $raidPlayers
            }
            if ($raidConnectionIndex -ne $raidConnected.Index) { $raidStableSince = $null; $raidConnectionIndex = $raidConnected.Index }
            if ($raidHasCrew) {
                $raidResult.twoPlayersObserved = $true
                if ($null -eq $raidStableSince) { $raidStableSince = [DateTime]::UtcNow }
                if (([DateTime]::UtcNow - $raidStableSince).TotalSeconds -ge $StableSeconds) {
                    $raidResult.passed = $true
                    $raidResult.stableSeconds = $StableSeconds
                    break
                }
            }
            else { $raidStableSince = $null }
        }
        if ([DateTime]::UtcNow -ge $raidNextProgress) {
            $raidNextProgress = [DateTime]::UtcNow.AddSeconds(20)
            Write-Output "Waiting: nativeSteam=$($raidResult.nativeSteamConnected), distinctAccounts=$($raidResult.distinctSteamAccounts), twoPlayers=$($raidResult.twoPlayersObserved)."
        }
        Start-Sleep -Milliseconds 500
    }
    if (!$raidResult.passed) { throw "No stable, distinct-account Steam session with two players was confirmed within $TimeoutSeconds seconds." }
    Write-Output 'PASS: native Steam connection, distinct host/client accounts, and at least two replicated players confirmed. Gameplay and invitations still require the manual checklist.'
}
catch {
    $raidResult.failure = $_.Exception.Message
    Write-Output "FAIL: $($raidResult.failure)"
    throw
}
finally {
    $raidResult.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $raidResult.elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $raidStarted).TotalSeconds, 1)
    try { $raidResult | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $raidResults 'result.json') -Encoding UTF8 }
    finally {
        if ($null -ne $raidProcess -and !$raidProcess.HasExited) {
            # Close only this invocation's player; leave Steam, the partner host, and any other game processes alone.
            $null = $raidProcess.CloseMainWindow()
            if (!$raidProcess.WaitForExit(5000)) { Stop-Process -InputObject $raidProcess -ErrorAction SilentlyContinue }
        }
    }
}
