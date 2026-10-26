# A-212 애니메이션·전기톱 연출

## 현재 연결 메타데이터 — 2026-10-20

현재 카드가 참조하는 Controller/clip을 정적으로 다시 읽은 값이다. 아래10월1일 등 날짜별 제작·검증 값은 당시 기록이며 프레임 픽셀/전체 모션을 오늘 재검증한 것으로 해석하지 않는다. [전체 유닛 현재 값](unit_animation_snapshot.md).

| 상태 | Sprite 키 / Samples / StopTime | Loop |
|---|---|---|
| Attack | 8 / 8fps / 1초 | 끔 |
| BeAttacked | 3 / 12fps / 0.25초 | 끔 |
| Death | 8 / 8fps / 1초 | 끔 |
| Drained | 3 / 12fps / 0.25초 | 켬 |
| Idle | 3 / 12fps / 0.25초 | 켬 |
| Run | 8 / 12fps / 0.666667초 | 켬 |

현재 기본 ATK/HP는6/30, Lv.13은7/42다. 전력4·전력 소모2·4연타 규칙과 공통 피격음은 유지한다.

2026-09-30 제작. 기존의 건메탈·올리브색 로봇, 붉은 눈, 긴 팔과 양손 원형 전기톱을 유지하면서 Attack·Run·Death만 새로 제작했다. Idle·BeAttacked와 원래 카드 능력치는 보존했다.

## 현재 소스 대조 — 2026-10-01

Attack·Run·Death의 현재 native 키 수·Samples·StopTime·Loop Time은 아래 제작 표와 일치한다. 현재 Idle은 3컷/12fps/0.25초 반복, BeAttacked는 3컷/12fps/0.25초 비반복이다. 최초 제작 때 보존한 파일과 이후 수동 편집 파일을 구분하며 현재 Assets를 우선한다. SawAttack은 아래 추가 음량 감소까지 반영한 사용자 녹음 편집본이고 실제 피해에는 교체 가능한 공통 HitSoundSettings를 사용한다.

## 모션과 편집 파일

| 모션 | 동작 | 컷 수 | 규격 | 속도·반복 |
| --- | --- | ---: | --- | --- |
| Attack | 준비 → 톱날을 번갈아 밀어 넣기 → 양손으로 갈기 → 회수 | 8 | 각 512×512px | 8fps, 1초, 반복 끔 |
| Run | 다리를 번갈아 내딛으며 전기톱 팔을 움직이기 | 8 | 각 512×512px | 12fps, 약 0.667초, 반복 켬 |
| Death | 붉은 눈이 꺼짐 → 무릎을 꿇음 → 금속 몸체가 쓰러짐 | 8 | 각 512×512px | 8fps, 1초, 반복 끔 |

실제 게임 편집 대상은 `Assets/Unit/A-212/{Attack,Run,Death}.png`다. 각 시트는 가로 한 줄 **4096×512px**, 각 컷의 하단 투명 여백은 **16px**다. 무기·프레임에 포함된 스파크도 셀 경계 안에 들어간다. 모션 하나의 전체 컷에 공통 배율을 적용해 쓰러지거나 다리를 굽힐 때 캐릭터를 다시 키우지 않았다. 원래 Idle의 약 295px 높이를 기준으로 제작했으며 공격의 넓은 팔 동작은 512px 셀에 들어가도록 약간 조정했다. 게임의 유닛 표시 배율은 변경하지 않았다.

`PNG → 해당 .anim → A-212.controller → A-212.asset의 Board Animator Controller`로 재생한다. 기존 컨트롤러 GUID `c9d0ec2e25d93f1448856b0001a54768`, PNG·클립 GUID와 기존에 유지되는 Sprite 이름의 fileID를 보존했다. Idle·BeAttacked·컨트롤러와 카드 파일은 바이트 단위로 유지했다. 별도 Resources 시트로 캐릭터 모션을 우회하지 않는다.

같은 컷 수로 그림을 교체할 때는 위 PNG를 수정하고 512px 격자·투명 배경·약 16px 접지 여백을 유지한다. `.meta`를 삭제하지 않는다. 컷 수를 변경할 때는 Unity Sprite Editor에서 분할 후 `.anim`의 키프레임을 편집한다. 이번 제작의 일회성 임포트 도구를 수동 편집한 그림 위에 다시 실행하지 않는다.

## VFX·효과음

