# 공격 전투기록 길게 누르기 미리보기

2026-10-29 사용자 요청. 기존 텍스트 전투기록의 **소환물 간 공격 기록**을 누르고 있는 동안 당시 두 카드를 시각화한다. 실제 공격 판정·자원·승패는 변경하지 않는다. 전체 전투기록 46종 목록과 향후 시각화 확장 후보는 [전투기록 46종 전체 목록](combat_log_catalog.md)을 참고한다.

## 화면과 조작

- 공격 문장을 약0.25초 가만히 누르면 중앙 왼쪽에 공격자, 오른쪽에 피격자 카드와 그 사이의 검 교차 아이콘이 표시된다. 내 공격/상대 공격 모두 같은 좌우 역할이며 하단 역할 문구에 플레이어/상대방을 구분한다.
- 손을 떼거나 로그 영역에서 벗어나거나 드래그하면 즉시 닫힌다. 화면 포커스/앱 활성 상태/뷰가 사라질 때도 닫는다. 드래그 핸들러는 새로 가로채지 않으므로 기존 ScrollRect로 전투기록을 스크롤할 수 있다.
- 실제 터치 대상은 TMP 텍스트에 붙는 `CombatLogHoldTarget`이다. `CombatLogTextView` 컨트롤러가 텍스트의 형제 오브젝트에 있어도 동작한다. 기존 serialized SetLog UnityEvent와 씬 연결을 유지한다.
- 공개된 공격 기록에만 TMP link를 부여한다. 스펠·이동·자원 등 다른 종류의 기록은 이번 단계에서 시각화하지 않는다. 쉴더·환술 리디렉션도 기존 기록의 객체/별도 기록 구분을 유지한다.
- 미리보기 Canvas는 정렬순서31200, 자체 스크롤 마스크 밖에 있고 포인터 raycast를 차단하지 않는다. 현재 카드 아트를 그대로 쓰며 화면/SafeArea 크기가 바뀔 때 두 카드 배치만 다시 계산한다.

## 능력치와 결과 표시

- 카드 하단 수치는 **공격 직전** 공격력·현재 체력이다. 3초 뒤 피해 표시가 사라져도 카드 수치 자체는 공격 후 값으로 바뀌지 않는다.
- 비교 기준은 당시 소환물의 `OriginalAttack` / `OriginalMaxHp`다. 이 값은 소환 시 이미 적용된 영구 카드 강화 수치를 포함한다. 이후 전투 중 효과/오라로 증가하면 초록, 감소하면 빨강, 같으면 흰색이다. 현재 체력은 원래 최대 체력과 비교하므로 이미 다친 체력은 빨강이다.
- 예시 확정: 상대 마스터3/323이 내 마스터33/333을 근접 공격. 왼쪽 공격자3(흰색)/323(빨강), 오른쪽 피격자33(초록)/333(흰색). 왼쪽 -33, 오른쪽 -3을 처음3초 동안 표시한다.
- 각 대상의 일반 공격/반격에 의한 실제 HP 피해 합계를 사용한다. 피해가0이하이면 피해 아이콘이 없다. 사망이면 피해 숫자를 대신해 해골을 표시한다. 초과 피해는 기존 판정의 실제 HP 차감량에 따르며 사망 표시는 이를 대체한다.
- 피해/해골은 `Show`한 시각부터 unscaledTime 기준3초에 한 번 사라진다. 계속 누르고 있어도 반복해서 나타나지 않으며 다음 번 누를 때 새로3초를 시작한다. 마감 시각끼리 직접 비교해 float 뺄셈의 경계 오차를 피한다.
- 누르기 시작할 때 미리보기 데이터를 복사하므로 손을 누르고 있는 중 새 서버 기록이 와도 대상·능력치가 바뀌지 않는다. 읽는 중에는 자동 하단 스크롤을 하지 않고 동일한 로그의 반복 갱신도 재스크롤하지 않는다.

## 기록·온라인·재접속

