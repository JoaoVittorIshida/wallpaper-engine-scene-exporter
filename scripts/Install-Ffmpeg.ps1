param(
    [string] $DestinationDirectory = (Join-Path $PSScriptRoot '..\src\WallpaperSceneExporter\ffmpeg'),
    [string] $CacheDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'WallpaperEngineSceneExporter\downloads')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ffmpeg-manifest.json') -Raw | ConvertFrom-Json
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
$cache = [IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$archive = Join-Path $cache ("ffmpeg-" + $manifest.version + '-essentials.zip')
$stage = Join-Path $cache ('extract-' + [Guid]::NewGuid().ToString('N'))

if (-not (Test-Path -LiteralPath $archive) -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $manifest.sha256) {
    Write-Output ("Baixando FFmpeg " + $manifest.version + ' de gyan.dev...')
    $download = $archive + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Invoke-WebRequest -Uri $manifest.url -OutFile $download -UseBasicParsing
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $manifest.sha256) {
            throw 'O SHA-256 do FFmpeg diverge do manifesto. Nenhum executavel foi instalado.'
        }
        Move-Item -LiteralPath $download -Destination $archive -Force
    }
    finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force }
    }
}

try {
    Write-Output 'SHA-256 confirmado. Extraindo FFmpeg e ffprobe...'
    Expand-Archive -LiteralPath $archive -DestinationPath $stage
    $source = Join-Path $stage $manifest.archiveRoot
    foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
        $binary = Join-Path $source ("bin\" + $name)
        if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw "Arquivo ausente: $name" }
        $version = & $binary -hide_banner -version
        if ($LASTEXITCODE -ne 0 -or $version[0] -notlike ("*version " + $manifest.version + '*')) {
            throw "Versao inesperada de $name."
        }
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'bin\ffmpeg.exe') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $source 'bin\ffprobe.exe') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $source 'README.txt') -Destination $destination -Force
    Write-Output ("FFmpeg " + $manifest.version + " e ffprobe instalados em: $destination")
}
finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($cache.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O diretorio de extracao esta fora do cache esperado.'
    }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
