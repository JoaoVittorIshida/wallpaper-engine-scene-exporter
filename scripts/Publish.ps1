param(
    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repository 'src\WallpaperSceneExporter\WallpaperSceneExporter.csproj'
[xml] $projectXml = Get-Content -LiteralPath $project
$version = [string] $projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao invalida no projeto.' }
$publishDirectory = Join-Path $repository "artifacts\publish\v$version\$Runtime"
$archive = Join-Path $repository "artifacts\WallpaperEngineSceneExporter-$version-$Runtime.zip"

# Only redistribute tools built with the accompanying complete source bundle.
$nativeDirectory = Join-Path $repository 'artifacts\ffmpeg-native'
$nativeRequired = @('ffmpeg.exe', 'ffprobe.exe', 'SHA256SUMS.txt', 'README.txt',
    'sources\Build-Ffmpeg.sh', 'sources\native-sources.json', 'sources\README.md',
    'licenses\COPYING.GPLv2', 'licenses\x264-COPYING.txt', 'licenses\zlib-LICENSE.txt',
    'licenses\MinGW-w64-copyright.txt', 'licenses\GCC-copyright.txt', 'build-info\config.h', 'build-info\config.mak',
    'build-info\x264-config.h', 'build-info\x264-config.mak', 'build-info\toolchain.txt')
foreach ($relative in $nativeRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $nativeDirectory $relative) -PathType Leaf)) {
        throw "Pacote nativo incompleto: $relative. Consulte docs/NATIVE-BUILD.md ou baixe o artefato media-tools-win-x64 do CI."
    }
}
foreach ($line in (Get-Content -LiteralPath (Join-Path $nativeDirectory 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-f]{64})  (ffmpeg\.exe|ffprobe\.exe)$') { throw 'Manifesto nativo invalido.' }
    if ((Get-FileHash -LiteralPath (Join-Path $nativeDirectory $Matches[2])).Hash.ToLowerInvariant() -ne $Matches[1]) {
        throw 'SHA-256 dos executaveis nativos nao confere.'
    }
}
$sourceManifest = Get-Content -LiteralPath (Join-Path $nativeDirectory 'sources\native-sources.json') -Raw | ConvertFrom-Json
foreach ($entry in $sourceManifest) {
    if ((Get-FileHash -LiteralPath (Join-Path $nativeDirectory ('sources\' + $entry.name))).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "SHA-256 do fonte nao confere: $($entry.name)"
    }
}

dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeFfmpeg=false -p:RestoreLockedMode=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'A publicacao falhou.' }

$copies = @{
    'config.json' = 'config.json'
    'LICENSE' = 'LICENSE.txt'
    'docs\PORTABLE.txt' = 'LEIA-ME.txt'
}
foreach ($source in $copies.Keys) {
    $destination = Join-Path $publishDirectory $copies[$source]
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repository $source) -Destination $destination -Force
}
$distributionConfig = Get-Content -LiteralPath (Join-Path $publishDirectory 'config.json') -Raw | ConvertFrom-Json
$distributionConfig.ffmpegPath = ''
$distributionConfig.wallpaperEnginePath = ''
$distributionConfig.autoProcess = $false
$distributionConfig.keepFailedArtifacts = $false
[IO.File]::WriteAllText((Join-Path $publishDirectory 'config.json'),
    ($distributionConfig | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

$required = @('WallpaperSceneExporter.exe', 'config.json', 'THIRD-PARTY-NOTICES.md',
    'LICENSE.txt', 'LEIA-ME.txt')
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $relative) -PathType Leaf)) {
        throw "Arquivo obrigatorio ausente: $relative"
    }
}

$mediaDestination = Join-Path $publishDirectory 'ffmpeg'
New-Item -ItemType Directory -Path $mediaDestination -Force | Out-Null
foreach ($relative in @('ffmpeg.exe', 'ffprobe.exe', 'SHA256SUMS.txt', 'README.txt', 'sources', 'licenses', 'build-info')) {
    Copy-Item -LiteralPath (Join-Path $nativeDirectory $relative) -Destination $mediaDestination -Recurse -Force
}

$licenseDestination = Join-Path $publishDirectory 'licenses'
New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repository 'docs\licenses') -Filter '*.txt' -File |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $licenseDestination -Force }

# Package only public distribution files, even when republishing a used folder.
$packageFiles = @('WallpaperSceneExporter.exe', 'config.json', 'THIRD-PARTY-NOTICES.md',
    'LICENSE.txt', 'LEIA-ME.txt', 'ffmpeg', 'licenses') |
    ForEach-Object { Join-Path $publishDirectory $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive -CompressionLevel Optimal -Force
$checksumPath = Join-Path $repository 'artifacts\SHA256SUMS.txt'
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($checksumPath, "$checksum  $([IO.Path]::GetFileName($archive))`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $repository 'artifacts\latest-portable-path.txt'),
    (Join-Path $publishDirectory 'WallpaperSceneExporter.exe'), [Text.UTF8Encoding]::new($false))
Write-Output "Publicacao: $publishDirectory"
Write-Output "Arquivo portatil: $archive"
Write-Output "SHA-256: $checksum"
