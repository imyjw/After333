# After333 PostgreSQL 복원 절차

최종 검증: 2026-09-12. 운영 DB를 덮어쓰지 않고 기존 운영 백업을 별도 PostgreSQL 인스턴스에 실제 복원했다.

## 확인한 결과

- 대상: 2026-09-12 19:13:14 KST 배포 전 백업, 2,863,466바이트. SHA256 `D7B37607CED3BA8231542D59FE411254257FC5FFF6DA33BADE810EB20A76EC76`.
- PostgreSQL 18 / UTF8 / libc / `Korean_Korea.utf8` 정렬·문자 분류 / `Asia/Seoul` 시간대.
- 스키마 마이그레이션 16개, 계정 43개, 런 93개 복원. 지갑·카드 컬렉션·런·거래·구매/강화 영수증 해시가 백업 당시 기록과 일치.
- public 테이블 30개 및 인덱스·제약 조건 복원. 현재 배포 서버 시작 전후의 모든 테이블 해시가 일치.
- 복원 DB에서 별도 테스트 계정의 Game ID 가입·로그인·조회, 티켓 구매, 케르베로스·마나의 샘 강화, 처리 완료/취소 기록, 33장 드래프트 저장을 검증.
- 임시 PostgreSQL과 HTTP 서버를 모두 종료 후 재시작. 기존 세션 조회·덱·재화·카드 레벨이 유지되고 완료 요청 재전송은 중복 차감하지 않으며 취소 요청은 계속 거부.
- 클라이언트 승수 업로드는 HTTP 410, 미완료 런 보상 청구는 HTTP 400. 테스트 전후 복원된 원래 모든 행에 변경·삭제 없음.
- `pg_restore` 자체는 약 1.85초, 초기화·복원·검사·재시작·종료 전체는 약 20.6초. 이 작은 로컬 백업의 측정값이며 실제 장애 대응 시간 보장은 아니다.
- 운영 서버 PID 6652 유지, 로컬·공개 HTTPS·DB health 정상. 임시 15449/17349 포트 리스너 없음.

상세 근거: [검증 결과](../TempBuild/PostgresRestoreDrill_20260912/Result.json), [작업 기록](../TempBuild/PostgresRestoreDrill_20260912/README.md).

## 동일한 격리 훈련 재실행

[검증 도구](../Server/Project333.PvpServer/Tools/TestPostgresRestoreDrill.ps1)는 운영 DB 접속 인자를 받지 않는다. 사용자의 LocalAppData 아래 새 전용 디렉터리와 비밀번호 보호 임시 PostgreSQL을 생성하고, 127.0.0.1의 고정 테스트 포트 15449(DB)/17349(HTTP)만 사용한다. 이미 사용 중인 포트가 있으면 기존 프로세스를 건드리지 않고 중단한다. 새 클러스터 경로와 빈 DB 여부를 확인한 뒤 `pg_restore --single-transaction --exit-on-error --no-owner --no-privileges`로 복원한다.

아래 명령은 이번에 검증한 백업과 릴리스를 다시 사용하는 훈련이다. 운영 복원 명령이 아니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestPostgresRestoreDrill.ps1 `
  -BackupRecordDirectory C:\Project_333\TempBuild\CardUpgradeCorrections_20260912 `
  -ServerOutput C:\Project_333\Builds\Server\Releases\20260912-card-upgrade-corrections `
  -SourceDatabaseSettings C:\Project_333\TempBuild\PostgresRestoreDrill_20260912\SourceLocale.json
```

다른 백업은 해당 백업의 `Backup.json`(경로·해시), `BackupDatabase.json`(백업 시점 비교값), `InspectDatabase.sql`과 DB 설정 기록을 함께 준비한다. 비교값과 덤프가 같은 상태를 나타내야 한다. 서비스가 계속 데이터를 쓰는 중이라면 덤프와 비교 조회에 같은 스냅샷을 사용하거나, 쓰기를 멈춘 시점에 둘 다 기록해야 한다. 이번 백업은 이전 배포의 변경 전후 비교 기록을 사용했고 실제 복원값과 일치했다.

