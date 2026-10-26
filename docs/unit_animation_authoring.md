# 소환물 애니메이션 편집

2026-10-20 현재 등록 유닛의 Controller→clip 키 수·Samples·StopTime·Loop Time은 [전체 연결 메타데이터](unit_animation_snapshot.md)에 다시 추출했다. 이 문서의 사용자 제작 규격은 유지하며 날짜별 옛 제작 값으로 사용자의 최신 Assets를 되돌리지 않는다.

2026-10-01 현재 소스·native 연결 대조. 소환물 모션은 PNG → Animation Clip → 카드에 등록한 Animator Controller 경로를 사용한다. 아래 날짜별 제작 표는 최초 제작 기록이며 현재 컷 수·Samples·Loop Time은 현재 `.anim`과 연결한 Controller가 결정한다.

## 공통 제작 기준 — 2026-09-27 사용자 확정

- **한 프레임은 512×512px**, 배경은 투명하게 제작한다.
- **각 컷의 최대 범위도 가로 512px·세로 512px를 넘지 않는다.** 캐릭터·무기·해당 컷에 함께 그린 효과까지 모두 셀 안에 들어가야 한다. 인접 프레임으로 그림이 넘어가거나 경계에서 잘리지 않도록 구도와 크기를 조정한다. 여러 컷을 합친 시트 전체는 512px보다 커도 되지만, 개별 컷의 크기와 그림 범위는 반드시 이 한도를 지킨다.
- **소환물의 아래쪽과 프레임 하단 사이에 약 16px의 투명 공백**을 둔다. 기존 생성 그림처럼 하단 공백을 크게 남기거나 캐릭터를 프레임의 세로 중앙에 배치하지 않는다.
- Idle·Attack·BeAttacked·Run·Death 모두 이 규격과 공통 발/접지 기준선을 사용한다. 의도된 들뜸·점프 등의 동작은 기준선에 대한 움직임으로 유지하며, 매 프레임의 최하단을 강제로 맞춰 동작을 지우지 않는다.
- 여러 컷을 시트로 내보낼 때도 각 512×512 셀을 기준으로 확인한다. 제작 프롬프트, 합성·변환 도구의 배치, 최종 PNG 검수에 같은 기준을 적용한다. 대기·공격·피격·이동·사망을 연결해서 재생할 때 불필요한 상하 위치 변화가 없는지 확인한다.
- 사용자는 광마와 블루드래곤의 애니메이션 PNG를 이미 이 기준으로 직접 수정했다고 알려왔다. 현재 Assets/Unit 폴더의 사용자 편집본을 보존한다. 과거 Tools/Art/Source 원본이나 변환 도구로 다시 덮어쓰지 않는다.
- 이 공통 제작 기준을 기록할 때는 기존 그림을 수정하지 않았다. 레드드래곤의 신규 제작에도 같은 기준을 적용한다. 아래 2026-09-26 모션 이전 및 검증 기록은 당시 상태이며 사용자 편집 이후의 블루드래곤·광마 PNG를 검증했다는 뜻이 아니다.

## 실제 연결

`Assets/Unit/<소환물>/<모션>.png` → `<모션>.anim` → `<소환물>.controller` → 카드 `.asset`의 **Board Animator Controller** → 전투 타일.

