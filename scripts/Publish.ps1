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

# FFmpeg is downloaded directly from its provider by the end user.
dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeFfmpeg=false -p:RestoreLockedMode=true -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'A publicacao falhou.' }

$copies = @{
    'config.json' = 'config.json'
    'LICENSE' = 'LICENSE.txt'
    'Instalar FFmpeg.cmd' = 'Instalar FFmpeg.cmd'
    'docs\PORTABLE.txt' = 'LEIA-ME.txt'
    'scripts\Install-Ffmpeg.ps1' = 'scripts\Install-Ffmpeg.ps1'
    'scripts\ffmpeg-manifest.json' = 'scripts\ffmpeg-manifest.json'
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
    'LICENSE.txt', 'LEIA-ME.txt', 'Instalar FFmpeg.cmd', 'scripts\Install-Ffmpeg.ps1', 'scripts\ffmpeg-manifest.json')
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $relative) -PathType Leaf)) {
        throw "Arquivo obrigatorio ausente: $relative"
    }
}

$licenseDestination = Join-Path $publishDirectory 'licenses'
New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repository 'docs\licenses') -Filter '*.txt' -File |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $licenseDestination -Force }

# Package only public distribution files, even when republishing a used folder.
$packageFiles = @('WallpaperSceneExporter.exe', 'config.json', 'THIRD-PARTY-NOTICES.md',
    'LICENSE.txt', 'LEIA-ME.txt', 'Instalar FFmpeg.cmd', 'scripts', 'licenses') |
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
