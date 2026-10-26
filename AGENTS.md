# After333 — 공통 에이전트 작업 규칙

갱신: 2026-10-26. Codex와 Google Antigravity가 함께 읽는 기준이다. 이 파일은 UTF-8 **24,000 bytes 이하**로 유지한다. 현재 작업 상태는 [PROGRESS.md](PROGRESS.md), 긴 검증 이력은 [docs/codex_handoff.md](docs/codex_handoff.md)에 기록한다. 10-24 이후 변경의 최종 동작·수정 위치·반영 상태는 [최근 인계](docs/antigravity_handoff.md)에서 먼저 확인한다.

## 1. 시작점과 문서 우선순위

- 활성 프로젝트는 `C:/Project_333`. `Documents/New project`는 이전 프로토타입이므로 수정하지 않는다. 별도 worktree에서는 그 checkout 루트를 사용한다.
- 제품 이름은 **After333**. 내부 `Project333` namespace/assembly/resource 경로, `PROJECT333_*` 환경변수, DB 이름, CLI 옵션, PlayerPrefs 키는 브랜드 변경을 이유로 바꾸지 않는다.
- 사용자 최신 결정이 우선한다. 문서 규칙 우선순위: `docs/battle_rules.md` → `docs/game_design.md` → `docs/draft_rules.md` → `docs/meta_rules.md`. 코드와 충돌하면 불일치를 보고하고 확인된 결정을 적용한다. 오래된 숫자를 조용히 복원하지 않는다.
- 작업 전 이 파일, `PROGRESS.md`, `docs/conversation_decisions.md`의 **작업 선호와 보류 사항**을 읽고 아래의 관련 문서만 선택한다. 새 에이전트는 `docs/antigravity_handoff.md`로 최근 작업을 파악하고, 이전 작업 재개 시 handoff의 최신 관련 항목을 선택해 확인한다. `docs/archive/`와 날짜별 기록은 역사 자료이며 현재 규칙을 덮어쓰지 않는다.

| 작업 | 먼저 읽을 문서 (`docs/` 아래) |
|---|---|
| 전투 판정 | `battle_rules.md`, `implementation_contract.md`의 Battle Gateway Contract / 관련 Battle Invariants / Golden Tests v1 |
| AI | `pve_ai.md`, `pve_ai_decks.md`, contract의 AI Behavior |
| 카드·강화 | `card_authoring_checklist.md`, `meta_rules.md`, 해당 `card_catalog.md` / `card_details.md`, `unit_races.md` |
| 온라인·계정 | `battle_modes.md`, `online_pvp_prototype.md`, `single_device_login.md`, `google_auth_setup.md` |
| 구매·보상·선물·광고 | 해당 `account_operation_retries.md`, `shop_card_packs.md`, `gift_inbox.md`, `rewarded_ads.md` |
| 튜토리얼·UI·음향 | 해당 `tutorial_implementation.md` / `tutorial_script_ko.md`, `editable_scene_layouts.md`, `audio_settings.md` / `background_music.md` |
| 애니메이션 | `unit_animation_authoring.md`, 해당 카드 presentation 문서 |
| 배포·백업 | `lightsail_operations.md`, `mybox_database_backup.md`, `postgres_restore_runbook.md` |

`implementation_contract.md`는 추출된 계약이며 이전 whitelist/범위의 명시적 예외가 있다. `special_effects.md`는 비교용 참고이며 전투 규칙을 대체하지 않는다.

## 2. 프로젝트와 핵심 게임 규칙