- 블루드래곤: `Assets/Unit/BlueDragon/BlueDragon.controller`, `Assets/Project333/ScriptableObjects/StarterTen/Cards/BlueDragon.asset`.
- 광마: `Assets/Unit/Gwangma/Gwangma.controller`, `Assets/Project333/ScriptableObjects/StarterTen/Cards/Gwangma.asset`.
- 레드드래곤: `Assets/Unit/RedDragon/RedDragon.controller`, `Assets/Project333/ScriptableObjects/StarterTen/Cards/RedDragon.asset`.
- A-212: `Assets/Unit/A-212/A-212.controller`, `Assets/Project333/ScriptableObjects/StarterTen/Cards/A-212.asset`.
- A-301: `Assets/Unit/A-301/A-301.controller`, `Assets/Project333/ScriptableObjects/StarterTen/Cards/A-301.asset`.
- 개방 방주: `Assets/Unit/GaebangChief/GaebangChief.controller`, `Cards/GaebangChief.asset`.
- 마나 대포: `Assets/Unit/ManaCannon/ManaCannon.controller`, `Cards/ManaCannon.asset`.
- 오크 전사·오크 광전사: `Assets/Unit/Orc/OrcWarrior.controller`를 공유한다. 둘의 현재 기본 ATK는 20이며 모션 배율과는 별개다.
- 오크 족장: `Cards/OrcChieftain.asset`은 현재 `Assets/Unit/OrcChieftian/OrcChieftian.controller`를 사용한다. `OrcChieftian`은 실제 폴더·파일 철자이며 CardId `OrcChieftain`과 구분한다. 최초 OrcWarrior 재사용 설명으로 현재 연결을 덮어쓰지 않는다.
- 엘프 장로(`ElfElder`): `ElfLongbowScout.controller`와 기존 엘프 장궁수 모션을 공유한다.
- 스켈레톤아처(`SkeletonArcher`): 현재 `Assets/Unit/Skeleton/Skeleton.controller`를 사용한다.
- 위 카드의 기존 컨트롤러 GUID와 등록을 유지한다. 캐릭터 ID를 보고 별도 Resources PNG를 잘라 우선 재생하던 경로는 제거했다.
- 실제 Play Mode에서는 공통 Animator 경로로 SpriteRenderer를 재생하고 UI Image에 현재 스프라이트를 전달한다. 비활성 오브젝트/에디터의 공통 클립 샘플러도 해당 카드 컨트롤러의 Animation Clip을 읽는다.
- 물대포·X자 검기·파이어브레스·전기톱 스파크·직선 레이저·소리·이동 연출은 전투 연출 코드가 담당한다. 피해·턴·반격·다중 타격의 판정은 기존 전투 게이트웨이가 결정한다.

## 현재 수동 편집 클립 요약 — 2026-10-01

아래 현재 표는 2026-10-01 `.anim`의 스프라이트 키 수·Samples·StopTime·Loop Time 및 카드/컨트롤러 참조를 읽어 확인한 값이다. PNG 픽셀·모든 게임 모션을 새로 실행 검증한 기록은 아니다. 이전 제작 표와 검사 결과는 당시 기록으로 유지하며, 현재 Assets 편집본이 재생 기준이다.

| 소환물 | Idle | Attack | BeAttacked | Run | Death |
|---|---|---|---|---|---|
| 블루드래곤 | 5컷/12fps/약0.417초 | 6컷/12fps/0.5초 | 6컷/12fps/0.5초 | Controller에 없음 | 8컷/12fps/약0.667초 |
| 광마 | 4컷/12fps/약0.333초 | 6컷/12fps/0.5초 | 8컷/12fps/약0.667초 | 6컷/12fps/0.5초 | 7컷/12fps/약0.583초 |
| 레드드래곤 | 4컷/12fps/약0.333초 | 6컷/12fps/0.5초 | 7컷/12fps/약0.583초 | 6컷/12fps/0.5초 | 7컷/12fps/약0.583초 |
| A-301 | 6컷/12fps/0.5초 | 6컷/12fps/0.5초 | 2컷/12fps/약0.167초 | Controller에 없음 | 8컷/12fps/약0.667초 |
| 오크 족장 | 2컷/12fps/약0.167초 | 4컷/12fps/약0.333초 | 2컷/12fps/약0.167초 | 4컷/12fps/약0.333초 | 5컷/12fps/약0.417초 |