- `BattleCombatLogEntryDto.SourcePreview` / `TargetPreview`는 선택적 `BattleCombatLogCardSnapshotDto`다. 카드/RuntimeId, 당시 공격력·현재/최대/원래 체력과 공격력, 실제 피해, 사망 여부를 보관한다. 게임 상태 객체를 참조하지 않는다.
- 서버는 공격 전 ServerOccupantSnapshot을 만들 때 실제 `occupant.Attack`을 복사한다(실시간 오라 포함). 기록 작성 시 결과만 붙인다. StateView/영구 스냅샷 복사와 Unity 관점 전환은 두 미리보기를 깊은 복사한다. 양쪽 좌석 모두 공격자/피격자 역할은 유지하고 소유자만 로컬 관점으로 바꾼다.
- 기존 전투기록의33개 제한을 그대로 쓰며 텍스트와 미리보기 배열은 같은 위치에서 추가/교체/삭제한다. 상대 손패/덱/비공개 트랩 종류를 추가로 전송하지 않는다.
- 튜토리얼의 `TutorialCombatLogRecorder`도 같은 DTO를 기록한다. 일반 로컬 전투는 이 읽기 전용 기록기를 공격 메타데이터 수집에 재사용한다. 다른 기록의 기존 문구·기능은 유지한다.
- 새 필드가 없는 구버전 기록은 텍스트로 계속 읽는다. 현재 전장 상태나 JSON 기본값으로 과거 능력치를 추측하지 않으며 길게 누르기 링크도 만들지 않는다. 최신 서버에서 새로 발생한 공격부터 정확한 미리보기 기록이 생성된다.

## 편집 위치

| 파일 | 역할 / 조절 값 |
|---|---|
| `Runtime/Application/Online/BattleCombatLogEntryDto.cs` | 선택적 과거 카드 스냅샷 DTO |
| `Server/Project333.PvpServer/BattleSessions/BattleSession.cs` | 권위 기록·StateView/복원 복사 |
| `Runtime/Application/Tutorial/TutorialCombatLogRecorder.cs` | 로컬 당시 수치 수집 |
| `Runtime/Presentation/Battle/BattleBootstrapper.CombatHistory.cs` | 기록 인덱스와 미리보기 연결 |
| `Runtime/Presentation/Battle/CombatLogTextView.cs` | TMP link·이벤트·스크롤 연동 |
| `Runtime/Presentation/Battle/CombatLogHoldTarget.cs` | `HoldSeconds=.25`와 포인터 해제/드래그/포커스 정리 |
| `Runtime/Presentation/Battle/CombatLogAttackPreview.cs` | `MarkerSeconds=3`, 증가/감소 색, 카드384×528·간격128·배치 |
| `Runtime/Presentation/Battle/CombatLogSymbolGraphic.cs` | 검 교차·해골은 UI/CombatLog PNG, 금빛 피해는 기존 UI 벡터 아이콘 |

Runtime 경로는 모두 `Assets/Project333/` 아래다. 카드 수치의 위치는 기존 `HandCardStatOverlayLayout.DefaultAttackPosition/DefaultHpPosition`과 같은 기준을 쓴다. 사용자 카드 아트·폰트·씬·기존 스프라이트는 편집하지 않았다.

## 검증과 반영

최종 결과는 [PROGRESS](../PROGRESS.md)의 같은 날짜 기록을 참조한다. 격리 Unity6000.3.12f1/D3D11에서 원래 능력치·색상·누르기/스크롤·결과3초·갱신 중 고정·33개 창·구 기록 호환과 실제 렌더 캡처를 확인한다. 서버 오프라인 검사는 `dotnet <검증출력>/Project333.AiChecks.dll --combat-log-previews-only`이며 실제 세션4시나리오·양쪽 StateView·상대 손패 숨김·전체JSON복원을 확인한다. 운영 DB/HTTP 서버는 사용하지 않는다.

앱 재빌드와 Lightsail 서버 재배포가 모두 필요하다. 새 DB 마이그레이션은 없고 실제 휴대폰/PvP 확인은 사용자 설치 후 별도다. SDK가 제외된 격리 검증 사본이므로 광고 동작을 검증한 것은 아니다.


## 전투기록 아이콘 교체 — 2026-10-30 / 2026-10-31

