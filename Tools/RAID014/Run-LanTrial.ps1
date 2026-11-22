param(
    [Parameter(Mandatory)][ValidateSet('Fishing', 'Gosha', 'Water', 'Skills')][string] $Case,
    [Parameter(Mandatory)][string] $Player,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [int] $Port = 18149,
    [string] $ExistingTeamSave
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Player).Path
if (Test-Path -LiteralPath $OutputDirectory) { throw "Choose a new output directory: $OutputDirectory" }
foreach ($leaf in 'host', 'peer', 'results') {
    New-Item -ItemType Directory -Path (Join-Path $OutputDirectory $leaf) -Force | Out-Null
}
$run = (Resolve-Path -LiteralPath $OutputDirectory).Path
$hostDir = Join-Path $run 'host'
$peerDir = Join-Path $run 'peer'
$results = Join-Path $run 'results'
$save = Join-Path $hostDir 'team.json'
if ($ExistingTeamSave) { Copy-Item -LiteralPath $ExistingTeamSave -Destination $save }

function Start-Player([string[]] $Options) {
    $quoted = foreach ($option in $Options) { '"' + $option + '"' }
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -ArgumentList ($quoted -join ' ') -WindowStyle Hidden -PassThru
}
function Read-Snapshot($path) {
    try { Get-Content -Raw -Encoding UTF8 -LiteralPath $path -ErrorAction Stop | ConvertFrom-Json } catch { $null }
}
$common = @('-force-d3d11', '-screen-fullscreen', '0', '-raid-local-port', "$Port", '-job-worker-count', '3')
$hostArgs = $common + @('-raid-host', '-raid-ui-probe', $hostDir, '-raid-save', $save, '-logFile', "$hostDir/player.log",
    '-screen-width', '1280', '-screen-height', '720', '-raid-profile', '137923b3-3adf-4f24-8c1c-43dc48ee1198')
switch ($Case) {
    'Fishing' {
        $report = "$results/fishing.json"
        $hostArgs += @('-batchmode', '-raid-fishing-peer', $peerDir, '-raid-fishing-report', $report)
    }
    'Gosha' {
        $report = "$results/gosha-crew.json"
        $hostArgs += @('-batchmode', '-raid-gosha-trial', $results, '-raid-gosha-peer', $peerDir)
    }
    'Skills' {
        $report = "$results/skill-results.json"
        $hostArgs += @('-batchmode', '-raid-skills-trial', $results, '-raid-skills-peer', $peerDir)
    }
    'Water' {
        $report = "$results/environment.json"
        $hostArgs += @('-raid-water-trial', $results)
    }
}
$hostGame = $null
$peerGame = $null
try {
    $hostGame = Start-Player $hostArgs
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 300
        $s = Read-Snapshot "$hostDir/state.json"
        if ($hostGame.HasExited -or (Get-Date) -ge $deadline) { throw "Host did not spawn; see $hostDir/player.log" }
    } until ($s -and $s.connected -and $s.players -eq 1)
    $peerArgs = $common + @('-batchmode', '-raid-client', '-screen-width', '1920', '-screen-height', '1080',
        '-raid-ui-probe', $peerDir, '-raid-profile', '86a1d665-8b18-4a02-a076-a92bb5018316', '-raid-save', "$peerDir/team.json", '-logFile', "$peerDir/player.log")
    $peerGame = Start-Player $peerArgs
    $deadline = (Get-Date).AddMinutes(10)
    do {
        Start-Sleep -Milliseconds 500
        $r = Read-Snapshot $report
        if ($r -and $r.status -in @('passed', 'complete')) { Write-Output $report; return }
        if ($r -and $r.status -eq 'failed') { throw "$($r.phase) $($r.error) ($report)" }
        if ($hostGame.HasExited -or $peerGame.HasExited -or (Get-Date) -ge $deadline) {
            throw "Trial stopped in '$($r.phase)' ($run)"
        }
    } while ($true)
} finally {
    foreach ($process in $peerGame, $hostGame) {
        if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    }
}