광마는 Idle/Run만 Loop Time을 켜고 나머지 세 모션은 끈다. 2026-10-20 대조 당시 블루드래곤·레드드래곤·A-301·오크 족장의 위 연결 클립은 Loop Time이 켜져 있었다. 2026-10-26 확인한 오크 족장의 Attack은 Loop Time=0, Idle은 Loop Time=1이며 현재 사용자 자산을 보존한다. 공격·사망 처리의 대기/다음 상태 전환은 공통 전투 연출 코드가 담당한다. 블루드래곤·광마·레드드래곤은 `Drained.anim` 파일이 남아 있어도 현재 Controller에 Drained 상태가 없다. A-301의 방전 시각 상태는 기존 `Disabled`(8컷/12fps/약0.667초)를 사용한다.

## 블루드래곤·광마 이전 당시 규격 — 2026-09-26 기록

아래 표는 이전 당시 구성이다. 사용자가 나중에 편집한 현재 설정을 덮어쓰는 기준으로 사용하지 않는다. 2026-09-27 소스 확인 시 블루드래곤은 Idle 5컷·Death 8컷으로 바뀌었고, 컨트롤러에는 Run·Drained가 등록되어 있지 않다. 현재 프레임과 속도는 실제 PNG·클립·컨트롤러가 결정한다.

| 모션/상태 | 블루드래곤 | 광마 | 반복 |
| --- | --- | --- | --- |
| Idle | 원래 그림 4컷,12fps,2048×512 | 생성 그림 6컷,6fps,3072×512 | 켬 |
| Attack | 6컷,12fps,3072×512 | 6컷,12fps,3072×512 | 끔 |
| BeAttacked | 6컷,15fps,3072×512 | 6컷,15fps,3072×512 | 끔 |
| Run | 6컷,12fps,3072×512 | 6컷,12fps,3072×512 | 켬 |
| Death | 6컷,8fps,3072×512 | 6컷,8fps,3072×512 | 끔 |
| Drained | Idle의 첫 스프라이트 1컷 고정 | Idle의 첫 스프라이트 1컷 고정 | 켬 |

이전 당시 캐릭터 컷은 512×512px로 분할했다. 당시 블루드래곤의 기존 `Idle.png`, `Idle.anim` 및 두 `.meta` 파일은 변경하지 않았다. 당시 추가한 `Drained.anim`은 새 그림 없이 Idle 첫 스프라이트를 참조했다. 파일이 남아 있더라도 현재 컨트롤러에 등록되어 있는지 별도로 확인한다.

## 레드드래곤 신규 모션 — 2026-09-27

| 모션 | 컷 수 | 프레임당 규격 | 속도 | 반복 |
| --- | ---: | --- | ---: | --- |
| Idle | 6 | 512×512px | 6fps | 켬 |
| Attack | 6 | 512×512px | 12fps | 끔 |
| BeAttacked | 6 | 512×512px | 15fps | 끔 |
| Run | 6 | 512×512px | 12fps | 켬 |
| Death | 6 | 512×512px | 8fps | 끔 |
| Drained | Idle 첫 컷 고정 | 같은 Idle 스프라이트 | 1fps | 켬 |

다섯 PNG는 각각 3072×512px다. 생성된 포즈 전체를 추출한 다음, 30컷에 동일한 배율을 적용해 셀 안에 넣었다. 하단 여백은 약 16px이며 런타임 추가 표시 배율은 다른 소환물과 같은 1배다. 사용자가 편집한 블루드래곤·광마 에셋은 이번 제작 대상에서 제외했다. [레드드래곤 제작·연결 기록](red_dragon_presentation.md).

## 공통 피격음 — 2026-09-27

소환물과 마스터의 피격음은 `Assets/Project333/Resources/Project333/Audio/Common/HitSoundSettings.prefab`의 AudioSource 음원·Volume 설정을 공통으로 사용한다. 현재 기본 음원은 `UnitHit.wav`이며 [공통 피격음 교체](common_hit_sound.md) 방법으로 바꿀 수 있다. 공격·반격·마법·효과 피해의 공통 피격 연출에서 재생하며, 광역 피해가 같은 프레임에 여러 대상을 때리면 한 번만 재생해 음량이 겹치지 않게 한다. 다음 프레임의 타격이나 연속 타격은 다시 재생할 수 있다.

