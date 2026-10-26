# 상대방 카드 사용 미리보기

점검·수정: 2026-10-24. 상대방이 사용한 카드를 화면 중앙에 자동 공개하는 연출이다. 길게 눌러 여는 카드 미리보기와 표시 목적·입력 경로가 다르지만 카드 이미지의 공개 누락을 함께 점검했다.

## 현재 동작

- 서버의 공개 카드 사용 이벤트를 자신의 좌석 기준으로 투영한 뒤 `BattleBootstrapper.QueueOpponentPlayedCardReveals()`가 FIFO로 처리한다. `CardPlayed`, `SpellCast`, `RobotFusionResolved`의 상대 카드가 대상이다.
- 유닛/건물은 서버가 보낸 강화 반영 공격력·체력, 지원되는 마법은 효과 수치를 표시한다. 카드 그림이 없으면 기존 CardId 텍스트 대체 표시를 사용한다. 현재 JSON-only `RobotFusion`은 별도 그림 자산이 없다.
- 설치된 트랩의 실제 정체는 공개하지 않는다. 상대 설치는 물음표/트랩만, 발동은 실제 카드를 양측에 공개한다. 흡수된 로봇마다 사용 카드를 중복 공개하지 않는다.
- 기본3초·다음 카드가 대기하면2초, 앞뒤0.15초 fade, 배경 dim 없음. 입력을 가로막거나 서버 명령 처리를 지연시키지 않는다.
- 전투가 끝났으면 이미 접수된 미리보기와 공격·피격·사망 연출이 완료된 뒤 승패 배너를 띄운다. 드래프트 복귀용 최소3.2초 대기도 그 뒤 시작한다. **재접속/연결 초기화에서는 과거 미리보기 기록을 비우는 기존 정책을 유지**한다.

## 확인된 원인과 수정

1. **로봇 합체의 공개 필터 누락.** 서버는 여러 타일 대상 합체에서 `SpellCast` 대신 `RobotFusionResolved`를 보낸다. 기존 필터가 이를 제외했다. 전용 공개 이벤트를 포함하고 자신의 사용·빈 정체·흡수 이벤트 제외는 유지했다.
2. **종료 배너·씬 복귀가 공개 대기열보다 먼저 진행.** 미리보기는 의도적으로 `IsBusy`에 들어가지 않는데, 종료 배너와 복귀 대기는 `IsBusy`만 검사했다. 연속 사용 뒤 환술 등이 아직 대기할 때 배너가 중앙을 덮거나 씬이 먼저 바뀔 수 있었다. 별도 `IsOpponentPlayedCardRevealPending`을 추가해 결과 표시와 복귀만 기다리게 했다. 행동 입력 잠금에는 추가하지 않았다.

`HuanShu` 단독 사용의 서버 코드에는 `SpellCast`와 CardId가 정상 생성된다. 격리 Unity의 양측 좌석·PvP/서버AI 설정·즉시 끝나는 메시지 다음 환술 사용은 통과했다. 종료와 무관한 실제 휴대폰에서의 환술 누락은 이번에 직접 재현하지 못했으므로 이 두 원인이 사용자가 본 모든 사례라고 단정하지 않는다.

## 변경 위치

- `Assets/Project333/Runtime/Presentation/Battle/OpponentPlayedCardRevealRules.cs`: 합체 이벤트 포함.
- `Assets/Project333/Runtime/Presentation/Battle/BattleBootstrapper.cs`: 별도 대기 상태와 종료 복귀 순서.
- `Assets/Project333/Runtime/Presentation/Battle/BattleOutcomeTextView.cs`: 공개 대기 중 결과 배너 표시 보류.
- `Assets/Project333/Tests/EditMode/OpponentCardRevealQueueTests.cs`: 실제 Unity 코루틴·좌석 투영·FIFO·종료/초기화 회귀.

`OnlineBattleConnectionTester.cs`, 서버 이벤트 생성·판정, 카드 데이터, 계정·DB·승패 저장, 씬/자산 설정은 수정하지 않았다. 새 클라이언트 빌드·설치로 반영한다.

## 검증 범위

수정 전 추가한5개 검사 중4개가 두 원인으로 실패했다(합체 filter/FIFO와 배너/복귀 대기). 수정 후 격리 Unity6000.3.12f1에서 **67/67** 통과: 새9개, 기존 공개 필터·연결·AI pacing·트랩 정체 필터/좌석 투영·승패 표시/음향 회귀 포함. 빠른6회 연속 사용의 전부 표시, 종료 직전 대기 중 환술, 입력 비차단, 새 연결의 과거 공개 제거와 다음 카드 정상 표시를 확인했다.

최초 검증 실행은 sandbox 환경의 Unity 라이선스 접근 실패198로 검사를 시작하지 못했다. 정상 승인된 실행 환경에서 재시도했다. 첫 수정 후 검사1개는 로봇 합체 그림이 있다는 테스트의 잘못된 가정으로 실패했으며 실제 기존 텍스트 대체 표시를 검증하도록 고쳤다. 게임 자산을 새로 만들지 않았다.

증거: 세션 시각화 `OpponentReveal_20261024/{BaselineExpanded.xml,Fixed.xml,Final.xml,Final.log,verification.json,before}`. 전체 Runtime은 SDK 없는 검증 사본에서 컴파일했으며 native LevelPlay assembly는 기존처럼 제외했다. 실제 휴대폰·인증 제공자·운영 PvP/AI 접속·패킷 전송·앱 빌드·배포·DB 작업은 수행하지 않았다. 서버 이벤트와 같은 DTO를 실제 클라이언트 코루틴에 전달한 검증이다.

## 재발 시 구분할 정보

새 클라이언트에서 재발하면 PvP/AI·카드명·대략적인 턴/시각·직후 전투 종료 또는 재접속 여부·실제 적용 BuildId를 확인한다. 중앙 이미지가 늦게 나오는 대기 상태, 이미지가 다른 창에 가려짐, 사용 이벤트 자체가 오지 않음, 재접속으로 기록이 지워짐을 구별한다. 토큰/비밀번호가 포함된 원본 로그를 공유하지 않는다.
