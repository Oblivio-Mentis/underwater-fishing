param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Output,
    [ValidateRange(1,65535)][int]$Port = 17844,
    [switch]$Ball,
    [switch]$VerifyDrops
)
$ErrorActionPreference = 'Stop'
$playerPath = (Resolve-Path -LiteralPath $Player).Path
$runPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $runPath) { throw "Use a new output directory: $runPath" }
$hostDir = Join-Path $runPath 'host'
$peerDir = Join-Path $runPath 'peer'
New-Item -Path $hostDir,$peerDir -ItemType Directory | Out-Null

function Start-Peer($roleFlag, $dir, $width, $height, $extra) {
    $base = @('-batchmode','-force-d3d11','-raid-local-port',$Port,'-screen-fullscreen','0',
        '-screen-width',$width,'-screen-height',$height,'-job-worker-count','3',$roleFlag,
        '-raid-ui-probe',$dir,'-raid-save',(Join-Path $dir 'save.json'),'-logFile',(Join-Path $dir 'Player.log'))
    $quoted = @($base + $extra | ForEach-Object { '"{0}"' -f $_ })
    Start-Process -FilePath $playerPath -WorkingDirectory (Split-Path -Parent $playerPath) -WindowStyle Hidden -ArgumentList $quoted -PassThru
}

$resultPath = Join-Path $runPath 'result.json'
try {
    $trialArgs = @('-raid-fishing-peer',$peerDir,'-raid-fishing-report',$resultPath)
    if ($Ball) { $trialArgs += '-raid-ball-harpoon' }
    if ($VerifyDrops) { $trialArgs += '-raid-verify-drops' }
    $hostProcess = Start-Peer '-raid-host' $hostDir 1280 720 $trialArgs
    $until = (Get-Date).AddSeconds(70)
    do {
        Start-Sleep -Milliseconds 300
        if ($hostProcess.HasExited -or (Get-Date) -gt $until) { throw "Host did not start. See $hostDir/Player.log." }
    } until (Test-Path -LiteralPath (Join-Path $hostDir 'state.json'))

    $peerProcess = Start-Peer '-raid-client' $peerDir 1920 1080 @()
    $until = (Get-Date).AddMinutes(12)
    do {
        Start-Sleep -Milliseconds 500
        if ($hostProcess.HasExited -or $peerProcess.HasExited) { throw "A peer exited. See the player logs in $runPath." }
        if ((Get-Date) -gt $until) { throw "Fishing trial timed out. See $resultPath." }
        try { $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json }
        catch { continue } # The host writes a fresh snapshot at each checkpoint.
    } until ($report.status -eq 'passed' -or $report.status -eq 'failed')
    if ($report.status -ne 'passed') { throw "$($report.phase): $($report.error)" }
    Write-Output "Passed. Checks and camera captures are in $runPath."
}
finally {
    foreach ($peer in @($peerProcess,$hostProcess)) {
        if ($null -ne $peer -and !$peer.HasExited) { $peer.Kill(); $peer.WaitForExit(8000) | Out-Null }
    }
}
