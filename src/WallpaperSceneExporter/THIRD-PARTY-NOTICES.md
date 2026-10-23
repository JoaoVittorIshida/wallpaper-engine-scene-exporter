# Third-party notices

The MIT license in this project applies to the Wallpaper Engine Scene Exporter code. Third-party software retains its own license.

## FFmpeg and ffprobe

The repository and public application ZIP do **not** redistribute FFmpeg executables. `Instalar FFmpeg.cmd` / `scripts/Install-Ffmpeg.ps1` downloads the following unmodified package directly from its provider for local use:

- Version: **9.0.2**, essentials Windows x64 build by Gyan Doshi.
- Provider: https://www.gyan.dev/ffmpeg/builds/
- Pinned archive: https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip
- SHA-256: `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba`
- Build license: **GPLv3**; includes external components such as libx264 with their respective licenses.
- FFmpeg project: https://ffmpeg.org/
- License information: https://ffmpeg.org/legal.html
- FFmpeg source release: https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz

The provider's `LICENSE` and `README.txt`, including build configuration and the exact FFmpeg source reference, are installed beside `ffmpeg.exe` and `ffprobe.exe`. Keep those files with that local installation. A generic FFmpeg source archive is not a substitute for all corresponding sources of the provider's external libraries when redistributing its native build.

FFmpeg is invoked as a separate executable through standard input and command-line arguments. Its libraries are not linked into this application.

## Managed dependencies

- **CommunityToolkit.Mvvm 8.4.0** — MIT; https://github.com/CommunityToolkit/dotnet
- **Vortice.Direct3D11 / Vortice.DXGI 3.8.3** — MIT; https://github.com/amerkoleci/Vortice.Windows
- **SharpGen.Runtime and SharpGen.Runtime.COM** — MIT; https://github.com/SharpGenTools/SharpGenTools
- **WinRT.Runtime / Microsoft.Windows.SDK.NET.Ref** — Microsoft Windows SDK projection components; https://github.com/microsoft/CsWinRT
- **.NET 10 runtime** — MIT and component notices; https://github.com/dotnet/runtime/blob/main/LICENSE.TXT and https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT

The precise resolved NuGet versions are recorded in `packages.lock.json`. Test-only packages are not included in the portable application.

Full license texts and upstream notices for the managed dependencies and the .NET runtime are included in `docs/licenses` in the repository and `licenses` in the portable application.

Wallpaper Engine is an external, separately licensed prerequisite and is not distributed by this project. This application is independent and is not affiliated with Wallpaper Engine, Valve, FFmpeg or the dependency authors.
