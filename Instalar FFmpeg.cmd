@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-Ffmpeg.ps1" -DestinationDirectory "%~dp0ffmpeg"
if errorlevel 1 (
    echo.
    echo A instalacao falhou. Confira a mensagem acima.
    pause
    exit /b 1
)
echo.
echo Tudo pronto. Abra WallpaperSceneExporter.exe.
pause
