# 소환물 스탯창 크기와 공격 잔여 표시

## 스탯창·글자 크기 편집

1. **Play 모드를 종료**하고 `Assets/Battle_VSlice.unity`를 연다.
2. Hierarchy 검색창에 **`t:TileTextView`**를 입력한다. 양쪽 전장 타일의 `TileTextView` 컴포넌트를 찾는 검색이다.
3. 검색 결과의 타일 **20개를 모두 선택**한다. 내 전장 10개와 상대 전장 10개가 포함됐는지 확인한다. 개별 타일만 바꾸고 싶으면 그 타일 하나만 선택한다.
4. Inspector의 **Tile Text View > Occupant Stats**를 펼쳐 아래 값을 수정한다.
5. **Ctrl+S**로 씬을 저장한 뒤 Play에서 실제 타일·소환물 크기와 함께 확인한다. Play 중 수정한 값은 종료하면 되돌아가므로 최종 값은 Play를 종료한 상태에서 적용한다.

| Inspector 항목 | 코드 필드 | 기능 | 코드 초기값 → 시험할 값 |
|---|---|---|---|
| Occupant Stat Bar Size | `_occupantStatBarSize` | 스탯창 배경 너비 X·높이 Y | `112×26` → **`150×34`** |
| Occupant Stat Font Size | `_occupantStatFontSize` | 체력·공격력 숫자의 글자 크기 | `18` → **`24`** |
| Occupant Stat Icon Size | `_occupantStatIconSize` | 하트·공격 방식 아이콘 크기 | `16×16` → **`20×20`** |
| Occupant Stat Bar Offset | `_occupantStatBarOffset` | 스탯창 위치. X는 좌우, Y는 위아래 | 현재 `0,-64`; 필요할 때만 조절 |

위 표는 코드 초기값을 기준으로 한 예시이고, 실제 씬에 사용자가 저장한 값이 우선한다. 위 시험값을 자동으로 씬에 적용하지 않았다. 사용자가 원하는 크기로 조절한다. 특히 숫자가 세 자리 이상이면 너비도 함께 늘린다. Y 위치에는 기존 소환물 배율·반응형 타일 크기도 반영된다.

스탯창 안의 `HpText`, `AttackText` 또는 `OccupantStatBar`의 RectTransform/Font Size만 직접 수정하면 `TileTextView`가 표시를 갱신하면서 다시 계산한다. **TileTextView의 위 설정을 수정**해야 유지된다. 코드의 필드 초기값을 바꾸는 것도 기존 씬의 직렬화된 값을 덮어쓰지 않는다.

`ApplyStatBarLayout()`은 체력 아이콘·숫자와 ATK 아이콘·숫자를 한 줄 묶음으로 보고, 초록 점 영역을 제외한 공간의 가운데에 정렬한다. 숫자도 각 텍스트 영역 안에서 가로·세로 중앙 정렬이다. 실제 글자 폭을 사용하므로 자릿수가 바뀌어도 묶음 전체가 중앙에 온다. 폭이 부족할 때는 묶음만 같은 비율로 줄여 초록 점과 겹치지 않게 한다. 원하는 글자 크기를 충분히 확보하려면 스탯창 너비도 함께 늘린다. 초록 점의 위치·크기·색·표시 조건은 유지하며 점의 점등 여부 때문에 숫자가 옆으로 움직이지 않는다.

## 공격 횟수가 남았을 때 초록 점 (2026-10-28)

- `OccupantStatBar/AttackReadyIndicator`에 작은 초록색 원을 생성한다. 기존 픽셀 아이콘 생성 함수를 이용해 한 번 만든 원형 Sprite를 모든 타일에서 공유한다. 별도 아트 파일·폰트 글리프가 필요하지 않다.
- **내 메인 턴**, **내 소환물**, **생존**, **남은 공격 횟수 > 0**, **공격력 > 0**, **소환 턴 공격 제한 없음**, **방전·봉인·스턴·빙결 없음**일 때만 표시한다. 마스터와 공격 가능한 건물도 같은 규칙을 따른다. 수동 공격 불가 건물에는 표시하지 않는다.
- 여러 번 공격할 수 있으면 마지막 횟수를 소모하기 전까지 유지한다. 상대 턴/상대 소환물/멀리건/턴 시작·종료 처리/전투 종료/빈 타일에는 표시하지 않는다. 서버가 확정한 공격 소모·턴 상태를 받아 갱신한다.
- 남은 공격 기회를 나타내는 표시다. 특정 대상의 전열 보호·은신, 마나 대포 등 별도 공격 비용 지불 가능 여부까지 보증하는 표시는 아니다. 실제 명령은 기존 공유/서버 공격 판정이 검증한다.
- 온라인은 `BattleStateViewProjector`의 로컬 좌석 투영 결과를 그대로 받으므로 서버 Player/AI 중 어느 좌석으로 접속해도 내 편에만 표시한다. 튜토리얼·로컬 전투도 같은 BoardPresenter 경로를 사용한다.
- 점은 스탯창 Canvas(`sortingOrder=4950`) 안에 있어 소환물 애니메이션보다 위에 표시되고, `raycastTarget=false`로 타일 클릭·드래그·꾹 누르기를 가로채지 않는다. 기존 씬 배치·폰트·이미지는 유지한다.

