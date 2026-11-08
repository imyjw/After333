# After333 — 공통 진행 현황

갱신: **2026-10-30 KST**, 담당: Codex. 소스 기준 `main` / HEAD `41641a3`(10-26 커밋) + 이후 미커밋 자산·문서 변경. 이 파일은 Codex·Antigravity의 현재 상태 요약이며 이전 대화나 실서비스 전수 검증을 대신하지 않는다. 공통 작업 규칙은 [AGENTS.md](AGENTS.md). Antigravity 첫 사용은 [최근 개발 인계](docs/antigravity_handoff.md) → [협업 절차](docs/agent_collaboration.md)를 읽는다.

## 진행 중 작업 예약

현재 이 문서에 등록된 활성 작업 없음. 실제 다른 작업자의 활동까지 확인한 뜻은 아니다. 새 작업자는 현재 checkout·사용자 변경을 확인한 뒤 아래에 범위를 예약한다.

| 작업 ID | 담당 | 목표 / 완료 조건 | 파일 범위 | branch / checkout | 상태·다음 행동 |
|---|---|---|---|---|---|

## 2026-11-08 — 튜토리얼 승리 직후 두 대사에서 개방방주 대사창 화면 하단 배치 (AG-20261108-04)

- **요청**: 튜토리얼 5단계 전투 승리 직후 "잘했네! 상대 마스터를 쓰러뜨리고 승리했군."(TutorialCue.PracticeVictory)과 "실제 골드 던전이라면 승리 보상을 받겠지만, 튜토리얼에서는 계정의 골드나 전적을 바꾸지 않네."(TutorialCue.PracticeVictoryIsolation) 두 대사가 나올 때 화면 중앙의 승리 애니메이션을 가리지 않도록 개방방주 대사창을 화면 아래쪽으로 이동.
- **수정**: TutorialSceneController.BattlePractice.cs의 RefreshBattlePracticeDialogueLayout에서 현재 표시 중인 대사의 큐가 TutorialCue.PracticeVictory 또는 TutorialCue.PracticeVictoryIsolation일 때도 카드 미리보기와 동일하게 대사창((.24f, .018f) ~ (.77f, .19f))과 초상화((.155f, .008f) ~ (.235f, .18f))를 화면 하단에 배치하도록 수정.
- **검증**: 격리 Unity 6000.3.12f1 EditMode TutorialCombatBattleTests **12/12 통과**(승리 두 대사 하단 배치 검증 포함, TempBuild/TutorialVictoryDialogueLayout_20261108/editmode.xml). 클라이언트 앱 빌드 시 반영되며 서버/DB 변경 없음. 예약 해제.

## 2026-11-08 — 튜토리얼 스켈레톤 아처 빨간색 활 문양 설명 대사 추가 및 챕터 6 시작 인덱스 동기화 (AG-20261108-03)

- **요청**: 튜토리얼 5단계 무기 색깔별 데미지 설명(TutorialCue.DamageTypeColor) 다음 단계로 스켈레톤 아처 카드 미리보기 창과 빨간색 활 문양 하이라이트를 유지하면서 "스켈레톤 아처는 빨간색 활 문양이니까 원거리 공격 유닛이고, 일반 공격시 물리 데미지를 입힌다는 뜻이지." 대사를 추가.
- **수정**: TutorialSession.cs의 DamageTypeColor 대사 직후에 동일 TutorialCue.DamageTypeColor를 갖는 새 TutorialStep을 추가하고 SixthChapterStep을 108 → 109로 동기화했으며, docs/tutorial_script_ko.md도 함께 갱신했다.
- **검증**: 격리 Unity 6000.3.12f1 EditMode 3개 클래스(TutorialSessionTests 40/40, TutorialCombatBattleTests 12/12, TutorialUpgradePracticeTests 4/4) **56/56 통과**(TempBuild/TutorialArcherDamageTypeStep_20261108/editmode.xml). 클라이언트 앱 빌드 시 반영되며 서버/DB 변경 없음. 예약 해제.

## 2026-11-08 — 튜토리얼 소환물 미리보기 실습을 아군 스켈레톤 아처 전용으로 변경 (AG-20261108-02)

- **요청**: 튜토리얼 5단계 소환물 미리보기 실습(TutorialAction.PreviewOccupant)의 대사를 "소환물을 꾹 누르면 카드와 현재 상태를 자세히 볼 수 있네. 소환된 우리 스켈레톤 아처를 길게 눌러보게."로 변경하고, 초록색 하이라이트와 길게 누르기 완료 판정을 모든 소환물이 아닌 소환된 아군 스켈레톤 아처(TutorialCombatBattle.ArcherTile)에만 적용.
- **수정**: TutorialSession.cs의 대사 및 docs/tutorial_script_ko.md 갱신, TutorialSceneController.CombatPractice.cs에서 PreviewOccupant 하이라이트를 PracticeTile(TutorialCombatBattle.ArcherTile) 단독 표시로 제한, BattleBootstrapper.TutorialCombatPractice.cs의 TutorialOccupantPreviewReleased 및 OnCombatPreviewReleased에서 	ile.OwnerId == PlayerId.Player && tile.CurrentOccupant?.CardId == "SkeletonArcher"일 때만 실습이 완료되도록 수정.
- **검증**: 격리 Unity 6000.3.12f1 EditMode TutorialCombatBattleTests **12/12 통과**(신규 PreviewOccupantRequiresPlayerSkeletonArcherAndIgnoresOtherOccupants 포함, TempBuild/TutorialArcherPreview_20261108/editmode.xml). 클라이언트 앱 빌드 시 반영되며 서버/DB 변경 없음. 예약 해제.

## 2026-11-08 — 튜토리얼 불필요 대사 2줄 제거 및 챕터 시작 인덱스 동기화 (AG-20261108-01)

- **원인/요청**: 사용자 요청으로 TutorialSession.cs의 빈 문자열 대사(Line 43)와 전투 골드 자원 중복 설명("새로운 전투가 시작되면 전투 자원도 새로 시작하네.", Line 98) 2개 항목 제거.
- **수정**: TutorialSession.cs의 Steps 배열에서 해당 2줄을 제거하고, 챕터 시작 인덱스 상수(FirstChapterStep=4, SecondChapterStep=14, ThirdChapterStep=29, FourthChapterStep=44, FifthChapterStep=69, SixthChapterStep=108)를 단계를 맞추어 동기화.
- **검증**: 격리 Unity 6000.3.12f1 EditMode 4개 클래스(TutorialSessionTests 40/40, TutorialCombatBattleTests 11/11, TutorialResourceBattleTests 6/6, TutorialUpgradePracticeTests 4/4) **61/61 통과**(TempBuild/TutorialStepSync_20261108/). 클라이언트 앱 빌드 시 반영되며 서버/DB 변경 없음. 예약 해제.
## 2026-11-07 — 튜토리얼 마나의 샘(건물) 이동 시도 시 이동 불가 안내창(토스트) 표시 누락 수정 (AG-20261107-02)