- 사용자 첨부 PNG를 원본 바이트 그대로 `Assets/Project333/Resources/Project333/UI/CombatLog/CrossedSwords.png`(교차 검), `Skull.png`(사망), `DamageBurst.png`(피해 폭발, 2026-10-31 추가)에 연결했다. 투명 배경·원래 비율을 유지하며 기존 140×140/166×166 UI 영역 안에 맞춘다.
- `CombatLogSymbolGraphic`이 공유 Resources 스프라이트를 캐시해 텍스처·UV를 그린다. 피해↔해골 재사용 때 `CombatLogAttackPreview.Bind`가 재질도 갱신한다. 기존 PNG가 없을 때만 이전 벡터를 대체 표시한다. 3초 표시·수치·색상·누르기·입력 통과·정렬순서는 변경하지 않았다.
- PNG 파일 교체는 같은 경로와 `.meta`를 유지한다. 임포터는 Sprite Single / FullRect / Bilinear / NPOT None / 알파 사용 / mipmap off / 최대512 / 읽기 비활성으로 Unity에서 생성했다. 원본 PNG는 축소하거나 재생성하지 않았다.
- 사용자가 수정한 공격력·체력 글꼴/숫자 설정과 기존 씬·카드 아트는 보존했다.
  - **아이콘 크기·위치 수정**: `CombatLogAttackPreview.Create`의 검 `sizeDelta(140, 140)`와 `MakePanel`의 `ResultMarker` `sizeDelta(166, 166)` / `anchoredPosition(0, -48)`.
  - **피해 텍스트(`-3` 등) 글꼴·글씨 크기 수정**: `CombatLogAttackPreview.MakePanel`(약 130행)의 `p.Damage = Label(p.Marker.transform, "Damage", font, 65);`에서 세 번째 인자(`font` → 예: `statFont`로 변경 시 공격력/체력과 같은 `ThaleahFat_TTF SDF` 픽셀 폰트 사용)와 네 번째 인자(`65` → 원하는 글씨 크기)를 조절한다.
- 별도 Unity6000.3.12f1/D3D11에서 네이티브 스프라이트 임포트 및 기존 `CombatLogAttackPreviewTests` 검사를 통과했다(정확한 건수는 PROGRESS). 실제 피해/3초 후/사망 캡처를 확인한다. 검증 사본의 SDK 광고는 제외되어 있으며 앱 빌드·실기기·운영 서버 검증은 수행하지 않았다.
- 클라이언트 재빌드·설치 필요. 서버 재배포·DB 마이그레이션 없음.

## 단독 카드 전투기록 누르기 미리보기 (2, 3, 10, 21~34번) — 2026-10-30

2026-10-30 사용자 요청 및 결정 확정. 기존 1번 소환물 간 공격(`Attack`)의 2장 대립 미리보기에 더해, 아래 **17종 전투기록**을 0.25초 이상 길게 누르고 있는 동안 **해당 시점의 카드 1장만 화면 중앙에 표시**한다.

- **대상 전투기록 (17종)**:
  - **2번 유닛 소환 (`UnitSummoned`)**: `SourcePreview` / `SourceOwnerId`
  - **3번 건물 건설 (`BuildingConstructed`)**: `SourcePreview` / `SourceOwnerId`
  - **10번 액티브 효과 발동 (`ActiveEffectActivated`)**: `SourcePreview` / `SourceOwnerId`
  - **21~34번 상태 부여·해제 (14종)**: `DrainedApplied`, `DrainedCleared`, `ErasureApplied`, `ErasureCleared`, `StunApplied`, `StunCleared`, `FreezeApplied`, `FreezeCleared`, `SealboundApplied`, `SealboundReleased`, `HidingApplied`, `HidingRevealed`, `HuanShuApplied`, `HuanShuCleared` (`TargetPreview` / `TargetOwnerId`)
- **화면 배치 및 라벨 (사용자 확정 A3)**:
  - `CombatLogAttackPreview.ShowSingle(card, owner)`가 왼쪽 카드 패널(`_left`)을 `(0, 20)` 중앙으로 이동시키고, 오른쪽 카드 패널(`_right`), 교차 검(`_swords`), 피해/사망 마커(`_left.Marker`)를 모두 숨긴다.
  - 카드 하단 라벨(`Role`)에는 `공격자 · ...` 대신 소유자만 **`플레이어`** 또는 **`상대방`**으로 표시한다.
  - 다시 1번 공격 기록(`Show`)을 열면 좌우 2장 배치와 교차 검이 즉시 복원된다.
