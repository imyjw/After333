param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$AudioDirectory = '',
    [string]$ReportDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AudioDirectory)) {
    $AudioDirectory = Join-Path $ProjectRoot 'TempBuild/ManaCannonPresentation_20261001/Export/Resources/Project333/Units/ManaCannon/Audio'
}
if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $ReportDirectory = Join-Path $ProjectRoot 'TempBuild/ManaCannonPresentation_20261001/Export/AudioReports'
}
$manaAssets = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'Assets')).TrimEnd('\','/')
$manaReportResolved = [IO.Path]::GetFullPath($ReportDirectory).TrimEnd('\','/')
if ($manaReportResolved.Equals($manaAssets, [StringComparison]::OrdinalIgnoreCase) -or $manaReportResolved.StartsWith($manaAssets + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation report output inside Assets is refused.'
}
$manaReportPath = Join-Path $ReportDirectory 'ManaCannonAudioIndependentValidation.json'
if (Test-Path -LiteralPath $manaReportPath) { throw 'Choose a fresh report directory; existing report is preserved.' }
function Read-ManaPcmCue {
    param([string]$Path, [double]$ExpectedSeconds, [string]$Name)
    $manaStream = [IO.File]::OpenRead($Path)
    $manaReader = [IO.BinaryReader]::new($manaStream)
    try {
        $manaAscii = [Text.Encoding]::ASCII
        if ($manaAscii.GetString($manaReader.ReadBytes(4)) -ne 'RIFF') { throw 'Missing RIFF header.' }
        $manaRiffSize = $manaReader.ReadUInt32()
        if ($manaAscii.GetString($manaReader.ReadBytes(4)) -ne 'WAVE') { throw 'Missing WAVE header.' }
        $manaFormat = $null
        $manaPcmBytes = $null
        while ($manaStream.Position + 8 -le $manaStream.Length) {
            $manaChunk = $manaAscii.GetString($manaReader.ReadBytes(4))
            $manaSize = $manaReader.ReadUInt32()
            $manaEnd = $manaStream.Position + $manaSize
            if ($manaEnd -gt $manaStream.Length) { throw 'Truncated RIFF chunk.' }
            if ($manaChunk -eq 'fmt ') {
                if ($manaSize -lt 16) { throw 'Invalid fmt chunk.' }
                $manaFormat = [ordered]@{
                    encoding = $manaReader.ReadUInt16(); channels = $manaReader.ReadUInt16()
                    sampleRate = $manaReader.ReadUInt32(); byteRate = $manaReader.ReadUInt32()
                    blockAlign = $manaReader.ReadUInt16(); bitsPerSample = $manaReader.ReadUInt16()
                }
            } elseif ($manaChunk -eq 'data') {
                if ($null -ne $manaPcmBytes) { throw 'Unexpected duplicate data chunk.' }
                $manaPcmBytes = $manaReader.ReadBytes($manaSize)
            }
            $manaStream.Position = $manaEnd + ($manaSize % 2)
        }
        if ($manaRiffSize + 8 -ne $manaStream.Length) { throw 'RIFF size mismatch.' }
        $manaContainerBytes = $manaStream.Length
    } finally { $manaReader.Dispose() }
    if ($null -eq $manaFormat -or $null -eq $manaPcmBytes) { throw 'Missing WAV chunks.' }
    if ($manaFormat.encoding -ne 1 -or $manaFormat.channels -ne 1 -or $manaFormat.sampleRate -ne 44100 -or $manaFormat.bitsPerSample -ne 16 -or $manaFormat.byteRate -ne 88200 -or $manaFormat.blockAlign -ne 2) { throw 'Unexpected PCM format.' }
    if ($manaPcmBytes.Length % 2 -ne 0) { throw 'Incomplete PCM sample.' }
    $manaSampleCount = $manaPcmBytes.Length / 2
    [Int16[]]$manaSamples = New-Object 'Int16[]' $manaSampleCount
    [Buffer]::BlockCopy($manaPcmBytes, 0, $manaSamples, 0, $manaPcmBytes.Length)
    $manaSum = 0.0; $manaSquares = 0.0; $manaPeak = 0; $manaClipped = 0
    $manaFirstPeak = 0; $manaLastPeak = 0; $manaFirstStep = 0; $manaLastStep = 0
    $manaPeakSampleIndex = 0; $manaFirstActive = -1; $manaLastActive = -1
    $manaWindows = New-Object 'System.Collections.Generic.List[object]'
    $manaWindowCount = 1103; $manaWindowSquares = 0.0; $manaWindowSamples = 0; $manaWindowIndex = 0
    for ($manaIndex = 0; $manaIndex -lt $manaSampleCount; $manaIndex++) {
        $manaValue = [int]$manaSamples[$manaIndex]
        $manaAbsolute = [Math]::Abs($manaValue)
        $manaSum += $manaValue
        $manaSquares += [double]$manaValue * $manaValue
        $manaWindowSquares += [double]$manaValue * $manaValue
        $manaWindowSamples++
        if ($manaAbsolute -gt $manaPeak) { $manaPeak = $manaAbsolute; $manaPeakSampleIndex = $manaIndex }
        if ($manaValue -eq -32768 -or $manaValue -eq 32767) { $manaClipped++ }
        if ($manaValue -ne 0) { if ($manaFirstActive -lt 0) { $manaFirstActive = $manaIndex }; $manaLastActive = $manaIndex }
        if ($manaIndex -lt 64) { $manaFirstPeak = [Math]::Max($manaFirstPeak, $manaAbsolute) }
        if ($manaIndex -ge $manaSampleCount - 64) { $manaLastPeak = [Math]::Max($manaLastPeak, $manaAbsolute) }
        if ($manaIndex -gt 0) {
            $manaStep = [Math]::Abs($manaValue - [int]$manaSamples[$manaIndex - 1])
            if ($manaIndex -lt 64) { $manaFirstStep = [Math]::Max($manaFirstStep, $manaStep) }
            if ($manaIndex -ge $manaSampleCount - 64) { $manaLastStep = [Math]::Max($manaLastStep, $manaStep) }
        }
        if ($manaWindowSamples -eq $manaWindowCount -or $manaIndex -eq $manaSampleCount - 1) {
            $manaWindowPower = $manaWindowSquares / $manaWindowSamples / (32768.0 * 32768.0)
            $manaWindows.Add([ordered]@{startSeconds=$manaWindowIndex * $manaWindowCount / 44100.0; samples=$manaWindowSamples; rmsDbfs=if($manaWindowPower -gt 0){10 * [Math]::Log10($manaWindowPower)}else{-120.0}})
            $manaWindowIndex++; $manaWindowSquares = 0.0; $manaWindowSamples = 0
        }
    }
    $manaDc = $manaSum / $manaSampleCount / 32768.0
    $manaSeconds = $manaSampleCount / [double]$manaFormat.sampleRate
    $manaPeakDb = 20 * [Math]::Log10($manaPeak / 32768.0)
    $manaRmsDb = 10 * [Math]::Log10($manaSquares / $manaSampleCount / (32768.0 * 32768.0))
    $manaEdgeCheck = $manaFirstPeak -eq 0 -and $manaLastPeak -eq 0 -and $manaFirstStep -eq 0 -and $manaLastStep -eq 0
    $manaActiveEdgeCheck = $manaFirstActive -ge 64 -and $manaLastActive -lt $manaSampleCount - 64 -and [Math]::Abs([int]$manaSamples[$manaFirstActive]) -le 2 -and [Math]::Abs([int]$manaSamples[$manaLastActive]) -le 2
    $manaPassed = $manaClipped -eq 0 -and $manaEdgeCheck -and $manaActiveEdgeCheck -and [Math]::Abs($manaDc) -lt 0.00005 -and [Math]::Abs($manaSeconds - $ExpectedSeconds) -lt 0.000001 -and $manaPeakDb -ge -20.1 -and $manaPeakDb -le -18.0 -and $manaRmsDb -lt -27
    return [ordered]@{
        name=$Name; file='Export/Resources/Project333/Units/ManaCannon/Audio/' + $Name + '.wav'
        validation='Independent RIFF chunk parser and decoded PCM analysis'; format=$manaFormat
        containerBytes=$manaContainerBytes; pcmBytes=$manaPcmBytes.Length; samples=$manaSampleCount
        durationSeconds=$manaSeconds; peakDbfs=$manaPeakDb; rmsDbfs=$manaRmsDb; dcOffset=$manaDc
        peakTimeSeconds=$manaPeakSampleIndex / 44100.0; clippedSamples=$manaClipped
        firstSample=$manaSamples[0]; lastSample=$manaSamples[$manaSampleCount - 1]
        boundary64Samples=[ordered]@{firstPeak=$manaFirstPeak / 32768.0; lastPeak=$manaLastPeak / 32768.0; firstMaxAdjacentStep=$manaFirstStep / 32768.0; lastMaxAdjacentStep=$manaLastStep / 32768.0; passed=$manaEdgeCheck}
        activeBoundary=[ordered]@{firstNonzeroSample=$manaFirstActive; lastNonzeroSample=$manaLastActive; firstNonzeroValue=$manaSamples[$manaFirstActive]; lastNonzeroValue=$manaSamples[$manaLastActive]; passed=$manaActiveEdgeCheck}
        windows25ms=$manaWindows.ToArray(); sha256=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant(); passed=$manaPassed
    }
}
$manaClips = @(
    Read-ManaPcmCue -Path (Join-Path $AudioDirectory 'ManaShot.wav') -ExpectedSeconds 0.40 -Name 'ManaShot'
    Read-ManaPcmCue -Path (Join-Path $AudioDirectory 'ManaExplosion.wav') -ExpectedSeconds 0.35 -Name 'ManaExplosion'
)
$manaOverallPass = @($manaClips | Where-Object { -not $_.passed }).Count -eq 0
$manaReport = [ordered]@{date='2026-10-01'; clips=$manaClips; passed=$manaOverallPass; listening='File/waveform validation only; no device or in-game listening validation is claimed.'}
[IO.Directory]::CreateDirectory($ReportDirectory) | Out-Null
[IO.File]::WriteAllText($manaReportPath, ($manaReport | ConvertTo-Json -Depth 9) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$manaReport | ConvertTo-Json -Depth 9
if (-not $manaOverallPass) { throw 'ManaCannon WAV validation failed.' }
