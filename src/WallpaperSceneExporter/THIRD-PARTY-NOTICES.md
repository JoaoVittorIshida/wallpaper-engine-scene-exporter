# Third-party notices

The MIT license in this project applies to the Wallpaper Engine Scene Exporter code. Third-party software retains its own license.

## FFmpeg and ffprobe

The portable application ZIP includes **FFmpeg and ffprobe 9.0.2**, compiled by this
project with **x264** at commit `31e19f92f00c7003fa115047ce50978bc98c3a0d` and
**zlib 1.3.1**. These FFmpeg binaries are licensed under **GPL-2.0-or-later**.
All upstream sources are unmodified. No nonfree components are enabled.

- FFmpeg: https://ffmpeg.org/ — https://ffmpeg.org/legal.html
- x264: https://www.videolan.org/developers/x264.html — GPL-2.0-or-later.
- zlib: https://zlib.net/ — zlib license.
- MinGW-w64 runtime notices and runtime exceptions: `ffmpeg/licenses/MinGW-w64-copyright.txt`.
- GCC runtime notices: `ffmpeg/licenses/GCC-copyright.txt`; GCC Runtime Library Exception 3.1: `licenses/GCC-RUNTIME-EXCEPTION.txt`.

The complete corresponding source archives of FFmpeg, x264 and zlib, their
SHA-256 manifest, build script and instructions are included in **`ffmpeg/sources`**
in the same application ZIP. Full native license texts are in **`ffmpeg/licenses`**;
generated configuration, toolchain versions and system DLL imports are in
**`ffmpeg/build-info`**. See `docs/NATIVE-BUILD.md` in the repository to rebuild.
Keep these files with any redistribution of the native binaries.

The optional developer-only `scripts/Install-Ffmpeg.ps1` downloads a pinned Gyan
build directly from its provider for local development. That downloaded build is
not used in the public portable ZIP; `Publish.ps1` requires the project-built
tools and their corresponding sources.

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
