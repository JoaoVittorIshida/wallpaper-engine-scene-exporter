param(
    [Parameter(Mandatory = $true)]
    [string] $DestinationDirectory,
    [string] $Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
[xml] $projectXml = Get-Content -LiteralPath (Join-Path $repository 'src\WallpaperSceneExporter\WallpaperSceneExporter.csproj')
$version = [string] $projectXml.Project.PropertyGroup.Version
$sourceDirectory = Join-Path $repository "artifacts\publish\v$version\$Runtime"
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
$executable = Join-Path $destination "WallpaperSceneExporter.exe"

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "A pasta de destino deve conter uma instalação existente do Scene Exporter."
}
if ($destination.TrimEnd('\') -eq $sourceDirectory.TrimEnd('\')) {
    throw "Escolha uma instalação diferente da pasta de publicação."
}
if (Get-CimInstance Win32_Process -Filter "Name='WallpaperSceneExporter.exe'" |
        Where-Object { $_.ExecutablePath -eq $executable }) {
    throw "Feche o Scene Exporter dessa pasta antes de atualizar."
}

$files = @(
    "WallpaperSceneExporter.exe",
    "config.json",
    "THIRD-PARTY-NOTICES.md",
    "LICENSE.txt",
    "LEIA-ME.txt",
    "Instalar FFmpeg.cmd",
    "scripts\Install-Ffmpeg.ps1",
    "scripts\ffmpeg-manifest.json"
)
$licenseDirectory = Join-Path $sourceDirectory 'licenses'
if (Test-Path -LiteralPath $licenseDirectory) {
    $files += Get-ChildItem -LiteralPath $licenseDirectory -Filter '*.txt' -File |
        ForEach-Object { 'licenses\' + $_.Name }
}
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory $relative) -PathType Leaf)) {
        throw "Publicação incompleta: $relative. Execute scripts\Publish.ps1 primeiro."
    }
}

$backupDirectory = Join-Path $repository ("artifacts\update-backups\" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
foreach ($relative in $files) {
    $installedFile = Join-Path $destination $relative
    if (Test-Path -LiteralPath $installedFile -PathType Leaf) {
        $backupFile = Join-Path $backupDirectory $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backupFile)) -Force | Out-Null
        Copy-Item -LiteralPath $installedFile -Destination $backupFile
    }
}

foreach ($relative in $files) {
    $installedFile = Join-Path $destination $relative
    if ($relative -eq "config.json" -and (Test-Path -LiteralPath $installedFile)) { continue }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($installedFile)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceDirectory $relative) -Destination $installedFile -Force
    if ((Get-FileHash -LiteralPath $installedFile).Hash -ne
            (Get-FileHash -LiteralPath (Join-Path $sourceDirectory $relative)).Hash) {
        throw "Falha ao verificar o arquivo atualizado: $relative. Backup em $backupDirectory"
    }
}

Write-Output "Aplicativo atualizado: $executable"
Write-Output "Configurações, input, output, temp e logs preservados."
Write-Output "Backup dos arquivos substituídos: $backupDirectory"
