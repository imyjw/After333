# 제한시간 만료 후 턴 상태 전달 지연

2026-10-25 소스 수정. 사용자 증상은 제한시간 종료 후 ‘내 턴 진행 중/처리 중...’ 안내가 1초에서 10초 이상 남는 경우다. 사용자는 사람 PvP/AI 중 어느 쪽인지 확실하지 않다고 답했다.

## 확인한 원인과 수정

- 기존 `ResolveTurnTimeoutAsync`는 공유 전투 판정으로 이미 다음 턴을 만든 뒤에도 감사 로그를 기다리고, 이벤트 전송·DB 스냅샷·결과 처리를 기다린 다음 StateView를 보냈다. 파일 쓰기 잠금이나 DB 지연이 다음 턴 통지를 막을 수 있었다.
- 기존 broadcast는 두 접속자에게 순서대로 전송했다. 소켓 전송은 5초 제한(전송 잠금 대기 포함)이 있으므로 한쪽의 느린 이벤트/상태 전송이 다른 정상 접속자의 상태를 늦출 수 있었다.
- 계속 진행되는 전투의 자동 턴 종료는 **다음 타이머 예약 → 접속자별 이벤트·전용 StateView 전송 → 감사 로그·스냅샷·결과 처리 → 필요한 AI 진행**으로 바꿨다. 각 기기 안에서는 이벤트→상태 순서를 지키되, 서로 다른 기기의 전송은 병렬 처리한다. StateView는 기존 소켓별 send gate 안에서 생성해 비공개 손패/트랩 필터링과 전투 기록 커서 순서를 보존한다.
- 기록은 생략하거나 fire-and-forget으로 바꾸지 않고 계속 await한다. 턴 만료 효과로 전투 자체가 끝난 경우에는 기존처럼 최종 StateView보다 **결과 저장을 먼저** 완료한다. 명령 검증, 중복 만료 방지, 기존 73초 deadline/턴 배너/멀리건 시간, 카드 효과, DTO와 클라이언트는 바꾸지 않았다.

실제 해당 전투에서 파일·DB·네트워크·연출 지연이 각각 몇 초였는지는 운영 로그 없이 확정할 수 없다. 이번 수정은 재현한 두 대기 경로를 제거한 것이며 실기기/운영 통신 지연의 상한을 보장하는 성능 측정은 아니다.

## 위치와 검사

- 서버: `Server/Project333.PvpServer/Program.cs`의 `ResolveTurnTimeoutAsync`, `BroadcastTurnTransitionAsync`.
- 검사: `Server/Project333.AiChecks/TurnTimeoutDeliveryChecks.cs`, CLI `--turn-timeout-delivery-only`.
- 검사 코드는 실제 서버 assembly의 만료 처리 함수를 호출하고 제어 가능한 WebSocket과 감사 로그 잠금을 사용한다. DB·실제 네트워크·계정·HTTP listener는 쓰지 않는다. 실제 10초를 자는 대신 해제되지 않은 Task를 유지한 상태에서 정상 접속자의 이벤트·상태가 먼저 도착하는지 검사한다.
- 수정 전12개 중6개 실패: 양쪽 선공 각각 감사 로그 차단·이벤트 전송 차단·상태 전송 차단에서 정상 기기가 상태를 받지 못했다. 기본 처리·전송 실패·멀리건은 통과했다.
- 최종 **16/16 통과**: 위 조건 외 AI 단일 접속자와 턴 시작 덱 소진 피해로 전투가 끝나는 경우를 양쪽 선공에서 추가. 정상 기기의 전용 손패/상대 비공개, 새 active/turn/deadline, 과거 콜백 무효화, 종료 상태 저장 대기 순서를 검사했다. 기존 OpeningGoldChecks 및 ActionDebuffOnlineChecks도 통과했다. 서버/검사 빌드 오류0, 기존 공유 소스 nullable 경고는 유지했다.

```powershell
Set-Location 'C:\Project_333'
dotnet build .\Server\Project333.AiChecks\Project333.AiChecks.csproj -c Release -o .\TempBuild\TurnTimeoutChecks
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet .\TempBuild\TurnTimeoutChecks\Project333.AiChecks.dll --turn-timeout-delivery-only
```

## 반영과 남은 확인

서버 재배포·재시작 후 운영에 반영된다. 이 수정만을 위해 클라이언트 빌드나 DB 마이그레이션은 필요 없다. 이번 작업에서 운영 서버 접속/배포·DB 작업·사용자 앱 빌드/설치·Unity Editor 조작은 하지 않았다. 재배포 후 기존 앱으로 사람 PvP 양쪽 좌석과 AI 전투에서 무행동 시간 만료를 확인하고, 턴 종료 효과가 있는 경우에도 정상 연출과 실제 차례를 함께 확인한다.

수정 전 재현 검사에서 실패를 처리하지 않은 .NET 예외로 종료해 Windows `dotnet.exe` 오류창이 떴다. 해당 격리 검사 프로세스만 실행 경로로 확인해 종료했다. 이후 검사는 실패 시 메시지와 종료 코드1을 반환하도록 바꿔 같은 원인의 오류창을 방지했다. 게임/운영 서버의 크래시가 아니며 재현 검사는 실제 DB나 계정에 접근하지 않았다.
