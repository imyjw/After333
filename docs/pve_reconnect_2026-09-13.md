# After333 PvE 60초 재접속 — 2026-09-13

## 적용 상태

- 서버·클라이언트 소스 수정과 격리 회귀 검증 완료.
- 운영 서버 배포 완료: 2026-09-13 14:37:57 KST, `20260913-pve-reconnect` 릴리스(PID 6880).
- 앱 빌드·설치 및 실제 Android/PC 재접속 화면 검사는 이번 작업에서 하지 않았다. 새 앱은 사용자가 빌드·설치한다.
- 서버 중단 시 진행 중 전투를 승패 없이 취소하는 기존 정책은 유지한다.

## 사용자 확정 규칙과 동작

서버가 실행 중인 상태에서 PvE 접속이 끊기면 PvP처럼 **서버의 끊김 감지 시점부터 60초** 동안 같은 계정으로 원래 전투에 재접속할 수 있다. 앱을 강제 종료했다 다시 실행해도 서버에 남은 전투와 잔여 시간을 조회한다. 새 방이나 AI 덱을 생성하지 않고 기존 전투 ID·좌석·런·덱·강화 정보·전장을 유지한다.

60초 안에 복귀하면 이전 재접속 예약이 해제된다. 기한이 지났고 전투가 아직 진행 중이면 서버가 원래 참가 런에 기권 패배를 한 번만 기록한다. 재접속 자체로 승패·티켓·재화가 바뀌지 않으며, 다른 계정이 기존 전투 ID를 알아도 좌석을 가져갈 수 없다.

기존 턴 기한과 AI 진행은 유지한다. 연결 끊김 때문에 턴 시간을 멈추거나 재접속할 때 새 턴 시간을 주는 규칙은 추가하지 않았다. 복귀 전에 정상 전투 결과가 확정됐다면 그 서버 결과가 적용된다. 서버 자체가 중단되면 60초 재접속과 별개로 [서버 중단 취소 정책](./server_interruption_cancellation_2026-09-13.md)에 따라 미완료 전투를 승패 없이 취소한다.

## 구현

| 부분 | 변경 내용 |
|---|---|
| 서버 조회 | 인증된 `GET /battle/reconnect-status`가 기존 `MatchId`, 좌석, 잔여 시간·기한과 `BattleMode=pve/pvp`를 반환한다. PvE는 현재 서버 프로세스의 계정별 예약을 조회한다. PvP는 기존처럼 DB와 메모리 예약의 일치를 확인한다. |
| 구형 앱 호환 | `/pvp/reconnect-status`는 계속 PvP만 반환한다. 모드 필드가 없는 기존 응답은 클라이언트에서 PvP로 해석한다. |
| 앱 재실행 | 덱/런 화면이 공통 조회 API의 모드에 맞춰 재접속 버튼과 남은 시간을 표시한다. PvE 재접속은 서버가 준 전투 ID와 재접속 의도를 사용하며, 로컬에서 60초를 다시 시작하지 않는다. |
| 참가자 보호 | 재접속 시 계정과 기존 참가자를 확인하고 원래 좌석·런·덱·강화 정보를 복원한다. 만료된 예약이나 이미 끝난 전투를 새 전투로 바꾸지 않는다. |
| 중복 전투 방지 | 같은 계정의 입장 조회·등록을 직렬화한다. 동시 PvE 요청 두 개가 별도 전투를 만들지 못하며, 기존 PvE/PvP가 진행·재접속 대기 중이면 다른 모드의 새 전투도 막는다. 다른 계정의 입장은 독립적으로 진행한다. |
| AI 실행 | 재접속 후 AI 진행을 이어갈 수 있게 하되 세션당 실행 루프 하나만 허용한다. 이전 소켓의 취소가 AI 루프를 중단하지 않도록 서버 수명을 사용하고, 서버 중단 시에는 실행을 막는다. |
| 만료 처리 | 기한 전 호출은 패배를 만들지 않고 실제 마감까지 기다린다. 예약의 계정·정확한 마감 시각을 대조하므로 오래된 콜백이 복귀한 전투나 새 예약을 잘못 종료하지 않는다. |

주요 소스는 `Server/Project333.PvpServer/Program.cs`, `BattleSessions/BattleSession.cs`, `BattleSessionManager.cs`, 클라이언트 `GuestAuthClient.cs`, `DraftSceneController.cs`, `DraftRunSessionState.cs`, `OnlineBattleConnectionTester.cs`다.

## 검증 결과

최종 서버 배포 후보로 `RunIsolatedRunAuthorityChecks.ps1`의 **91개 그룹**을 통과했다. 새 합성 계정과 별도 PostgreSQL/HTTP 서버만 사용하는 검사이며 운영 계정을 테스트에 사용하지 않았다.

