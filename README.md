# After333

공통 개발 규칙: [AGENTS.md](AGENTS.md) · 최신 진행/검증/배포 구분: [PROGRESS.md](PROGRESS.md) · [Codex·Antigravity 협업 안내](docs/agent_collaboration.md) · [10-24 이후 개발 인계](docs/antigravity_handoff.md)

최근 개발 인계 갱신: **2026-10-26**. 전체 규칙/운영 이력의10-20 대조와 이후 소스·검증 기록을 구분합니다. 현재 카드 정의60종·네이티브/덱 빌딩54종·실제 랜덤 보상41종·강화 가능44종. 튜토리얼6단계와 시작 손패5장이 구현되어 있습니다. 운영 서버는 서울 AWS Lightsail + Caddy + PostgreSQL18이며 공개 주소는 `https://api.after333.com`입니다. 운영 전환 확인 범위·백업·남은 시험은 [날짜별 운영·백업 상태](docs/current_project_status.md), 실행 명령은 [Lightsail 운영](docs/lightsail_operations.md)을 따릅니다.
<img width="2340" height="1080" alt="Screenshot_20260906_181227_After333" src="https://github.com/user-attachments/assets/0b078a7a-a5c8-4163-936d-75f36ec6d053" />

**덱 빌딩의 선택과 타일 배치의 전략을 결합한 턴제 카드 게임**

After333는 **판타지·무림·과학 문명**이 공존하는 세계를 배경으로, 세 문명의 카드를 조합해 33장의 덱을 만들고 각 플레이어의 5×2 전장에서 대결하는 게임입니다. 각 문명의 고유 자원(마나, 기, 전력)과 공통 자원인 골드를 전략적으로 수집·사용해 유닛과 건물을 소환하고 마법을 활용하며, **자신의 마스터를 지키면서 상대 마스터를 처치하는 것**이 전투의 목표입니다. 카드의 성능뿐 아니라 전열·후열 배치, 자원 운용, 행동 순서가 승패에 영향을 줍니다.

졸업작품으로 개발 중이며, **Unity 기반 전투 프로토타입을 계정·매칭·재접속·메타 성장이 연결된 서버 권위형 게임으로 확장**하는 데 중점을 두었습니다.

| 항목 | 내용 |
| --- | --- |
| 장르 | 타일 기반 턴제 전략 카드 게임 |
| 플랫폼 | Windows · Android, 가로 화면 |
| 플레이 모드 | 일반 전투: 사람 우선 매칭·20초 후 AI 대체 / 별도 골드 던전: 티켓 3개로 시작한 런마다 최대 3회·자정 초기화 없음 |
| 전투 목표 | 자신의 마스터를 지키고 상대 마스터 처치 |
| 핵심 목표 | 하나의 덱으로 3패 전에 33승 달성 |
| 주요 구현 범위 | 전투, 온라인 동기화·재접속, 계정·DB, 덱 빌딩, 보상·강화, 티켓 상점·광고, 모바일 UI |

## 세계관과 자원 전략

판타지의 마법과 용, 무림의 무공과 영약, 과학 문명의 로봇과 기계가 같은 전장에서 만납니다. 플레이어는 서로 다른 문명의 카드를 한 덱에 조합하고, 카드 구성에 맞는 자원 확보와 사용 계획을 세웁니다.

| 문명 | 고유 자원 |
| --- | --- |
| 판타지 | 마나 |
| 무림 | 기 |
| 과학 문명 | 전력 |

**골드는 세 문명이 공통으로 사용하는 전투 자원**입니다. 골드 비용을 지불하는 데 쓰일 뿐 아니라, 부족한 마나·기·전력을 1:1로 대신 지불할 수도 있습니다. 자원 생산 카드로 이후 턴을 준비할지, 지금 가진 자원을 소환과 마법에 투입할지, 골드를 다른 자원의 대체 비용으로 사용할지 판단하는 것이 핵심 전략입니다.

## 카드 비주얼

<p align="center">
  <img src="Assets/Project333/Resources/Project333/CardArtwork/BlueDragon.png" width="180" alt="판타지 카드: 블루 드래곤" />
  <img src="Assets/Project333/Resources/Project333/CardArtwork/Gwangma.png" width="180" alt="무림 카드: 광마" />
  <img src="Assets/Project333/Resources/Project333/CardArtwork/A-111.png" width="180" alt="과학 문명 카드: A-111" />
</p>

판타지·무림·과학 문명을 표현한 카드 아트 예시입니다.

