# Nuclear 폭발 효과음 — 녹음 교체, 2026-09-30

사용자가 제공한 `dragon-studio-nuclear-explosion-386181.mp3`의 폭발 시작 구간을 사용한다. 기존 합성 음원을 대체하며 추가 합성층·EQ·피치/속도 변경은 없다.

| 항목 | 값 |
|---|---|
| 실제 편집·교체 파일 | `Assets/Project333/Resources/Project333/SpellEffects/Nuclear/Audio/NuclearExplosion.wav` |
| Resources 키 | `Project333/SpellEffects/Nuclear/Audio/NuclearExplosion` |
| 보존 원본 | `Tools/Art/Source/Nuclear/Audio/dragon-studio-nuclear-explosion-386181.mp3` |
| 원본 포맷/길이 | stereo MP3 / 44,100Hz / 7.575510초 |
| 사용 구간 | 원본 0.000–1.680초 |
| 출력 | mono PCM16 WAV / 44,100Hz / 1.680초, 74,088샘플 |
| 페이드 | 20ms cosine attack / 180ms cosine release |
| peak / RMS | −16.505 / −29.298dBFS |
| one-shot gain | 기존 0.30; 사용자 AudioListener 음량을 한 번 적용 |
| WAV SHA256 | `a29550a587c767c4735c8e27e1400975b3d991a33dadbc40b7f0a7ff01e0582d` |
| 원본 SHA256 | `846997004ef3e6b8c2755d580f8c15b451b31e7ee441b1e296cfbba7afc66535` |

## 편집 이유와 재생

현재 `NuclearExplosionVfx`는 약1.7778초의 시전 연출 종료 시 AudioSource도 정지한다. 긴 원본을 그대로 넣으면 중간에 끊기므로 앞쪽 폭발 구간을1.68초로 편집하고 파일 안에서 끝 페이드를 완료했다. stereo를 mono로 평균하고 DC를 제거했다. 평균 음량은 이전 음원의 RMS −29.298dBFS에 맞추며 peak 상한 −12dBFS를 함께 적용한다. 이 녹음의 결과 peak는 −16.505dBFS다.

기존 WAV `.meta`/GUID `71837c37632fd614aaab8fb108b550f8`, PCM/Decompress On Load/Preserve Sample Rate/preload 설정을 보존했다. 런타임 재생 코드, 연출시간, 그림, gain0.30, 다른 공격/공통 UnitHit 음원은 변경하지 않았다. 음원이 캐시되므로 교체 뒤 Play Mode를 다시 시작한다. 휴대폰에는 사용자가 새 클라이언트를 빌드·설치해야 반영된다. 서버 업데이트는 필요 없다.

## 재현과 검증

`C:/Project_333`에서 설치된 Python3.13/numpy/soundfile로 실행한다.

```powershell
& "$env:LOCALAPPDATA/Programs/Python/Python313/python.exe" Tools/Art/PrepareNuclearExplosionFromRecording.py
```

기본 출력은 `TempBuild/NuclearExplosionRecordingExport`이며 Assets 내부 출력은 거부한다. exporter는 실제 WAV나 metadata를 자동으로 바꾸지 않는다. 검토한 WAV만 실제 경로에 복사하고 `.meta`는 보존한다. 과거 `PrepareNuclearExplosionAudio.py`는 이전 합성 음원 재현용으로만 남겨두었다.

```powershell
& ([scriptblock]::Create([IO.File]::ReadAllText('C:/Project_333/Tools/Art/ValidateNuclearExplosionRecording.ps1'))) -ProjectRoot 'C:/Project_333'
```

- 원본 파일/보존 복사본 SHA256 일치, 재생성한 WAV와 적용 WAV SHA256 일치.
- exporter와 별개의 RIFF/PCM 검사가 mono/PCM16/44.1kHz/74,088샘플, RIFF 크기와 byteRate/blockAlign을 확인했다.
- clipping0, 첫·마지막 sample0, 양끝64샘플 모두0, DC 평균 약7.0e−9, 지정 RMS/peak상한 검사 통과.
- 기존 Unity 음원 조건 mono/1.2~1.8초/peak<0.3도 파일 수치로 충족한다. 같은 포맷·참조의 WAV 교체여서 Unity 회귀검사는 반복하지 않았다. 기존 Unity 검사 결과는 교체 전 음원에 대한 결과다.
- 녹음의 내부 변화는 이전 DSP의 감쇠/주파수 모델과 다르므로 합성 음원 전용 `ValidateNuclearExplosionAudio.ps1`은 현재 파일의 검증기로 사용하지 않는다. 녹음 검사는 정확한 길이·포맷·음량·경계와 clipping을 확인한다.

현재 보고서는 `NuclearExplosionAudioValidation.json`, `NuclearExplosionAudioIndependentValidation.json`이다. 이전 합성 WAV·문서·두 보고서는 `Tools/Art/Archive/NuclearExplosionSynthesized_20260930*`에 보존했다. 이번 증거: `TempBuild/NuclearRecordedExplosion_20260930/`.

파일·waveform 검사이며 기기/게임 내 청취 검증은 아니다. 주 Editor/서버를 조작하거나 앱을 빌드·설치하지 않았다.