블루드래곤 물보라·광마 검기에서 재생하던 개별 명중음과 개별 피격음은 사용하지 않는다. 각 캐릭터의 공격 준비·발사·검 휘두름 등 공격음은 유지한다. 피해 없이 파괴되는 경우에는 피격음을 추가하지 않는다. 전체 음량·음소거 설정을 따른다.
## 이미지·동작 수정 방법

1. 위 `Assets/Unit/<소환물>/` 폴더의 PNG를 편집한다. 같은 프레임 수를 유지한다면 전체 크기,512px격자, 투명 배경, 발 기준선을 유지해서 덮어쓴다. `.meta`는 삭제하지 않는다.
2. Unity가 이미지를 다시 가져오면 기존 스프라이트 참조가 갱신된다. 변경 확인은 Play Mode를 새로 시작해서 한다.
3. 프레임 수를 바꾸려면 Sprite Editor에서512×512로 분할하고, 해당 `.anim`의 Sprite 키프레임을 수정한다. 클립의 Samples와 Loop Time으로 속도·반복을 설정한다.
4. Animator Controller의 `Idle`, `Attack`, `BeAttacked`, `Run`, `Death`, `Drained` 상태에 원하는 클립을 연결한다. 현재 타일 호출은 이 상태 이름을 사용한다. Idle이 기본 상태다. 공격/피격/사망 뒤 다음 상태로 돌아갈 시점은 전투 연출 코드가 제어하므로 임의의 자동 전이 추가는 피한다.
5. 컨트롤러 자체를 바꾸면 카드 `.asset`의 **Board Animator Controller**에 새 컨트롤러를 등록한다. Board Sprite는 정지/초기 표시용 이미지다.
6. 휴대폰에는 사용자가 최신 클라이언트를 빌드·설치해야 반영된다.

클립을 연결하거나 클립의 프레임을 수정하면 그 결과를 읽는다. 애니메이션 대기 시간도 해당 클립의 길이를 사용한다. 물대포·검기·효과음의 발사/명중 시점은 현재 연출 코드의 설정을 유지한다. 공격의 준비·명중 구간을 크게 바꾸면 그 타이밍도 함께 조정해야 한다. 컨트롤러 State Speed는 현재1이며, 속도 편집은 클립 Samples에서 한다.

## 화면 크기와 위치

해상도와 화면 표시 크기는 별개다. `TileTextView`의 공통 UI 표시 영역에 스프라이트 전체 프레임을 맞춘다. 현재 씬의96×96 기준 영역에1.35배와 타일 반응형 배율이 적용된다. 2026-09-27부터 광마와 블루드래곤을 포함한 모든 소환물의 추가 표시 배율은1배다. 블루드래곤의 Attack/BeAttacked/Run/Death에 적용되던0.7배 보정을1배로 통일했다. 이 보정은 프레임을 선택하거나 클립을 우회하지 않는다. 전환 중 발 기준선과 이동 위치, 공통 명중 위치의 하향10UI오프셋도 유지한다.

## 제작 원본과 보관본