- 가로 화면 **모바일 온라인 턴제 카드게임**, Android와 Windows 개발·시험 지원. 판타지/무림/과학문명의 유닛·건물·마법 카드를 사용한다.
- 계정 티켓 **3개**로 런 시작 → 후보 3장 중 1장씩 선택해 **33장** 덱 구성 → 일반 전투 **33승 또는 3패**까지 같은 덱 사용. 첫 선택은 전설 후보 3장, 이후 비전설 후보. 전설 최대 1장, 같은 비전설 최대 3장; 완성 후 덱 편집 불가.
- 플레이어마다 **5×2** 전장, 좌표 `(column 0..4, row 0=전열/1=후열)`. 한 타일 한 소환물, 기본 점유 1×1. 마스터 시작 위치 `(2,1)`, HP **333**, ATK **3**. 상대 마스터 HP≤0이면 승리; 동시 사망은 최종 HP 비교, 같으면 무승부(런 승패 증가 없음).
- 시작 손패 **5장**으로 멀리건. 선공 골드 **3**, 후공 **4**를 전투 초기부터 지급. 양측 첫 자기 턴에는 기본 골드 추가 없음, 이후 자기 턴마다 **골드+3**. 기본 드로우는 첫 턴 포함 **1장**.
- 마나/기/전력은 각 문명의 자원. 부족분은 전투 골드로 **1:1** 대체. 전투 골드와 계정 골드는 별개. 피해·파괴 턴 시작 효과를 자원 차감보다 먼저 처리하며 이미 죽은 소환물에는 유지비를 청구하지 않는다.
- 근접/원거리와 물리/마법/고정 피해를 구별한다. 전열 보호·반격·방어력·상태·트랩은 공유 서비스의 규칙을 따른다. 보통 소환한 턴에는 공격 불가(속공 예외), 건물 이동 불가. UI 문구만으로 새 효과를 구현하지 않는다.
- 일반 전투는 사람 우선 매칭, **20초** 후 서버 AI 대체. 골드 던전은 **유료 런당 최대 3회**, 자정 초기화 없음; 승리 계정 골드10/패배0, 런 성적 불변. 재접속 유예는 서버가 끊김을 감지한 뒤 **70초**.
- 강화 최대 Lv.13. 목표 Lv.1/2/3/4/5 카드·계정 골드 비용 각각 **3/6/10/20/30**, Lv.6–13 각각 **33**. 증가량은 카드별 사용자 확정표를 따른다.
- 카드팩은 계정 골드33에 보상 가능한 카드10장. 커먼/언커먼/레어/유니크/전설 **40/30/18/9/3%**, 독립 추첨·중복 가능. 드래프트 확률과 혼동하지 않는다.
- 튜토리얼 6단계는 실제 UI를 재사용하되 고정된 로컬 연습 상태로 동작한다. 계정 재화·강화·실제 런·운영 서버를 변경하지 않으며 필수 실습 완료 후 진행한다.

## 3. 기술 스택과 환경

| 영역 | 확인된 구성 / 근거 |
|---|---|
| 클라이언트 | Unity **6000.3.12f1**, C# **9.0** 호환 코드(기존 compiler rsp 확인), Built-in Render Pipeline, Legacy Input Manager. `ProjectSettings/ProjectVersion.txt`, GraphicsSettings, ProjectSettings |
| UI·직렬화 | uGUI **2.0.0**, TextMesh Pro, Newtonsoft JSON package **3.2.2**. `Packages/manifest.json` / `packages-lock.json` |
| 기타 패키지 | 2D Animation13.0.4, Pixel Perfect5.1.1, Unity Test Framework1.6.0, LevelPlay9.5.0. MCP provider는 현재 manifest/도구에서 확인되지 않음; 설치를 전제하지 않는다 |
| 서버 | C# / ASP.NET Core **.NET8 (`net8.0`)**, nullable·implicit usings 활성. 별도 `global.json`/`LangVersion` 고정 없음; 공용 소스에 Unity가 지원하지 않는 문법/API를 넣지 않는다 |
| 통신 | 계정·덱·경제는 HTTP(S) JSON, 전투는 **WebSocket/WSS `/battle`**. 자체 서버 매치메이킹/AI; Multiplayer Center 패키지를 NGO/Relay 사용으로 오인하지 않는다 |
| DB·인증 | PostgreSQL **18**, Npgsql8.0.5, SQL 순번 마이그레이션. Game ID·Google 인증, access/refresh session family, 최신 로그인 우선. Google.Apis.Auth1.75.0 |
| 운영 | 서울 AWS Lightsail Ubuntu24.04 x86_64 → Caddy → ASP.NET `127.0.0.1:7333` → PostgreSQL `localhost:5432`. HTTPS `https://api.after333.com`, WSS `wss://api.after333.com/battle` |

