param(
    [Parameter(Mandatory = $true)]
    [string] $InputMp4,

    [string] $FfmpegPath = "ffmpeg.exe",
    [string] $OutputDirectory = ".\gif-comparison",
    [int] $Width = 480,
    [int] $Height = 480,
    [int] $Fps = 30,
    [int] $Colors = 256
)

$ErrorActionPreference = "Stop"
$resolvedInput = (Resolve-Path -LiteralPath $InputMp4).Path
$resolvedFfmpeg = (Get-Command $FfmpegPath -ErrorAction Stop).Source
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$statsModes = @("full", "diff")
$dithers = @("sierra2_4a", "sierra3", "floyd_steinberg", "bayer")
$results = [System.Collections.Generic.List[object]]::new()

foreach ($statsMode in $statsModes) {
    $palette = Join-Path $resolvedOutput "palette_$statsMode.png"
    $paletteFilter = "fps=$Fps,scale=${Width}:${Height}:flags=lanczos,palettegen=max_colors=${Colors}:stats_mode=${statsMode}:reserve_transparent=0"
    & $resolvedFfmpeg -hide_banner -loglevel warning -y -i $resolvedInput -vf $paletteFilter $palette
    if ($LASTEXITCODE -ne 0) { throw "palettegen falhou para stats_mode=$statsMode" }

    foreach ($dither in $dithers) {
        $gif = Join-Path $resolvedOutput "${statsMode}_${dither}.gif"
        $useFilter = "[0:v]fps=$Fps,scale=${Width}:${Height}:flags=lanczos[v];[v][1:v]paletteuse=dither=${dither}:diff_mode=rectangle"
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        & $resolvedFfmpeg -hide_banner -loglevel warning -y -i $resolvedInput -i $palette -lavfi $useFilter -loop 0 $gif
        $stopwatch.Stop()
        if ($LASTEXITCODE -ne 0) { throw "paletteuse falhou para $statsMode/$dither" }
        $results.Add([pscustomobject]@{
            PaletteStats = $statsMode
            Dithering = $dither
            Bytes = (Get-Item -LiteralPath $gif).Length
            EncodeMilliseconds = $stopwatch.ElapsedMilliseconds
            File = $gif
        })
    }
}

$report = Join-Path $resolvedOutput "comparison.csv"
$results | Sort-Object Bytes | Export-Csv -LiteralPath $report -NoTypeInformation -Encoding UTF8
$results | Sort-Object Bytes | Format-Table -AutoSize
Write-Output "Relatório: $report"
