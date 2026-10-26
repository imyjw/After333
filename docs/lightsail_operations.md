# Lightsail 운영과 백업

정리2026-10-20. 10월15–19일 사용자가 실행해 제공한 결과를 정리한 운영 안내다. 오늘 원격 서버·DNS·DB·예약 작업을 변경하거나 재시험하지 않았다. [현재 확인 범위](current_project_status.md)를 함께 본다.

## 연결 구조

```text
클라이언트 HTTPS/WSS → api.after333.com (DNS only)
  → 서울 Lightsail의 Caddy :443 (HTTP :80은 인증/리다이렉트)
  → ASP.NET Core8 http://127.0.0.1:7333
  → PostgreSQL18 localhost:5432 / project333_live
```

Cloudflare Tunnel을 실행하던 Windows PowerShell은 이전 운영 방식이다. Cloudflare에서 DNS를 관리하는 것과 HTTP 트래픽이 Cloudflare 프록시를 지나는 것은 별개다. 운영 DNS에 프록시를 다시 켜거나 이전 터널을 켜면 현재 검증한 직접 경로와 달라진다. 시험용 `api-staging.after333.com`을 운영URL 대신 쓰지 않는다.

## 서버 시작·종료·재시작

Lightsail의 Ubuntu SSH 터미널에서 실행한다. Windows PowerShell의 서버 실행 스크립트를 그대로 붙여 넣지 않는다.

```bash
# 시작
sudo systemctl start after333.service
# 종료
sudo systemctl stop after333.service
# 재시작 — 진행 중 전투에 영향을 주므로 점검 때 수행
sudo systemctl restart after333.service
# 읽기 전용 상태 확인
systemctl is-active after333.service
systemctl is-active caddy.service
```

확인한 배치에서 unit은 `/etc/systemd/system/after333.service`, 운영 drop-in은 `/etc/systemd/system/after333.service.d/99-after333-production.conf`, 운영 환경 파일은 `/etc/after333/server.production.env`다. Windows에서 매번 입력하던 DB/Google/LevelPlay 값은 이 보호된 서버 환경 파일을 systemd가 읽어 전달한다. root 소유0600을 유지하고 내용을 채팅·Git·일반 로그로 출력하지 않는다. `/etc/after333/server.env`는 이전 시험 설정이며 운영 파일과 혼동하지 않는다.

unit/drop-in을 수정한 경우 `sudo systemctl daemon-reload` 후 재시작한다. 환경 파일의 값만 바꾸면 재시작으로 새 프로세스에 반영된다. 서버 실행 파일/카드 JSON을 로컬에서만 바꿔도 서버의 현재 릴리스가 자동 갱신되는 것은 아니다. 기존 릴리스 게시·업로드·검증 절차를 따른다.

`Restart=no`, static/수동 시작을 유지한다. 인스턴스 재부팅 후 서버 자동 시작이나 장애 자동 재시작은 설정하지 않았다. 이번 문서 작업은 `enable`, 자동 재시작 정책 변경 또는 예약 작업 설치를 수행하지 않는다.

## 공개 엔드포인트 점검

Windows에서 프로젝트의 기존 도구로 HTTPS·상태·WebSocket을 검사할 수 있다.

```powershell
& 'C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1' `
  -BaseUrl 'https://api.after333.com' `
  -ExpectedRequiredClientVersion '0.1.0-dev' `
  -TimeoutSeconds 15
```

`0.1.0-dev`는 이전 시점에 확인한 요구 버전이다. 향후 운영 호환 정책을 변경하면 그 값으로 검사한다. 운영DB/migration 정보의 전체 원문을 공개하지 말고 도구의 안전한 요약을 사용한다. HTTPS 실패 시 인증서 오류를 무시하거나 운영 도메인을 staging으로 바꿔 우회하지 않는다. Caddy/서버 상태와 DNS·인증서 발급 상태를 구분해 확인한다.

방화벽은80/443 공개,22는 본인 공인IP/32와 필요한 Lightsail 브라우저SSH 허용을 사용했다.7333과5432에 외부 허용 규칙을 만들지 않는다. 공인IP가 바뀌면 SSH 백업도 시간 초과할 수 있다.

## 백업 확인

Windows 작업 이름은 `After333-Encrypted-DB-Backup`, 시간은 매일21시KST다.

```powershell
Get-ScheduledTaskInfo -TaskName 'After333-Encrypted-DB-Backup' |
  Select-Object LastRunTime, NextRunTime, LastTaskResult
```

종료 코드0만 보지 않고 최근 실행 시각과 새 백업 RunId·완료 시각·`CopiedToMyboxDrive`·검증 플래그를 함께 본다. 기존 공개 도구 `GetLightsailScheduledBackupStatus.ps1`은 이 안전한 요약을 제공한다. 이 도구와 이전/복원 도구는 기존 세션 시각화의 `LightsailMigration_20261013`에 있고 Git 저장소의 Tools에 설치된 것으로 가정하지 않는다. 예약 실행용 보호된 패키지는 `%LOCALAPPDATA%\After333\Server\CloudBackup\Lightsail` 아래에 있다. 로컬 settings/작업XML/비밀키 내용을 공유하지 않는다.

실제 예약 관측 중에는 작업 수동 실행으로 기존 결과를 덮어쓰지 않는다. 수동 새 백업이 필요할 때만 다음 명령을 사용한다.

```powershell
Start-ScheduledTask -TaskName 'After333-Encrypted-DB-Backup'
```

노트북이 꺼져 있으면 그 순간 백업하지 못한다. `StartWhenAvailable` 설정은 다음 실행 가능한 시점의 보충 시도이며, 로그인·MYBOX 연결·SSH/IP 허용·네트워크 조건이 여전히 필요하다. 지연/재시도/실패 가능성을 구분한다. 정해진21시 실행은2026-10-19에 확인했지만 전원 종료 후 보충 실행은 별도로 실측하지 않았다.

DB 일일 백업과 서버 외부 파일 수동 백업은 별개다.57개 파일의 기존 암호화 백업·웹 독립 다운로드 확인은 전체 호스트 복원이나 TLS/역할 비밀번호 백업을 뜻하지 않는다. 키를 덤프와 함께 공유하지 않고 PC 밖 보관을 유지한다. [MYBOX 상세](mybox_database_backup.md), [격리 복원 확인](postgres_restore_runbook.md).

## 배포·장애·원복

유효한 최신 백업과 릴리스·outbox를 보존한 후 변경한다. 서버 중단으로 미완료 전투는 무승패 취소하며, 이미 확정된 결과는 내구성 저널에서 멱등 반영된다. 예전 노트북 서버와 Lightsail이 각자 다른 DB로 동시에 쓰지 않도록 한다. 전환 뒤 새 데이터가 생긴 상태에서는 DNS만 옛 노트북으로 되돌리지 않는다. 최신DB와 outbox를 보존·검증해 역이관하는 별도 복구 절차가 필요하다. 오늘 이 절차를 실행한 것은 아니다.