운영 DB는 `project333_live`. `api-staging.after333.com`은 이전 시험 주소이고 현재 운영 접속 주소가 아니다. Cloudflare는 DNS 관리에 사용하며 이전 Windows Tunnel 실행을 현 운영 방식으로 혼동하지 않는다. 소스·운영 버전은 별도로 확인한다.

## 4. 디렉터리와 주요 진입점

| 경로 | 역할 |
|---|---|
| `Assets/*_VSlice.unity` | GameStart(시작) → DeckBuilding → Draft → Battle; OwnedCards, Shop, Tutorial. 활성 7개 씬은 `ProjectSettings/EditorBuildSettings.asset` |
| `Assets/Project333/Runtime/Domain/` | Battle/Board/Cards/Effects/Resources 상태·규칙, Unity 비의존 공용 모델 |
| `Assets/Project333/Runtime/Application/` | Commands, Services, Gateways(`IBattleGateway`), Accounts, Online DTO/전송 |
| `Assets/Project333/Runtime/Infrastructure/Data/` | JSON 로더·검증·ScriptableObject 정의, `CardDatabaseValidator.cs`, `CardUpgradeRules.cs` |
| `Assets/Project333/Runtime/Presentation/` | 씬/UI/연출. `Battle/BattleBootstrapper.cs`, `Battle/BattleScreenPresenter.cs`, `GameStartSceneController.cs`, Tutorial/, Settings/ |
| `Assets/Project333/Resources/Project333/Data/cards.json` | 배포·전투 카드 데이터 원본. 서버 publish 시 `Data/cards.json`에 복사 |
| `Assets/Project333/ScriptableObjects/StarterTen/` | Cards/ 및 트랩 자산, `StarterTenCardCatalog.asset`, Decks/. 이름의 StarterTen은 현재 카드 수가 아님 |
| `Assets/Project333/Resources/Project333/` · `Assets/Unit/` | 카드 아트·UI·음향·상태 연출 / 소환물 스프라이트·클립·Animator |
| `Assets/Project333/Editor/` · `Tests/EditMode/` | 카드 생성/검증·씬 materialize·빌드 처리기 / NUnit·Unity 테스트 |
| `Server/Project333.PvpServer/` | `Program.cs`, Auth/, BattleSessions/(`BattleSession.cs`), Matchmaking/, Cards/, Shop/, Persistence/Migrations/, Tools/ |
| `Server/Project333.AiChecks/` | 오프라인 전투/AI/직렬화 회귀 실행 프로그램 |
| `Server/Project333.RunAuthorityChecks/` | 임시 DB·서버의 경제/런/단일 로그인·실제 WebSocket 검사 |
| `Server/Project333.LoadChecks/`, `*ClientChecks/` | 부하·계정 작업·광고 클라이언트 검사; 각 도구의 대상/격리 조건 확인 |
| `Tools/` · `docs/` | 아트/음향 제작 도구 / 규칙·운영·검증 문서. 과거 생성기가 현재 사용자 자산을 덮어쓰지 않게 주의 |
| `Packages/` · `ProjectSettings/` | 패키지 잠금·Unity 설정: 버전 관리 대상 |

Assembly는 `Project333.Runtime`, Editor 전용 `Project333.Editor`, 테스트 `Project333.Tests.EditMode`, SDK 참조 별도 `Project333.LevelPlay`로 나뉜다. 서버 csproj가 Domain/Commands/Services 및 일부 DTO·Data `.cs`를 직접 링크한다. 공용 로직 변경은 양쪽 컴파일 영향이 있다.

## 5. 빌드·실행·테스트

아래는 **사용 방법**이지 자동 실행 지시가 아니다. 클라이언트 빌드·설치는 사용자가 한다. 서버 배포·재시작·운영 DB 변경은 요청 범위를 확인한다. 새 개발용 PowerShell에서 시작하며 운영 비밀 환경변수를 그대로 상속하지 않는다.

```powershell
Set-Location 'C:\Project_333' # worktree에서는 해당 루트
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned

# DB 없는 로컬 연결/health 시험: 로그인·경제·매칭은 불가
& .\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 -NoDatabase -DevMinimumTickets -1
```

