# After333 MYBOX DB 백업 — 2026-09-13

## 설정 기록과 현재 확인 범위

예약 작업 등록·실행 확인은 2026-09-13, 마지막 기록된 실행과 독립 웹 다운로드/격리 복원은 2026-09-21이다. 아래는 그때 확인한 설정과 증거다. 2026-10-01 문서 동기화에서는 예약 작업·MYBOX·DB·복구 키·운영 프로세스를 조회하거나 실행하지 않았으므로 지금의 업로드/가동 성공을 새로 보장하지 않는다. 게임 서버 자동 시작은 별도이며 아직 설정 범위에 포함하지 않는다.

## 등록된 설정

- 사용자 지정 저장소: NAVER MYBOX, `N:\개인\After333-Backups`.
- 사용자 지정 주기: 매일 21:00, Windows 작업 `After333-Encrypted-DB-Backup`.
- 현재 사용자 로그인 상태에서만 실행. PC가 꺼져 예약을 놓치면 다음 실행 가능한 시점에 시도(StartWhenAvailable). MYBOX 연결·PostgreSQL 가동 필요. 게임 서버 프로세스 자체가 켜져 있을 필요는 없다.
- 실패 시 15분 간격 최대 3회 재시도, 한 실행 최대 30분, 중복 실행 방지.
- 게임 서버 자동 실행은 설정하지 않았다. 기존 백업 자동 삭제나 보관 기한도 설정하지 않았다.

## 백업 내용과 처리

운영 PostgreSQL `project333`의 pg_dump custom-format 백업을 만든다. 서버 설정·Google/LevelPlay 비밀 키·터널 설정·DB 밖 미처리 전투 결과 저널은 포함하지 않는다. 덤프만으로 다른 PC 전체 서버 설정을 복구할 수 있는 것은 아니다.

1. 기존 Windows 사용자 암호화 설정에서 DB 연결 정보만 읽는다. 값은 출력하지 않는다.
2. pg_dump로 일관된 DB 스냅샷을 백업한다. DB를 변경하거나 운영 서버를 중지하지 않는다.
3. pg_restore로 아카이브 전체를 SQL 출력 NUL로 읽어 손상 여부를 확인한다. 이 단계는 실제 DB 복원이 아니다.
4. 공식 age 1.3.2 도구로 공개 키 암호화한다.
5. 로컬 복호화 결과 SHA256이 원본 덤프와 같은지 확인한다.
6. 암호화 파일과 비민감 검증 JSON만 MYBOX에 복사하고 N:에서 읽은 해시를 비교한다.
7. 이 실행에서 만든 평문 덤프·복호화 임시 파일은 제거한다. 기존 백업은 삭제하지 않는다.

MYBOX 가상 드라이브는 캐시를 사용할 수 있으므로 N: 읽기 성공만으로 원격 저장 완료를 단정하지 않는다. 결과의 CloudServerConfirmation=NotChecked는 이를 명시한다. 별도 웹/모바일에서 확인한 사실은 이 문서에 구분해 기록한다.

## 검증 결과

- 최초 준비 중 잘못된 루트 `N:\After333-Backups`에는 MYBOX가 생성을 거부했다. 사용자가 만든 개인 폴더 아래 경로로 수정했다.
- 13:22 KST: `After333-DB-20260913-042208-8ea6dcb4.dump.age`, 2,867,176 bytes 생성·암호화·복호화·N: 읽기 해시 검사 통과. 사용자가 MYBOX 웹/휴대폰에서도 해당 파일이 보인다고 확인했다. 독립 다운로드를 다시 복원한 실적은 아니다.
- 등록된 예약 작업의 검증 실행에서 `After333-DB-20260913-042307-6118ccbe.dump.age` 추가 생성. 작업 종료 결과 0, 로컬 최신 결과 CopiedToMyboxDrive, 전체 아카이브 읽기·복호화 해시·N: 읽기 검사 통과. 다음 예약 2026-09-13 21:00.
- 잘못된 대상 폴더·잘못된 공개 키 설정에서 DB 접근 전 중단, 비정상 종료 코드와 실패 기록 확인.
- 별도 복사본의 암호문 손상과 잘못된 비밀 키 복호화가 거부됨을 확인.
- 기존 별도 DB 실제 복원 훈련은 9월 12일 기록을 참조한다. 이번 새 암호화 백업으로 DB 실제 복원을 다시 실행한 것은 아니다.

## 파일 위치

