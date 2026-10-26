# 마나 대포 연출과 자산 편집

## 현재 연결 메타데이터 — 2026-10-20

현재 카드가 참조하는 Controller/clip을 정적으로 다시 읽은 값이다. 아래10월1일 등 날짜별 제작·검증 값은 당시 기록이며 프레임 픽셀/전체 모션을 오늘 재검증한 것으로 해석하지 않는다. [전체 유닛 현재 값](unit_animation_snapshot.md).

| 상태 | Sprite 키 / Samples / StopTime | Loop |
|---|---|---|
| Attack | 8 / 10fps / 0.8초 | 끔 |
| BeAttacked | 2 / 12fps / 0.166667초 | 끔 |
| Death | 8 / 8fps / 1초 | 끔 |
| Drained | 1 / 1fps / 1초 | 켬 |
| Idle | 8 / 8fps / 1초 | 켬 |
| Run | 8 / 8fps / 1초 | 켬 |

마나 대포(ManaCannon)는 판타지 소속 언커먼 원거리 마법 유닛입니다. 소환 비용은 마나 2, 기본 공격력 40/HP 10, 방어력 0/0이며 이동 가능하고 종족과 특수효과는 없습니다. 공격 명령마다 마나 1을 먼저 사용하고 부족하면 골드 1로 대체합니다. 자원이 없거나 유효하지 않은 공격은 자원·공격 횟수를 소비하지 않습니다. 망각은 기존 규칙에 따라 이 카드의 공격 비용 효과를 제거합니다. Lv.3/6/9/13 공격력 +1, 나머지 Lv.1–13 HP +1로 Lv.13 기본 44/19입니다. 드래프트와 랜덤 카드 보상에 포함합니다.

## 그림과 Animator Controller

사용자의 예시 마나대포.png는 참고용으로 원본을 보존했습니다. 새로운 검은 강철·황동 대포, 푸른 마나 수정, 빛나는 포구를 제작했습니다. 도구 소스와 정확한 이미지 생성 프롬프트는 Tools/Art/ManaCannonPrompts.json, 원본과 생성 결과는 Tools/Art/Source/ManaCannon/에 보존합니다. 카드 그림은 새 Idle 첫 프레임을 사용합니다.

- Assets/Unit/ManaCannon/Idle.png: 8컷, 8fps 반복, 1초. 수정과 포구가 은은하게 빛납니다.
- Assets/Unit/ManaCannon/Attack.png: 8컷, 10fps 비반복, 0.8초. 충전 후 프레임 3(0.300초)에 푸른 포구가 발광하고 반동을 보입니다.
- Assets/Unit/ManaCannon/Death.png: 8컷, 8fps 비반복, 1초. 수정 과부하 후 대포가 무너져 잔해가 남습니다.
- 각 PNG는 4096×512이고 모든 컷은 정확히 512×512입니다. 24컷을 공통 배율로 포장해 같은 몸체 크기를 유지하며, 접지 기준 아래 투명 여백 16px와 컷 내부의 무기·불빛·잔해를 보존합니다.
- ManaCannon.controller에 Idle/Attack/Run/BeAttacked/Drained/Death 네이티브 클립을 연결합니다. Run은 요청한 세 가지 모션의 범위를 유지하도록 Idle 8컷을 재사용합니다. BeAttacked/Drained는 Idle_0 고정 프레임이며, 별도 피격 그림·효과음은 추가하지 않습니다.
- Cards/ManaCannon.asset의 Board Animator Controller와 Board Idle Sprite에 등록합니다. 기존 PNG → AnimationClip → Animator Controller → 카드 연결 방식이며 런타임 별도 Sprite.Create 모션 경로가 없습니다.

## 포탄과 명중

Assets/Project333/Resources/Project333/Units/ManaCannon/ProjectileAtlas.png와 ExplosionAtlas.png는 각각 2048×1024, 4×2 배열의 네이티브 512×512 8컷입니다. 투명 배경, Point 샘플링, 중심 Sprite 피벗, 압축/밉맵 없음으로 가져옵니다.

대포는 자신의 타일에서 공격합니다. Attack 시작부터 0.300초 충전하고, 푸른 마나 포탄이 0.350초 동안 실제 대상까지 직선으로 날아갑니다. 0.650초에 명중하면 포탄을 숨기고 폭발 8컷을 18fps로 표시합니다. 피해 숫자와 공통 피격 모션·UnitHit도 이 명중 시점에 표시합니다. 피해 판정과 자원 차감은 공유 전투 서비스/서버가 담당하며 VFX에서 다시 피해를 입히지 않습니다. 정상 원거리 공격에는 반격이 없습니다. 파괴된 대상은 폭발과 반응 후 기존 Death 처리에서 전체 파괴 모션을 재생합니다.

