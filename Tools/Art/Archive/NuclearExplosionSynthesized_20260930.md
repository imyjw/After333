# Nuclear 폭발 효과음 — 2026-09-30

버섯구름 폭발 시작에 한 번 재생하기 위한 오리지널 DSP 효과음이다. 짧고
둥근 압력 폭발음 뒤에 절제된 저음 rumble이 남고 1.68초에 자연스럽게
끝난다. 외부 녹음·샘플·생성 모델을 사용하지 않았다. 기존 공통 UnitHit와
다른 캐릭터 효과음은 수정하지 않았다.

| 항목 | 값 |
|---|---|
| 실제 편집·교체 파일 | `Assets/Project333/Resources/Project333/SpellEffects/Nuclear/Audio/NuclearExplosion.wav` |
| Resources 키 | `Project333/SpellEffects/Nuclear/Audio/NuclearExplosion` |
| 포맷 | mono PCM16 WAV / 44,100 Hz |
| 길이·샘플 수 | 1.680초 / 74,088 samples |
| 파일 크기 | 148,220 bytes |
| peak / RMS | −12.000 / −29.298 dBFS |
| 페이드 | 20ms raised-cosine attack / 180ms raised-cosine release |
| 권장 one-shot gain | 0.30, 기존 사용자 음량 설정과 함께 적용 |
| SHA-256 | `2a01a9f3f70356f7218060117b07ca660fecd700c1f0518e5773da2c137847de` |

## 소리 구성

- 90~1,050Hz의 짧은 압력 burst와 더 작은 180~1,650Hz 공기층을 빠르게
  감쇠시킨다. 날카로운 crack이나 오랫동안 지속되는 white-noise 층은 없다.
- 연속 위상의 208→83Hz 몸통과 95→58Hz의 둥근 저음 tail을 사용한다.
  작은 배음과 낮게 필터링한 압력층으로 잔향을 보완한다.
- 48Hz high-pass로 과도한 초저음을 줄이고 2,100Hz low-pass로 고음의
  날카로움을 억제한다. 모든 합성이 고정 seed `3330930`으로 재현된다.
- 마지막에는 같은 페이드로 DC를 제거한다. 파일 최대 음량을 −12dBFS로
  제한하므로 고음량 transient나 clipping이 없다.

## 검증 기록

합성기와 별개의 PowerShell RIFF/PCM parser로 헤더와 실제 sample을 검사했다.
`NuclearExplosionAudioValidation.json`은 합성/주파수 수치,
`NuclearExplosionAudioIndependentValidation.json`은 독립 검사 기록이다.

- PCM encoding1·mono·16bit·44,100Hz, byteRate88,200·blockAlign2와 RIFF 크기 일치.
- clipped samples **0**, 첫·마지막 PCM 값 **0**, DC 평균 **0.00000003213**.
- 첫 64 sample 최대 진폭 **0.00256348**, 최대 인접 변화 **0.00012207**;
  마지막 64 sample은 모두 0이며 양끝 경계 검사 통과.
- 전체 최대 인접 변화 **0.01553345**, peak 시점 **22.154ms**.
- 첫 200ms RMS **−21.733dBFS** → 마지막 200ms **−54.131dBFS**.
  끝으로 갈수록 작은 음량이 되며 tail 감쇠 검사 통과.
- 45Hz 미만 에너지 비율 **0.023038%**, 4kHz 초과 **0.00001599%**.
- 별도 `Prepared`/`Reproduced` 출력 및 실제 asset SHA-256이 일치한다.
- `--output`을 Assets 내부로 지정하면 파일을 쓰기 전에 거부한다.

증거 위치: `TempBuild/NuclearExplosionPresentation_20260930/Audio/`.
위 결과는 파일과 waveform 검사이며 기기·게임 내 청취 판단을 의미하지 않는다.
이 음원 작업에서 Unity 실행, 앱 빌드·설치 및 서버 작업은 하지 않았다.

## 재생성·교체

`C:/Project_333`에서 설치된 Python3.13/numpy/scipy로 실행한다.

```powershell
& "$env:LOCALAPPDATA/Programs/Python/Python313/python.exe" Tools/Art/PrepareNuclearExplosionAudio.py
```

기본 출력은 Unity Assets 밖 `TempBuild/NuclearExplosionAudioExport`다.
exporter는 실제 음원이나 `.meta`를 자동 교체하지 않는다. 검토한 파일을
위 실제 편집 파일에 복사하고, Unity가 생성한 `.meta`는 보존한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateNuclearExplosionAudio.ps1
```

권장 AudioImporter 설정은 PCM / Decompress On Load / Preserve Sample Rate /
Preload Audio Data다. 새로운 metadata와 importer 설정은 최종 Unity import
단계에서 생성한다. 캐시된 음원을 교체한 뒤에는 Play Mode를 다시 시작하고,
휴대폰에는 사용자가 새 클라이언트를 빌드·설치해야 반영된다.