계정/매칭 포함 로컬 실행은 **별도 개발 DB**의 연결 문자열을 `PROJECT333_DB_CONNECTION`에 비공개로 설정한 터미널에서 같은 스크립트를 `-NoDatabase` 없이 실행한다. `Program.cs`는 시작 시 미적용 SQL 마이그레이션을 자동 실행한다. DB 주소를 확인하기 전 서버를 실행하지 않는다. `-DevMinimumTickets -1`은 개발용 최소 티켓 보정을 끈다. 종료는 해당 터미널 Ctrl+C.

```powershell
# 컴파일만: 운영 서버 시작·DB 접속 없음, 실행 중 DLL과 출력 경로 분리
dotnet build .\Server\Project333.PvpServer\Project333.PvpServer.csproj -c Release --artifacts-path .\TempBuild\AgentCheck

# 오프라인 체크: 먼저 빌드하고 DLL 직접 실행 (프로젝트는 dotnet test 대상이 아님)
dotnet build .\Server\Project333.AiChecks\Project333.AiChecks.csproj -c Release -o .\TempBuild\AgentAiChecks
if ($LASTEXITCODE -ne 0) { throw 'AI check build failed.' }
dotnet .\TempBuild\AgentAiChecks\Project333.AiChecks.dll --opening-gold-only

# 실제 계정·DB 경로 검사: 새 임시 PostgreSQL 클러스터+루프백 서버 사용
dotnet build .\Server\Project333.PvpServer\Project333.PvpServer.csproj -c Release -o .\TempBuild\AgentServer
if ($LASTEXITCODE -ne 0) { throw 'Server check build failed.' }
& .\Server\Project333.RunAuthorityChecks\RunIsolatedRunAuthorityChecks.ps1 -ServerOutput .\TempBuild\AgentServer
# 해당 기능만 필요하면 위 명령에 -SingleLoginOnly 또는 -GoldDungeonOnly
```

DB 검사는 PostgreSQL18 도구가 필요하며 기본 포트15439/17339를 사용한다. 다른 검사와 동시에 실행하지 않는다. 일반 터미널의 임의 DB/운영 URL을 통합 검사 대상으로 바꾸지 않는다. `AiChecks`의 다른 필터는 `Program.cs`를 확인한다.

Unity 실행·빌드:
- Unity Hub에서 위 버전으로 프로젝트 열기 → `Assets/GameStart_VSlice.unity` → Play. 로컬 시험은 실제 **Server Endpoint** 값을 `http://127.0.0.1:7333`로 확인한다(저장된 URL이 Inspector 기본값보다 우선할 수 있음).
- 사용자 빌드: **File > Build Profiles** → Android 또는 Windows → 활성 7개 씬 확인 → **Build / Build And Run**, 산출물은 `Builds/`. Android 모듈/SDK/NDK/JDK와 서명 설정은 사용자 환경을 사용한다. `Scripts Only Build`는 build identity 갱신이 안 되어 금지한다. `Project333ClientBuildProcessor.cs`가 ClientBuild JSON을 생성한다.
- 카드 검증: **Tools > Project333 > Data > Validate cards.json**. 화면 materialize는 `docs/editable_scene_layouts.md`를 따른다. 사용자 Editor를 강제로 열거나 씬을 저장하지 않는다.
- 테스트: **Window > General > Test Runner**의 EditMode 및 관련 PlayMode 검사. 자동화는 사용자 Editor와 **별도 검증 checkout/copy**에서 실행; 미커밋 수정도 검증 사본에 반영됐는지 확인한다.

```powershell
# 경로는 해당 PC의 Unity 설치와 별도 검증 프로젝트로 지정
$unity = 'C:\Users\youndonghyeok\Documents\makegame\6000.3.12f1\Editor\Unity.exe'
$validationProject = 'C:\After333Validation' # Assets/Packages/ProjectSettings가 있는 별도 사본
$testOutput = Join-Path $validationProject 'TestResults'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
& $unity -batchmode -projectPath $validationProject -runTests -testPlatform EditMode -testResults "$testOutput\editmode.xml" -logFile "$testOutput\editmode.log"
# 관련 테스트만: -testFilter 'Project333.Tests.EditMode.EffectsVolumeTests'
# PlayMode 실행은 -testPlatform PlayMode 로 별도 수행; XML 결과까지 확인
```

