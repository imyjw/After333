# TimedBomb 폭발 효과음 — 2026-09-30

빨간 다이너마이트와 디지털 타이머를 사용하는 시한폭탄의 폭발에 한 번
재생하는 오리지널 DSP 효과음이다. 짧은 압력 burst와 낮은 몸통이 단단하게
울린 뒤, 짧고 부드러운 잔향이 자연히 감쇠한다. Nuclear의 긴 저음 rumble보다
짧고 건조한 구성이다. 외부 녹음·샘플·생성 모델은 사용하지 않았다.

| 항목 | 값 |
|---|---|
| 실제 편집·교체 파일 | `Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Audio/TimedBombExplosion.wav` |
| Resources 키 | `Project333/SpellEffects/TimedBomb/Audio/TimedBombExplosion` |
| 포맷 | mono PCM16 WAV / 44,100 Hz |
| 길이·샘플 수 | 0.960초 / 42,336 samples |
| 파일 크기 | 84,716 bytes |
| peak / RMS | −14.000 / −32.277 dBFS |
| 페이드 | 16ms raised-cosine attack / 120ms raised-cosine release |
| 권장 one-shot gain | 0.30, 기존 사용자 음량 설정과 함께 적용 |
| SHA-256 | `c91749acfa0413d2499aa8f532b78bb427fe96c83b3e33fdc91b29ccae729d5d` |

## 소리 구성

- 105~1,350Hz의 압력 burst를 72ms decay로 빠르게 줄이고, 작은
  260~1,850Hz 공기층은 40ms decay를 사용한다.
- 연속 위상의 177→82Hz 몸통과 96→74Hz의 짧은 저음 tail을 사용한다.
  몸통은 145ms, tail은 250ms decay이며 낮게 필터링한 작은 압력층을 섞는다.
- 60Hz high-pass로 초저음을 줄이고 1,900Hz low-pass로 날카로운 고음을
  억제한다. 타이머 beep나 tick 대신 폭발 효과음만 생성했다.
- 고정 seed `333093036`으로 재현된다. 최종 페이드를 적용한 채 DC를 제거하고,
  파일 최대 음량을 −14dBFS로 제한한다.

## 검증 기록

별도의 PowerShell RIFF/PCM parser로 실제 sample을 검사했다.
`TimedBombExplosionAudioValidation.json`은 합성/주파수 수치,
`TimedBombExplosionAudioIndependentValidation.json`은 독립 검사 결과다.

- PCM encoding1·mono·16bit·44,100Hz, byteRate88,200·blockAlign2와 RIFF 크기 일치.
- clipped samples **0**, 첫·마지막 PCM 값 **0**, DC 평균 **0.00000006055**.
- 첫 64 sample 최대 진폭 **0.00189209**, 최대 인접 변화 **0.00015259**;
  마지막 64 sample은 모두 0이며 양끝 경계 검사 통과.
- 전체 최대 인접 변화 **0.01779175**, peak 시점 **39.501ms**.
- 첫 200ms RMS **−26.193dBFS** → 마지막 200ms **−57.680dBFS**.
  tail 감쇠 검사 통과.
- 45Hz 미만 에너지 비율 **0.050888%**, 4kHz 초과 **0.00004193%**.
- 별도 `Prepared`/`Reproduced` 출력과 실제 asset의 SHA-256이 일치한다.
- `--output`을 Assets 내부로 지정하면 파일을 쓰기 전에 거부한다.

증거 위치: `TempBuild/TimedBombPresentation_20260930/Audio/`.
위 결과는 파일과 waveform 검사이며 기기·게임 내 청취 판단을 의미하지 않는다.
이 작업은 신규 음원·exporter·검사기에 한정되며 기존 Nuclear/A-212/A-301/UnitHit,
전투 코드와 문서는 수정하지 않았다. Unity·서버 실행과 앱 빌드·설치는 하지 않았다.

## 재생성·교체

`C:/Project_333`에서 설치된 Python3.13/numpy/scipy로 실행한다.

```powershell
& "$env:LOCALAPPDATA/Programs/Python/Python313/python.exe" Tools/Art/PrepareTimedBombExplosionAudio.py
```

기본 출력은 Unity Assets 밖 `TempBuild/TimedBombExplosionAudioExport`다.
exporter는 실제 음원이나 `.meta`를 자동 교체하지 않는다. 검토한 파일을
위 실제 편집 파일에 복사하고 Unity가 생성한 `.meta`는 보존한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateTimedBombExplosionAudio.ps1
```

권장 AudioImporter 설정은 PCM / Decompress On Load / Preserve Sample Rate /
Preload Audio Data다. 새로운 metadata와 importer 설정은 최종 Unity import
단계에서 생성한다. 교체한 음원은 Play Mode를 다시 시작해 캐시를 갱신하고,
휴대폰에는 사용자가 새 클라이언트를 빌드·설치해야 반영된다.