- 현재 별도 연출 제작 모션의 편집 대상은 `Assets/Unit/BlueDragon`, `Assets/Unit/Gwangma`, `Assets/Unit/RedDragon`, `Assets/Unit/A-212`, `Assets/Unit/A-301`, `Assets/Unit/GaebangChief`, `Assets/Unit/ManaCannon`이다. 오크 족장의 현재 전용 연결은 `Assets/Unit/OrcChieftian`이다. 재사용 카드는 위 실제 연결 절의 Controller 폴더에서 편집한다.
- 이전 생성 통합 PNG와 변환 중간 시트는 `Tools/Art/Source/BlueDragon`, `Tools/Art/Source/Gwangma`로 옮겼다. Unity Assets 밖의 제작 기록이므로 이 파일을 수정해도 게임이 바뀌지 않는다.
- 물 효과 `WaterAtlas.png`, 검기 `CrossSlashAtlas.png`, 레드드래곤 `FireAtlas.png`는 각 캐릭터의 Resources 경로를 사용한다.
- 레드드래곤 생성 원본은 `Tools/Art/Source/RedDragon`, 프롬프트는 `Tools/Art/RedDragonPrompts.json`에 보관한다. `PackRedDragonSprites`는 검증용 폴더에 포장하며, `ImportRedDragonPresentation.cs`는 Unity API를 사용하는 일회성 임포트 도구다. 수동 편집 이후에는 과거 원본으로 다시 실행하지 않는다.
- 교체 전 원본 소환물 폴더는 `Tools/Art/Archive/BoardMotionsBeforeController_20260926.zip`으로 보관했다.
- `ExportBlueDragon512.ps1`/`ExportGwangma512.ps1`은 이제 기본적으로 `TempBuild/<캐릭터>512Export`에 내보낸다. 현재 게임 에셋을 자동으로 덮어쓰지 않는다.
- `Tools/Art/MigrateBoardAnimationControllers.cs`는 이번 이전에 사용한 일회성 Unity Editor 코드다. 자동 실행되지 않으며, 사용자가 편집한 클립을 유지하려면 다시 실행하지 않는다. 스프라이트 provider의 편집 capability를 확인한 후 Unity API로 분할/클립/컨트롤러를 갱신했다. 스프라이트 `.meta`를 직접 작성하지 않았다.
## 컨트롤러 통일 최종 검증 (2026-09-26)

- 전체 런타임 소스 컴파일 통과. 별도 Unity6000.3.12f1에서 **49/49 검사 통과**.
- 각 컨트롤러가512px 모션 스프라이트와 올바른 fps/반복/길이를 참조함을 확인했다. 카드에 다른 AnimatorOverrideController를 지정했을 때 두 캐릭터 모두 교체한 Attack 프레임을 실제 Animator로 재생하고, 변경한 클립의 길이를 반환했다.
- 기존 캐릭터 Resources 시트를 검증 입력에서 제거한 상태로 물대포/검기·접근/공격/반격/복귀·취소/방전·원본 Idle 복귀를 확인했다.
- 두 카드의 등록 컨트롤러 GUID를 유지했고, 실제 반영된 에셋42개가 Unity 검사 입력과 해시 일치했다. 블루드래곤 원본 Idle PNG/클립/메타도 보존했다.
- 증거: `TempBuild/AnimatorControllerMigration_20260926/{compile-final.log,unity-final.xml,unity-final.log,validation-report.json}` 및 `TempBuild/GwangmaPresentation_20260926/CapturesController/`.
- 검증용 빈 씬에서 실제 Unity 재생과 캡처를 수행했다. 사용자의 전체 전투씬·Android 검증은 아니다. 검증 프로젝트 종료 시 TMP Essential Resources 안내와 초기 라이선스 연결 재시도는 테스트 성공 결과와 구분한다.
- 사용자 프로젝트의 씬/패키지 설정을 바꾸거나 클라이언트 빌드·설치, 서버 변경, 커밋·푸시는 하지 않았다. Unity CLI는 설치했으며 Pipeline 패키지는 별도 검증 프로젝트에만 추가했다.
현재 소환물별 배율 목록: [소환물 표시 배율](summon_display_scales.md).

## A-212 신규 모션 — 2026-09-30

Attack·Run·Death를 각 8컷, 512×512px, 하단 여백 16px로 제작했다. 각 PNG는 4096×512px다. Attack·Death는 8fps/1초/반복 끔, Run은 12fps/약 0.667초/반복 켬이다. 기존 Idle·BeAttacked·컨트롤러·카드와 표시 배율은 유지했다.