검증 사본의 회사/제품명·PlayerPrefs·자격 증명을 사용자 환경과 분리한다. SDK 없는 사본에서 LevelPlay assembly를 제외했다면 광고 검증 제외를 명시하고 실제 프로젝트 SDK를 삭제하지 않는다. 수정과 관련된 검증만 수행하며 전체 회귀/실기기/배포 확인을 했다고 확대 보고하지 않는다.

운영 명령은 Lightsail SSH에서 `sudo systemctl start|stop|restart after333.service`(하나를 선택). 진행 중 전투에 영향이 있으므로 로컬 실행과 혼동하지 않는다. 전체 절차는 `docs/lightsail_operations.md`; 부팅 자동 시작/자동 재시작은 계속 보류다.

## 6. 아키텍처·코딩과 보존 제약

- **서버 권위**: 계정, 덱 제시/선택/저장, 재화, 강화, 구매, 보상, 최종 전투 결과는 서버가 확정한다. UI는 intent를 `IBattleGateway`로 전달하고 서버 DTO/이벤트로 표시를 갱신한다. 로컬 게이트웨이도 기존 공유 서비스를 우회하지 않는다.
- WebSocket의 Sequence, ExpectedTurnNumber, 계정/좌석 검증을 유지한다. 손패·소환물은 RuntimeId로 추적하고 타일/목록 인덱스를 신원으로 사용하지 않는다. 이동/교환·드로우 도중 새 상태가 와도 뷰를 잘못 재사용하지 않는다.
- 상대 손패/트랩 등 비공개 정보는 **서버에서 시청자별로 필터**한다. UI에서 가리는 것만으로 대체하지 않는다. 인증 신원은 `auth_identities`, 재접속은 인증 계정의 원래 좌석/런/덱으로 복구한다.
- 경제 트랜잭션·멱등 operation/receipt·영속 결과 outbox를 유지한다. 클라이언트 승수/보상 제출을 다시 허용하지 않는다. 구매/강화 복구는 자동 처리하며 수동 복구 버튼·연결 복구 안내를 임의 추가하지 않는다.
- 서버 장애/점검 중 미완료 전투는 무승패 취소, 확정 결과는 멱등 전달. 모드별 도전 환급은 `battle_modes.md`. 단일 로그인 교체 자체로 패배를 만들지 않는다.
- **카드 변경은 한 묶음**: `cards.json` + 해당 ScriptableObject/카탈로그 + Domain/Data의 Rules/validator + 생성 도구 + 서버 링크/DTO + 테스트 + 카드/규칙 문서를 함께 대조한다. 복사된 `Builds/**/Data/cards.json`을 원본처럼 편집하지 않는다. 최종 랜덤 보상은 `RewardCardEligibility`까지 확인한다.
- 새 카드 강화 가능 여부와 Lv.1–13 증가량, 새 유닛 종족(없음 포함)은 사용자 확정이 필요하다. 기존 답은 다시 묻지 않고 이름/그림/유사 카드로 추측하지 않는다. 종족은 효과와 별개이며 망각으로 제거하지 않는다. 건물/마법에는 유닛 종족을 추가하지 않는다. 확정 예: Cerberus HP+1(Lv.1–12), ATK+1(Lv.13만); ManaPond HP+1(Lv.1–13), ATK 증가 없음.
- C# 기존 스타일: 4칸 들여쓰기, PascalCase 타입/공개 멤버, camelCase 인자/지역변수, `_camelCase` private 필드. MonoBehaviour 설정은 `[SerializeField] private`. 파일명/클래스명과 `Project333.*` namespace를 유지하고 주변 파일 스타일을 따른다. DTO/enum 숫자/직렬화 필드/CardId 변경은 호환성을 먼저 확인한다(`firebolt`, `A-111` 같은 기존 ID 보존).
- Domain/공유 Services에 UnityEngine·Editor 의존성을 넣지 않는다. Unity 객체 조작은 메인 스레드에서 하고 씬 종료/disable 시 비동기 작업·이벤트·코루틴을 정리한다. 연출 완료가 서버 판정을 대신하지 않는다.
- `.meta` GUID와 사용자 수동 자산/씬 편집을 보존한다. 새 자산과 `.meta`는 함께 관리한다. Visible Meta Files/Force Text 설정을 유지한다. 일괄 재생성·자동 정렬·씬 YAML 정리로 무관한 변경을 만들지 않는다.
- 소환물 프레임은 **512×512**, 접지 기준 아래 투명 여백 약 **16px**. 무기/효과 포함 셀 안에 완전히 넣고 프레임별로 독립 재중앙정렬하지 않는다. PNG → AnimationClip → Animator Controller → 카드 Board Animator Controller 연결 유지. 사용자가 수정한 BlueDragon/Gwangma와 GaebangChief Idle 등 현재 자산을 과거 생성물로 덮어쓰지 않는다.
- 모든 피격 공통음은 `TileTextView`의 UnitHit. 별도 캐릭터 피격음을 중복 추가하지 않는다. 새 효과음은 `After333SoundEffects`로 등록해 마스터/효과음 볼륨을 각각 한 번만 적용한다. 등록 후 원래 source.volume을 다시 덮어쓰지 않는다.
- 비밀번호·token·SSH/암호화/서명 개인키·DB 덤프·실사용자 로그를 코드/문서/커밋에 넣지 않는다. 진단에는 값 대신 이름·구성 여부·마스킹된 요약을 사용한다.
- 랭크/MMR/시즌, 유료 결제, 새 로그인 제공자, 다중 타일 규칙 등 미확정 기능을 추가하지 않는다. **GitHub Actions, 서버 자동 시작/재시작, 전투 기록 자동 정리**는 사용자 보류다.

