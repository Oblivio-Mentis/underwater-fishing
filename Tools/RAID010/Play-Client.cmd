@echo off
setlocal
set "raid010_dir=%~dp0"
if not exist "%raid010_dir%UnderwaterFishing-VisualSlice.exe" set "raid010_dir=%~dp0..\..\Builds\RAID010-VisualSlice\"
if not exist "%raid010_dir%UnderwaterFishing-VisualSlice.exe" (
  echo Build VisualVerticalSlice into Builds\RAID010-VisualSlice first.
  pause
  exit /b 1
)
start "" /D "%raid010_dir%" "%raid010_dir%UnderwaterFishing-VisualSlice.exe" -raid-client -screen-fullscreen 0 -screen-width 1280 -screen-height 720
