# 광마 연출

> 2026-09-27 사용자 수정: 각 512×512px 프레임의 소환물 아래 여백을 약 16px로 직접 조정한 PNG가 최신 편집본이다. 아래 원화 확대·발 위치 보존·검증 내용은 2026-09-26 당시 기록이다. 현재 파일을 과거 생성 원본으로 덮어쓰지 않는다. 향후 제작은 [공통 제작 기준](unit_animation_authoring.md)을 따른다.

최신 소스 대조: 2026-10-01. 현재 카드 기본 ATK/HP는 30/60이며 현재 Unit PNG·클립·Controller가 모션의 기준이다. 아래 제작·검증 기록은 당시 결과로 유지한다.

## 현재 클립 — 2026-10-01

아래 현재 표는 2026-10-01 `.anim`의 스프라이트 키 수·Samples·StopTime·Loop Time 및 카드/컨트롤러 참조를 읽어 확인한 값이다. PNG 픽셀·모든 게임 모션을 새로 실행 검증한 기록은 아니다. 이전 제작 표와 검사 결과는 당시 기록으로 유지하며, 현재 Assets 편집본이 재생 기준이다.

| 모션 | 현재 키 수 / Samples / 길이 | Loop Time |
|---|---|---|
| Idle | 4 / 12fps / 약0.333초 | 켬 |
| Attack | 6 / 12fps / 0.5초 | 끔 |
| BeAttacked | 8 / 12fps / 약0.667초 | 끔 |
| Run | 6 / 12fps / 0.5초 | 켬 |
| Death | 7 / 12fps / 약0.583초 | 끔 |

현재 Controller에 Drained 상태는 없다. 별도 방전·스턴·빙결 VFX는 현재 상태를 읽으며, 빙결은 본체 Animator/공통 샘플러만 정지하고 해제 시 원래 재생 위치를 이어 사용한다.

## 편집과 연결

`Assets/Unit/Gwangma/`의 PNG → Animation Clip → `Gwangma.controller` → `Gwangma.asset`의 Board Animator Controller를 사용한다. 기존 카드의 컨트롤러 등록과 GUID를 유지했으며, 코드가 카드의 컨트롤러를 무시하고 별도 Resources 시트를 재생하던 경로를 제거했다.

아래 표는 2026-09-26 이전 당시 구성이다. 현재는 사용자가 수정한 Unit 폴더의 그림과 클립이 기준이며, 2026-09-27 소스 확인 시 컨트롤러에 Drained 상태는 등록되어 있지 않다.

| 모션/상태 | 같은 폴더의 PNG/클립 | 이전 당시 프레임 / 속도 |
| --- | --- | --- |
| 평상시 | `Idle.png`, `Idle.anim` | 6 /6fps 반복 |
| 공격 | `Attack.png`, `Attack.anim` | 6 /12fps |
| 피격 | `BeAttacked.png`, `BeAttacked.anim` | 6 /15fps |
| 이동 | `Run.png`, `Run.anim` | 6 /12fps 반복 |
| 사망 | `Death.png`, `Death.anim` | 6 /8fps |
| 방전 | `Drained.anim` | Idle 첫 컷 고정 |

이전 당시 5개 PNG는 모두 3072×512, 한 컷 512×512였다. 사용자가 선택한 144×144 원화의 nearest-neighbour 확대 결과를 연결한 기록이며, 이후 직접 편집한 그림을 덮어쓰는 기준이 아니다. 새로운 세부 묘사를 그린 것이 아니며 캐릭터의 상대 크기·발 위치·30포즈를 유지한다. 화면 크기는 공통 UI 영역에 맞추며 광마 추가배율은1배다.

**[편집 방법·스프라이트 분할·컨트롤러 등록](unit_animation_authoring.md)**. 앞으로 위 소환물 폴더의 PNG나 클립을 편집한다. Animator Controller에서 지정한 클립이 프레임과 재생 시간을 결정한다.

## 공격·검기·소리