- 소스: `Server/Project333.PvpServer/Tools/BackupPostgresToMybox.ps1`.
- 예약 실행본: `%LOCALAPPDATA%\After333\Server\CloudBackup\BackupPostgresToMybox.ps1`.
- 설정: 같은 폴더의 `settings.json` (공개 암호화 키와 경로만 포함).
- 로그·암호화 로컬 사본: 같은 폴더의 `Runs`.
- 마지막 결과: 같은 폴더의 `latest-result.json`.
- 암호화 도구: `%LOCALAPPDATA%\After333\Tools\age-v1.3.2\age`.

예약 실행본은 소스의 복사본이다. 소스를 수정하면 검증 후 예약 실행본도 갱신해야 한다. GitHub push만으로 예약 실행본이 자동 갱신되지는 않는다.

공식 Windows ZIP의 SHA256: `F48D8F8F9EBE903AB5027ED067652F2CC1DB94BC206976430133B905DCD8E8C7`, GitHub 공식 release asset digest와 비교했다. [age 공식 사용법](https://github.com/FiloSottile/age).

## 복구 키의 별도 보관 — 사용자 확인 완료

복구 키 파일은 `%LOCALAPPDATA%\After333\Server\CloudBackup\After333-Recovery-Key.txt`다. 해당 로컬 폴더는 현재 Windows 사용자와 SYSTEM만 접근하도록 ACL을 설정했다. 도구의 Windows world-readable 경고와 별개로 실제 ACL을 확인했다. 키 내용은 채팅·로그·GitHub·MYBOX에 출력하거나 업로드하지 않았다.

**2026-09-13 사용자가 복구 키를 PC 밖에 보관했다고 확인했다.** 외부 보관 장소나 키 내용은 요청·수집하지 않았다. 외부 보관본을 이용한 독립 복구 검사는 아직 하지 않았다. PC와 키를 함께 잃으면 암호화 백업을 복호화할 수 없으므로 별도 보관을 유지한다.

## 수동 실행과 상태 확인

```powershell
Start-ScheduledTask -TaskName 'After333-Encrypted-DB-Backup'
Get-ScheduledTaskInfo -TaskName 'After333-Encrypted-DB-Backup'
Get-Content "$env:LOCALAPPDATA\After333\Server\CloudBackup\latest-result.json"
```

작업 결과 0과 latest-result의 최근 시각/Status를 함께 확인한다. N: 복사는 실제 MYBOX 업로드 완료를 독립 확인한 상태와 다르다. 자동 실패 알림 전송은 설정하지 않았다.

## 다른 PC에서 복구할 때

MYBOX에서 .dump.age와 해당 .json을 내려받고, 별도로 보관한 복구 키를 가져온다. 공식 age 도구로 복호화한 후 JSON의 PlaintextSha256과 비교한다. 예시의 경로는 복구용 새 폴더의 실제 경로로 바꾼다. 기존 덤프를 덮어쓰지 않는다.

```powershell
age --decrypt --identity "D:\Recovery\After333-Recovery-Key.txt" --output "D:\Recovery\project333-restored.dump" "D:\Recovery\After333-DB-<실행 ID>.dump.age"
Get-FileHash "D:\Recovery\project333-restored.dump" -Algorithm SHA256
```

그 뒤 [PostgreSQL 복원 절차](./postgres_restore_runbook.md)에 따라 새 복구 DB에서 검증한다. 운영 DB에 바로 덮어쓰지 않는다. 이전 PC에 묶인 DeploymentSettings clixml은 다른 PC용 인증 설정 백업을 대신하지 않는다.

## 2026-09-21 후속 확인 — 독립 다운로드 복원 준비

- Windows 예약 작업의 2026-09-21 21:00 KST 실행 결과는 0이다. 다음 예약은 2026-09-22 21:00 KST다.
- 최신 기록: `After333-DB-20260921-120001-c36c8c84.dump.age`, 3,359,048 bytes. 암호화·복호화 해시 및 N: 읽기 검증 성공, 상태 `CopiedToMyboxDrive`.
- 이 결과의 `CloudServerConfirmation`은 여전히 `NotChecked`다. N: 파일 존재나 작업 성공만으로 클라우드에서 독립 다운로드한 것으로 해석하지 않는다.
- 웹 MYBOX는 로그아웃 상태여서 로그인 화면을 사용자에게 열었다. 독립 다운로드, 새 암호화 백업의 실제 DB 복원은 아직 실행하지 않았다.
- 기존 `TestPostgresRestoreDrill.ps1`은 배포 백업 당시의 행 수·해시 비교 기록을 요구한다. MYBOX JSON에는 그 비교 기록이 없으므로 다른 날짜의 값을 재사용하거나 복원 후 값을 '백업 시점 기준값'이라고 만들지 않는다.
- 독립 다운로드 후에는 암호문/평문 해시, 빈 격리 DB 복원, 스키마·제약 조건·기록 조회를 검증하고, 별도 백업 시점 대조가 없는 범위를 구분해 보고한다. 운영 DB 복원·교체는 이 준비 작업에서 수행하지 않았다.

## 2026-09-21 독립 웹 다운로드 → 격리 DB 복원 완료

- 사용자가 MYBOX 웹에서 오늘 21:00 백업과 동명 JSON을 다운로드 폴더로 직접 내려받았다. 브라우저 도구의 자동 다운로드 오류 때문에 수동 다운로드를 사용했으며, N: 캐시나 로컬 암호화 사본으로 대체하지 않았다.
- 대상: `After333-DB-20260921-120001-c36c8c84.dump.age`, 3,359,048 bytes. 내려받은 암호문 SHA256과 JSON·로컬 생성 기록이 일치했고, 로컬 복구 키로 복호화한 평문 SHA256도 기록과 일치했다.
- 새 [격리 복원 도구](../Server/Project333.PvpServer/Tools/TestMyboxPostgresRestore.ps1)를 사용했다. 기존 복원 훈련의 백업 시점 비교값은 재사용하지 않았다.
- PostgreSQL 18, 별도 신규 클러스터, 127.0.0.1:15449, 무작위 SCRAM 비밀번호, 현재 사용자와 SYSTEM만 접근 가능한 디렉터리에서 실제 복원했다. 운영 DB 연결 설정은 사용하지 않았다.
- 34개 테이블 전체 조회, 유효하지 않은 제약 조건/인덱스 0개. 계정 43, 지갑 43, 런 94, 덱 91, 마이그레이션 19개를 확인했다. 이 숫자는 복원한 백업의 집계이며 현재 운영 DB와 대조한 결과는 아니다.
- 임시 PostgreSQL을 종료·재시작한 후 스키마·모든 테이블의 행 수/해시·시퀀스 상태가 정확히 일치했다. 복원 자체 2.18초, 성공한 검증 실행 전체 약 13.21초. 실제 장애 복구 시간 보장은 아니다.
- 운영 PostgreSQL PID 7096 및 게임 서버 PID 12640 유지, 게임 서버 health 정상. 임시 15449 리스너 종료, 평문 덤프와 임시 비밀번호 파일 제거 확인. 운영 DB 복원·교체는 하지 않았다.
- 첫 실행에서 pg_ctl의 백그라운드 프로세스가 출력 파이프를 유지해 도구가 대기했다. 출력을 전용 파일로 직접 기록하도록 수정한 뒤 새 클러스터로 재실행해 성공했다. 첫 클러스터도 수동 정상 종료 및 평문 정리를 확인했고 실패 기록은 보존했다.
- 잘못된 암호문 해시는 복호화·DB 생성 전에 거부됨을 별도 확인했다.

범위: 웹에서 내려받은 백업의 무결성·실제 DB 복원·DB 재시작 보존을 확인했다. 백업 생성 시점의 별도 행별 비교 기준은 없으며, 로캘은 9월 12일 기록을 사용했다. PC 밖 복구 키 사본, 게임 서버/API를 복구 DB에 연결하는 검사, 다른 PC의 전체 서버·인증 설정·DB 외 결과 저널 복구는 이번에 검증하지 않았다.

근거: [복원 결과](../TempBuild/MyboxRestoreDrill_20260921/Result.json), [최종 확인](../TempBuild/MyboxRestoreDrill_20260921/FinalVerification.json), [실행 로그](../TempBuild/MyboxRestoreDrill_20260921/restore-verified.log). 개인정보가 들어 있는 DB·SQL·행 해시는 저장소 밖의 접근 제한된 실행 폴더에 보존했다.

재실행 예시(운영 복원 명령이 아님):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestMyboxPostgresRestore.ps1 `
  -EncryptedBackupPath "$env:USERPROFILE\Downloads\After333-DB-20260921-120001-c36c8c84.dump.age" `
  -MetadataPath "$env:USERPROFILE\Downloads\After333-DB-20260921-120001-c36c8c84.dump.age.json" `
  -IdentityPath "$env:LOCALAPPDATA\After333\Server\CloudBackup\After333-Recovery-Key.txt" `
  -SourceDatabaseSettings C:\Project_333\TempBuild\PostgresRestoreDrill_20260912\SourceLocale.json
```
