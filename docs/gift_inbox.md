# After333 관리자 선물함

## 확정 동작

2026-09-13 사용자 요청 기준으로 게임 티켓과 서버 골드(Resource Gold)를 선물함으로 지급한다. 발송은 지갑을 바로 변경하지 않는다. 유저가 시작 화면 우상단의 **선물함**을 열고 각 선물의 **받기**를 누르면 서버가 수령을 확정한다.

- 전체 발송 대상은 **실제 발송 시점에 존재하는 계정**이다. 이후 가입 계정에는 예전 선물이 생기지 않는다. 삭제된 계정은 제외하며, 일시 비활성화/차단 계정도 기존 계정 목록에는 포함한다. 로그인·수령은 기존 계정 이용 제한을 따른다.
- 특정 계정 발송은 UUID 계정 ID로 지정한다. 같은 표시 이름이 있을 수 있으므로 이름 검색 결과에서 대상 ID를 확인한다.
- 기본 수령 기한은 없다. 발송할 때 선택적으로 만료 일시를 지정할 수 있다. 서버 DB 시각으로 만료를 판정한다.
- 티켓과 골드를 한 선물에 함께 넣을 수 있다. 수량은 음수가 아니며 둘 중 하나는 양수여야 한다.
- 제목은 80자, 본문은 1,000자, 관리자 사유는 300자까지의 일반 텍스트다. 사유는 관리자 기록에만 남고 유저에게 노출하지 않는다.
- 같은 선물은 계정당 한 번만 수령한다. 수령 상태, 지갑 증가, 거래 내역을 하나의 트랜잭션으로 저장한다.
- 통신 오류가 난 수령 요청은 동일 선물 ID로 자동 재시도한다. 앱을 재실행하면 서버 목록을 다시 읽어 수령 완료/미수령/만료 상태를 표시한다.
- 미수령 개수 배지, 개별 받기, 기간 만료 표시, 이전 목록 더 보기를 제공한다. 수령 완료 선물은 즉시 목록에서 사라지며 재조회·재실행 후에도 표시하지 않는다. 서버의 수령·거래 기록은 중복 지급 방지를 위해 유지한다. 이번 범위에는 카드·팩 첨부, 모두 받기, 발송 후 수정·회수는 포함하지 않는다.

## 관리자 도구

서버 PC에서 PowerShell을 열어 실행한다. 공개 관리자 API는 없고, 도구가 해당 Windows 사용자의 암호화된 서버 DB 설정으로 로컬 PostgreSQL에 접근한다. 비밀 키나 DB 비밀번호를 앱에 넣지 않는다. 서버를 중지할 필요는 없다.

### 처음 실행할 때

같은 PowerShell 창에서 아래 명령을 순서대로 실행한다.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
Set-Location C:\Project_333
$giftTool = 'C:\Project_333\Server\Project333.PvpServer\Tools\ManageGifts.ps1'
```

- `Set-ExecutionPolicy`: 현재 PowerShell 창에서 스크립트 실행을 허용한다. `-Scope Process`는 현재 세션에만 적용하며 창을 닫으면 적용이 끝난다. 컴퓨터 전체의 영구 설정은 변경하지 않는다. `-ExecutionPolicy Bypass`는 이 세션의 실행 정책 차단을 해제하고, `-Force`는 확인 질문을 생략한다.
- `Set-Location`: 프로젝트 폴더로 이동한다.
- `$giftTool`: 관리자 도구 경로를 저장한다. 이후 `& $giftTool`로 실행한다.

PowerShell 창을 새로 열었으면 위 준비 명령부터 다시 실행한다.

### 스크립트 실행이 차단될 때

`running scripts is disabled on this system`, `PSSecurityException`, `UnauthorizedAccess` 오류가 표시되면 현재 창에서 아래 명령을 실행한 뒤 계정 검색을 다시 시도한다. `$giftTool`은 위 준비 단계에서 설정한 값을 사용한다.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
& $giftTool -Mode Accounts -Search 'jaewon youn'
```

### 계정 찾기

```powershell
& $giftTool -Mode Accounts -Search '표시 이름'
```