같은 TileTextView의 **Attack Ready Indicator**에서 편집한다.

| 항목 | 기본값 | 기능 |
|---|---|---|
| Show Attack Ready Indicator | On | 초록 점 표시 기능 사용 |
| Attack Ready Indicator Size | 8 | 점의 너비·높이. 예: `10`으로 확대 |
| Attack Ready Indicator Color | 밝은 초록 | 점 색상·불투명도 |

크기는 스탯창 높이 안에 들어가도록 제한한다. 점 위치는 `ApplyStatBarLayout()`의 오른쪽 안쪽 3px 여백을 기준으로 한다. 글자와 겹치면 스탯창 너비를 늘린다. 새 설정은 씬 필드를 따로 연결하지 않아도 기본 On으로 동작한다.

## 구현 위치·검증

- `Assets/Project333/Runtime/Presentation/Battle/TileTextView.cs`: 스탯창 크기·글자·아이콘·점 배치와 이미지 생성.
- `Assets/Project333/Runtime/Presentation/Battle/BoardPresenter.cs`: 전장과 동일한 전투 스냅샷을 타일 뷰에 제공.
- `Assets/Project333/Runtime/Presentation/Battle/OccupantAttackReadiness.cs`: UI의 표시 조건. 공격 규칙 자체를 변경하지 않는다.
- `Assets/Project333/Tests/EditMode/OccupantAttackReadinessTests.cs`: 실제 AttackService/TurnStartService·양쪽 온라인 좌석·상태 이상 검사.
- `Assets/Project333/Tests/EditMode/OccupantAttackReadyIndicatorTests.cs`: 실제 Canvas에서 표시·소모·턴·소환물 교체·빈 전장·원형 투명도·입력 비차단·크기/폰트/정렬 확인.

검증 결과는 작업 완료 시 [PROGRESS](../PROGRESS.md) 및 [최근 인계](antigravity_handoff.md)에 기록한다. 소스 완료와 운영 반영은 별개다. 이 표시 변경은 클라이언트 재빌드가 필요하며 서버 재배포·DB 마이그레이션은 필요하지 않다.


### 완료 검증 (2026-10-28)

- 실제 분리 Unity6000.3.12f1 D3D11 EditMode 실행 **32/32 통과**: 조건17, Play 진입 Canvas UI2, 기존 이동·교환5, 능력치 증가 연출8. 실패/스킵0.
- 실제 공유 공격/턴 처리·온라인 좌석 투영 별도 검사17/17. 설치된 Unity 참조 DLL을 통한 런타임 별도 컴파일 오류0/경고88(직렬화 필드 미할당 등). Unity 검증 사본에서만 누락된 LevelPlay SDK assembly를 제외했고 활성 프로젝트 광고 기능은 변경하지 않았다.
- 기존 Battle 씬 해시와 배치 유지, 검증 사본과 실제 변경 소스·테스트 해시 일치. 앱 빌드·기기 확인·서버 배포·DB 접근은 실행하지 않았다.


### 중앙 정렬 후속 검증 (2026-10-28)

체력·ATK 문양과 숫자 묶음의 중앙 정렬, HP 자릿수 변화 시 재정렬·초록 점 고정·좁은 영역 내 배치·방전 색 복구를 기존 UI 검사에 보완했다. 최종 실제 분리 Unity **15/15 통과**(Play/Canvas2·이동/교환5·능력치 증가 연출8). 사용자 씬 크기·폰트 편집을 보존했으며 초록 점 배치 코드도 동일하다. 서버/DB 변경 없음, 앱 재빌드 필요.


## 검·활·지팡이 도안의 행 수 (2026-10-28)

`TileTextView.cs`의 `GetSwordIconSprite`, `GetBowIconSprite`, `GetMagicStaffIconSprite` 안 `new[]` 배열을 각각 **10줄, 각 줄9글자**로 변경했다(9×10픽셀). 검은 검신, 활은 양쪽 곡선, 지팡이는 손잡이를 연장해 기존 모양을 유지했다. `X`는 표시되는 픽셀, `.`은 투명 픽셀이다.

`CreateIconSprite`가 `height = rows.Count`로 자동 계산하므로 함수나 반복문의 숫자를 바꿀 필요가 없다. 행을 더 추가해도 같은 원리이며 모든 줄의 글자 수는 같아야 한다. 도안의 픽셀 수와 화면의 크기는 별개다. 실제 표시 크기는 기존 `Occupant Stat Icon Size`로 조절한다. static Sprite 캐시가 있으므로 변경 후 Play를 다시 시작하고, 도메인 리로드를 끈 경우 Editor를 재시작해 캐시를 갱신한다. 이번 변경은 도안만이며 색·스탯창/초록 점 배치·씬 저장값은 수정하지 않았다. 도안 행/열·diff 정적 확인, Unity 실행 검사는 재수행하지 않았다.
