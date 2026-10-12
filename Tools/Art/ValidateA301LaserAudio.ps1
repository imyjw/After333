param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$AudioPath,
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
if (-not $AudioPath) { $AudioPath = Join-Path $ProjectRoot 'Assets/Project333/Resources/Project333/Units/A-301/Audio/LaserShot.wav' }
if (-not $ReportPath) { $ReportPath = Join-Path $ProjectRoot 'TempBuild/A301Presentation_20260930/Audio/A301LaserAudioIndependentValidation.json' }
$stream = [System.IO.File]::OpenRead($AudioPath)
$reader = New-Object System.IO.BinaryReader($stream)
try {
    $ascii = [System.Text.Encoding]::ASCII
    if ($ascii.GetString($reader.ReadBytes(4)) -ne 'RIFF') { throw 'Missing RIFF header.' }
    $riffSize = $reader.ReadUInt32()
    if ($ascii.GetString($reader.ReadBytes(4)) -ne 'WAVE') { throw 'Missing WAVE header.' }
    $format = $null
    $pcmBytes = $null
    while ($stream.Position + 8 -le $stream.Length) {
        $chunk = $ascii.GetString($reader.ReadBytes(4))
        $size = $reader.ReadUInt32()
        $end = $stream.Position + $size
        if ($end -gt $stream.Length) { throw 'Truncated RIFF chunk.' }
        if ($chunk -eq 'fmt ') {
            if ($size -lt 16) { throw 'Invalid fmt chunk.' }
            $format = [ordered]@{
                encoding = $reader.ReadUInt16()
                channels = $reader.ReadUInt16()
                sampleRate = $reader.ReadUInt32()
                byteRate = $reader.ReadUInt32()
                blockAlign = $reader.ReadUInt16()
                bitsPerSample = $reader.ReadUInt16()
            }
        } elseif ($chunk -eq 'data') {
            $pcmBytes = $reader.ReadBytes($size)
        }
        $stream.Position = $end + ($size % 2)
    }
    if ($riffSize + 8 -ne $stream.Length) { throw 'RIFF size mismatch.' }
    $containerBytes = $stream.Length
} finally { $reader.Dispose() }
if ($null -eq $format -or $null -eq $pcmBytes) { throw 'Missing WAV chunks.' }
if ($format.encoding -ne 1 -or $format.channels -ne 1 -or $format.sampleRate -ne 44100 -or $format.bitsPerSample -ne 16 -or $format.byteRate -ne 88200 -or $format.blockAlign -ne 2) { throw 'Unexpected PCM format.' }
if ($pcmBytes.Length % 2 -ne 0) { throw 'Incomplete PCM sample.' }
$sampleCount = $pcmBytes.Length / 2
[System.Int16[]]$samples = New-Object 'System.Int16[]' $sampleCount
[System.Buffer]::BlockCopy($pcmBytes, 0, $samples, 0, $pcmBytes.Length)
$sum = 0.0
$squares = 0.0
$peak = 0
$clipped = 0
$first64Peak = 0
$last64Peak = 0
$first64Step = 0
$last64Step = 0
$peakIndex = 0
for ($i = 0; $i -lt $sampleCount; $i++) {
    $value = [int]$samples[$i]
    $absolute = [Math]::Abs($value)
    $sum += $value
    $squares += [double]$value * $value
    if ($absolute -gt $peak) { $peak = $absolute; $peakIndex = $i }
    if ($value -eq -32768 -or $value -eq 32767) { $clipped++ }
    if ($i -lt 64) { $first64Peak = [Math]::Max($first64Peak, $absolute) }
    if ($i -ge $sampleCount - 64) { $last64Peak = [Math]::Max($last64Peak, $absolute) }
    if ($i -gt 0) {
        $step = [Math]::Abs($value - [int]$samples[$i - 1])
        if ($i -lt 64) { $first64Step = [Math]::Max($first64Step, $step) }
        if ($i -ge $sampleCount - 64) { $last64Step = [Math]::Max($last64Step, $step) }
    }
}
$dc = $sum / $sampleCount / 32768.0
$seconds = $sampleCount / [double]$format.sampleRate
$peakDbfs = 20 * [Math]::Log10($peak / 32768.0)
$rmsDbfs = 10 * [Math]::Log10($squares / $sampleCount / (32768.0 * 32768.0))
$endpointCheck = $samples[0] -eq 0 -and $samples[$sampleCount - 1] -eq 0
$edgeCheck = $first64Peak -lt 328 -and $last64Peak -lt 328 -and $first64Step -lt 164 -and $last64Step -lt 164
$passed = $clipped -eq 0 -and $endpointCheck -and $edgeCheck -and [Math]::Abs($dc) -lt 0.001 -and [Math]::Abs($seconds - 0.26) -lt 0.000001 -and [Math]::Abs($peakDbfs + 14) -lt 0.02 -and [Math]::Abs($rmsDbfs + 25) -lt 0.3
$report = [ordered]@{
    file = [System.IO.Path]::GetFullPath($AudioPath)
    validation = 'Independent RIFF chunk and decoded PCM analysis; no numpy/scipy or synthesizer helper used.'
    format = $format
    containerBytes = $containerBytes
    pcmBytes = $pcmBytes.Length
    samples = $sampleCount
    durationSeconds = $seconds
    peakDbfs = $peakDbfs
    rmsDbfs = $rmsDbfs
    dcOffset = $dc
    peakTimeSeconds = $peakIndex / [double]$format.sampleRate
    clippedSamples = $clipped
    firstSample = $samples[0]
    lastSample = $samples[$sampleCount - 1]
    boundary64Samples = [ordered]@{
        firstPeak = $first64Peak / 32768.0
        lastPeak = $last64Peak / 32768.0
        firstMaxAdjacentStep = $first64Step / 32768.0
        lastMaxAdjacentStep = $last64Step / 32768.0
        passed = $edgeCheck
    }
    sha256 = (Get-FileHash -LiteralPath $AudioPath -Algorithm SHA256).Hash.ToLowerInvariant()
    passed = $passed
    listening = 'No device/in-game listening validation; file and waveform checks only.'
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ReportPath))) | Out-Null
[System.IO.File]::WriteAllText($ReportPath, ($report | ConvertTo-Json -Depth 6) + [Environment]::NewLine, $utf8)
$report | ConvertTo-Json -Depth 6
if (-not $passed) { throw 'A-301 WAV validation failed.' }