`AccountId`와 `DisplayName`을 확인한다. 검색어를 생략하면 최대 100개를 표시한다. 표시 이름이나 계정 ID 일부로 검색할 수 있다.

### 특정 계정에 보낼 선물 준비

아래 ID는 검색 결과의 실제 계정 ID로 바꾼다. **Prepare는 미리보기와 요청 파일 생성만 하며 발송하지 않는다.**

```powershell
& $giftTool -Mode Prepare `
  -AccountIds '계정 UUID' `
  -Title '테스트 참여 선물' `
  -Message '테스트에 참여해주셔서 감사합니다.' `
  -Tickets 3 `
  -ResourceGold 100 `
  -Reason '비공개 테스트 참여 지급'
```

`RequestFile`에 발송 요청 파일 경로가 출력된다. 기본 위치는 `%LOCALAPPDATA%\After333\Admin\GiftRequests`다. 수신 계정과 첨부 수량도 함께 출력된다. 골드만/티켓만 지급할 때는 다른 수량을 생략한다.

### 전체 계정에 보낼 선물 준비

```powershell
& $giftTool -Mode Prepare `
  -AllUsers `
  -Title '점검 보상' `
  -Message '기다려주셔서 감사합니다.' `
  -Tickets 1 `
  -ResourceGold 30 `
  -Reason '서버 점검 보상'
```

전체 대상 수는 Prepare와 Send 사이에 가입/삭제가 발생하면 달라질 수 있다. 최종 수신자 명단은 Send가 성공할 때 한 번만 확정한다.

### 기한 지정

Prepare 명령에 아래 옵션을 추가한다. 생략하면 무기한이다. 예시는 한국 시각 2026년 10월 1일 23시 59분이다.

```powershell
-ExpiresAt '2026-10-01T23:59:00+09:00'
```

### 내용 확인 및 실제 발송

앞서 출력된 RequestFile을 사용한다.

```powershell
$requestFile = '출력된 요청 파일의 전체 경로'
& $giftTool -Mode Preview -RequestFile $requestFile
& $giftTool -Mode Send -RequestFile $requestFile
```

**Send가 실제 선물함 발송이다.** `GiftId`, `RecipientCount`, `AlreadySent`가 출력된다. `AlreadySent=true`면 이전 발송을 재확인한 것으로 추가 선물은 생기지 않는다. 대상 유저는 시작 화면의 선물함에서 수령한다.

응답 오류나 창 종료로 발송 성공 여부가 불확실하면 **같은 요청 파일로 Send를 다시 실행**한다. 새 Prepare는 새 발송 ID를 만들므로, 같은 선물을 재시도할 때 새 요청을 만들지 않는다. 같은 ID로 내용을 바꾸면 서버가 거부한다. 이미 준비한 파일을 덮어쓰지 않으며 준비한 DB와 다른 DB에서는 발송할 수 없다.

도구는 실행 시 최신 로컬 소스로 관리 프로그램을 컴파일한다. 서버를 재시작하거나 앱을 빌드하지 않는다. 컴파일 로그는 `%LOCALAPPDATA%\After333\Admin\gift-admin-build.log`에 저장된다.

## 구현 경계

- 관리자: `Server/Project333.GiftAdmin`, `Tools/ManageGifts.ps1`
- 서비스/DTO: `Server/Project333.PvpServer/Gifts`
- DB: `0017_gift_inbox.sql`의 `gift_campaigns`, `gift_inbox`; `account_transactions`의 `admin_adjustment` 기록
- 유저 API: 인증된 `GET /gifts`, `POST /gifts/{giftId}/claim`만 노출한다. 수량·수신 계정은 클라이언트 요청으로 결정하지 않는다.
- 클라이언트: `GiftInboxClient`, `GiftInboxController`, 시작 화면 연결 및 계정/세션/서버 변경 시 오래된 응답 무효화
- Editor: `Tools > Project333 > Start > Install Gift Inbox`로 시작 씬 구조를 설치할 수 있다. 기존 미저장 씬 편집은 자동 저장하지 않는다.
- 시작 화면 선물함은 기존 재화 영역과 설정 톱니바퀴 사이에 배치한다. 새 앱 빌드는 사용자가 진행한다.
## 2026-09-13 적용·검증 결과