2026-10-24 대조 기준 공유 카드 정의는 60종(유닛 27·건물 7·마법 26), 네이티브 카드 카탈로그와 덱 빌딩 포함 카드는 각각 54종입니다. 실제 랜덤 보상 대상은 41종이며, 보상 허용 플래그가 적용된 43종과 구분합니다. 현재 스펙과 강화표는 [전체 카드 목록](docs/card_catalog.md)과 [카드 상세](docs/card_details.md), 문서별 안내는 [게임 문서 안내](docs/README.md)를 참고하세요.

## 게임 플레이

```text
로그인 → 티켓 3장으로 런 시작 → 3장 중 1장씩 선택해 33장 덱 구성 → 일반 전투
                                    → 33승 또는 3패로 런 종료
                                    → 보상 수령 → 카드 강화 · 다음 도전
```

런은 하나의 덱으로 여러 일반 전투를 이어가는 도전 단위입니다. 별도 골드 던전은 같은 덱을 사용하지만 런 승패를 바꾸지 않으며, 티켓 3개로 시작한 런마다 최대 3회 도전할 수 있고 승리 시 계정 골드 10을 지급합니다. 자정이나 재로그인으로 도전 횟수가 초기화되지는 않습니다. 전투 밖에서 획득한 카드와 골드는 계정에 누적되며, 다음 도전을 위한 성장으로 연결됩니다.

| 시스템 | 플레이 경험 |
| --- | --- |
| 전장과 자원 | 유닛·건물·마법을 활용한 5×2 타일 전투, 마나·기·전력·골드 운용 |
| 공격과 특수효과 | 근거리·원거리, 반격, 물리·마법·고정 피해, 방어력과 다양한 특수효과의 조합 |
| 덱 빌딩 | 매 선택마다 제시되는 카드 3장 중 1장 선택, 중단한 덱 구성 이어하기 |
| 온라인 전투 | 일반 전투·골드 던전의 서버 판정, 연결 종료 후 70초 이내 같은 전투 재접속 |
| 메타 성장 | 런 종료 보상, 동일 카드와 골드를 사용하는 최대 Lv.13 강화 |
| 상점과 광고 | 계정 골드로 티켓·33골드 카드팩(10장) 구매, 보상형 광고 시청 후 티켓 획득 |

## 기술 스택

| 영역 | 기술 |
| --- | --- |
| 게임 클라이언트 | Unity 6 · C# · Unity UI · TextMesh Pro |
| 게임 서버 | ASP.NET Core · .NET 8 · WebSocket |
| 데이터 저장 | PostgreSQL · Npgsql · SQL 마이그레이션 |
| 계정 인증 | 게임 ID 인증 · Google OAuth / OpenID Connect · 세션·갱신 토큰 |
| 모바일 연동 | Android Credential Manager · Android Keystore · IL2CPP |
| 광고 | Unity LevelPlay · 서버 간 보상 검증 콜백 |
| 테스트 | Unity Test Framework · NUnit · 서버 시나리오 검증 스크립트 |

## 아키텍처

```mermaid
flowchart LR
    Client["Unity 클라이언트<br/>입력 · UI · 애니메이션 · 효과음"]
    Server["ASP.NET Core 서버<br/>인증 · 매칭 · 전투 판정 · 메타 처리"]
    DB[("PostgreSQL<br/>계정 · 덱 · 보상 · 매치 · 스냅샷")]
    Ads["LevelPlay"]
    Client <-->|"HTTPS: 계정 / 덱 / 상점"| Server
    Client <-->|"WSS: 전투 명령 / 상태 / 이벤트"| Server
    Server <-->|"Npgsql / SQL"| DB
    Client -->|"보상형 광고 재생"| Ads
    Ads -->|"S2S 보상 콜백"| Server
```

**입력과 표현은 클라이언트, 게임 판정과 영속 데이터는 서버**가 담당합니다. Unity와 서버가 핵심 전투 규칙 코드를 공유하며, `Domain`, `Application`, `Infrastructure`, `Presentation`으로 역할을 나누었습니다.

## 핵심 구현

### 1. 로컬 전투에서 서버 권위형 PvP로 전환

**문제 인식:** 각 클라이언트가 전투를 계산하면 서로 다른 결과가 발생할 수 있고, 조작된 요청이나 재접속 상황에서 신뢰할 상태의 기준이 불명확해집니다.

소환·이동·공격·마법 사용을 명령으로 분리하고, 서버에서 행동 가능 여부와 비용을 검증한 뒤 전투 상태를 변경하도록 구성했습니다. 클라이언트는 `StateView`와 `BattleEvents`를 받아 화면을 갱신하며, 상대 손패는 카드 정체를 제외한 장수만 전달합니다.

