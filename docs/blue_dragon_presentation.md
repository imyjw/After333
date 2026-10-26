# 블루드래곤 연출

## 현재 연결 메타데이터 — 2026-10-20

현재 카드가 참조하는 Controller/clip을 정적으로 다시 읽은 값이다. 아래10월1일 등 날짜별 제작·검증 값은 당시 기록이며 프레임 픽셀/전체 모션을 오늘 재검증한 것으로 해석하지 않는다. [전체 유닛 현재 값](unit_animation_snapshot.md).

| 상태 | Sprite 키 / Samples / StopTime | Loop |
|---|---|---|
| Attack | 6 / 12fps / 0.5초 | 끔 |
| BeAttacked | 6 / 12fps / 0.5초 | 끔 |
| Death | 8 / 12fps / 0.666667초 | 끔 |
| Idle | 5 / 12fps / 0.416667초 | 켬 |

> 2026-09-27 사용자 수정: 각 512×512px 프레임의 소환물 아래 여백을 약 16px로 직접 조정한 PNG가 최신 편집본이다. 아래 원화 확대·발 위치 보존·검증 내용은 2026-09-26 당시 기록이다. 현재 파일을 과거 생성 원본으로 덮어쓰지 않는다. 향후 제작은 [공통 제작 기준](unit_animation_authoring.md)을 따른다.

최신 소스 대조: 2026-10-01. 현재 native 클립·Controller 연결과 공통 표시 배율을 확인했다. 아래 2026-09-26 실행 검증은 당시 기록이다.

## 편집과 연결

이제 `Assets/Unit/BlueDragon/`의 PNG → Animation Clip → `BlueDragon.controller` → `BlueDragon.asset`의 Board Animator Controller를 사용한다. 기존 카드의 컨트롤러 등록과 GUID를 유지했다. 캐릭터용 Resources 시트를 코드에서 잘라 재생하던 예외 경로는 제거했다.

2026-10-01 현재 `.anim`/Controller 설정은 다음과 같다. Idle·Attack·BeAttacked·Death의 Samples는 12, Loop Time은 모두 켬이며 StopTime은 각각 약0.417/0.5/0.5/0.667초다. 이 목록은 파일 확인 결과이며, 과거 검사가 사용자의 최신 편집본까지 검증했다는 의미는 아니다.

| 모션 | 같은 폴더의 PNG·클립 | 현재 소스 구성 |
| --- | --- | --- |
| 평상시 | `Idle.png`, `Idle.anim` | 512px × 5컷, 12fps 반복 |
| 공격 | `Attack.png`, `Attack.anim` | 512px × 6컷, 12fps |
| 피격 | `BeAttacked.png`, `BeAttacked.anim` | 512px × 6컷, 12fps |
| 사망 | `Death.png`, `Death.anim` | 512px × 8컷, 12fps |
| 이동·방전 | Run·Drained 상태 | 현재 컨트롤러에 등록되어 있지 않음 |

현재 등록한 모션은 공통 BoardScale 1배를 사용한다. Run·Drained 클립이 재생된다는 의미는 아니며, 공격·피격·사망 상태의 전환과 대기는 전투 연출 코드가 제어한다. 현재 연결한 클립 길이를 기준으로 읽고, 아래 최초 제작·이전 검사 수치는 해당 날짜의 기록으로 읽는다.

2026-09-26에는 생성 모션의 기존 229×229 그림을 nearest-neighbour로 512×512에 확대해 연결했다. 2026-09-27에는 사용자 요청으로 생성 모션의0.7배 보정을1배로 변경했다. 현재는 사용자가 수정한 Unit 폴더의 그림이 기준이다. 카드 능력치·피해 속성·원거리 공격·회복 효과는 이 연출 작업으로 변경하지 않았다.

**[편집 방법·스프라이트 분할·컨트롤러 등록](unit_animation_authoring.md)**. PNG와 `.anim`은 이제 위 소환물 폴더에서 편집한다. 클립 길이는 전투의 애니메이션 대기에 반영된다. 물대포 발사/명중 시점은 아래 연출 설정을 따른다.

## 물대포와 음향