- 실제 소켓 두 개로 같은 계정의 PvE 시작을 동시에 요청해 전투 하나만 진행되는지 확인.
- 실제 PvE 연결을 끊은 뒤 새 소켓으로 두 번 재접속. 기존 계정·좌석·전투·이동한 마스터·손패·자원·턴 기한 보존과 AI 손패 비공개 확인.
- 공통 조회 API의 PvE 모드·서버 잔여 시간, 구형 PvP API의 모드 분리, 다른 계정의 조회·좌석 접근 차단 확인.
- 복귀 후 종료 결과의 원래 런·덱 귀속, 동일 결과 재전달의 중복 반영 방지, 종료된 전투 재접속 거부 확인.
- 60초 기본값·명시값, 기한 전·후와 오래된 콜백, 서버 중단 시 이미 만료 시점에 도달한 예약도 무효 취소되는지 확인.
- 계정별 입장 대기·다른 계정 허용·대기 취소·중복 해제, 72개 연속 동시 입장, AI 루프 동시 진입 차단·해제 후 재진입·서버 중단 후 차단 확인.
- 별도 실제 HTTP 강제 종료·재시작에서 PvP/PvE 무효 취소, 기존 성적·재화 유지, 늦은 결과 차단과 같은 런·덱 재사용 확인. 구매/강화 재처리와 DB 미설정 성적 업로드 차단 부가 검사도 통과.

Unity 관련 **EditMode 46개**도 통과했다. 이는 해당 기능 중심 검사이며 과거 전체 Unity 검사 횟수와 합산하지 않는다. 앱 빌드 성공이나 실제 휴대폰 화면 검증을 의미하지 않는다.

## 운영 적용

2026-09-13 **14:37:57 KST**에 `C:\Project_333\Builds\Server\Releases\20260913-pve-reconnect`를 실행했다. 배포 BuildId는 `20260913T053240770Z-7f539fe90be64aecbe02a4b2412657f2`이며 새 프로세스는 PID 6880이다. 이 상태는 아래 검증 시점의 기록이며 상시 가동을 보장하는 표현이 아니다. 서버 자동 실행 설정은 계속 보류한다.

- 배포 전 현재 세션 0개, 미처리 종료 결과 0개 확인.
- 14:36:25 KST `project333_before_pve_reconnect_20260913_143625.dump` 생성. 크기 2,865,979바이트, SHA256 `D36B17016253A974F632BBEA6E09AB8653D974515401C898BD2F87A0ED04133A`. 전체 아카이브 읽기 검증을 통과했으며 실제 DB 복원은 하지 않았다.
- 배포 후 로컬·공개 health/DB/status, 공개 WSS 연결·정상 종료·재연결 확인. 로컬·공개 `/battle/reconnect-status`와 `/pvp/reconnect-status`의 미인증 요청은 모두 401 `missing_session_token`이며, 폐쇄한 클라이언트 성적 업로드 경로는 계속 410이다.
- 재접속 유예 60초와 `InterruptedBattlePolicy=cancel_without_result` 유지 확인.
- 계정 43개, 런 93개, 누적 205승/245패 및 지갑·카드 컬렉션·런·거래·결과 영수증 해시가 배포 전후 동일하다. 적용된 스키마 마이그레이션은 16개로 유지했으며 새 스키마 변경은 없다.

새로운 앱 재실행 후 PvE 조회·버튼·기존 전투 ID 복원은 새 클라이언트 코드에 포함된다. 운영 서버 업데이트만으로 기존 설치 앱의 코드가 바뀌지는 않는다. 앱 빌드·설치는 사용자가 진행한다.

## 근거

- [최종 서버 배포 후보 검사 로그](../TempBuild/PveReconnectDeploy_20260913/release-checks.log)
- [Unity EditMode 46개 결과](../TempBuild/PveReconnect_20260913/UnityTests.xml)
- 격리 서버/DB 검사 보존 경로: `C:\Users\youndonghyeok\AppData\Local\Temp\after333-run-authority-check-6a6ee35ebb1f42e897069c3153e09f24`
- [서버 회귀 도구 범위와 실행법](../Server/Project333.RunAuthorityChecks/README.md)
- [전투 규칙](./battle_rules.md), [온라인 서버 API·구현 설명](./online_pvp_prototype.md)
- [운영 배포 검증](../TempBuild/PveReconnectDeploy_20260913/DeploymentResult.json), [실행 서버 식별](../TempBuild/PveReconnectDeploy_20260913/RunningServer.json)
- [재접속 API·정책 확인](../TempBuild/PveReconnectDeploy_20260913/ReconnectEndpointChecks.json), [DB 백업 검증](../TempBuild/PveReconnectDeploy_20260913/Backup.json), [DB 전후 보존 검사](../TempBuild/PveReconnectDeploy_20260913/InspectDatabase.json)
