#!/usr/bin/env bash
# Cross-compile the exact Windows media tools shipped with the portable app.
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
output="${1:-$script_dir/../artifacts/ffmpeg-native}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
work="$(mktemp -d "$output/../native-build.XXXXXXXX")"
prefix="$work/prefix"
mkdir -p "$output/sources" "$output/licenses" "$output/build-info" "$prefix/lib/pkgconfig" "$prefix/include"
cp "$script_dir/Build-Ffmpeg.sh" "$script_dir/native-sources.json" "$output/sources/"
cp "$script_dir/../docs/NATIVE-BUILD.md" "$output/sources/README.md"

# A source bundle can be rebuilt offline: pass SOURCE_DIRECTORY=/path/to/sources.
python3 - "$script_dir/native-sources.json" "$output/sources" "${SOURCE_DIRECTORY:-}" <<'PY'
import hashlib, json, pathlib, shutil, sys, urllib.request
for item in json.loads(pathlib.Path(sys.argv[1]).read_text()):
    target = pathlib.Path(sys.argv[2]) / item['name']
    local = pathlib.Path(sys.argv[3]) / item['name'] if sys.argv[3] else None
    if local and local.resolve() != target.resolve():
        shutil.copyfile(local, target)
    elif not target.exists():
        urllib.request.urlretrieve(item['url'], target)
    if hashlib.sha256(target.read_bytes()).hexdigest() != item['sha256']:
        raise SystemExit('SHA-256 mismatch: ' + item['name'])
PY
tar -xf "$output/sources/ffmpeg-9.0.2.tar.xz" -C "$work"
tar -xf "$output/sources/x264-31e19f92f00c7003fa115047ce50978bc98c3a0d.tar.gz" -C "$work"
tar -xf "$output/sources/zlib-1.3.1.tar.gz" -C "$work"
export CC=x86_64-w64-mingw32-gcc-win32
export AR=x86_64-w64-mingw32-ar
export RANLIB=x86_64-w64-mingw32-ranlib
export PKG_CONFIG_LIBDIR="$prefix/lib/pkgconfig"
jobs="${BUILD_JOBS:-$(nproc)}"

cd "$work/zlib-1.3.1"
make -f win32/Makefile.gcc -j"$jobs" PREFIX=x86_64-w64-mingw32- CC="$CC" libz.a
cp libz.a "$prefix/lib/"
cp zlib.h zconf.h "$prefix/include/"
cat > "$prefix/lib/pkgconfig/zlib.pc" <<EOF
prefix=$prefix
libdir=\${prefix}/lib
includedir=\${prefix}/include
Name: zlib
Description: zlib compression library
Version: 1.3.1
Libs: -L\${libdir} -lz
Cflags: -I\${includedir}
EOF

cd "$work/x264-31e19f92f00c7003fa115047ce50978bc98c3a0d"
./configure --host=x86_64-w64-mingw32 --cross-prefix=x86_64-w64-mingw32- \
    --prefix="$prefix" --enable-static --disable-cli --disable-opencl --bit-depth=8
make -j"$jobs"
make install
cp config.h "$output/build-info/x264-config.h"
cp config.mak "$output/build-info/x264-config.mak"
cp COPYING "$output/licenses/x264-COPYING.txt"

cd "$work/ffmpeg-9.0.2"
./configure --prefix="$prefix" --target-os=mingw32 --arch=x86_64 \
    --enable-cross-compile --cross-prefix=x86_64-w64-mingw32- --cc="$CC" \
    --pkg-config=pkg-config --pkg-config-flags=--static \
    --extra-cflags="-I$prefix/include" --extra-ldflags="-L$prefix/lib -static" \
    --disable-autodetect --disable-everything --disable-network --disable-doc \
    --disable-debug --disable-shared --enable-static --enable-gpl --enable-libx264 --enable-zlib \
    --enable-ffmpeg --enable-ffprobe \
    --enable-encoder=libx264,gif,png --enable-decoder=h264,png,rawvideo,gif,wrapped_avframe \
    --enable-parser=h264,gif,png --enable-demuxer=mov,image2,rawvideo,gif \
    --enable-muxer=mp4,gif,image2 --enable-protocol=file,pipe --enable-indev=lavfi \
    --enable-filter=scale,fps,palettegen,paletteuse,format,split,testsrc2,buffer,buffersink
make -j"$jobs"
cp ffmpeg.exe ffprobe.exe "$output/"
cp COPYING.* "$output/licenses/"
cp LICENSE.md "$output/licenses/FFmpeg-LICENSE.md"
cp "$work/zlib-1.3.1/LICENSE" "$output/licenses/zlib-LICENSE.txt"
cp config.h ffbuild/config.mak ffbuild/config.log "$output/build-info/"
cp /usr/share/doc/mingw-w64-common/copyright "$output/licenses/MinGW-w64-copyright.txt"
cp /usr/share/doc/gcc-mingw-w64-x86-64-win32/copyright "$output/licenses/GCC-copyright.txt"
{
    "$CC" --version
    nasm -v
    make --version
    dpkg-query -W gcc-mingw-w64-x86-64-win32 mingw-w64-common mingw-w64-x86-64-dev nasm
} > "$output/build-info/toolchain.txt"
# Reject unexpected non-system DLL dependencies. Both executables must be standalone.
for executable in ffmpeg.exe ffprobe.exe; do
    x86_64-w64-mingw32-objdump -p "$output/$executable" | grep 'DLL Name:' > "$output/build-info/$executable.imports.txt"
    if grep -Eiv 'DLL Name: (KERNEL32|msvcrt|ADVAPI32|USER32|WS2_32|bcrypt|SHELL32|ole32|GDI32|ntdll|VERSION|PSAPI|SHLWAPI|CRYPT32|SECUR32|winmm)\.dll$' "$output/build-info/$executable.imports.txt"; then
        echo "Unexpected DLL dependency in $executable" >&2
        exit 1
    fi
done
cd "$output"
sha256sum ffmpeg.exe ffprobe.exe > SHA256SUMS.txt
printf '%s\n' 'FFmpeg 9.0.2 + x264 (GPL-2.0-or-later), zlib 1.3.1.' \
    'Built by scripts/Build-Ffmpeg.sh; no upstream source modifications.' \
    'Complete corresponding sources, build script and instructions: sources/.' \
    'Build configuration, compiler versions and DLL imports: build-info/.' > README.txt