양손 원형 전기톱 공격에 기존 4연타의 각 접촉 스파크와 공통 UnitHit를 맞춘다. 고유 효과음은 사용자가 제공한 원형 전기톱 녹음으로 편집한 0.480초 공격음 한 개이며 공격/반격 동작마다 한 번 재생한다. VFX는 공통 하향 피격 위치를 따른다. 생성 그림·VFX·소리·편집 경로·원본 보관과 검증 증거는 [A-212 제작 기록](a212_presentation.md)을 참고한다.

## A-301 신규 모션 — 2026-09-30

Idle·Attack·Death를 각 8컷, 프레임당 512×512px로 제작했다. 각 PNG는 4096×512px다. Idle·Death는 8fps/1초, Attack은 10fps/0.8초이며 Idle만 반복한다. 같은 표시 배율과 기본 하단 여백 16px를 유지하면서 의도한 부유 움직임·추락은 보존한다. 기존 BeAttacked·Disabled·컨트롤러·카드와 .aseprite 원본은 유지했다.

A-301은 자기 타일에서 공격하며 발사구에서 실제 공격 대상의 공통 하향 피격점까지 붉은 직선 레이저가 연결된다. Attack 시작 0.2초에 광선·피해 표시·공통 UnitHit를 맞추고, 조용한 0.260초 발사음을 한 번 재생한다. 레이저는 UI 도형으로 그리므로 긴 광선을 Attack PNG에 그리지 않는다. 소스·편집 경로·보관본·21개 Unity 검사 결과는 [A-301 제작 기록](a301_presentation.md)을 참고한다.

## 개방 방주 신규 모션 — 2026-10-01

`Assets/Unit/GaebangChief/<모션>.png` → 같은 이름의 `.anim` → `GaebangChief.controller` → `Assets/Project333/ScriptableObjects/StarterTen/Cards/GaebangChief.asset`의 **Board Animator Controller**를 사용한다. Board Sprite는 Idle_0이다.

| 상태 | 컷 수 | Samples | 반복 |
|---|---:|---:|---|
| Idle | 사용자 제작 4 | 12fps | 켬 |
| Attack | 8 | 12fps | 끔 |
| Run | 8 | 12fps | 켬 |
| Death | 8 | 8fps | 끔 |
| BeAttacked | Idle_0 고정 1컷 | 12fps | 끔 |
| Drained | Idle_0 고정 1컷 | 1fps | 켬 |

2026-10-02: 사용자가 직접 제작해 교체한 Idle.png와 편집한 Idle.anim을 사용한다. 현재 가로 4컷·12fps·약 0.333초 루프이며 튜토리얼도 같은 Idle_0→1→2→3 순서·속도로 재생한다. 금화 눈 설명 구간은 정지 이미지다. 연결 작업에서 사용자 PNG·메타·클립을 변경하지 않는다.

그림은 Idle·Attack·Run·Death 네 종류다. 현재 Idle은 2048×512px·가로 4컷, 다른 세 모션은 4096×512px·8컷이다. 각 컷은 512×512px이며 하단 투명 여백은 16px다. 사용자 Idle은 그림·배율·위치를 재가공하지 않고 사용하며 런타임 추가 표시 배율은 공통 1배다. 기본 Idle과 여섯 상태를 명시적으로 연결하며 자동 전이는 추가하지 않는다.

모션 편집은 현재 `Assets/Unit/GaebangChief/`의 PNG·클립에서 한다. 생성 원본 `Tools/Art/Source/GaebangChief/`와 과거 포장·일회성 임포트 도구로 사용자 편집 PNG를 덮어쓰지 않는다. 주먹 준비 0.25초·금화 8컷/16fps VFX와 공격음, 원본 카드 그림·도구·검증·실제 캡처 경로는 [개방 방주 제작·편집 기록](gaebang_chief_presentation.md)을 참고한다.

## 마나 대포 신규 모션 — 2026-10-01