- **시점별 능력치 캡처 및 색상 규칙 (사용자 확정 A1, A2, #10 및 공통 규칙)**:
  - **공통 색상 규칙**: 표시 시점의 공격력/현재 체력이 원래 공격력(`OriginalAttack`)/원래 최대 체력(`OriginalMaxHp`, 영구 카드 강화 포함)보다 높으면 **초록색**(`IncreasedColor`), 낮으면 **빨간색**(`DecreasedColor`), 같으면 **흰색**(`Color.white`)으로 표시한다.
  - **2번 유닛 소환 / 3번 건물 건설 (A1 확정 — 손패에 있을 때의 스탯)**: 소환 직후 전장 오라(오크 족장 등)나 서먼 트랩 피해가 반영되기 전, 손패에 있을 때의 공격력/체력(`OriginalAttack` / `OriginalMaxHp`, 기본값 + 영구 강화 수치)을 기록·표시한다.
  - **10번 액티브 효과 발동 (#10 확정 — 발동 직전 스탯)**: `ExecuteActiveEffectCommand`(서버) 및 `TutorialCombatLogRecorder`(로컬)에서 액티브 능력 실행 **직전(`before`)** 소환물의 공격력/현재 체력을 캡처해 표시한다.
  - **21~34번 상태 부여·해제 (A2 확정 — 상태 적용·해제 직후 스탯)**: 방전·망각·봉인·스턴·빙결·은신·환술이 부여되거나 해제된 **직후(`after`)** 소환물의 공격력/현재 체력(예: 개방 방주가 방전/망각/봉인으로 골드 공격력 보너스를 잃은 수치, 또는 해제되어 보너스를 회복한 수치)을 캡처해 표시한다.
- **검증 및 반영**:
  - 서버 오프라인 검사 `dotnet .\TempBuild\AgentAiChecks\Project333.AiChecks.dll --combat-log-previews-only` (공격 4시나리오 + 단독 카드 17종 전체, 양쪽 좌석 투영, 불변 복사, JSON 영속성 복원 통과) 및 격리 Unity EditMode `CombatLogAttackPreviewTests` / `TutorialCombatLogRecorderTests` / `BattleCombatLogEntryFormatterTests` 통과.
  - 온라인 전투 반영 시 **서버 재배포**와 **클라이언트 앱 재빌드**가 필요하며, DB 마이그레이션은 필요 없다.

## 마법·피해·회복·트랩 전투기록 누르기 미리보기 (7, 8, 9, 11, 12, 14, 41번) — 2026-10-31

2026-10-31 사용자 요청 및 결정 확정 (`AG-20261031-02`). 하스스톤 전투기록 시각화 형식을 참고해 **7번 마법 카드 사용(`SpellCast`)**, **11번 일반 공격·반격 이외의 피해(`Damage` — 대안 B)** 및 연관된 **8번 공격력 증가(`AttackBuffApplied`)**, **9번 파괴 표식 부여(`DestructionMarked`)**, **12번 체력 회복(`Healing`)**, **14번 파괴 표식에 의한 파괴(`DestroyedByMark`)**, **41번 트랩 발동(`TrapTriggered`)**의 길게 누르기 미리보기를 구현했다.

- **레이아웃 3종 규칙**:
  1. **비대상 마법 카드 및 시전 시 즉시 대상 효과가 없는 설치형 마법 (대상 없음)**:
     - 대상 카드: `ManaStone`(마정석), `ManaStoneBundle`(마정석 꾸러미), `JarOfGreed`(욕망의 단지), `Microreactor`(초소형 발전기), `PowerBank`(보조배터리), `TenThousandYearSnowGinseng`(영약:만년설삼), `MagicAmplificationCircle`(마법 증폭진), 그리고 시전 시점(`#7 SpellCast`)에는 즉시 피해 없이 턴 시작 지속 효과만 설치하는 `BiochemicalBomb`(생화학폭탄), `TimedBomb`(시한폭탄) 등.
     - 표시 방식: 화면 중앙에 사용한 마법 카드 1장을 표시(`ShowSingle`)하며, 마법 카드는 공격력/체력이 없으므로 좌우 하단 스탯 숫자를 숨기고(`HasCombatStats = false`) 카드 일러스트와 하단 소유자 라벨(`플레이어` / `상대방`)만 표시한다. (생화학폭탄·시한폭탄이 이후 턴 시작에 실제로 터져 피해를 줄 때는 **11번 피해 기록(`Damage`)**에서 `출처 마법 카드 → RightTriangle → 피해 대상 카드들`로 표시한다.)
  2. **단일 대상 카드 (`출처 카드 → 오른쪽 삼각형 → 대상 카드 1장`)**:
     - 대상 카드·기록: `Daehwandan`(대환단), `firebolt`(파이어볼), `CheonraJimang`(천라지망), `ExplosiveReactiveArmour`(트랩-반응장갑), `IronDome`(트랩-아이언돔), `HuanShu`(환술), `ManaShield`(마나쉴드), `PlasmaShield`(플라즈마쉴드), `SummonTrap`(트랩-서먼트랩), `TrapMirrorField`(트랩-미러필드), `TrapReturn`(트랩-리턴), `Gu`(고독) 및 단일 대상 피해/회복 기록(`Piercing`, `Fallout`, `DeckExhaustion`, `LifeSteal`).
     - 표시 방식: 왼쪽에 출처 카드(`_left`), 중앙에 오른쪽을 향하는 삼각형 아이콘(`Assets/Project333/Resources/Project333/UI/CombatLog/RightTriangle.png`, `Symbol.RightTriangle`), 오른쪽에 대상 카드(`_right`)를 배치한다.
     - 트랩 비공개 원칙: **40번 트랩 설치(`TrapInstalled`)** 기록은 절대 미리보기를 열지 않으며(`HasHoldPreview = false`), 조건을 만족해 트랩 종류가 밝혀지고 발동된 **41번 트랩 발동(`TrapTriggered`)** 기록에서만 미리보기를 표시한다.
  3. **다중 대상 카드 (`출처 카드 → 오른쪽 삼각형 → 대상 카드 5열 격자`)**:
     - 대상 카드·기록: `Firewall`(파이어월 시전 즉시 피해 및 턴 시작 피해), `BiochemicalBomb`(생화학폭탄 턴 시작 피해 `#11 Damage`), `FireSpear`(파이어스피어), `Earthquake`(지진), `Meteor`(메테오), `Nuclear`(뉴클리어), `NuclearPowerPlant`(원자력발전소), `BlueDragon`(블루드래곤), `RedDragon`(레드드래곤), `TimedBomb`(시한폭탄 폭발 피해 `#11 Damage`) 등에서 대상이 2장 이상인 경우.
     - 표시 방식: 왼쪽에 출처 카드, 중앙에 `RightTriangle.png` 아이콘, 오른쪽에 대상 카드들을 **한 줄에 최대 5장씩(`MaxTargetColumns = 5`)** 위에서부터 채우며 나란히 배치한다(예: 17장이면 $5 \times 4$ 배열로 위에서부터 채움).
     - 크기 조절: 오른쪽 대상 카드들의 크기는 대상 카드 수의 **5장 단위(`GetTargetCardScaleForCount`)**로 자동 조절된다(2~5장 `0.62`, 6~10장 `0.50`, 11~15장 `0.42`, 16장 이상 `0.35`).
- **카드 업그레이드 강화 적용 전투기록(`#4 UpgradeApplied`) 제거 (2026-10-31 `AG-20261031-04`)**:
  - `'강화 적용: 플레이어의 금광 채굴꾼에 ATK+0, HP+1 부여'`와 같은 카드 업그레이드 보너스 전투기록(`BattleCombatLogEntryType.UpgradeApplied`)은 전투기록 텍스트에 전혀 표시되지 않도록 서버 기록 생성(`BattleSession.AppendCardPlayedCombatLog`), 스냅샷 복원(`BattleSession.RestoreFromPersistenceSnapshot`), 클라이언트 수신(`OnlineBattleConnectionTester.ApplyCombatLogUpdate`), 텍스트 포매터(`BattleCombatLogEntryFormatter.Format`)에서 모두 제거·무시 처리했다(호환성을 위해 enum 숫자 `UpgradeApplied = 4`만 유지).
- **능력치·색상 및 3초 결과 아이콘 표시**:
  - 모든 대상 소환물 카드는 해당 전투기록이 발생하기 **직전(`before`) 시점의 공격력/체력**을 표시하며, 원래 공격력(`OriginalAttack`)/원래 최대 체력(`OriginalMaxHp`)보다 높으면 초록색(`IncreasedColor`), 낮으면 빨간색(`DecreasedColor`), 같으면 흰색으로 표시한다.
  - 대상 카드 중 실제로 피해를 입거나 체력을 회복하거나 파괴(처치)당한 카드들은 각 카드 이미지 위에 **3초 동안(`MarkerSeconds = 3f`)** 아래 아이콘을 표시한다:
    - **피해 아이콘**: `Assets/Project333/Resources/Project333/UI/CombatLog/DamageBurst.png` (`Symbol.Damage`) + `-피해량` 텍스트
    - **체력회복 아이콘**: `Assets/Project333/Resources/Project333/UI/CombatLog/HealIcon.png` (`Symbol.Heal`) + `+회복량` 텍스트
    - **해골 아이콘**: `Assets/Project333/Resources/Project333/UI/CombatLog/Skull.png` (`Symbol.Skull`, 사망/파괴 시 숫자 없이 표시)
- **검증 및 반영**:
  - `.NET8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과, 격리 Unity 6000.3.12f1 EditMode `CombatLogAttackPreviewTests` 통과.
  - 온라인 전투 반영 시 **서버 재배포**와 **클라이언트 앱 재빌드**가 필요하며, DB 마이그레이션은 필요 없다.

## 자원·드로우·생성 전투기록 누르기 미리보기 (17~20번) 및 해골 아이콘 교체 — 2026-11-03

2026-10-31 사용자 요청 (`AG-20261031-05`).

- **해골 아이콘 교체**: 사용자 첨부 그림의 바깥 체크무늬 배경을 투명 처리해 `Assets/Project333/Resources/Project333/UI/CombatLog/Skull.png`에 덮어썼다. `.meta` GUID `b5ff22930279cfb46a948783b7a926fa`는 유지하므로 기존 참조가 그대로 새 그림을 쓴다.
- **17번 자원 획득 / 18번 자원 소모**: `ShowSingle` 중앙 1장. 출처가 소환물이면 해당 명령 직전 스냅샷(같은 명령에서 소환된 경우 현재 보드)의 공격력/체력·색상, 마법이면 스탯 숨김. 서버 `BattleSession.AppendResourceChangeCombatLogs(beforeSnapshot, knownSourcePreview)`가 `SourcePreview`를 채운다.
- **19번 효과에 의한 카드 드로우**: `ShowDirected` — 출처 카드(욕망의 단지·개방 분타·유산 소환물) → `RightTriangle` → 드로우한 카드들(기존 5열 격자·크기 규칙).
  - 도메인 `BattleCardDrawEvent.DrawnCardId`로 실제 카드를 기록한다. 공유 `BattleEventDto`(양쪽에 방송)에는 넣지 않고 서버 내부 `_pendingCombatLogCardDrawEvents`에만 보관해 전투기록을 만든다.
  - 전에는 턴 종료 경로에서만 19번 기록을 만들었는데, 이제 모든 명령/턴 경로의 마지막(`AppendUnaccountedRemovalCombatLogs` 뒤)에서도 만든다. 그래서 욕망의 단지 사용과 공격·트랩·턴 시작으로 죽은 유산 소환물의 드로우도 기록된다.
- **20번 카드 생성·획득**: 로봇 공장(생성 직전 스냅샷) → `RightTriangle` → 생성된 로봇 카드 1장.
- **비공개 처리 (서버 권위)**: `BattleSession.ProjectCombatLogForViewer`가 보는 사람의 좌석과 기록 주인이 다르면 19/20번의 `TargetPreview`/`TargetPreviews`를 `BattleCombatLogCardSnapshotDto.CreateCardBack()`(`OpponentCardBack2`, 스탯 숨김)으로 바꾸고 `TargetCardId`를 비운 뒤 전송한다. 원본 기록(재접속 복원용)에는 실제 카드가 남지만 상대 클라이언트로 나가는 데이터에는 실제 카드 ID가 없다.
  - 로컬/튜토리얼 `TutorialCombatLogRecorder`는 Player 카드만 공개하고 AI 카드는 카드 뒷면으로 기록한다.
  - `CardArtworkLibrary`는 `OpponentCardBack2`가 없을 때 `OpponentCardBack`으로 대체한다.
- **검증**: `.NET 8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과(17번 ManaStone 단독, 19번 욕망의 단지 주인 실제 카드 2장/상대 뒷면 2장·직렬화 문자열에 실제 ID 없음, 20번 로봇 공장 주인 공개/상대 뒷면·`TargetCardId` 비움, 자원 기록 출처 미리보기, JSON 복원), 격리 Unity 6000.3.12f1 EditMode 5개 클래스(`CombatLogAttackPreviewTests`, `TutorialCombatLogRecorderTests`, `BattleCombatLogEntryFormatterTests`, `JarOfGreedTests`, `LegacyTests`) **188/188 통과**(`TempBuild/CombatLogResourceDraw_20261103/editmode2.xml`). 전체 `AiChecks`는 이번 변경과 무관한 `TimedBombChecks`(혼합 지속효과 순서)에서 먼저 실패했다.
  - 서버 재배포와 클라이언트 재빌드가 필요하며, DB 마이그레이션은 필요 없다.
