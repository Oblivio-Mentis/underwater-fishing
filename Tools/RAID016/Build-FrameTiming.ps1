param([string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor')
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath "$PSScriptRoot/../..").Path
$native=Join-Path $root 'Builds/ArtTiming'
$plugins=Join-Path $root 'Assets/Plugins/ArtTimingQA'
[void][IO.Directory]::CreateDirectory($native)
[void][IO.Directory]::CreateDirectory($plugins)
$vswhere='C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe'
$tools=(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
if(!$tools){throw 'Install the MSVC x64 C++ Build Tools and Windows SDK.'}
$script=Join-Path $native 'compile.cmd'
$source=(Resolve-Path -LiteralPath "$PSScriptRoot/FrameTimingD3D11.cpp").Path
$headers=(Resolve-Path -LiteralPath "$UnityEditor/Data/PluginAPI").Path
$lines=@(
  '@echo off'
  ('call "{0}\VC\Auxiliary\Build\vcvars64.bat" > nul' -f $tools)
  'if errorlevel 1 exit /b 1'
  ('cl.exe /nologo /std:c++17 /O2 /MT /LD /EHsc /I"{0}" /Fo"{1}\timing.obj" "{2}" /link /OUT:"{3}\Raid016Timing.dll" /IMPLIB:"{1}\Raid016Timing.lib"' -f $headers,$native,$source,$plugins)
  'exit /b %errorlevel%'
)
[IO.File]::WriteAllLines($script,$lines,[Text.Encoding]::ASCII)
& $script
if($LASTEXITCODE -ne 0){throw 'Build the development-only art timing plugin failed.'}
Copy-Item -LiteralPath "$PSScriptRoot/TimingPlugin.meta" -Destination (Join-Path $plugins 'Raid016Timing.dll.meta') -Force
Get-Item -LiteralPath (Join-Path $plugins 'Raid016Timing.dll')
