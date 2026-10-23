@echo off
setlocal
set "SCENE_EXPORTER_EXE="
if exist "%~dp0artifacts\latest-portable-path.txt" set /p "SCENE_EXPORTER_EXE="<"%~dp0artifacts\latest-portable-path.txt"
if not defined SCENE_EXPORTER_EXE set "SCENE_EXPORTER_EXE=%~dp0artifacts\publish\v1.2.1\win-x64\WallpaperSceneExporter.exe"
if not exist "%SCENE_EXPORTER_EXE%" (
    echo O aplicativo ainda nao foi publicado. Execute scripts\Publish.ps1 primeiro.
    pause
    exit /b 1
)
start "" "%SCENE_EXPORTER_EXE%"
