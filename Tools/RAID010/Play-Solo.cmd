@echo off
setlocal
set "raid010_dir=%~dp0"
if not exist "%raid010_dir%UnderwaterFishing-VisualSlice.exe" set "raid010_dir=%~dp0..\..\Builds\RAID010-VisualSlice\"
if not exist "%raid010_dir%UnderwaterFishing-VisualSlice.exe" (
  echo Build VisualVerticalSlice into Builds\RAID010-VisualSlice first.
  pause
  exit /b 1
)
if not exist "%raid010_dir%UserData" mkdir "%raid010_dir%UserData"
start "" /D "%raid010_dir%" "%raid010_dir%UnderwaterFishing-VisualSlice.exe" -raid-host -raid-save "%raid010_dir%UserData\team-progress.json" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
