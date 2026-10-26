# Play 전 현재 씬 UI 편집

2026-10-23: 덱 빌딩·드래프트·게임 시작·보유카드·전투 씬은 실행 중에만 생성하던 UI를 편집 모드의 실제 씬 오브젝트로 준비한다. 상점의 별도 버전3 편집 지원은 그대로 유지한다.

## 사용 방법

1. Unity의 스크립트 컴파일이 끝날 때까지 기다린다.
2. 해당 씬을 연다. 최초 스크립트 리로드, 대상 씬 열기, Play 종료 시 현재 UI가 한 번 준비된다.
3. 아직 이전 화면이면 **Tools > Project333 > Scenes > Update Current Editable Layouts**를 실행한다.
4. Hierarchy에서 실제 버튼을 선택해 `ButtonTouchSound` 등 컴포넌트를 추가한다. 이미 열려 있던 씬은 자동으로 저장하지 않으므로 **Ctrl+S**로 저장한다.

도구가 자동으로 열었던 닫힌 씬만 저장하고, 원래 활성 씬으로 돌아간다. 사용자가 이미 열어 둔 씬은 수정 여부와 관계없이 자동 저장하지 않는다. 사용자가 연 씬을 닫거나 Single 모드로 바꾸지 않는다. 이미 준비한 버전은 반복 생성하지 않는다. 기존 버튼·이미지와 추가 컴포넌트를 통째로 다시 만들지 않는다. 컨트롤러가 관리하는 배치·색상·폰트는 해당 Inspector 설정을 함께 확인한다.

## 대상과 표시 범위

| 씬 | Play 전에 준비하는 UI |
| --- | --- |
| `Assets/DeckBuilding_VSlice.unity` | 카드 선택 3슬롯, ATK/HP 텍스트 틀, 덱 목록·스크롤·뒤로 버튼, 지갑·설정 |
| `Assets/Draft_VSlice.unity` | 덱 목록, 골드 던전·전투 버튼, 보상·매칭 안내 UI, 지갑·설정 |
| `Assets/GameStart_VSlice.unity` | 현재 시작 화면, 튜토리얼·선물함·지갑·설정, 로그인 UI 구조 |
| `Assets/OwnedCards_VSlice.unity` | 6×2 카드 슬롯, 페이지 버튼, 카드 상세·뒤집는 강화표 구조, 지갑·설정 |
| `Assets/Battle_VSlice.unity` | 현재 배경·양쪽 전장 타일, 자원 아이콘, 덱·상대 손패·내 손패의 UI 틀, END·설정 버튼, 턴/오류/매칭/합체 안내 UI |

편집 모드에서는 로그인하지 않고 서버·광고 SDK·로컬 전투도 시작하지 않는다. 실제 덱·제시 카드·보유 수량·계정 골드·전투 손패·소환물은 Play에서 기존 흐름대로 채운다. 지갑은 `-`, 전투 자원은 편집용 0, 턴은 `Turn — / —`로 표시한다. 빈 카드 슬롯은 편집할 위치를 보여주는 틀이며 실제 보유 카드가 아니다.

로그인·상세창·턴 배너·오류창처럼 상황에 따라 나타나는 화면은 기본적으로 숨긴 상태다. 편집 중 구조를 보려면 해당 오브젝트의 활성 상태 및 CanvasGroup을 확인하되, 확인 후 기본 상태로 돌리고 저장한다. 실제 런/로그인/전투 상태에 따른 표시는 Play에서 결정한다. 전투의 타일·손패용 예전 디버그 문구는 편집 화면에서 비운다. 소환 애니메이션과 승패 연출을 자동 재생하는 기능은 아니다.

## 구현과 확인

- 공통 준비 코드: `Assets/Project333/Editor/Project333EditableScenesBuilder.cs` (현재 편집 UI 버전3).
- 기존 컨트롤러의 presentation-only 편집 메서드를 재사용한다. 계정·광고 컨트롤러에 ExecuteAlways를 붙이거나 Awake/OnEnable을 강제로 실행하지 않는다.
- 전투는 BattleScreenPresenter·HandPresenter·BattleBootstrapper·BattlePlayerInputController의 기존 편집 메서드와 자원/보드 레이아웃 메서드를 사용한다. 실제 BattleState와 gateway는 만들지 않는다.
- 격리 Unity 6000.3.12f1에서 저장 후 다시 연 UI, 중복 생성 방지, 미저장 씬/사용자 추가 이미지·효과음 컴포넌트 보존, 생성 Canvas의 씬 소속, 전투 미시작, 기존 상세창·덱 목록·시작 UI 검사를 수행했다. 실제 앱 빌드·설치와 휴대폰 검증은 별도다.

## 덱·상대 손패의 카드 뒷면 편집 (2026-10-25)

- Play 종료 상태에서 `PlayerDeckCardBack_00~02`, `OpponentDeckCardBack_00~02`의 **Image > Source Image**와 **RectTransform**을 바꾸고 Ctrl+S로 씬을 저장한다. 이제 기존 각 카드의 이미지·위치·앵커·피벗·크기·회전·배율과 `PlayerDeckRoot`/`OpponentDeckRoot` 배치를 Play 시작·활성화·카드 수 갱신 때 기본값으로 덮어쓰지 않는다. 생성용 Card Size/Padding/Stack Offset 값은 새로 만드는 덱 오브젝트의 기본값이며 이미 배치한 덱은 실제 RectTransform에서 편집한다.
- 상대 손패는 `OpponentCardBack_00`부터 있는 슬롯들의 **Image > Source Image**를 바꾼다. 여러 슬롯을 함께 선택해 같은 그림으로 바꿀 수 있다. 현재 숨겨진 슬롯도 포함한다. 손패 수에 따른 부채꼴 위치/회전/크기는 기존 OpponentHandPresenter가 계속 계산한다.
- 이미지가 비어 있는 슬롯만 Presenter의 Card Back Sprite/Texture/Resource 기본값으로 채운다. 이미 지정된 슬롯은 이 기본값을 변경해도 덮어쓰지 않는다. 덱 수에 따른 층 표시·남은 카드 안내는 유지하며, 덱 클릭 영역은 옮긴 카드들의 실제 영역을 감싼다. 드로우/멀리건 시작 위치와 뒷면도 실제 덱 카드에서 읽는다.
- 재현 검사에서 수정 전 내 덱·상대 덱·상대 손패3/3 실패 → 수정 후 관련 격리 Unity **41/41 통과**. 저장/재열기/실제 Play 진입/재활성화·숨겨진 슬롯·덱 수 변경·자동 손패 정렬·드로우 출발점·멀리건 회귀를 확인했다. 사용자 원본 씬을 재생성하거나 저장하지 않았으며 앱 빌드·실기기 확인은 별도다. 서버 재배포 불필요.