0.25초 준비 → 입에서 대상까지0.38초 비행 → 도착 시 물보라·타격음·피해 숫자·피격 모션 →0.4초 물보라. 원거리 드래곤은 제자리에서 발사한다. 사망 연출은 현재 컨트롤러에 연결한 Death 클립 길이를 따른다.

`Assets/Project333/Resources/Project333/Presentation/BlueDragon/WaterAtlas.png`는1448×1086,362px셀,4열×3행이다. 첫4컷은 비행, 나머지8컷은 충돌이다. 좌우 반전·입 위치·쉴더가 대신 맞는 대상·기존 취소/재접속 정리를 유지한다. 효과가 입력을 가로채거나 추가 피해를 만들지 않는다.

물보라는 중앙pivot을 사용하고 `TileTextView.GetVisualImpactWorldPosition()`의 공통 몸통 기준에서 하향10기준UI단위에 맞춘다. 실제 타일/Canvas 비율을 반영한다. 다른 유닛의 명중 효과도 같은 위치 계산을 재사용한다.

2026-09-27부터 명중·피격음은 모든 소환물이 사용하는 `Assets/Project333/Resources/Project333/Audio/Common/UnitHit.wav`로 통일한다. 물보라에서 별도 명중음을 겹쳐 재생하지 않는다. 블루드래곤의 충전·발사·사망음은 유지하며, 기존 개별 명중·피격 WAV는 보관된 파일이다. [기존 음원 제작 기록](../Tools/Art/BlueDragonAudio.md), [공통 피격음 제작 기록](../Tools/Art/RedDragonAndUnitHitAudio.md).

## 제작 원본과 검증 이력

생성 통합 `DragonAtlas.png`와 변환 중간 시트는 `Tools/Art/Source/BlueDragon/`에 보관한다. 현재 런타임 모션의 원본 경로와 혼동하지 않는다. [그림 제작 기록](blue_dragon_art_prompts.md), [원본 디자인 미리보기](previews/blue_dragon.html). HTML은 보관된 통합 이미지를 보여주므로 현재 Unit 폴더의 수동 편집을 자동 반영하지 않는다.

과거 생성 연출23/23,512px변환49/49 검사는 해당 시점의 기록이다. 최신 컨트롤러 이전 검증은 아래에 별도로 기록한다. 클라이언트 빌드·설치는 사용자가 한다. 이 연출 변경 자체에 서버 변경은 없다.
## 컨트롤러 통일 최종 검증 (2026-09-26)

- 전체 런타임 소스 컴파일 통과. 별도 Unity6000.3.12f1에서 **49/49 검사 통과**.
- 각 컨트롤러가512px 모션 스프라이트와 올바른 fps/반복/길이를 참조함을 확인했다. 카드에 다른 AnimatorOverrideController를 지정했을 때 두 캐릭터 모두 교체한 Attack 프레임을 실제 Animator로 재생하고, 변경한 클립의 길이를 반환했다.
- 기존 캐릭터 Resources 시트를 검증 입력에서 제거한 상태로 물대포/검기·접근/공격/반격/복귀·취소/방전·원본 Idle 복귀를 확인했다.
- 두 카드의 등록 컨트롤러 GUID를 유지했고, 실제 반영된 에셋42개가 Unity 검사 입력과 해시 일치했다. 블루드래곤 원본 Idle PNG/클립/메타도 보존했다.
- 증거: `TempBuild/AnimatorControllerMigration_20260926/{compile-final.log,unity-final.xml,unity-final.log,validation-report.json}` 및 `TempBuild/GwangmaPresentation_20260926/CapturesController/`.
- 검증용 빈 씬에서 실제 Unity 재생과 캡처를 수행했다. 사용자의 전체 전투씬·Android 검증은 아니다. 검증 프로젝트 종료 시 TMP Essential Resources 안내와 초기 라이선스 연결 재시도는 테스트 성공 결과와 구분한다.
- 사용자 프로젝트의 씬/패키지 설정을 바꾸거나 클라이언트 빌드·설치, 서버 변경, 커밋·푸시는 하지 않았다. Unity CLI는 설치했으며 Pipeline 패키지는 별도 검증 프로젝트에만 추가했다.