`Assets/Unit/ManaCannon/<모션>.png` → 네이티브 `.anim` → `ManaCannon.controller` → `Cards/ManaCannon.asset`의 **Board Animator Controller**를 사용한다. 새 카드 그림과 Board Idle Sprite는 새 Idle_0이다. 사용자 참고 원본은 변경하지 않았다.

Idle·Attack·Death 각 8컷, PNG 4096×512px/개별 컷 512×512px이며 24컷의 몸체 배율과 16px 접지 여백을 통일했다. Idle8fps/1초 반복, Attack10fps/0.8초 비반복, Death8fps/1초 비반복이다. Run은 Idle8컷/8fps 반복을 재사용하고 BeAttacked는 Idle_0/12fps 비반복, Drained는 Idle_0/1fps 반복으로 연결한다. 자동 전이는 없다.

공격 시작 0.300초에 실제 포구에서 푸른 마나 포탄을 발사하고 0.350초 직선 비행 후 명중한다. 별도 ProjectileAtlas·ExplosionAtlas는 각각 4×2의 512px 8컷이다. 명중 시점에 폭발과 이미 확정된 피해 표시·공통 UnitHit를 맞춘다. 새 충전·발사음과 마나 폭발음은 짧고 조용하게 제작했다. 원본/생성 프롬프트, 정확한 앵커, 안전한 포장 도구, 자산·음향 교체 경로는 [마나 대포 제작·편집 기록](mana_cannon_presentation.md)을 참고한다. 이후 수작업 수정한 PNG·meta를 초기 생성 결과로 덮어쓰지 않는다.

## 방전 공통 상태 VFX — 2026-10-01

방전 상태의 소환물에 별도 전기 Image를 겹쳐 재생한다. 캐릭터 PNG·AnimationClip·Animator Controller·카드 Board Animator Controller 연결과 표시 배율은 그대로다. 전기는 `Assets/Project333/Resources/Project333/StatusEffects/Drained/ElectricArcs.png`의 네이티브 512px 8컷(전체2048×1024)을 사용한다. 12fps/.667초 재생과 .75초 휴지, .1초 페이드·최대알파.55이며 추가 음향은 없다. 유닛 이동/반전/크기를 따르고 방전 해제·사망·봉인·숨김 시 꺼진다. 같은 RuntimeId의 온라인 갱신은 재생 중인 컷을 보존한다. 그림·밝기·속도 수정과69/69 최종 Unity검사·실제 화면 증거는 [방전 VFX 제작·편집 기록](drained_status_vfx.md)을 참고한다.

## 스턴·빙결 표시와 본체 일시정지 — 2026-10-01

스턴은 머리 위의 금색 별 8컷 순환, 빙결은 반투명 큰 얼음 결정으로 표시한다. 빙결 중 본체 Animator와 공통 클립 샘플러만 정지하고 해제 시 원래 속도·재생 위치를 이어 사용한다. 별·얼음은 독립 시계로 계속 재생된다. Death는 정지를 풀고 진행한다. 기존 Unit PNG/AnimationClip/Animator Controller/카드 연결·표시 배율은 변경하지 않았다. 실제 알파 영역을 metadata-only 캐시에 저장해 기존 Unit를 Readable로 바꾸지 않는다. 새 효과 그림·속도·투명도·캐시 작성기·검사 증거는 [스턴·빙결 VFX](stun_freeze_status_vfx.md)에 있다.

## 무피해 반격 후 Idle 복귀 (2026-10-26)

피해0·무적으로 공격을 막고 반격하면, 공통 전투 연출의 복귀 목록에 빠져 Attack의 마지막 프레임에 남던 경로를 수정했다. 살아남은 반격자만 동일 RuntimeId를 확인해 Idle로 돌리며 사망자는 Death를 유지한다. 오크 족장 PNG/anim/controller는 바꾸지 않았다. 실제 Play 재현2실패→3/3 및 관련 고유 검사41개 최종 통과. 자세한 원인·검증·앱 반영은 [반격 연출](counterattack_presentation.md).