정렬·시간대가 다르면 데이터가 같아도 정렬 순서나 날짜 문자열이 달라져 기존 해시가 달라질 수 있다. 이번 훈련의 최초 C 로캘 시도는 카드 컬렉션 해시가 달랐으며, 원본 로캘을 맞추자 전체 비교가 통과했다. 실제 자료 차이를 무시하고 해시 검사만 생략해서는 안 된다.

원본 백업, 임시 DB 파일, 원래 행의 비교 사본과 상세 로그는 `%LOCALAPPDATA%\After333\Server` 아래 보관한다. 저장소에는 원본 계정 내용·토큰·비밀번호를 복사하지 않고 집계·해시·성공 여부만 남긴다. 훈련 종료 시 자신이 시작한 프로세스만 정지하고 임시 DB 파일은 조사용으로 보존한다.

## 실제 장애 시 복구 순서

1. 복구할 백업의 생성 시각·해시·읽기 가능 여부와 당시 서버 릴리스를 확인한다. 백업 이후의 변경은 해당 백업만으로 복원되지 않는다.
2. 운영 쓰기를 멈추는 시점과 영향을 정하고 현재 DB·서버 설정·배포본·전투 결과 저널을 별도로 보존한다. 복원 전에 현재 DB를 삭제하거나 덮어쓰지 않는다.
3. 같은 PostgreSQL 호환 버전·인코딩·정렬·시간대의 새 복구 DB에 복원한다. 임시 훈련의 슈퍼유저를 운영 계정으로 사용하지 않고, 운영 연결 역할과 필요한 소유권·권한을 별도로 구성한다. `--no-owner --no-privileges`는 원래 역할/권한까지 복구했다는 의미가 아니다.
4. 마이그레이션, 인덱스·제약 조건, 계정·재화·카드·런·거래·영수증을 백업 시점 기록과 대조한다. 격리 서버에서 주요 API를 확인한 뒤 사용한 테스트 계정이 들어 있지 않은 별도의 최종 복구본을 준비한다.
5. 승인된 전환 시점에 서버 DB 연결을 최종 복구본으로 변경하고 시작한다. health/health-db, 로그인·기록 조회, HTTPS/WSS 연결을 확인한다. 기존 DB와 배포본은 보존한다.
6. 전환 후 쓰기가 발생한 경우 단순히 이전 DB로 연결을 되돌리면 그 변경이 사라질 수 있으므로, 실패 상태를 보존하고 두 DB의 차이를 확인한 뒤 복구 방법을 정한다.

이번에는 5번의 운영 전환을 실행하지 않았다. 자동 실행·예약 백업도 설정하지 않았다.

## DB 덤프 밖의 복구 대상

- 배포 릴리스 및 manifest, 카드 JSON과 서버 설정, Google/광고 인증 설정, TLS·터널 설정은 별도 자산이다. 이 덤프만으로 새 PC의 전체 서비스를 재구축한 것은 아니다.
- 미반영 전투 결과의 파일 저널(`PROJECT333_BATTLE_RESULT_OUTBOX_DIR`)은 PostgreSQL 덤프에 들어 있지 않다. 복구 DB와 같은 시점의 저널 보존·재처리 정책을 확인해야 한다. 이번 임시 서버에는 새 빈 저널 디렉터리를 사용했다.
- 진행 중 전투의 실시간 상태·클라이언트 재접속 복구는 별도 검증 과제다. 이번 DB/HTTP 재시작 성공을 진행 중 PvP 전투 복구 성공으로 해석하지 않는다.
- 원격 호스트 복구, PostgreSQL 역할/권한 전체 복구, 백업 예약·별도 장소 보관·시점 복구(PITR)는 이번 검증 범위가 아니다.