- 실제 Attack_3 포구의 Unity 정규화 좌표: (0.700727865, 0.404778212).
- 8개의 투사체 그림은 밝은 포탄 머리를 같은 지점에 등록했습니다. 런타임 Image 피벗은 (0.768758039, 0.400744872)로 머리 자체가 비행 경로를 따라갑니다. 네이티브 Sprite 피벗은 (0.5, 0.5)를 유지합니다.
- 폭발 첫 흰 코어는 (256,256)에 등록하며 이후 파편은 같은 원점을 유지합니다.
- 대상이 움직이면 현재 대상 위치를 따라갑니다. RuntimeId가 바뀌거나 취소·화면 전환·컴포넌트 비활성화/파괴가 발생하면 이전 효과·소리를 중단합니다. UI 이벤트를 가로채지 않는 Canvas overlay를 재사용합니다.
- 표시 크기와 타이밍은 Runtime/Presentation/Battle/ManaCannonProjectile.cs, ManaCannonPresentationAssets.cs에서 조절합니다. 현재 포탄은 유닛 표시 너비의 0.85배, 폭발은 1.4배이며 최소 크기만 적용합니다. 타이밍 변경 시 Attack 프레임과 발사 음향의 0.300초를 함께 맞춥니다.

## 효과음 교체

Audio/ManaShot.wav는 마나 충전과 짧은 발사음 0.400초, Audio/ManaExplosion.wav는 마법 폭발음 0.350초입니다. PCM16 모노 44.1kHz, 파일 피크 약 −20/−19dBFS이며 재생 게인 0.45/0.40과 기존 전체 음량·음소거를 한 번 적용합니다. 발사 부분은 0.300초 시작, 피크 약 0.309초로 맞췄습니다. 공통 UnitHit 파일은 그대로 사용합니다.

동일 파일명으로 WAV를 교체하고 .meta는 보존하면 Resource 연결이 유지됩니다. 발사음은 Attack 시작에 재생되므로 충전 뒤 발사 부분이 약 0.300초에 있는 파일을 사용합니다. 폭발음은 명중 순간에 재생됩니다. Tools/Art/GenerateManaCannonAudio.ps1과 ValidateManaCannonAudio.ps1은 재현과 독립 PCM 검사용입니다.

## 안전한 재제작

PackManaCannonSprites.ps1은 검토용 출력 폴더에 PNG를 내보내고 Assets 출력을 거부합니다. 원본 그림의 조각을 보존하고 컷별 크기 확대 대신 공통 배율과 기준점을 적용합니다. StageManaCannonPresentation.ps1과 ImportManaCannonPresentationAssets.cs는 검증 프로젝트와 Unity 네이티브 API를 사용하는 최초 등록 도구입니다. 이후 사용자가 직접 수정한 PNG를 초기 생성 결과로 덮어쓰지 않습니다.

검증 결과와 제한은 TempBuild/ManaCannonPresentation_20261001/verification.json과 docs/codex_handoff.md에 기록합니다. 클라이언트 앱 빌드와 온라인 서버 배포는 별도 적용이 필요합니다.

## 검증 기록 — 2026-10-01

Unity 6000.3.12f1 격리 프로젝트에서 새 카드의 규칙 50개·네이티브 자산 10개·실제 연출 7개, 총 67개를 통과했습니다. Canvas 재연결 후 표시 순서를 보완하고 해당 연출 7개를 다시 실행해 모두 통과했습니다. 관련 전체 최초 실행은 669/671이며 실패 두 개는 기존 A-301 검사와 현재 수작업 자산의 컷 수·사망 길이 기대값 불일치입니다. A-301 자산을 변경하지 않았고 전체 검사를 마지막에 다시 실행한 것은 아닙니다.

오프라인 서버 컴파일과 양쪽 좌석 × Lv.0–13 × 마나/골드 지불 56개 실제 소환·공격 조합, 자원 부족/고독/망각/방전/봉인/환술/복원/실제 AI 검사를 통과했습니다. 기존 카드와 기본 AI 검사도 통과했습니다. 마나 대포 등록 검사 당시 52개 공유 카드 정의·45개 네이티브 카탈로그·45개 드래프트·41개 강화 가능·36개 실제 보상 대상이었습니다. 현재 전체 집계는 [카드 목록](card_catalog.md)을 따릅니다. 이전 51개 카드·44개 등록 순서·966개 기존 자산을 보존했고 1646개 Unity 입력과 203개 서버 컴파일 입력을 대조했습니다.

[제작 컷 모음](../Tools/Art/Preview/ManaCannonContactSheet.png), 실제 charge/flight/impact/death 캡처 6개와 상세 [검증 기록](../TempBuild/ManaCannonPresentation_20261001/verification.json)을 보관했습니다. 격리 연출과 오프라인 공유 코드 검증이며 전체 온라인 전투·휴대폰 청음·앱 빌드/설치·서버 배포는 수행하지 않았습니다.
