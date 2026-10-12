# 마나 대포 공격음 제작 — 2026-10-01

청색 마나를 모아 포탈에서 발사하는 **ManaShot**과 대상에서 마나가 수정·공기처럼 퍼지는 **ManaExplosion** 두 효과음을 제작했다. 고정 시드의 원본 DSP 합성이며 녹음·외부 샘플·추가 라이브러리를 사용하지 않았다.

사용자가 요청한 마나 폭발음을 별도로 제공한다. 폭발음에는 저음 타격·육체 피격·둔탁한 충돌음을 넣지 않았다. 기존 공통 UnitHit와 다른 소리이며 공통 피해음은 이 제작 작업에서 변경하지 않았다.

| 파일 | 길이 | PCM 샘플 수 | 실제 피크 | RMS | 제안 OneShot gain |
|---|---:|---:|---:|---:|---:|
| ManaShot.wav | 0.400초 | 17,640 | −19.99947dBFS | −36.10555dBFS | 0.45 |
| ManaExplosion.wav | 0.350초 | 15,435 | −18.99913dBFS | −38.35582dBFS | 0.40 |

두 파일 모두 **44,100Hz / mono / PCM16 WAV**다. ManaShot의 컨테이너는 35,324바이트, ManaExplosion은 30,914바이트다.

## 소리와 재생 시점

ManaShot은 약 0.30초 동안 음높이가 오르는 마나 충전음·가벼운 숨결을 쌓은 다음 짧은 포탈 바람으로 발사한다. 최고 피크는 시작 후 약 0.30923초다. Attack의 충전 시작에 한 번 재생하고 0.300초의 발사 부분을 Attack 10fps의 frame 3 포탈 발사 시점에 맞췄다.

ManaExplosion은 마나탄이 대상에 도달할 때 한 번 재생한다. 0.35초 안에서 공기와 수정 잔향이 부드럽게 흩어진다. 첫 약 0.015초에서 피크에 도달하고 빠르게 줄어든다. 소리가 골드·피해·턴 판정을 변경하지 않는다.

위 gain은 조용한 첫 적용 값이다. 기존 AudioListener.volume·음소거 설정을 따르는 2D AudioSource에서 사용하며 마스터 음량을 소스에 다시 곱하지 않는다. 실제 게임 믹스 청취 결과를 확인했다고 주장하지 않으며 최종 연출 연결과 native 임포트는 통합 작업이 담당한다.

## 출력과 도구

- WAV: `TempBuild/ManaCannonPresentation_20261001/Export/Resources/Project333/Units/ManaCannon/Audio/{ManaShot,ManaExplosion}.wav`
- Resource key: `Project333/Units/ManaCannon/Audio/ManaShot`, `Project333/Units/ManaCannon/Audio/ManaExplosion`
- 분석·재현성·해시: `TempBuild/ManaCannonPresentation_20261001/Export/AudioReports/`
- 합성기: `Tools/Art/ManaCannonAudio.cs`
- 생성 스크립트: `Tools/Art/GenerateManaCannonAudio.ps1`
- 독립 검증기: `Tools/Art/ValidateManaCannonAudio.ps1`

짧은 효과음의 native 임포트에는 PCM / Decompress On Load / Preserve Sample Rate / preload를 사용한다. 이미 mono인 원본의 forceToMono와 background load는 필요하지 않다. importer·GUID는 Unity API로 등록하며 이 도구는 Assets 또는 `.meta`에 쓰지 않는다.

생성·검증 스크립트는 프로젝트 Assets 아래 출력과 기존 보고서 덮어쓰기를 거부한다. 합성기는 기존 WAV도 덮어쓰지 않는다. 다른 출력·보고서 디렉터리를 지정해 다시 생성한다.

## 검증

독립 PowerShell RIFF chunk parser가 헤더·fmt·data 길이, PCM 형식과 재생 길이를 검사했다. 디코딩 PCM에서 피크·RMS·DC·클리핑과 25ms 구간 RMS를 별도로 계산했다. 두 번째 PowerShell 프로세스로 다시 합성한 두 WAV를 다시 분석한 뒤 첫 생성본과 바이트 해시가 같음을 확인했다.

- 클리핑: 두 파일 모두 0샘플.
- DC: ManaShot −0.0000000536, ManaExplosion +0.0000000929.
- 시작·끝 64샘플과 해당 인접 샘플 변화: 모두 0.
- 실제 첫·마지막 비영점 값: 모두 1 LSB. 64샘플 무음 이후 fade-in과 마지막 fade-out으로 연결한다.
- 피크·RMS·경계·재생 길이·재현성 검사 모두 통과했다.
- 기기·전체 게임 믹스 청취, 앱 빌드·설치, 주 Unity Editor 조작이나 서버 배포는 이 오디오 작업에서 수행하지 않았다.

SHA-256:

- ManaShot: `20bffa19501e4f71ed64f23a382b8ea763dc45998dc4bbe4e8fdf9fd078bac9f`
- ManaExplosion: `1594fbf8573490e14f1617c888d4115f62ed6ecb680cf4448d93209d8c1d98d4`

## 재현

처음 생성할 때 `C:/Project_333`에서 다음 명령을 사용한다. 실행 정책 옵션은 해당 PowerShell 프로세스에만 적용한다.

`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/GenerateManaCannonAudio.ps1`

`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Art/ValidateManaCannonAudio.ps1`

이미 기본 출력이 있으면 생성기에는 새 `-OutputDirectory`·`-ReportDirectory`를, 검증기에는 같은 음원의 `-AudioDirectory`와 새 `-ReportDirectory`를 지정한다. 두 번째 결과의 WAV SHA를 위 해시와 비교한다.