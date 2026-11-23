param(
    [Parameter(Mandatory)][string]$Frames,
    [Parameter(Mandatory)][string]$Output,
    [int]$Fps = 20,
    [string]$Ffmpeg = "$env:LOCALAPPDATA\ms-playwright\ffmpeg-1013\ffmpeg-win64.exe"
)
# Stitches frame-%05d.png from FishingRecorder into an H.264 mp4 (plus a 480p GIF preview next to it).
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Ffmpeg)) { throw "ffmpeg not found at $Ffmpeg" }
$in = Join-Path (Resolve-Path -LiteralPath $Frames).Path 'frame-%05d.png'
& $Ffmpeg -y -loglevel error -framerate $Fps -i $in -c:v libx264 -pix_fmt yuv420p -crf 23 $Output
$gif = [IO.Path]::ChangeExtension($Output, '.gif')
& $Ffmpeg -y -loglevel error -framerate $Fps -i $in -vf "fps=10,scale=640:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=96[p];[s1][p]paletteuse=dither=bayer" $gif
Write-Output "Wrote $Output and $gif"
