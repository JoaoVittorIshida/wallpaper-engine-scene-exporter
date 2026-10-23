# Rebuilding the bundled FFmpeg tools

The portable distribution includes FFmpeg 9.0.2, x264 at commit
`31e19f92f00c7003fa115047ce50978bc98c3a0d`, and zlib 1.3.1. Their complete,
unmodified source archives, SHA-256 manifest and build script are in `ffmpeg/sources`
inside the application ZIP. Licenses are in `ffmpeg/licenses`. The FFmpeg binaries
are GPL-2.0-or-later; the application's independent C# code remains MIT.

The build uses Ubuntu 24.04 and MinGW-w64 to cross-compile static Windows x64
executables. Install the build tools:

```sh
sudo apt-get update
sudo apt-get install --no-install-recommends gcc-mingw-w64-x86-64-win32 \
    mingw-w64-common mingw-w64-x86-64-dev nasm make pkg-config python3 xz-utils
```

From this repository, run:

```sh
bash scripts/Build-Ffmpeg.sh
```

To rebuild using only the source files from a portable ZIP, create this layout:

```text
rebuild/
  scripts/       Build-Ffmpeg.sh, native-sources.json, and the three source archives
  docs/          README.md from the bundle, renamed NATIVE-BUILD.md
```

Then, from `rebuild/`:

```sh
SOURCE_DIRECTORY="$PWD/scripts" bash scripts/Build-Ffmpeg.sh "$PWD/result"
```

Only the initial installation of the compiler requires network access in this
second example. Source archives are verified against `native-sources.json`.
The output includes the executables, complete sources, license texts, generated
configuration and compiler versions. Temporary build directories are retained
beside the output for inspection. No patches are applied to upstream sources.

The configuration enables the app's H.264, GIF, PNG palette, scaling and frame
validation features. It intentionally has no network protocols or unrelated
codecs. Windows DLL imports are checked against a system DLL allowlist. Compiler
runtime components use their upstream runtime exceptions; their notices are
included in `MinGW-w64-copyright.txt`.

GitHub Actions builds these tools first, then runs the Windows integration tests
with these exact binaries before publishing the portable application artifact.