- **원인**: 튜토리얼 5단계에서 전열 차단 공격 시도(`TryAttackWithPlayerOccupant`)는 `catch (Exception ex)`에서 `if (_tutorialBattleEntry) ShowCommandErrorToast(ex.Message);`를 호출해 공통 오류 안내창을 띄웠으나, 이동 처리(`TryMovePlayerOccupant`)의 `catch (Exception ex)`에는 해당 호출이 빠져 있어 건물인 마나의 샘(`ManaPond`)을 이동 시도(`TutorialAction.AttemptBuildingMove`)할 때 `MoveService.Move`의 `"이 유닛은 이동 불가능합니다."` 예외 메시지가 화면 안내창으로 표시되지 않았다.
- **수정**: `BattleBootstrapper.TryMovePlayerOccupant`의 `catch (Exception ex)` 블록에서도 `if (_tutorialBattleEntry) ShowCommandErrorToast(ex.Message);`를 호출하도록 추가하고, `BattlePlayerInputController.HandleTileClicked`에서 이동 실패 후 해당 소환물이 더 이상 행동할 수 없는 상태(`!CanOccupantAct`)가 되면 선택 강조(`ClearUnitSelection`)도 함께 해제하도록 보완했다.
- **검증**: 격리 Unity 6000.3.12f1 EditMode `TutorialCombatBattleTests` **11/11 통과**(신규 `AttemptBuildingMoveShowsCommandErrorToastAndCompletesPracticeAction` 포함, `TempBuild/TutorialBuildingMoveToast_20261107/editmode.xml`). 클라이언트 전용 연출/UI 수정이므로 사용자 앱 재빌드만 필요하며 서버 재배포·DB 마이그레이션은 불필요하다. 예약 해제.

## 2026-11-07 — 파이어볼로 원자력 발전소 파괴 시 모든 소환물에 파이어볼 애니메이션이 재생되던 버그 수정 (AG-20261107-01)

- **원인**: `BattleBootstrapper.PlaySpellEffectAnimationSequence`는 프레임 기반 마법 이펙트(`FireboltSpellEffectId = "firebolt"`)를 재생할 때 `request.SpellEffectTargets`가 비어 있으면 `request.Impacts`의 모든 타일에 마법 이펙트를 재생하도록 되어 있었다. 기존에는 단일 대상 마법인 파이어볼(`firebolt`) 사용 시 `request.SpellEffectTargets`를 채우지 않았기 때문에, 피 1인 원자력 발전소(`NuclearPowerPlant`)를 파이어볼로 파괴하면 같은 이벤트 묶음에서 발생한 원자력 발전소 파괴 광역 피해(양측 전장 모든 소환물 고정 피해 30) 대상들이 모두 `request.Impacts`에 포함되어 양측 전장의 모든 소환물에 파이어볼 애니메이션이 재생됐다.
- **수정**: `BattleBootstrapper.cs`의 온라인(`BuildOnlineImpactAnimationRequest`)과 로컬/AI(`BuildImpactAnimationRequest`, `TryPlayCardOnBoard`, `RunAiTurnSequence`) 경로에서 `SpellEffectId == FireboltSpellEffectId`일 때 파이어볼이 실제로 지정·타격한 타일(`sourceEvent.TargetCoord` 또는 `BattleValueChangeCause.Spell` 팝업 좌표)만 `request.SpellEffectTargets`에 기록하도록 수정했다. 이로써 파이어볼 애니메이션은 지정한 원자력 발전소 타일에만 재생되고, 연쇄 폭발로 피해를 입은 나머지 소환물들은 피격/사망 반응과 피해 수치만 정상 표시한다.
- **검증**: 격리 Unity 6000.3.12f1 EditMode `NuclearExplosionPresentationTests.FireboltDestroyingOwnNuclearPowerPlantTargetsOnlyThePlantForFireboltAnimation` 통과(`TempBuild/FireboltNuclearPlantFix_20261107/editmode_firebolt.xml`). 클라이언트 전용 연출 수정이므로 사용자 앱 재빌드만 필요하며 서버 재배포·DB 마이그레이션은 불필요하다. 예약 해제.

## 2026-11-03 — 전투기록 해골 아이콘(`Skull.png`) 교체 및 추천 2순위(#17 자원 획득, #18 자원 소모, #19 카드 드로우, #20 카드 생성) 길게 누르기 미리보기 (AG-20261031-05)

- **해골 아이콘(`Skull.png`) 교체**: 사용자 첨부 이미지의 바깥 체크무늬 배경을 투명 처리(32bpp RGBA)해 `Assets/Project333/Resources/Project333/UI/CombatLog/Skull.png`에 반영했으며, 기존 `.meta` GUID(`b5ff22930279cfb46a948783b7a926fa`)를 그대로 유지했다.
- **#17 자원 획득(`ResourceGained`) / #18 자원 소모(`ResourceSpent`) 단독 카드 미리보기**: 화면 중앙에 효과를 발동한 출처 카드 1장을 단독 표시(`ShowSingle`; 소환물이면 직전 스냅샷/현재 보드 스탯 및 초록/빨강/흰색 색상, 마법이면 스탯 숨김).
- **#19 효과에 의한 카드 드로우(`CardDrawn`) 및 #20 카드 생성·획득(`CardGenerated`) 방향형 미리보기 + 좌석별 비공개**:
  - `출처 카드 → RightTriangle.png → 드로우/생성된 카드들(5열 격자)` 형식으로 표시(`ShowDirected`).
  - `BattleCardDrawEvent.DrawnCardId`를 도메인/서비스에 추가하되 공유 `BattleEventDto`에는 포함하지 않고 서버 내부 `_pendingCombatLogCardDrawEvents`로만 전달하며, 모든 명령/턴 경로의 마지막에서도 `#19 CardDrawn` 기록을 생성하도록 보완해 욕망의 단지 및 유산 사망 드로우까지 모두 기록한다.
  - 서버 `BattleSession.ProjectCombatLogForViewer`가 조회 좌석(`AssignedSeatId`)과 기록 주인이 다를 때 `#19 CardDrawn`과 `#20 CardGenerated`의 대상 카드 스냅샷을 **카드 뒷면(`OpponentCardBack2`, `HasCombatStats = false`)**으로 치환하고 `TargetCardId`를 비워 전송한다(내 드로우/생성은 나에게만 실제 카드를 공개하고 상대방에게는 뒷면으로 표시, 상대방의 드로우/생성은 나에게 뒷면으로 표시하고 상대방 본인에게만 공개). 로컬/튜토리얼 `TutorialCombatLogRecorder`도 동일하게 Player 카드만 공개하고 AI 카드는 카드 뒷면으로 기록한다.
