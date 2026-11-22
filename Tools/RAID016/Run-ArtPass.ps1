param([string]$Player,[string]$Output,[int]$Port=18401,[switch]$Timing,[switch]$Peer,[switch]$VerifySurfaces,[string]$MarksFile="$PSScriptRoot/camera-marks.json",[ValidateSet("d3d11","d3d12")][string]$Graphics="d3d11")
$ErrorActionPreference='Stop'
$playerPath=(Resolve-Path -LiteralPath $Player).Path
if(Test-Path -LiteralPath $Output){throw "Choose a fresh result directory"}
[void][IO.Directory]::CreateDirectory($Output)
$out=(Resolve-Path -LiteralPath $Output).Path
$marks=(Resolve-Path -LiteralPath $MarksFile).Path
$profile=[ordered]@{
    schemaVersion=1;biomass=24;chitin=7;scrap=13;electronics=3;lifetimeCatch=40
    reelLevel=0;finsLevel=0;tankLevel=0;hullLevel=0;winchLevel=0
    highestDepth=1;offering=0;hungerRemaining=160;goshaMet=$true;goshaTamed=$true
    tank=@();cargo=@();deliveredIds=@(1101,1102,1103);hoistPhase=0;hoistRemaining=0
}
$payload=$profile | ConvertTo-Json -Compress
$sha=[Security.Cryptography.SHA256]::Create()
try{$checksum=[Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload)))}finally{$sha.Dispose()}
$save=[ordered]@{format='RAID.TeamProgress';payload=$payload;sha256=$checksum}
[IO.File]::WriteAllText("$out/host.json",($save|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
function Play([string[]]$parameters){
    $args=@("-force-$Graphics",'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-raid-local-port',"$Port",'-job-worker-count','3')+$parameters
    $quoted=($args|ForEach-Object{'"'+$_+'"'}) -join ' '
    Start-Process -FilePath $playerPath -ArgumentList $quoted -WorkingDirectory (Split-Path $playerPath) -WindowStyle Hidden -PassThru
}
function Read($path){
    try {Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json} catch {$null}
}
$hostArgs=@('-raid-host','-raid-ui-probe',"$out/host",'-raid-save',"$out/host.json",'-logFile',"$out/host.log",'-raid-art-pass',$out,$marks,
    '-raid-profile','76119edf-50e7-4444-aef0-720c2761a479')
if($Timing){$hostArgs+='-raid-art-timing'}else{$hostArgs+='-batchmode'}
if($VerifySurfaces){$hostArgs+='--verify-nereid-surfaces'}
$hostProcess=$null
$peerProcess=$null
try{
    $hostProcess=Play $hostArgs
    if($Peer){
        $until=(Get-Date).AddSeconds(75)
        while(!(Test-Path -LiteralPath "$out/host/state.json")){
            if($hostProcess.HasExited -or (Get-Date) -gt $until){throw 'Host did not start'}
            Start-Sleep -Milliseconds 250
        }
        $peerProcess=Play @('-batchmode','-nographics','-raid-client','-raid-save',"$out/peer.json",'-logFile',"$out/peer.log",'-raid-profile','5a26c9d1-44d6-4bd6-9d77-4367d1850321')
    }
    $until=(Get-Date).AddSeconds(160)
    while($true){
        if(Test-Path -LiteralPath "$out/views.json"){
            $report=Read "$out/views.json"
            if($report.status -eq 'passed'){ "Matched camera capture complete ($out)";return }
            if($report.status -eq 'failed'){throw "$($report.phase) $($report.error)"}
        }
        if($hostProcess.HasExited -or (Get-Date) -gt $until){throw "Capture stopped. Read $out/host.log"}
        Start-Sleep -Milliseconds 250
    }
}
finally{
    foreach($p in $peerProcess,$hostProcess){
        if($p -and !$p.HasExited){$p.Kill();$p.WaitForExit()}
    }
}