- 서버: 15:19:41 KST에 `20260913-gift-inbox` 릴리스로 운영 적용했다. DB 마이그레이션은 16개에서 17개가 됐다.
- 배포 전 15:18:54 KST에 운영 DB 백업을 만들고 아카이브 전체 읽기를 검증했다. 실제 DB 복원은 하지 않았다. 계정·지갑·카드·런·거래·결과 영수증 해시는 배포 전후 같았다.
- 공개 HTTPS/WSS, DB 연결, 미인증 선물 조회/수령 401, 외부 관리자 발송 경로 부재 404를 확인했다. 운영 선물·수신함·선물 지급 거래는 모두 0건이며 실제 계정에 선물을 발송하지 않았다.
- 격리 서버 회귀 검사 106개 그룹 통과. 동시 발송/수령, 계정별 한 번 수령, 전체 계정 대상 확정, 만료, 타인 접근, 금액 초과와 DB 실패 롤백, 서버 재시작 이후 보존 및 재시도를 검증했다.
- Unity EditMode 관련 검사 70개 통과(선물함 27개 포함). 실제 API가 사용하는 수령 응답 파서에서 camelCase/PascalCase, 정상 0 잔액, 지갑 필드 누락/불완전 응답을 검증했다. 계정·세션 변경 시 오래된 응답 적용 차단과 기존 재화 표시·광고·재접속 검사도 포함했다.
- Windows PowerShell 5.1에서 관리자 도구의 읽기 전용 계정 검색, 한글 입력, 다른 작업 폴더 실행과 기존 환경변수 복원을 확인했다.
- 시작 씬에 실제 선물함 구조를 저장했고, 격리 Unity Editor에서 샘플 화면을 렌더링해 재화·선물함·톱니바퀴 배치와 목록을 확인했다. 샘플 선물은 서버에 저장하지 않았다.
- **앱 빌드·설치 및 실기기 선물 수령은 아직 하지 않았다. 새 앱은 사용자가 빌드·설치한다.**

검증 기록: `TempBuild/GiftInbox_20260913/server-checks-verified.log`, `UnityTests-final.xml`, `DeploymentResult.json`, `GiftEndpointChecks.json`, `GiftDatabaseCheck.json`.

[시작 화면 미리보기](../TempBuild/GiftInbox_20260913/start-screen.png) · [선물함 미리보기](../TempBuild/GiftInbox_20260913/gift-inbox.png)
## 2026-09-13 선물 개수만 표시되고 목록이 사라지는 문제 수정

사용자가 미수령 1개 문구는 보이지만 선물 항목은 없고 새로고침을 눌러도 반응이 보이지 않는다고 보고했다. 버튼 자체의 비활성화 여부는 사용자 관측으로 확정하지 않는다.

실행 중 목록 갱신의 try와 finally에서 같은 프레임에 BuildRows가 두 번 호출될 때, ClearRows의 Destroy는 프레임 끝까지 지연된다. 이름으로 기존 행을 찾는 코드가 삭제 예정인 비활성 행을 재사용하여 레이아웃 컴포넌트 추가 오류와 NullReferenceException을 발생시켰다. 그 결과 목록 표시와 후속 헤더/버튼 갱신이 중단됐다. 삭제 전에 이전 행을 Content에서 즉시 분리하도록 수정했다. 기존 Editor 미리보기는 DestroyImmediate를 사용하므로 이 실행 중 문제를 잡지 못했다.

빈 격리 씬에서 실제 Play Mode로 진입하는 회귀 검사를 추가했다. 수정 전 같은 프레임 연속 갱신에서 NullReferenceException으로 실패했고, 수정 후 프레임 종료 뒤 미수령 행·받기 버튼 유지와 수령 완료 상태의 연속 갱신이 통과했다. 관련 Unity 검사 총 71개 통과. 서버 접속·실제 계정 수령·재발송·앱 빌드·설치는 하지 않았다. 클라이언트 소스 수정이므로 사용자가 새 앱을 빌드·설치해야 하며 실기기 확인은 남아 있다.

증거: `TempBuild/GiftInboxRefresh_20260913/before.xml`, `after.xml`, `before.log`, `after.log`.