`IBattleGateway`로 화면과 전투 처리 경계를 분리해 로컬·온라인 처리 경로를 구분했습니다. 전투 규칙을 공유하되, 온라인에서는 서버 결과를 기준으로 표현하도록 설계했습니다.

관련 코드: [전투 Gateway](Assets/Project333/Runtime/Application/Gateways/IBattleGateway.cs) · [서버 전투 세션](Server/Project333.PvpServer/BattleSessions/BattleSession.cs) · [플레이어별 상태 생성](Assets/Project333/Runtime/Application/Online/BattleStateViewFactory.cs)

### 2. 중단 후에도 유지되는 전투와 덱 빌딩

**문제 인식:** 앱 종료나 모바일 네트워크 변경 이후에도 같은 계정의 진행 상태를 복원해야 합니다. 선택한 카드만 저장하면 재접속 때 제시 카드가 바뀌는 문제도 생깁니다.

서버에서 덱 빌딩 후보를 생성하고, 선택한 카드와 현재 제시 카드 3장을 함께 저장했습니다. 카드 선택 순번과 후보를 검증하고 선택 결과·다음 후보를 하나의 트랜잭션으로 반영해, 중단 이후에도 같은 선택 지점으로 돌아오도록 구성했습니다.

일반 전투와 골드 던전은 계정·매치·플레이어 자리 정보를 연결하고, 서버가 연결 종료를 감지한 뒤 70초의 재접속 유예를 적용합니다. 원래 자리·덱·전투 스냅샷을 복원하며, 같은 골드 던전 재접속으로 횟수를 추가 차감하지 않습니다. 이미 종료된 전투는 결과를 안내합니다. 서버 중단으로 끝나지 못한 전투는 승패 없이 취소하는 현재 정책을 따릅니다.

관련 코드: [오퍼 생성](Server/Project333.PvpServer/Runs/ServerDraftOfferGenerator.cs) · [런 진행 저장](Server/Project333.PvpServer/Runs/RunStartService.cs) · [매칭·재접속](Server/Project333.PvpServer/Matchmaking/PvpMatchmakingService.cs) · [스냅샷 저장](Server/Project333.PvpServer/BattlePersistence)

### 3. 보상·강화·광고의 데이터 정합성

**문제 인식:** 중복 클릭, 요청 재시도, 광고 콜백 재전송이 발생해도 재화가 중복 지급되거나 강화 비용만 차감되어서는 안 됩니다.

카드 강화는 서버에서 보유 수량·현재 레벨·비용을 검증하고, 골드 및 카드 소모와 레벨 변경을 DB 트랜잭션으로 처리합니다. 런 종료 보상 역시 계정의 서버 저장 상태를 기준으로 수령 여부를 관리합니다.

광고 SDK의 클라이언트 완료 이벤트를 지급 근거로 사용하지 않고, **LevelPlay의 서버 간 콜백 서명을 검증한 뒤** 티켓을 지급합니다. 광고 이벤트 ID와 보상 시도 상태를 저장해 같은 콜백이 다시 와도 지급 결과가 중복되지 않도록 구성했습니다.

관련 코드: [카드 강화](Server/Project333.PvpServer/Cards/CardUpgradeService.cs) · [광고 보상 검증](Server/Project333.PvpServer/Ads/RewardedAdService.cs) · [DB 스키마](Server/Project333.PvpServer/Persistence/Migrations)

### 4. 카드 확장을 고려한 규칙과 검증

**문제 인식:** 카드와 특수효과가 늘어날수록 개별 기능보다 효과끼리 충돌하는 경계 조건이 중요해집니다.

카드 정의는 JSON 데이터와 Unity 에셋으로 관리하고, 공격·피해·턴 진행·마법 처리를 서비스로 분리했습니다. 피해 속성별 방어력, 다단히트, 쉴더, 불굴, 봉인, 무적 등의 상호작용을 공통 규칙과 회귀 테스트로 검증합니다.

카드 데이터 Validator로 잘못된 정의를 확인하고, 기능 개발과 실제 카드 공개를 분리할 수 있도록 덱 빌딩·보상 등장 여부를 카드별로 관리합니다. 최근에는 지진·메테오·파이어스피어·보호막 카드, 스턴·빙결, 무료 설치 후 조건 충족 시 비용을 지불하는 비공개 트랩 5종을 추가했습니다. 뉴클리어는 상대 전체에 즉시 고정 피해를 주며, 영구 낙진은 사용 당시 상대 전장의 유닛·마스터에만 적용하며 이후 소환에는 적용하지 않습니다. 오크 전사와 오크 광전사의 기본 공격력은 모두 20입니다.

