# 전투 타일 클릭 누락 조사 — 2026-09-22

> 이 문서는 작성 당시의 보고서·검증 이력이다. 과거 카드/DB버전·미완료 항목·가동 상태를 오늘의 상태로 적용하지 않는다. 2026-10-20 현재 정리는 [프로젝트 상태](current_project_status.md), 운영은 [Lightsail 안내](lightsail_operations.md)를 따른다. 당시 측정값·성공/실패 기록은 보존한다.

## 결론

현재 입력 코드에서 실제 클릭 누락 결함을 재현했다. 소환물 카드 미리보기를 길게 눌러 연 뒤, 같은 타일의 다음 짧은 클릭이 무시된다. 길게 누른 채 타일 밖으로 나가 클릭 완료 이벤트가 발생하지 않으면 이후 두 번의 짧은 클릭이 무시된다.

사용자의 실제 PC 전투를 캡처한 것은 아니다. 확인된 재현은 현재 소스의 클릭 차단 상태 전이이며, 사용자가 경험한 모든 클릭 누락이 이 원인이라는 뜻은 아니다. 모바일 실기기는 검사하지 않았다.

## 원인

- TileLongPressRelay.Update는 미리보기를 열며 relay._suppressNextClick=true와 TileView.SuppressNextClick()을 모두 설정한다.
- 같은 누르기의 OnPointerClick에서 relay가 자체 플래그를 지우고 바로 return한다. TileView.NotifyClicked에 도달하지 않아 TileView의 플래그가 남는다.
- 다음 짧은 클릭은 relay를 통과해도 TileView가 남아 있던 플래그를 지우며 return하므로 Clicked 이벤트가 발생하지 않는다.
- OnPointerExit/OnDisable의 ResetState는 두 차단 플래그를 해제하지 않는다. 같은 누르기의 클릭 완료 이벤트까지 생략되면 두 개의 플래그가 각각 이후 클릭을 한 번씩 소비한다.
- TileView.Clicked가 오지 않으면 BattlePlayerInputController.HandleTileClicked가 실행되지 않는다. 아군 선택과 상대 공격 대상 클릭 모두 영향을 받는다.
- 타일별로 상태가 남으므로 다른 타일은 정상인데 특정 타일의 한두 번 클릭만 씹히는 양상과 부합한다.

## 재현 결과

수정하지 않은 실제 TileLongPressRelay.cs와 TileView.cs를 임시 .NET 프로젝트에서 직접 컴파일했다. Unity 타이머·포인터 타입·MonoBehaviour와 카드 미리보기 표시 경계만 테스트 대역으로 제공했다. 실제 Unity EventSystem/렌더링/PC 마우스 입력 실행 검증은 아니다.

| 입력 순서 | 이후 짧은 클릭 1/2/3회 후 실제 Clicked 누적 횟수 | 정상 기대 |
|---|---|---|
| 짧은 클릭만 | 1 / 2 / 3 | 1 / 2 / 3 |
| 미리보기 열기 → 같은 타일에서 놓기 | 0 / 1 / 2 | 1 / 2 / 3 |
| 미리보기 열기 → 밖으로 이동 → 놓기 | 0 / 0 / 1 | 1 / 2 / 3 |
| 미리보기 열기 → relay 비활성화 → 다음 입력 | 0 / 0 / 1 | 1 / 2 / 3 |

미리보기 임계 시간은 현재 전투 씬과 기본 코드 모두 0.4초다. 재현 자료: TempBuild/TileClickInvestigation_20260922/{Program.cs,BoundaryStubs.cs,Probe.csproj,probe.log}.

## 9월 13일 이력

- 사용자가 지정한 명령은 2026-09-13 19:08:57 KST이며, 19:12:16에 끝났다.
- 당시 작업은 최종 검증, 서버 게시/19:10 교체, 백업과 API·데이터 보존 검사, 문서 정리다. 해당 이어하기에서 타일·포인터 입력 수정은 발견되지 않았다.
- 직전 클라이언트 작업은 일반 매칭의 AI 전환, 골드 던전, 공통 재접속과 도전 결과 처리다.
- 이번 결함의 두 파일은 현재 HEAD 대비 수정이 없으며, 2026-06-08 커밋 186041c에도 같은 이중 차단 코드가 존재한다. 9월 13일 토큰 중단이나 이어하기가 이 결함을 새로 만들었다는 근거는 없다.

## 별도 조건과 조사 범위

연출 재생 중 IsBusy이면 기존 입력 컨트롤러가 클릭을 거부하고 'Animations are still resolving.' 상태를 설정한다. 이는 이번 플래그 잔류와 별도이며, 이번 조사에서 연출 잠금이 영구히 남는 현상을 재현한 것은 아니다.

아직 운영 코드 수정, 앱 빌드·설치, 서버 재시작은 하지 않았다. 수정 시에는 클릭 차단을 현재 누르기 동작 한 번에만 적용하고, 취소·포인터 이탈·비활성화 후 다음 새 클릭까지 남지 않도록 해야 한다. 짧은 클릭·미리보기 해제·이탈·재진입·아군 선택/상대 공격 흐름을 함께 확인해야 한다.


명칭 표기 갱신(2026-10-01): 이 조사에서 언급한 일일 AI 대전 모드는 현재 골드 던전으로 표기한다. 조사 당시의 현상·검증 결과를 다시 확인한 것은 아니다.

## Fix applied (2026-09-22)

The user authorized fixing this confirmed path and will report any further symptoms.

- Removed duplicate click suppression from TileView. TileLongPressRelay alone suppresses the release click belonging to a preview gesture.
- Every new pointer-down clears the preceding gesture's suppression, including when the tile has become empty or cannot show artwork.
- Pointer-up/exit/disable still closes the preview. Suppression survives gesture cleanup until its click or the next pointer-down, so exiting and returning during the same hold cannot turn preview release into an attack.
- Added six regression cases to TileCardPreviewTests: ordinary clicks, hold/release, hold/exit without click, hold/exit/release click, disable/re-enable, and an emptied tile.
- Executed the actual two production source files with stubbed Unity/preview boundaries: all six scenarios passed, each subsequent short press delivering exactly one event and no preview release action. Evidence: TempBuild/TileClickFix_20260922/probe.log.
- Unity runtime and EditMode test source compilation passed. The added Unity Canvas tests were compiled, not executed in Unity Test Runner. Live PC/mobile input remains unverified.
- Client rebuild is required to apply this input fix. No client app build/install or server restart/deployment was performed.
