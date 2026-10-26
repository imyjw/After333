# Antigravity를 위한 최근 개발 인계

갱신: **2026-10-26 KST**. 사용자가 10-24에 공통 에이전트 문서를 요청한 뒤 진행한 개발을 현재 소스 기준으로 묶었다. 이전 채팅이나 Codex 메모리 없이 읽을 수 있는 시작점이다. 작업 규칙은 [AGENTS.md](../AGENTS.md), 전체 기능·우선순위는 [PROGRESS.md](../PROGRESS.md)가 기준이다. 이 문서가 새 기능 구현·배포 지시는 아니다.

## 처음 읽는 순서

1. `C:/Project_333`를 연다. `Documents/New project`는 옛 프로토타입이다.
2. 루트 `AGENTS.md` → `PROGRESS.md` → [작업 선호·보류 사항](conversation_decisions.md#작업-선호와-보류-사항--이어서-작업할-때-먼저-읽기) → 이 문서를 읽는다.
3. `git branch --show-current`, `git log -1 --oneline`, `git status --short`, `git diff --stat`, `git worktree list`로 실제 checkout을 확인한다. 현재 소스 기준 커밋은 `main`의 **533b0c9**(10-26, `261026 캡디최종발표`)다. 과거 문서의 `99b4c50`은 10-24 작업 당시 기준이다.
4. 이번 문서 작업 최초 조사 시 미커밋 자산은 `Assets/Battle_VSlice.unity`, `Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF SDF.asset` 2개였고 staged·untracked는 없었다. 이후 Editor/사용자 작업으로 달라질 수 있다. 두 자산과 새 문서 diff를 사용자 변경으로 보존하고 현재 status를 다시 확인한다.
5. 새 작업 범위를 PROGRESS 예약표에 남긴 뒤 해당 기능 문서·소스·테스트만 읽는다. 같은 checkout은 한 명만 쓴다. 두 도구가 병렬로 구현하려면 [협업 절차](agent_collaboration.md)를 따라 별도 worktree를 준비한다. 등록된 과거 detached worktree를 새 활성 프로젝트로 추정하거나 정리하지 않는다.

Unity **6000.3.12f1**, uGUI, 서버 ASP.NET Core/.NET8, PostgreSQL18, HTTP/WSS 서버 권위 구조는 그대로다. 새 도구가 `AGENTS.md`를 자동으로 읽었다고 가정하지 말고 읽은 파일과 적용할 규칙을 첫 응답에서 확인한다.

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
| 스펠 사용·트랩 | `Runtime/Presentation/Hand/HandPresenter.SpellCardUse.cs`, `HandCardTextView.SpellCardUse.cs`, `SpellCardUseVfx.cs`, `BattleBootstrapper.cs`; `SpellCardUsePresentationTests.cs` |
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

- 기존 `CardPreviewDetailsTests`의 낙진2개 기대 문구(중첩/영구 관련)와 현재 툴팁 ‘양쪽 턴 시작마다 10의 고정 피해를 받는다.’가 불일치한다. 현재 문구를 예전 문자열로 되돌리거나 검사를 몰래 제외하지 않는다. 최신83개 드로우 검사에는 이2개가 포함되지 않아 **전체 테스트 통과**를 의미하지 않는다.
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