근접 공격 시 최소0.28초 이동 모션/돌진음으로 접근한다. 검 준비음 → 공격 시작0.16초에 쌍검 휘두름음 →0.25초에 대상의 공통 명중점에서 X자 검기/타격음 → 검기8프레임이0.4초 동안 사라진다. 생존한 공격자는 최소0.22초 이동 모션으로 복귀한다. 기존2연타 피해·반격 규칙은 그대로이며 그림/소리가 추가 피해를 만들지 않는다. 공격과 반격 검기는 별도 슬롯을 사용한다.

검기는 `Assets/Project333/Resources/Project333/Presentation/Gwangma/CrossSlashAtlas.png`(1774×887,4열×2행)를 사용한다. `GetVisualImpactWorldPosition()`의 공통 하향10UI오프셋과 중앙pivot을 유지하며 쉴더가 대신 맞으면 그 대상에 표시한다. 재접속 취소·비활성화·씬 종료·소환물 교체 시 남은 연출을 정리한다.

2026-09-27부터 검기 명중·피격음은 모든 소환물이 사용하는 `Assets/Project333/Resources/Project333/Audio/Common/UnitHit.wav`로 통일한다. `CrossSlashImpact`·`GwangmaHurt`는 런타임에서 재생하지 않고 파일만 보관한다. 광마의 `SwordDraw`·`SwordSwing`·`GwangmaDeath`·`Dash`는 유지한다. 짧은 mono PCM16/44.1kHz이며 전체 음량 설정을 따른다. [기존 음원 제작 기록](../Tools/Art/GwangmaAudio.md), [공통 피격음 제작 기록](../Tools/Art/RedDragonAndUnitHitAudio.md).

## 제작 원본과 검증 이력

`GwangmaAtlas.png`(1374×1145)와 변환 중간 시트는 `Tools/Art/Source/Gwangma/`에 보관한다. 제작 당시144×144 추출 창은x=열×229+45, 위쪽y=82/311/543/758/960이다. [그림 제작 기록](gwangma_art_prompts.md). 현재 게임은 이 제작 원본을 직접 로드하지 않는다.

이전 생성 연출41/41 및512px변환45/45 검사는 해당 시점의 기록이다. 최신 컨트롤러 이전 검증은 아래에 별도로 기록한다. 클라이언트 빌드·설치는 사용자가 한다. 이 변경 자체에 서버 변경은 없다.
## 컨트롤러 통일 최종 검증 (2026-09-26)

- 전체 런타임 소스 컴파일 통과. 별도 Unity6000.3.12f1에서 **49/49 검사 통과**.
- 각 컨트롤러가512px 모션 스프라이트와 올바른 fps/반복/길이를 참조함을 확인했다. 카드에 다른 AnimatorOverrideController를 지정했을 때 두 캐릭터 모두 교체한 Attack 프레임을 실제 Animator로 재생하고, 변경한 클립의 길이를 반환했다.
- 기존 캐릭터 Resources 시트를 검증 입력에서 제거한 상태로 물대포/검기·접근/공격/반격/복귀·취소/방전·원본 Idle 복귀를 확인했다.
- 두 카드의 등록 컨트롤러 GUID를 유지했고, 실제 반영된 에셋42개가 Unity 검사 입력과 해시 일치했다. 블루드래곤 원본 Idle PNG/클립/메타도 보존했다.
- 증거: `TempBuild/AnimatorControllerMigration_20260926/{compile-final.log,unity-final.xml,unity-final.log,validation-report.json}` 및 `TempBuild/GwangmaPresentation_20260926/CapturesController/`.
- 검증용 빈 씬에서 실제 Unity 재생과 캡처를 수행했다. 사용자의 전체 전투씬·Android 검증은 아니다. 검증 프로젝트 종료 시 TMP Essential Resources 안내와 초기 라이선스 연결 재시도는 테스트 성공 결과와 구분한다.
- 사용자 프로젝트의 씬/패키지 설정을 바꾸거나 클라이언트 빌드·설치, 서버 변경, 커밋·푸시는 하지 않았다. Unity CLI는 설치했으며 Pipeline 패키지는 별도 검증 프로젝트에만 추가했다.