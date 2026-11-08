# Antigravity를 위한 최근 개발 인계

갱신: **2026-10-26 KST**. 사용자가 10-24에 공통 에이전트 문서를 요청한 뒤 진행한 개발을 현재 소스 기준으로 묶었다. 이전 채팅이나 Codex 메모리 없이 읽을 수 있는 시작점이다. 작업 규칙은 [AGENTS.md](../AGENTS.md), 전체 기능·우선순위는 [PROGRESS.md](../PROGRESS.md)가 기준이다. 이 문서가 새 기능 구현·배포 지시는 아니다.

## 처음 읽는 순서

1. `C:/Project_333`를 연다. `Documents/New project`는 옛 프로토타입이다.
2. 루트 `AGENTS.md` → `PROGRESS.md` → [작업 선호·보류 사항](conversation_decisions.md#작업-선호와-보류-사항--이어서-작업할-때-먼저-읽기) → 이 문서를 읽는다.
3. `git branch --show-current`, `git log -1 --oneline`, `git status --short`, `git diff --stat`, `git worktree list`로 실제 checkout을 확인한다. 현재 작업 기준 커밋은 `main`의 **41641a3**(10-26, `261026 15:55`)이며 이후 공통 연출 등 미커밋 변경이 있다. 과거 문서의 `99b4c50`은 10-24 작업 당시 기준이다.
4. 이번 문서 작업 최초 조사 시 미커밋 자산은 `Assets/Battle_VSlice.unity`, `Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF SDF.asset` 2개였고 staged·untracked는 없었다. 이후 Editor/사용자 작업으로 달라질 수 있다. 두 자산과 새 문서 diff를 사용자 변경으로 보존하고 현재 status를 다시 확인한다.
5. 새 작업 범위를 PROGRESS 예약표에 남긴 뒤 해당 기능 문서·소스·테스트만 읽는다. 같은 checkout은 한 명만 쓴다. 두 도구가 병렬로 구현하려면 [협업 절차](agent_collaboration.md)를 따라 별도 worktree를 준비한다. 등록된 과거 detached worktree를 새 활성 프로젝트로 추정하거나 정리하지 않는다.

Unity **6000.3.12f1**, uGUI, 서버 ASP.NET Core/.NET8, PostgreSQL18, HTTP/WSS 서버 권위 구조는 그대로다. 새 도구가 `AGENTS.md`를 자동으로 읽었다고 가정하지 말고 읽은 파일과 적용할 규칙을 첫 응답에서 확인한다.

## 2026-11-08 튜토리얼 승리 직후 두 대사에서 개방방주 대사창 화면 하단 배치

- 요청(AG-20261108-04): 튜토리얼 5단계 승리 직후 TutorialCue.PracticeVictory 및 TutorialCue.PracticeVictoryIsolation 두 대사에서 개방방주 대사창이 중앙 승리 애니메이션을 가리지 않도록 화면 아래쪽으로 이동.
- 수정: TutorialSceneController.BattlePractice.cs의 RefreshBattlePracticeDialogueLayout에서 두 승리 큐일 때 대사창과 초상화를 화면 하단(y: .018f ~ .19f)에 배치하도록 수정 및 TutorialCombatBattleTests에 검증 추가.
- 검증: 격리 Unity 6000.3.12f1 EditMode TutorialCombatBattleTests **12/12 통과**(TempBuild/TutorialVictoryDialogueLayout_20261108/editmode.xml).

## 2026-11-08 튜토리얼 스켈레톤 아처 빨간색 활 문양 설명 대사 추가 및 챕터 6 인덱스 동기화

- 요청(AG-20261108-03): 튜토리얼 5단계 TutorialCue.DamageTypeColor 대사 다음에 스켈레톤 아처 카드 미리보기 창을 유지하면서 "스켈레톤 아처는 빨간색 활 문양이니까 원거리 공격 유닛이고, 일반 공격시 물리 데미지를 입힌다는 뜻이지." 대사를 추가.
- 수정: TutorialSession.cs에 동일 TutorialCue.DamageTypeColor 단계 추가 및 SixthChapterStep = 109 동기화, docs/tutorial_script_ko.md 갱신.
- 검증: 격리 Unity 6000.3.12f1 EditMode 3개 클래스(TutorialSessionTests, TutorialCombatBattleTests, TutorialUpgradePracticeTests) **56/56 통과**(TempBuild/TutorialArcherDamageTypeStep_20261108/editmode.xml).

## 2026-11-08 튜토리얼 소환물 미리보기 실습을 아군 스켈레톤 아처 전용으로 변경

- 요청(AG-20261108-02): 튜토리얼 5단계 소환물 미리보기 실습(TutorialAction.PreviewOccupant) 대사를 "소환물을 꾹 누르면 카드와 현재 상태를 자세히 볼 수 있네. 소환된 우리 스켈레톤 아처를 길게 눌러보게."로 변경하고, 초록색 하이라이트와 실습 판정을 아군 스켈레톤 아처에만 적용.
- 수정: TutorialSession.cs, TutorialSceneController.CombatPractice.cs, BattleBootstrapper.TutorialCombatPractice.cs, docs/tutorial_script_ko.md 수정 및 TutorialCombatBattleTests.PreviewOccupantRequiresPlayerSkeletonArcherAndIgnoresOtherOccupants 추가.
- 검증: 격리 Unity 6000.3.12f1 EditMode TutorialCombatBattleTests **12/12 통과**(TempBuild/TutorialArcherPreview_20261108/editmode.xml).

## 2026-11-08 튜토리얼 불필요 대사 2줄 제거 및 챕터 시작 인덱스 동기화

- 요청(AG-20261108-01): TutorialSession.cs의 빈 문자열 대사(Line 43)와 전투 골드 자원 중복 설명("새로운 전투가 시작되면 전투 자원도 새로 시작하네.", Line 98) 2개 항목 제거.
- 수정: TutorialSession.cs의 Steps 배열에서 해당 2줄을 제거하고, 챕터 시작 인덱스 상수(FirstChapterStep=4, SecondChapterStep=14, ThirdChapterStep=29, FourthChapterStep=44, FifthChapterStep=69, SixthChapterStep=108)를 단계를 맞추어 동기화.
- 검증: 격리 Unity 6000.3.12f1 EditMode 4개 클래스(TutorialSessionTests, TutorialCombatBattleTests, TutorialResourceBattleTests, TutorialUpgradePracticeTests) **61/61 통과**(TempBuild/TutorialStepSync_20261108/).

## 2026-11-07 튜토리얼 마나의 샘(건물) 이동 시도 시 이동 불가 안내창 표시 누락 수정

- 원인(`AG-20261107-02`): 튜토리얼 5단계에서 전열 차단 공격 시도(`TryAttackWithPlayerOccupant`)는 `catch (Exception ex)`에서 `if (_tutorialBattleEntry) ShowCommandErrorToast(ex.Message);`를 호출했으나, 이동 처리(`TryMovePlayerOccupant`)에는 해당 호출이 빠져 있어 마나의 샘(`ManaPond`) 이동 시도(`TutorialAction.AttemptBuildingMove`) 시 `"이 유닛은 이동 불가능합니다."` 안내창이 표시되지 않았다.
- 수정: `BattleBootstrapper.TryMovePlayerOccupant`의 `catch (Exception ex)` 블록에 `if (_tutorialBattleEntry) ShowCommandErrorToast(ex.Message);`를 추가하고, `BattlePlayerInputController.HandleTileClicked`에서 이동 실패 후 `!CanOccupantAct` 상태가 된 경우 선택 하이라이트(`ClearUnitSelection`)도 즉시 해제하도록 보완했다.
- 검증: 격리 Unity 6000.3.12f1 EditMode `TutorialCombatBattleTests` **11/11 통과**(`TempBuild/TutorialBuildingMoveToast_20261107/editmode.xml`). 클라이언트 앱 재빌드 필요, 서버 재배포·DB 마이그레이션 불필요.

## 2026-11-07 파이어볼로 원자력 발전소 파괴 시 모든 소환물에 파이어볼 이펙트가 재생되던 버그 수정

- 원인(`AG-20261107-01`): `BattleBootstrapper.PlaySpellEffectAnimationSequence`에서 `request.SpellEffectTargets`가 비어 있으면 `request.Impacts`의 모든 타일에 프레임 마법 이펙트(`firebolt`)를 재생했는데, 단일 대상 마법 `firebolt`가 `SpellEffectTargets`를 채우지 않아 피 1인 원자력 발전소(`NuclearPowerPlant`)를 파이어볼로 파괴할 때 연쇄 폭발(`BattleValueChangeCause.NuclearPowerPlant`)로 `request.Impacts`에 들어간 양측 전장 모든 소환물 타일에 파이어볼 애니메이션이 재생됐다.
- 수정: `BattleBootstrapper.cs`의 `BuildOnlineImpactAnimationRequest`, `BuildImpactAnimationRequest`, `TryPlayCardOnBoard`, `RunAiTurnSequence`에서 `SpellEffectId == FireboltSpellEffectId`일 때 파이어볼 대상 타일만 `request.SpellEffectTargets`에 추가하도록 수정했다.
- 검증: 격리 Unity 6000.3.12f1 EditMode `NuclearExplosionPresentationTests.FireboltDestroyingOwnNuclearPowerPlantTargetsOnlyThePlantForFireboltAnimation` 통과(`TempBuild/FireboltNuclearPlantFix_20261107/editmode_firebolt.xml`). 클라이언트 앱 재빌드 필요, 서버 재배포·DB 마이그레이션 불필요.

## 2026-11-03 전투기록 해골 아이콘 교체 및 #17~#20(자원·드로우·생성) 누르기 미리보기

- 사용자 확정(`AG-20261031-05`): `Skull.png`를 새 해골 그림(바깥 체크무늬 투명 처리, GUID `b5ff22930279cfb46a948783b7a926fa` 유지)으로 교체하고, 추천 2순위 전투기록 4종(#17 `ResourceGained`, #18 `ResourceSpent`는 출처 카드 1장 중앙 단독 표시; #19 `CardDrawn`, #20 `CardGenerated`는 `출처 카드 -> RightTriangle -> 드로우/생성 카드` 방향형 표시)을 구현했다.
- 비공개 원칙: #19 드로우 카드와 #20 생성 로봇 카드는 해당 플레이어 본인에게만 실제 카드를 공개하고, 상대방에게는 서버 `BattleSession.ProjectCombatLogForViewer`가 `OpponentCardBack2` 카드 뒷면(`HasCombatStats = false`, `TargetCardId = ""`)으로 치환해 전송한다. 로컬/튜토리얼 `TutorialCombatLogRecorder`도 Player 카드만 공개하고 AI 카드는 카드 뒷면으로 기록한다.
- 검증: `.NET 8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과, 격리 Unity 6000.3.12f1 EditMode 5개 클래스 **188/188 통과**(`TempBuild/CombatLogResourceDraw_20261103/editmode2.xml`). 앱 재빌드 및 서버 재배포 필요, DB 마이그레이션 없음. [상세](combat_log_attack_preview.md#자원드로우생성-전투기록-누르기-미리보기-1720번-및-해골-아이콘-교체--2026-11-03).

## 2026-10-29 공격 전투기록 누르기 미리보기

사용자 확정: 공격 문장을 누르고 있는 동안 왼쪽 공격자·오른쪽 피격자 카드와 검 교차를 표시한다. 공격 **직전** 공격력/현재HP를 당시 원래 값과 비교해 초록/빨강/흰색으로 표시하고, 실제 피해 또는 사망 해골을3초만 표시한다. 상대 마스터 예시는 공격력3(33이 아님)으로 정정됐다. 누르기0.25초·드래그는 기존스크롤·해제/이탈/포커스 해제는 닫힘. 새 서버 갱신 중에도 처음 복사한 기록을 유지한다.

선택적 `SourcePreview/TargetPreview` DTO를 서버 기록·깊은 복사·전체JSON 복원·Unity 관점 투영과 로컬/튜토리얼 수집에 연결했다. 구 기록의 당시 수치는 추측하지 않으며 이번 단계는 공격 기록만 시각화한다. 기존33개 창/판정/손패 비공개 유지, 새PNG·사용자 씬/폰트 변경 없음. [수정 위치·규칙](combat_log_attack_preview.md).

검증: Unity 관련62개(최종 새15+일반 로컬 게이트웨이/콜백1+기존46), 실제 캡처3장과3.05초 경과 확인. .NET8 서버 오류0/기존nullable101; 오프라인 서버4시나리오·양좌석·사망/피해·손패 숨김·전체JSON복원 통과. 검증 중 UI Renderer 누락/float 경계와 테스트 폰트·컴파일 입력을 고친 이력은 세션 `CombatLogHoldPreview_20261029`에 보존했다. 실기기·SDK광고·운영 접속/배포는 별도. 앱 재빌드와 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제.

## 2026-10-29 파이어월 마나4·즉시 피해

사용자 확정: 선택한5×1에 사용 즉시 및 다음2회 전역 턴 시작마다 마법 피해. 기본15/레벨당+1·현재 주문력·기존 방어/사망/유산 유지, 즉시 피해는 후속2회 횟수에서 제외한다. `FirewallEffectService`로 시전/턴 시작 피해를 공유하고 시전은 기존 SpellCast 큐만 사용해 중복 불꽃을 방지했다. 로컬/AI 팝업과 온라인 RuntimeId 보호, JSON·네이티브·검증기·미리보기/문서를 함께 갱신했다. 카드 아트·불꽃·씬은 변경하지 않았다.

격리 Unity 관련212개 통과(앞선176 + 최종 즉시피해12/연출24), .NET8 컴파일 오류0/기존 nullable101. 초기 테스트 컴파일2오류, 잘못된 tooltip overload1건과 검증 사본 누락 리소스14건을 고친 뒤 최종36/36 통과했다. 증거는 세션 `FirewallImmediate_20261029/tests2.xml`, `tests3.xml`, `verification.json`. SDK 광고·실기기·운영 배포는 미검증. 앱 재빌드/서버 재배포 필요, 새 DB 마이그레이션 없음. 예약 해제. [규칙·연결](firewall_presentation.md#즉시-피해-추가--2026-10-29).

## 2026-10-29 카드 드래그 중 손패 자동 접힘

사용자 확정: 펼친 손패에서 드래그하면 나머지 손패가 접히고, 카드를 손패로 되돌리면 즉시 펼쳐진다. 끌어내기/복귀를 반복할 수 있으며 복귀 후 놓으면 취소, 사용 성공 후에는 접힘 유지. 드래그 시작 위치·가로 범위를 고정하고140/70 경계 완충으로 접힘 배치에 따른 흔들림을 방지했다. 실제 드래그 카드의 크기/좌표는 유지하고 입력·뷰 취소/교체·동기 사용 중 원본 제거도 처리한다. 첫 접힌 손패의 드래그는 기존처럼 펼치기만 한다. `BattlePlayerInputController`, `HandPresenter` 및 기존 입력/드래그/스펠 검사3파일만 추가 수정. 새9 포함 격리 Unity **92/92** 통과, 앱·실기기·실서버 별도. 서버/DB·사용자 씬/폰트 쓰기 없음. [조절·판정·증거](compact_battle_hand.md#드래그-중-손패-접힘복귀-2026-10-29).

## 2026-10-29 내 손패 장수별 간격

사용자 확정: 장수별 간격을 Inspector에 노출하고 화면 SafeArea를 가로로 벗어날 때만 **간격을** 축소한다. 2~10장 기본값250/225/200/175/150/125/100/75/50, 카드1장은 중앙. HandPresenter의 새 `Spacing By Card Count`와 snapshot 깊은 복사/구 snapshot 누락 호환, 회전된 카드의 실제 피벗·선택 크기를 포함한 경계 계산을 추가했다. 기존 Min/Max/WidthFactor는 호환용 숨김이며 내 손패에서 무효; 상대 손패의 기존 API는 유지한다. 사용자 전투 씬·폰트·모래시계 위치·카드 그림은 수정하지 않았다. [설정/제한](compact_battle_hand.md#장수별-손패-간격과-화면-초과-조절-2026-10-29).

검증: 별도 Unity6000.3.12f1/D3D11에서 최종 배치·Inspector/기존 씬 호환 **31/31** 통과. 기존 접힘·드로우·드래그·상대 손패·마법 사용·임시 복제 검사 **41/43** 통과. 나머지2건은 모래시계가 카드 안에 있어야 한다는 예전 기대값과 현재 바깥으로 옮긴 위치의 충돌이며, 이번 수정 전 세 소스/배치 검사로 되돌린 검증 사본에서도 동일하게2건 실패했다. 모래시계 위치·검사는 변경하지 않았다. 실제 카드 렌더5장(2·5·10장, 좁은900px 및 선택1.75배)을 확인. 증거는 세션 `HandCountSpacing_20261029/tests.xml`, `final-layout.xml`, `baseline-hourglass.xml` 및 검증 사본 `Captures/HandCountSpacing_20261029/`. 광고 SDK 없는 별도 사본의 범위이며 앱 빌드·실기기·서버 접속은 수행하지 않았다.

## 10-24 이후 변경 — 현재 최종 동작

검사 수는 각 변경 당시의 격리 검증 기록이다. 아래 숫자를 합산해 전체 회귀 성공률로 표현하지 않는다. 이번 문서 갱신에서는 게임 테스트를 다시 실행하지 않았다.

| 범위 | 현재 동작·보존할 결정 | 기존 검증 / 상세 |
|---|---|---|
| 상대 카드 공개 | 상대가 사용한 카드 공개에 로봇 합체 이벤트를 포함. 공개 대기열이 남으면 승패 배너/화면 복귀가 먼저 끝내지 않게 처리. 상대 트랩 설치는 물음표/트랩 표시만, 발동은 실제 카드를 양측 공개. 일반 자기 사용은 제외 | 67/67. 환술 누락의 모든 실제 상황을 재현한 것은 아님. [상세](opponent_card_reveal.md) |
| 덱 목록 | 덱 빌딩·드래프트의 보이는 목록 기본 글자32pt. 사용자가 별도로 편집한 글자 크기는 보존 | 40/40. [덱 빌딩](deck_building_pixel_ui.md), [드래프트](draft_pixel_ui.md) |
| 승패 효과음 | 배너 시작에 Victory/Defeat MP3를 한 번 재생. 승리·패배 볼륨을 별도 설정하며 현재 **코드 초기값0.65/1**. 공통 효과음·마스터 볼륨을 각각 한 번 곱함 | 최초 음원12/12, 개별 볼륨 분리24/24는 당시 값의 검사. 현재0.65 문서 대조는 정적 확인. [볼륨 편집](battle_outcome_presentation.md#승리패배-볼륨을-따로-수정하기-2026-10-24-후속) |
| 튜토리얼 이전 | 다음 왼쪽 ‘이전’으로 방문한 대사만 복습. 완료된 실습/소환/자원/강화를 되돌리거나 재실행하지 않음. 복습 중 실습·초록 포커스 중단, 현재 위치 복귀 시 미완료 실습 요구 복원 | 74/74, 새11사례. 휴대폰 완주 별도. [구현](tutorial_implementation.md#대화-이전다음-이동-2026-10-24) |
| 임시 복제 | 손패·미리보기 오른쪽 위에 사용자 황금 모래시계. 미리보기 ‘임시 복제: 사용하지 않으면 이번 턴 종료 시 손패에서 사라집니다.’. 자기 턴 종료 확정 후 실제 제거된 RuntimeId만 **0.65초 회색 입자** 소멸 | 새8/8, 관련76/78 당시 기존 낙진 문구 검사2개 실패는 아래 참고. [표시·조절](compact_battle_hand.md#임시-복제-손패-표시와-소멸-2026-10-24) |
| 공통 스펠 사용 | 대상 없는 위쪽 드래그 스펠은 놓은 자리에서 푸른 조각으로 **0.145초** 분해. 응답 대기 중 원본을 숨기고 정지 사본 유지. 사용 확정 이벤트 즉시 시작하며 지진 등 전장 연출/손패 갱신 종료를 기다리지 않음. 거절 시 복원 | 지연 갱신 재현 후25/25. 초기0.58→0.29초는 이력이며 복원 금지. [상세](hand_spell_use_presentation.md) |
| 턴 시작 순서(10-26) | 준비/기본골드 → 직접파괴 → 낙진/시한폭탄/파이어월·생화학폭탄 → 살아남은 소환물 생산/지속마법 지급·기간 → 전력소모 → 공장 → 드로우 → 마왕 부활/일반 봉인 → Main. 지속 피해에 죽은 생산물은 자원도 내지 않음. 기존 첫3/4골드와 드로우1·전력의 골드 대체 유지 | 관련330/330·서버 컴파일 오류0. 원본 테스트13실패 재현. 온라인 반영 시 서버 재배포, 로컬/튜토리얼 반영 시 사용자 앱 빌드 필요. DB 마이그레이션 없음. [규칙](battle_rules.md#turn-structure), [계약](implementation_contract.md#turn-structure) |
| 타일 지속 피해 범위 밖 빈 타일 우선(10-26) | AI 전장에 활성 생화학폭탄·파이어월이 있고 범위 밖 빈 타일이 있으면 범위 안 소환 후보를 제외(`AiActionGenerator`)하고, 턴 종료 전 범위 안 이동 가능 아군 유닛(마스터 제외)을 실제 생존/HP 이득 확인 시 안전 빈 타일로 대피(`AiDecisionService.RequiredHazardEscape`, `AiMovementPolicy`). 광마 등 광폭화 유닛의 폭탄 자해 소환·잔류 방지, 안전 빈 타일이 없을 때는 기존 가치 비교 유지 | AI EditMode 20개 클래스 **385/385 통과**(신규7사례 포함), 서버 컴파일 오류0(기존 경고101). 온라인 반영 시 서버 재배포 필요. [상세](pve_ai.md#타일-지속-피해-범위-밖-빈-타일-우선-2026-10-26) |
| 환술 긴급 방어 마스터 체력 고정(10-26) | `AiPositionEvaluator.HuanShuDisruptionValue`에서 긴급 방어 보너스(`ownMasterExposureHp <= damage * 2`, `60 × diversionChance × min(1, damage / ownMasterExposureHp)`)를 행동 후 `master.CurrentHp` 대신 의사결정 시작 시점 마스터 체력(`ownMasterExposureHp`)으로 평가. 이미 환술에 걸린 고화력 적이 있을 때 AI 마스터가 반격 자해로 체력 경계를 넘어 가짜 점수를 얻는 공격 방지, 실제 위기 체력에서 손패 환술 시전 보너스는 유지 | AI EditMode 8개 클래스 **206/206 통과**(신규3사례 포함), 서버 컴파일 오류0(기존 경고101), `AiChecks` 통과. 온라인 반영 시 서버 재배포 필요. [상세](pve_ai.md#환술-긴급-방어-가치의-의사결정-시작-마스터-체력-고정-2026-10-26) |
| 공통 공격력·최대 체력 증가 | 대환단 전용 형태를 재사용해 모든 소환물에 공격력 빨강/최대 체력 초록/둘 다 노랑으로0.62초 비차단 재생. RuntimeId별 이전 확정 상태와 비교해 광역/양쪽/유닛·건물·마스터에 적용. 회복·첫 표시·재접속은 제외. 대환단 기본+30은 자기 마스터 빨강 | 공통8사례(기존3이관+신규5)+관련 **68/68 통과**, 실제3색 PNG 확인. 전투 규칙/새 예시 카드·서버 변경 없음. [상세](stat_increase_presentation.md) |
| 트랩 입력 | 트랩5종도 마스터 지정 대신 위쪽 허공 드래그와 같은0.145초 연출. 기존 무료 설치·중복 제한·살아 있는 마스터·발동 비용 유지. 일반 설치 ACK는 자기 미확정 트랩만 제출 순서로 연결 | 37/37. 상대 트랩 정보나 DTO를 새로 노출하지 않음. [상세](hand_spell_use_presentation.md) |
| 덱·상대 손패 편집 | 기존 Player/OpponentDeck 루트·6장 이미지/RectTransform을 Play에서 덮어쓰지 않음. 비어 있는 이미지에만 기본 뒷면 적용. 상대 손패는 이미지 보존, 수량별 부채꼴 정렬 유지 | 관련41/41, 이후 정리 보완한 검사15/15. Play 밖에서 편집·씬 저장 필요. [편집법](editable_scene_layouts.md#덱상대-손패의-카드-뒷면-편집-2026-10-25) |
| 내 덱 드로우 | 자기 실제 드로우만 오른쪽 아래 실제 PlayerDeck에서 뒤집히며 손패로 이동. **0.38초 / 묶음 간격0.07초**. 실제 입력 객체는 슬롯에 남으며 RuntimeId로 숨김/복구. 최초·재접속 손패/상대 드로우/임시 생성/멀리건 교체 제외 | 최초49/49와 렌더, 배율 어긋남 회귀78/78. 당시 실전17·19턴의 정확한 트리거는 미확정. [상세](hand_draw_presentation.md) |
| 드로우 출발점 캐시 | 전투 손패 Presenter가 출발 Pose를 공유·재사용. 화면/안전 영역/덱·부모 변환/Canvas/스프라이트/카메라 정보 변경 때만 재변환. 취소·재접속 시 초기화. **도착점**은 움직이는 손패를 계속 따름 | 최신83/83. 고정 배치100조회→전체 출발 변환1회. FPS/CPU 개선 실측은 아님. 10-25 무조건 매 프레임 출발 재변환은 이 방식으로 대체됨. [상세](hand_draw_presentation.md#출발-좌표-캐시-2026-10-26) |
| 반격 뒤 Idle | 방어/무적으로 피해0인 생존 반격자도 현재 RuntimeId를 확인해 Idle 복귀. 공격자 사망 조기 종료 전에 처리하고, 죽은 반격자의 Death는 유지. 오크 족장 등 사용자 클립은 수정하지 않음 | 재현3/3, 관련 **고유41개** 최종 통과(36/41 후 누락 자산 보완한5/5의 합집합). 한 번에41/41인 XML은 아님. [상세](counterattack_presentation.md) |
| 시간 만료 턴 전달 | 서버가 일반 자동 턴 종료 시 다음 타이머를 먼저 예약하고 각 접속자에 이벤트→전용 StateView를 병렬 전달한 뒤 감사/DB 작업 대기. 종료된 전투의 최종 상태는 결과 저장 후 전달. 규칙·시간·비공개 필터는 유지 | 오프라인 실제 처리 함수16/16, 빌드 오류0. 운영 적용 여부 별도. [서버 변경](turn_timeout_handoff.md) |

## 수정 위치를 빠르게 찾기

아래 경로는 활성 저장소 루트 기준이다. 전체 연출을 타일 인덱스/CardId만으로 추적하도록 바꾸지 않는다.

| 작업 | 주요 소스 / 테스트 |
|---|---|
| 상대 공개·결과 순서 | `Runtime/Presentation/Battle/`의 `OpponentPlayedCardRevealRules.cs`, `BattleOutcomeTextView.cs`, `BattleBootstrapper.cs`; `OpponentCardRevealQueueTests.cs`, `OpponentPlayedCardRevealRulesTests.cs` |
| 튜토리얼 복습 | `Runtime/Presentation/Tutorial/TutorialSceneController.Navigation.cs`, `Runtime/Application/Tutorial/TutorialSession.cs` |
| 임시 복제 | `Runtime/Presentation/Hand/HandPresenter.TemporaryReplicate.cs`, `TemporaryReplicateHourglass.cs`, `TemporaryReplicateDissolveView.cs`; `TemporaryReplicatePresentationTests.cs` |
| 스펠 사용·트랩·능력치 증가 | `Runtime/Presentation/Hand/HandPresenter.SpellCardUse.cs`, `HandCardTextView.SpellCardUse.cs`, `SpellCardUseVfx.cs`; `Runtime/Presentation/Battle/OccupantStatIncreaseVfx.cs`, `BattleScreenPresenter.cs`, `BattleBootstrapper.cs`; `SpellCardUsePresentationTests.cs`, `OccupantStatIncreasePresentationTests.cs` |
| 내 드로우 | `Runtime/Presentation/Hand/HandPresenter.Draw.cs`, `CardDrawFlight.cs`, `CardDrawOriginCache.cs`, `HandCardTextView.Draw.cs`; `HandDrawPresentationTests.cs`, `CardDrawOriginCacheTests.cs` |
| 덱·상대 손패 | `Runtime/Presentation/Hand/PlayerDeckPresenter.cs`, `OpponentDeckPresenter.cs`, `OpponentHandPresenter.cs`; `PlayerDeckPresenterTests.cs`, `OpponentHandPresenterTests.cs` |
| 반격 복귀 | `Runtime/Presentation/Battle/BattleBootstrapper.cs`의 `PlayAttackAnimationSequence`; `CounterattackIdleRecoveryTests.cs` |
| 시간 만료 전달 | `Server/Project333.PvpServer/Program.cs`의 `ResolveTurnTimeoutAsync`/`BroadcastTurnTransitionAsync`; `Server/Project333.AiChecks/TurnTimeoutDeliveryChecks.cs`, 필터 `--turn-timeout-delivery-only` |

표의 `Runtime/`는 `Assets/Project333/Runtime/`, 파일명만 적은 Unity 테스트는 `Assets/Project333/Tests/EditMode/` 아래다. 실제 파일 존재와 참조를 확인한 뒤 편집한다. 자세한 실패→수정→검증 이력은 [codex_handoff](codex_handoff.md)의10-24~26절을 선택해 읽는다.

## 구현 완료와 실제 반영 구분

| 대상 | 현재 확인 범위 / 다음 행동 |
|---|---|
| 클라이언트 연출·UI | 소스와 변경별 격리 검사 완료. 최신 설치 앱 BuildId·휴대폰 확인은 미확인. **사용자가** 앱 빌드·설치, 에이전트는 요청 없이 APK/PC 앱을 만들거나 설치하지 않음. 위 클라이언트 연출만으로 서버 재배포/마이그레이션 불필요 |
| 서버 턴 만료 수정 | 커밋에 포함되었지만 운영 release가 이 코드를 실행하는지는 재확인 필요. 반영하려면 서버 publish·배포·재시작. 이 수정 자체에는 새 SQL이나 새 클라이언트 불필요 |
| 단일 로그인 | 소스 최신 SQL은 `0025_single_login_family.sql`. 마지막 명시적 운영 DB 출력0024는 과거 이전 당시 기록. 이후 사용자가 로그인 교체를 관측했으므로 ‘0025 미적용’으로 단정하지 않음. 운영 migration/release와 호환 묶음 먼저 확인 |
| 운영·백업 | 운영 `https://api.after333.com`, WSS `/battle`, DB `project333_live`. `api-staging`은 옛 시험 주소. Lightsail 이전·21시 백업·격리 복원은 [날짜가 있는 기록](current_project_status.md)이며 오늘 원격 재확인한 상태가 아님 |

## 남은 확인과 보존 제약

- 기존 `CardPreviewDetailsTests`의 낙진2개 기대 문구(중첩/영구 관련)는 10-26에 현재 확정 툴팁 ‘양쪽 턴 시작마다 10의 고정 피해를 받는다.’ 기준으로 갱신해 격리 Unity 15/15(관련 미리보기·손패 8개 클래스 113/113) 통과를 확인했다. 별도로 `OccupantStatusTooltipCatalogTests`에는 소환물 아래 부착 디버프 제목을 예전 ‘낙진’으로 기대하는 검사 4건(현재 코드는 ‘낙진: 양쪽 턴 시작마다 10의 고정 피해를 입는다.’)이 남아 있으므로, 현재 문구를 예전 문자열로 되돌리거나 전체 테스트 통과로 확대 해석하지 않는다.
- SDK 없는 검증 사본에서 native LevelPlay를 제외한 검사 이력이 있다. 광고·Google 실제 제공자·운영 네트워크를 검증한 결과로 확대하지 않는다. 메인 프로젝트 SDK는 보존한다.
- 검증 파일 일부는 Git 밖의 로컬 세션 폴더에 있다. 예: `DrawOriginCache_20261026/Final.xml`·`verification.json`, `CounterIdleBug_20261025/Final.xml`·`SwapCompleted.xml`, `TurnTimeoutHandoff_20261025/Final16.log`. 상세 위치는 handoff에 있다. 다른 PC에서 없으면 문서 기록과 원본 검사 코드를 구별하고 필요한 범위만 재검증한다.
- 사용자 Editor의 Play 중단·씬 강제 저장·전체 UI 재생성은 하지 않는다. 수동 덱 배치/이미지, GaebangChief Idle, 현재 오크 족장/용 애니메이션과 `.meta`를 보존한다. RuntimeId 기반 손패·소환물 신원, 서버 권위·비공개 투영, 튜토리얼 로컬 격리를 유지한다.
- 서버 부팅 자동 시작/자동 재시작, GitHub Actions, 전투 기록 자동 정리는 보류다. 문서 인계를 운영 변경·커밋/push·새 기능 승인으로 해석하지 않는다. 게임 규칙을 바꾸지 않는 문서 정리에는 게임 테스트를 반복하지 않는다.

## Antigravity 첫 요청 예시

```text
C:/Project_333에서 시작해. AGENTS.md, PROGRESS.md, docs/conversation_decisions.md의 작업 선호·보류 사항,
docs/antigravity_handoff.md를 먼저 읽어. 이전 Codex 채팅이나 메모리를 공유한다고 가정하지 마.
현재 branch/HEAD, git status와 등록 worktree를 확인하고 읽은 문서·현재 상태·미확인 사항을 짧게 알려줘.
사용자 변경을 보존하고, 이번에 내가 요청한 작업의 파일 범위만 PROGRESS에 예약한 뒤 진행해.
같은 checkout에는 한 에이전트만 쓰고, 완료하면 소스·검증·앱 설치·서버 배포·DB 적용 상태를 구분해 기록해.
```

이 요청 뒤에 실제로 맡길 한 가지 작업을 덧붙인다. 이 문서를 읽는 것만으로 자동 재배포하거나 위 미검증 항목 전부를 실행하게 하지 않는다.


## 2026-10-28 추가 — 보상 상자 임시 개봉음

`RunRewardRevealView`에서 개봉이 실제 시작될 때만 `GoldSound.mp3`를 한 번 재생한다. `OpeningSoundResourcePath`와 `OpeningSoundVolume`(0.3)으로 전용 음원 교체·음량 조절, 공통 마스터/효과음 설정 준수. 골드 버튼 음원 자체와 서버 보상 규칙은 변경하지 않았다. 변경 파일 별도 C# 컴파일 오류0/경고0 및 음원/메타 경로 정적 확인; Unity Play/실기기 청취 미실행. 사용자 앱 재빌드 필요, 서버/DB 작업 없음. [설정·교체](audio_settings.md#런-보상-상자-개봉-효과음-2026-10-28). 작업 예약 해제.


## 2026-10-28 추가 — 특수효과 목록 최신화

[special_effects.md](special_effects.md)를 현재 카드60종·전투 규칙·공통 능력/상태 카탈로그와 정적 대조했다. 맨 앞에 공통17종(낙진 포함)과 현재 보유 카드, 방전/망각/봉인/환술/낙진/스턴/빙결, 전용 효과 구분을 추가했다. 파이어월 기본15 예시, 트랩 허공 드래그, 턴 시작10단계, 대환단 공격력만 증가·임시 복제 표시 등을 갱신했다. 일반 봉인·은신·추가 공격·기본 무적의 현재 카드 미할당을 구분한다. 부착 낙진 제목의 기존 ‘이름만’ 문서와 실제 카탈로그 차이 및 환술 아군 피해의 흡혈 주의는 숨기지 않고 유지했다. 문서/링크/diff 정적 검증, 전투 테스트 재실행 없음. 코드·카드·서버·DB·앱 변경 없음. 예약 해제.


## 2026-10-28 추가 — 욕망의 단지·자동 공통 사용 연출

JarOfGreed/중립레어/골드1/즉시2드로우, 후속 요청으로 덱빌딩On·보상Off·강화불가. 사용자 PNG 보존, native 자산/카탈로그·생성 도구/메타 추가. `SpellCardUseRules.IsUntargeted`를 입력과 시전이 함께 사용해 새 대상 없는 마법에도 자동 공통 푸른 조각 연출을 적용한다. 타일 대상 마법·로봇 합체 입력은 유지. 공유 코드+실제 서버 보상 필터18/18, 서버 컴파일0오류/101경고, 런타임 별도컴파일0오류/88경고(LevelPlay별도 제외), 생성기/Unity조건부검사·기존공통연출테스트 소스0오류/0경고. Unity초기로드 정체로 native import/Play 연출 실행 미완료; native자산은 기존 직렬화 형식으로 작성·정적검증. 사용자 앱 재빌드와 온라인 서버/Data 재배포 필요·DB migration 없음. 작업 예약 해제. [전체 근거](jar_of_greed.md).


## 2026-10-28 추가 — 공격 잔여 점·스탯창 크기 (AttackReadyDot_20261028)

TileTextView의 Occupant Stats 크기112×26/폰트18/아이콘16×16은 유지하고 사용자 조절 방법(예:150×34/24/20×20)을 [stat_bar_authoring.md](stat_bar_authoring.md)에 기록했다. 자식 Text/RectTransform이나 코드 초기값만 수정하면 기존 씬 값이 유지/덮어쓰기되는 점도 설명한다.

스탯창 오른쪽에 기본8px 초록 원을 공유 Sprite로 생성, Show/Size/Color 필드 추가. OccupantAttackReadiness는 내 Main·생존 아군·잔여 공격·양수 공격력·소환 제한/방전/봉인/스턴/빙결 해제를 확인하며 마스터/공격 가능 건물 포함. 특정 대상/자원 지불 보증은 아니다. BoardPresenter가 같은 투영 스냅샷을 SetAttackIndicatorBattle로 제공하므로 어느 온라인 좌석도 내 편만 표시한다. 수치 영역은 점용 공간을 항상 예약하고 너비/높이 변경을 반영, sorting4950/입력 비차단 유지. 기존 씬·자산 편집 보존.

실제 분리 Unity6000.3.12f1 D3D11 **32/32 통과**: 조건17 + 새 실제 Play/Canvas2 + 기존 TileVisualSwap5/StatIncrease8. 별도 실제 공유 AttackService/TurnStartService·StateView projector17/17, Unity DLL 참조 런타임 별도 컴파일0오류/88경고. 검증 사본에서만 누락된 LevelPlay SDK 경로를 제외; 활성 프로젝트 광고 SDK는 변경 없음. 초기 테스트 사본 경로/SDK 누락 실패는 유지했으며 수정 뒤 실행 결과만 최종으로 기록. 단순 컴파일만으로 Unity 실행 성공을 주장한 것이 아님. 앱 빌드/실기기/서버·DB·커밋 없음; 사용자 앱 재빌드 필요. 작업 예약 해제.


## 2026-10-28 추가 — 스탯 아이콘·숫자 중앙 정렬 (CenteredStats_20261028)

체력·ATK 문양도 숫자와 함께 중앙에 오도록 후속 요청 반영. TileTextView가 HP/ATK 각 아이콘+숫자를 하나의 줄로 계산해 고정 초록 점 영역을 제외한 공간의 중앙에 배치한다. 각 TMP도 Center, 실제 preferredWidth로 자릿수 갱신 즉시 재중앙화. 좁으면 묶음만 비율 축소, 점 배치 코드/표시 조건·사용자 씬 크기/폰트 보존. 숫자 바인딩 뒤·상태색 적용 전 layout을 계산해 방전/봉인의 회색을 덮어쓰지 않는다. 기존 UI 검사에 전체 묶음 중앙·자리수 변화·점 고정·방전 색 복구 검증 추가. 초기15/15 뒤 색 적용 순서를 보완하고 최종 실제 분리 Unity15/15(Play/Canvas2·이동교환5·스탯상승8) 재통과. 사용자 앱 재빌드 필요, 서버/DB 변경 없음. [편집 문서](stat_bar_authoring.md). 예약 해제.


## 2026-10-28 추가 — 서버 AI 턴 종료 `처리 중...` 후속 (AiTurnTimerPadding_20261028)

사용자는10-25 서버 수정 재배포 완료 후에도 PvP 서버 AI의 자기10·12턴 무행동 만료에서3~7초 처리 중을 보고했다. 별도 클라이언트 원인 재현: 서버의 AI 연출 보정 deadline(예:73+7=80초)을 OnlineBattleConnectionTester가 명목73초로 잘라 UI가 먼저0이 됐다. 실제 deadline 기반 계산으로 변경, 서버 스냅샷의 남은 시간을 기준으로 기존 기기 시계 역행 보호 유지. 턴/연출 규칙·UI 안내 문구·서버는 변경 없음. 수정 전 신규13중9실패, 수정 후 실제 분리 Unity46/46(신규13/기존연결10/AI페이싱23), 현재 소스·테스트 해시 일치. 사용자 앱 재빌드/설치 필요, 이번 수정의 서버 재배포·DB migration 불필요. 방금 실전 로그/기기 확인은 미수집이므로 모든 지연의 원인으로 단정하지 않는다. [근거/반영/확인법](turn_timeout_handoff.md). 예약 해제.


## 2026-10-28 추가 — 공격 방식 아이콘10행 (StatIcons10Rows_20261028)

사용자 현재 검/활/지팡이9×8 도안을 각각9×10으로 확장했다. TileTextView의 세 `new[]` 배열만 수정, 기존 색·아이콘 표시 크기·중앙 정렬·초록 점 유지. CreateIconSprite의 rows.Count 계산 그대로. 사용자 기존 도안 순서 보존하며 검신/활곡선/지팡이 손잡이 부분2행씩 추가. 행/열 길이·diff 정적 검증, Unity/앱 실행 미검증. 편집/캐시 갱신 안내는 [스탯창 문서](stat_bar_authoring.md). 예약 해제.


## 2026-10-29 추가 — 두 쉴드 사용자 시트·고정 크기 (FixedShields_20261029)

사용자 ManaShiled.png(3072×512,6컷), PlasmaShiled.png(4096×512,8컷)을 기존 SpellEffects/.../Formation.png에 바이트 그대로 연결했다. Unity 네이티브 SpriteDataProvider/전체 capability 검사 후 한 줄512셀로 재슬라이스; textureGUID와 유지되는 Formation_N spriteID 보존, Point/FullRect/max4096. 메타 텍스트 편집 없음. 카드 그림/카드 규칙은 변경 없음.

두 Vfx는 FixedFrameSize512 UI단위 고정, 몸체 alphaBounds/대각선/폭·높이별 확대 및 프레임별 접지 측정 제거. 독립 Canvas4908 유지,512셀 아래16px 발 기준점만 추적하며 소환물 스케일/반전은 상속하지 않는다. 기존12fps/fade.15/opacity/성공 이벤트/대상 신원·취소·busy를 유지; Mana6컷이라 총.65초, Plasma8컷.817초. 미래 크기 편집은 각 Vfx의 FixedFrameSize.

실제 분리 Unity6000.3.12f1 D3D11 최종28/28(네이티브12+연출16) 및 양쪽4종16장 렌더 확인. 첫26/28의2실패는 현재1086×1448 카드 아트에 대한 구512 기대값; 아트는 그대로 두고 whole authored dimensions로 수정. 이후256에서28통과했으나 작은 렌더를 확인해512+발밑 앵커로 최종 재검사28통과. 현재 소스/PNG/native meta/검사 해시가 실행 사본과 동일. 앱 재빌드 필요·운영/서버/DB 변경 없음. [마나 상세](mana_shield_presentation.md), [플라즈마 상세](plasma_shield_presentation.md). 예약 해제.


## 2026-10-29 — 마나 쉴드 두 번째 첨부 시트

ManaShiled.png 추가 원본으로 ManaShield/Formation.png만 교체(SHA256 BEAF4201E0C4E8631F29280FE58590C81F2F3FF7B9427D194D3B0470C2DAB91F). 같은3072×512·6컷이므로 meta/GUID/슬라이스 불변. 기존 고정512·12fps·발밑 기준점/소유자/취소 처리 유지. 관련 격리 Unity14/14 통과, 앱/실기기 별도·서버 변경 없음. 위치는 두 Vfx.cs의 UpdateLayout anchoredPosition에 Vector2 UI오프셋을 더해 사용자가 편집할 수 있다고 안내했으며 위치 코드를 대신 변경하지 않았다. [예시](mana_shield_presentation.md#애니메이션-위치-직접-조절).


## 2026-10-29 — PvP AI 매칭 타이머 정체

일반 PvP20초 이후 무한 대기 보고를 점검해, 같은MatchId로 재생성된 방의 AI 타이머 등록이 옛 방의 문자열 키에 거부되는 경로를 재현했다. `PvpAiFallbackScheduler`가 방 객체별 소유권/종료 후 접속 재확인·재등록과1초 재시도를 담당하고 Program은 기존 DB 승격/상태 발송 콜백을 유지한다. 최초20초·DB 실대기 검증/사람 예약 우선/재접속70초는 유지. 서버 컴파일 오류0·기존경고101, 새 오프라인 타이머6그룹(교체100회·동시200+200회)+기존 재접속10그룹 통과. 당시 운영 로그 없이 보고 사례의 원인까지 확정한 것은 아니다. 소스 완료·운영 재배포/실전 별도, 앱 재빌드/DB마이그레이션 불필요. [원인·증거·명령](battle_modes.md#ai-매칭-타이머-세션-교체-경쟁-수정-2026-10-29).


## 2026-10-29 — 유산(Legacy)

공통 키워드18번째. 툴팁 “사망/파괴 시 효과를 발동한다.”. 좀비·스켈레톤 아처·스켈레톤은 사망/직접 파괴 시 현재 소유자1드로우(강화 무관), 원전 폭발은 기존30물리를 유산으로 통합·전력+3 유지. LegacyRules 등록표와 CardDefinition/OccupantState 파생 보유값, 공통 제거/연쇄 큐·드로우 이벤트 사용. 억제·반환/합체/이동 제외·손패 제한·덱 소진과 동시 사망 승패 처리. JSON/4native 설명/검증·공통미리보기 갱신. 카드PNG/애니메이션·수치/강화는 보존. 새24 포함 관련226검사 통과, 서버0오류/101기존경고. 아처는 현재 사용자의 전용 컨트롤러/Idle을 보존하도록 옛 공유-Skeleton 가정의 자산 검사 갱신. 소스 완료·클라이언트빌드/서버배포/실기기 별도, DB마이그레이션 없음. [검증](special_effects.md#2026-10-29-유산-검증).


## 2026-10-30 — CombatLogIcons_20261030

사용자 첨부 해골/교차 검 PNG를 UI/CombatLog Resources에 원본 그대로 추가하고 기존 벡터 아이콘을 대체했다. 비율 유지·기존140/166영역, 피해 문양·3초 표시·사용자 글꼴/씬 유지. 동일 뷰의 피해↔해골 전환 시 텍스처 재질 갱신을 연결했다. Unity 생성 Single Sprite 메타·최대512·알파 사용. 별도 Unity6000.3.12f1/D3D11 기존 미리보기 검사 **16/16 통과**, 임포트·렌더 캡처 확인. 세션 CombatLogIcons_20261030/{import.log,tests.xml,tests.log,Captures,verification.json}에 증거. SDK 광고·실기기·앱 빌드·운영 배포/DB 미수행. 앱 재빌드 필요, 서버/마이그레이션 불필요. 예약 해제. [편집 위치](combat_log_attack_preview.md#전투기록-아이콘-교체--2026-10-30).

## 2026-10-30 — 전투기록 #2, #3, #10, #21~#34 단일 카드 길게 누르기 미리보기 (`AG-20261030-02`)

사용자 확정: 전투기록 카탈로그 46종 중 **#2 유닛 소환(`UnitSummoned`)**, **#3 건물 건설(`BuildingConstructed`)**, **#10 액티브 효과 발동(`ActiveEffectActivated`)**, **#21~#34 상태 부여·해제 14종(`DrainedApplied/Cleared`, `ErasureApplied/Cleared`, `StunApplied/Cleared`, `FreezeApplied/Cleared`, `SealboundApplied/Released`, `HidingApplied/Revealed`, `HuanShuApplied/Cleared`)** 총 17종에 대해 문장을 0.25초 이상 길게 누르면 화면 중앙(`(0, 20)`)에 해당 카드 1장만 표시한다. 두 번째 카드·검 교차·피해/사망 마커는 숨기고 카드 하단에 소유자 라벨(`플레이어`/`상대방`)을 표시한다.

당시 공격력/체력 시점 및 색상 규칙:
- 모든 전투기록 미리보기는 기록 당시의 공격력(`Attack`)·체력(`Hp`)을 해당 카드의 원래 공격력(`OriginalAttack`)·원래 최대 체력(`OriginalMaxHp`, 기본 수치 + 영구 강화 보너스)과 비교해 높으면 초록(`#56F06F`), 낮으면 빨강(`#FF5B5B`), 같으면 흰색으로 표시한다.
- **#2 유닛 소환 · #3 건물 건설**: 손패에 있을 당시 수치(`OriginalAttack` / `OriginalMaxHp`, 전장 오라나 소환 트랩 피해 적용 전)를 `SourcePreview`에 기록한다.
- **#10 액티브 효과 발동**: 액티브 발동 직전(`before`) 소환물 수치를 `SourcePreview`에 기록한다.
- **#21~#34 상태 부여·해제 14종**: 상태 부여/해제 직후(`after`) 소환물 수치를 `TargetPreview`에 기록한다(예: 망각 부여 시 골드 비례 공격력이 제거된 수치, 망각 해제 시 복구된 초록색 수치).

구현 및 검증: `BattleCombatLogEntryDto`, `CombatLogAttackPreview`, `CombatLogHoldTarget`, `BattleBootstrapper(.CombatHistory)`, `TutorialCombatLogRecorder`, `BattleSession`을 확장하고 `docs/combat_log_catalog.md`, `docs/combat_log_attack_preview.md`를 갱신했다. `.NET8` 서버 Release 빌드 **0 오류**, `Project333.AiChecks --combat-log-previews-only` **PASS**(공격 4종 + 단일 카드 17종, 양 좌석 관점 투영, 비공개 손패 보호, 전체 스냅샷 JSON 복원), 격리 Unity 6000.3.12f1 EditMode(`CombatLogAttackPreviewTests`, `TutorialCombatLogRecorderTests`, `BattleCombatLogEntryFormatterTests`) **49/49 통과**(`TempBuild/CombatLogSinglePreview_20261030/editmode.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제. [상세](combat_log_attack_preview.md#단일-카드-전투기록-미리보기-2-3-10-2134--2026-10-30).

## 2026-10-31 — 전투기록 피해 폭발 아이콘(`DamageBurst.png`) 교체 (`AG-20261031-01`)

사용자 첨부 폭발 PNG(1024×1024 RGBA)를 원본 바이트 그대로 `Assets/Project333/Resources/Project333/UI/CombatLog/DamageBurst.png`에 추가하고 `CombatLogSymbolGraphic`의 `Symbol.Damage`가 해당 스프라이트를 로드해 그리도록 연결했다(스프라이트 미존재 시 기존 벡터 폭발 fallback 유지). 피해 텍스트(`-3` 등)의 글꼴·크기 편집 위치(`CombatLogAttackPreview.cs` `MakePanel`의 `p.Damage = Label(p.Marker.transform, "Damage", font, 65);`)를 문서와 응답에 안내했다. 격리 Unity 6000.3.12f1 EditMode `CombatLogAttackPreviewTests` **18/18 통과**(`TempBuild/CombatLogDamageIcon_20261031/editmode.xml`) 및 실제 렌더 캡처(`01-damage.png`) 확인. 클라이언트 앱 재빌드 필요, 서버/DB 변경 없음. 예약 해제. [편집 위치](combat_log_attack_preview.md#전투기록-아이콘-교체--2026-10-30--2026-10-31).

## 2026-10-31 — 전투기록 #7, #8, #9, #11, #12, #14, #41 길게 누르기 미리보기 (`AG-20261031-02`)

사용자 확정: **#7 마법 카드 사용(`SpellCast`)**, **#11 일반 공격·반격 이외의 피해(`Damage` — 대안 B)** 및 연관된 **#8 공격력 증가(`AttackBuffApplied`)**, **#9 파괴 표식 부여(`DestructionMarked`)**, **#12 체력 회복(`Healing`)**, **#14 파괴 표식에 의한 파괴(`DestroyedByMark`)**, **#41 트랩 발동(`TrapTriggered`)**을 0.25초 이상 길게 누르면 하스스톤 형식의 시각화를 표시한다:
- **비대상 마법**(마정석, 마정석 꾸러미, 욕망의 단지, 초소형 발전기, 보조배터리, 영약:만년설삼, 마법 증폭진 등): 화면 중앙 1장 단독 표시(`ShowSingle`, 마법 카드는 공격력/체력 숫자를 숨기고 하단 소유자 라벨만 표시).
- **단일 대상**(대환단, 파이어볼, 천라지망, 트랩 5종 발동, 환술, 마나쉴드, 플라즈마쉴드, 고독 및 단일 대상 피해/회복): 왼쪽 출처 카드 → 중앙 `RightTriangle.png`(`Symbol.RightTriangle`) → 오른쪽 대상 카드 1장. **#40 트랩 설치(`TrapInstalled`)**는 비공개 원칙에 따라 미리보기를 열지 않으며 발동된 **#41(`TrapTriggered`)**에서만 표시.
- **다중 대상**(파이어월, 생화학폭탄, 파이어스피어, 지진, 메테오, 뉴클리어, 원자력발전소, 블루드래곤, 레드드래곤, 시한폭탄 등 2장 이상): 왼쪽 출처 카드 → 중앙 `RightTriangle.png` → 오른쪽 대상 카드들을 **한 줄당 5장씩(`MaxTargetColumns = 5`, 예: 17장이면 5×4 격자)** 위에서부터 채우며 나란히 배치하고, 대상 카드 수의 5장 단위(`GetTargetCardScaleForCount`)로 카드 크기를 자동 조절.
- **직전 스탯·색상 및 3초 결과 마커**: 대상 소환물 카드는 전투기록 직전(`before`) 공격력/체력을 원래 값(`OriginalAttack`/`OriginalMaxHp`)과 비교해 초록/빨강/흰색으로 표시하고, 실제 피해·회복·처치(파괴) 대상 카드 위에는 3초 동안 `DamageBurst.png`(`-N`), `HealIcon.png`(`+N`), `Skull.png`를 표시한다.

검증: `.NET8` 서버 Release 빌드 **0 오류**, `Project333.AiChecks --combat-log-previews-only` **PASS**, 격리 Unity 6000.3.12f1 EditMode `CombatLogAttackPreviewTests` **19/19 통과**(`TempBuild/CombatLogDirectedPreview_20261031/editmode.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제. [상세](combat_log_attack_preview.md#마법피해회복트랩-전투기록-누르기-미리보기-7-8-9-11-12-14-41번--2026-10-31).

## 2026-10-31 — Lightsail 운영 DB(`project333_live`) 선물함 지급 절차 문서화 (`AG-20261031-03`)

Lightsail 이전 이후 기존 `ManageGifts.ps1` 기본 실행이 예전 로컬 Windows DB 설정(`DeploymentSettings-20260909.clixml` → `project333`)을 바라보아 실제 운영 서버(`project333_live`)에 반영되지 않던 원인과, `Project333.GiftAdmin`의 루프백(`localhost`/`127.0.0.1`) 보안 제약을 유지하면서 **SSH 로컬 포트 포워딩(`127.0.0.1:15432 -> Lightsail 127.0.0.1:5432`) + `ManageGifts.ps1 -UseEnvironmentDatabase`**로 서버 골드(`-ResourceGold`)와 서버 티켓(`-Tickets`)을 지급하는 단계별 절차(`Accounts` → `Prepare` → `Preview` → `Send`)를 [docs/gift_inbox.md](gift_inbox.md#lightsail-운영-dbproject333_live-선물-지급-절차--2026-10-31-갱신)와 [docs/lightsail_operations.md](lightsail_operations.md#관리자-선물함-지급-서버-골드티켓)에 문서화했다(systemd `EnvironmentFile`의 `\"` 이스케이프 해제를 위해 `systemd-run ... /usr/bin/printenv PROJECT333_DB_CONNECTION` 사용). 코드·서버·DB 변경 없음. 예약 해제.

## 2026-10-31 — 생화학폭탄 마법 사용(#7 SpellCast) 단일 카드 미리보기 및 카드 업그레이드 전투기록(#4 UpgradeApplied) 제거 (`AG-20261031-04`)

- **생화학폭탄 마법카드 사용(`#7 SpellCast`) 미리보기 수정**:
  - `'플레이어가 생화학폭탄 마법카드를 사용했다.'` / `'상대방이 생화학폭탄 마법카드를 사용했다.'` 전투기록을 길게 누를 때 대상 없이 **생화학폭탄 마법카드 이미지 1장만** 중앙에 표시되도록 `BattleSession.BuildSpellTargetPreviews`와 `TutorialCombatLogRecorder`를 수정했다(이후 매 턴 시작에 생화학폭탄이 터져 피해를 입히는 `#11 Damage` 전투기록에서는 기존대로 `생화학폭탄 -> 피해 대상 카드들`을 모두 표시한다).
- **카드 업그레이드 전투기록(`#4 UpgradeApplied`) 완전 제거**:
  - `'강화 적용: 플레이어의 금광 채굴꾼에 ATK+0, HP+1 부여'` 등 카드 강화 보너스 전투기록이 전혀 출력되지 않도록 서버 기록 생성(`BattleSession.AppendCardPlayedCombatLog`), 영속 스냅샷 복원(`BattleSession.RestoreFromPersistenceSnapshot`), 클라이언트 수신(`OnlineBattleConnectionTester.ApplyCombatLogUpdate`), 텍스트 포매터(`BattleCombatLogEntryFormatter.Format`)에서 모두 제거·무시 처리했다(프로토콜 호환성을 위해 `BattleCombatLogEntryType.UpgradeApplied = 4` enum 정의만 유지).
- **검증**: `.NET8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과, 격리 Unity 6000.3.12f1 EditMode `BattleCombatLogEntryFormatterTests` **26/26 통과**(`TempBuild/CombatLogBiochemicalAndUpgrade_20261031/editmode.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제.
