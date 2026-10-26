# Codex · Google Antigravity 병행 개발

작성: 2026-10-24. 공통 규칙은 [AGENTS.md](../AGENTS.md), 현재 작업표는 [PROGRESS.md](../PROGRESS.md). 이 안내를 만들면서 앱·서버·Git 커밋·설치된 에이전트 설정을 변경하지 않았다.

## 두 도구에서 같은 컨텍스트 읽기

1. 두 도구 모두 `C:/Project_333` 또는 그 저장소의 작업용 checkout을 연다. 예전 `Documents/New project`를 개발 루트로 사용하지 않는다.
2. 새 대화 첫 요청에 아래 문장을 붙인다. AGENTS 자동 탐색과 별개로 실제 읽은 기준을 확인할 수 있다.

   > 루트 AGENTS.md와 PROGRESS.md를 먼저 읽고 현재 branch/HEAD 및 git status를 확인해. 다른 작업자의 변경을 보존하고, 이번 작업 파일 범위를 기록한 뒤 관련 문서만 읽어 진행해. 완료 시 검증 결과와 남은 작업을 PROGRESS.md에 갱신해.

3. Antigravity는 공식 문서상 디렉터리의 `AGENTS.md`/`GEMINI.md`를 규칙으로 읽는다. 이 프로젝트는 **AGENTS.md 한 벌**을 유지한다. 같은 내용을 GEMINI.md에 복사해 두 군데를 수정하지 않는다. [Google 공식 Rules 문서](https://antigravity.google/docs/rules/) (확인 2026-10-24).
4. 설치 버전/개인 설정의 실제 동작까지 이번에 시험한 것은 아니다. 규칙을 못 읽으면 Antigravity에 AGENTS를 명시적으로 읽게 하고 적용된 규칙을 확인한다. 전역 규칙이나 하위 폴더 규칙이 이 프로젝트와 충돌하지 않는지도 확인한다.
5. 채팅 기록·도구 승인·세션 메모리는 공유되지 않는다. 중요한 결정을 파일에 기록하고, 긴 대화 전체를 매번 읽게 하지 않는다. 공통 문서는 Git으로 같이 전달한다.

## 동시 작업 충돌 줄이기

- **지금 상태:** 같은 폴더를 두 도구에 열 수는 있지만, 한쪽 구현 중 다른 쪽은 리뷰/조사만 하도록 나누는 것이 안전하다. branch 이름만 바꿔도 같은 checkout의 파일과 index는 공유된다.
- **실제 병렬 구현:** 현재 미커밋 변경을 검토·기준 커밋으로 보존한 다음 별도 worktree를 사용한다. 예시는 사용자가 분리를 결정했을 때 실행한다.

```powershell
Set-Location 'C:\Project_333'
git status --short
# 먼저 기준 커밋 완료 및 작업 경로/branch 이름 충돌 여부 확인
git worktree add -b codex/audio-followup 'C:\Project_333-codex' main
git worktree add -b antigravity/tutorial-followup 'C:\Project_333-antigravity' main
```

worktree는 **커밋된 상태만** 가져오며 지금 수정한 미커밋 파일이 자동 복제되지 않는다. 각 도구가 자신의 폴더를 열게 한다. Unity `Library/`·`Temp/`를 공유/심볼릭 링크하지 않고 별도 import한다. 서로 다른 기능으로 범위를 나누고 같은 씬/카드/마이그레이션 파일은 동시에 수정하지 않는다.

PROGRESS 예약표는 진짜 파일 잠금이 아니다. 별도 worktree의 문서도 독립 사본이므로 사용자가 역할을 배정하고 통합 담당자를 한 명 정한다. 통합 시 코드뿐 아니라 최신 규칙·PROGRESS를 함께 반영한다. 충돌 해결 후 Unity GUID/참조, cards.json, 서버 공용 코드와 관련 검증을 다시 확인한다.

## 2026-10-24 Git 점검

작업 시작 시 **tracked 변경142개 + 신규78개 = 220개**, staged0개, HEAD `99b4c50`. 전체 변경을 이 문서 작업의 산출물로 취급하지 않는다.

- 기존 변경은 클라이언트 UI/애니메이션·오디오, 단일 로그인 서버/DB/클라이언트, AI·카드 표시, 사용자 수정 아트, 누적 문서 갱신을 포함한다.
- 의도된 삭제6개는 기존 `After333_Lobby.wav`/meta(새 MP3로 교체), BlueDragon `DragonDeath.wav`·`DragonHurt.wav`/meta다. 삭제라는 이유만으로 복구하지 않는다. Lobby meta GUID 보존도 검토한다.
- 신규 Assets 파일의 `.meta` 누락 없음. tracked 목록에 Library/Temp/Builds/Backups/Server bin·obj 없음.
- 비밀 파일명 및 강한 개인키 표식 점검 결과 발견0. 이 검사는 모든 비밀번호·개인정보 또는 **Git 과거 이력**까지 보장하지 않는다. 커밋 diff에서 실제 설정값도 직접 검토한다.
- `.gitignore`를 보완해 `.env` 변형, 개인키/복구키, DB 덤프·백업, 개발자별 서버 env, 테스트 산출물을 제외한다. `.env.example`/`.env.sample`/`.env.template` 및 서버 `.csproj`, SQL 마이그레이션, Unity `.meta`, 공통 에이전트 문서는 유지한다. 기존 tracked 파일에는 ignore가 소급 적용되지 않는다.
- 변경된 `After333_Battle.wav` 약 **39.97MiB**. 기존 TMP SDF 자산도 약36~38MiB다. Git LFS는 팀원 설치·호스팅 용량/과금·빌드 checkout을 검토한 뒤 도입한다. 이력 재작성이나 대용량 자산 제외를 자동 실행하지 않는다.
- 전체 `git diff --check`에는 기존 Unity 씬 6개와 ElfElder Animator의 `m_Name: ` 같은 빈 값 뒤 공백 경고 581건이 있다. 이를 없애려고 씬/자산 전체를 정규식으로 재작성하지 않는다. 이번 문서 변경의 새로운 공백 문제는 별도로 검사한다.

## 커밋 순서 권고

먼저 원본 작업 폴더를 보존한다. `git diff` 파일만으로는 **untracked와 바이너리 원본이 완전히 백업되지 않는다**. 확인된 자산을 포함한 기존 백업/사본이 있는지 확인한다. 변경을 날리는 reset/clean/stash를 정리 수단으로 사용하지 않는다.

의존성을 보며 다음 단위로 나눈다. 변경 파일이 여러 그룹에 걸치면 통합 담당자가 hunk를 검토한다.

1. **단일 로그인 기능:** 서버 Auth/Battle/Program/0025 + 계정 HTTP/세션 가드/로그인 UI + 관련 서버/Unity 검사와 문서. migration만 빠뜨리거나 서버만 단독 커밋하지 않는다.
2. **전투·UI 수정:** 보상창/배너, 드래그·이동 교환, 턴 표시, 편집 가능 씬·5번째 멀리건, 관련 Editor/runtime/test/scene/meta. 수동 수정한 씬과 사용자 아트를 검토한다.
3. **음향:** 추가/교체/삭제 오디오+meta, 공통 effects volume, 효과 재생 call site, 테스트, 생성 도구/문서 변경. 배너 코드가 앞 그룹에도 포함되므로 순서/의존성을 확인한다.
4. **AI·카드 표시 및 사용자 아트:** AiPositionEvaluator, Daehwandan 표시, 회귀 검사, 원래 요청의 아트/Animator 변경을 목적에 따라 묶는다.
5. **공통 문서와 누적 문서 갱신:** 이번 AGENTS/PROGRESS/ignore와 소개·규칙·handoff. 문서 작업 이전 변경이 같은 파일에 있으면 `git add -p`로 검토하거나 함께 커밋할 의도를 확인한다.

각 묶음의 예시 절차(경로는 해당 묶음으로 지정):

```powershell
git status --short
git diff --stat
git diff -- AGENTS.md .gitignore
# 추적 중 텍스트의 일부 변경 선택
git add -p -- AGENTS.md .gitignore
# 검토한 신규 문서만 지정 (예시)
git add -- PROGRESS.md docs/agent_collaboration.md
git diff --cached --stat
git diff --cached --check
git diff --cached
# 사용자가 이 staged 묶음의 커밋을 결정한 다음
git commit -m "docs: add shared agent context and progress"
```

위 네 파일만 커밋하면 이후 기능·문서가 모두 전달되는 것은 아니다. 이번 진입 링크 수정 및 기존 untracked 문서/코드/자산도 각 해당 묶음에서 포함한다. `.gitignore`로 가려지지 않았다는 이유만으로 파일을 공개해도 된다고 판단하지 않는다. `git add .`·강제 push·이력 재작성은 하지 않는다.

Unity의 Visible Meta Files / Force Text는 이미 설정되어 있다. 씬 병합이 늘어나면 UnityYAMLMerge를 검토하되 설정만으로 논리적 충돌이 해결되지는 않는다. 두 에이전트가 같은 migration 번호를 만들지 않도록 `Persistence/Migrations/`는 통합 시 마지막 번호를 확인한다.