관련 코드: [카드 데이터](Assets/Project333/Resources/Project333/Data/cards.json) · [전투 서비스](Assets/Project333/Runtime/Application/Services) · [데이터 검증](Assets/Project333/Runtime/Infrastructure/Data/CardDatabaseValidator.cs)

### 5. 해상도 대응과 전투 정보 표현

**문제 인식:** PC와 모바일의 화면 비율이 달라지면 타일·소환물·스탯·연출 위치가 어긋나고, 네트워크 상태를 그대로 표시하는 것만으로는 전투 과정을 이해하기 어렵습니다.

보드와 UI를 화면 크기에 맞춰 배치하고, 카드의 ATK/HP 숫자는 UI 사각형이 아닌 **실제로 표시되는 이미지 영역의 정규화 좌표**를 기준으로 배치했습니다. 이미지 비율을 유지하면서 남는 여백 때문에 숫자가 어긋나는 문제를 처리한 방식입니다.

전투 기록은 서버에 구조화된 사건을 저장하고, 클라이언트에서 한국어 문장으로 변환합니다. 최근 33줄을 재접속 시 복원하며, 상대 카드 공개 연출·특수효과 툴팁·드로우 효과음으로 판정 결과를 전달합니다.

손패는 중앙 하단에 접혀 있다가 클릭하면 부드럽게 펼쳐집니다. 비용을 지불할 수 있는 카드의 합법 대상 타일은 낮춘 밝기의 흰색으로 점멸합니다. 턴 종료를 누르면 다음 자기 턴까지 행동을 잠그지만 카드 열람은 허용합니다. 보유카드 상세는 현재 데이터와 종족을 표시하며, 그림을 뒤집으면 Lv.1–13의 강화 증가량 표를 볼 수 있습니다. 안내창은 NeoDunggeunmo Pro 글꼴과 짙은 남색 패널로 통일하고, 내 턴 표시는 기존 금색 픽셀 배너를 유지합니다.

관련 코드: [반응형 보드](Assets/Project333/Runtime/Presentation/Battle/BoardPresenter.cs) · [카드 스탯 좌표 계산](Assets/Project333/Runtime/Presentation/Hand/HandCardStatOverlayLayout.cs) · [구조화된 전투 기록](Assets/Project333/Runtime/Application/Online/BattleCombatLogEntryDto.cs) · [기록 문장 생성](Assets/Project333/Runtime/Application/Services/CombatLogFormatter.cs)

## 검증 방식

기능이 동작하는 경우뿐 아니라 **실패·재시도·상태 전환 시에도 규칙이 유지되는지**를 중심으로 검증합니다.

| 검증 대상 | 주요 확인 내용 |
| --- | --- |
| 전투 규칙 | 공격·반격, 방어력, 다단히트, 사망과 승패, 특수효과 간 상호작용 |
| 온라인 상태 | 플레이어 관점 변환, 비공개 손패 정보, 전투 상태 복원 |
| 계정과 메타 | 자동 로그인, 덱 빌딩 복원, 보상 중복 수령 방지, 강화 비용과 레벨 반영 |
| 화면 표현 | 서로 다른 화면 비율에서 카드 스탯 위치 및 크기 계산 |

테스트 코드: [EditMode 테스트](Assets/Project333/Tests/EditMode) · [피해 판정 테스트](Assets/Project333/Tests/EditMode/DamageTypeResolutionTests.cs) · [온라인 상태 복원 테스트](Assets/Project333/Tests/EditMode/BattleStateViewProjectorTests.cs) · [카드 스탯 배치 테스트](Assets/Project333/Tests/EditMode/HandCardStatOverlayLayoutTests.cs)

## 현재 개발 범위

아래 화면은 2026-09-06 당시 캡처이며 최신 UI 상태를 보증하는 자료는 아닙니다.
<img width="2340" height="1080" alt="Screenshot_20260906_181227_After333" src="https://github.com/user-attachments/assets/dafc8711-89ff-49ae-8533-29c77cfac4b4" />
전투 씬

<img width="2340" height="1080" alt="Screenshot_20260906_181457_After333" src="https://github.com/user-attachments/assets/22f1fbb3-23fd-498e-8af7-f3c13a36d74e" />
보유 카드 씬

<img width="2340" height="1080" alt="Screenshot_20260906_182005_After333" src="https://github.com/user-attachments/assets/4ca7f41a-92c2-4c96-b40b-dd8a42f4bd00" />
상점 씬

계정 로그인부터 덱 빌딩, 일반 전투·골드 던전, 결과 저장, 보상 수령, 카드 강화와 티켓 상점까지 연결된 플레이 흐름을 구현했습니다. 현재는 카드 콘텐츠·밸런스·AI 행동과 모바일 사용성을 지속적으로 개선하고 있습니다.