## 7. 두 에이전트 협업 절차

1. 시작 전 `git status --short`, `git diff --stat`, 현재 branch/HEAD와 `PROGRESS.md`를 확인한다. 사용자 수정과 다른 에이전트 작업을 구별하고 읽은 파일의 변경 여부를 쓰기 직전에 재확인한다.
2. 작업 ID·담당 에이전트·목표·파일 범위·branch/worktree·완료 조건을 PROGRESS의 작업 예약표에 기록한다. 같은 checkout의 기록/편집은 순서대로 한다. 문서 예약은 실제 lock이 아니므로 기록만 믿고 겹쳐 쓰지 않는다.
3. 같은 checkout은 **한 번에 한 에이전트만 쓰기**. 다른 에이전트는 읽기/리뷰. 실제 동시 구현은 공통 기준 커밋 후 별도 worktree/branch를 사용한다(미커밋 파일은 새 worktree에 자동 복사되지 않음). 두 Unity Editor가 같은 프로젝트 폴더를 동시에 열지 않는다.
4. `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `cards.json`, package lock, 마이그레이션 순번, AGENTS/PROGRESS는 공동 충돌 지점이다. 담당자를 한 명으로 정하고 합칠 때 GUID/참조/순번을 다시 검사한다. 공유 문서는 한 명이 통합하며 worktree별 예약표가 자동 동기화된다고 가정하지 않는다.
5. 다른 사람의 변경을 reset/clean/stash/checkout으로 지우지 않는다. 광범위 `git add .`를 하지 않는다. 커밋·push는 사용자가 요청할 때만 하며 기능별 필요한 자산/테스트/마이그레이션도 함께 선택한다.
6. 완료 후 관련 검증, `git diff --check`(기존 경고와 새 경고 구별), diff/status와 `.meta` 누락을 확인한다. PROGRESS에 변경 요약·결정·검증 결과/미검증·다음 행동을 남기고 예약을 해제한다. 상세 증거는 관련 docs/handoff에 링크하고 긴 로그를 복제하지 않는다.
7. 상태를 **소스 완료 / 격리 검증 / 실기기 확인 / 운영 배포**로 나누고 날짜·BuildId·migration을 확인된 범위에서만 기록한다. 차기 에이전트가 같은 검사를 불필요하게 반복하지 않도록 실패/수정/최종 결과도 남긴다.

추가 운용·커밋 정리 안내: [docs/agent_collaboration.md](docs/agent_collaboration.md). 채팅 이력·메모리·도구 권한은 두 제품 사이에 자동 공유되지 않는다. 공통 문서와 실제 diff가 인계의 기준이다.
