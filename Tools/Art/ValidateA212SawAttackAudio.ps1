param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)
$ErrorActionPreference = 'Stop'
$audioPath = Join-Path $ProjectRoot 'Assets/Project333/Resources/Project333/Units/A-212/Audio/SawAttack.wav'
$stream = [System.IO.File]::OpenRead($audioPath)
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
} finally {
    $reader.Dispose()
}
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
$first64MaxStep = 0
$last64MaxStep = 0
$peakSampleIndex = 0
for ($i = 0; $i -lt $sampleCount; $i++) {
    $value = [int]$samples[$i]
    $absolute = [Math]::Abs($value)
    $sum += $value
    $squares += [double]$value * $value
    if ($absolute -gt $peak) { $peak = $absolute; $peakSampleIndex = $i }
    if ($value -eq -32768 -or $value -eq 32767) { $clipped++ }
    if ($i -lt 64) { $first64Peak = [Math]::Max($first64Peak, $absolute) }
    if ($i -ge $sampleCount - 64) { $last64Peak = [Math]::Max($last64Peak, $absolute) }
    if ($i -gt 0) {
        $step = [Math]::Abs($value - [int]$samples[$i - 1])
        if ($i -lt 64) { $first64MaxStep = [Math]::Max($first64MaxStep, $step) }
        if ($i -ge $sampleCount - 64) { $last64MaxStep = [Math]::Max($last64MaxStep, $step) }
    }
}
$dc = $sum / $sampleCount / 32768.0
$seconds = $sampleCount / [double]$format.sampleRate
$endpointCheck = $samples[0] -eq 0 -and $samples[$sampleCount - 1] -eq 0
# Limit checks concern boundary discontinuities, not the saw's intended grit at mid-clip.
$edgeCheck = $first64Peak -lt 328 -and $last64Peak -lt 328 -and $first64MaxStep -lt 164 -and $last64MaxStep -lt 164
$passed = $clipped -eq 0 -and $endpointCheck -and $edgeCheck -and [Math]::Abs($dc) -lt 0.001 -and [Math]::Abs($seconds - 0.48) -lt 0.000001
$report = [ordered]@{
    file = 'Assets/Project333/Resources/Project333/Units/A-212/Audio/SawAttack.wav'
    validation = 'Independent RIFF chunk and decoded PCM analysis'
    format = $format
    containerBytes = $containerBytes
    pcmBytes = $pcmBytes.Length
    samples = $sampleCount
    durationSeconds = $seconds
    peakDbfs = 20 * [Math]::Log10($peak / 32768.0)
    rmsDbfs = 10 * [Math]::Log10($squares / $sampleCount / (32768.0 * 32768.0))
    dcOffset = $dc
    peakTimeSeconds = $peakSampleIndex / [double]$format.sampleRate
    clippedSamples = $clipped
    firstSample = $samples[0]
    lastSample = $samples[$sampleCount - 1]
    boundary64Samples = [ordered]@{
        firstPeak = $first64Peak / 32768.0
        lastPeak = $last64Peak / 32768.0
        firstMaxAdjacentStep = $first64MaxStep / 32768.0
        lastMaxAdjacentStep = $last64MaxStep / 32768.0
        passed = $edgeCheck
    }
    sha256 = (Get-FileHash -LiteralPath $audioPath -Algorithm SHA256).Hash.ToLowerInvariant()
    passed = $passed
    listening = 'No device/in-game listening validation; this report validates file and waveform properties only.'
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
$reportPath = Join-Path $PSScriptRoot 'A212SawAttackAudioIndependentValidation.json'
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 6) + [Environment]::NewLine, $utf8)
$report | ConvertTo-Json -Depth 6
if (-not $passed) { throw 'A-212 WAV validation failed.' }