- **검증**: `.NET 8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과, 격리 Unity 6000.3.12f1 EditMode 5개 클래스(`CombatLogAttackPreviewTests`, `TutorialCombatLogRecorderTests`, `BattleCombatLogEntryFormatterTests`, `JarOfGreedTests`, `LegacyTests`) **188/188 통과**(`TempBuild/CombatLogResourceDraw_20261103/editmode2.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제. [상세](docs/combat_log_attack_preview.md#자원드로우생성-전투기록-누르기-미리보기-1720번-및-해골-아이콘-교체--2026-11-03).

## 2026-10-31 — 생화학폭탄 마법 사용(#7 SpellCast) 단일 카드 미리보기 및 카드 업그레이드 전투기록(#4 UpgradeApplied) 제거 (AG-20261031-04)

- **생화학폭탄 마법카드 사용(`#7 SpellCast`) 단일 카드 표시**: `'플레이어가 생화학폭탄 마법카드를 사용했다.'` / `'상대방이 생화학폭탄 마법카드를 사용했다.'` 문장을 길게 누르면 대상 없이 **생화학폭탄 마법카드 1장만** 중앙에 표시되도록 `BattleSession.BuildSpellTargetPreviews` 및 `TutorialCombatLogRecorder`를 수정했다(이후 턴 시작에 발생하는 `#11 Damage` 피해 기록에서는 `생화학폭탄 -> 범위 내 피해 대상 전체`를 그대로 표시).
- **카드 업그레이드 강화 적용(`#4 UpgradeApplied`) 전투기록 완전 제거**: `'강화 적용: ...에 ATK+..., HP+... 부여'` 텍스트가 전투기록에 일절 나오지 않도록 서버 생성(`BattleSession.AppendCardPlayedCombatLog`), 스냅샷 복원(`BattleSession.RestoreFromPersistenceSnapshot`), 클라이언트 수신(`OnlineBattleConnectionTester.ApplyCombatLogUpdate`), 포매터(`BattleCombatLogEntryFormatter.Format`)에서 모두 제거·무시 처리했다(직렬화 호환을 위해 `UpgradeApplied = 4` enum 숫자만 보존).
- **검증**: `.NET8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과, 격리 Unity 6000.3.12f1 EditMode `BattleCombatLogEntryFormatterTests` **26/26 통과**(`TempBuild/CombatLogBiochemicalAndUpgrade_20261031/editmode.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, DB 마이그레이션 없음. 예약 해제.


같은 checkout에서는 쓰기 담당 한 명. worktree는 문서/미커밋 내용을 자동 공유하지 않는다. 공유 문서 갱신은 통합 담당자가 순서대로 처리한다.


## 2026-10-31 — Lightsail 운영 DB 선물함 서버 골드·티켓 지급 절차 문서화 (AG-20261031-03)

Lightsail 이전 후 기존 `ManageGifts.ps1` 기본 실행이 예전 로컬 Windows DB 설정(`DeploymentSettings-20260909.clixml` → `project333`)을 바라보던 원인과, `Project333.GiftAdmin` 루프백 보안 제약을 유지하며 **SSH 로컬 포트 포워딩(`127.0.0.1:15432 -> Lightsail 127.0.0.1:5432`) + `ManageGifts.ps1 -UseEnvironmentDatabase`**로 운영 DB(`project333_live`)에 서버 골드(`-ResourceGold`)·티켓(`-Tickets`)을 지급하는 단계별 절차(`Accounts` → `Prepare` → `Preview` → `Send`)를 [docs/gift_inbox.md](docs/gift_inbox.md#lightsail-운영-dbproject333_live-선물-지급-절차--2026-10-31-갱신)와 [docs/lightsail_operations.md](docs/lightsail_operations.md#관리자-선물함-지급-서버-골드티켓)에 정리했다. 코드·서버·DB 변경 없음. 예약 해제.

## 2026-10-31 — 전투기록 #7, #8, #9, #11, #12, #14, #41 길게 누르기 미리보기 (AG-20261031-02)

- **확정 규칙**: 하스스톤 전투기록 시각화 형식을 참고해 **#7(마법 카드 사용)**, **#11(일반 공격·반격 이외의 피해 — 대안 B)** 및 연관된 **#8(공격력 증가)**, **#9(파괴 표식 부여)**, **#12(체력 회복)**, **#14(파괴 표식에 의한 파괴)**, **#41(트랩 발동)** 전투기록 문장을 0.25초 이상 길게 누를 때 시각화한다.
  1. **비대상 마법 카드**(마정석, 마정석 꾸러미, 욕망의 단지, 초소형 발전기, 보조배터리, 영약:만년설삼, 마법 증폭진 등): 화면 중앙에 카드 1장을 표시(`ShowSingle`)하되 공격력/체력 숫자는 숨기고(`HasCombatStats = false`) 하단 소유자(`플레이어`/`상대방`)만 표시.
  2. **단일 대상 카드**(대환단, 파이어볼, 천라지망, 트랩-반응장갑/아이언돔/서먼트랩/미러필드/리턴, 환술, 마나쉴드, 플라즈마쉴드, 고독 및 단일 피해/회복): 왼쪽에 출처 카드, 중앙에 `RightTriangle.png` 아이콘, 오른쪽에 대상 카드 1장 배치(`ShowDirected`). **#40 트랩 설치(`TrapInstalled`)**는 비공개 규칙상 미리보기를 열지 않으며(`HasHoldPreview = false`), 발동되어 정체가 밝혀진 **#41 트랩 발동(`TrapTriggered`)**만 표시.
  3. **다중 대상 카드**(파이어월, 생화학폭탄, 파이어스피어, 지진, 메테오, 뉴클리어, 원자력발전소, 블루드래곤, 레드드래곤, 시한폭탄 등 2장 이상): 왼쪽에 출처 카드, 중앙에 `RightTriangle.png`, 오른쪽에 대상 카드들을 **한 줄당 5장씩(`MaxTargetColumns = 5`, 예: 17장이면 5×4 격자)** 위에서부터 채우며 배치하고 대상 수의 5장 단위(`GetTargetCardScaleForCount`)로 카드 크기를 조절.
  4. **시점 스탯·색상 및 3초 결과 아이콘**: 모든 대상 소환물 카드는 전투기록 직전(`before`) 공격력/체력을 원래 값(`OriginalAttack`/`OriginalMaxHp`)과 비교해 초록/빨강/흰색으로 표시하며, 실제로 피해를 입거나 회복하거나 처치·파괴된 대상 카드 위에는 3초 동안 피해 아이콘(`DamageBurst.png` + `-N`), 체력회복 아이콘(`HealIcon.png` + `+N`), 해골 아이콘(`Skull.png`)을 표시.
- **구현 범위**: `RightTriangle.png.meta`, `HealIcon.png.meta`, `BattleCombatLogEntryDto.cs`, `CombatLogSymbolGraphic.cs`, `CombatLogAttackPreview.cs`, `CombatLogHoldTarget.cs`, `OnlineBattleConnectionTester.cs`, `TutorialCombatLogRecorder.cs`, `BattleSession.cs`, `BattleSession.Traps.cs`, `CombatLogPreviewChecks.cs`, `CombatLogAttackPreviewTests.cs`, `docs/combat_log_catalog.md`, `docs/combat_log_attack_preview.md`.
- **검증**: `.NET8` 서버 Release 빌드 오류 0, `Project333.AiChecks --combat-log-previews-only` 통과(공격 4종 + 단일 카드 17종 + 마법/피해/회복/트랩/표식 directed 미리보기·양 좌석 관점·손패 비공개·전체 JSON 복원), 격리 Unity 6000.3.12f1 EditMode `CombatLogAttackPreviewTests` **19/19 통과**(`TempBuild/CombatLogDirectedPreview_20261031/editmode.xml`). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, 새 DB 마이그레이션 없음. 예약 해제. [상세](docs/combat_log_attack_preview.md#마법피해회복트랩-전투기록-누르기-미리보기-7-8-9-11-12-14-41번--2026-10-31).

## 2026-10-31 — 전투기록 피해 폭발 아이콘 교체 (AG-20261031-01)

사용자 첨부 폭발 PNG(1024×1024 RGBA)를 Assets/Project333/Resources/Project333/UI/CombatLog/DamageBurst.png에 원본 그대로 추가하고 CombatLogSymbolGraphic(Symbol.Damage)에 연결했다. 기존 166×166 영역·비율·3초 표시·사용자 폰트/씬 보존. 격리 Unity 6000.3.12f1 EditMode CombatLogAttackPreviewTests **18/18 통과**(TempBuild/CombatLogDamageIcon_20261031/editmode.xml) 및 실제 렌더 캡처 확인. 앱 재빌드 필요, 서버/DB 변경 없음. 예약 해제. [편집 위치](docs/combat_log_attack_preview.md#전투기록-아이콘-교체--2026-10-30--2026-10-31).

## 2026-10-30 — 전투기록 #2, #3, #10, #21~#34 단일 카드 길게 누르기 미리보기 (AG-20261030-02)

- **확정 규칙**: #2(유닛 소환), #3(건물 건설), #10(액티브 효과 발동), #21~#34(상태 부여·해제 14종) 총 17종 전투기록 문장을 0.25초 이상 길게 누르면 중앙((0, 20))에 단일 카드 1장과 하단 소유자(플레이어/상대방)만 표시하고 두 번째 카드·검 교차·결과 마커는 숨긴다. 원래 수치(OriginalAttack/OriginalMaxHp) 대비 높으면 초록(#56F06F), 낮으면 빨강(#FF5B5B), 같으면 흰색으로 표시하며, #2·#3은 손패 당시 수치, #10은 발동 직전(`before`) 수치, #21~#34는 상태 적용/해제 직후(`after`) 수치를 기록한다.
- **구현 범위**: BattleCombatLogEntryDto.cs, CombatLogAttackPreview.cs, CombatLogHoldTarget.cs, BattleBootstrapper.CombatHistory.cs, BattleBootstrapper.cs, TutorialCombatLogRecorder.cs, BattleSession.cs, CombatLogPreviewChecks.cs, CombatLogAttackPreviewTests.cs, docs/combat_log_catalog.md, docs/combat_log_attack_preview.md.
- **검증**: .NET8 서버 Release 빌드 오류 0, Project333.AiChecks --combat-log-previews-only 통과(공격 4종 + 단일 카드 17종·양 좌석 관점·손패 비공개·전체 JSON 복원), 격리 Unity 6000.3.12f1 EditMode 3개 클래스 **49/49 통과**(TempBuild/CombatLogSinglePreview_20261030/editmode.xml). 클라이언트 앱 재빌드 및 온라인 반영 시 서버 재배포 필요, 새 DB 마이그레이션 없음. 예약 해제. [상세](docs/combat_log_attack_preview.md#단일-카드-전투기록-미리보기-2-3-10-2134--2026-10-30).

## 2026-10-30 — CombatLogIcons_20261030

사용자 첨부 해골/교차 검 PNG를 UI/CombatLog Resources에 원본 그대로 추가하고 기존 벡터 아이콘을 대체했다. 비율 유지·기존140/166영역, 피해 문양·3초 표시·사용자 글꼴/씬 유지. 동일 뷰의 피해↔해골 전환 시 텍스처 재질 갱신을 연결했다. Unity 생성 Single Sprite 메타·최대512·알파 사용. 별도 Unity6000.3.12f1/D3D11 기존 미리보기 검사 **16/16 통과**, 임포트·렌더 캡처 확인. 세션 CombatLogIcons_20261030/{import.log,tests.xml,tests.log,Captures,verification.json}에 증거. SDK 광고·실기기·앱 빌드·운영 배포/DB 미수행. 앱 재빌드 필요, 서버/마이그레이션 불필요. 예약 해제. [편집 위치](docs/combat_log_attack_preview.md#전투기록-아이콘-교체--2026-10-30).

## 파이어월 즉시 피해·비용 변경 — 2026-10-29

- `FirewallImmediate_20261029` 소스 완료: 비용 마나4, Lv.0 마법 피해15 유지, 사용 즉시1회 + 다음2회 전역 턴 시작 피해(총3회). 즉시 피해는 `triggerCount:2`를 차감하지 않는다. 강화+1/레벨, 현재 주문력·방어·봉인·파괴·유산 처리는 기존 공유 규칙을 따른다.
- `FirewallEffectService`를 `SpellService`와 `TurnStartService`가 공유한다. 즉시 피해는 기존 SpellCast 연출에 붙이고 별도 Area 큐를 만들지 않는다. 로컬 플레이어/AI의 피해 팝업 누락을 제거하고 온라인 시전에도 RuntimeId 보호를 적용했다.
- JSON·네이티브 카드·검증기·카드 미리보기 설명을 사용자 문구로 통일했다: “5*1타일을 선택한다. 즉시 및 다음 2턴 시작 시, 선택한 타일에 마법 데미지를 입힌다.” 기존 이미지·메타 GUID·사용자 씬·다른 카드 수치는 보존했다.
- 검증: 격리 Unity6000.3.12f1/D3D11 관련 **212개 통과**. 즉시 피해/데이터/미리보기 새12 + 기존 파이어월 연출24는 최종36/36, 나머지176는 앞선 회귀 실행 통과. 기존 합성33피해 검사의 HP/기대값만 새 즉시 타격에 맞췄고 실제 카드 피해15는 유지했다. 첫 검사의 테스트 코드 컴파일 오류2개를 수정했고, 다음 실행의 잘못된 툴팁 overload1건·검증 사본 불꽃 리소스 누락14건을 바로잡아 최종36/36 통과했다. 실패 기록도 증거로 보존한다.
- .NET8 서버 컴파일 오류0(기존 nullable 경고101). 증거: 세션 `FirewallImmediate_20261029/tests2.xml`, `tests3.xml`, `server-build-final.log`, `verification.json`. 광고 SDK 없는 격리 사본의 범위이며 실기기·운영 접속·서버 배포는 별도다.
- 다음 행동: 사용자 클라이언트 재빌드 및 Lightsail 서버 재배포. 새 DB 마이그레이션 없음. 진행 예약 해제. [상세](docs/firewall_presentation.md#즉시-피해-추가--2026-10-29).

## 공격 전투기록 길게 누르기 — 2026-10-29

- `CombatLogHoldPreview_20261029` 소스 완료. 공격 문장을 약0.25초 가만히 누르면 왼쪽 공격자·오른쪽 피격자와 검 교차 아이콘을 표시하고, 손 떼기/이탈/드래그/포커스 해제 시 닫는다. 기존 ScrollRect와 씬의 SetLog 이벤트를 유지하며 실제 TMP 오브젝트에 포인터 처리를 붙였다.
- 공격 직전 실제 공격력·현재 체력과 원래 수치를 기록한다. 증가 초록/감소 빨강/동일 흰색. 피해는 양쪽 실제 공격/반격 HP 차감량, 사망은 해골로 대체, 피해0이하에는 피해 표시 없음. 결과 표시는 열 때부터3초만, 누르는 동안 카드 수치는 그대로 유지한다. 새 기록이 와도 복사한 당시 데이터가 바뀌지 않는다.
- 서버 DTO/기록/StateView/전체 JSON복원·클라이언트 관점 전환을 확장하고 로컬·튜토리얼도 같은 데이터를 기록한다. 기존33개 제한·상대 손패 숨김·판정은 유지. 구 기록은 선택 필드가 없으면 텍스트로만 읽고 과거 수치를 추측하지 않는다.
- 격리 Unity6000.3.12f1/D3D11 **62개 통과**(기존 기록46 + 최종 새15 + 일반 로컬 게이트웨이/기존 로그 콜백 통합1). 실제 렌더에서 피해→실제3.05초 대기 후 표시 제거→해골을 캡처했다. 검증 중 발견한 벡터 CanvasRenderer 누락과 만료시각 float 경계를 수정했다. 첫 캡처의 테스트 폰트 입력도 실제 기록의 NotoSansKR-Regular로 맞췄으며 사용자 폰트/이미지는 수정하지 않았다.
- .NET8 서버 컴파일 오류0/기존 nullable101. 오프라인 실제 서버 세션4시나리오에서 양쪽 공격자·사망/피해·두 관점·손패 비공개·전체 스냅샷 복원을 통과했다. 운영 서버/DB 접속·앱 빌드·실기기 확인은 하지 않았다.
- 증거: 세션 `CombatLogHoldPreview_20261029/tests4.xml`, `tests-complete.xml`, `tests-local.xml`, `server-build2.log`, `server-checks.log`, `verification.json`, `Captures/`. 다음 행동은 사용자 클라이언트 재빌드와 Lightsail 서버 재배포, 새 DB 마이그레이션 없음. SDK 광고 검증 제외. 예약 해제. [사용·구조·편집](docs/combat_log_attack_preview.md).

## 구현 완료 — 소스 기준

| 영역 | 구현 내용 | 근거 / 확인 범위 |
|---|---|---|
| 전투 규칙 | 5×2 전장, 마스터333/3, 근접·원거리, 물리·마법·고정, 방어·반격·전열 보호·상태·트랩·액티브 | [전투 규칙](docs/battle_rules.md), 공유 Domain/Services와 관련 테스트 |
| 전투 시작·턴 | 멀리건5장, 선공3/후공4골드 초기 배정, 양측 첫 턴 추가 골드 없음, 이후+3골드·매 자기 턴1드로우. 시작 순서: 준비/기본골드→직접파괴→지속피해→생존자생산/지속자원→전력소모→공장→드로우→부활/봉인→Main | BattleSetupService / TurnStartService / [멀리건](docs/mulligan_presentation.md) |
| 카드·강화 | JSON61종(유닛27/건물7/마법27), 네이티브/드래프트55종. 강화44종·최대13레벨, 비용3/6/10/20/30/33 | JSON 기본 true 포함 flags·카탈로그 재확인. [카드](docs/card_catalog.md), [제작](docs/card_authoring_checklist.md), [메타](docs/meta_rules.md) |
| 드래프트·런 | 서버 제시3장 중1장 선택,33장 덱,33승/3패 종료, 선택·중단 복구·결과 저장 | [드래프트](docs/draft_rules.md), [전투 모드](docs/battle_modes.md) |
| PvP·AI·골드 던전 | 사람 우선/20초 후AI,70초 재접속, 런당 던전3회·승리 계정골드10. 공유 규칙 기반 AI 평가·탐색 및 고정덱 | [모드](docs/battle_modes.md), [AI](docs/pve_ai.md) |
| 계정·단일 로그인 | Game ID/Google, refresh, 최신 로그인 우선·이전 세션 종료·새 기기의 원래 좌석 복귀, 강제 로그아웃 뒤 Google 버튼 복구 | [단일 로그인](docs/single_device_login.md). 0025 소스/격리 DB 검사 완료; 운영 DB 버전은 아래 구분 |
| 경제·상점 | 서버 지갑, 티켓 구매·광고 S2S, 강화·중복 요청 방지,33골드10장 카드팩·개봉 연출·자동 복구, 선물함 | [복구](docs/account_operation_retries.md), [카드팩](docs/shop_card_packs.md), [선물함](docs/gift_inbox.md), [광고](docs/rewarded_ads.md) |
| 보상 | 실제 랜덤 대상41종(includeInRewards 적용 true43종과 별개), 런 종료 상자/보상창·전체 Canvas 크기 수정 | RewardCardEligibility / [보상 UI](docs/run_reward_chest.md) |
| 튜토리얼 | 1~6단계, 이전/다음 대사 복습(실습 상태 유지), 개방 방주 대화·시각 자료·초록 포커스·필수 실습, 멀리건·전투·마법·이동교환·강화, 계정과 로컬 연습 분리 | [구현](docs/tutorial_implementation.md), [대본](docs/tutorial_script_ko.md) |
| 화면·연출 | 공통 공격력/최대HP 증가 빨강·초록·노랑, 내 덱→손패 드로우 비행·뒤집기, 임시 복제 모래시계·소멸 안내·회색 입자, 카드 미리보기/툴팁, 로그인 전체화면 입력 차단(설정 버튼 예외), 손패·이동교환 표시 수정, 승리/패배 배너, Play 전 씬 UI·멀리건5번째 슬롯 | [편집 가능 UI](docs/editable_scene_layouts.md), [결과 배너](docs/battle_outcome_presentation.md), handoff |
| 음향 | 로비/전투 BGM 교체, 이동·버튼·카드·전투 효과음, 승리/패배 MP3. 마스터/음악/효과음 분리; 신규 설치 기본값50/100/100%, 기존 저장값 유지 | [오디오](docs/audio_settings.md), [BGM](docs/background_music.md), [이동음](docs/battle_movement_audio.md) |
| 운영·복구 | 서울 Lightsail 이전, HTTPS/WSS, PostgreSQL18, 매일21시 암호화 MYBOX 백업, 운영과 분리한 실제 DB 복원·서버 파일 별도 백업·임시 평문 정리 | [운영](docs/lightsail_operations.md), [2026-10-20 상태 기록](docs/current_project_status.md). 날짜가 있는 사용자 확인 근거 |

## 날짜별 기존 검증 기록 — 이번 인계에서 게임 검사는 재실행하지 않음

| 날짜 | 범위 | 결과 / 제한 |
|---|---|---|
| 10-29 | 카드 드래그 중 접힘·손패 복귀 펼침 | 펼친 손패 드래그 시작→나머지 접힘, 끌어낸 뒤 손패로 복귀→펼침·놓으면 취소, 성공 사용→접힘 유지. 고정 시작 범위·경계 완충·중단 복원·동기 사용 뷰 제거 콜백 처리. 새9 포함 실제 Unity92/92, 앱 빌드·실기기 별도. 서버/DB 변경 없음. [상세](docs/compact_battle_hand.md#드래그-중-손패-접힘복귀-2026-10-29) |
| 10-29 | 내 손패 장수별 간격 | 2~10장250/225/200/175/150/125/100/75/50, SafeArea 가로 초과 시 간격만 축소·회전/선택/접힘 유지. 새 Inspector 설정 snapshot·기존 씬 호환. 최종31/31, 관련 기존41/43(모래시계 위치 기대값2건은 수정 전에도 동일 실패·사용자 위치 보존), 카드 렌더5장. 소스 완료·앱 빌드/실기기 별도, 서버/DB 변경 없음. [조절 방법](docs/compact_battle_hand.md#장수별-손패-간격과-화면-초과-조절-2026-10-29) |
| 10-29 | 유산 공통 특수효과 | 좀비/스켈레톤 아처/스켈레톤 사망·파괴 시 소유자1드로우, 원전 폭발 전체 물리30을 유산으로 통합. 공통 억제·반환 제외·중복 방지·덱 소진/동시 승패, JSON/4native·툴팁·문서 일치. 새24 포함 관련226검사 통과(아처 전용 자산 기준 보정), 서버 오류0/기존경고101. 앱 재빌드·서버 재배포 필요, DB변경 없음·카드 PNG인쇄 문구 보존. [규칙](docs/battle_rules.md#유산-legacy--2026-10-29) |
| 10-29 | PvP20초 뒤 AI 타이머 대기 정체 | 같은MatchId의 새 방이 옛 타이머 키에 막히는 경로 재현. 세션 객체별 타이머·종료 후 접속 재확인·일시 오류1초 재시도, DB20초/사람 우선 유지. .NET8 서버 오류0(기존경고101), 오프라인 타이머6그룹+기존 재접속10그룹·교체100회 통과. 운영 사례 원인 단정 아님·재배포/실전 확인 별도, 앱/DB 변경 불필요. [상세](docs/battle_modes.md#ai-매칭-타이머-세션-교체-경쟁-수정-2026-10-29) |
| 10-29 | 마나 쉴드 새 원본 재교체 | 추가 제공한3072×512·6컷 RGBA 원본으로 Formation.png 교체. meta/GUID·512고정크기·위치 코드·플라즈마 보존. 마나 native/연출14/14, 두 효과 UpdateLayout의 UI오프셋 편집 안내 추가. 앱 빌드/실기기 별도·서버/DB 변경 없음. [안내](docs/mana_shield_presentation.md#애니메이션-위치-직접-조절) |
| 10-29 | 쉴드 사용자 시트·고정 크기 | ManaShiled6컷/PlasmaShiled8컷 원본 연결·네이티브 재슬라이스, 소환물과 무관한512×512 UI크기·발밑 기준점. 실제 Unity28/28 및16장 렌더, PNG·GUID 보존. 사용자 카드 그림 불변. 앱 재빌드 필요·서버/DB 변경 없음. [마나](docs/mana_shield_presentation.md), [플라즈마](docs/plasma_shield_presentation.md) |
| 10-28 | 공격 방식 아이콘10행 | TileTextView의 검/활/지팡이 픽셀 도안을9×8→9×10으로 확장. 기존 모양·색·중앙 배치·점 유지, 행/열 길이와diff 정적 확인. Unity 재실행·앱 빌드 없음. [편집](docs/stat_bar_authoring.md) |
| 10-28 | AI 연출 보정 타이머 조기0 표시 | 서버 AI 연출 여유(3·7·30초 포함)를 클라이언트가 명목 길이로 잘라 먼저0/처리 중으로 표시하는 문제 수정. 실제 deadline 존중·시계 역행 보호 유지. 수정 전 새13중9실패→실제 Unity46/46. 기존 서버 수정 재배포는 사용자 확인, 이번 클라이언트는 새 앱 필요. 운영 실전 재확인 별도·서버/DB 변경 없음. [상세](docs/turn_timeout_handoff.md) |
| 10-28 | 스탯창 아이콘·숫자 중앙 정렬 | 체력/ATK 아이콘·숫자 한 줄 묶음을 고정 초록 점 영역 밖 중앙에 배치. 실제 글자 폭 반영·폭 부족 시 묶음 비율 축소, 상태별 회색 유지. 최종 실제 Unity **15/15**, 사용자 씬·크기·폰트와 점 배치 코드 보존. 앱 재빌드 필요, 서버/DB 변경 없음. [안내](docs/stat_bar_authoring.md) |
| 10-28 | 공격 잔여 초록 점·스탯창 편집 안내 | 내 Main의 생존 아군 중 공격력/횟수·상태가 허용하는 소환물에만 점 표시(마스터·공격 가능 건물 포함). 실제 Unity **32/32**(새 UI2+조건17+기존13), 별도 공유 판정17/17·런타임 컴파일오류0. 기존 씬 크기 유지, Inspector 편집 안내. 클라이언트 재빌드 필요·서버/DB 변경 없음. [상세](docs/stat_bar_authoring.md) |
| 10-28 | 욕망의 단지·허공 마법 연출 공통화 | JarOfGreed 골드1·2드로우·덱빌딩On/보상Off·강화불가, 이미지·native 등록. 공유/서버필터18/18, 서버/런타임/생성기·검사 소스 컴파일오류0. Unity초기로드 정체로 native import·Play 미완료. 서버 재배포/앱 빌드 필요, DB 변경 없음. [상세](docs/jar_of_greed.md) |
| 10-28 | 특수효과 목록 문서 | 공통17종·보유 카드·상태/전용 효과 구분, 파이어월15·트랩 허공 설치·턴 시작/낙진·복제/대환단 표시 갱신. 현재 카드60종/카탈로그 정적 대조·문서 링크/diff 확인, 게임 테스트 재실행 없음. [목록](docs/special_effects.md) |
| 10-28 | 보상 상자 개봉 효과음 | 개봉 성공 진입 시 GoldSound 1회·개별0.3·공통 효과음/마스터 적용. 실제 변경 파일 Unity DLL 참조 별도 컴파일 오류0/경고0, 경로·메타 정적 확인. Unity Play/실기기 청취·앱 빌드 미실행. 서버/DB 변경 없음. [교체 방법](docs/audio_settings.md#런-보상-상자-개봉-효과음-2026-10-28) |
| 10-21 | 단일 로그인 | 임시 PostgreSQL 집중14그룹 + 일반130 + 재시작/DB 미설정5; Unity EditMode53/PlayMode1. 운영 배포 증거와 분리 |
| 10-22 | 교체 로그인 후 Google 준비 중 | 수정 전 재현 실패 → 격리 EditMode76/PlayMode1 통과; 실제 Google 제공자·폰 재확인 별도 |
| 10-23 | Play 전 UI/멀리건 | 기존 씬 검증94, 5번째 슬롯 포함95 통과. 사용자 열려 있는 씬을 강제 저장하지 않음 |
| 10-23 | BGM/효과음 볼륨 | BGM3, 효과음·메뉴·관련 회귀16 통과; 실제 기기 청취 별도 |
| 10-23 | 마스터 기본값50% | 기존 설정/음악5 통과; 기존 PlayerPrefs 유지 |
| 10-24 | 승리/패배 음향 | 격리 Unity12/12. 사용자 파일 원본 동일, 연출 시작1회·중복 억제·볼륨·중지 검사; 클라이언트 빌드 미실행 |
| 10-24 | 상대 카드 공개 누락 | 종료 배너/복귀가 대기열을 앞서는 경로와 합체 전용 이벤트 누락 수정. 수정 전4실패 → 격리67/67 통과. 환술 기본 좌석/AI·PvP 검사 통과; 실제 기기 누락 전체 원인 단정 안 함. [상세](docs/opponent_card_reveal.md) |
| 10-24 | 덱 목록 글자 확대 | 덱 빌딩22→32pt, 드래프트 표시26→32pt. 원본/대체 목록 및 생성 기본값 일치, 기존 목록·UI 회귀40/40. 앱 빌드·실기기 확인 별도 |
| 10-24 | 승리·패배 개별 볼륨 | `_victorySoundVolume`/`_defeatSoundVolume` 분리(당시1/1; 현재 코드0.65/1), cue별 소스 재사용. 서로 다른 볼륨·승리만 음소거·공통 배율과 기존 회귀24/24 통과. 앱 반영 별도 |
| 10-24 | 튜토리얼 이전 버튼 | 다음 왼쪽 이전·방문 대사 복습, 실습 입력 잠금/복귀·주제 선택 초기화·좁은 패널 배치. 기존 튜토리얼/격리 회귀74/74(새11사례) 통과. 앱 빌드·실기기 확인 별도 |
| 10-24 | 임시 복제 표시·소멸 | 새8/8(실제 Play4 포함), 관련 전체76/78. 기존 낙진 설명 검사2개 문구 불일치 유지. 일반/임시/소멸 중 Unity 렌더 확인; 앱·서버 반영 없음. [상세](docs/compact_battle_hand.md) |
| 10-24 | 임시 복제 아이콘 교체 | 사용자 황금 모래시계 원본 PNG·투명 여백 스프라이트 영역, 손패/미리보기 공통 교체. 기존8/8 및 Unity 렌더 확인; 앱 빌드·실기기 확인 별도 |
| 10-24 | 스펠 카드 공통 사용 연출 | 초기0.58초의 대상 없는 손패 마법 푸른 조각 연출·RuntimeId별 원본 숨김. 새5+기존18=23/23 및 Unity 렌더 확인. 현재0.145초·즉시 확정은10-25 후속 행; 앱·실기기·실서버 별도. [상세](docs/hand_spell_use_presentation.md) |
| 10-24 | 스펠 사용 연출2배속 | 공통 Duration0.58→0.29초. 정규화된 진행률에 따라 분해·이동·반짝임·페이드 전체가2배속. 상수1개만 변경·비율/문서 정적 확인; Unity·실기기 재검사/앱 빌드 없음 |
| 10-26 | AI 타일 지속 피해 범위 밖 빈 타일 우선 | AI 전장에 활성 생화학폭탄·파이어월이 있고 범위 밖 빈 타일이 있으면 범위 안 유닛·건물 소환 후보를 제외(`AiActionGenerator`)하고, 턴 종료 전 범위 안 이동 가능 아군 유닛(마스터 제외)을 실제 생존/HP 이득 확인 시 안전 빈 타일로 대피(`AiDecisionService.RequiredHazardEscape`, `AiMovementPolicy`). 광마 등 광폭화 유닛이 폭탄 피해로 공격력을 올리려고 범위 안에 소환·잔류하는 문제 해결, 안전 빈 타일이 없을 때는 기존 가치 비교 허용. 격리 Unity AI EditMode 20개 클래스 **385/385 통과**(`AiHazardEscapeTests` 신규7사례 포함), `.NET8` 서버 컴파일 오류0(기존 경고101). 온라인 반영 시 서버 재배포 필요. [상세](docs/pve_ai.md#타일-지속-피해-범위-밖-빈-타일-우선-2026-10-26) |
| 10-26 | AI 환술 긴급 방어 가치 마스터 체력 고정 | `AiPositionEvaluator.HuanShuDisruptionValue`가 행동 후 `state.AI.Master.CurrentHp` 대신 의사결정 시작 시점 마스터 체력(`ownMasterExposureHp`)을 기준으로 긴급 보너스를 평가하도록 수정. 이미 환술에 걸린 고화력 적이 있을 때 AI 마스터가 방어력 높은 적에게 자해 공격해 반격으로 체력 경계(`damage * 2`)를 넘어 점수를 얻는 왜곡 제거, 실제 위기 체력에서 손패 환술 시전 보너스는 유지. 격리 Unity AI EditMode 8개 클래스 **206/206 통과**(`AiOwnMasterHpBandTests` 신규3사례 포함), `.NET8` 서버 컴파일 오류0(기존 경고101), `AiChecks` 통과. 온라인 반영 시 서버 재배포 필요. [상세](docs/pve_ai.md#환술-긴급-방어-가치의-의사결정-시작-마스터-체력-고정-2026-10-26) |
| 10-26 | 턴 시작 10단계 순서 | 준비·기본골드 후 직접파괴→낙진/시한폭탄/타일피해→생존자 자원/지속마법→유지비→공장→드로우→부활/봉인→Main. 사망 생산물의 자원 미지급. 새20사례 포함 관련330/330, 변경 전13실패 재현, .NET8 서버 컴파일 오류0(기존 null 처리 등 경고101). 시한폭탄 팝업 경계 갱신, 원본에서도 실패한 골드/비용 테스트 입력2건 보정. 운영 배포·앱 빌드 별도, DB 변경 없음. [순서](docs/battle_rules.md#turn-structure) |
| 10-26 | 공통 능력치 증가 3색 연출 | 기존 대환단 도형·0.62초 유지, 공격력 빨강/최대HP 초록/둘 다 노랑. 카드별 호출 대신 확정 상태의 BaseAttack·MaxHp 증가를 RuntimeId별 감지, 유닛·건물·마스터/양쪽·광역 동시 적용. 회복·초기/재접속 상태 제외. 공통8사례(기존3이관+신규5) 및 관련68/68·실제 3색 렌더 통과. 예시 새 카드/게임 수치·서버 변경 없음. [상세](docs/stat_increase_presentation.md) |
| 10-26 | 대환단 전용 강화 연출 — 이후 공통화 | 대환단(`Daehwandan`) 사용 성공 시 시전자(플레이어/상대방) 마스터 발밑 붉은색 원형 파동→몸 좌우 기운 상승 0.62초(`DaehwandanMasterVfx`, `sortingOrder=260 < OccupantStatBar 4950`, 비차단) 추가, 손패 푸른 조각 분해(`0.145초`) 유지, 취소·거절·빈 스냅샷 미재생. AI 행동 뒤 중복 `RefreshPresenter()` 제거 및 테스트 캡처 경로를 `Application.dataPath` 기준 상대 경로(`../Captures/Daehwandan_20261026`)로 정리 후 격리 Unity에서 `DaehwandanPresentationTests` 3/3 + `SpellCardUsePresentationTests` 10/10 = **13/13 통과**, 단계별 렌더/GIF(`TempBuild/DaehwandanVfx_20261026/`) 확인. 앱 빌드·서버 배포 없음. [상세](docs/hand_spell_use_presentation.md) |
| 10-26 | 뉴클리어 낙진 툴팁 테스트 | `CardPreviewDetailsTests`의 구 낙진 문구 기대값2건을 현재 툴팁(`양쪽 턴 시작마다 10의 고정 피해를 받는다.`) 기준으로 갱신. 격리 Unity에서 `CardPreviewDetailsTests` 15/15 및 미리보기·손패 관련8개 클래스113/113 통과(기존 임시 복제 묶음78/78 포함). 별도로 `OccupantStatusTooltipCatalogTests`의 소환물 부착 디버프 제목 구 기대값(`낙진` vs 현재 `낙진: 양쪽 턴 시작마다 10의 고정 피해를 입는다.`) 4건 불일치는 미수정 유지. [상세](docs/occupant_status_tooltips.md#뉴클리어-카드의-낙진-설명-2026-10-08) |
| 10-26 | Antigravity 최근 개발 인계 | 10-24 이후12개 변경의 최종 동작·소스/테스트 위치·기존 검증/한계·배포 구분 통합. HEAD533b0c9 및 승리 볼륨0.65 반영, 링크/경로·문서 diff 정적 검사. 게임 검사/빌드/배포/커밋 없음. [시작점](docs/antigravity_handoff.md) |
| 10-26 | 드로우 출발 좌표 캐시 | 전투 UI의 출발 좌표를 연속 드로우에 재사용, 배치/화면/Canvas/카메라 변경 시 재변환·재접속 정리 시 초기화. 관련83/83, 고정 배치100조회→전체 출발 변환1회. FPS/폰 확인·앱 빌드 별도, 서버 변경 없음. [상세](docs/hand_draw_presentation.md) |
| 10-26 | 무피해 반격 Idle 복귀 | 방어/무적으로 피해0인 반격자의 복귀 누락 수정, 공격자 사망 조기 종료 전 처리·Death 유지. 실제 재현2실패→3/3, 관련 고유41개 최종 통과. 사용자 자산 보존·앱/실전 확인 별도. [상세](docs/counterattack_presentation.md) |
| 10-25 | 드로우 출발점 갱신 | 분리 Canvas 배율 변경 시173.6px 어긋남 재현1실패→당시 실제 덱 Transform 매 프레임 재변환(현재는10-26 캐시 방식).17·19·21턴/멀리건 미재시작·마지막 덱 포함78/78 통과. 당시 실전 트리거·폰 재확인 별도, 앱 재빌드 필요. [상세](docs/hand_draw_presentation.md) |
| 10-25 | 내 덱→손패 드로우 연출 | 실제 자기 드로우만 현재 덱 위치·뒷면에서0.38초 뒤집기 이동, 연속0.07초 간격. RuntimeId별 표시/입력·멀리건/재접속 분리·정리 포함49/49 및 Unity 렌더 확인. 앱 빌드·실기기 별도, 서버/DB 변경 없음. [상세](docs/hand_draw_presentation.md) |
| 10-25 | 시간 만료 턴 전달 지연 | 감사/DB·느린 상대 소켓 이전에 이벤트·상태 전송, 다음 타이머 선예약·종료 결과 저장 유지. 실제 함수 재현6/12→16/16, 기존 시작/행동 상태 검사 통과. 운영 release 재확인·반영 시 서버 배포 필요. 이번 수정 작업에서 배포하지 않음. [상세](docs/turn_timeout_handoff.md) |
| 10-25 | 덱·상대 손패 뒷면 편집 보존 | 기존 덱6장/Root 배치·슬롯별 이미지 유지, 비어 있는 슬롯만 기본 이미지. 클릭 영역·드로우·수량/손패 정렬 유지. 수정 전3실패→관련41/41 통과(저장·Play 진입 포함). 원본 씬 재생성·앱 빌드·실기기 없음 |
| 10-25 | 트랩 위로 드래그 설치 | 마스터 지정 제거·기존0.145초 공통 연출, 일반 설치 ACK를 자기 트랩 대기에만 연결·비밀 유지. 트랩5종/취소/중복/좌석 투영 포함37/37 통과. 앱 빌드·실기기 별도, 서버 재배포 불필요 |
| 10-25 | 스펠 즉시 분해·추가2배속 | 0.145초, 지진 등 전장 효과/손패 갱신 전 확정 즉시 재생. 대기 중 놓은 위치 유지·원본 숨김·거절 복원·슬롯 신원 보존. 수정 전 재현1실패→25/25 통과. 실기기·실전 전장 효과·앱 빌드 별도 |
| 10-24 | 공통 에이전트 문서 | AGENTS 크기·파일/문서 경로·ignore 정책·Git 상태 점검. 코드/자산/서버 변경이나 게임 테스트 실행 아님 |

증거 상세는 [handoff](docs/codex_handoff.md) 최신 관련 항목. `TempBuild/` 또는 로컬 세션 산출물은 Git에 없는 경우가 있으므로 새 PC에서 증거 파일이 없다면 재검증 여부를 판단한다.

## 운영 반영·기기 확인 상태

- 확인된 운영 주소는 `https://api.after333.com`, DB `project333_live`. 사용자 확인: 운영 Android Google 로그인·AI 결과 저장·광고 티켓+1, Unity Editor Windows Google 로그인 성공. 사람 PvP·재접속·결과는 이전 **시험 서버**의 Android+Windows 검사 기록과 구분한다.
- 최신 **소스** 마이그레이션은 `0025_single_login_family.sql`. 명시적으로 출력 확인된 마지막 **운영 DB** 기록은 0024(이전 작업 당시). 사용자가 이후 실제 기기에서 로그인 교체 동작을 확인했지만, 이를 현재 운영 release/DB version 전체 확인으로 대체하지 않는다. **현재 운영 버전은 재확인 필요**이며 0025 미적용이라고 단정하지 않는다.
- 최신 클라이언트 설치 BuildId는 미확인. UI·오디오 소스 완료를 앱 설치 완료로 기록하지 않는다. 변경된 공유 서버 코드도 배포 전에는 운영 반영으로 기록하지 않는다.
- 10-19 실제21:00 백업 성공·독립 다운로드·별도 DB 복원·서버 파일 백업이 확인되었다. 일일 작업은 DB 대상이며 서버 파일 변경을 자동 포함하지 않는다. 노트북 로그인/가동·MYBOX 연결·SSH 허용이 필요하다.

## 다음 작업 우선순위

다음은 현재 증거의 빈틈에 따른 **권장 순서**다. 새 기능 확정이나 운영 변경 승인이 아니다.

| 우선순위 | 작업 / 완료 기준 | 상태 |
|---|---|---|
| P0 | 기준 커밋533b0c9 이후 자산·이번 문서 diff 검토. 병렬 구현을 시작할 때 역할/별도 branch·worktree와 공통 문서 버전 확인 | 기준 커밋 존재. 추가 커밋·push/기존 worktree 재사용은 자동 실행하지 않음 |
| P0 | 서버 release/BuildId·카드 정의·운영 migration 확인. 턴 만료 전달 수정 포함 여부와 필요 시0025/서버/클라이언트 호환 배포 계획 확인 | 확인 필요; DB를 미확인 상태에서 재적용하지 않음 |
| P1 | 사용자 최신 앱 빌드 후 Android·Windows에서 단일 로그인 교체→Google 재로그인→원래 전투 재접속, 승패/재화 중복 없음 확인 | 최신 전체 경로 실기기 재검증 필요 |
| P1 | 다양한 폰 화면에서 보상창 크기, 로그인 dim/input, 손패 드로우/배율·캐시/교환/드래그, 능력치 증가3색·다중 대상, 무피해 반격 Idle, 마스터↔엘프 장로 교환, 멀리건5장, UI 편집 결과 확인 | 소스·격리 검사 완료, 최신 기기 확인 별도 |
| P1 | 이동/승패/BGM·3종 볼륨 청취, 기존 저장값/신규 설치 기본값 확인. 튜토리얼1~6 단계 실습 완주와 로컬 상태 격리 확인 | 사용자 기기 시험 필요 |
| P1 | 구매·카드팩·강화·선물·광고 중 앱 종료/재로그인 복구를 최신 배포에서 확인. 실광고 반복 no-fill은 제공자 진단과 앱 잠금 상태를 구별 | 기능 존재; 운영 전수 확인 아님 |
| P2 | 코드/설정 배포 후 서버 파일 백업 갱신 절차 유지, 노트북21시 종료 후 보충 실행 실측. 복구키 별도 보관과 전체 호스트 복구 범위 확인 | DB 복원은 완료; 호스트 전체 복원/보충 실행은 미검증 |
| P2 | 큰 오디오/폰트 자산 증가에 대비해 Git LFS와 UnityYAMLMerge 사용 여부 결정 | 제안; 자동 도입/이력 재작성 안 함 |

**보류 유지:** 서버 부팅 자동 시작/자동 재시작, GitHub Actions, 전투 기록 자동 정리. **미확정 범위:** 랭크/MMR/시즌, 유료 결제, 제작, 개별 카드 상점, 새 로그인 제공자, Card Grader 등. 새 요청 전 임의 구현하지 않는다.

## Git 점검과 최근 인계

2026-10-26 인계 최초 조사 기준 `main`, HEAD **533b0c9** (`261026 캡디최종발표`). 직전 커밋은99b4c50이며 최근 기능 소스와 문서가 새 커밋에 포함되어 있다. 최초 status는 tracked 자산2개 수정·untracked0·staged0: `Assets/Battle_VSlice.unity`, `Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF SDF.asset`. Editor/사용자 조작으로 이후 status는 달라질 수 있다. 이번에는 이 자산들을 편집하지 않고 인계 문서만 갱신했다.

10-24의 tracked142/신규78, 당시 완료145/신규80과 AGENTS21,099bytes는 **그날의 기록**이며 현재 pending 수가 아니다. 현재 값은 `git status`로 읽는다. AGENTS는24,000bytes 이하를 유지한다. 기존 ignore 정책·커밋 분류는 [협업 안내](docs/agent_collaboration.md); 문서의 날짜별 검증은 당시 결과이며 전체 코드 검사를 재실행한 뜻이 아니다.

등록된 다른 detached worktree는 오래된 기준이다. 소유자/변경 상태를 확인하지 않고 삭제·clean·reset하거나 최신 작업 폴더로 쓰지 않는다. 이번 갱신은 문서만 통합했으며 스테이징/커밋/push, Unity 조작, 빌드/배포·운영 DB 확인은 수행하지 않았다. 최신 읽기 순서·기능별 다음 행동은 [Antigravity 인계](docs/antigravity_handoff.md)에 있다.

완료 기록 양식(새 작업은 이 아래를 계속 늘리기보다 현재 표와 관련 문서 갱신):

```text
날짜 / 작업 ID / 담당 / branch·기준 커밋:
목표와 변경 파일:
확정 결정 및 관련 규칙 문서:
검증 명령·결과·증거 위치 / 미검증 사항:
클라이언트 빌드·실기기·서버 배포·DB 적용 상태:
남은 작업 / 다음 담당자 행동 / 예약 해제:
```