- VFX: `Assets/Project333/Resources/Project333/Units/A-212/SawImpactAtlas.png`. 2048×1024px의 4열×2행 시트, 각 셀 512×512px. 겹친 톱날 자국과 황금색 금속 스파크가 튀고 흩어진다.
- 공격음: `Assets/Project333/Resources/Project333/Units/A-212/Audio/SawAttack.wav`. 사용자가 제공한 원형 전기톱 녹음의 3.02~3.50초 구간으로 교체한 **0.480초 mono PCM16 / 44,100Hz** WAV. 원음의 높이·속도를 유지하고 앞 20ms·뒤 45ms 페이드를 적용했다. 이후 사용자 요청으로 3.5kHz 고음역을 넓게 4dB 완화하고 전체 음량 감쇠를 6dB로 적용했다(첫 완화본보다 추가 4dB 감소). 공격/반격 한 동작의 접촉 시작에 gain 0.30으로 한 번 재생한다.
- 공통 피격음은 기존 `Assets/Project333/Resources/Project333/Audio/Common/UnitHit.wav`를 계속 사용한다. 고유 피격·달리기·사망음은 추가하지 않았다.
- VFX는 기존 공통 피격 지점의 하향 오프셋을 사용한다. 시점마다 대상의 실제 표시 위치를 따라가며 UI 클릭을 가로막지 않는다. 같은 슬롯의 연출 교체·취소·씬 비활성화·소환물 교체 시 효과와 공격음을 정리한다. 공격자·반격자용 슬롯을 분리한다.

## 전투 타이밍과 판정

근접 접근에서 Run을 재생한다(최소 0.28초). Attack의 준비 2컷 뒤, 시작 후 0.25초부터 0.125초 간격으로 기존 4연타의 각 타격에 스파크·피해 숫자·공통 피격음이 맞춰진다. 전기톱 모터음은 4회 겹치지 않고 한 번 재생된다. 회수 포즈까지 재생하고 Run으로 원위치에 복귀한다(최소 0.22초). Death는 등록된 클립 전체 길이를 기다린다.

피해 수치·타격 횟수·반격 자격·사망 판정은 기존 전투 상태/로그에서 받는다. 연출 코드가 체력이나 공격 횟수를 변경하지 않는다. 망각으로 n연타가 사라지면 기존 `EffectiveHitsPerAttack`의 1타를 사용한다. 공격을 시작한 유닛은 반격 피해로 Attack 모션을 BeAttacked로 교체하지 않는다.

구현: `A212PresentationAssets.cs`, `A212SawAttack.cs`, `TileTextView.cs`, `BattleBootstrapper.cs`(모두 `Assets/Project333/Runtime/Presentation/Battle`).

## 제작·보관과 검증

- 생성 원본: `Tools/Art/Source/A-212/*-Generated-20260930.png`.
- 이미지 프롬프트: `Tools/Art/A212Prompts.json`.
- 시트 정리 도구: `Tools/Art/PackA212Sprites.cs` 및 `PackA212Sprites.ps1`. 기본 출력은 Assets 밖의 `TempBuild/A212SpriteExport`이며 현재 게임 그림을 덮어쓰지 않는다. 생성 원본의 완전한 포즈를 추출하고 동일 모션 공통 배율로 512px 셀에 배치한다. 거의 투명한 생성 흔적과 고립된 픽셀 잡음을 제거한다. 셀을 넘친 그림을 단순 잘라내지 않는다.
- Unity 임포트: `Tools/Art/ImportA212Presentation.cs`. 별도 Editor에서 Sprite Data Provider의 편집 capability를 먼저 확인한 뒤 분할·클립·텍스처·오디오 설정을 갱신한다. 스프라이트 `.meta`를 직접 작성하지 않는다.
- 녹음 기반 음원 편집·독립 검사: `Tools/Art/PrepareA212SawAttackFromRecording.py`, `ValidateA212SawAttackAudio.ps1`, [음원 제작 기록](../Tools/Art/A212SawAttackAudio.md). 이전 합성 도구는 보관하며 Assets 밖으로만 내보낸다.
- 교체 전 폴더: `Tools/Art/Archive/A212BeforePresentation_20260930.zip`. 기존 `.aseprite`는 게임 폴더에 그대로 남아 있다.
- 미리보기: `Tools/Art/Preview/A212ContactSheet.png`.
- 검증 증거: `TempBuild/A212Presentation_20260930/`의 임포트·프레임 포장·원본 해시 보존·Unity 재생 검사 결과.

전체 런타임 및 테스트 소스 컴파일을 검사했다. 새 24컷의 크기·셀 내부 범위·하단 여백, Unity의 8개 Sprite 참조와 클립 길이, VFX 8셀과 PCM 오디오 가져오기를 확인했다. 음원 독립 RIFF/PCM 검사·클리핑 0·시작/끝 경계·결정적 재생성 해시도 통과했다.

사용자의 전체 전투씬·휴대폰 픽셀/청취 검증과 클라이언트 빌드·설치는 별도다. 이 작업에서 서버 변경·배포·앱 빌드·설치·커밋·푸시는 수행하지 않는다. 휴대폰 반영에는 사용자가 최신 클라이언트를 빌드·설치해야 한다.

### 최종 재생 검증 — 2026-09-30

별도 Unity 6000.3.12f1에서 **12/12 검사 통과**. A-212 7개 검사와 블루드래곤·광마·레드드래곤·컨트롤러 교체 5개 회귀 검사다. 실제 Animator의 Run→Attack→Idle, 양쪽 스파크와 4+4 피해 숫자, 공통 피격음 경로, 취소·대상 교체·마지막 yield 경계, 1타의 완전한 전기톱음 꼬리, Death 1초 유지, 실제 체력 불변을 확인했다.

첫 재생 검사에서 이동 시작 이전의 프레임 지연이 접근 시간에 포함될 수 있음을 발견했다. 공통 이동을 시작 시각 이후 경과시간으로 수정하고 첫 tick의 원점·후속 이동·비활성화 후 마지막 위치 보정 차단을 직접 검증했다. 0.12초 스파크의 0.125초 반복 사이를 고정 시간으로 샘플링하던 검사는 실제 Attack 상태에서 양쪽 스파크와 8개 피해 숫자를 함께 관찰하도록 보강했다.

증거는 `unity-results-complete.xml`, `unity-tests-complete.log`, `asset-preservation-validation.json`, `tested-file-hashes.json`, `reproduction-validation.json`, `Captures/`다(모두 `TempBuild/A212Presentation_20260930`). 실제 적용된 3개 모션과 네 런타임 파일은 재생 검사 입력과 해시가 같다. PNG 재현도 원래 포장본과 해시가 같다. 검증 전용 프로젝트에는 외부 광고 SDK가 없으므로 그 프로젝트에서만 LevelPlay 어댑터를 검사 입력에서 제외했다. 실제 프로젝트의 광고 코드·패키지는 변경하지 않았다.

검증 캡처에서 TMP 피해 팝업의 재질이 분홍색으로 표시되어, 숫자는 생성된 TMP_Text의 값과 개수로 검증했다. 캐릭터·스파크 재생과 위치는 캡처로 확인했으며 실제 전투씬의 글꼴·재질 검수는 별도다. 이 검증 환경 차이 때문에 실제 게임의 팝업 재질을 수정하지 않았다.

### 사용자 제공 원형 전기톱 녹음으로 교체 — 2026-09-30

`bfcmusic-circular-saw-2-fx-325571.mp3`의 3.02~3.50초를 기존 SawAttack 경로에 적용했다. 클리핑 0, 시작·끝 0, RIFF/PCM 및 경계 검사 통과. 반복 처리·적용 파일의 SHA-256이 같고 오디오 `.meta`와 GUID도 유지했다. 기존 모션·VFX·재생 코드는 수정하지 않았다.

위 12/12 Unity 검사는 이 음원 교체 전의 연출 검사다. 이번에는 동일 경로·형식·길이의 WAV 파일 교체에 필요한 오디오 검사만 수행했으며 휴대폰 청취·클라이언트 빌드는 수행하지 않았다. 검증 증거: `TempBuild/A212SawRecording_20260930/`.

### 전기톱음 고음·음량 완화 — 2026-09-30

같은 녹음·0.480초 길이를 유지하면서 날카로운 고음역과 전체 음량을 낮췄다. 새 WAV의 peak -9.893dBFS / RMS -21.159dBFS, 이전 녹음본 대비 RMS -5.494dB다. 필터 뒤 재정규화하지 않아 실제 음량 감소를 유지한다. 재생 gain 0.30과 `.meta`·GUID는 그대로다. RIFF/PCM·클리핑 0·시작/끝 경계·반복 처리 해시 일치 확인. 증거: `TempBuild/A212SawSoftened_20260930/`. Unity·앱 빌드·실제 기기 청취는 추가 실행하지 않았다.

### 전기톱 전체 음량 추가 감소 — 2026-09-30

사용자 요청으로 이전 완화본보다 전체 음량을 추가 4dB 낮췄다. 고음 EQ·길이·타이밍과 gain 0.30은 유지했다. 현재 peak -13.893dBFS / RMS -25.159dBFS. 기존 `.meta` 유지와 PCM 검사 확인; 증거 `TempBuild/A212SawQuieter_20260930/`.
